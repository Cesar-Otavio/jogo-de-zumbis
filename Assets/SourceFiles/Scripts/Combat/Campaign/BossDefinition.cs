using UnityEngine;

namespace Combat
{
    /* A boss as data, on top of the infected body. Its difficulty comes from its moves and windows
     * of weakness rather than a mountain of health: a telegraphed charge that leaves it stunned and
     * vulnerable if it crashes into something, heavy blows with a long wind-up, and for the final
     * boss phases, a shockwave, summoned runners and a weakness to light.
     */
    [CreateAssetMenu(menuName = "Combat/Boss", fileName = "Boss_")]
    public class BossDefinition : ScriptableObject
    {
        public string Id = "boss";
        public string DisplayName = "Chefe";
        public string Title = "";

        [Header("Body")]
        public float MaxHealth = 1600.0f;
        public float Scale = 1.7f;
        public Color Tint = new Color(0.7f, 0.45f, 0.45f);
        public RuntimeAnimatorController Animator;
        [Tooltip("Ground speed at which the move animation plays at normal speed")]
        public float MoveAnimationSpeed = 5.0f;
        [Tooltip("Damage multiplier on the head")]
        public float HeadMultiplier = 1.5f;

        [Header("Movement")]
        public float ChaseSpeed = 2.6f;

        [Header("Blows")]
        public float MeleeDamage = 35.0f;
        public float MeleeRange = 2.6f;
        [Tooltip("Seconds of wind-up: the swing is slow and readable")]
        public float MeleeWindup = 0.9f;
        public float MeleeCooldown = 2.0f;

        [Header("Charge")]
        public bool CanCharge = true;
        public float ChargeWindup = 1.3f;
        public float ChargeSpeed = 11.0f;
        public float ChargeDuration = 1.6f;
        public float ChargeDamage = 40.0f;
        public float ChargeCooldown = 7.0f;
        public float ChargeMinRange = 6.0f;
        public float ChargeMaxRange = 26.0f;
        [Tooltip("Stunned after crashing into something solid")]
        public float StunTime = 3.5f;
        [Tooltip("Damage multiplier while stunned")]
        public float VulnerableMultiplier = 2.5f;

        [Header("Final boss")]
        [Tooltip("Health fractions where the next phase starts (faster, new attacks, summons)")]
        public float[] Phases = { };
        public bool Shockwave;
        public float ShockwaveRadius = 7.0f;
        public float ShockwaveDamage = 35.0f;
        public float ShockwaveWindup = 1.5f;
        public float ShockwaveCooldown = 9.0f;
        [Tooltip("Infected called in at each phase change")]
        public int SummonCount;
        public ZombieVariant SummonVariant;
        [Tooltip("Takes more damage and slows down under lamps and flares")]
        public bool WeakToLight;
        public float LightMultiplier = 1.8f;

        [Header("Rewards")]
        public int Credits = 500;
        public int Experience = 400;
        public int Score = 2500;
        public string Achievement;
    }
}
