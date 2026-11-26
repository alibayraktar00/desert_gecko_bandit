using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesertGecko.Core
{
    /// <summary>Sahneler arası geçiş. Her geçişte duraklatma (timeScale) sıfırlanır.</summary>
    public static class SceneFlow
    {
        /// <summary>Unity'nin PlayMode test koşucusunun geçici sahnesi açıksa true.</summary>
        public static bool IsTestRunnerScene => SceneManager.GetActiveScene().name.StartsWith("InitTestScene");

        /// <summary>
        /// Oyun başladığında aktif sahne MainMenu değilse otomatik olarak MainMenu'ye yönlendirir.
        /// Böylece oyun her zaman ana menüden başlar — editörde hangi sahne açık olursa olsun.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootstrapToMenu()
        {
            string current = SceneManager.GetActiveScene().name;
            if (IsTestRunnerScene)
                return; // test koşucusunu menüye yönlendirirsek testler hiç başlamaz

            if (current != LevelCatalog.MainMenuScene)
            {
                Debug.Log($"[SceneFlow] Oyun '{current}' sahnesinde başladı, MainMenu'ye yönlendiriliyor.");
                Load(LevelCatalog.MainMenuScene);
            }
        }

        public static void LoadLevel(int index)
        {
            if (index < 0 || index >= LevelCatalog.Count)
            {
                LoadMainMenu();
                return;
            }
            GameProgress.LastPlayed = index;
            Load(LevelCatalog.Levels[index].Scene);
        }

        /// <summary>
        /// Bölümü arka planda yüklemeye başlar ama etkinleştirmez; geçiş efekti doğru anda
        /// allowSceneActivation = true diyerek sahneye geçer. Böylece yükleme sırasında ekran donmaz.
        /// Geçersiz index'te ana menüyü yükler.
        /// </summary>
        public static AsyncOperation BeginLevelLoad(int index)
        {
            string scene = LevelCatalog.MainMenuScene;
            if (index >= 0 && index < LevelCatalog.Count)
            {
                GameProgress.LastPlayed = index;
                scene = LevelCatalog.Levels[index].Scene;
            }
            var op = SceneManager.LoadSceneAsync(scene);
            if (op != null)
                op.allowSceneActivation = false;
            return op;
        }

        public static void LoadMainMenu() => Load(LevelCatalog.MainMenuScene);

        public static void RestartCurrent() => Load(SceneManager.GetActiveScene().name);

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Load(string scene)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }
    }
}
