using DesertGecko.Environment;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.DevTools
{
    /// <summary>
    /// Sadece test sahnesi için: düşen oyuncuyu başlangıca döndürür,
    /// ekranda kontrolleri ve oyuncunun durumunu gösterir.
    /// </summary>
    public class TestLevelHelper : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] CameraFollow cameraFollow;
        [SerializeField] Vector2 spawnPoint = new Vector2(0f, 1f);
        [SerializeField] float killY = -12f;
        [SerializeField] bool showHelp = true;

        GUIStyle style;

        void Update()
        {
            if (player != null && player.transform.position.y < killY)
            {
                player.Teleport(spawnPoint);
                if (cameraFollow != null)
                    cameraFollow.SnapToTarget();
            }
        }

        void OnGUI()
        {
            if (!showHelp || player == null)
                return;

            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, padding = new RectOffset(10, 10, 8, 8) };
            string text =
                "A/D veya oklar: koş\n" +
                "Space: zıpla (havada tekrar bas: çift zıplama)\n" +
                "Space basılı tut (düşerken): süzül\n" +
                "Duvara doğru bas: yapış  |  W/S: tırman  |  Space: duvar zıplaması\n" +
                $"Durum: {player.State}   Hız: {player.Velocity.x:0.0}, {player.Velocity.y:0.0}";
            GUI.Box(new Rect(10, 10, 520, 104), text, style);
        }
    }
}
