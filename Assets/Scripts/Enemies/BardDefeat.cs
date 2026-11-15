using System.Collections;
using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Bard'ın epik yenilgisi: savrulmaz, olduğu yerde ağır ağır çöker.
    /// Son darbede zaman bir an durur, sonra ağır çekime girer; ağır darbe karesi, ekran sarsılır, mor ve kızıl kıvılcım
    /// saçılır. Canavar dizlerinin üstüne çöker, mor bir dumanın içinde insana döner; öne eğilir, yere yığılır, serilir.
    /// Gövdesinden mor ruh zerreleri yükselir, sonunda ceset yavaşça solar. Kareler arası kısa bir geçişle (iki
    /// görselin karışması) akar, kare kare zıplamaz. Düşman kapanmış olduğu için kendi başına yaşar.
    /// </summary>
    public class BardDefeat : MonoBehaviour
    {
        const float Blend = 0.14f;          // iki kare arası geçiş
        const float SlowScale = 0.4f;       // ağır çekim
        const float SlowTime = 1.1f;        // ağır çekimin gerçek süresi
        const float SlowRecover = 0.5f;     // normale dönüş
        const float FadeTime = 1.1f;
        const float SlideDistance = 0.35f;  // darbeyle geriye doğru kayma (yerde kalır)

        static readonly Color Purple = new Color32(0x8E, 0x5A, 0xD6, 0xFF);
        static readonly Color Crimson = new Color32(0xD8, 0x3A, 0x4A, 0xFF);
        static readonly Color Dust = new Color32(0xB0, 0x92, 0x6A, 0xFF);

        /// <summary>Gövde yere serildiğinde (gitar oradan çıkar). Uçuruma düşmez, her zaman çağrılır.</summary>
        public event System.Action<Vector2> Finished;

        SpriteRenderer current, next;
        Sprite[] frames;
        float[] durations;
        int formChange, soulFrame, downFrame;
        int knockDirection;

        /// <param name="frames">Sırayla oynayacak kareler.</param>
        /// <param name="durations">Her karenin süresi (ağır çekimle uzar).</param>
        /// <param name="formChange">Bu kareye geçerken canavar insana döner: mor duman.</param>
        /// <param name="downFrame">Gövde bu karede yere serilmiş sayılır: Finished.</param>
        /// <param name="soulFrame">Bu kareden sonra ruh zerreleri yükselir.</param>
        public static BardDefeat Spawn(Vector2 position, Sprite[] frames, float[] durations, bool flipX, int order, int knockDirection,
            int formChange, int downFrame, int soulFrame)
        {
            var go = new GameObject("BardYenilgi");
            go.transform.position = SnapToGround(position);
            var effect = go.AddComponent<BardDefeat>();
            effect.frames = frames;
            effect.durations = durations;
            effect.knockDirection = knockDirection;
            effect.formChange = formChange;
            effect.downFrame = downFrame;
            effect.soulFrame = soulFrame;
            effect.current = Layer(go.transform, "Govde", flipX, order);
            effect.next = Layer(go.transform, "Gecis", flipX, order + 1);
            effect.current.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            effect.StartCoroutine(effect.Play());
            return effect;
        }

        static SpriteRenderer Layer(Transform parent, string name, bool flipX, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.flipX = flipX;
            sr.sortingOrder = order;
            return sr;
        }

        static Vector2 SnapToGround(Vector2 position)
        {
            RaycastHit2D hit = Physics2D.Raycast(position + Vector2.up * 0.3f, Vector2.down, 3f, GameLayers.GroundMask);
            return hit ? new Vector2(position.x, hit.point.y) : position;
        }

        Vector2 Chest => (Vector2)transform.position + Vector2.up * 0.9f;

        IEnumerator Play()
        {
            // Son darbe: an durur, sonra ağır çekim.
            HitStop.Request(0.12f);
            StartCoroutine(SlowMotion());
            CameraShake.AddAt(transform.position, 0.45f);
            PixelBurst.Spawn(Chest, Purple, 16, 5.5f, 0.7f);
            PixelBurst.Spawn(Chest, Crimson, 10, 4f, 0.55f);
            StartCoroutine(Slide());

            int count = frames != null ? frames.Length : 0;
            float nextSoulAt = 0f;
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    if (i == formChange)
                    {
                        // Canavar insana döner: mor duman kabarır.
                        PixelBurst.Spawn(Chest, Purple, 20, 3f, 0.9f);
                        PixelBurst.Spawn(Chest + Vector2.up * 0.4f, new Color(0.3f, 0.2f, 0.35f), 12, 1.6f, 1.1f);
                    }
                    yield return CrossFade(frames[i], i == formChange ? Blend * 3f : Blend);
                    if (i == downFrame)
                    {
                        CameraShake.AddAt(transform.position, 0.2f);
                        PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 0.1f, Dust, 12, 2.5f, 0.5f);
                        Finished?.Invoke(transform.position);
                    }
                }
                float hold = durations != null && i < durations.Length ? durations[i] : 0.5f;
                for (float t = 0f; t < hold; t += Time.deltaTime)
                {
                    if (i >= soulFrame && Time.time >= nextSoulAt)
                    {
                        nextSoulAt = Time.time + 0.12f;
                        PixelBurst.Spawn((Vector2)transform.position + new Vector2(Random.Range(-0.5f, 0.5f), 0.4f), Purple, 1, 1.4f, 0.9f);
                    }
                    yield return null;
                }
            }

            // Ceset yavaşça solar, son toz.
            PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 0.2f, Purple, 10, 1.5f, 1f);
            for (float t = 0f; t < FadeTime; t += Time.deltaTime)
            {
                SetAlpha(current, 1f - t / FadeTime);
                yield return null;
            }
            Destroy(gameObject);
        }

        IEnumerator CrossFade(Sprite sprite, float duration)
        {
            next.sprite = sprite;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                SetAlpha(next, k);
                SetAlpha(current, 1f - k * 0.6f); // eski kare tamamen silinmeden yenisi belirir
                yield return null;
            }
            current.sprite = sprite;
            SetAlpha(current, 1f);
            SetAlpha(next, 0f);
        }

        /// <summary>Darbeyle geriye doğru yumuşakça kayar; ayağı yerde kalır.</summary>
        IEnumerator Slide()
        {
            Vector3 from = transform.position;
            Vector3 to = from + Vector3.right * (knockDirection * SlideDistance);
            if (Physics2D.Raycast((Vector2)from + Vector2.up * 0.5f, Vector2.right * knockDirection, SlideDistance + 0.4f, GameLayers.GroundMask))
                yield break; // arkası duvar
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float k = 1f - (1f - t / 0.6f) * (1f - t / 0.6f);
                transform.position = Vector3.Lerp(from, to, k);
                yield return null;
            }
            transform.position = to;
        }

        /// <summary>Gerçek zamanla kısa bir ağır çekim; menü ya da başka bir yavaşlatma zamanı değiştirdiyse karışmaz.</summary>
        IEnumerator SlowMotion()
        {
            yield return new WaitForSecondsRealtime(0.13f); // önce HitStop'un duraklaması biter
            if (!Mathf.Approximately(Time.timeScale, 1f))
                yield break;
            Time.timeScale = SlowScale;
            float end = Time.unscaledTime + SlowTime;
            while (Time.unscaledTime < end)
            {
                if (!Mathf.Approximately(Time.timeScale, SlowScale))
                    yield break; // menü açıldı
                yield return null;
            }
            for (float t = 0f; t < SlowRecover; t += Time.unscaledDeltaTime)
            {
                if (Time.timeScale <= 0f)
                    yield break;
                Time.timeScale = Mathf.Lerp(SlowScale, 1f, t / SlowRecover);
                yield return null;
            }
            if (Time.timeScale > 0f)
                Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            // Ağır çekimin ortasında silinirse (sahne değişti, oyuncu doğdu) zaman yavaş kalmasın.
            if (Time.timeScale > 0f && Time.timeScale < 1f)
                Time.timeScale = 1f;
        }

        static void SetAlpha(SpriteRenderer sr, float alpha)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}
