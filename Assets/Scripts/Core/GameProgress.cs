using UnityEngine;

namespace DesertGecko.Core
{
    /// <summary>
    /// Oyuncunun ilerlemesi ve ayarları (PlayerPrefs'te saklanır): açılan leveller,
    /// her levelde toplanan en çok kristal, ses seviyesi, tam ekran.
    /// </summary>
    public static class GameProgress
    {
        const string DefaultPrefix = "DGB_";

        /// <summary>Testler gerçek kaydı bozmasın diye kendi önekini kullanır.</summary>
        public static string KeyPrefix = DefaultPrefix;

        // Domain reload kapalı: yarıda kesilen bir test öneki sonraki Play'e taşımasın.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPrefix() => KeyPrefix = DefaultPrefix;

        static string Key(string name) => KeyPrefix + name;

        /// <summary>Oynanabilir en yüksek level (0 tabanlı). İlk level hep açık.</summary>
        public static int HighestUnlocked => Mathf.Clamp(PlayerPrefs.GetInt(Key("unlocked"), 0), 0, LevelCatalog.Count - 1);

        /// <summary>En son girilen level; "Devam et" buraya götürür.</summary>
        public static int LastPlayed
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(Key("last"), 0), 0, HighestUnlocked);
            set { PlayerPrefs.SetInt(Key("last"), value); PlayerPrefs.Save(); }
        }

        public static bool HasProgress => PlayerPrefs.HasKey(Key("last"));

        public static bool IsUnlocked(int level) => level >= 0 && level <= HighestUnlocked;
        public static bool IsCompleted(int level) => PlayerPrefs.GetInt(Key($"done_{level}"), 0) == 1;
        public static int BestCrystals(int level) => PlayerPrefs.GetInt(Key($"crystals_{level}"), 0);

        public static int TotalCrystals
        {
            get
            {
                int total = 0;
                for (int i = 0; i < LevelCatalog.Count; i++)
                    total += BestCrystals(i);
                return total;
            }
        }

        /// <summary>Level bitti: sonraki leveli aç, kristal rekorunu güncelle.</summary>
        public static void CompleteLevel(int level, int crystals)
        {
            if (level < 0 || level >= LevelCatalog.Count)
                return;
            PlayerPrefs.SetInt(Key($"done_{level}"), 1);
            if (crystals > BestCrystals(level))
                PlayerPrefs.SetInt(Key($"crystals_{level}"), crystals);
            int next = Mathf.Min(level + 1, LevelCatalog.Count - 1);
            if (next > HighestUnlocked)
                PlayerPrefs.SetInt(Key("unlocked"), next);
            PlayerPrefs.SetInt(Key("last"), next);
            PlayerPrefs.Save();
        }

        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(Key("unlocked"));
            PlayerPrefs.DeleteKey(Key("last"));
            for (int i = 0; i < LevelCatalog.Count; i++)
            {
                PlayerPrefs.DeleteKey(Key($"done_{i}"));
                PlayerPrefs.DeleteKey(Key($"crystals_{i}"));
            }
            PlayerPrefs.Save();
        }

        // --- Ayarlar ---

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(Key("volume"), 0.8f);
            set
            {
                PlayerPrefs.SetFloat(Key("volume"), Mathf.Clamp01(value));
                PlayerPrefs.Save();
                AudioListener.volume = Mathf.Clamp01(value);
            }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(Key("fullscreen"), 0) == 1;
            set
            {
                PlayerPrefs.SetInt(Key("fullscreen"), value ? 1 : 0);
                PlayerPrefs.Save();
                ApplyScreenMode(value);
            }
        }

        static void ApplyScreenMode(bool fullscreen)
        {
            if (Application.isEditor)
                return; // editörde Game penceresini bozmasın
            Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySavedSettings()
        {
            AudioListener.volume = Volume;
            ApplyScreenMode(Fullscreen);
        }
    }
}
