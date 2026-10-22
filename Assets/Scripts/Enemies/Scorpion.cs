using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// İlk düşman: çölde devriye gezen akrep. 3 can, 1 temas hasarı, oyuncuyu görünce hızlanır.
    /// Değerler EnemyBase'deki varsayılanlardır; burada sadece yürürken sekme efekti var.
    /// </summary>
    public class Scorpion : EnemyBase
    {
        [Header("Akrep görseli")]
        [Tooltip("Yürürken yukarı aşağı sekecek görsel (sprite'ın bulunduğu alt obje)")]
        [SerializeField] Transform visual;
        [SerializeField] float bobHeight = 0.05f;
        [SerializeField] float bobFrequency = 9f;

        float visualBaseY;

        protected override void Awake()
        {
            base.Awake();
            if (visual != null)
                visualBaseY = visual.localPosition.y;
        }

        protected override void Update()
        {
            base.Update();
            if (visual == null)
                return;

            bool walking = Mathf.Abs(body.linearVelocity.x) > 0.1f;
            float frequency = bobFrequency * (IsChasing ? 1.6f : 1f);
            float offset = walking ? Mathf.Abs(Mathf.Sin(Time.time * frequency)) * bobHeight : 0f;
            visual.localPosition = new Vector3(visual.localPosition.x, visualBaseY + offset, visual.localPosition.z);
        }
    }
}
