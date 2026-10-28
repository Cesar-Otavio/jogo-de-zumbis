using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Combat
{
    /* Where each sound goes in the game's AudioMixer (Resources/Audio/GameMixer, built by
     * Tools/Combat/Phase 7/6). The volume options still scale each source as before (GameSettings), so
     * nothing a player set changes; the mixer adds the groups and the ducking: while someone speaks, the
     * ambience drops (the music already drops, DialogueDirector.MusicDuck) and the effects stay where they
     * are, so an infected coming is still heard. Without the mixer asset everything plays as before.
     */
    public static class AudioRouting
    {
        public enum Channel { Music, Sfx, Dialogue, Radio, Ambient, Ui }

        // Master first, then one per channel in the same order
        public static readonly string[] GroupNames = { "Master", "Music", "SFX", "Dialogue", "Radio", "Ambient", "UI" };
        public const float AmbientDuckDb = -8.0f;

        private static AudioMixer _mixer;
        private static AudioMixerGroup[] _groups;
        private static bool _loaded;
        private static float _duck;

        public static string VolumeParameter(string group) => "Vol_" + group;

        // 0 = no ducking, 1 = fully ducked (for tests and the HUD)
        public static float Duck => _duck;

        public static AudioMixer Mixer
        {
            get
            {
                if (_loaded) return _mixer;
                _loaded = true;
                _mixer = Resources.Load<AudioMixer>("Audio/GameMixer");
                if (_mixer == null) return null;
                _groups = new AudioMixerGroup[GroupNames.Length - 1];
                for (int i = 0; i < _groups.Length; i++)
                    foreach (AudioMixerGroup group in _mixer.FindMatchingGroups(GroupNames[i + 1]))
                        if (group.name == GroupNames[i + 1]) _groups[i] = group;
                return _mixer;
            }
        }

        public static AudioMixerGroup Group(Channel channel) => Mixer != null ? _groups[(int)channel] : null;

        public static void Route(AudioSource source, Channel channel)
        {
            if (source != null) source.outputAudioMixerGroup = Group(channel);
        }

        // called every frame by the dialogue director
        public static void UpdateDuck(bool speaking, float deltaTime)
        {
            float target = speaking ? 1.0f : 0.0f;
            if (_duck == target) return;
            _duck = Mathf.MoveTowards(_duck, target, deltaTime * 1.5f);
            if (Mixer != null) _mixer.SetFloat(VolumeParameter("Ambient"), AmbientDuckDb * _duck);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = false;
            _mixer = null;
            _duck = 0.0f;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // sources placed in the scenes (weapons, doors, props) that no system routes: effects
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Mixer == null) return;
            if (mode == LoadSceneMode.Single) UpdateDuckNow(0.0f);
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                if (source.outputAudioMixerGroup == null) source.outputAudioMixerGroup = Group(Channel.Sfx);
        }

        private static void UpdateDuckNow(float value)
        {
            _duck = value;
            if (Mixer != null) _mixer.SetFloat(VolumeParameter("Ambient"), AmbientDuckDb * _duck);
        }
    }
}
