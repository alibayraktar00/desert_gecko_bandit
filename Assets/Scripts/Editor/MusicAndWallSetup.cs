using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DesertGecko.Editor
{
    /// <summary>
    /// Sahnede Müzik ve AudioListener kurulumunu yapan araç.
    /// Menü: DesertGecko ► Müzik Kurulumu
    /// </summary>
    public static class MusicAndWallSetup
    {
        [MenuItem("DesertGecko/Müzik Kurulumu")]
        static void Setup()
        {
            SetupAudioListener();
            SetupBackgroundMusic();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[DesertGecko] Müzik ve ses sistemi kurulumu tamamlandı!");
        }

        static void SetupAudioListener()
        {
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Undo.AddComponent<AudioListener>(cam.gameObject);
                    Debug.Log("[DesertGecko] Main Camera'ya AudioListener eklendi.");
                }
            }
        }

        static void SetupBackgroundMusic()
        {
            var existing = Object.FindAnyObjectByType<Core.BackgroundMusic>();
            if (existing != null)
            {
                var existingSO = new SerializedObject(existing);
                var clipProp = existingSO.FindProperty("musicClip");
                if (clipProp.objectReferenceValue == null)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Shadows_on_the_Mesa.mp3");
                    if (clip != null)
                    {
                        clipProp.objectReferenceValue = clip;
                        existingSO.ApplyModifiedProperties();
                        Debug.Log("[DesertGecko] Mevcut BackgroundMusic'e müzik clip'i atandı.");
                    }
                }
                return;
            }

            var go = new GameObject("BackgroundMusic");
            Undo.RegisterCreatedObjectUndo(go, "BackgroundMusic Oluştur");

            var bgMusic = go.AddComponent<Core.BackgroundMusic>();
            var musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Shadows_on_the_Mesa.mp3");
            if (musicClip != null)
            {
                var so = new SerializedObject(bgMusic);
                so.FindProperty("musicClip").objectReferenceValue = musicClip;
                so.FindProperty("volume").floatValue = 0.6f;
                so.FindProperty("playOnAwake").boolValue = true;
                so.ApplyModifiedProperties();
                Debug.Log("[DesertGecko] BackgroundMusic oluşturuldu ve müzik atandı: " + musicClip.name);
            }
        }
    }
}
