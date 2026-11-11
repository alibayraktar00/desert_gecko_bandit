using UnityEngine;

namespace DesertGecko.Weapons
{
    /// <summary>Namlu alevi: birkaç kare oynar ve kaybolur. Kareler sağa doğru açılır, nişan açısına döndürülür.</summary>
    public class MuzzleFlash : MonoBehaviour
    {
        const float FrameTime = 0.035f;

        Sprite[] frames;
        SpriteRenderer sr;
        float age;

        public static void Spawn(Sprite[] frames, Vector2 position, Vector2 direction, int order)
        {
            if (frames == null || frames.Length == 0)
                return;
            var go = new GameObject("NamluAlevi");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            var flash = go.AddComponent<MuzzleFlash>();
            flash.frames = frames;
            flash.sr = go.AddComponent<SpriteRenderer>();
            flash.sr.sprite = frames[0];
            flash.sr.sortingOrder = order;
        }

        void Update()
        {
            age += Time.deltaTime;
            int index = Mathf.FloorToInt(age / FrameTime);
            if (index >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            sr.sprite = frames[index];
        }
    }
}
