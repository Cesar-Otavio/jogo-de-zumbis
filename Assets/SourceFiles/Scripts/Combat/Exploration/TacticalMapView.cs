using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Combat
{
    /* The full map (M): the whole base with the fog of what hasn't been seen, the places found and
     * their names, the player, the current objective and the player's own pins (left click puts
     * one, right click removes the nearest). The world keeps going, slowed down (GameplayOverlay).
     */
    public class TacticalMapView : MonoBehaviour, IPointerClickHandler
    {
        public const string Name = "map";
        public static TacticalMapView Instance { get; private set; }
        public static bool IsOpen => GameplayOverlay.Open == Name;

        private GameObject _panel;
        private RectTransform _mapRect;
        private RawImage _map;
        private RawImage _fog;
        private Image _player;
        private Image _objective;
        private TextMeshProUGUI _objectiveLabel;
        private TextMeshProUGUI _legend;
        private readonly Dictionary<MapMarker, (Image dot, TextMeshProUGUI label)> _markers = new Dictionary<MapMarker, (Image, TextMeshProUGUI)>();
        private readonly List<Image> _pins = new List<Image>();
        private StarterAssetsInputs _input;

        // fits under the title in the 1080 reference height
        private const float MapSize = 820.0f;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            Build();
            _panel.SetActive(false);
        }

        private void Build()
        {
            RectTransform root = UiKit.Canvas("TacticalMap", transform, 125);
            _panel = UiKit.Panel("MapPanel", root, Vector2.zero, Vector2.one, new Color(0.02f, 0.025f, 0.03f, 1.0f)).gameObject;
            var panel = (RectTransform)_panel.transform;

            UiKit.Text(panel, "MAPA TÁTICO", 40, new Vector2(0.05f, 0.9f), new Vector2(0.5f, 0.97f), TextAlignmentOptions.Left, UiKit.Accent, true);
            UiKit.Text(panel, "Base Militar — setor principal", 22, new Vector2(0.05f, 0.865f), new Vector2(0.5f, 0.905f), TextAlignmentOptions.Left, UiKit.Muted);

            // the map, square, on the left; clicks on it place pins
            Image frame = UiKit.Box("Frame", panel, new Vector2(0.0f, 0.5f), new Vector2(96.0f + MapSize * 0.5f, -55.0f), Vector2.one * (MapSize + 6.0f), new Color(1.0f, 0.78f, 0.3f, 0.25f));
            _map = NewRaw("Map", frame.rectTransform, MapSize);
            _map.raycastTarget = true;
            _mapRect = _map.rectTransform;
            _fog = NewRaw("Fog", _mapRect, MapSize);
            // the click handler is this component, so the map forwards its clicks here
            _map.gameObject.AddComponent<ClickRelay>().Target = this;

            _objective = Dot(_mapRect, new Color(1.0f, 0.78f, 0.3f), 22.0f, null);
            _objective.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            _objectiveLabel = Label(_mapRect, new Color(1.0f, 0.85f, 0.5f));
            _player = Dot(_mapRect, Color.white, 28.0f, MapSprites.Arrow);

            // legend and help on the right
            _legend = UiKit.Text(panel, "", 24, new Vector2(0.56f, 0.2f), new Vector2(0.95f, 0.86f), TextAlignmentOptions.TopLeft, Color.white);
            _legend.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Text(panel, "<color=#FFC74D>Clique</color> marcar   ·   <color=#FFC74D>Botão direito</color> remover marca   ·   <color=#FFC74D>[M]</color> ou <color=#FFC74D>[Esc]</color> fechar",
                20, new Vector2(0.56f, 0.06f), new Vector2(0.95f, 0.12f), TextAlignmentOptions.Left, UiKit.Muted);
            UiKit.Button(panel, "FECHAR [M]", new Vector2(0.78f, 0.13f), new Vector2(0.95f, 0.19f), Close, true, 22);
        }

        private static RawImage NewRaw(string name, RectTransform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * size;
            var raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false;
            return raw;
        }

        private static Image Dot(RectTransform parent, Color color, float size, Sprite sprite)
        {
            Image image = UiKit.Box("Dot", parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size, color);
            image.sprite = sprite;
            image.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            return image;
        }

        private static TextMeshProUGUI Label(RectTransform parent, Color color)
        {
            TextMeshProUGUI label = UiKit.Text(parent, "", 17, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TextAlignmentOptions.Left, color, true);
            label.rectTransform.pivot = new Vector2(0.0f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(260.0f, 24.0f);
            return label;
        }

        // ---------------------------------------------------------------- open / close

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            MapDefinition map = MapDefinition.Instance;
            if (map == null || map.Map == null)
            {
                ItemActions.Player?.ShowNotice("Mapa indisponível");
                return;
            }

            if (!GameplayOverlay.TryOpen(Name)) return;
            _panel.SetActive(true);
            PlayUi();
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            GameplayOverlay.Close(Name);
            _panel.SetActive(false);
            PlayUi();
        }

        private static void PlayUi()
        {
            CombatEffects fx = CombatEffects.Instance;
            if (fx != null) fx.PlayUI(fx.SwitchSound, 0.35f);
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.TacticalMapView.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (_input == null && InventoryManager.Player != null) _input = InventoryManager.Player.GetComponent<StarterAssetsInputs>();
            if (_input != null && _input.map)
            {
                _input.map = false;
                Toggle();
            }

            if (!IsOpen) return;
            // closed from outside (pause, death, a cinematic)
            if (GameplayOverlay.Open != Name || GameFlow.Instance == null || GameFlow.Instance.Current != GameFlow.State.Playing)
            {
                _panel.SetActive(false);
                return;
            }

            GameplayOverlay.SuppressGameplayInput();
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) Close();
            Refresh();
        }

        // ---------------------------------------------------------------- drawing

        private Vector2 ToPanel(MapDefinition map, Vector3 world)
        {
            Vector2 uv = map.ToMap(world);
            return (uv - new Vector2(0.5f, 0.5f)) * MapSize;
        }

        private void Refresh()
        {
            MapDefinition map = MapDefinition.Instance;
            if (map == null) return;
            _map.texture = map.Map;
            _fog.texture = MapDiscovery.Fog;

            Transform player = InventoryManager.Player != null ? InventoryManager.Player.transform : null;
            _player.gameObject.SetActive(player != null);
            if (player != null)
            {
                _player.rectTransform.anchoredPosition = ToPanel(map, player.position);
                _player.rectTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, -player.eulerAngles.y);
            }

            int known = 0, total = 0;
            var found = new List<string>();
            foreach (var pair in _markers)
            {
                if (pair.Key != null && MapMarker.All.Contains(pair.Key)) continue;
                pair.Value.dot.gameObject.SetActive(false);
                pair.Value.label.gameObject.SetActive(false);
            }

            foreach (MapMarker marker in MapMarker.All)
            {
                if (!_markers.TryGetValue(marker, out var view))
                {
                    Image dot = Dot(_mapRect, MapMarker.ColorOf(marker.Kind), 14.0f, marker.Kind == MapMarkerKind.Workbench && ItemCatalog.Instance != null ? ItemCatalog.Instance.WorkbenchIcon : MapSprites.Circle);
                    view = (dot, Label(_mapRect, MapMarker.ColorOf(marker.Kind)));
                    _markers[marker] = view;
                }

                bool ally = marker.Kind == MapMarkerKind.Ally;
                // teammates are drawn but aren't places: they don't count towards what was explored
                if (!ally) total++;
                bool visible = marker.Known;
                view.dot.gameObject.SetActive(visible);
                // benches show by their icon (and in the list): their names would sit on the place they're in
                view.label.gameObject.SetActive(visible && marker.Kind != MapMarkerKind.Workbench);
                if (!visible) continue;
                if (!ally) known++;
                if (!ally) found.Add($"<color=#{ColorUtility.ToHtmlStringRGB(MapMarker.ColorOf(marker.Kind))}>•</color> {marker.Label}  <color=#FFFFFF80>{MapMarker.KindName(marker.Kind)}</color>");
                Vector2 at = ToPanel(map, marker.Position);
                view.dot.rectTransform.anchoredPosition = at;
                view.dot.rectTransform.sizeDelta = Vector2.one * (marker.Kind == MapMarkerKind.Workbench ? 26.0f : 14.0f);
                view.label.rectTransform.anchoredPosition = at + new Vector2(14.0f, 0.0f);
                view.label.text = marker.Label;
            }

            while (_pins.Count < MapMarker.Custom.Count) _pins.Add(Dot(_mapRect, new Color(1.0f, 0.35f, 0.3f), 16.0f, null));
            for (int i = 0; i < _pins.Count; i++)
            {
                bool used = i < MapMarker.Custom.Count;
                _pins[i].gameObject.SetActive(used);
                if (!used) continue;
                _pins[i].rectTransform.anchoredPosition = ToPanel(map, MapMarker.Custom[i]);
                _pins[i].rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            }

            string objectiveText = "Nenhum objetivo marcado";
            Vector3 target = Vector3.zero;
            string label = null;
            bool hasObjective = MissionManager.Instance != null && MissionManager.Instance.TryGetMarker(out target, out label);
            _objective.gameObject.SetActive(hasObjective);
            _objectiveLabel.gameObject.SetActive(hasObjective);
            if (hasObjective)
            {
                Vector2 at = ToPanel(map, target);
                _objective.rectTransform.anchoredPosition = at;
                _objectiveLabel.rectTransform.anchoredPosition = at + new Vector2(18.0f, 0.0f);
                _objectiveLabel.text = label;
                float distance = player != null ? Vector2.Distance(new Vector2(target.x, target.z), new Vector2(player.position.x, player.position.z)) : 0.0f;
                objectiveText = $"{label}  <color=#FFFFFF99>· {Mathf.RoundToInt(distance)} m</color>";
            }

            _legend.text =
                $"<color=#FFC74D><b>OBJETIVO</b></color>\n{objectiveText}\n\n" +
                $"<color=#FFC74D><b>LOCAIS CONHECIDOS</b></color>  {known}/{total}\n{(found.Count > 0 ? string.Join("\n", found) : "<color=#FFFFFF80>Explore para descobrir a base.</color>")}\n\n" +
                $"<color=#FFC74D><b>EXPLORADO</b></color>  {Mathf.RoundToInt(MapDiscovery.RevealedFraction * 100.0f)}%\n" +
                $"<color=#FFC74D><b>MARCAS</b></color>  {MapMarker.Custom.Count}/{MapMarker.MaxCustom}";
        }

        // ---------------------------------------------------------------- pins

        public void OnPointerClick(PointerEventData eventData)
        {
            MapDefinition map = MapDefinition.Instance;
            if (!IsOpen || map == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_mapRect, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
            Vector3 world = map.ToWorld(local / MapSize + new Vector2(0.5f, 0.5f));

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                int nearest = -1;
                float best = Mathf.Max(6.0f, map.Size.x * 0.03f);
                for (int i = 0; i < MapMarker.Custom.Count; i++)
                {
                    float d = Vector2.Distance(new Vector2(MapMarker.Custom[i].x, MapMarker.Custom[i].z), new Vector2(world.x, world.z));
                    if (d < best)
                    {
                        best = d;
                        nearest = i;
                    }
                }

                if (nearest >= 0) MapMarker.Custom.RemoveAt(nearest);
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                // the oldest pin makes room for a new one
                if (MapMarker.Custom.Count >= MapMarker.MaxCustom) MapMarker.Custom.RemoveAt(0);
                MapMarker.Custom.Add(world);
            }

            PlayUi();
            Refresh();
        }

        // forwards the map image's clicks to the view (the view itself isn't a graphic)
        private class ClickRelay : MonoBehaviour, IPointerClickHandler
        {
            public TacticalMapView Target;
            public void OnPointerClick(PointerEventData eventData) => Target.OnPointerClick(eventData);
        }
    }
}
