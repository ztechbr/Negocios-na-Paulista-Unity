using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace PaulistaBusinessWorld
{
    [Serializable]
    public class KnowledgeRow
    {
        public long Id;
        public string Question, Answer, Intent, Sector, Company, Source, Created;
        public float Score;
        public int Uses;
        [NonSerialized] public float[] Vec;
    }

    [Serializable]
    public class InteractionRow
    {
        public long Id, KnowledgeId;
        public string Time, Npc, Company, PlayerText, Reply, Source;
        public int Feedback;
    }

    [Serializable]
    public class DealRow
    {
        public long Id;
        public string Time, Npc, Company, Product, Unit, Status;
        public float ListPrice, FinalPrice, Total;
        public int Quantity, TermMonths, Rounds;
    }

    // Persistência do banco de negócios e do modelo local.
    public interface IBusinessStore
    {
        string Description { get; }
        string GetMeta(string key);
        void SetMeta(string key, string value);
        List<KnowledgeRow> LoadKnowledge();
        long AddKnowledge(KnowledgeRow row);
        void UpdateKnowledge(KnowledgeRow row);
        void DeleteKnowledgeBySource(string source);
        void RunInTransaction(Action action);
        long AddInteraction(InteractionRow row);
        void SetInteractionFeedback(long id, int feedback);
        (int total, int positive, int negative) InteractionStats();
        long AddDeal(DealRow row);
        List<DealRow> LoadDeals();
        void Flush();
    }

    public static class Vectors
    {
        public static byte[] ToBytes(float[] v)
        {
            var bytes = new byte[v.Length * 4];
            Buffer.BlockCopy(v, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static float[] FromBytes(byte[] b)
        {
            if (b == null || b.Length == 0) return null;
            var v = new float[b.Length / 4];
            Buffer.BlockCopy(b, 0, v, 0, b.Length);
            return v;
        }
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // SQLite nativo do Windows 10/11 (winsqlite3.dll, já incluso no sistema). Vetores ficam em colunas BLOB.
    public sealed class SqliteStore : IBusinessStore, IDisposable
    {
        const string Lib = "winsqlite3";
        const int SQLITE_OK = 0, SQLITE_ROW = 100, SQLITE_DONE = 101;
        static readonly IntPtr Transient = new IntPtr(-1);

        [DllImport(Lib)] static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Lib)] static extern int sqlite3_close(IntPtr db);
        [DllImport(Lib)] static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int nByte, out IntPtr stmt, IntPtr tail);
        [DllImport(Lib)] static extern int sqlite3_step(IntPtr stmt);
        [DllImport(Lib)] static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport(Lib)] static extern int sqlite3_bind_text(IntPtr stmt, int idx, byte[] value, int n, IntPtr destructor);
        [DllImport(Lib)] static extern int sqlite3_bind_blob(IntPtr stmt, int idx, byte[] value, int n, IntPtr destructor);
        [DllImport(Lib)] static extern int sqlite3_bind_int64(IntPtr stmt, int idx, long value);
        [DllImport(Lib)] static extern int sqlite3_bind_double(IntPtr stmt, int idx, double value);
        [DllImport(Lib)] static extern int sqlite3_bind_null(IntPtr stmt, int idx);
        [DllImport(Lib)] static extern long sqlite3_column_int64(IntPtr stmt, int col);
        [DllImport(Lib)] static extern double sqlite3_column_double(IntPtr stmt, int col);
        [DllImport(Lib)] static extern IntPtr sqlite3_column_text(IntPtr stmt, int col);
        [DllImport(Lib)] static extern IntPtr sqlite3_column_blob(IntPtr stmt, int col);
        [DllImport(Lib)] static extern int sqlite3_column_bytes(IntPtr stmt, int col);
        [DllImport(Lib)] static extern long sqlite3_last_insert_rowid(IntPtr db);
        [DllImport(Lib)] static extern IntPtr sqlite3_errmsg(IntPtr db);

        IntPtr db;
        public string Path { get; }
        public string Description => $"SQLite ({Path})";

        public static SqliteStore TryOpen(string path)
        {
            try { return new SqliteStore(path); }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is InvalidOperationException)
            {
                Debug.LogWarning("SQLite indisponível, usando armazenamento JSON: " + e.Message);
                return null;
            }
        }

        SqliteStore(string path)
        {
            Path = path;
            if (sqlite3_open_v2(Utf8(path), out db, 0x2 | 0x4, IntPtr.Zero) != SQLITE_OK)
                throw new InvalidOperationException("Falha ao abrir " + path);
            Exec("PRAGMA journal_mode=WAL");
            Exec(@"CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT)");
            Exec(@"CREATE TABLE IF NOT EXISTS knowledge(
                id INTEGER PRIMARY KEY AUTOINCREMENT, question TEXT NOT NULL, answer TEXT, intent TEXT, sector TEXT,
                company TEXT, source TEXT NOT NULL, score REAL DEFAULT 0, uses INTEGER DEFAULT 0, created TEXT, vec BLOB)");
            Exec("CREATE INDEX IF NOT EXISTS ix_knowledge_company ON knowledge(company, source)");
            Exec(@"CREATE TABLE IF NOT EXISTS interactions(
                id INTEGER PRIMARY KEY AUTOINCREMENT, time TEXT, npc TEXT, company TEXT, player_text TEXT, reply TEXT,
                source TEXT, knowledge_id INTEGER, feedback INTEGER DEFAULT 0)");
            Exec(@"CREATE TABLE IF NOT EXISTS deals(
                id INTEGER PRIMARY KEY AUTOINCREMENT, time TEXT, npc TEXT, company TEXT, product TEXT, unit TEXT, status TEXT,
                list_price REAL, final_price REAL, total REAL, quantity INTEGER, term_months INTEGER, rounds INTEGER)");
        }

        static byte[] Utf8(string s) => Encoding.UTF8.GetBytes((s ?? "") + "\0");

        string Error() => Marshal.PtrToStringUTF8(sqlite3_errmsg(db));

        IntPtr Prepare(string sql, object[] args)
        {
            if (sqlite3_prepare_v2(db, Utf8(sql), -1, out var stmt, IntPtr.Zero) != SQLITE_OK)
                throw new InvalidOperationException(Error() + " — " + sql);
            for (int i = 0; i < args.Length; i++)
            {
                int idx = i + 1;
                switch (args[i])
                {
                    case null: sqlite3_bind_null(stmt, idx); break;
                    case string s: { var b = Encoding.UTF8.GetBytes(s); sqlite3_bind_text(stmt, idx, b, b.Length, Transient); break; }
                    case byte[] blob: sqlite3_bind_blob(stmt, idx, blob, blob.Length, Transient); break;
                    case int n: sqlite3_bind_int64(stmt, idx, n); break;
                    case long l: sqlite3_bind_int64(stmt, idx, l); break;
                    case float f: sqlite3_bind_double(stmt, idx, f); break;
                    case double d: sqlite3_bind_double(stmt, idx, d); break;
                    default: throw new ArgumentException("Tipo não suportado: " + args[i].GetType());
                }
            }
            return stmt;
        }

        void Exec(string sql, params object[] args)
        {
            var stmt = Prepare(sql, args);
            try
            {
                int rc = sqlite3_step(stmt);
                if (rc != SQLITE_DONE && rc != SQLITE_ROW) throw new InvalidOperationException(Error() + " — " + sql);
            }
            finally { sqlite3_finalize(stmt); }
        }

        void Query(string sql, Action<IntPtr> row, params object[] args)
        {
            var stmt = Prepare(sql, args);
            try { while (sqlite3_step(stmt) == SQLITE_ROW) row(stmt); }
            finally { sqlite3_finalize(stmt); }
        }

        static string Text(IntPtr stmt, int col)
        {
            var p = sqlite3_column_text(stmt, col);
            return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
        }

        static byte[] Blob(IntPtr stmt, int col)
        {
            int n = sqlite3_column_bytes(stmt, col);
            var p = sqlite3_column_blob(stmt, col);
            if (p == IntPtr.Zero || n == 0) return null;
            var b = new byte[n];
            Marshal.Copy(p, b, 0, n);
            return b;
        }

        public string GetMeta(string key)
        {
            string value = null;
            Query("SELECT value FROM meta WHERE key = ?", s => value = Text(s, 0), key);
            return value;
        }

        public void SetMeta(string key, string value) =>
            Exec("INSERT INTO meta(key, value) VALUES(?, ?) ON CONFLICT(key) DO UPDATE SET value = excluded.value", key, value);

        public List<KnowledgeRow> LoadKnowledge()
        {
            var list = new List<KnowledgeRow>();
            Query("SELECT id, question, answer, intent, sector, company, source, score, uses, created, vec FROM knowledge", s => list.Add(new KnowledgeRow
            {
                Id = sqlite3_column_int64(s, 0), Question = Text(s, 1), Answer = Text(s, 2), Intent = Text(s, 3),
                Sector = Text(s, 4), Company = Text(s, 5), Source = Text(s, 6), Score = (float)sqlite3_column_double(s, 7),
                Uses = (int)sqlite3_column_int64(s, 8), Created = Text(s, 9), Vec = Vectors.FromBytes(Blob(s, 10)),
            }));
            return list;
        }

        public long AddKnowledge(KnowledgeRow r)
        {
            Exec("INSERT INTO knowledge(question, answer, intent, sector, company, source, score, uses, created, vec) VALUES(?,?,?,?,?,?,?,?,?,?)",
                r.Question, r.Answer, r.Intent, r.Sector, r.Company, r.Source, r.Score, r.Uses, r.Created, r.Vec != null ? Vectors.ToBytes(r.Vec) : null);
            return r.Id = sqlite3_last_insert_rowid(db);
        }

        public void UpdateKnowledge(KnowledgeRow r) =>
            Exec("UPDATE knowledge SET question=?, answer=?, intent=?, source=?, score=?, uses=?, vec=? WHERE id=?",
                r.Question, r.Answer, r.Intent, r.Source, r.Score, r.Uses, r.Vec != null ? Vectors.ToBytes(r.Vec) : null, r.Id);

        public void DeleteKnowledgeBySource(string source) => Exec("DELETE FROM knowledge WHERE source = ?", source);

        public void RunInTransaction(Action action)
        {
            Exec("BEGIN");
            try { action(); Exec("COMMIT"); }
            catch { Exec("ROLLBACK"); throw; }
        }

        public long AddInteraction(InteractionRow r)
        {
            Exec("INSERT INTO interactions(time, npc, company, player_text, reply, source, knowledge_id, feedback) VALUES(?,?,?,?,?,?,?,?)",
                r.Time, r.Npc, r.Company, r.PlayerText, r.Reply, r.Source, r.KnowledgeId, r.Feedback);
            return r.Id = sqlite3_last_insert_rowid(db);
        }

        public void SetInteractionFeedback(long id, int feedback) => Exec("UPDATE interactions SET feedback = ? WHERE id = ?", feedback, id);

        public (int total, int positive, int negative) InteractionStats()
        {
            (int, int, int) result = (0, 0, 0);
            Query("SELECT COUNT(*), SUM(feedback > 0), SUM(feedback < 0) FROM interactions",
                s => result = ((int)sqlite3_column_int64(s, 0), (int)sqlite3_column_int64(s, 1), (int)sqlite3_column_int64(s, 2)));
            return result;
        }

        public long AddDeal(DealRow r)
        {
            Exec("INSERT INTO deals(time, npc, company, product, unit, status, list_price, final_price, total, quantity, term_months, rounds) VALUES(?,?,?,?,?,?,?,?,?,?,?,?)",
                r.Time, r.Npc, r.Company, r.Product, r.Unit, r.Status, r.ListPrice, r.FinalPrice, r.Total, r.Quantity, r.TermMonths, r.Rounds);
            return r.Id = sqlite3_last_insert_rowid(db);
        }

        public List<DealRow> LoadDeals()
        {
            var list = new List<DealRow>();
            Query("SELECT id, time, npc, company, product, unit, status, list_price, final_price, total, quantity, term_months, rounds FROM deals ORDER BY id DESC", s => list.Add(new DealRow
            {
                Id = sqlite3_column_int64(s, 0), Time = Text(s, 1), Npc = Text(s, 2), Company = Text(s, 3), Product = Text(s, 4),
                Unit = Text(s, 5), Status = Text(s, 6), ListPrice = (float)sqlite3_column_double(s, 7), FinalPrice = (float)sqlite3_column_double(s, 8),
                Total = (float)sqlite3_column_double(s, 9), Quantity = (int)sqlite3_column_int64(s, 10), TermMonths = (int)sqlite3_column_int64(s, 11),
                Rounds = (int)sqlite3_column_int64(s, 12),
            }));
            return list;
        }

        public void Flush() { }

        public void Dispose()
        {
            if (db != IntPtr.Zero) sqlite3_close(db);
            db = IntPtr.Zero;
        }
    }
