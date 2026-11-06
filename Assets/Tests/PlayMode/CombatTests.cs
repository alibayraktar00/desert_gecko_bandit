using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DesertGecko.Collectibles;
using DesertGecko.Effects;
using DesertGecko.Enemies;
using DesertGecko.Environment;
using DesertGecko.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DesertGecko.Tests
{
    /// <summary>
    /// Savaş döngüsü: can, dil, kuyruk, akrep, kristal ve checkpoint davranışları.
    /// Her test kendi küçük dünyasını (zemin + oyuncu + gerekenler) koddan kurar.
    /// </summary>
    public class CombatTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [OneTimeSetUp]
        public void KatmanlarVar()
        {
            foreach (string layer in new[] { "Ground", "Player", "Enemy", "PlayerAttack" })
                Assert.GreaterOrEqual(LayerMask.NameToLayer(layer), 0,
                    $"'{layer}' katmanı yok; önce Desert Gecko → Test Sahnesini Kur çalıştırılmalı.");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
                if (go != null)
                    Object.Destroy(go);
            created.Clear();

            // Testler sırasında oyunun kendisinin oluşturduğu objeler
            foreach (var c in Object.FindObjectsByType<Crystal>()) Object.Destroy(c.gameObject);
            foreach (var p in Object.FindObjectsByType<TailProjectile>()) Object.Destroy(p.gameObject);
            foreach (var m in Object.FindObjectsByType<EnemyBullet>()) Object.Destroy(m.gameObject);
            foreach (var b in Object.FindObjectsByType<PixelBurst>()) Object.Destroy(b.gameObject);
        }

        // --- Can ---

        [UnityTest]
        public IEnumerator Hasar_CaniAzaltir_VeKisaSureDokunulmazlikVerir()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            yield return null;

            health.TakeDamage(1, Vector2.right);
            Assert.AreEqual(5, health.CurrentHealth);
            Assert.IsTrue(health.IsInvincible);

            health.TakeDamage(1, Vector2.right);
            Assert.AreEqual(5, health.CurrentHealth, "dokunulmazken ikinci hasar işlememeli");
        }

        [UnityTest]
        public IEnumerator Iyilesme_MaksimumuGecmez()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            yield return null;

            health.TakeDamage(3, Vector2.right);
            Assert.AreEqual(3, health.CurrentHealth);
            health.Heal(2);
            Assert.AreEqual(5, health.CurrentHealth);
            health.Heal(5);
            Assert.AreEqual(6, health.CurrentHealth);
        }

        [UnityTest]
        public IEnumerator Olum_CheckpointteDogurur_VeDusmanlariSifirlar()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            Scorpion scorpion = Scorpion(new Vector2(6f, 0f));
            yield return null;

            health.SetCheckpoint(new Vector2(-3f, 0.1f));
            scorpion.TakeDamage(3, Vector2.right);
            Assert.IsFalse(scorpion.gameObject.activeSelf, "ölen akrep gizlenmeli");

            health.TakeDamage(6, Vector2.left);
            Assert.IsFalse(health.IsAlive);

            yield return new WaitForSeconds(1.3f);

            Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
            Assert.That(health.transform.position.x, Is.EqualTo(-3f).Within(0.3f));
            Assert.IsTrue(scorpion.gameObject.activeSelf, "oyuncu doğunca akrep geri gelmeli");
            Assert.AreEqual(3, scorpion.Health);
        }

        [UnityTest]
        public IEnumerator Olum_YerindenOynamisCanliDusmaniBaslangicaDondurur()
        {
            Ground(-10f, 20f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            Scorpion scorpion = Scorpion(new Vector2(4f, 0f));
            yield return new WaitForSeconds(0.5f); // oyuncuyu görüp kovalamaya başlasın

            Assert.Less(scorpion.transform.position.x, 3.8f, "akrep kovalarken yer değiştirmeli");
            // Sıfırlandığı anki konum: hemen ardından yeniden devriyeye çıkıyor.
            // Akrep PlayerRespawned'a Awake'te abone oldu, bu yüzden bizimki ondan sonra çalışır.
            float? xAtRespawn = null;
            System.Action record = () => xAtRespawn = scorpion.transform.position.x;
            PlayerHealth.PlayerRespawned += record;
            try
            {
                health.SetCheckpoint(new Vector2(-8f, 0.1f));
                health.TakeDamage(6, Vector2.left);
                yield return new WaitForSeconds(1.2f);
            }
            finally
            {
                PlayerHealth.PlayerRespawned -= record;
            }

            Assert.IsTrue(xAtRespawn.HasValue, "oyuncu yeniden doğmalı");
            Assert.That(xAtRespawn.Value, Is.EqualTo(4f).Within(0.01f));
            Assert.AreEqual(3, scorpion.Health);
        }

        [UnityTest]
        public IEnumerator Ucurum_CaniAzaltir_VeCheckpointeDondurur()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            yield return null;

            health.SetCheckpoint(new Vector2(-4f, 0.1f));
            health.FallIntoPit(2);
            Assert.IsTrue(health.IsAlive);
            Assert.AreEqual(4, health.CurrentHealth);
            Assert.That(health.transform.position.x, Is.EqualTo(-4f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Olunce_KuyrukGeriGelir()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            var tail = health.GetComponent<TailAttack>();
            SetPrivate(tail, "projectilePrefab", ProjectileTemplate());
            yield return null;

            Assert.IsTrue(tail.TryThrow());
            health.TakeDamage(6, Vector2.left);
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(health.GetComponent<PlayerController>().HasTail);
        }

        // --- Dil ---

        [UnityTest]
        public IEnumerator Dil_OnundekiDusmaniVurur_ArkadakiniVurmaz()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            Scorpion front = Scorpion(new Vector2(1.8f, 0f));
            Scorpion behind = Scorpion(new Vector2(-1.8f, 0f));
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(health.GetComponent<TongueAttack>().TryAttack());
            Assert.AreEqual(2, front.Health);
            Assert.AreEqual(3, behind.Health);
        }

        [UnityTest]
        public IEnumerator Dil_BocegiYakalar_VeCanYeniler()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            Bug bug = BugAt(new Vector2(1.5f, 0.8f));
            yield return new WaitForFixedUpdate();

            health.TakeDamage(3, Vector2.right);
            Assert.IsTrue(health.GetComponent<TongueAttack>().TryAttack());
            Assert.AreEqual(5, health.CurrentHealth);
            Assert.IsFalse(bug.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Dil_BeklemeSuresindeTekrarSaldirmaz()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            yield return null;

            var tongue = health.GetComponent<TongueAttack>();
            Assert.IsTrue(tongue.TryAttack());
            Assert.IsFalse(tongue.TryAttack());
            yield return new WaitForSeconds(0.35f);
            Assert.IsTrue(tongue.TryAttack());
        }

        // --- Kuyruk ---

        [UnityTest]
        public IEnumerator Kuyruk_BeklemedeYok_SonraYavasYavasBuyur()
        {
            Ground(-10f, 40f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            TailAttack tail = health.GetComponent<TailAttack>();
            PlayerController controller = health.GetComponent<PlayerController>();
            SetPrivate(tail, "projectilePrefab", ProjectileTemplate());
            SetPrivate(tail, "cooldown", 0.4f);
            SetPrivate(tail, "regrowDuration", 0.6f);
            yield return new WaitForSeconds(0.1f);

            Assert.IsTrue(tail.TryThrow());
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0f, tail.TailGrowth, "beklemede kuyruk hiç olmamalı");
            Assert.IsFalse(controller.HasTail);

            yield return new WaitForSeconds(0.5f);
            Assert.That(tail.TailGrowth, Is.GreaterThan(0f).And.LessThan(1f), "bekleme bitince büyümeli");
            Assert.IsFalse(controller.HasTail, "tam büyümeden kullanılamamalı");

            yield return new WaitForSeconds(0.45f);
            Assert.AreEqual(1f, tail.TailGrowth);
            Assert.IsTrue(controller.HasTail);
        }

        [UnityTest]
        public IEnumerator Kuyruk_FirlatincaGider_BeklemeSonundaGeriGelir()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            var tail = health.GetComponent<TailAttack>();
            var controller = health.GetComponent<PlayerController>();
            SetPrivate(tail, "projectilePrefab", ProjectileTemplate());
            SetPrivate(tail, "cooldown", 0.3f);
            SetPrivate(tail, "regrowDuration", 0f);
            yield return null;

            Assert.IsTrue(tail.TryThrow());
            Assert.IsFalse(controller.HasTail);
            Assert.IsFalse(tail.TryThrow(), "kuyruk yokken tekrar fırlatılamamalı");
            Assert.Less(tail.CooldownProgress, 0.5f);

            yield return new WaitForSeconds(0.45f);
            Assert.IsTrue(controller.HasTail);
            Assert.AreEqual(1f, tail.CooldownProgress);
        }

        [UnityTest]
        public IEnumerator Kuyruk_YerdekiAkrebiOldurur_VeKristalDuser()
        {
            Ground(-10f, 20f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            var tail = health.GetComponent<TailAttack>();
            SetPrivate(tail, "projectilePrefab", ProjectileTemplate());
            Scorpion scorpion = Scorpion(new Vector2(4f, 0f));
            SetPrivate(scorpion.gameObject.AddComponent<CrystalDrop>(), "crystalPrefab", CrystalTemplate());
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(tail.TryThrow());
            yield return new WaitForSeconds(0.6f);

            Assert.IsFalse(scorpion.gameObject.activeSelf, "3 hasarlık kuyruk 3 canlı akrebi öldürmeli");
            Assert.GreaterOrEqual(Object.FindObjectsByType<Crystal>().Length, 2, "şablon + en az 1 düşen kristal");
        }

        [UnityTest]
        public IEnumerator Akrep_DirilinceTekrarKristalDusurmez()
        {
            Ground(-10f, 20f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            Scorpion scorpion = Scorpion(new Vector2(8f, 0f));
            SetPrivate(scorpion.gameObject.AddComponent<CrystalDrop>(), "crystalPrefab", CrystalTemplate());
            yield return null;

            scorpion.TakeDamage(3, Vector2.right);
            int afterFirstKill = Object.FindObjectsByType<Crystal>().Length;

            health.TakeDamage(6, Vector2.left); // öl ve akrebi dirilt
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(scorpion.gameObject.activeSelf);
            scorpion.TakeDamage(3, Vector2.right);

            Assert.AreEqual(afterFirstKill, Object.FindObjectsByType<Crystal>().Length, "ikinci ölümde kristal düşmemeli");
        }

        // --- Akrep ---

        [UnityTest]
        public IEnumerator Akrep_TemasHasariVerir()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            Scorpion(new Vector2(0.3f, 0f));

            for (int i = 0; i < 5; i++)
                yield return new WaitForFixedUpdate();
            Assert.AreEqual(5, health.CurrentHealth);
        }

        [UnityTest]
        public IEnumerator Akrep_YakindakiOyuncuyuKovalar_UzaklasincaBirakir()
        {
            Ground(-10f, 30f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            Scorpion scorpion = Scorpion(new Vector2(3.5f, 0f));
            for (int i = 0; i < 3; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(scorpion.IsChasing);
            Assert.AreEqual(-1, scorpion.FacingDirection, "oyuncuya (sola) dönmeli");

            health.GetComponent<PlayerController>().Teleport(new Vector2(25f, 0.05f));
            for (int i = 0; i < 3; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(scorpion.IsChasing);
        }

        [UnityTest]
        public IEnumerator Akrep_UcurumKenarindaDurur()
        {
            Ground(-2f, 2f);
            Scorpion scorpion = Scorpion(new Vector2(1f, 0f));
            SetPrivate(scorpion, "patrolDistance", 10f); // sınır uçurum olsun
            for (int i = 0; i < 150; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(scorpion.transform.position.x, Is.InRange(-2f, 2f), "akrep zeminden düşmemeli");
            Assert.Greater(scorpion.transform.position.y, -0.5f);
        }

        [UnityTest]
        public IEnumerator Akrep_KenardanUzakDurur()
        {
            Ground(-3f, 3f);
            Scorpion scorpion = Scorpion(new Vector2(0f, 0f));
            SetPrivate(scorpion, "patrolDistance", 10f);

            float widest = 0f;
            for (int i = 0; i < 300; i++)
            {
                yield return new WaitForFixedUpdate();
                widest = Mathf.Max(widest, Mathf.Abs(scorpion.transform.position.x));
            }
            Assert.Greater(widest, 0.5f, "devriye gezmeli");
            Assert.Less(widest, 2f, "uçurum kenarına kadar gelmemeli");
        }

        [UnityTest]
        public IEnumerator Akrep_AsagidakiOyuncuyuKenardaBeklemez()
        {
            Ground(-10f, 10f);
            var cliff = Track(new GameObject("Yamac") { layer = LayerMask.NameToLayer("Ground") });
            cliff.transform.position = new Vector3(5f, 1f, 0f);
            cliff.AddComponent<BoxCollider2D>().size = new Vector2(10f, 2f);
            Player(new Vector2(-0.5f, 0.05f)); // 2 birim aşağıda, algılama kutusunun içinde
            Scorpion scorpion = Scorpion(new Vector2(3.5f, 2f));

            for (int i = 0; i < 10; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(scorpion.IsChasing);
        }

        [UnityTest]
        public IEnumerator Temas_AkrepDurur_OyuncuUzaklasir()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            Scorpion scorpion = Scorpion(new Vector2(0.3f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(5, health.CurrentHealth);
            float scorpionX = scorpion.transform.position.x;

            yield return new WaitForSeconds(0.3f);
            Assert.That(scorpion.transform.position.x, Is.EqualTo(scorpionX).Within(0.15f), "temastan sonra peşinden gitmemeli");
            Assert.Greater(Mathf.Abs(health.transform.position.x - scorpion.transform.position.x), 1f, "oyuncu uzaklaşmalı");
        }

        [UnityTest]
        public IEnumerator Temas_DokunulmazlikBitinceIcindeyseHasarsizItilir()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            SetPrivate(health, "invincibleDuration", 0.4f);
            Scorpion scorpion = Scorpion(new Vector2(0.3f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(5, health.CurrentHealth);

            yield return new WaitForSeconds(0.1f);
            health.GetComponent<PlayerController>().Teleport(scorpion.transform.position + Vector3.up * 0.05f);
            yield return new WaitForSeconds(0.4f);

            Assert.AreEqual(5, health.CurrentHealth, "içinde kaldı diye yeniden can yakmamalı");
            Assert.Greater(Mathf.Abs(health.transform.position.x - scorpion.transform.position.x), 0.4f, "düşmandan itilmeli");
        }

        [UnityTest]
        public IEnumerator GeriTepme_OyuncuyuUcurumaAtmaz()
        {
            Ground(-1f, 10f);
            PlayerHealth health = Player(new Vector2(0.5f, 0.05f));
            yield return new WaitForFixedUpdate();

            health.TakeDamage(1, Vector2.left);
            yield return new WaitForSeconds(1f);
            Assert.Greater(health.transform.position.y, -0.5f, "kenar yakınında kısa itilmeli");
        }

        // --- Duvara yapışma ---

        [UnityTest]
        public IEnumerator Basamaga_Yapismaz_YuksekDuvaraYapisir()
        {
            // Atama eski ayar nesnesini yok eder; geri yüklemek için kopyası saklanır.
            var savedSettings = Object.Instantiate(InputSystem.settings);
            var settings = Object.Instantiate(InputSystem.settings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("TestKeyboard");
            try
            {
                Ground(-10f, 10f);
                var step = Track(new GameObject("Basamak") { layer = LayerMask.NameToLayer("Ground") });
                step.transform.position = new Vector3(2f, 0.5f, 0f);
                step.AddComponent<BoxCollider2D>().size = new Vector2(2f, 1f);
                Wall(7f); // 0-4 arası yüksek duvar
                PlayerController player = Player(new Vector2(0.6f, 0.05f)).GetComponent<PlayerController>();
                yield return new WaitForFixedUpdate();

                // W: yerdeyken de yapışmayı dener; basamakta bunu yapmamalı.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.W, Key.Space));
                bool clungToStep = false, clungToWall = false;
                for (float t = 0f; t < 3f && !clungToWall; t += Time.fixedDeltaTime)
                {
                    yield return new WaitForFixedUpdate();
                    if (player.State != PlayerController.MoveState.WallCling)
                        continue;
                    if (player.transform.position.x < 5f) clungToStep = true;
                    else clungToWall = true;
                }
                Assert.IsFalse(clungToStep, "1 birimlik basamağa yapışmamalı");
                Assert.IsTrue(clungToWall, "yüksek duvara yapışmalı");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = savedSettings;
            }
        }

        // --- Toplanabilirler ve checkpoint ---

        [UnityTest]
        public IEnumerator Kristal_DokununcaToplanir()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            Crystal crystal = CrystalAt(new Vector2(0f, 0.5f));
            for (int i = 0; i < 3; i++)
                yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, health.GetComponent<PlayerWallet>().Crystals);
            Assert.IsTrue(crystal == null, "toplanan kristal yok olmalı");
        }

        [UnityTest]
        public IEnumerator Kristal_HareketsizOyuncununUstuneDusunceToplanir()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            yield return new WaitForSeconds(1.5f); // oyuncu yere otursun, hareketsiz kalsın

            Crystal crystal = CrystalAt(new Vector2(0f, 2.5f));
            crystal.Launch(Vector2.zero);
            yield return new WaitForSeconds(1f);

            Assert.AreEqual(1, health.GetComponent<PlayerWallet>().Crystals);
        }

        [UnityTest]
        public IEnumerator Checkpoint_DokununcaAktifOlur()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            var go = Track(new GameObject("Checkpoint"));
            go.transform.position = new Vector3(4f, 0f, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1f, 2f);
            col.offset = new Vector2(0f, 1f);
            var checkpoint = go.AddComponent<Checkpoint>();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(checkpoint.IsActive);

            health.GetComponent<PlayerController>().Teleport(new Vector2(4f, 0.05f));
            for (int i = 0; i < 3; i++)
                yield return new WaitForFixedUpdate();

            Assert.IsTrue(checkpoint.IsActive);
            Assert.That(health.CheckpointPosition.x, Is.EqualTo(4f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Checkpoint_Tutusur_DumanDongusuOynar_YenisiYaninca_Soner()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            Checkpoint first = Object.Instantiate(LoadPrefab<Checkpoint>("Checkpoint"), new Vector2(-5f, 0f), Quaternion.identity);
            Checkpoint second = Object.Instantiate(LoadPrefab<Checkpoint>("Checkpoint"), new Vector2(5f, 0f), Quaternion.identity);
            Track(first.gameObject);
            Track(second.gameObject);
            yield return null;
            Sprite off = first.CurrentFrame;
            Assert.AreEqual("kapali", off.name);

            first.Activate(health);
            yield return new WaitForSeconds(0.1f);
            StringAssert.StartsWith("yanis_", first.CurrentFrame.name, "önce fener tutuşmalı");

            yield return new WaitForSeconds(0.5f);
            StringAssert.StartsWith("aktif_", first.CurrentFrame.name, "tutuşma bitince tütmeli");
            Sprite shown = first.CurrentFrame;
            yield return new WaitForSeconds(0.25f);
            Assert.AreNotEqual(shown, first.CurrentFrame, "duman döngüsü ilerlemeli");
            Assert.AreEqual(off, second.CurrentFrame, "ötekisi kapalı kalmalı");

            second.Activate(health);
            yield return null;
            Assert.IsFalse(first.IsActive);
            Assert.AreEqual(off, first.CurrentFrame, "yenisi yanınca eskisi sönmeli");
            Assert.That(health.CheckpointPosition.x, Is.EqualTo(5f).Within(0.01f));
        }

        // --- Yılan silahşor ---

        // --- Madenci Armadillo ---

        [UnityTest]
        public IEnumerator Armadillo_UyarirKapanirYuvarlanir_ZirhiIslemez()
        {
            Ground(-20f, 20f);
            Player(new Vector2(-7.5f, 0.05f));
            Armadillo armadillo = Zirhli(new Vector2(0f, 0.05f));

            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(Armadillo.ArmadilloState.Notice, armadillo.State, "önce uyarı vermeli");

            yield return new WaitForSeconds(0.75f);
            Assert.AreEqual(Armadillo.ArmadilloState.Roll, armadillo.State);
            Assert.Less(armadillo.GetComponent<Rigidbody2D>().linearVelocity.x, -8f, "oyuncuya doğru hızla yuvarlanmalı");

            int before = armadillo.Health;
            armadillo.TakeDamage(1, Vector2.right);
            Assert.AreEqual(before, armadillo.Health, "yuvarlanırken hasar almamalı");
        }

        [UnityTest]
        public IEnumerator Armadillo_DuvaraCarpinca_Sersemler_VurulabilirSonraToparlanir()
        {
            Ground(-20f, 20f);
            Wall(-3f);
            PlayerHealth player = Player(new Vector2(-1.5f, 0.05f));
            Armadillo armadillo = Zirhli(new Vector2(3f, 0.05f));

            // Oyuncu havada tutulur: top ona çarpıp durmasın, duvara kadar gitsin.
            var movement = player.GetComponent<PlayerController>();
            float limit = Time.time + 3f;
            while (armadillo.State != Armadillo.ArmadilloState.Daze && Time.time < limit)
            {
                movement.Teleport(new Vector2(-1.5f, 1.4f));
                yield return null;
            }
            Assert.AreEqual(Armadillo.ArmadilloState.Daze, armadillo.State, "duvara çarpınca sersemlemeli");

            int before = armadillo.Health;
            armadillo.TakeDamage(1, Vector2.right);
            Assert.AreEqual(before - 1, armadillo.Health, "sersemken hasar almalı");
            Assert.AreEqual(Armadillo.ArmadilloState.Curl, armadillo.State, "vurulunca hemen kapanmalı");

            before = armadillo.Health;
            armadillo.TakeDamage(1, Vector2.right);
            Assert.AreEqual(before, armadillo.Health, "kapanırken ikinci darbe işlememeli");

            while (armadillo.State == Armadillo.ArmadilloState.Curl)
                yield return null;
            Assert.AreEqual(Armadillo.ArmadilloState.Roll, armadillo.State, "vurana doğru yuvarlanmalı");
        }

        [UnityTest]
        public IEnumerator Armadillo_OyuncuyuGecinceAcilir_GeriDoner()
        {
            Ground(-30f, 30f);
            PlayerHealth player = Player(new Vector2(-4f, 0.05f));
            Armadillo armadillo = Zirhli(new Vector2(0f, 0.05f));

            float limit = Time.time + 3f;
            while (armadillo.State != Armadillo.ArmadilloState.Unfold && Time.time < limit)
                yield return null;
            Assert.AreEqual(Armadillo.ArmadilloState.Unfold, armadillo.State, "oyuncuyu geçince açılmalı");
            Assert.Less(armadillo.transform.position.x, -2f, "oyuncuya kadar yuvarlanmış olmalı");
            Assert.Greater(armadillo.transform.position.x, -7f, "çok uzağa gitmemeli");

            yield return new WaitForSeconds(0.6f);
            int toPlayer = player.transform.position.x >= armadillo.transform.position.x ? 1 : -1;
            Assert.AreEqual(toPlayer, armadillo.FacingDirection, "oyuncuya dönmeli");
            Assert.That(armadillo.State, Is.EqualTo(Armadillo.ArmadilloState.Notice).Or.EqualTo(Armadillo.ArmadilloState.Swing), "yeniden saldırmalı");
        }

        [UnityTest]
        public IEnumerator Armadillo_YakindakiniKazmaylaVurur()
        {
            Ground(-20f, 20f);
            PlayerHealth health = Player(new Vector2(-1.5f, 0.05f));
            Armadillo armadillo = Zirhli(new Vector2(0f, 0.05f));

            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(Armadillo.ArmadilloState.Swing, armadillo.State, "yakındakine kazma sallamalı");
            yield return new WaitForSeconds(0.5f);
            Assert.Less(health.CurrentHealth, health.MaxHealth, "kazma can yakmalı");
        }

        [UnityTest]
        public IEnumerator Dusman_DarbeSonrasiKisaSureVurulamaz()
        {
            Ground(-20f, 20f);
            Player(new Vector2(-15f, 0.05f));
            Scorpion scorpion = Object.Instantiate(LoadPrefab<Scorpion>("Scorpion"), new Vector2(0f, 0.05f), Quaternion.identity);
            Track(scorpion.gameObject);
            yield return null;

            scorpion.TakeDamage(1, Vector2.right);
            scorpion.TakeDamage(1, Vector2.right);
            Assert.AreEqual(2, scorpion.Health, "koruma süresinde ikinci darbe işlememeli");

            yield return new WaitForSeconds(0.75f);
            scorpion.TakeDamage(1, Vector2.right);
            Assert.AreEqual(1, scorpion.Health, "koruma bitince vurulabilmeli");
        }

        [UnityTest]
        public IEnumerator Akrep_GerilipSokar_OyuncununIcindenGecmez()
        {
            Ground(-20f, 20f);
            PlayerHealth health = Player(new Vector2(-3f, 0.05f));
            Scorpion scorpion = Object.Instantiate(LoadPrefab<Scorpion>("Scorpion"), new Vector2(0f, 0.05f), Quaternion.identity);
            Track(scorpion.gameObject);
            yield return null;

            scorpion.TakeDamage(1, Vector2.right);
            bool sawWindup = false, sawStrike = false;
            float limit = Time.time + 3f;
            while ((scorpion.Counter != DesertGecko.Enemies.Scorpion.CounterPhase.None) && Time.time < limit)
            {
                sawWindup |= scorpion.Counter == DesertGecko.Enemies.Scorpion.CounterPhase.Windup;
                sawStrike |= scorpion.Counter == DesertGecko.Enemies.Scorpion.CounterPhase.Strike;
                Assert.Greater(scorpion.transform.position.x, health.transform.position.x + 0.5f, "oyuncuyla üst üste binmemeli");
                Assert.Less(Mathf.Abs(scorpion.GetComponent<Rigidbody2D>().linearVelocity.x), 8f, "bir anda fırlamamalı");
                yield return null;
            }
            Assert.IsTrue(sawWindup, "önce gerilerek uyarmalı");
            Assert.IsTrue(sawStrike, "sonra sokmalı");
        }

        [UnityTest]
        public IEnumerator Akrep_IgnesiMenzildekiniYakar()
        {
            Ground(-20f, 20f);
            PlayerHealth health = Player(new Vector2(-0.9f, 0.05f));
            Scorpion scorpion = Object.Instantiate(LoadPrefab<Scorpion>("Scorpion"), new Vector2(0.5f, 0.05f), Quaternion.identity);
            Track(scorpion.gameObject);
            yield return null;

            scorpion.TakeDamage(1, Vector2.right);
            float limit = Time.time + 2f;
            while (health.CurrentHealth == health.MaxHealth && Time.time < limit)
                yield return null;
            Assert.Less(health.CurrentHealth, health.MaxHealth, "yakındaki oyuncuyu sokmalı");
        }

        [UnityTest]
        public IEnumerator Yilan_VurulunaUyarmadanAtesEder()
        {
            Ground(-20f, 20f);
            Player(new Vector2(-3f, 0.05f));
            Rattlesnake snake = Snake(new Vector2(0f, 0.05f));
            yield return null;

            snake.TakeDamage(1, Vector2.right);
            float limit = Time.time + 1f;
            while (snake.State != Rattlesnake.AttackState.Shoot && Time.time < limit)
            {
                Assert.AreNotEqual(Rattlesnake.AttackState.Rattle, snake.State, "çıngırak çalmamalı");
                yield return null;
            }
            Assert.AreEqual(Rattlesnake.AttackState.Shoot, snake.State, "kısa sürede ateş etmeli");
        }

        // --- Çalı Faresi ---

        [UnityTest]
        public IEnumerator Fare_GizliykenHareketsiz_UzaktakineTepkiVermez()
        {
            Ground(-20f, 20f);
            PlayerHealth health = Player(new Vector2(-2f, 0.05f));
            TumbleweedMouse mouse = Fare(new Vector2(0f, 0.05f));
            yield return new WaitForSeconds(0.1f);
            float x = mouse.transform.position.x;

            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(TumbleweedMouse.MouseState.Disguised, mouse.State, "üstüne gelinmeden çıkmamalı");
            Assert.AreEqual(x, mouse.transform.position.x, 0.01f, "hiç hareket etmemeli");
            Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
        }

        [UnityTest]
        public IEnumerator Fare_UstuneGeleniFirlatir_CikarKovalar_BirDahaGizlenmez()
        {
            Ground(-30f, 30f);
            PlayerHealth health = Player(new Vector2(-0.4f, 0.05f));
            TumbleweedMouse mouse = Fare(new Vector2(0f, 0.05f));
            var body = health.GetComponent<Rigidbody2D>();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(TumbleweedMouse.MouseState.Reveal, mouse.State, "üstüne gelince çıkmalı");
            Assert.Less(body.linearVelocity.x, -3f, "oyuncu geri fırlamalı");
            Assert.Greater(body.linearVelocity.y, 2f, "oyuncu havalanmalı");
            Assert.AreEqual(health.MaxHealth, health.CurrentHealth, "fırlatma can yakmamalı");

            health.GetComponent<PlayerController>().Teleport(new Vector2(-10f, 0.05f));
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(TumbleweedMouse.MouseState.Chase, mouse.State, "peşine düşmeli");
            Assert.Less(mouse.GetComponent<Rigidbody2D>().linearVelocity.x, -5f);

            float limit = Time.time + 4f;
            while (mouse.State != TumbleweedMouse.MouseState.Stab && Time.time < limit)
            {
                Assert.Greater(mouse.transform.position.x, health.transform.position.x + 0.4f, "içinden geçmemeli");
                yield return null;
            }
            Assert.AreEqual(TumbleweedMouse.MouseState.Stab, mouse.State, "yetişince bıçaklamalı");

            health.GetComponent<PlayerController>().Teleport(new Vector2(20f, 0.05f));
            yield return new WaitForSeconds(2f);
            Assert.IsFalse(mouse.IsDisguised, "bir daha çalıya girmemeli");
        }

        [UnityTest]
        public IEnumerator Fare_DiliIkiKezSavusturur_UcuncudeOlur()
        {
            Ground(-20f, 20f);
            Player(new Vector2(-3f, 0.05f));
            TumbleweedMouse mouse = Fare(new Vector2(0f, 0.05f));
            yield return null;

            mouse.TakeDamage(1, Vector2.right); // çalıya vurmak ortaya çıkarır, savuşturma harcamaz
            Assert.AreEqual(TumbleweedMouse.MouseState.Reveal, mouse.State);
            Assert.AreEqual(2, mouse.ParriesLeft);

            yield return new WaitForSeconds(0.6f);
            mouse.TakeDamage(1, Vector2.right);
            Assert.IsTrue(mouse.IsAlive, "ilk dili savuşturmalı");
            Assert.AreEqual(1, mouse.ParriesLeft);
            mouse.TakeDamage(1, Vector2.right);
            Assert.IsTrue(mouse.IsAlive, "ikinci dili savuşturmalı");
            Assert.AreEqual(0, mouse.ParriesLeft);
            mouse.TakeDamage(1, Vector2.right);
            Assert.IsFalse(mouse.IsAlive, "üçüncü dilde ölmeli");
        }

        [UnityTest]
        public IEnumerator Fare_YakindakiniSavusturunca_KarsiSaldirir_KuyrukDelipGecer()
        {
            Ground(-20f, 20f);
            Player(new Vector2(-1.2f, 0.05f));
            TumbleweedMouse mouse = Fare(new Vector2(0f, 0.05f));
            yield return null;

            mouse.TakeDamage(1, Vector2.right);
            yield return new WaitForSeconds(0.6f);
            mouse.TakeDamage(1, Vector2.right);
            Assert.AreEqual(TumbleweedMouse.MouseState.Stab, mouse.State, "savuşturunca hemen bıçaklamalı");

            mouse.TakeDamage(3, Vector2.right);
            Assert.IsFalse(mouse.IsAlive, "kuyruk savuşturulamamalı");
        }

        // --- Platform altından takip ---

        void Shelf(float xMin, float xMax, float top)
        {
            var go = Track(new GameObject("Shelf") { layer = LayerMask.NameToLayer("Ground") });
            go.transform.position = new Vector3((xMin + xMax) / 2f, top - 0.25f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(xMax - xMin, 0.5f);
        }

        [UnityTest]
        public IEnumerator Takip_PlatformdakiOyuncuyuAltindanKovalamaz()
        {
            Ground(-20f, 20f);
            Shelf(-6f, 6f, 2.2f);
            Player(new Vector2(-2f, 2.3f));
            Scorpion scorpion = Object.Instantiate(LoadPrefab<Scorpion>("Scorpion"), new Vector2(0f, 0.05f), Quaternion.identity);
            Track(scorpion.gameObject);

            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(scorpion.IsChasing, "üstteki platformda duran oyuncuyu kovalamamalı");
        }

        [UnityTest]
        public IEnumerator Takip_ZiplayanOyuncuyuKovalamayiBirakmaz()
        {
            Ground(-20f, 20f);
            PlayerHealth health = Player(new Vector2(-3f, 0.05f));
            Scorpion scorpion = Object.Instantiate(LoadPrefab<Scorpion>("Scorpion"), new Vector2(0f, 0.05f), Quaternion.identity);
            Track(scorpion.gameObject);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(scorpion.IsChasing);

            health.GetComponent<PlayerController>().Teleport(new Vector2(-3f, 1.5f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(scorpion.IsChasing, "havadaki oyuncu yüzünden bırakmamalı");
        }

        [UnityTest]
        public IEnumerator Armadillo_DuvarArkasindakiniGormez()
        {
            Ground(-20f, 20f);
            Wall(-3f);
            Player(new Vector2(-4.5f, 0.05f));
            Armadillo armadillo = Zirhli(new Vector2(0f, 0.05f));
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(Armadillo.ArmadilloState.Patrol, armadillo.State);
        }

        [UnityTest]
        public IEnumerator Armadillo_UcurumKenarindaDurur_Dusmez()
        {
            Ground(-6f, 20f);
            Player(new Vector2(-5.5f, 0.05f));
            Armadillo armadillo = Zirhli(new Vector2(0f, 0.05f));
            float limit = Time.time + 3f;
            while (armadillo.State != Armadillo.ArmadilloState.Daze && Time.time < limit)
                yield return null;
            Assert.AreEqual(Armadillo.ArmadilloState.Daze, armadillo.State, "kenarda çarpmış gibi sersemlemeli");
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(armadillo.transform.position.y, -0.5f, "uçuruma düşmemeli");
        }

        [UnityTest]
        public IEnumerator Cakal_OnceIrkilir_SonraOyuncudanHizliKovalar()
        {
            Ground(-20f, 10f);
            Player(new Vector2(-6f, 0.05f));
            Jackal jackal = Cakal(new Vector2(0f, 0.05f));

            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(Jackal.JackalState.Alert, jackal.State, "menzile girince önce irkilmeli");
            Assert.AreEqual(0f, jackal.transform.position.x, 0.4f, "irkilirken yerinde durmalı");

            yield return new WaitForSeconds(0.45f);
            Assert.AreEqual(Jackal.JackalState.None, jackal.State);
            Assert.IsTrue(jackal.IsChasing);
            Assert.Less(jackal.GetComponent<Rigidbody2D>().linearVelocity.x, -5.5f, "oyuncudan hızlı koşmalı");
        }

        [UnityTest]
        public IEnumerator Cakal_YakindaDurur_DogrultmaSonrasiSacmaAtar()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(-2f, 0.05f));
            Jackal jackal = Cakal(new Vector2(0f, 0.05f));

            yield return new WaitForSeconds(0.6f); // irkilme bitti, nişan alıyor
            Assert.AreEqual(Jackal.JackalState.Aim, jackal.State, "yakındaki oyuncuya nişan almalı");
            Assert.AreEqual(6, health.CurrentHealth, "doğrultma bitmeden vurmamalı");
            float before = jackal.transform.position.x;

            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(4, health.CurrentHealth, "saçma tam kalp götürmeli");
            Assert.Greater(jackal.transform.position.x, before + 0.2f, "geri tepmeyle geriye kaymalı");

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(Jackal.JackalState.Reload, jackal.State, "atıştan sonra doldurmalı");
        }

        [UnityTest]
        public IEnumerator Cakal_DuvarArkasindakiniVuramaz()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(-2f, 0.05f));
            Wall(-1f);
            Cakal(new Vector2(0f, 0.05f));

            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(6, health.CurrentHealth, "saçma duvardan geçmemeli");
        }

        [UnityTest]
        public IEnumerator Cakal_MenzilDisindakiniVuramaz_SacmaKisaMenzilli()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(-2.2f, 0.05f));
            Jackal jackal = Cakal(new Vector2(0f, 0.05f));

            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(Jackal.JackalState.Aim, jackal.State);
            health.transform.position = new Vector3(-6f, 0.05f, 0f); // nişan alırken kaçtı

            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(6, health.CurrentHealth, "menzilden kaçan oyuncu vurulmamalı");
        }

        [UnityTest]
        public IEnumerator Cakal_Olunce_GovdeUcar_SapkaYavasDuser()
        {
            Ground(-10f, 10f);
            Player(new Vector2(-6f, 0.05f));
            Jackal jackal = Cakal(new Vector2(0f, 0.05f));
            yield return new WaitForSeconds(0.1f);

            jackal.TakeDamage(jackal.Health, Vector2.right);
            yield return null;

            Assert.IsFalse(jackal.gameObject.activeSelf, "ölünce gizlenmeli");
            JackalDefeat effect = Object.FindAnyObjectByType<JackalDefeat>();
            Assert.IsNotNull(effect, "yenilgi sahnesi oynamalı");
            Track(effect.gameObject);
            Transform body = effect.transform.Find("Govde"), hat = effect.transform.Find("Sapka");

            yield return new WaitForSeconds(0.8f);
            Assert.Greater(body.position.x, 1f, "gövde oyuncudan uzağa uçmalı");
            Assert.AreEqual(0f, body.position.y, 0.15f, "gövde yere kapaklanmalı");
            Assert.Greater(hat.position.y, 0.5f, "gövde yere düştüğünde şapka hâlâ havada olmalı");
        }

        [UnityTest]
        public IEnumerator Yilan_GorunceHemenAtesEtmez_UyaridanSonraVurur()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(-4f, 0.05f));
            Rattlesnake snake = Snake(new Vector2(0f, 0.05f));

            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(Rattlesnake.AttackState.Rattle, snake.State, "önündeki oyuncuyu görünce çıngırak çalmalı");
            Assert.IsTrue(snake.GetComponentInChildren<SpriteRenderer>().flipX, "sola bakarken aynalanmalı");

            yield return new WaitForSeconds(0.5f);
            Assert.IsNull(Object.FindAnyObjectByType<EnemyBullet>(), "uyarı bitmeden ateş etmemeli");
            Assert.AreEqual(6, health.CurrentHealth);

            yield return new WaitForSeconds(1.1f);
            Assert.AreEqual(5, health.CurrentHealth, "uyarıdan sonra mermi oyuncuya çarpmalı");
        }

        [UnityTest]
        public IEnumerator Yilan_ArkasindakiOyuncuyuGormez()
        {
            Ground(-10f, 10f);
            Player(new Vector2(4f, 0.05f));
            Rattlesnake snake = Snake(new Vector2(0f, 0.05f)); // devriyeye sola bakarak başlar

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(Rattlesnake.AttackState.None, snake.State);
        }

        [UnityTest]
        public IEnumerator Yilan_IcindenGecipArkasindaKalanOyuncuyuGormez()
        {
            Ground(-10f, 10f);
            Player(new Vector2(0.2f, 0.05f)); // görüş ışını oyuncunun collider'ı içinde başlar
            Rattlesnake snake = Snake(new Vector2(0f, 0.05f)); // sola bakar

            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(Rattlesnake.AttackState.None, snake.State);
        }

        [UnityTest]
        public IEnumerator Yilan_DuvarArkasindakiOyuncuyuGormez()
        {
            Ground(-10f, 10f);
            Wall(-2f);
            Player(new Vector2(-4f, 0.05f));
            Rattlesnake snake = Snake(new Vector2(0f, 0.05f));

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(Rattlesnake.AttackState.None, snake.State);
        }

        [UnityTest]
        public IEnumerator Yilan_UyariSirasindaVurulursaVuranaKarsiAtesEder()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(-4f, 0.05f));
            Rattlesnake snake = Snake(new Vector2(0f, 0.05f));
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(Rattlesnake.AttackState.Rattle, snake.State);

            health.GetComponent<PlayerController>().Teleport(new Vector2(4f, 0.05f));
            snake.TakeDamage(1, Vector2.left);
            Assert.AreNotEqual(Rattlesnake.AttackState.Rattle, snake.State, "çıngırağı kesilmeli");
            Assert.AreEqual(1, snake.FacingDirection, "saldırana dönmeli");

            // Karşı atış ~0.45 sn sonra çıkar, mermi 4 birimi ~0.45 sn'de geçer.
            yield return new WaitForSeconds(1.1f);
            Assert.Less(health.CurrentHealth, 6, "vurana karşılık vermeli");
        }

        [UnityTest]
        public IEnumerator Yilan_VurulunceSaldirganaDoner_SonraOnaNisanAlir()
        {
            Ground(-10f, 10f);
            Player(new Vector2(4f, 0.05f));
            Rattlesnake snake = Snake(new Vector2(0f, 0.05f));
            yield return new WaitForSeconds(0.1f);

            snake.TakeDamage(1, Vector2.left);
            Assert.AreEqual(1, snake.FacingDirection, "saldırana dönmeli");

            // Önce karşı atış (~1 sn), sonra normal bekleme (1.5 sn), sonra çıngırak.
            yield return new WaitForSeconds(2.7f);
            Assert.AreEqual(1, snake.FacingDirection);
            Assert.IsFalse(snake.GetComponentInChildren<SpriteRenderer>().flipX, "sağa bakarken aynalanmamalı");
            Assert.AreEqual(Rattlesnake.AttackState.Rattle, snake.State, "bekleme bitince sağdaki oyuncuya nişan almalı");
            Assert.IsFalse(snake.IsChasing, "silahşor kovalamaz");
        }

        [UnityTest]
        public IEnumerator Yilan_SifirlaninciaSaldirisiBiter_MermiAtmaz()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(-4f, 0.05f));
            Rattlesnake snake = Snake(new Vector2(0f, 0.05f));
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(Rattlesnake.AttackState.Rattle, snake.State);

            snake.ResetEnemy(); // sola bakar; oyuncu sağa geçince yeniden nişan almaz
            health.GetComponent<PlayerController>().Teleport(new Vector2(6f, 0.05f));
            Assert.AreEqual(Rattlesnake.AttackState.None, snake.State);

            yield return new WaitForSeconds(1.2f);
            Assert.IsNull(Object.FindAnyObjectByType<EnemyBullet>(), "iptal edilen saldırı mermi atmamalı");
        }

        [UnityTest]
        public IEnumerator Mermi_OyuncuYenidenDogunucaSilinir()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            EnemyBullet bullet = Object.Instantiate(LoadPrefab<EnemyBullet>("EnemyBullet"), new Vector3(-8f, 5f, 0f), Quaternion.identity);
            bullet.Launch(-1, new Vector2(-8f, 5f));

            health.TakeDamage(health.MaxHealth, Vector2.left);
            yield return new WaitForSeconds(1.2f); // yeniden doğuş 1 sn, mermi ömrü 2.5 sn
            Assert.IsTrue(health.IsAlive);
            Assert.IsTrue(bullet == null, "havada mermi kalmamalı");
        }

        [UnityTest]
        public IEnumerator Mermi_UstundekiOyuncuyaDegmez_DuvardaYokOlur()
        {
            Ground(-10f, 10f);
            Wall(6f);
            var ledge = Track(new GameObject("Raf") { layer = LayerMask.NameToLayer("Ground") });
            ledge.transform.position = new Vector3(3f, 0.95f, 0f);
            ledge.AddComponent<BoxCollider2D>().size = new Vector2(3f, 0.1f);
            PlayerHealth health = Player(new Vector2(3f, 1.05f)); // mermi hizasının üstünde: zıplamış gibi

            EnemyBullet bullet = Object.Instantiate(LoadPrefab<EnemyBullet>("EnemyBullet"), new Vector3(0f, 0.54f, 0f), Quaternion.identity);
            bullet.Launch(1, new Vector2(0f, 0.54f));

            yield return new WaitForSeconds(1f);
            Assert.AreEqual(6, health.CurrentHealth, "mermi altından geçmeli");
            Assert.IsTrue(bullet == null, "duvara çarpınca yok olmalı");
        }

        // --- Test dünyası ---

        // Gerçek prefab: sprite, ses ve mermi bağlantıları da denenmiş olur.
        static T LoadPrefab<T>(string name) where T : Component
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<T>($"Assets/Prefabs/{name}.prefab");
            Assert.IsNotNull(prefab, $"{name} prefab'ı yok; önce Desert Gecko → Levelleri Kur çalıştırılmalı.");
            return prefab;
