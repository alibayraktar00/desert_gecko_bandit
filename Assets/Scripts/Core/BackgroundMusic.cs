using UnityEngine;

namespace DesertGecko.Core
{
    /// <summary>
    /// Sahne başladığında arka plan müziğini başlatır ve sonsuz döngüde çalar.
    /// Sahnede AudioListener yoksa otomatik ekler (ses duyulması için şart).
    /// Sahneye elle eklenmemiş olsa bile Play moduna girildiğinde otomatik başlar.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BackgroundMusic : MonoBehaviour
    {
        [Header("Müzik Ayarları")]
        [SerializeField] AudioClip musicClip;
        [SerializeField, Range(0f, 1f)] float volume = 0.6f;
        [SerializeField] bool playOnAwake = true;

        const string FallbackResourceName = "Shadows_on_the_Mesa";

        static BackgroundMusic instance;
        AudioSource audioSource;

        /// <summary>
        /// Sahneye elle eklenmemişse bile Play moduna girildiğinde otomatik başlatır.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (SceneFlow.IsTestRunnerScene)
                return; // kalıcı müzik objesi testlerin sahnelerine taşınmasın

            if (FindAnyObjectByType<BackgroundMusic>() == null)
            {
                var go = new GameObject("BackgroundMusic");
                go.AddComponent<BackgroundMusic>();
                Debug.Log("[BackgroundMusic] Sahneye otomatik eklendi ve başlatıldı.");
            }
        }

        void Awake()
        {
            // Singleton kontrolü
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            // AudioListener kontrolü — sahnede yoksa ses ÇIKMAZ, bu yüzden garantiye alıyoruz
            EnsureAudioListener();

            audioSource = GetComponent<AudioSource>();

            // Clip Inspector'dan atanmamışsa Resources'tan yükle
            if (musicClip == null)
            {
                musicClip = Resources.Load<AudioClip>(FallbackResourceName);
            }

            audioSource.clip = musicClip;
            audioSource.loop = true;
            audioSource.volume = volume;
            audioSource.spatialBlend = 0f; // 2D Ses: her yerden tam güçte ve temiz duyulur
            audioSource.playOnAwake = false;

            if (playOnAwake && musicClip != null)
            {
                audioSource.Play();
                Debug.Log($"[BackgroundMusic] Müzik çalıyor: {musicClip.name} (Ses: {volume:P0})");
            }
            else if (musicClip == null)
            {
                Debug.LogError("[BackgroundMusic] Müzik dosyası (Shadows_on_the_Mesa) bulunamadı!");
            }
        }

        void EnsureAudioListener()
        {
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                    Debug.Log("[BackgroundMusic] Main Camera'ya AudioListener otomatik eklendi.");
                }
                else
                {
                    gameObject.AddComponent<AudioListener>();
                    Debug.Log("[BackgroundMusic] BackgroundMusic objesine AudioListener eklendi.");
                }
            }
        }

        public void SetVolume(float newVolume)
        {
            volume = Mathf.Clamp01(newVolume);
            if (audioSource != null)
                audioSource.volume = volume;
        }

        public void StopMusic()
        {
            if (audioSource != null)
                audioSource.Stop();
        }

        public void PlayMusic()
        {
            if (audioSource != null && musicClip != null)
            {
                audioSource.clip = musicClip;
                audioSource.Play();
            }
        }
    }
}
