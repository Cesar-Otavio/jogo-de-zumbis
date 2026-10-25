using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Combat
{
    /* The round minimap under the score card: the baked base map moved under the player (no second
     * camera rendering the level), the fog of what hasn't been seen, known places, benches, the
     * player's pins and the current objective (pinned to the rim when it's far). It also does the
     * revealing as the player walks, and tells Duarte when a place is found for the first time.
     */
    public class MinimapView : MonoBehaviour
    {
        public static MinimapView Instance { get; private set; }

        public const float Diameter = 260.0f;
        public static readonly float[] ZoomRadius = { 32.0f, 55.0f, 85.0f };

        [Tooltip("Metres around the player that become known")]
        public float RevealRadius = 22.0f;

        private GameObject _canvas;
        private RectTransform _content;
        private RectTransform _mapRect;
        private RawImage _map;
        private RawImage _fog;
        private Image _player;
        private Image _objective;
        private TextMeshProUGUI _north;
        private TextMeshProUGUI _objectiveText;
        private readonly Dictionary<MapMarker, Image> _markers = new Dictionary<MapMarker, Image>();
        private readonly List<Image> _pins = new List<Image>();
        private readonly HashSet<MapMarker> _known = new HashSet<MapMarker>();
        private CombatHUD _hud;
        private Transform _playerTransform;
        private float _nextReveal;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            MapDiscovery.SaveIfNeeded(true);
        }

        private void OnApplicationQuit() => MapDiscovery.SaveIfNeeded(true);

        private void Start()
        {
            MapMarker.FromMissionAnchors();
            _hud = FindAnyObjectByType<CombatHUD>();
            Build();
            // what's already known when the game starts doesn't need announcing
            foreach (MapMarker marker in MapMarker.All) if (marker.Known) _known.Add(marker);
        }

        private void Build()
        {
            RectTransform root = UiKit.Canvas("Minimap", transform, 55);
            _canvas = root.gameObject;

            // a dark disc with a thin rim; the map inside is clipped to it
            Image rim = UiKit.Box("Rim", root, Vector2.one, new Vector2(-190.0f, -322.0f), Vector2.one * (Diameter + 8.0f), new Color(1.0f, 0.78f, 0.3f, 0.35f));
            rim.sprite = MapSprites.Circle;
            Image disc = UiKit.Box("Disc", rim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * Diameter, new Color(0.03f, 0.035f, 0.045f, 0.92f));
            disc.sprite = MapSprites.Circle;
            disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            _content.SetParent(disc.rectTransform, false);
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = Vector2.zero;

            _map = NewRaw("Map", _content);
            _mapRect = _map.rectTransform;
            _fog = NewRaw("Fog", _mapRect);
            _fog.rectTransform.anchorMin = Vector2.zero;
            _fog.rectTransform.anchorMax = Vector2.one;
            _fog.rectTransform.offsetMin = _fog.rectTransform.offsetMax = Vector2.zero;

            _objective = Marker(_content, new Color(1.0f, 0.78f, 0.3f), 18.0f, null);
            _objective.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            _player = UiKit.Box("Player", disc.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22.0f, 22.0f), Color.white);
            _player.sprite = MapSprites.Arrow;

            // a soft ring over the clipped edge (the mask itself is hard)
            Image edge = UiKit.Box("Edge", rim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (Diameter + 8.0f), new Color(1.0f, 0.78f, 0.3f, 0.55f));
            edge.sprite = MapSprites.Rim;

            _north = UiKit.Text(disc.rectTransform, "N", 20, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center, new Color(1.0f, 0.78f, 0.3f), true);
            _north.rectTransform.sizeDelta = new Vector2(30.0f, 30.0f);

            _objectiveText = UiKit.Text(root, "", 18, Vector2.one, Vector2.one, TextAlignmentOptions.Center, UiKit.Muted);
            _objectiveText.rectTransform.sizeDelta = new Vector2(320.0f, 28.0f);
            _objectiveText.rectTransform.anchoredPosition = new Vector2(-190.0f, -322.0f - Diameter * 0.5f - 22.0f);
        }

        private static RawImage NewRaw(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            var raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false;
            return raw;
        }

        private static Image Marker(RectTransform parent, Color color, float size, Sprite sprite)
        {
            Image image = UiKit.Box("Marker", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), color);
            image.sprite = sprite;
            image.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.0f, -1.0f);
            return image;
        }

        private void LateUpdate()
        {
            MapDefinition map = MapDefinition.Instance;
            if (_playerTransform == null && InventoryManager.Player != null) _playerTransform = InventoryManager.Player.transform;

            bool playing = GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Playing;
            if (playing && _playerTransform != null) Discover();

            bool show = map != null && map.Map != null && _playerTransform != null && playing && GameSettings.Minimap
                        && (_hud == null || _hud.Visible) && !CinematicPlayer.Playing && GameplayOverlay.Open != TacticalMapView.Name;
            if (_canvas.activeSelf != show) _canvas.SetActive(show);
            if (!show) return;

            Draw(map);
        }

        // reveal around the player a few times a second, and greet newly found places
        private void Discover()
        {
            if (Time.unscaledTime < _nextReveal) return;
            _nextReveal = Time.unscaledTime + 0.25f;
            MapDiscovery.Reveal(_playerTransform.position, RevealRadius);
            MapDiscovery.SaveIfNeeded();

            foreach (MapMarker marker in MapMarker.All)
            {
                if (_known.Contains(marker) || !marker.Known) continue;
                _known.Add(marker);
                PickupFeed.Report("place:" + marker.Id, null, $"• {marker.Label}", 0, MapMarker.ColorOf(marker.Kind));
                if (DialogueDirector.Instance != null && !string.IsNullOrEmpty(marker.Id)) DialogueDirector.Instance.Play("poi_" + marker.Id);
            }
        }

        private void Draw(MapDefinition map)
        {
            float radius = ZoomRadius[GameSettings.MinimapZoom];
            float ppm = Diameter * 0.5f / radius;
            Vector3 position = _playerTransform.position;

            Transform cam = Camera.main != null ? Camera.main.transform : _playerTransform;
            float heading = Mathf.Atan2(cam.forward.x, cam.forward.z) * Mathf.Rad2Deg;
            float body = _playerTransform.eulerAngles.y;
            bool rotates = GameSettings.MinimapRotates;
            float turn = rotates ? heading : 0.0f;
            _content.localRotation = Quaternion.Euler(0.0f, 0.0f, turn);
            _player.rectTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, turn - body);
            Quaternion upright = Quaternion.Euler(0.0f, 0.0f, -turn);

            _map.texture = map.Map;
            _fog.texture = MapDiscovery.Fog;
            _mapRect.sizeDelta = map.Size * ppm;
            _mapRect.anchoredPosition = -new Vector2(position.x - map.Min.x, position.z - map.Min.y) * ppm;

            // north on the rim
            float a = turn * Mathf.Deg2Rad;
            _north.rectTransform.anchoredPosition = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * (Diameter * 0.5f - 16.0f);
            _north.gameObject.SetActive(rotates);

            float rim = Diameter * 0.5f - 12.0f;
            // a marker that went away (a teammate who left) takes its dot with it
            foreach (var pair in _markers)
                if (pair.Key == null || !MapMarker.All.Contains(pair.Key)) pair.Value.gameObject.SetActive(false);

            foreach (MapMarker marker in MapMarker.All)
            {
                if (!_markers.TryGetValue(marker, out Image image))
                {
                    image = Marker(_content, MapMarker.ColorOf(marker.Kind), 12.0f, marker.Kind == MapMarkerKind.Workbench ? null : MapSprites.Circle);
                    if (marker.Kind == MapMarkerKind.Workbench && ItemCatalog.Instance != null) image.sprite = ItemCatalog.Instance.WorkbenchIcon;
                    _markers[marker] = image;
                }

                Vector2 offset = Flat(marker.Position - position) * ppm;
                // teammates stay on the rim when they're far, like pins: you can always tell which way they are
                if (marker.Kind == MapMarkerKind.Ally) offset = Vector2.ClampMagnitude(offset, rim);
                bool visible = marker.Known && offset.magnitude <= rim + 0.01f;
                image.gameObject.SetActive(visible);
                if (!visible) continue;
                image.rectTransform.anchoredPosition = offset;
                image.rectTransform.localRotation = upright;
                image.rectTransform.sizeDelta = Vector2.one * (marker.Kind == MapMarkerKind.Workbench ? 20.0f : 11.0f);
            }

            // pins and the objective stay on the rim when they're beyond it
            while (_pins.Count < MapMarker.Custom.Count) _pins.Add(Marker(_content, new Color(1.0f, 0.35f, 0.3f), 12.0f, null));
            for (int i = 0; i < _pins.Count; i++)
            {
                bool used = i < MapMarker.Custom.Count;
                _pins[i].gameObject.SetActive(used);
                if (!used) continue;
                _pins[i].rectTransform.anchoredPosition = Vector2.ClampMagnitude(Flat(MapMarker.Custom[i] - position) * ppm, rim);
                _pins[i].rectTransform.localRotation = upright * Quaternion.Euler(0, 0, 45);
            }

            Vector3 target = Vector3.zero;
            string label = null;
            bool hasObjective = GameSettings.ObjectiveHints && MissionManager.Instance != null && MissionManager.Instance.TryGetMarker(out target, out label);
            _objective.gameObject.SetActive(hasObjective);
            if (hasObjective)
            {
                Vector2 offset = Flat(target - position) * ppm;
                _objective.rectTransform.anchoredPosition = Vector2.ClampMagnitude(offset, rim);
                _objective.rectTransform.localRotation = upright * Quaternion.Euler(0, 0, 45);
                float pulse = 1.0f + Mathf.Sin(Time.unscaledTime * 5.0f) * 0.12f;
                _objective.rectTransform.localScale = Vector3.one * pulse;
                float distance = Vector3.Distance(Flat3(target), Flat3(position));
                _objectiveText.text = $"<color=#FFC74D>•</color> {label}  ·  {Mathf.RoundToInt(distance)} m";
            }
            else
            {
                _objectiveText.text = "";
            }
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
        private static Vector3 Flat3(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }
}
