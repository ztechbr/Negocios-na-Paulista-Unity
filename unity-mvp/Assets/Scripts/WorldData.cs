namespace PaulistaBusinessWorld
{
    public enum InteractableKind { Company, Person, Hub, Museum }

    public sealed class Company
    {
        public readonly string Name, Sector, Description;
        public readonly float X;

        public Company(string name, string sector, float x, string description)
        {
            Name = name; Sector = sector; X = x; Description = description;
        }
    }

    public sealed class Person
    {
        public readonly string Name, Title, Sector;
        public readonly bool Female, Wanders;
        public readonly AvatarLook Look;
        public readonly UnityEngine.Vector3 Position;

        public Person(string name, string title, string sector, bool female, AvatarLook look, float x, float z, bool wanders = true)
        {
            Name = name; Title = title; Sector = sector; Female = female; Look = look;
            Position = new UnityEngine.Vector3(x, 0.08f, z); Wanders = wanders;
        }
    }

    // Dados fictícios do protótipo web (web/main.js).
    public static class WorldData
    {
        public static readonly Company[] Companies =
        {
            new Company("Banco Horizonte", "Financeiro", -115, "Soluções bancárias, pagamentos e análise de risco"),
            new Company("Vida São Paulo", "Saúde", -90, "Saúde digital, gestão hospitalar e medicina preditiva"),
            new Company("Lex & Partners", "Jurídico", -65, "Serviços jurídicos e inteligência contratual"),
            new Company("Paulista Retail", "Varejo", -40, "Varejo omnicanal e planejamento de demanda"),
            new Company("Nexa Analytics", "Tecnologia", -15, "IA, engenharia de dados e serviços empresariais"),
            new Company("Nova Energia", "Energia", 15, "Transição energética, eficiência e previsão operacional"),
            new Company("Conecta Telecom", "Telecom", 40, "Conectividade e infraestrutura de redes"),
            new Company("Capital Seguros", "Seguros", 65, "Precificação de seguros e prevenção de fraudes"),
            new Company("MetroLog", "Logística", 90, "Rotas, supply chain e otimização operacional"),
            new Company("Educa Digital", "Educação", 115, "Educação corporativa e analytics de aprendizagem"),
            new Company("BioLab Paulista", "Indústria", 140, "Pesquisa, fabricação e qualidade industrial"),
            new Company("Urban Places", "Imobiliário", 165, "Empreendimentos, locação e análise imobiliária"),
        };

        // Look: (feminino, pele, terno, cabelo, gravata/blusa, saia)
        public static readonly Person[] People =
        {
            new Person("Marina Costa", "Diretora de inovação", "Tecnologia", true, new AvatarLook(true, 2, 0, 1, 2, true), -75, -9),
            new Person("Ricardo Lima", "CEO", "Financeiro", false, new AvatarLook(false, 0, 0, 0, 0, false), -55, 9),
            new Person("Fernanda Alves", "Diretora comercial", "Saúde", true, new AvatarLook(true, 1, 4, 0, 1, false), -35, -9),
            new Person("Carlos Mendes", "CIO", "Telecom", false, new AvatarLook(false, 1, 1, 0, 3, false), -15, 9),
            new Person("Juliana Rocha", "Diretora de operações", "Varejo", true, new AvatarLook(true, 3, 2, 2, 0, true), 5, -9),
            new Person("Pedro Carvalho", "CFO", "Indústria", false, new AvatarLook(false, 2, 3, 1, 1, false), 25, 9),
            new Person("Camila Ribeiro", "Diretora de estratégia", "Energia", true, new AvatarLook(true, 0, 5, 1, 3, true), 45, -9),
            new Person("André Martins", "Diretor jurídico", "Jurídico", false, new AvatarLook(false, 3, 2, 2, 2, false), 65, 9),
            new Person("Beatriz Nogueira", "Diretora de pessoas", "Educação", true, new AvatarLook(true, 1, 0, 2, 4, false), 180, 3.5f, false),
            new Person("Rafael Teixeira", "Diretor de vendas", "Seguros", false, new AvatarLook(false, 0, 2, 1, 4, false), 189, 3.5f, false),
            new Person("Larissa Prado", "Diretora de novos negócios", "Imobiliário", true, new AvatarLook(true, 2, 1, 0, 1, true), 173, -2, false),
            new Person("Gustavo Pires", "Diretor de supply chain", "Logística", false, new AvatarLook(false, 1, 0, 2, 3, false), 197, -2, false),
        };

        public static Company CompanyOf(string sector) =>
            System.Array.Find(Companies, c => c.Sector == sector);

        public static readonly UnityEngine.Vector3 SpawnPoint = new UnityEngine.Vector3(-155, 0.2f, 0);
        public static readonly UnityEngine.Vector3 HubArrival = new UnityEngine.Vector3(182, 0.6f, 12);
        public const float HubX = 185, HubZ = -7;
    }
}
