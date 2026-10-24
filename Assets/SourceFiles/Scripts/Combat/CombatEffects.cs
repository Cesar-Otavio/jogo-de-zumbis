using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    // Pooled visual and audio effects shared by every weapon
    public class CombatEffects : MonoBehaviour
    {
        public static CombatEffects Instance { get; private set; }

        [Header("Materials")]
        public Material SparkMaterial;
        public Material FlashMaterial;
        public Material TracerMaterial;
        public Material SmokeMaterial;
        public Material DecalMaterial;
        public Material BrassMaterial;

        [Header("Loot")]
        public Material CrateMaterial;
        public Material CrateMetalMaterial;
        public Material AmmoBoxMaterial;
        public Material MedkitMaterial;
        public Material CrossMaterial;
        public AudioClip CrateOpenSound;
        public AudioClip AmmoPickupSound;
        public AudioClip HealthPickupSound;

        [Header("Impacts")]
        public AudioClip[] GroundImpactSounds;
        public AudioClip[] MetalImpactSounds;
        public AudioClip[] FleshImpactSounds;
        public AudioClip[] ShellDropSounds;
        public AudioClip[] BulletWhizSounds;

        [Header("Feedback")]
        public AudioClip HitMarkerSound;
        public AudioClip HeadshotSound;
        public AudioClip KillSound;
        public AudioClip PickupSound;
        public AudioClip SwitchSound;

        [Header("Robots")]
        public AudioClip[] RobotHurtSounds;
        public AudioClip RobotDeathSound;
        public AudioClip[] PlayerHurtSounds;
        public AudioClip AlertSound;

        private const int AudioVoices = 48;
        private const int MaxDecals = 80;
        private const int MaxShells = 40;

        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private readonly List<AudioLowPassFilter> _voiceFilters = new List<AudioLowPassFilter>();

        [Header("Spatial audio")]
        [Tooltip("Sounds with a wall between them and the listener play muffled and quieter")]
        public bool Occlusion = true;
        public float OccludedCutoff = 1100.0f;
        [Range(0, 1)] public float OccludedVolume = 0.6f;
        private int _nextVoice;

        private ParticleSystem _sparks;
        private ParticleSystem _smoke;

        private readonly Queue<Transform> _decals = new Queue<Transform>();
        private readonly Queue<Rigidbody> _shells = new Queue<Rigidbody>();
        private readonly Stack<LineRenderer> _freeTracers = new Stack<LineRenderer>();
        private readonly List<(LineRenderer line, float born, float life)> _liveTracers = new List<(LineRenderer, float, float)>();
        private readonly Stack<GameObject> _freeFlashes = new Stack<GameObject>();
        private readonly List<(GameObject flash, float born)> _liveFlashes = new List<(GameObject, float)>();

        private Mesh _quad;
        private Mesh _shellMesh;

        private void Awake()
        {
            Instance = this;

            for (int i = 0; i < AudioVoices; i++)
            {
                var voice = new GameObject("Voice").AddComponent<AudioSource>();
                voice.transform.SetParent(transform);
                voice.playOnAwake = false;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                voice.dopplerLevel = 0.0f;
                _voices.Add(voice);
                var filter = voice.gameObject.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = 22000.0f;
                _voiceFilters.Add(filter);
            }

            _quad = BuildQuad();
            _shellMesh = BuildShellMesh();
            _sparks = BuildParticles("Sparks", SparkMaterial, ParticleSystemRenderMode.Stretch, 1.5f);
            _smoke = BuildParticles("Smoke", SmokeMaterial, ParticleSystemRenderMode.Billboard, -0.05f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float now = Time.time;

            for (int i = _liveTracers.Count - 1; i >= 0; i--)
            {
                var (line, born, life) = _liveTracers[i];
                float t = (now - born) / life;
                if (t >= 1.0f)
                {
                    line.enabled = false;
                    _freeTracers.Push(line);
                    _liveTracers.RemoveAt(i);
                    continue;
                }

                // the tail catches up with the head, so the tracer reads as a moving bullet
                Vector3 from = line.GetPosition(0);
                Vector3 to = line.GetPosition(1);
                line.SetPosition(0, Vector3.Lerp(from, to, t * 0.35f));
                Color c = line.startColor;
                c.a = 1.0f - t;
                line.startColor = c;
                line.endColor = c;
            }

            for (int i = _liveFlashes.Count - 1; i >= 0; i--)
            {
                var (flash, born) = _liveFlashes[i];
                if (flash == null)
                {
                    // destroyed together with the weapon it was attached to
                    _liveFlashes.RemoveAt(i);
                    continue;
                }

                if (now - born < 0.05f) continue;
                flash.SetActive(false);
                flash.transform.SetParent(transform);
                _freeFlashes.Push(flash);
                _liveFlashes.RemoveAt(i);
            }
        }

        // ---------------------------------------------------------------- audio

        public void PlaySound(AudioClip clip, Vector3 position, float volume = 1.0f, float pitchVariation = 0.05f,
            float spatial = 1.0f, float minDistance = 2.0f, float maxDistance = 80.0f, float pitch = 1.0f)
        {
            if (clip == null) return;

            // reuse the voice that started longest ago
            AudioSource voice = _voices[_nextVoice];
            AudioLowPassFilter filter = _voiceFilters[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Count;

            // something solid in between: duller and quieter, e.g. an enemy behind a container
            bool occluded = Occlusion && spatial > 0.5f && IsOccluded(position);
            filter.cutoffFrequency = occluded ? OccludedCutoff : 22000.0f;

            voice.transform.position = position;
            voice.clip = clip;
            voice.volume = volume * (occluded ? OccludedVolume : 1.0f);
            voice.pitch = pitch * (1.0f + Random.Range(-pitchVariation, pitchVariation));
            voice.spatialBlend = spatial;
            voice.minDistance = minDistance;
            voice.maxDistance = maxDistance;
            voice.Play();
        }

        private static bool IsOccluded(Vector3 position)
        {
            Camera listener = Camera.main;
            if (listener == null) return false;
            Vector3 from = listener.transform.position;
            Vector3 offset = position - from;
            float distance = offset.magnitude;
            if (distance < 2.0f || distance > 120.0f) return false;
            // the player's own body and characters don't block sound
            return Physics.Raycast(from, offset / distance, distance - 0.5f, CombatLayers.Sight & ~(1 << CombatLayers.Player), QueryTriggerInteraction.Ignore);
        }

        public void PlayRandom(AudioClip[] clips, Vector3 position, float volume = 1.0f, float spatial = 1.0f)
        {
            if (clips == null || clips.Length == 0) return;
            PlaySound(clips[Random.Range(0, clips.Length)], position, volume, 0.08f, spatial);
        }

        public void PlayUI(AudioClip clip, float volume = 0.6f)
        {
            PlaySound(clip, transform.position, volume, 0.0f, 0.0f);
        }

        // ---------------------------------------------------------------- visuals

        public void MuzzleFlash(Transform weapon, Vector3 position, Quaternion rotation, float scale)
        {
            GameObject flash = null;
            while (flash == null && _freeFlashes.Count > 0) flash = _freeFlashes.Pop();
            if (flash == null) flash = BuildFlash();
            flash.transform.SetParent(weapon, false);
            flash.transform.SetPositionAndRotation(position, rotation * Quaternion.Euler(0.0f, 0.0f, Random.Range(0.0f, 360.0f)));
            flash.transform.localScale = Vector3.one * (scale * Random.Range(0.8f, 1.2f)) / weapon.lossyScale.x;
            flash.SetActive(true);
            _liveFlashes.Add((flash, Time.time));
        }

        public void Tracer(Vector3 from, Vector3 to, float width = 0.025f, float life = 0.07f)
        {
            LineRenderer line = _freeTracers.Count > 0 ? _freeTracers.Pop() : BuildTracer();
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = width * 0.4f;
            line.endWidth = width;
            line.startColor = new Color(1.0f, 0.85f, 0.5f, 1.0f);
            line.endColor = line.startColor;
            line.enabled = true;
            _liveTracers.Add((line, Time.time, life));
        }

        public void FleshImpact(Vector3 point, Vector3 normal, Vector3 direction)
        {
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < 7; i++)
            {
                emit.position = point;
                emit.velocity = (normal * 0.5f + direction * 0.7f + Random.insideUnitSphere * 0.6f) * Random.Range(0.8f, 2.5f);
                emit.startLifetime = Random.Range(0.25f, 0.5f);
                emit.startSize = Random.Range(0.05f, 0.14f);
                emit.startColor = new Color(0.45f, 0.03f, 0.03f, 0.85f);
                _smoke.Emit(emit, 1);
            }

            PlayRandom(FleshImpactSounds, point, 0.8f);
        }

        public void Impact(Vector3 point, Vector3 normal, bool metal, Transform surface)
        {
            var emit = new ParticleSystem.EmitParams();
            int sparks = metal ? Random.Range(10, 16) : Random.Range(4, 8);
            for (int i = 0; i < sparks; i++)
            {
                emit.position = point + normal * 0.02f;
                emit.velocity = (normal + Random.insideUnitSphere * 0.8f).normalized * Random.Range(2.0f, metal ? 7.0f : 4.0f);
                emit.startLifetime = Random.Range(0.12f, 0.35f);
                emit.startSize = Random.Range(0.015f, 0.035f);
                emit.startColor = metal ? new Color(1.0f, 0.75f, 0.35f) : new Color(1.0f, 0.9f, 0.7f);
                _sparks.Emit(emit, 1);
            }

            int puffs = metal ? 2 : 5;
            for (int i = 0; i < puffs; i++)
            {
                emit.position = point + normal * 0.05f;
                emit.velocity = (normal + Random.insideUnitSphere * 0.5f) * Random.Range(0.3f, 1.2f);
                emit.startLifetime = Random.Range(0.5f, 1.1f);
                emit.startSize = Random.Range(0.15f, 0.35f);
                emit.startColor = metal ? new Color(0.3f, 0.3f, 0.32f, 0.5f) : new Color(0.75f, 0.72f, 0.68f, 0.55f);
                _smoke.Emit(emit, 1);
            }

            if (metal)
            {
                PlayRandom(MetalImpactSounds, point, 0.8f);
            }
            else
            {
                PlayRandom(GroundImpactSounds, point, 0.7f);
                Decal(point, normal, surface);
            }
        }

        private void Decal(Vector3 point, Vector3 normal, Transform surface)
        {
            // decals stuck to objects that got destroyed are gone too
            Transform decal = _decals.Count >= MaxDecals ? _decals.Dequeue() : null;
            if (decal == null)
            {
                var go = new GameObject("BulletHole", typeof(MeshFilter), typeof(MeshRenderer));
                go.GetComponent<MeshFilter>().sharedMesh = _quad;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = DecalMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                decal = go.transform;
            }

            decal.SetParent(null);
            decal.SetPositionAndRotation(point + normal * 0.005f,
                Quaternion.LookRotation(-normal) * Quaternion.Euler(0.0f, 0.0f, Random.Range(0.0f, 360.0f)));
            decal.localScale = Vector3.one * Random.Range(0.07f, 0.11f);
            // follow moving platforms without inheriting their scale
            if (surface != null && !surface.gameObject.isStatic) decal.SetParent(surface, true);
            _decals.Enqueue(decal);
        }

        public void EjectShell(Vector3 position, Quaternion rotation, Vector3 velocity, float size = 1.0f)
        {
            Rigidbody shell;
            if (_shells.Count >= MaxShells)
            {
                shell = _shells.Dequeue();
            }
            else
            {
                var go = new GameObject("Shell", typeof(MeshFilter), typeof(MeshRenderer), typeof(CapsuleCollider), typeof(Rigidbody), typeof(ShellCasing));
                go.layer = CombatLayers.Debris;
                go.GetComponent<MeshFilter>().sharedMesh = _shellMesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = BrassMaterial;
                var col = go.GetComponent<CapsuleCollider>();
                col.direction = 2;
                col.radius = 0.006f;
                col.height = 0.03f;
                shell = go.GetComponent<Rigidbody>();
                shell.mass = 0.01f;
                shell.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                shell.interpolation = RigidbodyInterpolation.Interpolate;
            }

            shell.transform.SetPositionAndRotation(position, rotation);
            shell.transform.localScale = Vector3.one * size;
            shell.linearVelocity = velocity;
            shell.angularVelocity = Random.insideUnitSphere * 25.0f;
            shell.GetComponent<ShellCasing>().Arm();
            _shells.Enqueue(shell);
        }

        // ---------------------------------------------------------------- builders

        private ParticleSystem BuildParticles(string name, Material material, ParticleSystemRenderMode mode, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = 600;

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1.0f,
                mode == ParticleSystemRenderMode.Stretch ? AnimationCurve.Linear(0, 1, 1, 0) : AnimationCurve.EaseInOut(0, 0.6f, 1, 1.6f));

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(mode == ParticleSystemRenderMode.Stretch ? 0.8f : 0.6f, 0.3f), new GradientAlphaKey(0, 1) });
            colorOverLife.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.04f;
                renderer.lengthScale = 1.0f;
            }

            ps.Play();
            return ps;
        }

        private GameObject BuildFlash()
        {
            var go = new GameObject("MuzzleFlash");
            // two crossed quads along the barrel plus one facing forward read as a flash from any angle
            for (int i = 0; i < 3; i++)
            {
                var part = new GameObject("Part", typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(go.transform, false);
                part.GetComponent<MeshFilter>().sharedMesh = _quad;
                var renderer = part.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = FlashMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (i == 0)
                {
                    part.transform.localScale = new Vector3(0.12f, 0.12f, 1.0f);
                }
                else
                {
                    part.transform.localRotation = Quaternion.Euler(0.0f, 90.0f, i == 1 ? 0.0f : 90.0f);
                    part.transform.localPosition = new Vector3(0.0f, 0.0f, 0.09f);
                    part.transform.localScale = new Vector3(0.22f, 0.09f, 1.0f);
                }
            }

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.75f, 0.4f);
            light.range = 6.0f;
            light.intensity = 6.0f;
            light.shadows = LightShadows.None;
            return go;
        }

        private LineRenderer BuildTracer()
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.sharedMaterial = TracerMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.numCapVertices = 2;
            return line;
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "FxQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildShellMesh()
        {
            // a small open cylinder is enough for a casing
            const int sides = 8;
            const float radius = 0.006f;
            const float length = 0.03f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2.0f / sides;
                var p = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0.0f);
                vertices.Add(p + Vector3.back * length * 0.5f);
                vertices.Add(p + Vector3.forward * length * 0.5f);
            }

            for (int i = 0; i < sides; i++)
            {
                int v = i * 2;
                triangles.AddRange(new[] { v, v + 1, v + 2, v + 1, v + 3, v + 2 });
            }

            int capBack = vertices.Count;
            vertices.Add(Vector3.back * length * 0.5f);
            for (int i = 0; i < sides; i++)
            {
                triangles.AddRange(new[] { capBack, i * 2 + 2, i * 2 });
            }

            var mesh = new Mesh { name = "Shell" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
