using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Combat
{
    /* A patch of burning fuel from a molotov: damages whoever stands in it over time (the player
     * at half rate), cuts the navigation mesh so enemies path around it when they can, and burns
     * out after a while. Lit, crackling and visible from afar.
     */
    public class FireZone : MonoBehaviour
    {
        public float Radius = 3.2f;
        public float Duration = 8.0f;
        public float DamagePerSecond = 14.0f;
        public float PlayerDamageScale = 0.5f;
        public GameObject Instigator;

        private float _started;
        private float _nextTick;
        private Light _light;
        private AudioSource _loop;
        private ParticleSystem _flames;
        private ParticleSystem _smoke;
        private PlayerCombat _thrower;

        public static FireZone Create(Vector3 point, GameObject instigator)
        {
            var go = new GameObject("FireZone");
            go.transform.position = point;
            var zone = go.AddComponent<FireZone>();
            zone.Instigator = instigator;
            return zone;
        }

        private void Start()
        {
            _started = Time.time;
            _thrower = Instigator != null ? Instigator.GetComponent<PlayerCombat>() : null;

            // enemies route around the flames instead of walking through
            var obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = Radius * 0.8f;
            obstacle.height = 2.0f;
            obstacle.carving = true;

            var lightObject = new GameObject("FireLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * 1.0f;
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1.0f, 0.55f, 0.2f);
            _light.range = Radius * 4.0f;
            _light.shadows = LightShadows.None;

            _loop = gameObject.AddComponent<AudioSource>();
            AudioRouting.Route(_loop, AudioRouting.Channel.Sfx);
            _loop.clip = CampaignAssets.Clip(a => a.FireLoop);
            _loop.loop = true;
            _loop.spatialBlend = 1.0f;
            _loop.maxDistance = 30.0f;
            _loop.volume = 0.8f;
            if (_loop.clip != null) _loop.Play();

            Material fire = CampaignAssets.Instance != null ? CampaignAssets.Instance.FireMaterial : null;
            CombatEffects fx = CombatEffects.Instance;
            _flames = Particles.Burst(transform, fire != null ? fire : fx != null ? fx.SparkMaterial : null, new Color(1.0f, 0.5f, 0.15f), 0.6f, 0.9f, 60.0f, true, 1.5f);
            ParticleSystem.ShapeModule shape = _flames.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Radius * 0.8f;
            ParticleSystem.MainModule main = _flames.main;
            main.gravityModifier = -0.3f;
            _smoke = Particles.Burst(transform, fx != null ? fx.SmokeMaterial : null, new Color(0.15f, 0.15f, 0.15f, 0.5f), 1.5f, 3.0f, 8.0f, true, 1.0f);
            NoiseSystem.Emit(transform.position, 14.0f, NoiseCategory.Explosion, Factions.Environment, gameObject);
        }

        private void Update()
        {
            float age = Time.time - _started;
            float fade = Mathf.Clamp01((Duration - age) / 1.5f);
            _light.intensity = 8.0f * fade * (0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 10.0f, 1.0f));
            if (_loop != null) _loop.volume = 0.8f * fade;

            if (age >= Duration)
            {
                _flames.Stop();
                _smoke.Stop();
                Destroy(gameObject, 3.0f);
                enabled = false;
                return;
            }

            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 0.5f;
            Burn(0.5f);
        }

        private void Burn(float seconds)
        {
            var burning = new List<Health>();
            foreach (Health health in Health.Living)
            {
                if (health == null || health.IsDead) continue;
                Vector3 offset = health.transform.position - transform.position;
                offset.y = 0.0f;
                if (offset.magnitude <= Radius && Mathf.Abs(health.transform.position.y - transform.position.y) < 2.0f) burning.Add(health);
            }

            foreach (Health health in burning)
            {
                float amount = DamagePerSecond * seconds * (health.Team == Factions.Player ? PlayerDamageScale : 1.0f);
                bool wasDead = health.IsDead;
                health.TakeDamage(new DamageInfo
                {
                    Amount = amount,
                    Point = health.transform.position + Vector3.up,
                    Direction = (health.transform.position - transform.position).normalized,
                    Force = 1.0f,
                    Instigator = Instigator
                });

                if (_thrower != null && health.Team != Factions.Player && !wasDead && health.IsDead)
                {
                    _thrower.RegisterHit(new HitReport { Target = health, Damage = amount, Killed = true });
                }
            }
        }

        // a fire burning at this point, e.g. for enemies to avoid
        public static bool Burning(Vector3 point)
        {
            foreach (FireZone zone in FindObjectsByType<FireZone>())
            {
                if (zone.enabled && Vector3.Distance(zone.transform.position, point) < zone.Radius) return true;
            }

            return false;
        }
    }
}
