using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    // A named place in the level that missions refer to: a zone, a building, a gate...
    public class MissionAnchor : MonoBehaviour
    {
        public string Id;
        [Tooltip("Shown next to the objective marker")]
        public string Label;

        private static readonly Dictionary<string, MissionAnchor> Registry = new Dictionary<string, MissionAnchor>();

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(Id)) Registry[Id] = this;
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(Id) && Registry.TryGetValue(Id, out MissionAnchor anchor) && anchor == this) Registry.Remove(Id);
        }

        public static MissionAnchor Find(string id)
        {
            return id != null && Registry.TryGetValue(id, out MissionAnchor anchor) ? anchor : null;
        }

        // the position of anything with this id: an anchor, a device or a pickup
        public static bool TryLocate(string id, out Vector3 position)
        {
            MissionAnchor anchor = Find(id);
            if (anchor != null)
            {
                position = anchor.transform.position;
                return true;
            }

            MissionDevice device = MissionDevice.Find(id);
            if (device != null && device.isActiveAndEnabled)
            {
                position = device.transform.position;
                return true;
            }

            // the nearest pickup still lying around
            foreach (MissionPickup pickup in MissionPickup.All)
            {
                if (pickup.ItemId == id && pickup.isActiveAndEnabled)
                {
                    position = pickup.transform.position;
                    return true;
                }
            }

            position = Vector3.zero;
            return false;
        }

        public static bool Exists(string id)
        {
            return TryLocate(id, out _);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1.0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 1.0f);
        }
    }
}
