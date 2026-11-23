using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DesertGecko.UI
{
    /// <summary>
    /// Ana menünün revolver tamburu. Menü yazıları tamburun yan yüzüne kazınmıştır: tambur dönünce yazılar ve
    /// oluklar silindirin üstünde yuvarlanır (öne gelen tam boy, kenara giden basık), arka yüzdeki altı fişek de
    /// aynı açıyla döner. Öne bakan şerit seçilidir ve altın renktedir. Yukarı/aşağı (klavye, gamepad, fare
    /// tekerleği) bir hazne döndürür ve tıkırdar; boş hazneler atlanır. Onaylayınca horoz kurulur, silah ateşlenip
    /// geri teper, ardından seçeneğin eylemi çalışır. Menü duraklatılmış olabilir diye her şey ölçeksiz zamanla.
    /// </summary>
    public class RevolverMenu : MonoBehaviour
    {
        public const int Chambers = 6;

        [Serializable]
        public class LabelArt
        {
            public string text;
            [Tooltip("Çeliğe kazınmış hali")] public Sprite idle;
            [Tooltip("Seçiliyken altın hali")] public Sprite selected;
        }

        [Header("Arka yüz: fişekler")]
        [Tooltip("Altı fişek; 0 = ilk seçenek")]
        [SerializeField] Transform[] rims = new Transform[Chambers];
        [Tooltip("Fişek halkasının merkezi (tambur göbeği); fişekler bunun çocuğu")]
        [SerializeField] Transform ring;
        [Tooltip("Halkanın yatay/dikey yarıçapı, dünya birimi (perspektif yüzünden elips)")]
        [SerializeField] Vector2 ringRadius = new Vector2(0.6f, 0.9f);

        [Header("Yan yüz: yazılar ve oluklar")]
        [Tooltip("Her haznenin yazı şeridi (tambur maskesinin içinde görünür)")]
        [SerializeField] SpriteRenderer[] bands = new SpriteRenderer[Chambers];
        [Tooltip("Oluk kenar çizgileri: her hazne arasında bir oluk, her oluğun iki kenarı (2 x 6 = 12)")]
        [SerializeField] SpriteRenderer[] flutes = new SpriteRenderer[Chambers * 2];
        [Tooltip("Tamburun arka yüzünün (fişekli yüz) merkezi; çizgiler bu yüzün kenarından başlar (yerel birim)")]
        [SerializeField] Vector2 faceCenter;
        [Tooltip("Arka yüzün yatay/dikey yarıçapı (yerel birim)")]
        [SerializeField] Vector2 faceRadius = new Vector2(1.1f, 1.35f);
        [Tooltip("Oluk ve yazı çizgilerinin açısı (derece); hepsi birbirine paralel")]
        [SerializeField] float lineAngle = -13f;
        [Tooltip("Yan yüzün sol ucunun x'i: oluklar buradan yüzün kenarına kadar uzanır (yerel birim)")]
        [SerializeField] float drumLeftX;
        [Tooltip("Yazının ortasının x'i (yerel birim)")]
        [SerializeField] float labelX;
        [Tooltip("Oluğun genişliği: iki kenar çizgisi oluk ekseninden bu kadar derece ayrı")]
        [SerializeField] float grooveHalfWidth = 12f;
        [Tooltip("Yazıların kazınmış/altın görselleri; SetText bu listeden metne göre seçer")]
        [SerializeField] LabelArt[] labelArt = Array.Empty<LabelArt>();

        [Tooltip("Oluk çizgilerinin yazılara göre aşağı kayması, yerel birim")]
        [SerializeField] float fluteDrop;

        [Header("Ateş geçişi")]
        [Tooltip("Menüdeki büyük, yazılı tamburlu silahın parçaları (geçişte gizlenir)")]
        [SerializeField] GameObject[] menuGunParts = Array.Empty<GameObject>();
        [Tooltip("Tamburu çerçeveye oturmuş gerçekçi silah ve onu tutan el (geçişte görünür, ateş eder)")]
        [SerializeField] GameObject[] realGunParts = Array.Empty<GameObject>();
        [Tooltip("Geçişte silahın üstüne çöken toz bulutları (pus)")]
        [SerializeField] SpriteRenderer[] hazePuffs = Array.Empty<SpriteRenderer>();
        [Tooltip("Pusun toplam süresi (saniye); silahlar yarısında değişir")]
        [SerializeField] float hazeTime = 1.2f;

        [Header("Silah")]
        [Tooltip("Onaylayınca geri tepen silah (bilekten döner)")]
        [SerializeField] Transform gun;
        [Tooltip("Ateşte bir an görünen namlu alevi (yoksa boş)")]
        [SerializeField] GameObject muzzleFlash;
        [SerializeField] float recoilAngle = 9f;

        [Header("Ses")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip tickClip;
        [SerializeField] AudioClip cockClip;
        [SerializeField] AudioClip shotClip;
        [Tooltip("Pusun en yoğun anında, gerçekçi silah belirirken: tamburun yuvaya oturma sesi")]
        [SerializeField] AudioClip closeClip;

        [Header("Zamanlama")]
        [Tooltip("Bir haznelik dönüş süresi (saniye)")]
        [SerializeField] float stepTime = 0.2f;

        readonly Action[] actions = new Action[Chambers];
        readonly bool[] fires = new bool[Chambers];
        Action back;
        Action<int> adjust;
        readonly LabelArt[] chamberArt = new LabelArt[Chambers];
        int selected;
        float shown, from, to, moveStart = -1f;
        int lastTick, enabledFrame;
        bool busy;
        Vector3 gunPosition;
        Quaternion gunRotation;
        Vector3[] hazeBase;
        InputAction up, down, submit, cancel;

        public int Selected => selected;

        /// <summary>Seçili hazne değişince (dönüş başlarken) çağrılır: menü üstteki bilgi yazısını günceller.</summary>
        public event Action<int> SelectionChanged;

        /// <summary>
        /// Tamburun seçeneklerini baştan kurar (en fazla 6): tambur bir tur dönerek yeni sayfaya geçer, ilk seçenek
        /// öne gelir. fire[i] true olan seçenek onaylanınca pus, gerçekçi silah ve ateş gelir (oyunu başlatanlar);
        /// diğerleri tıkırtıyla hemen çalışır. onBack: Escape / gamepad B (null ise geri yok).
        /// </summary>
        public void SetPage(string[] texts, Action[] pageActions, bool[] fire, Action onBack)
        {
            adjust = null;
            back = onBack;
            for (int i = 0; i < Chambers; i++)
            {
                bool used = i < texts.Length;
                actions[i] = used ? pageActions[i] : null;
                fires[i] = used && fire != null && i < fire.Length && fire[i];
                SetText(i, used ? texts[i] : null);
            }
            selected = 0;
            from = shown;
            to = (Mathf.Floor(shown / Chambers) + 1f) * Chambers; // ileri doğru bir tur: hazne 0 öne gelir
            moveStart = Time.unscaledTime;
            SelectionChanged?.Invoke(selected);
        }

        /// <summary>
        /// Ayar modu: tambur her döndüğünde onStep(+1/-1) çağrılır (ör. ses seviyesi), boş hazneler atlanmaz.
        /// Onay veya geri moddan çıkar ve onBack çalışır.
        /// </summary>
        public void SetAdjust(string text, Action<int> onStep, Action onBack)
        {
            for (int i = 0; i < Chambers; i++)
            {
                actions[i] = null;
                fires[i] = false;
                SetText(i, text);
            }
            adjust = onStep;
            back = onBack;
        }

        /// <summary>Bir hazneye seçenek bağlar. action null ise hazne boş sayılır ve gezinmede atlanır.</summary>
        public void SetOption(int chamber, string text, Action action)
        {
            actions[chamber] = action;
            SetText(chamber, text);
        }

        public void SetText(int chamber, string text)
        {
            chamberArt[chamber] = Array.Find(labelArt, a => a != null && a.text == text);
            if (chamberArt[chamber] == null && !string.IsNullOrEmpty(text))
                Debug.LogWarning($"[RevolverMenu] '{text}' için yazı görseli yok (tools/menu_tambur_ayir.py LABELS)");
            Layout();
        }

        void Awake()
        {
            up = MakeAction("<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up");
            down = MakeAction("<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down");
            submit = MakeAction("<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            cancel = MakeAction("<Keyboard>/escape", "<Keyboard>/backspace", "<Gamepad>/buttonEast");
            up.performed += _ => Step(-1);
            down.performed += _ => Step(1);
            submit.performed += _ => Confirm();
            cancel.performed += _ => Back();
            if (gun != null)
            {
                gunPosition = gun.localPosition;
                gunRotation = gun.localRotation;
            }
            if (muzzleFlash != null)
                muzzleFlash.SetActive(false);
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
            busy = false;
            ShowGun(menu: true);
            foreach (SpriteRenderer puff in hazePuffs)
                if (puff != null)
                    puff.enabled = false;
            enabledFrame = Time.frameCount; // alt panelden "Geri" tıklaması aynı karede ateşlemesin
            up?.Enable();
            down?.Enable();
            submit?.Enable();
            cancel?.Enable();
            Layout();
        }

        void OnDisable()
        {
            up?.Disable();
            down?.Disable();
            submit?.Disable();
            cancel?.Disable();
        }

        void OnDestroy()
        {
            up?.Dispose();
            down?.Dispose();
            submit?.Dispose();
            cancel?.Dispose();
        }

        void Update()
        {
            if (!busy && Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (wheel > 0.1f) Step(-1);
                else if (wheel < -0.1f) Step(1);
                if (Mouse.current.leftButton.wasPressedThisFrame && Time.frameCount > enabledFrame)
                    Confirm();
            }

            if (moveStart >= 0f)
            {
                float duration = stepTime * Mathf.Max(1f, Mathf.Sqrt(Mathf.Abs(to - from)));
                float p = Mathf.Clamp01((Time.unscaledTime - moveStart) / duration);
                shown = Mathf.LerpUnclamped(from, to, EaseOutBack(p));
                // her hazne yuvaya geçtiğinde bir tık (geri esnemede tekrar çalmasın diye en yakın tam sayı)
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
                Layout();
            }
        }

        // dir: +1 bir sonraki dolu hazne, -1 bir önceki. Hep aynı yöne dönen sınırsız bir sayaç tutulur,
        // böylece sondan başa geçince tambur geri sarmaz, ileri dönmeye devam eder.
        void Step(int dir)
        {
            if (busy)
                return;
            if (adjust != null)
            {
                // ayar modu: her yönde bir hazne döner ve değeri bir adım değiştirir
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
            from = shown;
            to = (moveStart >= 0f ? to : shown) + dir * steps;
            moveStart = Time.unscaledTime;
            selected = target;
            SelectionChanged?.Invoke(selected);
        }

        void Confirm()
        {
            if (busy)
                return;
            if (adjust != null)
            {
                Back();
                return;
            }
            if (moveStart >= 0f || actions[selected] == null)
                return;
            if (fires[selected])
                StartCoroutine(Fire(actions[selected]));
            else
            {
                Play(cockClip, 1.15f); // ateş etmeyen seçim: kısa horoz tıkı
                actions[selected]();
            }
        }

        void Back()
        {
            if (busy || back == null)
                return;
            Play(tickClip, 0.8f);
            back();
        }

        IEnumerator Fire(Action action)
        {
            busy = true;
            // pus: toz bulutları silahın üstüne çöker; en yoğun anında menü silahı yerine tamburu yuvasına oturmuş
            // gerçekçi silah gelir ("klak"), pus dağılır, sonra horoz kurulur ve ateş edilir
            yield return Haze();
            yield return Wait(0.35f);
            Play(cockClip, 1f);
            yield return Wait(0.45f);
            Play(shotClip, 1f);
            if (muzzleFlash != null)
                muzzleFlash.SetActive(true);
            // geri tepme: hızlı kalkış, yavaş iniş
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.unscaledDeltaTime;
                if (muzzleFlash != null && t > 0.08f)
                    muzzleFlash.SetActive(false);
                float kick = t < 0.06f ? t / 0.06f : 1f - EaseOutCubic((t - 0.06f) / 0.44f);
                if (gun != null)
                {
                    gun.localRotation = gunRotation * Quaternion.Euler(0f, 0f, recoilAngle * kick);
                    gun.localPosition = gunPosition + new Vector3(0.25f, 0.1f, 0f) * kick;
                }
                yield return null;
            }
            if (gun != null)
            {
                gun.localRotation = gunRotation;
                gun.localPosition = gunPosition;
            }
            yield return Wait(0.6f); // duman dağılırken bir an bekle, sonra sahne geçişi
            action();
            busy = false;
        }

        void ShowGun(bool menu)
        {
            foreach (GameObject part in menuGunParts)
                if (part != null)
                    part.SetActive(menu);
            foreach (GameObject part in realGunParts)
                if (part != null)
                    part.SetActive(!menu);
        }

        IEnumerator Haze()
        {
            if (hazeBase == null || hazeBase.Length != hazePuffs.Length)
            {
                hazeBase = new Vector3[hazePuffs.Length];
                for (int i = 0; i < hazePuffs.Length; i++)
                    if (hazePuffs[i] != null)
                        hazeBase[i] = hazePuffs[i].transform.localScale;
            }
            bool swapped = false;
            float t = 0f;
            while (t < hazeTime)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / hazeTime);
                for (int i = 0; i < hazePuffs.Length; i++)
                {
                    SpriteRenderer puff = hazePuffs[i];
                    if (puff == null)
                        continue;
                    // her bulut biraz gecikmeyle şişer, sonra dağılır (alfa 0 -> 1 -> 0)
                    float q = Mathf.Clamp01(p * 1.25f - i * 0.05f);
                    float alpha = Mathf.Sin(q * Mathf.PI);
                    puff.enabled = alpha > 0.01f;
                    puff.color = new Color(1f, 1f, 1f, alpha);
                    puff.transform.localScale = hazeBase[i] * (0.6f + 0.7f * q);
                }
                if (!swapped && p >= 0.45f)
                {
                    swapped = true;
                    ShowGun(menu: false);
                    Play(closeClip, 1f);
                }
                yield return null;
            }
            foreach (SpriteRenderer puff in hazePuffs)
                if (puff != null)
                    puff.enabled = false;
            if (!swapped)
                ShowGun(menu: false);
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
                yield return null;
        }

        void Play(AudioClip clip, float pitch)
        {
            if (audioSource == null || clip == null)
                return;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip);
        }

        /// <summary>Fişekleri, yazıları ve olukları tamburun o anki dönüşüne göre yerleştirir (kurulumda editörden de çağrılır).</summary>
        public void Layout()
        {
            for (int k = 0; k < Chambers; k++)
            {
                float d = Mathf.Repeat(k - shown + Chambers / 2f, Chambers) - Chambers / 2f; // -3..3, 0 = seçili
                if (ring != null && rims[k] != null)
                {
                    float a = (180f + 60f * d) * Mathf.Deg2Rad;
                    rims[k].localPosition = new Vector3(ringRadius.x * Mathf.Cos(a), ringRadius.y * Mathf.Sin(a), 0f);
                }
                LabelArt art = chamberArt[k];
                PlaceOnDrum(bands[k], 60f * d, art == null ? null : Mathf.Abs(d) < 0.5f ? art.selected : art.idle, false);
                // hazneler arası oluk (fişeklerin arasındaki şişliklere denk gelir), iki kenarı ayrı çizgi
                PlaceOnDrum(flutes[2 * k], 60f * d + 30f - grooveHalfWidth, null, true);
                PlaceOnDrum(flutes[2 * k + 1], 60f * d + 30f + grooveHalfWidth, null, true);
            }
        }

        // Yan yüzde phi açısındaki (0 = tam öne bakan, +90 = alt kenar) çizgiye yerleştirir. Çizgi, arka yüzün
        // kenarında o açıya denk gelen noktadan başlar (fişek yuvalarıyla hizalı) ve lineAngle ile sola uzanır;
        // bütün çizgiler paraleldir. Dönünce silindir gibi basılır (cos phi).
        void PlaceOnDrum(SpriteRenderer sr, float phi, Sprite sprite, bool stretch)
        {
            if (sr == null)
                return;
            float rad = phi * Mathf.Deg2Rad;
            float facing = Mathf.Cos(rad);
            if (facing <= 0.08f || (!stretch && sprite == null))
            {
                sr.enabled = false;
                return;
            }
            sr.enabled = true;
            if (!stretch)
                sr.sprite = sprite;
            // yüzün kenarındaki başlangıç noktası (phi = 0 yüzün en sol noktası, + aşağı)
            var start = new Vector2(faceCenter.x - faceRadius.x * facing, faceCenter.y - faceRadius.y * Mathf.Sin(rad));
            float a = lineAngle * Mathf.Deg2Rad, slope = Mathf.Tan(a);
            float x = stretch ? (drumLeftX + start.x) * 0.5f : labelX;
            var tr = sr.transform;
            tr.localPosition = new Vector3(x, start.y + (x - start.x) * slope - (stretch ? fluteDrop : 0f), 0f);
            // yazı görselleri eğimli üretilir (tools/menu_tambur_ayir.py, harf harf basamaklı), döndürülmez
            tr.localRotation = Quaternion.Euler(0f, 0f, stretch ? lineAngle : 0f);
            float length = (start.x - drumLeftX) / Mathf.Cos(a);
            float sx = stretch && sr.sprite != null ? Mathf.Max(0f, length) / sr.sprite.bounds.size.x : 1f;
            tr.localScale = new Vector3(sx, Mathf.Max(0.05f, facing), 1f);
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);
    }
}
