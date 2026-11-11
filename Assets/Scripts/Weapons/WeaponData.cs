using System.Collections.Generic;
using UnityEngine;

namespace DesertGecko.Weapons
{
    /// <summary>Gila'nın düşmandan alıp kullanabildiği silahın verisi: görseller, mermi, hasar, atış hızı.</summary>
    [CreateAssetMenu(menuName = "Desert Gecko/Silah", fileName = "Silah")]
    public class WeaponData : ScriptableObject
    {
        public enum Aim { Forward, Diagonal, Up }

        /// <summary>
        /// Mermi/saçma namludan düz çıkar; atılan (dinamit) kavis çizer, fitili bitince patlar;
        /// yakın dövüş (kazma) sallanır, vuruş anında önündekilere vurur. Mermi sayısı yakın dövüşte dayanıklılıktır.
        /// </summary>
        public enum FireMode { Bullet, Thrown, Melee }

        /// <summary>Bir gövde karesine uyan silah kareleri: silah o karedeki yumruğun içinde durur.</summary>
        [System.Serializable]
        public struct HeldPose
        {
            public Sprite body;
            public Sprite[] forward;
            public Sprite[] diagonal;
            public Sprite[] up;
            public Vector2 forwardMuzzle;
            public Vector2 diagonalMuzzle;
            public Vector2 upMuzzle;
        }

        [SerializeField] string displayName = "Silah";

        [Header("Görsel")]
        [Tooltip("Yerde duran silah ve arayüz ikonu")]
        [SerializeField] Sprite icon;
        [Tooltip("Gila'nın yumruğundaki silah kareleri: 0 tutuş, 1 ateş, 2 toparlanma")]
        [SerializeField] Sprite[] forwardFrames;
        [SerializeField] Sprite[] diagonalFrames;
        [SerializeField] Sprite[] upFrames;
        [Tooltip("Namlu ucu, Gila'nın görsel pivotuna göre (sağa bakarken), birim")]
        [SerializeField] Vector2 forwardMuzzle = new Vector2(0.6f, 0.5f);
        [SerializeField] Vector2 diagonalMuzzle = new Vector2(0.45f, 0.8f);
        [SerializeField] Vector2 upMuzzle = new Vector2(0.25f, 0.9f);
        [Tooltip("Gövde karesine göre silah kareleri (tools/gila_silah_ciz.py). Listede olmayan karelerde yukarıdakiler kullanılır")]
        [SerializeField] HeldPose[] poses;
        [SerializeField] Sprite bulletSprite;
        [SerializeField] Sprite[] muzzleFlashFrames;
        [SerializeField] Sprite ammoIcon;
        [SerializeField] Sprite ammoIconEmpty;

        [Header("Atış")]
        [SerializeField] FireMode fireMode = FireMode.Bullet;
        [Tooltip("Mermi bitince silah elden düşer (dinamit gibi tükenen silahlar): boş silah fırlatılmaz")]
        [SerializeField] bool consumable;
        [SerializeField] int ammo = 6;
        [SerializeField] int damage = 2;
        [Tooltip("İki atış arasındaki en kısa süre")]
        [SerializeField] float fireInterval = 0.3f;
        [SerializeField] float bulletSpeed = 18f;
        [SerializeField] float bulletRange = 10f;
        [Tooltip("Bir atıştaki tane sayısı: 1 = tek mermi, fazlası saçma")]
        [SerializeField] int pellets = 1;
        [Tooltip("Tanelerin nişan çizgisinden en fazla sapması (derece, tek yön): menzil uzadıkça dağılırlar")]
        [SerializeField] float spreadAngle = 0f;
        [Tooltip("Tanelerin hız ve menzil farkı (0-1): saçma düzgün bir yay değil, dağınık bir bulut olsun")]
        [SerializeField] float pelletJitter = 0f;
        [Tooltip("Atışta Gila'yı geri iten hız (0 = itmez)")]
        [SerializeField] float recoil = 0f;
        [Tooltip("Mermi zemine/duvara çarpınca saplı kalır, bir süre sonra söner (diken)")]
        [SerializeField] bool sticksInGround;
        [Tooltip("Atışta ekran sarsıntısı (0-1): altıpatlar hafif, pompalı belirgin")]
        [SerializeField] float shake = 0.1f;
        [Tooltip("Ateşten sonra toparlanma karesinin (örneğin pompanın geri çekilmesi) ekranda kalma süresi")]
        [SerializeField] float recoverTime = 0.14f;
        [Tooltip("Mermisi bitince fırlatılan silahın hasarı")]
        [SerializeField] int thrownDamage = 1;

        [Header("Atılan (dinamit)")]
        [Tooltip("İleri atış hızı (baktığı yöne, yukarı); çapraz ve yukarı atış bundan türetilir")]
        [SerializeField] Vector2 throwVelocity = new Vector2(8f, 6f);
        [SerializeField] float fuseTime = 1.3f;
        [SerializeField] float blastRadius = 1.7f;
        [SerializeField] AudioClip impactSound;
        [SerializeField] float impactVolume = 0.8f;

        [Header("Yakın dövüş (kazma)")]
        [Tooltip("Vuruş alanı: Gila'nın önünde bu genişlik ve yükseklikte")]
        [SerializeField] Vector2 meleeArea = new Vector2(1.5f, 1.4f);
        [Tooltip("Kaldırma, vuruş ve takip karelerinin süreleri; vuruş, vuruş karesine geçerken olur")]
        [SerializeField] float windupTime = 0.1f;
        [SerializeField] float strikeTime = 0.12f;
        [SerializeField] float followTime = 0.14f;

