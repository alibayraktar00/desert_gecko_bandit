using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// İlk düşman: çölde devriye gezen akrep. 3 can, 1 temas hasarı, oyuncuyu görünce hızlanır.
    /// Vurulunca sersemler, bir adım geri çekilir, iğnesini kaldırıp gerilir (uyarı), sonra kısa bir
    /// hamleyle sokar. Hamle oyuncunun önünde durur, içinden geçmez. Diğer değerler EnemyBase'dedir.
    /// </summary>
    public class Scorpion : EnemyBase
    {
        public enum CounterPhase { None, Hurt, BackOff, Windup, Strike, Recover }

        [Header("Akrep görseli")]
        [Tooltip("Yürürken yukarı aşağı sekecek görsel (sprite'ın bulunduğu alt obje)")]
        [SerializeField] Transform visual;
        [SerializeField] float bobHeight = 0.05f;
        [SerializeField] float bobFrequency = 9f;

        [Header("Karşı saldırı")]
        [Tooltip("Vuran bundan uzaktaysa karşılık vermez, normal kovalar")]
        [SerializeField] float counterRange = 6f;
        [SerializeField] float hurtTime = 0.3f;
        [SerializeField] float backOffTime = 0.2f;
        [SerializeField] float backOffSpeed = 2.5f;
        [Tooltip("İğneyi kaldırıp gerilme: oyuncunun kaçma ya da vurma fırsatı")]
        [SerializeField] float windupTime = 0.4f;
        [SerializeField] float strikeSpeed = 6f;
        [SerializeField] float maxStrikeTime = 0.3f;
        [Tooltip("Hamle oyuncuya bu kadar kala durur: üst üste binmesin")]
        [SerializeField] float strikeStopDistance = 1f;
        [SerializeField] float stingReach = 1.35f;
        [SerializeField] float stingHeight = 1f;
        [SerializeField] int stingDamage = 1;
        [SerializeField] float recoverTime = 0.4f;
        [SerializeField] Color stingColor = new Color32(0xF4, 0xD3, 0x5E, 0xFF);

        public CounterPhase Counter { get; private set; }

        protected override bool HoldPosition => Counter != CounterPhase.None;

        float visualBaseY;
        Vector3 visualBaseScale = Vector3.one;
        float phaseStartedAt, phaseEndsAt;
        int strikeDirection;

        protected override void Awake()
        {
            base.Awake();
            if (visual != null)
            {
                visualBaseY = visual.localPosition.y;
                visualBaseScale = visual.localScale;
            }
        }

        void Enter(CounterPhase phase, float duration)
        {
            Counter = phase;
            phaseStartedAt = Time.time;
            phaseEndsAt = Time.time + duration;
        }

        int DirectionToPlayer => Player != null && Player.transform.position.x >= transform.position.x ? 1 : -1;

        protected override void OnDamaged()
        {
            if (Player == null || !Player.IsAlive || Mathf.Abs(Player.transform.position.x - transform.position.x) > counterRange)
            {
                Counter = CounterPhase.None;
                return;
            }
            Enter(CounterPhase.Hurt, hurtTime); // darbe hamleyi keser: gerilirken vurmak işe yarar
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isActiveAndEnabled || Counter == CounterPhase.None)
                return;

            bool timeUp = Time.time >= phaseEndsAt;
            switch (Counter)
            {
                case CounterPhase.Hurt:
                    if (timeUp)
                    {
                        TurnTowards(DirectionToPlayer);
                        Enter(CounterPhase.BackOff, backOffTime);
                    }
                    break;
                case CounterPhase.BackOff:
                    if (!IsBlocked(-FacingDirection))
                        body.linearVelocity = new Vector2(-FacingDirection * backOffSpeed, body.linearVelocity.y);
                    if (timeUp)
                    {
                        TurnTowards(DirectionToPlayer);
                        Enter(CounterPhase.Windup, windupTime);
                    }
                    break;
                case CounterPhase.Windup:
                    if (timeUp)
                    {
                        strikeDirection = DirectionToPlayer;
                        TurnTowards(strikeDirection);
                        Enter(CounterPhase.Strike, maxStrikeTime);
                    }
                    break;
                case CounterPhase.Strike:
                    Strike(timeUp);
                    break;
                case CounterPhase.Recover:
                    if (timeUp)
                        Counter = CounterPhase.None;
                    break;
            }
        }

        void Strike(bool timeUp)
        {
            bool playerGone = Player == null || !Player.IsAlive;
            float gap = playerGone ? 0f : (Player.transform.position.x - transform.position.x) * strikeDirection;
            if (timeUp || playerGone || gap <= strikeStopDistance || IsBlocked(strikeDirection))
            {
                Sting();
                Enter(CounterPhase.Recover, recoverTime);
                return;
            }
            body.linearVelocity = new Vector2(strikeDirection * strikeSpeed, body.linearVelocity.y);
        }

        void Sting()
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            Vector2 tip = (Vector2)transform.position + new Vector2(strikeDirection * 0.8f, 0.45f);
            PixelBurst.Spawn(tip, stingColor, 6, 3f, 0.25f);
            if (Player == null || !Player.IsAlive)
                return;
            Vector2 toPlayer = Player.transform.position - transform.position;
            if (toPlayer.x * strikeDirection >= -0.3f && Mathf.Abs(toPlayer.x) <= stingReach && Mathf.Abs(toPlayer.y) <= stingHeight)
                Player.TakeDamage(stingDamage, new Vector2(strikeDirection, 0f));
        }

        public override void ResetEnemy()
        {
            base.ResetEnemy();
            Counter = CounterPhase.None;
            if (visual != null)
                visual.localScale = visualBaseScale;
        }

        protected override void Update()
        {
            base.Update();
            if (visual == null)
                return;

            bool walking = Mathf.Abs(body.linearVelocity.x) > 0.1f && Counter == CounterPhase.None;
            float frequency = bobFrequency * (IsChasing ? 1.6f : 1f);
            float offset = walking ? Mathf.Abs(Mathf.Sin(Time.time * frequency)) * bobHeight : 0f;
            visual.localPosition = new Vector3(visual.localPosition.x, visualBaseY + offset, visual.localPosition.z);
            visual.localScale = Vector3.Scale(visualBaseScale, CounterPose());
        }

        // Gerilirken yukarı uzayıp titrer, sokarken öne yayılır, sonra normale döner.
        Vector3 CounterPose()
        {
            switch (Counter)
            {
                case CounterPhase.Windup:
                    float t = Mathf.Clamp01((Time.time - phaseStartedAt) / windupTime);
                    float shiver = Mathf.Sin(Time.time * 50f) * 0.03f;
                    return new Vector3(1f - 0.15f * t + shiver, 1f + 0.3f * t, 1f);
                case CounterPhase.Strike:
                    return new Vector3(1.25f, 0.85f, 1f);
                case CounterPhase.Recover:
                    float r = Mathf.Clamp01((Time.time - phaseStartedAt) / recoverTime);
                    return Vector3.Lerp(new Vector3(1.25f, 0.85f, 1f), Vector3.one, r);
                default:
                    return Vector3.one;
            }
        }
    }
}
