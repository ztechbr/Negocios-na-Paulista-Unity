using System.Collections.Generic;
using UnityEngine;

namespace PaulistaBusinessWorld
{
    // Aparência de um avatar executivo: índices nas paletas abaixo.
    [System.Serializable]
    public struct AvatarLook
    {
        public bool Female;
        public int Skin, Suit, Hair, Accent;
        public bool Skirt;

        public AvatarLook(bool female, int skin, int suit, int hair, int accent, bool skirt)
        {
            Female = female; Skin = skin; Suit = suit; Hair = hair; Accent = accent; Skirt = skirt;
        }

        public static readonly Color[] Skins = { C(0xf0c19b), C(0xd19d78), C(0xa67353), C(0x6b4632) };
        public static readonly string[] SkinNames = { "Clara", "Média", "Morena", "Negra" };
        public static readonly Color[] Suits = { C(0x1f2a44), C(0x33363d), C(0x17171b), C(0x5a5f69), C(0x5b2333), C(0x8c7a64) };
        public static readonly string[] SuitNames = { "Marinho", "Grafite", "Preto", "Cinza", "Bordô", "Areia" };
        public static readonly Color[] Hairs = { C(0x1e1915), C(0x4a2e1c), C(0x8a6a45), C(0xb9b4ad) };
        public static readonly string[] HairNames = { "Preto", "Castanho", "Loiro escuro", "Grisalho" };
        public static readonly Color[] Accents = { C(0x8e1f2c), C(0x2a5ea8), C(0x3f7a52), C(0xc9a24a), C(0xe8e8ea) };
        public static readonly string[] AccentNames = { "Vinho", "Azul", "Verde", "Dourado", "Pérola" };

        static Color C(int rgb) => WorldBuilder.Hex(rgb);
    }

    // Monta um executivo de terno com primitivas e anima braços e pernas ao caminhar.
    public class AvatarRig : MonoBehaviour
    {
        Transform leftLeg, rightLeg, leftArm, rightArm;
        float phase, amount;

        static Material baseMaterial;
        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        static Material Mat(Color c)
        {
            if (baseMaterial == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMaterial = probe.GetComponent<Renderer>().sharedMaterial;
                Destroy(probe);
            }
            if (materials.TryGetValue(c, out var m)) return m;
            m = new Material(baseMaterial) { color = c };
            m.SetFloat("_Glossiness", 0.2f);
            materials[c] = m;
            return m;
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 size, Color c)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = Mat(c);
            return g.transform;
        }

