using UnityEngine;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Sahipsiz tek seferlik ses: çalan nesne o an kaybolsa da (yutulan böcek gibi) ses sonuna kadar çalar.
    /// 2D ses: kameraya uzaklıkla kısılmaz.
    /// </summary>
    public static class OneShotSound
    {
        public static void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null)
                return;
            var go = new GameObject("Ses_" + clip.name);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.PlayOneShot(clip, volume);
            Object.Destroy(go, clip.length + 0.1f);
        }
    }
}