#else
            Assert.Ignore("Prefab yüklemek için editör gerekir.");
            return null;
#endif
        }

        Jackal Cakal(Vector2 position)
        {
            Jackal jackal = Object.Instantiate(LoadPrefab<Jackal>("Jackal"), position, Quaternion.identity);
            Track(jackal.gameObject);
            return jackal;
        }

        Armadillo Zirhli(Vector2 position)
        {
            Armadillo armadillo = Object.Instantiate(LoadPrefab<Armadillo>("Armadillo"), position, Quaternion.identity);
            Track(armadillo.gameObject);
            return armadillo;
        }

        TumbleweedMouse Fare(Vector2 position)
        {
            TumbleweedMouse mouse = Object.Instantiate(LoadPrefab<TumbleweedMouse>("TumbleweedMouse"), position, Quaternion.identity);
            Track(mouse.gameObject);
            return mouse;
        }

        Rattlesnake Snake(Vector2 position)
        {
            Rattlesnake snake = Object.Instantiate(LoadPrefab<Rattlesnake>("Rattlesnake"), position, Quaternion.identity);
            Track(snake.gameObject);
            return snake;
        }

        void Wall(float x)
        {
            var go = Track(new GameObject("Wall") { layer = LayerMask.NameToLayer("Ground") });
            go.transform.position = new Vector3(x, 2f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 4f);
        }

        GameObject Track(GameObject go)
        {
            created.Add(go);
            return go;
        }

        void Ground(float xMin, float xMax)
        {
            var go = Track(new GameObject("Ground") { layer = LayerMask.NameToLayer("Ground") });
            go.transform.position = new Vector3((xMin + xMax) / 2f, -1f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(xMax - xMin, 2f);
        }

        PlayerHealth Player(Vector2 position)
        {
            var go = Track(new GameObject("Player") { layer = LayerMask.NameToLayer("Player") });
            go.transform.position = position;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.7f, 0.9f);
            box.offset = new Vector2(0f, 0.45f);
            go.AddComponent<PlayerInputReader>();
            go.AddComponent<PlayerController>();
            var health = go.AddComponent<PlayerHealth>();
            go.AddComponent<PlayerWallet>();
            go.AddComponent<TongueAttack>();
            go.AddComponent<TailAttack>();
            return health;
        }

        Scorpion Scorpion(Vector2 position)
        {
            var go = Track(new GameObject("Scorpion") { layer = LayerMask.NameToLayer("Enemy") });
            go.transform.position = position;
            go.AddComponent<SpriteRenderer>();
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.9f, 0.45f);
            box.offset = new Vector2(0f, 0.225f);
            go.AddComponent<Rigidbody2D>().gravityScale = 3f;
            return go.AddComponent<Scorpion>();
        }

        Bug BugAt(Vector2 position)
        {
            var go = Track(new GameObject("Bug"));
            go.transform.position = position;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.3f;
            return go.AddComponent<Bug>();
        }

        // Şablon: oyun alanının dışında durur, CrystalDrop bundan kopya üretir.
        Crystal CrystalTemplate() => CrystalAt(new Vector2(0f, 500f));

        // Konum bileşenden önce verilmeli: Crystal durduğu yeri Awake'te kaydeder.
        Crystal CrystalAt(Vector2 position)
        {
            var go = Track(new GameObject("Crystal"));
            go.transform.position = position;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.25f;
            return go.AddComponent<Crystal>();
        }

        TailProjectile ProjectileTemplate()
        {
            var go = Track(new GameObject("TailProjectile") { layer = LayerMask.NameToLayer("PlayerAttack") });
            go.transform.position = new Vector3(0f, 500f, 0f);
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0.3f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.22f;
            return go.AddComponent<TailProjectile>();
        }

        static void SetPrivate(object target, string field, object value)
        {
            FieldInfo info = null;
            for (var type = target.GetType(); type != null && info == null; type = type.BaseType)
                info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{field} bulunamadı");
            info.SetValue(target, value);
        }
    }
}
