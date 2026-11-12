using System.Collections;
using DesertGecko.Collectibles;
using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Kertenkele sandığı: yalnız dille açılır (Gila'nın kendi kilidi). Dil değince sarsılır, kapağı aralanır,
    /// altınlar patlayarak saçılır, sandık açık kalır. Mermi, kazma, dinamit işlemez: üstünde kıvılcım çakar.
    /// Açılan sandık oyuncu ölse de kapanmaz. Collider tetikleyici: önünden geçilir, dil bulur, mermiyi durdurmaz.
    /// Kareler: tools/sandik_isle.py (0 kapalı, 1 sarsılıyor, 2 aralanıyor, 3 açılıyor, 4 patlama, 5 açık).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class TreasureChest : MonoBehaviour, IDamageable
    {
        [SerializeField] Sprite[] frames;
        [SerializeField] AudioClip openSound;
        [Tooltip("Sarsıntı süresi: kilit dili tanır, sandık kıpırdar")]
        [SerializeField] float rattleTime = 0.45f;
        [SerializeField] Color goldColor = new Color32(0xF4, 0xC8, 0x3C, 0xFF);
        [SerializeField] Color sparkColor = new Color32(0xFF, 0xF1, 0xB0, 0xFF);

        public bool IsAlive => !opened;

        SpriteRenderer sr;
        BoxCollider2D box;
        bool opened;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            if (frames != null && frames.Length > 0)
                sr.sprite = frames[0];
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
            StartCoroutine(Open());
            return true;
        }

        IEnumerator Open()
        {
            Vector3 home = transform.position;
            Vector2 center = box.bounds.center;
            if (openSound != null)
                OneShotSound.Play(openSound, 0.9f);

            // 1: sarsılma. Kare 1'de çizili titreşim çizgileri var; üstüne sağa sola piksel piksel sallanır.
            Show(1);
            for (float t = 0f; t < rattleTime; t += Time.deltaTime)
            {
                float step = Mathf.Floor(t * 30f) % 2f == 0f ? 1f : -1f;
                transform.position = home + new Vector3(step / 32f, 0f, 0f);
                yield return null;
            }
            transform.position = home;

            // 2-3: kapak aralanır, açılır; içinden altın ışığı.
            Show(2);
            PixelBurst.Spawn(center + Vector2.up * 0.4f, sparkColor, 5, 1.5f, 0.3f);
            yield return new WaitForSeconds(0.12f);
            Show(3);
            yield return new WaitForSeconds(0.12f);

            // 4: patlama. Altınlar fırlar, ekran hafif sarsılır.
            Show(4);
            CameraShake.AddAt(center, 0.25f);
            PixelBurst.Spawn(center + Vector2.up * 0.6f, goldColor, 18, 5f, 0.7f);
            PixelBurst.Spawn(center + Vector2.up * 0.6f, sparkColor, 10, 3.5f, 0.5f);
            var drop = GetComponent<CrystalDrop>();
            if (drop != null)
                drop.Drop();
            yield return new WaitForSeconds(0.35f);

            // 5: açık, altın dolu, öyle kalır.
            Show(5);
        }

        void Show(int frame)
        {
            if (frames != null && frame < frames.Length && frames[frame] != null)
                sr.sprite = frames[frame];
        }
    }
}
