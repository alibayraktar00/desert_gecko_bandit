using System.Collections;
using DesertGecko.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesertGecko.UI
{
    /// <summary>Level bitince açılan panel: toplanan kristaller, sonraki level / tekrar / ana menü.</summary>
    public class LevelCompleteUI : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Text titleLabel;
        [SerializeField] Text crystalsLabel;
        [SerializeField] Button nextButton;
        [SerializeField] Button retryButton;
        [SerializeField] Button mainMenuButton;

        const float InputDelay = 0.5f;

        int levelIndex = -1;

        void Awake()
        {
            nextButton.onClick.AddListener(() => SceneFlow.LoadLevel(levelIndex + 1));
            retryButton.onClick.AddListener(SceneFlow.RestartCurrent);
            mainMenuButton.onClick.AddListener(SceneFlow.LoadMainMenu);
            panel.SetActive(false);
        }

        public void Show(int level, int crystals, bool newRecord)
        {
            levelIndex = level;
            bool hasNext = level >= 0 && level + 1 < LevelCatalog.Count;

            titleLabel.text = hasNext ? "Level tamamlandı!" : "Kızgın Kum Vadisi bitti!";
            crystalsLabel.text = newRecord && crystals > 0
                ? $"Kristal: {crystals}   (yeni rekor!)"
                : $"Kristal: {crystals}";
            nextButton.gameObject.SetActive(hasNext);
            panel.SetActive(true);
            StartCoroutine(EnableButtonsAfterDelay(hasNext ? nextButton : mainMenuButton));
        }

        // Gamepad'de zıplama tuşu aynı zamanda "onay"; bitişe zıplayarak giren oyuncu ekranı
        // görmeden "Sonraki Level"e basmasın diye düğmeler kısa bir süre kapalı başlar.
        IEnumerator EnableButtonsAfterDelay(Button first)
        {
            SetInteractable(false);
            yield return new WaitForSecondsRealtime(InputDelay);
            SetInteractable(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        void SetInteractable(bool value)
        {
            nextButton.interactable = value;
            retryButton.interactable = value;
            mainMenuButton.interactable = value;
        }
    }
}
