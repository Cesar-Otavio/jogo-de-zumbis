using UnityEngine;

namespace Combat
{
    /* A thrown consumable in flight and after it lands:
     *   Grenade: bounces, explodes 2.5 s after the throw (area damage, see Explosion).
     *   Molotov: shatters on the first hit and leaves a fire zone.
     *   Flare: burns red for a while, lighting the way; the infected come to look at it.
     *   Decoy: beeps for a while; every beep is a noise enemies investigate.
     * Built from primitives so it needs no model.
     */
    public class ThrownItem : MonoBehaviour
    {
        public QuickItem Kind;
        public GameObject Thrower;

        [Header("Grenade")]
        public float Fuse = 2.5f;
        public float BlastRadius = 7.0f;
        public float BlastDamage = 160.0f;

        [Header("Flare")]
        public float FlareTime = 40.0f;
        public float FlareLure = 22.0f;

        [Header("Decoy")]
        public float DecoyTime = 10.0f;
        public float DecoyNoise = 30.0f;

        private float _thrownAt;
        private float _landedAt = -1.0f;
        private float _nextPulse;
        private bool _spent;
        private Light _light;
        private AudioSource _loop;
        private ParticleSystem _sparks;

        public static ThrownItem Launch(QuickItem kind, Vector3 origin, Vector3 velocity, GameObject thrower)
        {
            var go = new GameObject("Thrown_" + kind);
            go.transform.position = origin;
            go.layer = CombatLayers.Debris;
            var item = go.AddComponent<ThrownItem>();
            item.Kind = kind;
            item.Thrower = thrower;
            item.Build();

            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.5f;
            body.linearDamping = 0.0f;
            body.angularDamping = 0.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 8.0f;

            // never bounce off the thrower
            Collider own = go.GetComponent<Collider>();
            if (thrower != null)
            {
                foreach (Collider col in thrower.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(own, col);
            }

            return item;
        }

        private void Build()
        {
            CombatEffects fx = CombatEffects.Instance;
            switch (Kind)
            {
                case QuickItem.Grenade:
                    Shape(PrimitiveType.Sphere, Vector3.zero, new Vector3(0.09f, 0.11f, 0.09f), fx != null ? fx.AmmoBoxMaterial : null);
                    Shape(PrimitiveType.Cube, new Vector3(0.0f, 0.065f, 0.0f), new Vector3(0.03f, 0.03f, 0.03f), fx != null ? fx.CrateMetalMaterial : null);
                    break;
                case QuickItem.Molotov:
                    Shape(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.08f, 0.1f, 0.08f), fx != null ? fx.BrassMaterial : null);
                    Shape(PrimitiveType.Cylinder, new Vector3(0.0f, 0.12f, 0.0f), new Vector3(0.03f, 0.03f, 0.03f), fx != null ? fx.MedkitMaterial : null);
                    break;
                case QuickItem.Flare:
                    Shape(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.04f, 0.1f, 0.04f), fx != null ? fx.CrossMaterial : null);
                    break;
                default:
                    Shape(PrimitiveType.Cube, Vector3.zero, new Vector3(0.1f, 0.06f, 0.07f), fx != null ? fx.CrateMetalMaterial : null);
                    Shape(PrimitiveType.Cube, new Vector3(0.0f, 0.035f, 0.0f), new Vector3(0.02f, 0.01f, 0.02f), fx != null ? fx.CrossMaterial : null);
                    break;
            }

            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.07f;
        }

        private void Shape(PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            part.layer = CombatLayers.Debris;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void Start()
        {
            _thrownAt = Time.time;
            if (Kind == QuickItem.Flare) Ignite();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_spent) return;
            if (Kind == QuickItem.Molotov)
            {
                Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                Shatter(point);
                return;
            }

            if (_landedAt < 0.0f && collision.relativeVelocity.magnitude < 6.0f) _landedAt = Time.time;
            // a little clatter on each bounce
            if (collision.relativeVelocity.magnitude > 2.0f && CombatEffects.Instance != null)
            {
                CombatEffects.Instance.PlayRandom(CombatEffects.Instance.ShellDropSounds, transform.position, 0.6f);
            }
        }

