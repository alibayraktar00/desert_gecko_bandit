using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Yolcu vagonu: Gila içerideyken dış kaplama saydamlaşır, arkasındaki iç görünüş (koltuklar, lambalar) çıkar.
    /// Bina içlerindeki gibi: dışarı çıkınca kaplama geri gelir.
    /// </summary>
    public class TrainCarInterior : MonoBehaviour
    {
        [SerializeField] SpriteRenderer outside;
        [Tooltip("İç hacim, vagonun yerel koordinatında (alt orta = 0,0)")]
        [SerializeField] Vector2 insideMin = new Vector2(-4.5f, 0.7f);
        [SerializeField] Vector2 insideMax = new Vector2(4.5f, 3.4f);
        [SerializeField] float fadeSpeed = 6f;
        [SerializeField] float insideAlpha = 0.08f;

        static Transform player;
        float alpha = 1f;

        void Update()
        {
            if (outside == null)
                return;
            if (player == null)
            {
                var p = FindAnyObjectByType<PlayerController>();
                if (p == null)
                    return;
                player = p.transform;
            }
            Vector2 local = transform.InverseTransformPoint(player.position + Vector3.up * 0.4f);
            bool inside = local.x > insideMin.x && local.x < insideMax.x && local.y > insideMin.y && local.y < insideMax.y;
            alpha = Mathf.MoveTowards(alpha, inside ? insideAlpha : 1f, fadeSpeed * Time.deltaTime);
            Color c = outside.color;
            c.a = alpha;
            outside.color = c;
        }
    }
}
