using UnityEngine;

namespace Combat
{
    // Keeps a hovering weapon pickup here, bringing it back a while after it is taken
    public class WeaponSpawnPoint : MonoBehaviour
    {
        public WeaponData Weapon;
        public float RespawnDelay = 20.0f;

        private WeaponPickup _pickup;
        private float _respawnAt;

        private void Start()
        {
            Spawn();
        }

        private void Update()
        {
            // co-op client: guns on the ground are the host's to place
            if (_pickup != null || Net.IsClientOnly) return;

            if (_respawnAt <= 0.0f)
            {
                _respawnAt = Time.time + RespawnDelay;
            }
            else if (Time.time >= _respawnAt)
            {
                Spawn();
            }
        }

        private void Spawn()
        {
            _respawnAt = 0.0f;
            var go = new GameObject("Pickup_" + Weapon.DisplayName);
            go.transform.SetParent(transform, false);
            _pickup = go.AddComponent<WeaponPickup>();
            _pickup.Data = Weapon;
            _pickup.Floating = true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1.0f, 0.8f, 0.3f, 0.6f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(0.8f, 0.3f, 0.3f));
        }
    }
}
