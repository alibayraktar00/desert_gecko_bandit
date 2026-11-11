using DesertGecko.Collectibles;
using DesertGecko.Core;
using DesertGecko.Enemies;
using DesertGecko.Environment;
using DesertGecko.Player;
using DesertGecko.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Savaş döngüsünün prefablarını (kuyruk mermisi, kristal, böcek, akrep, checkpoint),
    /// oyuncuya eklenen bileşenleri, seviye yerleşimini ve arayüzü kurar. TestSceneBuilder çağırır.
    /// </summary>
    public static class CombatSetupBuilder
    {
        const string PrefabFolder = "Assets/Prefabs";

        public class CombatAssets
        {
            public TailProjectile TailProjectile;
            public Crystal Crystal;
            public Bug Bug;
            public Scorpion Scorpion;
            public EnemyBullet EnemyBullet;
            public Rattlesnake Rattlesnake;
            public Jackal Jackal;
            public Armadillo Armadillo;
            public TumbleweedMouse TumbleweedMouse;
            public BanditBat BanditBat;
            public SpikeMutant SpikeMutant;
            public Checkpoint Checkpoint;
            public Sprite Tongue, HeartFull, HeartHalf, HeartEmpty, Ring, CrystalIcon, TailIcon;
        }

        public static CombatAssets BuildPrefabs()
        {
            var a = new CombatAssets
            {
                Tongue = PlaceholderArt.Tongue(),
                HeartFull = PlaceholderArt.Heart(2),
                HeartHalf = PlaceholderArt.Heart(1),
                HeartEmpty = PlaceholderArt.Heart(0),
                Ring = PlaceholderArt.Ring(),
                CrystalIcon = LoadCollectible(CrystalFolder + "altin_ikon"), // kırpılmış: sayaç kutusunu doldurur
                TailIcon = PlaceholderArt.TailProjectile(),
            };
            a.TailProjectile = BuildTailProjectile(a.TailIcon);
            a.Crystal = BuildCrystal(LoadCollectible(CrystalFolder + "altin_0")); // oyundaki kareler 40x40 tuvalde
            a.Bug = BuildBug();
            a.Scorpion = BuildScorpion(PlaceholderArt.Scorpion(), a.Crystal);
            a.EnemyBullet = BuildEnemyBullet(PlaceholderArt.Bullet());
            a.Rattlesnake = BuildRattlesnake(a.EnemyBullet, a.Crystal);
            a.Jackal = BuildJackal(a.Crystal);
            a.Armadillo = BuildArmadillo(a.Crystal);
            a.TumbleweedMouse = BuildTumbleweedMouse(a.Crystal);
            a.BanditBat = BuildBanditBat(a.Crystal);
            a.SpikeMutant = BuildSpikeMutant(a.Crystal);
            a.Checkpoint = BuildCheckpoint();
            return a;
        }

        // --- Prefablar ---

        static TailProjectile BuildTailProjectile(Sprite sprite)
        {
            var root = new GameObject("TailProjectile") { layer = LayerMask.NameToLayer(GameLayers.PlayerAttack) };
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0.3f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.22f;

            Transform visual = AddSprite(root.transform, "Visual", sprite, 12).transform;
            var projectile = root.AddComponent<TailProjectile>();
            SetField(projectile, "visual", visual);
            return Save<TailProjectile>(root, "TailProjectile");
        }

        const string CrystalFolder = "Assets/Sprites/Collectibles/altin/";

        static Sprite LoadCollectible(string pathWithoutExtension)
        {
            string path = pathWithoutExtension + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                // İçe aktarma kuralları derlenmeden önce gelmiş olabilir.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return sprite;
        }

        // Kare sırası (tools/altin_isle.py): 0 altın, 1-3 parıltı gezinir ve çakar, 4 ışınlı patlama, 5 kırılma,
        // 6-7 kırıntılar (8-9 neredeyse boş, kullanılmıyor). altin_hale: arkada zonklayan parıltı.
        // Arayüz ikonu altin_ikon (0. kare, kenarı kırpılmış). Sınıf adı (Crystal) kayıtlı sahneler bozulmasın diye kaldı.
        static Crystal BuildCrystal(Sprite sprite)
        {
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            var root = new GameObject("Crystal");

            Sprite haloSprite = LoadCollectible(CrystalFolder + "altin_hale");
            SpriteRenderer glow = AddSprite(root.transform, "Parilti", haloSprite, 4);
            if (haloSprite != null)
                glow.transform.localScale = Vector3.one * (1.1f / haloSprite.bounds.size.x);
            glow.color = new Color(1f, 0.85f, 0.45f, 0.3f);
            if (unlit != null)
                glow.sharedMaterial = unlit;

            SpriteRenderer sr = AddSprite(root.transform, "Visual", sprite, 5);
            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.3f;

            Sprite[] Frames(int from, int to)
            {
                var frames = new Sprite[to - from + 1];
                for (int i = from; i <= to; i++)
                    frames[i - from] = LoadCollectible(CrystalFolder + "altin_" + i);
                return frames;
            }
            AssetDatabase.ImportAsset("Assets/Audio/altin_toplama.wav", ImportAssetOptions.ForceSynchronousImport);

            var crystal = root.AddComponent<Crystal>();
            SetMask(crystal, "groundMask", GameLayers.GroundMask);
            SetField(crystal, "spriteRenderer", sr);
            SetField(crystal, "glow", glow);
            SetArray(crystal, "glintFrames", Frames(0, 3));
            SetArray(crystal, "collectFrames", Frames(4, 7));
            SetField(crystal, "unlitMaterial", unlit);
            SetField(crystal, "collectClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/altin_toplama.wav"));
            // Altın 19 piksel boyunda: yere düşünce alt kenarı toprağa değsin.
            var so = new SerializedObject(crystal);
            so.FindProperty("radius").floatValue = 0.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save<Crystal>(root, "Crystal");
        }

        const string BugFolder = "Assets/Sprites/Collectibles/bocek/";
        const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        // Kare sırası (tools/zumrut_bocek_isle.py): 0-7 süzülme (kanat çırpar, karın zonklar), 8-10 yutulurken
        // kıvılcımlanıp dağılır, 11 duman (kullanılmıyor). bocek_hale: karın parıltısı, bocek_halka: şifa dalgası.
        static Bug BuildBug()
        {
            Sprite Load(string name)
            {
                string path = BugFolder + name + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                return sprite;
            }
            Sprite[] Frames(int from, int to)
            {
                var frames = new Sprite[to - from + 1];
                for (int i = from; i <= to; i++)
                    frames[i - from] = Load($"bocek_{i}");
                return frames;
            }
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);

            var root = new GameObject("Bug");
            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.35f;

            // Karın parıltısı gövdenin arkasında, karnın (sol alt) üstünde; gece de parlasın diye ışıksız.
            Sprite haloSprite = Load("bocek_hale");
            SpriteRenderer glow = AddSprite(root.transform, "Parilti", haloSprite, 4);
            glow.transform.localPosition = new Vector3(-0.12f, -0.1f, 0f);
            if (haloSprite != null)
                glow.transform.localScale = Vector3.one * (1.3f / haloSprite.bounds.size.x);
            glow.color = new Color(0.45f, 1f, 0.6f, 0.45f);
            if (unlit != null)
                glow.sharedMaterial = unlit;

            // 0-7: sabit gövde üstünde tek, yavaş kanat çırpışı (araç gövdeyi kilitler, titreme olmaz).
            Sprite[] hover = Frames(0, 7);
            SpriteRenderer sr = AddSprite(root.transform, "Visual", hover[0], 5);

            foreach (string clip in new[] { "bocek_slap", "bocek_yutma" })
                AssetDatabase.ImportAsset($"Assets/Audio/{clip}.wav", ImportAssetOptions.ForceSynchronousImport);

            var bug = root.AddComponent<Bug>();
            SetField(bug, "spriteRenderer", sr);
            SetArray(bug, "hoverFrames", hover);
            SetArray(bug, "swallowFrames", Frames(8, 10));
            SetField(bug, "glow", glow);
            SetField(bug, "ringSprite", Load("bocek_halka"));
            SetField(bug, "unlitMaterial", unlit);
            SetField(bug, "slapClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bocek_slap.wav"));
            SetField(bug, "gulpClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bocek_yutma.wav"));
            return Save<Bug>(root, "Bug");
        }

        static Scorpion BuildScorpion(Sprite sprite, Crystal crystalPrefab)
        {
            var root = new GameObject("Scorpion") { layer = LayerMask.NameToLayer(GameLayers.Enemy) };
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.9f, 0.45f);
            box.offset = new Vector2(0f, 0.225f);

            SpriteRenderer sr = AddSprite(root.transform, "Visual", sprite, 8);
            var scorpion = root.AddComponent<Scorpion>();
            SetField(scorpion, "spriteRenderer", sr);
            SetField(scorpion, "visual", sr.transform);
            SetMask(scorpion, "groundMask", GameLayers.GroundMask);
            // Dört can: dille dört darbe, silahla çabuk.
            var health = new SerializedObject(scorpion);
            health.FindProperty("maxHealth").intValue = 4;
            health.ApplyModifiedPropertiesWithoutUndo();

            var drop = root.AddComponent<CrystalDrop>();
            SetField(drop, "crystalPrefab", Fresh(crystalPrefab));
            return Save<Scorpion>(root, "Scorpion");
        }

        static EnemyBullet BuildEnemyBullet(Sprite sprite)
        {
            var root = new GameObject("EnemyBullet");
            SpriteRenderer sr = AddSprite(root.transform, null, sprite, 12);
            var bullet = root.AddComponent<EnemyBullet>();
            SetField(bullet, "spriteRenderer", sr);
            return Save<EnemyBullet>(root, "EnemyBullet");
        }

        // Kare sırası (tools/gemini_dusman_isle.py): 0 bekleme, 1 tıslama, 2 çıngırak havada,
        // 3 çıngırak titrer, 4 nişan, 5 ateş, 6 vuruldu, 7 öldü.
        static Rattlesnake BuildRattlesnake(EnemyBullet bulletPrefab, Crystal crystalPrefab)
        {
            Sprite[] frames = LoadEnemyFrames("yilan", 8);

            var root = new GameObject("Rattlesnake") { layer = LayerMask.NameToLayer(GameLayers.Enemy) };
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.75f, 1.05f);
            box.offset = new Vector2(0f, 0.525f);

            SpriteRenderer sr = AddSprite(root.transform, "Visual", frames[0], 8);
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var snake = root.AddComponent<Rattlesnake>();
            SetField(snake, "spriteRenderer", sr);
            SetMask(snake, "groundMask", GameLayers.GroundMask);
            SetField(snake, "bulletPrefab", Fresh(bulletPrefab));
            SetField(snake, "audioSource", audio);
            SetField(snake, "rattleClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/cingirak.wav"));
            SetField(snake, "shotClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/silah.wav"));
            SetArray(snake, "walkFrames", new Object[] { frames[0], frames[2] });
            SetArray(snake, "rattleFrames", new Object[] { frames[2], frames[3] });
            SetField(snake, "aimFrame", frames[4]);
            SetField(snake, "shootFrame", frames[5]);
            SetField(snake, "hissFrame", frames[1]);
            SetField(snake, "hurtFrame", frames[6]);
            SetField(snake, "deathFrame", frames[7]);

            // Kareler sağa bakar; sola giderken aynalanır.
            var so = new SerializedObject(snake);
            so.FindProperty("spriteFacesLeft").boolValue = false;
            so.FindProperty("patrolSpeed").floatValue = 1.5f;
            so.FindProperty("maxHealth").intValue = 5;
            so.ApplyModifiedPropertiesWithoutUndo();

            var drop = root.AddComponent<CrystalDrop>();
            SetField(drop, "crystalPrefab", Fresh(crystalPrefab));
            return Save<Rattlesnake>(root, "Rattlesnake");
        }

        // Kare sırası (tools/gemini_cakal_isle.py): 0-4 devriye, 5 fark etme, 6-12 koşu, 13 nişan, 14 ateş,
        // 15-19 doldurma, 20 hasar, 21-24 sevinç, 25 yenik.
        static Jackal BuildJackal(Crystal crystalPrefab)
        {
            Sprite[] f = LoadEnemyFrames("cakal", 26);
            string hatPath = SpriteImportRules.EnemiesFolder + "cakal/cakal_sapka.png";
            AssetDatabase.ImportAsset(hatPath, ImportAssetOptions.ForceSynchronousImport);

            var root = new GameObject("Jackal") { layer = LayerMask.NameToLayer(GameLayers.Enemy) };
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.7f, 1.1f);
            box.offset = new Vector2(0f, 0.55f);

            SpriteRenderer sr = AddSprite(root.transform, "Visual", f[0], 8);
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var jackal = root.AddComponent<Jackal>();
            SetField(jackal, "spriteRenderer", sr);
            SetMask(jackal, "groundMask", GameLayers.GroundMask);
            SetField(jackal, "audioSource", audio);
            SetField(jackal, "shotClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/silah.wav"));
            SetArray(jackal, "patrolFrames", new Object[] { f[0], f[1], f[2], f[3], f[4] });
            SetField(jackal, "alertFrame", f[5]);
            SetArray(jackal, "runFrames", new Object[] { f[6], f[7], f[8], f[9], f[10], f[11], f[12] });
            SetField(jackal, "aimFrame", f[13]);
            SetField(jackal, "fireFrame", f[14]);
            SetArray(jackal, "reloadFrames", new Object[] { f[15], f[16], f[17], f[18], f[19] });
            SetField(jackal, "hurtFrame", f[20]);
            SetArray(jackal, "victoryFrames", new Object[] { f[21], f[22], f[23], f[24] });
            SetField(jackal, "defeatFrame", f[25]);
            SetField(jackal, "hatFrame", AssetDatabase.LoadAssetAtPath<Sprite>(hatPath));

            // Kareler sağa bakar. Yavaş, sinsi devriye; oyuncudan (5.5) hızlı depar.
            var so = new SerializedObject(jackal);
            so.FindProperty("spriteFacesLeft").boolValue = false;
            so.FindProperty("patrolSpeed").floatValue = 1.2f;
            so.FindProperty("chaseSpeed").floatValue = 6.5f;
            so.FindProperty("detectRange").floatValue = 7f;
            so.FindProperty("loseRange").floatValue = 10f;
            so.FindProperty("maxHealth").intValue = 6;
            so.ApplyModifiedPropertiesWithoutUndo();

            var drop = root.AddComponent<CrystalDrop>();
            SetField(drop, "crystalPrefab", Fresh(crystalPrefab));
            return Save<Jackal>(root, "Jackal");
        }

        // Kare sırası (tools/gemini_armadillo_isle.py): 0-6 bekleme, 7-13 devriye, 14-20 fark, 21-23 kapanma,
        // 24-30 top, 31-39 sersem, 40-48 toparlanma, 49-56 yenik.
        static Armadillo BuildArmadillo(Crystal crystalPrefab)
        {
            Sprite[] f = LoadEnemyFrames("armadillo", 57);
            Object[] Range(int from, int to)
            {
                var frames = new Object[to - from + 1];
                for (int i = from; i <= to; i++)
                    frames[i - from] = f[i];
                return frames;
            }

            var root = new GameObject("Armadillo") { layer = LayerMask.NameToLayer(GameLayers.Enemy) };
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.95f, 1.15f);
            box.offset = new Vector2(0f, 0.575f);

            SpriteRenderer sr = AddSprite(root.transform, "Visual", f[7], 8);
            var armadillo = root.AddComponent<Armadillo>();
            SetField(armadillo, "spriteRenderer", sr);
            SetMask(armadillo, "groundMask", GameLayers.GroundMask);
            SetArray(armadillo, "idleFrames", Range(0, 6));
            SetArray(armadillo, "patrolFrames", Range(7, 13));
            SetArray(armadillo, "noticeFrames", Range(14, 20));
            SetArray(armadillo, "curlFrames", Range(21, 23));
            SetArray(armadillo, "rollFrames", Range(24, 30));
            SetArray(armadillo, "dazeFrames", Range(31, 39));
            SetArray(armadillo, "recoverFrames", Range(40, 48));
            SetField(armadillo, "hurtFrame", f[41]);
            SetArray(armadillo, "defeatFrames", Range(49, 56));

            // Kareler sağa bakar. Hantal yürür, sekiz can (sadece sersemken vurulabilir; dinamit zırhını kırar).
            var so = new SerializedObject(armadillo);
            so.FindProperty("spriteFacesLeft").boolValue = false;
            so.FindProperty("patrolSpeed").floatValue = 1.1f;
            so.FindProperty("maxHealth").intValue = 8;
            so.ApplyModifiedPropertiesWithoutUndo();

            var drop = root.AddComponent<CrystalDrop>();
            SetField(drop, "crystalPrefab", Fresh(crystalPrefab));
            return Save<Armadillo>(root, "Armadillo");
        }

        // Kare sırası (tools/gemini_cali_faresi_isle.py): 0-3 nefes, 4-5 göz, 6-12 top, 13-17 çıkış, 18 zıplama,
        // 19-25 koşu, 26-32 bıçak, 33-36 yorgun, 37-42 saklanma, 43-47 yenik.
        static TumbleweedMouse BuildTumbleweedMouse(Crystal crystalPrefab)
        {
            Sprite[] f = LoadEnemyFrames("cali_faresi", 48);
            Sprite[] Range(int from, int to)
            {
                var frames = new Sprite[to - from + 1];
                for (int i = from; i <= to; i++)
                    frames[i - from] = f[i];
                return frames;
            }

            var root = new GameObject("TumbleweedMouse") { layer = LayerMask.NameToLayer(GameLayers.Enemy) };
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 1f);
            box.offset = new Vector2(0f, 0.5f);

            SpriteRenderer sr = AddSprite(root.transform, "Visual", f[0], 8);
            var mouse = root.AddComponent<TumbleweedMouse>();
            SetField(mouse, "spriteRenderer", sr);
            SetMask(mouse, "groundMask", GameLayers.GroundMask);
            SetField(mouse, "disguiseFrame", f[0]);
            SetArray(mouse, "revealFrames", Range(13, 17));
            SetField(mouse, "jumpFrame", f[18]);
            SetArray(mouse, "runFrames", Range(19, 25));
            SetArray(mouse, "stabFrames", Range(26, 32));
            SetArray(mouse, "fatigueFrames", Range(33, 36));
            SetField(mouse, "parryFrame", f[18]);
            SetArray(mouse, "defeatFrames", Range(43, 47));

            // Kareler sağa bakar. Sıska: savuşturmaları bitince iki darbede ölür.
            var so = new SerializedObject(mouse);
            so.FindProperty("spriteFacesLeft").boolValue = false;
            so.FindProperty("maxHealth").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();

            var drop = root.AddComponent<CrystalDrop>();
            SetField(drop, "crystalPrefab", Fresh(crystalPrefab));
            return Save<TumbleweedMouse>(root, "TumbleweedMouse");
        }

        // Kare sırası (tools/gemini_yarasa_isle.py): 0-7 asılı, 8-15 uçuş, 16-23 süzülme, 24-31 dinamit
        // (24-27 fitil yanar, 28-29 dinamit hâlâ pençede, 30-31 bıraktı), 32-39 alay, 40-47 sersem,
        // 48-55 hasar, 56-63 toparlanma, 64-71 yenik (çakılma).
        static BanditBat BuildBanditBat(Crystal crystalPrefab)
        {
            Sprite[] f = LoadEnemyFrames("yarasa", 72);
            Sprite[] Range(int from, int to)
            {
                var frames = new Sprite[to - from + 1];
                for (int i = from; i <= to; i++)
                    frames[i - from] = f[i];
                return frames;
            }

            BatDynamite dynamite = BuildBatDynamite();

            // Transform yarasanın ortası: kareler 64 px tuvalde ortalı, pivot alt orta -> Visual 1 birim aşağı.
            var root = new GameObject("BanditBat") { layer = LayerMask.NameToLayer(GameLayers.Enemy) };
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 0.75f);
            box.offset = new Vector2(0f, -0.05f);

            SpriteRenderer sr = AddSprite(root.transform, "Visual", f[0], 8);
            sr.transform.localPosition = new Vector3(0f, -1f, 0f);

            // Gece görünürlüğü: arkada soluk ay ışığı halesi (ışıksız, hep parlak) ve gövdeyi aydınlatan küçük ışık.
            // Gücünü yarasa ortam ışığına göre ayarlar; gündüz ikisi de kapalı.
            Sprite glowSprite = PlaceholderArt.Glow();
            SpriteRenderer halo = AddSprite(root.transform, "GeceHalesi", glowSprite, 7);
            halo.transform.localScale = Vector3.one * (1.9f / glowSprite.bounds.size.x);
            halo.color = new Color(0.86f, 0.8f, 1f, 0f);
            var haloMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            if (haloMaterial != null)
                halo.sharedMaterial = haloMaterial;
            var nightLight = new GameObject("GeceIsigi").AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            nightLight.transform.SetParent(root.transform, false);
            nightLight.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            nightLight.pointLightOuterRadius = 1.7f;
            nightLight.pointLightInnerRadius = 0.3f;
            nightLight.color = new Color(0.82f, 0.78f, 1f);
            nightLight.intensity = 0f;

            var bat = root.AddComponent<BanditBat>();
            SetField(bat, "spriteRenderer", sr);
            SetMask(bat, "groundMask", GameLayers.GroundMask);
            SetField(bat, "dynamitePrefab", Fresh(dynamite));
            SetField(bat, "nightHalo", halo);
            SetField(bat, "nightLight", nightLight);
            SetArray(bat, "hangFrames", Range(0, 7));
            SetArray(bat, "flyFrames", Range(8, 15));
            SetArray(bat, "glideFrames", Range(16, 23));
            SetArray(bat, "windupFrames", Range(24, 27));
            SetArray(bat, "releaseFrames", Range(30, 31));
            SetArray(bat, "tauntFrames", Range(32, 39));
            SetArray(bat, "dizzyFrames", Range(40, 47));
            SetArray(bat, "hurtFrames", Range(48, 55));
            SetArray(bat, "recoverFrames", Range(56, 63));
            SetArray(bat, "defeatFrames", Range(64, 71));

            // Kareler sağa bakar. Üç can: havada çapraz dille ya da yukarı ateşle, yorgunken yakından.
            var so = new SerializedObject(bat);
            so.FindProperty("spriteFacesLeft").boolValue = false;
            so.FindProperty("maxHealth").intValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();

            var drop = root.AddComponent<CrystalDrop>();
            SetField(drop, "crystalPrefab", Fresh(crystalPrefab));
            return Save<BanditBat>(root, "BanditBat");
        }

        static BatDynamite BuildBatDynamite()
        {
            string path = $"{SpriteImportRules.EnemiesFolder}yarasa/yarasa_dinamit.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            var root = new GameObject("BatDynamite");
            // Dönme ekseni dinamitin ortası olsun: alt-orta pivotlu sprite yarı boyu kadar aşağıda.
            var pivot = new GameObject("Visual").transform;
            pivot.SetParent(root.transform, false);
            SpriteRenderer sr = AddSprite(pivot, "Sprite", sprite, 12);
            if (sprite != null)
                sr.transform.localPosition = new Vector3(0f, -sprite.bounds.size.y / 2f, 0f);
            var d = root.AddComponent<BatDynamite>();
            SetField(d, "visual", pivot);
            return Save<BatDynamite>(root, "BatDynamite");
        }

        // Kare sırası (tools/gemini_diken_isle.py): 0-7 bekleme, 8-15 devriye, 16-23 çömelme, 24-31 titreme,
        // 32-39 yaylım (dolu -> yarı -> kel), 40-47 nefes, 48-55 yenilenme, 56-63 kel bekleme, 64-71 kel yenik,
        // 72-79 yarı bekleme, 80-87 yarı yürüyüş, 88-95 yarı sendeleme, 96-103 dolu yenik, 104-111 yarı yenik,
        // 112-119 kel sendeleme.
        static SpikeMutant BuildSpikeMutant(Crystal crystalPrefab)
        {
            Sprite[] f = LoadEnemyFrames("diken", 120);
            Sprite[] Range(int from, int to)
            {
                var frames = new Sprite[to - from + 1];
                for (int i = from; i <= to; i++)
                    frames[i - from] = f[i];
                return frames;
            }

            EnemyBullet spike = BuildSpikeProjectile();

            var root = new GameObject("SpikeMutant") { layer = LayerMask.NameToLayer(GameLayers.Enemy) };
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.05f, 1.25f);
            box.offset = new Vector2(0f, 0.625f);

            SpriteRenderer sr = AddSprite(root.transform, "Visual", f[0], 8);
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            foreach (string clip in new[] { "diken_gerilme", "diken_atis", "diken_fis" })
                AssetDatabase.ImportAsset($"Assets/Audio/{clip}.wav", ImportAssetOptions.ForceSynchronousImport);

            var mutant = root.AddComponent<SpikeMutant>();
            SetField(mutant, "spriteRenderer", sr);
            SetMask(mutant, "groundMask", GameLayers.GroundMask);
            SetField(mutant, "spikePrefab", Fresh(spike));
            SetField(mutant, "audioSource", audio);
            SetField(mutant, "bristleClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/diken_gerilme.wav"));
            SetField(mutant, "shotClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/diken_atis.wav"));
            SetField(mutant, "regrowClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/diken_fis.wav"));
            SetArray(mutant, "idleFrames", Range(0, 7));
            SetArray(mutant, "walkFrames", Range(8, 15));
            SetArray(mutant, "crouchFrames", Range(16, 23));
            SetArray(mutant, "bristleFrames", Range(24, 31));
            SetArray(mutant, "volleyFrames", Range(32, 39));
            SetArray(mutant, "exhaustedFrames", Range(40, 47));
            SetArray(mutant, "regrowFrames", Range(48, 55));
            SetArray(mutant, "halfIdleFrames", Range(72, 79));
            SetArray(mutant, "hurtFrames", Range(112, 114));
            SetArray(mutant, "fullDefeatFrames", Range(96, 103));
            SetArray(mutant, "halfDefeatFrames", Range(104, 111));
            SetArray(mutant, "baldDefeatFrames", Range(64, 71));

            // Kareler sağa bakar. Ağır yürür; sadece kelken vurulur (patlama ve kazma dikenleri kırar), sekiz can.
            var so = new SerializedObject(mutant);
            so.FindProperty("spriteFacesLeft").boolValue = false;
            so.FindProperty("patrolSpeed").floatValue = 0.9f;
            so.FindProperty("maxHealth").intValue = 8;
            // Ağır gövde: darbe onu az savurur (çakal gibi hafif düşmanlar 4 birim geri uçar).
            so.FindProperty("knockbackVelocity").vector2Value = new Vector2(1.5f, 1.5f);
            so.ApplyModifiedPropertiesWithoutUndo();

            var drop = root.AddComponent<CrystalDrop>();
            SetField(drop, "crystalPrefab", Fresh(crystalPrefab));
            return Save<SpikeMutant>(root, "SpikeMutant");
        }

        /// <summary>Diken mermisi: yılan mermisiyle aynı davranış, kendi görseli ve hızıyla.</summary>
        static EnemyBullet BuildSpikeProjectile()
        {
            string path = $"{SpriteImportRules.EnemiesFolder}diken/diken_mermi.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            var root = new GameObject("SpikeProjectile");
            SpriteRenderer sr = AddSprite(root.transform, "Visual", sprite, 12);
            // Alt-orta pivotlu sprite yarı boyu kadar aşağıda: diken merminin tam ortasından uçsun.
            if (sprite != null)
                sr.transform.localPosition = new Vector3(0f, -sprite.bounds.size.y / 2f, 0f);
            var bullet = root.AddComponent<EnemyBullet>();
            SetField(bullet, "spriteRenderer", sr);
            var so = new SerializedObject(bullet);
            so.FindProperty("speed").floatValue = 8f;
            so.FindProperty("lifetime").floatValue = 1.6f;
            so.FindProperty("radius").floatValue = 0.08f;
            so.FindProperty("burstColor").colorValue = new Color32(0xD8, 0xDC, 0xE6, 0xFF);
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save<EnemyBullet>(root, "SpikeProjectile");
        }

        static Sprite[] LoadEnemyFrames(string enemy, int count)
        {
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                string path = $"{SpriteImportRules.EnemiesFolder}{enemy}/{enemy}_{i}.png";
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frames[i] == null)
                {
                    // İçe aktarma kuralları derlenmeden önce gelmiş olabilir.
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
            return frames;
        }

        // Kareler: tools/checkpoint_dag_isle.py
        static Sprite CheckpointFrame(string name)
        {
            string path = SpriteImportRules.PropsFolder + "Checkpoint/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Object[] CheckpointFrames(string prefix, int count)
        {
            var frames = new Object[count];
            for (int i = 0; i < count; i++)
                frames[i] = CheckpointFrame(prefix + "_" + i);
            return frames;
        }

        static Checkpoint BuildCheckpoint()
        {
            var root = new GameObject("Checkpoint");
            var col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            // Uzun sütun: üstünden zıplayarak ya da süzülerek geçen oyuncu da checkpoint'i alır.
            col.size = new Vector2(1f, 12f);
            col.offset = new Vector2(0f, 6f);
            Sprite off = CheckpointFrame("kapali");
            SpriteRenderer totem = AddSprite(root.transform, "Totem", off, 2);
            totem.transform.localPosition = new Vector3(0f, -0.09f, 0f); // taşlar toprağa gömülsün, havada durmasın
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var checkpoint = root.AddComponent<Checkpoint>();
            SetField(checkpoint, "totem", totem);
            SetField(checkpoint, "offFrame", off);
            SetArray(checkpoint, "igniteFrames", CheckpointFrames("yanis", 4));
            SetArray(checkpoint, "litFrames", CheckpointFrames("aktif", 30));
            SetField(checkpoint, "audioSource", audio);
            AssetDatabase.ImportAsset("Assets/Audio/fener.wav", ImportAssetOptions.ForceSynchronousImport);
            SetField(checkpoint, "igniteClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/fener.wav"));
            return Save<Checkpoint>(root, "Checkpoint");
        }

        // --- Oyuncu ---

        /// <summary>Player prefab köküne can, cüzdan, dil ve kuyruk saldırısını ekler.</summary>
        public static void AddPlayerCombat(GameObject root, SpriteRenderer visual, CombatAssets a)
        {
            var health = root.AddComponent<PlayerHealth>();
            SetField(health, "blinkRenderer", visual);
            root.AddComponent<PlayerWallet>();

            // Dil, çevrilen Visual'ın değil kökün çocuğu: yönünü TongueAttack kendisi ayarlar.
            SpriteRenderer tongue = AddSprite(root.transform, "Dil", a.Tongue, 11);
            tongue.enabled = false;
            var tongueAttack = root.AddComponent<TongueAttack>();
            SetField(tongueAttack, "tongueRenderer", tongue);
            SetMask(tongueAttack, "hitMask", LayerMask.GetMask("Default", GameLayers.Enemy));

            var tail = root.AddComponent<TailAttack>();
            SetField(tail, "projectilePrefab", Fresh(a.TailProjectile));

            // Düşmandan alınan silah; silah verileri WeaponSetup ile kurulur.
            root.AddComponent<PlayerWeapon>();
        }

        // --- Seviye ---

        /// <summary>Test seviyesine akrepleri, böcekleri, kristalleri ve checkpoint'leri yerleştirir.</summary>
        public static void PopulateLevel(CombatAssets a)
        {
            var root = new GameObject("Oyun").transform;

            // Akrepler: her biri kendi zemin parçasında, devriye alanı duvar/uçurumla sınırlı.
            Place(a.Scorpion, root, "Akrep_1", 10f, 0f);
            Place(a.Scorpion, root, "Akrep_2", 25f, 0f);
            Place(a.Scorpion, root, "Akrep_3", 44f, 0f);
            Place(a.Scorpion, root, "Akrep_4", 86f, 0f);

            Place(a.Bug, root, "Bocek_1", 12.5f, 5.3f);
            Place(a.Bug, root, "Bocek_2", 37f, 1.2f);
            Place(a.Bug, root, "Bocek_3", 90f, 1.2f);

            Place(a.Crystal, root, "Kristal_1", 7f, 3f);
            Place(a.Crystal, root, "Kristal_2", 18.5f, 7f);
            Place(a.Crystal, root, "Kristal_3", 40.5f, 7.6f);
            Place(a.Crystal, root, "Kristal_4", 53f, 11.6f);
            Place(a.Crystal, root, "Kristal_5", 64f, 6f);
            Place(a.Crystal, root, "Kristal_6", 3f, 0.6f);

            Place(a.Checkpoint, root, "Checkpoint_1", 35.5f, 0f);
            Place(a.Checkpoint, root, "Checkpoint_2", 73.5f, 0f);
        }

        internal static GameObject Place(Component prefab, Transform parent, string name, float x, float y)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Fresh(prefab).gameObject, parent);
            go.name = name;
            go.transform.position = new Vector3(x, y, 0f);
            return go;
        }

        // --- Arayüz ---

        /// <summary>
        /// Kamera üzerine çizilen arayüz (640x360 referans): sol üstte kalpler ve altında kuyruk
        /// çemberi, sağ üstte kristal sayacı.
        /// </summary>
        public static void BuildHud(Camera cam, GameObject player, CombatAssets a)
        {
            var canvasGo = new GameObject("HUD", typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            Transform hud = canvasGo.transform;

            // Kalpler
            RectTransform heartsRoot = CreateRect("Kalpler", hud, new Vector2(0f, 1f), new Vector2(8f, -8f), new Vector2(54f, 16f));
            var hearts = new Image[3];
            for (int i = 0; i < hearts.Length; i++)
                hearts[i] = ImageAt($"Kalp_{i + 1}", heartsRoot, new Vector2(0f, 1f), new Vector2(i * 18f, 0f), new Vector2(16f, 16f), a.HeartFull);
            var healthUI = heartsRoot.gameObject.AddComponent<HealthUI>();
            SetField(healthUI, "health", player.GetComponent<PlayerHealth>());
            SetArray(healthUI, "hearts", hearts);
            SetField(healthUI, "fullHeart", a.HeartFull);
            SetField(healthUI, "halfHeart", a.HeartHalf);
            SetField(healthUI, "emptyHeart", a.HeartEmpty);

            // Kuyruk çemberi
            RectTransform tailRoot = CreateRect("KuyrukSayaci", hud, new Vector2(0f, 1f), new Vector2(8f, -28f), new Vector2(18f, 18f));
            Image ringBack = ImageAt("Arka", tailRoot, new Vector2(0f, 1f), Vector2.zero, new Vector2(18f, 18f), a.Ring);
            ringBack.color = new Color(0f, 0f, 0f, 0.45f);
            Image ringFill = ImageAt("Dolgu", tailRoot, new Vector2(0f, 1f), Vector2.zero, new Vector2(18f, 18f), a.Ring);
            ringFill.type = Image.Type.Filled;
            ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top;
            ringFill.fillClockwise = true;
            ringFill.fillAmount = 1f;
            ImageAt("Ikon", tailRoot, new Vector2(0f, 1f), new Vector2(4f, -4f), new Vector2(10f, 10f), a.TailIcon);
            var tailUI = tailRoot.gameObject.AddComponent<TailCooldownUI>();
            SetField(tailUI, "tail", player.GetComponent<TailAttack>());
            SetField(tailUI, "fill", ringFill);

            // Kristal sayacı
            RectTransform scoreRoot = CreateRect("Kristaller", hud, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(56f, 16f));
            ImageAt("Ikon", scoreRoot, new Vector2(0f, 1f), Vector2.zero, new Vector2(16f, 16f), a.CrystalIcon)
                .preserveAspect = true; // altın 18x19: kare kutuda basılmasın
            RectTransform labelRect = CreateRect("Sayi", scoreRoot, new Vector2(0f, 1f), new Vector2(20f, 1f), new Vector2(36f, 18f));
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(20f, -8f);
            var label = labelRect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 14;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.text = "0";
            var shadow = labelRect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.17f, 0.1f, 0.07f, 1f);
            shadow.effectDistance = new Vector2(1f, -1f);
            var scoreUI = scoreRoot.gameObject.AddComponent<ScoreUI>();
            SetField(scoreUI, "wallet", player.GetComponent<PlayerWallet>());
            SetField(scoreUI, "label", label);
        }

        /// <summary>Gece levellerinin ısı çubuğu: kuyruk çemberinin yanında.</summary>
        public static void BuildHeatBar(BodyHeat heat, Sprite solid, Sprite icon)
        {
            Transform hud = GameObject.Find("HUD").transform;
            RectTransform root = CreateRect("IsiCubugu", hud, new Vector2(0f, 1f), new Vector2(30f, -31f), new Vector2(58f, 12f));
            ImageAt("Simge", root, new Vector2(0f, 1f), Vector2.zero, new Vector2(12f, 12f), icon);
            Image back = ImageAt("Arka", root, new Vector2(0f, 1f), new Vector2(14f, -2f), new Vector2(44f, 8f), solid);
            back.color = new Color(0f, 0f, 0f, 0.55f);
            Image fill = ImageAt("Dolgu", root, new Vector2(0f, 1f), new Vector2(15f, -3f), new Vector2(42f, 6f), solid);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            var ui = root.gameObject.AddComponent<BodyHeatUI>();
            SetField(ui, "heat", heat);
            SetField(ui, "fill", fill);
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Image ImageAt(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite)
        {
            var image = CreateRect(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        // --- Yardımcılar ---

        static SpriteRenderer AddSprite(Transform parent, string childName, Sprite sprite, int order)
        {
            GameObject go = parent.gameObject;
            if (childName != null)
            {
                go = new GameObject(childName);
                go.transform.SetParent(parent, false);
            }
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>
        /// Prefab, başka bir prefab kaydedilirken yeniden içe aktarılmış olabilir; eldeki referans
        /// eskimiş olur. Prefab adı bileşen tipiyle aynı olduğu için dosyasından taze yükler.
        /// </summary>
        static T Fresh<T>(T prefab) where T : Component =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{prefab.GetType().Name}.prefab").GetComponent<T>();

        static T Save<T>(GameObject root, string name) where T : Component
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<T>();
        }

        static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetMask(Object target, string field, int mask)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
