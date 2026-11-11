using DesertGecko.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesertGecko.UI
{
    /// <summary>
    /// Giriş ekranı: Başla/Devam et, level seçimi, ayarlar, çıkış. Üç panel arasında geçiş yapar;
    /// klavye/gamepad ile gezinilebilsin diye her panel açılınca ilk düğme seçilir.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Paneller")]
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject levelsPanel;
        [SerializeField] GameObject settingsPanel;

        [Header("Ana panel")]
        [SerializeField] Button playButton;
        [SerializeField] Text playLabel;
        [SerializeField] Button levelsButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;
        [SerializeField] Text totalCrystalsLabel;

        [Header("Level seçimi")]
        [SerializeField] Button[] levelButtons;
        [SerializeField] Text[] levelLabels;
        [SerializeField] Button levelsBackButton;

        [Header("Ayarlar")]
        [SerializeField] Slider volumeSlider;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] Button resetButton;
        [SerializeField] Text resetLabel;
        [SerializeField] Button settingsBackButton;

        bool resetArmed;

        void Awake()
        {
            // "Devam Et" en ileri açılmış levele götürür (eski bir leveli tekrar oynamak bunu geri çekmez).
            playButton.onClick.AddListener(() => SceneFlow.LoadLevel(GameProgress.HighestUnlocked));
            levelsButton.onClick.AddListener(() => Show(levelsPanel));
            settingsButton.onClick.AddListener(() => Show(settingsPanel));
            quitButton.onClick.AddListener(SceneFlow.Quit);
            levelsBackButton.onClick.AddListener(() => Show(mainPanel));
            settingsBackButton.onClick.AddListener(() => Show(mainPanel));

            for (int i = 0; i < levelButtons.Length; i++)
            {
                int level = i;
                levelButtons[i].onClick.AddListener(() => SceneFlow.LoadLevel(level));
            }

            volumeSlider.value = GameProgress.Volume;
            volumeSlider.onValueChanged.AddListener(v => GameProgress.Volume = v);
            fullscreenToggle.isOn = GameProgress.Fullscreen;
            fullscreenToggle.onValueChanged.AddListener(on => GameProgress.Fullscreen = on);
            resetButton.onClick.AddListener(OnResetClicked);
        }

        void Start()
        {
            Time.timeScale = 1f;
            Show(mainPanel);
        }

        void Show(GameObject panel)
        {
            mainPanel.SetActive(panel == mainPanel);
            levelsPanel.SetActive(panel == levelsPanel);
            settingsPanel.SetActive(panel == settingsPanel);
            Refresh();

            Selectable first = panel == levelsPanel ? levelButtons[0]
                : panel == settingsPanel ? (Selectable)volumeSlider
                : playButton;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        void Refresh()
        {
            playLabel.text = GameProgress.HasProgress ? "Devam Et" : "Başla";
            int total = GameProgress.TotalCrystals;
            totalCrystalsLabel.text = total > 0 ? $"Toplam altın: {total}" : "";

            for (int i = 0; i < levelButtons.Length && i < LevelCatalog.Count; i++)
            {
                bool unlocked = GameProgress.IsUnlocked(i);
                levelButtons[i].interactable = unlocked;
                string status = !unlocked ? "Kilitli"
                    : GameProgress.IsCompleted(i) ? $"Bitti  -  {GameProgress.BestCrystals(i)} altın"
                    : "Yeni";
                levelLabels[i].text = $"{LevelCatalog.Levels[i].Title}\n<size=11>{status}</size>";
            }

            resetArmed = false;
            resetLabel.text = "İlerlemeyi Sıfırla";
        }

        // İlk tıklamada onay ister, ikincide siler.
        void OnResetClicked()
        {
            if (!resetArmed)
            {
                resetArmed = true;
                resetLabel.text = "Emin misin? Tekrar bas";
                return;
            }
            GameProgress.ResetProgress();
            Refresh();
            resetLabel.text = "Sıfırlandı";
        }
    }
}
