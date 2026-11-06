using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Gerçek görseller gelene kadar kullanılan basit piksel çizimler. Her çağrıda PNG'yi
    /// yeniden yazar ve sprite olarak (32 PPU, Point filtre) içe aktarır.
    /// </summary>
    public static class PlaceholderArt
    {
        public const string Folder = "Assets/Sprites/Placeholder";
        const float PixelsPerUnit = 32f;

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color32 Outline = new Color32(0x2B, 0x1A, 0x12, 0xFF);

        /// <summary>Basit piksel tuvali; (0,0) sol alt köşe.</summary>
        public class Canvas
        {
            public readonly int Width, Height;
            public readonly Color32[] Pixels;

            public Canvas(int width, int height)
            {
                Width = width;
                Height = height;
                Pixels = new Color32[width * height];
                for (int i = 0; i < Pixels.Length; i++) Pixels[i] = Clear;
            }

            public void Set(int x, int y, Color32 c)
            {
                if (x >= 0 && y >= 0 && x < Width && y < Height)
                    Pixels[y * Width + x] = c;
            }

            public void Rect(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, c);
            }

            /// <summary>İç dolgu ve 1 piksel dış çizgiyle dikdörtgen.</summary>
            public void Box(int x0, int y0, int x1, int y1, Color32 fill)
            {
                Rect(x0, y0, x1, y1, Outline);
                Rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1, fill);
            }

            public void Disc(float cx, float cy, float r, Color32 c)
            {
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r)
                        Set(x, y, c);
            }
        }

        public static Sprite Create(string fileName, int width, int height, Vector2 pivot, Action<Canvas> draw)
        {
            string path = $"{Folder}/{fileName}.png";
            var canvas = new Canvas(width, height);
            draw(canvas);

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(canvas.Pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
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
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

        // --- Seviye ---

        public static Sprite Tile() => Create("tile_placeholder", 32, 32, Center, c =>
        {
            c.Rect(0, 0, 31, 31, new Color32(200, 200, 200, 255));
            c.Rect(1, 1, 30, 30, new Color32(255, 255, 255, 255));
        });

        // Sağa bakan basit kertenkele silueti; gerçek Gila kareleri gelince kullanılmaz.
        public static Sprite Gila() => Create("gila_placeholder", 32, 32, Center, c =>
        {
            var body = new Color32(0xE8, 0xA0, 0x4A, 255);
            c.Box(9, 2, 22, 20, body);
            c.Box(13, 18, 27, 29, body);
            c.Rect(13, 17, 22, 19, new Color32(0xB5, 0x45, 0x2B, 255));
            c.Rect(22, 23, 25, 26, Outline);
            c.Rect(23, 24, 24, 25, new Color32(0xF4, 0xD3, 0x5E, 255));
            c.Box(2, 4, 9, 7, body);
            c.Rect(10, 0, 13, 2, Outline);
            c.Rect(18, 0, 21, 2, Outline);
        });

        // Kaktüs totemi (checkpoint): 1x2 tile, alttan pivotlu.
        public static Sprite Totem() => Create("totem_placeholder", 32, 64, BottomCenter, c =>
        {
            var green = new Color32(0x5E, 0x8C, 0x3A, 255);
            var face = new Color32(0xF4, 0xD3, 0x5E, 255);
            c.Box(10, 0, 21, 52, green);
            c.Box(3, 22, 10, 40, green);   // sol kol
            c.Box(21, 28, 28, 46, green);  // sağ kol
            c.Rect(13, 40, 14, 43, face);  // gözler
            c.Rect(17, 40, 18, 43, face);
            c.Rect(13, 33, 18, 34, face);  // ağız
            c.Box(11, 52, 20, 58, new Color32(0xB5, 0x45, 0x2B, 255)); // tepe çiçeği
        });

        // --- Düşman ---

        // Sola bakan akrep: gövde, kıskaçlar, yukarı kıvrık kuyruk ve iğne. Alttan pivotlu.
        public static Sprite Scorpion() => Create("akrep_placeholder", 32, 32, BottomCenter, c =>
        {
            var shell = new Color32(0x8E, 0x2E, 0x1E, 255);
            var light = new Color32(0xC9, 0x5A, 0x3A, 255);
            c.Box(6, 2, 22, 9, shell);          // gövde
            c.Rect(8, 7, 20, 8, light);
            c.Box(1, 6, 7, 11, shell);          // kıskaç
            c.Box(1, 11, 4, 14, shell);
            c.Box(21, 6, 25, 11, shell);        // kuyruk kökü
            c.Box(23, 10, 27, 17, shell);
            c.Box(20, 16, 26, 21, shell);       // kuyruk ucu öne kıvrılır
            c.Rect(18, 18, 20, 19, Outline);    // iğne
            c.Rect(7, 9, 8, 10, new Color32(0xF4, 0xD3, 0x5E, 255)); // göz
            for (int i = 0; i < 4; i++)         // bacaklar
                c.Rect(8 + i * 4, 0, 9 + i * 4, 2, Outline);
        });

        // --- Toplanabilirler ve oyuncu ---

        public static Sprite Bug() => Create("bocek_placeholder", 16, 16, Center, c =>
        {
            var shell = new Color32(0x4C, 0xA0, 0x3A, 255);
            c.Disc(8f, 7f, 5.5f, Outline);
            c.Disc(8f, 7f, 4.5f, shell);
            c.Rect(8, 3, 8, 11, Outline);                          // kanat çizgisi
            c.Rect(6, 12, 10, 14, Outline);                        // kafa
            c.Set(5, 15, Outline); c.Set(11, 15, Outline);         // antenler
            c.Rect(5, 8, 6, 9, new Color32(0x9A, 0xE0, 0x6A, 255)); // parlama
        });

        public static Sprite Bullet() => Create("mermi_placeholder", 8, 8, Center, c =>
        {
            c.Rect(0, 3, 6, 4, new Color32(0xE8, 0xC0, 0x4A, 255));
            c.Set(7, 3, new Color32(0xFF, 0xF0, 0xA0, 255));
            c.Set(7, 4, new Color32(0xFF, 0xF0, 0xA0, 255));
            c.Rect(0, 3, 1, 4, new Color32(0xA8, 0x70, 0x2A, 255)); // kovan
        });

        public static Sprite Crystal() => Create("kristal_placeholder", 16, 16, Center, c =>
        {
            var blue = new Color32(0x5A, 0xB8, 0xE8, 255);
            var shine = new Color32(0xD8, 0xF4, 0xFF, 255);
            for (int y = 0; y < 16; y++)
            {
                int half = y < 8 ? y : 15 - y;     // elmas şekli
                c.Rect(7 - half, y, 8 + half, y, Outline);
                if (half > 0)
                    c.Rect(8 - half, y, 7 + half, y, blue);
            }
            c.Rect(5, 8, 6, 10, shine);
        });

        public static Sprite TailProjectile() => Create("kuyruk_placeholder", 16, 16, Center, c =>
        {
            var tail = new Color32(0xE8, 0xA0, 0x4A, 255);
            c.Disc(8f, 8f, 6.5f, Outline);
            c.Disc(8f, 8f, 5.5f, tail);
            c.Disc(8f, 8f, 3f, Outline);
            c.Disc(8f, 8f, 2f, tail);
            c.Rect(8, 2, 14, 3, new Color32(0xB5, 0x45, 0x2B, 255)); // spiral ucu
        });

        // 1 birim genişliğinde dil; pivot sol kenarda, scale.x ile uzatılır.
        public static Sprite Tongue() => Create("dil_placeholder", 32, 6, new Vector2(0f, 0.5f), c =>
        {
            var pink = new Color32(0xE8, 0x6A, 0x8A, 255);
            c.Rect(0, 1, 27, 4, pink);
            c.Rect(0, 4, 27, 4, new Color32(0xF5, 0xA0, 0xB5, 255));
            c.Rect(26, 0, 31, 5, new Color32(0xC8, 0x3A, 0x5A, 255)); // yapışkan uç
        });

        // --- Level ---

        // Eğitim tabelası: direk üstünde ahşap levha. Alttan pivotlu.
        public static Sprite SignBoard() => Create("tabela_placeholder", 32, 32, BottomCenter, c =>
        {
            var wood = new Color32(0xA8, 0x6A, 0x3A, 255);
            var dark = new Color32(0x7A, 0x4B, 0x2A, 255);
            c.Box(14, 0, 17, 18, dark);            // direk
            c.Box(3, 14, 28, 28, wood);            // levha
            c.Rect(6, 23, 25, 23, dark);           // yazı çizgileri
            c.Rect(6, 19, 21, 19, dark);
        });

        // Bitiş direği: uzun ahşap direk. Alttan pivotlu, 3 birim.
        public static Sprite GoalPole() => Create("bitis_direk_placeholder", 16, 96, BottomCenter, c =>
        {
            var wood = new Color32(0x7A, 0x4B, 0x2A, 255);
            c.Box(6, 0, 9, 92, wood);
            c.Box(4, 90, 11, 95, new Color32(0xF4, 0xD3, 0x5E, 255)); // tepe topuzu
        });

        // "ARANIYOR" posteri bayrak gibi: direğe sol kenarından tutturulur (pivot sol orta).
        public static Sprite GoalFlag() => Create("bitis_bayrak_placeholder", 32, 24, new Vector2(0f, 0.5f), c =>
        {
            var paper = new Color32(0xF2, 0xE6, 0xD0, 255);
            var red = new Color32(0xB5, 0x45, 0x2B, 255);
            c.Box(0, 0, 31, 23, paper);
            c.Rect(3, 18, 28, 20, red);            // "ARANIYOR" şeridi
            c.Box(10, 4, 21, 15, new Color32(0xE8, 0xA0, 0x4A, 255)); // gecko yüzü
            c.Rect(18, 10, 19, 11, Outline);       // göz
        });

        // Düz beyaz kare: renklendirilip kenarlıksız dolgu olarak kullanılır (tile'ın ızgara çizgisi yok).
        public static Sprite Solid() => Create("duz_placeholder", 4, 4, Center, c => c.Rect(0, 0, 3, 3, new Color32(255, 255, 255, 255)));

        // Uçurum ağzı: yukarıda şeffaf, aşağıda koyu toprak (2x32, 1 birim). Ölçeklenerek kullanılır.
        public static Sprite PitGradient() => Create("ucurum_gecis", 2, 32, new Vector2(0.5f, 1f), c =>
        {
            for (int y = 0; y < 32; y++)
            {
                // Piksel sanat görünümü için 8 basamaklı geçiş (yumuşak değil)
                byte alpha = (byte)(Mathf.Floor((31 - y) / 4f) / 7f * 255f);
                c.Set(0, y, new Color32(0x1A, 0x0C, 0x09, alpha));
                c.Set(1, y, new Color32(0x1A, 0x0C, 0x09, alpha));
            }
        });

        // Fener parıltısı: piksel sanat için basamaklı halkalar, ortası dolu.
        public static Sprite Glow() => Create("parilti", 128, 128, Center, c =>
        {
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Mathf.Sqrt((x - 63.5f) * (x - 63.5f) + (y - 63.5f) * (y - 63.5f)) / 64f;
                if (d >= 1f)
                    continue;
                float falloff = (1f - d) * (1f - d); // kenara doğru hızla söner: halka kenarı görünmesin
                byte alpha = (byte)(Mathf.Ceil(falloff * 48f) / 48f * 255f);
                c.Set(x, y, new Color32(255, 255, 255, alpha));
            }
        });

        // Boru parçaları: koyu çelik, yer yer pas. Yan yana tekrar edince dikişsiz birleşir.
        static readonly Color32 PipeDark = new Color32(0x2E, 0x2C, 0x32, 0xFF);
        static readonly Color32 PipeMid = new Color32(0x5C, 0x5A, 0x60, 0xFF);
        static readonly Color32 PipeLight = new Color32(0x8E, 0x8F, 0x94, 0xFF);
        static readonly Color32 PipeRust = new Color32(0x7A, 0x48, 0x2E, 0xFF);
        static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

        // Dikey: 16 genişlik, 64 boy (2 birim). Her tekrarda bir kelepçe.
        public static Sprite PipeVertical() => Create("boru_dikey", 16, 64, TopCenter, c =>
        {
            c.Rect(1, 0, 14, 63, PipeDark);
            c.Rect(2, 0, 13, 63, PipeMid);
            c.Rect(4, 0, 6, 63, PipeLight);
            c.Rect(11, 0, 13, 63, new Color32(0x44, 0x42, 0x48, 0xFF));
            c.Rect(0, 26, 15, 33, PipeDark);
            c.Rect(1, 27, 14, 32, PipeMid);
            c.Rect(3, 27, 5, 32, PipeLight);
            c.Rect(7, 45, 9, 50, PipeRust);
            c.Set(2, 29, PipeDark);
            c.Set(13, 29, PipeDark);
        });

        public static Sprite PipeHorizontal() => Create("boru_yatay", 64, 16, TopCenter, c =>
        {
            c.Rect(0, 1, 63, 14, PipeDark);
            c.Rect(0, 2, 63, 13, PipeMid);
            c.Rect(0, 9, 63, 11, PipeLight);
            c.Rect(0, 2, 63, 4, new Color32(0x44, 0x42, 0x48, 0xFF));
            c.Rect(26, 0, 33, 15, PipeDark);
            c.Rect(27, 1, 32, 14, PipeMid);
            c.Rect(27, 10, 32, 12, PipeLight);
            c.Rect(46, 5, 51, 7, PipeRust);
        });

        // Birleşim: boruların kesiştiği ya da döndüğü yerdeki kalın parça.
        public static Sprite PipeJoint() => Create("boru_birlesim", 22, 22, Center, c =>
        {
            c.Rect(1, 1, 20, 20, PipeDark);
            c.Rect(2, 2, 19, 19, PipeMid);
            c.Rect(4, 13, 14, 17, PipeLight);
            c.Rect(2, 2, 19, 4, new Color32(0x44, 0x42, 0x48, 0xFF));
            foreach (var (x, y) in new[] { (3, 3), (18, 3), (3, 18), (18, 18) })
                c.Set(x, y, PipeDark);
            c.Rect(14, 6, 17, 8, PipeRust);
        });

        // Vana: kırmızı çarklı birleşim.
        public static Sprite PipeValve() => Create("boru_vana", 26, 26, Center, c =>
        {
            var wheel = new Color32(0x9A, 0x3A, 0x2C, 0xFF);
            c.Disc(13f, 13f, 12f, PipeDark);
            c.Disc(13f, 13f, 11f, wheel);
            c.Disc(13f, 13f, 8f, PipeDark);
            c.Rect(12, 3, 13, 22, wheel);
            c.Rect(3, 12, 22, 13, wheel);
            c.Disc(13f, 13f, 3f, PipeLight);
        });

        // --- Arayüz ---

        public static Sprite Heart(int fill) => Create($"kalp_{(fill == 2 ? "dolu" : fill == 1 ? "yarim" : "bos")}", 16, 16, Center, c =>
        {
            var red = new Color32(0xD8, 0x3A, 0x3A, 255);
            var empty = new Color32(0x4A, 0x3A, 0x40, 255);
            // X = dış çizgi, # = iç; yarım kalpte sol yarı (x <= 7) dolu.
            string[] shape =
            {
                "................",
                "..XXX.....XXX...",
                ".X###X...X###X..",
                "X#####X.X#####X.",
                "X######X######X.",
                "X#############X.",
                "X#############X.",
                ".X###########X..",
                "..X#########X...",
                "...X#######X....",
                "....X#####X.....",
                ".....X###X......",
                "......X#X.......",
                ".......X........",
                "................",
                "................",
            };
            for (int row = 0; row < 16; row++)
            for (int x = 0; x < 16; x++)
            {
                int y = 15 - row; // tablo yukarıdan aşağı, tuval aşağıdan yukarı
                char cell = shape[row][x];
                if (cell == 'X')
                    c.Set(x, y, Outline);
                else if (cell == '#')
                    c.Set(x, y, fill == 2 || (fill == 1 && x <= 7) ? red : empty);
            }
        });

        // Kuyruk bekleme çemberi: Image (Filled, Radial 360) için halka.
        public static Sprite Ring() => Create("halka_placeholder", 32, 32, Center, c =>
        {
            var white = new Color32(255, 255, 255, 255);
            c.Disc(16f, 16f, 15f, white);
            c.Disc(16f, 16f, 10f, Clear);
        });
    }
}
