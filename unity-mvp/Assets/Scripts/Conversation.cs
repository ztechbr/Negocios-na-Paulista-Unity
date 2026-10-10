using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PaulistaBusinessWorld
{
    [System.Serializable]
    public class PlayerProfile
    {
        public string Name = "Visitante";
        public string Title = "Executivo(a)";
        public string Company = "Minha empresa";
        public AvatarLook Look = new AvatarLook(false, 1, 0, 0, 1, false);

        const string Key = "pbw.profile";

        public static PlayerProfile Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            return json.Length > 0 ? JsonUtility.FromJson<PlayerProfile>(json) : null;
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }

    public class ChatLine
    {
        public bool FromPlayer, System;
        public string Text, Source;  // Source: "claude", "local" ou "negociação"
        public long InteractionId, KnowledgeId;
        public int Feedback;
    }

    // Conversa com um executivo. Com chave de API, o Claude responde usando o portfólio e o banco local como contexto,
    // e cada resposta alimenta o modelo local. Sem chave, o modelo local responde sozinho.
    public class Conversation
    {
        const int MaxTurns = 24;

        public readonly Person Person;
        public readonly List<ChatLine> Lines = new List<ChatLine>();
        public readonly HashSet<string> Asked = new HashSet<string>();
        public Negotiation Negotiation;
        public bool Waiting { get; private set; }
        readonly List<ClaudeClient.Message> history = new List<ClaudeClient.Message>();
        readonly string company;

        public Conversation(Person person, PlayerProfile profile)
        {
            Person = person;
            company = WorldData.CompanyOf(person.Sector)?.Name;
            Lines.Add(new ChatLine { Text = $"Olá, {profile.Name}! Sou {person.Name}, {person.Title} da {company ?? person.Sector}. Em que posso ajudar?" });
        }

        public void AddSystem(string text) => Lines.Add(new ChatLine { System = true, Text = text });
        public void AddNpc(string text, string source = "negociação") { if (!string.IsNullOrEmpty(text)) Lines.Add(new ChatLine { Text = text, Source = source }); }
        public void AddPlayer(string text) => Lines.Add(new ChatLine { FromPlayer = true, Text = text });

        string SystemPrompt(PlayerProfile profile, string question)
        {
            var model = LocalModel.Instance;
            var c = WorldData.CompanyOf(Person.Sector);
            var others = string.Join("; ", WorldData.Companies.Select(x => $"{x.Name} ({x.Sector})"));
            var portfolio = string.Join("\n", model.PortfolioLines(company).Select(l => "- " + l));
            var references = model.Retrieve(question, company, 3)
                .Where(x => x.score > 0.3f && x.row.Source != "seed")
                .Select(x => $"- P: {x.row.Question}\n  R: {x.row.Answer}");
            return
$@"Você é {Person.Name}, {Person.Title} da {c?.Name} (setor {Person.Sector}): {c?.Description}.
Você está no Paulista Business World, um evento virtual de networking empresarial ambientado em uma versão estilizada da Avenida Paulista, em São Paulo. Há um Meeting Hub no fim da avenida para reuniões.
Empresas presentes (todas fictícias): {others}.
Você conversa por chat com {profile.Name}, {profile.Title} da {profile.Company}.

Portfólio da sua empresa (preços de tabela):
{portfolio}

Respostas que você já deu antes a perguntas parecidas (mantenha coerência):
{string.Join("\n", references)}

Como responder:
- Português do Brasil, tom profissional, cordial e natural, como numa conversa de networking presencial.
- Respostas curtas: no máximo 3 frases. Sem listas, sem markdown.
- Use os produtos e preços do portfólio acima; não invente outros preços. As empresas são fictícias.
- Para negociar preço, convide o jogador a usar a aba Negociar; para reuniões, o botão Convidar para reunião.
- Você não tem acesso à internet nem a sistemas reais; não prometa ações fora da conversa.";
        }

        public void Send(string text, PlayerProfile profile, MonoBehaviour host, System.Action changed)
        {
            text = text.Trim();
            if (text.Length == 0 || Waiting) return;
            var model = LocalModel.Instance;
            Asked.Add(text);
            AddPlayer(text);
            changed();

            if (!ClaudeClient.HasKey)
            {
                ReplyLocal(text, profile, changed);
                return;
            }

            history.Add(new ClaudeClient.Message { role = "user", content = text });
            while (history.Count > MaxTurns) history.RemoveRange(0, 2); // mantém pares usuário/assistente
            Waiting = true;
            host.StartCoroutine(ClaudeClient.Send(SystemPrompt(profile, text), history, (reply, error) =>
            {
                Waiting = false;
                if (error != null)
                {
                    Debug.LogWarning(error);
                    history.RemoveAt(history.Count - 1); // erro não entra no histórico enviado à IA
                    ReplyLocal(text, profile, changed, "\n(" + error + " — resposta do modelo local.)");
                    return;
                }
                history.Add(new ClaudeClient.Message { role = "assistant", content = reply });
                var learned = model.Learn(text, reply, Person, "learned");
                var id = model.Log(Person, text, reply, "claude", learned.Id);
                Lines.Add(new ChatLine { Text = reply, Source = "claude", InteractionId = id, KnowledgeId = learned.Id });
                changed();
            }));
        }

        void ReplyLocal(string text, PlayerProfile profile, System.Action changed, string suffix = "")
        {
            var model = LocalModel.Instance;
            var answer = model.Answer(text, Person, profile);
            var id = model.Log(Person, text, answer.Text, "local", answer.KnowledgeId);
            Lines.Add(new ChatLine { Text = answer.Text + suffix, Source = "local", InteractionId = id, KnowledgeId = answer.KnowledgeId });
            if (answer.Intent == "desconhecido")
                AddSystem(ClaudeClient.HasKey
                    ? "Pergunta guardada no banco; a IA vai respondê-la e o modelo local aprende."
                    : "Pergunta guardada no banco como pendente. Com a IA ativada, ela será respondida e aprendida.");
            changed();
        }

        public void Rate(ChatLine line, int value)
        {
            if (line.Feedback == value) return;
            LocalModel.Instance.Feedback(line.KnowledgeId, line.InteractionId, value, line.Feedback);
            line.Feedback = value;
        }
    }
}
