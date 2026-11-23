using System.Collections;
using System.Collections.Generic;
using DesertGecko.Effects;
using DesertGecko.Enemies;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Tren kazası ara sahnesi; oyunun içinde akar. Gila kömür vagonunun önüne varınca başlar:
    /// sinema şeritleri iner, kamera geri çekilip öne bakar. Raylara bağlanmış dinamit demeti görünür, fitili
    /// cızırdar; makinist düdüğe asılır, frenler çığlık atar, tekerleklerden kıvılcım saçılır ama tren duramaz.
    /// Lokomotif dinamitin üstünden geçerken patlar: ekran beyaza keser, ağır çekim; lokomotif parçalara ayrılır
    /// (kabin, kazan, baca, şasi, tekerlekler savrulur), ardından kazan da patlar. Kömür vagonu çukura burnunu sokar,
    /// yolcu vagonları birbirine girip zikzak raydan çıkar. Gila havaya fırlar, ekran kararır.
    /// Açıldığında Gila kaza yerinde yüzüstü baygın yatıyor; yavaş yavaş doğrulur, kontrol geri gelir.
    /// </summary>
    public class TrainCrash : MonoBehaviour
    {
        [SerializeField] TrainRide ride;
        [Tooltip("Arkadan öne: sondan bir önceki kömür vagonu, son eleman lokomotif. Kökler alt orta noktalarından döner")]
        [SerializeField] Transform[] cars;
        [Tooltip("Gila bu x'i geçince sahne başlar (kömür vagonunun önü)")]
        [SerializeField] float triggerX;
        [SerializeField] float locoFrontX;
        [SerializeField] float trackY;
        [Tooltip("Raylara bağlı dinamit (başta kapalı)")]
        [SerializeField] Transform dynamite;
        [SerializeField] float dynamiteLead = 20f;
        [Tooltip("Lokomotifin görseli: patlayınca gizlenir, yerine parçalar açılır")]
        [SerializeField] GameObject locoLook;
        [Tooltip("Lokomotif parçaları (başta kapalı, lokomotifin içinde yerli yerinde)")]
        [SerializeField] Transform[] locoPieces;
        [Tooltip("Ekran açıldığında Gila'nın yattığı yer (kaza yeri, zemin üstü)")]
        [SerializeField] Vector2 wakePoint;

        [Header("Efektler (pixelfx)")]
        [SerializeField] Sprite[] explosionFrames;
        [SerializeField] Sprite[] dustFrames;
        [SerializeField] Sprite[] stunFrames;
        [SerializeField] float fxFps = 15f;

        [Header("Gila: doğrulma (Player/Gila/kalkis)")]
        [SerializeField] Sprite[] getUpFrames;
        [Tooltip("Her karenin süresi: uyuyor, kıpırdanıyor, doğruluyor, kalkıyor, sendeleyip çöküyor, toparlanıyor")]
        [SerializeField] float[] getUpTimes = { 1.1f, 0.5f, 0.55f, 0.4f, 0.45f, 0.35f, 0.3f, 0.3f };

        [Header("Ses")]
        [SerializeField] AudioClip whistleClip;
        [SerializeField] AudioClip brakeClip;
        [SerializeField] AudioClip fuseClip;
        [SerializeField] AudioClip blastClip;
        [SerializeField] AudioClip crashClip;

        const float Gravity = 22f;
        static readonly Color SparkColor = new Color32(0xFF, 0xD0, 0x60, 0xFF);
        static readonly Color FuseColor = new Color32(0xFF, 0xF0, 0xA0, 0xFF);

        PlayerController player;
        PlayerHealth health;
        bool started;

        class Body
        {
            public Transform t;
            public Vector2 v;
            public float w, angle, delay, maxAngle = 999f;
            public float halfWidth, height;
            public bool landed, bounced, heavy, dusty = true;
        }

        void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player != null)
                health = player.GetComponent<PlayerHealth>();
            if (dynamite != null)
                dynamite.gameObject.SetActive(false);
            if (locoPieces != null)
                foreach (Transform p in locoPieces)
                    p.gameObject.SetActive(false);
        }

        void Update()
        {
            if (started || player == null || ride == null || !ride.Departed || (health != null && !health.IsAlive))
                return;
            if (player.transform.position.x >= triggerX)
            {
                started = true;
                StartCoroutine(Run());
            }
        }

        IEnumerator Run()
        {
            var visual = player.GetComponentInChildren<PlayerVisual>();
            var cam = FindAnyObjectByType<CameraFollow>();
            int n = cars.Length;
            Transform loco = cars[n - 1];
            float locoCenter = loco.position.x + 1f; // dinamit kazanın altına gelince patlar

            // 1) Kontrol kesilir; Gila vagona tutunur, düşmanlar trenle birlikte donar.
            player.MovementLocked = true;
            player.Frozen = true;
            player.transform.SetParent(NearestCar(player.transform.position.x), true);
            FreezeTrainEnemies();
            foreach (EnemyBullet b in FindObjectsByType<EnemyBullet>())
                Destroy(b.gameObject);
            CinematicOverlay.Bars(true, 0.5f);
            if (cam != null)
            {
                cam.SetFocus(new Vector2(locoFrontX + 3f, trackY + 3.2f), 0.9f);
                cam.ZoomTo(6.3f, 1.4f);
            }
            ride.Controlled = true;
            yield return new WaitForSeconds(0.5f);

            // 2) Raylarda dinamit: fitil cızırdar, düdük, fren, kıvılcım. Tren duramaz.
            var wheels = new List<WheelSpin>();
            foreach (Transform car in cars)
                wheels.AddRange(car.GetComponentsInChildren<WheelSpin>());
            dynamite.gameObject.SetActive(true);
            dynamite.position = new Vector3(locoFrontX + dynamiteLead, trackY, 0f);
            OneShotSound.Play(fuseClip, 0.8f);
            yield return new WaitForSeconds(0.25f);
            OneShotSound.Play(whistleClip, 0.9f);
            OneShotSound.Play(brakeClip, 0.8f);
            float speed = TrainScroll.Speed, nextSpark = 0f, nextFuse = 0f;
            while (dynamite.position.x > locoCenter)
            {
                float dt = Time.deltaTime;
                speed = Mathf.MoveTowards(speed, ride.TopSpeed * 0.55f, ride.TopSpeed * 0.35f * dt);
                TrainScroll.Speed = speed;
                dynamite.position += Vector3.left * (speed * dt);
                if (Time.time >= nextSpark && wheels.Count > 0)
                {
                    nextSpark = Time.time + 0.025f;
                    Transform w = wheels[Random.Range(0, wheels.Count)].transform;
                    PixelBurst.Spawn((Vector2)w.position + new Vector2(-0.1f, 0.08f), SparkColor, 3, 3.5f, 0.3f);
                }
                if (Time.time >= nextFuse)
                {
                    nextFuse = Time.time + 0.05f;
                    PixelBurst.Spawn((Vector2)dynamite.position + new Vector2(0.22f, 0.95f), FuseColor, 2, 2f, 0.2f);
                }
                CameraShake.Add(0.8f * dt);
                yield return null;
            }

            // 3) PATLAMA: lokomotif dinamitin üstünde parçalanır. Dünya durur, tren kendi ağırlığıyla savrulur.
            Vector2 blast = new Vector2(dynamite.position.x, trackY);
            dynamite.gameObject.SetActive(false);
            float impactSpeed = Mathf.Max(7f, speed);
            ride.Halt();
            CinematicOverlay.Flash(0.25f);
            OneShotSound.Play(blastClip);
            OneShotSound.Play(crashClip);
            CameraShake.Add(1f);
            FrameFx.Spawn(explosionFrames, fxFps, blast, 30, false, 1.15f);
            Time.timeScale = 0.3f;
            foreach (TrainSmoke s in loco.GetComponentsInChildren<TrainSmoke>())
                s.Stopped = true;
            // Trenin çarpışmaları kapanır; vagona tutunan Gila'nınkiler hariç (yoksa sonra zeminden geçer).
            foreach (Transform car in cars)
                foreach (Collider2D c in car.GetComponentsInChildren<Collider2D>())
                    if (c.GetComponentInParent<PlayerController>() == null)
                        c.enabled = false;

            var bodies = new List<Body>();
            ShatterLocomotive(loco, blast, impactSpeed, bodies);
            DerailCars(impactSpeed, bodies);
            foreach (WheelSpin w in wheels)
                w.FreeSpin = impactSpeed * 0.6f;

            Transform playerVisual = visual != null ? visual.transform : null;
            Vector2 pVel = new Vector2(6.5f, 12.5f);
            bool playerFlying = false, boilerBlown = false;
            float flown = 0f, impactReal = Time.unscaledTime;
            while (Time.unscaledTime - impactReal < 2.7f)
            {
                float real = Time.unscaledTime - impactReal;
                if (real > 1.2f)
                    Time.timeScale = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01((real - 1.2f) / 0.4f));
                if (real > 1.9f && real - Time.unscaledDeltaTime <= 1.9f)
                    CinematicOverlay.Fade(1f, 0.6f);
                float dt = Time.deltaTime;
                flown += dt;
                // Kazan da patlar: ikinci, daha yüksek ateş topu
                if (!boilerBlown && flown > 0.32f)
                {
                    boilerBlown = true;
                    FrameFx.Spawn(explosionFrames, fxFps, blast + new Vector2(1.6f, 0f), 31, true, 0.85f);
                    CameraShake.Add(0.7f);
                }
                StepBodies(bodies, dt);
                foreach (WheelSpin w in wheels)
                    w.FreeSpin = Mathf.MoveTowards(w.FreeSpin, 0f, 4f * dt);

                // Gila fırlar: döne döne yukarı ve ileri, kadrajdan çıkar.
                if (!playerFlying && flown >= 0.06f)
                {
                    playerFlying = true;
                    player.transform.SetParent(null, true);
                }
                if (playerFlying)
                {
                    pVel.y -= Gravity * dt;
                    player.transform.position += (Vector3)(pVel * dt);
                    if (playerVisual != null)
                        playerVisual.Rotate(0f, 0f, -620f * dt);
                }
                yield return null;
            }

            // 4) Karanlık: Gila kaza yerinde yüzüstü yatıyor. Fizik açık: yere kendisi oturur.
            Time.timeScale = 1f;
            foreach (WheelSpin w in wheels)
                w.FreeSpin = 0f;
            if (playerVisual != null)
                playerVisual.localRotation = Quaternion.identity;
            player.transform.SetParent(null, true);
            foreach (Collider2D c in player.GetComponentsInChildren<Collider2D>(true))
                c.enabled = true;
            Vector2 wake = wakePoint + Vector2.up * 0.3f;
            player.Frozen = false;
            player.Teleport(wake);
            if (visual != null && getUpFrames != null && getUpFrames.Length > 0)
                visual.SetPose(getUpFrames[0]);
            if (health != null)
                health.SetCheckpoint(wakePoint + Vector2.up * 0.1f);
            if (cam != null)
            {
                cam.ClearFocus();
                cam.ResetZoom();
                cam.SnapToTarget();
            }
            GameObject stars = SpawnStars(wakePoint + new Vector2(0.1f * player.FacingDirection, -0.75f));
            yield return Hold(0.5f, wake, true);
            CinematicOverlay.Fade(0f, 1.3f);
            yield return Hold(1.2f, wake, false);

            // 5) Yavaş yavaş doğrulur: uyuyor, kıpırdanır, doğrulur, kalkar, sendeleyip çöker, toparlanır.
            if (visual != null && getUpFrames != null)
            {
                for (int i = 0; i < getUpFrames.Length; i++)
                {
                    visual.SetPose(getUpFrames[i]);
                    if (stars != null && i == 2)
                        stars.transform.position = wakePoint + new Vector2(0.1f * player.FacingDirection, -0.45f);
                    if (stars != null && i == 3)
                        stars.transform.position = wakePoint + new Vector2(0.05f * player.FacingDirection, 0.05f);
                    yield return Hold(i < getUpTimes.Length ? getUpTimes[i] : 0.4f, wake, false);
                }
                visual.ClearPose();
            }
            if (stars != null)
                Destroy(stars, 0.5f);
            CinematicOverlay.Bars(false, 0.6f);
            player.MovementLocked = false;
        }

        /// <summary>Bekler; Gila zeminin altına kayarsa (fizik ilk karede ıskalarsa) yerine geri koyar.</summary>
        IEnumerator Hold(float seconds, Vector2 wake, bool realtime)
        {
            float end = (realtime ? Time.unscaledTime : Time.time) + seconds;
            while ((realtime ? Time.unscaledTime : Time.time) < end)
            {
                if (player.transform.position.y < wakePoint.y - 0.6f || Mathf.Abs(player.transform.position.x - wake.x) > 1.5f)
                    player.Teleport(wake);
                yield return null;
            }
        }

        Transform NearestCar(float x)
        {
            Transform nearest = cars[0];
            foreach (Transform car in cars)
                if (Mathf.Abs(car.position.x - x) < Mathf.Abs(nearest.position.x - x))
                    nearest = car;
            return nearest;
        }

        /// <summary>Lokomotif görseli gizlenir; parçaları ve tekerlekleri patlamanın merkezinden dışarı savrulur.</summary>
        void ShatterLocomotive(Transform loco, Vector2 blast, float impactSpeed, List<Body> bodies)
        {
            if (locoLook != null)
                locoLook.SetActive(false);
            var flying = new List<Transform>();
            if (locoPieces != null)
                flying.AddRange(locoPieces);
            foreach (WheelSpin w in loco.GetComponentsInChildren<WheelSpin>())
                flying.Add(w.transform);
            foreach (Transform piece in flying)
            {
                piece.gameObject.SetActive(true);
                piece.SetParent(null, true);
                Bounds b = VisualBounds(piece);
                Vector2 away = (Vector2)b.center - blast;
                float dist = Mathf.Max(0.5f, away.magnitude);
                Vector2 dir = away / dist;
                float power = Mathf.Lerp(15f, 7f, Mathf.Clamp01(dist / 6f));
                bodies.Add(new Body
                {
                    t = piece,
                    v = dir * power + new Vector2(impactSpeed * 0.35f, 6f + Random.Range(0f, 4f)),
                    w = Random.Range(-380f, 380f),
                    halfWidth = b.extents.x,
                    height = b.size.y,
                    heavy = b.size.x > 2.5f,
                });
            }
        }

        /// <summary>Kömür vagonu patlama çukuruna burnunu sokar; yolcu vagonları sırayla birbirine girip zikzak raydan çıkar.</summary>
        void DerailCars(float impactSpeed, List<Body> bodies)
        {
            int n = cars.Length;
            for (int i = 0; i < n - 1; i++)
            {
                Transform car = cars[i];
                int fromFront = n - 2 - i; // 0: kömür vagonu
                Bounds b = VisualBounds(car);
                var body = new Body
                {
                    t = car,
                    halfWidth = b.extents.x,
                    height = b.size.y,
                    delay = fromFront * 0.08f,
                    heavy = true,
                };
                if (fromFront == 0)
                {
                    body.v = new Vector2(impactSpeed * 0.45f, 6.5f);
                    body.w = -150f;               // burnu çukura, kuyruğu havaya
                    body.maxAngle = 75f;
                }
                else
                {
                    float side = fromFront % 2 == 0 ? 1f : -1f;
                    body.v = new Vector2(impactSpeed * Mathf.Lerp(0.4f, 0.15f, fromFront / (float)n), Random.Range(2.2f, 4.8f));
                    body.w = side * Random.Range(50f, 100f);
                    body.maxAngle = Random.Range(12f, 32f);
                }
                bodies.Add(body);
            }
        }

        void StepBodies(List<Body> bodies, float dt)
        {
            foreach (Body b in bodies)
            {
                if (b.delay > 0f)
                {
                    b.delay -= dt;
                    continue;
                }
                Vector3 p = b.t.position;
                if (!b.landed)
                {
                    b.v.y -= Gravity * dt;
                    b.angle = Mathf.Clamp(b.angle + b.w * dt, -b.maxAngle, b.maxAngle);
                }
                else
                {
                    b.v.x *= Mathf.Exp(-3.5f * dt);
                    b.w *= Mathf.Exp(-6f * dt);
                    b.angle += b.w * dt;
                }
                p += (Vector3)(b.v * dt);

                // Dönmüş gövdenin en alt noktası zemine değince oturur; hafif parçalar bir kez seker.
                float lowest = LowestPoint(b.halfWidth, b.height, b.angle);
                if (p.y + lowest <= trackY && b.v.y <= 0f)
                {
                    p.y = trackY - lowest;
                    if (!b.heavy && !b.bounced && b.v.y < -4f)
                    {
                        b.bounced = true;
                        b.v.y = -b.v.y * 0.35f;
                        b.w *= -0.5f;
                    }
                    else
                    {
                        b.v.y = 0f;
                        if (!b.landed)
                        {
                            b.landed = true;
                            if (b.dusty)
                                FrameFx.Spawn(dustFrames, fxFps, new Vector2(p.x, trackY), 25, b.v.x < 0f, b.heavy ? 1f : 0.55f);
                            CameraShake.AddAt(p, b.heavy ? 0.45f : 0.15f);
                        }
                    }
                }
                b.t.position = p;
                b.t.rotation = Quaternion.Euler(0f, 0f, b.angle);
            }
        }

        /// <summary>Alt ortadan dönen dikdörtgenin (genişlik 2*halfWidth, boy height) en alt noktası, pivota göre.</summary>
        static float LowestPoint(float halfWidth, float height, float angle)
        {
            float a = angle * Mathf.Deg2Rad, s = Mathf.Sin(a), c = Mathf.Cos(a);
            float min = 0f;
            foreach (Vector2 corner in new[] { new Vector2(-halfWidth, 0f), new Vector2(halfWidth, 0f), new Vector2(-halfWidth, height), new Vector2(halfWidth, height) })
                min = Mathf.Min(min, corner.x * s + corner.y * c);
            return min;
        }

        static Bounds VisualBounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length == 0)
                return new Bounds(t.position, new Vector3(1f, 1f, 0f));
            Bounds b = renderers[0].bounds;
            foreach (SpriteRenderer r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }

        void FreezeTrainEnemies()
        {
            foreach (EnemyBase e in FindObjectsByType<EnemyBase>())
            {
                float x = e.transform.position.x;
                if (x < ride.TrainMinX - 1f || x > ride.TrainMaxX + 1f || e.transform.position.y < trackY - 0.5f)
                    continue;
                var rb = e.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.simulated = false;
                e.enabled = false;
                e.transform.SetParent(NearestCar(x), true);
            }
        }

        GameObject SpawnStars(Vector2 at)
        {
            if (stunFrames == null || stunFrames.Length == 0)
                return null;
            var go = new GameObject("Sersem");
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 40;
            go.AddComponent<SpriteLoop>().Configure(stunFrames, fxFps);
            return go;
        }
    }
}
