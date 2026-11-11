using DesertGecko.Collectibles;
using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Altın damarlı kaya: yalnız kazma (yakın dövüş) ve patlama işler. İlk kazma darbesinde çatlar, ikincisinde
    /// kırılır; dinamit tek seferde kırar. Mermi, saçma, diken, dil ve kuyruk kıvılcım saçıp seker. Kırılınca taş
    /// parçaları saçılır, içinden kristaller fırlar ve kaya yolu açar. Kırılan kaya oyuncu ölse de geri gelmez.
    /// Default katmanında durur: Gila'yı ve düşmanları dövüş odası kapısı gibi durdurur.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class BreakableRock : MonoBehaviour, IDamageable
    {
        [Tooltip("Kırılması için gereken kazma darbesi; patlama her zaman tek seferde kırar")]
        [SerializeField] int hits = 2;
        [SerializeField] Sprite intactSprite;
        [SerializeField] Sprite crackedSprite;
        [SerializeField] AudioClip breakSound;
        [SerializeField] Color rockColor = new Color32(0x94, 0x43, 0x2E, 0xFF);
        [SerializeField] Color dustColor = new Color32(0xCF, 0x8D, 0x50, 0xFF);
        [SerializeField] Color goldColor = new Color32(0xE8, 0xB8, 0x48, 0xFF);

        public bool IsAlive => hitsLeft > 0;

        SpriteRenderer sr;
        BoxCollider2D box;
        int hitsLeft;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            box = GetComponent<BoxCollider2D>();
            hitsLeft = Mathf.Max(1, hits);
            if (intactSprite != null)
                sr.sprite = intactSprite;
        }

        public bool TakeDamage(DamageInfo info)
        {
            if (!IsAlive)
                return false;
            Vector2 center = box.bounds.center;
            if (info.Kind != DamageKind.Melee && info.Kind != DamageKind.Explosion)
            {
                // Taş: kıvılcım saçar, darbe işlemez.
                PixelBurst.Spawn(center + (Vector2)(-info.Direction.normalized * box.bounds.extents.x * 0.8f), Color.white, 3, 2.5f, 0.15f);
                return false;
            }

            hitsLeft = info.Kind == DamageKind.Explosion ? 0 : hitsLeft - 1;
            if (hitsLeft > 0)
            {
                if (crackedSprite != null)
                    sr.sprite = crackedSprite;
                PixelBurst.Spawn(center, dustColor, 8, 2.5f, 0.4f);
                PixelBurst.Spawn(center, rockColor, 5, 3f, 0.35f);
                CameraShake.AddAt(center, 0.15f);
                return true;
            }
            Break(center);
            return true;
        }

        void Break(Vector2 center)
        {
            PixelBurst.Spawn(center, rockColor, 18, 5f, 0.6f);
            PixelBurst.Spawn(center, dustColor, 16, 3.5f, 0.7f);
            PixelBurst.Spawn(center, goldColor, 8, 4f, 0.5f);
            CameraShake.AddAt(center, 0.3f);
            if (breakSound != null)
                OneShotSound.Play(breakSound, 0.8f);
            var drop = GetComponent<CrystalDrop>();
            if (drop != null)
                drop.Drop();
            sr.enabled = false;
            box.enabled = false;
        }
    }
}
