using System.Collections;
using DesertGecko.Core;
using DesertGecko.Levels;
using DesertGecko.Player;
using DesertGecko.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DesertGecko.Tests
{
    /// <summary>
    /// Ana menü ve level akışı: ilerleme kaydı, sahnelerin hatasız açılması, bitiş noktası,
    /// duraklatma. Gerçek kayıtlı ilerlemeye dokunmamak için ayrı bir anahtar öneki kullanır.
    /// </summary>
    public class LevelFlowTests
    {
        string savedPrefix;

        [SetUp]
        public void SetUp()
        {
            savedPrefix = GameProgress.KeyPrefix;
            GameProgress.KeyPrefix = "DGB_TEST_";
            GameProgress.ResetProgress();
        }

        [TearDown]
        public void TearDown()
        {
            GameProgress.ResetProgress();
            GameProgress.KeyPrefix = savedPrefix;
            Time.timeScale = 1f;
        }

        // --- İlerleme kaydı ---

        [Test]
        public void IlkLevelAcik_DigerleriKilitli()
        {
            Assert.IsTrue(GameProgress.IsUnlocked(0));
            Assert.IsFalse(GameProgress.IsUnlocked(1));
            Assert.IsFalse(GameProgress.HasProgress);
        }

        [Test]
        public void LevelBitince_SonrakiAcilir_KristalRekoruTutulur()
        {
            GameProgress.CompleteLevel(0, 5);
            Assert.IsTrue(GameProgress.IsUnlocked(1));
            Assert.IsTrue(GameProgress.IsCompleted(0));
            Assert.AreEqual(1, GameProgress.LastPlayed, "devam et bir sonraki levele götürmeli");

            GameProgress.CompleteLevel(0, 3);
            Assert.AreEqual(5, GameProgress.BestCrystals(0), "daha az kristal rekoru düşürmemeli");
            GameProgress.CompleteLevel(0, 8);
            Assert.AreEqual(8, GameProgress.BestCrystals(0));
            Assert.AreEqual(8, GameProgress.TotalCrystals);
        }

        [Test]
        public void SonLevelBitince_KilitDisinaTasmaz()
        {
            int last = LevelCatalog.Count - 1;
            for (int i = 0; i <= last; i++)
                GameProgress.CompleteLevel(i, 1);
            Assert.AreEqual(last, GameProgress.HighestUnlocked);
            Assert.AreEqual(last, GameProgress.LastPlayed);
        }

        [Test]
        public void Sifirlama_HerSeyiSiler()
        {
            GameProgress.CompleteLevel(0, 4);
            GameProgress.ResetProgress();
            Assert.IsFalse(GameProgress.IsUnlocked(1));
            Assert.AreEqual(0, GameProgress.TotalCrystals);
            Assert.IsFalse(GameProgress.HasProgress);
        }

        // --- Sahneler ---

        [UnityTest]
        public IEnumerator AnaMenu_HatasizAcilir()
        {
            yield return SceneManager.LoadSceneAsync(LevelCatalog.MainMenuScene);
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenu>());
        }

        [UnityTest]
        public IEnumerator HerLevel_HatasizAcilir_VeGerekenleriIcerir(
            [Values(0, 1)] int level)
        {
            // Kilitli level "son oynanan" sayılmaz; hepsini açıp son oynananı başka bir levele çek.
            for (int i = 0; i < LevelCatalog.Count; i++)
                GameProgress.CompleteLevel(i, 0);
            GameProgress.LastPlayed = level == 0 ? 1 : 0;

            yield return SceneManager.LoadSceneAsync(LevelCatalog.Levels[level].Scene);
            for (int i = 0; i < 10; i++)
                yield return new WaitForFixedUpdate();

            LevelManager manager = LevelManager.Current;
            Assert.IsNotNull(manager, "LevelManager yok");
            Assert.AreEqual(level, manager.LevelIndex);
            Assert.IsNotNull(Object.FindAnyObjectByType<LevelGoal>(), "bitiş noktası yok");
            Assert.IsNotNull(Object.FindAnyObjectByType<PauseMenu>(), "duraklatma menüsü yok");
            Assert.IsNotNull(Object.FindAnyObjectByType<LevelCompleteUI>(), "level sonu ekranı yok");

            PlayerHealth player = manager.Player.GetComponent<PlayerHealth>();
            Assert.IsTrue(player.IsAlive);
            Assert.Greater(player.transform.position.y, -1f, "oyuncu başlangıçta düşmemeli");
            Assert.AreEqual(level, GameProgress.LastPlayed, "girilen level son oynanan olmalı");
        }

        [UnityTest]
        public IEnumerator BitisNoktasi_LeveliTamamlar_VeSonrakiniAcar()
        {
            yield return SceneManager.LoadSceneAsync(LevelCatalog.Levels[0].Scene);
            yield return new WaitForFixedUpdate();

            LevelManager manager = LevelManager.Current;
            LevelGoal goal = Object.FindAnyObjectByType<LevelGoal>();
            manager.Player.Teleport((Vector2)goal.transform.position + Vector2.up * 0.1f);
            for (int i = 0; i < 5; i++)
                yield return new WaitForFixedUpdate();

            Assert.IsTrue(manager.IsComplete);
            Assert.IsTrue(manager.Player.Frozen, "level bitince oyuncu durmalı");
            Assert.IsFalse(manager.Player.GetComponent<PlayerInputReader>().enabled, "level bitince girdi kapanmalı");
            Assert.IsFalse(manager.Player.GetComponent<TongueAttack>().enabled, "level bitince saldırı kapanmalı");
            Assert.IsTrue(GameProgress.IsUnlocked(1));
            Assert.IsTrue(GameProgress.IsCompleted(0));
        }

        [UnityTest]
        public IEnumerator Duraklatma_ZamaniDurdurur_DevamEtSurdurur()
        {
            yield return SceneManager.LoadSceneAsync(LevelCatalog.Levels[0].Scene);
            yield return null;

            PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>();
            var input = LevelManager.Current.Player.GetComponent<PlayerInputReader>();
            pause.Pause();
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(input.enabled, "duraklatılmışken oyuncu girdisi kapalı olmalı");

            pause.Resume();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(input.enabled);
        }

        [UnityTest]
        public IEnumerator Ucuruma_Dusmek_CaniAzaltir_VeCheckpointeDondurur()
        {
            yield return SceneManager.LoadSceneAsync(LevelCatalog.Levels[0].Scene);
            yield return new WaitForFixedUpdate();

            PlayerController player = LevelManager.Current.Player;
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            Vector2 start = health.CheckpointPosition;
            player.Teleport(new Vector2(start.x, -20f));
            yield return null;
            yield return null;

            Assert.AreEqual(health.MaxHealth - 2, health.CurrentHealth);
            Assert.That(player.transform.position.y, Is.GreaterThan(-5f), "checkpoint'e dönmeli");
        }
    }
}
