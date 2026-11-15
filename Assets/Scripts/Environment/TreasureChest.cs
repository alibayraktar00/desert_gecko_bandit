using System.Collections;
using DesertGecko.Collectibles;
using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Kertenkele sandığı: yalnız dille açılır (Gila'nın kendi kilidi). Hemen açılmaz, merak uyandırarak açılır:
    /// kilit parlar ve bir an susar; sandık önce hafif, sonra giderek sert titrer, kapağın aralığından ışık ve
    /// kıvılcım sızar; kapak bir aralanır, içi bir an görünür ve geri kapanır; son sarsıntıdan sonra kapak fırlar,
    /// altın ışığı patlar, altınlar saçılır. Açık sandıkta altınlar pırıldamaya devam eder.
    /// Kapalı dururken arada bir kilidinde parıltı çakar; Gila yaklaşınca üstünde "J + dil" ipucu belirir.
    /// Dil kilide yapışır ve gerilim boyunca orada kalıp çekiştirir (TongueAttack, LockPoint ve TeaseDuration).
    /// Mermi, kazma, dinamit işlemez: üstünde kıvılcım çakar. Açılan sandık oyuncu ölse de kapanmaz.
    /// Collider tetikleyici: önünden geçilir, dil bulur, mermiyi durdurmaz.
    /// Kareler: tools/sandik_isle.py (0 kapalı, 1 kilitte parıltı, 2 aralık, 3 açık, 4 ışık patlaması, 5-7 pırıltı).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class TreasureChest : MonoBehaviour, IDamageable
    {
        [SerializeField] Sprite[] frames;
        [Tooltip("Gerilim: kilit tıkırtısı, titreme, aralanıp kapanma (TeaseDuration ile aynı uzunlukta)")]
        [SerializeField] AudioClip teaseSound;
        [Tooltip("Açılış: kapak fırlar, altınlar dökülür")]
        [SerializeField] AudioClip openSound;
        [Tooltip("İçerideki altının ışığı: aralıktan sızar, açılınca patlar, sonra sıcak bir parıltı olarak kalır")]
        [SerializeField] Light2D glow;
        [SerializeField] float glowPeak = 2.2f;
        [SerializeField] float glowRest = 0.7f;
        [Tooltip("Gila yaklaşınca sandığın üstünde beliren ipucu (J tuşu ve dil)")]
        [SerializeField] SpriteRenderer hint;
        [SerializeField] float hintRange = 4f;

        [Header("Gerilim (sn)")]
        [Tooltip("Kilit dili tanır, parlar: sessiz bir an")]
        [SerializeField] float lockPause = 0.4f;
        [Tooltip("Hafiften serte titreme, aralıktan ışık sızar")]
        [SerializeField] float rattleTime = 1.0f;
        [Tooltip("Kapak aralanır, içi bir an görünür")]
        [SerializeField] float peekTime = 0.35f;
        [Tooltip("Kapak geri düşer; kısa sessizlik")]
        [SerializeField] float dropTime = 0.3f;
        [Tooltip("Son ve en sert sarsıntı")]
        [SerializeField] float finalRattleTime = 0.35f;

        [Header("Kıvılcım ve renk")]
        [SerializeField] Color goldColor = new Color32(0xF4, 0xC8, 0x3C, 0xFF);
        [SerializeField] Color sparkColor = new Color32(0xFF, 0xF1, 0xB0, 0xFF);
        [SerializeField] Color dustColor = new Color32(0x8A, 0x6E, 0x4E, 0xFF);
        [Tooltip("Kapalı sandığın kilidinde parıltının çakma aralığı (sn)")]
        [SerializeField] Vector2 glintEvery = new Vector2(2.5f, 4.5f);
        [SerializeField] float sparkleFps = 6f;

        public bool IsAlive => !opened;
        /// <summary>Gerilim + açılışın toplam süresi (ses bu süreye göre üretildi).</summary>
        public float TeaseDuration => lockPause + rattleTime + peekTime + dropTime + finalRattleTime;
        /// <summary>Kilidin dünyadaki yeri; sandık titrerken onunla oynar (dilin ucu buraya yapışır).</summary>
        public Vector2 LockPoint => (Vector2)transform.position + Vector2.up * (box.offset.y + box.size.y * 0.12f);

        const int Closed = 0, Glint = 1, Ajar = 2, Open = 3, Burst = 4, SparkleFirst = 5;
        const float Pixel = 1f / 32f;

        SpriteRenderer sr;
        BoxCollider2D box;
        Vector3 home;
        bool opened, revealed;
        float nextGlintAt, glintUntil, hintAlpha;
        PlayerHealth player;
        Vector3 hintHome;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            home = transform.position;
            Show(Closed);
            SetGlow(0f);
            nextGlintAt = Time.time + Random.Range(0.5f, glintEvery.y);
            if (hint != null)
            {
                hintHome = hint.transform.localPosition;
                SetHint(0f);
            }
        }

        /// <summary>Gila yakındaysa ve sandık kapalıysa ipucu yumuşakça belirir, hafifçe sallanır.</summary>
        void UpdateHint()
        {
            if (hint == null)
                return;
            if (player == null)
                player = FindAnyObjectByType<PlayerHealth>();
            bool near = !opened && player != null && player.IsAlive
                && Vector2.Distance(player.transform.position, home) <= hintRange;
            hintAlpha = Mathf.MoveTowards(hintAlpha, near ? 1f : 0f, Time.deltaTime * 4f);
            SetHint(hintAlpha);
            hint.transform.localPosition = hintHome + Vector3.up * (Mathf.Round(Mathf.Sin(Time.time * 4f) * 1.5f) * Pixel);
        }

        void SetHint(float alpha)
        {
            Color c = hint.color;
            c.a = alpha;
            hint.color = c;
            hint.enabled = alpha > 0.01f;
        }

        void Update()
        {
            UpdateHint();
            if (revealed)
            {
                // Açık sandık: altınlar pırıldar, ışık hafifçe nefes alır.
                int count = frames != null ? frames.Length - SparkleFirst : 0;
                if (count > 0)
                    Show(SparkleFirst + Mathf.FloorToInt(Time.time * sparkleFps) % count);
                SetGlow(glowRest * (0.85f + 0.15f * Mathf.Sin(Time.time * 3f)));
                return;
            }
            if (opened)
                return;
            // Kapalı: arada bir kilidinde parıltı çakar.
            if (Time.time >= nextGlintAt)
            {
                glintUntil = Time.time + 0.18f;
                nextGlintAt = Time.time + Random.Range(glintEvery.x, glintEvery.y);
            }
            Show(Time.time < glintUntil ? Glint : Closed);
        }

        public bool TakeDamage(DamageInfo info)
        {
            if (opened)
                return false;
            if (info.Kind != DamageKind.Tongue)
            {
                // Kilit yalnız dili tanır: diğer darbeler kıvılcım çakıp seker.
                PixelBurst.Spawn(box.bounds.center, Color.white, 3, 2.5f, 0.15f);
                return false;
            }
            opened = true;
            StartCoroutine(OpenRoutine());
            return true;
        }

        Vector2 Seam => (Vector2)home + Vector2.up * (box.offset.y + box.size.y * 0.15f); // kapağın aralığı
        Vector2 Keyhole => (Vector2)home + Vector2.up * (box.offset.y + box.size.y * 0.05f);

        IEnumerator OpenRoutine()
        {
            if (teaseSound != null)
                OneShotSound.Play(teaseSound, 0.9f);

            // 1: kilit dili tanır. Parlar, sonra sessizlik.
            Show(Glint);
            PixelBurst.Spawn(Keyhole, sparkColor, 4, 1.2f, 0.35f);
            yield return new WaitForSeconds(lockPause);

            // 2: titreme hafiften serte; aralıktan ışık ve kıvılcım sızar, sıklaşır.
            Show(Closed);
            float nextLeak = 0f;
            for (float t = 0f; t < rattleTime; t += Time.deltaTime)
            {
                float k = t / rattleTime;
                Shake(t, Mathf.Lerp(0.5f, 1.6f, k), Mathf.Lerp(10f, 28f, k), k > 0.5f);
                SetGlow(glowRest * 0.6f * k);
                if (t >= nextLeak)
                {
                    nextLeak = t + Mathf.Lerp(0.3f, 0.07f, k);
                    PixelBurst.Spawn(Seam + new Vector2(Random.Range(-0.6f, 0.6f), 0f), goldColor, 1, 1.2f, 0.4f);
                }
                yield return null;
            }
            transform.position = home;

            // 3: kapak aralanır, içi bir an görünür...
            Show(Ajar);
            SetGlow(glowRest);
            PixelBurst.Spawn(Seam, sparkColor, 6, 1.8f, 0.35f);
            yield return new WaitForSeconds(peekTime);

            // ...ve geri düşer. Toz kalkar, kısa sessizlik.
            Show(Closed);
            SetGlow(glowRest * 0.25f);
            transform.position = home + Vector3.down * Pixel;
            PixelBurst.Spawn((Vector2)home + Vector2.up * 0.1f, dustColor, 5, 1.5f, 0.3f);
            yield return new WaitForSeconds(0.06f);
            transform.position = home;
            yield return new WaitForSeconds(dropTime - 0.06f);

            // 4: son ve en sert sarsıntı.
            for (float t = 0f; t < finalRattleTime; t += Time.deltaTime)
            {
                Shake(t, 2f, 34f, true);
                SetGlow(glowRest * (0.4f + 0.6f * t / finalRattleTime));
                yield return null;
            }
            transform.position = home;

            // 5: kapak fırlar, altın ışığı patlar, altınlar saçılır.
            if (openSound != null)
                OneShotSound.Play(openSound, 0.9f);
            Show(Ajar);
            yield return new WaitForSeconds(0.05f);
            Show(Open);
            yield return new WaitForSeconds(0.07f);
            Show(Burst);
            Vector2 center = Seam + Vector2.up * 0.3f;
            CameraShake.AddAt(center, 0.3f);
            PixelBurst.Spawn(center, goldColor, 22, 5.5f, 0.75f);
            PixelBurst.Spawn(center, sparkColor, 14, 3.5f, 0.6f);
            var drop = GetComponent<CrystalDrop>();
            if (drop != null)
                drop.Drop();
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                SetGlow(Mathf.Lerp(glowPeak, glowRest, t / 0.45f));
                yield return null;
            }

            // 6: açık kalır, altınlar pırıldar (Update).
            revealed = true;
        }

        /// <summary>Piksel piksel sağa sola titrer; sert evrede bir de zıplar.</summary>
        void Shake(float t, float pixels, float rate, bool hop)
        {
            float side = Mathf.Floor(t * rate) % 2f == 0f ? 1f : -1f;
            float up = hop && Mathf.Floor(t * rate * 0.5f) % 3f == 0f ? Pixel : 0f;
            transform.position = home + new Vector3(Mathf.Round(side * pixels) * Pixel, up, 0f);
        }

        void SetGlow(float intensity)
        {
            if (glow == null)
                return;
            glow.intensity = intensity;
            glow.enabled = intensity > 0.01f;
        }

        void Show(int frame)
        {
            if (frames != null && frame < frames.Length && frames[frame] != null)
                sr.sprite = frames[frame];
        }
    }
}
