using DesertGecko.Environment;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.DevTools
{
    /// <summary>
    /// Sadece test sahnesi için: uçuruma düşen oyuncuyu son checkpoint'e döndürür (1 kalp hasarla),
    /// ekranın altında kontrolleri ve oyuncunun durumunu gösterir.
    /// </summary>
    public class TestLevelHelper : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] CameraFollow cameraFollow;
        [SerializeField] float killY = -12f;
        [SerializeField] int pitDamage = 2;
        [SerializeField] bool showHelp = true;

        PlayerHealth health;
        GUIStyle style;

        void Awake()
        {
            if (player != null)
                health = player.GetComponent<PlayerHealth>();
        }

        void OnEnable()
        {
            if (health != null)
                health.Respawned += SnapCamera;
        }

        void OnDisable()
        {
            if (health != null)
                health.Respawned -= SnapCamera;
        }

        void Update()
        {
            if (player == null || player.transform.position.y >= killY)
                return;

            if (health != null)
            {
                bool wasAlive = health.IsAlive;
                health.FallIntoPit(pitDamage);
                if (wasAlive && health.IsAlive)
                    SnapCamera();
            }
            else
            {
                player.Teleport(Vector2.up);
                SnapCamera();
            }
        }

        void SnapCamera()
        {
            if (cameraFollow != null)
                cameraFollow.SnapToTarget();
        }

        void OnGUI()
        {
            if (!showHelp || player == null)
                return;

            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, padding = new RectOffset(10, 10, 6, 6) };
            string text =
                "A/D: koş   Space: zıpla (havada tekrar: çift zıplama, basılı tut: süzül)\n" +
                "Duvara doğru bas: yapış   W/S: tırman   Space: duvar zıplaması\n" +
                "J: dil (W/S ile yukarı/aşağı)   K: silahla ateş   L: kuyruk fırlat\n" +
                $"Durum: {player.State}   Hız: {player.Velocity.x:0.0}, {player.Velocity.y:0.0}";
            GUI.Box(new Rect(10, Screen.height - 92, 560, 82), text, style);
        }
    }
}
