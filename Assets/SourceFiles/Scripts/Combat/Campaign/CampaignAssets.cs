using System;
using UnityEngine;

namespace Combat
{
    // Sounds and materials shared by the campaign systems, assigned once by the setup
    public class CampaignAssets : MonoBehaviour
    {
        public static CampaignAssets Instance { get; private set; }

        [Header("Throwables")]
        public AudioClip[] Explosions;
        public AudioClip PinPull;
        public AudioClip ThrowWhoosh;
        public AudioClip MolotovBreak;
        public AudioClip FireLoop;
        public AudioClip FlareLoop;
        public AudioClip DecoyBeep;
        public AudioClip MedkitUse;
        public Material FireMaterial;
        public Material FlareMaterial;

        [Header("Barricades")]
        public AudioClip BarricadeBuild;
        public AudioClip BarricadeHit;
        public AudioClip BarricadeBreak;

        [Header("Missions and shop")]
        public AudioClip ObjectiveDone;
        public AudioClip MissionDone;
        public AudioClip RadioBlip;
        public AudioClip ShopBuy;
        public AudioClip ShopDeny;
        public AudioClip Achievement;
        public AudioClip Helicopter;

        [Header("Bosses")]
        public AudioClip BossRoar;
        public AudioClip BossCharge;
        public AudioClip BossSlam;
        public AudioClip BossStunned;
        public AudioClip BossDeath;
        public AudioClip Shockwave;
        public AudioClip BossMusic;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static AudioClip Clip(Func<CampaignAssets, AudioClip> pick)
        {
            return Instance != null ? pick(Instance) : null;
        }

        public static void Play(Func<CampaignAssets, AudioClip> pick, Vector3 position, float volume = 1.0f, float maxDistance = 60.0f)
        {
            AudioClip clip = Clip(pick);
            if (clip != null && CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(clip, position, volume, 0.05f, 1.0f, 3.0f, maxDistance);
        }

        public static void PlayUI(Func<CampaignAssets, AudioClip> pick, float volume = 0.7f)
        {
            AudioClip clip = Clip(pick);
            if (clip != null && CombatEffects.Instance != null) CombatEffects.Instance.PlayUI(clip, volume);
        }
    }
}
