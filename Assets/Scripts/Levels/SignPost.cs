using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Levels
{
    /// <summary>
    /// Eğitim tabelası: oyuncu yaklaşınca üstünde ipucu yazısı belirir, uzaklaşınca kaybolur.
    /// Yazı, tabelanın altındaki dünya-uzayı Canvas'ta duruyor (CanvasGroup ile solar).
    /// </summary>
    public class SignPost : MonoBehaviour
    {
        [SerializeField] CanvasGroup bubble;
        [SerializeField] float showDistance = 3.5f;
        [SerializeField] float fadeSpeed = 6f;

        Transform player;

        void Start()
        {
            var found = FindAnyObjectByType<PlayerController>();
            if (found != null)
                player = found.transform;
            if (bubble != null)
                bubble.alpha = 0f;
        }

        void Update()
        {
            if (bubble == null || player == null)
                return;
            Vector2 d = player.position - transform.position;
            bool near = Mathf.Abs(d.x) < showDistance && Mathf.Abs(d.y) < showDistance;
            bubble.alpha = Mathf.MoveTowards(bubble.alpha, near ? 1f : 0f, fadeSpeed * Time.deltaTime);
        }
    }
}
