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
        const float TimeLimit = 240f;       // oyun saniyesi
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
        }

        /// <summary>Bir level oynanışı: karar verme, kayıt, rapor.</summary>
        class BotRun
        {
            readonly LevelManager manager;
            readonly PlayerController player;
            readonly PlayerHealth health;
            readonly PlayerWallet wallet;
            readonly TailAttack tail;
            readonly BoxCollider2D box;
            readonly Keyboard keyboard;
            readonly int groundMask = GameLayers.GroundMask;

            public bool Completed;
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
            bool doubleUsed;
            bool releaseForDouble;
            bool tongueWasDown, tailWasDown, waitingForTail;
            bool teleported; // checkpoint'e dönünce ilerleme ölçümü baştan başlar
            float recoverUntil = -1f;
            int recoverDir = -1;

            public BotRun(LevelManager manager, Keyboard keyboard)
            {
                this.manager = manager;
                this.keyboard = keyboard;
                player = manager.Player;
                health = player.GetComponent<PlayerHealth>();
                wallet = player.GetComponent<PlayerWallet>();
                tail = player.GetComponent<TailAttack>();
                box = player.GetComponent<BoxCollider2D>();

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
                float bestX = player.transform.position.x;
                float lastProgressTime = 0f, sampleAt = 0f;
                bool stuckLogged = false;

                while (!Completed && elapsed < TimeLimit)
                {
                    float x = player.transform.position.x;
                    if (teleported || waitingForTail)
                    {
                        teleported = false;
                        bestX = x;
                        lastProgressTime = elapsed;
                    }
                    if (x > bestX + 0.5f)
                    {
                        bestX = x;
                        lastProgressTime = elapsed;
                        stuckLogged = false;
                    }
                    // Duvara tırmanırken x değişmez ama bu da ilerlemedir; yüksek duvarın tepesinde vazgeçmesin.
                    if (player.State == PlayerController.MoveState.WallCling && player.Velocity.y > 0.1f)
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

                    Press(Decide());
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

                // Takılınca kısa bir geri çekilme
                if (elapsed < recoverUntil)
                {
                    keys.Add(recoverDir < 0 ? Key.A : Key.D);
                    if (grounded && Random.value < 0.05f) keys.Add(Key.Space);
                    return keys;
                }

                bool goRight = true, jump = false;
                bool groundAhead = Physics2D.Raycast(new Vector2(b.max.x + 0.45f, b.min.y + 0.3f), Vector2.down, 0.9f, groundMask);
                bool wallNear = Physics2D.Raycast(b.center, Vector2.right, b.extents.x + 0.8f, groundMask);

                // --- Düşmanlar ---
                EnemyBase threat = enemies
                    .Where(e => e != null && e.IsAlive)
                    .Where(e => e.transform.position.x > b.center.x - 0.6f && e.transform.position.x < b.center.x + 8f)
                    .Where(e => Mathf.Abs(e.transform.position.y - b.min.y) < 1.2f) // masanın altındaki düşmana dil yetişmez
                    .OrderBy(e => e.transform.position.x)
                    .FirstOrDefault();
                bool tongue = false, throwTail = false;
                if (threat != null && (grounded || state == PlayerController.MoveState.Airborne))
                {
                    float dx = threat.transform.position.x - b.center.x;
                    if (dx < 2.2f)
                    {
                        goRight = false;
                        tongue = true;
                        if (player.FacingDirection < 0)
                            keys.Add(Key.D); // önce ona dön
                        if (dx < 0.9f)
                            keys.Add(Key.A); // çok yakın: geri adım
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
                    keys.Add(Key.D);
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
                            RaycastHit2D wall = Physics2D.Raycast(new Vector2(b.max.x, b.min.y + 0.3f), Vector2.right, 0.6f, groundMask);
                            bool lowStep = wall && !Physics2D.Raycast(new Vector2(b.max.x, b.min.y + 2.6f), Vector2.right, 1.2f, groundMask);
                            if (!groundAhead && !player.HasTail)
                            {
                                // Kuyruk yokken süzülemez: uçurumun kenarında kuyruğun çıkmasını bekle.
                                if (!waitingForTail) Log($"uçurum kenarında kuyruğu bekliyor x={b.center.x:0.0}");
                                waitingForTail = true;
                                keys.Remove(Key.D);
                                break;
                            }
                            waitingForTail = false;
                            if (!groundAhead || lowStep)
                                jump = true;
                        }
                        break;

                    case PlayerController.MoveState.WallCling:
                        jump = false; // zıplarsa duvardan geriye fırlar
                        break;

                    default:
                        if (v.y > 0f)
                        {
                            jump = true; // tam yükseklik
                        }
                        else
                        {
                            bool groundBelow = Physics2D.Raycast(new Vector2(b.center.x, b.min.y), Vector2.down, 2f, groundMask);
                            if (!doubleUsed && !groundBelow && player.HasTail)
                            {
                                // Çift zıplama için tuşu bir kare bırak, sonra bas.
                                if (!releaseForDouble) { releaseForDouble = true; jump = false; }
                                else { doubleUsed = true; jump = true; }
                            }
                            else
                            {
                                jump = true; // süzül
                            }
                        }
                        break;
                }
                // Basılı tutulan tuş yeni zıplama sayılmaz: yere inince bir kare bırak.
                if (jump && grounded && spaceWasDown)
                    jump = false;
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
