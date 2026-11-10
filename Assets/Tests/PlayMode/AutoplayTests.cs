using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DesertGecko.Collectibles;
using DesertGecko.Core;
using DesertGecko.Enemies;
using DesertGecko.Environment;
using DesertGecko.Levels;
using DesertGecko.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DesertGecko.Tests
{
    /// <summary>
    /// Levelleri baştan sona oynayan bot. Gerçek bir sanal klavyeye tuş basar, yani oyuncu
    /// kodu normal oyundaki gibi çalışır. Her level için yol, hasar, ölüm, kristal ve takılma
    /// noktalarını Logs/Autoplay altına yazar (harita çizimi için geometriyle birlikte).
    ///
    /// Yavaş olduğu için normal test çalıştırmasında atlanır; açıkça seçilince çalışır.
    /// </summary>
    [Explicit, Category("Autoplay")]
    public class AutoplayTests
    {
        const float TimeLimit = 600f;       // oyun saniyesi
        const float StuckSeconds = 6f;      // bu kadar ilerlemezse kurtulma manevrası
        const float GiveUpSeconds = 40f;    // bu kadar ilerlemezse level başarısız

        string savedPrefix;
        InputSettings savedSettings;
        Keyboard keyboard;

        [SetUp]
        public void SetUp()
        {
            savedPrefix = GameProgress.KeyPrefix;
            GameProgress.KeyPrefix = "DGB_BOT_";
            GameProgress.ResetProgress();

            Application.runInBackground = true; // editör odakta değilken de sürsün
            // Batch modda pencere odakta değil; sanal klavyenin girdisi yine de oyuna gitsin.
            savedSettings = InputSystem.settings;
            var settings = Object.Instantiate(savedSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            keyboard = InputSystem.AddDevice<Keyboard>("BotKeyboard");

            Time.captureFramerate = 60; // oyun zamanı gerçek zamandan bağımsız, sabit 60 fps
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            if (savedSettings != null)
                InputSystem.settings = savedSettings;
            GameProgress.ResetProgress();
            GameProgress.KeyPrefix = savedPrefix;
        }

        [UnityTest]
        public IEnumerator Bot_LeveliBitirir([Values(0, 1, 2)] int level)
        {
            yield return SceneManager.LoadSceneAsync(LevelCatalog.Levels[level].Scene);
            yield return null;

            var run = new BotRun(LevelManager.Current, keyboard);
            yield return run.Play();
            string report = run.WriteReport(LevelCatalog.Levels[level].Scene);
            Debug.Log(report);

            Assert.IsTrue(run.Completed, $"Bot leveli bitiremedi.\n{report}");
            Assert.Greater(run.DoubleJumps, 0, $"Bot hiç çift zıplama kullanmadı.\n{report}");
        }

        /// <summary>Level 3'ü oyun çalışırken görüntüler: ışıklar, gayzer, çamur ve fırtına ancak burada gerçek halindedir.</summary>
        [UnityTest]
        public IEnumerator Gorsel_Level3()
        {
            yield return SceneManager.LoadSceneAsync("Level_3");
            yield return null;
            PlayerController player = LevelManager.Current.Player;
            var follow = Object.FindAnyObjectByType<CameraFollow>();
            var points = new[]
            {
                new Vector2(3f, 0.1f), new Vector2(55f, 3f), new Vector2(130f, 4f), new Vector2(230f, 9.1f), new Vector2(255f, 3.1f),
                new Vector2(265f, 3.1f), new Vector2(283f, 13.6f), new Vector2(308f, 16f), new Vector2(345f, 24.1f), new Vector2(170f, 4f), new Vector2(456f, 16.1f), new Vector2(480f, 18.1f),
            };
            string dir = Path.Combine(Application.dataPath, "..", "Temp", "Screenshots");
            for (int i = 0; i < points.Length; i++)
            {
                player.Frozen = false;
                player.Teleport(points[i]);
                player.Frozen = true; // boşluğun üstünde de görüntü alınabilsin
                follow.SnapToTarget();
                float wait = points[i].x == 345f ? 4.2f : 2.2f; // fırtına ekrana girsin
                for (float t = 0f; t < wait; t += Time.deltaTime)
                {
                    if (t < 0.1f) follow.SnapToTarget();
                    yield return null;
                }
                File.WriteAllBytes(Path.Combine(dir, $"oyun_{i:00}_{points[i].x:0}.png"), RenderCamera());
            }
        }

        static byte[] RenderCamera()
        {
            Camera cam = Camera.main;
            var rt = new RenderTexture(640, 360, 24);
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
            RenderTexture.active = rt;
            var tex = new Texture2D(640, 360, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            byte[] png = tex.EncodeToPNG();
            Object.Destroy(tex);
            rt.Release();
            return png;
        }

        // Reels kaydı: dikey 9:16, 540x960. Ortografik boy 7.5 → birim başına 64 piksel: piksel sanatı tam 2 kat, bulanmaz.
        const int ReelWidth = 540, ReelHeight = 960;
        const float ReelOrthoSize = 7.5f, ReelFps = 30f;

        /// <summary>Bölüm başına kaydedilecek anlar: x aralığı ve en fazla kaç saniye.</summary>
        static readonly Dictionary<int, (string name, float xMin, float xMax, float seconds)[]> ReelMoments = new Dictionary<int, (string, float, float, float)[]>
        {
            { 0, new[] { ("kamp", 8f, 40f, 6f), ("kanyon", 165f, 215f, 7f), ("akbaba", 330f, 380f, 7f), ("son", 440f, 490f, 6f) } },
            { 1, new[] { ("asansor", 38f, 70f, 7f), ("kaya", 135f, 165f, 6f), ("ruzgar", 240f, 270f, 7f), ("arena", 325f, 360f, 7f) } },
            { 2, new[] { ("ates", 30f, 60f, 5f), ("kanca", 120f, 160f, 7f), ("gayzer", 262f, 295f, 7f), ("baca", 304f, 330f, 8f), ("firtina", 345f, 410f, 9f), ("safak", 458f, 505f, 7f) } },
        };

        /// <summary>
        /// Instagram Reels için ham kareler: bot leveli oynarken belirlenen anlarda dikey kameradan kare alır.
        /// Çıktı: Temp/Reels/L{level}_{an}/0000.png ... Kurgu tools/reels_kurgu.py ile yapılır.
        /// </summary>
        [UnityTest]
        public IEnumerator Reels_Kayit([Values(0, 1, 2)] int level)
        {
            yield return SceneManager.LoadSceneAsync(LevelCatalog.Levels[level].Scene);
            yield return null;
            var hud = GameObject.Find("HUD");
            if (hud != null)
                hud.SetActive(false); // kalpler ve sayaçlar reklamda görünmesin

            string root = Path.Combine(Application.dataPath, "..", "Temp", "Reels");
            var moments = ReelMoments[level];
            var done = new bool[moments.Length];
            var saved = new int[moments.Length];
            var started = new float[moments.Length];
            for (int i = 0; i < started.Length; i++)
                started[i] = -1f;
            float nextShot = 0f;
            var rt = new RenderTexture(ReelWidth, ReelHeight, 24);
            var tex = new Texture2D(ReelWidth, ReelHeight, TextureFormat.RGB24, false);

            var run = new BotRun(LevelManager.Current, keyboard);
            PlayerController player = LevelManager.Current.Player;
            var storm = Object.FindAnyObjectByType<ColdFront>();
            run.OnFrame = t =>
            {
                float x = player.transform.position.x;
                for (int i = 0; i < moments.Length; i++)
                {
                    var m = moments[i];
                    if (done[i] || x < m.xMin || x > m.xMax)
                        continue;
                    if (started[i] < 0f)
                    {
                        started[i] = t;
                        Directory.CreateDirectory(Path.Combine(root, $"L{level + 1}_{m.name}"));
                    }
                    if (t - started[i] > m.seconds)
                    {
                        done[i] = true;
                        continue;
                    }
                    // Dikey kadraj dar: kovalamaca sahnesinde fırtına Gila'nın hemen arkasında dursun ki görünsün.
                    if (m.name == "firtina" && storm != null && storm.IsRunning)
                        storm.transform.position = new Vector3(x - 2.6f, storm.transform.position.y, 0f);
                    if (t < nextShot)
                        return;
                    nextShot = t + 1f / ReelFps;
                    File.WriteAllBytes(Path.Combine(root, $"L{level + 1}_{m.name}", $"{saved[i]++:0000}.png"), RenderPortrait(rt, tex, m.name == "firtina" ? -2.4f : 0f));
                    return;
                }
            };
            yield return run.Play();
            Debug.Log($"[DesertGecko] Reels L{level + 1}: " + string.Join(", ", moments.Select((m, i) => $"{m.name}={saved[i]}")));
            Object.Destroy(tex);
            rt.Release();
            Assert.Greater(saved.Sum(), 0, "hiç kare alınmadı");
        }

        static byte[] RenderPortrait(RenderTexture rt, Texture2D tex, float shiftX = 0f)
        {
            Camera cam = Camera.main;
            cam.transform.position += Vector3.right * shiftX; // kovalayanı kadraja almak için
            float size = cam.orthographicSize, aspect = cam.aspect;
            cam.orthographicSize = ReelOrthoSize;
            cam.aspect = (float)ReelWidth / ReelHeight;
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
            cam.orthographicSize = size;
            cam.ResetAspect();
            cam.transform.position -= Vector3.right * shiftX;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, ReelWidth, ReelHeight), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            return tex.EncodeToPNG();
        }

        /// <summary>Bir level oynanışı: karar verme, kayıt, rapor.</summary>
        class BotRun
        {
            readonly LevelManager manager;
            readonly PlayerController player;
            readonly PlayerHealth health;
            readonly PlayerWallet wallet;
            readonly TailAttack tail;
            readonly TongueAttack tongueAttack;
            int swingsDone;
            ColdFront storm;
            float stormClosest = float.PositiveInfinity;
            bool stormShot;
            float coldestHeat = 1f, coldSeconds;
            int freezeHits;
            BodyHeat heat;
            float swingStartedAt;
            bool latchPressed;
            readonly BoxCollider2D box;
            readonly Rigidbody2D body;
            readonly Keyboard keyboard;
            readonly int groundMask = GameLayers.GroundMask;

            public bool Completed;
            /// <summary>Her karede çağrılır (oyun saniyesiyle): Reels kaydı kare almak için kullanır.</summary>
            public System.Action<float> OnFrame;
            public int DoubleJumps;
            float elapsed;
            readonly List<string> events = new List<string>();
            readonly List<Vector3> path = new List<Vector3>(); // x, y, durum
            readonly List<Vector2> velocities = new List<Vector2>();
            readonly List<Vector2> stuckPoints = new List<Vector2>();
            readonly List<Vector2> damagePoints = new List<Vector2>();
            readonly List<Vector2> pitPoints = new List<Vector2>();
            readonly List<Vector2> deathPoints = new List<Vector2>();
            readonly List<Vector2> crystalStart = new List<Vector2>();
            readonly List<Crystal> crystals;
            readonly List<EnemyBase> enemies;
            int enemyDamageHits, pitFalls, deaths, killed;

            // Karar durumu
            bool spaceWasDown;
            float groundJumpPressedAt = -1f;
            float doublePressedAt = -1f;
            bool doubleForTarget;
            bool doubleUsed;
            bool releaseForDouble;
            bool tongueWasDown, tailWasDown, waitingForTail;
            bool teleported; // checkpoint'e dönünce ilerleme ölçümü baştan başlar
            float recoverUntil = -1f;
            int recoverDir = -1;

            // Rota: level "BotRota" noktaları tanımlıyorsa bot sırayla onları gezer (katlı levellerde sola da gider).
            readonly List<Vector2> route = new List<Vector2>();
            int routeIndex;
            int moveDir = 1;
            float bestProgress;

            bool RouteDone => routeIndex >= route.Count;

            void LoadRoute()
            {
                GameObject root = GameObject.Find("BotRota");
                if (root == null)
                    return;
                foreach (Transform point in root.transform)
                    route.Add(point.position);
            }

            /// <summary>Hedefe vardıysa sonrakine geçer, yönü ayarlar, yeni hedefte ilerleme ölçümünü sıfırlar.</summary>
            void UpdateRoute(Bounds b)
            {
                if (route.Count == 0 || RouteDone)
                {
                    moveDir = 1;
                    return;
                }
                Vector2 target = route[routeIndex];
                bool onGround = player.State == PlayerController.MoveState.Grounded;
                if (onGround && Mathf.Abs(b.center.x - target.x) < 0.7f && b.min.y > target.y - 0.4f)
                {
                    routeIndex++;
                    Log($"rota noktası {routeIndex}/{route.Count} x={b.center.x:0.0} y={b.min.y:0.0}");
                    teleported = true;
                    if (RouteDone)
                        return;
                    target = route[routeIndex];
                }
                // Tırmanırken aşağı düştüyse geri dönüp o noktadan yeniden denesin.
                while (onGround && routeIndex > 0 && route[routeIndex].y >= route[routeIndex - 1].y
                       && b.min.y < route[routeIndex - 1].y - 1f)
                {
                    routeIndex--;
                    teleported = true;
                    target = route[routeIndex];
                }
                moveDir = target.x >= b.center.x ? 1 : -1;
            }

            /// <summary>
            /// Çift zıplama gerekiyor mu: tek zıplama hedef yüksekliğe yetmiyorsa ya da düşerken altında zemin yoksa.
            /// Tepe noktasını beklemez; kalan yükseliş yetmeyeceği belli olunca basar.
            /// </summary>
            bool WantsDoubleJump(Bounds b, Vector2 v)
            {
                // Rotada yukarıdaki bir noktaya gidiyorsa zıplamayı tam yükseklikte kullan.
                doubleForTarget = route.Count > 0 && !RouteDone && route[routeIndex].y > b.min.y + 0.2f;
                if (doubleUsed)
                    return false;
                bool toTarget = route.Count > 0 && !RouteDone && route[routeIndex].y > b.min.y + 0.2f
                    && Mathf.Abs(route[routeIndex].x - b.center.x) < 4f;
                if (toTarget)
                {
                    float gravity = Mathf.Abs(Physics2D.gravity.y) * body.gravityScale;
                    float remainingRise = v.y > 0f && gravity > 0f ? v.y * v.y / (2f * gravity) : 0f;
                    if (v.y < 4f && b.min.y + remainingRise < route[routeIndex].y + 0.3f)
                    {
                        doubleForTarget = true;
                        return true;
                    }
                }
                return v.y <= 0f && player.HasTail
                    && !Physics2D.Raycast(new Vector2(b.center.x, b.min.y), Vector2.down, 2f, groundMask);
            }

            /// <summary>Basılı tuş yeni basış sayılmaz: bir kare bırak, bir kare bas; zıplama gerçekleşene kadar.</summary>
            bool PressCycleForDouble()
            {
                if (!doubleForTarget)
                {
                    // Boşluk üstünde: bir kare bırak, bir kare bas.
                    releaseForDouble = !releaseForDouble;
                    return !releaseForDouble;
                }
                // Yukarıdaki hedefe: basıştan sonra kısa süre basılı kalsın, hemen bırakılırsa zıplama kesilir.
                if (elapsed - doublePressedAt < 0.12f)
                    return true;
                if (spaceWasDown)
                    return false;
                doublePressedAt = elapsed;
                return true;
            }

            bool InGeyserColumn(Bounds b) =>
                Object.FindObjectsByType<Geyser>(FindObjectsInactive.Exclude).Any(g => g.GetComponent<BoxCollider2D>().bounds.Contains(b.center));

            // Kancada: sallanarak hız kazan, ileri doğru yükselirken bırak.
            List<Key> SwingKeys(Bounds b, Vector2 v, int dir)
            {
                var keys = new List<Key>();
                Transform anchor = player.SwingAnchor;
                bool past = anchor != null && (b.center.x - anchor.position.x) * dir > 1.2f;
                bool release = (past && v.x * dir > 5f) || elapsed - swingStartedAt > 7f;
                if (release)
                {
                    swingsDone++;
                    Log($"kancadan uçtu x={b.center.x:0.0} y={b.min.y:0.0} hız={v.x:0.0}");
                    keys.Add(dir > 0 ? Key.D : Key.A);
                    keys.Add(Key.Space); // süzülme için basılı başlasın
                    return keys;
                }
                keys.Add(Key.J);
                keys.Add(v.x >= 0f ? Key.D : Key.A); // salınımı büyüt
                if (Mathf.Abs(v.x) < 0.5f)
                {
                    keys.Remove(Key.A);
                    keys.Remove(Key.D);
                    keys.Add(dir > 0 ? Key.D : Key.A);
                }
                return keys;
            }

            // Havada, altı boşken önünde kanca varsa yapış.
            bool WantsToLatch(Bounds b, int dir)
            {
                var hook = tongueAttack.HookInReach;
                if (hook == null)
                    return false;
                if ((hook.transform.position.x - b.center.x) * dir < 0.8f)
                    return false;
                return !Physics2D.Raycast(new Vector2(b.center.x, b.min.y), Vector2.down, 3f, groundMask);
            }

            // Sıcak hava sütununda yerdeyse zıplasın: havada basılı tutunca yükselir.
            bool InThermal(Bounds b) =>
                Object.FindObjectsByType<Thermal>(FindObjectsInactive.Exclude).Any(t => t.GetComponent<BoxCollider2D>().bounds.Contains(b.center));

            bool OnMover(Bounds b) =>
                Object.FindObjectsByType<MovingPlatform>(FindObjectsInactive.Exclude).Select(m => m.GetComponent<Collider2D>()).Where(c => c != null).Select(c => c.bounds)
                    .Any(p => b.center.x > p.min.x && b.center.x < p.max.x && Mathf.Abs(p.max.y - b.min.y) < 0.25f);

            // Asansör tam üstündeyse ve zıplama menzilindeyse üstüne çıksın.
            bool UnderLift(Bounds b) =>
                Object.FindObjectsByType<MovingPlatform>(FindObjectsInactive.Exclude).Select(m => m.GetComponent<Collider2D>()).Where(c => c != null).Select(c => c.bounds)
                    .Any(p => b.center.x > p.min.x && b.center.x < p.max.x && p.max.y > b.min.y + 0.5f && p.max.y < b.min.y + 2.5f);

            /// <summary>Hedef yukarıdaysa altına gelince zıplasın (tek yönlü çıkıntılar).</summary>
            bool WantsToRiseToTarget(Bounds b)
            {
                if (route.Count == 0 || RouteDone)
                    return false;
                Vector2 target = route[routeIndex];
                return target.y > b.min.y + 1f && target.y < b.min.y + 5f && Mathf.Abs(target.x - b.center.x) < 2.5f;
            }

            public BotRun(LevelManager manager, Keyboard keyboard)
            {
                this.manager = manager;
                this.keyboard = keyboard;
                player = manager.Player;
                health = player.GetComponent<PlayerHealth>();
                wallet = player.GetComponent<PlayerWallet>();
                tail = player.GetComponent<TailAttack>();
                tongueAttack = player.GetComponent<TongueAttack>();
                heat = player.GetComponent<BodyHeat>();
                storm = Object.FindAnyObjectByType<ColdFront>();
                if (heat != null)
                    heat.Froze += () => freezeHits++;
                box = player.GetComponent<BoxCollider2D>();
                body = player.GetComponent<Rigidbody2D>();
                LoadRoute();
                // Çift zıplama basışta değil, gerçekleşince sayılır: basış kaybolursa yeniden dener.
                player.DoubleJumped += () =>
                {
                    doubleUsed = true;
                    DoubleJumps++;
                    Log($"çift zıplama x={player.transform.position.x:0.0} y={player.transform.position.y:0.0}");
                };

                crystals = Object.FindObjectsByType<Crystal>(FindObjectsInactive.Exclude).ToList();
                crystalStart.AddRange(crystals.Select(c => (Vector2)c.transform.position));
                enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude).ToList();
                foreach (var e in enemies)
                    e.Died += _ => { killed++; Log($"{e.GetType().Name} öldü x={e.transform.position.x:0.0}"); };

                health.Damaged += hp =>
                {
                    Vector2 p = player.transform.position;
                    if (p.y < -10f) { pitFalls++; teleported = true; pitPoints.Add(LastSafePoint()); Log($"UÇURUMA DÜŞTÜ (son zemin x={LastSafePoint().x:0.0}) can={hp}"); }
                    else { enemyDamageHits++; damagePoints.Add(p); Log($"hasar aldı x={p.x:0.0} y={p.y:0.0} can={hp}"); }
                };
                health.Respawned += () => teleported = true;
                health.Died += () => { deaths++; deathPoints.Add(player.transform.position); Log($"ÖLDÜ x={player.transform.position.x:0.0}"); };
                manager.Completed += () => { Completed = true; Log("LEVEL BİTTİ"); };
            }

            void Log(string message) => events.Add($"[{elapsed,6:0.0}s] {message}");

            Vector2 LastSafePoint()
            {
                for (int i = path.Count - 1; i >= 0; i--)
                    if ((int)path[i].z == (int)PlayerController.MoveState.Grounded)
                        return path[i];
                return path.Count > 0 ? (Vector2)path[0] : Vector2.zero;
            }

            public IEnumerator Play()
            {
                float lastProgressTime = 0f, sampleAt = 0f;
                bool stuckLogged = false;
                bestProgress = float.NegativeInfinity;

                while (!Completed && elapsed < TimeLimit)
                {
                    float x = player.transform.position.x;
                    UpdateRoute(box.bounds);
                    float progress = x * moveDir;
                    if (teleported || waitingForTail)
                    {
                        teleported = false;
                        bestProgress = progress;
                        lastProgressTime = elapsed;
                    }
                    if (progress > bestProgress + 0.5f)
                    {
                        bestProgress = progress;
                        lastProgressTime = elapsed;
                        stuckLogged = false;
                    }
                    // Duvara tırmanırken x değişmez ama bu da ilerlemedir; yüksek duvarın tepesinde vazgeçmesin.
                    if (player.State == PlayerController.MoveState.WallCling && player.Velocity.y > 0.1f)
                        lastProgressTime = elapsed;
                    // Asansörde, gayzerde ve kancada beklemek de ilerlemedir.
                    if (player.IsSwinging || (player.IsGrounded && (OnMover(box.bounds) || InGeyserColumn(box.bounds))))
                        lastProgressTime = elapsed;
                    float idle = elapsed - lastProgressTime;
                    if (idle > GiveUpSeconds)
                    {
                        Log($"PES ETTİ: {GiveUpSeconds:0} sn ilerleyemedi, x={x:0.0} y={player.transform.position.y:0.0}");
                        break;
                    }
                    if (idle > StuckSeconds && !stuckLogged)
                    {
                        stuckLogged = true;
                        stuckPoints.Add(player.transform.position);
                        Log($"TAKILDI x={x:0.0} y={player.transform.position.y:0.0} durum={player.State}");
                    }
                    if (idle > StuckSeconds && elapsed > recoverUntil + 2.5f)
                    {
                        // Geri çekilip yeniden dene; her denemede yön sırayla değişmesin, hep biraz geri.
                        recoverUntil = elapsed + 0.5f + Random.value * 0.6f;
                    }

                    if (elapsed >= sampleAt)
                    {
                        sampleAt = elapsed + 0.1f;
                        path.Add(new Vector3(x, player.transform.position.y, (int)player.State));
                        velocities.Add(player.Velocity);
                    }

                    if (storm != null && storm.IsRunning)
                    {
                        float lead = x - storm.FrontX;
                        stormClosest = Mathf.Min(stormClosest, lead);
                        if (!stormShot && lead < 9f)
                        {
                            stormShot = true;
                            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Temp", "Screenshots", "oyun_firtina.png"), RenderCamera());
                            Log($"fırtına yakın x={x:0.0} ara={lead:0.0}");
                        }
                    }
                    if (heat != null)
                    {
                        coldestHeat = Mathf.Min(coldestHeat, heat.Heat);
                        if (heat.Heat < 0.55f)
                            coldSeconds += Time.deltaTime;
                    }
                    Press(Decide());
                    OnFrame?.Invoke(elapsed);
                    yield return null;
                    elapsed += Time.deltaTime;
                }
                Press(new List<Key>());
            }

            void Press(List<Key> keys) =>
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));

            List<Key> Decide()
            {
                var keys = new List<Key>();
                if (!health.IsAlive)
                    return keys;

                Bounds b = box.bounds;
                Vector2 v = player.Velocity;
                var state = player.State;
                bool grounded = state == PlayerController.MoveState.Grounded;
                if (player.HasTail)
                    waitingForTail = false; // beklerken ilerleme sayacı işlemiyor, kuyruk çıkınca hemen işlesin
                if (grounded || state == PlayerController.MoveState.WallCling)
                {
                    doubleUsed = false;
                    releaseForDouble = false;
                }

                if (state == PlayerController.MoveState.Swinging)
                {
                    latchPressed = false;
                    return SwingKeys(b, v, moveDir);
                }
                if ((state == PlayerController.MoveState.Airborne || state == PlayerController.MoveState.Gliding) && WantsToLatch(b, moveDir))
                {
                    // Basış olarak algılansın: önceki karede J bırakılmış olmalı.
                    latchPressed = !latchPressed;
                    if (latchPressed)
                    {
                        swingStartedAt = elapsed;
                        keys.Add(Key.J);
                        keys.Add(moveDir > 0 ? Key.D : Key.A);
                        keys.Add(Key.Space);
                        return keys;
                    }
                }

                // Gayzerin üstünde, hedef yukarıdaysa fışkırmasını bekle.
                if (grounded && InGeyserColumn(b) && !RouteDone && route.Count > 0 && route[routeIndex].y > b.min.y + 3f)
                    return keys;

                // Hedef yukarıdaysa asansörde sabit dur: koşarsa kenardan düşer.
                if (grounded && OnMover(b) && !RouteDone && route.Count > 0
                    && route[routeIndex].y > b.min.y + 0.4f && Mathf.Abs(route[routeIndex].x - b.center.x) < 1.5f)
                    return keys;

                // Takılınca kısa bir geri çekilme
                if (elapsed < recoverUntil)
                {
                    keys.Add(recoverDir < 0 ? Key.A : Key.D);
                    if (grounded && Random.value < 0.05f) keys.Add(Key.Space);
                    return keys;
                }

                int dir = moveDir;
                Key forward = dir > 0 ? Key.D : Key.A, backward = dir > 0 ? Key.A : Key.D;
                float frontX = dir > 0 ? b.max.x : b.min.x;
                bool goRight = true, jump = false; // goRight: gidiş yönünde ilerliyor
                bool groundAhead = Physics2D.Raycast(new Vector2(frontX + dir * 0.45f, b.min.y + 0.3f), Vector2.down, 0.9f, groundMask);
                bool wallNear = Physics2D.Raycast(b.center, Vector2.right * dir, b.extents.x + 0.8f, groundMask);

                // --- Düşmanlar ---
                EnemyBase threat = enemies
                    .Where(e => e != null && e.IsAlive)
                    .Where(e => (e.transform.position.x - b.center.x) * dir > -0.6f && (e.transform.position.x - b.center.x) * dir < 8f)
                    .Where(e => Mathf.Abs(e.transform.position.y - b.min.y) < 1.2f) // masanın altındaki düşmana dil yetişmez
                    .OrderBy(e => (e.transform.position.x - b.center.x) * dir)
                    .FirstOrDefault();
                bool tongue = false, throwTail = false;
                if (threat != null && (grounded || state == PlayerController.MoveState.Airborne))
                {
                    float dx = (threat.transform.position.x - b.center.x) * dir;
                    if (dx < 2.2f)
                    {
                        goRight = false;
                        tongue = true;
                        if (player.FacingDirection != dir)
                            keys.Add(forward); // önce ona dön
                        if (dx < 0.9f)
                            keys.Add(backward); // çok yakın: geri adım
                    }
                    else if (dx < 6.5f && tail.IsReady && grounded && groundAhead) // uçurum kenarında kuyruğu harcama
                    {
                        throwTail = true;
                    }
                }
                // Tuş basışı olarak algılansın: bir kare bas, bir kare bırak.
                if (tongue) { if (!tongueWasDown) keys.Add(Key.J); tongueWasDown = !tongueWasDown; }
                else tongueWasDown = false;
                if (throwTail) { if (!tailWasDown) keys.Add(Key.K); tailWasDown = !tailWasDown; }
                else tailWasDown = false;

                if (goRight)
                {
                    keys.Add(forward);
                    // Sadece duvar önündeyken yukarı bas; çapraz basış koşuyu/süzülmeyi yavaşlatır.
                    if (wallNear || state == PlayerController.MoveState.WallCling)
                        keys.Add(Key.W);
                }

                // --- Zıplama ---
                switch (state)
                {
                    case PlayerController.MoveState.Grounded:
                        if (goRight)
                        {
                            RaycastHit2D wall = Physics2D.Raycast(new Vector2(frontX, b.min.y + 0.3f), Vector2.right * dir, 0.6f, groundMask);
                            // Tek yönlü platformun yanından geçilir, engel değildir.
                            bool solidWall = wall && !wall.collider.usedByEffector;
                            bool lowStep = solidWall && !Physics2D.Raycast(new Vector2(frontX, b.min.y + 2.6f), Vector2.right * dir, 1.2f, groundMask);
                            if (!groundAhead && !player.HasTail)
                            {
                                // Kuyruk yokken süzülemez: uçurumun kenarında kuyruğun çıkmasını bekle.
                                if (!waitingForTail) Log($"uçurum kenarında kuyruğu bekliyor x={b.center.x:0.0}");
                                waitingForTail = true;
                                keys.Remove(forward);
                                break;
                            }
                            waitingForTail = false;
                            if (!groundAhead || lowStep || WantsToRiseToTarget(b) || InThermal(b) || UnderLift(b))
                                jump = true;
                        }
                        break;

                    case PlayerController.MoveState.WallCling:
                        jump = false; // zıplarsa duvardan geriye fırlar
                        break;

                    default:
                        jump = WantsDoubleJump(b, v) ? PressCycleForDouble() : true; // aksi halde basılı tut: tam yükseklik, sonra süzülme
                        break;
                }
                // Basılı tutulan tuş yeni zıplama sayılmaz: yere inince bir kare bırak.
                // Ama yeni basılmışsa bırakma: fizik adımı gelene kadar hâlâ yerde görünür ve erken bırakış
                // zıplamayı keser (tam yükseklik yerine ~1 birim yükselirdi).
                if (grounded && elapsed - groundJumpPressedAt < 0.1f)
                    jump = true;
                else if (jump && grounded && spaceWasDown)
                    jump = false;
                else if (jump && grounded)
                    groundJumpPressedAt = elapsed;
                spaceWasDown = jump;
                if (jump)
                    keys.Add(Key.Space);
                return keys;
            }

            // --- Rapor ---

            public string WriteReport(string sceneName)
            {
                int collected = wallet.Crystals;
                var sb = new StringBuilder();
                sb.AppendLine($"=== {sceneName} ===");
                sb.AppendLine($"sonuç: {(Completed ? "BİTİRDİ" : "BİTİREMEDİ")}  süre: {elapsed:0.0} sn");
                sb.AppendLine($"kristal: {collected} / {crystalStart.Count} (yerleştirilen)  düşman: {killed} / {enemies.Count} öldürüldü");
                sb.AppendLine($"çift zıplama: {DoubleJumps}  kancadan uçuş: {swingsDone}");
                if (storm != null)
                    sb.AppendLine($"fırtınayla en yakın ara: {stormClosest:0.0}  dağıldı: {storm.HasEnded}");
                if (heat != null)
                    sb.AppendLine($"en düşük ısı: {coldestHeat:0.00}  üşüyerek geçen süre: {coldSeconds:0} sn  donma hasarı: {freezeHits}");
                sb.AppendLine($"düşman hasarı: {enemyDamageHits}  uçuruma düşme: {pitFalls}  ölüm: {deaths}  takılma: {stuckPoints.Count}");
                foreach (string e in events)
                    sb.AppendLine(e);

                string dir = System.Environment.GetEnvironmentVariable("DGB_AUTOPLAY_DIR");
                if (string.IsNullOrEmpty(dir))
                    dir = Path.Combine(Application.dataPath, "..", "Logs", "Autoplay");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, sceneName + ".txt"), sb.ToString());
                File.WriteAllText(Path.Combine(dir, sceneName + ".json"), MapJson());
                return sb.ToString();
            }

            static string F(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);
            static string P(Vector2 p) => $"[{F(p.x)},{F(p.y)}]";
            static string List(IEnumerable<Vector2> ps) => "[" + string.Join(",", ps.Select(P)) + "]";

            string MapJson()
            {
                var solids = Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude)
                    .Where(c => !c.isTrigger && (c.attachedRigidbody == null || c.attachedRigidbody.bodyType == RigidbodyType2D.Static))
                    .Where(c => c.gameObject.layer == LayerMask.NameToLayer(GameLayers.Ground) || c.gameObject.layer == 0)
                    .Select(c => c.bounds)
                    .Select(bd => $"[{F(bd.min.x)},{F(bd.min.y)},{F(bd.max.x)},{F(bd.max.y)}]");
                var collectedSet = crystals.Select((c, i) => (c, i)).Where(t => t.c == null || !t.c.gameObject.activeInHierarchy).Select(t => crystalStart[t.i]);
                var missed = crystals.Select((c, i) => (c, i)).Where(t => t.c != null && t.c.gameObject.activeInHierarchy).Select(t => crystalStart[t.i]);
                var checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Exclude).Select(c => (Vector2)c.transform.position);
                var goal = Object.FindAnyObjectByType<LevelGoal>();

                var sb = new StringBuilder("{");
                sb.Append("\"solids\":[").Append(string.Join(",", solids)).Append("],");
                sb.Append("\"path\":[").Append(string.Join(",", path.Select((p, i) => $"[{F(p.x)},{F(p.y)},{(int)p.z},{F(velocities[i].x)},{F(velocities[i].y)}]"))).Append("],");
                sb.Append("\"crystalsTaken\":").Append(List(collectedSet)).Append(',');
                sb.Append("\"crystalsMissed\":").Append(List(missed)).Append(',');
                sb.Append("\"damage\":").Append(List(damagePoints)).Append(',');
                sb.Append("\"pits\":").Append(List(pitPoints)).Append(',');
                sb.Append("\"deaths\":").Append(List(deathPoints)).Append(',');
                sb.Append("\"stuck\":").Append(List(stuckPoints)).Append(',');
                sb.Append("\"checkpoints\":").Append(List(checkpoints)).Append(',');
                sb.Append("\"goal\":").Append(goal != null ? P(goal.transform.position) : "null").Append(',');
                sb.Append($"\"completed\":{(Completed ? "true" : "false")}");
                sb.Append('}');
                return sb.ToString();
            }
        }
    }
}
