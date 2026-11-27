using UnityEditor;
using UnityEngine;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Assets/Fonts altındaki fontlar (eğitim parşömeninin yazısı, Merriweather Bold): dinamik ve yumuşak (Smooth)
    /// üretilir; parşömen kanvası yazıyı 4 kat çözünürlükte çizer, böylece ekranda net ve okunaklı durur.
    /// </summary>
    public class FontImportRules : AssetPostprocessor
    {
        const string FontsFolder = "Assets/Fonts/";

        void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith(FontsFolder) || !(assetImporter is TrueTypeFontImporter importer))
                return;
            importer.fontSize = 32;
            importer.fontRenderingMode = FontRenderingMode.Smooth;
            importer.fontTextureCase = FontTextureCase.Dynamic;
            importer.includeFontData = true;
        }

        /// <summary>Kurallar sonradan eklendiğinde fontu ve parşömen görsellerini yeniden içe aktarır.</summary>
        [MenuItem("Desert Gecko/Parşömen ve Fontu Yeniden İçe Aktar")]
        public static string ReimportParchment()
        {
            int n = 0;
            foreach (string folder in new[] { FontsFolder.TrimEnd('/'), SpriteImportRules.ParchmentFolder.TrimEnd('/') })
                foreach (string guid in AssetDatabase.FindAssets("t:Font t:Texture2D", new[] { folder }))
                {
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                    n++;
                }
            return n + " dosya";
        }
    }
}
