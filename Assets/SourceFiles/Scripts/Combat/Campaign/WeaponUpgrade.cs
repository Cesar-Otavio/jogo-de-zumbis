using UnityEngine;

namespace Combat
{
    public enum UpgradeSlot
    {
        Barrel,
        Magazine,
        Grip,
        Sight,
        Muzzle,
        Mechanism
    }

    /* A reusable weapon modification: percentages applied to any compatible gun, so no weapon
     * carries its own upgrade code. One upgrade per slot per weapon. Visual parts are optional
     * and can be assigned later; without one the upgrade still works.
     */
    [CreateAssetMenu(menuName = "Combat/Weapon Upgrade", fileName = "Upgrade_")]
    public class WeaponUpgrade : ScriptableObject
    {
        public string DisplayName = "Melhoria";
        [TextArea] public string Description;
        public UpgradeSlot Slot;
        public int Price = 300;
        [Tooltip("Player level needed before the shop offers it")]
        public int RequiredLevel = 1;

        [Header("Compatibility")]
        public bool Pistols = true;
        public bool Rifles = true;
        public bool Scoped = true;
        public bool Shotguns = true;

        [Header("Effects (fractions, 0.15 = +15%)")]
        public float Damage;
        public float Magazine;
        [Tooltip("Less recoil: 0.3 = 30% less kick")]
        public float RecoilReduction;
        [Tooltip("Less spread and faster settling")]
        public float Stability;
        public float ReloadSpeed;
        [Tooltip("Zoom of an alternative sight: 0.15 = 15% narrower field of view when aiming")]
        public float SightZoom;
        public bool Suppressor;

        [Header("Optional visual")]
        [Tooltip("Model mounted on the gun when the upgrade is fitted (none yet: the effect works without it)")]
        public GameObject Part;

        public bool Fits(WeaponData weapon)
        {
            if (weapon == null) return false;
            if (weapon.HasScope) return Scoped && !(SightZoom > 0.0f);
            if (weapon.Pellets > 1) return Shotguns && !Suppressor;
            return weapon.Hold == HoldStyle.Pistol ? Pistols : Rifles;
        }
    }
}
