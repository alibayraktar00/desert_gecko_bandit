using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertGecko.Core;
using DesertGecko.Environment;
using DesertGecko.Levels;
using DesertGecko.Player;
using DesertGecko.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Kızgın Kum Vadisi'nin 3 levelini koddan kurar: zemin, duvarlar, platformlar, düşmanlar,
    /// toplanabilirler, tabelalar, bitiş, HUD, duraklatma ve level sonu menüleri.
    /// Menü: Desert Gecko → Levelleri Kur.
    ///
    /// DİKKAT: Kurulum sahneyi baştan yazar. Bir level sahnede elle düzenlendiyse
    /// yeniden kurmadan önce o düzenlemeleri buradaki level tanımına taşıyın.
    /// </summary>
    public static class LevelBuilder
    {
        const string SceneFolder = "Assets/Scenes";

        // Gila'nın hareket menzilleri (PlayerController varsayılanlarından, biraz pay bırakılarak).
        const float SingleJumpRise = 2.5f, SingleJumpGap = 4f;
        const float DoubleJumpRise = 4.5f, DoubleJumpGap = 6.5f;
        // Süzülüş: yatay 6.5, düşüş ~2.14 birim/sn (1.8 sınırı + o fizik adımındaki yerçekimi). Bot ölçümü.
        const float GlideRatio = 6.5f / 2.14f;
        const float GlideRunPerDrop = GlideRatio * 0.85f;    // menzil kontrolünde payla
        const float JumpApexRun = 3.5f, JumpApexRise = 3.2f; // tek zıplamada tepe noktasına kadar
        const float MaxApexRun = 5f, MaxApexRise = 5f;       // çift zıplamada tepe noktası (en uzun süzülüş)
        const float ClingGap = 6f;                          // atlayıp karşı duvara yapışabileceği boşluk

        // Zeminlerin alt sınırı. Oyuncu killY'ye (-14) düşerken kamera ~-20'ye kadar görür;
        // zemin daha yukarıda bitseydi uçurum kenarlarının altında gökyüzü görünürdü.
        const float GroundBottom = -24f;

        [MenuItem("Desert Gecko/Levelleri Kur")]
        public static void BuildAllMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[DesertGecko] Play modunda level kurulamaz.");
                return;
            }
            // Açık sahnedeki kaydedilmemiş değişiklikler sessizce kaybolmasın.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildAll(null);
        }

        /// <summary>
        /// Komut satırı: ana menüyü ve 3 leveli kurar, her birinden ekran görüntüsü alır.
        /// -executeMethod DesertGecko.EditorTools.LevelBuilder.BuildAllAndCaptureBatch -shotDir klasör
        /// </summary>
        public static void BuildAllAndCaptureBatch()
        {
            string shotDir = SceneCapture.ArgumentValue("-shotDir") ?? "Temp/Screenshots";
            int warnings = BuildAll(shotDir);
            EditorApplication.Exit(warnings == 0 ? 0 : 2);
        }

        /// <returns>Level tasarım uyarısı sayısı.</returns>
        static int BuildAll(string shotDir)
        {
            TestSceneBuilder.SceneContext ctx = TestSceneBuilder.Prepare();
            int warnings = 0;

            MainMenuBuilder.Build(ctx);
            if (shotDir != null)
                SceneCapture.Capture(Path.Combine(shotDir, "menu.png"), new Vector3(0f, 1.5f, -10f));

            var definitions = new System.Action<LevelPainter>[] { Level_1_1, Level_1_2, Level_1_3 };
            for (int i = 0; i < definitions.Length; i++)
            {
                LevelCatalog.LevelInfo info = LevelCatalog.Levels[i];
                var painter = BuildLevel(ctx, info, definitions[i]);
                warnings += painter.Validate(info.Title);

                if (shotDir != null)
                    foreach (Vector2 cam in painter.ShotPoints)
                        SceneCapture.Capture(Path.Combine(shotDir, $"{info.Scene}_{cam.x:0}.png"), new Vector3(cam.x, cam.y, -10f));
            }

            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[DesertGecko] Menü ve {definitions.Length} level kuruldu, {warnings} tasarım uyarısı.");
            return warnings;
        }

        static LevelPainter BuildLevel(TestSceneBuilder.SceneContext ctx, LevelCatalog.LevelInfo info, System.Action<LevelPainter> define)
        {
            var scene = TestSceneBuilder.NewSceneWithLight();
            var painter = new LevelPainter(ctx);
            define(painter);
            painter.FillPits();

            GameObject player = TestSceneBuilder.SpawnPlayer(ctx, painter.Spawn);
            Camera cam = TestSceneBuilder.CreateCamera(painter.Spawn + new Vector2(0f, 1.5f), player.transform, out CameraFollow follow);
            CombatSetupBuilder.BuildHud(cam, player, ctx.Combat);
            BackgroundBuilder.Build();

            Canvas menus = UiKit.CreateCanvas("Menuler", cam, 200);
            LevelCompleteUI complete = BuildCompletePanel(menus.transform);
            BuildPauseMenu(menus.transform, player.GetComponent<PlayerInputReader>());
            UiKit.CreateEventSystem();

            var manager = new GameObject("LevelManager").AddComponent<LevelManager>();
            TestSceneBuilder.SetField(manager, "player", player.GetComponent<PlayerController>());
            TestSceneBuilder.SetField(manager, "cameraFollow", follow);
            TestSceneBuilder.SetField(manager, "completeUI", complete);

            string path = $"{SceneFolder}/{info.Scene}.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[DesertGecko] Level kuruldu: " + path);
            return painter;
        }

        static void SetBuildScenes()
        {
            var paths = new List<string> { $"{SceneFolder}/{LevelCatalog.MainMenuScene}.unity" };
            paths.AddRange(LevelCatalog.Levels.Select(l => $"{SceneFolder}/{l.Scene}.unity"));
            // Test sahnesi listede kalsın ama oyunun sürümüne girmesin.
            string testScene = $"{SceneFolder}/TestLevel_Movement.unity";
            paths.Add(testScene);
            EditorBuildSettings.scenes = paths
                .Where(p => File.Exists(p))
                .Select(p => new EditorBuildSettingsScene(p, p != testScene))
                .ToArray();
        }

        // =====================================================================
        // Level tanımları. Birim = 1 tile = 32 piksel; y = zeminin üst yüzeyi.
        // Gila: tek zıplama ~3 birim yükseğe / ~6 birim öteye, çift zıplama ~5 birim yükseğe,
        // süzülürken her 1 birim düşüşte ~3 birim ilerler; her duvara tırmanabilir.
        // =====================================================================

        // 1-1 İlk Adımlar: her mekaniği tabelayla tek tek öğreten eğitim leveli.
        static void Level_1_1(LevelPainter p)
        {
            p.Spawn = new Vector2(0f, 0f);
            p.Boundary(-9f);

            // Koşma
            p.Ground(-8f, 16f, 0f);
            p.Sign(3f, 0f, "A / D  ya da oklar: koş");
            p.Plant("kaktus_00", -4f, 0f);
            p.Plant("kaktus_21", 9.5f, 0f);
            p.CrystalLine(6f, 0.7f, 3, 1f);

            // Zıplama
            p.Sign(13f, 0f, "Space: zıpla\nBoşluğun üstünden atla");
            p.Ground(19f, 34f, 0f);
            p.Crystal(17.5f, 2.6f);
            p.Plant("kaktus_12", 24f, 0f);

            // Basamak
            p.Sign(30f, 0f, "Yüksek kenarlara zıpla");
            p.Ground(34f, 46f, 2f);
            p.Plant("kaktus_22", 40f, 2f);

            // Çift zıplama
            p.Sign(42.5f, 2f, "Havadayken tekrar Space:\nçift zıplama (kuyruk itişi)");
            p.Ground(46f, 62f, 6f);
            p.Crystal(45f, 5.4f);
            p.Checkpoint(49f, 6f);

            // Dil: böcek ve ilk akrep
            p.Sign(53f, 6f, "J: dil saldırısı\nBöcek yakala, can yenile");
            p.Bug(57f, 7.3f);
            p.Ground(62f, 76f, 6f);
            p.Sign(64f, 6f, "Akrebe J ile 3 kez vur\nDokunursa can yakar!");
            p.Scorpion(71f, 6f);
            p.Plant("kaktus_16", 68f, 6f);

            // Duvar tırmanma
            p.Sign(73.5f, 6f, "Duvara doğru bas: yapış\nW / S: tırman");
            p.Ground(76f, 92f, 12f);
            p.CrystalLine(75.4f, 8f, 3, 1.3f, vertical: true);
            p.Plant("kaktus_03", 84f, 12f);

            // Süzülme
            // Karşı taraf sadece 2 birim alçakta: süzülüşle levelin geri kalanı atlanamaz.
            p.Sign(88f, 12f, "Uçurumdan atla, düşerken\nSpace basılı tut: süzül");
            p.CrystalArc(96f, 16.2f, 106f, 12.8f, 5); // zıplayıp süzülenin yolu
            p.Ground(104f, 136f, 10f);
            p.Checkpoint(108f, 10f);

            // Kuyruk
            p.Sign(114f, 10f, "K: kuyruk fırlat. Kuyruk\nçıkana kadar süzülemezsin!");
            p.Bug(118f, 11.3f);
            p.Scorpion(124f, 10f);
            p.Scorpion(129f, 10f);
            p.Plant("kaktus_01", 121f, 10f);
            p.Goal(133f, 10f);
            p.Boundary(136f);

            p.ShotPoints.AddRange(new[] { new Vector2(6f, 1.5f), new Vector2(50f, 7.5f), new Vector2(99f, 13f), new Vector2(126f, 11.5f) });
        }

        // 1-2 Kanyon Duvarları: platformlar, tırmanma, yüksek kanyon ve uzun süzülüş.
        static void Level_1_2(LevelPainter p)
        {
            p.Spawn = new Vector2(0f, 0f);
            p.Boundary(-9f);

            p.Ground(-8f, 18f, 0f);
            p.Scorpion(10f, 0f);
            p.CrystalLine(4f, 0.7f, 3, 1f);
            p.Plant("kaktus_02", -3f, 0f);
            p.Plant("kaktus_19", 8f, 0f);

            // Halatlı platformlarla uçurum geçişi
            p.Platform(22f, 2f, "platform_03");
            p.Platform(27.5f, 3.5f, "platform_00");
            p.Platform(33f, 2.5f, "platform_02");
            p.Crystal(22f, 2.8f);
            p.Crystal(27.5f, 4.3f);
            p.Crystal(33f, 3.3f);

            p.Ground(37f, 59f, 1f);
            p.Checkpoint(39f, 1f);
            p.Scorpion(48f, 1f);
            p.Bug(44f, 2.4f);
            p.Plant("kaktus_25", 54f, 1f);

            // Kanyon duvarı: 11 birim tırmanış
            p.Sign(56f, 1f, "Kanyonun tepesine tırman");
            p.Ground(59f, 82f, 12f);
            p.CrystalLine(58.4f, 4f, 4, 2f, vertical: true);
            p.Checkpoint(62f, 12f);
            p.Scorpion(68f, 12f);
            p.Scorpion(76f, 12f);
            p.Plant("kaktus_05", 72f, 12f);
            p.Platform(71f, 15.5f, "platform_04");
            p.CrystalLine(69.5f, 16.3f, 4, 1f);

            // Kanyonun üstünde biraz daha yüksek bir tepe (aşağı inilseydi süzülüşle bölüm atlanırdı)
            p.Ground(86f, 102f, 14f);
            p.Scorpion(90f, 14f);
            p.Table(96f, 14f, "platform_10");
            p.Crystal(96f, 16f);

            // Uzun süzülüş: karşı taraf neredeyse aynı yükseklikte, süzülmeden geçilmez
            p.Sign(99.5f, 14f, "Uzun bir süzülüş!\nSpace basılı tut");
            p.CrystalArc(106f, 18.2f, 114f, 15.4f, 4);
            p.Ground(116f, 152f, 13f);
            p.Checkpoint(119f, 13f);
            p.Scorpion(126f, 13f);
            p.Scorpion(134f, 13f);
            p.Bug(130f, 14.3f);
            p.Platform(138f, 16f, "platform_05");
            p.CrystalLine(136.5f, 16.8f, 4, 1f);
            p.Scorpion(142f, 13f);
            p.Plant("kaktus_28", 145f, 13f);
            p.Goal(148f, 13f);
            p.Boundary(152f);

            p.ShotPoints.AddRange(new[] { new Vector2(26f, 2.5f), new Vector2(62f, 10f), new Vector2(104f, 15f), new Vector2(140f, 15f) });
        }

        // 1-3 Akbaba Gölgesi: dar platformlar, iki uzun süzülüş, yoğun akrep sürüsü.
        static void Level_1_3(LevelPainter p)
        {
            p.Spawn = new Vector2(0f, 0f);
            p.Boundary(-9f);

            p.Ground(-8f, 10f, 0f);
            p.Plant("kaktus_00", -4f, 0f);

            // Dar platformlar
            p.Platform(14f, 2.5f, "platform_01");
            p.Platform(18.5f, 4f, "platform_01");
            p.Crystal(14f, 3.3f);
            p.Crystal(18.5f, 4.8f);

            p.Ground(22f, 34f, 3f);
            p.Checkpoint(24f, 3f);
            p.Scorpion(30f, 3f);

            // Tırmanış ve ilk büyük süzülüş (akrep uçurum kenarından uzakta)
            p.Ground(34f, 52f, 7f);
            p.Scorpion(40f, 7f);
            p.Bug(45f, 8.3f);
            p.Plant("kaktus_06", 48f, 7f);
            p.CrystalArc(56f, 11.2f, 68f, 7.1f, 5);
            p.Ground(70f, 90f, 4f);
            p.Checkpoint(72f, 4f);
            p.Scorpion(76f, 4f);
            p.Scorpion(82f, 4f);

            // Çift zıplamalık boşluk
            p.Crystal(93f, 8f);
            p.Ground(96f, 112f, 2f);
            p.Table(103f, 2f, "platform_09");
            p.Scorpion(108f, 2f);

            // İkinci tırmanış, tepede asılı platform
            p.Ground(112f, 128f, 8f);
            p.Platform(120f, 11.5f, "platform_04");
            p.CrystalLine(118.5f, 12.3f, 4, 1f);
            p.Scorpion(117f, 8f);

            // Son süzülüş ve akrep sürüsü
            p.CrystalArc(132f, 12.2f, 140f, 9.4f, 4);
            p.Ground(142f, 172f, 6f);
            p.Checkpoint(144f, 6f);
            p.Scorpion(150f, 6f);
            p.Bug(153f, 7.3f);
            p.Scorpion(156f, 6f);
            p.Scorpion(162f, 6f);
            p.Table(159f, 6f, "platform_13");
            p.Plant("kaktus_17", 147f, 6f);
            p.Goal(168f, 6f);
            p.Boundary(172f);

            p.ShotPoints.AddRange(new[] { new Vector2(16f, 3f), new Vector2(60f, 8f), new Vector2(118f, 10f), new Vector2(156f, 7.5f) });
        }

        // =====================================================================
        // Level içi menüler
        // =====================================================================

        static LevelCompleteUI BuildCompletePanel(Transform canvas)
        {
            RectTransform overlay = UiKit.Overlay("LevelSonu", canvas, new Color(0f, 0f, 0f, 0.55f));
            RectTransform box = UiKit.Panel("Kutu", overlay, Vector2.zero, new Vector2(280f, 200f));
            Text title = UiKit.Shadowed(UiKit.Label("Baslik", box, "Level tamamlandı!", new Vector2(0f, 68f), new Vector2(260f, 30f), 22, UiKit.ButtonHighlight));
            Text crystals = UiKit.Label("Kristal", box, "Kristal: 0", new Vector2(0f, 36f), new Vector2(260f, 22f), 15, UiKit.LightText);
            Button next = UiKit.Button("Sonraki", box, "Sonraki Level", new Vector2(0f, -4f), new Vector2(180f, 30f), out _);
            Button retry = UiKit.Button("Tekrar", box, "Tekrar Oyna", new Vector2(0f, -40f), new Vector2(180f, 30f), out _);
            Button menu = UiKit.Button("AnaMenu", box, "Ana Menü", new Vector2(0f, -76f), new Vector2(180f, 30f), out _);

            var ui = canvas.gameObject.AddComponent<LevelCompleteUI>();
            TestSceneBuilder.SetField(ui, "panel", overlay.gameObject);
            TestSceneBuilder.SetField(ui, "titleLabel", title);
            TestSceneBuilder.SetField(ui, "crystalsLabel", crystals);
            TestSceneBuilder.SetField(ui, "nextButton", next);
            TestSceneBuilder.SetField(ui, "retryButton", retry);
            TestSceneBuilder.SetField(ui, "mainMenuButton", menu);
            overlay.gameObject.SetActive(false); // level bitince açılır
            return ui;
        }

        static void BuildPauseMenu(Transform canvas, PlayerInputReader input)
        {
            RectTransform overlay = UiKit.Overlay("Duraklat", canvas, new Color(0f, 0f, 0f, 0.55f));
            RectTransform box = UiKit.Panel("Kutu", overlay, Vector2.zero, new Vector2(240f, 190f));
            UiKit.Shadowed(UiKit.Label("Baslik", box, "Duraklatıldı", new Vector2(0f, 62f), new Vector2(220f, 30f), 22, UiKit.ButtonHighlight));
            Button resume = UiKit.Button("Devam", box, "Devam Et", new Vector2(0f, 18f), new Vector2(170f, 30f), out _);
            Button restart = UiKit.Button("Yeniden", box, "Yeniden Başlat", new Vector2(0f, -18f), new Vector2(170f, 30f), out _);
            Button menu = UiKit.Button("AnaMenu", box, "Ana Menü", new Vector2(0f, -54f), new Vector2(170f, 30f), out _);

            var pause = canvas.gameObject.AddComponent<PauseMenu>();
            TestSceneBuilder.SetField(pause, "panel", overlay.gameObject);
            TestSceneBuilder.SetField(pause, "resumeButton", resume);
            TestSceneBuilder.SetField(pause, "restartButton", restart);
            TestSceneBuilder.SetField(pause, "mainMenuButton", menu);
            TestSceneBuilder.SetField(pause, "playerInput", input);
            overlay.gameObject.SetActive(false); // ESC ile açılır
        }

        // =====================================================================
        // Level çizim aracı
        // =====================================================================

        internal class LevelPainter
        {
            readonly TestSceneBuilder.SceneContext ctx;
            readonly Transform geometry, decor, actors;
            readonly List<(float xMin, float xMax, float top)> grounds = new List<(float, float, float)>();
            // Sadece havadaki platformlar; yerdeki masalar boşluk geçişine yardım etmez.
            readonly List<(float xMin, float xMax, float top)> platforms = new List<(float, float, float)>();
            readonly List<float> checkpoints = new List<float>();
            int count;
            bool hasGoal;

            public Vector2 Spawn;
            public readonly List<Vector2> ShotPoints = new List<Vector2>();

            static Sprite signSprite, poleSprite, flagSprite, pitSprite;

            public LevelPainter(TestSceneBuilder.SceneContext ctx)
            {
                this.ctx = ctx;
                var root = new GameObject("Level").transform;
                geometry = Child(root, "Zemin");
                decor = Child(root, "Dekor");
                actors = new GameObject("Oyun").transform;
                signSprite = signSprite != null ? signSprite : PlaceholderArt.SignBoard();
                poleSprite = poleSprite != null ? poleSprite : PlaceholderArt.GoalPole();
                flagSprite = flagSprite != null ? flagSprite : PlaceholderArt.GoalFlag();
                pitSprite = pitSprite != null ? pitSprite : PlaceholderArt.PitGradient();
            }

            /// <summary>
            /// Zemin parçaları arasındaki her boşluğa yukarıdan aşağı koyulaşan bir karanlık koyar.
            /// Yoksa arka plandaki tepelerin koyu alt kısmı uçurumu zemin gibi gösteriyor.
            /// </summary>
            public void FillPits()
            {
                const float fadeHeight = 1f, bottom = GroundBottom - 4f;
                var sorted = grounds.OrderBy(g => g.xMin).ToList();
                for (int i = 0; i + 1 < sorted.Count; i++)
                {
                    float x0 = sorted[i].xMax, x1 = sorted[i + 1].xMin;
                    if (x1 - x0 <= 0f)
                        continue;
                    float width = x1 - x0, top = Mathf.Min(sorted[i].top, sorted[i + 1].top) - 0.05f;

                    var pit = new GameObject(Name("Ucurum")).transform;
                    pit.SetParent(geometry);
                    pit.position = new Vector3((x0 + x1) / 2f, top, 0f);

                    var fade = new GameObject("Gecis").AddComponent<SpriteRenderer>();
                    fade.transform.SetParent(pit, false);
                    fade.sprite = pitSprite;
                    fade.sortingOrder = -50; // arka planın önünde, dekorun arkasında
                    fade.transform.localScale = new Vector3(width / pitSprite.bounds.size.x, fadeHeight / pitSprite.bounds.size.y, 1f);

                    var solid = new GameObject("Karanlik").AddComponent<SpriteRenderer>();
                    solid.transform.SetParent(pit, false);
                    solid.sprite = ctx.Solid;
                    solid.drawMode = SpriteDrawMode.Tiled;
                    float depth = top - fadeHeight - bottom;
                    solid.size = new Vector2(width, depth);
                    solid.transform.localPosition = new Vector3(0f, -fadeHeight - depth / 2f, 0f);
                    solid.color = new Color32(0x0E, 0x07, 0x05, 0xFF); // arka planın koyu kahvesinden belirgin şekilde koyu
                    solid.sortingOrder = -50;
                }
            }

            static Transform Child(Transform parent, string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent);
                return t;
            }

            string Name(string prefix) => $"{prefix}_{++count}";

            public void Ground(float xMin, float xMax, float top)
            {
                TestSceneBuilder.Ground(geometry, Name("Zemin"), xMin, xMax, GroundBottom, top, ctx.Tile, ctx.GroundLayer);
                grounds.Add((xMin, xMax, top));
            }

            public void Wall(float xMin, float xMax, float yMin, float yMax) =>
                TestSceneBuilder.Block(geometry, Name("Duvar"), xMin, xMax, yMin, yMax, TestSceneBuilder.Rock, ctx.Tile, ctx.GroundLayer);

            /// <summary>
            /// Level kenarı: yüksek, 1 birim kalın duvar. Ground katmanında değil (Default), bu yüzden
            /// Gila çarpar ama yapışıp tırmanamaz, tepeden level dışına atlayamaz.
            /// </summary>
            public void Boundary(float xMin) =>
                TestSceneBuilder.Block(geometry, Name("Kenar"), xMin, xMin + 1f, GroundBottom, 60f, TestSceneBuilder.Rock, ctx.Tile, 0);

            public void Platform(float centerX, float top, string sprite)
            {
                TestSceneBuilder.PropPlatform(geometry, Name("Platform"), centerX, top, sprite, ctx.Tile, ctx.GroundLayer);
                Sprite s = TestSceneBuilder.Prop(sprite);
                float half = s != null ? s.bounds.size.x / 2f : 1.5f;
                platforms.Add((centerX - half, centerX + half, top));
            }

            public void Table(float centerX, float groundTop, string sprite) =>
                TestSceneBuilder.PropPlatform(geometry, Name("Masa"), centerX, groundTop, sprite, ctx.Tile, ctx.GroundLayer, onGround: true);

            public void Plant(string sprite, float x, float groundTop) =>
                TestSceneBuilder.PropDecor(decor, sprite, x, groundTop, ctx.Tile);

            public void Scorpion(float x, float groundTop) => CombatSetupBuilder.Place(ctx.Combat.Scorpion, actors, Name("Akrep"), x, groundTop);
            public void Bug(float x, float y) => CombatSetupBuilder.Place(ctx.Combat.Bug, actors, Name("Bocek"), x, y);
            public void Crystal(float x, float y) => CombatSetupBuilder.Place(ctx.Combat.Crystal, actors, Name("Kristal"), x, y);
            public void Checkpoint(float x, float groundTop)
            {
                checkpoints.Add(x);
                CombatSetupBuilder.Place(ctx.Combat.Checkpoint, actors, Name("Checkpoint"), x, groundTop);
            }

            public void CrystalLine(float x, float y, int n, float spacing, bool vertical = false)
            {
                for (int i = 0; i < n; i++)
                    Crystal(vertical ? x : x + i * spacing, vertical ? y + i * spacing : y);
            }

            /// <summary>Süzülüş yolunu gösteren kristal yayı (iki nokta arasında hafif bükülen çizgi).</summary>
            public void CrystalArc(float x0, float y0, float x1, float y1, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    float t = n == 1 ? 0.5f : (float)i / (n - 1);
                    float sag = Mathf.Sin(t * Mathf.PI) * 0.6f;
                    Crystal(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t) + sag);
                }
            }

            /// <summary>Eğitim tabelası: oyuncu yaklaşınca üstünde yazı belirir.</summary>
            public void Sign(float x, float groundTop, string text)
            {
                var root = new GameObject(Name("Tabela"));
                root.transform.SetParent(decor);
                root.transform.position = new Vector3(x, groundTop, 0f);
                var board = root.AddComponent<SpriteRenderer>();
                board.sprite = signSprite;
                board.sortingOrder = -1;

                var canvasGo = new GameObject("Balon", typeof(RectTransform));
                canvasGo.transform.SetParent(root.transform, false);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 50;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 4f; // dünya uzayında yazı keskin dursun
                var rect = (RectTransform)canvasGo.transform;
                rect.sizeDelta = new Vector2(240f, 56f);
                rect.localScale = Vector3.one * 0.02f; // 240x56 → 4.8x1.1 birim
                rect.localPosition = new Vector3(0f, 2.3f, 0f);
                var group = canvasGo.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                var bg = canvasGo.AddComponent<Image>();
                bg.color = UiKit.PanelColor;
                Text label = UiKit.Label("Yazi", canvasGo.transform, text, Vector2.zero, new Vector2(230f, 52f), 15, UiKit.LightText);
                label.lineSpacing = 0.9f;

                var sign = root.AddComponent<SignPost>();
                TestSceneBuilder.SetField(sign, "bubble", group);
            }

            public void Goal(float x, float groundTop)
            {
                hasGoal = true;
                var root = new GameObject("Bitis");
                root.transform.SetParent(actors);
                root.transform.position = new Vector3(x, groundTop, 0f);
                var col = root.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(1.2f, 3f);
                col.offset = new Vector2(0f, 1.5f);

                var pole = new GameObject("Direk").AddComponent<SpriteRenderer>();
                pole.transform.SetParent(root.transform, false);
                pole.sprite = poleSprite;
                pole.sortingOrder = 2;

                var flag = new GameObject("Bayrak").AddComponent<SpriteRenderer>();
                flag.transform.SetParent(root.transform, false);
                flag.transform.localPosition = new Vector3(0.12f, 2.35f, 0f);
                flag.sprite = flagSprite;
                flag.sortingOrder = 3;

                var goal = root.AddComponent<LevelGoal>();
                TestSceneBuilder.SetField(goal, "flag", flag.transform);
            }

            /// <summary>
            /// Zemin parçaları arasındaki her boşluğun Gila'nın menzilinde olduğunu kontrol eder.
            /// Boşlukta platform varsa geçiş platformla sağlanıyor sayılır.
            /// </summary>
            public int Validate(string levelName)
            {
                int warnings = 0;
                void Warn(string message)
                {
                    warnings++;
                    Debug.LogWarning($"[DesertGecko][{levelName}] {message}");
                }

                if (!hasGoal) Warn("Bitiş noktası yok.");
                var sorted = grounds.OrderBy(g => g.xMin).ToList();
                bool spawnOnGround = sorted.Any(g => Spawn.x >= g.xMin && Spawn.x <= g.xMax && Mathf.Abs(Spawn.y - g.top) < 0.5f);
                if (!spawnOnGround) Warn("Başlangıç noktası bir zeminin üst yüzeyinde değil.");

                // Zeminler ve havadaki platformlar tek bir yüzey zinciri: soldan sağa her adım menzilde mi?
                var surfaces = sorted.Concat(platforms).OrderBy(s => s.xMin).ToList();
                for (int i = 0; i + 1 < surfaces.Count; i++)
                {
                    var a = surfaces[i];
                    // Bir sonraki yüzey: a'nın sağ kenarından sonra başlayan en yakın yüzey.
                    var next = surfaces.Skip(i + 1).Where(s => s.xMax > a.xMax).OrderBy(s => s.xMin).FirstOrDefault();
                    if (next.xMax <= a.xMax)
                        continue;
                    float gap = next.xMin - a.xMax;
                    float rise = next.top - a.top;
                    if (gap <= 0f)
                        continue; // bitişik/üst üste: yükseliyorsa duvar, Gila tırmanır

                    bool reachable = (rise <= SingleJumpRise && gap <= SingleJumpGap)
                        || (rise <= DoubleJumpRise && gap <= DoubleJumpGap)
                        || (rise > 0f && gap <= ClingGap && grounds.Contains(next)) // platformun yüzüne yapışılmaz
                        || (rise <= 0f && gap <= JumpApexRun + (JumpApexRise - rise) * GlideRunPerDrop); // zıpla + süzül
                    if (!reachable)
                        Warn($"x={a.xMax:0.#}→{next.xMin:0.#} arası {gap:0.#} birimlik boşluk ({rise:+0.#;-0.#;0} yükseklik) Gila'nın menzili dışında.");
                }

                // En uzun süzülüş (çift zıplama + süzülme) bir checkpoint'in üstünden geçiyorsa
                // oyuncu bölümü atlayabilir.
                for (int i = 0; i + 1 < sorted.Count; i++)
                {
                    var a = sorted[i];
                    if (sorted[i + 1].xMin - a.xMax <= 0f)
                        continue;
                    float landing = MaxGlideLanding(a, sorted.Skip(i + 1));
                    foreach (float cp in checkpoints.Where(cp => cp > sorted[i + 1].xMax && cp < landing))
                        Warn($"x={a.xMax:0.#} kenarından süzülen oyuncu x={landing:0.#}'e iniyor, x={cp:0.#} checkpoint'ini içeren bölüm atlanıyor.");
                }
                return warnings;
            }

            /// <summary>Kenardan en uzun süzülüşün ilk değdiği zemin noktası (yüzüne çarpma ya da üstüne iniş).</summary>
            static float MaxGlideLanding((float xMin, float xMax, float top) from, IEnumerable<(float xMin, float xMax, float top)> rest)
            {
                float apexX = from.xMax + MaxApexRun, apexY = from.top + MaxApexRise;
                float HeightAt(float x) => x <= apexX ? apexY : apexY - (x - apexX) / GlideRatio;
                foreach (var g in rest)
                {
                    if (HeightAt(g.xMin) <= g.top)
                        return g.xMin; // yüzüne çarpar
                    if (HeightAt(g.xMax) <= g.top)
                        return apexX + (apexY - g.top) * GlideRatio;
                }
                return float.PositiveInfinity;
            }
        }
    }
}
