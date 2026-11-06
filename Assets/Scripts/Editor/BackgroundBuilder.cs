using System.IO;
using System.Collections.Generic;
using DesertGecko.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Paralaks arka plan katmanlarını açık sahneye kurar (eskisini silip yeniden).
    /// Menü: Desert Gecko → Arka Planı Kur. TestSceneBuilder da sahneyi kurarken bunu çağırır.
    /// </summary>
    public static class BackgroundBuilder
    {
        // tools/gemini_arkaplan_isle.py BOTTOM_PAD ile aynı olmalı
        public const int BottomPadPixels = 128;
        const float PixelsPerUnit = 32f;
        const string RootName = "Arkaplan";
        const string TestScenePath = "Assets/Scenes/TestLevel_Movement.unity";

        // Oyuncu yerde dururken kameranın yüksekliği (zemin y=0 + CameraFollow offset 1.5)
        static readonly Vector2 CameraOrigin = new Vector2(0f, 1.5f);

        struct Layer
        {
            public string Name, Sprite;
            public Vector2 Follow;
            public float HorizonY;   // katmanın asıl alt kenarının dünyadaki yüksekliği veya dikey ofseti
            public int Order;
            public bool Repeat;
            public int TileCount;
        }

        static readonly Layer[] DayLayers =
        {
            // Gökyüzü kameraya 1:1 kilitli; HorizonY ile dikey konumu biraz aşağı çekilir (-1.2f). TileCount=5 ile kenar boşluğu kalmaz.
            new Layer { Name = "Gokyuzu", Sprite = "bg_gunduz_gokyuzu", Follow = new Vector2(1f, 1f), HorizonY = -1.2f, Order = -100, Repeat = true, TileCount = 5 },
            new Layer { Name = "UzakMesalar", Sprite = "bg_gunduz_uzak", Follow = new Vector2(0.85f, 0.75f), HorizonY = 1.2f, Order = -90, Repeat = true, TileCount = 4 },
            new Layer { Name = "OrtaTepeler", Sprite = "bg_gunduz_orta", Follow = new Vector2(0.6f, 0.5f), HorizonY = 0f, Order = -80, Repeat = true, TileCount = 4 },
        };

        // Gündüz katmanlarıyla aynı şekiller, hemen önlerinde: şafakta solarak gündüzü açığa çıkarırlar.
        static readonly Layer[] NightLayers =
        {
            new Layer { Name = "GeceGokyuzu", Sprite = "bg_gece_gokyuzu_uzun", Follow = new Vector2(1f, 1f), HorizonY = -1.2f, Order = -99, Repeat = true, TileCount = 5 },
            new Layer { Name = "GeceMesalar", Sprite = "bg_gece_uzak", Follow = new Vector2(0.85f, 0.75f), HorizonY = 1.2f, Order = -89, Repeat = true, TileCount = 4 },
            new Layer { Name = "GeceTepeler", Sprite = "bg_gece_orta", Follow = new Vector2(0.6f, 0.5f), HorizonY = 0f, Order = -79, Repeat = true, TileCount = 4 },
            new Layer { Name = "GeceOnPlan", Sprite = "bg_gece_on", Follow = new Vector2(0.4f, 0.3f), HorizonY = -1f, Order = -70, Repeat = true, TileCount = 4 },
        };

        const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        /// <summary>Gece katmanlarını gündüzün önüne ekler; hepsi ışıktan etkilenmez, gökyüzü kendi renginde kalır.</summary>
        public static SpriteRenderer[] BuildNight()
        {
            var root = GameObject.Find(RootName).transform;
            var material = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>())
                sr.sharedMaterial = material;
            var night = new List<SpriteRenderer>();
            foreach (Layer layer in NightLayers)
            {
                SpriteRenderer sr = AddLayer(root, layer, Camera.main);
                if (sr != null)
                {
                    sr.sharedMaterial = material;
                    night.Add(sr);
                }
            }
            return night.ToArray();
        }

        [MenuItem("Desert Gecko/Arka Planı Kur")]
        public static void BuildInActiveScene()
        {
            if (Build())
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>Arka planı açık sahneye kurar. Katman görselleri yoksa hiçbir şey yapmaz.</summary>
        public static bool Build()
        {
            Camera cam = Camera.main;
            if (cam == null || LoadSprite(DayLayers[0].Sprite) == null)
                return false;

            GameObject old = GameObject.Find(RootName);
            if (old != null)
                Object.DestroyImmediate(old);

            var root = new GameObject(RootName).transform;
            foreach (Layer layer in DayLayers)
                AddLayer(root, layer, cam);
            return true;
        }

        static SpriteRenderer AddLayer(Transform root, Layer layer, Camera cam)
        {
            Sprite sprite = LoadSprite(layer.Sprite);
            if (sprite == null)
                return null;

            var go = new GameObject(layer.Name);
            go.transform.SetParent(root);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = layer.Order;

            // Gökyüzü ortadan, diğerleri alt kenardan pivotlu.
            // Gökyüzü kameraya 1:1 kilitli ve HorizonY ile aşağı kaydırılır; diğer katmanlar ufuk hizasına göre konumlanır.
            Vector2 origin = (layer.Follow == Vector2.one)
                ? new Vector2(CameraOrigin.x, CameraOrigin.y + layer.HorizonY)
                : new Vector2(CameraOrigin.x, layer.HorizonY - BottomPadPixels / PixelsPerUnit);

            var parallax = go.AddComponent<ParallaxLayer>();
            var so = new SerializedObject(parallax);
            so.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
            so.FindProperty("followFactor").vector2Value = layer.Follow;
            so.FindProperty("cameraOrigin").vector2Value = CameraOrigin;
            so.FindProperty("layerOrigin").vector2Value = origin;
            so.FindProperty("repeatX").boolValue = layer.Repeat;
            if (layer.TileCount > 0)
                so.FindProperty("tileCount").intValue = layer.TileCount;
            so.ApplyModifiedPropertiesWithoutUndo();

            parallax.Setup();
            parallax.Follow(cam.transform.position);
            return sr;
        }

        static Sprite LoadSprite(string name)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteImportRules.BackgroundFolder + name + ".png");
            if (s == null)
                s = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteImportRules.BackgroundFolder + name + ".jpg");
            return s;
        }

        /// <summary>
        /// Komut satırı: test sahnesine arka planı kurar, kaydeder ve oyun kamerasından ekran görüntüleri alır.
        /// -executeMethod DesertGecko.EditorTools.BackgroundBuilder.BuildAndCaptureBatch -shotDir klasör
        /// </summary>
        public static void BuildAndCaptureBatch()
        {
            string shotDir = SceneCapture.ArgumentValue("-shotDir") ?? "Temp/Screenshots";

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteImportRules.BackgroundFolder.TrimEnd('/') }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

            Scene scene = EditorSceneManager.OpenScene(TestScenePath);
            bool built = Build();
            Debug.Log($"[DesertGecko] Arka plan kuruldu: {built}");

            GameObject player = GameObject.Find("Player");
            if (player != null)
                player.transform.position = Vector3.zero;
            EditorSceneManager.SaveScene(scene);

            SceneCapture.Capture(Path.Combine(shotDir, "arkaplan_baslangic.png"), new Vector3(2f, CameraOrigin.y, -10f));
            SceneCapture.Capture(Path.Combine(shotDir, "arkaplan_baca.png"), new Vector3(48f, 6f, -10f));
            SceneCapture.Capture(Path.Combine(shotDir, "arkaplan_ucurum.png"), new Vector3(64f, 6f, -10f));

            EditorApplication.Exit(built ? 0 : 1);
        }
    }
}