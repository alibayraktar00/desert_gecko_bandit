using System.Linq;
using System.Text.RegularExpressions;
using DesertGecko.Player;
using UnityEditor;
using UnityEngine;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Assets/Sprites/Player/Gila/&lt;animasyon&gt;/ klasörlerindeki kareleri Player prefab'ındaki
    /// PlayerVisual'a bağlar. Yeni kareler içe aktarılınca otomatik çalışır; menüden elle de çalıştırılabilir.
    /// </summary>
    [InitializeOnLoad]
    public static class GilaSpriteBinder
    {
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

        static GilaSpriteBinder()
        {
            EditorApplication.delayCall += BindIfMissing;
        }

        // Kareler bu script derlenmeden önce içe aktarıldıysa (ayarları eksik kalmış olabilir)
        // bir kez yeniden içe aktar; bağlama işi içe aktarma bitince otomatik çalışır.
        static void BindIfMissing()
        {
            if (!AssetDatabase.IsValidFolder(SpriteImportRules.GilaFolder.TrimEnd('/')))
                return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var visual = prefab != null ? prefab.GetComponentInChildren<PlayerVisual>() : null;
            if (visual == null)
                return;

            var idle = new SerializedObject(visual).FindProperty("idleFrames");
            if (idle.arraySize > 0 && idle.GetArrayElementAtIndex(0).objectReferenceValue != null)
                return;

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteImportRules.GilaFolder.TrimEnd('/') }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("Desert Gecko/Gila Spritelarını Bağla")]
        public static void Bind()
        {
            Sprite[] idle = LoadFrames("idle");
            if (idle.Length == 0 || AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var visual = root.GetComponentInChildren<PlayerVisual>();
                var so = new SerializedObject(visual);
                SetFrames(so, "idleFrames", idle);
                SetFrames(so, "runFrames", LoadFrames("run"));
                SetFrames(so, "glideFrames", LoadFrames("glide"));
                SetFrames(so, "wallFrames", LoadFrames("wall"));
                so.FindProperty("riseFrame").objectReferenceValue = LoadFrames("rise").FirstOrDefault();
                so.FindProperty("apexFrame").objectReferenceValue = LoadFrames("apex").FirstOrDefault();
                so.FindProperty("fallFrame").objectReferenceValue = LoadFrames("fall").FirstOrDefault();
                so.FindProperty("tintByState").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Kareler ayak hizasından pivotlu, görsel kökle aynı yerde durmalı.
                visual.transform.localPosition = Vector3.zero;
                var sr = visual.GetComponent<SpriteRenderer>();
                sr.sprite = idle[0];
                sr.color = Color.white;

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"[DesertGecko] Gila spriteları bağlandı ({idle.Length} idle kare).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Sprite[] LoadFrames(string anim)
        {
            string folder = SpriteImportRules.GilaFolder + anim;
            if (!AssetDatabase.IsValidFolder(folder))
                return new Sprite[0];

            return AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(FrameNumber)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(s => s != null)
                .ToArray();
        }

        // gila_run_10 sıralamada gila_run_2'den sonra gelsin.
        static int FrameNumber(string path)
        {
            Match m = Regex.Match(path, @"_(\d+)\.png$");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        static void SetFrames(SerializedObject so, string field, Sprite[] frames)
        {
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }
    }
}
