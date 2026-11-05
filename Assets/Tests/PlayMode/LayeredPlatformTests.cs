using System.Collections;
using System.Collections.Generic;
using DesertGecko.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DesertGecko.Tests
{
    /// <summary>
    /// Katmanlı level parçaları: tek yönlü platform (alttan geçilir, üstünde durulur, S + Space ile inilir).
    /// Gerçek Gila hareketini sanal klavyeyle sürer.
    /// </summary>
    public class LayeredPlatformTests
    {
        readonly List<GameObject> created = new List<GameObject>();
        InputSettings savedSettings;
        Keyboard keyboard;
        PlayerController player;
        int groundLayer;

        [SetUp]
        public void SetUp()
        {
            Application.runInBackground = true; // editör odakta değilken de sürsün
            savedSettings = InputSystem.settings;
            var settings = Object.Instantiate(savedSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            keyboard = InputSystem.AddDevice<Keyboard>("PlatformTestKeyboard");
            groundLayer = LayerMask.NameToLayer("Ground");

            Solid(-20f, 40f, -1f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
                Object.Destroy(go);
            created.Clear();
            InputSystem.RemoveDevice(keyboard);
            if (savedSettings != null)
                InputSystem.settings = savedSettings;
        }

        GameObject Solid(float xMin, float xMax, float yMin, float yMax)
        {
            var go = new GameObject("Solid") { layer = groundLayer };
            go.transform.position = new Vector2((xMin + xMax) / 2f, (yMin + yMax) / 2f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(xMax - xMin, yMax - yMin);
            created.Add(go);
            return go;
        }

        void OneWay(float xMin, float xMax, float top, float thickness = 0.3f)
        {
            GameObject go = Solid(xMin, xMax, top - thickness, top);
            go.GetComponent<BoxCollider2D>().usedByEffector = true;
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 170f;
        }

        IEnumerator SpawnPlayer(float x, float y)
        {
            var go = new GameObject("Oyuncu");
            go.SetActive(false);
            var body = go.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.7f, 0.9f);
            box.offset = new Vector2(0f, 0.45f);
            box.sharedMaterial = new PhysicsMaterial2D { friction = 0f, bounciness = 0f };
            go.layer = LayerMask.NameToLayer("Player");
            go.AddComponent<PlayerInputReader>();
            player = go.AddComponent<PlayerController>();
            go.transform.position = new Vector2(x, y);
            go.SetActive(true);
            created.Add(go);
            yield return new WaitForSeconds(0.4f);
        }

        void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        IEnumerator Hold(float seconds, params Key[] keys)
        {
            Press(keys);
            yield return new WaitForSeconds(seconds);
        }

        float Feet => player.transform.position.y;

        [UnityTest]
        public IEnumerator TekYonluPlatform_AlttanGecilir_UstundeDurulur()
        {
            OneWay(-2f, 2f, 2f);
            yield return SpawnPlayer(0f, 0f);

            yield return Hold(0.75f, Key.Space);
            yield return Hold(0.5f);

            Assert.AreEqual(2f, Feet, 0.05f, "zıplayınca platformun üstüne çıkıp orada durmalı");
            Assert.IsTrue(player.IsGrounded);
        }

        [UnityTest]
        public IEnumerator TekYonluPlatform_AsagiArtiZiplamaylaInilir()
        {
            OneWay(-2f, 2f, 2f);
            yield return SpawnPlayer(0f, 2.1f);
            Assert.AreEqual(2f, Feet, 0.05f);

            yield return Hold(0.1f, Key.S);
            yield return Hold(0.1f, Key.S, Key.Space);
            yield return Hold(0.6f);

            Assert.AreEqual(0f, Feet, 0.05f, "platformdan aşağı düşüp zemine inmeli");
            Assert.IsTrue(player.IsGrounded);
        }

        [UnityTest]
        public IEnumerator TekYonluPlatform_DuvarSayilmaz()
        {
            OneWay(1f, 4f, 0.9f, 0.9f);
            yield return SpawnPlayer(0f, 0f);

            yield return Hold(0.5f, Key.D, Key.W);

            Assert.AreNotEqual(PlayerController.MoveState.WallCling, player.State, "yan yüzüne yapışmamalı");
            Assert.Greater(player.transform.position.x, 1.5f, "yandan içine girebilmeli");
        }

        [UnityTest]
        public IEnumerator CiftZiplama_TekZiplamaninYetmedigiPlatformaCikar()
        {
            OneWay(-2f, 2f, 4.4f); // tek zıplama 2.8, çift zıplamayla toplam yaklaşık 5
            yield return SpawnPlayer(0f, 0f);

            int doubleJumps = 0;
            player.DoubleJumped += () => doubleJumps++;
            yield return Hold(0.3f, Key.Space);
            yield return Hold(0.05f);
            yield return Hold(0.5f, Key.Space);
            yield return Hold(0.4f);

            Assert.AreEqual(1, doubleJumps, "havada ikinci basış çift zıplama olmalı");
            Assert.AreEqual(4.4f, Feet, 0.05f, "çift zıplamayla platforma çıkmalı");
        }

        [UnityTest]
        public IEnumerator KatliZemindenAyniHizadakiPlatformaYurunur_OradanInilir()
        {
            Solid(-6f, 2f, 2f, 3f);  // kat döşemesi
            OneWay(2f, 6f, 3f);      // döşemenin devamı, alttan geçilen platform
            yield return SpawnPlayer(0f, 3.2f);

            yield return Hold(0.6f, Key.D);
            yield return Hold(0.3f);
            Assert.Greater(player.transform.position.x, 2.5f, "döşemeden platforma takılmadan geçmeli");
            Assert.AreEqual(3f, Feet, 0.05f, "platformda batmadan durmalı");
            Assert.IsTrue(player.IsGrounded);

            yield return Hold(0.1f, Key.S);
            yield return Hold(0.1f, Key.S, Key.Space);
            yield return Hold(0.7f);
            Assert.AreEqual(0f, Feet, 0.05f, "S + Space ile alt kata inmeli");
        }
    }
}
