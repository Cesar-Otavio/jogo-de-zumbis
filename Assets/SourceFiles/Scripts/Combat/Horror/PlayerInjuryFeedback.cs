using StarterAssets;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Combat
{
    public enum InjuryState
    {
        Healthy,
        Injured,
        Critical
    }

    /* How the body feels: heavy breathing when hurt, a heartbeat, washed-out colours and slower
     * sprinting when critical. The red edges are drawn by the HUD; everything visual is scaled
     * by the injury effects setting, and nothing takes control away from the player.
     */
    public class PlayerInjuryFeedback : MonoBehaviour
    {
        [Range(0, 1)] public float InjuredBelow = 0.6f;
        [Range(0, 1)] public float CriticalBelow = 0.25f;
        [Tooltip("Sprint speed multiplier while critical")]
        public float CriticalSprint = 0.8f;

        public AudioClip BreathingLoop;
        public AudioClip HeartbeatLoop;

        public InjuryState State { get; private set; }

        private Health _health;
        private ThirdPersonController _controller;
        private AudioSource _breathing;
        private AudioSource _heartbeat;
        private Volume _volume;
        private ColorAdjustments _color;
        private ChromaticAberration _aberration;
        private float _blend;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _controller = GetComponent<ThirdPersonController>();
            _breathing = Loop(BreathingLoop);
            _heartbeat = Loop(HeartbeatLoop);
            BuildVolume();
        }

        private AudioSource Loop(AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0.0f;
            source.volume = 0.0f;
            source.playOnAwake = false;
            if (clip != null) source.Play();
            return source;
        }

        // a global volume on top of the night grade, weighted by how badly hurt the player is
        private void BuildVolume()
        {
            var go = new GameObject("InjuryVolume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 50.0f;
            _volume.weight = 0.0f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _color = profile.Add<ColorAdjustments>();
            _color.saturation.Override(-60.0f);
            _aberration = profile.Add<ChromaticAberration>();
            _aberration.intensity.Override(0.6f);
            _volume.sharedProfile = profile;
        }

        private void Update()
        {
            float health = _health.Normalized;
            bool dead = _health.IsDead;
            State = dead || health >= InjuredBelow ? InjuryState.Healthy : health >= CriticalBelow ? InjuryState.Injured : InjuryState.Critical;

            // 0 healthy .. 1 at death's door
            float injury = dead ? 0.0f : Mathf.Clamp01(Mathf.InverseLerp(InjuredBelow, 0.0f, health));
            float critical = dead ? 0.0f : Mathf.Clamp01(Mathf.InverseLerp(CriticalBelow, 0.05f, health));
            _blend = Mathf.MoveTowards(_blend, critical, Time.deltaTime * 0.8f);

            bool paused = Time.timeScale <= 0.0f;
            _breathing.volume = paused ? 0.0f : Mathf.Lerp(_breathing.volume, injury * 0.55f, Time.unscaledDeltaTime * 2.0f);
            _breathing.pitch = 1.0f + critical * 0.15f;
            _heartbeat.volume = paused ? 0.0f : Mathf.Lerp(_heartbeat.volume, critical * 0.8f, Time.unscaledDeltaTime * 2.0f);
            _heartbeat.pitch = 1.0f + critical * 0.25f;

            _volume.weight = _blend * GameSettings.InjuryEffects;
            if (_controller != null) _controller.SprintLimit = State == InjuryState.Critical ? CriticalSprint : 1.0f;
        }
    }
}
