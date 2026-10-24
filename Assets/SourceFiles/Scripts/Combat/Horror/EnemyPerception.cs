using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public enum AwarenessState
    {
        Patrol,
        Investigate,
        Search,
        Chase,
        Attack,
        Return
    }

    /* Senses shared by every enemy: sight (field of view, distance, obstacles, darkness, crouching,
     * flashlight), hearing (NoiseSystem events) and memory (last known position, forgetting).
     * It picks a target among every hostile faction and drives the awareness state; the AI scripts
     * read the state and decide how to move and fight.
     */
    [RequireComponent(typeof(Health))]
    public class EnemyPerception : MonoBehaviour
    {
        [Header("Sight")]
        public float SightRange = 30.0f;
        public float FieldOfView = 120.0f;
        [Tooltip("Field of view once alerted (searching or chasing)")]
        public float AlertFieldOfView = 220.0f;
        public float EyeHeight = 1.6f;
        [Tooltip("Anything this close is noticed in any direction")]
        public float CloseRange = 2.2f;
        [Tooltip("Awareness gained per second at close range with the target fully visible")]
        public float DetectionSpeed = 2.0f;
        [Tooltip("Awareness lost per second while nothing is seen")]
        public float AwarenessDecay = 0.25f;

        [Header("Hearing")]
        public float HearingMultiplier = 1.0f;

        [Header("Memory")]
        [Tooltip("Seconds without seeing the target before giving chase up and searching")]
        public float LoseSightTime = 3.0f;
        [Tooltip("Right after losing sight, the target is still tracked for this long (momentum, not omniscience)")]
        public float TrackingAfterLoss = 0.8f;
        public float InvestigateTime = 10.0f;
        public float SearchTime = 12.0f;
        public float ReturnTime = 10.0f;
        public float PerceptionInterval = 0.2f;

        public AwarenessState State { get; private set; } = AwarenessState.Patrol;
        public Health Target { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public Vector3 InvestigatePosition { get; private set; }
        // a scream or an alarm: hurry there
        public bool InvestigateUrgent { get; private set; }
        public bool CanSeeTarget { get; private set; }
        public float StateTime => Time.time - _stateStarted;
        public float LastSeenTime { get; private set; } = -100.0f;
        // how close this enemy is to spotting the player, 0..1, for the stealth meter
        public float PlayerAwareness { get; private set; }
        public Vector3 Home { get; set; }

        public event Action<AwarenessState, AwarenessState> StateChanged;

        // every enemy with senses, e.g. for the HUD and the tension director
        public static readonly List<EnemyPerception> All = new List<EnemyPerception>();

        private Health _health;
        private float _nextPerception;
        private float _stateStarted;
        private bool _attacking;
        private readonly List<Candidate> _candidates = new List<Candidate>();

        private class Candidate
        {
            public Health Health;
            public float Awareness;
            public bool Seen;
            public float Distance;
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            Home = transform.position;
        }

        private void OnEnable()
        {
            All.Add(this);
            NoiseSystem.Emitted += OnNoise;
            // spread the checks of different enemies over different frames
            _nextPerception = Time.time + UnityEngine.Random.Range(0.0f, PerceptionInterval);
        }

        private void OnDisable()
        {
            All.Remove(this);
            NoiseSystem.Emitted -= OnNoise;
        }

        private void Start()
        {
            _health.Damaged += OnDamaged;
        }

        private void Update()
        {
            if (_health.IsDead) return;

            if (Time.time >= _nextPerception)
            {
                float dt = PerceptionInterval + (Time.time - _nextPerception);
                _nextPerception = Time.time + PerceptionInterval;
                Perceive(dt);
            }

            UpdateState();
        }

        // ---------------------------------------------------------------- sight

        private void Perceive(float dt)
        {
            Vector3 eye = transform.position + Vector3.up * EyeHeight;
            bool alert = State == AwarenessState.Chase || State == AwarenessState.Attack || State == AwarenessState.Search;
            float fov = alert ? AlertFieldOfView : FieldOfView;

            foreach (Health other in Health.Living)
            {
                if (other == _health || other.IsDead || !Factions.AreHostile(_health.Team, other.Team)) continue;

                float distance = Vector3.Distance(transform.position, other.transform.position);
                if (distance > SightRange * 1.5f) continue;

                Candidate candidate = GetCandidate(other, true);
                candidate.Distance = distance;
                candidate.Seen = false;

                var stealth = other.GetComponent<PerceptionTarget>();
                float visibility = stealth != null ? stealth.Visibility : 1.0f;
                float range = SightRange * visibility;
                if (distance <= range)
                {
                    Vector3 chest = other.transform.position + Vector3.up * (stealth != null ? stealth.ChestHeight : 1.2f);
                    Vector3 toTarget = chest - eye;
                    bool inView = distance <= CloseRange || Vector3.Angle(transform.forward, toTarget) <= fov * 0.5f;
                    candidate.Seen = inView && HasLineOfSight(eye, chest, other.transform);
                }

                if (candidate.Seen)
                {
                    // fast up close and in the open, slow at the edge of sight or in the dark
                    float closeness = 1.0f - Mathf.Clamp01(distance / Mathf.Max(range, 0.1f));
                    float rate = DetectionSpeed * Mathf.Lerp(0.35f, 3.0f, closeness * closeness) * Mathf.Max(visibility, 0.3f);
                    if (alert) rate *= 2.0f;
                    candidate.Awareness = Mathf.Min(1.5f, candidate.Awareness + rate * dt);
                }
                else
                {
                    candidate.Awareness = Mathf.Max(0.0f, candidate.Awareness - AwarenessDecay * dt);
                }
            }

            // forget the dead and the ones that wandered far off
            _candidates.RemoveAll(c => c.Health == null || c.Health.IsDead || (c.Awareness <= 0.0f && c.Distance > SightRange * 1.5f));

            PlayerAwareness = 0.0f;
            foreach (Candidate c in _candidates)
            {
                if (c.Health.Team == Factions.Player) PlayerAwareness = Mathf.Max(PlayerAwareness, Mathf.Clamp01(c.Awareness));
            }

            ChooseTarget();
        }

        private Candidate GetCandidate(Health health, bool create)
        {
            foreach (Candidate c in _candidates)
            {
                if (c.Health == health) return c;
            }

            if (!create) return null;
            var candidate = new Candidate { Health = health };
            _candidates.Add(candidate);
            return candidate;
        }

        private static bool HasLineOfSight(Vector3 eye, Vector3 point, Transform target)
        {
            Vector3 direction = point - eye;
            float distance = direction.magnitude;
            if (distance < 0.01f) return true;
            if (Physics.Raycast(eye, direction / distance, out RaycastHit hit, distance, CombatLayers.Sight, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.transform.IsChildOf(target);
            }

            return true;
        }

        private void ChooseTarget()
        {
            Candidate current = Target != null ? GetCandidate(Target, false) : null;
            Candidate best = null;
            float bestScore = float.MinValue;
            foreach (Candidate c in _candidates)
            {
                if (!c.Seen || c.Awareness < 1.0f) continue;
                // nearest wins, the current target is kept unless something is clearly closer
                float score = -c.Distance;
                if (c == current) score += 6.0f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            if (best != null)
            {
                Target = best.Health;
                CanSeeTarget = true;
                LastKnownPosition = best.Health.transform.position;
                LastSeenTime = Time.time;
                if (State != AwarenessState.Chase && State != AwarenessState.Attack) SetState(AwarenessState.Chase);
                return;
            }

            CanSeeTarget = current != null && current.Seen;
            if (CanSeeTarget)
            {
                LastKnownPosition = Target.transform.position;
                LastSeenTime = Time.time;
                return;
            }

            // half noticed: something moved over there
            if (State == AwarenessState.Patrol || State == AwarenessState.Return)
            {
                foreach (Candidate c in _candidates)
                {
                    if (c.Seen && c.Awareness >= 0.4f)
                    {
                        Investigate(c.Health.transform.position, false, 1.5f);
                        break;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- states

        private void UpdateState()
        {
            switch (State)
            {
                case AwarenessState.Chase:
                case AwarenessState.Attack:
                    if (Target == null || Target.IsDead)
                    {
                        Target = null;
                        SetState(AwarenessState.Search);
                        break;
                    }

                    float unseen = Time.time - LastSeenTime;
                    // momentum: they keep heading where the target actually went for a moment
                    if (!CanSeeTarget && unseen < TrackingAfterLoss) LastKnownPosition = Target.transform.position;
                    if (unseen > LoseSightTime) SetState(AwarenessState.Search);
                    else if (State != (_attacking ? AwarenessState.Attack : AwarenessState.Chase)) SetState(_attacking ? AwarenessState.Attack : AwarenessState.Chase);
                    break;

                case AwarenessState.Investigate:
                    if (StateTime > InvestigateTime) SetState(AwarenessState.Search);
                    break;

                case AwarenessState.Search:
                    if (StateTime > SearchTime)
                    {
                        Target = null;
                        SetState(AwarenessState.Return);
                    }

                    break;

                case AwarenessState.Return:
                    if (StateTime > ReturnTime || (transform.position - Home).sqrMagnitude < 4.0f) SetState(AwarenessState.Patrol);
                    break;
            }
        }

        private void SetState(AwarenessState state)
        {
            if (state == State) return;
            AwarenessState previous = State;
            State = state;
            _stateStarted = Time.time;
            if (state != AwarenessState.Investigate) InvestigateUrgent = false;
            StateChanged?.Invoke(previous, state);
        }

        // the AI reports when it is in striking range, so the state reads Attack
        public void SetAttacking(bool attacking)
        {
            _attacking = attacking;
        }

        // the AI reached the point it was investigating: look around there
        public void ReachedInvestigatePoint()
        {
            if (State == AwarenessState.Investigate)
            {
                LastKnownPosition = InvestigatePosition;
                SetState(AwarenessState.Search);
            }
        }

        public void Investigate(Vector3 position, bool urgent, float error)
        {
            if (State == AwarenessState.Chase || State == AwarenessState.Attack) return;

            // hearing isn't exact: the farther and fainter, the rougher the guess
            Vector2 offset = UnityEngine.Random.insideUnitCircle * error;
            InvestigatePosition = position + new Vector3(offset.x, 0.0f, offset.y);
            InvestigateUrgent = urgent || InvestigateUrgent && State == AwarenessState.Investigate;
            if (State == AwarenessState.Investigate) _stateStarted = Time.time;
            else SetState(AwarenessState.Investigate);
        }

        // e.g. a screamer calling the horde: chase straight to where the prey is
        public void Alert(Health target, Vector3 position)
        {
            if (target != null && !target.IsDead && Factions.AreHostile(_health.Team, target.Team))
            {
                Candidate candidate = GetCandidate(target, true);
                candidate.Awareness = Mathf.Max(candidate.Awareness, 0.9f);
            }

            Investigate(position, true, 3.0f);
        }

        // give up on everything, e.g. after respawning the target elsewhere
        public void Calm()
        {
            Target = null;
            _candidates.Clear();
            SetState(AwarenessState.Return);
        }

        // ---------------------------------------------------------------- hearing and pain

        private void OnNoise(NoiseEvent noise)
        {
            if (_health.IsDead || noise.Source == gameObject) return;
            // friends' noises don't alarm, the environment and enemies do
            if (noise.Team >= 0 && !Factions.AreHostile(_health.Team, noise.Team)) return;

            float distance = Vector3.Distance(transform.position, noise.Position);
            float radius = noise.Radius * HearingMultiplier;
            if (distance > radius) return;

            // a noise from the current target gives its position away
            if (Target != null && noise.Source != null && noise.Source.transform.IsChildOf(Target.transform))
            {
                if (State == AwarenessState.Chase || State == AwarenessState.Attack || State == AwarenessState.Search)
                {
                    LastKnownPosition = noise.Position;
                    if (State == AwarenessState.Search)
                    {
                        _stateStarted = Time.time;
                    }

                    return;
                }
            }

            if (State == AwarenessState.Chase || State == AwarenessState.Attack) return;

            float faintness = distance / Mathf.Max(radius, 0.1f);
            bool urgent = noise.Category == NoiseCategory.Scream || noise.Category == NoiseCategory.Alarm ||
                          noise.Category == NoiseCategory.Gunshot || noise.Category == NoiseCategory.Explosion;
            Investigate(noise.Position, urgent, Mathf.Lerp(0.5f, 4.0f, faintness));
        }

        private void OnDamaged(DamageInfo info)
        {
            if (_health.IsDead || info.Instigator == null) return;
            if (!info.Instigator.TryGetComponent(out Health attacker) || !Factions.AreHostile(_health.Team, attacker.Team)) return;

            // pain points straight at the attacker
            Candidate candidate = GetCandidate(attacker, true);
            candidate.Awareness = 1.5f;
            candidate.Seen = true;
            Target = attacker;
            LastKnownPosition = attacker.transform.position;
            LastSeenTime = Time.time;
            if (State != AwarenessState.Chase && State != AwarenessState.Attack) SetState(AwarenessState.Chase);
        }
    }
}
