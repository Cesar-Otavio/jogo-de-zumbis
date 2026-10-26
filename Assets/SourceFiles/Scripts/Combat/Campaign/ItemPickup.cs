using UnityEngine;

namespace Combat
{
    // A quick item lying around (grenades, molotovs, flares...): walked over, it goes into the bag if there's room
    public class ItemPickup : MonoBehaviour
    {
        public QuickItem Item = QuickItem.Grenade;
        public int Amount = 1;
        public float PickupRadius = 1.3f;
        [Tooltip("Gone after this long (0 = stays)")]
        public float Lifetime;

        private float _born;
        private Transform _visual;
        private InventoryManager _inventory;
        private Transform _player;

        public static ItemPickup Spawn(QuickItem item, int amount, Vector3 position, float lifetime)
        {
            var go = new GameObject("Item_" + item);
            go.transform.position = position;
            var pickup = go.AddComponent<ItemPickup>();
            pickup.Item = item;
            pickup.Amount = amount;
            pickup.Lifetime = lifetime;
            return pickup;
        }

        private void Start()
        {
            _born = Time.time;
            _inventory = InventoryManager.Player;
            if (_inventory != null) _player = _inventory.transform;
            NetWorld.Register(this);

            CombatEffects fx = CombatEffects.Instance;
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            Material box = fx != null ? fx.AmmoBoxMaterial : null;
            Material mark = Item == QuickItem.Medkit ? (fx != null ? fx.CrossMaterial : null) : fx != null ? fx.BrassMaterial : null;
            Part(Vector3.zero, new Vector3(0.3f, 0.16f, 0.22f), box);
            Part(new Vector3(0.0f, 0.09f, 0.0f), new Vector3(0.2f, 0.02f, 0.12f), mark);

            var glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.localPosition = Vector3.up * 0.5f;
            var glow = glowObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1.0f, 0.55f, 0.2f);
            glow.range = 2.0f;
            glow.intensity = 1.0f;
            glow.shadows = LightShadows.None;
        }

        private void Part(Vector3 position, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(_visual, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.ItemPickup.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            float t = Time.time - _born;
            _visual.localPosition = Vector3.up * (0.3f + Mathf.Sin(t * 2.4f) * 0.05f);
            _visual.localRotation = Quaternion.Euler(0.0f, t * 50.0f, 0.0f);
            if (Lifetime > 0.0f && t > Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (_player == null || (_player.position - transform.position).sqrMagnitude > PickupRadius * PickupRadius) return;
            if (!_inventory.CanTake(Item)) return;
            // co-op: whoever the host gives it to (first come)
            if (!NetWorld.MayTake(this, () => { if (this != null) TakeNow(); })) return;
            TakeNow();
        }

        private void OnDestroy() => NetWorld.Unregister(this);

        private void TakeNow()
        {
            if (_inventory == null || !_inventory.CanTake(Item)) return;
            Amount -= _inventory.Add(Item, Amount, true);
            if (CombatEffects.Instance != null) CombatEffects.Instance.PlayUI(CombatEffects.Instance.AmmoPickupSound, 0.6f);
            if (Amount <= 0) Destroy(gameObject);
        }
    }
}
