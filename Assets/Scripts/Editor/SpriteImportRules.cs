using UnityEditor;
using UnityEngine;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Assets/Sprites altındaki görseller için piksel sanat içe aktarma ayarları:
    /// Sprite, 32 PPU, Point filtre, sıkıştırmasız. Gila kareleri ayak hizasından pivotlanır.
    /// _Gelen klasörü (Gemini'nin ham çıktıları) dokunulmadan bırakılır.
    /// </summary>
    public class SpriteImportRules : AssetPostprocessor
    {
        public const string GilaFolder = "Assets/Sprites/Player/Gila/";
        const string SpritesFolder = "Assets/Sprites/";
        const string RawFolder = "Assets/Sprites/_Gelen/";

        // tools/gemini_sprite_isle.py: 64x64 tuval, ayaklar 48. satırda -> alttan 16/64
        static readonly Vector2 GilaPivot = new Vector2(0.5f, 0.25f);

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpritesFolder) || assetPath.StartsWith(RawFolder))
                return;

            bool isGila = assetPath.StartsWith(GilaFolder);
            // Gila kareleri script ile üretiliyor, ayarları hep zorla. Diğerlerinde elle yapılan değişikliklere saygı duy.
            if (!isGila && !assetImporter.importSettingsMissing)
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            if (isGila)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = GilaPivot;
                importer.SetTextureSettings(settings);
            }
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (path.StartsWith(GilaFolder) && path.EndsWith(".png"))
                {
                    EditorApplication.delayCall -= GilaSpriteBinder.Bind;
                    EditorApplication.delayCall += GilaSpriteBinder.Bind;
                    return;
                }
            }
        }
    }
}
