using UnityEditor;
using UnityEngine;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Assets/Sprites altındaki görseller için piksel sanat içe aktarma ayarları:
    /// Sprite, 32 PPU, Point filtre, sıkıştırmasız. Gila kareleri ayak hizasından,
    /// arka plan katmanları alt kenarından pivotlanır (gökyüzü ortadan).
    /// _Gelen klasörü (Gemini'nin ham çıktıları) dokunulmadan bırakılır.
    /// </summary>
    public class SpriteImportRules : AssetPostprocessor
    {
        public const string GilaFolder = "Assets/Sprites/Player/Gila/";
        public const string BackgroundFolder = "Assets/Sprites/Environment/Background/";
        const string SpritesFolder = "Assets/Sprites/";
        const string RawFolder = "Assets/Sprites/_Gelen/";

        // tools/gemini_sprite_isle.py: 64x64 tuval, ayaklar 48. satırda -> alttan 16/64
        static readonly Vector2 GilaPivot = new Vector2(0.5f, 0.25f);

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpritesFolder) || assetPath.StartsWith(RawFolder))
                return;

            bool isGila = assetPath.StartsWith(GilaFolder);
            bool isBackground = assetPath.StartsWith(BackgroundFolder);
            // Bu klasörlerdeki görseller script ile üretiliyor, ayarları hep zorla.
            // Diğerlerinde elle yapılan değişikliklere saygı duy.
            if (!isGila && !isBackground && !assetImporter.importSettingsMissing)
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
            else if (isBackground)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                bool isSky = assetPath.Contains("gokyuzu");
                settings.spriteAlignment = (int)(isSky ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
                settings.spriteMeshType = SpriteMeshType.FullRect; // yan yana tekrar (Tiled) için gerekli
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
