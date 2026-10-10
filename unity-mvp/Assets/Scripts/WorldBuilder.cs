using System.Collections.Generic;
using UnityEngine;

namespace PaulistaBusinessWorld
{
    // Gera proceduralmente a avenida, os edifícios, o museu, os executivos e o Meeting Hub.
    public class WorldBuilder : MonoBehaviour
    {
        public sealed class BuildingRef
        {
            public Company Company;
            public Renderer Renderer;
            public Color BaseColor;
        }

        public readonly List<BuildingRef> Buildings = new List<BuildingRef>();

        static readonly Color[] FacadeColors =
        {
            Hex(0x355775), Hex(0x4a637c), Hex(0x77899d), Hex(0x465970), Hex(0x587b86), Hex(0x687787)
        };

        Material baseMaterial;
        Font font;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        Transform staticRoot, dynamicRoot;

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        public void Build()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMaterial = probe.GetComponent<Renderer>().sharedMaterial;
            Destroy(probe);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            staticRoot = new GameObject("Cenario (estatico)").transform;
            staticRoot.SetParent(transform);
            dynamicRoot = new GameObject("Interativos").transform;
            dynamicRoot.SetParent(transform);

            BuildEnvironment();
            BuildStreet();
            BuildCompanies();
            BuildMuseum();
            BuildPeople();
            BuildMeetingHub();

            StaticBatchingUtility.Combine(staticRoot.gameObject);
        }

        public void ApplySectorFilter(string sector)
        {
            var dim = new Color(0.16f, 0.17f, 0.19f);
            foreach (var b in Buildings)
            {
                bool active = sector == null || b.Company.Sector == sector;
                b.Renderer.material.color = active ? b.BaseColor : Color.Lerp(b.BaseColor, dim, 0.7f);
            }
        }

        // ---------- Primitivas ----------