        static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        public static AvatarRig Build(Transform parent, AvatarLook look)
        {
            var root = new GameObject("Avatar");
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<AvatarRig>();
            var t = root.transform;
            var skin = AvatarLook.Skins[look.Skin % AvatarLook.Skins.Length];
            var suit = AvatarLook.Suits[look.Suit % AvatarLook.Suits.Length];
            var hair = AvatarLook.Hairs[look.Hair % AvatarLook.Hairs.Length];
            var accent = AvatarLook.Accents[look.Accent % AvatarLook.Accents.Length];
            var shirt = new Color(0.96f, 0.96f, 0.97f);
            var shoes = new Color(0.08f, 0.07f, 0.07f);
            var trouser = Color.Lerp(suit, Color.black, 0.15f);

            if (!look.Female)
            {
                // Terno masculino: paletó, camisa, gravata, calça social e sapatos.
                rig.leftLeg = Pivot(t, "Perna E", new Vector3(-0.11f, 0.92f, 0));
                rig.rightLeg = Pivot(t, "Perna D", new Vector3(0.11f, 0.92f, 0));
                foreach (var leg in new[] { rig.leftLeg, rig.rightLeg })
                {
                    Part(PrimitiveType.Cube, leg, new Vector3(0, -0.44f, 0), new Vector3(0.18f, 0.88f, 0.2f), trouser);
                    Part(PrimitiveType.Cube, leg, new Vector3(0, -0.88f, 0.05f), new Vector3(0.15f, 0.08f, 0.3f), shoes);
                }
                Part(PrimitiveType.Cube, t, new Vector3(0, 0.95f, 0), new Vector3(0.44f, 0.16f, 0.24f), trouser);
                Part(PrimitiveType.Cube, t, new Vector3(0, 1.25f, 0), new Vector3(0.52f, 0.62f, 0.28f), suit);
                Part(PrimitiveType.Cube, t, new Vector3(0, 1.41f, 0.135f), new Vector3(0.15f, 0.3f, 0.02f), shirt);
                Part(PrimitiveType.Cube, t, new Vector3(0, 1.36f, 0.15f), new Vector3(0.06f, 0.36f, 0.02f), accent);
                Part(PrimitiveType.Cube, t, new Vector3(0, 1.545f, 0.15f), new Vector3(0.08f, 0.05f, 0.025f), accent);
                rig.leftArm = Pivot(t, "Braco E", new Vector3(-0.32f, 1.5f, 0));
                rig.rightArm = Pivot(t, "Braco D", new Vector3(0.32f, 1.5f, 0));
                foreach (var arm in new[] { rig.leftArm, rig.rightArm })
                {
                    Part(PrimitiveType.Cube, arm, new Vector3(0, -0.29f, 0), new Vector3(0.14f, 0.6f, 0.16f), suit);
                    Part(PrimitiveType.Cube, arm, new Vector3(0, -0.6f, 0), new Vector3(0.11f, 0.04f, 0.13f), shirt);
                    Part(PrimitiveType.Sphere, arm, new Vector3(0, -0.66f, 0), Vector3.one * 0.1f, skin);
                }
                Part(PrimitiveType.Cylinder, t, new Vector3(0, 1.6f, 0), new Vector3(0.12f, 0.05f, 0.12f), skin);
                Part(PrimitiveType.Sphere, t, new Vector3(0, 1.76f, 0), new Vector3(0.22f, 0.26f, 0.24f), skin);
                Part(PrimitiveType.Sphere, t, new Vector3(0, 1.835f, -0.015f), new Vector3(0.235f, 0.15f, 0.25f), hair);
                Face(t, 1.775f, 0.11f);
            }
            else
            {
                // Terno feminino: blazer, blusa, saia lápis ou calça social e scarpin.
                rig.leftLeg = Pivot(t, "Perna E", new Vector3(-0.085f, 0.9f, 0));
                rig.rightLeg = Pivot(t, "Perna D", new Vector3(0.085f, 0.9f, 0));
                var legColor = look.Skirt ? Color.Lerp(skin, new Color(0.25f, 0.2f, 0.18f), 0.25f) : trouser;
                foreach (var leg in new[] { rig.leftLeg, rig.rightLeg })
                {
                    Part(PrimitiveType.Cube, leg, new Vector3(0, -0.43f, 0), new Vector3(look.Skirt ? 0.11f : 0.15f, 0.86f, look.Skirt ? 0.12f : 0.17f), legColor);
                    Part(PrimitiveType.Cube, leg, new Vector3(0, -0.87f, 0.04f), new Vector3(0.1f, 0.07f, 0.24f), shoes);
                    Part(PrimitiveType.Cube, leg, new Vector3(0, -0.88f, -0.065f), new Vector3(0.03f, 0.08f, 0.03f), shoes);
                }
                if (look.Skirt)
                    Part(PrimitiveType.Cube, t, new Vector3(0, 0.79f, 0), new Vector3(0.38f, 0.44f, 0.24f), suit);
                else
                    Part(PrimitiveType.Cube, t, new Vector3(0, 0.93f, 0), new Vector3(0.38f, 0.16f, 0.22f), trouser);
                Part(PrimitiveType.Cube, t, new Vector3(0, 1.26f, 0), new Vector3(0.42f, 0.56f, 0.25f), suit);
                Part(PrimitiveType.Cube, t, new Vector3(0, 1.03f, 0), new Vector3(0.44f, 0.12f, 0.26f), suit);
                Part(PrimitiveType.Cube, t, new Vector3(0, 1.4f, 0.122f), new Vector3(0.13f, 0.26f, 0.02f), accent);
                Part(PrimitiveType.Cube, t, new Vector3(0, 1.2f, 0.128f), new Vector3(0.04f, 0.04f, 0.02f), new Color(0.85f, 0.75f, 0.45f));
                rig.leftArm = Pivot(t, "Braco E", new Vector3(-0.265f, 1.49f, 0));
                rig.rightArm = Pivot(t, "Braco D", new Vector3(0.265f, 1.49f, 0));
                foreach (var arm in new[] { rig.leftArm, rig.rightArm })
                {
                    Part(PrimitiveType.Cube, arm, new Vector3(0, -0.27f, 0), new Vector3(0.11f, 0.56f, 0.13f), suit);
                    Part(PrimitiveType.Sphere, arm, new Vector3(0, -0.6f, 0), Vector3.one * 0.085f, skin);
                }
                Part(PrimitiveType.Cylinder, t, new Vector3(0, 1.58f, 0), new Vector3(0.1f, 0.05f, 0.1f), skin);
                Part(PrimitiveType.Sphere, t, new Vector3(0, 1.73f, 0), new Vector3(0.2f, 0.25f, 0.22f), skin);
                Part(PrimitiveType.Sphere, t, new Vector3(0, 1.805f, -0.015f), new Vector3(0.22f, 0.16f, 0.235f), hair);
                if (look.Hair % 2 == 0)
                    Part(PrimitiveType.Cube, t, new Vector3(0, 1.62f, -0.085f), new Vector3(0.22f, 0.32f, 0.08f), hair); // cabelo longo
                else
                    Part(PrimitiveType.Sphere, t, new Vector3(0, 1.78f, -0.13f), Vector3.one * 0.11f, hair); // coque
                Face(t, 1.745f, 0.1f);
            }
            return rig;
        }

        static void Face(Transform t, float y, float z)
        {
            var eye = new Color(0.1f, 0.08f, 0.07f);
            Part(PrimitiveType.Sphere, t, new Vector3(-0.045f, y, z), Vector3.one * 0.025f, eye);
            Part(PrimitiveType.Sphere, t, new Vector3(0.045f, y, z), Vector3.one * 0.025f, eye);
        }

        // speed: velocidade horizontal em m/s.
        public void Animate(float speed)
        {
            float target = Mathf.Clamp01(speed / 4f);
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime * 4f);
            phase += Time.deltaTime * Mathf.Lerp(4f, 11f, Mathf.Clamp01(speed / 20f)) * (amount > 0.01f ? 1 : 0);
            float swing = Mathf.Sin(phase) * 32f * amount;
            leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            leftArm.localRotation = Quaternion.Euler(-swing * 0.8f, 0, 0);
            rightArm.localRotation = Quaternion.Euler(swing * 0.8f, 0, 0);
        }
    }
}
