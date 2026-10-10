using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PaulistaBusinessWorld
{
    // Funciona tanto com o Input System novo quanto com o Input Manager legado.
    public static class InputCompat
    {
#if ENABLE_INPUT_SYSTEM
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;

        public static Vector2 Move()
        {
            if (K == null) return Vector2.zero;
            float x = (K.dKey.isPressed || K.rightArrowKey.isPressed ? 1 : 0) - (K.aKey.isPressed || K.leftArrowKey.isPressed ? 1 : 0);
            float y = (K.wKey.isPressed || K.upArrowKey.isPressed ? 1 : 0) - (K.sKey.isPressed || K.downArrowKey.isPressed ? 1 : 0);
            return new Vector2(x, y);
        }
        public static bool Run() => K != null && (K.leftShiftKey.isPressed || K.rightShiftKey.isPressed);
        public static bool ResetPressed() => K != null && K.rKey.wasPressedThisFrame;
        public static bool PanoramaPressed() => K != null && K.pKey.wasPressedThisFrame;
        public static bool ViewPressed() => K != null && K.vKey.wasPressedThisFrame;
        public static bool InteractPressed() => K != null && K.eKey.wasPressedThisFrame;
        public static bool PointerDown() => M != null && (M.leftButton.wasPressedThisFrame || M.rightButton.wasPressedThisFrame);
        public static bool PointerUp() => M != null && (M.leftButton.wasReleasedThisFrame || M.rightButton.wasReleasedThisFrame);
        public static bool PointerHeld() => M != null && (M.leftButton.isPressed || M.rightButton.isPressed);
        public static Vector2 PointerPosition() => M != null ? M.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
        public static Vector2 Move()
        {
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            return new Vector2(x, y);
        }
        public static bool Run() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        public static bool ResetPressed() => Input.GetKeyDown(KeyCode.R);
        public static bool PanoramaPressed() => Input.GetKeyDown(KeyCode.P);
        public static bool ViewPressed() => Input.GetKeyDown(KeyCode.V);
        public static bool InteractPressed() => Input.GetKeyDown(KeyCode.E);
        public static bool PointerDown() => Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
        public static bool PointerUp() => Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1);
        public static bool PointerHeld() => Input.GetMouseButton(0) || Input.GetMouseButton(1);
        public static Vector2 PointerPosition() => Input.mousePosition;
#else
#error "Ative o Input System ou o Input Manager em Project Settings > Player > Active Input Handling."
#endif
    }
}
