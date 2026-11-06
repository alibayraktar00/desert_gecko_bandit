namespace DesertGecko.Core
{
    /// <summary>Oyundaki levellerin sırası, sahne adları ve görünen adları.</summary>
    public static class LevelCatalog
    {
        public const string MainMenuScene = "MainMenu";

        public readonly struct LevelInfo
        {
            public readonly string Scene;
            public readonly string Title;
            public readonly string Subtitle;

            public LevelInfo(string scene, string title, string subtitle)
            {
                Scene = scene;
                Title = title;
                Subtitle = subtitle;
            }
        }

        public static readonly LevelInfo[] Levels =
        {
            new LevelInfo("Level_1", "1  Kızgın Kum Vadisi", "Kızgın Kum Vadisi"),
            new LevelInfo("Level_2", "2  Terk Edilmiş Maden", "Kızgın Kum Vadisi"),
        };

        public static int Count => Levels.Length;

        /// <returns>Sahne bir level değilse -1.</returns>
        public static int IndexOf(string sceneName)
        {
            for (int i = 0; i < Levels.Length; i++)
                if (Levels[i].Scene == sceneName)
                    return i;
            return -1;
        }
    }
}
