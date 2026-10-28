using UnityEngine;

namespace Combat
{
    public enum FireMode
    {
        Semi,
        Auto
    }

    public enum HoldStyle
    {
        Pistol,
        Rifle
    }

    public enum ReloadStyle
    {
        Magazine,
        SingleShell
    }

    // what kind of gun it is: decides where it's carried when not in the hands
    public enum WeaponClass
    {
        Pistol,
        SubmachineGun,
        Shotgun,
        Rifle,
        Sniper
    }

    // Everything that makes one gun feel different from another
    [CreateAssetMenu(menuName = "Combat/Weapon Data", fileName = "Weapon_")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Weapon";
        public GameObject Model;
        public HoldStyle Hold = HoldStyle.Rifle;
        public WeaponClass Class = WeaponClass.Rifle;

        [Header("Presentation")]
        public Sprite Icon;
        [Tooltip("e.g. \".45 ACP\", \"5,45 mm\", \"calibre 12\"")]
        public string AmmoName = "munição";
        public Sprite AmmoIcon;

        [Header("Firing")]
        public FireMode Mode = FireMode.Auto;
        [Tooltip("Seconds between shots")]
        public float FireInterval = 0.1f;
        public float Damage = 20.0f;
        [Tooltip("Projectiles per shot, more than one for shotguns")]
        public int Pellets = 1;
        public float Range = 120.0f;
        [Tooltip("Suppressed shots are heard from much closer")]
        public bool Suppressed;
        [Tooltip("Impulse given to rigidbodies that are hit")]
        public float HitForce = 4.0f;

        [Header("Spread (degrees)")]
        public float HipSpread = 2.0f;
        public float AimSpread = 0.4f;
        public float MoveSpread = 1.5f;
        public float SpreadPerShot = 0.4f;
        public float MaxBloom = 4.0f;
        public float BloomRecovery = 8.0f;

        [Header("Ammo")]
        public int MagazineSize = 30;
        public int StartReserve = 90;
        public ReloadStyle Reload = ReloadStyle.Magazine;
        public float ReloadTime = 2.0f;
        [Tooltip("Time per shell for single shell reloads")]
        public float ShellInsertTime = 0.5f;

        [Header("Recoil")]
        public float KickBack = 0.06f;
        public float KickPitch = 6.0f;
        public float KickYaw = 2.0f;
        public float CameraPitchKick = 0.8f;
        public float CameraYawKick = 0.3f;

        [Header("Aiming")]
        public float AimFov = 30.0f;
        public bool HasScope;

        [Header("Model Points (weapon local space)")]
        public Vector3 GripPoint;
        public Vector3 SupportPoint;
        public Vector3 MuzzlePoint;
        public Vector3 EjectPoint;
        [Tooltip("Point behind the sights the eye looks through when aiming down sights")]
        public Vector3 SightPoint;

        [Header("Attachment")]
        [Tooltip("Optional scope or sight mounted on the weapon")]
        public GameObject Attachment;
        public Vector3 AttachmentOffset;

        [Header("Moving Parts")]
        [Tooltip("Child that comes out on reload")]
        public string MagazinePart;
        [Tooltip("Slide, bolt or pump that moves when cycling")]
        public string CyclingPart;
        public Vector3 CyclingTravel = new Vector3(0.0f, 0.0f, 0.05f);
        [Tooltip("Bolt action or pump: the part is worked by hand after each shot")]
        public bool ManualCycle;

        [Header("Audio")]
        public AudioClip[] ShotSounds;
        public AudioClip MagOutSound;
        public AudioClip MagInSound;
        public AudioClip ChargeSound;
        public AudioClip CycleSound;
        public AudioClip ShellInsertSound;
        public AudioClip DrySound;
    }
}
