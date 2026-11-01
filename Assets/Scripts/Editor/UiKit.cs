using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Menü ve paneller için ortak arayüz parçaları (640x360 referans, western renkleri).
    /// Arayüz kameraya çizilir (Screen Space - Camera), böylece ekran görüntülerinde de görünür.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color PanelColor = new Color32(0x2B, 0x1A, 0x12, 0xE6);
        public static readonly Color BorderColor = new Color32(0xE8, 0xA0, 0x4A, 0xFF);
        public static readonly Color ButtonColor = new Color32(0xE8, 0xA0, 0x4A, 0xFF);
        public static readonly Color ButtonHighlight = new Color32(0xF4, 0xD3, 0x5E, 0xFF);
        public static readonly Color ButtonText = new Color32(0x2B, 0x1A, 0x12, 0xFF);
        public static readonly Color LightText = new Color32(0xF2, 0xE6, 0xD0, 0xFF);

        static Font font;
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Canvas CreateCanvas(string name, Camera cam, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Yeni Input System ile çalışan EventSystem (klavye/gamepad/fare ile menü gezintisi).</summary>
        public static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Tüm ekranı kaplayan yarı saydam karartma.</summary>
        public static RectTransform Overlay(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        public static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            rect.gameObject.AddComponent<Image>().color = PanelColor;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(2f, -2f);
            return rect;
        }

        public static Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = align;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Text Shadowed(Text label)
        {
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.1f, 0.05f, 0.03f, 1f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        public static Button Button(string name, Transform parent, string text, Vector2 position, Vector2 size, out Text label, int fontSize = 16)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(0x2B, 0x1A, 0x12, 0xFF);
            outline.effectDistance = new Vector2(2f, -2f);

            var button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHighlight;
            colors.selectedColor = ButtonHighlight;
            colors.pressedColor = new Color32(0xB5, 0x45, 0x2B, 0xFF);
            colors.disabledColor = new Color(0.45f, 0.4f, 0.38f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.targetGraphic = image;

            label = Label("Yazi", rect, text, Vector2.zero, size, fontSize, ButtonText);
            label.fontStyle = FontStyle.Bold;
            ((RectTransform)label.transform).anchorMin = Vector2.zero;
            ((RectTransform)label.transform).anchorMax = Vector2.one;
            ((RectTransform)label.transform).sizeDelta = Vector2.zero;
            return button;
        }

        public static Slider Slider(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            SetLayerRecursive(go, LayerMask.NameToLayer("UI"));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            go.transform.Find("Background").GetComponent<Image>().color = new Color32(0x4A, 0x2A, 0x18, 0xFF);
            go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = ButtonColor;
            Image handle = go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>();
            handle.color = LightText;
            ColorBlock colors = slider.colors;
            colors.selectedColor = ButtonHighlight;
            colors.highlightedColor = ButtonHighlight;
            slider.colors = colors;
            return slider;
        }

        public static Toggle Toggle(string name, Transform parent, string text, Vector2 position, Vector2 size)
        {
            GameObject go = DefaultControls.CreateToggle(new DefaultControls.Resources());
            go.name = name;
            SetLayerRecursive(go, LayerMask.NameToLayer("UI"));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var toggle = go.GetComponent<Toggle>();
            go.transform.Find("Background").GetComponent<Image>().color = LightText;
            go.transform.Find("Background/Checkmark").GetComponent<Image>().color = new Color32(0xB5, 0x45, 0x2B, 0xFF);
            var label = go.transform.Find("Label").GetComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = 14;
            label.color = LightText;
            ColorBlock colors = toggle.colors;
            colors.selectedColor = ButtonHighlight;
            colors.highlightedColor = ButtonHighlight;
            toggle.colors = colors;
            return toggle;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
