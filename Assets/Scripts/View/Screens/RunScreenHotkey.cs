using UnityEngine;

namespace Hwatu.View.Screens
{
    public sealed class RunScreenHotkey : MonoBehaviour
    {
        private System.Action _onDebugToggle;

        public void Bind(System.Action onDebugToggle)
        {
            _onDebugToggle = onDebugToggle;
        }

        private void Update()
        {
            bool pressed;
#if ENABLE_INPUT_SYSTEM
            pressed = UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame;
#else
            pressed = Input.GetKeyDown(KeyCode.F1);
#endif
            if (pressed) _onDebugToggle?.Invoke();
        }
    }
}
