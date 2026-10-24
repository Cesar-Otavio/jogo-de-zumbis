using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Combat
{
    /* The military shop between waves (L). Spends military credits, never the score. Every
     * purchase is checked (balance, level, per-run limit, compatibility, room) and paid in one
     * step, at most one per click. The game keeps running while it's open (it only opens in the
     * break between waves), and the next wave waits for it to close.
     */
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }
        public static bool IsOpen { get; private set; }
        // a mission reward: everything cheaper for the rest of the run
        public static float Discount;

        public ShopCatalog Catalog;

        private PlayerCombat _player;
        private StarterAssetsInputs _input;
        private InventoryManager _inventory;
        private EnemySpawner _spawner;
        private readonly Dictionary<string, int> _bought = new Dictionary<string, int>();
        private ShopCategory _category = ShopCategory.Armas;
        private int _lastPurchaseFrame = -1;

        private GameObject _panel;
        private TextMeshProUGUI _credits;
        private TextMeshProUGUI _info;
        private RectTransform _list;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly List<Button> _tabs = new List<Button>();

        private static readonly string[] CategoryNames = { "ARMAS", "MUNIÇÃO", "PROTEÇÃO", "CONSUMÍVEIS", "MELHORIAS" };

        private void Awake()
        {
            Instance = this;
            IsOpen = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            IsOpen = false;
        }

        private void Start()
        {
            _player = FindAnyObjectByType<PlayerCombat>();
            _input = _player != null ? _player.GetComponent<StarterAssetsInputs>() : null;
            _inventory = _player != null ? _player.GetComponent<InventoryManager>() : null;
            _spawner = FindAnyObjectByType<EnemySpawner>();
            Build();
            RunState.CreditsChanged += (_, __) => { if (IsOpen) Refresh(); };
        }

        public void ResetForRun()
        {
            _bought.Clear();
            Discount = 0.0f;
            if (IsOpen) Close();
        }

        // the shop only opens in the break between waves, with no fight going on
        public bool Available
        {
            get
            {
                GameFlow flow = GameFlow.Instance;
                if (flow == null || flow.Current != GameFlow.State.Playing || _player == null || _player.Health.IsDead) return false;
                if (CampaignManager.Instance != null && CampaignManager.Instance.IsRunning) return CampaignManager.Instance.InIntermission;
                return _spawner != null && _spawner.InIntermission;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.lKey.wasPressedThisFrame)
            {
                if (IsOpen) Close();
                else if (Available) Open();
                else if (_player != null && GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Playing) _player.ShowNotice("A loja abre no intervalo entre ondas");
            }

            if (!IsOpen) return;
            // a wave can't start with the shop open, but if it somehow does, the shop gets out of the way
            if (!Available) Close();
            if (_input != null)
            {
                _input.fire = false;
                _input.aim = false;
            }

            // switching guns (Q) changes which upgrades apply
            WeaponData current = _player.Current?.Data;
            if (current != _shownWeapon)
            {
                _shownWeapon = current;
                Refresh();
            }
        }

        private WeaponData _shownWeapon;

        public void Open()
        {
            IsOpen = true;
            _panel.SetActive(true);
            if (_input != null) _input.SetCursorLocked(false);
            Refresh();
        }

        public void Close()
        {
            IsOpen = false;
            _panel.SetActive(false);
            if (_input != null && GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Playing) _input.SetCursorLocked(true);
        }

        // ---------------------------------------------------------------- buying

        public int PriceOf(ShopCatalog.Entry entry)
        {
            return Mathf.Max(0, Mathf.RoundToInt(entry.Price * (1.0f - Discount)));
        }

        // null when it can be bought, otherwise why not
        public string Unavailable(ShopCatalog.Entry entry)
        {
            if (Progression.Level < entry.RequiredLevel) return "NÍVEL " + entry.RequiredLevel;
            if (entry.LimitPerRun > 0 && _bought.TryGetValue(entry.Id, out int count) && count >= entry.LimitPerRun) return "ESGOTADO";

            switch (entry.Kind)
            {
                case ShopItemKind.Weapon:
                    foreach (WeaponInstance owned in _player.Slots)
                    {
                        if (owned != null && WeaponUpgrades.SameWeapon(owned.Data, entry.Weapon)) return "JÁ POSSUI";
                    }

                    break;
                case ShopItemKind.Ammo:
                case ShopItemKind.AmmoFull:
                    if (_player.Current == null && _player.Other == null) return "SEM ARMA";
                    break;
                case ShopItemKind.Heal:
                    if (_player.Health.Current >= _player.Health.MaxHealth - 0.5f) return "VIDA CHEIA";
                    break;
                case ShopItemKind.Armor:
                    if (_player.Health.Armor >= 59.0f) return "COLETE CHEIO";
                    break;
                case ShopItemKind.Consumable:
                    if (_inventory == null || !_inventory.CanTake(entry.Item)) return "SEM ESPAÇO";
                    break;
                case ShopItemKind.WeaponUpgrade:
                    if (_player.Current == null) return "SEM ARMA";
                    if (WeaponUpgrades.Has(_player.Current.Data, entry.Upgrade)) return "INSTALADO";
                    if (!entry.Upgrade.Fits(WeaponUpgrades.BaseOf(_player.Current.Data))) return "INCOMPATÍVEL";
                    if (WeaponUpgrades.SlotTaken(_player.Current.Data, entry.Upgrade.Slot)) return "ENCAIXE OCUPADO";
                    break;
            }

            if (RunState.Credits < PriceOf(entry)) return "SEM CRÉDITOS";
            return null;
        }

        public bool TryBuy(ShopCatalog.Entry entry)
        {
            // one purchase per frame: a double click or a held key can't buy twice
            if (_lastPurchaseFrame == Time.frameCount || !IsOpen) return false;
            _lastPurchaseFrame = Time.frameCount;

            if (Unavailable(entry) != null || !RunState.TrySpend(PriceOf(entry)))
            {
                CampaignAssets.PlayUI(a => a.ShopDeny, 0.6f);
                return false;
            }

            Apply(entry);
            _bought[entry.Id] = (_bought.TryGetValue(entry.Id, out int count) ? count : 0) + 1;
            CampaignAssets.PlayUI(a => a.ShopBuy, 0.7f);
            Refresh();
            return true;
        }

        private void Apply(ShopCatalog.Entry entry)
        {
            switch (entry.Kind)
            {
                case ShopItemKind.Weapon:
                    WeaponData data = entry.Weapon;
                    _player.Give(new WeaponInstance(data, data.MagazineSize, data.MagazineSize * 3));
                    break;
                case ShopItemKind.Ammo:
                    foreach (WeaponInstance weapon in _player.Slots) if (weapon != null) weapon.Reserve += weapon.Data.MagazineSize * Mathf.Max(1, entry.Amount);
                    break;
                case ShopItemKind.AmmoFull:
                    foreach (WeaponInstance weapon in _player.Slots)
                    {
                        if (weapon == null) continue;
                        weapon.Magazine = weapon.Data.MagazineSize;
                        weapon.Reserve = Mathf.Max(weapon.Reserve, weapon.Data.MagazineSize * 6);
                    }

                    break;
                case ShopItemKind.Armor:
                    _player.Health.Armor = 60.0f;
                    break;
                case ShopItemKind.Heal:
                    _player.Health.ResetHealth();
                    break;
                case ShopItemKind.Consumable:
                    _inventory.Add(entry.Item, entry.Amount, true);
                    break;
                case ShopItemKind.WeaponUpgrade:
                    WeaponUpgrades.Fit(_player.Current.Data, entry.Upgrade);
                    break;
                case ShopItemKind.Stamina:
                    _player.Controller.MaxStamina += 15.0f;
                    _player.Controller.RefillStamina();
                    break;
                case ShopItemKind.Capacity:
                    if (_inventory != null) _inventory.ExtraCapacity++;
                    break;
            }
        }

        // ---------------------------------------------------------------- presentation

        // the icon comes from what's being sold: the gun, the quick item, the catalog's shared icons
        private Sprite IconOf(ShopCatalog.Entry entry)
        {
            if (entry.Icon != null) return entry.Icon;
            ItemCatalog catalog = ItemCatalog.Instance;
            switch (entry.Kind)
            {
                case ShopItemKind.Weapon: return ItemCatalog.IconOf(entry.Weapon);
                case ShopItemKind.Ammo:
                case ShopItemKind.AmmoFull: return _player != null && _player.Current != null ? ItemCatalog.AmmoIconOf(_player.Current.Data) : ItemCatalog.IconOf("ammo_box");
                case ShopItemKind.Armor: return catalog != null ? catalog.ArmorIcon : null;
                case ShopItemKind.Heal: return catalog != null ? catalog.HealthIcon : null;
                case ShopItemKind.Consumable: return ItemCatalog.IconOf(entry.Item);
                case ShopItemKind.WeaponUpgrade: return catalog != null ? catalog.UpgradeIcon : null;
                case ShopItemKind.Stamina: return catalog != null ? catalog.StaminaIcon : null;
                case ShopItemKind.Capacity: return catalog != null ? catalog.CapacityIcon : null;
                default: return catalog != null ? catalog.FallbackIcon : null;
            }
        }

        private static Color TintOf(ShopCatalog.Entry entry)
        {
            switch (entry.Kind)
            {
                case ShopItemKind.Weapon:
                case ShopItemKind.Armor: return PickupFeed.TintOf(ItemCategory.Weapon);
                case ShopItemKind.Ammo:
                case ShopItemKind.AmmoFull: return PickupFeed.TintOf(ItemCategory.Ammo);
                case ShopItemKind.Heal:
                case ShopItemKind.Stamina: return PickupFeed.TintOf(ItemCategory.Medical);
                case ShopItemKind.Consumable:
                    ItemDefinition item = ItemCatalog.Find(ItemCatalog.IdOf(entry.Item));
                    return PickupFeed.TintOf(item != null ? item.Category : ItemCategory.Tactical);
                case ShopItemKind.Capacity: return PickupFeed.TintOf(ItemCategory.Tactical);
                default: return PickupFeed.TintOf(ItemCategory.Currency);
            }
        }

        // the entry's own text, or what the item catalog says about it, plus what a gun fires
        private static string DescriptionOf(ShopCatalog.Entry entry)
        {
            string text = entry.Description;
            if (string.IsNullOrEmpty(text) && entry.Kind == ShopItemKind.Consumable)
            {
                ItemDefinition item = ItemCatalog.Find(ItemCatalog.IdOf(entry.Item));
                if (item != null) text = item.Description;
            }

            if (entry.Kind == ShopItemKind.Weapon && entry.Weapon != null)
            {
                string kind = ClassName(entry.Weapon.Class) + " · " + entry.Weapon.AmmoName;
                text = string.IsNullOrEmpty(text) ? kind : kind + " — " + text;
            }

            return text;
        }

        private static string ClassName(WeaponClass type)
        {
            switch (type)
            {
                case WeaponClass.Pistol: return "Pistola";
                case WeaponClass.SubmachineGun: return "Submetralhadora";
                case WeaponClass.Shotgun: return "Escopeta";
                case WeaponClass.Sniper: return "Fuzil de precisão";
                default: return "Fuzil";
            }
        }

        // ---------------------------------------------------------------- interface

        private void Build()
        {
            RectTransform root = UiKit.Canvas("Shop", transform, 120);
            Image panel = UiKit.Panel("ShopPanel", root, new Vector2(0.18f, 0.1f), new Vector2(0.82f, 0.9f), new Color(UiKit.PanelColor.r, UiKit.PanelColor.g, UiKit.PanelColor.b, 1.0f));
            panel.raycastTarget = true;
            _panel = root.gameObject;
            RectTransform rect = panel.rectTransform;
            UiKit.Text(rect, "LOJA MILITAR", 44, new Vector2(0.04f, 0.88f), new Vector2(0.6f, 0.97f), TextAlignmentOptions.Left, UiKit.Accent, true);
            _credits = UiKit.Text(rect, "", 32, new Vector2(0.5f, 0.88f), new Vector2(0.96f, 0.97f), TextAlignmentOptions.Right, Color.white, true);

            for (int i = 0; i < CategoryNames.Length; i++)
            {
                int index = i;
                float x = 0.04f + i * 0.184f;
                _tabs.Add(UiKit.Button(rect, CategoryNames[i], new Vector2(x, 0.79f), new Vector2(x + 0.17f, 0.86f), () =>
                {
                    _category = (ShopCategory)index;
                    Refresh();
                }, false, 22));
            }

            _list = UiKit.Panel("List", rect, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.77f), new Color(0, 0, 0, 0.25f)).rectTransform;
            _info = UiKit.Text(rect, "", 20, new Vector2(0.04f, 0.03f), new Vector2(0.7f, 0.12f), TextAlignmentOptions.Left, UiKit.Muted);
            _info.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Button(rect, "FECHAR [L]", new Vector2(0.74f, 0.03f), new Vector2(0.96f, 0.11f), Close, true, 24);
            _panel.SetActive(false);
        }

        private void Refresh()
        {
            if (_panel == null) return;
            _credits.text = $"CRÉDITOS  <color=#FFC74D>{RunState.Credits}</color>";
            for (int i = 0; i < _tabs.Count; i++) _tabs[i].GetComponent<Image>().color = i == (int)_category ? new Color(1, 0.78f, 0.3f, 0.35f) : new Color(1, 1, 1, 0.1f);

            foreach (GameObject row in _rows) Destroy(row);
            _rows.Clear();
            if (Catalog == null || Catalog.Entries == null) return;

            var shown = new List<ShopCatalog.Entry>();
            foreach (ShopCatalog.Entry entry in Catalog.Entries)
            {
                if (entry.Category != _category) continue;
                // upgrades the current gun can't take are left out instead of cluttering the list
                if (entry.Kind == ShopItemKind.WeaponUpgrade && _player.Current != null && !entry.Upgrade.Fits(WeaponUpgrades.BaseOf(_player.Current.Data))) continue;
                shown.Add(entry);
            }

            float rowHeight = 1.0f / 8.0f;
            for (int i = 0; i < shown.Count && i < 8; i++)
            {
                ShopCatalog.Entry entry = shown[i];
                float top = 1.0f - i * rowHeight;
                Image row = UiKit.Panel("Row", _list, new Vector2(0.0f, top - rowHeight + 0.008f), new Vector2(1.0f, top - 0.008f), new Color(1, 1, 1, i % 2 == 0 ? 0.04f : 0.07f));
                _rows.Add(row.gameObject);
                RectTransform r = row.rectTransform;
                // icon in the category colour, like the quick bar and the pickup feed
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.rectTransform.SetParent(r, false);
                icon.rectTransform.anchorMin = new Vector2(0.01f, 0.12f);
                icon.rectTransform.anchorMax = new Vector2(0.11f, 0.88f);
                icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.sprite = IconOf(entry);
                icon.enabled = icon.sprite != null;
                icon.color = Unavailable(entry) == null ? TintOf(entry) : new Color(1.0f, 1.0f, 1.0f, 0.3f);
                UiKit.Text(r, entry.DisplayName, 24, new Vector2(0.13f, 0.45f), new Vector2(0.6f, 1.0f), TextAlignmentOptions.Left, Color.white, true);
                UiKit.Text(r, DescriptionOf(entry), 17, new Vector2(0.13f, 0.0f), new Vector2(0.62f, 0.5f), TextAlignmentOptions.TopLeft, UiKit.Muted);
                UiKit.Text(r, PriceOf(entry) + " CR", 24, new Vector2(0.6f, 0.0f), new Vector2(0.76f, 1.0f), TextAlignmentOptions.Right, UiKit.Accent, true);

                string reason = Unavailable(entry);
                Button buy = UiKit.Button(r, reason ?? "COMPRAR", new Vector2(0.79f, 0.15f), new Vector2(0.99f, 0.85f), () => TryBuy(entry), reason == null, 20);
                buy.interactable = reason == null;
            }

            _info.text = _category == ShopCategory.Melhorias
                ? (_player.Current != null ? $"Melhorias para a arma em mãos: <b>{_player.Current.Data.DisplayName}</b>. Troque de arma (Q) para ver as dela." : "Segure uma arma para ver suas melhorias.")
                : "Créditos vêm de abates, ondas, missões e depósitos. A pontuação e o recorde não são afetados.";
        }
    }
}
