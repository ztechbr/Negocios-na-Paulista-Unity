using System;
using UnityEngine;

namespace PaulistaBusinessWorld
{
    // Jogador com avatar próprio: WASD/setas, Shift corre, arrastar o mouse gira, V alterna 3ª/1ª pessoa,
    // E conversa com o executivo mais próximo, R reinicia, P visão panorâmica.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        const float WalkSpeed = 6, RunSpeed = 14, LookSensitivity = 0.23f;
        const float MinX = -208, MaxX = 208, MinZ = -24, MaxZ = 24;
        const float ThirdPersonDistance = 4.2f;
        static readonly Vector3 PanoramaPosition = new Vector3(25, 85, 150);

        public Camera Camera { get; private set; }
        public bool Panorama { get; private set; }
        public bool ThirdPerson { get; private set; } = true;
        public bool InputBlocked { get; set; }
        public bool Preview { get; set; } // câmera de frente para o avatar (editor)
        public Transform TalkFocus { get; set; } // executivo em conversa: câmera sobre o ombro
        public event Action<Vector2> Clicked;
        public event Action InteractPressed;
        public Func<Vector2, bool> IsOverUI = _ => false;

        CharacterController controller;
        Transform avatarHolder;
        AvatarRig rig;
        TextMesh nameLabel;
        float yaw = 90, pitch = 12, verticalSpeed, bodyYaw = 90;
        bool dragging, moved;
        Vector2 lastPointer, downPointer;

        public void Init(PlayerProfile profile, WorldBuilder world)
        {
            controller = GetComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0, 0.9f, 0);
            controller.stepOffset = 0.4f;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            Camera = camGo.AddComponent<Camera>();
            Camera.fieldOfView = 65;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 650;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = WorldBuilder.Hex(0x8fc9ef);

