using System.Collections;
using System.Collections.Generic;
using DesertGecko.Environment;
using DesertGecko.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DesertGecko.Tests
{
    /// <summary>Level 3 mekanikleri: vücut ısısı, ısı kaynağı, dille sallanma, gayzer.</summary>
    public class NightMechanicTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
                if (go != null)
                    Object.Destroy(go);
            created.Clear();
        }

        GameObject Track(GameObject go)
        {
            created.Add(go);
            return go;
        }

        void Ground(float xMin, float xMax)
        {
            var go = Track(new GameObject("Zemin") { layer = LayerMask.NameToLayer("Ground") });
            go.transform.position = new Vector2((xMin + xMax) / 2f, -0.5f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(xMax - xMin, 1f);
        }

        PlayerController Player(Vector2 position)
        {
            var go = Track(new GameObject("Player") { layer = LayerMask.NameToLayer("Player") });
            go.transform.position = position;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.7f, 0.9f);
            box.offset = new Vector2(0f, 0.45f);
            go.AddComponent<PlayerInputReader>();
            var controller = go.AddComponent<PlayerController>();
            go.AddComponent<PlayerHealth>();
            go.AddComponent<PlayerWallet>();
            go.AddComponent<TongueAttack>();
            go.AddComponent<TailAttack>();
            return controller;
        }

        static void SetPrivate(object target, string field, object value) =>
            target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(target, value);

        [UnityTest]
        public IEnumerator Isi_SogudukcaYavaslatir_AtesteDolar_DonunceCanGider()
        {
            Ground(-10f, 10f);
            PlayerController controller = Player(new Vector2(0f, 0.05f));
            var heat = controller.gameObject.AddComponent<BodyHeat>();
            var health = controller.GetComponent<PlayerHealth>();
            SetPrivate(heat, "secondsToFreeze", 0.5f);
            SetPrivate(heat, "freezeDamageInterval", 0.3f);
            yield return null;
            Assert.AreEqual(1f, controller.SpeedMultiplier);

            yield return new WaitForSeconds(0.45f);
            Assert.Less(heat.Heat, 0.3f);
            Assert.Less(controller.SpeedMultiplier, 1f, "üşüyünce yavaşlamalı");
            Assert.Less(controller.JumpMultiplier, 1f);

            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(heat.IsFrozen);
            Assert.Less(health.CurrentHealth, health.MaxHealth, "donunca can gitmeli");

            var fire = Track(new GameObject("Ates"));
            fire.transform.position = controller.transform.position;
            fire.AddComponent<CircleCollider2D>().radius = 3f;
            fire.AddComponent<WarmthSource>().Configure(4f, false);
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(heat.Heat, 0.5f, "ateşin yanında ısınmalı");
            Assert.AreEqual(1f, controller.SpeedMultiplier, 0.001f);
        }

        [UnityTest]
        public IEnumerator SicakBolge_FazladanHizVerir()
        {
            Ground(-10f, 10f);
            PlayerController controller = Player(new Vector2(0f, 0.05f));
            var heat = controller.gameObject.AddComponent<BodyHeat>();
            var zone = Track(new GameObject("Sicak"));
            zone.transform.position = controller.transform.position;
            zone.AddComponent<BoxCollider2D>().size = new Vector2(6f, 6f);
            zone.AddComponent<WarmthSource>().Configure(0f, true);
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(heat.IsHot);
            Assert.Greater(controller.SpeedMultiplier, 1f);
        }

        SwingPoint Hook(Vector2 at)
        {
            var go = Track(new GameObject("Kanca"));
            go.transform.position = at;
            return go.AddComponent<SwingPoint>();
        }

        [UnityTest]
        public IEnumerator Kanca_YapisincaSallanir_BirakincaHiziKorunur()
        {
            PlayerController controller = Player(new Vector2(-3f, 4f));
            Hook(new Vector2(0f, 7f));
            TongueAttack tongue = controller.GetComponent<TongueAttack>();
            tongue.HoldHook(true);
            yield return null;
            yield return null;
            Assert.IsNotNull(tongue.HookInReach, "menzildeki kanca seçilmeli");
            Assert.IsTrue(tongue.TryLatch());
            Assert.IsTrue(controller.IsSwinging);

            // Kancanın altından sağa geçene kadar sallansın.
            float limit = Time.time + 2f;
            while (controller.transform.position.x < 1f && Time.time < limit)
            {
                Vector2 offset = (Vector2)controller.transform.position + new Vector2(0f, 0.6f) - new Vector2(0f, 7f);
                Assert.LessOrEqual(offset.magnitude, 4.5f, "halat uzamamalı");
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(controller.transform.position.x, 1f, "sallanıp karşıya geçmeli");
            float speed = controller.Velocity.x;
            Assert.Greater(speed, 5.5f, "savrulma koşudan hızlı olmalı");

            controller.EndSwing();
            Assert.IsFalse(controller.IsSwinging);
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(controller.Velocity.x, 5.5f, "bırakınca hız hemen koşu hızına düşmemeli");
        }

        [UnityTest]
        public IEnumerator Gayzer_FiskirincaFirlatir()
        {
            Ground(-10f, 10f);
            PlayerController controller = Player(new Vector2(0f, 0.05f));
            var go = Track(new GameObject("Gayzer"));
            go.transform.position = Vector2.zero;
            go.AddComponent<BoxCollider2D>();
            var geyser = go.AddComponent<Geyser>();
            geyser.Configure(2f, 6f, 1f, 0f, null);
            SetPrivate(geyser, "eruptTime", 0.5f);
            SetPrivate(geyser, "warnTime", 0.1f);
            typeof(Geyser).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(geyser, null);

            float top = 0f;
            float end = Time.time + 1.6f;
            while (Time.time < end)
            {
                top = Mathf.Max(top, controller.transform.position.y);
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(top, 4f, "gayzer oyuncuyu yukarı fırlatmalı");
        }
    }
}
