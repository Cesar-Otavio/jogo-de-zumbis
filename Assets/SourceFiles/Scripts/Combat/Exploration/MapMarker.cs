using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public enum MapMarkerKind
    {
        PointOfInterest,
        Exit,
        Extraction,
        Workbench,
        SafeArea,
        Supplies,
        // a co-op teammate: always known, moves with them
        Ally
    }

    /* Something worth a mark on the map: places of the story, gates, the helipad, benches. It is
     * only drawn once that part of the map is known (MapDiscovery), unless AlwaysKnown. Enemies
     * are never markers: the map doesn't do the player's listening for them.
     */
    public class MapMarker : MonoBehaviour
    {
        public static readonly List<MapMarker> All = new List<MapMarker>();

        // the player's own pins, placed on the tactical map
        public static readonly List<Vector3> Custom = new List<Vector3>();
        public const int MaxCustom = 5;

        public string Id;
        public MapMarkerKind Kind;
        public string Label;
        public bool AlwaysKnown;

        public Vector3 Position => transform.position;
        public bool Known => AlwaysKnown || MapDiscovery.IsRevealed(transform.position);

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public static MapMarker Add(GameObject on, string id, MapMarkerKind kind, string label)
        {
            var marker = on.GetComponent<MapMarker>();
            if (marker == null) marker = on.AddComponent<MapMarker>();
            marker.Id = id;
            marker.Kind = kind;
            marker.Label = label;
            return marker;
        }

        // the places missions refer to become markers; their kind follows the id
        public static void FromMissionAnchors()
        {
            foreach (MissionAnchor anchor in Object.FindObjectsByType<MissionAnchor>())
            {
                if (string.IsNullOrEmpty(anchor.Id) || anchor.GetComponent<MapMarker>() != null) continue;
                MapMarkerKind kind = anchor.Id.StartsWith("gate") ? MapMarkerKind.Exit
                    : anchor.Id == "helipad" ? MapMarkerKind.Extraction
                    : anchor.Id == "depot" ? MapMarkerKind.Supplies
                    : MapMarkerKind.PointOfInterest;
                Add(anchor.gameObject, anchor.Id, kind, anchor.Label);
            }
        }

        public static Color ColorOf(MapMarkerKind kind)
        {
            switch (kind)
            {
                case MapMarkerKind.Exit: return new Color(0.55f, 0.9f, 0.55f);
                case MapMarkerKind.Extraction: return new Color(0.45f, 0.85f, 1.0f);
                case MapMarkerKind.Workbench: return new Color(1.0f, 0.78f, 0.3f);
                case MapMarkerKind.SafeArea: return new Color(0.6f, 1.0f, 0.75f);
                case MapMarkerKind.Supplies: return new Color(1.0f, 0.9f, 0.6f);
                case MapMarkerKind.Ally: return new Color(0.35f, 0.8f, 1.0f);
                default: return new Color(0.88f, 0.88f, 0.9f);
            }
        }

        public static string KindName(MapMarkerKind kind)
        {
            switch (kind)
            {
                case MapMarkerKind.Exit: return "Portão";
                case MapMarkerKind.Extraction: return "Extração";
                case MapMarkerKind.Workbench: return "Bancada";
                case MapMarkerKind.SafeArea: return "Área segura";
                case MapMarkerKind.Supplies: return "Suprimentos";
                case MapMarkerKind.Ally: return "Aliado";
                default: return "Local";
            }
        }
    }
}