        [Header("Ses")]
        [SerializeField] AudioClip fireSound;
        [SerializeField] float fireVolume = 0.55f;
        [Tooltip("Atıştan sonra mekanizma sesi (pompa: çak-çak); boşsa yok")]
        [SerializeField] AudioClip cycleSound;
        [SerializeField] float cycleDelay = 0.1f;
        [Tooltip("Mekanizma çalışınca boş kovan fırlar (pompalı)")]
        [SerializeField] bool ejectsShell;
        [SerializeField] Color shellColor = new Color32(0xE2, 0x4E, 0x38, 0xFF);
        [Tooltip("Atma ve sallama sesi (dinamit, kazma)")]
        [SerializeField] AudioClip swingSound;
        [Tooltip("Mermi zırhtan/dikenden sekince")]
        [SerializeField] AudioClip ricochetSound;
        [Tooltip("Atılan dinamitin fitil cızırtısı")]
        [SerializeField] AudioClip fuseSound;
        [SerializeField] AudioClip pickupSound;

        [Header("Cila")]
        [Tooltip("Bu silahla vuruş/öldürme anında oyunun bir an durması (sn, 0 = yok)")]
        [SerializeField] float hitStop = 0.03f;
        [Tooltip("Elde silah varken yerdekiyle değiştirme ipucu (S tuşu)")]
        [SerializeField] Sprite swapHintSprite;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public Sprite BulletSprite => bulletSprite;
        public Sprite[] MuzzleFlashFrames => muzzleFlashFrames;
        public Sprite AmmoIcon => ammoIcon;
        public Sprite AmmoIconEmpty => ammoIconEmpty;
        public int Ammo => ammo;
        public int Damage => damage;
        public float FireInterval => fireInterval;
        public float BulletSpeed => bulletSpeed;
        public float BulletRange => bulletRange;
        public int ThrownDamage => thrownDamage;
        public FireMode Mode => fireMode;
        public Vector2 MeleeArea => meleeArea;
        public float WindupTime => windupTime;
        public float StrikeTime => strikeTime;
        public float FollowTime => followTime;
        public bool Consumable => consumable;
        public Vector2 ThrowVelocity => throwVelocity;
        public float FuseTime => fuseTime;
        public float BlastRadius => blastRadius;
        public AudioClip ImpactSound => impactSound;
        public float ImpactVolume => impactVolume;
        public int Pellets => Mathf.Max(1, pellets);
        public float SpreadAngle => spreadAngle;
        public float PelletJitter => pelletJitter;
        public float Recoil => recoil;
        public float Shake => shake;
        public bool SticksInGround => sticksInGround;
        public float RecoverTime => recoverTime;
        public AudioClip FireSound => fireSound;
        public AudioClip CycleSound => cycleSound;
        public float CycleDelay => cycleDelay;
        public bool EjectsShell => ejectsShell;
        public Color ShellColor => shellColor;
        public AudioClip SwingSound => swingSound;
        public AudioClip RicochetSound => ricochetSound;
        public AudioClip FuseSound => fuseSound;
        public AudioClip PickupSound => pickupSound;
        public float HitStop => hitStop;
        public Sprite SwapHintSprite => swapHintSprite;
        public float FireVolume => fireVolume;

        Dictionary<Sprite, int> poseByBody;

        void OnEnable() => poseByBody = null; // editörde düzenlenince yeniden kurulsun

        bool TryGetPose(Sprite body, out HeldPose pose)
        {
            pose = default;
            if (body == null || poses == null || poses.Length == 0)
                return false;
            if (poseByBody == null)
            {
                poseByBody = new Dictionary<Sprite, int>();
                for (int i = 0; i < poses.Length; i++)
                    if (poses[i].body != null)
                        poseByBody[poses[i].body] = i;
            }
            if (!poseByBody.TryGetValue(body, out int index))
                return false;
            pose = poses[index];
            return true;
        }

        /// <summary>Gövde karesine uyan silah kareleri; o kare için çizim yoksa varsayılan kareler.</summary>
        public Sprite[] FramesFor(Aim aim, Sprite body)
        {
            if (TryGetPose(body, out HeldPose pose))
            {
                Sprite[] frames = aim == Aim.Up ? pose.up : aim == Aim.Diagonal ? pose.diagonal : pose.forward;
                if (frames == null || frames.Length == 0 || frames[0] == null)
                    frames = pose.forward; // bu yöne çizim yok (kazma yalnız ileri sallanır)
                if (frames != null && frames.Length > 0 && frames[0] != null)
                    return frames;
            }
            return FramesFor(aim);
        }

        public Vector2 MuzzleFor(Aim aim, Sprite body)
        {
            if (TryGetPose(body, out HeldPose pose))
            {
                Vector2 muzzle = aim == Aim.Up ? pose.upMuzzle : aim == Aim.Diagonal ? pose.diagonalMuzzle : pose.forwardMuzzle;
                return muzzle != Vector2.zero ? muzzle : pose.forwardMuzzle;
            }
            return MuzzleFor(aim);
        }

        public Sprite[] FramesFor(Aim aim) => aim switch
        {
            Aim.Up => upFrames != null && upFrames.Length > 0 ? upFrames : forwardFrames,
            Aim.Diagonal => diagonalFrames != null && diagonalFrames.Length > 0 ? diagonalFrames : forwardFrames,
            _ => forwardFrames,
        };

        public Vector2 MuzzleFor(Aim aim) => aim switch
        {
            Aim.Up => upMuzzle,
            Aim.Diagonal => diagonalMuzzle,
            _ => forwardMuzzle,
        };
    }
}
