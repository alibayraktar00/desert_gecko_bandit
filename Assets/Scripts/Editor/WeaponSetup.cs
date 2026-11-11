using System.IO;
using System.Linq;
using DesertGecko.Enemies;
using DesertGecko.Player;
using DesertGecko.Weapons;
using UnityEditor;
using UnityEngine;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Silah verilerini (tools/gila_silah_ciz.py çizimleri) kurar, düşmanlara düşürecekleri silahı,
    /// oyuncuya PlayerWeapon bileşenini ekler. Tekrar çalıştırmak güvenli: var olanı günceller.
    /// </summary>
    public static class WeaponSetup
    {
        const string DataFolder = "Assets/Data/Weapons";
        const string HeldFolder = "Assets/Sprites/Player/Gila/silah/";
        const string ItemFolder = "Assets/Sprites/Weapons/";
        const string PlayerPrefab = "Assets/Prefabs/Player.prefab";

        [MenuItem("Desert Gecko/Silahları Kur")]
        public static string Build()
        {
            WeaponData revolver = BuildRevolver();
            SetEnemyDrop("Assets/Prefabs/Rattlesnake.prefab", revolver);
            WeaponData shotgun = BuildShotgun();
            SetEnemyDrop("Assets/Prefabs/Jackal.prefab", shotgun, so =>
                so.FindProperty("pelletSprite").objectReferenceValue = Sprite(ItemFolder + "sacma.png"));
            SetEnemyDrop("Assets/Prefabs/BanditBat.prefab", BuildDynamite());
            SetEnemyDrop("Assets/Prefabs/Armadillo.prefab", BuildPickaxe());
            SetEnemyDrop("Assets/Prefabs/SpikeMutant.prefab", BuildSpikes());
            SetEnemyDrop("Assets/Prefabs/TumbleweedMouse.prefab", BuildKnife());
            AddPlayerWeapon(); // prefab yeniden kaydedilir: kaldırılmış eski alanlar düşer
            AssetDatabase.SaveAssets();
            return "silahlar kuruldu";
        }

        /// <summary>Her silahta ortak: ad, ikon, elde tutulan kareler, mermi ve arayüz görselleri, namlu alevi.</summary>
        static SerializedObject Begin(WeaponData data, string displayName, string file, string bullet, string ammoIcon)
        {
            var so = new SerializedObject(data);
            so.FindProperty("displayName").stringValue = displayName;
            Set(so, "icon", Sprite(ItemFolder + file + ".png"));
            SetPoses(so, file);
            Set(so, "bulletSprite", Sprite(ItemFolder + bullet + ".png"));
            Set(so, "ammoIcon", Sprite(ItemFolder + ammoIcon + ".png"));
            Set(so, "ammoIconEmpty", Sprite(ItemFolder + ammoIcon + "_bos.png"));
            var flash = so.FindProperty("muzzleFlashFrames");
            flash.arraySize = 3;
            for (int i = 0; i < 3; i++)
                flash.GetArrayElementAtIndex(i).objectReferenceValue = Sprite(ItemFolder + $"namlu_{i}.png");
            Set(so, "fireSound", Clip("silah"));
            // Hepsinde ortak cila: alma ve sekme sesi, değiştirme ipucu. Silaha özgü olanlar aşağıda.
            Set(so, "pickupSound", Clip("silah_al"));
            Set(so, "ricochetSound", Clip("sekme"));
            Set(so, "swapHintSprite", Sprite(ItemFolder + "tus_s.png"));
            Set(so, "cycleSound", null);
            so.FindProperty("ejectsShell").boolValue = false;
            Set(so, "swingSound", null);
            Set(so, "fuseSound", null);
            so.FindProperty("hitStop").floatValue = 0.03f;
            return so;
        }

        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{name}.wav");

        static WeaponData BuildRevolver()
        {
            WeaponData data = LoadOrCreate("Altipatlar");
            var so = Begin(data, "Altıpatlar", "altipatlar", "mermi", "mermi_ikon");
            so.FindProperty("ammo").intValue = 6;
            so.FindProperty("shake").floatValue = 0.12f;
            so.FindProperty("damage").intValue = 2;
            so.FindProperty("fireInterval").floatValue = 0.3f;
            so.FindProperty("bulletSpeed").floatValue = 18f;
            so.FindProperty("bulletRange").floatValue = 10f;
            so.FindProperty("thrownDamage").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>
        /// Pompalı (Çakal'dan): 4 fişek, her atış 7 tane saçma. Yakından bütün taneler (7 hasar) isabet eder,
        /// menzil uzadıkça koni açılır ve taneler sönerek kaybolur. Geri tepmesi Gila'yı iter; pompa her atıştan
        /// sonra geri çekilir.
        /// </summary>
        static WeaponData BuildShotgun()
        {
            WeaponData data = LoadOrCreate("Pompali");
            var so = Begin(data, "Pompalı", "pompali", "sacma", "fisek_ikon");
            so.FindProperty("ammo").intValue = 4;
            so.FindProperty("shake").floatValue = 0.32f;
            Set(so, "fireSound", Clip("pompali"));
            Set(so, "cycleSound", Clip("pompa"));
            so.FindProperty("cycleDelay").floatValue = 0.12f; // pompanın geri çekildiği kareyle birlikte
            so.FindProperty("ejectsShell").boolValue = true;
            so.FindProperty("hitStop").floatValue = 0.05f;
            so.FindProperty("damage").intValue = 1;
            so.FindProperty("pellets").intValue = 7;
            so.FindProperty("spreadAngle").floatValue = 9f;
            so.FindProperty("pelletJitter").floatValue = 0.6f;
            so.FindProperty("fireInterval").floatValue = 0.7f;
            so.FindProperty("bulletSpeed").floatValue = 20f;
            so.FindProperty("bulletRange").floatValue = 6.5f;
            so.FindProperty("recoil").floatValue = 5f;
            so.FindProperty("recoverTime").floatValue = 0.35f;
            so.FindProperty("thrownDamage").intValue = 1;
            so.FindProperty("fireVolume").floatValue = 0.8f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        static WeaponData LoadOrCreate(string name)
        {
            string path = $"{DataFolder}/{name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (data != null)
                return data;
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets/Data", "Weapons");
            data = ScriptableObject.CreateInstance<WeaponData>();
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        static Sprite Sprite(string path)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
                Debug.LogWarning($"[DesertGecko] Silah sprite'ı bulunamadı: {path}");
            return sprite;
        }

        static void Set(SerializedObject so, string field, Object value) => so.FindProperty(field).objectReferenceValue = value;

        [System.Serializable] class PoseFile { public PoseEntry[] poses; }
        [System.Serializable] class PoseEntry { public string body; public float[] ileri; public float[] capraz; public float[] yukari; }

        /// <summary>
        /// gila_silah_ciz.py'nin her gövde karesi için çizdiği kol karelerini ve namlu uçlarını (pozlar.json) bağlar.
        /// Varsayılan kareler (listede olmayan gövde kareleri için) idle_0 pozudur.
        /// </summary>
        static void SetPoses(SerializedObject so, string weapon)
        {
            string folder = HeldFolder + weapon + "/";
            var file = JsonUtility.FromJson<PoseFile>(File.ReadAllText(folder + "pozlar.json"));
            var poses = so.FindProperty("poses");
            poses.arraySize = file.poses.Length;
            for (int i = 0; i < file.poses.Length; i++)
            {
                PoseEntry entry = file.poses[i];
                var pose = poses.GetArrayElementAtIndex(i);
                string kind = entry.body.Split('_')[1];
                pose.FindPropertyRelative("body").objectReferenceValue = Sprite($"Assets/Sprites/Player/Gila/{kind}/{entry.body}.png");
                SetFrames(pose.FindPropertyRelative("forward"), $"{folder}{entry.body}_ileri");
                SetFrames(pose.FindPropertyRelative("diagonal"), $"{folder}{entry.body}_capraz");
                SetFrames(pose.FindPropertyRelative("up"), $"{folder}{entry.body}_yukari");
                pose.FindPropertyRelative("forwardMuzzle").vector2Value = V(entry.ileri);
                pose.FindPropertyRelative("diagonalMuzzle").vector2Value = V(entry.capraz);
                pose.FindPropertyRelative("upMuzzle").vector2Value = V(entry.yukari);

                if (entry.body != "gila_idle_0")
                    continue;
                SetFrames(so.FindProperty("forwardFrames"), $"{folder}{entry.body}_ileri");
                SetFrames(so.FindProperty("diagonalFrames"), $"{folder}{entry.body}_capraz");
                SetFrames(so.FindProperty("upFrames"), $"{folder}{entry.body}_yukari");
                so.FindProperty("forwardMuzzle").vector2Value = V(entry.ileri);
                so.FindProperty("diagonalMuzzle").vector2Value = V(entry.capraz);
                so.FindProperty("upMuzzle").vector2Value = V(entry.yukari);
            }
        }

        /// <summary>JSON'daki [x, y]; o nişan yönü çizilmediyse sıfır (oyunda ileri yön kullanılır).</summary>
        static Vector2 V(float[] xy) => xy != null && xy.Length >= 2 ? new Vector2(xy[0], xy[1]) : Vector2.zero;

        /// <summary>prefix_0, prefix_1, ... dosyaları sırayla (kazmada 4 kare; çizilmeyen nişan yönünde hiç yok).</summary>
        static void SetFrames(SerializedProperty prop, string prefix)
        {
            int count = 0;
            while (File.Exists($"{prefix}_{count}.png"))
                count++;
            prop.arraySize = count;
            for (int i = 0; i < count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = Sprite($"{prefix}_{i}.png");
        }

        /// <summary>
        /// Dinamit (Yarasa'dan): 3 çubuk, kavisli atılır, seker ve yuvarlanır; 1,3 sn fitilden sonra ya da düşmana
        /// değince patlar. Alandaki her düşmana 4 patlama hasarı. Gila'yı yakmaz, savurur. Son çubukla elden düşer.
        /// </summary>
        static WeaponData BuildDynamite()
        {
            WeaponData data = LoadOrCreate("Dinamit");
            var so = Begin(data, "Dinamit", "dinamit", "dinamit_cubuk", "dinamit_ikon");
            so.FindProperty("muzzleFlashFrames").arraySize = 0;
            Set(so, "fireSound", null);
            so.FindProperty("fireMode").enumValueIndex = (int)WeaponData.FireMode.Thrown;
            so.FindProperty("consumable").boolValue = true;
            so.FindProperty("ammo").intValue = 3;
            so.FindProperty("shake").floatValue = 0f; // atış sarsmaz, patlama sarsar (Blast)
            Set(so, "swingSound", Clip("savurma"));
            Set(so, "fuseSound", Clip("fitil"));
            so.FindProperty("damage").intValue = 4;
            so.FindProperty("fireInterval").floatValue = 0.45f;
            so.FindProperty("recoverTime").floatValue = 0.2f;
            so.FindProperty("throwVelocity").vector2Value = new Vector2(8f, 6f);
            so.FindProperty("fuseTime").floatValue = 1.3f;
            so.FindProperty("blastRadius").floatValue = 1.7f;
            Set(so, "impactSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/patlama.wav"));
            so.FindProperty("impactVolume").floatValue = 0.85f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>
        /// Kazma (Armadillo'dan): yakın dövüş. Kaldırır, önüne indirir: önündeki her düşmana 4 hasar.
        /// 5 vuruşluk dayanıklılık; son vuruştan sonra kırılır.
        /// </summary>
        static WeaponData BuildPickaxe()
        {
            WeaponData data = LoadOrCreate("Kazma");
            var so = Begin(data, "Kazma", "kazma", "kazma", "kazma_ikon");
            so.FindProperty("muzzleFlashFrames").arraySize = 0;
            Set(so, "bulletSprite", null);
            Set(so, "fireSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/kazma.wav"));
            so.FindProperty("fireVolume").floatValue = 0.7f;
            so.FindProperty("fireMode").enumValueIndex = (int)WeaponData.FireMode.Melee;
            so.FindProperty("consumable").boolValue = true;
            so.FindProperty("ammo").intValue = 5;
            so.FindProperty("shake").floatValue = 0.22f;
            Set(so, "swingSound", Clip("savurma"));
            so.FindProperty("hitStop").floatValue = 0.06f;
            so.FindProperty("damage").intValue = 4;
            so.FindProperty("fireInterval").floatValue = 0.5f;
            so.FindProperty("meleeArea").vector2Value = new Vector2(1.5f, 1.4f);
            so.FindProperty("windupTime").floatValue = 0.1f;
            so.FindProperty("strikeTime").floatValue = 0.12f;
            so.FindProperty("followTime").floatValue = 0.14f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>
        /// Bıçak (Çalı Faresi'nden): hızlı, kısa menzilli yakın dövüş. Önüne saplar: önündeki düşmana 2 hasar.
        /// 6 saplamalık dayanıklılık, yalnız isabette azalır. Taşa işlemez. Fare her ölüşünde bırakır.
        /// </summary>
        static WeaponData BuildKnife()
        {
            WeaponData data = LoadOrCreate("Bicak");
            var so = Begin(data, "Bıçak", "bicak", "bicak", "bicak_ikon");
            so.FindProperty("muzzleFlashFrames").arraySize = 0;
            Set(so, "bulletSprite", null);
            Set(so, "fireSound", Clip("bocek_slap"));
            so.FindProperty("fireVolume").floatValue = 0.6f;
            so.FindProperty("fireMode").enumValueIndex = (int)WeaponData.FireMode.Melee;
            so.FindProperty("consumable").boolValue = true;
            so.FindProperty("ammo").intValue = 6;
            so.FindProperty("shake").floatValue = 0.1f;
            Set(so, "swingSound", Clip("savurma"));
            so.FindProperty("hitStop").floatValue = 0.04f;
            so.FindProperty("damage").intValue = 2;
            so.FindProperty("fireInterval").floatValue = 0.3f;
            so.FindProperty("meleeArea").vector2Value = new Vector2(1.3f, 1.1f);
            so.FindProperty("windupTime").floatValue = 0.05f;
            so.FindProperty("strikeTime").floatValue = 0.08f;
            so.FindProperty("followTime").floatValue = 0.1f;
            so.FindProperty("breaksRocks").boolValue = false;
            so.FindProperty("alwaysDrops").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>
        /// Diken demeti (Diken Mutantı'ndan): 4 atış, her atış 3 dikeni dar bir yelpazeyle fırlatır (diken başı 1 hasar,
        /// yakından üçü de isabet eder). Uzun menzilli; zemine/duvara saplanıp bir süre kalır.
        /// </summary>
        static WeaponData BuildSpikes()
        {
            WeaponData data = LoadOrCreate("Diken");
            var so = Begin(data, "Diken demeti", "diken", "diken_tek", "diken_ikon");
            so.FindProperty("muzzleFlashFrames").arraySize = 0;
            Set(so, "fireSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/diken_atis.wav"));
            so.FindProperty("fireVolume").floatValue = 0.8f;
            so.FindProperty("ammo").intValue = 4;
            so.FindProperty("damage").intValue = 1;
            so.FindProperty("pellets").intValue = 3;
            so.FindProperty("spreadAngle").floatValue = 7f;
            so.FindProperty("pelletJitter").floatValue = 0.15f;
            so.FindProperty("fireInterval").floatValue = 0.35f;
            so.FindProperty("bulletSpeed").floatValue = 17f;
            so.FindProperty("bulletRange").floatValue = 9f;
            so.FindProperty("sticksInGround").boolValue = true;
            so.FindProperty("shake").floatValue = 0.06f;
            so.FindProperty("thrownDamage").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        static void SetEnemyDrop(string prefabPath, WeaponData weapon, System.Action<SerializedObject> extra = null)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var enemy = root.GetComponent<EnemyBase>();
                var so = new SerializedObject(enemy);
                so.FindProperty("weaponDrop").objectReferenceValue = weapon;
                extra?.Invoke(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void AddPlayerWeapon()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                if (root.GetComponent<PlayerWeapon>() == null)
                    root.AddComponent<PlayerWeapon>();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
