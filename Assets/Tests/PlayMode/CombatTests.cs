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
        public IEnumerator Kuyruk_FirlatincaGider_BeklemeSonundaGeriGelir()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(0f, 0.05f));
            var tail = health.GetComponent<TailAttack>();
            var controller = health.GetComponent<PlayerController>();
            SetPrivate(tail, "projectilePrefab", ProjectileTemplate());
            SetPrivate(tail, "cooldown", 0.3f);
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

        // --- Yılan silahşor ---

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
        public IEnumerator Yilan_UyariSirasindaVurulursaAtesEtmez()
        {
            Ground(-10f, 10f);
            PlayerHealth health = Player(new Vector2(-4f, 0.05f));
            Rattlesnake snake = Snake(new Vector2(0f, 0.05f));
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(Rattlesnake.AttackState.Rattle, snake.State);

            health.GetComponent<PlayerController>().Teleport(new Vector2(4f, 0.05f));
            snake.TakeDamage(1, Vector2.left);
            Assert.AreEqual(Rattlesnake.AttackState.None, snake.State, "nişanı bozulmalı");
            Assert.AreEqual(1, snake.FacingDirection, "saldırana dönmeli");

            // İptal olmasaydı atış ~0.85 sn sonra çıkardı; yeni saldırının mermisi ise en erken 1.75 sn sonra.
            yield return new WaitForSeconds(1f);
            Assert.IsNull(Object.FindAnyObjectByType<EnemyBullet>());
            Assert.AreEqual(6, health.CurrentHealth);
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

            yield return new WaitForSeconds(1.2f);
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
