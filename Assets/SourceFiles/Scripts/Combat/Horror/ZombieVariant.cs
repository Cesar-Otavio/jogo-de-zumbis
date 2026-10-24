using UnityEngine;

namespace Combat
{
    public enum ZombieKind
    {
        Common,
        Runner,
        Tank,
        Screamer,
        Crawler
    }

    /* Everything that makes one kind of infected different, applied on top of the common zombie
     * (multipliers, so wave and difficulty scaling still work). New kinds are new assets.
     */
    [CreateAssetMenu(menuName = "Combat/Zombie Variant", fileName = "ZombieVariant")]
    public class ZombieVariant : ScriptableObject
    {
        public ZombieKind Kind;
        public string DisplayName = "Zumbi";
        [Tooltip("Extra points for killing one, on top of the zombie points")]
        public int BonusPoints;

        [Header("Body")]
        public float Scale = 1.0f;
        [Tooltip("Multiplies the skin color")]
        public Color Tint = Color.white;
        [Tooltip("Animator for this kind (e.g. running or crawling), empty keeps the common one")]
        public RuntimeAnimatorController Animator;
        [Tooltip("Ground speed at which the move animation plays at normal speed")]
        public float MoveAnimationSpeed = 0.9f;
        [Tooltip("Height of the hit capsule and eyes, e.g. low for crawlers")]
        public float Height = 1.8f;

        [Header("Stats (multipliers)")]
        public float Health = 1.0f;
        public float WanderSpeed = 1.0f;
        public float ChaseSpeed = 1.0f;
        public float Damage = 1.0f;
        public float AttackCooldown = 1.0f;
        [Tooltip("Seconds of wind-up before the blow lands; a long one is the tell for heavy attacks")]
        public float AttackHitTime = 0.6f;
        public float AttackRange = 1.5f;
        [Tooltip("Damage multiplier on the head")]
        public float HeadMultiplier = 1.0f;

        [Header("Senses")]
        public float SightRange = 22.0f;
        public float Hearing = 1.0f;
        public float DetectionSpeed = 1.6f;

        [Header("Behaviour")]
        [Range(0, 1)] [Tooltip("1 = can't be shoved or staggered")]
        public float ShoveResistance;
        [Tooltip("Breaks doors and barricades that block the way")]
        public bool BreaksDoors;
        public float DoorDamage = 10.0f;
        [Tooltip("Hides in dark corners and waits instead of wandering")]
        public bool Ambusher;

        [Header("Audio")]
        public AudioClip[] Groans;
        [Tooltip("When it spots its prey, so the player can tell what is coming")]
        public AudioClip DetectSound;
        [Tooltip("Played as the wind-up of the attack starts")]
        public AudioClip WindupSound;
        [Tooltip("Pitch of its voice, lower is bigger")]
        public float VoicePitch = 1.0f;
    }
}
