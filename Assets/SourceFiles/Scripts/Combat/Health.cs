using System;
using UnityEngine;

namespace Combat
{
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public float Force;
        public GameObject Instigator;
        public Rigidbody HitBody;
        public bool Headshot;
    }

    public class Health : MonoBehaviour
    {
        public float MaxHealth = 100.0f;

        [Tooltip("0 = player, 1 = enemies. Damage between members of the same team is ignored")]
        public int Team;

        [Tooltip("Seconds without taking damage before regenerating")]
        public float RegenDelay;

        [Tooltip("Health per second, 0 disables regeneration")]
        public float RegenRate;

        [Tooltip("Regeneration stops at this fraction of the maximum; beyond it only healing items help")]
        [Range(0, 1)] public float RegenCap = 1.0f;

        // e.g. a few seconds of protection after respawning
        public float InvulnerableUntil { get; set; }

        public float Current { get; private set; }
        public bool IsDead => Current <= 0.0f;
        public float Normalized => Current / MaxHealth;

        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;
        // any character dying, e.g. for missions and achievements
        public static event Action<Health, DamageInfo> AnyDied;
        // e.g. a boss in a vulnerable moment takes more damage
        [NonSerialized] public float DamageTakenMultiplier = 1.0f;
        // armour (a vest bought in the shop) soaks this fraction of the damage until it runs out
        [NonSerialized] public float Armor;
        [NonSerialized] public float ArmorAbsorb = 0.5f;
        // how much damage was taken in total, e.g. "a mission without taking damage"
        public float DamageTaken { get; private set; }

        // every character with health in the scene (check IsDead), e.g. for the threat arrows
        public static readonly System.Collections.Generic.List<Health> Living = new System.Collections.Generic.List<Health>();

        private float _lastDamageTime;

        private void OnEnable()
        {
            Living.Add(this);
        }

        private void OnDisable()
        {
            Living.Remove(this);
        }

        private void Awake()
        {
            Current = MaxHealth;
        }

        private void Update()
        {
            if (IsDead || RegenRate <= 0.0f || Current >= MaxHealth * RegenCap) return;
            if (Time.time - _lastDamageTime < RegenDelay) return;

            Current = Mathf.Min(MaxHealth * RegenCap, Current + RegenRate * Time.deltaTime);
        }

        // co-op: a copy of someone else's character (an infected shown on a client, a teammate's puppet)
        // hands the hit to whoever owns the real one instead of taking it here; true = handled
        [NonSerialized] public Func<DamageInfo, bool> Intercept;

        public void TakeDamage(DamageInfo info)
        {
            if (Intercept != null && Intercept(info)) return;
            if (IsDead || Time.time < InvulnerableUntil) return;

            if (info.Instigator != null && info.Instigator.TryGetComponent(out Health attacker) &&
                attacker != this && attacker.Team == Team) return;

            info.Amount *= DamageTakenMultiplier;
            if (Armor > 0.0f)
            {
                float soaked = Mathf.Min(Armor, info.Amount * ArmorAbsorb);
                Armor -= soaked;
                info.Amount -= soaked;
            }

            Current = Mathf.Max(0.0f, Current - info.Amount);
            DamageTaken += info.Amount;
            _lastDamageTime = Time.time;
            Damaged?.Invoke(info);

            if (IsDead)
            {
                Died?.Invoke(info);
                AnyDied?.Invoke(this, info);
            }
        }

        public void ResetHealth()
        {
            Current = MaxHealth;
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(MaxHealth, Current + amount);
        }

        // co-op copies show the health of the real character elsewhere, without any damage events
        public void Mirror(float current, float max)
        {
            MaxHealth = Mathf.Max(1.0f, max);
            Current = Mathf.Clamp(current, 0.0f, MaxHealth);
        }

        // e.g. tougher enemies on later waves
        public void SetMaxHealth(float maxHealth)
        {
            MaxHealth = maxHealth;
            Current = maxHealth;
        }
    }
}