            avatarHolder = new GameObject("Corpo").transform;
            avatarHolder.SetParent(transform, false);
            nameLabel = world.NameLabel(profile.Name, transform, 2.15f);
            ApplyProfile(profile);
            Teleport(WorldData.SpawnPoint, 90);
        }

        public void ApplyProfile(PlayerProfile profile)
        {
            if (rig != null) Destroy(rig.gameObject);
            rig = AvatarRig.Build(avatarHolder, profile.Look);
            nameLabel.text = profile.Name;
            UpdateVisibility();
        }

        public void Teleport(Vector3 position, float newYaw)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            yaw = bodyYaw = newYaw;
            pitch = 12;
            verticalSpeed = 0;
            Panorama = false;
            UpdateVisibility();
            ApplyCamera();
        }

        public void TogglePanorama()
        {
            Panorama = !Panorama;
            UpdateVisibility();
            ApplyCamera();
        }

        public void ToggleThirdPerson()
        {
            ThirdPerson = !ThirdPerson;
            UpdateVisibility();
        }

        // Vira o corpo e a câmera para um ponto (ex.: o executivo com quem se conversa).
        public void Face(Vector3 point)
        {
            var dir = point - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) return;
            yaw = bodyYaw = Quaternion.LookRotation(dir).eulerAngles.y;
        }

        void UpdateVisibility()
        {
            bool show = ThirdPerson || Panorama || Preview;
            avatarHolder.gameObject.SetActive(show);
            nameLabel.gameObject.SetActive(Panorama);
        }

        void Update()
        {
            HandlePointer();
            UpdateVisibility();
            float speed = 0;
            if (!InputBlocked)
            {
                if (InputCompat.ResetPressed()) Teleport(WorldData.SpawnPoint, 90);
                if (InputCompat.PanoramaPressed()) TogglePanorama();
                if (InputCompat.ViewPressed()) ToggleThirdPerson();
                if (InputCompat.InteractPressed()) InteractPressed?.Invoke();
            }
            if (!Panorama) speed = Walk();
            rig.Animate(speed);
            avatarHolder.rotation = Quaternion.Euler(0, bodyYaw, 0);
        }

        void LateUpdate() => ApplyCamera();

        void HandlePointer()
        {
            var pos = InputCompat.PointerPosition();
            if (InputCompat.PointerDown() && !IsOverUI(pos))
            {
                dragging = true;
                moved = false;
                lastPointer = downPointer = pos;
            }
            if (dragging && InputCompat.PointerHeld())
            {
                var delta = pos - lastPointer;
                if ((pos - downPointer).sqrMagnitude > 16) moved = true;
                if (moved && !Panorama)
                {
                    yaw += delta.x * LookSensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * LookSensitivity, -32, 60);
                }
                lastPointer = pos;
            }
            if (dragging && InputCompat.PointerUp())
            {
                dragging = false;
                if (!moved) Clicked?.Invoke(pos);
            }
        }

        float Walk()
        {
            var input = InputBlocked ? Vector2.zero : InputCompat.Move();
            var move = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
            if (move.sqrMagnitude > 1) move.Normalize();
            float speed = move.sqrMagnitude > 0 ? (InputCompat.Run() && !InputBlocked ? RunSpeed : WalkSpeed) : 0;
            if (speed > 0)
                bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, Quaternion.LookRotation(move).eulerAngles.y, 540 * Time.deltaTime);
            move *= speed;

            verticalSpeed = controller.isGrounded ? -2f : verticalSpeed - 25f * Time.deltaTime;
            move.y = verticalSpeed;
            controller.Move(move * Time.deltaTime);

            var p = transform.position;
            var clamped = new Vector3(Mathf.Clamp(p.x, MinX, MaxX), p.y, Mathf.Clamp(p.z, MinZ, MaxZ));
            if (clamped != p) controller.Move(clamped - p);
            if (transform.position.y < -5) Teleport(WorldData.SpawnPoint, 90);
            return speed;
        }

        void ApplyCamera()
        {
            if (Camera == null) return;
            var ct = Camera.transform;
            if (Panorama)
            {
                ct.position = PanoramaPosition;
                ct.LookAt(Vector3.zero);
                return;
            }
            if (Preview)
            {
                var fwd = Quaternion.Euler(0, bodyYaw, 0) * Vector3.forward;
                var camRot = Quaternion.LookRotation(-fwd);
                var chest = transform.position + Vector3.up * 1.15f;
                ct.position = chest + fwd * 3.2f + Vector3.up * 0.2f;
                ct.LookAt(chest - (camRot * Vector3.right) * 0.9f);
                return;
            }
            if (TalkFocus != null)
            {
                var toNpc = TalkFocus.position - transform.position;
                toNpc.y = 0;
                var f = toNpc.sqrMagnitude > 0.01f ? toNpc.normalized : Vector3.forward;
                var right = Vector3.Cross(Vector3.up, f);
                ct.position = transform.position + Vector3.up * 1.95f + right * 0.75f - f * 1.7f;
                ct.LookAt(TalkFocus.position + Vector3.up * 1.45f + right * 0.8f);
                return;
            }
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var head = transform.position + Vector3.up * 1.65f;
            if (!ThirdPerson)
            {
                ct.SetPositionAndRotation(head, Quaternion.Euler(Mathf.Clamp(pitch, -32, 37), yaw, 0));
                return;
            }
            // 3ª pessoa: câmera atrás do avatar, aproximando quando há parede no caminho.
            var pivot = transform.position + Vector3.up * 1.7f;
            float dist = ThirdPersonDistance;
            var back = rot * Vector3.back;
            if (Physics.SphereCast(pivot, 0.2f, back, out var hit, dist, ~0, QueryTriggerInteraction.Ignore) && hit.collider.gameObject != gameObject)
                dist = Mathf.Max(0.6f, hit.distance - 0.1f);
            ct.SetPositionAndRotation(pivot + back * dist + Vector3.up * 0.3f, rot);
        }
    }
}
