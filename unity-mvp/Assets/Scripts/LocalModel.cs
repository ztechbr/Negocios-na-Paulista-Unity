using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PaulistaBusinessWorld
{
    // Modelo local de perguntas e respostas: cada pergunta vira um vetor (hashing de termos e n-gramas, 512 dimensões)
    // salvo no banco. Responde por similaridade de cosseno, aprende com as respostas do Claude e com o feedback
    // do jogador, e guarda perguntas sem resposta para a IA responder quando houver chave.
    public class LocalModel
    {
        public const int Dims = 512;
        const int EmbedVersion = 1;
        const float AnswerThreshold = 0.34f;
        const int MaxDerivedPerCompany = 40;

        public static LocalModel Instance { get; private set; }

        public readonly IBusinessStore Store;
        public readonly List<KnowledgeRow> Knowledge;
        readonly BusinessSeed seed = BusinessSeed.Instance;
        readonly HashSet<string> deriving = new HashSet<string>();

        public struct LocalAnswer
        {
            public string Text, Intent;
            public long KnowledgeId;
            public float Confidence;
            public BusinessSeed.Product Product;
        }

        public static LocalModel Init()
        {
            if (Instance != null) return Instance;
            var dir = Application.persistentDataPath;
            IBusinessStore store = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            store = SqliteStore.TryOpen(Path.Combine(dir, "paulista_business.db"));
#endif
            store ??= new JsonStore(Path.Combine(dir, "paulista_business.json"));
            return Instance = new LocalModel(store);
        }

        LocalModel(IBusinessStore store)
        {
            Store = store;
            if (Store.GetMeta("seed_version") != seed.version.ToString()) Reseed();
            Knowledge = Store.LoadKnowledge();
            bool reembed = Store.GetMeta("embed_version") != EmbedVersion.ToString();
            Store.RunInTransaction(() =>
            {
                foreach (var row in Knowledge)
                    if (reembed || row.Vec == null || row.Vec.Length != Dims)
                    {
                        row.Vec = Embed(row.Question);
                        Store.UpdateKnowledge(row);
                    }
                Store.SetMeta("embed_version", EmbedVersion.ToString());
            });
        }

        // Recria as linhas "seed" a partir do JSON, preservando o que foi aprendido.
        void Reseed()
        {
            Store.RunInTransaction(() =>
            {
                Store.DeleteKnowledgeBySource("seed");
                foreach (var intent in seed.intents)
                    foreach (var q in intent.questions)
                        Store.AddKnowledge(NewRow(q, string.Join("||", intent.answers), intent.intent, null, null, "seed"));
                foreach (var p in seed.products)
                {
                    var sector = WorldData.Companies.FirstOrDefault(c => c.Name == p.company)?.Sector;
                    foreach (var intent in seed.productIntents)
                        foreach (var q in intent.questions)
                            Store.AddKnowledge(NewRow(q.Replace("{produto}", p.name), string.Join("||", intent.answers), intent.intent + ":" + p.name, sector, p.company, "seed"));
                }
                Store.SetMeta("seed_version", seed.version.ToString());
            });
        }

        static KnowledgeRow NewRow(string q, string a, string intent, string sector, string company, string source) => new KnowledgeRow
        {
            Question = q, Answer = a, Intent = intent, Sector = sector, Company = company, Source = source,
            Created = DateTime.Now.ToString("s"), Vec = Embed(q),
        };

        // ---------- Vetorização ----------

        static readonly HashSet<string> Stopwords = new HashSet<string>(
            "a o as os um uma uns umas de da do das dos e em no na nos nas por para pra com sem que se ao aos voce voces vc vcs eu me meu minha nos nosso nossa sua seu suas seus isso esse essa este esta aqui la ja tem ter ser sao foi como mais muito qual quais quem onde quando ou mas tambem ate sobre entre".Split(' '));

        public static string Normalize(string text)
        {
            var d = (text ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (var ch in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            }
            return sb.ToString();
        }

        public static List<string> Tokens(string text)
        {
            var tokens = new List<string>();
            foreach (var raw in Normalize(text).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Stopwords.Contains(raw)) continue;
                var t = raw.Length > 4 && raw.EndsWith("s") ? raw.Substring(0, raw.Length - 1) : raw; // plural simples
                tokens.Add(t.Length > 6 ? t.Substring(0, 6) : t); // radical por prefixo
            }
            return tokens;
        }

        static uint Hash(string s)
        {
            uint h = 2166136261;
            foreach (var c in s) { h ^= c; h *= 16777619; }
            return h;
        }

        static void AddFeature(float[] v, string feature, float weight)
        {
            uint h = Hash(feature);
            v[h % Dims] += (h & 0x80000000) != 0 ? weight : -weight; // hashing com sinal reduz colisões
        }

        public static float[] Embed(string text)
        {
            var v = new float[Dims];
            var tokens = Tokens(text);
            var counts = new Dictionary<string, int>();
            foreach (var t in tokens) counts[t] = counts.TryGetValue(t, out var n) ? n + 1 : 1;
            foreach (var kv in counts) AddFeature(v, "w:" + kv.Key, 1f + Mathf.Log(kv.Value));
            for (int i = 0; i + 1 < tokens.Count; i++) AddFeature(v, "b:" + tokens[i] + "_" + tokens[i + 1], 0.7f);
            foreach (var t in counts.Keys) // n-gramas de caracteres toleram erros de digitação
                for (int i = 0; i + 3 <= t.Length; i++) AddFeature(v, "c:" + t.Substring(i, 3), 0.25f);
            float norm = Mathf.Sqrt(v.Sum(x => x * x));
            if (norm > 0) for (int i = 0; i < Dims; i++) v[i] /= norm;
            return v;
        }

        public static float Cosine(float[] a, float[] b)
        {
            float s = 0;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }

        // ---------- Busca e resposta ----------

        public List<(KnowledgeRow row, float score)> Retrieve(string question, string company, int k, bool answeredOnly = true)
        {
            var q = Embed(question);
            return Knowledge
                .Where(r => (r.Company == null || r.Company == company) && (!answeredOnly || !string.IsNullOrEmpty(r.Answer)))
                .Select(r => (r, Cosine(q, r.Vec) + 0.04f * Mathf.Clamp(r.Score, -3, 3) + (r.Company == company ? 0.03f : 0)))
                .OrderByDescending(x => x.Item2)
                .Take(k)
                .ToList();
        }

        public LocalAnswer Answer(string question, Person npc, PlayerProfile profile)
        {
            var company = WorldData.CompanyOf(npc.Sector)?.Name;
            var ctx = new TemplateContext(npc, profile);
            var best = Retrieve(question, company, 1).FirstOrDefault();
            if (best.row == null || best.score < AnswerThreshold)
            {
                AddPending(question, npc);
                return new LocalAnswer { Text = ctx.FillVariant(string.Join("||", seed.fallback)), Intent = "desconhecido", Confidence = best.score };
            }
            var row = best.row;
            BusinessSeed.Product product = null;
            if (row.Intent != null && row.Intent.Contains(":"))
            {
                var name = row.Intent.Substring(row.Intent.IndexOf(':') + 1);
                product = seed.products.FirstOrDefault(p => p.name == name && p.company == company);
                if (product != null) ctx.SetProduct(product);
            }
            row.Uses++;
            Store.UpdateKnowledge(row);
            return new LocalAnswer { Text = ctx.FillVariant(row.Answer), Intent = row.Intent, KnowledgeId = row.Id, Confidence = best.score, Product = product };
        }

        // Guarda perguntas sem resposta: a IA responde quando houver chave.
        void AddPending(string question, Person npc)
        {
            var company = WorldData.CompanyOf(npc.Sector)?.Name;
            var v = Embed(question);
            if (Knowledge.Any(r => r.Source == "pending" && r.Company == company && Cosine(v, r.Vec) > 0.9f)) return;
            var row = NewRow(question, null, "pendente", npc.Sector, company, "pending");
            Store.AddKnowledge(row);
            Knowledge.Add(row);
        }

        // Aprende uma resposta (do Claude ou derivada). Atualiza a linha quase idêntica, se existir.
        public KnowledgeRow Learn(string question, string answer, Person npc, string source, string intent = "aprendido")
        {
            var company = WorldData.CompanyOf(npc.Sector)?.Name;
            var v = Embed(question);
            var twin = Knowledge.FirstOrDefault(r => r.Company == company && r.Source != "seed" && Cosine(v, r.Vec) > 0.92f);
            if (twin != null)
            {
                twin.Answer = answer;
                if (twin.Source == "pending") twin.Source = source;
                twin.Intent = intent;
                twin.Score += 0.25f;
                Store.UpdateKnowledge(twin);
                return twin;
            }
            var row = NewRow(question, answer, intent, npc.Sector, company, source);
            Store.AddKnowledge(row);
            Knowledge.Add(row);
            return row;
        }

        // value: -1, 0 ou +1 (avaliação atual do jogador); previous: avaliação anterior da mesma resposta.
        public void Feedback(long knowledgeId, long interactionId, int value, int previous)
        {
            var row = Knowledge.FirstOrDefault(r => r.Id == knowledgeId);
            if (row != null)
            {
                row.Score += value - previous;
                Store.UpdateKnowledge(row);
            }
            if (interactionId > 0) Store.SetInteractionFeedback(interactionId, value);
        }

        public long Log(Person npc, string playerText, string reply, string source, long knowledgeId) => Store.AddInteraction(new InteractionRow
        {
            Time = DateTime.Now.ToString("s"), Npc = npc.Name, Company = WorldData.CompanyOf(npc.Sector)?.Name,
            PlayerText = playerText, Reply = reply, Source = source, KnowledgeId = knowledgeId,
        });

        // Perguntas sugeridas: prioriza perguntas novas (derivadas/aprendidas) e intenções ainda não exploradas.
        public List<string> Suggestions(Person npc, PlayerProfile profile, ICollection<string> asked, int count)
        {
            var company = WorldData.CompanyOf(npc.Sector)?.Name;
            var ctx = new TemplateContext(npc, profile);
            var rng = new System.Random(Environment.TickCount);
            return Knowledge
                .Where(r => (r.Company == null || r.Company == company) && !string.IsNullOrEmpty(r.Answer)
                            && r.Intent != "saudacao" && r.Intent != "despedida")
                .Select(r => (q: ctx.Fill(r.Question), r))
                .Where(x => !asked.Contains(x.q))
                .OrderByDescending(x => (x.r.Source == "derived" || x.r.Source == "learned" ? 1.5 : 0) + x.r.Score * 0.2 + rng.NextDouble())
                .GroupBy(x => x.r.Intent?.Split(':')[0]) // uma pergunta por intenção
                .Select(g => g.First().q)
                .Take(count)
                .ToList();
        }

        public string[] PortfolioLines(string company) =>
            seed.PortfolioOf(company).Select(p => $"{p.name} ({p.kind}): {p.description}. Preço de tabela {Money.Format(p.price)} {p.unit}.").ToArray();

        public (int seedRows, int derived, int learned, int pending, int interactions, int positive, int negative) Stats()
        {
            var s = Store.InteractionStats();
            return (Knowledge.Count(r => r.Source == "seed"), Knowledge.Count(r => r.Source == "derived"),
                Knowledge.Count(r => r.Source == "learned"), Knowledge.Count(r => r.Source == "pending"), s.total, s.positive, s.negative);
        }

        // ---------- Derivação com a API ----------

        [Serializable] class DerivedItem { public string question, answer, intent; }
        [Serializable] class DerivedList { public DerivedItem[] items; }

        const string DeriveSchema = @"{""type"":""object"",""additionalProperties"":false,""required"":[""items""],""properties"":{""items"":{""type"":""array"",""items"":{""type"":""object"",""additionalProperties"":false,""required"":[""question"",""answer"",""intent""],""properties"":{""question"":{""type"":""string""},""answer"":{""type"":""string""},""intent"":{""type"":""string""}}}}}}";

        // Pede ao Claude novas perguntas derivadas das existentes e respostas para perguntas pendentes da empresa.
        public void DeriveFor(Person npc, PlayerProfile profile, MonoBehaviour host, Action<string> done)
        {
            var company = WorldData.Companies.FirstOrDefault(c => c.Sector == npc.Sector);
            if (company == null || !ClaudeClient.HasKey || deriving.Contains(company.Name)) return;
            var pending = Knowledge.Where(r => r.Company == company.Name && r.Source == "pending").Take(6).ToList();
            int derivedCount = Knowledge.Count(r => r.Company == company.Name && r.Source == "derived");
            if (pending.Count == 0 && derivedCount >= MaxDerivedPerCompany) return;
            deriving.Add(company.Name);

            var ctx = new TemplateContext(npc, profile);
            var examples = Knowledge.Where(r => r.Source == "seed" && r.Company == null).OrderBy(_ => UnityEngine.Random.value)
                .Take(10).Select(r => "- " + ctx.Fill(r.Question));
            var existing = Knowledge.Where(r => r.Company == company.Name && r.Source == "derived").Select(r => "- " + r.Question).Take(40);
            int newCount = derivedCount >= MaxDerivedPerCompany ? 0 : 6;

            var prompt = new StringBuilder();
            prompt.AppendLine($"Você gera conteúdo para o banco de perguntas de um jogo de networking empresarial. Empresa fictícia: {company.Name}, setor {company.Sector}: {company.Description}.");
            prompt.AppendLine($"Quem responde é {npc.Name}, {npc.Title}, em português do Brasil, tom profissional e cordial, no máximo 3 frases, sem markdown.");
            prompt.AppendLine("Portfólio:");
            foreach (var line in PortfolioLines(company.Name)) prompt.AppendLine("- " + line);
            prompt.AppendLine("Perguntas de exemplo do banco atual:");
            foreach (var e in examples) prompt.AppendLine(e);
            if (existing.Any())
            {
                prompt.AppendLine("Perguntas já derivadas (não repita):");
                foreach (var e in existing) prompt.AppendLine(e);
            }
            if (newCount > 0)
                prompt.AppendLine($"Crie {newCount} perguntas NOVAS que um executivo visitante faria a esta empresa, derivadas das de exemplo (aprofundando, variando ou combinando temas como portfólio, preço, negociação, cases, riscos e parcerias), cada uma com a resposta.");
            if (pending.Count > 0)
            {
                prompt.AppendLine("Responda também, mantendo o texto exato da pergunta, estas perguntas que jogadores fizeram e o banco não soube responder:");
                foreach (var p in pending) prompt.AppendLine("- " + p.Question);
            }
            prompt.AppendLine("Em intent, use uma palavra-chave curta em minúsculas (ex.: portfolio, preco, desconto, cases, parceria, riscos).");

            host.StartCoroutine(ClaudeClient.SendStructured(prompt.ToString(), DeriveSchema, (json, error) =>
            {
                deriving.Remove(company.Name);
                if (error != null) { done?.Invoke(null); Debug.LogWarning(error); return; }
                DerivedList result;
                try { result = JsonUtility.FromJson<DerivedList>(json); }
                catch (ArgumentException e) { done?.Invoke(null); Debug.LogWarning(e.Message); return; }
                int added = 0, answered = 0;
                Store.RunInTransaction(() =>
                {
                    foreach (var item in result?.items ?? Array.Empty<DerivedItem>())
                    {
                        if (string.IsNullOrWhiteSpace(item.question) || string.IsNullOrWhiteSpace(item.answer)) continue;
                        bool wasPending = pending.Any(p => Normalize(p.Question).Trim() == Normalize(item.question).Trim());
                        Learn(item.question, item.answer, npc, wasPending ? "learned" : "derived", item.intent);
                        if (wasPending) answered++; else added++;
                    }
                });
                done?.Invoke($"Banco de negócios: +{added} perguntas derivadas e {answered} pendentes respondidas para {company.Name}.");
            }));
        }

        // ---------- Negócios ----------

        public List<DealRow> Deals() => Store.LoadDeals();

        public void SaveDeal(DealRow deal)
        {
            deal.Time = DateTime.Now.ToString("s");
            Store.AddDeal(deal);
        }

        // Quanto o jogador já arrancou desta empresa em negócios anteriores (0..1 da margem máxima).
        // O executivo "aprende" e fica mais firme com quem sempre negocia no limite.
        public float PastConcessionRatio(string company)
        {
            var closed = Store.LoadDeals().Where(d => d.Company == company && d.Status == "Fechado" && d.ListPrice > 0).ToList();
            if (closed.Count == 0) return 0;
            return closed.Average(d => 1 - d.FinalPrice / d.ListPrice);
        }

        public void Flush() => Store.Flush();
    }
}
