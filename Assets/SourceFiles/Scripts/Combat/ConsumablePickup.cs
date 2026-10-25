using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* Ammo boxes and medkits, picked up just by walking over them.
     * Placed ones come back a while after they are taken; dropped ones disappear after a while.
     */
    public class ConsumablePickup : MonoBehaviour
    {
        public enum Kind
        {
            Ammo,
            Health
        }

        public static readonly List<ConsumablePickup> All = new List<ConsumablePickup>();

        public Kind Type = Kind.Ammo;
        [Tooltip("Placed pickups come back after this many seconds, 0 = never")]
        public float RespawnDelay = 45.0f;
        [Tooltip("Health restored by a medkit")]
        public float HealAmount = 40.0f;
        [Tooltip("Magazines added to each carried weapon by an ammo box")]
        public int AmmoMagazines = 2;
        public float PickupRadius = 1.3f;

        private float _lifetime;
        private float _born;
        private float _respawnAt = -1.0f;
        private Transform _visual;
        private Light _glow;
        private PlayerCombat _player;

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void OnDestroy() => NetWorld.Unregister(this);

        public float Lifetime => _lifetime;

        public static ConsumablePickup Spawn(Kind kind, Vector3 position, float lifetime)
        {
            var go = new GameObject(kind == Kind.Ammo ? "AmmoBox" : "Medkit");
            go.transform.position = position;
            var pickup = go.AddComponent<ConsumablePickup>();
            pickup.Type = kind;
            pickup.RespawnDelay = 0.0f;
            pickup._lifetime = lifetime;
            return pickup;
        }

        private void Start()
        {
            _born = Time.time;
            _player = FindAnyObjectByType<PlayerCombat>();
            BuildVisual();
            NetWorld.Register(this);
        }

        private void BuildVisual()
        {
            CombatEffects fx = CombatEffects.Instance;
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);

            if (Type == Kind.Ammo)
            {
                Part(_visual, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.42f, 0.24f, 0.26f), fx != null ? fx.AmmoBoxMaterial : null);
                Part(_visual, new Vector3(0.0f, 0.13f, 0.0f), new Vector3(0.3f, 0.03f, 0.18f), fx != null ? fx.BrassMaterial : null);
                Part(_visual, new Vector3(0.0f, 0.15f, 0.0f), new Vector3(0.12f, 0.03f, 0.05f), fx != null ? fx.CrateMetalMaterial : null);
            }
            else
            {
                Part(_visual, Vector3.zero, new Vector3(0.38f, 0.26f, 0.22f), fx != null ? fx.MedkitMaterial : null);
                // red cross on the front and on the lid
                Part(_visual, new Vector3(0.0f, 0.0f, 0.112f), new Vector3(0.16f, 0.05f, 0.01f), fx != null ? fx.CrossMaterial : null);
                Part(_visual, new Vector3(0.0f, 0.0f, 0.112f), new Vector3(0.05f, 0.16f, 0.01f), fx != null ? fx.CrossMaterial : null);
                Part(_visual, new Vector3(0.0f, 0.132f, 0.0f), new Vector3(0.16f, 0.01f, 0.05f), fx != null ? fx.CrossMaterial : null);
                Part(_visual, new Vector3(0.0f, 0.132f, 0.0f), new Vector3(0.05f, 0.01f, 0.16f), fx != null ? fx.CrossMaterial : null);
            }

            var glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.localPosition = Vector3.up * 0.5f;
            _glow = glowObject.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.color = Type == Kind.Ammo ? new Color(1.0f, 0.8f, 0.3f) : new Color(0.4f, 1.0f, 0.5f);
            _glow.range = 2.2f;
            _glow.intensity = 1.2f;
            _glow.shadows = LightShadows.None;
        }

        private static void Part(Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void Update()
        {
            if (_respawnAt > 0.0f)
            {
                if (Time.time < _respawnAt) return;
                _respawnAt = -1.0f;
                SetShown(true);
                NetWorld.Shown(this);
            }

            float t = Time.time - _born;
            _visual.localPosition = Vector3.up * (0.3f + Mathf.Sin(t * 2.4f) * 0.05f);
            _visual.localRotation = Quaternion.Euler(0.0f, t * 50.0f, 0.0f);

            if (_lifetime > 0.0f && t > _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (_player == null || _player.Health.IsDead) return;
            if ((_player.transform.position - transform.position).sqrMagnitude > PickupRadius * PickupRadius) return;
            // co-op: whoever the host gives it to (first come)
            if (!CanConsume() || !NetWorld.MayTake(this, () => { if (this != null && TryConsume()) Taken(); })) return;
            if (TryConsume()) Taken();
        }

        // what TryConsume would do, without doing it
        private bool CanConsume()
        {
            if (Type == Kind.Health)
            {
                if (_player.Health.Current < _player.Health.MaxHealth - 0.5f) return true;
                InventoryManager inventory = _player.GetComponent<InventoryManager>();
                return inventory != null && inventory.CanTake(QuickItem.Medkit);
            }

            foreach (WeaponInstance weapon in _player.Slots)
                if (weapon != null) return true;
            return false;
        }

        // co-op host: a client took this fixed (respawning) one
        public void TakenRemotely()
        {
            if (RespawnDelay > 0.0f) Taken();
            else Destroy(gameObject);
        }

        private bool TryConsume()
        {
            CombatEffects fx = CombatEffects.Instance;
            if (Type == Kind.Health)
            {
                // at full health it goes in the bag for later, if there's room; otherwise it waits on the ground
                if (_player.Health.Current >= _player.Health.MaxHealth - 0.5f)
                {
                    InventoryManager inventory = _player.GetComponent<InventoryManager>();
                    return inventory != null && inventory.CanTake(QuickItem.Medkit) && inventory.Add(QuickItem.Medkit, 1, true) > 0;
                }

                _player.Health.Heal(HealAmount);
                if (fx != null) fx.PlayUI(fx.HealthPickupSound, 0.7f);
                PickupFeed.Health(Mathf.RoundToInt(HealAmount));
                return true;
            }

            bool any = false;
            foreach (WeaponInstance weapon in _player.Slots)
            {
                if (weapon == null) continue;
                int rounds = weapon.Data.MagazineSize * AmmoMagazines;
                weapon.Reserve += rounds;
                PickupFeed.Ammo(weapon.Data, rounds);
                any = true;
            }

            if (!any) return false;
            if (fx != null) fx.PlayUI(fx.AmmoPickupSound, 0.7f);
            return true;
        }

        private void Taken()
        {
            if (RespawnDelay > 0.0f)
            {
                _respawnAt = Time.time + RespawnDelay;
                SetShown(false);
                NetWorld.Hidden(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SetShown(bool shown)
        {
            if (_visual != null) _visual.gameObject.SetActive(shown);
            if (_glow != null) _glow.enabled = shown;
        }
    }
}
