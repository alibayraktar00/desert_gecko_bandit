using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesertGecko.Core
{
    /// <summary>Sahneler arası geçiş. Her geçişte duraklatma (timeScale) sıfırlanır.</summary>
    public static class SceneFlow
    {
        /// <summary>
        /// Oyun başladığında aktif sahne MainMenu değilse otomatik olarak MainMenu'ye yönlendirir.
        /// Böylece oyun her zaman ana menüden başlar — editörde hangi sahne açık olursa olsun.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootstrapToMenu()
        {
            string current = SceneManager.GetActiveScene().name;
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
