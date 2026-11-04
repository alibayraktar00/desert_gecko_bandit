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
                CrystalIcon = PlaceholderArt.Crystal(),
                TailIcon = PlaceholderArt.TailProjectile(),
            };
            a.TailProjectile = BuildTailProjectile(a.TailIcon);
            a.Crystal = BuildCrystal(a.CrystalIcon);
            a.Bug = BuildBug(PlaceholderArt.Bug());
            a.Scorpion = BuildScorpion(PlaceholderArt.Scorpion(), a.Crystal);
            a.EnemyBullet = BuildEnemyBullet(PlaceholderArt.Bullet());
            a.Rattlesnake = BuildRattlesnake(a.EnemyBullet, a.Crystal);
            a.Checkpoint = BuildCheckpoint(PlaceholderArt.Totem());
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

        static Crystal BuildCrystal(Sprite sprite)
        {
            var root = new GameObject("Crystal");
            AddSprite(root.transform, null, sprite, 5);
            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.25f;
            var crystal = root.AddComponent<Crystal>();
            SetMask(crystal, "groundMask", GameLayers.GroundMask);
            return Save<Crystal>(root, "Crystal");
        }

        static Bug BuildBug(Sprite sprite)
        {
            var root = new GameObject("Bug");
            AddSprite(root.transform, null, sprite, 5);
            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.3f;
            root.AddComponent<Bug>();
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
            so.ApplyModifiedPropertiesWithoutUndo();

            var drop = root.AddComponent<CrystalDrop>();
            SetField(drop, "crystalPrefab", Fresh(crystalPrefab));
            return Save<Rattlesnake>(root, "Rattlesnake");
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

        static Checkpoint BuildCheckpoint(Sprite sprite)
        {
            var root = new GameObject("Checkpoint");
            var col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            // Uzun sütun: üstünden zıplayarak ya da süzülerek geçen oyuncu da checkpoint'i alır.
            col.size = new Vector2(1f, 12f);
            col.offset = new Vector2(0f, 6f);
            SpriteRenderer totem = AddSprite(root.transform, "Totem", sprite, 2);
            var checkpoint = root.AddComponent<Checkpoint>();
            SetField(checkpoint, "totem", totem);
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
            ImageAt("Ikon", scoreRoot, new Vector2(0f, 1f), Vector2.zero, new Vector2(16f, 16f), a.CrystalIcon);
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
