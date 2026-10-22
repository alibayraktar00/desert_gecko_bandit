using System.IO;
using DesertGecko.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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
            public float HorizonY;   // katmanın asıl alt kenarının dünyadaki yüksekliği (kamera CameraOrigin'deyken)
            public int Order;
            public bool Repeat;
        }

        static readonly Layer[] DayLayers =
        {
            new Layer { Name = "Gokyuzu", Sprite = "bg_gunduz_gokyuzu", Follow = new Vector2(1f, 1f), Order = -100, Repeat = false },
            new Layer { Name = "UzakMesalar", Sprite = "bg_gunduz_uzak", Follow = new Vector2(0.85f, 0.75f), HorizonY = 1.2f, Order = -90, Repeat = true },
            new Layer { Name = "OrtaTepeler", Sprite = "bg_gunduz_orta", Follow = new Vector2(0.6f, 0.5f), HorizonY = 0.2f, Order = -80, Repeat = true },
        };

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
            {
                Sprite sprite = LoadSprite(layer.Sprite);
                if (sprite == null)
                    continue;

                var go = new GameObject(layer.Name);
                go.transform.SetParent(root);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = layer.Order;

                // Gökyüzü ortadan, diğerleri alt kenardan pivotlu; alttaki dolgu payı kadar aşağı it.
                Vector2 origin = layer.Repeat
                    ? new Vector2(CameraOrigin.x, layer.HorizonY - BottomPadPixels / PixelsPerUnit)
                    : CameraOrigin;

                var parallax = go.AddComponent<ParallaxLayer>();
                var so = new SerializedObject(parallax);
                so.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
                so.FindProperty("followFactor").vector2Value = layer.Follow;
                so.FindProperty("cameraOrigin").vector2Value = CameraOrigin;
                so.FindProperty("layerOrigin").vector2Value = origin;
                so.FindProperty("repeatX").boolValue = layer.Repeat;
                so.ApplyModifiedPropertiesWithoutUndo();

                parallax.Setup();
                parallax.Follow(cam.transform.position);
            }
            return true;
        }

        static Sprite LoadSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(SpriteImportRules.BackgroundFolder + name + ".png");

        /// <summary>
        /// Komut satırı: test sahnesine arka planı kurar, kaydeder ve oyun kamerasından ekran görüntüleri alır.
        /// -executeMethod DesertGecko.EditorTools.BackgroundBuilder.BuildAndCaptureBatch -shotDir klasör
        /// </summary>
        public static void BuildAndCaptureBatch()
        {
            string shotDir = ArgumentValue("-shotDir") ?? "Temp/Screenshots";
            Directory.CreateDirectory(shotDir);

            // Görseller bu scriptler derlenmeden içe aktarıldıysa kurallar uygulanmamış olabilir.
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteImportRules.BackgroundFolder.TrimEnd('/') }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

            Scene scene = EditorSceneManager.OpenScene(TestScenePath);
            bool built = Build();
            Debug.Log($"[DesertGecko] Arka plan kuruldu: {built}");

            // Oyuncu başlangıçta havada duruyor; görüntü için yere koy.
            GameObject player = GameObject.Find("Player");
            if (player != null)
                player.transform.position = Vector3.zero;
            EditorSceneManager.SaveScene(scene);

            // Batch modda ilk çizimde dokular henüz GPU'ya yüklenmemiş oluyor; bir kez boşa çiz.
            Capture(Path.Combine(shotDir, "_isinma.png"), new Vector3(2f, CameraOrigin.y, -10f));
            File.Delete(Path.Combine(shotDir, "_isinma.png"));

            Capture(Path.Combine(shotDir, "arkaplan_baslangic.png"), new Vector3(2f, CameraOrigin.y, -10f));
            Capture(Path.Combine(shotDir, "arkaplan_baca.png"), new Vector3(48f, 6f, -10f));
            Capture(Path.Combine(shotDir, "arkaplan_ucurum.png"), new Vector3(64f, 6f, -10f));

            EditorApplication.Exit(built ? 0 : 1);
        }

        static void Capture(string path, Vector3 cameraPosition)
        {
            Camera cam = Camera.main;
            Vector3 saved = cam.transform.position;
            cam.transform.position = cameraPosition;
            foreach (ParallaxLayer layer in Object.FindObjectsByType<ParallaxLayer>())
            {
                layer.Setup();
                layer.Follow(cameraPosition);
            }

            const int width = 640, height = 360;
            var rt = new RenderTexture(width, height, 24);
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
                RenderPipeline.SubmitRenderRequest(cam, request);
            else
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);

            // Sahne kaydedildikten sonra çağrıldığı için kalıcı değişiklik bırakmasın.
            cam.transform.position = saved;
            Debug.Log("[DesertGecko] Ekran görüntüsü: " + path);
        }

        static string ArgumentValue(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