        private void Update()
        {
            if (_spent) return;
            float age = Time.time - _thrownAt;

            switch (Kind)
            {
                case QuickItem.Grenade:
                    if (age >= Fuse)
                    {
                        _spent = true;
                        Explosion.Detonate(transform.position + Vector3.up * 0.15f, BlastRadius, BlastDamage, Thrower);
                        Destroy(gameObject);
                    }

                    break;

                case QuickItem.Molotov:
                    // a throw into the void still bursts eventually
                    if (age > 6.0f) Shatter(transform.position);
                    break;

                case QuickItem.Flare:
                    if (_light != null) _light.intensity = 9.0f * (0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 12.0f, 0.0f)) * Mathf.Clamp01((FlareTime - age) / 3.0f);
                    // a red glow the infected come to look at
                    if (FlareLure > 0.0f && Time.time >= _nextPulse)
                    {
                        _nextPulse = Time.time + 3.0f;
                        NoiseSystem.Emit(transform.position, FlareLure, NoiseCategory.Thrown, Factions.Environment, gameObject);
                    }

                    if (age > FlareTime) BurnOut();
                    break;

                case QuickItem.Decoy:
                    if (_landedAt > 0.0f && Time.time >= _nextPulse)
                    {
                        _nextPulse = Time.time + 1.0f;
                        CampaignAssets.Play(a => a.DecoyBeep, transform.position, 0.9f, 45.0f);
                        NoiseSystem.Emit(transform.position, DecoyNoise, NoiseCategory.Thrown, Factions.Environment, gameObject);
                    }

                    if (age > DecoyTime + 2.0f)
                    {
                        _spent = true;
                        Destroy(gameObject, 0.1f);
                    }

                    break;
            }
        }

        // is a lit flare shining on this point (e.g. the final boss is weak to it)
        public static bool FlareNear(Vector3 point, float radius)
        {
            foreach (ThrownItem item in FindObjectsByType<ThrownItem>())
            {
                if (item.Kind == QuickItem.Flare && !item._spent && (item.transform.position - point).sqrMagnitude < radius * radius) return true;
            }

            return false;
        }

        private void Ignite()
        {
            var go = new GameObject("FlareLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.2f;
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1.0f, 0.15f, 0.08f);
            _light.range = 16.0f;
            _light.intensity = 9.0f;
            _light.shadows = LightShadows.None;

            _loop = gameObject.AddComponent<AudioSource>();
            AudioRouting.Route(_loop, AudioRouting.Channel.Sfx);
            _loop.clip = CampaignAssets.Clip(a => a.FlareLoop);
            _loop.loop = true;
            _loop.spatialBlend = 1.0f;
            _loop.maxDistance = 25.0f;
            _loop.volume = 0.5f;
            if (_loop.clip != null) _loop.Play();

            _sparks = Particles.Burst(transform, CampaignAssets.Instance != null ? CampaignAssets.Instance.FlareMaterial : null,
                new Color(1.0f, 0.3f, 0.15f), 0.03f, 1.5f, 40.0f, true);
        }

        private void BurnOut()
        {
            _spent = true;
            if (_light != null) _light.enabled = false;
            if (_loop != null) _loop.Stop();
            if (_sparks != null) _sparks.Stop();
            Destroy(gameObject, 2.0f);
        }

        private void Shatter(Vector3 point)
        {
            _spent = true;
            CampaignAssets.Play(a => a.MolotovBreak, point, 1.0f, 40.0f);
            if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3.0f, CombatLayers.Sight, QueryTriggerInteraction.Ignore)) point = hit.point;
            FireZone.Create(point, Thrower);
            Destroy(gameObject);
        }
    }

    /* Area damage with explicit rules: full damage at the centre falling off to nothing at the
     * edge, a third through cover, the thrower takes half (it hurts, it doesn't one-shot),
     * barricades and doors in range are battered, and the blast is heard far away.
     */
    public static class Explosion
    {
        public const float PlayerDamageScale = 0.5f;
        public const float CoverScale = 0.35f;

        public static void Detonate(Vector3 center, float radius, float damage, GameObject instigator)
        {
            PlayerCombat thrower = instigator != null ? instigator.GetComponent<PlayerCombat>() : null;
            foreach (Health health in Health.Living.ToArray())
            {
                if (health == null || health.IsDead) continue;
                Vector3 chest = health.transform.position + Vector3.up * 1.0f;
                float distance = Vector3.Distance(center, chest);
                if (distance > radius) continue;

                float amount = damage * (1.0f - Mathf.Pow(distance / radius, 1.5f));
                Vector3 direction = (chest - center).normalized;
                if (Physics.Linecast(center, chest, out RaycastHit hit, CombatLayers.Sight, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(health.transform))
                {
                    amount *= CoverScale;
                }

                if (health.Team == Factions.Player) amount *= PlayerDamageScale;
                if (amount < 1.0f) continue;

                bool wasDead = health.IsDead;
                health.TakeDamage(new DamageInfo
                {
                    Amount = amount,
                    Point = chest,
                    Direction = direction + Vector3.up * 0.5f,
                    Force = 14.0f,
                    Instigator = instigator
                });

                // the player's grenade kills count like gun kills (score, kill counter, missions)
                if (thrower != null && health.Team != Factions.Player && !wasDead)
                {
                    thrower.RegisterHit(new HitReport { Target = health, Damage = amount, Killed = health.IsDead });
                }
            }

            Breakables.HitAround(center, radius * 0.6f, damage * 0.4f, instigator);
            NoiseSystem.Emit(center, NoiseSystem.DefaultRadius(NoiseCategory.Explosion), NoiseCategory.Explosion, thrower != null ? Factions.Player : Factions.Environment, instigator);
            Effect(center);

            // shake the view, more the closer it went off
            PlayerCombat player = PlayerCombat.Instance;
            if (player != null)
            {
                float near = 1.0f - Mathf.Clamp01(Vector3.Distance(player.transform.position, center) / (radius * 4.0f));
                player.AddShake(near * 1.5f);
            }
        }

        public static void Effect(Vector3 center)
        {
            CampaignAssets instance = CampaignAssets.Instance;
            if (instance != null && instance.Explosions != null && instance.Explosions.Length > 0 && CombatEffects.Instance != null)
            {
                CombatEffects.Instance.PlaySound(instance.Explosions[Random.Range(0, instance.Explosions.Length)], center, 1.0f, 0.08f, 1.0f, 8.0f, 150.0f);
            }

            TimedLight.Flash(center + Vector3.up, new Color(1.0f, 0.65f, 0.3f), 60.0f * Mathf.Lerp(0.4f, 1.0f, GameSettings.Flashes), 18.0f, 0.35f);
            CombatEffects fx = CombatEffects.Instance;
            var root = new GameObject("Explosion").transform;
            root.position = center;
            Particles.Burst(root, fx != null ? fx.SparkMaterial : null, new Color(1.0f, 0.6f, 0.25f), 0.08f, 0.6f, 160.0f, false, 9.0f);
            Particles.Burst(root, fx != null ? fx.SmokeMaterial : null, new Color(0.2f, 0.2f, 0.2f, 0.7f), 1.2f, 3.5f, 30.0f, false, 2.0f);
            Object.Destroy(root.gameObject, 5.0f);
        }
    }

    // throwaway particle effects built in code
    public static class Particles
    {
        public static ParticleSystem Burst(Transform parent, Material material, Color color, float size, float life, float amount, bool continuous, float speed = 2.0f)
        {
            var go = new GameObject("Particles");
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = continuous;
            main.duration = continuous ? 1.0f : 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startColor = color;
            main.gravityModifier = continuous ? 0.4f : 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            ParticleSystem.EmissionModule emission = particles.emission;
            if (continuous) emission.rateOverTime = amount;
            else
            {
                emission.rateOverTime = 0.0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, (short)amount) });
            }

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = continuous ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Sphere;
            shape.angle = 25.0f;
            shape.radius = 0.1f;
            go.transform.localRotation = Quaternion.Euler(-90.0f, 0.0f, 0.0f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particles.Play();
            return particles;
        }
    }
}
