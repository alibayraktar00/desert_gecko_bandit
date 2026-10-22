using UnityEngine;
using UnityEngine.InputSystem;

namespace DesertGecko.Player
{
    /// <summary>
    /// Klavye ve gamepad girdisini okur. Aksiyonlar kodda tanımlı, ayrı bir asset gerekmez.
    /// Diğer scriptler sadece bu sınıfın özelliklerini okur.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        InputAction move;
        InputAction jump;
        InputAction tongue;
        InputAction tail;
        InputAction camouflage;

        public Vector2 Move { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool TonguePressed { get; private set; }
        public bool TailPressed { get; private set; }
        public bool CamouflagePressed { get; private set; }

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            move.AddBinding("<Gamepad>/dpad");

            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            tongue = new InputAction("Tongue", InputActionType.Button);
            tongue.AddBinding("<Keyboard>/j");
            tongue.AddBinding("<Gamepad>/buttonWest");

            tail = new InputAction("Tail", InputActionType.Button);
            tail.AddBinding("<Keyboard>/k");
            tail.AddBinding("<Gamepad>/buttonEast");

            camouflage = new InputAction("Camouflage", InputActionType.Button);
            camouflage.AddBinding("<Keyboard>/l");
            camouflage.AddBinding("<Gamepad>/buttonNorth");
        }

        void OnEnable()
        {
            move.Enable();
            jump.Enable();
            tongue.Enable();
            tail.Enable();
            camouflage.Enable();
        }

        void OnDisable()
        {
            move.Disable();
            jump.Disable();
            tongue.Disable();
            tail.Disable();
            camouflage.Disable();
        }

        void OnDestroy()
        {
            move.Dispose();
            jump.Dispose();
            tongue.Dispose();
            tail.Dispose();
            camouflage.Dispose();
        }

        void Update()
        {
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            JumpHeld = jump.IsPressed();
            // "Pressed" bayrakları FixedUpdate tüketene kadar açık kalır,
            // böylece iki fizik adımı arasında basılan tuş kaybolmaz.
            JumpPressed |= jump.WasPressedThisFrame();
            TonguePressed |= tongue.WasPressedThisFrame();
            TailPressed |= tail.WasPressedThisFrame();
            CamouflagePressed |= camouflage.WasPressedThisFrame();
        }

        public bool ConsumeJump() { bool v = JumpPressed; JumpPressed = false; return v; }
        public bool ConsumeTongue() { bool v = TonguePressed; TonguePressed = false; return v; }
        public bool ConsumeTail() { bool v = TailPressed; TailPressed = false; return v; }
        public bool ConsumeCamouflage() { bool v = CamouflagePressed; CamouflagePressed = false; return v; }
    }
}
