using System;
using System.Collections;
using DesertGecko.Core;
using DesertGecko.Levels;
using DesertGecko.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DesertGecko.UI
{
    /// <summary>
    /// ESC (gamepad: Start) ile açılan duraklatma menüsü: ekranın solunda bir revolver tamburu. Seçenekler doğrudan
    /// hazneler: dolu hazne (fişekli) bir seçenek, boş hazne atlanır. Saat 3'teki hazne seçilidir: fişeği parlar ve
    /// yanından bordo bir bandana "tık" diye sağa açılıp rüzgârda dalgalanır, seçeneğin adı üstünde küçük bir duman
    /// bulutunun içinden belirir. Diğer seçeneklerin adları tamburun çevresinde küçük ve soluk durur, tamburla döner.
    /// Sağ/sol, yukarı/aşağı, fare tekerleği tamburu bir hazne çevirir (tık sesi), onay seçeneği çalıştırır. Fareyle
    /// bir hazneye tıklamak onu saat 3'e çevirir, bandanaya tıklamak onaylar. Ayarlar (ses, tam ekran, geri) aynı tamburda.
    /// Duraklayınca zaman durur ve oyuncu girdisi kapanır; menü ölçeksiz zamanla çalışır.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public const int Chambers = 6;

        [Serializable]
        public class LabelArt
        {
            public string key;
            public Sprite idle;
            public Sprite selected;
        }

        [SerializeField] GameObject panel;
        [SerializeField] PlayerInputReader playerInput;
        [Tooltip("Yarı saydam karartma (açılışta belirir)")]
        [SerializeField] Image dim;
        [SerializeField] float dimAlpha = 0.6f;

        [Header("Tambur")]
        [Tooltip("Dönen tambur görseli (merkezi tamburun göbeği)")]
        [SerializeField] RectTransform drum;
        [Tooltip("Hazne başına fişek görseli; 0 = ilk seçenek, saat yönünde")]
        [SerializeField] Image[] cartridges = new Image[Chambers];
        [Tooltip("Hazne başına yazı; tamburun dışında döner, dik kalır")]
        [SerializeField] Image[] labels = new Image[Chambers];
        [SerializeField] Sprite cartridgeSprite;
        [SerializeField] Sprite cartridgeSelectedSprite;
        [Tooltip("Hazne halkasının yarıçapı (kanvas birimi)")]
        [SerializeField] float chamberRadius = 54f;
        [Tooltip("Görseldeki hazne merkezleri, tambur görselinin ortasına göre (sprite pikseli, y yukarı), saat 12'den " +
                 "saat yönünde. Gemini'nin çizdiği hazneler tam düzgün altıgen değil; boşsa chamberRadius çemberi kullanılır.")]
        [SerializeField] Vector2[] holeOffsets = Array.Empty<Vector2>();
        [Tooltip("Tambur görselinin ekrandaki piksel büyütmesi (holeOffsets'i kanvas birimine çevirir)")]
        [SerializeField] float drumPixelScale = 2f;
        [Tooltip("Yazıların merkeze uzaklığı (kanvas birimi); üstteki yazı tabelaya oturur")]
        [SerializeField] float labelRadius = 128f;
        [SerializeField] LabelArt[] labelArt = Array.Empty<LabelArt>();
        [Tooltip("Seçili haznenin açısı: saat yönünde derece, 0 = üst, 90 = saat 3 (bandananın çıktığı yer)")]
        [SerializeField] float selectedAngle = 90f;
        [Tooltip("Seçili olmayan yazıların saydamlığı (küçük ve soluk dururlar)")]
        [SerializeField] float idleLabelAlpha = 0.55f;

        [Header("Bandana (seçili seçenek)")]
        [Tooltip("Seçili haznenin yanından açılan, dalgalanan bordo bandana (sol ucu tamburun arkasında)")]
        [SerializeField] UiWave banner;
        [Tooltip("Bandananın üstündeki seçili seçenek yazısı")]
        [SerializeField] Image bannerLabel;
        [Tooltip("Yazı belirirken çıkan duman")]
        [SerializeField] Image smoke;
        [Tooltip("Yazının bandana üzerindeki yeri (0 sol uç, 1 sağ uç)")]
        [SerializeField] float bannerLabelAt = 0.55f;
        [Tooltip("Ses seçiliyken yazının ve göstergenin bandana üzerindeki yeri")]
        [SerializeField] float volumeLabelAt = 0.3f;
        [SerializeField] float volumeAt = 0.6f;
        [Tooltip("Bandananın açılma süresi (saniye)")]
        [SerializeField] float unrollTime = 0.5f;
        [Tooltip("Bandananın tamburun arkasına sarılma süresi (saniye)")]
        [SerializeField] float closeTime = 0.24f;
        [SerializeField] float smokeTime = 0.9f;

        [Header("Ses göstergesi (ayarlar sayfası)")]
        [SerializeField] GameObject volumeRoot;
        [SerializeField] Image[] volumeSegments = Array.Empty<Image>();
        [SerializeField] Color volumeOn = new Color32(0xF4, 0xD3, 0x5E, 0xFF);
        [SerializeField] Color volumeOff = new Color32(0x3C, 0x2A, 0x22, 0xFF);

        [Header("Ses")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip tickClip;
        [SerializeField] AudioClip cockClip;
        [SerializeField] AudioClip openClip;

        [Header("Zamanlama")]
        [Tooltip("Bir haznelik dönüş süresi (saniye)")]
        [SerializeField] float stepTime = 0.3f;
        [SerializeField] float openTime = 0.75f;
        [Tooltip("Tamburun yerine otururken geçip geri gelme miktarı (ağır tambur: küçük)")]
        [SerializeField] float settleOvershoot = 0.9f;

        public bool IsPaused { get; private set; }

        readonly Action[] actions = new Action[Chambers];
        readonly LabelArt[] chamberArt = new LabelArt[Chambers];
        Action back;
        Action<int> adjust;
        int selected, lastTick;
        float shown, from, to, moveStart = -1f, openStart = -1f;
        bool busy;
        float unroll, smokeStart;
        bool smoking, puffed;
        int bannerFor = -1;
        InputAction pauseAction, prev, next, submit, cancel;

        void Awake()
        {
            pauseAction = MakeAction("<Keyboard>/escape", "<Gamepad>/start");
            prev = MakeAction("<Keyboard>/leftArrow", "<Keyboard>/upArrow", "<Keyboard>/a", "<Keyboard>/w",
                "<Gamepad>/dpad/left", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/left", "<Gamepad>/leftStick/up");
            next = MakeAction("<Keyboard>/rightArrow", "<Keyboard>/downArrow", "<Keyboard>/d", "<Keyboard>/s",
                "<Gamepad>/dpad/right", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/right", "<Gamepad>/leftStick/down");
            submit = MakeAction("<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            cancel = MakeAction("<Keyboard>/backspace", "<Gamepad>/buttonEast");
            panel.SetActive(false);
        }

        static InputAction MakeAction(params string[] bindings)
        {
            var action = new InputAction(type: InputActionType.Button);
            foreach (string b in bindings)
                action.AddBinding(b);
            return action;
        }

        void OnEnable()
        {
            pauseAction.Enable();
            prev.Enable();
            next.Enable();
            submit.Enable();
            cancel.Enable();
        }

        void OnDisable()
        {
            pauseAction.Disable();
            prev.Disable();
            next.Disable();
            submit.Disable();
            cancel.Disable();
        }

        void OnDestroy()
        {
            pauseAction.Dispose();
            prev.Dispose();
            next.Dispose();
            submit.Dispose();
            cancel.Dispose();
            if (IsPaused)
                Time.timeScale = 1f; // sahne duraklatılmışken kapanırsa zaman donuk kalmasın
        }

        void Update()
        {
            if (pauseAction.WasPressedThisFrame())
            {
                if (!IsPaused)
                {
                    if (LevelManager.Current == null || !LevelManager.Current.IsComplete)
                        Pause();
                    return;
                }
                Back();
            }
            if (!IsPaused)
                return;

            if (!busy)
            {
                if (prev.WasPressedThisFrame()) Step(-1);
                if (next.WasPressedThisFrame()) Step(1);
                if (submit.WasPressedThisFrame()) Confirm();
                if (cancel.WasPressedThisFrame()) Back();
                if (Mouse.current != null)
                {
                    float wheel = Mouse.current.scroll.ReadValue().y;
                    if (wheel > 0.1f) Step(-1);
                    else if (wheel < -0.1f) Step(1);
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                        Click(Mouse.current.position.ReadValue());
                }
            }
            Animate();
        }

        public void Pause()
        {
            IsPaused = true;
            busy = false;
            Time.timeScale = 0f;
            panel.SetActive(true);
            if (playerInput != null)
                playerInput.enabled = false;
            unroll = 0f;
            smoking = false;
            ShowMain();
            // açılış: tambur bir tur geriden dönerek gelir, yerine oturur
            from = shown = -Chambers;
            to = 0f;
            selected = 0;
            moveStart = openStart = Time.unscaledTime;
            lastTick = Mathf.RoundToInt(shown);
            Play(openClip != null ? openClip : cockClip, 1f);
            Layout();
        }

        public void Resume()
        {
            IsPaused = false;
            busy = false;
            adjust = null;
            Time.timeScale = 1f;
            panel.SetActive(false);
            if (playerInput != null)
                playerInput.enabled = true;
        }

        // ---- sayfalar ----

        void ShowMain()
        {
            SetPage(new[] { "devam", "bastan", "ayarlar", "menu" },
                new Action[] { Resume, () => Fire(SceneFlow.RestartCurrent), ShowSettings, () => Fire(SceneFlow.LoadMainMenu) },
                Resume);
        }

        void ShowSettings()
        {
            SetPage(new[] { "ses", "tamekran", "geri" },
                new Action[] { EnterVolume, ToggleFullscreen, ShowMain },
                ShowMain);
            Turn();
        }

        // Ses modu: tambur her döndüğünde ses %10 değişir; onay veya geri ayarlara döner.
        void EnterVolume()
        {
            adjust = dir =>
            {
                GameProgress.Volume = Mathf.Clamp01(Mathf.Round((GameProgress.Volume + dir * 0.1f) * 10f) / 10f);
                RefreshVolume();
            };
            back = () =>
            {
                adjust = null;
                back = ShowMain;
            };
        }

        void ToggleFullscreen() => GameProgress.Fullscreen = !GameProgress.Fullscreen;

        void SetPage(string[] keys, Action[] pageActions, Action onBack)
        {
            adjust = null;
            back = onBack;
            for (int i = 0; i < Chambers; i++)
            {
                bool used = i < keys.Length;
                actions[i] = used ? pageActions[i] : null;
                chamberArt[i] = used ? Array.Find(labelArt, a => a != null && a.key == keys[i]) : null;
            }
            selected = 0;
            bannerFor = -1; // yeni sayfa: bandana kapanıp yeniden açılır
            RefreshVolume(); // gösterge bandanada, "SES" seçiliyken görünür
            Layout();
        }

        // sayfa değişince tambur ileri bir tur döner, ilk hazne üste gelir
        void Turn()
        {
            from = shown;
            to = (Mathf.Floor(shown / Chambers) + 1f) * Chambers;
            moveStart = Time.unscaledTime;
            Play(cockClip, 1.2f);
        }

        // ---- girdi ----

        // dir: +1 saat yönünde bir sonraki dolu hazne, -1 bir önceki. Sayaç sınırsız: sondan başa geçince geri sarmaz.
        void Step(int dir)
        {
            if (adjust != null)
            {
                from = shown;
                to = (moveStart >= 0f ? to : shown) + dir;
                moveStart = Time.unscaledTime;
                adjust(dir);
                return;
            }
            int steps = 0, target = selected;
            do
            {
                target = (target + dir + Chambers) % Chambers;
                steps++;
            } while (actions[target] == null && steps < Chambers);
            if (actions[target] == null || target == selected)
                return;
            RotateTo(target, dir * steps);
        }

        void RotateTo(int target, int delta)
        {
            from = shown;
            to = (moveStart >= 0f ? to : shown) + delta;
            moveStart = Time.unscaledTime;
            selected = target;
        }

        void Confirm()
        {
            if (adjust != null)
            {
                Back();
                return;
            }
            if (actions[selected] == null)
                return;
            Play(cockClip, 1.15f);
            actions[selected]();
        }

        void Back()
        {
            if (busy || back == null)
                return;
            Play(tickClip, 0.8f);
            back();
        }

        // Fareyle tıklama: en yakın dolu hazneye (fişek ya da yazısı) çevirir; seçili olana tıklanırsa onaylar.
        void Click(Vector2 screen)
        {
            var root = (RectTransform)drum.parent;
            Camera cam = GetComponentInParent<Canvas>().worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, cam, out Vector2 local))
                return;
            if (banner != null && unroll >= 1f && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)banner.transform, screen, cam))
            {
                Confirm(); // açık bandanaya tıklamak seçili seçeneği onaylar
                return;
            }
            int best = -1;
            float bestDist = float.MaxValue;
            for (int k = 0; k < Chambers; k++)
            {
                if (actions[k] == null && adjust == null)
                    continue;
                foreach (Graphic g in new Graphic[] { cartridges[k], labels[k] })
                {
                    if (g == null || !g.enabled)
                        continue;
                    var rt = g.rectTransform;
                    Vector2 d = local - rt.anchoredPosition;
                    Vector2 half = rt.sizeDelta * 0.5f + new Vector2(6f, 6f); // biraz pay: küçük fişeğe kolay tıklansın
                    if (Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y && d.sqrMagnitude < bestDist)
                    {
                        best = k;
                        bestDist = d.sqrMagnitude;
                    }
                }
            }
            if (best < 0)
                return;
            if (adjust != null)
            {
                Back();
                return;
            }
            if (best == selected && moveStart < 0f)
            {
                Confirm();
                return;
            }
            int delta = (best - selected + Chambers) % Chambers;
            if (delta > Chambers / 2)
                delta -= Chambers;
            RotateTo(best, delta);
        }

        // Ateşli seçim (baştan başlat, ana menü): horoz kurulur, tambur bir an titrer, sonra eylem.
        void Fire(Action action) => StartCoroutine(FireRoutine(action));

        IEnumerator FireRoutine(Action action)
        {
            busy = true;
            float end = Time.unscaledTime + 0.25f;
            Vector2 basePos = drum.anchoredPosition;
            while (Time.unscaledTime < end)
            {
                drum.anchoredPosition = basePos + new Vector2(0f, UnityEngine.Random.Range(-1, 2));
                yield return null;
            }
            drum.anchoredPosition = basePos;
            action();
        }

        // ---- çizim ----

        void Animate()
        {
            if (openStart >= 0f)
            {
                float p = Mathf.Clamp01((Time.unscaledTime - openStart) / openTime);
                drum.localScale = Vector3.one * Mathf.LerpUnclamped(0.55f, 1f, EaseOutBack(p, settleOvershoot));
                if (dim != null)
                    dim.color = new Color(dim.color.r, dim.color.g, dim.color.b, dimAlpha * Mathf.Clamp01(p * 2f));
                if (p >= 1f)
                    openStart = -1f;
            }
            if (moveStart >= 0f)
            {
                float span = Mathf.Abs(to - from);
                float duration = openStart >= 0f ? openTime : stepTime * Mathf.Max(1f, Mathf.Sqrt(span));
                float p = Mathf.Clamp01((Time.unscaledTime - moveStart) / duration);
                shown = Mathf.LerpUnclamped(from, to, EaseOutBack(p, settleOvershoot));
                int notch = Mathf.RoundToInt(shown);
                if (notch != lastTick && p < 0.95f)
                {
                    lastTick = notch;
                    Play(tickClip, 0.9f + 0.2f * UnityEngine.Random.value);
                }
                if (p >= 1f)
                {
                    moveStart = -1f;
                    shown = to;
                    lastTick = Mathf.RoundToInt(to);
                }
            }
            Layout();
            AnimateBanner();
        }

        // Bandana: tambur dönmeye başlayınca yumuşakça sağdan sola tamburun arkasına sarılır, yazı da kumaş üstünden
        // geçerken söner. İçerik yalnızca bandana kapalıyken değişir; tambur durunca "tık" diye yeniden açılır, kumaş
        // yazının hizasına gelince yazı küçük bir duman bulutuyla belirir. Ses ayarında bandana açık kalır (tambur
        // yalnızca kadran gibi döner), "SES" yazısının yanında ses göstergesi durur.
        void AnimateBanner()
        {
            if (banner == null)
                return;
            float dt = Time.unscaledDeltaTime;
            bool settled = moveStart < 0f && openStart < 0f;
            bool open = bannerFor == selected && (settled || adjust != null);
            if (!open)
            {
                unroll = Mathf.MoveTowards(unroll, 0f, dt / closeTime);
                if (unroll <= 0f && bannerFor != selected)
                {
                    bannerFor = selected; // kapalıyken yeni seçeneğe geç
                    puffed = false;
                }
            }
            else
            {
                if (unroll <= 0f)
                    Play(cockClip, 1.6f);
                unroll = Mathf.MoveTowards(unroll, 1f, dt / unrollTime);
            }
            float reveal = EaseOutCubic(unroll);
            banner.Reveal = reveal;

            LabelArt art = bannerFor >= 0 ? chamberArt[bannerFor] : null;
            bool isVolume = art != null && art.key == "ses";
            float labelAt = isVolume ? volumeLabelAt : bannerLabelAt;
            // yazı, kumaş onun hizasını geçtikçe belirir / kapanırken söner
            float visible = art == null ? 0f : Mathf.Clamp01((reveal - (labelAt - 0.05f)) / 0.3f);
            Vector2 at = OnBanner(labelAt);
            if (!puffed && open && visible > 0f)
            {
                puffed = true;
                smoking = true;
                smokeStart = Time.unscaledTime;
            }

            if (bannerLabel != null)
            {
                bannerLabel.enabled = visible > 0f;
                if (visible > 0f)
                {
                    bannerLabel.sprite = art.selected;
                    bannerLabel.rectTransform.sizeDelta = PixelSize(art.selected) * 1.5f; // bandanada büyük (3x)
                    bannerLabel.rectTransform.anchoredPosition = Snap(at);
                    bannerLabel.color = new Color(1f, 1f, 1f, visible);
                }
            }

            if (volumeRoot != null)
            {
                bool showVolume = isVolume && visible > 0f;
                volumeRoot.SetActive(showVolume);
                if (showVolume)
                {
                    ((RectTransform)volumeRoot.transform).anchoredPosition = Snap(OnBanner(volumeAt));
                    var group = volumeRoot.GetComponent<CanvasGroup>();
                    if (group != null)
                        group.alpha = visible;
                    RefreshVolume();
                }
            }

            if (smoke != null)
            {
                float t = smoking ? Time.unscaledTime - smokeStart : -1f;
                bool show = t >= 0f && t < smokeTime;
                smoke.enabled = show;
                if (show)
                {
                    float p = t / smokeTime;
                    smoke.rectTransform.anchoredPosition = Snap(at + new Vector2(0f, 6f * p));
                    smoke.rectTransform.localScale = Vector3.one * (0.4f + 0.6f * EaseOutCubic(p));
                    smoke.color = new Color(1f, 1f, 1f, 0.6f * (1f - p) * (1f - p));
                }
                else
                    smoking = false;
            }
        }

        // bandananın x01 noktası (0 sol uç, 1 sağ uç), dalgasıyla birlikte
        Vector2 OnBanner(float x01)
        {
            var rect = (RectTransform)banner.transform;
            return rect.anchoredPosition + new Vector2(rect.rect.width * x01, banner.OffsetAt(x01));
        }

        /// <summary>Editör önizlemesi için: animasyonu atlayıp menüyü durmuş, bandanası açık haliyle çizer.</summary>
        public void ShowSettledForPreview(bool settings = false)
        {
            if (settings)
                ShowSettings();
            else
                ShowMain();
            moveStart = openStart = -1f;
            bannerFor = selected;
            unroll = 1f;
            puffed = true;
            smoking = false;
            Layout();
            AnimateBanner();
        }

        static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);

        /// <summary>Tamburu, fişekleri ve yazıları o anki dönüşe göre yerleştirir (kurulumda editörden de çağrılır).</summary>
        public void Layout()
        {
            if (drum == null)
                return;
            // hazne k saat yönünde k*60 derecede; tambur shown kadar geri dönünce seçili hazne üste gelir.
            // Tambur görseli 60 derecede kendine eş: yalnızca küsuratı döndürülür, durunca açı 0 olur ve
            // piksel sanatı döndürme kırıklığı olmadan keskin görünür.
            // Görselde hazneler saat 12'den başlar; seçili hazne selectedAngle'da olsun diye görsel o kadar saat yönünde
            // çevrilir (90 derecenin katı: piksel sanatı keskin kalır).
            // Küsurat en yakın hazneye göre alınır (-0.5..0.5): görsel başa sarma anı iki haznenin ortasına, tambur
            // hızla dönerken denk gelir. Durup otururken (ve geri esnerken) sarma olmaz; hazneler tam eşit aralıklı
            // olmadığından sarma anında görülen küçük sıçrama yerine oturma sırasında titreme yapmaz.
            int turned = Mathf.RoundToInt(shown);
            float spin = -selectedAngle + (shown - turned) * 60f;
            drum.localRotation = Quaternion.Euler(0f, 0f, spin);
            Vector2 center = drum.anchoredPosition;
            float scale = drum.localScale.x;
            int top = Mathf.RoundToInt(Mathf.Repeat(shown, Chambers)) % Chambers;
            for (int k = 0; k < Chambers; k++)
            {
                float a = (selectedAngle + (k - shown) * 60f) * Mathf.Deg2Rad; // 0 = üst, + saat yönü
                var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                bool used = actions[k] != null || adjust != null;
                bool isTop = k == top;
                if (cartridges[k] != null)
                {
                    cartridges[k].enabled = used;
                    cartridges[k].sprite = isTop && adjust == null ? cartridgeSelectedSprite : cartridgeSprite;
                    cartridges[k].rectTransform.sizeDelta = PixelSize(cartridges[k].sprite);
                    // 2'lik ızgaraya yuvarlanmaz: delik merkezleri tambur pikselinin kenarında (tek sayılı birimde) durur,
                    // yuvarlamak fişeği yarım piksel kaydırır ve dönerken titretirdi
                    Vector2 pos = center + ChamberOffset(k, turned, spin, dir) * scale;
                    cartridges[k].rectTransform.anchoredPosition = moveStart < 0f && openStart < 0f
                        ? new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y)) : pos;
                    cartridges[k].rectTransform.localScale = Vector3.one * scale;
                }
                if (labels[k] != null)
                {
                    LabelArt art = chamberArt[k];
                    // seçili seçeneğin yazısı bandanada; çevredekiler küçük ve soluk
                    bool show = art != null && actions[k] != null && !(isTop && banner != null && moveStart < 0f);
                    labels[k].enabled = show;
                    if (!show)
                        continue;
                    labels[k].sprite = art.idle;
                    labels[k].rectTransform.sizeDelta = PixelSize(labels[k].sprite);
                    labels[k].rectTransform.anchoredPosition = Snap(center + dir * labelRadius * scale);
                    labels[k].color = new Color(1f, 1f, 1f, idleLabelAlpha * Mathf.Clamp01(scale));
                }
            }
        }

        // UI'da piksel sanatı 2x çizilir (SetNativeSize sprite PPU'su yüzünden 3 kattan fazla büyütürdü)
        static Vector2 PixelSize(Sprite sprite) => sprite != null ? sprite.rect.size * 2f : Vector2.zero;

        // Hazne k'nın tambur merkezine göre yeri: görseldeki gerçek deliğin (k - turned) tamburla birlikte döndürülmüş
        // hali. Böylece fişek, tambur nereye dönerse dönsün tam deliğin ortasına oturur.
        Vector2 ChamberOffset(int k, int turned, float spin, Vector2 idealDir)
        {
            if (holeOffsets == null || holeOffsets.Length != Chambers)
                return idealDir * chamberRadius;
            int hole = ((k - turned) % Chambers + Chambers) % Chambers;
            Vector2 o = holeOffsets[hole] * drumPixelScale;
            float r = spin * Mathf.Deg2Rad, c = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector2(o.x * c - o.y * sn, o.x * sn + o.y * c);
        }

        static Vector2 Snap(Vector2 v) => new Vector2(Mathf.Round(v.x / 2f) * 2f, Mathf.Round(v.y / 2f) * 2f); // 2x piksel ızgarası

        void RefreshVolume()
        {
            int lit = Mathf.RoundToInt(GameProgress.Volume * volumeSegments.Length);
            for (int i = 0; i < volumeSegments.Length; i++)
                if (volumeSegments[i] != null)
                    // ses ayarlanırken dilimler parlak, değilse biraz soluk
                    volumeSegments[i].color = i < lit ? (adjust != null ? volumeOn : Color.Lerp(volumeOn, volumeOff, 0.35f)) : volumeOff;
        }

        void Play(AudioClip clip, float pitch)
        {
            if (audioSource == null || clip == null)
                return;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip);
        }

        // c1: geçip geri gelme miktarı (1.70158 klasik; küçüldükçe daha ağır, sakin oturur)
        static float EaseOutBack(float x, float c1)
        {
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
