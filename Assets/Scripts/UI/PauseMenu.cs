using DesertGecko.Core;
using DesertGecko.Levels;
using DesertGecko.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DesertGecko.UI
{
    /// <summary>
    /// ESC (gamepad: Start) ile açılan duraklatma menüsü: devam et, leveli yeniden başlat, ana menü.
    /// Duraklayınca zaman durur ve oyuncu girdisi kapanır (menüde basılan tuşlar oyuna sızmasın).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button mainMenuButton;
        [SerializeField] PlayerInputReader playerInput;

        public bool IsPaused { get; private set; }

        InputAction pauseAction;

        void Awake()
        {
            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");

            resumeButton.onClick.AddListener(Resume);
            restartButton.onClick.AddListener(SceneFlow.RestartCurrent);
            mainMenuButton.onClick.AddListener(SceneFlow.LoadMainMenu);
            panel.SetActive(false);
        }

        void OnEnable() => pauseAction.Enable();
        void OnDisable() => pauseAction.Disable();

        void OnDestroy()
        {
            pauseAction.Dispose();
            if (IsPaused)
                Time.timeScale = 1f; // sahne duraklatılmışken kapanırsa zaman donuk kalmasın
        }

        void Update()
        {
            if (!pauseAction.WasPressedThisFrame())
                return;
            if (IsPaused)
                Resume();
            else if (LevelManager.Current == null || !LevelManager.Current.IsComplete)
                Pause();
        }

        public void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;
            panel.SetActive(true);
            if (playerInput != null)
                playerInput.enabled = false;
            Select(resumeButton);
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            panel.SetActive(false);
            if (playerInput != null)
                playerInput.enabled = true;
        }

        static void Select(Selectable target)
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }
}
