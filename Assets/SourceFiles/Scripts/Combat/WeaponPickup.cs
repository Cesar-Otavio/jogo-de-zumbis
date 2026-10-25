using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* A weapon lying in the world that can be picked up.
     * Placed pickups hover and spin with a soft glow; dropped ones fall with physics.
     */
    public class WeaponPickup : MonoBehaviour
    {
        public static readonly List<WeaponPickup> All = new List<WeaponPickup>();

        public WeaponData Data;
        [Tooltip("-1 = full magazine")]
        public int Magazine = -1;
        [Tooltip("-1 = the weapon's starting reserve")]
        public int Reserve = -1;
        [Tooltip("Hover and spin in place instead of falling with physics")]
        public bool Floating = true;
        [Tooltip("Dropped pickups disappear after this many seconds, 0 = never")]
        public float Lifetime;

        public bool Highlighted { get; set; }
        public Vector3 Center => _visual != null ? _visual.position : transform.position;
        // where the model is right now, so the hands can take it from there
        public Pose VisualPose => _visual != null ? new Pose(_visual.position, _visual.rotation) : new Pose(transform.position, transform.rotation);

        private Transform _visual;
        private Light _glow;
        private Vector3 _initialVelocity;
        private float _highlight;
        private float _born;

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void OnDestroy() => NetWorld.Unregister(this);

        private void Start()
        {
            _born = Time.time;
            if (Data == null)
            {
                Destroy(gameObject);
                return;
            }

            if (Magazine < 0) Magazine = Data.MagazineSize;
            if (Reserve < 0) Reserve = Data.StartReserve;
            NetWorld.Register(this);

            GameObject visual = Instantiate(Data.Model, transform);
            visual.name = "Visual";
            foreach (Collider col in visual.GetComponentsInChildren<Collider>()) Destroy(col);
            _visual = visual.transform;

            if (Floating)
            {
                SetLayer(2); // Ignore Raycast: bullets fly through hovering pickups
                var glow = new GameObject("Glow");
                glow.transform.SetParent(transform, false);
                glow.transform.localPosition = Vector3.up * 0.35f;
                _glow = glow.AddComponent<Light>();
                _glow.type = LightType.Point;
                _glow.color = new Color(1.0f, 0.8f, 0.4f);
                _glow.range = 1.6f;
                _glow.intensity = 1.5f;
                _glow.shadows = LightShadows.None;
                // center the gun on the pivot so it spins around its middle
                _visual.localPosition = -(Data.GripPoint + Data.MuzzlePoint) * 0.5f;
            }
            else
            {
                SetLayer(CombatLayers.Debris);
                AddPhysics();
            }
        }

        private void SetLayer(int layer)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
        }

        private void AddPhysics()
        {
            // box collider that wraps the model
            Quaternion rotation = transform.rotation;
            transform.rotation = Quaternion.identity;
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer r in _visual.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            transform.rotation = rotation;

            var box = gameObject.AddComponent<BoxCollider>();
            box.center = bounds.center - transform.position;
            box.size = bounds.size;
            var body = gameObject.AddComponent<Rigidbody>();
            body.mass = 3.0f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = _initialVelocity;
            body.angularVelocity = Random.insideUnitSphere * 3.0f;
        }

        private void Update()
        {
            _highlight = Mathf.MoveTowards(_highlight, Highlighted ? 1.0f : 0.0f, Time.deltaTime * 5.0f);

            if (Floating && _visual != null)
            {
                float t = Time.time - _born;
                transform.Rotate(0.0f, 70.0f * Time.deltaTime, 0.0f, Space.World);
                Vector3 center = (Data.GripPoint + Data.MuzzlePoint) * 0.5f;
                _visual.localPosition = Vector3.up * (0.5f + Mathf.Sin(t * 2.2f) * 0.06f) - center;
                _visual.localScale = Vector3.one * (1.0f + _highlight * 0.12f * (1.0f + Mathf.Sin(t * 8.0f) * 0.3f));
                if (_glow != null) _glow.intensity = 1.5f + _highlight * 2.5f;
            }

            if (Lifetime > 0.0f && Time.time - _born > Lifetime) Destroy(gameObject);
        }

        public WeaponInstance Take()
        {
            var weapon = new WeaponInstance(Data, Magazine, Reserve);
            Destroy(gameObject);
            return weapon;
        }

        public static WeaponPickup Drop(WeaponInstance weapon, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            var go = new GameObject("Dropped_" + weapon.Data.DisplayName);
            go.transform.SetPositionAndRotation(position, rotation);
            var pickup = go.AddComponent<WeaponPickup>();
            pickup.Data = weapon.Data;
            pickup.Magazine = weapon.Magazine;
            pickup.Reserve = weapon.InfiniteReserve ? weapon.Data.MagazineSize : weapon.Reserve;
            pickup.Floating = false;
            pickup.Lifetime = 60.0f;
            pickup._initialVelocity = velocity;
            return pickup;
        }
    }
}
