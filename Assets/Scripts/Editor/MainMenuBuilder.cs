using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Giriş ekranını kurar: Gemini çizimi çöl arka planı, sol üstte sallanan başlık tabelası, sağ altta
    /// Gila elinde revolver. Bütün menü (ana sayfa, leveller, ayarlar, ses) revolverin tamburunda döner;
    /// en üstte seçili seçeneğin açıklaması ve ses göstergesi durur. LevelBuilder çağırır.
    /// </summary>
    public static class MainMenuBuilder
    {
        internal static void Build(TestSceneBuilder.SceneContext ctx)
        {
            var scene = TestSceneBuilder.NewSceneWithLight();
            Camera cam = TestSceneBuilder.CreateCamera(CameraCenter, null, out _);

            // Gemini'den gelen menü görselleri (tools/gemini_menu_isle.py): tam ekran çöl, sol üstte sallanan
            // tabela, sağ altta Gila'nın tuttuğu revolver. Yerleşim 640x360 referans ekranın piksel koordinatlarıyla.
            Place(MenuArt + "menu_arka_plan.png", "ArkaPlan", null, new Vector2(320f, 180f), -100);
            var sign = new GameObject("Tabela");
            sign.transform.position = ScreenToWorld(new Vector2(124f, 4f)); // zincirlerin tepesi: asılma noktası
            sign.AddComponent<SignSway>();
            SpriteRenderer signArt = Place(MenuArt + "menu_tabela.png", "TabelaGorsel", sign.transform, Vector2.zero, 10);
            if (signArt != null)
                signArt.transform.localPosition = new Vector3(0f, -signArt.sprite.bounds.extents.y, 0f);
            RevolverParts parts = BuildRevolver();

            Canvas canvas = UiKit.CreateCanvas("Menu", cam, 100);
            Transform root = canvas.transform;

            // Ana ekran: seçenekler tamburun yan yüzüne kazınmış, tamburla birlikte döner (RevolverMenu).
            var main = new GameObject("AnaMenu", typeof(RectTransform)).GetComponent<RectTransform>();
            main.SetParent(root, false);
            main.sizeDelta = new Vector2(640f, 360f);
            Text total = UiKit.Label("ToplamKristal", main, "", new Vector2(-200f, -160f), new Vector2(200f, 18f), 13, UiKit.LightText);
            UiKit.Shadowed(total);
            var audio = main.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            var revolver = main.gameObject.AddComponent<RevolverMenu>();
            var rso = new SerializedObject(revolver);
            SetArray(rso.FindProperty("rims"), parts.Rims);
            rso.FindProperty("ring").objectReferenceValue = parts.Ring;
            rso.FindProperty("ringRadius").vector2Value = RingRadiusPx / PixelsPerUnit;
            SetArray(rso.FindProperty("bands"), parts.Bands);
            SetArray(rso.FindProperty("flutes"), parts.Flutes);
            rso.FindProperty("faceCenter").vector2Value = parts.Local(FaceCenterPx);
            rso.FindProperty("faceRadius").vector2Value = FaceRadiusPx / PixelsPerUnit;
            rso.FindProperty("lineAngle").floatValue = LineAngle;
            rso.FindProperty("drumLeftX").floatValue = parts.Local(new Vector2(DrumLeftXPx, 0f)).x;
            rso.FindProperty("labelX").floatValue = parts.Local(new Vector2(LabelCenterXPx, 0f)).x;
            SerializedProperty art = rso.FindProperty("labelArt");
            art.arraySize = LabelFiles.Length;
            for (int i = 0; i < LabelFiles.Length; i++)
            {
                SerializedProperty item = art.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("text").stringValue = LabelFiles[i].text;
                item.FindPropertyRelative("idle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{MenuArt}menu_yazi_{LabelFiles[i].file}_0.png");
                item.FindPropertyRelative("selected").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{MenuArt}menu_yazi_{LabelFiles[i].file}_1.png");
            }
            rso.FindProperty("gun").objectReferenceValue = parts.Gun;
            SetArray(rso.FindProperty("menuGunParts"), parts.MenuGun);
            SetArray(rso.FindProperty("realGunParts"), parts.RealGun);
            SetArray(rso.FindProperty("hazePuffs"), parts.Haze);
            rso.FindProperty("fluteDrop").floatValue = FluteDropPx / PixelsPerUnit;
            rso.FindProperty("closeClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/tambur_kapanma.wav");
            rso.FindProperty("muzzleFlash").objectReferenceValue = parts.Flash;
            rso.FindProperty("audioSource").objectReferenceValue = audio;
            rso.FindProperty("tickClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/tambur_tik.wav");
            rso.FindProperty("cockClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/horoz_kurma.wav");
            rso.FindProperty("shotClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/silah.wav");
            rso.ApplyModifiedPropertiesWithoutUndo();
            // Sahnede (ve ekran görüntüsünde) menü oyundaki ilk haliyle dursun; oyunda MainMenu metinleri yeniden yazar.
            string[] names = { "Başla", "Level Seç", "Ayarlar", "Çıkış" };
            for (int i = 0; i < names.Length; i++)
                revolver.SetText(i, names[i]);
            revolver.Layout();

            // En üst: seçili seçeneğin açıklaması ve ses göstergesi (Ayarlar'da görünür). Bütün menü tamburda.
            Text info = UiKit.Shadowed(UiKit.Label("Bilgi", main, "", InfoPos, new Vector2(380f, 22f), 16, UiKit.ButtonHighlight));
            info.fontStyle = FontStyle.Bold;
            var volumeRoot = new GameObject("SesGostergesi", typeof(RectTransform)).GetComponent<RectTransform>();
            volumeRoot.SetParent(main, false);
            volumeRoot.anchoredPosition = VolumePos;
            UiKit.Shadowed(UiKit.Label("SesYazi", volumeRoot, "SES", new Vector2(-88f, 0f), new Vector2(40f, 18f), 14, UiKit.LightText));
            var segments = new Image[VolumeSegments];
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = new GameObject($"Dilim_{i}", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                segments[i].rectTransform.SetParent(volumeRoot, false);
                segments[i].rectTransform.sizeDelta = new Vector2(10f, 8f + i * 1.2f); // sağa doğru yükselen çubuklar
                segments[i].rectTransform.anchoredPosition = new Vector2(-60f + i * 13f, (8f + i * 1.2f) / 2f - 8f);
                segments[i].raycastTarget = false;
            }
            volumeRoot.gameObject.SetActive(false);

            var menu = canvas.gameObject.AddComponent<MainMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("revolver").objectReferenceValue = revolver;
            so.FindProperty("dust").objectReferenceValue = BuildDustTransition();
            so.FindProperty("totalCrystalsLabel").objectReferenceValue = total;
            so.FindProperty("infoLabel").objectReferenceValue = info;
            so.FindProperty("volumeRoot").objectReferenceValue = volumeRoot.gameObject;
            SetArray(so.FindProperty("volumeSegments"), segments);
            so.ApplyModifiedPropertiesWithoutUndo();

            UiKit.CreateEventSystem();
            EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{LevelCatalog.MainMenuScene}.unity");
            Debug.Log("[DesertGecko] Ana menü kuruldu.");
        }

        /// <summary>
        /// EditorBridge için: ana menü sahnesinde tamburu birkaç ara açıda durdurup görüntüler
        /// (Temp/Screenshots/Tambur_*.png). Sahneyi kaydetmez.
        /// </summary>
        public static string CaptureRevolverSteps()
        {
            string previous = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene($"Assets/Scenes/{LevelCatalog.MainMenuScene}.unity");
            var menu = Object.FindAnyObjectByType<RevolverMenu>();
            string[] names = { "Başla", "Level Seç", "Ayarlar", "Çıkış" };
            for (int i = 0; i < names.Length; i++)
                menu.SetText(i, names[i]);
            var shown = typeof(RevolverMenu).GetField("shown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (float step in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                shown.SetValue(menu, step);
                menu.Layout();
                SceneCapture.Capture($"Temp/Screenshots/Tambur_{step * 100f:0}.png", new Vector3(CameraCenter.x, CameraCenter.y, -10f));
            }
            // pustan sonra beliren gerçekçi silah (tamburu yuvasında) ve pusun en yoğun anı
            var show = typeof(RevolverMenu).GetMethod("ShowGun", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            show.Invoke(menu, new object[] { false });
            SceneCapture.Capture("Temp/Screenshots/Tambur_kapali.png", new Vector3(CameraCenter.x, CameraCenter.y, -10f));
            var puffs = (SpriteRenderer[])typeof(RevolverMenu).GetField("hazePuffs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(menu);
            foreach (SpriteRenderer puff in puffs)
                puff.enabled = true;
            SceneCapture.Capture("Temp/Screenshots/Tambur_pus.png", new Vector3(CameraCenter.x, CameraCenter.y, -10f));
            if (!string.IsNullOrEmpty(previous))
                EditorSceneManager.OpenScene(previous);
            return "tamam";
        }

        const string MenuArt = "Assets/Sprites/UI/MainMenu/";
        // Ateşten sonraki sahne geçişi: soldan sağa savrulan yarı saydam toz topakları (DustTransition), en üstte
        // ayrı bir kanvasta; oynayınca sahneler arası taşınır.
        static DustTransition BuildDustTransition()
        {
            var go = new GameObject("TozGecisi", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            RectTransform Stretch(string name)
            {
                var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                r.SetParent(go.transform, false);
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
                return r;
            }
            var veil = Stretch("KumPerdesi").gameObject.AddComponent<Image>();
            veil.color = new Color32(0xE2, 0xCD, 0xAA, 0);
            veil.raycastTarget = false;
            veil.enabled = false;
            RectTransform area = Stretch("Topaklar");
            var sound = go.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/savurma.wav");
            var dust = go.AddComponent<DustTransition>();
            var so = new SerializedObject(dust);
            so.FindProperty("area").objectReferenceValue = area;
            so.FindProperty("puff").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(MenuArt + "menu_toz_bulut.png");
            so.FindProperty("veil").objectReferenceValue = veil;
            so.FindProperty("sound").objectReferenceValue = sound;
            so.ApplyModifiedPropertiesWithoutUndo();
            return dust;
        }

        // Üst bilgi ve ses göstergesi (640x360 tuvalin merkezine göre); tabelanın sağında, ekranın üstünde.
        static readonly Vector2 InfoPos = new Vector2(110f, 150f);
        static readonly Vector2 VolumePos = new Vector2(110f, 124f);
        const int VolumeSegments = 10;

        // Revolver yerleşimi. Silah görseli (380x217) ekranda sol üst köşesi (270,149) olacak şekilde durur.
        // Sprite içi ölçüler tools/menu_tambur_ayir.py'deki RING_CENTER / RING_RADIUS ile aynı olmalı.
        static readonly Vector2 GunTopLeftScreen = new Vector2(270f, 149f);
        static readonly Vector2 GunSize = new Vector2(380f, 217f);
        static readonly Vector2 WristPx = new Vector2(340f, 181.5f);    // geri tepme bu noktanın etrafında döner (bilek)
        static readonly Vector2 RingCenterPx = new Vector2(146.4f, 85.75f);
        static readonly Vector2 RingRadiusPx = new Vector2(19f, 28.8f);
        // Tamburun yan yüzü (sprite pikseli). Oluk ve yazı çizgileri arka yüzün kenarından, fişek yuvalarıyla hizalı
        // başlar ve orijinal çizimdeki oluklar gibi paralel, sağa doğru 13 derece inerek sola uzanır.
        static readonly Vector2 FaceCenterPx = new Vector2(147f, 83f);   // tools/menu_tambur_ayir.py FACE_CENTER
        static readonly Vector2 FaceRadiusPx = new Vector2(36f, 43f);
        const float LineAngle = -13f;                                     // menu_tambur_ayir.py TEXT_SLOPE_DEG ile aynı
        const float DrumLeftXPx = 60f;
        const float LabelCenterXPx = 88f;
        const float FluteDropPx = 1.2f;                                    // oluk çizgileri yazılara göre biraz aşağıda
        // Ateş geçişi: pus çöker, menü silahı yerine tamburu çerçeveye oturmuş gerçekçi silah ve Gila'nın eli gelir
        // (tools/menu_gercekci_silah.py; ikisi de menü silahıyla aynı tuval ve hizada). Ateşi gerçekçi silah eder.
        static readonly Vector2 MuzzlePx = new Vector2(116f, 34.5f);       // gerçekçi silahın namlu ucu
        static readonly Vector2[] HazePx =                                  // pus bulutlarının merkezleri (sprite pikseli)
        {
            new Vector2(150f, 70f), new Vector2(90f, 40f), new Vector2(230f, 60f), new Vector2(120f, 120f),
            new Vector2(210f, 130f), new Vector2(270f, 110f), new Vector2(40f, 30f),
        };
        static readonly Vector2 FlashMuzzlePx = new Vector2(52f, 41f);     // alev görselinde namluya değen nokta
        const float FlashAngle = -12f;                                     // namlu hafif yukarı bakıyor

        // Tambur yazıları: oyundaki metin -> menu_yazi_<dosya>_0/1.png (tools/menu_tambur_ayir.py LABELS)
        static readonly (string text, string file)[] LabelFiles =
        {
            ("Başla", "basla"), ("Devam Et", "devam"), ("Level Seç", "level"), ("Ayarlar", "ayarlar"), ("Çıkış", "cikis"),
            ("Level 1", "level1"), ("Level 2", "level2"), ("Level 3", "level3"), ("Level 4", "level4"), ("Geri", "geri"),
            ("Ses", "ses"), ("Tam Ekran", "tamekran"), ("Sıfırla", "sifirla"),
        };

        struct RevolverParts
        {
            public Transform Gun, Ring;
            public Transform[] Rims;
            public SpriteRenderer[] Bands, Flutes;
            public GameObject Flash;
            public GameObject[] MenuGun, RealGun;
            public SpriteRenderer[] Haze;

            // sprite pikseli (sol üstten) -> silahın yerel birimi (bilek = 0,0)
            public Vector2 Local(Vector2 spritePx) => new Vector2((spritePx.x - WristPx.x) / PixelsPerUnit, (WristPx.y - spritePx.y) / PixelsPerUnit);
        }

        // Silah katmanları: tamburun yan yüzü (16) ve üstündeki maskeli yazılar, oluklar (17) gövdenin (19) arkasında;
        // tamburun arka yüzü (20) ve fişekler (21) gövdenin önünde; en önde horoz ve tetik parmağı (22); namlu alevi (23);
        // pus (25). Gerçekçi silah (19) ve el (22) ateş geçişine kadar gizli.
        static RevolverParts BuildRevolver()
        {
            var parts = new RevolverParts();
            var gun = new GameObject("Revolver").transform;
            gun.position = ScreenToWorld(GunTopLeftScreen + WristPx);
            parts.Gun = gun;
            Vector3 center = parts.Local(GunSize / 2f);

            SpriteRenderer body = Place(MenuArt + "menu_silah_bos.png", "Govde", gun, Vector2.zero, 19);
            if (body != null)
                body.transform.localPosition = center;
            // tambur ve üstündeki her şey (fişekler, yazılar, oluklar, maske) bunun çocuğu
            var cylinder = new GameObject("Tambur").transform;
            cylinder.SetParent(gun, false);
            SpriteRenderer cylArt = Place(MenuArt + "menu_tambur.png", "TamburYanYuz", cylinder, Vector2.zero, 16);
            if (cylArt != null)
                cylArt.transform.localPosition = center;
            SpriteRenderer faceArt = Place(MenuArt + "menu_tambur_yuz.png", "TamburArkaYuz", cylinder, Vector2.zero, 20);
            if (faceArt != null)
                faceArt.transform.localPosition = center;
            SpriteRenderer front = Place(MenuArt + "menu_silah_on.png", "HorozVeParmak", gun, Vector2.zero, 22);
            if (front != null)
                front.transform.localPosition = center;

            var maskSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MenuArt + "menu_tambur_maske.png");
            if (maskSprite != null)
            {
                var mask = new GameObject("TamburMaskesi").AddComponent<SpriteMask>();
                mask.transform.SetParent(cylinder, false);
                mask.transform.localPosition = center;
                mask.sprite = maskSprite;
            }
            parts.Bands = new SpriteRenderer[RevolverMenu.Chambers];
            parts.Flutes = new SpriteRenderer[RevolverMenu.Chambers * 2];
            for (int i = 0; i < RevolverMenu.Chambers; i++)
            {
                parts.Bands[i] = new GameObject($"Yazi_{i}").AddComponent<SpriteRenderer>();
                parts.Bands[i].transform.SetParent(cylinder, false);
                parts.Bands[i].sortingOrder = 17;
                parts.Bands[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
            for (int i = 0; i < parts.Flutes.Length; i++)
            {
                parts.Flutes[i] = Place(MenuArt + "menu_oluk.png", $"Oluk_{i}", cylinder, Vector2.zero, 17);
                if (parts.Flutes[i] != null)
                    parts.Flutes[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }

            parts.Ring = new GameObject("FisekHalkasi").transform;
            parts.Ring.SetParent(cylinder, false);
            parts.Ring.localPosition = parts.Local(RingCenterPx);
            parts.Rims = new Transform[RevolverMenu.Chambers];
            for (int i = 0; i < parts.Rims.Length; i++)
            {
                SpriteRenderer rim = Place(MenuArt + "menu_fisek.png", $"Fisek_{i}", parts.Ring, Vector2.zero, 21);
                parts.Rims[i] = rim != null ? rim.transform : null;
            }

            // ateş geçişinde beliren gerçekçi silah ve el; başta gizli
            SpriteRenderer real = Place(MenuArt + "menu_gercekci_silah.png", "GercekciSilah", gun, Vector2.zero, 19);
            SpriteRenderer hand = Place(MenuArt + "menu_el.png", "El", gun, Vector2.zero, 22);
            parts.MenuGun = new[] { body != null ? body.gameObject : null, cylinder.gameObject, front != null ? front.gameObject : null };
            parts.RealGun = new[] { real != null ? real.gameObject : null, hand != null ? hand.gameObject : null };
            foreach (GameObject go in parts.RealGun)
                if (go != null)
                {
                    go.transform.localPosition = center;
                    go.SetActive(false);
                }
            parts.Haze = new SpriteRenderer[HazePx.Length];
            for (int i = 0; i < HazePx.Length; i++)
            {
                parts.Haze[i] = Place(MenuArt + "menu_pus.png", $"Pus_{i}", gun, Vector2.zero, 25);
                if (parts.Haze[i] == null)
                    continue;
                parts.Haze[i].transform.localPosition = parts.Local(HazePx[i]);
                parts.Haze[i].transform.localScale = Vector3.one * (1.3f + 0.15f * (i % 3));
                parts.Haze[i].enabled = false;
            }

            SpriteRenderer flash = Place(MenuArt + "menu_namlu_alevi.png", "NamluAlevi", gun, Vector2.zero, 23);
            if (flash != null)
            {
                // alevin namluya değen noktası namlunun ucuna gelsin
                Vector2 pivotOffset = new Vector2(flash.sprite.rect.width / 2f - FlashMuzzlePx.x, FlashMuzzlePx.y - flash.sprite.rect.height / 2f) / PixelsPerUnit;
                Quaternion rot = Quaternion.Euler(0f, 0f, FlashAngle);
                flash.transform.localRotation = rot;
                flash.transform.localPosition = (Vector3)parts.Local(MuzzlePx) + rot * (Vector3)pivotOffset;
                parts.Flash = flash.gameObject;
                parts.Flash.SetActive(false); // sadece ateş anında görünür
            }
            return parts;
        }

        static readonly Vector2 CameraCenter = new Vector2(0f, 1.5f);
        const float PixelsPerUnit = 32f; // SpriteImportRules ile aynı

        // 640x360 referans ekranda sol üstten piksel konumu -> dünya konumu (kamera CameraCenter'da).
        static Vector3 ScreenToWorld(Vector2 px)
        {
            return new Vector3(CameraCenter.x + (px.x - 320f) / PixelsPerUnit, CameraCenter.y + (180f - px.y) / PixelsPerUnit, 0f);
        }

        // Görseli sahneye koyar: parent yoksa px ekran konumu (görselin merkezi), varsa yerel konum.
        static SpriteRenderer Place(string path, string name, Transform parent, Vector2 px, int order)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[DesertGecko] Menü görseli bulunamadı: {path} (python tools/gemini_menu_isle.py)");
                return null;
            }
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            else
                go.transform.position = ScreenToWorld(px);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static void SetArray(SerializedProperty prop, Object[] values)
        {
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