        Material Mat(Color c, float emission = 0)
        {
            if (emission <= 0 && materials.TryGetValue(c, out var cached)) return cached;
            var m = new Material(baseMaterial) { color = c };
            m.SetFloat("_Glossiness", 0.15f);
            if (emission > 0)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
                return m;
            }
            materials[c] = m;
            return m;
        }

        GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 size, Material m,
            bool collider = true, bool shadows = true)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = size;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!collider) Destroy(g.GetComponent<Collider>());
            return g;
        }

        GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 size, Color c, bool collider = true, bool shadows = true) =>
            Prim(PrimitiveType.Cube, name, parent, pos, size, Mat(c), collider, shadows);

        TextMesh Label(string text, Transform parent, Vector3 pos, float height, float yRotation, bool background, bool billboard = false)
        {
            var go = new GameObject("Rotulo - " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yRotation, 0);
            var t = go.AddComponent<TextMesh>();
            t.font = font;
            t.text = text;
            t.fontSize = 64;
            t.characterSize = height / 6.4f;
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.color = Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            if (billboard) go.AddComponent<Billboard>();
            if (background)
            {
                float width = height * 0.62f * text.Length + height;
                var plate = Cube("Placa", go.transform, new Vector3(0, 0, 0.06f), new Vector3(width, height * 1.7f, 0.05f), Hex(0x0b2440), false, false);
                plate.transform.localRotation = Quaternion.identity;
            }
            return t;
        }

        // ---------- Cena ----------

        void BuildEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex(0x7d93a6);
            RenderSettings.ambientEquatorColor = Hex(0x5f6f7c);
            RenderSettings.ambientGroundColor = Hex(0x34414c);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex(0x9bcce9);
            RenderSettings.fogStartDistance = 120;
            RenderSettings.fogEndDistance = 450;

            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.transform.SetParent(transform);
            sun.type = LightType.Directional;
            sun.color = Hex(0xffeed7);
            sun.intensity = 1.0f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, 35, 0);
        }

        void BuildStreet()
        {
            var s = staticRoot;
            Cube("Gramado", s, new Vector3(0, -0.15f, 0), new Vector3(1400, 0.15f, 700), Hex(0x5f8a66));
            Cube("Avenida Paulista", s, new Vector3(5, 0.03f, 0), new Vector3(440, 0.1f, 20), Hex(0x303f50));
            foreach (int side in new[] { -1, 1 })
            {
                Cube("Calcada", s, new Vector3(5, 0.06f, side * 13.5f), new Vector3(440, 0.15f, 7), Hex(0x9a9b97));
                Cube("Canteiro", s, new Vector3(5, 0.07f, side * 18.3f), new Vector3(440, 0.13f, 2.4f), Hex(0x6eaa77));
                for (int x = -202; x < 215; x += 14)
                    Cube("Faixa", s, new Vector3(x, 0.09f, side * 4.4f), new Vector3(6, 0.03f, 0.2f), Hex(0xf9f9f0), false, false);

                var trunk = Mat(Hex(0x635243));
                var leaves = Mat(Hex(0x388558));
                var leaves2 = Mat(Hex(0x3b9761));
                var pole = Mat(Hex(0x5f6476));
                var bulb = Mat(Hex(0xfff0bb), 1.2f);
                for (int x = -190; x < 205; x += 25)
                {
                    float z = side * 21.5f;
                    Prim(PrimitiveType.Cylinder, "Tronco", s, new Vector3(x, 2.5f, z), new Vector3(0.8f, 2.5f, 0.8f), trunk);
                    Prim(PrimitiveType.Sphere, "Copa", s, new Vector3(x, 6.5f, z), Vector3.one * 5.4f, leaves, false);
                    Prim(PrimitiveType.Sphere, "Copa", s, new Vector3(x + 1, 7.7f, z), Vector3.one * 4.4f, leaves2, false);
                    Prim(PrimitiveType.Cube, "Poste", s, new Vector3(x + 3, 2.6f, side * 12.5f), new Vector3(0.25f, 5, 0.25f), pole);
                    Prim(PrimitiveType.Sphere, "Luminaria", s, new Vector3(x + 3, 5.4f, side * 12.5f), Vector3.one * 1.3f, bulb, false, false);
                }
            }
        }

        void BuildCompanies()
        {
            var companies = WorldData.Companies;
            var glass = Mat(Hex(0x8dd2ea));
            for (int i = 0; i < companies.Length; i++)
            {
                var c = companies[i];
                int side = i % 2 == 1 ? 1 : -1;
                float x = c.X, z = side * 37, h = 18 + (i * 13) % 37, w = 18 + (i % 3) * 3;
                float front = z - side * 9f;
                var color = FacadeColors[i % FacadeColors.Length];

                var group = new GameObject("Empresa - " + c.Name).transform;
                group.SetParent(dynamicRoot);
                var body = Cube("Edificio", group, new Vector3(x, h / 2, z), new Vector3(w, h, 18), color);
                var it = body.AddComponent<Interactable>();
                it.Kind = InteractableKind.Company;
                it.Index = i;
                Buildings.Add(new BuildingRef { Company = c, Renderer = body.GetComponent<Renderer>(), BaseColor = color });

                for (float y = 3; y < h - 1; y += 4)
                    for (float dx = -w / 2 + 2; dx < w / 2 - 1; dx += 4)
                        Prim(PrimitiveType.Cube, "Janela", staticRoot, new Vector3(x + dx, y, front - side * 0.06f), new Vector3(2, 1.7f, 0.08f), glass, false, false);

                Cube("Cobertura", staticRoot, new Vector3(x, h + 0.2f, z), new Vector3(w + 1, 0.5f, 19), Hex(0x29485b), false);
                Label(c.Name, group, new Vector3(x, 4.2f, front - side * 0.35f), 1.6f, side < 0 ? 180 : 0, true);
            }
        }

        void BuildMuseum()
        {
            // Volume geométrico inspirado em um museu suspenso: interpretação abstrata, não reconstrução.
            var group = new GameObject("Museu - Cultura").transform;
            group.SetParent(dynamicRoot);
            var red = Hex(0xc9473f);
            foreach (int x in new[] { -10, 10 })
                foreach (int z in new[] { -3, 3 })
                    Cube("Pilar vermelho", staticRoot, new Vector3(x, 6.5f, z - 69), new Vector3(1.3f, 13, 1.2f), red);
            var body = Cube("Volume suspenso", group, new Vector3(0, 17, -69), new Vector3(27, 8, 12), Hex(0x385671));
            body.AddComponent<Interactable>().Kind = InteractableKind.Museum;
            Cube("Praca", staticRoot, new Vector3(0, 0.12f, -69), new Vector3(30, 0.25f, 17), Hex(0xbfc0b7));
            Label("MUSEU · CULTURA", group, new Vector3(0, 23, -69), 1.6f, 180, true, true);
        }

        public readonly List<NpcController> Npcs = new List<NpcController>();

        void BuildPeople()
        {
            var people = WorldData.People;
            for (int i = 0; i < people.Length; i++)
            {
                var person = people[i];
                var root = new GameObject("Executivo - " + person.Name);
                root.transform.SetParent(dynamicRoot);
                root.transform.position = person.Position;
                // Quem fica no Meeting Hub olha para o centro da sala.
                var toHub = new Vector3(WorldData.HubX, 0, WorldData.HubZ) - person.Position;
                toHub.y = 0;
                root.transform.rotation = person.Wanders ? Quaternion.Euler(0, 90, 0) : Quaternion.LookRotation(toHub);

                var rig = AvatarRig.Build(root.transform, person.Look);
                var col = root.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.95f, 0);
                col.height = 1.9f;
                col.radius = 0.35f;
                var it = root.AddComponent<Interactable>();
                it.Kind = InteractableKind.Person;
                it.Index = i;
                var npc = root.AddComponent<NpcController>();
                npc.Person = person;
                npc.Rig = rig;
                npc.BaseX = person.Position.x;
                npc.Phase = i * 1.6f;
                Npcs.Add(npc);
                Label(person.Name, root.transform, new Vector3(0, 2.15f, 0), 0.22f, 0, true, true);
            }
        }

        public TextMesh NameLabel(string text, Transform parent, float y) => Label(text, parent, new Vector3(0, y, 0), 0.22f, 0, true, true);

        void BuildMeetingHub()
        {
            float hx = WorldData.HubX, hz = WorldData.HubZ;
            var group = new GameObject("Meeting Hub").transform;
            group.SetParent(dynamicRoot);
            var floor = Cube("Piso", group, new Vector3(hx, 0.2f, hz), new Vector3(23, 0.4f, 20), Hex(0xd1d8dc));
            floor.AddComponent<Interactable>().Kind = InteractableKind.Hub;
            foreach (int x in new[] { 174, 196 })
                foreach (int z in new[] { -17, 3 })
                    Cube("Pilar", staticRoot, new Vector3(x, 4.4f, z), new Vector3(0.3f, 8, 0.3f), Hex(0x607c94));
            Cube("Cobertura", staticRoot, new Vector3(hx, 8.6f, hz), new Vector3(23, 0.5f, 20), Hex(0x24415f));
            Cube("Mesa", staticRoot, new Vector3(hx, 1.15f, hz), new Vector3(8, 0.18f, 4.4f), Hex(0x875e3e));
            Cube("Base da mesa", staticRoot, new Vector3(hx, 0.75f, hz), new Vector3(1, 0.7f, 1), Hex(0x5a3e29));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                Cube("Cadeira", staticRoot, new Vector3(hx + Mathf.Cos(a) * 5.2f, 0.75f, hz + Mathf.Sin(a) * 3.7f), new Vector3(1.1f, 0.7f, 1.1f), Hex(0x3c6883));
            }
            Label("MEETING HUB", group, new Vector3(hx, 10.2f, hz + 10.1f), 1.3f, 180, true);
        }
    }
}
