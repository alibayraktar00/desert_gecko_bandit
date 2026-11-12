using UnityEditor;
using UnityEngine;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Assets/Sprites altındaki görseller için piksel sanat içe aktarma ayarları:
    /// Sprite, 32 PPU, Point filtre, sıkıştırmasız. Gila kareleri ayak hizasından,
    /// arka plan katmanları ve düşman kareleri alt kenarından pivotlanır (gökyüzü ortadan).
    /// _Gelen klasörü (Gemini'nin ham çıktıları) dokunulmadan bırakılır.
    /// </summary>
    public class SpriteImportRules : AssetPostprocessor
    {
        public const string GilaFolder = "Assets/Sprites/Player/Gila/";
        public const string BackgroundFolder = "Assets/Sprites/Environment/Background/";
        public const string PropsFolder = "Assets/Sprites/Environment/Props/";
        public const string GroundFolder = "Assets/Sprites/Environment/Ground/";
        public const string EnemiesFolder = "Assets/Sprites/Enemies/";
        public const string LayerArtFolder = "Assets/Sprites/Environment/Katman/";
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
            bool isProp = assetPath.StartsWith(PropsFolder);
            bool isGround = assetPath.StartsWith(GroundFolder);
            bool isEnemy = assetPath.StartsWith(EnemiesFolder);
            bool isLayerArt = assetPath.StartsWith(LayerArtFolder);
            // Bu klasörlerdeki görseller script ile üretiliyor, ayarları hep zorla.
            // Diğerlerinde elle yapılan değişikliklere saygı duy.
            if (!isGila && !isBackground && !isProp && !isGround && !isEnemy && !isLayerArt && !assetImporter.importSettingsMissing)
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
            else if (isProp || isEnemy)
            {
                // Kaktüsler ve düşmanlar yere, platformlar üst kenarlarından yerleştirilir; hepsi için alt orta pivot.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                // tavan_ ile başlayanlar (sarkıt, kök, zincir) üst kenarından asılır.
                bool hangs = System.IO.Path.GetFileName(assetPath).StartsWith("tavan_");
                settings.spriteAlignment = (int)(hangs ? SpriteAlignment.TopCenter : SpriteAlignment.BottomCenter);
                if (assetPath.EndsWith("tavan_kapi_kanat.png"))
                {
                    // Dövüş odası kapısı odanın boyuna uzar: kirişler ve dikenler sabit, ortadaki tahtalar tekrar eder.
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteBorder = new Vector4(0f, 11f, 0f, 11f);
                }
                else if (assetPath.EndsWith("tavan_kapi_ust.png"))
                {
                    // Kapının üstündeki taş sütun: topraklı üst kenar sabit, iki tuğla sırası tekrar eder.
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteBorder = new Vector4(0f, 15f, 0f, 9f);
                }
                importer.SetTextureSettings(settings);
            }
            else if (isLayerArt)
            {
                // Hepsi üst kenarından pivotlu ve yan yana tekrar eder; kaya çıkıntısının uçları tekrar etmez.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                // Mağara arka duvarı her odanın tabanına oturur: alt kenarından pivotlu, kısalırsa üstü kesilir.
                settings.spriteAlignment = (int)(assetPath.EndsWith("mg_arka.png") ? SpriteAlignment.BottomCenter : SpriteAlignment.TopCenter);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                if (assetPath.EndsWith("kaya_kenar.png"))
                    settings.spriteBorder = new Vector4(16f, 0f, 16f, 0f);
                importer.SetTextureSettings(settings);
                // Mağara arka duvarı küçük bir kaynaktan geliyor: oda boyunu kaplasın diye büyütülür. 16'da pikseller
                // kaba kare görünüyordu; 22 oda yüksekliğine (~8.7 birim) yetiyor, pikseller daha ince.
                if (assetPath.EndsWith("mg_arka.png"))
                    importer.spritePixelsPerUnit = 22;
            }
            else if (isGround)
            {
                // Zemin dokusu üst kenarından pivotlu: blok yüzeyine oturur, aşağı doğru yan yana tekrar eder.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.TopCenter;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
        }

        /// <summary>
        /// Katman görsellerini zorla yeniden içe aktarır. Kurallar değişip görsel aynı kalınca Unity eski ayarı tutar
        /// (ör. pivot); bu menü yeni kuralı uygular.
        /// </summary>
        [MenuItem("Desert Gecko/Katman Görsellerini Yeniden İçe Aktar")]
        public static string ReimportLayerArt()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { LayerArtFolder.TrimEnd('/') });
            foreach (string guid in guids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            return guids.Length + " görsel";
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
