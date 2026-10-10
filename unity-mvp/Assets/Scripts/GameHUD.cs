using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PaulistaBusinessWorld
{
    // HUD em IMGUI: informações, filtro por setor, networking, agenda local, conversa com executivos,
    // editor do avatar do jogador, configuração da IA, avisos e minimapa.
    public class GameHUD : MonoBehaviour
    {
        const string AgendaKey = "pbw.agenda";
        const float TalkDistance = 3.5f;

        WorldBuilder world;
        PlayerController player;
        PlayerProfile profile;
        string[] sectors;
        int sectorIndex;
        bool sectorListOpen, agendaOpen, helpOpen = true;
        Interactable selected;
        string notice;
        float noticeUntil;
        readonly List<string> agenda = new List<string>();
        readonly List<Rect> uiRects = new List<Rect>();
        Vector2 agendaScroll;
        float scale = 1;

        // Conversa
        readonly Dictionary<Person, Conversation> conversations = new Dictionary<Person, Conversation>();
        NpcController talking;
        string chatInput = "";
        Vector2 chatScroll;
        bool scrollChatToEnd, focusChat;

        // Editor de avatar e IA
        bool editorOpen, editorIsFirstRun, aiOpen, rememberKey, bankOpen;
        int chatTab; // 0 conversa, 1 portfólio, 2 negociar
        List<string> suggestions = new List<string>();
        bool refreshSuggestions;
        float offerDiscount = 0.05f, nextFlush;
        Vector2 portfolioScroll, bankScroll;
        System.Action deferred; // mudanças de estado aplicadas depois do desenho (evita erros de layout do IMGUI)
        PlayerProfile draft;
        string keyInput = "";

        GUIStyle panel, title, body, small, button, noticeStyle, field, bubbleNpc, bubbleMe, hint, suggestionStyle;
        Texture2D white;

        public void Init(WorldBuilder w, PlayerController p, PlayerProfile prof, bool showAvatarEditor)
        {
            world = w;
            player = p;
            profile = prof;
            sectors = new[] { "Todos os setores" }.Concat(WorldData.Companies.Select(c => c.Sector).Distinct()).ToArray();
            player.Clicked += OnWorldClick;
            player.InteractPressed += OnInteract;
            player.IsOverUI = IsOverUI;
            var saved = PlayerPrefs.GetString(AgendaKey, "");
            if (saved.Length > 0) agenda.AddRange(saved.Split('\n'));
            if (showAvatarEditor) OpenAvatarEditor(firstRun: true);
        }

        void Update()
        {
            if (Time.time > nextFlush) { LocalModel.Instance.Flush(); nextFlush = Time.time + 10; }
            if (refreshSuggestions && talking != null)
            {
                suggestions = LocalModel.Instance.Suggestions(talking.Person, profile, conversations[talking.Person].Asked, 4);
                refreshSuggestions = false;
            }
            player.InputBlocked = talking != null || editorOpen || aiOpen;
            player.Preview = editorOpen;
            player.TalkFocus = talking != null ? talking.transform : null;
            if (talking != null && Vector3.Distance(talking.transform.position, player.transform.position) > 10) EndConversation();
        }

        bool IsOverUI(Vector2 screenPos)
        {
            var gui = new Vector2(screenPos.x, Screen.height - screenPos.y) / scale;
            return uiRects.Any(r => r.Contains(gui));
        }

        void OnWorldClick(Vector2 screenPos)
        {
            var ray = player.Camera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 600) && hit.collider.TryGetComponent<Interactable>(out var it))
                selected = it;
        }

        NpcController NearestNpc(float maxDistance)
        {
            NpcController best = null;
            float bestD = maxDistance;
            foreach (var npc in world.Npcs)
            {
                float d = Vector3.Distance(npc.transform.position, player.transform.position);
                if (d < bestD) { bestD = d; best = npc; }
            }
            return best;
        }

        void OnInteract()
        {
            var npc = NearestNpc(TalkDistance);
            if (npc != null) StartConversation(npc);
        }

        public void StartConversation(NpcController npc)
        {
            if (talking != null) talking.TalkTarget = null;
            var toPlayer = player.transform.position - npc.transform.position;
            toPlayer.y = 0;
            if (toPlayer.magnitude > 6)
            {
                // Aproxima o jogador do executivo antes de conversar.
                var spot = npc.transform.position + (toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.left) * 2.2f;
                player.Teleport(new Vector3(spot.x, npc.transform.position.y + 0.3f, Mathf.Clamp(spot.z, -23, 23)), player.transform.eulerAngles.y);
                Notify($"Você se aproximou de {npc.Person.Name}.");
            }
            talking = npc;
            npc.TalkTarget = player.transform;
            player.Face(npc.transform.position);
            if (!conversations.ContainsKey(npc.Person)) conversations[npc.Person] = new Conversation(npc.Person, profile);
            chatInput = "";
            chatTab = 0;
            scrollChatToEnd = focusChat = refreshSuggestions = true;
            selected = npc.GetComponent<Interactable>();
            LocalModel.Instance.DeriveFor(npc.Person, profile, this, msg =>
            {
                if (msg != null) Notify(msg);
                refreshSuggestions = true;
            });
        }

        // Usado por testes automatizados (-pbw-offer): negocia o primeiro produto com o desconto indicado.
        public void TestNegotiation(float discount)
        {
            var conv = conversations[talking.Person];
            var product = BusinessSeed.Instance.PortfolioOf(WorldData.CompanyOf(talking.Person.Sector)?.Name)[0];
            StartNegotiation(conv, product);
            conv.AddNpc(conv.Negotiation.SetTerm(24));
            float offer = product.price * (1 - discount);
            conv.AddPlayer($"Proponho {Money.Format(offer)} {product.unit}.");
            conv.AddNpc(conv.Negotiation.Offer(offer));
            offerDiscount = discount;
        }

        void OnApplicationQuit() => LocalModel.Instance?.Flush();

        void Ask(string text)
        {
            if (talking == null) return;
            chatTab = 0;
            conversations[talking.Person].Send(text, profile, this, () => { scrollChatToEnd = true; refreshSuggestions = true; });
        }

        // Usado por testes automatizados (-pbw-say).
        public void SayInConversation(string text)
        {
            Ask(text);
        }

        void EndConversation()
        {
            if (talking != null) talking.TalkTarget = null;
            talking = null;
            GUI.FocusControl(null);
        }

        void Notify(string text)
        {
            notice = text;
            noticeUntil = Time.time + 4.2f;
        }

        void Invite(string who)
        {
            var slot = System.TimeSpan.FromMinutes(14 * 60 + 30 * (agenda.Count % 8));
            agenda.Add($"{slot:hh\\:mm} · {who} · Meeting Hub");
            PlayerPrefs.SetString(AgendaKey, string.Join("\n", agenda));
            PlayerPrefs.Save();
            Notify($"Convite de reunião simulado para {who} às {slot:hh\\:mm}.");
        }

        public void OpenAvatarEditor(bool firstRun = false)
        {
            EndConversation();
            draft = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));
            editorIsFirstRun = firstRun;
            editorOpen = true;
        }

        public void CloseAvatarEditor()
        {
            if (!editorOpen) return;
            editorOpen = false;
            player.ApplyProfile(profile);
        }

        // ---------- IMGUI ----------

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void EnsureStyles()
        {
            if (panel != null) return;
            white = Tex(Color.white);
            panel = new GUIStyle { padding = new RectOffset(14, 14, 12, 14), normal = { background = Tex(new Color(0.035f, 0.094f, 0.153f, 0.92f)) } };
            title = new GUIStyle { fontSize = 19, fontStyle = FontStyle.Bold, richText = true, wordWrap = true, normal = { textColor = WorldBuilder.Hex(0xe9f2ff) } };
            body = new GUIStyle { fontSize = 14, richText = true, wordWrap = true, margin = new RectOffset(0, 0, 4, 4), normal = { textColor = WorldBuilder.Hex(0xc9d8e6) } };
            small = new GUIStyle(body) { fontSize = 12, normal = { textColor = WorldBuilder.Hex(0xa9bdce) } };
            button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, padding = new RectOffset(10, 10, 8, 8), margin = new RectOffset(0, 0, 6, 0),
                normal = { background = Tex(WorldBuilder.Hex(0x2279b8)), textColor = Color.white },
                hover = { background = Tex(WorldBuilder.Hex(0x2f8fd4)), textColor = Color.white },
                active = { background = Tex(WorldBuilder.Hex(0x1a5f91)), textColor = Color.white },
                onNormal = { background = Tex(WorldBuilder.Hex(0x56bbff)), textColor = WorldBuilder.Hex(0x071321) },
                onHover = { background = Tex(WorldBuilder.Hex(0x56bbff)), textColor = WorldBuilder.Hex(0x071321) },
            };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 15, padding = new RectOffset(8, 8, 7, 7), normal = { background = Tex(WorldBuilder.Hex(0x142b41)), textColor = Color.white }, focused = { background = Tex(WorldBuilder.Hex(0x1b3a57)), textColor = Color.white } };
            noticeStyle = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 15, padding = new RectOffset(16, 16, 12, 12), normal = { background = Tex(new Color(0.075f, 0.18f, 0.28f, 0.95f)), textColor = Color.white } };
            bubbleNpc = new GUIStyle(body) { fontSize = 15, padding = new RectOffset(10, 10, 7, 7), margin = new RectOffset(0, 60, 4, 4), normal = { background = Tex(WorldBuilder.Hex(0x1b3a57)), textColor = Color.white } };
            bubbleMe = new GUIStyle(bubbleNpc) { margin = new RectOffset(60, 0, 4, 4), normal = { background = Tex(WorldBuilder.Hex(0x2279b8)), textColor = Color.white } };
            hint = new GUIStyle(noticeStyle) { fontSize = 14, padding = new RectOffset(12, 12, 8, 8) };
            suggestionStyle = new GUIStyle(button) { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(10, 10, 5, 5), margin = new RectOffset(0, 0, 3, 0),
                normal = { background = Tex(WorldBuilder.Hex(0x1b3a57)), textColor = WorldBuilder.Hex(0xcfe6ff) } };
        }

        bool Button(string label, params GUILayoutOption[] opts) => GUILayout.Button(label, button, opts);

        void OnGUI()
        {
            EnsureStyles();
            scale = Mathf.Clamp(Screen.height / 860f, 0.7f, 2.2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float w = Screen.width / scale, h = Screen.height / scale;
            if (Event.current.type == EventType.Layout) uiRects.Clear();
            bool compact = w < 760;

            if (editorOpen) DrawPanel(new Rect(16, 16, 340, h - 32), DrawAvatarEditor);
            else DrawPanel(new Rect(16, 16, compact ? 270 : 320, h - 32), DrawMainPanel);
            if (!compact && !editorOpen && talking == null) DrawPanel(new Rect(w - 266, 16, 250, h - 200), DrawNetworkPanel);
            if (aiOpen) DrawPanel(new Rect(w / 2 - 230, 90, 460, 400), DrawAiSettings);
            if (bankOpen && !aiOpen) DrawPanel(new Rect(w / 2 - 260, 60, 520, h - 120), DrawBank);

            if (talking != null) DrawPanel(compact ? new Rect(16, h - 430, w - 32, 420) : new Rect(w - 476, 16, 460, h - 32), DrawChat);
            else if (!editorOpen && !aiOpen)
            {
                var near = NearestNpc(TalkDistance);
                if (near != null)
                {
                    var text = $"Pressione E para conversar com {near.Person.Name}";
                    var size = hint.CalcSize(new GUIContent(text));
                    GUI.Label(new Rect((w - size.x) / 2, h - 135, size.x, size.y), text, hint);
                }
                if (helpOpen) DrawPanel(new Rect(w / 2 - 330, h - 80, 660, 70), DrawHelp);
            }

            if (notice != null && Time.time < noticeUntil)
            {
                var size = noticeStyle.CalcSize(new GUIContent(notice));
                size.x = Mathf.Min(size.x, w * 0.8f);
                GUI.Label(new Rect((w - size.x) / 2, 18, size.x, noticeStyle.CalcHeight(new GUIContent(notice), size.x)), notice, noticeStyle);
            }

            if (!editorOpen && talking == null) DrawMinimap(new Rect(w - 191, h - 128, 175, 112));

            if (deferred != null && Event.current.type != EventType.Layout)
            {
                var action = deferred;
                deferred = null;
                action();
            }
        }

        void DrawPanel(Rect area, System.Action content)
        {
            GUILayout.BeginArea(area);
            GUILayout.BeginVertical(panel);
            content();
            GUILayout.EndVertical();
            var r = GUILayoutUtility.GetLastRect();
            GUILayout.EndArea();
            if (Event.current.type == EventType.Repaint)
                uiRects.Add(new Rect(area.x + r.x, area.y + r.y, r.width, r.height));
        }

        void DrawMainPanel()
        {
            GUILayout.Label("PAULISTA <color=#56bbff>BUSINESS WORLD</color>", title);
            GUILayout.Label("São Paulo · MVP 3D estilizado", small);
            GUILayout.Label($"Você: <b>{profile.Name}</b> · {profile.Title}, {profile.Company}", body);

            GUILayout.BeginHorizontal();
            if (Button("Meu avatar")) OpenAvatarEditor();
            GUILayout.Space(6);
            if (Button(ClaudeClient.HasKey ? "IA: ativada" : "IA: offline")) { aiOpen = !aiOpen; keyInput = ""; }
            GUILayout.EndHorizontal();
            if (Button("Banco de negócios")) bankOpen = !bankOpen;

            if (Button((sectorListOpen ? "▲ " : "▼ ") + "Setor: " + sectors[sectorIndex]))
                sectorListOpen = !sectorListOpen;
            if (sectorListOpen)
            {
                int pick = GUILayout.SelectionGrid(sectorIndex, sectors, 2, GUILayout.Height(24 * Mathf.Ceil(sectors.Length / 2f)));
                if (pick != sectorIndex)
                {
                    sectorIndex = pick;
                    sectorListOpen = false;
                    world.ApplySectorFilter(pick == 0 ? null : sectors[pick]);
                    Notify(pick == 0 ? "Exibindo todos os setores." : $"Destacando o setor {sectors[pick]}.");
                }
            }

            GUILayout.Space(8);
            DrawDetail();
            GUILayout.Space(4);
            if (Button("Ir ao centro de negócios"))
            {
                player.Teleport(WorldData.HubArrival, 180);
                Notify("Você chegou ao centro de negócios.");
            }
#if !UNITY_WEBGL
            if (Button("Sair")) Application.Quit();
#endif
        }

        void DrawDetail()
        {
            if (selected == null)
            {
                GUILayout.Label("Clique em um edifício ou executivo para ver detalhes. Perto de um executivo, pressione E para conversar.", body);
                return;
            }
            switch (selected.Kind)
            {
                case InteractableKind.Company:
                {
                    var c = WorldData.Companies[selected.Index];
                    GUILayout.Label($"<b>{c.Name}</b>", title);
                    GUILayout.Label($"Setor: {c.Sector}", body);
                    GUILayout.Label(c.Description, body);
                    GUILayout.Label("Empresa demonstrativa fictícia.", small);
                    if (Button("Propor reunião")) Invite(c.Name);
                    break;
                }
                case InteractableKind.Person:
                {
                    var p = WorldData.People[selected.Index];
                    var company = WorldData.CompanyOf(p.Sector);
                    GUILayout.Label($"<b>{p.Name}</b>", title);
                    GUILayout.Label($"{p.Title} · {company?.Name}", body);
                    GUILayout.Label($"Área: {p.Sector}", body);
                    GUILayout.Label(ClaudeClient.HasKey ? "Conversa com IA (Claude) + modelo local." : "Conversa com o modelo local (offline).", small);
                    if (Button("Conversar")) StartConversation(world.Npcs[selected.Index]);
                    if (Button("Convidar para reunião")) Invite(p.Name);
                    break;
                }
                case InteractableKind.Hub:
                    GUILayout.Label("<b>Meeting Hub</b>", title);
                    GUILayout.Label("Espaço para encontros, apresentações e negociações virtuais. Quatro executivos aguardam aqui.", body);
                    break;
                case InteractableKind.Museum:
                    GUILayout.Label("<b>Museu · Cultura</b>", title);
                    GUILayout.Label("Espaço cultural estilizado. Interpretação abstrata, sem reprodução fiel de edifício real.", body);
                    break;
            }
        }

        void DrawNetworkPanel()
        {
            GUILayout.Label("<b>Networking executivo</b>", body);
            GUILayout.Label($"{WorldData.People.Length} executivos disponíveis para conversar", small);
            if (Button("Agendar reunião simulada"))
                Notify("Agenda simulada: reunião de networking às 14h, sala Meeting Hub. Nenhum evento real foi criado.");
            if (Button(player.ThirdPerson ? "Câmera: 3ª pessoa (V)" : "Câmera: 1ª pessoa (V)")) player.ToggleThirdPerson();
            if (Button(player.Panorama ? "Voltar a caminhar" : "Visão panorâmica"))
            {
                player.TogglePanorama();
                Notify(player.Panorama ? "Visão panorâmica ativada." : "Caminhada ativada.");
            }
            if (Button($"Minha agenda ({agenda.Count})")) agendaOpen = !agendaOpen;
            if (agendaOpen)
            {
                if (agenda.Count == 0) GUILayout.Label("Nenhum convite ainda.", small);
                agendaScroll = GUILayout.BeginScrollView(agendaScroll, GUILayout.MaxHeight(200));
                foreach (var item in agenda) GUILayout.Label(item, small);
                GUILayout.EndScrollView();
                if (agenda.Count > 0 && Button("Limpar agenda"))
                {
                    agenda.Clear();
                    PlayerPrefs.DeleteKey(AgendaKey);
                }
            }
            if (Button(helpOpen ? "Ocultar ajuda" : "Mostrar ajuda")) helpOpen = !helpOpen;
        }

        void DrawHelp()
        {
            GUILayout.Label("W A S D / setas caminham · arraste o mouse para girar · Shift corre · E conversa · V alterna câmera · R reinicia · P panorâmica", small);
            GUILayout.Label("Clique em edifícios e executivos para ver detalhes. Convites e perfil ficam salvos apenas neste computador.", small);
        }

        void DrawChat()
        {
            var conv = conversations[talking.Person];
            var company = WorldData.CompanyOf(talking.Person.Sector);
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { e.Use(); deferred = EndConversation; }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{talking.Person.Name}</b>  <size=12><color=#a9bdce>{talking.Person.Title} · {company?.Name}</color></size>", title);
            if (Button("Fechar (Esc)", GUILayout.Width(110))) deferred = EndConversation;
            GUILayout.EndHorizontal();
            int tab = GUILayout.Toolbar(chatTab, new[] { "Conversa", "Portfólio", "Negociar" }, button);
            if (tab != chatTab) { int t = tab; deferred = () => chatTab = t; }

            float listHeight = Mathf.Max(200, Screen.height / scale - 330);
            if (chatTab == 0) DrawConversationTab(conv, listHeight);
            else if (chatTab == 1) DrawPortfolioTab(conv, company, listHeight);
            else DrawNegotiationTab(conv, company);

            GUILayout.BeginHorizontal();
            if (Button("Convidar para reunião")) Invite(talking.Person.Name);
            GUILayout.FlexibleSpace();
            GUILayout.Label(ClaudeClient.HasKey ? "IA: Claude + modelo local" : "Modelo local (offline)", small, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
        }

        void DrawLine(ChatLine line, string npcFirst)
        {
            if (line.System) { GUILayout.Label($"<i>{line.Text}</i>", small); return; }
            if (line.FromPlayer) { GUILayout.Label($"<b>Você:</b> {line.Text}", bubbleMe); return; }
            var tag = line.Source == "claude" ? " <size=11><color=#9fe0a0>IA</color></size>"
                : line.Source == "local" ? " <size=11><color=#f1bb72>modelo local</color></size>" : "";
            GUILayout.Label($"<b>{npcFirst}:</b>{tag} {line.Text}", bubbleNpc);
        }

        void DrawConversationTab(Conversation conv, float listHeight)
        {
            var e = Event.current;
            bool send = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "chat";
            if (send) e.Use();
            var first = talking.Person.Name.Split(' ')[0];

            chatScroll = GUILayout.BeginScrollView(chatScroll, GUILayout.Height(listHeight - 90));
            foreach (var line in conv.Lines) DrawLine(line, first);
            if (conv.Waiting) GUILayout.Label($"<i>{first} está digitando…</i>", small);
            GUILayout.EndScrollView();
            if (scrollChatToEnd && e.type == EventType.Repaint) { chatScroll.y = float.MaxValue; scrollChatToEnd = false; }

            // Avaliação da última resposta: ajusta o peso da resposta no modelo local.
            var last = conv.Lines.LastOrDefault(l => !l.FromPlayer && !l.System && l.InteractionId > 0);
            if (last != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("A última resposta ajudou?", small, GUILayout.ExpandWidth(false));
                if (GUILayout.Toggle(last.Feedback > 0, "Sim", button, GUILayout.Width(60)) != last.Feedback > 0) { var l = last; deferred = () => conv.Rate(l, l.Feedback > 0 ? 0 : 1); }
                if (GUILayout.Toggle(last.Feedback < 0, "Não", button, GUILayout.Width(60)) != last.Feedback < 0) { var l = last; deferred = () => conv.Rate(l, l.Feedback < 0 ? 0 : -1); }
                GUILayout.EndHorizontal();
            }

            if (suggestions.Count > 0)
            {
                GUILayout.Label("Perguntas sugeridas:", small);
                foreach (var q in suggestions)
                    if (GUILayout.Button(q, suggestionStyle)) { var text = q; deferred = () => Ask(text); }
            }

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("chat");
            chatInput = GUILayout.TextField(chatInput, 400, field, GUILayout.ExpandWidth(true));
            if (Button("Enviar", GUILayout.Width(80))) send = true;
            GUILayout.EndHorizontal();
            if (focusChat) { GUI.FocusControl("chat"); focusChat = false; }

            if (send && chatInput.Trim().Length > 0 && !conv.Waiting)
            {
                var text = chatInput;
                chatInput = "";
                focusChat = true;
                deferred = () => Ask(text);
            }
        }

        void DrawPortfolioTab(Conversation conv, Company company, float listHeight)
        {
            GUILayout.Label($"Portfólio da {company?.Name}. Preços de tabela; condições finais na aba Negociar.", small);
            portfolioScroll = GUILayout.BeginScrollView(portfolioScroll, GUILayout.Height(listHeight));
            foreach (var p in BusinessSeed.Instance.PortfolioOf(company?.Name))
            {
                GUILayout.BeginVertical(bubbleNpc);
                GUILayout.Label($"<b>{p.name}</b>  <size=12><color=#a9bdce>{p.kind}</color></size>", body);
                GUILayout.Label($"<color=#56bbff><b>{Money.Format(p.price)}</b></color> {p.unit}", body);
                GUILayout.Label($"{Capitalize(p.description)}. Para {p.audience}.", small);
                GUILayout.BeginHorizontal();
                if (Button("Perguntar")) { var q = $"O que é o {p.name}?"; deferred = () => Ask(q); }
                GUILayout.Space(6);
                if (Button("Negociar")) { var prod = p; deferred = () => StartNegotiation(conv, prod); }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        void StartNegotiation(Conversation conv, BusinessSeed.Product product)
        {
            conv.Negotiation = new Negotiation(talking.Person, profile, product, LocalModel.Instance.PastConcessionRatio(product.company));
            offerDiscount = 0.05f;
            conv.AddSystem($"Negociação iniciada: {product.name}.");
            conv.AddNpc(conv.Negotiation.Opening());
            chatTab = 2;
            scrollChatToEnd = true;
        }

        void DrawNegotiationTab(Conversation conv, Company company)
        {
            var n = conv.Negotiation;
            var first = talking.Person.Name.Split(' ')[0];
            if (n == null)
            {
                GUILayout.Label("Escolha o que deseja negociar:", body);
                foreach (var p in BusinessSeed.Instance.PortfolioOf(company?.Name))
                    if (Button($"{p.name} · {Money.Format(p.price)} {p.unit}")) { var prod = p; deferred = () => StartNegotiation(conv, prod); }
                return;
            }

            var pr = n.Product;
            GUILayout.Label($"<b>{pr.name}</b> · tabela {Money.Format(pr.price)} {pr.unit}", body);
            foreach (var line in conv.Lines.Skip(Mathf.Max(0, conv.Lines.Count - 3))) DrawLine(line, first);

            if (n.Status != Negotiation.State.Open)
            {
                GUILayout.Label(n.Status == Negotiation.State.Closed
                    ? $"<color=#9fe0a0><b>Negócio fechado</b></color>: {Money.Format(n.Ask)} {pr.unit} · total {Money.Format(n.Total(n.Ask))}"
                    : "<color=#f1bb72><b>Negociação encerrada sem acordo.</b></color>", body);
                if (Button("Nova negociação")) deferred = () => conv.Negotiation = null;
                return;
            }

            GUILayout.Label($"Pedido atual: <b>{Money.Format(n.Ask)}</b> {pr.unit} · rodada {n.Round} · paciência {new string('●', Mathf.Max(0, n.Patience))}{new string('○', Mathf.Max(0, 6 - n.Patience))}", body);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Quantidade: <b>{n.Quantity}</b>", body, GUILayout.Width(150));
            foreach (var step in new[] { -10, -1, 1, 10 })
                if (Button(step > 0 ? "+" + step : step.ToString(), GUILayout.Width(48))) { int q = n.Quantity + step; deferred = () => conv.AddNpc(n.SetQuantity(q)); }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Desconto por volume a partir de {pr.volume} unidades.", small);

            if (pr.Monthly)
            {
                int termIdx = n.TermMonths >= 36 ? 2 : n.TermMonths >= 24 ? 1 : 0;
                int pick = GUILayout.Toolbar(termIdx, new[] { "12 meses", "24 meses", "36 meses" }, button);
                if (pick != termIdx) { int months = (pick + 1) * 12; deferred = () => conv.AddNpc(n.SetTerm(months)); }
            }

            GUILayout.BeginHorizontal();
            foreach (var (arg, label) in new[] { (Negotiation.Argument.Avista, "Pagamento à vista"), (Negotiation.Argument.Case, "Seremos case"), (Negotiation.Argument.Concorrente, "Concorrente mais barato") })
            {
                GUI.enabled = !n.UsedArguments.Contains(arg);
                if (Button(label)) { var a = arg; deferred = () => conv.AddNpc(n.UseArgument(a)); }
                GUI.enabled = true;
                GUILayout.Space(4);
            }
            GUILayout.EndHorizontal();

            float offer = Mathf.Round(pr.price * (1 - offerDiscount) * 100f) / 100f;
            GUILayout.Label($"Sua proposta: <b>{Money.Format(offer)}</b> {pr.unit} ({offerDiscount * 100:0}% de desconto) · total {Money.Format(n.Total(offer))}", body);
            offerDiscount = Mathf.Round(GUILayout.HorizontalSlider(offerDiscount, 0f, 0.4f) * 100f) / 100f;
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (Button("Enviar proposta")) deferred = () => { conv.AddPlayer($"Proponho {Money.Format(offer)} {pr.unit} para {n.Quantity} unidade(s)."); conv.AddNpc(n.Offer(offer)); };
            GUILayout.Space(6);
            if (Button($"Aceitar {Money.Format(n.Ask)}")) deferred = () => { conv.AddPlayer("Aceito a sua proposta."); conv.AddNpc(n.AcceptAsk()); };
            GUILayout.EndHorizontal();
        }

        void DrawBank()
        {
            var model = LocalModel.Instance;
            var st = model.Stats();
            var deals = model.Deals();
            var closed = deals.Where(d => d.Status == "Fechado").ToList();
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Banco de negócios</b>", title);
            if (Button("Fechar", GUILayout.Width(80))) bankOpen = false;
            GUILayout.EndHorizontal();
            GUILayout.Label($"Armazenamento: {model.Store.Description}", small);
            bankScroll = GUILayout.BeginScrollView(bankScroll);
            GUILayout.Label("<b>Modelo local</b> (vetores de 512 dimensões, busca por similaridade)", body);
            GUILayout.Label($"Perguntas do banco inicial: {st.seedRows}\nDerivadas pela IA: {st.derived}\nAprendidas em conversas: {st.learned}\nPendentes (aguardando IA): {st.pending}", body);
            GUILayout.Label($"Interações registradas: {st.interactions} · avaliações úteis {st.positive} · não úteis {st.negative}", body);
            GUILayout.Label(ClaudeClient.HasKey
                ? "Com a IA ativada, cada conversa nova deriva perguntas e responde às pendentes da empresa (até 40 derivadas por empresa)."
                : "Ative a IA para derivar novas perguntas e ensinar o modelo local. Sem IA, o banco inicial e o que já foi aprendido continuam funcionando.", small);

            GUILayout.Space(8);
            GUILayout.Label($"<b>Meus negócios</b> · {closed.Count} fechados · {deals.Count - closed.Count} sem acordo · volume total {Money.Format(closed.Sum(d => d.Total))}", body);
            if (deals.Count == 0) GUILayout.Label("Nenhuma negociação ainda. Converse com um executivo e use a aba Negociar.", small);
            foreach (var d in deals.Take(30))
            {
                var desc = d.Status == "Fechado"
                    ? $"{Money.Format(d.FinalPrice)} {d.Unit} × {d.Quantity}{(d.TermMonths > 0 ? $" × {d.TermMonths} meses" : "")} = <b>{Money.Format(d.Total)}</b> ({(1 - d.FinalPrice / d.ListPrice) * 100:0}% de desconto)"
                    : $"sem acordo após {d.Rounds} rodadas";
                GUILayout.Label($"{d.Time?.Replace('T', ' ')} · <b>{d.Product}</b> ({d.Company}, com {d.Npc}): {desc}", small);
            }
            GUILayout.EndScrollView();
        }

        int Choice(string label, int value, string[] options, int columns)
        {
            GUILayout.Label(label, small);
            return GUILayout.SelectionGrid(value, options, columns, button);
        }

        void DrawAvatarEditor()
        {
            GUILayout.Label(editorIsFirstRun ? "Bem-vindo(a)! Crie seu avatar" : "Meu avatar", title);
            GUILayout.Label("Seu nome e cargo aparecem para os executivos durante as conversas.", small);
            GUILayout.Label("Nome", small);
            draft.Name = GUILayout.TextField(draft.Name, 40, field);
            GUILayout.Label("Cargo", small);
            draft.Title = GUILayout.TextField(draft.Title, 50, field);
            GUILayout.Label("Empresa", small);
            draft.Company = GUILayout.TextField(draft.Company, 50, field);

            var look = draft.Look;
            int gender = Choice("Traje", look.Female ? 1 : 0, new[] { "Terno masculino", "Terno feminino" }, 2);
            look.Female = gender == 1;
            if (look.Female) look.Skirt = Choice("Modelo", look.Skirt ? 0 : 1, new[] { "Saia", "Calça" }, 2) == 0;
            look.Skin = Choice("Tom de pele", look.Skin, AvatarLook.SkinNames, 4);
            look.Suit = Choice("Cor do terno", look.Suit, AvatarLook.SuitNames, 3);
            look.Hair = Choice("Cabelo", look.Hair, AvatarLook.HairNames, 2);
            look.Accent = Choice(look.Female ? "Blusa" : "Gravata", look.Accent, AvatarLook.AccentNames, 3);
            if (!look.Equals(draft.Look))
            {
                draft.Look = look;
                player.ApplyProfile(draft); // pré-visualização
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (Button("Salvar e entrar"))
            {
                if (string.IsNullOrWhiteSpace(draft.Name)) draft.Name = "Visitante";
                profile.Name = draft.Name.Trim();
                profile.Title = draft.Title.Trim();
                profile.Company = draft.Company.Trim();
                profile.Look = draft.Look;
                profile.Save();
                conversations.Clear(); // novas conversas usam o perfil atualizado
                editorOpen = false;
                player.ApplyProfile(profile);
                Notify($"Bem-vindo(a) à Paulista, {profile.Name}!");
            }
            if (!editorIsFirstRun)
            {
                GUILayout.Space(6);
                if (Button("Cancelar")) CloseAvatarEditor();
            }
            GUILayout.EndHorizontal();
        }

        void DrawAiSettings()
        {
            GUILayout.Label("Conversas com IA (Claude)", title);
            GUILayout.Label($"Com uma chave da API da Anthropic, os executivos conversam livremente usando o modelo <b>{ClaudeClient.Model}</b>. Sem chave, eles usam respostas prontas.", body);
            GUILayout.Label(ClaudeClient.HasKey ? "<color=#9fe0a0>Chave configurada.</color>" : "<color=#f1bb72>Nenhuma chave configurada.</color>", body);
            GUILayout.Label("Cole sua chave (sk-ant-…). Também é possível definir a variável de ambiente ANTHROPIC_API_KEY antes de abrir o jogo.", small);
            keyInput = GUILayout.PasswordField(keyInput, '•', 200, field);
            rememberKey = GUILayout.Toggle(rememberKey, " Lembrar neste computador (salva em texto simples no perfil do usuário)");
            GUILayout.Label("O uso da API é cobrado na sua conta Anthropic. A chave é enviada apenas para api.anthropic.com.", small);
            GUILayout.BeginHorizontal();
            if (Button("Salvar chave") && keyInput.Trim().Length > 0)
            {
                ClaudeClient.SetKey(keyInput, rememberKey);
                keyInput = "";
                aiOpen = false;
                Notify("IA conectada. Converse com um executivo para testar.");
            }
            GUILayout.Space(6);
            if (Button("Remover chave salva"))
            {
                ClaudeClient.SetKey(null, false);
                Notify(ClaudeClient.HasKey ? "Chave salva removida (a variável de ambiente continua ativa)." : "Chave removida. Modo offline.");
            }
            GUILayout.Space(6);
            if (Button("Fechar")) aiOpen = false;
            GUILayout.EndHorizontal();
        }

        void DrawMinimap(Rect r)
        {
            if (Event.current.type == EventType.Repaint) uiRects.Add(r);
            void Fill(Rect rect, Color c) { GUI.color = c; GUI.DrawTexture(rect, white); }
            Fill(r, new Color(0.063f, 0.141f, 0.231f, 0.9f));
            float MapX(float x) => r.x + (x + 220) / 440 * r.width;
            float MapZ(float z) => r.y + r.height / 2 - z / 80 * r.height; // ±80 m visíveis no eixo Z
            Fill(new Rect(r.x, MapZ(10), r.width, MapZ(-10) - MapZ(10)), WorldBuilder.Hex(0x173c4b));
            Fill(new Rect(r.x, MapZ(17), r.width, MapZ(10) - MapZ(17)), WorldBuilder.Hex(0x708f8c));
            Fill(new Rect(r.x, MapZ(-10), r.width, MapZ(-17) - MapZ(-10)), WorldBuilder.Hex(0x708f8c));
            var activeSector = sectorIndex == 0 ? null : sectors[sectorIndex];
            for (int i = 0; i < WorldData.Companies.Length; i++)
            {
                var c = WorldData.Companies[i];
                float z = i % 2 == 1 ? 37 : -37;
                var color = activeSector == null || activeSector == c.Sector ? WorldBuilder.Hex(i % 2 == 1 ? 0xf1bb72 : 0x76c6eb) : new Color(0.3f, 0.35f, 0.4f);
                Fill(new Rect(MapX(c.X) - 4, MapZ(z) - 7, 8, 14), color);
            }
            Fill(new Rect(MapX(WorldData.HubX) - 5, MapZ(WorldData.HubZ) - 4, 10, 8), WorldBuilder.Hex(0x9fe0a0));
            foreach (var npc in world.Npcs)
            {
                var np = npc.transform.position;
                Fill(new Rect(MapX(np.x) - 1.5f, MapZ(np.z) - 1.5f, 3, 3), WorldBuilder.Hex(0xffd36b));
            }
            var p = player.transform.position;
            Fill(new Rect(MapX(p.x) - 3, MapZ(p.z) - 3, 6, 6), Color.white);
            GUI.color = Color.white;
        }
    }
}
