using System.IO;
using DesertGecko.DevTools;
using DesertGecko.Environment;
using DesertGecko.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Placeholder sprite'larla test sahnesini sıfırdan kurar: seviye, oyuncu, düşmanlar,
    /// toplanabilirler, checkpoint'ler, arayüz ve (görseller varsa) paralaks arka plan.
    /// Menü: Desert Gecko → Test Sahnesini Kur. Komut satırından -executeMethod ile de çalışır.
    /// </summary>
    public static class TestSceneBuilder
    {
        const int PixelsPerUnit = 32;
        const string ScenePath = "Assets/Scenes/TestLevel_Movement.unity";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const string NoFrictionPath = "Assets/Materials/NoFriction.physicsMaterial2D";

        static readonly string[] Folders =
        {
            "Assets/Scripts/Player", "Assets/Scripts/Environment", "Assets/Scripts/Enemies",
            "Assets/Scripts/Collectibles", "Assets/Scripts/UI", "Assets/Scripts/DevTools",
            "Assets/Sprites/Player", "Assets/Sprites/Enemies", "Assets/Sprites/Environment",
            "Assets/Sprites/UI", "Assets/Sprites/_Gelen", PlaceholderArt.Folder,
            "Assets/Prefabs", "Assets/Scenes", "Assets/Audio", "Assets/Materials",
        };

        static readonly Color Sand = new Color32(0xE8, 0xA0, 0x4A, 0xFF);
        static readonly Color Rock = new Color32(0xB5, 0x45, 0x2B, 0xFF);
        static readonly Color Plank = new Color32(0x7A, 0x4B, 0x2A, 0xFF);
        static readonly Color Sky = new Color32(0xF6, 0xD8, 0xA0, 0xFF);
        static readonly Color Cactus = new Color32(0x5E, 0x8C, 0x3A, 0xFF);

        [MenuItem("Desert Gecko/Test Sahnesini Kur")]
        public static void Build()
        {
            PlayerSettings.productName = "Desert Gecko Bandit";
            PlayerSettings.companyName = "Desert Gecko";

            foreach (string folder in Folders)
                EnsureFolder(folder);

            int groundLayer = EnsureLayer("Ground", 6);
            EnsureLayer("Player", 7);
            EnsureLayer("Enemy", 8);
            EnsureLayer("PlayerAttack", 9);

            Sprite tile = PlaceholderArt.Tile();
            Sprite gila = PlaceholderArt.Gila();
            PhysicsMaterial2D noFriction = CreateNoFrictionMaterial();
            CombatSetupBuilder.CombatAssets combat = CombatSetupBuilder.BuildPrefabs();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            var level = new GameObject("Level").transform;
            BuildLevel(level, tile, groundLayer);

            GameObject playerPrefab = BuildPlayerPrefab(gila, noFriction, combat);
            GilaSpriteBinder.Bind(); // gerçek Gila kareleri varsa placeholder'ın yerine geçer
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = Vector3.zero;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 1.5f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 360f / PixelsPerUnit / 2f; // 640x360 referans çözünürlük
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var follow = camGo.AddComponent<CameraFollow>();
            SetField(follow, "target", player.transform);

            var helperGo = new GameObject("TestLevelHelper");
            var helper = helperGo.AddComponent<TestLevelHelper>();
            SetField(helper, "player", player.GetComponent<PlayerController>());
            SetField(helper, "cameraFollow", follow);

            CombatSetupBuilder.PopulateLevel(combat);
            CombatSetupBuilder.BuildHud(cam, player, combat);
            BackgroundBuilder.Build(); // arka plan görselleri varsa paralaks katmanlarını ekler

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[DesertGecko] Test sahnesi kuruldu: " + ScenePath);
        }

        /// <summary>
        /// Komut satırı: sahneyi yeniden kurar ve oyun kamerasından ekran görüntüleri alır.
        /// -executeMethod DesertGecko.EditorTools.TestSceneBuilder.BuildAndCaptureBatch -shotDir klasör
        /// </summary>
        public static void BuildAndCaptureBatch()
        {
            string shotDir = SceneCapture.ArgumentValue("-shotDir") ?? "Temp/Screenshots";
            Build();
            SceneCapture.Capture(Path.Combine(shotDir, "sahne_baslangic.png"), new Vector3(6f, 1.5f, -10f));
            SceneCapture.Capture(Path.Combine(shotDir, "sahne_platformlar.png"), new Vector3(17f, 3f, -10f));
            SceneCapture.Capture(Path.Combine(shotDir, "sahne_checkpoint.png"), new Vector3(40f, 3f, -10f));
            EditorApplication.Exit(0);
        }

        // Birim = 1 tile = 32 piksel. Sahne soldan sağa: düz zemin, basamaklar, boşluk,
        // tırmanma duvarı, baca (duvar zıplaması), yüksek platformdan süzülerek geçilecek uçurum.
        static void BuildLevel(Transform parent, Sprite tile, int layer)
        {
            Block(parent, "Zemin_1", -12, 30, -4, 0, Sand, tile, layer);
            Block(parent, "Platform_1", 5, 9, 2, 2.5f, Plank, tile, layer);
            Block(parent, "Platform_2", 11, 14, 4, 4.5f, Plank, tile, layer);
            Block(parent, "Platform_3", 17, 20, 6, 6.5f, Plank, tile, layer);
            Block(parent, "SolDuvar", -13, -12, -4, 12, Rock, tile, layer);

            Block(parent, "Zemin_2", 34, 58, -4, 0, Sand, tile, layer);
            Block(parent, "TirmanmaDuvari", 40, 41, 0, 7, Rock, tile, layer);
            Block(parent, "Baca_Sol", 46, 47, 0, 11, Rock, tile, layer);
            Block(parent, "Baca_Sag", 49, 50, 3, 10.5f, Rock, tile, layer);
            Block(parent, "YuksekPlatform", 49, 58, 10.5f, 11, Plank, tile, layer);

            Block(parent, "Zemin_3", 72, 100, -4, 0, Sand, tile, layer);
            Block(parent, "SagDuvar", 100, 101, -4, 12, Rock, tile, layer);

            Decor(parent, "Kaktus_1", 2, 0, 1, 2, Cactus, tile);
            Decor(parent, "Kaktus_2", 24, 0, 1, 3, Cactus, tile);
            Decor(parent, "Kaktus_3", 80, 0, 1, 2, Cactus, tile);
            Decor(parent, "Bitis", 95, 0, 0.3f, 4, Color.red, tile);
        }

        static void Block(Transform parent, string name, float xMin, float xMax, float yMin, float yMax, Color color, Sprite sprite, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent);
            var size = new Vector2(xMax - xMin, yMax - yMin);
            go.transform.position = new Vector3(xMin + size.x / 2f, yMin + size.y / 2f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.color = color;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
        }

        static void Decor(Transform parent, string name, float x, float yBottom, float width, float height, Color color, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(x, yBottom + height / 2f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(width, height);
            sr.color = color;
            sr.sortingOrder = -1;
        }

        static GameObject BuildPlayerPrefab(Sprite sprite, PhysicsMaterial2D noFriction, CombatSetupBuilder.CombatAssets combat)
        {
            var root = new GameObject("Player") { layer = LayerMask.NameToLayer("Player") };

            var body = root.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.7f, 0.9f);
            box.offset = new Vector2(0f, 0.45f);
            box.sharedMaterial = noFriction;

            root.AddComponent<PlayerInputReader>();
            var controller = root.AddComponent<PlayerController>();
            SetLayerMask(controller, "groundMask", LayerMask.GetMask("Ground"));

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root.transform);
            visualGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var sr = visualGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 10;
            var visual = visualGo.AddComponent<PlayerVisual>();
            SetField(visual, "controller", controller);
            SetField(visual, "spriteRenderer", sr);

            CombatSetupBuilder.AddPlayerCombat(root, sr, combat);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static PhysicsMaterial2D CreateNoFrictionMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);
            if (mat != null)
                return mat;
            mat = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(mat, NoFrictionPath);
            return mat;
        }

        // --- Yardımcılar ---

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static int EnsureLayer(string name, int preferredIndex)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0)
                return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            int index = preferredIndex;
            if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue))
            {
                for (index = 8; index < 32; index++)
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue))
                        break;
            }
            layers.GetArrayElementAtIndex(index).stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return index;
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path))
                return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLayerMask(Object target, string field, int mask)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
