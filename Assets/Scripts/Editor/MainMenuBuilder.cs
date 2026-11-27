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
    /// Giriş ekranını kurar: Gemini çizimi çöl arka planı (ekranı kaplar); sol altta yön tabelası menüsü
    /// (SignpostMenu: direğe çakılı ok tahtaları), ekranın tepesinden zincirle sarkan, sallanan başlık tabelası;
    /// sağda ince bir direğe yaslanıp puro tüttüren Gila (127 kare, 12 fps, ~10 sn döngü), direğin dibinde kum tümseği ve
    /// tepesinde akbaba. Sol grup ekranın soluna, Gila sağına yapışır (ScreenEdgeAnchor), böylece farklı ekran
    /// oranlarında yerleşim bozulmaz. En altta seçili seçeneğin açıklaması ve ses göstergesi, en üstte toplam altın.
    /// LevelBuilder çağırır.
    /// </summary>
    public static class MainMenuBuilder
    {
        internal static void Build(TestSceneBuilder.SceneContext ctx)
        {
            var scene = TestSceneBuilder.NewSceneWithLight();
            Camera cam = TestSceneBuilder.CreateCamera(CameraCenter, null, out _);

            // Yerleşim 640x360 referans ekranın piksel koordinatlarıyla (sol üst 0,0).
            SpriteRenderer bg = Place(MenuArt + "menu_arka_plan.png", "ArkaPlan", null, new Vector2(320f, 180f), -100);
            if (bg != null)
                bg.gameObject.AddComponent<ScreenEdgeAnchor>().Configure(ScreenEdgeAnchor.Edge.Center, 0f, true, cam);

            BuildGila(cam);
            var audioGo = new GameObject("MenuSesi");
            var audio = audioGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            SignpostMenu signpost = BuildSignpost(cam, audio);
            BuildTitleSign(cam);
            // yön tabelası ekranın soluna yapışır (640x360 yerleşimiyle kurulduktan sonra)
            signpost.gameObject.AddComponent<ScreenEdgeAnchor>().Configure(ScreenEdgeAnchor.Edge.Left, PoleCenterXPx / PixelsPerUnit, false, cam);

            Canvas canvas = UiKit.CreateCanvas("Menu", cam, 100);
            var main = new GameObject("AnaMenu", typeof(RectTransform)).GetComponent<RectTransform>();
            main.SetParent(canvas.transform, false);
            main.sizeDelta = new Vector2(640f, 360f);
            Text total = UiKit.Shadowed(UiKit.Label("ToplamAltin", main, "", TotalPos, new Vector2(220f, 18f), 13, UiKit.LightText));

            // En alt: seçili seçeneğin açıklaması, üstünde ses göstergesi (Ayarlar'da görünür).
            Text info = UiKit.Shadowed(UiKit.Label("Bilgi", main, "", InfoPos, new Vector2(300f, 22f), 15, UiKit.ButtonHighlight));
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
            so.FindProperty("signpost").objectReferenceValue = signpost;
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

        const string MenuArt = "Assets/Sprites/UI/MainMenu/";
        const string SignArt = MenuArt + "Tabela/";
        const string GilaArt = MenuArt + "Gila/";
        const int ArtScale = 2; // Gila ve yön tabelası çizimleri 2 kat büyütülür (tools/gemini_gila_puro_isle.py)

        // Üst/alt yazılar (640x360 tuvalin merkezine göre).
        static readonly Vector2 TotalPos = new Vector2(30f, 160f);
        static readonly Vector2 InfoPos = new Vector2(10f, -158f);
        static readonly Vector2 VolumePos = new Vector2(10f, -136f);
        const int VolumeSegments = 10;

        // ---- sağda direğe yaslanmış, puro tüttüren Gila; direğin tepesinde akbaba ----
        const int GilaFrames = 127;
        const float GilaFps = 12f;
        const float GilaRightInsetPx = 18f; // karenin sağ kenarı ekranın sağından bu kadar içeride
        // Her kare eşit süre: beklemeler ve duman geçişleri tools/gemini_gila_puro_isle.py KEYS'te kare olarak üretilir
        // (~10 sn). Farklı bekleme süreleri oynatmayı takılıyormuş gibi gösteriyordu.
        static readonly int[] GilaHolds = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Repeat(1, GilaFrames));
        const float GilaPostTopArt = 3f;      // tools/gemini_gila_puro_isle.py çıktısı: direğin tepesi (karenin üstünden; üstünde duman payı)
        const float GilaPostCenterArt = 63f;  // tools/gemini_gila_puro_isle.py çıktısı: direğin ortası (karenin solundan)
        const int VultureFrames = 4;
        const float VultureFps = 5f;
        static readonly int[] VultureHolds = { 14, 5, 4, 2 }; // çoğu zaman kıpırdamaz, ara ara başını eğip tüylerini kabartır
        const float VulturePostCenterArt = 13.5f; // tools/gemini_akbaba_isle.py çıktısı

        static Sprite[] Sequence(string pattern, int count, int[] holds)
        {
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < count; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(string.Format(pattern, i));
                if (sprite == null)
                    return null;
                for (int k = 0; k < holds[i]; k++)
                    list.Add(sprite);
            }
            return list.ToArray();
        }

        static void BuildGila(Camera cam)
        {
            Sprite[] frames = Sequence(GilaArt + "gila_puro_{0:000}.png", GilaFrames, GilaHolds);
            if (frames == null)
            {
                Debug.LogWarning("[DesertGecko] Gila menü kareleri yok (python tools/gemini_gila_puro_isle.py)");
                return;
            }
            var go = new GameObject("GilaPuro");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = 15;
            go.transform.localScale = Vector3.one * ArtScale;
            Vector2 art = frames[0].rect.size;                   // sanat pikseli
            Vector2 size = art * ArtScale / PixelsPerUnit;       // dünya birimi
            float bottom = CameraCenter.y - 180f / PixelsPerUnit;
            go.transform.position = new Vector3(0f, bottom + size.y / 2f, 0f);
            go.AddComponent<SpriteLoop>().Configure(frames, GilaFps);
            go.AddComponent<ScreenEdgeAnchor>().Configure(ScreenEdgeAnchor.Edge.Right, GilaRightInsetPx / PixelsPerUnit + size.x / 2f, false, cam);

            // akbaba: direğin tepesine tüner (Gila'nın çocuğu, aynı 2x ölçek; yerel birim = sanat pikseli / 32)
            Sprite[] vulture = Sequence(GilaArt + "akbaba_{0:00}.png", VultureFrames, VultureHolds);
            if (vulture == null)
            {
                Debug.LogWarning("[DesertGecko] Akbaba kareleri yok (python tools/gemini_akbaba_isle.py)");
                return;
            }
            var bird = new GameObject("Akbaba");
            bird.transform.SetParent(go.transform, false);
            var bsr = bird.AddComponent<SpriteRenderer>();
            bsr.sprite = vulture[0];
            bsr.sortingOrder = 16;
            Vector2 birdArt = vulture[0].rect.size;
            float x = (GilaPostCenterArt - art.x / 2f) - (VulturePostCenterArt - birdArt.x / 2f);
            float y = art.y / 2f - GilaPostTopArt + birdArt.y / 2f - 1f; // kapağın alt satırı direğin tepesiyle üst üste
            bird.transform.localPosition = new Vector3(x, y, 0f) / PixelsPerUnit;
            bird.AddComponent<SpriteLoop>().Configure(vulture, VultureFps, 1.3f);

            // direğin dibine zeminle kaynaşan kum tümseği (tools/gemini_tabela_isle.py, sağdaki zeminin renkleri)
            var moundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SignArt + "direk_tumsek.png");
            if (moundSprite != null)
            {
                var mound = new GameObject("DirekTumsek").AddComponent<SpriteRenderer>();
                mound.transform.SetParent(go.transform, false);
                mound.sprite = moundSprite;
                mound.sortingOrder = 17;
                Vector2 m = moundSprite.rect.size;
                mound.transform.localPosition = new Vector3(GilaPostCenterArt - art.x / 2f, -art.y / 2f + m.y / 2f - 2f, 0f) / PixelsPerUnit;
            }
        }

        // ---- başlık tabelası: ekranın tepesinden zincirle sarkar ----
        const float TitleCenterXPx = 124f;   // tabelanın ortası ekranın solundan
        const float TitleChainTopPx = 4f;    // eski kısa zincirin tepesi; zincir bunun ChainExtendPx üstünden başlar
        const float ChainExtendPx = 36f;     // tools/menu_tabela_zincir.py EXTEND

        static void BuildTitleSign(Camera cam)
        {
            var sign = new GameObject("BaslikTabelasi");
            // asılma noktası zincirin tepesi, ekranın biraz üstünde: tabela oradan sarkar ve sallanır
            sign.transform.position = ScreenToWorld(new Vector2(TitleCenterXPx, TitleChainTopPx - ChainExtendPx));
            sign.AddComponent<SignSway>();
            SpriteRenderer signArt = Place(MenuArt + "menu_tabela_asili.png", "TabelaGorsel", sign.transform, Vector2.zero, 30);
            if (signArt != null)
                signArt.transform.localPosition = new Vector3(0f, -signArt.sprite.bounds.extents.y, 0f);
            sign.AddComponent<ScreenEdgeAnchor>().Configure(ScreenEdgeAnchor.Edge.Left, TitleCenterXPx / PixelsPerUnit, false, cam);
        }

        // ---- sol altta yön tabelası ----
        const int Planks = 5;                    // en kalabalık sayfa: 4 level + Geri
        const float PoleCenterXPx = 62f;         // direğin ortası ekranın solundan
        const float PoleBottomPx = 364f;         // tümseğin altı biraz ekran dışında
        const float FirstPlankTopPx = 166f;      // ilk tahtanın üst kenarı (başlık tabelasının altı)
        const float PlankStepPx = 38f;           // tahtalar arası (tahta 17 sanat pikseli = 34 ekran pikseli)
        const float PlankNailPx = 8f;            // tahtanın sol ucu direğin ortasından bu kadar solda (çiviler direğin üstünde)
        const float LabelCenterArt = 33f;        // yazının ortası tahtanın solundan (sanat pikseli; gemini_tabela_isle.py TEXT_CENTER_X)

        static SignpostMenu BuildSignpost(Camera cam, AudioSource audio)
        {
            var root = new GameObject("YonTabelasi").transform;
            root.position = ScreenToWorld(new Vector2(PoleCenterXPx, 0f));
            root.position = new Vector3(root.position.x, 0f, 0f);

            SpriteRenderer pole = Place(SignArt + "tabela_direk.png", "Direk", root, Vector2.zero, 20);
            if (pole != null)
            {
                pole.transform.localScale = Vector3.one * ArtScale;
                float h = pole.sprite.bounds.size.y * ArtScale;
                pole.transform.position = new Vector3(root.position.x, ScreenToWorld(new Vector2(0f, PoleBottomPx)).y + h / 2f, 0f);
            }

            var plankSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SignArt + "tabela_tahta.png");
            var planks = new Transform[Planks];
            var boards = new SpriteRenderer[Planks];
            var labels = new SpriteRenderer[Planks];
            float plankH = plankSprite != null ? plankSprite.bounds.size.y * ArtScale : 34f / PixelsPerUnit;
            for (int i = 0; i < Planks; i++)
            {
                // kök tahtanın sol ucunda (direğe çakılı yer): dönüş ölçeği buradan uygulanır, tahta direğin etrafında döner
                var plank = new GameObject($"Tahta_{i}").transform;
                plank.SetParent(root, false);
                float centerY = FirstPlankTopPx + i * PlankStepPx + plankH * PixelsPerUnit / 2f;
                plank.position = ScreenToWorld(new Vector2(PoleCenterXPx - PlankNailPx, centerY));
                planks[i] = plank;
                if (plankSprite != null)
                {
                    boards[i] = new GameObject("Gorsel").AddComponent<SpriteRenderer>();
                    boards[i].transform.SetParent(plank, false);
                    boards[i].sprite = plankSprite;
                    boards[i].sortingOrder = 21 + 2 * i;
                    boards[i].transform.localScale = Vector3.one * ArtScale;
                    boards[i].transform.localPosition = new Vector3(plankSprite.bounds.extents.x * ArtScale, 0f, 0f);
                }
                labels[i] = new GameObject("Yazi").AddComponent<SpriteRenderer>();
                labels[i].transform.SetParent(plank, false);
                labels[i].sortingOrder = 22 + 2 * i;
                labels[i].transform.localScale = Vector3.one * ArtScale;
                labels[i].transform.localPosition = new Vector3(LabelCenterArt * ArtScale / PixelsPerUnit, 0f, 0f);
            }

            var menu = root.gameObject.AddComponent<SignpostMenu>();
            var so = new SerializedObject(menu);
            SetArray(so.FindProperty("planks"), planks);
            SetArray(so.FindProperty("boards"), boards);
            SetArray(so.FindProperty("labels"), labels);
            SerializedProperty art = so.FindProperty("labelArt");
            art.arraySize = SignLabels.Length;
            for (int i = 0; i < SignLabels.Length; i++)
            {
                SerializedProperty item = art.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("text").stringValue = SignLabels[i].text;
                item.FindPropertyRelative("idle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{SignArt}tabela_yazi_{SignLabels[i].file}_0.png");
                item.FindPropertyRelative("selected").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{SignArt}tabela_yazi_{SignLabels[i].file}_1.png");
            }
            so.FindProperty("selectedSlide").floatValue = 8f / PixelsPerUnit;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("tickClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/tambur_tik.wav");
            so.FindProperty("cockClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/horoz_kurma.wav");
            so.FindProperty("shotClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/silah.wav");
            so.ApplyModifiedPropertiesWithoutUndo();

            // Sahnede (ve ekran görüntüsünde) menü oyundaki ilk haliyle dursun; oyunda MainMenu yazıları yeniden kurar.
            string[] names = { "Başla", "Level Seç", "Ayarlar", "Çıkış" };
            for (int i = 0; i < Planks; i++)
                menu.SetText(i, i < names.Length ? names[i] : null);

            return menu;
        }

        // Tahta yazıları: oyundaki metin -> tabela_yazi_<dosya>_0/1.png (tools/gemini_tabela_isle.py LABELS)
        static readonly (string text, string file)[] SignLabels =
        {
            ("Başla", "basla"), ("Devam Et", "devam"), ("Level Seç", "level"), ("Ayarlar", "ayarlar"), ("Çıkış", "cikis"),
            ("Level 1", "level1"), ("Level 2", "level2"), ("Level 3", "level3"), ("Level 4", "level4"), ("Geri", "geri"),
            ("Ses", "ses"), ("Tam Ekran", "tamekran"), ("Sıfırla", "sifirla"),
        };

        // Oyunu başlatınca sahne geçişi: soldan sağa savrulan yarı saydam toz topakları (DustTransition), en üstte
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
                Debug.LogWarning($"[DesertGecko] Menü görseli bulunamadı: {path}");
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
