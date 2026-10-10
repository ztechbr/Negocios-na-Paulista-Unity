using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PaulistaBusinessWorld
{
    // Banco de negócios inicial (Resources/business_seed.json): portfólio, perguntas, respostas e falas de negociação.
    [Serializable]
    public class BusinessSeed
    {
        [Serializable] public class SectorInsight { public string sector, trends, pains; }
        [Serializable]
        public class Product
        {
            public string company, name, kind, description, unit, audience;
            public float price, maxDiscount;
            public int volume;
            public bool Monthly => unit.Contains("mês");
        }
        [Serializable] public class Intent { public string intent; public string[] questions, answers; }
        [Serializable] public class Lines { public string key; public string[] lines; }

        public int version;
        public SectorInsight[] sectorInsights;
        public Product[] products;
        public Intent[] intents, productIntents;
        public Lines[] negotiation;
        public string[] fallback;

        static BusinessSeed instance;
        public static BusinessSeed Instance =>
            instance ??= JsonUtility.FromJson<BusinessSeed>(Resources.Load<TextAsset>("business_seed").text);

        public Product[] PortfolioOf(string company) => products.Where(p => p.company == company).ToArray();
        public SectorInsight InsightOf(string sector) => sectorInsights.FirstOrDefault(s => s.sector == sector);
        public string[] NegotiationLines(string key) => negotiation.FirstOrDefault(n => n.key == key)?.lines ?? new[] { "" };
    }

    public static class Money
    {
        // Formata em reais sem depender da cultura do sistema (o WebGL usa cultura invariante).
        public static string Format(float value)
        {
            long cents = (long)Math.Round(value * 100.0);
            long reais = cents / 100;
            int c = (int)(cents % 100);
            var digits = reais.ToString();
            var sb = new StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append('.');
                sb.Append(digits[i]);
            }
            return $"R$ {sb},{c:00}";
        }
    }

    // Preenche marcadores {chave} das respostas com dados do executivo, da empresa, do jogador e do produto.
    public class TemplateContext
    {
        readonly Dictionary<string, string> values = new Dictionary<string, string>();

        public TemplateContext(Person npc, PlayerProfile profile, BusinessSeed.Product product = null)
        {
            var seed = BusinessSeed.Instance;
            var company = WorldData.CompanyOf(npc.Sector);
            var portfolio = seed.PortfolioOf(company?.Name);
            var insight = seed.InsightOf(npc.Sector);
            var highlight = portfolio.Length > 0 ? portfolio[Mathf.Abs(npc.Name.GetHashCode()) % portfolio.Length] : null;

            values["npc"] = npc.Name;
            values["npc_primeiro"] = npc.Name.Split(' ')[0];
            values["cargo"] = npc.Title;
            values["empresa"] = company?.Name ?? npc.Sector;
            values["setor"] = npc.Sector;
            values["setor_lower"] = npc.Sector.ToLowerInvariant();
            values["descricao"] = company?.Description.ToLowerInvariant() ?? "";
            values["jogador"] = profile.Name;
            values["jogador_empresa"] = profile.Company;
            values["produtos"] = JoinPt(portfolio.Select(p => p.name));
            values["tendencias"] = insight?.trends ?? "digitalização";
            values["desafios"] = insight?.pains ?? "eficiência";
            if (highlight != null)
            {
                values["produto_destaque"] = highlight.name;
                values["preco_destaque"] = $"{Money.Format(highlight.price)} {highlight.unit}";
            }
            if (portfolio.Length > 0)
            {
                values["preco_min"] = Money.Format(portfolio.Min(p => p.price));
                values["preco_max"] = Money.Format(portfolio.Max(p => p.price));
            }
            if (product != null) SetProduct(product);
        }

        public void SetProduct(BusinessSeed.Product p)
        {
            values["produto"] = p.name;
            values["tipo_lower"] = p.kind.ToLowerInvariant();
            values["descricao_produto"] = p.description;
            values["preco"] = Money.Format(p.price);
            values["unidade"] = p.unit;
            values["publico"] = p.audience;
        }

        public void Set(string key, string value) => values[key] = value;

        public string Fill(string template)
        {
            var sb = new StringBuilder(template);
            foreach (var kv in values) sb.Replace("{" + kv.Key + "}", kv.Value);
            return sb.ToString();
        }

        // Escolhe uma variante ("a||b") e preenche os marcadores.
        public string FillVariant(string variants)
        {
            var options = variants.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
            return options.Length == 0 ? "" : Fill(options[UnityEngine.Random.Range(0, options.Length)]);
        }

        static string JoinPt(IEnumerable<string> items)
        {
            var list = items.ToList();
            if (list.Count == 0) return "";
            if (list.Count == 1) return list[0];
            return string.Join(", ", list.Take(list.Count - 1)) + " e " + list[list.Count - 1];
        }
    }
}
