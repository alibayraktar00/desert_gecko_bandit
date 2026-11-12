using System.Collections;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Tavandan inen ağır ahşap kapı. İnerken önce zinciri boşalır ve tavandan taş dökülür (kısa titreme),
    /// sonra yerçekimiyle hızlanıp yere çakılır: ekran sarsılır, toz kalkar, gümbürtü mağarada yankılanır.
    /// Açılırken makara takırtısıyla yavaşça tavanın arkasına çekilir. Dövüş odası (ArenaGate) yönetir.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class CeilingGate : MonoBehaviour
    {
        enum State { Open, Shudder, Falling, Closed, Rising }

        [Tooltip("İnmeden önceki titreme: zincir boşalır, tavandan taş dökülür")]
        [SerializeField] float shudderTime = 0.35f;
        [SerializeField] float gravity = 55f;
        [SerializeField] float riseSpeed = 2.4f;
        [Tooltip("Yere çarpınca ekran sarsıntısı (0-1)")]
        [SerializeField] float slamShake = 0.7f;
        [SerializeField] AudioClip releaseSound;
        [SerializeField] AudioClip slamSound;
        [SerializeField] AudioClip riseSound;
        [SerializeField] Sprite dustSprite;
        [SerializeField] Color debrisColor = new Color32(0x5E, 0x3B, 0x26, 0xFF);
        [SerializeField] Color dustColor = new Color32(0xD8, 0xB8, 0x98, 0xFF);

        const float HearingRange = 26f; // bundan uzaktaki kapı duyulmaz

        State state = State.Open;
        Vector3 down;
        float openHeight = 9f;
        float velocity, timer, nextKick;
        float floorOffset, roofOffset, halfWidth; // kapalıyken kanat konumuna göre
        AudioSource loop;
        Coroutine bounce;
        bool initialized;

        public bool IsClosed => state == State.Closed;

        public void SetOpenHeight(float height)
        {
            Init();
            openHeight = height;
        }

        /// <summary>Kapıyı efektsiz, anında açık ya da kapalı konuma koyar (sahne başı, oyuncu yeniden doğunca).</summary>
        public void Snap(bool open)
        {
            Init();
            if (bounce != null)
                StopCoroutine(bounce); // toz kendi kendine söner, yarıda kesilmesin
            StopLoop();
            velocity = 0f;
            state = open ? State.Open : State.Closed;
            transform.position = open ? OpenPosition : down;
        }

        /// <summary>Kapıyı indirir. Tam açıkken önce titrer; kalkarken yarıda kalırsa hemen düşer.</summary>
        public void Close()
        {
            Init();
            if (state == State.Closed || state == State.Shudder || state == State.Falling)
                return;
            StopLoop();
            velocity = 0f;
            timer = 0f;
            nextKick = 0f;
            if (state == State.Open)
            {
                state = State.Shudder;
                PlaySound(releaseSound, 0.9f);
                CameraShake.AddAt(transform.position, 0.2f, HearingRange);
            }
            else
            {
                state = State.Falling;
            }
        }

        /// <summary>Kapıyı makara takırtısıyla tavanın arkasına çeker.</summary>
        public void Open()
        {
            Init();
            if (state == State.Open || state == State.Rising)
                return;
            if (bounce != null)
                StopCoroutine(bounce);
            state = State.Rising;
            timer = 0f;
            nextKick = 0f;
            CameraShake.AddAt(Bottom, 0.3f, HearingRange);
            StartLoop();
        }

        Vector3 OpenPosition => down + Vector3.up * openHeight;
        Vector2 Bottom => new Vector2(down.x, transform.position.y + floorOffset);
        float HeaderBottom => down.y + roofOffset; // kapalı kanadın üstü: taş sütunun alt kenarı

        void Awake() => Init();

        void Init()
        {
            if (initialized)
                return;
            initialized = true;
            down = transform.position; // sahnede kapalı konumda kurulur
            var col = GetComponent<BoxCollider2D>();
            floorOffset = col.offset.y - col.size.y / 2f;
            roofOffset = col.offset.y + col.size.y / 2f;
            halfWidth = col.size.x / 2f;
        }

        void Update()
        {
            switch (state)
            {
                case State.Shudder: UpdateShudder(); break;
                case State.Falling: UpdateFalling(); break;
                case State.Rising: UpdateRising(); break;
            }
        }

        void UpdateShudder()
        {
            timer += Time.deltaTime;
            // Zincir boşaldıkça kanat birkaç piksel kayar ve yana titrer; tavandan taş dökülür.
            float slip = Mathf.Min(timer / shudderTime, 1f) * 0.18f;
            float jitter = Mathf.Sin(timer * 90f) * 0.035f;
            transform.position = OpenPosition + new Vector3(jitter, -slip, 0f);
            if (timer >= nextKick)
            {
                nextKick = timer + 0.08f;
                Pebbles(new Vector2(down.x + Random.Range(-halfWidth, halfWidth), HeaderBottom), 3, 2f);
            }
            if (timer >= shudderTime)
            {
                transform.position = OpenPosition + Vector3.down * slip;
                state = State.Falling;
                velocity = 1.5f;
            }
        }

        void UpdateFalling()
        {
            velocity += gravity * Time.deltaTime;
            Vector3 p = transform.position;
            p.y -= velocity * Time.deltaTime;
            if (p.y <= down.y)
            {
                transform.position = down;
                Land();
                return;
            }
            transform.position = p;
        }

        void UpdateRising()
        {
            transform.position = Vector3.MoveTowards(transform.position, OpenPosition, riseSpeed * Time.deltaTime);
            timer += Time.deltaTime;
            // Her makara dişinde hafif bir sarsıntı ve tavandan biraz kum.
            if (timer >= nextKick)
            {
                nextKick = timer + 0.45f;
                CameraShake.AddAt(Bottom, 0.12f, HearingRange);
                Pebbles(new Vector2(down.x + Random.Range(-halfWidth, halfWidth), HeaderBottom), 2, 1.5f);
            }
            if (transform.position == OpenPosition)
            {
                state = State.Open;
                StopLoop();
                CameraShake.AddAt(Bottom, 0.15f, HearingRange);
            }
        }

        void Land()
        {
            state = State.Closed;
            Vector2 floor = Bottom;
            CameraShake.AddAt(floor, slamShake, HearingRange);
            PlaySound(slamSound, 1f);
            Pebbles(floor + Vector2.left * halfWidth, 8, 5f);
            Pebbles(floor + Vector2.right * halfWidth, 8, 5f);
            Pebbles(new Vector2(down.x, HeaderBottom), 6, 2.5f); // sarsıntıyla tavan da dökülür
            if (dustSprite != null)
            {
                StartCoroutine(Dust(floor, false));
                StartCoroutine(Dust(floor, true));
            }
            bounce = StartCoroutine(Bounce());
        }

        // Ağır kanat çarpınca bir parça geri seker ve oturur.
        IEnumerator Bounce()
        {
            const float duration = 0.18f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.position = down + Vector3.up * (Mathf.Sin(t / duration * Mathf.PI) * 0.1f);
                yield return null;
            }
            transform.position = down;
        }

        IEnumerator Dust(Vector2 floor, bool mirrored)
        {
            var go = new GameObject("KapiTozu");
            go.transform.position = floor;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dustSprite;
            sr.sortingOrder = 6; // kanadın önünde
            sr.flipX = mirrored;
            Color c = dustColor;
            float drift = mirrored ? -0.25f : 0.25f;
            const float duration = 0.9f;
            for (float t = 0f; t < duration && go != null; t += Time.deltaTime)
            {
                float k = t / duration;
                float grow = 0.75f + 0.55f * (1f - (1f - k) * (1f - k));
                go.transform.localScale = new Vector3(grow, grow * 0.85f, 1f);
                go.transform.position = floor + new Vector2(drift * k, 0.35f * k);
                c.a = (mirrored ? 0.7f : 0.9f) * (1f - k * k);
                sr.color = c;
                yield return null;
            }
            if (go != null)
                Destroy(go);
        }

        void Pebbles(Vector2 at, int count, float speed)
        {
            PixelBurst.Spawn(at, debrisColor, count, speed, 0.6f);
        }

        float Volume()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return 1f;
            float distance = Vector2.Distance(cam.transform.position, transform.position);
            return 1f - Mathf.Clamp01(distance / HearingRange);
        }

        void PlaySound(AudioClip clip, float volume)
        {
            float v = volume * Volume();
            if (v > 0.01f)
                OneShotSound.Play(clip, v);
        }

        void StartLoop()
        {
            if (riseSound == null)
                return;
            if (loop == null)
            {
                loop = gameObject.AddComponent<AudioSource>();
                loop.playOnAwake = false;
                loop.loop = true;
                loop.spatialBlend = 0f;
            }
            loop.clip = riseSound;
            loop.volume = 0.7f * Volume();
            loop.Play();
        }

        void StopLoop()
        {
            if (loop != null)
                loop.Stop();
        }

        void LateUpdate()
        {
            if (loop != null && loop.isPlaying)
                loop.volume = 0.7f * Volume();
        }
    }
}
