using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DesertGecko.UI
{
    /// <summary>
    /// Ana menünün yön tabelası: bir direğe çakılı, sağı gösteren ok biçimli tahtalar. Her tahta bir seçenek.
    /// Seçili tahta direkten biraz dışarı kayar, yazısı altın olur ve rüzgârda hafifçe sallanır.
    /// Sayfa değişince tahtalar yukarıdan aşağı sırayla direğin etrafında döner (yatay ölçek 1 -> 0 -> 1): yarıda,
    /// tahta sırtını dönmüşken yazısı yenisiyle değişir. Oyunu başlatan seçenek onaylanınca tahta hızla fırıl fırıl
    /// döner, horoz kurulur ve ateş edilir; sonra eylem çalışır (MainMenu toz geçişini başlatır).
    /// Girdi: yukarı/aşağı (klavye, gamepad, tekerlek), fareyle üstüne gelme, onay, geri. Ölçeksiz zaman.
    /// Arayüzü RevolverMenu ile aynı (SetPage, SetAdjust, SelectionChanged), MainMenu ikisiyle de çalışır.
    /// </summary>
    public class SignpostMenu : MonoBehaviour
    {
        [Serializable]
        public class LabelArt
        {
            public string text;
            [Tooltip("Tahtaya yakılmış koyu yazı")] public Sprite idle;
            [Tooltip("Seçiliyken altın yazı")] public Sprite selected;
        }

        [Tooltip("Tahtaların kökü; pivotları direğe çakılı sol uç. Yukarıdan aşağı sıralı")]
        [SerializeField] Transform[] planks = Array.Empty<Transform>();
        [Tooltip("Her tahtanın yazısı (tahtanın çocuğu)")]
        [SerializeField] SpriteRenderer[] labels = Array.Empty<SpriteRenderer>();
        [Tooltip("Her tahtanın görseli (renk koyulaştırmak ve fare için)")]
        [SerializeField] SpriteRenderer[] boards = Array.Empty<SpriteRenderer>();
        [SerializeField] LabelArt[] labelArt = Array.Empty<LabelArt>();

        [Header("Görünüm")]
        [Tooltip("Seçili tahtanın dışarı kayması (dünya birimi)")]
        [SerializeField] float selectedSlide = 0.25f;
        [Tooltip("Seçili olmayan tahtaların rengi")]
        [SerializeField] Color idleTint = new Color(0.78f, 0.74f, 0.7f, 1f);
        [Tooltip("Seçili tahtanın sallanma açısı (derece)")]
        [SerializeField] float swayAngle = 2.2f;

        [Header("Zamanlama")]
        [SerializeField] float flipTime = 0.32f;
        [Tooltip("Tahtalar arası gecikme (yukarıdan aşağı dalga)")]
        [SerializeField] float flipStagger = 0.07f;

        [Header("Ses")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip tickClip;
        [SerializeField] AudioClip flipClip;
        [SerializeField] AudioClip cockClip;
        [SerializeField] AudioClip shotClip;

        Action[] actions = Array.Empty<Action>();
        bool[] fires = Array.Empty<bool>();
        LabelArt[] current = Array.Empty<LabelArt>();
        Action back;
        Action<int> adjust;
        int selected, count, enabledFrame;
        bool busy;
        float[] slide = Array.Empty<float>();      // 0..1 her tahtanın dışarı kayma oranı
        float[] flip = Array.Empty<float>();       // her tahtanın yatay ölçeği (dönüş)
        float[] kick = Array.Empty<float>();       // onay/ayar dürtmesi
        Vector3[] basePos = Array.Empty<Vector3>();
        Coroutine flipping;
        InputAction up, down, left, right, submit, cancel;
        Camera cam;

        public int Selected => selected;
        public int PlankCount => planks.Length;

        /// <summary>Seçili tahta değişince çağrılır: menü üstteki bilgi yazısını günceller.</summary>
        public event Action<int> SelectionChanged;

        /// <summary>
        /// Tahtaları yeni sayfaya kurar (en fazla planks.Length): tahtalar sırayla döner, dönüşün ortasında yazılar
        /// değişir. fire[i] true olan seçenek onaylanınca tahta fırıl fırıl döner ve ateş edilir (oyunu başlatanlar).
        /// onBack: Escape / gamepad B (null ise geri yok).
        /// </summary>
        public void SetPage(string[] texts, Action[] pageActions, bool[] fire, Action onBack)
        {
            EnsureArrays();
            adjust = null;
            back = onBack;
            var next = new LabelArt[planks.Length];
            for (int i = 0; i < planks.Length; i++)
            {
                bool used = i < texts.Length;
                actions[i] = used ? pageActions[i] : null;
                fires[i] = used && fire != null && i < fire.Length && fire[i];
                next[i] = used ? Find(texts[i]) : null;
            }
            count = Mathf.Min(texts.Length, planks.Length);
            selected = 0;
            SelectionChanged?.Invoke(selected);
            if (!isActiveAndEnabled || !Application.isPlaying)
            {
                // editörde kurulum: dönmeden doğrudan yaz
                Array.Copy(next, current, current.Length);
                for (int i = 0; i < planks.Length; i++)
                    flip[i] = current[i] != null ? 1f : 0f;
                Layout();
                return;
            }
            if (flipping != null)
                StopCoroutine(flipping);
            flipping = StartCoroutine(FlipTo(next));
        }

        /// <summary>
        /// Ayar modu: seçili tahtanın yazısı text olur; sağ/sol (ve yukarı/aşağı) her basışta onStep(+1/-1) çağrılır,
        /// tahta dürtülür. Onay veya geri moddan çıkar ve onBack çalışır.
        /// </summary>
        public void SetAdjust(string text, Action<int> onStep, Action onBack)
        {
            EnsureArrays();
            adjust = onStep;
            back = onBack;
            current[selected] = Find(text);
            kick[selected] = 1f;
            Layout();
        }

        /// <summary>Editör kurulumu için: tahtaya yazıyı doğrudan koyar.</summary>
        public void SetText(int plank, string text)
        {
            EnsureArrays();
            current[plank] = string.IsNullOrEmpty(text) ? null : Find(text);
            flip[plank] = current[plank] != null ? 1f : 0f;
            Layout();
        }

        LabelArt Find(string text)
        {
            LabelArt art = Array.Find(labelArt, a => a != null && a.text == text);
            if (art == null && !string.IsNullOrEmpty(text))
                Debug.LogWarning($"[SignpostMenu] '{text}' için yazı görseli yok (tools/gemini_tabela_isle.py LABELS)");
            return art;
        }

        void EnsureArrays()
        {
            int n = planks.Length;
            if (current.Length == n)
                return;
            actions = new Action[n];
            fires = new bool[n];
            current = new LabelArt[n];
            slide = new float[n];
            flip = new float[n];
            kick = new float[n];
            basePos = new Vector3[n];
            for (int i = 0; i < n; i++)
                basePos[i] = planks[i] != null ? planks[i].localPosition : Vector3.zero;
        }

        void Awake()
        {
            EnsureArrays();
            cam = Camera.main;
            up = MakeAction("<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up");
            down = MakeAction("<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down");
            left = MakeAction("<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
            right = MakeAction("<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
            submit = MakeAction("<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            cancel = MakeAction("<Keyboard>/escape", "<Keyboard>/backspace", "<Gamepad>/buttonEast");
            up.performed += _ => Step(-1);
            down.performed += _ => Step(1);
            left.performed += _ => Nudge(-1);
            right.performed += _ => Nudge(1);
            submit.performed += _ => Confirm();
            cancel.performed += _ => Back();
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
            enabledFrame = Time.frameCount;
            foreach (InputAction a in new[] { up, down, left, right, submit, cancel })
                a?.Enable();
        }

        void OnDisable()
        {
            foreach (InputAction a in new[] { up, down, left, right, submit, cancel })
                a?.Disable();
        }

        void OnDestroy()
        {
            foreach (InputAction a in new[] { up, down, left, right, submit, cancel })
                a?.Dispose();
        }

        void Update()
        {
            if (!busy && flipping == null && Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (adjust != null)
                {
                    if (wheel > 0.1f) Nudge(-1);
                    else if (wheel < -0.1f) Nudge(1);
                }
                else
                {
                    if (wheel > 0.1f) Step(-1);
                    else if (wheel < -0.1f) Step(1);
                }
                int hover = PlankUnderMouse();
                if (adjust == null && hover >= 0 && hover != selected && Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f)
                    Select(hover);
                if (Mouse.current.leftButton.wasPressedThisFrame && Time.frameCount > enabledFrame && (hover >= 0 || adjust != null))
                {
                    if (adjust == null && hover != selected)
                        Select(hover);
                    Confirm();
                }
            }

            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < planks.Length; i++)
            {
                float target = i == selected && current[i] != null ? 1f : 0f;
                slide[i] = Mathf.MoveTowards(slide[i], target, dt * 6f);
                kick[i] = Mathf.MoveTowards(kick[i], 0f, dt * 4f);
            }
            Layout();
        }

        int PlankUnderMouse()
        {
            if (cam == null)
                cam = Camera.main;
            if (cam == null)
                return -1;
            Vector2 screen = Mouse.current.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            for (int i = 0; i < boards.Length && i < count; i++)
            {
                if (boards[i] == null || current[i] == null)
                    continue;
                Bounds b = boards[i].bounds;
                if (world.x >= b.min.x && world.x <= b.max.x && world.y >= b.min.y && world.y <= b.max.y)
                    return i;
            }
            return -1;
        }

        void Select(int index)
        {
            if (index == selected || index < 0 || actions[index] == null)
                return;
            selected = index;
            Play(tickClip, 0.9f + 0.2f * UnityEngine.Random.value);
            SelectionChanged?.Invoke(selected);
        }

        void Step(int dir)
        {
            if (busy || flipping != null)
                return;
            if (adjust != null)
            {
                Nudge(-dir); // yukarı = artır
                return;
            }
            int target = selected;
            for (int steps = 0; steps < planks.Length; steps++)
            {
                target = (target + dir + planks.Length) % planks.Length;
                if (actions[target] != null)
                    break;
            }
            Select(target);
        }

        void Nudge(int dir)
        {
            if (busy || adjust == null)
                return;
            kick[selected] = 1f;
            Play(tickClip, dir > 0 ? 1.1f : 0.9f);
            adjust(dir);
        }

        void Confirm()
        {
            if (busy || flipping != null)
                return;
            if (adjust != null)
            {
                Back();
                return;
            }
            if (actions[selected] == null)
                return;
            if (fires[selected])
                StartCoroutine(Fire(actions[selected]));
            else
            {
                kick[selected] = 1f;
                Play(cockClip, 1.15f);
                actions[selected]();
            }
        }

        void Back()
        {
            if (busy || back == null || flipping != null)
                return;
            Play(tickClip, 0.8f);
            back();
        }

        IEnumerator FlipTo(LabelArt[] next)
        {
            float start = Time.unscaledTime;
            var swapped = new bool[planks.Length];
            var played = new bool[planks.Length];
            float total = flipTime + flipStagger * planks.Length;
            while (Time.unscaledTime - start < total)
            {
                float t = Time.unscaledTime - start;
                for (int i = 0; i < planks.Length; i++)
                {
                    bool had = current[i] != null, will = next[i] != null;
                    if (!had && !will)
                    {
                        flip[i] = 0f;
                        continue;
                    }
                    float p = Mathf.Clamp01((t - i * flipStagger) / flipTime);
                    if (p > 0f && !played[i])
                    {
                        played[i] = true;
                        Play(flipClip != null ? flipClip : tickClip, 0.85f + 0.1f * i);
                    }
                    if (p < 0.5f)
                    {
                        // ilk yarı: tahta direğin etrafında dönüp kenarı görünür hale gelir
                        float q = p / 0.5f;
                        flip[i] = had ? Mathf.Cos(q * Mathf.PI * 0.5f) : 0f;
                    }
                    else
                    {
                        if (!swapped[i])
                        {
                            swapped[i] = true;
                            current[i] = next[i];
                        }
                        float q = (p - 0.5f) / 0.5f;
                        flip[i] = will ? EaseOutBack(q) : 0f;
                    }
                }
                yield return null;
            }
            for (int i = 0; i < planks.Length; i++)
            {
                current[i] = next[i];
                flip[i] = current[i] != null ? 1f : 0f;
            }
            flipping = null;
        }

        IEnumerator Fire(Action action)
        {
            busy = true;
            Play(cockClip, 1f);
            // tahta direğin etrafında giderek hızlanıp yavaşlayarak iki tur döner
            float t = 0f;
            const float spin = 0.9f;
            while (t < spin)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / spin);
                float angle = EaseInOut(p) * Mathf.PI * 4f;
                flip[selected] = Mathf.Cos(angle);
                Layout();
                yield return null;
            }
            flip[selected] = 1f;
            kick[selected] = 1f;
            Play(shotClip, 1f);
            yield return Wait(0.35f);
            action();
            busy = false;
        }

        /// <summary>Tahtaları ve yazıları o anki duruma göre yerleştirir (kurulumda editörden de çağrılır).</summary>
        public void Layout()
        {
            EnsureArrays();
            float time = Application.isPlaying ? Time.unscaledTime : 0f;
            for (int i = 0; i < planks.Length; i++)
            {
                Transform p = planks[i];
                if (p == null)
                    continue;
                LabelArt art = current[i];
                bool on = art != null && Mathf.Abs(flip[i]) > 0.02f;
                p.gameObject.SetActive(on);
                if (!on)
                    continue;
                float s = EaseOutCubic(slide[i]);
                float shove = Mathf.Sin(kick[i] * Mathf.PI) * 0.12f;
                p.localPosition = basePos[i] + new Vector3(selectedSlide * s + shove, 0f, 0f);
                float sway = s * swayAngle * Mathf.Sin(time * 2.3f + i) - kick[i] * 3f * Mathf.Sin(kick[i] * 9f);
                p.localRotation = Quaternion.Euler(0f, 0f, sway);
                // arkası dönükken (negatif ölçek) yazı ters görünmesin: yazı sadece önden bakarken
                p.localScale = new Vector3(flip[i], 1f, 1f);
                bool front = flip[i] > 0f;
                bool isSelected = i == selected && s > 0.5f;
                if (i < labels.Length && labels[i] != null)
                {
                    labels[i].enabled = front;
                    labels[i].sprite = isSelected ? art.selected : art.idle;
                }
                if (i < boards.Length && boards[i] != null)
                    boards[i].color = Color.Lerp(idleTint, Color.white, s);
            }
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

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);
        static float EaseInOut(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }
}
