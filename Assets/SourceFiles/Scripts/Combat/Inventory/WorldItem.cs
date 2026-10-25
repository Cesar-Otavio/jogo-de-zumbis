using UnityEngine;

namespace Combat
{
    /* A catalog item lying in the world that goes into the backpack with E: crafting materials,
     * bandages, things the player dropped. Quick items go through the quick bar first (and its
     * overflow into the backpack). With no room it stays where it is.
     */
    public class WorldItem : MonoBehaviour, IInteractable
    {
        public string ItemId = "scrap";
        public int Amount = 1;
        [Tooltip("Placed loot keeps its glow small so it reads in the dark without lighting the room")]
        public bool Glow = true;

        private Transform _visual;
        private float _born;

        public Vector3 InteractionPoint => transform.position + Vector3.up * 0.3f;
        public float InteractionRange => 1.8f;

        public string Prompt
        {
            get
            {
                string name = ItemCatalog.NameOf(ItemId);
                string amount = Amount > 1 ? $" ×{Amount}" : "";
                return Room() > 0 ? $"{Interactables.Key}Pegar {name}{amount}" : $"{name}{amount} — <color=#FF6655>mochila cheia</color>";
            }
        }

        public static WorldItem Spawn(string id, int amount, Vector3 position)
        {
            var go = new GameObject("WorldItem_" + id);
            go.transform.position = position;
            var item = go.AddComponent<WorldItem>();
            item.ItemId = id;
            item.Amount = amount;
            return item;
        }

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);
        private void OnDestroy() => NetWorld.Unregister(this);

        private int Room()
        {
            if (Backpack.TryQuickItem(ItemId, out QuickItem quick) && InventoryManager.Player != null && InventoryManager.Player.CanTake(quick)) return Amount;
            return Backpack.RoomFor(ItemId);
        }

        private void Start()
        {
            _born = Time.time + Random.value * 3.0f;
            NetWorld.Register(this);
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);

            // a small crate in the category colour with the item's icon on top
            ItemDefinition definition = ItemCatalog.Find(ItemId);
            Color tint = definition != null ? PickupFeed.TintOf(definition.Category) : Color.white;
            CombatEffects fx = CombatEffects.Instance;
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(_visual, false);
            box.transform.localScale = new Vector3(0.28f, 0.16f, 0.22f);
            if (fx != null && fx.CrateMetalMaterial != null) box.GetComponent<MeshRenderer>().sharedMaterial = fx.CrateMetalMaterial;

            Sprite icon = ItemCatalog.IconOf(ItemId);
            if (icon != null)
            {
                var label = new GameObject("Icon", typeof(SpriteRenderer));
                label.transform.SetParent(_visual, false);
                label.transform.localPosition = new Vector3(0.0f, 0.085f, 0.0f);
                label.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
                label.transform.localScale = Vector3.one * 0.08f;
                var sprite = label.GetComponent<SpriteRenderer>();
                sprite.sprite = icon;
                sprite.color = tint;
            }

            if (!Glow) return;
            var glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.localPosition = Vector3.up * 0.45f;
            var glow = glowObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = Color.Lerp(tint, Color.white, 0.4f);
            glow.range = 1.6f;
            glow.intensity = 0.8f;
            glow.shadows = LightShadows.None;
        }

        private void Update()
        {
            float t = Time.time - _born;
            if (_visual != null) _visual.localPosition = Vector3.up * (0.12f + Mathf.Sin(t * 2.0f) * 0.02f);
        }

        public void Interact(PlayerCombat player)
        {
            // co-op: whoever the host gives it to (first come)
            if (Room() <= 0 || !NetWorld.MayTake(this, () => { if (this != null) Interact(player); })) return;
            int taken;
            if (Backpack.TryQuickItem(ItemId, out QuickItem quick) && InventoryManager.Player != null)
            {
                taken = InventoryManager.Player.Add(quick, Amount, true);
            }
            else
            {
                taken = Backpack.Add(ItemId, Amount);
                if (taken > 0)
                {
                    ItemDefinition definition = ItemCatalog.Find(ItemId);
                    PickupFeed.Report(ItemId, ItemCatalog.IconOf(ItemId), ItemCatalog.NameOf(ItemId), taken,
                        definition != null ? PickupFeed.TintOf(definition.Category) : Color.white);
                }
            }

            if (taken <= 0)
            {
                player.ShowNotice("Mochila cheia — descarte algo [Tab]");
                return;
            }

            if (CombatEffects.Instance != null) CombatEffects.Instance.PlayUI(CombatEffects.Instance.PickupSound, 0.5f);
            Amount -= taken;
            if (Amount <= 0) Destroy(gameObject);
        }
    }
}
