using UnityEngine;

namespace PaulistaBusinessWorld
{
    public class Interactable : MonoBehaviour
    {
        public InteractableKind Kind;
        public int Index;
    }

    // Mantém rótulos virados para a câmera.
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }

    // Executivo: caminha de um lado para o outro (ou fica parado) e se vira para quem conversa com ele.
    public class NpcController : MonoBehaviour
    {
        public Person Person;
        public float BaseX, Phase;
        public AvatarRig Rig;
        public Transform TalkTarget;

        float baseYaw, clock;

        void Start() => baseYaw = transform.eulerAngles.y;

        void Update()
        {
            float speed = 0;
            if (TalkTarget != null)
            {
                var dir = TalkTarget.position - transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 360 * Time.deltaTime);
            }
            else if (Person.Wanders)
            {
                // Vai e volta 3 m ao longo da avenida, virado para a direção do passo.
                clock += Time.deltaTime; // relógio próprio: retoma de onde parou após uma conversa
                float t = clock * 0.5f + Phase;
                var p = transform.position;
                float newX = BaseX + Mathf.Sin(t) * 3f;
                speed = Mathf.Abs(newX - p.x) / Mathf.Max(Time.deltaTime, 1e-4f);
                p.x = newX;
                transform.position = p;
                float vx = Mathf.Cos(t);
                var face = Quaternion.Euler(0, vx >= 0 ? 90 : -90, 0);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, 240 * Time.deltaTime);
            }
            else
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, baseYaw, 0), 120 * Time.deltaTime);
            }
            Rig.Animate(speed);
        }
    }
}
