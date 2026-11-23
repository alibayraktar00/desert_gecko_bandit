using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Ara sahne katmanı: üstten ve alttan kayan siyah sinema şeritleri ve tam ekran kararma. Kendi tuvalini
    /// çalışma anında kurar, her şeyin üstünde çizilir. Gerçek zamanla işler (ağır çekimden etkilenmez).
    /// </summary>
    public class CinematicOverlay : MonoBehaviour
    {
        const float BarHeight = 0.12f; // ekran boyunun oranı

        static CinematicOverlay instance;
        RectTransform top, bottom;
        Image fade, flash;
        float flashStart = -10f, flashDuration = 0.1f;
        float barFrom, barTo, barStart, barDuration;
        float fadeFrom, fadeTo, fadeStart, fadeDuration;
        float bar;

        static CinematicOverlay Instance
        {
            get
            {
                if (instance == null)
                    instance = Create();
                return instance;
            }
        }

        /// <summary>Şeritleri getirir (show) ya da kaldırır.</summary>
        public static void Bars(bool show, float duration)
        {
            CinematicOverlay o = Instance;
            o.barFrom = o.bar;
            o.barTo = show ? 1f : 0f;
            o.barStart = Time.unscaledTime;
            o.barDuration = Mathf.Max(0.0001f, duration);
        }

        /// <summary>Patlama anı: ekran bir an beyaza keser, hızla söner.</summary>
        public static void Flash(float duration)
        {
            CinematicOverlay o = Instance;
            o.flashStart = Time.unscaledTime;
            o.flashDuration = Mathf.Max(0.0001f, duration);
        }

        /// <summary>Ekranı karartır (1) ya da açar (0).</summary>
        public static void Fade(float target, float duration)
        {
            CinematicOverlay o = Instance;
            o.fadeFrom = o.fade.color.a;
            o.fadeTo = target;
            o.fadeStart = Time.unscaledTime;
            o.fadeDuration = Mathf.Max(0.0001f, duration);
        }

        static CinematicOverlay Create()
        {
            var go = new GameObject("CinematicOverlay");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            go.AddComponent<CanvasScaler>();
            var o = go.AddComponent<CinematicOverlay>();
            o.top = Panel(go.transform, "UstSerit", new Vector2(0f, 1f));
            o.bottom = Panel(go.transform, "AltSerit", new Vector2(0f, 0f));
            o.fade = Panel(go.transform, "Karartma", new Vector2(0f, 0f)).GetComponent<Image>();
            var rt = o.fade.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            o.fade.color = new Color(0f, 0f, 0f, 0f);
            o.flash = Panel(go.transform, "Parlama", new Vector2(0f, 0f)).GetComponent<Image>();
            o.flash.rectTransform.anchorMin = Vector2.zero;
            o.flash.rectTransform.anchorMax = Vector2.one;
            o.flash.rectTransform.sizeDelta = Vector2.zero;
            o.flash.color = new Color(1f, 0.97f, 0.88f, 0f);
            o.Apply();
            return o;
        }

        static RectTransform Panel(Transform parent, string name, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Color.black;
            go.GetComponent<Image>().raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.pivot = pivot;
            rt.anchorMin = new Vector2(0f, pivot.y);
            rt.anchorMax = new Vector2(1f, pivot.y);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        void Update() => Apply();

        void Apply()
        {
            float now = Time.unscaledTime;
            float kb = barDuration > 0f ? Mathf.Clamp01((now - barStart) / barDuration) : 1f;
            bar = Mathf.Lerp(barFrom, barTo, kb * kb * (3f - 2f * kb));
            float h = Screen.height * BarHeight * bar / (GetComponent<Canvas>().scaleFactor);
            top.sizeDelta = new Vector2(0f, h);
            bottom.sizeDelta = new Vector2(0f, h);

            float kf = fadeDuration > 0f ? Mathf.Clamp01((now - fadeStart) / fadeDuration) : 1f;
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(fadeFrom, fadeTo, kf));

            float ks = Mathf.Clamp01((now - flashStart) / flashDuration);
            Color fc = flash.color;
            fc.a = (1f - ks) * (1f - ks) * 0.9f;
            flash.color = fc;
        }
    }
}
