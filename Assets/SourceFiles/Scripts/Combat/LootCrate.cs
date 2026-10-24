using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* A military chest: walk up and press the interact key to open it. The lid swings open and a random
     * weapon (plus some ammo) pops out. Chests close and restock when a new wave starts.
     */
    public class LootCrate : MonoBehaviour
    {
        public static readonly List<LootCrate> All = new List<LootCrate>();

        [Tooltip("Weapons that can come out, picked by weight")]
        public WeaponData[] LootTable;
        public float[] Weights;
        [Range(0, 1)] public float AmmoChance = 0.7f;
        [Range(0, 1)] public float MedkitChance = 0.35f;
        [Range(0, 1)] public float ItemChance = 0.45f;

        public bool IsOpen { get; private set; }
        public Vector3 Center => transform.position + Vector3.up * 0.4f;

        private Transform _lid;
        private Light _beacon;
        private float _openTime = -10.0f;

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            CombatEffects fx = CombatEffects.Instance;
            Material wood = fx != null ? fx.CrateMaterial : null;
            Material metal = fx != null ? fx.CrateMetalMaterial : null;

            // a solid body that blocks bullets and the player
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0.0f, 0.3f, 0.0f);
            box.size = new Vector3(0.95f, 0.6f, 0.6f);

            Part("Body", transform, new Vector3(0.0f, 0.25f, 0.0f), new Vector3(0.9f, 0.5f, 0.55f), wood);
            Part("BandLeft", transform, new Vector3(-0.3f, 0.25f, 0.0f), new Vector3(0.06f, 0.52f, 0.57f), metal);
            Part("BandRight", transform, new Vector3(0.3f, 0.25f, 0.0f), new Vector3(0.06f, 0.52f, 0.57f), metal);
            Part("Latch", transform, new Vector3(0.0f, 0.44f, 0.285f), new Vector3(0.12f, 0.1f, 0.03f), metal);

            // the lid hinges on the back top edge
            _lid = new GameObject("Lid").transform;
            _lid.SetParent(transform, false);
            _lid.localPosition = new Vector3(0.0f, 0.5f, -0.275f);
            Part("LidTop", _lid, new Vector3(0.0f, 0.06f, 0.285f), new Vector3(0.92f, 0.12f, 0.57f), wood);
            Part("LidBand", _lid, new Vector3(0.0f, 0.06f, 0.285f), new Vector3(0.95f, 0.13f, 0.06f), metal);

            // a faint glow so closed chests can be found in the dark
            var beaconObject = new GameObject("Beacon");
            beaconObject.transform.SetParent(transform, false);
            beaconObject.transform.localPosition = new Vector3(0.0f, 0.9f, 0.0f);
            _beacon = beaconObject.AddComponent<Light>();
            _beacon.type = LightType.Point;
            _beacon.color = new Color(1.0f, 0.75f, 0.35f);
            _beacon.range = 3.5f;
            _beacon.shadows = LightShadows.None;
        }

        private static void Part(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void Update()
        {
            if (_lid == null) return;

            float t = Mathf.Clamp01((Time.time - _openTime) / 0.45f);
            float angle = IsOpen ? Mathf.Lerp(0.0f, -110.0f, 1.0f - (1.0f - t) * (1.0f - t)) : 0.0f;
            _lid.localRotation = Quaternion.Euler(angle, 0.0f, 0.0f);
            if (_beacon != null) _beacon.intensity = IsOpen ? 0.0f : 0.8f + Mathf.Sin(Time.time * 3.0f) * 0.3f;
        }

        // weighted: scrap, cloth and wiring are everywhere, tactical parts are rare
        private static readonly (string id, float weight)[] Materials =
        {
            ("scrap", 5.0f), ("cloth", 4.0f), ("wiring", 3.0f), ("med_supplies", 2.5f), ("electronics", 2.0f),
            ("mech_parts", 2.0f), ("chemicals", 1.5f), ("tactical_parts", 0.8f)
        };

        private static string RandomMaterial()
        {
            float total = 0.0f;
            foreach (var m in Materials) total += m.weight;
            float pick = Random.value * total;
            foreach (var m in Materials)
            {
                pick -= m.weight;
                if (pick <= 0.0f) return m.id;
            }

            return "scrap";
        }

        public void Open(PlayerCombat player)
        {
            if (IsOpen) return;

            IsOpen = true;
            _openTime = Time.time;
            CombatEffects fx = CombatEffects.Instance;
            if (fx != null) fx.PlaySound(fx.CrateOpenSound, Center, 0.9f);

            WeaponData weapon = PickWeapon();
            if (weapon != null)
            {
                var go = new GameObject("CrateLoot_" + weapon.DisplayName);
                go.transform.position = transform.position + Vector3.up * 0.55f;
                var pickup = go.AddComponent<WeaponPickup>();
                pickup.Data = weapon;
                pickup.Floating = true;
                pickup.Lifetime = 90.0f;
            }

            Vector3 side = transform.right * 0.8f;
            if (Random.value < AmmoChance) ConsumablePickup.Spawn(ConsumablePickup.Kind.Ammo, transform.position + side, 60.0f);
            if (Random.value < MedkitChance) ConsumablePickup.Spawn(ConsumablePickup.Kind.Health, transform.position - side, 60.0f);
            if (Random.value < ItemChance)
            {
                QuickItem[] items = { QuickItem.Grenade, QuickItem.Molotov, QuickItem.Flare, QuickItem.Decoy, QuickItem.Barricade };
                ItemPickup.Spawn(items[Random.Range(0, items.Length)], 1, transform.position + transform.forward * 0.8f, 60.0f);
            }

            // crafting materials: common things often, rare parts sometimes, fewer on hard difficulties
            int materials = Random.value < 0.75f * GameDifficulty.Settings.Supplies ? Random.Range(1, 3) : 0;
            for (int i = 0; i < materials; i++)
            {
                string id = RandomMaterial();
                Vector3 at = transform.position + transform.forward * -0.8f + side * (i == 0 ? 0.4f : -0.4f) + Vector3.up * 0.05f;
                WorldItem.Spawn(id, id == "scrap" || id == "cloth" ? Random.Range(1, 3) : 1, at);
            }

            // a chest found pays a few credits too
            RunState.AddCredits(15, "baú");
            ItemCatalog catalog = ItemCatalog.Instance;
            PickupFeed.Report("credits", catalog != null ? catalog.CreditsIcon : null, "Créditos militares", 15, PickupFeed.TintOf(ItemCategory.Currency));
        }

        // a new wave: shut and restock
        public void Restock()
        {
            IsOpen = false;
        }

        private WeaponData PickWeapon()
        {
            if (LootTable == null || LootTable.Length == 0) return null;

            float total = 0.0f;
            for (int i = 0; i < LootTable.Length; i++) total += Weight(i);
            float roll = Random.value * total;
            for (int i = 0; i < LootTable.Length; i++)
            {
                roll -= Weight(i);
                if (roll <= 0.0f) return LootTable[i];
            }

            return LootTable[LootTable.Length - 1];
        }

        private float Weight(int index)
        {
            return Weights != null && index < Weights.Length ? Weights[index] : 1.0f;
        }
    }
}
