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
    /// Placeholder sprite'larla hareket test sahnesini sıfırdan kurar.
    /// Menü: Desert Gecko → Test Sahnesini Kur. Komut satırından -executeMethod ile de çalışır.
    /// </summary>
    public static class TestSceneBuilder
    {
        const int PixelsPerUnit = 32;
        const string ScenePath = "Assets/Scenes/TestLevel_Movement.unity";
        const string PlaceholderDir = "Assets/Sprites/Placeholder";
        const string TilePath = PlaceholderDir + "/tile_placeholder.png";
        const string GilaPath = PlaceholderDir + "/gila_placeholder.png";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const string NoFrictionPath = "Assets/Materials/NoFriction.physicsMaterial2D";

        static readonly string[] Folders =
        {
            "Assets/Scripts/Player", "Assets/Scripts/Environment", "Assets/Scripts/Enemies",
            "Assets/Scripts/Collectibles", "Assets/Scripts/UI", "Assets/Scripts/DevTools",
            "Assets/Sprites/Player", "Assets/Sprites/Enemies", "Assets/Sprites/Environment",
            "Assets/Sprites/UI", "Assets/Sprites/_Gelen", PlaceholderDir,
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

            Sprite tile = CreatePlaceholderSprite(TilePath, DrawTile);
            Sprite gila = CreatePlaceholderSprite(GilaPath, DrawGila);
            PhysicsMaterial2D noFriction = CreateNoFrictionMaterial();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            var level = new GameObject("Level").transform;
            BuildLevel(level, tile, groundLayer);

            GameObject playerPrefab = BuildPlayerPrefab(gila, noFriction);
            GilaSpriteBinder.Bind(); // gerçek Gila kareleri varsa placeholder'ın yerine geçer
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0f, 1f, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 2.5f, -10f);
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

            BackgroundBuilder.Build(); // arka plan görselleri varsa paralaks katmanlarını ekler

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[DesertGecko] Test sahnesi kuruldu: " + ScenePath);
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

        static GameObject BuildPlayerPrefab(Sprite sprite, PhysicsMaterial2D noFriction)
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

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // --- Placeholder sprite çizimleri (32x32) ---

        static void DrawTile(Color32[] px)
        {
            var fill = new Color32(255, 255, 255, 255);
            var edge = new Color32(200, 200, 200, 255);
            for (int y = 0; y < PixelsPerUnit; y++)
            for (int x = 0; x < PixelsPerUnit; x++)
            {
                bool border = x == 0 || y == 0 || x == PixelsPerUnit - 1 || y == PixelsPerUnit - 1;
                px[y * PixelsPerUnit + x] = border ? edge : fill;
            }
        }

        // Sağa bakan basit kertenkele silueti: gövde, kafa, göz, kuyruk. Yönü görmeye yeter.
        static void DrawGila(Color32[] px)
        {
            var clear = new Color32(0, 0, 0, 0);
            var body = new Color32(0xE8, 0xA0, 0x4A, 255);
            var outline = new Color32(0x2B, 0x1A, 0x12, 255);
            var bandana = new Color32(0xB5, 0x45, 0x2B, 255);
            var eye = new Color32(0xF4, 0xD3, 0x5E, 255);

            for (int i = 0; i < px.Length; i++) px[i] = clear;

            void Rect(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    px[y * PixelsPerUnit + x] = c;
            }

            Rect(9, 2, 22, 20, outline);   // gövde dış çizgi
            Rect(10, 3, 21, 19, body);
            Rect(13, 18, 27, 29, outline); // kafa
            Rect(14, 19, 26, 28, body);
            Rect(13, 17, 22, 19, bandana); // boyundaki bandana
            Rect(22, 23, 25, 26, outline); // göz
            Rect(23, 24, 24, 25, eye);
            Rect(2, 4, 9, 7, outline);     // kuyruk
            Rect(3, 5, 9, 6, body);
            Rect(10, 0, 13, 2, outline);   // ayaklar
            Rect(18, 0, 21, 2, outline);
        }

        static Sprite CreatePlaceholderSprite(string path, System.Action<Color32[]> draw)
        {
            var tex = new Texture2D(PixelsPerUnit, PixelsPerUnit, TextureFormat.RGBA32, false);
            var pixels = new Color32[PixelsPerUnit * PixelsPerUnit];
            draw(pixels);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // Tiled çizim için gerekli
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
