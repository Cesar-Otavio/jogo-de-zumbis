using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Combat
{
    /* A boss built on an infected body (the ZombieAI is switched off, the health, hitboxes,
     * ragdoll and footsteps are reused). Fights in readable moves:
     *   Chase -> Melee (slow wind-up) or Charge (roar, then a straight run).
     *   A charge that hits something solid stuns it: a few seconds of taking much more damage.
     *   Charges smash barricades and doors. Missing the player without crashing gives nothing.
     * Final boss extras: phases, a telegraphed shockwave you can jump over, summoned infected,
     * and weakness to light (lamps with power, flares).
     */
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class BossAI : MonoBehaviour
    {
        public enum Move
        {
            Intro,
            Chase,
            Melee,
            ChargeWindup,
            Charging,
            Recover,
            Stunned,
            ShockwaveWindup,
            Dead
        }

        public BossDefinition Definition;

        public static readonly List<BossAI> Active = new List<BossAI>();
        public static event Action<BossAI> Defeated;
        // telegraphs for the HUD: "INVESTIDA!", "ONDA DE CHOQUE!"
        public static event Action<BossAI, string> Warning;

        // the corpse stays in Active until it is cleared away; this asks about the fight itself
        public static bool AnyAlive
        {
            get
            {
                foreach (BossAI boss in Active)
                {
                    if (boss != null && boss.State != Move.Dead) return true;
                }

                return false;
            }
        }

        public Move State { get; private set; } = Move.Intro;
        public bool Vulnerable => State == Move.Stunned;
        public bool Lit { get; private set; }
        public int Phase { get; private set; }
        public Health Health => _health;

        private NavMeshAgent _agent;
        private Health _health;
        private Animator _animator;
        private RagdollController _ragdoll;
        // this machine's player (camera shake); the one being hunted is _target, which in co-op can be a
        // teammate's puppet (their hits go to them through the puppet's health)
        private PlayerCombat _player;
        private Health _target;
        private float _nextTargetPick;
        private float _stateStarted;
        private float _nextMelee;
        private float _nextCharge;
        private float _nextShockwave;
        private Vector3 _chargeDirection;
        private Vector3 _lastChargePosition;
        private bool _chargeHitPlayer;
        private bool _meleeLanded;
        private float _nextLightCheck;
        private float _nextSmash;
        private float _speedScale = 1.0f;
        private LineRenderer _ring;
        private MaterialPropertyBlock _block;
        private Color _tint;

        public static GameObject Create(BossDefinition definition, GameObject bodyPrefab, Vector3 position, DifficultySettings difficulty)
        {
            GameObject go = Instantiate(bodyPrefab, position, Quaternion.identity);
            go.name = definition.DisplayName;
            // the infected brain is replaced; its body, health and hitboxes stay
            var zombie = go.GetComponent<ZombieAI>();
            if (zombie != null) zombie.enabled = false;
            var perception = go.GetComponent<EnemyPerception>();
            if (perception != null) perception.enabled = false;

            go.transform.localScale *= definition.Scale;
            var health = go.GetComponent<Health>();
            // tougher on harder settings, but only a little: the moves make the difficulty
            health.SetMaxHealth(definition.MaxHealth * Mathf.Lerp(1.0f, difficulty.EnemyHealth, 0.5f));
            foreach (Hitbox hitbox in go.GetComponentsInChildren<Hitbox>(true))
            {
                if (hitbox.IsHead) hitbox.Multiplier *= definition.HeadMultiplier;
            }

            var agent = go.GetComponent<NavMeshAgent>();
            agent.height = 1.9f;
            agent.acceleration = 30.0f;
            agent.angularSpeed = 0.0f;

            var animator = go.GetComponentInChildren<Animator>();
            if (definition.Animator != null && animator != null) animator.runtimeAnimatorController = definition.Animator;

            var boss = go.AddComponent<BossAI>();
            boss.Definition = definition;
            return go;
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _animator = GetComponentInChildren<Animator>();
            _ragdoll = GetComponentInChildren<RagdollController>();
            _block = new MaterialPropertyBlock();
            _agent.updateRotation = false;
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void Start()
        {
            _player = FindAnyObjectByType<PlayerCombat>();
            _health.Died += OnDied;
            _health.Damaged += OnDamaged;
            _tint = Definition.Tint;
            Tint(_tint);
            BuildRing();
            Enter(Move.Intro);
            CampaignAssets.Play(a => a.BossRoar, transform.position + Vector3.up * 2.0f, 1.0f, 120.0f);
            Subtitles.Show($"[{Definition.DisplayName} ruge]", transform.position, 3.0f);
            _nextCharge = Time.time + 4.0f;
            _nextShockwave = Time.time + 6.0f;
        }

        private void Enter(Move state)
        {
            State = state;
            _stateStarted = Time.time;
        }

        private float StateTime => Time.time - _stateStarted;

        // ---------------------------------------------------------------- update

        private void Update()
        {
            if (State == Move.Dead) return;
            if (Time.time >= _nextTargetPick || _target == null)
            {
                _nextTargetPick = Time.time + 0.5f;
                _target = PickTarget();
            }

            if (_target == null) return;

            UpdateLight();
            UpdatePhase();
            _health.DamageTakenMultiplier = Vulnerable ? Definition.VulnerableMultiplier : Lit ? Definition.LightMultiplier : 1.0f;

            Vector3 target = _target.transform.position;
            float distance = Vector3.Distance(transform.position, target);
            bool playerDown = _target.IsDead || _target.Untargetable;

            switch (State)
            {
                case Move.Intro:
                    Stop();
                    Face(target, 120.0f);
                    if (StateTime > 2.2f) Enter(Move.Chase);
                    break;

                case Move.Chase:
                    if (playerDown)
                    {
                        Stop();
                        break;
                    }

                    if (distance <= Definition.MeleeRange && Time.time >= _nextMelee)
                    {
                        StartMelee();
                        break;
                    }

                    if (Definition.Shockwave && Phase >= 1 && Time.time >= _nextShockwave && distance < Definition.ShockwaveRadius * 1.3f)
                    {
                        StartShockwave();
                        break;
                    }

                    if (Definition.CanCharge && Time.time >= _nextCharge && distance >= Definition.ChargeMinRange && distance <= Definition.ChargeMaxRange && HasLineOfSight(target))
                    {
                        Enter(Move.ChargeWindup);
                        Stop();
                        CampaignAssets.Play(a => a.BossCharge, transform.position + Vector3.up * 2.0f, 1.0f, 100.0f);
                        Warning?.Invoke(this, "INVESTIDA! SAIA DA FRENTE");
                        Subtitles.Show("[rugido de investida]", transform.position, 1.5f);
                        break;
                    }

                    _agent.isStopped = false;
                    _agent.speed = Definition.ChaseSpeed * _speedScale;
                    if (_agent.isOnNavMesh) _agent.SetDestination(target);
                    FaceMovement();
                    // a barricade or door in the way is torn down with a few blows
                    if (Time.time >= _nextSmash)
                    {
                        IBreakable obstacle = Breakables.Blocking(transform.position, target, 2.5f);
                        if (obstacle != null)
                        {
                            _nextSmash = Time.time + 1.0f;
                            obstacle.Hit(80.0f, true, gameObject);
                            if (_animator != null) _animator.SetTrigger("Attack");
                        }
                    }
                    break;

                case Move.Melee:
                    Stop();
                    Face(target, 90.0f);
                    if (!_meleeLanded && StateTime >= Definition.MeleeWindup)
                    {
                        _meleeLanded = true;
                        CampaignAssets.Play(a => a.BossSlam, transform.position + transform.forward * 1.5f, 1.0f, 60.0f);
                        Vector3 toPlayer = target - transform.position;
                        toPlayer.y = 0.0f;
                        if (toPlayer.magnitude <= Definition.MeleeRange + 0.6f && Vector3.Angle(transform.forward, toPlayer) < 70.0f)
                        {
                            HitPlayer(Definition.MeleeDamage, toPlayer.normalized, 8.0f);
                        }
                    }

                    if (StateTime >= Definition.MeleeWindup + 0.6f)
                    {
                        RestoreAnimationSpeed();
                        Enter(Move.Chase);
                    }

                    break;

                case Move.ChargeWindup:
                    Stop();
                    // tracks the player while roaring, then locks the direction
                    Face(target, 200.0f);
                    if (StateTime >= Definition.ChargeWindup)
                    {
                        _chargeDirection = target - transform.position;
                        _chargeDirection.y = 0.0f;
                        _chargeDirection.Normalize();
                        transform.rotation = Quaternion.LookRotation(_chargeDirection);
                        _lastChargePosition = transform.position;
                        _chargeHitPlayer = false;
                        Enter(Move.Charging);
                    }

                    break;

                case Move.Charging:
                    UpdateCharge();
                    break;

                case Move.Recover:
                    Stop();
                    if (StateTime > 0.9f) Enter(Move.Chase);
                    break;

                case Move.Stunned:
                    Stop();
                    // a pulsing glow while it can be hurt badly
                    Tint(Color.Lerp(_tint, new Color(1.6f, 0.9f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10.0f)));
                    if (StateTime >= Definition.StunTime)
                    {
                        Tint(_tint);
                        RestoreAnimationSpeed();
                        Enter(Move.Chase);
                    }

                    break;

                case Move.ShockwaveWindup:
                    Stop();
                    float t = Mathf.Clamp01(StateTime / Definition.ShockwaveWindup);
                    DrawRing(Definition.ShockwaveRadius * t, true);
                    if (t >= 1.0f)
                    {
                        DrawRing(0.0f, false);
                        CampaignAssets.Play(a => a.Shockwave, transform.position, 1.0f, 100.0f);
                        // every survivor in reach is hit; jumping over it or rolling through it avoids the hit
                        foreach (Health survivor in Health.Living)
                        {
                            if (!IsSurvivor(survivor)) continue;
                            Vector3 offset = survivor.transform.position - transform.position;
                            offset.y = 0.0f;
                            if (offset.magnitude <= Definition.ShockwaveRadius && !Airborne(survivor)) HitSurvivor(survivor, Definition.ShockwaveDamage, offset.normalized, 10.0f);
                        }

                        if (_player != null) _player.AddShake(1.2f);
                        _nextShockwave = Time.time + Definition.ShockwaveCooldown;
                        Enter(Move.Recover);
                    }

                    break;
            }

            UpdateAnimator();
        }

        private void StartMelee()
        {
            Enter(Move.Melee);
            _meleeLanded = false;
            _nextMelee = Time.time + Definition.MeleeCooldown;
            if (_animator != null)
            {
                _animator.speed = Mathf.Clamp(0.6f / Definition.MeleeWindup, 0.4f, 1.0f);
                _animator.SetTrigger("Attack");
            }

            CampaignAssets.Play(a => a.BossRoar, transform.position + Vector3.up * 2.0f, 0.6f, 60.0f);
        }

        private void StartShockwave()
        {
            Enter(Move.ShockwaveWindup);
            Warning?.Invoke(this, "ONDA DE CHOQUE! PULE");
            Subtitles.Show("[o chão treme]", transform.position, 1.5f);
            if (_animator != null) _animator.SetTrigger("Attack");
        }

        private void UpdateCharge()
        {
            float step = Definition.ChargeSpeed * _speedScale * Time.deltaTime;
            Vector3 before = transform.position;
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.Move(_chargeDirection * step);
            }

            // barricades and doors in the way are smashed
            Breakables.HitAround(transform.position + _chargeDirection * 1.2f, 2.0f, 9999.0f, gameObject);

            // the player in the way gets run over, once
            if (!_chargeHitPlayer && _target != null && !_target.IsDead)
            {
                Vector3 offset = _target.transform.position - transform.position;
                offset.y = 0.0f;
                if (offset.magnitude < 1.8f * Definition.Scale * 0.7f)
                {
                    _chargeHitPlayer = true;
                    HitPlayer(Definition.ChargeDamage, _chargeDirection, 14.0f);
                }
            }

            // something solid in front: it crashes and is left stunned
            int mask = CombatLayers.Sight & ~(1 << CombatLayers.Player);
            bool wall = Physics.SphereCast(transform.position + Vector3.up * 1.2f, 0.6f, _chargeDirection, out RaycastHit hit, 1.2f, mask, QueryTriggerInteraction.Ignore)
                        && !hit.collider.transform.IsChildOf(transform);
            // or it got stuck at the edge of the walkable area (a container, a building)
            bool blocked = StateTime > 0.25f && (transform.position - before).magnitude < step * 0.3f;

            if (wall || blocked)
            {
                Crash();
                return;
            }

            if (StateTime >= Definition.ChargeDuration)
            {
                _nextCharge = Time.time + Definition.ChargeCooldown;
                Enter(Move.Recover);
            }
        }

        private void Crash()
        {
            _nextCharge = Time.time + Definition.ChargeCooldown;
            Enter(Move.Stunned);
            CampaignAssets.Play(a => a.BossStunned, transform.position + Vector3.up * 2.0f, 1.0f, 80.0f);
            Warning?.Invoke(this, "ATORDOADO — ATAQUE AGORA");
            Subtitles.Show("[impacto — a criatura cambaleia]", transform.position, 2.0f);
            if (_player != null) _player.AddShake(0.8f);
            if (_animator != null)
            {
                _animator.SetTrigger("Hit");
                _animator.speed = 0.35f;
            }
        }

        private void HitPlayer(float damage, Vector3 direction, float force)
        {
            if (_target != null) HitSurvivor(_target, damage, direction, force);
        }

        private void HitSurvivor(Health survivor, float damage, Vector3 direction, float force)
        {
            survivor.TakeDamage(new DamageInfo
            {
                Amount = damage,
                Point = survivor.transform.position + Vector3.up * 1.2f,
                Direction = direction,
                Force = force,
                Instigator = gameObject
            });
        }

        // the nearest survivor still standing: this machine's player, or in co-op a teammate's puppet
        private Health PickTarget()
        {
            Health best = null;
            float bestDistance = float.MaxValue;
            foreach (Health survivor in Health.Living)
            {
                if (!IsSurvivor(survivor)) continue;
                float d = (survivor.transform.position - transform.position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = survivor;
                }
            }

            // nobody left standing: stay on whoever it had, so it stops instead of wandering
            return best != null ? best : _target != null ? _target : _player != null ? _player.Health : null;
        }

        private static bool IsSurvivor(Health health) => health != null && health.Team == Factions.Player && !health.IsDead && !health.Untargetable;

        private bool Airborne(Health survivor)
        {
            if (_player != null && survivor == _player.Health) return !_player.Controller.Grounded;
            var puppet = survivor.GetComponent<NetPuppet>();
            return puppet != null && !puppet.Grounded;
        }

        // co-op client: the host's boss called out a move
        public static void ShowRemoteWarning(string text) => Warning?.Invoke(null, text);

        // ---------------------------------------------------------------- phases and light

        private void UpdatePhase()
        {
            if (Definition.Phases == null) return;
            int phase = 0;
            foreach (float threshold in Definition.Phases)
            {
                if (_health.Normalized <= threshold) phase++;
            }

            if (phase <= Phase) return;
            Phase = phase;
            _speedScale = 1.0f + 0.12f * phase;
            Warning?.Invoke(this, phase == Definition.Phases.Length ? "ENFURECIDA!" : "NOVA FASE");
            CampaignAssets.Play(a => a.BossRoar, transform.position + Vector3.up * 2.0f, 1.0f, 120.0f);

            if (Definition.SummonCount > 0)
            {
                EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
                if (spawner != null) spawner.SpawnPressure(Definition.SummonCount, transform.position, Definition.SummonVariant);
                Subtitles.Show("[ela chama a horda]", transform.position, 3.0f);
            }
        }

        private void UpdateLight()
        {
            if (!Definition.WeakToLight || Time.time < _nextLightCheck) return;
            _nextLightCheck = Time.time + 0.3f;
            bool lit = LightFlicker.IsLit(transform.position + Vector3.up) || ThrownItem.FlareNear(transform.position, 9.0f);
            if (lit && !Lit) Warning?.Invoke(this, "EXPOSTA À LUZ — VULNERÁVEL");
            Lit = lit;
            _speedScale = (1.0f + 0.12f * Phase) * (Lit ? 0.75f : 1.0f);
        }

        // ---------------------------------------------------------------- helpers

        private bool HasLineOfSight(Vector3 target)
        {
            Vector3 eye = transform.position + Vector3.up * 2.0f;
            Vector3 chest = target + Vector3.up * 1.2f;
            int mask = CombatLayers.Sight & ~(1 << CombatLayers.Player);
            return !Physics.Linecast(eye, chest, mask, QueryTriggerInteraction.Ignore);
        }

        private void Stop()
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
        }

        private void FaceMovement()
        {
            Vector3 velocity = _agent.velocity;
            velocity.y = 0.0f;
            if (velocity.sqrMagnitude > 0.05f) Face(transform.position + velocity, 160.0f);
        }

        private void Face(Vector3 point, float degreesPerSecond)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * Time.deltaTime);
        }

        private void UpdateAnimator()
        {
            if (_animator == null || !_animator.enabled) return;
            float speed = State == Move.Charging ? Definition.ChargeSpeed : _agent.velocity.magnitude;
            speed /= Mathf.Max(transform.localScale.y, 0.1f);
            _animator.SetFloat("Speed", speed);
            _animator.SetFloat("WalkSpeedScale", Mathf.Max(0.5f, speed / Mathf.Max(Definition.MoveAnimationSpeed / Definition.Scale, 0.1f)));
        }

        private void RestoreAnimationSpeed()
        {
            if (_animator != null) _animator.speed = 1.0f;
        }

        private void Tint(Color color)
        {
            foreach (Renderer body in GetComponentsInChildren<Renderer>())
            {
                Material[] materials = body.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].HasProperty("_BaseColor")) continue;
                    body.GetPropertyBlock(_block, i);
                    _block.SetColor("_BaseColor", materials[i].GetColor("_BaseColor") * color);
                    body.SetPropertyBlock(_block, i);
                }
            }
        }

        private void BuildRing()
        {
            var go = new GameObject("ShockwaveRing");
            go.transform.SetParent(transform, false);
            _ring = go.AddComponent<LineRenderer>();
            _ring.loop = true;
            _ring.useWorldSpace = true;
            _ring.positionCount = 48;
            _ring.widthMultiplier = 0.15f;
            _ring.startColor = new Color(1.0f, 0.2f, 0.1f, 0.9f);
            _ring.endColor = new Color(1.0f, 0.2f, 0.1f, 0.9f);
            _ring.sharedMaterial = CombatEffects.Instance != null ? CombatEffects.Instance.TracerMaterial : null;
            _ring.enabled = false;
        }

        private void DrawRing(float radius, bool show)
        {
            _ring.enabled = show && radius > 0.1f;
            if (!_ring.enabled) return;
            for (int i = 0; i < _ring.positionCount; i++)
            {
                float angle = i / (float)_ring.positionCount * Mathf.PI * 2.0f;
                _ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle) * radius, 0.15f, Mathf.Sin(angle) * radius));
            }
        }

        // ---------------------------------------------------------------- damage and death

        private void OnDamaged(DamageInfo info)
        {
            if (_health.IsDead) return;
            // even a boss flinches when hit hard while stunned
            if (Vulnerable && info.Amount > 40.0f && _animator != null) _animator.SetTrigger("Hit");
        }

        private void OnDied(DamageInfo info)
        {
            Enter(Move.Dead);
            DrawRing(0.0f, false);
            RestoreAnimationSpeed();
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.enabled = false;
            _health.DamageTakenMultiplier = 1.0f;
            if (_ragdoll != null)
            {
                _ragdoll.SetRagdoll(true);
                _ragdoll.Push(info.HitBody, info.Point, info.Direction * 60.0f);
            }
            else if (_animator != null)
            {
                _animator.SetBool("Dead", true);
            }

            CampaignAssets.Play(a => a.BossDeath, transform.position + Vector3.up * 2.0f, 1.0f, 150.0f);
            RunState.AddCredits(Definition.Credits, Definition.DisplayName);
            Progression.AddExperience(Definition.Experience, "chefe");
            if (!string.IsNullOrEmpty(Definition.Achievement)) Progression.Unlock(Definition.Achievement);
            Defeated?.Invoke(this);
            Destroy(gameObject, 20.0f);
        }
    }
}
