using System.Collections.Generic;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DesertGecko.Collectibles
{
    /// <summary>
    /// Zümrüt Kaktüs Böceği: tombul, etli, karnı neon yeşil parlayan çöl böceği. Sadece dil ile yakalanır.
    /// Havada sinüs dalgasıyla süzülür, kanatları hızla çırpılır; şişkin karnı nefes alır gibi zonklar
    /// (parıltı ve varsa 2D ışık birlikte). Gila'ya doğru bakar.
    /// Yakalanınca: neon kıvılcımlar saçılır, "şlap" sesi; böcek dilin ucunda ağza çekilir (BugSwallow),
    /// yutulunca Gila'dan yeşil şifa halkası yayılır. Can ve ısı yakalandığı an verilir: jalapeño gibi içini ısıtır.
    /// </summary>
    public class Bug : MonoBehaviour
    {
        [SerializeField] int healAmount = 2;
        [SerializeField] float heatAmount = 0.45f;

        [Header("Yakalama çemberi")]
        [Tooltip("Görünmez çember: dil bu çembere değerse böceğe yapışıp kapar, tam isabet gerekmez")]
        [SerializeField] float catchRadius = 0.9f;

        [Header("Süzülme")]
        [Tooltip("Yavaş, iç içe geçmiş dalgalar: sekiz çizer gibi süzülür, ileri geri titremez")]
        [SerializeField] float hoverHeight = 0.16f;
        [SerializeField] float hoverPeriod = 2.6f;
        [SerializeField] float driftWidth = 0.22f;
        [SerializeField] float driftPeriod = 5.2f;
        [Tooltip("Gila bu kadar yana geçmeden yönünü değiştirmez: tam altındayken sağa sola dönüp durmasın")]
        [SerializeField] float turnDeadZone = 0.6f;

        [Header("Kareler")]
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] Sprite[] hoverFrames;
        [Tooltip("8 karelik döngü tek bir yavaş kanat çırpışı: saniyede bir çırpış")]
        [SerializeField] float wingFps = 8f;
        [Tooltip("Yutulurken oynar: kıvılcımlanıp dağılır")]
        [SerializeField] Sprite[] swallowFrames;

        [Header("Zonklama")]
        [SerializeField] SpriteRenderer glow;
        [SerializeField] float pulseSpeed = 2.4f;
        [SerializeField] Vector2 glowAlpha = new Vector2(0.25f, 0.6f);
        [SerializeField] Vector2 glowScale = new Vector2(0.85f, 1.15f);
        [Tooltip("Varsa ışık da zonklar (gece bölümlerinde eklenen nokta ışık)")]
        [SerializeField] Vector2 lightPulse = new Vector2(0.6f, 1.25f);

        [Header("Yakalama")]
        [SerializeField] Color burstColor = new Color32(0x5C, 0xFF, 0x8A, 0xFF);
        [SerializeField] Color pollenColor = new Color32(0xC8, 0xFF, 0x6A, 0xFF);
        [SerializeField] Sprite ringSprite;
        [Tooltip("Halka ve yutulan böcek gece de parlasın: ışıktan etkilenmeyen malzeme")]
        [SerializeField] Material unlitMaterial;
        [SerializeField] Color ringColor = new Color32(0x6C, 0xFF, 0x96, 0xFF);
        [SerializeField] AudioClip slapClip;
        [SerializeField] AudioClip gulpClip;

        Vector3 restPosition;
        Vector3 glowBaseScale = Vector3.one;
        Color glowBaseColor = Color.white;
        Light2D bugLight;
        float lightBase;
        float phase;
        PlayerHealth player;

        void Awake()
        {
            restPosition = transform.position;
            phase = Random.value * 10f;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (glow != null)
            {
                glowBaseScale = glow.transform.localScale;
                glowBaseColor = glow.color;
            }
        }

        void Start()
        {
            player = FindAnyObjectByType<PlayerHealth>();
            // Işık bölüm kurulurken eklenir (LevelPainter.Firefly); prefab'da olmayabilir.
            bugLight = GetComponentInChildren<Light2D>();
            if (bugLight != null)
                lightBase = bugLight.intensity;
        }

        static readonly List<Bug> active = new List<Bug>();

        /// <summary>Sahnedeki yakalanabilir böcekler (dil bunlar arasında arar).</summary>
        public static IReadOnlyList<Bug> Active => active;

        public float CatchRadius => catchRadius;

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void Update()
        {
            transform.position = restPosition + HoverOffset(Time.time + phase);

            if (spriteRenderer != null)
            {
                if (hoverFrames != null && hoverFrames.Length > 0)
                    spriteRenderer.sprite = hoverFrames[Mathf.FloorToInt((Time.time + phase) * wingFps) % hoverFrames.Length];
                if (player != null)
                {
                    float dx = player.transform.position.x - transform.position.x;
                    if (Mathf.Abs(dx) > turnDeadZone)
                        spriteRenderer.flipX = dx < 0f; // kareler sağa bakar
                }
            }

            // Karın nefes alır gibi zonklar: 0..1 arası yumuşak dalga.
            float pulse = 0.5f + 0.5f * Mathf.Sin((Time.time + phase) * pulseSpeed * Mathf.PI * 2f * 0.5f);
            if (glow != null)
            {
                Color c = glowBaseColor;
                c.a = Mathf.Lerp(glowAlpha.x, glowAlpha.y, pulse);
                glow.color = c;
                glow.transform.localScale = glowBaseScale * Mathf.Lerp(glowScale.x, glowScale.y, pulse);
            }
            if (bugLight != null)
                bugLight.intensity = lightBase * Mathf.Lerp(lightPulse.x, lightPulse.y, pulse);
        }

        /// <summary>
        /// Yavaş ve yumuşak süzülme: dikeyde nefes gibi inip kalkar, yatayda ondan iki kat yavaş kayar.
        /// İkinci, uyumsuz bir dalga hareketi her turda biraz değiştirir: mekanik git-gel gibi durmaz.
        /// </summary>
        Vector3 HoverOffset(float t)
        {
            const float Tau = Mathf.PI * 2f;
            float y = Mathf.Sin(t * Tau / hoverPeriod) * hoverHeight
                + Mathf.Sin(t * Tau / (hoverPeriod * 2.7f)) * hoverHeight * 0.35f;
            float x = Mathf.Sin(t * Tau / driftPeriod) * driftWidth;
            return new Vector3(x, y, 0f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, catchRadius);
        }

        /// <returns>Yakalandıysa true.</returns>
        public bool Catch(PlayerHealth health) => Catch(health, health.transform.position + Vector3.up * 0.6f);

        /// <param name="mouth">Dilin çıktığı nokta: böcek oraya çekilip yutulur.</param>
        /// <returns>Yakalandıysa true.</returns>
        public bool Catch(PlayerHealth health, Vector2 mouth)
        {
            if (!gameObject.activeSelf)
                return false;
            health.Heal(healAmount);
            var heat = health.GetComponent<BodyHeat>();
            if (heat != null)
                heat.AddHeat(heatAmount);

            // Kaptığı an: böceğin olduğu yerde neon kıvılcımlar ve kaktüs polenleri.
            PixelBurst.Spawn(transform.position, burstColor, 10, 3.2f, 0.4f);
            PixelBurst.Spawn(transform.position, pollenColor, 6, 1.6f, 0.6f);
            OneShotSound.Play(slapClip);

            BugSwallow.Spawn(transform.position, mouth, health.transform, swallowFrames,
                spriteRenderer != null && spriteRenderer.flipX, spriteRenderer != null ? spriteRenderer.sortingOrder : 5,
                ringSprite, ringColor, burstColor, gulpClip, unlitMaterial);
            gameObject.SetActive(false);
            return true;
        }
    }
}
