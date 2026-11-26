using DesertGecko.Player;
using DesertGecko.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Levellerin duraklatma menüsünü kurar: karartma, solda dönen revolver tamburu (Gemini görseli, tools/durdur_tambur_isle.py),
    /// haznelere oturan fişekler, tamburun çevresinde dönen küçük piksel yazılar, saat 3'teki seçili hazneden sağa
    /// açılan dalgalı bordo bandana (UiWave), üstündeki seçili yazı ve duman. Tambur, fişek ve yazılar 2x piksel
    /// boyunda; bandana ve duman Gemini'nin ince pikselleriyle 1x çizilir (640x360 referans kanvas).
    /// </summary>
    public static class PauseMenuBuilder
    {
        const string Art = "Assets/Sprites/UI/Pause/";
        const float Scale = 2f;
        // tools/durdur_tambur_isle.py çıktısı: hazne halkası yarıçapı ~27 px (sprite pikseli)
        const float ChamberRadiusPx = 27f;
        // tools/durdur_tambur_isle.py'nin ölçtüğü hazne merkezleri (sprite pikseli, görsel ortasına göre, y yukarı),
        // saat 12'den saat yönünde. Görsel değişirse aracın çıktısıyla güncellenir.
        static readonly Vector2[] HoleOffsetsPx =
        {
            new Vector2(-0.5f, 27.5f), new Vector2(22.5f, 13.5f), new Vector2(22.5f, -12.5f),
            new Vector2(-0.5f, -26.5f), new Vector2(-23.5f, -12.5f), new Vector2(-23.5f, 13.5f),
        };
        static readonly Vector2 DrumCenter = new Vector2(-140f, 0f);
        const float BannerScale = 1.5f; // bandana ince pikselli; tamburla orantılı dursun
        const float LabelRadius = 118f;
        const float BannerTuck = 40f; // bandananın sol ucu tambur merkezinin bu kadar sağından başlar (tamburun arkasında kalır)
        static readonly string[] LabelKeys = { "devam", "bastan", "ayarlar", "menu", "ses", "tamekran", "geri" };

        public static void Build(Transform canvas, PlayerInputReader input)
        {
            RectTransform overlay = UiKit.Overlay("Duraklat", canvas, new Color(0.05f, 0.02f, 0.02f, 0.6f));
            Image dim = overlay.GetComponent<Image>();

            // bandana tamburdan önce eklenir: sol ucu tamburun arkasında kalır
            Sprite bannerSprite = Load("durdur_bandana.png");
            Image bannerImage = Sprite("Bandana", overlay, bannerSprite, Vector2.zero, BannerScale);
            RectTransform bannerRect = bannerImage.rectTransform;
            bannerRect.pivot = new Vector2(0f, 0.5f);
            bannerRect.anchoredPosition = DrumCenter + new Vector2(BannerTuck, 0f);
            UiWave wave = bannerImage.gameObject.AddComponent<UiWave>();

            Sprite drumSprite = Load("durdur_tambur.png");
            Image drum = Sprite("Tambur", overlay, drumSprite, DrumCenter);
            var labels = new Image[PauseMenu.Chambers];
            var cartridges = new Image[PauseMenu.Chambers];
            Sprite cartridge = Load("durdur_fisek.png");
            for (int i = 0; i < PauseMenu.Chambers; i++)
                cartridges[i] = Sprite($"Fisek_{i}", overlay, cartridge, DrumCenter);

            for (int i = 0; i < PauseMenu.Chambers; i++)
                labels[i] = Sprite($"Yazi_{i}", overlay, null, DrumCenter);
            Image bannerLabel = Sprite("BandanaYazi", overlay, null, bannerRect.anchoredPosition);
            Image smoke = Sprite("Duman", overlay, Load("durdur_duman.png"), bannerRect.anchoredPosition, BannerScale);
            smoke.enabled = false;

            // ses göstergesi: "SES" seçiliyken bandananın üstünde, yazının yanında 10 dilim (PauseMenu yerleştirir)
            RectTransform volume = UiKit.Rect("SesGostergesi", overlay, new Vector2(0.5f, 0.5f), bannerRect.anchoredPosition, new Vector2(124f, 12f));
            volume.gameObject.AddComponent<Image>().color = new Color32(0x30, 0x08, 0x14, 0xFF);
            volume.gameObject.AddComponent<CanvasGroup>();
            var segments = new Image[10];
            for (int i = 0; i < segments.Length; i++)
            {
                RectTransform seg = UiKit.Rect($"Dilim_{i}", volume, new Vector2(0.5f, 0.5f), new Vector2(-54f + i * 12f, 0f), new Vector2(10f, 8f));
                segments[i] = seg.gameObject.AddComponent<Image>();
            }
            volume.gameObject.SetActive(false);

            var audio = canvas.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.ignoreListenerPause = true;

            var pause = canvas.gameObject.AddComponent<PauseMenu>();
            var so = new SerializedObject(pause);
            so.FindProperty("panel").objectReferenceValue = overlay.gameObject;
            so.FindProperty("playerInput").objectReferenceValue = input;
            so.FindProperty("dim").objectReferenceValue = dim;
            so.FindProperty("drum").objectReferenceValue = drum.rectTransform;
            SetArray(so.FindProperty("cartridges"), cartridges);
            SetArray(so.FindProperty("labels"), labels);
            so.FindProperty("cartridgeSprite").objectReferenceValue = cartridge;
            so.FindProperty("cartridgeSelectedSprite").objectReferenceValue = Load("durdur_fisek_secili.png");
            so.FindProperty("chamberRadius").floatValue = ChamberRadiusPx * Scale;
            SerializedProperty holes = so.FindProperty("holeOffsets");
            holes.arraySize = HoleOffsetsPx.Length;
            for (int i = 0; i < HoleOffsetsPx.Length; i++)
                holes.GetArrayElementAtIndex(i).vector2Value = HoleOffsetsPx[i];
            so.FindProperty("drumPixelScale").floatValue = Scale;
            so.FindProperty("labelRadius").floatValue = LabelRadius;
            so.FindProperty("banner").objectReferenceValue = wave;
            so.FindProperty("bannerLabel").objectReferenceValue = bannerLabel;
            so.FindProperty("smoke").objectReferenceValue = smoke;
            SerializedProperty art = so.FindProperty("labelArt");
            art.arraySize = LabelKeys.Length;
            for (int i = 0; i < LabelKeys.Length; i++)
            {
                SerializedProperty item = art.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("key").stringValue = LabelKeys[i];
                item.FindPropertyRelative("idle").objectReferenceValue = Load($"durdur_yazi_{LabelKeys[i]}_0.png");
                item.FindPropertyRelative("selected").objectReferenceValue = Load($"durdur_yazi_{LabelKeys[i]}_1.png");
            }
            so.FindProperty("volumeRoot").objectReferenceValue = volume.gameObject;
            SetArray(so.FindProperty("volumeSegments"), segments);
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("tickClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/tambur_tik.wav");
            so.FindProperty("cockClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/horoz_kurma.wav");
            so.FindProperty("openClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/tambur_kapanma.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
            overlay.gameObject.SetActive(false); // ESC ile açılır
        }

        /// <summary>
        /// EditorBridge için: Level 1'i açar, duraklatma menüsünü ana sayfasıyla gösterip Temp/Screenshots/PauseMenu.png'ye
        /// çizer, sahneyi kaydetmeden geri yükler.
        /// </summary>
        public static string Preview() => Capture(false, "Temp/Screenshots/PauseMenu.png");

        /// <summary>EditorBridge için: ayarlar sayfası (SES seçili, gösterge bandanada) Temp/Screenshots/PauseMenu_Ses.png'ye.</summary>
        public static string PreviewVolume() => Capture(true, "Temp/Screenshots/PauseMenu_Ses.png");

        static string Capture(bool settings, string path)
        {
            if (EditorApplication.isPlaying)
                return "Play modunda çalışmaz.";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Level_1.unity");
            PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>();
            var so = new SerializedObject(pause);
            ((GameObject)so.FindProperty("panel").objectReferenceValue).SetActive(true);
            pause.ShowSettledForPreview(settings);
            SceneCapture.Capture(path, Camera.main.transform.position);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene.path); // değişiklikleri at
            return "duraklatma menüsü görüntüsü alındı: " + path;
        }

        static Sprite Load(string file)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + file);
            if (sprite == null)
                Debug.LogWarning($"[PauseMenuBuilder] {Art + file} yok (python tools/durdur_tambur_isle.py)");
            return sprite;
        }

        static Image Sprite(string name, Transform parent, Sprite sprite, Vector2 position, float scale = Scale)
        {
            RectTransform rect = UiKit.Rect(name, parent, new Vector2(0.5f, 0.5f), position, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.sprite = sprite;
            if (sprite != null)
                rect.sizeDelta = sprite.rect.size * scale;
            else
                image.enabled = false;
            return image;
        }

        static void SetArray(SerializedProperty prop, Object[] values)
        {
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
