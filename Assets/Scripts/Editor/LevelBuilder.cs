using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertGecko.Core;
using DesertGecko.Enemies;
using DesertGecko.Environment;
using DesertGecko.Levels;
using DesertGecko.Player;
using DesertGecko.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Kızgın Kum Vadisi'nin levellerini koddan kurar: zemin, duvarlar, platformlar, düşmanlar,
    /// toplanabilirler, tabelalar, bitiş, HUD, duraklatma ve level sonu menüleri.
    /// Menü: Desert Gecko → Levelleri Kur → Hepsi ya da tek bir level.
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

        [MenuItem("Desert Gecko/Levelleri Kur/Hepsi")]
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

        [MenuItem("Desert Gecko/Levelleri Kur/Level 1")]
        public static string BuildLevel1() => BuildSingleLevel(0, Level_1);

        [MenuItem("Desert Gecko/Levelleri Kur/Level 2")]
        public static string BuildLevel2() => BuildSingleLevel(1, Level_2);

        [MenuItem("Desert Gecko/Levelleri Kur/Level 3")]
        public static string BuildLevel3() => BuildSingleLevel(2, Level_3);

        /// <summary>EditorBridge için: menü, level ve sahne listesi; bağlantı noktalarının görüntüsüyle.</summary>
        public static string BuildAllWithShots() => BuildAll("Temp/Screenshots") + " tasarım uyarısı";

        /// <summary>Level 3'te kaplıca bacasından Soğuk Cephe'ye çıkışı görüntüler; sahneyi kaydetmez.</summary>
        public static string CaptureKaplicaExit()
        {
            string previous = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene($"{SceneFolder}/Level_3.unity");
            foreach (float x in new[] { KaplicaX + 110f, KaplicaX + 118f, CepheX, CepheX + 6f })
                SceneCapture.Capture($"Temp/Screenshots/L3cikis_{x:0}.png", new Vector3(x, 26f, -10f));
            // Level 2: derin galerinin sonundaki sıcak hava bacası
            EditorSceneManager.OpenScene($"{SceneFolder}/Level_2.unity");
            SceneCapture.Capture("Temp/Screenshots/L2baca.png", new Vector3(DerinGaleriX + 108f, 9f, -10f));
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(previous) ? $"{SceneFolder}/Level_3.unity" : previous);
            return "tamam";
        }

        /// <summary>Level 3'ün çalışma anında değişen hallerini (şafak, fırtına) görüntüler; sahneyi kaydetmez.</summary>
        public static string PreviewLevel3()
        {
            string previous = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene($"{SceneFolder}/Level_3.unity");
            var sky = Object.FindAnyObjectByType<NightSky>();
            foreach (float t in new[] { 0f, 0.35f, 0.65f, 1f })
            {
                sky.Preview(t);
                SceneCapture.Capture($"Temp/Screenshots/L3onizleme_safak_{t * 100f:0}.png", new Vector3(SafakX + 30f, 21f, -10f));
            }
            sky.Preview(0f);
            var storm = GameObject.Find("AyazFirtinasi").transform;
            storm.position = new Vector3(CepheX + 30f, 0f, 0f);
            foreach (SpriteRenderer sr in storm.GetComponentsInChildren<SpriteRenderer>())
                sr.enabled = true;
            var cam = Camera.main;
            cam.transform.position = new Vector3(CepheX + 34f, 25f, -10f);
            typeof(ColdFront).GetMethod("Animate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(storm.GetComponent<ColdFront>(), null); // bulutları kameraya göre diz
            SceneCapture.Capture("Temp/Screenshots/L3onizleme_firtina.png", new Vector3(CepheX + 34f, 25f, -10f));
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(previous) ? $"{SceneFolder}/Level_3.unity" : previous); // değişiklikler kaydedilmeden atılır
            return "tamam";
        }

        /// <summary>
        /// Tek bir leveli yeniden kurar, diğer sahnelere dokunmaz. Açık sahnede kaydedilmemiş değişiklik
        /// varsa durur. Kurduktan sonra görüntü alır ve önceki sahneye döner.
        /// </summary>
        static string BuildSingleLevel(int index, System.Action<LevelPainter> define)
        {
            string blocked = EditorApplication.isPlaying ? "Play modunda level kurulamaz."
                : EditorSceneManager.GetActiveScene().isDirty ? "Açık sahnede kaydedilmemiş değişiklik var, önce kaydedin." : null;
            if (blocked != null)
            {
                Debug.LogWarning("[DesertGecko] " + blocked);
                return blocked;
            }
            var active = EditorSceneManager.GetActiveScene();
            string previous = active.path;

            TestSceneBuilder.SceneContext ctx = TestSceneBuilder.Prepare();
            LevelCatalog.LevelInfo info = LevelCatalog.Levels[index];
            var painter = BuildLevel(ctx, info, define);
            int warnings = painter.Validate(info.Title);
            foreach (Vector2 cam in painter.ShotPoints)
                SceneCapture.Capture(Path.Combine("Temp/Screenshots", $"{info.Scene}_{cam.x:0}_{cam.y:0}.png"), new Vector3(cam.x, cam.y, -10f));
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
                EditorSceneManager.OpenScene(previous);
            return warnings + " tasarım uyarısı";
        }

        /// <summary>
        /// Komut satırı: ana menüyü ve levelleri kurar, her birinden ekran görüntüsü alır.
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

            var definitions = new System.Action<LevelPainter>[] { Level_1, Level_2, Level_3 };
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
            painter.DressCliffs();
            painter.DressCaveFloors();
            painter.ScatterGroundCover();
            painter.DressBoundaries();

            GameObject player = TestSceneBuilder.SpawnPlayer(ctx, painter.Spawn);
            Camera cam = TestSceneBuilder.CreateCamera(painter.Spawn + new Vector2(0f, 1.5f), player.transform, out CameraFollow follow);
            CombatSetupBuilder.BuildHud(cam, player, ctx.Combat);
            BackgroundBuilder.Build();
            if (painter.Night)
                painter.FinishNight(player);

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

        // Level 1: Kızgın Kum Vadisi, tek ve uzun bir yol. Üç bölüm ve aralarındaki iki iniş.
        // Bölümler kendi yerel x'iyle yazılır (OriginX), böylece biri uzayınca sadece sonrakilerin başlangıcı değişir.
        // Her bölüm alçaktan başlayıp tırmandığı için yükseklik birikmez: arka plan ve killY her yerde aynı çalışır.
        const float KanyonX = 160f, AkbabaX = 325f;

        static void Level_1(LevelPainter p)
        {
            p.Spawn = new Vector2(0f, 0f);
            p.Boundary(-9f);

            Bolum_IlkAdimlar(p);
            Dekor_IlkAdimlar(p);
            Baglanti_VadiyeInis(p);

            p.OriginX = KanyonX;
            Bolum_KanyonDuvarlari(p);
            Baglanti_MesayaSuzulus(p);
            Dekor_KanyonDuvarlari(p);

            p.OriginX = AkbabaX;
            Bolum_AkbabaGolgesi(p);
            Dekor_AkbabaGolgesi(p);

            p.ShotPoints.AddRange(new[] { new Vector2(138f, 8f), new Vector2(156f, 3f), new Vector2(308f, 8f), new Vector2(325f, 4f), new Vector2(388f, 11f), new Vector2(355f, 8f), new Vector2(455f, 18f),
                new Vector2(22f, 3f), new Vector2(34f, 4f), new Vector2(62f, 9f), new Vector2(82f, 14f), new Vector2(205f, 4f), new Vector2(478f, 18f) });
        }

        // İlk Adımlar: her mekaniği tabelayla tek tek öğreten eğitim bölümü.
        static void Bolum_IlkAdimlar(LevelPainter p)
        {
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
            p.Ladder(75.5f, 6f, 12.6f);
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
            p.Sign(131f, 10f, "Vadiye iniş:\nkayalardan aşağı atla");
        }

        // Bağlantı 1: eğitimin tepesinden kayalık basamaklarla vadi tabanına. Düşmansız nefes alma yeri;
        // sondaki böcek canı doldurur, kanyonun başındaki checkpoint de burayı kapatır.
        static void Baglanti_VadiyeInis(LevelPainter p)
        {
            p.CrystalArc(134f, 10.5f, 138f, 8.6f, 3);
            p.Ground(133f, 139f, 7.5f);
            p.Plant("kaktus_12", 136.5f, 7.5f);
            p.Ground(139f, 146f, 5f);
            p.Bug(142.5f, 6.3f);
            p.Plant("kaktus_22", 145f, 5f);
            p.Platform(149f, 3f, "platform_03");
            p.Crystal(149f, 3.8f);
        }

        // Kanyon Duvarları: platformlar, tırmanma, yüksek kanyon ve uzun süzülüş.
        static void Bolum_KanyonDuvarlari(LevelPainter p)
        {
            p.Ground(-8f, 18f, 0f);
            p.Checkpoint(-5f, 0f);
            p.Sign(-0.5f, 0f, "Kanyon Duvarları");
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
            p.Jackal(73f, 12f);
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
            p.Jackal(135f, 13f);
            p.Plant("kaktus_28", 138f, 13f);
        }

        // Bağlantı 2: kanyonun ucundan vadi tabanına en uzun süzülüş. Düşen oyuncu hemen kenardaki
        // checkpoint'e döner; iniş yeri süzülmeden ulaşılamayacak kadar uzak, süzülünce rahat yetişir.
        static void Baglanti_MesayaSuzulus(LevelPainter p)
        {
            p.Checkpoint(140f, 13f);
            p.Sign(142f, 13f, "Karşıdaki vadiye süzül:\nSpace basılı tut");
            p.CrystalArc(145f, 12.5f, 157f, 6.5f, 5);
        }

        // Akbaba Gölgesi: üstü kapalı, katman katman iki mesa. İlkinde tek yönlü platformlarla zikzak çıkılır,
        // ikincisinde tek yönlü kaya çıkıntılarıyla baca tırmanılır. Aralarda uzun süzülüşler.
        static void Bolum_AkbabaGolgesi(LevelPainter p)
        {
            p.Ground(-8f, 10f, 0f);
            p.Checkpoint(-2f, 0f);
            p.Sign(1.5f, 0f, "Akbaba Gölgesi");
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
            p.RockMass(27f, 31f, floor1 + 3f, roof);  // giriş tünelinin üstü
            p.RockEdge(27f, 40f, floor1 + 3f, -1);
            p.DoorFade(27f, -1, 4.5f, floor1, floor1 + 3f);
            p.RockMass(27f, 66f, roof, 60f);          // tavan
            p.RockMass(63f, 66f, GroundBottom, floor3 - 1f); // kalın sağ duvar, kapı en üst katta bir tünel
            p.RockEdge(66f, floor3, GroundBottom, 1);
            p.RockMass(63f, 66f, floor3 + 3f, roof);
            p.RockEdge(66f, 40f, floor3 + 3f, 1);
            p.DoorFade(66f, 1, 4.5f, floor3 - 1f, floor3 + 3f);

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
            p.Jackal(59f, floor3);

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
            p.RockMass(109f, 113f, baseY + 3f, shaftRoof);
            p.RockEdge(109f, 40f, baseY + 3f, -1);
            p.DoorFade(109f, -1, 4.5f, baseY, baseY + 3f);
            p.RockMass(109f, 131f, shaftRoof, 60f);
            p.RockMass(127f, 131f, GroundBottom, 19f);
            p.RockEdge(131f, 19.5f, GroundBottom, 1);
            p.RockMass(127f, 131f, 22.5f, shaftRoof);
            p.RockEdge(131f, 40f, 22.5f, 1);
            p.DoorFade(131f, 1, 4.5f, 19f, 22.5f);
            p.Sign(110.5f, baseY, "Çıkıntılardan aşağıdan\nzıplayarak geçebilirsin");

            p.Ledge(114f, 120f, 9.5f);
            p.Crystal(116.5f, 10.3f);
            p.Ledge(121f, 126f, 12f);
            p.Bug(123.5f, 13.3f);
            p.Ledge(114f, 120f, 14.5f);
            p.Crystal(116.5f, 15.3f);
            p.Ledge(121f, 126f, 17f);
            p.Crystal(123.5f, 17.8f);
            p.Ledge(121f, 133f, 19.5f, register: true); // en üst çıkıntı tünelden dışarı uzanır

            // Son süzülüş ve akrep sürüsü
            p.CrystalArc(132f, 22.5f, 139f, 19.8f, 4);
            p.Ground(142f, 170f, 16f);
            p.Checkpoint(144f, 16f);
            p.Scorpion(150f, 16f);
            p.Bug(153f, 17.3f);
            p.Jackal(156f, 16f);
            p.Snake(162f, 16f);
            p.Table(159f, 16f, "platform_13");
            p.Plant("kaktus_17", 147f, 16f);
            p.Goal(167f, 16f);
            p.Boundary(170f);

            // Botun izlediği yol; test bu noktaları sırayla gezer.
            p.Route(58f, floor1 + 2.5f, 54.5f, floor2, 38.5f, floor2 + 2.5f, 41.5f, floor3, 66f, floor3, 79f, 9f,
                110f, baseY, 114.5f, 9.5f, 121.5f, 12f, 119.3f, 14.5f, 121.5f, 17f, 124f, 19.5f, 130f, 19.5f, 167f, 16f);
        }

        // Level 2: Terk Edilmiş Maden. Orta zorluk. Her bölümün kendine ait bir fikri var, tekrar eden geçiş yok:
        // fikir önce güvenli yerde tanıtılır, sonra sertleşir, sonra beklenmedik biçimde kullanılır.
        //   Kanyon Ağzı  : hareketli platform        Maden Şaftı : çürük tahta, düşen kaya, asansör, gizli oda
        //   Rüzgâr Geçidi: sıcak hava sütunu         Derin Galeri: zikzak kuyu, dövüş odası, kaya altında köprü
        //   Son Tırmanış : hepsi bir arada
        // İki madenin girişi de çıkışı da farklı: tahtası yıkılan kuyu / zikzak kuyu, asansörlü kapı / sıcak hava bacası.
        const float MadenSaftiX = 100f, RuzgarGecidiX = 207f, DerinGaleriX = 295f, SonTirmanisX = 413f;

        static void Level_2(LevelPainter p)
        {
            p.Spawn = new Vector2(0f, 0f);
            p.Boundary(-9f);

            Maden_KanyonAgzi(p);
            Dekor_KanyonAgzi(p);
            p.OriginX = MadenSaftiX;
            Maden_Safti(p);
            Dekor_MadenSafti(p);
            p.OriginX = RuzgarGecidiX;
            Maden_RuzgarGecidi(p);
            Dekor_RuzgarGecidi(p);
            p.OriginX = DerinGaleriX;
            Maden_DerinGaleri(p);
            Dekor_DerinGaleri(p);
            p.OriginX = SonTirmanisX;
            Maden_SonTirmanis(p);
            Dekor_SonTirmanis(p);

            // Botun geri dönmesi ya da durup beklemesi gereken yerler; dünya koordinatı.
            p.OriginX = 0f;
            p.Route(194f, 4f, 194f, 16f, 201f, 16f, 210f, 15f, 303f, 17f, 309f, 12f, 298f, 7f, 315f, 1f);

            p.ShotPoints.AddRange(new[]
            {
                new Vector2(20f, 3f), new Vector2(45f, 7f), new Vector2(72f, 12f), new Vector2(103f, 9f), new Vector2(135f, 4f),
                new Vector2(152f, 4f), new Vector2(172f, 9f), new Vector2(192f, 11f), new Vector2(228f, 17f), new Vector2(250f, 19f),
                new Vector2(282f, 20f), new Vector2(302f, 14f), new Vector2(340f, 5f), new Vector2(370f, 6f), new Vector2(408f, 12f),
                new Vector2(450f, 22f), new Vector2(8f, 3f), new Vector2(88f, 14f), new Vector2(214f, 17f), new Vector2(424f, 22f), new Vector2(485f, 26f),
            });
        }

        // Kanyon Ağzı (yüzey). Fikir: hareketli platform.
        static void Maden_KanyonAgzi(LevelPainter p)
        {
            p.Ground(-8f, 12f, 0f);
            p.Plant("kaktus_07", -4f, 0f);
            p.Sign(1.5f, 0f, "Terk Edilmiş Maden\nBu sefer yol daha sert");
            p.CrystalLine(4f, 0.7f, 2, 1f);
            p.Scorpion(8f, 0f);

            // Tanıtım: tek platform, kısa boşluk
            p.Mover(15f, 1f, "tahta_zincir", new Vector2(6f, 0f), 2.2f);
            p.CrystalArc(16f, 2.5f, 21f, 2.5f, 3);

            // Düşman ikilisi: yılan mermisi oyuncuyu akrebin üstüne sıçratır
            p.Ground(24f, 40f, 2f);
            p.Scorpion(28f, 2f);
            p.Plant("kaktus_19", 31f, 2f);
            p.Snake(35f, 2f);

            // Sertleşme: yüksek kayaya asansörle (ya da tırmanarak) çık
            p.Mover(43f, 3f, "tahta_tekerlek", new Vector2(0f, 7.5f), 2f);
            p.Crystal(43f, 7f);
            p.Crystal(43f, 9f);
            p.Ground(46f, 62f, 10f);
            p.Checkpoint(48f, 10f);
            p.Plant("kaktus_05", 52f, 10f);
            p.SahteCali(50f, 10f);
            p.Jackal(57f, 10f);

            // Bükülme: yatay ve dikey platform sırayla; biri kaçarsa diğeri uzaklaşmış olur
            p.Mover(65f, 10f, "tahta_zincir", new Vector2(6f, 0f), 2.5f);
            p.Crystal(68f, 11.5f);
            p.Mover(76f, 8f, "tahta_tekerlek", new Vector2(0f, 5f), 2f, 0.5f);
            p.Crystal(76f, 14.3f);
            p.Platform(71.5f, 15.8f, "ada_2"); // asansörün tepesinden çift zıplamayla çıkılan ödül adası
            p.CrystalLine(70.5f, 16.8f, 3, 1f);

            p.Ground(79f, 100f, 12f);
            p.Scorpion(83f, 12f);
            p.Table(87f, 12f, "iskele_kirmizi");
            p.Crystal(87f, 14.2f);
            p.Snake(91f, 12f);
            p.Plant("kaktus_16", 94.5f, 12f);
            p.SahteCali(84f, 12f);
            p.Fare(95.5f, 12f);
            p.Sign(97f, 12f, "Eski maden kuyusu.\nTahtalar çürük, dikkat!");
        }

        // Maden Şaftı (yer altı). Giriş: üstünden geçilen tahtalar yıkılır, oyuncu kuyuya düşer.
        // Fikir: düşen kaya (önce boş yerde, sonra sıra sıra). Çıkış: düşman ateşi altında asansör. Tavanda gizli oda.
        static void Maden_Safti(LevelPainter p)
        {
            const float floorA = 1f, roofA = 9.5f, floorB = 4f, roofB = 11f, roofC = 22f, exitY = 16f;

            p.CaveMouth(0f, 8f, 12f, 5f);
            p.CaveWall(8f, 101f, roofC);
            p.RockMass(8f, 62f, roofA, 60f);
            p.RockEdge(8f, 40f, roofA, -1);
            p.RockMass(62f, 66f, roofB, 60f);
            p.RockMass(66f, 76f, 15f, 60f);
            p.RockMass(76f, 80f, roofB, 60f);
            p.RockMass(80f, 104f, roofC, 60f);
            p.RockEdge(80f, roofC, roofB, 1);
            p.RockMass(100f, 104f, GroundBottom, exitY - 0.5f); // kalın sağ duvar, kapı en üstte bir tünel
            p.RockMass(100f, 104f, exitY + 3.5f, roofC);
            p.RockEdge(100f, exitY - 0.5f, floorB, -1);
            p.RockEdge(100f, roofC, exitY + 3.5f, -1);
            p.RockEdge(104f, exitY - 0.5f, GroundBottom, 1);
            p.RockEdge(104f, 40f, exitY + 3.5f, 1);
            p.DoorFade(104f, 1, 4.5f, exitY - 0.5f, exitY + 3.5f);

            // Kuyu ağzı: üç çürük tahta, altında iki tane daha
            p.Crumble(1.33f, 12f, "tahta_ray");
            p.Crumble(4f, 12f, "tahta_ray");
            p.Crumble(6.67f, 12f, "tahta_ray");
            p.Crumble(1.5f, 7f, "tahta_ray");
            p.Crumble(6f, 7f, "tahta_ray");
            p.Crystal(4f, 8.5f);
            p.Crystal(4f, 3.5f);

            // Tanıtım: düşen kayayı boş yerde göster
            p.CaveFloor(0f, 30f, floorA);
            p.FallingRock(17f, roofA);
            p.Crystal(17f, 2f);
            p.Scorpion(25f, floorA);

            // Çürük köprü: durmadan koş
            p.Chasm(30f, 38f, floorA);
            p.Crumble(31.33f, floorA, "tahta_ray");
            p.Crumble(34f, floorA, "tahta_ray");
            p.Crumble(36.67f, floorA, "tahta_ray");
            p.Crystal(34f, 2.5f);

            // Sertleşme: sıra sıra kaya, aralarda düşman
            p.CaveFloor(38f, 62f, floorA);
            p.Checkpoint(39.5f, floorA);
            p.FallingRock(44f, roofA);
            p.FallingRock(47.5f, roofA);
            p.Scorpion(49f, floorA);
            p.FallingRock(51f, roofA);
            p.FallingRock(54.5f, roofA);
            p.Yarasa(56.5f, roofA); // kayalar düşerken tepeden dinamit: ikisine birden bak
            p.Snake(58f, floorA);
            p.Bug(60.5f, floorA + 1.3f);

            // Gizli oda: kristaller tavanın içini işaret eder, kaya sahtedir
            p.CaveFloor(62f, 100f, floorB);
            p.Checkpoint(64f, floorB);
            p.Ledge(68f, 74f, 7.5f);
            p.Crystal(71f, 8.3f);
            p.Crystal(71f, 10.2f);
            p.SecretRock(66f, 76f, roofB, 15f);
            p.Ledge(66.5f, 75.5f, 11.5f);
            p.CrystalLine(68f, 12.4f, 5, 1.5f);
            p.Bug(71f, 13.8f);

            // Çıkış: asansör, üstünden yılan ateş eder
            p.Jackal(84f, floorB);
            p.Ledge(82f, 90f, 10f);
            p.Snake(86f, 10f);
            p.Crystal(83f, 10.8f);
            p.Sign(89f, floorB, "Yuvarlanırken zırhı delinmez, atla!\nÇarpıp sersemleyince vur");
            p.Armadillo(93f, floorB);
            p.Mover(94f, 5f, "tahta_kafes", new Vector2(0f, 11f), 2.4f);
            p.Ledge(96.5f, 105f, exitY, register: true); // tünelden dışarı uzanır
            p.Crystal(98f, exitY + 0.8f);
        }

        // Rüzgâr Geçidi (yüzey). Fikir: sıcak hava sütunu, Gila'nın süzülüşünü yukarı taşır.
        static void Maden_RuzgarGecidi(LevelPainter p)
        {
            p.Ground(0f, 18f, 15f);
            p.Checkpoint(3f, 15f);
            p.Plant("kaktus_21", 6f, 15f);
            p.Snake(10f, 15f);
            p.Sign(15.5f, 15f, "Sıcak hava seni taşır:\natla, Space basılı tut");
            p.Fare(6.5f, 15f);

            // Tanıtım: tek sütun, kısa boşluk
            p.Thermal(19f, 24f, 8f, 21f, 17f);
            p.CrystalLine(21.5f, 15f, 3, 2f, vertical: true);
            p.Ground(26f, 36f, 18f);
            p.Plant("kaktus_12", 28f, 18f);
            p.Scorpion(32f, 18f);

            // Sertleşme: iki sütunla geniş kanyon; yere inecek yer yok
            p.Thermal(38f, 43f, 6f, 24f, 19f);
            p.Thermal(50f, 55f, 6f, 24f, 19f);
            p.CrystalArc(43.5f, 23f, 49.5f, 22f, 3);
            p.Platform(46.5f, 15f, "ada_3"); // sütundan düşene son şans
            p.Ground(60f, 72f, 19f);
            p.Checkpoint(62f, 19f);
            p.SahteCali(64f, 19f);
            p.Jackal(66f, 19f);
            p.Snake(70f, 19f);

            // Bükülme: sütun sadece yukarı çıkar, kuyunun ağzı karşı tepede
            p.Thermal(73f, 78f, 10f, 25f, 20f);
            p.Crystal(75.5f, 24f);
            p.Ground(80f, 88f, 22f);
            p.SahteCali(83f, 22f);
            p.Plant("kaktus_17", 82f, 22f);
            p.Scorpion(85f, 22f);
        }

        // Derin Galeri (yer altı). Giriş: sağa sola inilen zikzak kuyu. Fikir: dövüş odası.
        // Sonra öğrenilen her şey birleşir: çürük köprü + düşen kaya + hareketli platform. Çıkış: sıcak hava bacası.
        static void Maden_DerinGaleri(LevelPainter p)
        {
            const float floorA = 1f, roofA = 9f, floorB = 4f, roofB = 13f, shaftTop = 22f;

            p.CaveMouth(0f, 16f, shaftTop, 7f);
            p.CaveWall(16f, 110f, roofB);
            p.CaveMouth(110f, 118f, 20f, 6f);
            p.RockMass(16f, 62f, roofA, 60f);
            p.RockEdge(16f, 40f, roofA, -1);
            p.RockMass(62f, 110f, roofB, 60f);
            p.RockEdge(62f, roofB, roofA, 1);
            p.RockEdge(110f, 40f, roofB, 1); // kütlenin kenarı tavanda biter: galeride içinden geçilen sahte duvar olmasın
            p.RockMass(117f, 118f, GroundBottom, 19.6f); // bacanın sağı tırmanılmaz: yukarı sadece rüzgârla çıkılır

            // Zikzak kuyu: her raf bir sonrakine sağdan ya da soldan iner
            p.CaveFloor(0f, 62f, floorA);
            p.Shelf(0f, 9f, 17f, 1.2f, -1);
            p.Crystal(4f, 17.8f);
            p.Shelf(7f, 16f, 12f, 1.2f, 1);
            p.Scorpion(12f, 12f);
            p.Shelf(0f, 9f, 7f, 1.2f, -1);
            p.Snake(4f, 7f);
            p.CrystalLine(11f, 2f, 3, 1.2f);

            // Dövüş odası: kapılar iner, dördü de ölmeden açılmaz. Çıkıntılar kaçış yeri.
            p.Checkpoint(26f, floorA);
            p.Arena(30f, 60f, 36f, floorA, roofA);
            p.Ledge(38f, 44f, 4.2f);
            p.Ledge(47f, 53f, 4.2f);
            p.Crystal(41f, 5f);
            p.Crystal(50f, 5f);
            p.Scorpion(42f, floorA);
            p.Jackal(47f, floorA);
            p.Snake(53f, floorA);
            p.Armadillo(57f, floorA);
            p.Yarasa(45f, roofA, wakeHalfWidth: 15f); // kapılar inince odanın tavanından uyanır

            // Birleşim: çürük köprü, tavandan kaya, ortada hareketli platform
            p.Chasm(62f, 88f, floorA);
            p.Crumble(63.33f, floorA, "tahta_ray");
            p.Crumble(66f, floorA, "tahta_ray");
            p.Crumble(68.67f, floorA, "tahta_ray");
            p.Crumble(71.33f, floorA, "tahta_ray");
            p.FallingRock(65.5f, roofB);
            p.FallingRock(70f, roofB);
            p.Mover(74f, 2.5f, "tahta_zincir", new Vector2(7f, 0f), 2.5f);
            p.CrystalArc(74f, 4.5f, 81f, 4.5f, 3);
            p.Crumble(83.33f, 2.5f, "tahta_ray");
            p.Crumble(86f, 2.5f, "tahta_ray");
            p.FallingRock(85f, roofB);

            p.CaveFloor(88f, 118f, floorB);
            p.Checkpoint(89.5f, floorB);
            p.Bug(92f, floorB + 1.3f);
            p.Yarasa(95f, roofB); // galerinin tavanında uyur, yılanla birlikte iki kat baskı
            p.Snake(97f, floorB);
            p.Armadillo(103f, floorB);
            p.Sign(108.5f, floorB, "Sıcak hava bacası:\nSpace basılı tut, yukarı!");

            // Baca: yer altından gökyüzüne
            p.Thermal(111f, 117f, floorB, 24f, 16f);
            p.CrystalLine(114f, 8f, 5, 2.5f, vertical: true);
        }

        // Son Tırmanış (yüzey). Yeni fikir yok: tahta, platform, rüzgâr art arda, sonunda en kalabalık grup.
        static void Maden_SonTirmanis(LevelPainter p)
        {
            p.Ground(0f, 20f, 20f);
            p.Checkpoint(2.5f, 20f);
            p.Plant("kaktus_25", 6f, 20f);
            p.Snake(10f, 20f);
            p.SahteCali(13f, 20f);
            p.Jackal(16f, 20f);
            p.Fare(18.5f, 20f);

            p.Crumble(21.33f, 20f, "tahta_ray");
            p.Crumble(24f, 20f, "tahta_ray");
            p.Crumble(26.67f, 20f, "tahta_ray");
            p.Mover(31f, 20f, "tahta_zincir", new Vector2(8f, 0f), 2.5f);
            p.Crystal(35f, 21.5f);
            p.Thermal(42f, 47f, 12f, 27f, 22f);
            p.Crystal(44.5f, 25f);

            p.Ground(49f, 58f, 22f);
            p.Bug(51f, 23.3f);
            p.Scorpion(55f, 22f);
            p.Platform(60.5f, 23f, "ada_1");
            p.Crystal(60.5f, 23.8f);

            p.Ground(63f, 82f, 24f);
            p.SahteCali(65f, 24f);
            p.Snake(67f, 24f);
            p.Fare(75.5f, 24f);
            p.Table(70f, 24f, "tahta_korkuluk");
            p.Crystal(70f, 26.2f);
            p.Jackal(73f, 24f);
            p.Plant("kaktus_28", 75.5f, 24f);
            p.Jackal(77f, 24f);
            p.Goal(80f, 24f);
            p.Boundary(82f);
        }

        // Level 3: Ayaz Gecesi. Gila soğukkanlı: gece üşür, yavaşlar; ateşten ateşe ilerler.
        // Her bölümün kendi fikri var: ısı, dille sallanma, gayzer ve yükselen çamur, fırtınadan kaçış, şafak.
        const float TelgrafX = 100f, KaplicaX = 205f, CepheX = 330f, SafakX = 455f;

        static void Level_3(LevelPainter p)
        {
            p.Spawn = new Vector2(0f, 0f);
            p.Night = true;
            p.DawnStartX = SafakX + 2f;
            p.DawnEndX = SafakX + 40f;
            p.Boundary(-9f);

            Ayaz_Kamp(p);
            p.OriginX = TelgrafX;
            Ayaz_TelgrafKanyonu(p);
            p.OriginX = KaplicaX;
            Ayaz_Kaplica(p);
            p.OriginX = CepheX;
            Ayaz_SogukCephe(p);
            p.OriginX = SafakX;
            Ayaz_Safak(p);

            // Botun durup bekleyeceği ya da sırayla tırmanacağı yerler; dünya koordinatı.
            p.OriginX = 0f;
            p.Route(262f, 3f, 272f, 8.5f, 278f, 8.5f, 287f, 13.5f, 290f, 13.5f, 297f, 8f, 308f, 6f, 309f, 9.5f, 319f, 13f, 309f, 16.5f, 319f, 20f, 327f, 24f);
            p.ShotPoints.AddRange(new[]
            {
                new Vector2(5f, 3f), new Vector2(40f, 5f), new Vector2(56f, 5f), new Vector2(112f, 7f), new Vector2(135f, 8f),
                new Vector2(160f, 8f), new Vector2(190f, 12f), new Vector2(212f, 11f), new Vector2(262f, 8f), new Vector2(285f, 12f),
                new Vector2(315f, 13f), new Vector2(345f, 26f), new Vector2(395f, 22f), new Vector2(450f, 18f), new Vector2(480f, 18f), new Vector2(500f, 21f),
            });
        }

        // Ayaz Kampı (yüzey). Fikir: vücut ısısı. Ateşler arası mesafe giderek açılır, sonda kor arabası.
        static void Ayaz_Kamp(LevelPainter p)
        {
            // Tanıtım: ateşin başında başla, soğuğu tabela anlatır
            p.Ground(-8f, 16f, 0f);
            p.Fire(-2f, 0f);
            p.Deco("yuz_kamp", -5.5f, 0f);
            p.Sign(3f, 0f, "Güneş battı, çöl buz kesti.\nÜşüdükçe yavaşlarsın, ateşte ısın");
            p.CrystalLine(7f, 0.8f, 3, 1.2f);
            p.Plant("kaktus_07", 12f, 0f);

            p.Ground(16f, 30f, 1f);
            p.Scorpion(22f, 1f);
            p.Sign(26f, 1f, "Zümrüt böceği dille yakala (J):\nısıtır ve can verir");
            p.Firefly(28.5f, 2.4f);

            p.Ground(30f, 48f, 2f);
            p.Checkpoint(32f, 2f);
            p.Altar(35f, 2f, 3.6f); // checkpoint'in yanında uzaktan görünen sunak
            p.Deco("yuz_kazma", 39f, 2f);
            p.Snake(44f, 2f);
            p.Crystal(41f, 3f);

            // Sertleşme: uzun boşluk, tek ısı kaynağı üstünde binilen araba
            p.BrazierCart(51f, 2f, new Vector2(8f, 0f), 2.2f);
            p.CrystalArc(52f, 4f, 59f, 4f, 3);

            p.Ground(62f, 80f, 4f);
            p.Deco("yuz_araba", 66f, 4f);
            p.SahteCali(69f, 4f);
            p.Diken(75f, 4f); // arabadan inen Gila'ya yaylım: salvoların arasında zıpla, kelleşince vur
            p.Firefly(76f, 5.6f);

            // Bükülme: üstte ısınıp alta dalan iki yol; ateş sadece üst yolda
            p.Ground(84f, 100f, 3f);
            p.Checkpoint(86f, 3f);
            p.Altar(89f, 3f);
            p.Fare(93f, 3f);
            p.Pole(97f, 3f, 7.2f);
            p.Sign(94.5f, 3f, "Eski telgraf hattı. Fenere dille yapış:\nJ basılı sallan, bırakınca uç");
        }

        // Telgraf Kanyonu (yüzey). Fikir: dille sallanma. Tek fener, zincir, hareketli, sonra yukarı doğru.
        static void Ayaz_TelgrafKanyonu(LevelPainter p)
        {
            const float wireTop = 10f;

            // Tanıtım: tek fener, kısa boşluk; süzülerek de geçilir, korkmadan denensin
            p.Wire(-3f, wireTop, 19f, wireTop);
            p.Hook(8f, 7.5f);
            p.CrystalArc(4f, 6f, 12f, 6f, 3);
            p.Ground(16f, 26f, 3f);
            p.Pole(19f, 3f, 7f);
            p.Scorpion(23f, 3f);

            // Zincir: artık süzülüş yetmez, iki fener arka arkaya
            p.Pole(25f, 3f, 7f);
            p.Wire(25f, wireTop, 56f, wireTop);
            p.Hook(33f, 7.5f);
            p.Hook(42f, 7.5f);
            p.Firefly(37.5f, 5.5f);
            p.Ground(50f, 62f, 3f);
            p.Checkpoint(52f, 3f);
            p.Yarasa(54.5f, wireTop); // telgraf telinde baş aşağı uyur
            p.Pole(56f, 3f, 7f);
            p.Snake(59f, 3f);

            // Hareketli: hat üstünde makaralı fener
            p.Pole(61f, 3f, 8f);
            p.Wire(61f, 11f, 86f, 11f);
            p.MovingHook(66f, 8f, 10f, 11f, 3f);
            p.CrystalArc(67f, 6f, 76f, 6f, 3);
            p.Ground(83f, 90f, 5f);
            p.Pole(86f, 5f, 6f);
            p.Plant("kaktus_12", 88f, 5f);

            // Bükülme: yukarı doğru iki fener, karşı kayada yılan
            p.Pole(89f, 5f, 9f);
            p.Wire(89f, 14f, 118f, 15f);
            p.Hook(95f, 11f);
            p.Hook(104f, 12.5f);
            p.Crystal(99.5f, 10f);
            p.Ground(112f, 129f, 9f);
            p.Checkpoint(113.5f, 9f);
            p.Yarasa(116f, 14.95f); // eğik telin ucunda: zırhlıyla birlikte
            p.Altar(116f, 9f);
            p.Pole(118f, 9f, 6f);
            p.Armadillo(121f, 9f);
        }

        // Kaplıca (yer altı). Giriş: sıcak yamaçtan yürüyerek. Fikir: gayzer; çıkış: yükselen çamurda baca tırmanışı.
        // Sıcakta Gila hızlanır: soğuk yüzeyin tam tersi.
        static void Ayaz_Kaplica(LevelPainter p)
        {
            const float roofA = 15f, roofB = 23f, roofShaft = 30f, doorY = 24f;

            // Kaya kütlesi: tünel, sonra yükselen tavan
            p.CaveWall(24f, 125f, 40f);
            p.RockMass(24f, 44f, 15f, 60f);
            p.RockEdge(24f, 15f, 9f, -1);
            p.DoorFade(24f, -1, 4f, 9f, 15f);
            p.RockMass(44f, 62f, roofA, 60f);
            p.RockMass(62f, 100f, roofB, 60f);
            p.RockEdge(62f, roofB, roofA, -1);
            p.RockMass(100f, 125f, roofShaft, 60f);
            p.RockEdge(100f, roofShaft, roofB, -1);

            p.HotZone(26f, 125f, -5f, 40f);
            p.Sign(28f, 9f, "Kaplıcanın sıcağı içine işler:\nburada daha hızlısın");
            Dekor_Kaplica(p);

            // Tünelden aşağı basamak basamak
            p.CaveFloor(24f, 36f, 9f);
            p.CaveFloor(36f, 40f, 7.5f);
            p.CaveFloor(40f, 44f, 5.5f);
            p.CaveFloor(44f, 62f, 3f);
            p.Checkpoint(49f, 3f);
            p.Yarasa(45.5f, 15f); // tünelden inen basamakların tavanında
            p.Firefly(52f, 4.6f);

            // Tanıtım: taş üstünde gayzer. Bekle, fışkırınca havalan.
            p.Sign(55f, 3f, "Gayzer kabarınca üstüne gel,\nseni yukarı fırlatır");
            p.Geyser(60f, 3f, 8f, 3.6f, 0f);
            p.BoilingPool(62f, 100f, 1.5f);
            p.Ledge(64f, 72f, 8.5f, register: true);
            p.CrystalLine(66f, 9.4f, 3, 2f);
            p.Scorpion(70f, 8.5f);

            // Sertleşme: kaya kaya gayzerler, tek sıra
            p.Slab(72f, 76f, 8.5f, 1.2f);
            p.Geyser(75f, 8.5f, 8.5f, 3.6f, 0.35f);
            p.Ledge(78f, 86f, 13.5f, register: true);
            p.Snake(84f, 13.5f);
            p.Crystal(80f, 14.3f);

            // Bükülme: iki gayzer sırayla; ilki ikinciye atar
            p.Slab(86f, 89f, 13.5f, 1.2f);
            p.Geyser(87.5f, 13.5f, 6f, 3.2f, 0.6f);
            p.Ledge(92f, 100f, 8f, register: true); // alçakta: baca dibinden girilir, üstten atlanamaz
            p.Checkpoint(94f, 8f);
            p.Bug(97f, 9.4f);

            // Baca: dibi kapalı oda, çamur yükselir. Raflar sağa sola, ortadaki sütuna da tırmanılır.
            p.CaveFloor(100f, 118f, 6f);
            p.Sign(104f, 6f, "Bu çatlak kaynıyor.\nHızlı tırman!");
            p.Slab(99f, 100f, 26f, 15f, fringe: false); // yan duvarlar tırmanılır: çamurdan kaçışın ikinci yolu
            p.RockEdge(100f, 26f, 11f, 1);
            p.Slab(117.5f, 118f, doorY, doorY - 6.5f, fringe: false);
            p.Ledge(101f, 107f, 9.5f);
            p.Ledge(111f, 117f, 13f);
            p.Ledge(101f, 107f, 16.5f);
            p.Ledge(111f, 117f, 20f);
            p.Crystal(104f, 10.3f);
            p.Crystal(114f, 13.8f);
            p.Crystal(104f, 17.3f);
            p.Crystal(114f, 20.8f);
            p.Mud(100f, 118f, 3f, doorY - 1f, 7.5f, 2.1f); // tabanın 3 birim altından, hızlı: arkana bakmadan tırman

            // Kapının altındaki kaya tam kapı zemininde biter: tek yönlü rafla arasında basamak kalmasın.
            // Yarım birim aşağıda bitince köşeden zıplayan Gila rafla kayanın arasına sıkışıp kayanın içine itiliyordu.
            p.RockMass(118f, 125f, GroundBottom, doorY);
            p.RockMass(118f, 125f, doorY + 4f, roofShaft);
            p.RockEdge(118f, doorY, 6f, -1);
            p.RockEdge(125f, doorY, GroundBottom, 1);
            p.DoorFade(125f, 1, 4.5f, doorY - 0.5f, doorY + 4f);
            p.Ledge(106f, 126f, doorY, register: true);
        }

        // Sıcak ışıklar, kaynak çevresi mantar ve maden kalıntısı: dışarının mavisinden sonra içerisi turuncu olsun.
        static void Dekor_Kaplica(LevelPainter p)
        {
            foreach (var (x, y, radius) in new[] { (32f, 12f, 11f), (50f, 9f, 14f), (66f, 14f, 15f), (82f, 17f, 15f), (95f, 16f, 13f), (109f, 11f, 15f), (109f, 23f, 15f) })
                p.SpringGlow(x, y, radius, 1.9f);

            p.InCave = true;
            p.Deco("mgr_mantar_kutuk", 31f, 9f);
            p.Deco("mgr_cevher_turuncu", 34f, 9f);
            p.Deco("mgr_cevher_turuncu", 46.5f, 3f);
            p.Deco("mgr_vagon", 53f, 3f);
            p.Deco("mgr_mantar", 57f, 3f);
            p.Deco("mgr_varil", 103f, 6f);
            p.Deco("mgr_cevher_mavi", 113.5f, 6f);
            p.Hang("tavan_sarkit_2", 48f, 15f);
            p.Hang("tavan_kok", 54f, 15f);
            p.Hang("tavan_sarkit_1", 59f, 15f);
            p.Hang("tavan_kristal", 68f, 23f);
            p.Hang("tavan_sarkit_3", 74f, 23f);
            p.Hang("tavan_sarkit_2", 83f, 23f);
            p.Hang("tavan_damla", 91f, 23f);
            p.Hang("tavan_zincir", 96f, 23f);
            p.Hang("tavan_sarkit_2", 104f, 30f);
            p.Hang("tavan_kok", 113f, 30f);
            p.Drip(54f, 15f, 3.3f);
            p.Drip(83f, 23f, 14f);
            p.InCave = false;
        }

        // Soğuk Cephe (yüzey). Fikir: arkadan gelen fırtına. Durmadan koşulur; öğrenilen her şey hızlı hızlı gelir.
        static void Ayaz_SogukCephe(LevelPainter p)
        {
            p.Ground(0f, 22f, 24f);
            p.Checkpoint(2.5f, 24f);
            p.Altar(5.5f, 24f, 3.6f);
            p.Sign(9f, 24f, "Ufukta ayaz fırtınası!\nDurma, büyük ateşe koş");
            p.Storm(-14f, 12f, 122f, 5.3f); // koşu hızına yakın: durmak yakalanmak demek

            p.Ground(26f, 40f, 22f);
            p.Scorpion(33f, 22f);
            p.Crystal(30f, 23f);

            p.Crumble(41.33f, 22f, "tahta_ray");
            p.Crumble(44f, 22f, "tahta_ray");
            p.Crumble(46.67f, 22f, "tahta_ray");
            p.Ground(49f, 60f, 20f);
            p.Checkpoint(50.5f, 20f);
            p.Altar(53f, 20f);
            p.Jackal(57f, 20f);

            p.Pole(59f, 20f, 7f);
            p.Wire(59f, 27f, 78f, 25f);
            p.Hook(68f, 23.5f);
            p.CrystalArc(64f, 22f, 72f, 21f, 3);
            p.Ground(76f, 90f, 18f);
            p.Pole(78f, 18f, 7f);
            p.Yarasa(76.5f, 25.2f); // fırtına kovalarken telden dinamit: durmak yok
            p.Firefly(84f, 19.6f);
            p.SahteCali(86f, 18f);

            p.Thermal(90f, 96f, 10f, 26f, 22f);
            p.Crystal(93f, 23f);
            p.Ground(97f, 111f, 22f);
            p.Checkpoint(98.5f, 22f);
            p.Snake(104f, 22f);
            p.Fare(108f, 22f);

            // Son atlayış: aşağıda büyük ateş
            p.Ground(116f, 148f, 16f);
            p.Fire(124f, 16f, 5f);
            p.Fire(127f, 16f, 5f);
            p.Deco("yuz_kaya_1", 120f, 16f);
            p.Deco("yuz_cadir", 131f, 16f);
            p.Checkpoint(135f, 16f);
            p.Sign(138f, 16f, "Fırtına ateşe yaklaşamaz.\nGün doğmak üzere");
        }

        // Şafak (yüzey). Fikir: gece biter, ayaz kalkar. Son grup gün ışığında, bitiş tepede.
        static void Ayaz_Safak(LevelPainter p)
        {
            p.Scorpion(22f, 16f);
            p.Plant("kaktus_25", 18f, 16f);
            p.Ground(23f, 42f, 18f);
            p.Checkpoint(24.5f, 18f);
            p.SahteCali(28f, 18f);
            p.Snake(32f, 18f);
            p.Armadillo(38.5f, 18f);
            p.Ground(42f, 56f, 20f);
            p.Fare(51f, 20f); // bayrağın önünde: kenardan uzak, köşe kuralına takılmasın
            p.Deco("yuz_degirmen", 48f, 20f);
            p.Goal(53f, 20f);
            p.Boundary(56f);
        }


        // Dekorlar yalnızca görsel: oynanışa dokunmaz, bölümlerin hikâyesini anlatır.
        static readonly Color LampLight = new Color(1f, 0.72f, 0.36f, 0.4f);
        static readonly Color MushroomLight = new Color(1f, 0.9f, 0.5f, 0.32f);
        static readonly Color BlueLight = new Color(0.45f, 0.85f, 1f, 0.34f);
        static readonly Color OrangeLight = new Color(1f, 0.6f, 0.2f, 0.34f);
        static readonly Color VioletLight = new Color(0.65f, 0.5f, 1f, 0.3f);

        // Kanyonun kenarındaki son yerleşim: kurumuş ağaçlar, bozulmuş araba, kuyuyu gösteren tabela.
        static void Dekor_KanyonAgzi(LevelPainter p)
        {
            p.Deco("yuz_tabela_aranan", -1.5f, 0f);
            p.Deco("yuz_cali", 10f, 0f);
            p.Deco("yuz_kaya_1", 26f, 2f);
            p.Deco("yuz_agac_1", 38f, 2f);
            p.Deco("yuz_agac_2", 54.5f, 10f);
            p.Deco("yuz_direk", 60.5f, 10f);
            p.Deco("yuz_araba", 81.5f, 12f);
            p.Fence(89f, 96.5f, 12f);
            p.Deco("yuz_tabela_yon", 98.8f, 12f);
        }

        // Terk edilmiş işletme: kırık kütükler, kazma kürek yığını, patlayıcı deposu, asansör makinesi. Gizli odada kaybolmuş bir madenci.
        static void Dekor_MadenSafti(LevelPainter p)
        {
            const float a = 1f, b = 4f, roofA = 9.5f, roofB = 11f, roofC = 22f;

            p.InCave = true;
            p.Deco("yuz_kutuk", 4.5f, a);
            p.Hang("tavan_kok", 13f, roofA);
            p.Hang("tavan_sarkit_2", 21f, roofA);
            p.Fener(24f, a);
            p.Hang("tavan_damla", 28f, roofA);
            p.Drip(27.7f, roofA - 0.6f, a);
            p.Deco("mgr_su", 28f, a);

            p.Hang("tavan_kok", 34f, roofA);

            p.Deco("yuz_kazma", 42.5f, a);
            p.Hang("tavan_sarkit_3", 41f, roofA);
            p.Deco("yuz_kaya_2", 52.5f, a);
            p.Hang("tavan_zincir", 57f, roofA);
            p.Deco("mgr_tnt", 59f, a);
            p.Deco("mgr_mantar", 61f, a);
            p.Glow(61f, a + 0.5f, MushroomLight, 2.5f);

            p.Hang("tavan_sarmasik", 64.5f, roofB);
            p.Deco("mgr_cevher_mavi", 67f, b);
            p.Glow(67f, b + 0.5f, BlueLight, 2.5f);
            p.Deco("yuz_cuval", 71.5f, b);
            p.Fener(76f, b);

            // Gizli oda
            p.Deco("mgr_iskelet", 69f, 11.5f);
            p.Deco("yuz_kurukafa", 73.5f, 11.5f);
            p.Glow(71f, 13f, LampLight, 3f, 0.2f);

            p.Deco("mgr_cark", 90.5f, b);
            p.Deco("yuz_sandik_varil", 83f, b);
            p.Hang("tavan_zincir", 88f, roofC);
            p.Hang("tavan_ag", 82f, roofC);
            p.Fener(98f, b);
            p.InCave = false;
        }

        // Yeryüzünde madencilerin kampı ve kuyunun ağzındaki iskele.
        static void Dekor_RuzgarGecidi(LevelPainter p)
        {
            p.Deco("yuz_kamp", 9f, 15f);
            p.Deco("yuz_top_b", 13.5f, 15f);
            p.Deco("yuz_kurukafa", 30f, 18f);
            p.Deco("yuz_agac_2", 34.5f, 18f);
            p.Deco("yuz_sandik_varil", 65f, 19f);
            p.Deco("yuz_cuval", 70f, 19f);
            p.Deco("yuz_kova_direk", 85.5f, 22f);
        }

        // Daha derin ve daha doğal: kökler, kristaller, mantarlar. Sonda buhar borularının geldiği baca.
        static void Dekor_DerinGaleri(LevelPainter p)
        {
            const float a = 1f, b = 4f, roofA = 9f, roofB = 13f;

            p.InCave = true;
            p.Hang("tavan_kok", 5f, 15.3f);
            p.Deco("mgr_cevher_turuncu", 14f, 12f);
            p.Glow(14f, 12.5f, OrangeLight, 2.5f);
            p.Hang("tavan_sarmasik", 11f, 10.3f);
            p.Hang("tavan_sarkit_2", 3f, 4.9f);
            p.Deco("mgr_kristal_kucuk", 1.5f, 7f);
            p.Glow(1.5f, 7.5f, VioletLight, 1.8f);
            p.Deco("mgr_mantar", 11.5f, a);
            p.Glow(11.5f, a + 0.5f, MushroomLight, 2.5f);

            p.Hang("tavan_sarkit_2", 19f, roofA);
            p.Deco("yuz_mezar", 22.5f, a);
            p.Deco("yuz_kurukafa", 19f, a);

            // Dövüş odası
            p.Deco("mgr_dikit_2", 33.5f, a);
            p.Deco("yuz_sandik_varil", 44.5f, a);
            p.Hang("tavan_zincir", 45f, roofA);
            p.Hang("tavan_sarkit_2", 35f, roofA);
            p.Deco("mgr_tnt", 55f, a);
            p.Fener(50.5f, a);
            p.Hang("tavan_sarkit_3", 53f, roofA);

            // Köprü
            p.Hang("tavan_kristal", 63f, roofB);
            p.Glow(63f, roofB - 0.8f, VioletLight, 2f);
            p.Hang("tavan_sarmasik", 75f, roofB);
            p.Hang("tavan_kok", 79f, roofB);

            p.Hang("tavan_ag", 90f, roofB);
            p.Deco("mgr_kristal_siyah", 93.5f, b);
            p.Glow(93.5f, b + 1f, VioletLight, 3f);
            p.Deco("mgr_dikit_1", 96f, b);
            p.Deco("mgr_mantar_kutuk", 100.5f, b);
            p.Glow(100.5f, b + 1f, MushroomLight, 3f);
            p.Hang("tavan_damla", 105.5f, roofB);
            p.Drip(105.2f, roofB - 0.6f, b);
            p.Deco("mgr_su", 105.5f, b);

            // Baca: sıcak hava borulardan geliyor
            // Duvardan duvara uzanan boru ağı; üstte gökyüzüne açılır
            p.PipeUp(111.4f, b, 19.4f);
            p.PipeUp(114f, b, 19.4f);
            p.PipeUp(116.3f, b, 19.4f);
            p.PipeAcross(110.2f, 111.4f, 12.5f);
            p.PipeAcross(111.4f, 114f, 7f);
            p.PipeAcross(114f, 116.3f, 10f);
            p.PipeAcross(111.4f, 116.3f, 15.5f);
            p.PipeAcross(116.3f, 117.2f, 6f);
            p.PipeValve(111.4f, 9.5f);
            p.PipeValve(116.3f, 13f);
            p.PipeValve(114f, 17.5f);
            p.Glow(114f, b + 1f, OrangeLight, 4f);
            p.InCave = false;
        }

        // Finale doğru: yel değirmeni, kaya, son kamp. Bitişin yanında aranıyor ilanı.
        static void Dekor_SonTirmanis(LevelPainter p)
        {
            p.Deco("yuz_degirmen", 13f, 20f);
            p.Deco("yuz_kaya_2", 3.8f, 20f);
            p.Deco("yuz_cali", 53f, 22f);
            p.Deco("yuz_kaya_1", 65.5f, 24f);
            p.Deco("yuz_aranan_kavanoz", 76f, 24f);
        }

        // Level 1 dekoru: oynanışa dokunmaz.
        static void Dekor_IlkAdimlar(LevelPainter p)
        {
            p.Deco("yuz_tabela_yon", -1f, 0f);
            p.Deco("yuz_top_b", 12f, 0f);
            p.Deco("yuz_agac_1", 21.5f, 0f);
            p.Deco("yuz_kaya_2", 27f, 0f);
            p.Fence(34.6f, 46f, 2f); // sağ ucu kayaya dayanır
            p.Deco("yuz_degirmen", 60f, 6f);
            p.Deco("yuz_kurukafa", 67f, 6f);
            p.Deco("yuz_araba", 80.5f, 12f);
            p.Deco("yuz_direk", 108f, 10f);
            p.Deco("yuz_kaya_1", 123.5f, 10f);
            p.Deco("yuz_tabela_yon_2", 129f, 10f);
        }

        static void Dekor_KanyonDuvarlari(LevelPainter p)
        {
            p.Deco("yuz_agac_2", 13f, 0f);
            p.Deco("yuz_kamp", 44f, 1f);
            p.Deco("yuz_mezar", 62f, 12f);
            p.Deco("yuz_cuval", 88.5f, 14f);
            p.Fence(109.5f, 121f, 13f);
            p.Deco("yuz_kova_direk", 133.5f, 13f);
        }

        static void Dekor_AkbabaGolgesi(LevelPainter p)
        {
            p.Deco("yuz_tabela_yon_2", 6.5f, 0f);
            p.Deco("yuz_agac_1", 94f, 9f);
            p.Deco("yuz_cadir", 153.5f, 16f);
            p.Deco("yuz_aranan_kavanoz", 164.5f, 16f);
        }

        // =====================================================================
        // Level içi menüler
        // =====================================================================

        static LevelCompleteUI BuildCompletePanel(Transform canvas)
        {
            RectTransform overlay = UiKit.Overlay("LevelSonu", canvas, new Color(0f, 0f, 0f, 0.55f));
            RectTransform box = UiKit.Panel("Kutu", overlay, Vector2.zero, new Vector2(280f, 200f));
            Text title = UiKit.Shadowed(UiKit.Label("Baslik", box, "Level tamamlandı!", new Vector2(0f, 68f), new Vector2(260f, 30f), 22, UiKit.ButtonHighlight));
            Text crystals = UiKit.Label("Kristal", box, "Altın: 0", new Vector2(0f, 36f), new Vector2(260f, 22f), 15, UiKit.LightText);
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
            const float BoundaryTop = 60f;
            readonly List<(float xMin, float xMax, float top)> grounds = new List<(float, float, float)>();
            readonly List<float> boundaries = new List<float>();
            readonly Dictionary<(float, float, float), Transform> groundObjects = new Dictionary<(float, float, float), Transform>();
            // Sadece havadaki platformlar; yerdeki masalar boşluk geçişine yardım etmez.
            readonly List<(float xMin, float xMax, float top)> platforms = new List<(float, float, float)>();
            readonly List<float> checkpoints = new List<float>();
            readonly List<(float xMin, float xMax)> indoors = new List<(float, float)>();
            readonly List<(float x, float half)> occupied = new List<(float, float)>();
            readonly List<(float x, float y, float half)> bigDecor = new List<(float, float, float)>();
            readonly List<(float xMin, float xMax, float top)> lifts = new List<(float, float, float)>();
            readonly List<(float xMin, float xMax, float top)> caveFloors = new List<(float, float, float)>();
            int count;
            bool hasGoal;

            public Vector2 Spawn;
            /// <summary>Bölüm tanımlarındaki x'lere eklenir; ShotPoints ve Spawn dünya koordinatıdır.</summary>
            public float OriginX;
            public readonly List<Vector2> ShotPoints = new List<Vector2>();

            static Sprite signSprite, poleSprite, flagSprite, pitSprite, glowSprite;

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
                var sorted = grounds.OrderBy(g => g.xMin).ToList();
                for (int i = 0; i + 1 < sorted.Count; i++)
                {
                    float x0 = sorted[i].xMax, x1 = sorted[i + 1].xMin;
                    if (x1 - x0 <= 0f)
                        continue;
                    float rim = Mathf.Min(sorted[i].top, sorted[i + 1].top);
                    Pit(x0, x1, rim - 0.05f, -50);
                    OutdoorPitZones(x0, x1, rim);
                }
            }

            // Zeminler arası boşluk mağaranın üstüne denk gelebilir: orası uçurum değil, tabanı var.
            void OutdoorPitZones(float x0, float x1, float rim)
            {
                float from = x0;
                foreach (var r in indoors.OrderBy(r => r.xMin))
                {
                    if (r.xMax <= from || r.xMin >= x1)
                        continue;
                    if (r.xMin > from)
                        PitKillZone(from, r.xMin, rim);
                    from = Mathf.Max(from, r.xMax);
                }
                if (from < x1)
                    PitKillZone(from, x1, rim);
            }

            /// <summary>Alçak kenarın bu kadar altına inen oyuncu düşmüş sayılır; bol pay süzülüşe ve yapışmaya yer bırakır.</summary>
            void PitKillZone(float x0, float x1, float rim)
            {
                const float depthBelowRim = 4.5f, zoneHeight = 40f;
                var go = new GameObject(Name("UcurumBolgesi"));
                go.transform.SetParent(geometry);
                go.transform.position = new Vector3((x0 + x1) / 2f, rim - depthBelowRim - zoneHeight / 2f, 0f);
                go.AddComponent<BoxCollider2D>().size = new Vector2(x1 - x0, zoneHeight);
                go.AddComponent<PitZone>();
            }

            // order: açık havada arka planın, mağarada arka duvarın hemen önünde; dekorun arkasında.
            void Pit(float x0, float x1, float top, int order)
            {
                const float fadeHeight = 2.5f, bottom = GroundBottom - 4f;
                float width = x1 - x0;

                var pit = new GameObject(Name("Ucurum")).transform;
                pit.SetParent(geometry);
                pit.position = new Vector3((x0 + x1) / 2f, top, 0f);

                var fade = new GameObject("Gecis").AddComponent<SpriteRenderer>();
                fade.transform.SetParent(pit, false);
                fade.sprite = pitSprite;
                fade.sortingOrder = order;
                fade.transform.localScale = new Vector3(width / pitSprite.bounds.size.x, fadeHeight / pitSprite.bounds.size.y, 1f);

                var solid = new GameObject("Karanlik").AddComponent<SpriteRenderer>();
                solid.transform.SetParent(pit, false);
                solid.sprite = ctx.Solid;
                float depth = top - fadeHeight - bottom;
                Vector2 unit = solid.sprite.bounds.size;
                solid.transform.localScale = new Vector3(width / unit.x, depth / unit.y, 1f); // düz renk: tekrar değil ölçek
                solid.transform.localPosition = new Vector3(0f, -fadeHeight - depth / 2f, 0f);
                solid.color = new Color32(0x1A, 0x0C, 0x09, 0xFF); // zeminlerin derin toprağıyla aynı: dipte düz siyah blok durmasın
                solid.sortingOrder = order;
            }

            /// <summary>
            /// Her açık zemin kenarına pürüzlü kaya sütunu, omuz ve dibine taş yığını koyar; cetvelle
            /// kesilmiş köşeleri ve alt zeminle birleşimi örter. Yalnızca görsel, çarpışma değişmez.
            /// </summary>
            public void DressCliffs()
            {
                const float eps = 0.01f, minHeight = 0.8f, minRubbleRoom = 4f;
                foreach (var g in grounds)
                {
                    foreach (int side in new[] { -1, 1 }) // zeminin sol ve sağ kenarı
                    {
                        float x = side < 0 ? g.xMin : g.xMax;
                        if (boundaries.Any(b => Mathf.Abs(x - b) < eps || Mathf.Abs(x - (b + 1f)) < eps))
                            continue; // orada dağ var
                        var next = grounds.Concat(caveFloors).Where(o => !o.Equals(g) && o.xMin <= x + eps && o.xMax >= x - eps).ToList();
                        bool pit = next.Count == 0;
                        float low = pit ? GroundBottom : next.Max(o => o.top);
                        if (g.top - low < minHeight)
                            continue;
                        CliffEdge(x, g.top, side);
                        if (!pit && next.Max(o => o.xMax - o.xMin) >= minRubbleRoom)
                            Rubble(x, low, side);
                    }
                }
            }

            /// <summary>Yer altı tabanlarının açık kenarlarını da aynı pürüzlü yamaçla örter. Yalnızca görsel.</summary>
            public void DressCaveFloors()
            {
                const float eps = 0.01f, minHeight = 1f, minRubbleRoom = 4f;
                var all = grounds.Concat(caveFloors).ToList();
                foreach (var f in caveFloors)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        float x = side < 0 ? f.xMin : f.xMax;
                        var next = all.Where(o => !o.Equals(f) && o.xMin <= x + eps && o.xMax >= x - eps).ToList();
                        if (next.Count > 0 && f.top - next.Max(o => o.top) < minHeight)
                            continue; // komşusu aynı boyda ya da yüksek: kenar görünmez
                        CliffEdge(x, f.top, side, 2); // taban görseliyle aynı sırada olursa çizim sırası rastgele değişir
                        if (next.Count > 0 && next.Max(o => o.xMax - o.xMin) >= minRubbleRoom)
                            Rubble(x, next.Max(o => o.top), side);
                    }
                }
            }

            void CliffEdge(float x, float top, int side, int order = 1, float bottom = GroundBottom)
            {
                const float faceOffset = 0.375f; // görselde yamaç yüzeyi, ortanın bu kadar dışında
                Sprite face = LayerArt("zemin_yamac"), cap = LayerArt("zemin_yamac_ust");
                float capHeight = cap.bounds.size.y;
                var position = new Vector2(x - side * faceOffset, top);

                var capSr = new GameObject(Name("YamacUst")).AddComponent<SpriteRenderer>();
                capSr.transform.SetParent(geometry);
                capSr.transform.position = position;
                capSr.sprite = cap;
                capSr.flipX = side > 0;
                capSr.sortingOrder = order;

                SpriteRenderer sr = TiledArt(geometry, Name("Yamac"), face, position + Vector2.down * capHeight,
                    new Vector2(face.bounds.size.x, top - capHeight - bottom), order);
                sr.flipX = side > 0;
            }

            void Rubble(float x, float groundTop, int side)
            {
                Sprite s = LayerArt("zemin_yamac_dip");
                var sr = new GameObject(Name("Tas")).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(geometry);
                sr.transform.position = new Vector2(x + side * s.bounds.size.x / 2f, groundTop + s.bounds.size.y - 0.06f);
                sr.sprite = s;
                sr.flipX = side > 0;
                sr.sortingOrder = 3;
            }

            /// <summary>Duvara dayalı ahşap merdiven. Görseldir; tırmanma duvar tırmanışıyla olur.</summary>
            public void Ladder(float x, float yMin, float yMax) =>
                TiledArt(geometry, Name("Merdiven"), LayerArt("merdiven"), new Vector2(x + OriginX, yMax), new Vector2(1f, yMax - yMin), 3);

            static Transform Child(Transform parent, string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent);
                return t;
            }

            string Name(string prefix) => $"{prefix}_{++count}";

            public void Ground(float xMin, float xMax, float top)
            {
                xMin += OriginX;
                xMax += OriginX;
                TestSceneBuilder.Ground(geometry, Name("Zemin"), xMin, xMax, GroundBottom, top, ctx.Tile, ctx.GroundLayer);
                grounds.Add((xMin, xMax, top));
                groundObjects[(xMin, xMax, top)] = geometry.GetChild(geometry.childCount - 1);
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
            public void BackWall(float xMin, float xMax, float yMin, float yMax)
            {
                indoors.Add((xMin + OriginX, xMax + OriginX));
                TiledArt(geometry, Name("ArkaDuvar"), LayerArt("kaya_arka"), new Vector2((xMin + xMax) / 2f + OriginX, yMax),
                    new Vector2(xMax - xMin, yMax - yMin), -40);
            }

            static readonly Color CaveWallTint = new Color(0.62f, 0.62f, 0.68f); // açık havadan belirgin biçimde karanlık

            /// <summary>Yer altının koyu arka duvarı: uçurumların dibine kadar iner, üstü çukurun ağzında biter.</summary>
            public void CaveWall(float xMin, float xMax, float top)
            {
                indoors.Add((xMin + OriginX, xMax + OriginX));
                SpriteRenderer sr = TiledArt(geometry, Name("MagaraDuvar"), LayerArt("kaya_arka"), new Vector2((xMin + xMax) / 2f + OriginX, top),
                    new Vector2(xMax - xMin, top - GroundBottom), -40);
                sr.color = CaveWallTint;
            }

            /// <summary>
            /// Gökyüzüne açılan mağara ağzı: arka duvar düz bir çizgiyle bitmez, yukarı doğru basamak basamak açılır.
            /// top: geçişin en üstü, fade: geçişin yüksekliği.
            /// </summary>
            public void CaveMouth(float xMin, float xMax, float top, float fade)
            {
                const int bands = 7;
                float solidTop = top - fade;
                CaveWall(xMin, xMax, solidTop);
                float bandHeight = fade / bands;
                for (int i = 0; i < bands; i++)
                {
                    SpriteRenderer sr = TiledArt(geometry, Name("MagaraAgzi"), LayerArt("kaya_arka"),
                        new Vector2((xMin + xMax) / 2f + OriginX, solidTop + (i + 1) * bandHeight), new Vector2(xMax - xMin, bandHeight), -40);
                    Color c = CaveWallTint;
                    c.a = 1f - (i + 1f) / (bands + 1f);
                    sr.color = c;
                }
            }

            /// <summary>
            /// Tünel kapısı: içerinin karanlığı dışarı doğru basamak basamak açılır, düz çizgiyle gökyüzüne geçilmez.
            /// outerX: tünelin dış ağzı, side: dışarının yönü (1 sağ, -1 sol), length: geçişin uzunluğu.
            /// </summary>
            public void DoorFade(float outerX, int side, float length, float yMin, float yMax)
            {
                float inner = outerX + OriginX - side * length;
                indoors.Add((Mathf.Min(inner, outerX + OriginX) - 1f, Mathf.Max(inner, outerX + OriginX) + 0.5f));
                var sr = new GameObject(Name("KapiGecisi")).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(geometry);
                sr.transform.position = new Vector3(outerX + OriginX, (yMin + yMax) / 2f, 0f);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, side > 0 ? -90f : 90f); // saydam ucu dışarı baksın
                sr.sprite = pitSprite;
                sr.sortingOrder = -39;
                sr.transform.localScale = new Vector3((yMax - yMin) / pitSprite.bounds.size.x, length / pitSprite.bounds.size.y, 1f);
            }

            /// <summary>
            /// Duvardan çıkan kaya rafı: kutu gibi değil, duvara yaklaştıkça kalınlaşan bir çıkıntı. Çarpışması düz Slab'dır,
            /// altındaki kademeler ve pürüzlü uç yalnızca görsel. wallSide: rafın yaslandığı duvarın yönü (-1 sol, 1 sağ).
            /// </summary>
            public void Shelf(float xMin, float xMax, float top, float thickness, int wallSide)
            {
                Slab(xMin, xMax, top, thickness, fringe: false);
                xMin += OriginX;
                xMax += OriginX;
                float width = xMax - xMin;
                float wallX = wallSide < 0 ? xMin : xMax;
                float freeX = wallSide < 0 ? xMax : xMin;
                CliffEdge(freeX, top, -wallSide, 3, top - thickness - 0.15f);

                float y = top - thickness;
                foreach ((float reach, float depth) in new[] { (0.72f, 0.5f), (0.42f, 0.45f) })
                {
                    float near = wallX, far = wallX - wallSide * width * reach;
                    float a = Mathf.Min(near, far), b = Mathf.Max(near, far);
                    SpriteRenderer belly = TiledArt(geometry, Name("RafKarni"), LayerArt("kaya_kutle"), new Vector2((a + b) / 2f, y),
                        new Vector2(b - a, depth), 2);
                    Fringe(belly.transform, a, b, y - depth);
                    CliffEdge(far, y, -wallSide, 3, y - depth - 0.1f);
                    y -= depth;
                }
                Rubble(wallX, top, -wallSide); // duvar dibinde taşlar: birleşim yeri görünmesin
            }

            /// <summary>Yer altı tabanı: dibe kadar katı kaya. Yanlarına Gila yapışıp tırmanabilir.</summary>
            public void CaveFloor(float xMin, float xMax, float top)
            {
                caveFloors.Add((xMin + OriginX, xMax + OriginX, top));
                Slab(xMin, xMax, top, top - GroundBottom, register: true);
            }

            /// <summary>Kayanın dik yüzüne pürüzlü kenar: cetvelle kesilmiş görünmesin. side: yüzün baktığı yön. Yalnızca görsel.</summary>
            public void RockEdge(float x, float yTop, float yBottom, int side)
            {
                const float faceOffset = 0.375f;
                Sprite face = LayerArt("zemin_yamac");
                SpriteRenderer sr = TiledArt(geometry, Name("KayaKenar"), face, new Vector2(x + OriginX - side * faceOffset, yTop),
                    new Vector2(face.bounds.size.x, yTop - yBottom), 3);
                sr.flipX = side > 0;
            }

            /// <summary>Yer altı uçurumu: dipsiz görünsün diye arka duvarın önüne karanlık koyar.</summary>
            public void Chasm(float xMin, float xMax, float top)
            {
                Pit(xMin + OriginX, xMax + OriginX, top - 0.05f, -35);
                PitKillZone(xMin + OriginX, xMax + OriginX, top);
            }

            /// <summary>
            /// Tavan ve dış cephe: katı kaya, Default katmanında. Gila çarpar ama yapışıp tırmanamaz;
            /// böylece mesanın üstüne çıkılıp katlar atlanamaz. Alt kenarından saçak sarkar.
            /// </summary>
            public void RockMass(float xMin, float xMax, float yMin, float yMax)
            {
                xMin += OriginX;
                xMax += OriginX;
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
            public void Slab(float xMin, float xMax, float top, float thickness = 1f, bool register = false, bool fringe = true)
            {
                xMin += OriginX;
                xMax += OriginX;
                var size = new Vector2(xMax - xMin, thickness);
                var center = new Vector2((xMin + xMax) / 2f, top);
                SpriteRenderer sr = TiledArt(geometry, Name("Doseme"), LayerArt("kaya_kutle"), center, size, 1);
                sr.gameObject.layer = ctx.GroundLayer;
                var col = sr.gameObject.AddComponent<BoxCollider2D>();
                col.size = size;
                col.offset = new Vector2(0f, -thickness / 2f);
                Sprite rim = LayerArt("kaya_ust");
                TiledArt(sr.transform, "Kenar", rim, center, new Vector2(size.x, rim.bounds.size.y), 2);
                if (fringe)
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
                xMin += OriginX;
                xMax += OriginX;
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
                    point.position = new Vector2(xy[i] + OriginX, xy[i + 1]);
                }
            }

            public void Wall(float xMin, float xMax, float yMin, float yMax) =>
                TestSceneBuilder.Block(geometry, Name("Duvar"), xMin + OriginX, xMax + OriginX, yMin, yMax, TestSceneBuilder.Rock, ctx.Tile, ctx.GroundLayer);

            /// <summary>
            /// Level kenarı: yüksek, 1 birim kalın görünmez duvar. Ground katmanında değil (Default), bu yüzden
            /// Gila çarpar ama yapışıp tırmanamaz, tepeden level dışına atlayamaz. Görünüşünü dağ verir.
            /// </summary>
            public void Boundary(float xMin)
            {
                xMin += OriginX;
                var wall = new GameObject(Name("Kenar")).AddComponent<BoxCollider2D>();
                wall.transform.SetParent(geometry);
                wall.transform.position = new Vector2(xMin + 0.5f, (GroundBottom + BoundaryTop) / 2f);
                wall.size = new Vector2(1f, BoundaryTop - GroundBottom);
                boundaries.Add(xMin);
            }

            /// <summary>
            /// Her level kenarının ardına dağ koyar; bitişik zeminin görselini dağın altına uzatır, böylece
            /// toprak katmanları dikişsiz devam eder. Zeminin çarpışması değişmez.
            /// </summary>
            public void DressBoundaries()
            {
                const float eps = 0.01f, rockAheadOfFace = 0.9f, soilWidth = 30f;
                Sprite mountain = LayerArt("dag");
                Vector2 size = mountain.bounds.size;
                foreach (float xMin in boundaries)
                {
                    // Sol kenarda level duvarın sağında, sağ kenarda solunda başlar.
                    var inner = grounds.Where(g => Mathf.Abs(g.xMin - (xMin + 1f)) < eps || Mathf.Abs(g.xMax - xMin) < eps).ToList();
                    bool levelOnRight = inner.Any(g => Mathf.Abs(g.xMin - (xMin + 1f)) < eps);
                    float top = inner[0].top;
                    float face = levelOnRight ? xMin + 1f : xMin;
                    int outward = levelOnRight ? -1 : 1;

                    WidenGroundArt(groundObjects[inner[0]], outward, soilWidth);

                    // Görselin dibindeki kaya yüzün önüne taşar: oyuncu önünden geçer, dekordur.
                    var sr = new GameObject(Name("Dag")).AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(geometry);
                    sr.transform.position = new Vector2(face + outward * (size.x / 2f - rockAheadOfFace), top + size.y - 0.06f);
                    sr.sprite = mountain;
                    sr.flipX = outward > 0;
                    sr.sortingOrder = 2;

                    ShotPoints.Add(new Vector2(face - outward * 5f, top + 1.5f));
                }
            }

            static void WidenGroundArt(Transform ground, int outward, float width)
            {
                var col = ground.GetComponent<BoxCollider2D>();
                col.offset -= new Vector2(outward * width / 2f, 0f);
                ground.position += new Vector3(outward * width / 2f, 0f, 0f);
                foreach (SpriteRenderer sr in ground.GetComponentsInChildren<SpriteRenderer>())
                    sr.size += new Vector2(width, 0f);
            }

            public void Platform(float centerX, float top, string sprite)
            {
                centerX += OriginX;
                var surface = Surface(sprite);
                TestSceneBuilder.PropPlatform(geometry, Name("Platform"), centerX, top, sprite, ctx.Tile, ctx.GroundLayer,
                    surfaceDepth: surface.depth, widthFactor: surface.width);
                Sprite s = TestSceneBuilder.Prop(sprite);
                float half = s != null ? s.bounds.size.x / 2f : 1.5f;
                platforms.Add((centerX - half, centerX + half, top));
            }

            // --- Level 2 mekanikleri ---

            // Yürünen yüzey her görselde sprite'ın tepesinde değil: üstteki bitki, korkuluk ya da halat için piksel payı.
            // Değerler: tools/magara_paketi_isle.py çıktısı, gözle düzeltilmiş. width: yürünen genişlik oranı.
            static readonly Dictionary<string, (int depth, float width)> Surfaces = new Dictionary<string, (int, float)>
            {
                { "ada_1", (10, 0.8f) }, { "ada_2", (15, 0.8f) }, { "ada_3", (12, 0.85f) }, { "ada_4", (14, 0.85f) },
                { "tahta_zincir", (5, 0.9f) }, { "tahta_ray", (4, 0.94f) }, { "tahta_tekerlek", (7, 0.9f) },
                { "tahta_kafes", (58, 0.72f) }, { "iskele_kirmizi", (3, 0.9f) }, { "tahta_korkuluk", (11, 0.9f) },
            };

            static (float depth, float width) Surface(string sprite) =>
                Surfaces.TryGetValue(sprite, out var s) ? (s.depth / 32f, s.width) : (0f, 0.94f);

            GameObject OneWayProp(string prefix, float centerX, float top, string sprite)
            {
                var surface = Surface(sprite);
                TestSceneBuilder.PropPlatform(geometry, Name(prefix), centerX, top, sprite, ctx.Tile, ctx.GroundLayer, oneWay: true,
                    surfaceDepth: surface.depth, widthFactor: surface.width);
                return geometry.GetChild(geometry.childCount - 1).gameObject;
            }

            // --- Level 3: gece mekanikleri ---

            /// <summary>Bu level gece: arka plan, ışıklar, vücut ısısı ve ısı çubuğu kurulur.</summary>
            public bool Night;
            public float DawnStartX, DawnEndX;

            const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
            static readonly Color FireLight = new Color(1f, 0.62f, 0.3f);
            static readonly Color SpringLight = new Color(1f, 0.55f, 0.28f);
            const int SwingOrder = 3;

            static Material Unlit => AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);

            static Light2D PointLight(Transform parent, Vector2 world, Color color, float radius, float intensity, float flicker = 0f)
            {
                var light = new GameObject("Isik").AddComponent<Light2D>();
                light.transform.SetParent(parent);
                light.transform.position = world;
                light.lightType = Light2D.LightType.Point;
                light.pointLightOuterRadius = radius;
                light.pointLightInnerRadius = radius * 0.2f;
                light.color = color;
                light.intensity = intensity;
                if (flicker > 0f)
                    light.gameObject.AddComponent<LightFlicker>().Configure(flicker);
                return light;
            }

            // Gökyüzünün önünde de görünen ışık halesi: arka plan ışıktan etkilenmediği için ayrıca çizilir.
            void Halo(Transform parent, Vector2 world, Color color, float radius)
            {
                glowSprite = glowSprite != null ? glowSprite : PlaceholderArt.Glow();
                var sr = new GameObject(Name("Hale")).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(parent);
                sr.transform.position = world;
                sr.transform.localScale = Vector3.one * (radius * 2f / glowSprite.bounds.size.x);
                sr.sprite = glowSprite;
                sr.sharedMaterial = Unlit;
                sr.color = color;
                sr.sortingOrder = -30;
                sr.gameObject.AddComponent<AmbientGlow>().Configure(1.5f, 0.12f, 0.25f);
            }

            static WarmthSource HeatCircle(Transform parent, Vector2 world, float radius, float rate)
            {
                var go = new GameObject("Isi");
                go.transform.SetParent(parent);
                go.transform.position = world;
                go.AddComponent<CircleCollider2D>().radius = radius;
                var source = go.AddComponent<WarmthSource>();
                source.Configure(rate, false);
                return source;
            }

            /// <summary>Kamp ateşi: taş halka, alev, sıcak ışık. Yanından koşarak geçen bile büyük ölçüde ısınır.</summary>
            public void Fire(float x, float y, float radius = 3.6f)
            {
                float wx = x + OriginX;
                Deco("mgr_kamp_ates", x, y);
                Campfire(wx, y + 0.1f);
                foreach (var loop in decor.GetComponentsInChildren<DesertGecko.Effects.SpriteLoop>())
                    loop.GetComponent<SpriteRenderer>().sharedMaterial = Unlit; // alev gecede de parlak
                HeatCircle(decor, new Vector2(wx, y + 0.8f), radius, 1.4f);
                PointLight(decor, new Vector2(wx, y + 1f), FireLight, radius * 2.8f, 1.9f, 0.14f);
                Halo(decor, new Vector2(wx, y + 1f), new Color(1f, 0.58f, 0.25f, 0.42f), radius * 1.5f);
            }

            const int AltarFrames = 7; // ates_sunak_1..7 (0 yeni tutuşan kor, döngüye girmez)

            /// <summary>
            /// Ateş sunağı: boynuzlu ahşap direğin tepesinde demir sepette yanan ateş (tools/ates_sunak_isle.py).
            /// Isı noktası ve gecenin uzaktan görünen işareti. Işık ve hale alevin hizasında.
            /// Sepetin ağzı tek yönlü platform: üstüne inilir, alttan içinden geçilir. Üstünde duran her
            /// seferinde yarım kalp yanar ve savrulur; direğe yandan ya da alttan çarpmak can yakmaz.
            /// </summary>
            public void Altar(float x, float y, float heatRadius = 2.6f)
            {
                float wx = x + OriginX;
                var sr = new GameObject(Name("AtesSunagi")).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(decor);
                sr.transform.position = new Vector3(wx, y - 0.06f, 0f); // direğin dibi toprağa gömülsün
                var frames = new Sprite[AltarFrames];
                for (int i = 0; i < AltarFrames; i++)
                    frames[i] = TestSceneBuilder.Prop("ates_sunak_" + (i + 1));
                sr.sprite = frames[0];
                sr.sortingOrder = -9;
                var loop = sr.gameObject.AddComponent<DesertGecko.Effects.SpriteLoop>();
                var so = new SerializedObject(loop);
                SerializedProperty prop = so.FindProperty("frames");
                prop.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                so.FindProperty("fps").floatValue = 9f;
                so.ApplyModifiedPropertiesWithoutUndo();
                occupied.Add((wx, 0.6f));

                const float FlameY = 1.45f; // sepetteki alevin ortası, direğin dibinden
                HeatCircle(decor, new Vector2(wx, y + 0.8f), heatRadius, 1.2f);
                PointLight(decor, new Vector2(wx, y + FlameY), FireLight, heatRadius * 2.8f, 1.8f, 0.12f);
                Halo(decor, new Vector2(wx, y + FlameY), new Color(1f, 0.58f, 0.25f, 0.38f), heatRadius * 1.3f);
                // Sepetin ağzı: üstüne basılır. Tepesindeki alev yalnız üstte durana değer.
                const float RimY = 1.32f, RimWidth = 0.7f;
                var rim = new GameObject(Name("SunakTepe")) { layer = ctx.GroundLayer };
                rim.transform.SetParent(geometry);
                rim.transform.position = new Vector3(wx, y + RimY, 0f);
                AddOneWayCollider(rim, RimWidth, 0.2f, new Vector2(0f, -0.1f));

                GameObject flame = FlameHazard(wx, y + RimY - 0.1f, new Vector2(RimWidth + 0.1f, 0.7f));
                flame.GetComponent<FireHazard>().Configure(1, fromAboveOnly: true, surfaceHeight: 0.1f);
            }

            /// <summary>
            /// Alevin kendisi: dokunan Gila yarım kalp yanar ve savrulur. Isınma alanından (HeatCircle) çok dar,
            /// yalnız alevin gövdesi. Üstünde binilen kor arabasına eklenmez: o yolculuk için.
            /// </summary>
            GameObject FlameHazard(float wx, float baseY, Vector2 size)
            {
                var go = new GameObject(Name("AlevHasar"));
                go.transform.SetParent(decor);
                go.transform.position = new Vector3(wx, baseY, 0f);
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = size;
                box.offset = new Vector2(0f, size.y / 2f);
                go.AddComponent<FireHazard>();
                return go;
            }

            /// <summary>Üstünde kor sepeti taşıyan platform: boşluğu geçerken ısıtır.</summary>
            public void BrazierCart(float centerX, float top, Vector2 travel, float speed, float phase = 0f)
            {
                Mover(centerX, top, "tahta_zincir", travel, speed, phase);
                Transform cart = geometry.GetChild(geometry.childCount - 1);
                float wx = centerX + OriginX;
                var sr = new GameObject("Mangal").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(cart);
                sr.transform.position = new Vector3(wx, top - 0.05f, 0f);
                sr.sprite = TestSceneBuilder.Prop("gece_mangal");
                sr.sortingOrder = 0;
                HeatCircle(cart, new Vector2(wx, top + 0.8f), 2.6f, 1f);
                PointLight(cart, new Vector2(wx, top + 1f), FireLight, 7f, 1.6f, 0.1f);
                Halo(cart, new Vector2(wx, top + 1f), new Color(1f, 0.58f, 0.25f, 0.34f), 3f);
            }

            /// <summary>Gece için zümrüt böcek: dille yakalanır, can ve ısı verir. Karnıyla birlikte zonklayan kendi ışığı var.</summary>
            public void Firefly(float x, float y)
            {
                Bug(x, y);
                Transform bug = actors.GetChild(actors.childCount - 1);
                // Titreme yok: ışığın gücünü Bug karnın zonklamasıyla birlikte sürer.
                PointLight(bug, new Vector2(x + OriginX, y), new Color(0.45f, 1f, 0.6f), 2.6f, 0.9f);
            }

            // Hat parçaları: fenerlerin zinciri bu hatta çıkar. y(x): sarkık hattın yüksekliği.
            readonly List<(float x0, float y0, float x1, float y1)> wires = new List<(float, float, float, float)>();
            const float WireSag = 1.1f;

            /// <summary>Tahta telgraf direği: tepesi yuz_direk'ten, gövdesi tekrar eder.</summary>
            public void Pole(float x, float groundTop, float height)
            {
                float wx = x + OriginX;
                Sprite head = LayerArt("gece_direk_tepe"), trunk = LayerArt("gece_direk_govde");
                float headHeight = head.bounds.size.y;
                float top = groundTop + height;
                occupied.Add((wx, 0.6f));
                TiledArt(decor, Name("DirekGovde"), trunk, new Vector2(wx, top - headHeight + 0.05f), new Vector2(trunk.bounds.size.x, height - headHeight + 0.2f), -6);
                TiledArt(decor, Name("DirekTepe"), head, new Vector2(wx, top), head.bounds.size, -6);
            }

            /// <summary>İki direğin tepesi arasında sarkan hat.</summary>
            public void Wire(float xA, float yA, float xB, float yB)
            {
                const int segments = 12;
                xA += OriginX;
                xB += OriginX;
                wires.Add((xA, yA, xB, yB));
                Vector2 unit = ctx.Solid.bounds.size;
                for (int i = 0; i < segments; i++)
                {
                    Vector2 a = WirePoint(xA, yA, xB, yB, (float)i / segments), b = WirePoint(xA, yA, xB, yB, (i + 1f) / segments);
                    var sr = new GameObject(Name("Hat")).AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(decor);
                    sr.transform.position = (a + b) / 2f;
                    Vector2 d = b - a;
                    sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    sr.transform.localScale = new Vector3((d.magnitude + 0.04f) / unit.x, 0.07f / unit.y, 1f);
                    sr.sprite = ctx.Solid;
                    sr.color = new Color32(0x24, 0x18, 0x18, 0xFF);
                    sr.sortingOrder = -6;
                }
            }

            static Vector2 WirePoint(float xA, float yA, float xB, float yB, float t) =>
                new Vector2(Mathf.Lerp(xA, xB, t), Mathf.Lerp(yA, yB, t) - Mathf.Sin(t * Mathf.PI) * WireSag);

            float WireYAt(float wx, float fallback)
            {
                foreach (var w in wires)
                    if (wx >= w.x0 && wx <= w.x1)
                        return WirePoint(w.x0, w.y0, w.x1, w.y1, Mathf.InverseLerp(w.x0, w.x1, wx)).y;
                return fallback;
            }

            /// <summary>Hatta zincirle asılı kanca feneri: dil yapışır, yanından geçeni de ısıtır. Hat yoksa zincir roofY'ye çıkar.</summary>
            public void Hook(float x, float y, float roofY = 0f)
            {
                float wx = x + OriginX;
                BuildHook(actors, wx, y, WireYAt(wx, roofY));
                platforms.Add((wx - 3f, wx + 3f, y - 4.2f));
                lifts.Add((wx - 3f, wx + 3f, y - 4.2f));
            }

            /// <summary>Hat üstünde gidip gelen makaralı kanca.</summary>
            public void MovingHook(float x, float y, float wireY, float travelX, float speed, float phase = 0f)
            {
                float wx = x + OriginX;
                // Makara hattın üstünde gider: kök hattın tam üstünde, hat sarktıkça o da iner çıkar.
                (float x0, float y0, float x1, float y1)? wire = null;
                foreach (var w in wires)
                    if (wx >= w.x0 && wx + travelX <= w.x1)
                        wire = w;
                float onWire = wire.HasValue ? WireYAt(wx, wireY) : wireY;

                var trolley = new GameObject(Name("Makara"));
                trolley.transform.SetParent(actors);
                trolley.transform.position = new Vector2(wx, onWire);

                // gece_makara: 16 px, alt orta pivot; tekerleğin oluğu (1.5. satır) hatta, askının halkası en altta.
                Sprite art = TestSceneBuilder.Prop("gece_makara");
                float artHeight = art.bounds.size.y;
                float grooveFromBottom = artHeight * (16f - 1.5f) / 16f;
                var wheel = new GameObject("Teker").AddComponent<SpriteRenderer>();
                wheel.transform.SetParent(trolley.transform);
                wheel.transform.position = new Vector2(wx, onWire - grooveFromBottom);
                wheel.sprite = art;
                wheel.sortingOrder = -5; // hattın (-6) önünde: hat oluğa girer gibi görünür

                BuildHook(trolley.transform, wx, y, onWire - grooveFromBottom + 0.06f);
                var body = trolley.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                var mover = trolley.AddComponent<MovingPlatform>();
                mover.Configure(new Vector2(travelX, 0f), speed, phase);
                if (wire.HasValue)
                    mover.FollowWire(new Vector2(wire.Value.x0, wire.Value.y0), new Vector2(wire.Value.x1, wire.Value.y1), WireSag);
                platforms.Add((wx - 3f, wx + travelX + 3f, y - 4.2f));
                lifts.Add((wx - 3f, wx + travelX + 3f, y - 4.2f));
            }

            void BuildHook(Transform parent, float wx, float y, float chainTop)
            {
                var root = new GameObject(Name("Kanca"));
                root.transform.SetParent(parent);
                root.transform.position = new Vector2(wx, y);

                Vector2 unit = ctx.Solid.bounds.size;
                float length = Mathf.Max(0.1f, chainTop - y);
                var chain = new GameObject("Zincir").AddComponent<SpriteRenderer>();
                chain.transform.SetParent(root.transform);
                chain.transform.position = new Vector2(wx, y + length / 2f);
                chain.transform.localScale = new Vector3(0.09f / unit.x, length / unit.y, 1f);
                chain.sprite = ctx.Solid;
                chain.color = new Color32(0x2C, 0x20, 0x20, 0xFF);
                chain.sortingOrder = SwingOrder - 1;

                Sprite art = TestSceneBuilder.Prop("gece_kanca_fener");
                var lantern = new GameObject("Fener").AddComponent<SpriteRenderer>();
                lantern.transform.SetParent(root.transform);
                lantern.transform.position = new Vector2(wx, y - art.bounds.size.y + 0.12f); // halkası kancanın noktasında
                lantern.sprite = art;
                lantern.sharedMaterial = Unlit;
                lantern.sortingOrder = SwingOrder;

                var marker = new GameObject("Hedef").AddComponent<SpriteRenderer>();
                marker.transform.SetParent(root.transform);
                marker.transform.position = new Vector2(wx, y);
                marker.sprite = ctx.Combat.Ring;
                marker.sharedMaterial = Unlit;
                marker.color = new Color(1f, 0.95f, 0.6f, 0.95f);
                marker.sortingOrder = SwingOrder + 1;

                root.AddComponent<SwingPoint>().Configure(marker);
                HeatCircle(root.transform, new Vector2(wx, y - 1.5f), 2.8f, 0.9f);
                PointLight(root.transform, new Vector2(wx, y - 0.5f), FireLight, 6f, 1.6f, 0.1f);
                Halo(root.transform, new Vector2(wx, y - 0.5f), new Color(1f, 0.7f, 0.35f, 0.4f), 2.4f);
            }

            /// <summary>Periyotla fışkıran gayzer. reach: sütunun boyu; cycle: tur süresi.</summary>
            public void Geyser(float x, float floorY, float reach, float cycle, float phase, float width = 2f)
            {
                float wx = x + OriginX;
                var go = new GameObject(Name("Gayzer"));
                go.transform.SetParent(actors);
                go.transform.position = new Vector2(wx, floorY);
                go.AddComponent<BoxCollider2D>();

                Sprite[] flow = Enumerable.Range(0, 4).Select(i => LayerArt("gece_gayzer_akis_" + i)).ToArray();
                SpriteRenderer column = TiledArt(go.transform, "Sutun", flow[0], new Vector2(wx, floorY + reach), new Vector2(flow[0].bounds.size.x, reach), 4); // görselin kendi genişliği: yan yana tekrar etmesin
                column.sharedMaterial = Unlit;
                Sprite[] spray = Enumerable.Range(0, 4).Select(i => TestSceneBuilder.Prop("gece_gayzer_tepe_" + i)).ToArray();
                var crown = new GameObject("Tepe").AddComponent<SpriteRenderer>();
                crown.transform.SetParent(go.transform, false);
                crown.sprite = spray[0];
                crown.sharedMaterial = Unlit;
                crown.sortingOrder = 5;
                crown.enabled = false;
                Deco("mgr_su", x, floorY);
                SpriteRenderer foam = ChildSprite(go.transform, "Kopuk", TestSceneBuilder.Prop("gece_gayzer_kopuk_0"), new Vector2(wx, floorY - 0.05f), 6);
                SpriteRenderer surge = ChildSprite(go.transform, "Kabarma", TestSceneBuilder.Prop("gece_gayzer_kabarma_0"), new Vector2(wx, floorY), 4);
                var geyser = go.AddComponent<Geyser>();
                geyser.Configure(width, reach, cycle, phase, column);
                geyser.SetArt(flow, crown, spray, foam, Enumerable.Range(0, 8).Select(i => TestSceneBuilder.Prop("gece_gayzer_kopuk_" + i)).ToArray(),
                    surge, Enumerable.Range(0, 3).Select(i => TestSceneBuilder.Prop("gece_gayzer_kabarma_" + i)).ToArray());
                PointLight(go.transform, new Vector2(wx, floorY + reach * 0.5f), new Color(0.65f, 0.85f, 1f), reach * 0.7f + 2f, 0.55f);
                platforms.Add((wx - 3f, wx + 3f, floorY + reach * 0.75f));
                lifts.Add((wx - 3f, wx + 3f, floorY + reach * 0.75f));
            }

            SpriteRenderer ChildSprite(Transform parent, string name, Sprite sprite, Vector2 world, int order)
            {
                var sr = new GameObject(name).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(parent, false);
                sr.transform.position = world;
                sr.sprite = sprite;
                sr.sharedMaterial = Unlit;
                sr.sortingOrder = order;
                sr.enabled = false;
                return sr;
            }

            /// <summary>Yer altının sıcak bölgesi: içinde Gila hızlanır, çubuk dolu kalır. Duvarlar turuncu aydınlanır.</summary>
            public void HotZone(float xMin, float xMax, float yMin, float yMax)
            {
                var go = new GameObject(Name("SicakBolge"));
                go.transform.SetParent(actors);
                go.transform.position = new Vector2((xMin + xMax) / 2f + OriginX, (yMin + yMax) / 2f);
                go.AddComponent<BoxCollider2D>().size = new Vector2(xMax - xMin, yMax - yMin);
                go.AddComponent<WarmthSource>().Configure(0f, true);
            }

            /// <summary>Mağara ışığı: sıcak yer altı duvarını boyar.</summary>
            public void SpringGlow(float x, float y, float radius = 11f, float intensity = 0.95f) =>
                PointLight(decor, new Vector2(x + OriginX, y), SpringLight, radius, intensity, 0.06f);

            /// <summary>Kaynar çamur havuzu: değen yanar. Üstünden gayzerle ya da kayalarla geçilir.</summary>
            public void BoilingPool(float xMin, float xMax, float y)
            {
                float wx0 = xMin + OriginX, wx1 = xMax + OriginX;
                Pit(wx0, wx1, y - 0.05f, -35);
                MudArt(geometry, (wx0 + wx1) / 2f, y, xMax - xMin, 0f, 6); // altını uçurumun karanlığı örter
                PointLight(decor, new Vector2((wx0 + wx1) / 2f, y + 1f), SpringLight, (xMax - xMin) * 0.6f + 3f, 0.85f, 0.05f);
                var zone = new GameObject(Name("KaynarHavuz"));
                zone.transform.SetParent(geometry);
                zone.transform.position = new Vector2((wx0 + wx1) / 2f, y - 0.45f - 5f);
                zone.AddComponent<BoxCollider2D>().size = new Vector2(xMax - xMin, 10f);
                zone.AddComponent<PitZone>();
            }

            // Kaynar yüzey: Gemini'nin lav setinden 8 karelik dalgalanan şerit; altı koyu çamur gövdesi.
            // Kaynar yüzey: Gemini'nin lav setinden 8 karelik dalgalanan şerit. Üstü verilen y'de (komşu zeminin altında),
            // altı sadece yükselen çamurda koyu lavla dolar; havuzda altını uçurum karanlığı örter.
            static void MudArt(Transform parent, float wx, float y, float width, float depth, int order)
            {
                Sprite[] surface = Enumerable.Range(0, 8).Select(i => LayerArt("gece_lav_yuzey_" + i)).ToArray();
                float crestHeight = surface[0].bounds.size.y;
                var material = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
                SpriteRenderer c = TiledArt(parent, "CamurUst", surface[0], new Vector2(wx, y + 0.1f), new Vector2(width, crestHeight), order + 1);
                c.sharedMaterial = material;
                c.gameObject.AddComponent<DesertGecko.Effects.SpriteLoop>().Configure(surface, 6f, wx * 0.37f);
                if (depth > crestHeight)
                {
                    // Yüzeyin en alt rengiyle düz koyu lav: çatlaklı doku yok.
                    var solid = new GameObject("Camur").AddComponent<SpriteRenderer>();
                    solid.transform.SetParent(parent, false);
                    Sprite pixel = PlaceholderArt.Solid();
                    Vector2 unit = pixel.bounds.size;
                    float fill = depth - crestHeight + 0.2f;
                    solid.transform.position = new Vector2(wx, y + 0.1f - crestHeight + 0.1f - fill / 2f);
                    solid.transform.localScale = new Vector3(width / unit.x, fill / unit.y, 1f);
                    solid.sprite = pixel;
                    solid.sharedMaterial = material;
                    solid.color = new Color32(0x6E, 0x14, 0x0E, 0xFF);
                    solid.sortingOrder = order;
                }

                // Fışkırmalar: her 5-6 birimde bir, farklı zamanlarda.
                Sprite[] spurt = Enumerable.Range(0, 8).Select(i => TestSceneBuilder.Prop("gece_lav_fiskirma_" + i)).ToArray();
                var random = new System.Random(Mathf.RoundToInt(wx * 10f));
                for (float sx = wx - width / 2f + 2.5f; sx < wx + width / 2f - 1.5f; sx += 8f + (float)random.NextDouble() * 4f)
                {
                    var sr = new GameObject("Fiskirma").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(parent, false);
                    sr.transform.position = new Vector2(sx, y - 0.55f); // dibi yüzeyin arkasında kalsın: lavdan çıkıyor görünsün
                    sr.sprite = spurt[0];
                    sr.sharedMaterial = material;
                    sr.sortingOrder = order; // yüzeyin arkasında
                    sr.gameObject.AddComponent<DesertGecko.Effects.SpriteLoop>().Configure(spurt, 5f, (float)random.NextDouble() * 4f, 2.5f + (float)random.NextDouble() * 2f);
                }
            }

            /// <summary>Bacada yükselen kaynar çamur. Oyuncu triggerY'yi geçince kabarır.</summary>
            public RisingMud Mud(float xMin, float xMax, float bottomY, float topY, float triggerY, float speed)
            {
                float wx0 = xMin + OriginX, wx1 = xMax + OriginX;
                var go = new GameObject(Name("YukselenCamur"));
                go.transform.SetParent(actors);
                go.transform.position = new Vector2((wx0 + wx1) / 2f, bottomY);
                MudArt(go.transform, (wx0 + wx1) / 2f, bottomY, xMax - xMin, 30f, 8);
                PointLight(go.transform, new Vector2((wx0 + wx1) / 2f, bottomY + 1f), SpringLight, 10f, 1.1f, 0.06f);
                var mud = go.AddComponent<RisingMud>();
                mud.Configure(bottomY, topY, new Vector2(wx0, wx1), triggerY, speed);
                return mud;
            }

            /// <summary>Arkadan gelen ayaz fırtınası: triggerX'i geçince başlar, endX'te dağılır.</summary>
            public void Storm(float behindX, float triggerX, float endX, float speed)
            {
                const float backWidth = 60f, scale = 2f;
                float x = behindX + OriginX;
                var go = new GameObject("AyazFirtinasi");
                go.transform.SetParent(actors);
                go.transform.position = new Vector2(x, 0f);
                var material = Unlit;
                Color night = new Color(0.72f, 0.64f, 0.7f); // gecenin içinde: gündüz renkleri biraz karartılır

                // Gövde: bulutların arkasında ekranı dolduran koyu toz.
                Sprite pixel = ctx.Solid;
                Vector2 unit = pixel.bounds.size;
                var body = new GameObject("Govde").AddComponent<SpriteRenderer>();
                body.transform.SetParent(go.transform, false);
                body.transform.localPosition = new Vector3(-4.5f - backWidth / 2f, 0f, 0f);
                body.transform.localScale = new Vector3(backWidth / unit.x, 40f / unit.y, 1f);
                body.sprite = pixel;
                body.sharedMaterial = material;
                body.color = new Color32(0x4E, 0x36, 0x2A, 0xFF);
                body.sortingOrder = 38; // yedi bulut sırasının (46..40) arkasında

                // Bulut sıraları: yerdeki en önde, yukarı çıktıkça geride ama düz bir eğimle değil, dalgalı
                // (ortada bir kabarma öne taşar). Yedi sıra ekranın tepesini de kapatır: arkadaki gövdenin
                // dümdüz kenarı hiçbir yükseklikte görünmez.
                string[] kinds = { "a", "b", "a", "b", "a", "b", "a" };
                float[] leads = { 1.4f, 0.1f, -1.1f, -0.2f, -1.4f, -2.6f, -2.0f };
                var rows = new List<Transform>();
                for (int i = 0; i < kinds.Length; i++)
                {
                    Sprite[] frames = Enumerable.Range(0, 6).Select(f => TestSceneBuilder.Prop($"gece_firtina_bulut_{kinds[i]}_{f}")).ToArray();
                    var row = new GameObject("Bulut" + i).AddComponent<SpriteRenderer>();
                    row.transform.SetParent(go.transform, false);
                    row.transform.localScale = new Vector3(scale, scale, 1f);
                    row.sprite = frames[0];
                    row.sharedMaterial = material;
                    row.color = Color.Lerp(night, Color.white, i * 0.06f);
                    row.sortingOrder = 46 - i; // yerdeki önde
                    row.gameObject.AddComponent<DesertGecko.Effects.SpriteLoop>().Configure(frames, 5f + i * 0.4f, i * 0.37f);
                    rows.Add(row.transform);
                }
                var front = go.AddComponent<ColdFront>();
                front.Configure(x, triggerX + OriginX, endX + OriginX, speed);
                front.SetArt(rows.ToArray(), leads, body.transform);
                foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>())
                    sr.enabled = false; // başlayınca görünür
            }

            /// <summary>Oyuncu ve sahne kurulduktan sonra gece düzeni. Gündüz leveli için çağrılmaz.</summary>
            public void FinishNight(GameObject player)
            {
                SpriteRenderer[] nightLayers = BackgroundBuilder.BuildNight();
                var heat = player.AddComponent<BodyHeat>();
                CombatSetupBuilder.BuildHeatBar(heat, ctx.Solid, TestSceneBuilder.Prop("gece_alev_simge"));

                // Gila'nın çevresi soluk aydınlık: gece karanlığında kaybolmasın.
                Light2D aura = PointLight(player.transform, player.transform.position + Vector3.up * 0.6f, new Color(0.85f, 0.88f, 1f), 7f, 0.5f);

                var sun = GameObject.Find("Global Light 2D").GetComponent<Light2D>();
                new GameObject("Gece").AddComponent<NightSky>().Configure(player.transform, sun, nightLayers, new[] { aura }, DawnStartX, DawnEndX, Camera.main,
                    new Color32(22, 17, 53, 255)); // gökyüzü görselinin en üst rengi
            }

            // --- Dekor (çarpışmasız) ---

            /// <summary>Mağara içinde true: nesneler duvarın karanlığına uysun, altlarına gölge düşsün.</summary>
            public bool InCave;

            static readonly Color CaveTint = new Color(0.74f, 0.7f, 0.68f);
            // Görseller yandan çizili ve düz tabanlı; bunlar ayrıca zemine gömülür (görsel yüksekliğine oran).
            static readonly Dictionary<string, float> SinkOverride = new Dictionary<string, float>
            {
                { "mgr_su", 0.62f },
            };

            /// <summary>Zemine oturan görsel: düz tabanıyla toprağa hafifçe girer, mağarada altına gölge düşer.</summary>
            public void Deco(string sprite, float x, float y, int order = -10)
            {
                Sprite s = TestSceneBuilder.Prop(sprite);
                float wx = x + OriginX;
                float sink = SinkOverride.TryGetValue(sprite, out float fraction) ? s.bounds.size.y * fraction : 0.1f; // kaynaktaki ince zemin çizgisi toprağın arkasında kalsın
                var sr = new GameObject(sprite).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(decor);
                sr.transform.position = new Vector3(wx, y - sink, 0f);
                sr.sprite = s;
                sr.sortingOrder = order;
                occupied.Add((wx, s.bounds.size.x / 2f));
                if (InCave)
                {
                    sr.color = CaveTint;
                    Shadow(wx, y, s.bounds.size.x);
                }
                else if (s.bounds.size.x >= BigDecorWidth)
                    bigDecor.Add((wx, y, s.bounds.size.x / 2f));
                if (sprite == "yuz_kamp")
                    Campfire(wx + CampfireOffsetX, y);
            }

            const float CampfireOffsetX = -0.7f; // yuz_kamp görselinde ateşin yeri
            const int CampfireFrames = 12;

            /// <summary>Dalgalanan alev ve tüten duman: kareler tools/yuzey_paketi_isle.py çiziyor.</summary>
            void Campfire(float wx, float y)
            {
                var sr = new GameObject(Name("KampAtesi")).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(decor);
                sr.transform.position = new Vector3(wx, y + 0.05f, 0f);
                var frames = new Sprite[CampfireFrames];
                for (int i = 0; i < CampfireFrames; i++)
                    frames[i] = TestSceneBuilder.Prop("yuz_ates_" + i);
                sr.sprite = frames[0];
                sr.sortingOrder = -9;
                FlameHazard(wx, y, new Vector2(0.6f, 0.95f)); // alevin içine giren yanar
                var loop = sr.gameObject.AddComponent<DesertGecko.Effects.SpriteLoop>();
                var so = new SerializedObject(loop);
                SerializedProperty prop = so.FindProperty("frames");
                prop.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                so.FindProperty("fps").floatValue = 10f;
                so.ApplyModifiedPropertiesWithoutUndo();
                occupied.Add((wx, 0.5f));
                Glow(wx - OriginX, y + 0.5f, new Color(1f, 0.62f, 0.25f, 0.28f), 1.8f, 0.5f);
            }

            /// <summary>Çit: direkten direğe dizilir, iki ucu da direkte biter (ya da kayanın arkasına girer).</summary>
            public void Fence(float xMin, float xMax, float y)
            {
                float width = TestSceneBuilder.Prop("yuz_cit").bounds.size.x;
                int count = Mathf.Max(1, Mathf.RoundToInt((xMax - xMin) / width));
                float step = count > 1 ? (xMax - xMin - width) / (count - 1) : 0f;
                for (int i = 0; i < count; i++)
                    Deco("yuz_cit", xMin + width / 2f + i * step, y);
            }

            // --- Zemin örtüsü: nesneler yalnız kalmasın, zemin boş durmasın ---

            const float BigDecorWidth = 1.8f;
            const int CoverOrder = -9, DistantOrder = -25;
            static readonly Color DistantTint = new Color(0.86f, 0.66f, 0.6f); // arka planın sıcak pusuna yakın
            static readonly string[] CoverSprites = { "yuz_cali_a", "yuz_cali_c", "yuz_top_a", "yuz_top_b", "yuz_top_c", "yuz_kaktus_b" };
            static readonly string[] DistantSprites =
            {
                "yuz_kaya_1", "yuz_kaya_2", "yuz_cali_b", "yuz_agac_1", "yuz_agac_2", "yuz_kaktus_a", "yuz_kaktus_c", "yuz_cali_a", "yuz_top_b",
            };
            static readonly string[] CaveSprites = { "mgr_dikit_1", "yuz_kaya_2", "yuz_kaya_1" };

            /// <summary>
            /// Açık havadaki her zemine önde küçük çalı ve taşlar, arkada puslu kaya ve ağaçlar; büyük nesnelerin
            /// yanına da çalı serper. Mağara tabanına seyrek taş. Yerleşim sabit tohumlu: her kurulumda aynı çıkar.
            /// </summary>
            public void ScatterGroundCover()
            {
                var random = new System.Random(4242);
                float Between(float a, float b) => a + (float)random.NextDouble() * (b - a);
                string Pick(string[] list) => list[random.Next(list.Length)];
                bool Indoors(float x) => indoors.Any(r => x >= r.xMin && x <= r.xMax);
                bool Free(float x, float half) => !occupied.Any(o => Mathf.Abs(o.x - x) < o.half + half);

                foreach (var g in grounds)
                {
                    if (g.xMax - g.xMin < 3f)
                        continue;
                    // Arka sıra: sürekli bir uzak bant, oyuncu ile gökyüzü arasını doldurur.
                    for (float x = g.xMin + Between(0.4f, 1.5f); x < g.xMax; x += Between(2.2f, 4.2f))
                        if (!Indoors(x))
                            Cover(Pick(DistantSprites), x, g, 0.72f, DistantTint, DistantOrder);
                    // Ön sıra: küçük çalılar ve taş öbekleri, çoğu yerde ikişer ikişer.
                    for (float x = g.xMin + Between(1f, 3f); x < g.xMax; x += Between(3f, 6.5f))
                    {
                        if (Indoors(x) || !Free(x, 0.7f))
                            continue;
                        Cover(Pick(CoverSprites), x, g, 1f, Color.white, CoverOrder);
                        if (random.NextDouble() < 0.5)
                            Cover(Pick(CoverSprites), x + Between(0.9f, 1.5f), g, 0.8f, Color.white, CoverOrder);
                    }
                }

                foreach (var d in bigDecor)
                    foreach (int side in new[] { -1, 1 })
                    {
                        float x = d.x + side * (d.half + Between(0.1f, 0.5f));
                        var g = grounds.FirstOrDefault(o => x >= o.xMin && x <= o.xMax && Mathf.Abs(o.top - d.y) < 0.01f);
                        if (g.xMax > g.xMin)
                            Cover(Pick(CoverSprites), x, g, 0.9f, Color.white, CoverOrder);
                    }

                foreach (var f in caveFloors)
                {
                    if (f.xMax - f.xMin < 4f)
                        continue;
                    for (float x = f.xMin + Between(1f, 4f); x < f.xMax; x += Between(5f, 9f))
                        if (Free(x, 0.6f))
                            Cover(Pick(CaveSprites), x, f, 0.55f, CaveTint, CoverOrder);
                }
            }

            // Görsel zeminin kenarından taşarsa havada kalır: sığmıyorsa koyulmaz.
            void Cover(string sprite, float x, (float xMin, float xMax, float top) surface, float scale, Color color, int order)
            {
                Sprite s = TestSceneBuilder.Prop(sprite);
                if (order == CoverOrder && sprite.StartsWith("yuz_top_"))
                    scale = 1f; // Çalı Faresi'nin gizli haliyle aynı boy: küçükse ayırt edilirdi
                float half = s.bounds.size.x * scale / 2f;
                if (x - half < surface.xMin || x + half > surface.xMax)
                    return;
                var sr = new GameObject(sprite).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(decor);
                sr.transform.position = new Vector3(x, surface.top - 0.1f * scale, 0f);
                sr.transform.localScale = new Vector3(scale, scale, 1f);
                sr.sprite = s;
                sr.color = color;
                sr.sortingOrder = order;
            }

            // Duvara düşen yumuşak gölge: nesneyi arkasındaki kayaya bağlar.
            void Shadow(float wx, float y, float width)
            {
                glowSprite = glowSprite != null ? glowSprite : PlaceholderArt.Glow();
                var sr = new GameObject("Golge").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(decor);
                sr.transform.position = new Vector3(wx, y, 0f);
                float unit = glowSprite.bounds.size.x;
                sr.transform.localScale = new Vector3(width * 1.5f / unit, 2.2f / unit, 1f);
                sr.sprite = glowSprite;
                sr.color = new Color(0f, 0f, 0f, 0.75f);
                sr.sortingOrder = -11;
            }

            // --- Boru ağı: arka duvarda, parıltının arkasında ---

            const int PipeOrder = -32;
            static readonly Color PipeTint = new Color(0.72f, 0.7f, 0.72f);
            const float PipeThickness = 0.5f;

            /// <summary>Dikey boru; iki ucu da birleşim parçasıyla biter.</summary>
            public void PipeUp(float x, float yMin, float yMax)
            {
                Sprite s = PlaceholderArt.PipeVertical();
                TiledArt(decor, Name("Boru"), s, new Vector2(x + OriginX, yMax), new Vector2(PipeThickness, yMax - yMin), PipeOrder).color = PipeTint;
                PipePart(PlaceholderArt.PipeJoint(), x, yMin);
                PipePart(PlaceholderArt.PipeJoint(), x, yMax);
            }

            /// <summary>Yatay boru; iki ucu da birleşim parçasıyla biter.</summary>
            public void PipeAcross(float xMin, float xMax, float y)
            {
                Sprite s = PlaceholderArt.PipeHorizontal();
                TiledArt(decor, Name("Boru"), s, new Vector2((xMin + xMax) / 2f + OriginX, y + PipeThickness / 2f), new Vector2(xMax - xMin, PipeThickness), PipeOrder).color = PipeTint;
                PipePart(PlaceholderArt.PipeJoint(), xMin, y);
                PipePart(PlaceholderArt.PipeJoint(), xMax, y);
            }

            public void PipeValve(float x, float y) => PipePart(PlaceholderArt.PipeValve(), x, y);

            void PipePart(Sprite s, float x, float y)
            {
                var sr = new GameObject(Name("BoruParca")).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(decor);
                sr.transform.position = new Vector3(x + OriginX, y, 0f);
                sr.sprite = s;
                sr.color = PipeTint;
                sr.sortingOrder = PipeOrder + 1;
            }

            /// <summary>Tavandan sarkan görsel (tavan_ ile başlayanlar). Kökü kayanın saçağını örter.</summary>
            public void Hang(string sprite, float x, float roofY)
            {
                var sr = new GameObject(sprite).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(decor);
                sr.transform.position = new Vector3(x + OriginX, roofY + 0.12f, 0f);
                sr.sprite = TestSceneBuilder.Prop(sprite);
                sr.sortingOrder = 3;
                if (InCave)
                    sr.color = CaveTint;
            }

            /// <summary>Arka duvarı aydınlatan yumuşak parıltı.</summary>
            public void Glow(float x, float y, Color color, float radius, float flicker = 0f)
            {
                glowSprite = glowSprite != null ? glowSprite : PlaceholderArt.Glow();
                var sr = new GameObject(Name("Parilti")).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(decor);
                sr.transform.position = new Vector3(x + OriginX, y, 0f);
                sr.transform.localScale = Vector3.one * (radius * 2f / glowSprite.bounds.size.x);
                sr.sprite = glowSprite;
                sr.color = color;
                sr.sortingOrder = -30;
                sr.gameObject.AddComponent<AmbientGlow>().Configure(1.5f, 0.18f, flicker);
            }

            /// <summary>Direğinde fener asılı kazma küreği yığını; feneri gerçekten yanar.</summary>
            public void Fener(float x, float y)
            {
                Deco("mgr_fener", x, y);
                Glow(x - 0.39f, y + 1.44f, LampLight, 3f, 0.35f);
            }

            public void Drip(float x, float top, float bottom)
            {
                var sr = new GameObject(Name("Damla")).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(decor);
                sr.transform.position = new Vector3(x + OriginX, top, 0f);
                sr.transform.localScale = new Vector3(0.09f, 0.2f, 1f) * (1f / ctx.Solid.bounds.size.x);
                sr.sprite = ctx.Solid;
                sr.color = new Color(0.55f, 0.82f, 0.95f, 0.9f);
                sr.sortingOrder = 2;
                sr.gameObject.AddComponent<WaterDrip>().Configure(top, bottom);
            }

            /// <summary>Gidip gelen platform. Menzil kontrolünde yolunun tamamı tek yüzey sayılır.</summary>
            public void Mover(float centerX, float top, string sprite, Vector2 travel, float speed, float phase = 0f)
            {
                centerX += OriginX;
                GameObject go = OneWayProp("Hareketli", centerX, top, sprite);
                go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                go.AddComponent<MovingPlatform>().Configure(travel, speed, phase);
                if (sprite == "tahta_kafes")
                    Rope(go.transform);
                float half = TestSceneBuilder.Prop(sprite).bounds.size.x * Surface(sprite).width / 2f;
                var surface = (centerX - half, centerX + half + travel.x, top);
                platforms.Add(surface);
                if (travel.y > 0f)
                    lifts.Add(surface); // asansör: yüksekliği değişir, yanındaki yüzeylere yetişir
            }

            // Kafesin halatı tavana kadar uzansın: kafes görselinde kısa kesilmiş.
            void Rope(Transform cage)
            {
                const float length = 30f;
                float top = TestSceneBuilder.Prop("tahta_kafes").bounds.size.y;
                var sr = new GameObject("Halat").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(cage, false);
                sr.transform.localPosition = new Vector3(0f, top + length / 2f - 0.05f, 0f);
                Vector2 unit = ctx.Solid.bounds.size;
                sr.transform.localScale = new Vector3(0.12f / unit.x, length / unit.y, 1f);
                sr.sprite = ctx.Solid;
                sr.color = new Color32(0x6B, 0x4A, 0x2E, 0xFF);
                sr.sortingOrder = 0; // tavan kayasının arkasında kalır
            }

            /// <summary>Basılınca yıkılan tahta. Düşen tahta menzil kontrolünde yüzey sayılır: üstünden koşulur.</summary>
            public void Crumble(float centerX, float top, string sprite)
            {
                centerX += OriginX;
                OneWayProp("Curuk", centerX, top, sprite).AddComponent<CrumblingPlatform>();
                float half = TestSceneBuilder.Prop(sprite).bounds.size.x / 2f;
                platforms.Add((centerX - half, centerX + half, top));
            }

            /// <summary>Tavandan sarkan, oyuncu altına gelince düşen kaya.</summary>
            public void FallingRock(float x, float roofY)
            {
                var sr = new GameObject(Name("Sarkit")).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(actors);
                sr.transform.position = new Vector3(x + OriginX, roofY + 0.2f, 0f); // kökü tavana gömülü
                sr.sprite = TestSceneBuilder.Prop("tavan_sarkit_1");
                sr.sortingOrder = 1; // tavanın arkasından sarksın
                sr.gameObject.AddComponent<FallingRock>();
            }

            /// <summary>Sıcak hava sütunu. flowY: menzil kontrolünde sütunun sayıldığı yükseklik.</summary>
            public void Thermal(float xMin, float xMax, float yMin, float yMax, float flowY, float lift = 6f)
            {
                xMin += OriginX;
                xMax += OriginX;
                var go = new GameObject(Name("SicakHava"));
                go.transform.SetParent(actors);
                go.transform.position = new Vector3((xMin + xMax) / 2f, (yMin + yMax) / 2f, 0f);
                go.AddComponent<BoxCollider2D>();
                go.AddComponent<Thermal>().Configure(new Vector2(xMax - xMin, yMax - yMin), lift, ctx.Solid);
                platforms.Add((xMin, xMax, flowY));
            }

            /// <summary>İçinden geçilen, oyuncu girince saydamlaşan sahte kaya. Arkasındaki odanın duvarı CaveWall'dan gelir.</summary>
            public void SecretRock(float xMin, float xMax, float yMin, float yMax)
            {
                xMin += OriginX;
                xMax += OriginX;
                var size = new Vector2(xMax - xMin, yMax - yMin);
                SpriteRenderer sr = TiledArt(geometry, Name("SahteKaya"), LayerArt("kaya_kutle"), new Vector2((xMin + xMax) / 2f, yMax), size, 4);
                var col = sr.gameObject.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = size;
                col.offset = new Vector2(0f, -size.y / 2f);
                Fringe(sr.transform, xMin, xMax, yMin);
                sr.gameObject.AddComponent<SecretWall>();
            }

            /// <summary>Dövüş odası: triggerX'i geçince giriş kapısı iner, xMin-xMax arasındaki düşmanlar ölünce kapılar kalkar.</summary>
            public void Arena(float xMin, float xMax, float triggerX, float floorY, float roofY)
            {
                xMin += OriginX;
                xMax += OriginX;
                triggerX += OriginX;
                float height = roofY - floorY;
                var go = new GameObject(Name("DovusOdasi"));
                go.transform.SetParent(actors);
                go.transform.position = new Vector3(triggerX, floorY + height / 2f, 0f);
                var zone = go.AddComponent<BoxCollider2D>();
                zone.isTrigger = true;
                zone.size = new Vector2(1f, height);
                go.AddComponent<ArenaGate>().Configure(Gate(xMin - 0.5f, floorY, height), Gate(xMax + 0.5f, floorY, height), height + 1f,
                    new Vector2(xMin, xMax), new Vector2(floorY - 1f, roofY));
            }

            // Ahşap kapı: Default katmanında, Gila yapışamaz. Açılınca tavanın arkasına kalkar.
            Transform Gate(float x, float floorY, float height)
            {
                SpriteRenderer sr = TiledArt(geometry, Name("Kapi"), LayerArt("merdiven"), new Vector2(x, floorY + height), new Vector2(1f, height), 1);
                var col = sr.gameObject.AddComponent<BoxCollider2D>();
                col.size = new Vector2(1f, height);
                col.offset = new Vector2(0f, -height / 2f);
                return sr.transform;
            }

            public void Table(float centerX, float groundTop, string sprite)
            {
                var surface = Surface(sprite);
                TestSceneBuilder.PropPlatform(geometry, Name("Masa"), centerX + OriginX, groundTop, sprite, ctx.Tile, ctx.GroundLayer, onGround: true,
                    surfaceDepth: surface.depth, widthFactor: surface.width);
            }

            public void Plant(string sprite, float x, float groundTop)
            {
                occupied.Add((x + OriginX, 0.8f));
                TestSceneBuilder.PropDecor(decor, sprite, x + OriginX, groundTop, ctx.Tile);
            }

            public void Scorpion(float x, float groundTop) => CombatSetupBuilder.Place(ctx.Combat.Scorpion, actors, Name("Akrep"), x + OriginX, groundTop);
            public void Jackal(float x, float groundTop) => CombatSetupBuilder.Place(ctx.Combat.Jackal, actors, Name("Cakal"), x + OriginX, groundTop);
            public void Armadillo(float x, float groundTop) => CombatSetupBuilder.Place(ctx.Combat.Armadillo, actors, Name("Armadillo"), x + OriginX, groundTop);
            public void Diken(float x, float groundTop) => CombatSetupBuilder.Place(ctx.Combat.SpikeMutant, actors, Name("DikenMutanti"), x + OriginX, groundTop);
            public void Fare(float x, float groundTop) => CombatSetupBuilder.Place(ctx.Combat.TumbleweedMouse, actors, Name("CaliFaresi"), x + OriginX, groundTop);

            /// <summary>
            /// Haydut Yarasa: <paramref name="roofY"/> tavanın/telin alt yüzü; yarasa oraya asılı uyur.
            /// <paramref name="wakeHalfWidth"/> verilirse Gila yatayda bu kadar yaklaşınca uyanır (dövüş odasında odanın tamamı).
            /// </summary>
            public void Yarasa(float x, float roofY, float wakeHalfWidth = 0f)
            {
                GameObject bat = CombatSetupBuilder.Place(ctx.Combat.BanditBat, actors, Name("Yarasa"), x + OriginX, roofY - 0.5f);
                if (wakeHalfWidth <= 0f)
                    return;
                var so = new SerializedObject(bat.GetComponent<BanditBat>());
                so.FindProperty("wakeHalfWidth").floatValue = wakeHalfWidth;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            /// <summary>Çalı Faresi'nin gizli haliyle aynı, zararsız çalı: oyuncu her çalıdan şüphelensin.</summary>
            public void SahteCali(float x, float groundTop) => Deco("yuz_top_a", x, groundTop);

            public void Snake(float x, float groundTop) => CombatSetupBuilder.Place(ctx.Combat.Rattlesnake, actors, Name("Yilan"), x + OriginX, groundTop);
            public void Bug(float x, float y) => CombatSetupBuilder.Place(ctx.Combat.Bug, actors, Name("Bocek"), x + OriginX, y);
            public void Crystal(float x, float y) => CombatSetupBuilder.Place(ctx.Combat.Crystal, actors, Name("Kristal"), x + OriginX, y);
            public void Checkpoint(float x, float groundTop)
            {
                x += OriginX;
                checkpoints.Add(x);
                occupied.Add((x, 1f));
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
                occupied.Add((x + OriginX, 0.6f));
                var root = new GameObject(Name("Tabela"));
                root.transform.SetParent(decor);
                root.transform.position = new Vector3(x + OriginX, groundTop, 0f);
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
                root.transform.position = new Vector3(x + OriginX, groundTop, 0f);
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
                        || (gap <= SingleJumpGap && (lifts.Contains(a) || lifts.Contains(next)))
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
