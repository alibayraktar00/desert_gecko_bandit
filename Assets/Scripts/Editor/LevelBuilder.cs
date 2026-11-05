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
        // Koşu 5.5, zıplama 2.8 + çift zıplama 2.2. Hareket değerleri değişirse bunları da güncelleyin.
        const float SingleJumpRise = 2.3f, SingleJumpGap = 3.3f;
        const float DoubleJumpRise = 4.3f, DoubleJumpGap = 5.5f;
        // Süzülüş: yatay 4.5, düşüş ~2.14 birim/sn (1.8 sınırı + o fizik adımındaki yerçekimi). Bot ölçümü.
        const float GlideRatio = 4.5f / 2.14f;
        const float GlideRunPerDrop = GlideRatio * 0.85f;    // menzil kontrolünde payla
        const float MaxApexRun = 4f, MaxApexRise = 5f;       // çift zıplamada tepe noktası (süzülüş buradan başlar)
        const float ClingGap = 5f;                          // atlayıp karşı duvara yapışabileceği boşluk

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
        /// Sadece 1-3'ü yeniden kurar, diğer sahnelere dokunmaz. Açık sahnede kaydedilmemiş değişiklik
        /// varsa durur. Kurduktan sonra görüntü alır ve önceki sahneye döner.
        /// </summary>
        [MenuItem("Desert Gecko/Level 1-3'u Kur")]
        public static string BuildLevel13()
        {
            if (EditorApplication.isPlaying)
                return "play modunda kurulamaz";
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty)
                return "açık sahnede kaydedilmemiş değişiklik var: " + active.path;
            string previous = active.path;

            TestSceneBuilder.SceneContext ctx = TestSceneBuilder.Prepare();
            LevelCatalog.LevelInfo info = LevelCatalog.Levels[2];
            var painter = BuildLevel(ctx, info, Level_1_3);
            int warnings = painter.Validate(info.Title);
            foreach (Vector2 cam in painter.ShotPoints)
                SceneCapture.Capture(Path.Combine("Temp/Screenshots", $"{info.Scene}_{cam.x:0}_{cam.y:0}.png"), new Vector3(cam.x, cam.y, -10f));
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
                EditorSceneManager.OpenScene(previous);
            return warnings + " tasarım uyarısı";
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
            p.Ground(18.5f, 32.5f, 0f);
            p.Steps(32.5f, 34f, 0f, 2f);
            p.Crystal(17.2f, 2.4f);
            p.Plant("kaktus_12", 24f, 0f);

            // Basamak
            p.Sign(30f, 0f, "Yüksek kenarlara zıpla");
            p.Ground(34f, 46f, 2f);
            p.Plant("kaktus_22", 40f, 2f);

            // Çift zıplama
            p.Sign(42.5f, 2f, "Havadayken tekrar Space:\nçift zıplama (kuyruk itişi)");
            p.Ground(46f, 76f, 6f); // aynı yükseklikte tek parça: iki parçanın birleşim yerinde karakter takılır
            p.Crystal(45f, 5.4f);
            p.Checkpoint(49f, 6f);

            // Dil: böcek ve ilk akrep
            p.Sign(53f, 6f, "J: dil saldırısı\nBöcek yakala, can yenile");
            p.Bug(57f, 7.3f);
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
            p.CrystalArc(94f, 15.5f, 100f, 12.5f, 5); // zıplayıp süzülenin yolu
            p.Ground(101f, 133f, 10f);
            p.Checkpoint(105f, 10f);

            // Kuyruk
            p.Sign(111f, 10f, "K: kuyruk fırlat. Kuyruk\nçıkana kadar süzülemezsin!");
            p.Bug(115f, 11.3f);
            p.Scorpion(121f, 10f);
            p.Scorpion(126f, 10f);
            p.Plant("kaktus_01", 118f, 10f);
            p.Goal(130f, 10f);
            p.Boundary(133f);

            p.ShotPoints.AddRange(new[] { new Vector2(6f, 1.5f), new Vector2(50f, 7.5f), new Vector2(96f, 13f), new Vector2(123f, 11.5f) });
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
            // Platformlar arası boşluklar yeni hıza göre daraltıldı
            p.Platform(21f, 2f, "platform_03");
            p.Platform(25.5f, 3.5f, "platform_00");
            p.Platform(30f, 2.5f, "platform_02");
            p.Crystal(21f, 2.8f);
            p.Crystal(25.5f, 4.3f);
            p.Crystal(30f, 3.3f);

            p.Ground(34f, 56f, 1f);
            p.Checkpoint(36f, 1f);
            p.Sign(39f, 1f, "Çıngırak duyarsan kaç!\nMermi alçaktan gelir: zıpla");
            p.Snake(47f, 1f);
            p.Bug(41f, 2.4f);
            p.Plant("kaktus_25", 51f, 1f);

            // Kanyon duvarı: 11 birim tırmanış
            p.Sign(53f, 1f, "Kanyonun tepesine tırman");
            p.Ground(56f, 79f, 12f);
            p.CrystalLine(55.4f, 4f, 4, 2f, vertical: true);
            p.Checkpoint(59f, 12f);
            p.Scorpion(65f, 12f);
            p.Scorpion(73f, 12f);
            p.Plant("kaktus_05", 69f, 12f);
            p.Platform(68f, 15.5f, "platform_04");
            p.CrystalLine(66.5f, 16.3f, 4, 1f);

            // Kanyonun üstünde biraz daha yüksek bir tepe (aşağı inilseydi süzülüşle bölüm atlanırdı)
            p.Ground(82.5f, 98f, 14f);
            p.Scorpion(86.5f, 14f);
            p.Table(92.5f, 14f, "platform_10");
            p.Crystal(92.5f, 16f);

            // Uzun süzülüş: karşı taraf neredeyse aynı yükseklikte, süzülmeden geçilmez
            p.Sign(95.5f, 14f, "Uzun bir süzülüş!\nSpace basılı tut");
            p.CrystalArc(101f, 17.5f, 108f, 14.8f, 4);
            p.Ground(109f, 143f, 13f);
            p.Checkpoint(112f, 13f);
            p.Scorpion(119f, 13f);
            p.Snake(127f, 13f);
            p.Bug(123f, 14.3f);
            p.Platform(131f, 16f, "platform_05");
            p.CrystalLine(129.5f, 16.8f, 4, 1f);
            p.Scorpion(135f, 13f);
            p.Plant("kaktus_28", 138f, 13f);
            p.Goal(141f, 13f);
            p.Boundary(143f);

            p.ShotPoints.AddRange(new[] { new Vector2(24f, 2.5f), new Vector2(59f, 10f), new Vector2(100f, 15f), new Vector2(133f, 15f) });
        }

        // 1-3 Akbaba Gölgesi: üstü kapalı, katman katman iki mesa. İlkinde tek yönlü platformlarla zikzak çıkılır,
        // ikincisinde tek yönlü kaya çıkıntılarıyla baca tırmanılır. Aralarda uzun süzülüşler.
        static void Level_1_3(LevelPainter p)
        {
            p.Spawn = new Vector2(0f, 0f);
            p.Boundary(-9f);

            p.Ground(-8f, 10f, 0f);
            p.Plant("kaktus_00", -4f, 0f);

            // Dar platformlar
            p.Platform(13f, 2.5f, "platform_01");
            p.Platform(17f, 4f, "platform_01");
            p.Crystal(13f, 3.3f);
            p.Crystal(17f, 4.8f);

            p.Ground(20.5f, 64f, 3f); // mesanın alt katı dışarıdaki zeminle tek parça
            p.Checkpoint(22.5f, 3f);
            p.Plant("kaktus_19", 25f, 3f);
            p.Sign(27.5f, 3f, "Kapalı mesa: platformlarla çık\nS + Space: platformdan in");

            // --- Birinci mesa: 3 kat. Duvar tırmanılamaz, katlar arası tek yol platformlar. ---
            const float floor1 = 3f, floor2 = 8f, floor3 = 13f, roof = 17f;
            p.BackWall(31f, 63f, floor1, roof);
            p.RockMass(30f, 31f, floor1 + 3f, roof);  // giriş kapısının üstü
            p.RockMass(30f, 64f, roof, 60f);          // tavan
            p.RockMass(63f, 64f, floor1, floor3 - 1f); // sağ duvar, kapı en üst katta
            p.RockMass(63f, 64f, floor3 + 3f, roof);

            // 1. kat: sağa koş, sağ uçtaki platformlardan yukarı çık
            p.Scorpion(38f, floor1);
            p.Ledge(43f, 48f, floor1 + 2.3f);
            p.CrystalLine(44f, floor1 + 3.1f, 3, 1.5f);
            p.Bug(50f, floor1 + 1.3f);
            p.Ledge(54f, 62f, floor1 + 2.5f);
            p.Ledge(52f, 56f, floor2); // 2. kat döşemesinin devamı, alttan geçilir

            // 2. kat: sola koş
            p.Slab(31f, 52f, floor2);
            p.Checkpoint(50f, floor2);
            p.Snake(40f, floor2);
            p.Ledge(45f, 48.5f, floor2 + 2.2f);
            p.Crystal(46.75f, floor2 + 3f);
            p.Bug(32.5f, floor2 + 1.3f); // koridorun sonundaki küçük ödül
            p.Ledge(36f, 42f, floor2 + 2.5f);
            p.Ledge(40f, 44f, floor3); // 3. kat döşemesinin devamı, alttan geçilir

            // 3. kat: sağa koş, kapıdan çık
            p.Slab(44f, 66f, floor3, register: true);
            p.Scorpion(52f, floor3);
            p.Ledge(49f, 53f, floor3 + 2f);
            p.CrystalLine(50f, floor3 + 2.8f, 2, 2f);
            p.Scorpion(59f, floor3);

            // Süzülüş: karşı taraf yüksekte, sonraki bölümler atlanamaz
            p.Sign(62f, floor3, "Kapıdan atla, Space ile süzül");
            p.CrystalArc(68f, floor3 + 3f, 76f, floor3 - 0.6f, 5);
            p.Ground(78f, 97f, 9f);
            p.Checkpoint(80f, 9f);
            p.Scorpion(85f, 9f);
            p.Table(90f, 9f, "platform_09");
            p.Plant("kaktus_06", 94f, 9f);

            // Çift zıplamalık boşluk
            p.Crystal(99f, 12.5f);

            // --- İkinci mesa: tek yönlü kaya çıkıntılarıyla baca. ---
            const float baseY = 7f, shaftRoof = 23f;
            p.Ground(101.5f, 128f, baseY);
            p.Snake(108f, baseY);
            p.BackWall(113f, 127f, baseY, shaftRoof);
            p.RockMass(112f, 113f, baseY + 3f, shaftRoof);
            p.RockMass(112f, 128f, shaftRoof, 60f);
            p.RockMass(127f, 128f, baseY, 19.5f);
            p.RockMass(127f, 128f, 22.5f, shaftRoof);
            p.Sign(110.5f, baseY, "Çıkıntılardan aşağıdan\nzıplayarak geçebilirsin");

            p.Ledge(114f, 120f, 9.5f);
            p.Crystal(116.5f, 10.3f);
            p.Ledge(121f, 126f, 12f);
            p.Bug(123.5f, 13.3f);
            p.Ledge(114f, 120f, 14.5f);
            p.Crystal(116.5f, 15.3f);
            p.Ledge(121f, 126f, 17f);
            p.Crystal(123.5f, 17.8f);
            p.Ledge(121f, 130f, 19.5f, register: true); // en üst çıkıntı kapıdan dışarı uzanır

            // Son süzülüş ve akrep sürüsü
            p.CrystalArc(132f, 22.5f, 139f, 19.8f, 4);
            p.Ground(142f, 170f, 16f);
            p.Checkpoint(144f, 16f);
            p.Scorpion(150f, 16f);
            p.Bug(153f, 17.3f);
            p.Scorpion(156f, 16f);
            p.Snake(162f, 16f);
            p.Table(159f, 16f, "platform_13");
            p.Plant("kaktus_17", 147f, 16f);
            p.Goal(167f, 16f);
            p.Boundary(170f);

            // Botun izlediği yol; test bu noktaları sırayla gezer.
            p.Route(58f, floor1 + 2.5f, 54.5f, floor2, 38.5f, floor2 + 2.5f, 41.5f, floor3, 66f, floor3, 79f, 9f,
                110f, baseY, 114.5f, 9.5f, 121.5f, 12f, 119.3f, 14.5f, 121.5f, 17f, 124f, 19.5f, 130f, 19.5f, 167f, 16f);

            p.ShotPoints.AddRange(new[]
            {
                new Vector2(24f, 5f), new Vector2(40f, 6f), new Vector2(45f, 10f), new Vector2(56f, 13f),
                new Vector2(120f, 13f), new Vector2(122f, 19f), new Vector2(155f, 18f),
            });
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
                // Üst sınır alçak kenarda kalır: yüksek kenara çıksa boşluğun üstünde sahte bir zemin görünürdü.
                const float fadeHeight = 2.5f, bottom = GroundBottom - 4f;
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
                    float depth = top - fadeHeight - bottom;
                    Vector2 unit = solid.sprite.bounds.size;
                    solid.transform.localScale = new Vector3(width / unit.x, depth / unit.y, 1f); // düz renk: tekrar değil ölçek
                    solid.transform.localPosition = new Vector3(0f, -fadeHeight - depth / 2f, 0f);
                    solid.color = new Color32(0x1A, 0x0C, 0x09, 0xFF); // zeminlerin derin toprağıyla aynı: dipte düz siyah blok durmasın
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

            /// <summary>
            /// İki zemin yüksekliği arasına yaklaşık 1 birimlik eşit basamaklar koyar. Gila alçak yüzeylere
            /// yapışmadığı için zıplayarak çıkar (eğimli yüzeyde duvar sensörü rampaya yapışırdı).
            /// </summary>
            public void Steps(float xMin, float xMax, float fromTop, float toTop)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(toTop - fromTop)) - 1);
                float width = (xMax - xMin) / count;
                float rise = (toTop - fromTop) / (count + 1);
                for (int i = 0; i < count; i++)
                    Ground(xMin + i * width, xMin + (i + 1) * width, fromTop + (i + 1) * rise);
            }

            // --- Katmanlı level parçaları ---

            static Sprite LayerArt(string name) =>
                AssetDatabase.LoadAssetAtPath<Sprite>(SpriteImportRules.LayerArtFolder + name + ".png");

            static SpriteRenderer TiledArt(Transform parent, string name, Sprite sprite, Vector2 topCenter, Vector2 size, int order)
            {
                var sr = new GameObject(name).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(parent);
                sr.transform.position = topCenter;
                sr.sprite = sprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = size;
                sr.sortingOrder = order;
                return sr;
            }

            /// <summary>Çarpışmasız mağara arka duvarı. Gökyüzünü kapatır, üstü kapalı hissi verir.</summary>
            public void BackWall(float xMin, float xMax, float yMin, float yMax) =>
                TiledArt(geometry, Name("ArkaDuvar"), LayerArt("kaya_arka"), new Vector2((xMin + xMax) / 2f, yMax),
                    new Vector2(xMax - xMin, yMax - yMin), -40);

            /// <summary>
            /// Tavan ve dış cephe: katı kaya, Default katmanında. Gila çarpar ama yapışıp tırmanamaz;
            /// böylece mesanın üstüne çıkılıp katlar atlanamaz. Alt kenarından saçak sarkar.
            /// </summary>
            public void RockMass(float xMin, float xMax, float yMin, float yMax)
            {
                var size = new Vector2(xMax - xMin, yMax - yMin);
                SpriteRenderer sr = TiledArt(geometry, Name("Kaya"), LayerArt("kaya_kutle"),
                    new Vector2((xMin + xMax) / 2f, yMax), size, 2);
                var col = sr.gameObject.AddComponent<BoxCollider2D>();
                col.size = size;
                col.offset = new Vector2(0f, -size.y / 2f);
                Fringe(sr.transform, xMin, xMax, yMin);
            }

            static void Fringe(Transform parent, float xMin, float xMax, float y)
            {
                Sprite s = LayerArt("kaya_sacak");
                TiledArt(parent, "Sacak", s, new Vector2((xMin + xMax) / 2f, y), new Vector2(xMax - xMin, s.bounds.size.y), 2);
            }

            /// <summary>
            /// Kat döşemesi: üstünde yürünür, aynı zamanda alt katın tavanıdır (altından geçilemez).
            /// register: level akışının parçası, menzil kontrolünde yüzey sayılır.
            /// </summary>
            public void Slab(float xMin, float xMax, float top, float thickness = 1f, bool register = false)
            {
                var size = new Vector2(xMax - xMin, thickness);
                var center = new Vector2((xMin + xMax) / 2f, top);
                SpriteRenderer sr = TiledArt(geometry, Name("Doseme"), LayerArt("kaya_kutle"), center, size, 1);
                sr.gameObject.layer = ctx.GroundLayer;
                var col = sr.gameObject.AddComponent<BoxCollider2D>();
                col.size = size;
                col.offset = new Vector2(0f, -thickness / 2f);
                Sprite rim = LayerArt("kaya_ust");
                TiledArt(sr.transform, "Kenar", rim, center, new Vector2(size.x, rim.bounds.size.y), 2);
                Fringe(sr.transform, xMin, xMax, top - thickness);
                if (register)
                    platforms.Add((xMin, xMax, top));
            }

            /// <summary>
            /// Tek yönlü collider: alttan ve yandan geçilir, üstüne inilir, S + Space ile aşağı inilir.
            /// </summary>
            static void AddOneWayCollider(GameObject go, float width, float thickness, Vector2 offset)
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(width, thickness);
                col.offset = offset;
                col.usedByEffector = true;
                var effector = go.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 170f;
                effector.useSideFriction = false;
                effector.useSideBounce = false;
            }

            /// <summary>İnce kaya çıkıntısı: alttan zıplayarak geçilen tek yönlü platform.</summary>
            public void Ledge(float xMin, float xMax, float top, bool register = false)
            {
                if (register)
                    platforms.Add((xMin, xMax, top));
                Sprite s = LayerArt("kaya_kenar");
                SpriteRenderer sr = TiledArt(geometry, Name("Cikinti"), s, new Vector2((xMin + xMax) / 2f, top),
                    new Vector2(xMax - xMin, s.bounds.size.y), 1);
                sr.gameObject.layer = ctx.GroundLayer;
                AddOneWayCollider(sr.gameObject, xMax - xMin, 0.3f, new Vector2(0f, -0.15f));
            }

            /// <summary>Otomatik testteki botun sırayla gideceği noktalar: x, y, x, y...</summary>
            public void Route(params float[] xy)
            {
                var root = new GameObject("BotRota").transform;
                for (int i = 0; i + 1 < xy.Length; i += 2)
                {
                    var point = new GameObject((i / 2).ToString()).transform;
                    point.SetParent(root);
                    point.position = new Vector2(xy[i], xy[i + 1]);
                }
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
            public void Snake(float x, float groundTop) => CombatSetupBuilder.Place(ctx.Combat.Rattlesnake, actors, Name("Yilan"), x, groundTop);
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
                    if (Mathf.Abs(gap) < 0.01f && Mathf.Abs(rise) < 0.01f && grounds.Contains(a) && grounds.Contains(next))
                        Warn($"x={a.xMax:0.#}: aynı yükseklikte bitişik iki zemin, birleşim yerinde karakter takılır; tek parça yapın.");
                    if (gap <= 0f)
                        continue; // bitişik/üst üste: yükseliyorsa duvar, Gila tırmanır

                    bool reachable = (rise <= SingleJumpRise && gap <= SingleJumpGap)
                        || (rise <= DoubleJumpRise && gap <= DoubleJumpGap)
                        || (rise > 0f && gap <= ClingGap && grounds.Contains(next)) // platformun yüzüne yapışılmaz
                        || (rise <= 0f && gap <= MaxApexRun + (MaxApexRise - rise) * GlideRunPerDrop); // çift zıpla + süzül
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
