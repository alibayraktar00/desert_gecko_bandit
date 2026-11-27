using DesertGecko.Player;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.Levels
{
    /// <summary>
    /// Eğitim tabelası: kurdeleli kapalı parşömen havada hafifçe süzülür. Oyuncu yaklaşınca rulo küçük bir sıçrayış
    /// yapar, kurdele çözülür, tutamaklar iki yana açılıp kâğıdı serer, sonra ipucu harf harf yazılır. Oyuncu uzaklaşınca
    /// yazı silinir ve aynı adımlar tersine oynar; kapalı rulo yerinde süzülmeye devam eder (yarıda dönülürse kaldığı
    /// yerden geri açılır).
    /// Kâğıdın genişliği yazının boyuna göre ayarlanır (kenarları sabit, ortası tekrar eden parşömen kâğıdı).
    /// Görseller dünya-uzayı bir Canvas'ta; Canvas birimi = sanat pikseli (LevelBuilder kurar).
    /// </summary>
    public class SignPost : MonoBehaviour
    {
        [SerializeField] CanvasGroup bubble;
        [SerializeField] float showDistance = 3.5f;

        [Header("Parşömen")]
        [SerializeField] RectTransform scroll;     // hepsinin kökü (rulonun düşüşü ve büyümesi buna uygulanır)
        [SerializeField] Image rolled;             // kapalı rulo
        [SerializeField] Sprite[] rolledFrames;    // 0: kurdeleli, 1: kurdele çözülmüş
        [SerializeField] RectTransform reveal;     // RectMask2D: genişliği kâğıdın görünen kısmı
        [SerializeField] RectTransform paper;      // tam genişlikte, ortası tekrar eden kâğıt
        [SerializeField] RectTransform leftRoller;
        [SerializeField] RectTransform rightRoller;
        [SerializeField] Text label;
        [SerializeField] float paddingX = 16f;     // yazı ile kâğıt kenarı arası (sanat pikseli)
        [SerializeField] float minPaperWidth = 77f;
        [SerializeField] float charsPerSecond = 38f;

        const float DropTime = 0.18f;    // rulo yukarıdan düşüp oturur
        const float UntieTime = 0.14f;   // kurdele çözülmüş kare
        const float UnrollTime = 0.42f;  // tutamaklar açılır
        const float OpenTime = DropTime + UntieTime + UnrollTime;
        const float CloseSpeed = 1f;     // sarılma açılışla aynı hızda (tutamaklar kapanır, kurdele bağlanır, rulo zıplar)
        const float EraseTime = 0.45f;   // yazının silinmesi en fazla bu kadar sürer
        const float RollerHalf = 4f;     // tutamak genişliğinin yarısı (8 px)
        const float BobAmount = 2f;      // süzülme genliği (sanat pikseli)
        const float BobSpeed = 1.6f;     // rad/sn: ~4 sn'de bir iniş-çıkış

        float bobOffset;

        Transform player;
        string fullText = "";
        float paperWidth;
        float phase;      // 0 kapalı ... OpenTime tamamen açık
        float typed;      // yazılan karakter sayısı (kesirli)
        int shownChars = -1;

        void Start()
        {
            var found = FindAnyObjectByType<PlayerController>();
            if (found != null)
                player = found.transform;
            if (label != null)
            {
                fullText = label.text;
                // Kâğıt yazıya göre: en uzun satır + iki yan boşluk, en az bir kâğıt boyu; çift sayı (ortalı, keskin)
                float textWidth = label.preferredWidth;
                paperWidth = Mathf.Max(minPaperWidth, Mathf.Ceil((textWidth + 2f * paddingX) / 2f) * 2f);
                if (paper != null)
                    paper.sizeDelta = new Vector2(paperWidth, paper.sizeDelta.y);
                label.rectTransform.sizeDelta = new Vector2(paperWidth - 2f * paddingX + 4f, label.rectTransform.sizeDelta.y);
            }
            phase = 0f;
            typed = 0f;
            bobOffset = transform.position.x * 0.7f;
            Apply();
        }

        void Update()
        {
            if (player == null)
                return;
            Vector2 d = player.position - transform.position;
            bool near = Mathf.Abs(d.x) < showDistance && Mathf.Abs(d.y) < showDistance;
            float dt = Time.deltaTime;
            if (near)
            {
                if (phase < OpenTime)
                    phase = Mathf.Min(OpenTime, phase + dt);
                else
                    typed = Mathf.Min(fullText.Length, typed + dt * charsPerSecond * PauseFactor());
            }
            else if (typed > 0f)
            {
                // Kapanış açılışın tersi: önce yazı sondan başa silinir (en fazla EraseTime), sonra parşömen sarılır
                float rate = Mathf.Max(charsPerSecond * 2f, fullText.Length / EraseTime);
                typed = Mathf.Max(0f, typed - dt * rate);
            }
            else
                phase = Mathf.Max(0f, phase - dt * CloseSpeed);
            Apply();
        }

        // Noktalama ve satır sonunda kısa duraklama: yazı okunur bir ritimle akar.
        float PauseFactor()
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(typed) - 1, 0, Mathf.Max(0, fullText.Length - 1));
            if (fullText.Length == 0)
                return 1f;
            char c = fullText[i];
            return c == '\n' || c == '.' || c == '!' ? 0.25f : c == ',' || c == ':' ? 0.45f : 1f;
        }

        void Apply()
        {
            if (bubble != null)
                bubble.alpha = 1f;   // kapalı rulo hep görünür, havada süzülür

            // 1) rulo küçük bir sıçrayış yapar, 2) kurdele çözülür
            bool rolledStage = phase < DropTime + UntieTime;
            if (rolled != null)
            {
                rolled.enabled = rolledStage;
                Sprite frame = rolledFrames != null && rolledFrames.Length >= 2 ? rolledFrames[phase < DropTime ? 0 : 1] : null;
                if (frame != null && rolled.sprite != frame)
                {
                    rolled.sprite = frame;
                    rolled.rectTransform.sizeDelta = frame.rect.size;   // iki kare farklı genişlikte; esnetilirse pikseller bozulur
                }
            }
            if (scroll != null)
            {
                // Süzülme: yavaş yukarı aşağı (±2 px, tam piksele yuvarlı). Her tabela farklı anda, birlikte inip kalkmasın.
                float bob = Mathf.Round(Mathf.Sin((Time.time + bobOffset) * BobSpeed) * BobAmount);
                // Açılırken rulo zıplar gibi 6 px yükselip iner
                float k = Mathf.Clamp01(phase / DropTime);
                float hop = phase > 0f ? Mathf.Round(Mathf.Sin(k * Mathf.PI) * 6f) : 0f;
                scroll.anchoredPosition = new Vector2(0f, bob + hop);
            }

            // 3) tutamaklar açılır, kâğıt ortadan görünür
            float u = Mathf.Clamp01((phase - DropTime - UntieTime) / UnrollTime);
            float open = rolledStage ? 0f : EaseOutCubic(u);
            float half = Mathf.Round(open * paperWidth / 2f);
            if (reveal != null)
            {
                reveal.gameObject.SetActive(!rolledStage);
                reveal.sizeDelta = new Vector2(half * 2f, reveal.sizeDelta.y);
            }
            if (leftRoller != null)
            {
                leftRoller.gameObject.SetActive(!rolledStage);
                leftRoller.anchoredPosition = new Vector2(-half - RollerHalf, 0f);
            }
            if (rightRoller != null)
            {
                rightRoller.gameObject.SetActive(!rolledStage);
                rightRoller.anchoredPosition = new Vector2(half + RollerHalf, 0f);
            }

            // 4) yazı harf harf (kalan kısım saydam yazılır, böylece satırlar baştan yerinde durur ve kaymaz)
            if (label != null)
            {
                int n = phase >= OpenTime ? Mathf.Clamp(Mathf.FloorToInt(typed), 0, fullText.Length) : 0;
                if (n != shownChars)
                {
                    shownChars = n;
                    label.text = n >= fullText.Length ? fullText
                        : fullText.Substring(0, n) + "<color=#00000000>" + fullText.Substring(n) + "</color>";
                }
            }
        }

        static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    }
}
