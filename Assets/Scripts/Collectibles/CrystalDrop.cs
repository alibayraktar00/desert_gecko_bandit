using UnityEngine;

namespace DesertGecko.Collectibles
{
    /// <summary>Düşmana eklenir: düşman ölünce etrafa kristal saçar.</summary>
    public class CrystalDrop : MonoBehaviour
    {
        [SerializeField] Crystal crystalPrefab;
        [SerializeField] int minCount = 1;
        [SerializeField] int maxCount = 2;
        [SerializeField] float launchSpeed = 5f;
        [SerializeField] Vector2 spawnOffset = new Vector2(0f, 0.5f);

        public void Drop()
        {
            if (crystalPrefab == null)
                return;

            int count = Random.Range(minCount, maxCount + 1);
            for (int i = 0; i < count; i++)
            {
                // 60°–120° arası yelpaze: yukarı ve iki yana saçılsın
                float t = count == 1 ? 0.5f : (float)i / (count - 1);
                float angle = Mathf.Lerp(60f, 120f, t) + Random.Range(-8f, 8f);
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                Crystal crystal = Instantiate(crystalPrefab, (Vector2)transform.position + spawnOffset, Quaternion.identity);
                crystal.Launch(direction * launchSpeed);
            }
        }
    }
}
