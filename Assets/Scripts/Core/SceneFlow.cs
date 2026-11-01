using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesertGecko.Core
{
    /// <summary>Sahneler arası geçiş. Her geçişte duraklatma (timeScale) sıfırlanır.</summary>
    public static class SceneFlow
    {
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
