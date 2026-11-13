using DesertGecko.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Geceyi ve şafağı yönetir: oyuncu şafak aralığında ilerledikçe gece katmanları solar, gündüz katmanları görünür,
    /// ışık ısınır ve ayaz biter. Yalnızca oyuncunun konumuna bağlıdır; geri dönünce gece de geri gelir.
    /// </summary>
    public class NightSky : MonoBehaviour
    {
        [SerializeField] Transform player;
        [SerializeField] Light2D globalLight;
        [SerializeField] SpriteRenderer[] nightLayers;
        [SerializeField] Light2D[] aura;
        [SerializeField] float dawnStartX, dawnEndX;
        [SerializeField] Color nightColor = new Color(0.46f, 0.54f, 0.86f);
        [SerializeField] float nightIntensity = 0.5f;
        [SerializeField] Color midDawnColor = new Color(1f, 0.66f, 0.5f);
        [Tooltip("Gökyüzü görselinin üstünde kalan şerit: kameranın arka plan rengi")]
        [SerializeField] Camera skyCamera;
        [SerializeField] Color nightBackground = new Color32(0x16, 0x12, 0x3C, 0xFF);
        [SerializeField] Color dayBackground;

        BodyHeat heat;
        float[] auraIntensity;

        /// <summary>0 = gece, 1 = gündüz.</summary>
        public float Dawn { get; private set; }

        public void Configure(Transform playerTransform, Light2D sun, SpriteRenderer[] layers, Light2D[] glows, float startX, float endX, Camera cam, Color nightTop)
        {
            skyCamera = cam;
            dayBackground = cam.backgroundColor;
            nightBackground = nightTop;
            player = playerTransform;
            globalLight = sun;
            nightLayers = layers;
            aura = glows;
            dawnStartX = startX;
            dawnEndX = endX;
            Apply(0f);
        }

        void Awake()
        {
            if (player != null)
                heat = player.GetComponent<BodyHeat>();
            auraIntensity = new float[aura != null ? aura.Length : 0];
            for (int i = 0; i < auraIntensity.Length; i++)
                auraIntensity[i] = aura[i].intensity;
            Apply(0f);
        }

        /// <summary>
        /// Gün batımından başlayan level (Level 4): duskStartX'ten duskEndX'e doğru kararır, dawnStartX'ten sonra
        /// şafak söker. duskStartX levelin başlangıcının gerisinde verilirse level gün batımının ortasında açılır.
        /// </summary>
        public void SetDusk(float startX, float endX)
        {
            duskStartX = startX;
            duskEndX = endX;
            hasDusk = true;
        }

        [SerializeField] bool hasDusk;
        [SerializeField] float duskStartX, duskEndX;

        void Update()
        {
            if (player == null)
                return;
            float x = player.position.x;
            Apply(hasDusk && x < dawnStartX ? Mathf.InverseLerp(duskEndX, duskStartX, x) : Mathf.InverseLerp(dawnStartX, dawnEndX, x));
        }

        /// <summary>Editörde şafağı önizlemek için.</summary>
        public void Preview(float t) => Apply(t);

        void Apply(float t)
        {
            Dawn = t;
            if (skyCamera != null)
                skyCamera.backgroundColor = Color.Lerp(nightBackground, dayBackground, t);
            if (globalLight != null)
            {
                globalLight.color = t < 0.5f ? Color.Lerp(nightColor, midDawnColor, t * 2f) : Color.Lerp(midDawnColor, Color.white, t * 2f - 1f);
                globalLight.intensity = Mathf.Lerp(nightIntensity, 1f, t);
            }
            if (nightLayers != null)
                foreach (SpriteRenderer layer in nightLayers)
                    if (layer != null)
                        layer.color = new Color(1f, 1f, 1f, 1f - t);
            if (auraIntensity != null)
                for (int i = 0; i < auraIntensity.Length; i++)
                    if (aura[i] != null)
                        aura[i].intensity = auraIntensity[i] * (1f - t);
            if (heat != null)
                heat.Coldness = 1f - t;
        }
    }
}