#endif

    // Alternativa para WebGL e outras plataformas: tudo em JSON (arquivo ou PlayerPrefs). Vetores são recalculados ao carregar.
    public sealed class JsonStore : IBusinessStore
    {
        [Serializable] class Meta { public string key, value; }
        [Serializable]
        class Data
        {
            public long nextId = 1;
            public List<Meta> meta = new List<Meta>();
            public List<KnowledgeRow> knowledge = new List<KnowledgeRow>();
            public List<InteractionRow> interactions = new List<InteractionRow>();
            public List<DealRow> deals = new List<DealRow>();
        }

        const string PrefsKey = "pbw.businessdb";
        readonly string path;
        Data data;
        bool dirty;

        public string Description => path != null ? $"JSON ({path})" : "JSON (armazenamento do navegador)";

        public JsonStore(string filePath)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            path = null;
            var json = PlayerPrefs.GetString(PrefsKey, "");
#else
            path = filePath;
            var json = File.Exists(path) ? File.ReadAllText(path) : "";
#endif
            data = json.Length > 0 ? JsonUtility.FromJson<Data>(json) : new Data();
        }

        public string GetMeta(string key) => data.meta.FirstOrDefault(m => m.key == key)?.value;

        public void SetMeta(string key, string value)
        {
            var m = data.meta.FirstOrDefault(x => x.key == key);
            if (m == null) data.meta.Add(new Meta { key = key, value = value });
            else m.value = value;
            dirty = true;
        }

        public List<KnowledgeRow> LoadKnowledge() => new List<KnowledgeRow>(data.knowledge);
        public long AddKnowledge(KnowledgeRow r) { r.Id = data.nextId++; data.knowledge.Add(r); dirty = true; return r.Id; }
        public void UpdateKnowledge(KnowledgeRow r) => dirty = true; // as linhas são as mesmas instâncias da memória
        public void DeleteKnowledgeBySource(string source) { data.knowledge.RemoveAll(k => k.Source == source); dirty = true; }
        public void RunInTransaction(Action action) { action(); Flush(); }

        public long AddInteraction(InteractionRow r)
        {
            r.Id = data.nextId++;
            data.interactions.Add(r);
            if (data.interactions.Count > 2000) data.interactions.RemoveAt(0);
            dirty = true;
            return r.Id;
        }

        public void SetInteractionFeedback(long id, int feedback)
        {
            var r = data.interactions.FirstOrDefault(i => i.Id == id);
            if (r != null) { r.Feedback = feedback; dirty = true; }
        }

        public (int total, int positive, int negative) InteractionStats() =>
            (data.interactions.Count, data.interactions.Count(i => i.Feedback > 0), data.interactions.Count(i => i.Feedback < 0));

        public long AddDeal(DealRow r) { r.Id = data.nextId++; data.deals.Add(r); dirty = true; Flush(); return r.Id; }
        public List<DealRow> LoadDeals() => data.deals.OrderByDescending(d => d.Id).ToList();

        public void Flush()
        {
            if (!dirty) return;
            var json = JsonUtility.ToJson(data);
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
#else
            File.WriteAllText(path, json);
#endif
            dirty = false;
        }
    }
}
