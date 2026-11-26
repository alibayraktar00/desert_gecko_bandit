using System;
using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.UI
{
    /// <summary>
    /// Giriş ekranı: bütün menü revolver tamburunun üstünde (RevolverMenu). Sayfalar tamburun yazılarını değiştirir:
    ///   Ana sayfa : Başla/Devam Et, Level Seç, Ayarlar, Çıkış
    ///   Leveller  : Level 1-4, Geri (açık level seçilince silah ateşlenir ve level yüklenir)
    ///   Ayarlar   : Ses, Tam Ekran, Sıfırla, Geri
    ///   Ses       : tambur çevrildikçe ses seviyesi artar/azalır, en üstteki göstergede görünür
    /// Yalnızca oyunu başlatan seçimler ateş eder; diğerleri tıkırtıyla hemen açılır. En üstte seçili seçeneğin
    /// açıklaması (level adı ve durumu, ses yüzdesi, tam ekran) yazar.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] RevolverMenu revolver;
        [SerializeField] Text totalCrystalsLabel;
        [Tooltip("Ateşten sonra ekranı kaplayan toz; level ekran kapalıyken yüklenir (yoksa doğrudan yüklenir)")]
        [SerializeField] DustTransition dust;

        [Header("Üst bilgi")]
        [Tooltip("Seçili seçeneğin açıklaması")]
        [SerializeField] Text infoLabel;
        [Tooltip("Ses göstergesi (Ayarlar ve ses modunda görünür)")]
        [SerializeField] GameObject volumeRoot;
        [SerializeField] Image[] volumeSegments = Array.Empty<Image>();
        [SerializeField] Color volumeOn = new Color32(0xF4, 0xD3, 0x5E, 0xFF);
        [SerializeField] Color volumeOff = new Color32(0x3C, 0x2A, 0x22, 0xFF);

        string[] infos = Array.Empty<string>();
        bool resetArmed;

        void Awake()
        {
            revolver.SelectionChanged += ShowInfo;
        }

        void Start()
        {
            Time.timeScale = 1f;
            ShowMain();
        }

        void SetPage(string[] texts, Action[] actions, bool[] fire, Action back, string[] pageInfos, bool showVolume)
        {
            infos = pageInfos;
            resetArmed = false;
            if (volumeRoot != null)
                volumeRoot.SetActive(showVolume);
            RefreshVolume();
            int total = GameProgress.TotalCrystals;
            totalCrystalsLabel.text = total > 0 ? $"Toplam altın: {total}" : "";
            revolver.SetPage(texts, actions, fire, back);
        }

        void ShowInfo(int chamber)
        {
            if (infoLabel != null)
                infoLabel.text = chamber >= 0 && chamber < infos.Length ? infos[chamber] : "";
        }

        void ShowMain()
        {
            bool progress = GameProgress.HasProgress;
            string next = LevelCatalog.Levels[GameProgress.HighestUnlocked].Title;
            SetPage(
                new[] { progress ? "Devam Et" : "Başla", "Level Seç", "Ayarlar", "Çıkış" },
                new Action[] { () => StartLevel(GameProgress.HighestUnlocked), ShowLevels, ShowSettings, SceneFlow.Quit },
                new[] { true, false, false, false },
                null,
                new[] { progress ? $"Kaldığın yer: {next}" : "Gila'nın adını temize çıkar!", "Bölüm seç", "Ses, ekran ve ilerleme", "Oyundan çık" },
                showVolume: false);
        }

        void ShowLevels()
        {
            int n = Mathf.Min(LevelCatalog.Count, RevolverMenu.Chambers - 1);
            var texts = new string[n + 1];
            var actions = new Action[n + 1];
            var fire = new bool[n + 1];
            var pageInfos = new string[n + 1];
            for (int i = 0; i < n; i++)
            {
                int level = i;
                bool unlocked = GameProgress.IsUnlocked(i);
                texts[i] = $"Level {i + 1}";
                fire[i] = unlocked;
                actions[i] = unlocked ? () => StartLevel(level) : () => { };
                string status = !unlocked ? "Kilitli"
                    : GameProgress.IsCompleted(i) ? $"Bitti - {GameProgress.BestCrystals(i)} altın"
                    : "Yeni";
                pageInfos[i] = $"{LevelCatalog.Levels[i].Title}  ({status})";
            }
            texts[n] = "Geri";
            actions[n] = ShowMain;
            pageInfos[n] = "Ana menüye dön";
            SetPage(texts, actions, fire, ShowMain, pageInfos, showVolume: false);
        }

        void ShowSettings()
        {
            SetPage(
                new[] { "Ses", "Tam Ekran", "Sıfırla", "Geri" },
                new Action[] { EnterVolume, ToggleFullscreen, ResetProgress, ShowMain },
                null,
                ShowMain,
                new[] { VolumeInfo(), FullscreenInfo(), "İlerlemeyi sıfırla", "Ana menüye dön" },
                showVolume: true);
        }

        // Ses modu: tamburun bütün haznelerinde "SES" yazar, her dönüş sesi %10 değiştirir.
        void EnterVolume()
        {
            infos = new[] { "Tamburu çevir: ses  |  Onayla: tamam" };
            ShowInfo(0);
            revolver.SetAdjust("Ses", dir =>
            {
                GameProgress.Volume = Mathf.Clamp01(Mathf.Round((GameProgress.Volume - dir * 0.1f) * 10f) / 10f);
                RefreshVolume();
            }, ShowSettings);
        }

        void ToggleFullscreen()
        {
            GameProgress.Fullscreen = !GameProgress.Fullscreen;
            infos[1] = FullscreenInfo();
            ShowInfo(1);
        }

        // İlk seçimde onay ister, ikincide siler.
        void ResetProgress()
        {
            if (!resetArmed)
            {
                resetArmed = true;
                infos[2] = "Emin misin? Tekrar seç";
                ShowInfo(2);
                return;
            }
            GameProgress.ResetProgress();
            resetArmed = false;
            infos[2] = "İlerleme sıfırlandı";
            ShowInfo(2);
        }

        void StartLevel(int level)
        {
            if (dust != null)
                dust.Play(() => SceneFlow.BeginLevelLoad(level));
            else
                SceneFlow.LoadLevel(level);
        }

        static string VolumeInfo() => $"Ses: %{Mathf.RoundToInt(GameProgress.Volume * 100f)}";
        static string FullscreenInfo() => GameProgress.Fullscreen ? "Tam ekran: Açık" : "Tam ekran: Kapalı";

        void RefreshVolume()
        {
            int lit = Mathf.RoundToInt(GameProgress.Volume * volumeSegments.Length);
            for (int i = 0; i < volumeSegments.Length; i++)
                if (volumeSegments[i] != null)
                    volumeSegments[i].color = i < lit ? volumeOn : volumeOff;
            if (infos.Length > 0 && infos[0] != null && infos[0].StartsWith("Ses:"))
                infos[0] = VolumeInfo();
        }
    }
}
