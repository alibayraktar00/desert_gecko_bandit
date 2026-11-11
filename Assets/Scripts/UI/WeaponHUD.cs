using System.Collections.Generic;
using DesertGecko.Player;
using DesertGecko.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.UI
{
    /// <summary>
    /// Sol üstte, ısı çubuğunun altında: elde tutulan silahın ikonu ve mermileri (dolu/boş kovanlar).
    /// Sahnedeki "HUD" tuvaline çalışırken eklenir; sahne kurulumunu değiştirmek gerekmez. Silah yokken gizlidir.
    /// </summary>
    public class WeaponHUD : MonoBehaviour
    {
        static readonly Vector2 Anchor = new Vector2(8f, -48f);
        const float PipGap = 1f, IconGap = 4f;

        const float PopTime = 0.18f, BlinkRate = 5f;

        PlayerWeapon weapon;
        RectTransform root;
        Image icon;
        readonly List<Image> pips = new List<Image>();
        float pickedAt = -10f;
        float firedAt = -10f;
        int spentPip = -1; // son harcanan mermi: bir an beyaz parlar

        /// <summary>Sahnedeki HUD tuvalini bulur ve silah göstergesini ekler. HUD yoksa (test sahnesi) bir şey yapmaz.</summary>
        public static void Attach(PlayerWeapon weapon)
        {
            GameObject hud = GameObject.Find("HUD");
            if (hud == null || hud.GetComponent<Canvas>() == null || hud.GetComponentInChildren<WeaponHUD>(true) != null)
                return;
            var go = new GameObject("Silah", typeof(RectTransform));
            go.layer = hud.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(hud.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Anchor;
            rect.sizeDelta = new Vector2(80f, 12f);
            var ui = go.AddComponent<WeaponHUD>();
            ui.root = rect;
            ui.weapon = weapon;
            ui.Build();
        }

        void Build()
        {
            icon = NewImage("Ikon");
            weapon.Changed += Refresh;
            weapon.Fired += OnFired;
            weapon.PickedUp += OnPickedUp;
            Refresh();
        }

        void OnDestroy()
        {
            if (weapon != null)
            {
                weapon.Changed -= Refresh;
                weapon.Fired -= OnFired;
                weapon.PickedUp -= OnPickedUp;
            }
        }

        void OnFired()
        {
            firedAt = Time.unscaledTime;
            spentPip = weapon.Ammo; // Fired, mermi düştükten sonra gelir: harcanan bu sıradaki kovan
        }

        void OnPickedUp() => pickedAt = Time.unscaledTime;

        /// <summary>
        /// Silah alınınca ikon sıçrar; harcanan kovan bir an beyaz parlar; son mermi kalınca o kovan yanıp söner
        /// (mermi bitmek üzere).
        /// </summary>
        void Update()
        {
            if (weapon == null || weapon.Current == null || icon == null)
                return;
            float pop = 1f - Mathf.Clamp01((Time.unscaledTime - pickedAt) / PopTime);
            icon.rectTransform.localScale = Vector3.one * (1f + 0.35f * pop);

            bool flashing = Time.unscaledTime - firedAt < 0.1f;
            bool lowAmmo = weapon.Ammo == 1 && weapon.Current.Ammo > 2;
            bool blinkOn = Mathf.Repeat(Time.unscaledTime * BlinkRate, 1f) < 0.5f;
            for (int i = 0; i < pips.Count && i < weapon.Current.Ammo; i++)
            {
                Color c = Color.white;
                if (i == spentPip && flashing)
                    c = new Color(1f, 1f, 1f, 1f);
                else if (lowAmmo && i == 0 && !blinkOn)
                    c = new Color(1f, 0.55f, 0.45f, 1f);
                pips[i].color = c;
                pips[i].rectTransform.localScale = Vector3.one * (i == spentPip && flashing ? 1.4f : 1f);
            }
        }

        Image NewImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        static void Place(Image image, Sprite sprite, float x, float y)
        {
            image.sprite = sprite;
            var rect = image.rectTransform;
            rect.anchoredPosition = new Vector2(x, y);
            // Piksel ölçeği kalplerle aynı: 1 sprite pikseli = 1 arayüz birimi.
            rect.sizeDelta = sprite != null ? sprite.rect.size : Vector2.zero;
        }

        void Refresh()
        {
            WeaponData current = weapon != null ? weapon.Current : null;
            root.gameObject.SetActive(current != null);
            if (current == null)
                return;

            Place(icon, current.Icon, 0f, 0f);
            float x = (current.Icon != null ? current.Icon.rect.width : 16f) + IconGap;
            float iconHeight = current.Icon != null ? current.Icon.rect.height : 10f;

            while (pips.Count < current.Ammo)
                pips.Add(NewImage("Mermi_" + (pips.Count + 1)));
            for (int i = 0; i < pips.Count; i++)
            {
                bool used = i < current.Ammo;
                pips[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                Sprite sprite = i < weapon.Ammo ? current.AmmoIcon : current.AmmoIconEmpty;
                float height = sprite != null ? sprite.rect.height : 6f;
                Place(pips[i], sprite, x, -(iconHeight - height) * 0.5f);
                x += (sprite != null ? sprite.rect.width : 3f) + PipGap;
            }
        }
    }
}
