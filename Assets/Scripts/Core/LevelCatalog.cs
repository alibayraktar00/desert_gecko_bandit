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
            new LevelInfo("Level_1-1", "1-1  İlk Adımlar", "Kızgın Kum Vadisi"),
            new LevelInfo("Level_1-2", "1-2  Kanyon Duvarları", "Kızgın Kum Vadisi"),
            new LevelInfo("Level_1-3", "1-3  Akbaba Gölgesi", "Kızgın Kum Vadisi"),
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
