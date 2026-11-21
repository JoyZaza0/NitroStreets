//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ALIyerEdon
{
    public class ControllerMouseSimulator : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Mouse cursor movement speed using stick input")]
        [SerializeField] private float mouseSensitivity = 3000;

        [Header("Click Settings")]
        [Tooltip("Minimum time (in seconds) between consecutive clicks")]
        [SerializeField] private float clickCooldown = 0;

        private float nextClickTime;

        [Header("UI Module")]
        [SerializeField] private InputSystemUIInputModule uiModule;

        public Texture2D cursorTexture;

        private bool uiPatched;

        void Awake()
        {
            if (uiModule == null && EventSystem.current != null)
                uiModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
        }

        public void Update_CursorIcon(bool isVisible)
        {
            if (isVisible)
            {
                Cursor.SetCursor(cursorTexture, Vector2.zero, UnityEngine.CursorMode.Auto);
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                // Setup a virtually invisible 1x1 texture
                Texture2D cursorTexture_Hide = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                cursorTexture_Hide.SetPixel(0, 0, new Color(0, 0, 0, 0.000123f)); // Alpha is 1/255 (invisible to eye, but valid for OS)
                cursorTexture_Hide.Apply();

                Cursor.SetCursor(cursorTexture_Hide, Vector2.zero, CursorMode.Auto);
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        void Update()
        {
            var gamepad = Gamepad.current;
            var mouse = Mouse.current;

            if (mouse == null) return;

            if (gamepad != null)
            {
                PatchUIModuleOnce();

                Vector2 stickInput = gamepad.leftStick.ReadValue();

                if (stickInput.magnitude > 0.15f)
                {
                    Vector2 currentMousePos = mouse.position.ReadValue();
                    Vector2 translation = stickInput * mouseSensitivity * Time.unscaledDeltaTime;

                    Vector2 newMousePos = currentMousePos + translation;
                    newMousePos.x = Mathf.Clamp(newMousePos.x, 0, Screen.width);
                    newMousePos.y = Mathf.Clamp(newMousePos.y, 0, Screen.height);

                    mouse.WarpCursorPosition(newMousePos);
                }

                if (gamepad.buttonSouth.wasPressedThisFrame && Time.unscaledTime >= nextClickTime)
                {
                    nextClickTime = Time.unscaledTime + clickCooldown;

                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState
                    {
                        position = mouse.position.ReadValue(),
                        buttons = 1 << (int)MouseButton.Left
                    });
                }
                else if (gamepad.buttonSouth.wasReleasedThisFrame)
                {
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState
                    {
                        position = mouse.position.ReadValue(),
                        buttons = 0
                    });
                }
            }
        }

        private void PatchUIModuleOnce()
        {
            if (uiPatched) return;
            if (uiModule == null) return;

            uiModule.submit = null;
            uiModule.cancel = null;

            uiPatched = true;
        }
    }
}
