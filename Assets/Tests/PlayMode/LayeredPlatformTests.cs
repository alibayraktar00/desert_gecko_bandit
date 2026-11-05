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
    /// Katmanlı level parçaları: tek yönlü platform (alttan geçilir, üstünde durulur, S + Space ile inilir)
    /// ve zıplamadan yürünen merdiven. Gerçek Gila hareketini sanal klavyeyle sürer.
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

        void Stairs(float xFrom, float yFrom, int steps)
        {
            for (int i = 0; i < steps; i++)
                OneWay(xFrom + i * 0.5f, xFrom + (i + 1) * 0.5f, yFrom + (i + 1) * 0.25f, 0.2f);
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
        public IEnumerator Merdiven_ZiplamadanYurunerekCikilirVeInilir()
        {
            Stairs(1f, 0f, 12); // 6 birim uzun, 3 birim yüksek
            Solid(7f, 15f, 2f, 3f); // merdivenin bittiği kat
            yield return SpawnPlayer(0f, 0f);

            Press(Key.D);
            var trace = new System.Text.StringBuilder();
            float nextSample = 0f;
            for (float t = 0f; t < 2f && player.transform.position.x < 8f; t += Time.deltaTime)
            {
                Assert.AreNotEqual(PlayerController.MoveState.WallCling, player.State);
                if (t >= nextSample)
                {
                    nextSample = t + 0.04f;
                    trace.Append($"{player.transform.position.x:0.00}/{Feet:0.00}/{player.State.ToString()[0]} ");
                }
                yield return null;
            }
            Assert.Greater(player.transform.position.x, 8f, "merdiven yürüyüşü yavaşlatmamalı: " + trace);
            Assert.AreEqual(3f, Feet, 0.1f, "merdivenin tepesindeki kata çıkmalı: " + trace);

            yield return Hold(0.1f);
            bool leftGround = false;
            Press(Key.A);
            for (float t = 0f; t < 2f && player.transform.position.x > -0.5f; t += Time.deltaTime)
            {
                if (player.transform.position.x < 6.5f && player.transform.position.x > 1.5f && !player.IsGrounded)
                    leftGround = true;
                yield return null;
            }
            Assert.IsFalse(leftGround, "inerken her basamakta havalanmamalı");
            Assert.AreEqual(0f, Feet, 0.1f);
        }
    }
}
