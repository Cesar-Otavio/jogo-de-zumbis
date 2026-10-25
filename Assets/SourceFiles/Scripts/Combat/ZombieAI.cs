using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Combat
{
    /* Infected that shambles around, investigates what it hears, chases whatever hostile it spots
     * (the player or soldiers) and attacks up close. Senses and memory live in EnemyPerception;
     * this script moves and fights. A ZombieVariant turns it into a runner, tank, screamer or crawler.
     * Uses its own animator: Speed (float), Attack and Hit (triggers), Dead (bool).
     */
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(EnemyPerception))]
    public class ZombieAI : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Ground speed of the walk animation at normal playback speed")]
        public float WalkAnimationSpeed = 0.9f;
        public float WanderSpeed = 0.7f;
        public float InvestigateSpeed = 1.0f;
        public float ChaseSpeed = 1.4f;
        public float WanderRadius = 8.0f;
        [Tooltip("Wandering drifts towards the player's rough area, so big maps don't stay empty")]
        public bool HuntPlayer = true;
        [Tooltip("The area is a guess this many meters off, refreshed now and then: never the exact position")]
        public float HuntFuzz = 14.0f;
        public float HuntRefresh = 12.0f;

        [Header("Attack")]
        public float AttackRange = 1.5f;
        public float AttackDamage = 18.0f;
        public float AttackCooldown = 1.8f;
        [Tooltip("Seconds into the attack animation when the blow lands")]
        public float AttackHitTime = 0.6f;

        [Header("Death")]
        [Tooltip("Hits stronger than this throw the body as a ragdoll instead of playing the death animation")]
        public float RagdollForce = 8.0f;
        public float CorpseTime = 7.0f;

        [Header("Audio")]
        public AudioClip[] GroanSounds;
        public AudioClip AttackSound;
        public AudioClip[] HurtSounds;
        public AudioClip DeathSound;

        [Header("Variant")]
        [Tooltip("Optional: what kind of infected this is; the spawner can also assign one")]
        public ZombieVariant Variant;

        // an infected spotted the player: for warnings in the HUD
        public static event Action<ZombieAI> SpottedPlayer;

        // stand still until then, e.g. a screamer filling its lungs
        [System.NonSerialized] public float HoldUntil;

        public ZombieVariant AppliedVariant { get; private set; }
        // co-op: what a copy on another machine needs to look the same (attacks started, the animator's speed)
        public int AttackCount { get; private set; }
        public float AnimationSpeed => _animator != null ? _animator.GetFloat(_animIDSpeed) : 0.0f;
        public ZombieKind Kind => AppliedVariant != null ? AppliedVariant.Kind : ZombieKind.Common;
        public bool IsDead => _dead;
        public bool IsStaggered => Time.time < _staggerUntil;
        public bool IsAttacking => Time.time - _attackStarted < AttackCooldown * 0.8f;
        public EnemyPerception Perception => _perception;

        private NavMeshAgent _agent;
        private Animator _animator;
        private Health _health;
        private RagdollController _ragdoll;
        private EnemyPerception _perception;
        private Transform _player;

        private bool _dead;
        private float _deathTime;
        private float _nextRepath;
        private float _nextGroan;
        private float _attackStarted = -100.0f;
        private bool _attackLanded = true;
        private Health _attackTarget;
        private IBreakable _attackDoor;
        private float _staggerUntil;
        private Vector3 _knockback;
        private float _waitUntil;
        private Vector3 _huntGuess;
        private float _nextHuntGuess;
        private Vector3 _ambushSpot;
        private bool _hasAmbushSpot;
        private float _nextDoorCheck;
        // the door or barricade sealing the way when no path gets through
        private IBreakable _gate;
        private float _gateUntil;
        private float _voicePitch = 1.0f;
        private bool _slowSwing;

        private int _animIDSpeed;
        private int _animIDAttack;
        private int _animIDHit;
        private int _animIDDead;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _health = GetComponent<Health>();
            _ragdoll = GetComponentInChildren<RagdollController>();
            _perception = GetComponent<EnemyPerception>();

            _agent.updateRotation = false;
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDAttack = Animator.StringToHash("Attack");
            _animIDHit = Animator.StringToHash("Hit");
            _animIDDead = Animator.StringToHash("Dead");
            _health.Team = Factions.Infected;

            if (Variant != null) ApplyVariant(Variant);
        }

        private void Start()
        {
            _perception.Home = transform.position;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            _perception.StateChanged += OnAwarenessChanged;
            _nextGroan = Time.time + Random.Range(1.0f, 5.0f);

            var player = FindAnyObjectByType<PlayerCombat>();
            if (player != null) _player = player.transform;

            // each zombie walks a little differently
            if (_animator != null) _animator.SetFloat("CycleOffset", Random.value);
        }

        // ---------------------------------------------------------------- variants

        public void ApplyVariant(ZombieVariant variant)
        {
            if (variant == null || AppliedVariant != null) return;
            AppliedVariant = variant;

            transform.localScale *= variant.Scale;
            _health.SetMaxHealth(_health.MaxHealth * variant.Health);
            WanderSpeed *= variant.WanderSpeed;
            InvestigateSpeed *= Mathf.Lerp(1.0f, variant.ChaseSpeed, 0.5f);
            ChaseSpeed *= variant.ChaseSpeed;
            AttackDamage *= variant.Damage;
            AttackCooldown *= variant.AttackCooldown;
            AttackHitTime = variant.AttackHitTime;
            AttackRange = variant.AttackRange;
            WalkAnimationSpeed = variant.MoveAnimationSpeed;
            _voicePitch = variant.VoicePitch;
            if (variant.Groans != null && variant.Groans.Length > 0) GroanSounds = variant.Groans;

            var perception = GetComponent<EnemyPerception>();
            perception.SightRange = variant.SightRange;
            perception.HearingMultiplier = variant.Hearing;
            perception.DetectionSpeed = variant.DetectionSpeed;
            perception.EyeHeight = variant.Height * 0.9f;

            var agent = GetComponent<NavMeshAgent>();
            // the agent is scaled with the body, its height is in local units
            agent.height = variant.Height / Mathf.Max(variant.Scale, 0.1f);

            if (variant.Animator != null && _animator != null) _animator.runtimeAnimatorController = variant.Animator;

            foreach (Hitbox hitbox in GetComponentsInChildren<Hitbox>(true))
            {
                if (hitbox.IsHead) hitbox.Multiplier *= variant.HeadMultiplier;
            }

            if (variant.Tint != Color.white) Tint(variant.Tint);
            if (variant.Kind == ZombieKind.Screamer && GetComponent<ScreamerBehaviour>() == null) gameObject.AddComponent<ScreamerBehaviour>();
        }

        private void Tint(Color tint)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer body in GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = body.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    string property = materials[i].HasProperty("_BaseColor") ? "_BaseColor" : materials[i].HasProperty("_Color") ? "_Color" : null;
                    if (property == null) continue;
                    body.GetPropertyBlock(block, i);
                    block.SetColor(property, materials[i].GetColor(property) * tint);
                    body.SetPropertyBlock(block, i);
                }
            }
        }

        // ---------------------------------------------------------------- update

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.ZombieAI.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (_dead)
            {
                float t = Time.time - _deathTime;
                if (t > CorpseTime - 1.5f)
                {
                    if (_ragdoll != null && _ragdoll.IsRagdoll) _ragdoll.Freeze();
                    transform.position += Vector3.down * Time.deltaTime * 0.5f;
                }

                if (t > CorpseTime) Destroy(gameObject);
                return;
            }

            UpdateGroans();

            bool attacking = IsAttacking;
            bool staggered = IsStaggered;
            if (_knockback.sqrMagnitude > 0.01f && _agent.isOnNavMesh)
            {
                _agent.Move(_knockback * Time.deltaTime);
                _knockback = Vector3.MoveTowards(_knockback, Vector3.zero, Time.deltaTime * 10.0f);
            }

            if (Time.time < HoldUntil)
            {
                _agent.isStopped = true;
                if (_perception.Target != null) Face(_perception.Target.transform.position, 180.0f);
                UpdateAnimator();
                return;
            }

            switch (_perception.State)
            {
                case AwarenessState.Chase:
                case AwarenessState.Attack:
                    if (_perception.Target != null) Chase(attacking, staggered);
                    else Search(staggered);
                    break;
                case AwarenessState.Investigate:
                    Investigate(staggered);
                    break;
                case AwarenessState.Search:
                    Search(staggered);
                    break;
                case AwarenessState.Return:
                    MoveTo(_perception.Home, WanderSpeed, staggered);
                    FaceMovement();
                    break;
                default:
                    if (AppliedVariant != null && AppliedVariant.Ambusher) Ambush(staggered);
                    else Wander(staggered);
                    break;
            }

            if (!attacking) _perception.SetAttacking(false);
            UpdateAttack();
            UpdateAnimator();
        }

        private void UpdateGroans()
        {
            if (Time.time < _nextGroan) return;

            bool hunting = _perception.State == AwarenessState.Chase || _perception.State == AwarenessState.Attack;
            // ambushers keep quiet until they strike
            bool quiet = AppliedVariant != null && AppliedVariant.Ambusher && !hunting;
            _nextGroan = Time.time + Random.Range(hunting ? 2.5f : 5.0f, hunting ? 5.0f : 10.0f) * (quiet ? 2.5f : 1.0f);
            if (CombatEffects.Instance != null && GroanSounds != null && GroanSounds.Length > 0)
            {
                AudioClip clip = GroanSounds[Random.Range(0, GroanSounds.Length)];
                CombatEffects.Instance.PlaySound(clip, transform.position + Vector3.up * 1.6f, quiet ? 0.35f : 0.8f, 0.08f, 1.0f, 2.0f, 35.0f, _voicePitch);
            }
        }

        private void Chase(bool attacking, bool staggered)
        {
            Health target = _perception.Target;
            Vector3 targetPosition = _perception.CanSeeTarget ? target.transform.position : _perception.LastKnownPosition;
            float distance = Vector3.Distance(transform.position, targetPosition);

            if (attacking || staggered)
            {
                _agent.isStopped = true;
                if (attacking) Face(_attackDoor != null ? _attackDoor.Position : targetPosition, 240.0f);
                return;
            }

            if (_perception.CanSeeTarget && distance <= AttackRange)
            {
                _agent.isStopped = true;
                Face(targetPosition, 360.0f);

                Vector3 flat = targetPosition - transform.position;
                flat.y = 0.0f;
                if (Time.time - _attackStarted >= AttackCooldown && Vector3.Angle(transform.forward, flat) < 30.0f)
                {
                    StartAttack(target, null);
                }

                return;
            }

            if (BatterDoor(targetPosition)) return;

            _agent.isStopped = false;
            _agent.speed = ChaseSpeed;
            if (Time.time >= _nextRepath && _agent.isOnNavMesh)
            {
                _nextRepath = Time.time + 0.25f;
                _agent.SetDestination(targetPosition);
            }

            FaceMovement();
        }

        // a closed door between the zombie and where it wants to go gets battered
        private bool BatterDoor(Vector3 toward)
        {
            if (_gate != null && (!_gate.BlocksPath || Time.time > _gateUntil)) _gate = null;
            if (Time.time < _nextDoorCheck) return _gate != null;
            _nextDoorCheck = Time.time + 0.4f;

            IBreakable obstacle = Breakables.Blocking(transform.position, _gate != null ? _gate.Position : toward, 2.0f);
            // the path stops short (a closed door or barricade seals the only way in): go to it and break through
            if (obstacle == null && _gate == null && _agent.isOnNavMesh && !_agent.pathPending && _agent.hasPath && _agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                _gate = Breakables.Gateway(transform.position, toward, 12.0f);
                _gateUntil = Time.time + 15.0f;
            }

            if (obstacle == null)
            {
                if (_gate == null || !_agent.isOnNavMesh) return false;
                _agent.isStopped = false;
                _agent.speed = ChaseSpeed;
                _agent.SetDestination(_gate.Position);
                FaceMovement();
                return true;
            }

            if (Time.time - _attackStarted < AttackCooldown) return false;
            _agent.isStopped = true;
            StartAttack(null, obstacle);
            return true;
        }

        private void Investigate(bool staggered)
        {
            Vector3 point = _perception.InvestigatePosition;
            if (IsAttacking) return;
            if (BatterDoor(point))
            {
                // banging on it keeps it busy here instead of giving up
                _perception.Investigate(point, _perception.InvestigateUrgent, 0.0f);
                return;
            }

            MoveTo(point, _perception.InvestigateUrgent ? ChaseSpeed : InvestigateSpeed, staggered);
            FaceMovement();

            Vector3 flat = point - transform.position;
            flat.y = 0.0f;
            // close enough, or the path ran out (the spot itself may be unreachable)
            bool arrived = flat.sqrMagnitude < 2.5f * 2.5f || (_perception.StateTime > 1.5f && _agent.isOnNavMesh && !_agent.pathPending && !_agent.hasPath);
            if (arrived) _perception.ReachedInvestigatePoint();
        }

        // shuffle around where the prey was last seen or heard, pausing to listen
        private void Search(bool staggered)
        {
            if (IsAttacking || BatterDoor(_perception.LastKnownPosition)) return;
            _agent.isStopped = staggered;
            _agent.speed = InvestigateSpeed;
            if (_agent.pathPending || !_agent.isOnNavMesh) return;

            if (_agent.remainingDistance <= 0.6f)
            {
                if (_waitUntil <= 0.0f)
                {
                    _waitUntil = Time.time + Random.Range(1.0f, 2.5f);
                }
                else if (Time.time >= _waitUntil)
                {
                    _waitUntil = 0.0f;
                    Vector3 target = _perception.LastKnownPosition + Random.insideUnitSphere * 6.0f;
                    if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3.0f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
                }
            }

            FaceMovement();
        }

        private void Wander(bool staggered)
        {
            _agent.isStopped = staggered;
            // shuffle faster while the player is far away
            bool hunt = HuntPlayer && TensionDirector.HuntPressure > 0.05f && _player != null;
            bool far = hunt && (_player.position - transform.position).sqrMagnitude > 30.0f * 30.0f;
            _agent.speed = far ? Mathf.Lerp(WanderSpeed, ChaseSpeed, TensionDirector.HuntPressure) : WanderSpeed;
            if (_agent.pathPending || !_agent.isOnNavMesh) return;

            if (_agent.remainingDistance <= 0.5f)
            {
                if (_waitUntil <= 0.0f)
                {
                    _waitUntil = Time.time + Random.Range(2.0f, 6.0f);
                }
                else if (Time.time >= _waitUntil)
                {
                    _waitUntil = 0.0f;
                    // drift towards a rough guess of where the living are, refreshed now and then
                    if (hunt && Time.time >= _nextHuntGuess)
                    {
                        _nextHuntGuess = Time.time + HuntRefresh * Random.Range(0.7f, 1.3f);
                        Vector2 fuzz = Random.insideUnitCircle * HuntFuzz;
                        _huntGuess = _player.position + new Vector3(fuzz.x, 0.0f, fuzz.y);
                    }

                    bool drift = hunt && Random.value < TensionDirector.HuntPressure;
                    Vector3 target = drift
                        ? Vector3.MoveTowards(transform.position, _huntGuess, 20.0f) + Random.insideUnitSphere * WanderRadius
                        : _perception.Home + Random.insideUnitSphere * WanderRadius;
                    if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3.0f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
                }
            }

            FaceMovement();
        }

        // crawlers find a dark spot and lie in wait there
        private void Ambush(bool staggered)
        {
            if (!_hasAmbushSpot)
            {
                _hasAmbushSpot = true;
                _ambushSpot = transform.position;
                for (int i = 0; i < 12; i++)
                {
                    Vector3 candidate = _perception.Home + Random.insideUnitSphere * 12.0f;
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3.0f, NavMesh.AllAreas)) continue;
                    if (LightFlicker.IsLit(hit.position + Vector3.up)) continue;
                    _ambushSpot = hit.position;
                    break;
                }
            }

            MoveTo(_ambushSpot, WanderSpeed, staggered);
            FaceMovement();
        }

        private void MoveTo(Vector3 point, float speed, bool staggered)
        {
            _agent.isStopped = staggered;
            _agent.speed = speed;
            if (!_agent.isOnNavMesh || Time.time < _nextRepath) return;
            _nextRepath = Time.time + 0.5f;
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 3.0f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
        }

        // ---------------------------------------------------------------- attack

        private void StartAttack(Health target, IBreakable door)
        {
            AttackCount++;
            _attackStarted = Time.time;
            _attackLanded = false;
            _attackTarget = target;
            _attackDoor = door;
            _perception.SetAttacking(target != null);

            // slow, heavy blows play the swing slower: the long wind-up is the warning
            if (_animator != null)
            {
                _animator.speed = Mathf.Clamp(0.6f / Mathf.Max(AttackHitTime, 0.1f), 0.45f, 1.2f);
                _slowSwing = true;
                _animator.SetTrigger(_animIDAttack);
            }

            CombatEffects fx = CombatEffects.Instance;
            if (fx == null) return;
            if (AttackSound != null) fx.PlaySound(AttackSound, transform.position + Vector3.up * 1.6f, 0.9f, 0.05f, 1.0f, 2.0f, 40.0f, _voicePitch);
            if (AppliedVariant != null && AppliedVariant.WindupSound != null)
            {
                fx.PlaySound(AppliedVariant.WindupSound, transform.position + Vector3.up * 1.6f, 1.0f, 0.04f, 1.0f, 3.0f, 50.0f);
                Subtitles.Show("[rugido pesado]", transform.position, 1.5f);
            }
        }

        private void UpdateAttack()
        {
            if (_attackLanded) return;

            if (Time.time - _attackStarted < AttackHitTime) return;
            _attackLanded = true;
            RestoreAnimationSpeed();

            if (_attackDoor != null)
            {
                bool breaks = AppliedVariant != null && AppliedVariant.BreaksDoors;
                _attackDoor.Hit(AppliedVariant != null ? AppliedVariant.DoorDamage : 4.0f, breaks, gameObject);
                _attackDoor = null;
                return;
            }

            if (_attackTarget == null || _attackTarget.IsDead) return;

            // the blow only connects if the target is still in front and in reach
            Vector3 toTarget = _attackTarget.transform.position - transform.position;
            toTarget.y = 0.0f;
            if (toTarget.magnitude <= AttackRange + 0.4f && Vector3.Angle(transform.forward, toTarget) < 60.0f)
            {
                _attackTarget.TakeDamage(new DamageInfo
                {
                    Amount = AttackDamage,
                    Point = _attackTarget.transform.position + Vector3.up * 1.2f,
                    Direction = toTarget.normalized,
                    Force = Kind == ZombieKind.Tank ? 9.0f : 3.0f,
                    Instigator = gameObject
                });
            }
        }

        // a push from the player: staggers and knocks back, and interrupts a blow that hasn't landed
        public bool Shove(Vector3 direction, float strength)
        {
            if (_dead) return false;

            float resistance = AppliedVariant != null ? AppliedVariant.ShoveResistance : 0.0f;
            float effect = strength * (1.0f - resistance);
            // heavy ones (a tank resists 90%) don't budge
            if (effect < 0.2f) return false;

            if (!_attackLanded && effect > 0.5f)
            {
                _attackLanded = true;
                _attackDoor = null;
                RestoreAnimationSpeed();
            }

            if (TryGetComponent(out ScreamerBehaviour screamer)) screamer.Interrupt();

            _staggerUntil = Time.time + 0.4f + effect * 0.8f;
            direction.y = 0.0f;
            _knockback = direction.normalized * effect * 4.0f;
            if (_animator != null) _animator.SetTrigger(_animIDHit);
            return true;
        }

        // ---------------------------------------------------------------- events

        private void OnAwarenessChanged(AwarenessState previous, AwarenessState state)
        {
            bool hunting = state == AwarenessState.Chase || state == AwarenessState.Attack;
            bool wasHunting = previous == AwarenessState.Chase || previous == AwarenessState.Attack;
            if (!hunting || wasHunting) return;

            _nextGroan = Time.time + Random.Range(1.5f, 3.0f);
            CombatEffects fx = CombatEffects.Instance;
            if (fx != null && AppliedVariant != null && AppliedVariant.DetectSound != null)
            {
                fx.PlaySound(AppliedVariant.DetectSound, transform.position + Vector3.up * 1.5f, 1.0f, 0.05f, 1.0f, 4.0f, 60.0f);
            }

            if (_perception.Target != null && _perception.Target.Team == Factions.Player) SpottedPlayer?.Invoke(this);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (_health.IsDead) return;

            // big hits make the zombie stagger, the heavy ones barely flinch
            float resistance = AppliedVariant != null ? AppliedVariant.ShoveResistance : 0.0f;
            if (info.Amount >= 15.0f * (1.0f + resistance * 3.0f) && Time.time >= _staggerUntil)
            {
                _staggerUntil = Time.time + 0.45f * (1.0f - resistance);
                if (_animator != null) _animator.SetTrigger(_animIDHit);
            }

            if (CombatEffects.Instance != null && HurtSounds != null && HurtSounds.Length > 0 && Random.value < 0.5f)
            {
                AudioClip clip = HurtSounds[Random.Range(0, HurtSounds.Length)];
                CombatEffects.Instance.PlaySound(clip, transform.position + Vector3.up * 1.5f, 0.8f, 0.08f, 1.0f, 2.0f, 40.0f, _voicePitch);
            }
        }

        private void OnDied(DamageInfo info)
        {
            _dead = true;
            _deathTime = Time.time;
            _slowSwing = true;
            RestoreAnimationSpeed();
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.enabled = false;

            if (_ragdoll != null && info.Force >= RagdollForce)
            {
                _ragdoll.SetRagdoll(true);
                _ragdoll.Push(info.HitBody, info.Point, info.Direction * info.Force * 6.0f);
            }
            else if (_animator != null)
            {
                _animator.SetBool(_animIDDead, true);
            }

            if (CombatEffects.Instance != null && DeathSound != null)
            {
                CombatEffects.Instance.PlaySound(DeathSound, transform.position + Vector3.up * 1.2f, 1.0f, 0.05f, 1.0f, 2.0f, 50.0f, _voicePitch);
            }
        }

        private void RestoreAnimationSpeed()
        {
            if (!_slowSwing) return;
            _slowSwing = false;
            if (_animator != null) _animator.speed = 1.0f;
        }

        // ---------------------------------------------------------------- movement

        private void FaceMovement()
        {
            Vector3 velocity = _agent.velocity;
            velocity.y = 0.0f;
            if (velocity.sqrMagnitude > 0.02f) Face(transform.position + velocity, 200.0f);
        }

        private void Face(Vector3 point, float degreesPerSecond)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                degreesPerSecond * Time.deltaTime);
        }

        private void UpdateAnimator()
        {
            if (_animator == null || !_animator.enabled) return;

            float speed = _agent.velocity.magnitude / Mathf.Max(transform.localScale.y, 0.1f);
            _animator.SetFloat(_animIDSpeed, speed);
            // play the walk faster or slower so the feet match the ground speed
            _animator.SetFloat("WalkSpeedScale", Mathf.Max(0.5f, speed / Mathf.Max(WalkAnimationSpeed, 0.1f)));
        }
    }
}
