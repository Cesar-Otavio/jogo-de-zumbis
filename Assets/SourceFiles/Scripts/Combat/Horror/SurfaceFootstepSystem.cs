using StarterAssets;
using UnityEngine;
using UnityEngine.AI;

namespace Combat
{
    /* Footsteps that sound like the ground they land on, louder when running and on metal,
     * almost silent when crouching. The player's steps come from the animation events (OnFootstep / OnLand);
     * characters without them (or between events) step on a timer from their speed.
     * Every step can also be heard by the AI through the NoiseSystem.
     */
    public class SurfaceFootstepSystem : MonoBehaviour
    {
        public FootstepLibrary Library;
        [Tooltip("Let the AI hear these steps (the player); enemies only play the sound")]
        public bool EmitNoise = true;
        [Tooltip("Steps from the animation events; otherwise only from the timer")]
        public bool UseAnimationEvents = true;
        public LayerMask GroundLayers = ~0;

        [Header("Loudness")]
        [Range(0, 1)] public float Volume = 0.6f;
        public float CrouchVolume = 0.25f;
        public float RunVolume = 1.0f;
        [Tooltip("Speed above which steps count as running")]
        public float RunSpeed = 4.2f;
        [Tooltip("Meters per step, for the timer")]
        public float StrideLength = 1.4f;
        public float MaxDistance = 30.0f;

        [Header("Noise radius for the AI (meters)")]
        public float CrouchNoise = 2.5f;
        public float WalkNoise = 7.0f;
        public float RunNoise = 16.0f;
        public float LandNoise = 11.0f;

        public SurfaceType CurrentSurface { get; private set; }
        // how loud the last step was, 0..1, e.g. for the stealth meter
        public float LastLoudness { get; private set; }
        public float LastStepTime { get; private set; } = -10.0f;

        private ThirdPersonController _controller;
        private CharacterController _characterController;
        private NavMeshAgent _agent;
        private Health _health;
        private float _distanceSinceStep;
        private float _lastEventTime = -10.0f;
        private bool _wasGrounded = true;
        private float _fallSpeed;

        private void Awake()
        {
            _controller = GetComponent<ThirdPersonController>();
            _characterController = GetComponent<CharacterController>();
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
        }

        private Vector3 Velocity
        {
            get
            {
                if (_characterController != null && _characterController.enabled) return _characterController.velocity;
                if (_agent != null && _agent.enabled) return _agent.velocity;
                return Vector3.zero;
            }
        }

        private bool Crouching => _controller != null && _controller.IsCrouching;

        private void Update()
        {
            if (_health != null && _health.IsDead) return;

            Vector3 velocity = Velocity;
            float speed = new Vector3(velocity.x, 0.0f, velocity.z).magnitude;
            bool grounded = _controller == null || _controller.Grounded;

            // landings: the harder the fall, the louder
            if (_controller != null)
            {
                if (!grounded) _fallSpeed = Mathf.Max(_fallSpeed, -velocity.y);
                if (grounded && !_wasGrounded && _fallSpeed > 3.0f) Land(Mathf.InverseLerp(3.0f, 12.0f, _fallSpeed));
                if (grounded) _fallSpeed = 0.0f;
                _wasGrounded = grounded;
            }

            if (!grounded || speed < 0.3f)
            {
                _distanceSinceStep = StrideLength * 0.5f;
                return;
            }

            // the timer only fills in when animation events stop coming (e.g. enemies, or blended-out clips)
            bool eventsActive = UseAnimationEvents && Time.time - _lastEventTime < 0.8f;
            _distanceSinceStep += speed * Time.deltaTime;
            float stride = StrideLength * (speed > RunSpeed ? 1.35f : Crouching ? 0.7f : 1.0f);
            if (_distanceSinceStep >= stride)
            {
                _distanceSinceStep = 0.0f;
                if (!eventsActive) Step(speed);
            }
        }

        // animation events from the locomotion clips
        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (!UseAnimationEvents || animationEvent.animatorClipInfo.weight < 0.5f) return;
            _lastEventTime = Time.time;
            _distanceSinceStep = 0.0f;
            Vector3 velocity = Velocity;
            Step(new Vector3(velocity.x, 0.0f, velocity.z).magnitude);
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            // landings are detected from the fall speed instead, which also covers jumps without the event
        }

        public void Step(float speed)
        {
            if (Library == null || speed < 0.3f) return;
            // enemies far from the listener don't use up voices
            if (!EmitNoise && Camera.main != null && (Camera.main.transform.position - transform.position).sqrMagnitude > MaxDistance * MaxDistance) return;

            CurrentSurface = Surfaces.Below(transform.position, GroundLayers);
            FootstepLibrary.SurfaceSounds sounds = Library.Get(CurrentSurface);
            if (sounds == null) return;

            bool running = speed > RunSpeed;
            float gait = Crouching ? CrouchVolume : running ? RunVolume : Mathf.Lerp(0.55f, 0.8f, speed / RunSpeed);
            Play(sounds.Steps, Volume * gait * sounds.Volume, running ? 0.06f : 0.1f);

            // rain leaves the ground wet: a splash under the step
            if (WeatherController.Wetness > 0.35f && CurrentSurface != SurfaceType.Water && CurrentSurface != SurfaceType.Metal)
            {
                Play(Library.WetLayer, Volume * gait * 0.5f * WeatherController.Wetness, 0.1f);
            }

            float radius = (Crouching ? CrouchNoise : running ? RunNoise : WalkNoise) * sounds.Loudness;
            LastLoudness = Mathf.Clamp01(radius / RunNoise);
            LastStepTime = Time.time;
            if (EmitNoise)
            {
                NoiseCategory category = running ? NoiseCategory.Run : NoiseCategory.Footstep;
                NoiseSystem.Emit(transform.position, radius, category, _health != null ? _health.Team : Factions.Player, gameObject);
            }
        }

        private void Land(float strength)
        {
            if (Library == null) return;

            CurrentSurface = Surfaces.Below(transform.position, GroundLayers);
            FootstepLibrary.SurfaceSounds sounds = Library.Get(CurrentSurface);
            if (sounds == null) return;

            AudioClip[] clips = sounds.Lands != null && sounds.Lands.Length > 0 ? sounds.Lands : sounds.Steps;
            Play(clips, Volume * Mathf.Lerp(0.7f, 1.2f, strength) * sounds.Volume, 0.05f);

            float radius = LandNoise * Mathf.Lerp(0.6f, 1.4f, strength) * sounds.Loudness * (Crouching ? 0.5f : 1.0f);
            LastLoudness = Mathf.Clamp01(radius / RunNoise);
            LastStepTime = Time.time;
            if (EmitNoise) NoiseSystem.Emit(transform.position, radius, NoiseCategory.Land, _health != null ? _health.Team : Factions.Player, gameObject);
        }

        private void Play(AudioClip[] clips, float volume, float pitchVariation)
        {
            if (clips == null || clips.Length == 0 || CombatEffects.Instance == null) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            CombatEffects.Instance.PlaySound(clip, transform.position + Vector3.up * 0.1f, Mathf.Clamp01(volume), pitchVariation, 1.0f, 1.5f, MaxDistance);
        }
    }
}
