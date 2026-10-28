using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Combat
{
    /* The backpack screen (Tab), and the bench screen when a CraftingStation is used:
     *   left   - what the survivor carries: both guns, the vest, the quick bar, mission items
     *   centre - the backpack grid, or the recipe list on the crafting tab
     *   right  - the selected thing: what it is, and only the actions that are possible now
     * Everything goes through Backpack, ItemActions, InventoryManager and Crafting; this class only
     * shows and forwards. Mouse and gamepad both work (buttons are ordinary selectables).
     */
    public class InventoryView : MonoBehaviour
    {
        public const string Name = "inventory";
        public static InventoryView Instance { get; private set; }
        public static bool IsOpen => GameplayOverlay.Open == Name;

        private enum Tab { Backpack, Crafting }
        private enum Selection { None, Slot, Quick, Recipe }

        private const int Columns = 6;

        private GameObject _panel;
        private Tab _tab;
        private CraftingStationType? _station;
        private string _stationName;
        private Selection _selection;
        private int _selected = -1;
        private int _moveFrom = -1;

        private TextMeshProUGUI _title;
        private TextMeshProUGUI _capacity;
        private Button _tabBackpack;
        private Button _tabCrafting;
        private GameObject _gridArea;
        private GameObject _recipeArea;
        private readonly List<SlotView> _slots = new List<SlotView>();
        private readonly List<SlotView> _quick = new List<SlotView>();
        private readonly List<RecipeRow> _recipes = new List<RecipeRow>();
        private TextMeshProUGUI _equipment;
        private TextMeshProUGUI _missionItems;

        private Image _detailIcon;
        private TextMeshProUGUI _detailName;
        private TextMeshProUGUI _detailMeta;
        private TextMeshProUGUI _detailText;
        private TextMeshProUGUI _detailReason;
        private readonly List<Button> _actions = new List<Button>();

        private StarterAssetsInputs _input;
        private bool _dirty;

        private class SlotView
        {
            public Button Button;
            public Image Frame;
            public Image Icon;
            public TextMeshProUGUI Count;
        }

        private class RecipeRow
        {
            public Button Button;
            public Image Frame;
            public Image Icon;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI State;
            public CraftingRecipe Recipe;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Backpack.Changed -= MarkDirty;
        }

        private void Start()
        {
            Build();
            _panel.SetActive(false);
            Backpack.Changed += MarkDirty;
            RunState.ItemsChanged += _ => MarkDirty();
            Crafting.Crafted += _ => MarkDirty();
        }

        private void MarkDirty() => _dirty = true;

        // ---------------------------------------------------------------- building

        private void Build()
        {
            RectTransform root = UiKit.Canvas("Backpack", transform, 125);
            _panel = UiKit.Panel("BackpackPanel", root, new Vector2(0.06f, 0.07f), new Vector2(0.94f, 0.93f), new Color(0.04f, 0.045f, 0.055f, 1.0f)).gameObject;
            var panel = (RectTransform)_panel.transform;
            UiKit.Panel("Edge", panel, new Vector2(0.0f, 0.995f), Vector2.one, new Color(1.0f, 0.78f, 0.3f, 0.6f));

            _title = UiKit.Text(panel, "MOCHILA", 38, new Vector2(0.02f, 0.9f), new Vector2(0.3f, 0.98f), TextAlignmentOptions.Left, UiKit.Accent, true);
            _tabBackpack = UiKit.Button(panel, "MOCHILA", new Vector2(0.3f, 0.915f), new Vector2(0.43f, 0.97f), () => SetTab(Tab.Backpack), false, 22);
            _tabCrafting = UiKit.Button(panel, "FABRICAÇÃO", new Vector2(0.44f, 0.915f), new Vector2(0.57f, 0.97f), () => SetTab(Tab.Crafting), false, 22);
            _capacity = UiKit.Text(panel, "", 24, new Vector2(0.6f, 0.9f), new Vector2(0.84f, 0.98f), TextAlignmentOptions.Right, Color.white);
            UiKit.Button(panel, "FECHAR [Tab]", new Vector2(0.85f, 0.915f), new Vector2(0.98f, 0.97f), Close, true, 20);

            BuildEquipment(panel);
            BuildGrid(panel);
            BuildRecipes(panel);
            BuildDetails(panel);
        }

        private void BuildEquipment(RectTransform panel)
        {
            Image column = UiKit.Panel("Equipment", panel, new Vector2(0.02f, 0.04f), new Vector2(0.27f, 0.88f), new Color(1, 1, 1, 0.03f));
            RectTransform rect = column.rectTransform;
            UiKit.Text(rect, "EQUIPAMENTO", 22, new Vector2(0.06f, 0.93f), new Vector2(0.94f, 0.99f), TextAlignmentOptions.Left, UiKit.Muted, true);
            _equipment = UiKit.Text(rect, "", 21, new Vector2(0.06f, 0.56f), new Vector2(0.94f, 0.92f), TextAlignmentOptions.TopLeft, Color.white);
            _equipment.textWrappingMode = TextWrappingModes.Normal;

            UiKit.Text(rect, "BARRA RÁPIDA", 20, new Vector2(0.06f, 0.49f), new Vector2(0.94f, 0.55f), TextAlignmentOptions.Left, UiKit.Muted, true);
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                float x = 0.06f + (i % 3) * 0.3f, y = 0.36f - (i / 3) * 0.13f;
                _quick.Add(NewSlot(rect, new Vector2(x, y), new Vector2(x + 0.27f, y + 0.12f), () => SelectQuick(index)));
            }

            UiKit.Text(rect, "ITENS DE MISSÃO", 20, new Vector2(0.06f, 0.15f), new Vector2(0.94f, 0.21f), TextAlignmentOptions.Left, UiKit.Muted, true);
            _missionItems = UiKit.Text(rect, "", 18, new Vector2(0.06f, 0.01f), new Vector2(0.94f, 0.15f), TextAlignmentOptions.TopLeft, new Color(0.6f, 0.88f, 1.0f));
            _missionItems.textWrappingMode = TextWrappingModes.Normal;
        }

        private void BuildGrid(RectTransform panel)
        {
            Image area = UiKit.Panel("Grid", panel, new Vector2(0.29f, 0.04f), new Vector2(0.66f, 0.88f), new Color(1, 1, 1, 0.03f));
            _gridArea = area.gameObject;
            RectTransform rect = area.rectTransform;
            int rows = Mathf.CeilToInt(Backpack.DefaultSlots / (float)Columns);
            float cell = 0.94f / Columns;
            for (int i = 0; i < Backpack.DefaultSlots; i++)
            {
                int index = i;
                int col = i % Columns, row = i / Columns;
                float x = 0.03f + col * cell, y = 0.9f - (row + 1) * 0.2f;
                _slots.Add(NewSlot(rect, new Vector2(x + 0.006f, y + 0.01f), new Vector2(x + cell - 0.006f, y + 0.19f), () => SelectSlot(index)));
            }

            float below = 0.9f - rows * 0.2f - 0.02f;
            UiKit.Button(rect, "ORGANIZAR", new Vector2(0.03f, below - 0.08f), new Vector2(0.33f, below), () => { Backpack.Sort(); Deselect(); }, false, 20);
            UiKit.Text(rect, "Materiais servem para fabricar na bancada.\nItens de missão não ocupam espaço.", 18,
                new Vector2(0.36f, below - 0.1f), new Vector2(0.97f, below), TextAlignmentOptions.Left, UiKit.Muted);
        }

        private void BuildRecipes(RectTransform panel)
        {
            Image area = UiKit.Panel("Recipes", panel, new Vector2(0.29f, 0.04f), new Vector2(0.66f, 0.88f), new Color(1, 1, 1, 0.03f));
            _recipeArea = area.gameObject;
            RectTransform rect = area.rectTransform;
            CraftingCatalog catalog = CraftingCatalog.Instance;
            int count = catalog != null ? catalog.Recipes.Length : 0;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                float top = 0.97f - i * 0.115f;
                Image frame = UiKit.Panel("Recipe", rect, new Vector2(0.03f, top - 0.105f), new Vector2(0.97f, top), new Color(1, 1, 1, 0.06f));
                frame.raycastTarget = true;
                var button = frame.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => SelectRecipe(index));
                var row = new RecipeRow { Button = button, Frame = frame, Recipe = catalog.Recipes[i] };
                row.Icon = UiKit.Panel("Icon", frame.rectTransform, new Vector2(0.02f, 0.1f), new Vector2(0.12f, 0.9f), Color.white);
                row.Icon.preserveAspect = true;
                row.Name = UiKit.Text(frame.rectTransform, "", 22, new Vector2(0.15f, 0.5f), new Vector2(0.98f, 0.95f), TextAlignmentOptions.Left, Color.white, true);
                row.State = UiKit.Text(frame.rectTransform, "", 17, new Vector2(0.15f, 0.05f), new Vector2(0.98f, 0.5f), TextAlignmentOptions.Left, UiKit.Muted);
                _recipes.Add(row);
            }

            if (count == 0) UiKit.Text(rect, "Nenhuma receita disponível.", 22, new Vector2(0.05f, 0.8f), new Vector2(0.95f, 0.95f), TextAlignmentOptions.Left, UiKit.Muted);
        }

        private void BuildDetails(RectTransform panel)
        {
            Image column = UiKit.Panel("Details", panel, new Vector2(0.68f, 0.04f), new Vector2(0.98f, 0.88f), new Color(1, 1, 1, 0.03f));
            RectTransform rect = column.rectTransform;
            _detailIcon = UiKit.Panel("Icon", rect, new Vector2(0.06f, 0.74f), new Vector2(0.36f, 0.96f), Color.white);
            _detailIcon.preserveAspect = true;
            _detailName = UiKit.Text(rect, "", 28, new Vector2(0.4f, 0.86f), new Vector2(0.96f, 0.96f), TextAlignmentOptions.Left, Color.white, true);
            _detailName.textWrappingMode = TextWrappingModes.Normal;
            _detailMeta = UiKit.Text(rect, "", 19, new Vector2(0.4f, 0.74f), new Vector2(0.96f, 0.86f), TextAlignmentOptions.TopLeft, UiKit.Muted);
            _detailMeta.textWrappingMode = TextWrappingModes.Normal;
            _detailText = UiKit.Text(rect, "", 20, new Vector2(0.06f, 0.4f), new Vector2(0.96f, 0.72f), TextAlignmentOptions.TopLeft, Color.white);
            _detailText.textWrappingMode = TextWrappingModes.Normal;
            _detailReason = UiKit.Text(rect, "", 19, new Vector2(0.06f, 0.33f), new Vector2(0.96f, 0.4f), TextAlignmentOptions.Left, new Color(1.0f, 0.55f, 0.45f));

            for (int i = 0; i < 4; i++)
            {
                float y = 0.25f - i * 0.075f;
                Button button = UiKit.Button(rect, "", new Vector2(0.06f, y), new Vector2(0.94f, y + 0.065f), () => { }, i == 0, 20);
                _actions.Add(button);
            }
        }

        private SlotView NewSlot(RectTransform parent, Vector2 min, Vector2 max, System.Action onClick)
        {
            Image frame = UiKit.Panel("Slot", parent, min, max, new Color(1, 1, 1, 0.07f));
            frame.raycastTarget = true;
            var button = frame.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.0f, 0.9f, 0.6f);
            colors.selectedColor = new Color(1.0f, 0.85f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            Image icon = UiKit.Panel("Icon", frame.rectTransform, new Vector2(0.14f, 0.18f), new Vector2(0.86f, 0.9f), Color.white);
            icon.preserveAspect = true;
            TextMeshProUGUI count = UiKit.Text(frame.rectTransform, "", 20, new Vector2(0.5f, 0.0f), new Vector2(0.95f, 0.3f), TextAlignmentOptions.BottomRight, Color.white, true);
            return new SlotView { Button = button, Frame = frame, Icon = icon, Count = count };
        }

        // ---------------------------------------------------------------- open / close

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open(Tab.Backpack, null);
        }

        public void OpenBackpack() => Open(Tab.Backpack, null);

        public void OpenCrafting(CraftingStation station) => Open(Tab.Crafting, station);

        private void Open(Tab tab, CraftingStation station)
        {
            if (!GameplayOverlay.TryOpen(Name)) return;
            _station = station != null ? station.Type : (CraftingStationType?)null;
            _stationName = station != null ? station.DisplayName : null;
            _panel.SetActive(true);
            Deselect();
            SetTab(tab);
            PlayUi(0.4f);
        }

        public void Close()
        {
            if (!IsOpen) return;
            GameplayOverlay.Close(Name);
            _panel.SetActive(false);
            _moveFrom = -1;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            PlayUi(0.4f);
        }

        private void SetTab(Tab tab)
        {
            _tab = tab;
            _gridArea.SetActive(tab == Tab.Backpack);
            _recipeArea.SetActive(tab == Tab.Crafting);
            _title.text = tab == Tab.Crafting && _stationName != null ? _stationName.ToUpperInvariant() : "MOCHILA";
            Deselect();
            if (tab == Tab.Crafting && _recipes.Count > 0) SelectRecipe(0);
            Refresh();
            // gamepad: start on the first thing in the centre
            GameObject first = tab == Tab.Crafting ? (_recipes.Count > 0 ? _recipes[0].Button.gameObject : null) : _slots[0].Button.gameObject;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first);
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.InventoryView.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (_input == null && InventoryManager.Player != null) _input = InventoryManager.Player.GetComponent<StarterAssetsInputs>();
            if (_input != null && _input.inventory)
            {
                _input.inventory = false;
                Toggle();
            }

            if (!IsOpen)
            {
                if (_panel.activeSelf) _panel.SetActive(false);
                return;
            }

            if (GameFlow.Instance == null || GameFlow.Instance.Current != GameFlow.State.Playing)
            {
                _panel.SetActive(false);
                return;
            }

            GameplayOverlay.SuppressGameplayInput();
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) Close();
            // the quick bar, armor and guns change outside this screen too; it's cheap to redraw
            if (_dirty || Time.frameCount % 15 == 0) Refresh();
        }

        // ---------------------------------------------------------------- selection

        private void Deselect()
        {
            _selection = Selection.None;
            _selected = -1;
            _moveFrom = -1;
            _dirty = true;
        }

        private void SelectSlot(int index)
        {
            // second click of a move: put it there
            if (_moveFrom >= 0)
            {
                int from = _moveFrom;
                _moveFrom = -1;
                Backpack.Move(from, index);
                _selection = Backpack.Slot(index) != null ? Selection.Slot : Selection.None;
                _selected = index;
                PlayUi(0.4f);
                Refresh();
                return;
            }

            _selection = Backpack.Slot(index) != null ? Selection.Slot : Selection.None;
            _selected = index;
            PlayUi(0.25f);
            Refresh();
        }

        private void SelectQuick(int index)
        {
            _moveFrom = -1;
            _selection = Selection.Quick;
            _selected = index;
            PlayUi(0.25f);
            Refresh();
        }

        private void SelectRecipe(int index)
        {
            _selection = Selection.Recipe;
            _selected = index;
            PlayUi(0.25f);
            Refresh();
        }

        private static void PlayUi(float volume)
        {
            CombatEffects fx = CombatEffects.Instance;
            if (fx != null) fx.PlayUI(fx.SwitchSound, volume);
        }

        // ---------------------------------------------------------------- drawing

        private void Refresh()
        {
            _dirty = false;
            InventoryManager quickBar = InventoryManager.Player;
            PlayerCombat player = ItemActions.Player;

            _capacity.text = $"Espaço  <b>{Backpack.UsedSlots}/{Backpack.SlotCount}</b>";
            Tint(_tabBackpack, _tab == Tab.Backpack);
            Tint(_tabCrafting, _tab == Tab.Crafting);

            for (int i = 0; i < _slots.Count; i++)
            {
                ItemStack stack = Backpack.Slot(i);
                SlotView view = _slots[i];
                bool active = i < Backpack.SlotCount;
                view.Button.gameObject.SetActive(active);
                ShowStack(view, stack != null ? stack.Id : null, stack != null ? stack.Count : 0, false);
                bool selected = (_selection == Selection.Slot && _selected == i) || _moveFrom == i;
                view.Frame.color = _moveFrom == i ? new Color(1.0f, 0.78f, 0.3f, 0.35f) : selected ? new Color(1.0f, 1.0f, 1.0f, 0.22f) : new Color(1, 1, 1, 0.07f);
            }

            for (int i = 0; i < _quick.Count; i++)
            {
                var item = (QuickItem)i;
                int count = quickBar != null ? quickBar.Count(item) : 0;
                ShowStack(_quick[i], ItemCatalog.IdOf(item), count, true);
                bool chosen = quickBar != null && quickBar.Selected == item;
                _quick[i].Frame.color = _selection == Selection.Quick && _selected == i ? new Color(1, 1, 1, 0.22f) : chosen ? new Color(1.0f, 0.78f, 0.3f, 0.18f) : new Color(1, 1, 1, 0.07f);
                _quick[i].Count.text = quickBar != null ? $"{count}/{quickBar.CapacityOf(item)}" : "";
            }

            _equipment.text = EquipmentText(player);
            _missionItems.text = MissionItemsText();

            foreach (RecipeRow row in _recipes)
            {
                CraftingRecipe recipe = row.Recipe;
                string reason = Crafting.CannotCraft(recipe, _station);
                row.Icon.sprite = ItemCatalog.IconOf(recipe.ProductId);
                row.Name.text = recipe.DisplayName + (recipe.ProductAmount > 1 ? $" ×{recipe.ProductAmount}" : "");
                row.State.text = reason == null ? "<color=#66FF88>Pode fabricar</color>" : reason;
                row.State.text += "   <color=#FFFFFF80>· " + (recipe.Station == CraftingStationType.Anywhere ? "na mochila" : Crafting.StationName(recipe.Station)) + "</color>";
                bool selected = _selection == Selection.Recipe && _recipes.IndexOf(row) == _selected;
                row.Frame.color = selected ? new Color(1, 1, 1, 0.2f) : new Color(1, 1, 1, reason == null ? 0.08f : 0.04f);
                row.Icon.color = reason == null ? Color.white : new Color(1, 1, 1, 0.45f);
            }

            ShowDetails(quickBar);
        }

        private static void Tint(Button button, bool on)
        {
            var image = button.GetComponent<Image>();
            image.color = on ? new Color(1.0f, 0.78f, 0.3f, 0.35f) : new Color(1, 1, 1, 0.1f);
        }

        private static void ShowStack(SlotView view, string id, int count, bool alwaysIcon)
        {
            bool has = !string.IsNullOrEmpty(id) && (count > 0 || alwaysIcon);
            view.Icon.enabled = has;
            if (has)
            {
                view.Icon.sprite = ItemCatalog.IconOf(id);
                ItemDefinition item = ItemCatalog.Find(id);
                Color tint = item != null ? PickupFeed.TintOf(item.Category) : Color.white;
                view.Icon.color = count > 0 ? tint : new Color(tint.r, tint.g, tint.b, 0.25f);
            }

            view.Count.text = !alwaysIcon && count > 1 ? count.ToString() : "";
        }

        private static string EquipmentText(PlayerCombat player)
        {
            if (player == null) return "";
            string Gun(WeaponInstance weapon, string slot)
            {
                if (weapon == null) return $"<color=#FFFFFF80>{slot}:</color> vazio";
                var fitted = WeaponUpgrades.On(weapon.Data);
                var names = new List<string>();
                if (fitted != null) foreach (WeaponUpgrade upgrade in fitted) names.Add(upgrade.DisplayName);
                string extra = names.Count > 0 ? $"\n   <color=#FFC74D>{string.Join(", ", names)}</color>" : "";
                return $"<color=#FFFFFF80>{slot}:</color> <b>{weapon.Data.DisplayName}</b>{extra}\n   {weapon.Magazine}/{weapon.Data.MagazineSize} · reserva {weapon.Reserve} · {weapon.Data.AmmoName}";
            }

            return Gun(player.Current, "Em mãos") + "\n" + Gun(player.Other, "Nas costas") + "\n" +
                   $"<color=#FFFFFF80>Colete:</color> {(player.Health.Armor > 0.5f ? $"{Mathf.RoundToInt(player.Health.Armor)}/60" : "sem proteção")}\n" +
                   $"<color=#FFFFFF80>Vida:</color> {Mathf.RoundToInt(player.Health.Current)}/{Mathf.RoundToInt(player.Health.MaxHealth)}";
        }

        private static string MissionItemsText()
        {
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> item in RunState.AllItems)
            {
                if (item.Value <= 0) continue;
                parts.Add(item.Value > 1 ? $"{ItemCatalog.NameOf(item.Key)} ×{item.Value}" : ItemCatalog.NameOf(item.Key));
            }

            return parts.Count > 0 ? string.Join(" · ", parts) : "<color=#FFFFFF60>Nenhum</color>";
        }

        // ---------------------------------------------------------------- details and actions

        private void ShowDetails(InventoryManager quickBar)
        {
            foreach (Button action in _actions)
            {
                action.gameObject.SetActive(false);
                action.onClick.RemoveAllListeners();
            }

            _detailReason.text = "";
            string id = null;
            int count = 0;
            if (_selection == Selection.Slot && Backpack.Slot(_selected) != null)
            {
                ItemStack stack = Backpack.Slot(_selected);
                id = stack.Id;
                count = stack.Count;
                SlotActions(stack, quickBar);
            }
            else if (_selection == Selection.Quick && quickBar != null)
            {
                var item = (QuickItem)_selected;
                id = ItemCatalog.IdOf(item);
                count = quickBar.Count(item);
                QuickActions(item, quickBar);
            }
            else if (_selection == Selection.Recipe && _selected >= 0 && _selected < _recipes.Count)
            {
                ShowRecipe(_recipes[_selected].Recipe);
                return;
            }

            ItemDefinition definition = ItemCatalog.Find(id);
            _detailIcon.enabled = id != null;
            if (id == null)
            {
                _detailName.text = _tab == Tab.Crafting ? "" : "Selecione um item";
                _detailMeta.text = "";
                _detailText.text = _moveFrom >= 0 ? "Escolha o espaço de destino." : "";
                return;
            }

            _detailIcon.sprite = ItemCatalog.IconOf(id);
            _detailIcon.color = definition != null ? PickupFeed.TintOf(definition.Category) : Color.white;
            _detailName.text = ItemCatalog.NameOf(id);
            _detailMeta.text = $"{CategoryName(definition)}\nQuantidade: {count}" + (definition != null && definition.MaxStack > 1 ? $"  (pilha de {definition.MaxStack})" : "");
            _detailText.text = (definition != null ? definition.Description : "") + UsedIn(id);
        }

        private void SlotActions(ItemStack stack, InventoryManager quickBar)
        {
            int slot = _selected;
            string cannot = ItemActions.CannotUse(stack.Id);
            ItemDefinition definition = stack.Definition;
            int button = 0;
            if (cannot == null) Action(button++, "USAR", () => ItemActions.Use(slot));
            else _detailReason.text = cannot;

            if (Backpack.TryQuickItem(stack.Id, out QuickItem quick) && quickBar != null)
            {
                bool room = quickBar.Count(quick) < quickBar.CapacityOf(quick);
                if (room) Action(button++, "MOVER PARA A BARRA", () => quickBar.MoveFromBackpack(quick));
                else if (cannot == "Use pela barra rápida") _detailReason.text = "Barra rápida cheia para este item";
            }

            if (stack.Count > 1 && Backpack.UsedSlots < Backpack.SlotCount) Action(button++, "SEPARAR PILHA", () => Backpack.Split(slot));
            Action(button++, "MOVER", () => { _moveFrom = slot; Refresh(); });
            if (definition == null || definition.Droppable)
            {
                if (button < _actions.Count) Action(button++, stack.Count > 1 ? "DESCARTAR 1" : "DESCARTAR", () => { ItemActions.Drop(slot, 1); if (Backpack.Slot(slot) == null) Deselect(); });
            }
        }

        private void QuickActions(QuickItem item, InventoryManager quickBar)
        {
            int button = 0;
            if (quickBar.Selected != item) Action(button++, "SELECIONAR NA BARRA", () => quickBar.Select(item));
            if (quickBar.Count(item) > 0)
            {
                if (Backpack.RoomFor(ItemCatalog.IdOf(item)) > 0) Action(button++, "GUARDAR 1 NA MOCHILA", () => quickBar.MoveToBackpack(item));
                else _detailReason.text = "Mochila cheia";
            }

            int spare = Backpack.Count(ItemCatalog.IdOf(item));
            if (spare > 0 && quickBar.Count(item) < quickBar.CapacityOf(item)) Action(button, $"PEGAR DA MOCHILA ({spare})", () => quickBar.MoveFromBackpack(item));
        }

        private void ShowRecipe(CraftingRecipe recipe)
        {
            string product = recipe.ProductId;
            ItemDefinition definition = ItemCatalog.Find(product);
            _detailIcon.enabled = true;
            _detailIcon.sprite = ItemCatalog.IconOf(product);
            _detailIcon.color = definition != null ? PickupFeed.TintOf(definition.Category) : Color.white;
            _detailName.text = recipe.DisplayName;
            string where = recipe.Station == CraftingStationType.Anywhere ? "Pode ser feito na mochila" : $"Requer {Crafting.StationName(recipe.Station)}";
            _detailMeta.text = $"Produz {recipe.ProductAmount}× {ItemCatalog.NameOf(product)}\n{where}";

            var text = new System.Text.StringBuilder();
            text.AppendLine(recipe.Description);
            text.AppendLine();
            text.AppendLine("<color=#FFC74D><b>MATERIAIS</b></color>");
            foreach (CraftingRecipe.Ingredient ingredient in recipe.Ingredients)
            {
                int have = Backpack.Count(ingredient.ItemId);
                string color = have >= ingredient.Count ? "#66FF88" : "#FF6655";
                text.AppendLine($"<color={color}>{have}/{ingredient.Count}</color>  {ItemCatalog.NameOf(ingredient.ItemId)}");
            }

            _detailText.text = text.ToString();
            string reason = Crafting.CannotCraft(recipe, _station);
            _detailReason.text = reason ?? "";
            if (reason == null)
            {
                Action(0, "FABRICAR", () =>
                {
                    if (!Crafting.Craft(recipe, _station)) return;
                    CombatEffects fx = CombatEffects.Instance;
                    if (fx != null) fx.PlayUI(fx.PickupSound, 0.6f);
                    ItemActions.Player?.ShowNotice($"Fabricado: {recipe.DisplayName}");
                });
            }
        }

        private void Action(int index, string label, System.Action onClick)
        {
            if (index >= _actions.Count) return;
            Button button = _actions[index];
            button.gameObject.SetActive(true);
            UiKit.SetButtonText(button, label);
            button.onClick.AddListener(() =>
            {
                onClick();
                PlayUi(0.4f);
                Refresh();
            });
        }

        private static string CategoryName(ItemDefinition item)
        {
            if (item == null) return "Item";
            switch (item.Category)
            {
                case ItemCategory.Medical: return "Medicina";
                case ItemCategory.Tactical: return "Tático";
                case ItemCategory.Construction: return "Construção";
                case ItemCategory.Material: return "Material de fabricação";
                case ItemCategory.Ammo: return "Munição";
                case ItemCategory.Mission: return "Missão";
                case ItemCategory.Document: return "Documento";
                default: return "Item";
            }
        }

        // a material's description ends with what it's for
        private static string UsedIn(string id)
        {
            CraftingCatalog catalog = CraftingCatalog.Instance;
            if (catalog == null) return "";
            var uses = new List<string>();
            foreach (CraftingRecipe recipe in catalog.Recipes)
            {
                foreach (CraftingRecipe.Ingredient ingredient in recipe.Ingredients)
                {
                    if (ingredient.ItemId == id && !uses.Contains(recipe.DisplayName)) uses.Add(recipe.DisplayName);
                }
            }

            return uses.Count > 0 ? $"\n\n<color=#FFFFFF99>Usado em: {string.Join(", ", uses)}</color>" : "";
        }
    }
}
