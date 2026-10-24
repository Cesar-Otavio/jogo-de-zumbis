using UnityEngine;
using UnityEngine.AI;

namespace Combat
{
    /* Soldier that patrols, investigates what its EnemyPerception hears or half sees, then fights
     * whatever hostile it spots (the player or the infected): it moves to a firing position, stops,
     * aims (getting more accurate the longer it aims) and shoots in bursts before repositioning again.
     */
    [DefaultExecutionOrder(20)]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(WeaponHolder))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(EnemyPerception))]
    public class EnemyShooterAI : MonoBehaviour
    {
        private enum State
        {
            Patrol,
            Combat,
            Search,
            Dead
        }

        [Header("Weapon")]
        public WeaponData[] Loadout;
        [Tooltip("Enemy bullets hurt less than the player's")]
        public float DamageMultiplier = 0.3f;
        [Tooltip("Damage multiplier against the infected and other non-player targets")]
        public float DamageMultiplierVsInfected = 0.8f;

        [Header("Senses")]
        public float EyeHeight = 1.6f;

        [Header("Movement")]
        public float WalkSpeed = 1.6f;
        public float RunSpeed = 4.0f;
        public float PatrolRadius = 10.0f;
        [Tooltip("Patrols drift towards a rough guess of the player's area, so big maps don't stay empty")]
        public bool HuntPlayer = true;
        public float HuntRadius = 15.0f;
        public float HuntRefresh = 15.0f;
        public float PreferredMinRange = 7.0f;
        public float PreferredMaxRange = 16.0f;

        [Header("Shooting")]
        public float ReactionTime = 0.8f;
        [Tooltip("Aim error in meters at 10 m, when starting to shoot and after aiming for a while")]
        public float AimErrorStart = 1.4f;
        public float AimErrorEnd = 0.35f;
        public float AccuracyRampTime = 2.5f;

        [Header("Death")]
        public float CorpseTime = 8.0f;
        public AudioClip[] HurtSounds;
        public AudioClip DeathSound;

        private NavMeshAgent _agent;
        private Animator _animator;
        private WeaponHolder _holder;
        private Health _health;
        private RagdollController _ragdoll;

        private EnemyPerception _perception;
        private Transform _player;
        private Health _playerHealth;
        // whoever the perception picked: the player or an infected
        private Transform _target;
        private Health _targetHealth;
        private Vector3 _huntGuess;
        private float _nextHuntGuess;
        private Vector3 _investigating;
        private Vector3 _knockback;

        private State _state = State.Patrol;
        private Vector3 _home;
        private Vector3 _lastKnown;
        private float _lastSeenTime = -100.0f;
        private bool _canSee;
        private float _nextPerception;
        private float _waitUntil;

        private bool _moving;
        private float _moveStarted;
        private float _engageStarted;
        private float _readyToShootAt;
        private int _burstsLeft;
        private float _burstEndsAt;
        private float _nextBurstAt;
        private bool _tapPending;
        private Vector3 _aimNoise;
        private float _deathTime;

        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDMotionSpeed;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
            _holder = GetComponent<WeaponHolder>();
            _health = GetComponent<Health>();
            _ragdoll = GetComponent<RagdollController>();
            _perception = GetComponent<EnemyPerception>();
            _health.Team = Factions.Soldiers;

            _agent.updateRotation = false;
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void Start()
        {
            _home = transform.position;
            _perception.Home = _home;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;

            var player = FindAnyObjectByType<PlayerCombat>();
            if (player != null)
            {
                _player = player.transform;
                _playerHealth = player.GetComponent<Health>();
            }

            _holder.DamageMultiplier = DamageMultiplier;
            _holder.DamageMultiplierVsAI = DamageMultiplierVsInfected;
            if (Loadout != null && Loadout.Length > 0)
            {
                WeaponData weapon = Loadout[Random.Range(0, Loadout.Length)];
                _holder.Equip(new WeaponInstance(weapon, weapon.MagazineSize, 0, true));
            }
        }

        private void Update()
        {
            if (_state == State.Dead)
            {
                // sink into the ground before disappearing
                float t = Time.time - _deathTime;
                if (t > CorpseTime - 1.5f)
                {
                    if (_ragdoll != null && _ragdoll.IsRagdoll) _ragdoll.Freeze();
                    transform.position += Vector3.down * Time.deltaTime * 0.6f;
                }

                if (t > CorpseTime) Destroy(gameObject);
                return;
            }

            if (_knockback.sqrMagnitude > 0.01f && _agent.isOnNavMesh)
            {
                _agent.Move(_knockback * Time.deltaTime);
                _knockback = Vector3.MoveTowards(_knockback, Vector3.zero, Time.deltaTime * 10.0f);
            }

            if (Time.time >= _nextPerception)
            {
                _nextPerception = Time.time + 0.1f;
                ReadPerception();
            }

            switch (_state)
            {
                case State.Patrol:
                    Patrol();
                    break;
                case State.Combat:
                    Combat();
                    break;
                case State.Search:
                    Search();
                    break;
            }

            UpdateAnimator();
        }

        // ---------------------------------------------------------------- senses

        private Vector3 TargetChest()
        {
            if (_target == null) return _lastKnown;
            var stealth = _targetHealth != null ? _targetHealth.GetComponent<PerceptionTarget>() : null;
            if (stealth != null) return _target.position + Vector3.up * stealth.ChestHeight;
            // the infected: about two thirds of their height (low for crawlers)
            var agent = _targetHealth != null ? _targetHealth.GetComponent<NavMeshAgent>() : null;
            float height = agent != null ? agent.height * _target.localScale.y : 1.8f;
            return _target.position + Vector3.up * height * 0.65f;
        }

        // the perception picks the target and the awareness state; this maps it onto the fighting states
        private void ReadPerception()
        {
            Health target = _perception.Target;
            _targetHealth = target;
            _target = target != null ? target.transform : null;
            _canSee = _perception.CanSeeTarget && _target != null;
            if (_canSee)
            {
                _lastKnown = TargetChest();
                _lastSeenTime = Time.time;
            }
            else if (_target != null)
            {
                _lastKnown = _perception.LastKnownPosition + Vector3.up * 1.2f;
            }

            switch (_perception.State)
            {
                case AwarenessState.Chase:
                case AwarenessState.Attack:
                    if (_state != State.Combat && _target != null) EnterCombat();
                    break;

                case AwarenessState.Investigate:
                    // walk (or run, for gunfire and alarms) to where the noise came from
                    Vector3 point = _perception.InvestigatePosition;
                    if (_state != State.Search || (point - _investigating).sqrMagnitude > 1.0f)
                    {
                        _state = State.Search;
                        _investigating = point;
                        _lastKnown = point + Vector3.up * 1.2f;
                        MoveTo(point, _perception.InvestigateUrgent ? RunSpeed : WalkSpeed * 1.3f);
                    }

                    if (!_agent.pathPending && _agent.remainingDistance <= 1.5f) _perception.ReachedInvestigatePoint();
                    break;

                case AwarenessState.Search:
                    if (_state != State.Search)
                    {
                        _state = State.Search;
                        MoveTo(_perception.LastKnownPosition, RunSpeed * 0.7f);
                    }

                    break;

                default:
                    if (_state != State.Patrol)
                    {
                        _state = State.Patrol;
                        _holder.TriggerHeld = false;
                        MoveTo(_home, WalkSpeed);
                    }

                    break;
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            _holder.Flinch(info.Direction);
            _readyToShootAt = Mathf.Max(_readyToShootAt, Time.time + 0.25f);

            if (CombatEffects.Instance != null && HurtSounds != null && HurtSounds.Length > 0 && Random.value < 0.6f)
            {
                CombatEffects.Instance.PlayRandom(HurtSounds, transform.position + Vector3.up * 1.5f, 0.8f);
            }
        }

        // ---------------------------------------------------------------- states

        private void EnterCombat()
        {
            _state = State.Combat;
            _moving = false;
            BeginEngage();
            if (CombatEffects.Instance != null)
            {
                CombatEffects.Instance.PlaySound(CombatEffects.Instance.AlertSound, transform.position + Vector3.up * 1.6f, 0.6f);
            }
        }

        private void Patrol()
        {
            _holder.Lowered = true;
            _holder.Aiming = false;
            _holder.TriggerHeld = false;
            _holder.AimPoint = transform.position + transform.forward * 10.0f + Vector3.up * 1.2f;

            if (_agent.pathPending) return;

            if (_agent.remainingDistance <= 0.5f)
            {
                if (_waitUntil <= 0.0f)
                {
                    _waitUntil = Time.time + Random.Range(1.5f, 4.0f);
                }
                else if (Time.time >= _waitUntil)
                {
                    _waitUntil = 0.0f;
                    bool hunt = HuntPlayer && _player != null && _playerHealth != null && !_playerHealth.IsDead &&
                                Random.value < TensionDirector.HuntPressure;
                    // a rough guess of the player's area, refreshed now and then: never the exact position
                    if (hunt && Time.time >= _nextHuntGuess)
                    {
                        _nextHuntGuess = Time.time + HuntRefresh * Random.Range(0.7f, 1.3f);
                        Vector2 fuzz = Random.insideUnitCircle * HuntRadius;
                        _huntGuess = _player.position + new Vector3(fuzz.x, 0.0f, fuzz.y);
                    }

                    Vector3 target = hunt
                        ? _huntGuess + Random.insideUnitSphere * PatrolRadius * 0.5f
                        : _home + Random.insideUnitSphere * PatrolRadius;
                    // jog while still far away, walk once close
                    float speed = hunt && Vector3.Distance(transform.position, _huntGuess) > 30.0f ? RunSpeed * 0.7f : WalkSpeed;
                    if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4.0f, NavMesh.AllAreas)) MoveTo(hit.position, speed);
                }
            }

            FaceMovement();
        }

        private void Search()
        {
            _holder.Lowered = false;
            _holder.Aiming = false;
            _holder.TriggerHeld = false;
            _holder.AimPoint = _lastKnown;

            if (!_agent.pathPending && _agent.remainingDistance <= 1.0f)
            {
                // look around; the perception decides when to give up
                transform.Rotate(0.0f, 60.0f * Time.deltaTime, 0.0f);
                _holder.AimPoint = transform.position + transform.forward * 10.0f + Vector3.up * 1.3f;
                if (_perception.State == AwarenessState.Search && _perception.StateTime > 3.0f && Random.value < Time.deltaTime * 0.5f)
                {
                    // check another spot nearby
                    Vector3 spot = _perception.LastKnownPosition + Random.insideUnitSphere * 8.0f;
                    if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 3.0f, NavMesh.AllAreas)) MoveTo(hit.position, WalkSpeed * 1.3f);
                }
            }
            else
            {
                FaceMovement();
            }
        }

        private void Combat()
        {
            _holder.Lowered = false;

            if (_target == null)
            {
                _holder.TriggerHeld = false;
                return;
            }

            // lost sight: push to where the target was until the perception gives up
            if (!_canSee && Time.time - _lastSeenTime > 1.5f)
            {
                _holder.TriggerHeld = false;
                _holder.Aiming = false;
                _holder.AimPoint = _lastKnown;
                if (!_moving || Time.time - _moveStarted > 2.0f)
                {
                    _moving = true;
                    _moveStarted = Time.time;
                    MoveTo(_perception.LastKnownPosition, RunSpeed);
                }

                FaceMovement();
                return;
            }

            if (_holder.Current != null && _holder.Current.Magazine == 0 && !_holder.IsReloading)
            {
                _holder.StartReload();
            }

            if (_moving)
            {
                _holder.Aiming = false;
                _holder.TriggerHeld = false;
                _holder.AimPoint = _lastKnown;
                FaceMovement();

                bool arrived = !_agent.pathPending && _agent.remainingDistance <= 0.6f;
                if (arrived || Time.time - _moveStarted > 4.0f) BeginEngage();
                return;
            }

            // engaging: stand, aim and shoot in bursts
            Vector3 target = _canSee ? TargetChest() : _lastKnown;
            FaceTowards(target, 400.0f);

            // accuracy improves while aiming and falls off with distance
            float aimed = Mathf.Clamp01((Time.time - _engageStarted) / AccuracyRampTime);
            float distanceFactor = Mathf.Clamp(Vector3.Distance(transform.position, target) / 10.0f, 0.6f, 2.5f);
            float error = Mathf.Lerp(AimErrorStart, AimErrorEnd, aimed) * distanceFactor;
            _aimNoise = Vector3.Lerp(_aimNoise, Random.insideUnitSphere, Time.deltaTime * 2.0f);
            _holder.Aiming = true;
            _holder.AimPoint = target + _aimNoise * error;

            UpdateBursts(target);

            if (_burstsLeft <= 0 && !_holder.IsReloading && Time.time >= _nextBurstAt)
            {
                Reposition();
            }
        }

        private void UpdateBursts(Vector3 target)
        {
            WeaponInstance weapon = _holder.Current;
            _holder.TriggerHeld = false;
            if (weapon == null || !_canSee || _holder.IsReloading) return;

            Vector3 flat = target - transform.position;
            flat.y = 0.0f;
            bool facing = Vector3.Angle(transform.forward, flat) < 15.0f;
            if (!facing || Time.time < _readyToShootAt) return;

            if (weapon.Data.Mode == FireMode.Auto)
            {
                if (Time.time < _burstEndsAt)
                {
                    _holder.TriggerHeld = true;
                }
                else if (Time.time >= _nextBurstAt && _burstsLeft > 0)
                {
                    _burstsLeft--;
                    _burstEndsAt = Time.time + Random.Range(0.25f, 0.7f);
                    _nextBurstAt = _burstEndsAt + Random.Range(0.4f, 1.0f);
                    _holder.TriggerHeld = true;
                }
            }
            else if (Time.time >= _nextBurstAt && _burstsLeft > 0)
            {
                // semi-auto: one tap per "burst", the press is released next frame
                if (_tapPending)
                {
                    _tapPending = false;
                    _burstsLeft--;
                    _nextBurstAt = Time.time + Random.Range(0.35f, 0.9f) + weapon.Data.FireInterval;
                }
                else
                {
                    _tapPending = true;
                    _holder.TriggerHeld = true;
                }
            }
        }

        private void BeginEngage()
        {
            _moving = false;
            _agent.ResetPath();
            _engageStarted = Time.time;
            _readyToShootAt = Time.time + ReactionTime * Random.Range(0.7f, 1.4f);
            _burstsLeft = Random.Range(2, 5);
            _nextBurstAt = _readyToShootAt;
            _burstEndsAt = 0.0f;
        }

        private void Reposition()
        {
            Vector3 destination = ChooseFiringPosition();
            _moving = true;
            _moveStarted = Time.time;
            MoveTo(destination, RunSpeed);
        }

        private Vector3 ChooseFiringPosition()
        {
            if (_target == null) return transform.position;

            Vector3 fromPlayer = transform.position - _target.position;
            fromPlayer.y = 0.0f;
            float distance = fromPlayer.magnitude;
            Vector3 direction = distance > 0.1f ? fromPlayer / distance : -_target.forward;

            Vector3 fallback = transform.position;
            for (int i = 0; i < 10; i++)
            {
                // flank around the player at a comfortable range
                float angle = Random.Range(-70.0f, 70.0f);
                float range = Random.Range(PreferredMinRange, PreferredMaxRange);
                Vector3 candidate = _target.position + Quaternion.Euler(0.0f, angle, 0.0f) * direction * range;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) continue;

                if (i == 0) fallback = hit.position;
                Vector3 eye = hit.position + Vector3.up * EyeHeight;
                Vector3 chest = TargetChest();
                if (!Physics.Linecast(eye, chest, out RaycastHit block, CombatLayers.Sight, QueryTriggerInteraction.Ignore) ||
                    block.collider.transform.IsChildOf(_target))
                {
                    return hit.position;
                }
            }

            return fallback;
        }

        // ---------------------------------------------------------------- movement

        private void MoveTo(Vector3 position, float speed)
        {
            if (!_agent.isOnNavMesh) return;
            _agent.speed = speed;
            _agent.SetDestination(position);
        }

        private void FaceMovement()
        {
            Vector3 velocity = _agent.velocity;
            velocity.y = 0.0f;
            if (velocity.sqrMagnitude > 0.05f) FaceTowards(transform.position + velocity, 360.0f);
        }

        private void FaceTowards(Vector3 point, float degreesPerSecond)
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

            Vector3 velocity = _agent.velocity;
            velocity.y = 0.0f;
            _animator.SetFloat(_animIDSpeed, velocity.magnitude);
            _animator.SetFloat(_animIDMotionSpeed, 1.0f);
            _animator.SetBool(_animIDGrounded, true);
            _holder.MoveSpeed01 = Mathf.Clamp01(velocity.magnitude / RunSpeed);
        }

        // a push from the player: knocked back, off balance and slower to shoot
        public bool Shove(Vector3 direction, float strength)
        {
            if (_state == State.Dead) return false;
            direction.y = 0.0f;
            _knockback = direction.normalized * strength * 3.5f;
            _holder.Flinch(direction);
            _holder.TriggerHeld = false;
            _readyToShootAt = Mathf.Max(_readyToShootAt, Time.time + 0.6f + strength * 0.6f);
            return true;
        }

        // ---------------------------------------------------------------- death

        private void OnDied(DamageInfo info)
        {
            _state = State.Dead;
            _deathTime = Time.time;
            _holder.TriggerHeld = false;
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.enabled = false;

            WeaponInstance weapon = _holder.Unequip();
            if (weapon != null)
            {
                // leave the gun behind with some ammo for the player
                var dropped = new WeaponInstance(weapon.Data, weapon.Data.MagazineSize, weapon.Data.MagazineSize);
                WeaponPickup.Drop(dropped, transform.position + Vector3.up * 1.2f + transform.forward * 0.3f,
                    transform.rotation * Quaternion.Euler(0.0f, 90.0f, 0.0f), info.Direction * 1.5f + Vector3.up * 1.0f);
            }

            if (_ragdoll != null)
            {
                _ragdoll.SetRagdoll(true);
                _ragdoll.Push(info.HitBody, info.Point, info.Direction * info.Force * 6.0f);
            }

            if (CombatEffects.Instance != null && DeathSound != null)
            {
                CombatEffects.Instance.PlaySound(DeathSound, transform.position + Vector3.up * 1.2f, 1.0f);
            }
        }

        // animation events from the shared locomotion clips
        private void OnFootstep(AnimationEvent animationEvent)
        {
        }

        private void OnLand(AnimationEvent animationEvent)
        {
        }
    }
}
