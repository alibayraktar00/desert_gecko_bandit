using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Sıcak su gayzeri: belirli aralıkla fışkırır, içindeki Gila'yı yukarı fırlatır. Önce kabarcıklarla haber verir.
    /// Zamanı kendi saatiyle tutar; aynı evreyle kurulan gayzerler sırayla çalışır.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Geyser : MonoBehaviour
    {
        [SerializeField] float period = 4f;
        [SerializeField] float warnTime = 0.8f;
        [SerializeField] float eruptTime = 1.5f;
        [Range(0f, 1f)]
        [SerializeField] float phase;
        [SerializeField] float launchSpeed = 15f;
        [SerializeField] SpriteRenderer column;
        [Tooltip("Sütunun akış kareleri (tekrar eden)")]
        [SerializeField] Sprite[] columnFrames;
        [Tooltip("Tepedeki fışkırma")]
        [SerializeField] SpriteRenderer crown;
        [SerializeField] Sprite[] crownFrames;
        [SerializeField] float frameRate = 12f;
        [Tooltip("Tepenin fokurdama hızı: akıştan yavaş")]
        [SerializeField] float crownRate = 6f;
        [Tooltip("Dipteki köpük")]
        [SerializeField] SpriteRenderer foam;
        [SerializeField] Sprite[] foamFrames;
        [Tooltip("Kabarma: 0 küçük (uyarı), 1 yükselen, 2 dağılan")]
        [SerializeField] SpriteRenderer surge;
        [SerializeField] Sprite[] surgeFrames;
        [Tooltip("Sütun bu yükseklikten kısayken kabarma görseli gösterilir")]
        [SerializeField] float surgeHeight = 2.4f;
        [SerializeField] Color bubbleColor = new Color32(0xE2, 0xF4, 0xFA, 0xFF);

        BoxCollider2D zone;
        float baseHeight, clock, height, nextBubble;

        public bool IsErupting => CycleTime >= period - eruptTime;
        public bool IsWarning => !IsErupting && CycleTime >= period - eruptTime - warnTime;
        float CycleTime => Mathf.Repeat(clock + phase * period, period);

        public void Configure(float width, float reach, float cycle, float startPhase, SpriteRenderer columnArt)
        {
            period = cycle;
            phase = startPhase;
            column = columnArt;
            zone = GetComponent<BoxCollider2D>();
            zone.isTrigger = true;
            zone.size = new Vector2(width, reach);
            zone.offset = new Vector2(0f, reach / 2f);
            // Sütunun tepesinden sonra biraz daha yükselecek kadar hız (yerçekimi: 9.81 x 3.5).
            launchSpeed = Mathf.Sqrt(2f * 34.3f * Mathf.Max(reach * 0.35f, 2f));
        }

        public void SetArt(Sprite[] flow, SpriteRenderer top, Sprite[] spray, SpriteRenderer foamArt, Sprite[] foamLoop, SpriteRenderer surgeArt, Sprite[] surgeStages)
        {
            columnFrames = flow;
            crown = top;
            crownFrames = spray;
            foam = foamArt;
            foamFrames = foamLoop;
            surge = surgeArt;
            surgeFrames = surgeStages;
        }

        void Awake()
        {
            zone = GetComponent<BoxCollider2D>();
            zone.isTrigger = true;
            baseHeight = zone.size.y;
            if (column != null)
                column.size = new Vector2(column.size.x, 0f);
        }

        void Update()
        {
            clock += Time.deltaTime;
            bool erupting = IsErupting;
            float target = erupting ? baseHeight : IsWarning ? 0.8f : 0f;
            height = Mathf.MoveTowards(height, target, (erupting ? baseHeight * 4f : baseHeight * 3f) * Time.deltaTime);
            // Uyarıda dipte köpük ve küçük kabarma; fışkırınca sütun yükselir, ucu fokurdar ve inerken uç da iner.
            bool warning = !erupting && IsWarning;
            bool flowing = height > 0.5f && !warning;
            if (column != null)
            {
                column.size = new Vector2(column.size.x, height);
                column.transform.localPosition = new Vector3(0f, height, 0f);
                column.enabled = flowing;
                if (columnFrames != null && columnFrames.Length > 0)
                    column.sprite = columnFrames[Frame(columnFrames.Length)];
            }
            if (crown != null)
            {
                crown.enabled = flowing;
                if (crownFrames != null && crownFrames.Length > 0)
                    crown.sprite = crownFrames[Mathf.FloorToInt(clock * crownRate) % crownFrames.Length];
                // Uçların genişliği farklı: her kare sütunun genişliğine oturur.
                float fit = column != null && crown.sprite != null ? column.size.x / crown.sprite.bounds.size.x : 1f;
                crown.transform.localScale = new Vector3(fit, 1f, 1f);
                crown.transform.localPosition = new Vector3(0f, height - 0.2f, 0f);
            }
            if (surge != null && surgeFrames != null && surgeFrames.Length > 0)
            {
                surge.enabled = warning;
                surge.sprite = surgeFrames[0];
                float grow = Mathf.Clamp01((CycleTime - (period - eruptTime - warnTime)) / warnTime);
                surge.transform.localScale = new Vector3(Mathf.Lerp(0.5f, 0.9f, grow), Mathf.Lerp(0.2f, 0.6f, grow), 1f);
            }
            if (foam != null)
            {
                foam.enabled = warning; // fışkırınca dipte kalmasın
                foam.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
                if (foamFrames != null && foamFrames.Length > 0)
                    foam.sprite = foamFrames[Frame(foamFrames.Length)];
            }
            if ((IsWarning || erupting) && Time.time >= nextBubble)
            {
                nextBubble = Time.time + (erupting ? 0.1f : 0.16f);
                float spread = zone.size.x * 0.4f;
                PixelBurst.Spawn(transform.position + new Vector3(Random.Range(-spread, spread), 0.15f, 0f), bubbleColor, 2, 2.2f, 0.35f);
            }
        }

        int Frame(int count) => Mathf.FloorToInt(clock * frameRate) % count;

        void OnTriggerStay2D(Collider2D other)
        {
            if (!IsErupting)
                return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null)
                return;
            // Sütun henüz oyuncuya yetişmediyse fırlatmasın.
            if (player.transform.position.y - transform.position.y <= height)
                player.Launch(launchSpeed);
        }
    }
}
