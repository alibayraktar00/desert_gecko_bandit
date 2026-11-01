using System.Linq;
using System.Text.RegularExpressions;
using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Giriş ekranını kurar: arka planda çöl manzarası ve yerinde duran Gila, solda başlık,
    /// sağda menü (Başla/Devam, Level Seç, Ayarlar, Çıkış). LevelBuilder çağırır.
    /// </summary>
    public static class MainMenuBuilder
    {
        internal static void Build(TestSceneBuilder.SceneContext ctx)
        {
            var scene = TestSceneBuilder.NewSceneWithLight();
            Camera cam = TestSceneBuilder.CreateCamera(new Vector2(0f, 1.5f), null, out _);
            BackgroundBuilder.Build();

            // Manzara: zemin, kaktüsler, sahnede duran Gila
            var level = new GameObject("Manzara").transform;
            TestSceneBuilder.Ground(level, "Zemin", -14f, 14f, -12f, 0f, ctx.Tile, ctx.GroundLayer);
            TestSceneBuilder.PropDecor(level, "kaktus_00", -8.5f, 0f, ctx.Tile);
            TestSceneBuilder.PropDecor(level, "kaktus_21", -2.5f, 0f, ctx.Tile);
            TestSceneBuilder.PropDecor(level, "kaktus_16", 8.5f, 0f, ctx.Tile);
            BuildGila(new Vector2(-5.5f, 0f));

            Canvas canvas = UiKit.CreateCanvas("Menu", cam, 100);
            Transform root = canvas.transform;

            // Başlık (sol üst)
            UiKit.Shadowed(UiKit.Label("Baslik", root, "DESERT GECKO\nBANDIT", new Vector2(-150f, 105f), new Vector2(300f, 90f), 34, UiKit.ButtonHighlight))
                .fontStyle = FontStyle.Bold;
            UiKit.Shadowed(UiKit.Label("AltBaslik", root, "Gila'nın adını temize çıkar!", new Vector2(-150f, 52f), new Vector2(300f, 20f), 14, UiKit.LightText));

            // Ana panel (sağ)
            RectTransform main = UiKit.Panel("AnaPanel", root, new Vector2(165f, -10f), new Vector2(210f, 196f));
            Button play = UiKit.Button("Basla", main, "Başla", new Vector2(0f, 68f), new Vector2(170f, 32f), out Text playLabel, 18);
            Button levels = UiKit.Button("LevelSec", main, "Level Seç", new Vector2(0f, 28f), new Vector2(170f, 32f), out _);
            Button settings = UiKit.Button("Ayarlar", main, "Ayarlar", new Vector2(0f, -12f), new Vector2(170f, 32f), out _);
            Button quit = UiKit.Button("Cikis", main, "Çıkış", new Vector2(0f, -52f), new Vector2(170f, 32f), out _);
            Text total = UiKit.Label("ToplamKristal", main, "", new Vector2(0f, -80f), new Vector2(190f, 18f), 13, UiKit.LightText);

            // Level seçimi
            RectTransform levelsPanel = UiKit.Panel("LevelPaneli", root, new Vector2(140f, -10f), new Vector2(270f, 230f));
            UiKit.Shadowed(UiKit.Label("Baslik", levelsPanel, "Kızgın Kum Vadisi", new Vector2(0f, 92f), new Vector2(250f, 24f), 18, UiKit.ButtonHighlight));
            var levelButtons = new Button[LevelCatalog.Count];
            var levelLabels = new Text[LevelCatalog.Count];
            for (int i = 0; i < LevelCatalog.Count; i++)
            {
                levelButtons[i] = UiKit.Button($"Level_{i + 1}", levelsPanel, LevelCatalog.Levels[i].Title,
                    new Vector2(0f, 48f - i * 48f), new Vector2(230f, 40f), out levelLabels[i], 15);
            }
            Button levelsBack = UiKit.Button("Geri", levelsPanel, "Geri", new Vector2(0f, -92f), new Vector2(120f, 28f), out _);

            // Ayarlar
            RectTransform settingsPanel = UiKit.Panel("AyarPaneli", root, new Vector2(150f, -10f), new Vector2(250f, 220f));
            UiKit.Shadowed(UiKit.Label("Baslik", settingsPanel, "Ayarlar", new Vector2(0f, 86f), new Vector2(230f, 24f), 18, UiKit.ButtonHighlight));
            UiKit.Label("SesYazi", settingsPanel, "Ses", new Vector2(-85f, 50f), new Vector2(50f, 20f), 14, UiKit.LightText, TextAnchor.MiddleLeft);
            Slider volume = UiKit.Slider("Ses", settingsPanel, new Vector2(20f, 50f), new Vector2(150f, 16f));
            Toggle fullscreen = UiKit.Toggle("TamEkran", settingsPanel, "Tam ekran", new Vector2(0f, 14f), new Vector2(200f, 22f));
            Button reset = UiKit.Button("Sifirla", settingsPanel, "İlerlemeyi Sıfırla", new Vector2(0f, -30f), new Vector2(200f, 30f), out Text resetLabel, 14);
            Button settingsBack = UiKit.Button("Geri", settingsPanel, "Geri", new Vector2(0f, -78f), new Vector2(120f, 28f), out _);

            var menu = canvas.gameObject.AddComponent<MainMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("mainPanel").objectReferenceValue = main.gameObject;
            so.FindProperty("levelsPanel").objectReferenceValue = levelsPanel.gameObject;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel.gameObject;
            so.FindProperty("playButton").objectReferenceValue = play;
            so.FindProperty("playLabel").objectReferenceValue = playLabel;
            so.FindProperty("levelsButton").objectReferenceValue = levels;
            so.FindProperty("settingsButton").objectReferenceValue = settings;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("totalCrystalsLabel").objectReferenceValue = total;
            SetArray(so.FindProperty("levelButtons"), levelButtons);
            SetArray(so.FindProperty("levelLabels"), levelLabels);
            so.FindProperty("levelsBackButton").objectReferenceValue = levelsBack;
            so.FindProperty("volumeSlider").objectReferenceValue = volume;
            so.FindProperty("fullscreenToggle").objectReferenceValue = fullscreen;
            so.FindProperty("resetButton").objectReferenceValue = reset;
            so.FindProperty("resetLabel").objectReferenceValue = resetLabel;
            so.FindProperty("settingsBackButton").objectReferenceValue = settingsBack;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Ekran görüntüsünde ana panel görünsün; diğerleri oyunda MainMenu tarafından açılır.
            levelsPanel.gameObject.SetActive(false);
            settingsPanel.gameObject.SetActive(false);

            UiKit.CreateEventSystem();
            EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{LevelCatalog.MainMenuScene}.unity");
            Debug.Log("[DesertGecko] Ana menü kuruldu.");
        }

        // Menüde yerinde nefes alan Gila (idle kareleri), iki kat büyük.
        static void BuildGila(Vector2 position)
        {
            const string folder = SpriteImportRules.GilaFolder + "idle";
            if (!AssetDatabase.IsValidFolder(folder))
                return;
            Sprite[] frames = AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Regex.IsMatch(p, @"_\d+\.png$")) // kalıba uymayan dosya kurulumu kırmasın
                .OrderBy(p => int.Parse(Regex.Match(p, @"_(\d+)\.png$").Groups[1].Value))
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .ToArray();
            if (frames.Length == 0)
                return;

            var gila = new GameObject("Gila");
            gila.transform.position = position;
            gila.transform.localScale = new Vector3(2f, 2f, 1f);
            var sr = gila.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = 10;
            var loop = gila.AddComponent<SpriteLoop>();
            var so = new SerializedObject(loop);
            SetArray(so.FindProperty("frames"), frames);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(SerializedProperty prop, Object[] values)
        {
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
