using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* Upgrades fitted during a run. Each base weapon gets one runtime copy of its WeaponData with
     * the upgrades applied; guns of that kind use the copy, so firing, reloading, ADS, recoil, IK,
     * pickups and the HUD all keep reading ordinary WeaponData. The base asset is never touched.
     */
    public static class WeaponUpgrades
    {
        private static readonly Dictionary<WeaponData, List<WeaponUpgrade>> Fitted = new Dictionary<WeaponData, List<WeaponUpgrade>>();
        private static readonly Dictionary<WeaponData, WeaponData> Upgraded = new Dictionary<WeaponData, WeaponData>();
        private static readonly Dictionary<WeaponData, WeaponData> BaseOfCopy = new Dictionary<WeaponData, WeaponData>();

        public static event Action<WeaponData> Changed;

        public static void Reset()
        {
            foreach (WeaponData copy in Upgraded.Values)
            {
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }

            Fitted.Clear();
            Upgraded.Clear();
            BaseOfCopy.Clear();
        }

        public static WeaponData BaseOf(WeaponData data)
        {
            return data != null && BaseOfCopy.TryGetValue(data, out WeaponData original) ? original : data;
        }

        public static bool SameWeapon(WeaponData a, WeaponData b)
        {
            return a != null && b != null && BaseOf(a) == BaseOf(b);
        }

        // what a gun of this kind should use right now: the upgraded copy, or the asset itself
        public static WeaponData Resolve(WeaponData data)
        {
            WeaponData original = BaseOf(data);
            return original != null && Upgraded.TryGetValue(original, out WeaponData copy) ? copy : original;
        }

        public static IReadOnlyList<WeaponUpgrade> On(WeaponData data)
        {
            return Fitted.TryGetValue(BaseOf(data), out List<WeaponUpgrade> list) ? list : (IReadOnlyList<WeaponUpgrade>)Array.Empty<WeaponUpgrade>();
        }

        public static bool Has(WeaponData data, WeaponUpgrade upgrade)
        {
            foreach (WeaponUpgrade fitted in On(data)) if (fitted == upgrade) return true;
            return false;
        }

        public static bool SlotTaken(WeaponData data, UpgradeSlot slot)
        {
            foreach (WeaponUpgrade fitted in On(data)) if (fitted.Slot == slot) return true;
            return false;
        }

        public static bool CanFit(WeaponData data, WeaponUpgrade upgrade)
        {
            return upgrade != null && upgrade.Fits(BaseOf(data)) && !SlotTaken(data, upgrade.Slot);
        }

        public static bool Fit(WeaponData data, WeaponUpgrade upgrade)
        {
            WeaponData original = BaseOf(data);
            if (!CanFit(original, upgrade)) return false;

            if (!Fitted.TryGetValue(original, out List<WeaponUpgrade> list)) Fitted[original] = list = new List<WeaponUpgrade>();
            list.Add(upgrade);
            Rebuild(original);
            Changed?.Invoke(original);
            return true;
        }

        private static void Rebuild(WeaponData original)
        {
            if (Upgraded.TryGetValue(original, out WeaponData old) && old != null)
            {
                BaseOfCopy.Remove(old);
                UnityEngine.Object.Destroy(old);
            }

            WeaponData copy = UnityEngine.Object.Instantiate(original);
            copy.name = original.name + "_Upgraded";
            foreach (WeaponUpgrade upgrade in Fitted[original]) Apply(copy, original, upgrade);
            Upgraded[original] = copy;
            BaseOfCopy[copy] = original;
        }

        private static void Apply(WeaponData copy, WeaponData original, WeaponUpgrade upgrade)
        {
            copy.Damage += original.Damage * upgrade.Damage;
            copy.MagazineSize += Mathf.Max(upgrade.Magazine > 0.0f ? 1 : 0, Mathf.RoundToInt(original.MagazineSize * upgrade.Magazine));

            float recoil = 1.0f - Mathf.Clamp01(upgrade.RecoilReduction);
            copy.KickBack *= recoil;
            copy.KickPitch *= recoil;
            copy.KickYaw *= recoil;
            copy.CameraPitchKick *= recoil;
            copy.CameraYawKick *= recoil;

            float steady = 1.0f - Mathf.Clamp01(upgrade.Stability);
            copy.HipSpread *= steady;
            copy.AimSpread *= steady;
            copy.MoveSpread *= steady;
            copy.SpreadPerShot *= steady;
            copy.BloomRecovery *= 1.0f + upgrade.Stability;

            float reload = 1.0f / (1.0f + Mathf.Max(0.0f, upgrade.ReloadSpeed));
            copy.ReloadTime *= reload;
            copy.ShellInsertTime *= reload;

            if (upgrade.SightZoom > 0.0f) copy.AimFov *= 1.0f - upgrade.SightZoom;
            if (upgrade.Suppressor) copy.Suppressed = true;
        }
    }
}
