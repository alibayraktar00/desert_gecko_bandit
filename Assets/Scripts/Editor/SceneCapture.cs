using System.IO;
using DesertGecko.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Açık sahneyi oyun kamerasından PNG'ye çizer (editörde veya batch modda).
    /// Paralaks katmanları verilen kamera konumuna göre yerleştirilir; kamera eski yerine döner.
    /// </summary>
    public static class SceneCapture
    {
        const int Width = 640, Height = 360;
        static bool warmedUp;

        public static void Capture(string path, Vector3 cameraPosition)
        {
            // Batch modda ilk çizimde dokular henüz GPU'ya yüklenmemiş oluyor; bir kez boşa çiz.
            if (!warmedUp)
            {
                warmedUp = true;
                Render(cameraPosition);
            }
            byte[] png = Render(cameraPosition);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, png);
            Debug.Log("[DesertGecko] Ekran görüntüsü: " + path);
        }

        static byte[] Render(Vector3 cameraPosition)
        {
            Camera cam = Camera.main;
            Vector3 saved = cam.transform.position;
            cam.transform.position = cameraPosition;
            foreach (ParallaxLayer layer in Object.FindObjectsByType<ParallaxLayer>())
            {
                layer.Setup();
                layer.Follow(cameraPosition);
            }

            var rt = new RenderTexture(Width, Height, 24);
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
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            byte[] png = tex.EncodeToPNG();

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            cam.transform.position = saved;
            return png;
        }

        /// <summary>
        /// Komut satırı: test sahnesini OLDUĞU GİBİ açar (yeniden kurmaz) ve ekran görüntüleri alır.
        /// Elle düzenlenmiş sahneyi bozmadan kontrol etmek için.
        /// -executeMethod DesertGecko.EditorTools.SceneCapture.CaptureTestSceneBatch -shotDir klasör
        /// </summary>
        public static void CaptureTestSceneBatch()
        {
            string shotDir = ArgumentValue("-shotDir") ?? "Temp/Screenshots";
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/TestLevel_Movement.unity");
            Capture(Path.Combine(shotDir, "sahne_baslangic.png"), new Vector3(6f, 1.5f, -10f));
            Capture(Path.Combine(shotDir, "sahne_derin.png"), new Vector3(20f, -2f, -10f));
            Capture(Path.Combine(shotDir, "sahne_son_bolum.png"), new Vector3(85f, 3f, -10f));
            UnityEditor.EditorApplication.Exit(0);
        }

        public static string ArgumentValue(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
