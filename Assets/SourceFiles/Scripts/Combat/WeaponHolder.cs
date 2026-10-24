using System;
using UnityEngine;

namespace Combat
{
    // Runtime state of one gun: which weapon it is and how much ammo it holds
    [Serializable]
    public class WeaponInstance
    {
        public WeaponData Data;
        public int Magazine;
        public int Reserve;
        public bool InfiniteReserve;

        public WeaponInstance(WeaponData data, int magazine, int reserve, bool infiniteReserve = false)
        {
            Data = data;
            Magazine = magazine;
            Reserve = reserve;
            InfiniteReserve = infiniteReserve;
        }

        public bool CanReload => Magazine < Data.MagazineSize && (InfiniteReserve || Reserve > 0);
    }

    public struct HitReport
    {
        public Health Target;
        public float Damage;
        public bool Headshot;
        public bool Killed;
    }

    /* Holds, animates and fires a weapon for any humanoid character (player or enemy).
     * The weapon is posed procedurally around the shoulder, following the aim point with
     * springs for recoil, sway and bob, and both hands are placed on it with Animator IK.
     * The owner sets AimPoint, Aiming, TriggerHeld etc. every frame.
     */
    [DefaultExecutionOrder(40)]
    [RequireComponent(typeof(Animator))]
    public partial class WeaponHolder : MonoBehaviour
    {
        [Header("Pose")]
        public float ShoulderHeight = 1.36f;
        public float ShoulderSide = 0.12f;
        public Vector3 RifleHipGrip = new Vector3(0.05f, -0.16f, 0.30f);
        public Vector3 RifleAimGrip = new Vector3(0.0f, -0.07f, 0.32f);
        public Vector3 PistolHipGrip = new Vector3(-0.02f, -0.12f, 0.40f);
        public Vector3 PistolAimGrip = new Vector3(-0.08f, -0.03f, 0.48f);
        [Tooltip("How quickly the weapon follows the aim; lower values give more sway")]
        public float AimSharpness = 16.0f;
        [Tooltip("Max angle between the body and the aim before the weapon stops turning")]
        public float MaxAimYaw = 75.0f;

        [Header("Hands")]
        public Vector3 RightHandRotation = new Vector3(-20.0f, -90.0f, -90.0f);
        public Vector3 LeftHandRotation = new Vector3(0.0f, 90.0f, 90.0f);
        public Vector3 PistolLeftHandRotation = new Vector3(-20.0f, 90.0f, 60.0f);
        public Vector3 PistolSupportOffset = new Vector3(-0.025f, -0.025f, 0.01f);

        [Header("Combat")]
        [Tooltip("Scales the damage of every shot, used to make enemies weaker than the player")]
        public float DamageMultiplier = 1.0f;
        [Tooltip("Used instead of DamageMultiplier when shooting anything but the player")]
        public float DamageMultiplierVsAI = 1.0f;
        [Tooltip("Enemies shooting near the player make a whiz sound")]
        public bool IsPlayer;

        // set by the owner every frame
        [NonSerialized] public Vector3 AimPoint;
        [NonSerialized] public bool Aiming;
        [NonSerialized] public bool Sprinting;
        [NonSerialized] public bool Lowered;
        [NonSerialized] public bool Holstered;
        [NonSerialized] public bool TriggerHeld;
        [NonSerialized] public float MoveSpeed01;
        [Tooltip("Hide the weapon model, e.g. while looking through a scope")]
        [NonSerialized] public bool HideVisual;
        // how far the shoulders have dropped, e.g. crouching
        [NonSerialized] public float CrouchDrop;

        // first person aiming: while aiming, the sights are brought to SightAnchor along ViewRotation
        [NonSerialized] public bool SightAim;
        [NonSerialized] public Quaternion ViewRotation = Quaternion.identity;
        [NonSerialized] public Vector3 SightAnchor;

        public WeaponInstance Current { get; private set; }
        public bool IsReloading { get; private set; }
        public float ReloadProgress { get; private set; }
        public float CurrentSpread { get; private set; }
        public float AimBlend { get; private set; }
        public Vector3 MuzzlePosition => _visual != null ? _visual.TransformPoint(Current.Data.MuzzlePoint) : transform.position;
        public bool HasVisual => _visual != null;
        public Vector3 SightPosition => _visual != null ? _visual.TransformPoint(Current.Data.SightPoint) : transform.position;
        // looking down the barrel: the model's barrel points along its -Z
        public Vector3 SightForward => _visual != null ? -_visual.forward : transform.forward;

        public event Action<WeaponInstance> Fired;
        public event Action<HitReport> Hit;
        public event Action<WeaponInstance> Reloaded;

        private Animator _animator;
        private Health _health;

        private Transform _visual;
        private Transform _magazine;
        private Transform _cyclingPart;
        private Vector3 _magazineRestPosition;
        private Quaternion _magazineRestRotation;
        private Vector3 _magazineCenter; // weapon space, the parts' pivots sit at the weapon origin
        private Vector3 _cyclingRestPosition;
        private Vector3 _cyclingCenter;
        private Transform _backVisual;
        private WeaponData _backData;
        private bool _visualHidden;

        private Quaternion _aimRotation = Quaternion.identity;
        private Quaternion _sightRotation = Quaternion.identity;
        private float _lastAimYaw;
        private float _turnRoll;
        private float _sprintBlend;
        private float _lowerBlend;
        private float _holsterBlend;
        private float _equipBlend;
        private float _ikWeight;
        private float _bobPhase;

        private Vector3 _recoilPosition, _recoilPositionVelocity;
        private Vector3 _recoilRotation, _recoilRotationVelocity;
        private float _cycleKick;
        private float _manualCycleStart = -1.0f;
        private bool _manualCycleSoundPlayed;
        private bool _manualCycleShellEjected;

        private float _fireCooldown;
        private float _bloom;
        private bool _triggerWasHeld;
        private float _queuedPressTime = -1.0f;

        private float _reloadTime;
        private bool _reloadWasEmpty;
        private int _reloadStage;
        private bool _cancelReload;
        private bool _magazineInHand;

        private Vector3 _leftHandTarget;
        private Vector3 _shoulder;
        private Quaternion _handFrame;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _health = GetComponent<Health>();
            _aimRotation = transform.rotation;
            _lastAimYaw = transform.eulerAngles.y;
        }

        // ---------------------------------------------------------------- equipment

        public void Equip(WeaponInstance weapon)
        {
            FinishGear();
            DestroyVisual(ref _visual);
            Current = weapon;
            IsReloading = false;
            _magazineInHand = false;
            _manualCycleStart = -1.0f;
            _fireCooldown = 0.25f;
            _bloom = 0.0f;
            if (weapon == null || weapon.Data == null)
            {
                Current = null;
                return;
            }

            AdoptVisual(BuildVisual(weapon.Data, "Weapon_" + weapon.Data.DisplayName));

            // the weapon comes up from the low ready position
            _equipBlend = 0.0f;
            _recoilRotationVelocity += new Vector3(-40.0f, 0.0f, 25.0f);
            _aimRotation = Quaternion.LookRotation(transform.forward);
            if (CombatEffects.Instance != null)
            {
                CombatEffects.Instance.PlaySound(CombatEffects.Instance.SwitchSound, transform.position + Vector3.up, 0.7f);
            }
        }

        // the visual of the weapon in the hands: finds its moving parts
        private void AdoptVisual(Transform visual)
        {
            _visual = visual;
            _visualHidden = false;
            _magazine = FindDeep(_visual, Current.Data.MagazinePart);
            _cyclingPart = FindDeep(_visual, Current.Data.CyclingPart);
            if (_magazine != null)
            {
                _magazine.gameObject.SetActive(true);
                _magazineRestPosition = _magazine.localPosition;
                _magazineRestRotation = _magazine.localRotation;
                _magazineCenter = PartCenter(_magazine);
            }

            if (_cyclingPart != null)
            {
                _cyclingRestPosition = _cyclingPart.localPosition;
                _cyclingCenter = PartCenter(_cyclingPart);
            }
        }

        public WeaponInstance Unequip()
        {
            FinishGear();
            WeaponInstance weapon = Current;
            DestroyVisual(ref _visual);
            Current = null;
            IsReloading = false;
            return weapon;
        }

        // shows a second weapon slung on the back
        public void SetBackWeapon(WeaponData data)
        {
            if (_backData == data) return;
            DestroyVisual(ref _backVisual);
            _backData = data;
            _backSide = 0;
            if (data != null) _backVisual = BuildVisual(data, "BackWeapon_" + data.DisplayName);
        }

        public void StartReload()
        {
            if (Current == null || IsReloading || !Current.CanReload || Holstered) return;

            IsReloading = true;
            _reloadTime = 0.0f;
            _reloadStage = 0;
            _cancelReload = false;
            _reloadWasEmpty = Current.Magazine == 0;
            ReloadProgress = 0.0f;
        }

        // a jolt when the owner gets hit
        // the gun is driven forward for a rifle-butt or a jab, using the recoil springs
        public void MeleeThrust()
        {
            _recoilPositionVelocity += new Vector3(0.0f, 0.4f, 4.5f);
            _recoilRotationVelocity += new Vector3(-60.0f, 25.0f, 40.0f);
        }

        public void Flinch(Vector3 direction)
        {
            Vector3 local = Quaternion.Inverse(_aimRotation) * direction;
            _recoilRotationVelocity += new Vector3(-Mathf.Abs(local.z) * 30.0f - 20.0f, local.x * 60.0f, UnityEngine.Random.Range(-40.0f, 40.0f));
            _recoilPositionVelocity += new Vector3(local.x, 0.0f, -0.5f) * 0.6f;
        }

        private Transform BuildVisual(WeaponData data, string name)
        {
            GameObject visual = Instantiate(data.Model, transform);
            visual.name = name;
            if (data.Attachment != null)
            {
                GameObject attachment = Instantiate(data.Attachment, visual.transform);
                attachment.name = "Attachment";
                attachment.transform.localPosition = data.AttachmentOffset;
                attachment.transform.localRotation = Quaternion.identity;
            }

            foreach (Collider col in visual.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (Transform t in visual.GetComponentsInChildren<Transform>()) t.gameObject.layer = gameObject.layer;
            return visual.transform;
        }

        private static void DestroyVisual(ref Transform visual)
        {
            if (visual != null) Destroy(visual.gameObject);
            visual = null;
        }

        // center of a part's mesh in weapon space
        private Vector3 PartCenter(Transform part)
        {
            Renderer renderer = part.GetComponent<Renderer>();
            Vector3 world = renderer != null ? renderer.bounds.center : part.position;
            return _visual.InverseTransformPoint(world);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name == name) return t;
            }

            return null;
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0f) return;

            UpdateBlends(dt);
            UpdateSprings(dt);
            UpdateGear(dt);

            if (Current == null)
            {
                _ikWeight = Mathf.MoveTowards(_ikWeight, 0.0f, dt * 6.0f);
                PoseBackWeapon();
                return;
            }

            UpdateFiring(dt);
            if (IsReloading) UpdateReload(dt);
            UpdateManualCycle();
            PoseWeapon(dt);
            PoseBackWeapon();
        }

        private void UpdateBlends(float dt)
        {
            AimBlend = Smooth(AimBlend, Aiming && !Sprinting && !Holstered && !IsReloading ? 1.0f : 0.0f, dt, 7.0f);
            _sprintBlend = Smooth(_sprintBlend, Sprinting ? 1.0f : 0.0f, dt, 8.0f);
            _lowerBlend = Smooth(_lowerBlend, Lowered ? 1.0f : 0.0f, dt, 4.0f);
            _holsterBlend = Smooth(_holsterBlend, Holstered ? 1.0f : 0.0f, dt, 5.0f);
            _equipBlend = Mathf.MoveTowards(_equipBlend, 1.0f, dt * 3.0f);
            _ikWeight = Smooth(_ikWeight, Holstered ? 0.0f : 1.0f, dt, 8.0f);
        }

        private static float Smooth(float current, float target, float dt, float speed)
        {
            return Mathf.Lerp(current, target, 1.0f - Mathf.Exp(-speed * dt));
        }

        private void UpdateSprings(float dt)
        {
            Spring(ref _recoilPosition, ref _recoilPositionVelocity, 220.0f, 20.0f, dt);
            Spring(ref _recoilRotation, ref _recoilRotationVelocity, 160.0f, 15.0f, dt);
            _cycleKick = Mathf.MoveTowards(_cycleKick, 0.0f, dt * 14.0f);

            if (Current != null)
            {
                _bloom = Mathf.MoveTowards(_bloom, 0.0f, Current.Data.BloomRecovery * dt);
            }
        }

        private static void Spring(ref Vector3 value, ref Vector3 velocity, float stiffness, float damping, float dt)
        {
            // semi-implicit integration in small steps: stable even when a frame takes a long time
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.016f), 1, 30);
            float step = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                velocity += (-stiffness * value - damping * velocity) * step;
                value += velocity * step;
            }

            // never let a bad frame poison the pose
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) || value.sqrMagnitude > 1e6f)
            {
                value = Vector3.zero;
                velocity = Vector3.zero;
            }
        }

        // ---------------------------------------------------------------- posing

        private void PoseWeapon(float dt)
        {
            WeaponData data = Current.Data;
            bool pistol = data.Hold == HoldStyle.Pistol;
            Quaternion yaw = Quaternion.Euler(0.0f, transform.eulerAngles.y, 0.0f);

            _shoulder = transform.position + Vector3.up * (ShoulderHeight - CrouchDrop) + yaw * Vector3.right * (pistol ? 0.03f : ShoulderSide);

            // aim direction, kept within reach of the arms
            Vector3 aimDirection = AimPoint - _shoulder;
            if (aimDirection.sqrMagnitude < 1.0f) aimDirection = yaw * Vector3.forward;
            Vector3 local = Quaternion.Inverse(yaw) * aimDirection.normalized;
            float localYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -MaxAimYaw, MaxAimYaw);
            float localPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1.0f, 1.0f)) * Mathf.Rad2Deg, -70.0f, 70.0f);
            Quaternion targetAim = yaw * Quaternion.Euler(localPitch, localYaw, 0.0f);
            if (float.IsNaN(_aimRotation.x)) _aimRotation = targetAim;
            _aimRotation = Quaternion.Slerp(_aimRotation, targetAim, 1.0f - Mathf.Exp(-AimSharpness * dt));

            // turning the view rolls the weapon a little
            float aimYaw = _aimRotation.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(_lastAimYaw, aimYaw) / dt;
            _lastAimYaw = aimYaw;
            _turnRoll = Mathf.Lerp(_turnRoll, Mathf.Clamp(-yawRate * 0.02f, -10.0f, 10.0f), 1.0f - Mathf.Exp(-10.0f * dt));

            // walking bob and idle breathing, quieter while aiming
            float bobAmount = MoveSpeed01 * (1.0f - AimBlend * 0.75f);
            _bobPhase += dt * Mathf.Lerp(4.0f, 11.0f, MoveSpeed01);
            float breathe = Mathf.Sin(Time.time * 1.6f);
            Vector3 position = new Vector3(Mathf.Sin(_bobPhase) * 0.012f, -Mathf.Abs(Mathf.Cos(_bobPhase)) * 0.02f, 0.0f) * bobAmount;
            position.y += breathe * 0.003f * (1.0f - AimBlend * 0.6f);
            Vector3 rotation = new Vector3(Mathf.Abs(Mathf.Cos(_bobPhase)) * 2.0f, Mathf.Sin(_bobPhase) * 1.5f, Mathf.Sin(_bobPhase) * 2.0f) * bobAmount;
            rotation.x += breathe * 0.4f;
            rotation.z += _turnRoll;

            // low ready while sprinting or relaxed, and coming up when equipped
            float low = Mathf.Max(_sprintBlend, _lowerBlend * 0.8f);
            position += new Vector3(-0.06f, -0.12f, -0.08f) * low;
            rotation += new Vector3(40.0f, pistol ? -15.0f : -30.0f, 15.0f) * low;
            float equip = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, _equipBlend);
            position += new Vector3(0.0f, -0.25f, -0.1f) * equip;
            rotation += new Vector3(60.0f, 0.0f, 20.0f) * equip;

            // reload: tilt the weapon to look into the magazine well
            float reload = ReloadPoseWeight();
            if (pistol)
            {
                position += new Vector3(0.02f, 0.03f, -0.12f) * reload;
                rotation += new Vector3(-15.0f, 15.0f, 20.0f) * reload;
            }
            else
            {
                position += new Vector3(-0.04f, 0.03f, -0.06f) * reload;
                rotation += new Vector3(-10.0f, 5.0f, 30.0f) * reload;
            }

            position += _recoilPosition;
            rotation += _recoilRotation;

            Vector3 grip = pistol ? Vector3.Lerp(PistolHipGrip, PistolAimGrip, AimBlend) : Vector3.Lerp(RifleHipGrip, RifleAimGrip, AimBlend);
            _handFrame = _aimRotation * Quaternion.Euler(rotation);
            Quaternion weaponRotation = _handFrame * Quaternion.Euler(0.0f, 180.0f, 0.0f);
            Vector3 weaponPosition = _shoulder + _aimRotation * (grip + position) - weaponRotation * data.GripPoint;

            // first person aiming: line the sights up with the view, keeping a softer recoil and sway
            if (SightAim && AimBlend > 0.001f)
            {
                _sightRotation = Quaternion.Slerp(_sightRotation, ViewRotation, 1.0f - Mathf.Exp(-30.0f * dt));
                Vector3 adsTilt = _recoilRotation * 0.5f + new Vector3(breathe * 0.12f, 0.0f, _turnRoll * 0.2f);
                Quaternion adsRotation = _sightRotation * Quaternion.Euler(adsTilt) * Quaternion.Euler(0.0f, 180.0f, 0.0f);
                Vector3 adsPosition = SightAnchor + _sightRotation * (_recoilPosition * 0.6f + reload * new Vector3(0.0f, -0.12f, -0.05f))
                                      - adsRotation * data.SightPoint;
                float ads = Mathf.SmoothStep(0.0f, 1.0f, AimBlend);
                weaponPosition = Vector3.Lerp(weaponPosition, adsPosition, ads);
                weaponRotation = Quaternion.Slerp(weaponRotation, adsRotation, ads);
                _handFrame = weaponRotation * Quaternion.Euler(0.0f, 180.0f, 0.0f);
            }
            else
            {
                _sightRotation = _aimRotation;
            }

            // blend onto the back while holstered (climbing)
            if (_holsterBlend > 0.001f)
            {
                BackPose(data, 0, out Vector3 backPosition, out Quaternion backRotation);
                float h = Mathf.SmoothStep(0.0f, 1.0f, _holsterBlend);
                weaponPosition = Vector3.Lerp(weaponPosition, backPosition, h);
                weaponRotation = Quaternion.Slerp(weaponRotation, backRotation, h);
            }

            ApplyGearMotion(ref weaponPosition, ref weaponRotation, dt);
            _visual.SetPositionAndRotation(weaponPosition, weaponRotation);
            if (_visualHidden != HideVisual)
            {
                _visualHidden = HideVisual;
                foreach (Renderer r in _visual.GetComponentsInChildren<Renderer>()) r.enabled = !HideVisual;
            }

            PoseParts();
            _leftHandTarget = LeftHandTarget();
        }

        private void PoseParts()
        {
            WeaponData data = Current.Data;
            if (_cyclingPart != null)
            {
                float travel = _cycleKick;
                if (_manualCycleStart >= 0.0f)
                {
                    // pump / bolt worked by hand: back then forward
                    float t = (Time.time - _manualCycleStart) / 0.45f;
                    travel = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                }
                else if (IsReloading && _reloadWasEmpty && data.Reload == ReloadStyle.Magazine)
                {
                    float t = Mathf.InverseLerp(0.68f, 0.8f, ReloadProgress);
                    travel = Mathf.Max(travel, Mathf.Sin(t * Mathf.PI));
                }

                _cyclingPart.localPosition = _cyclingRestPosition + data.CyclingTravel * travel;
            }
        }

        private float ReloadPoseWeight()
        {
            if (!IsReloading) return 0.0f;
            if (Current.Data.Reload == ReloadStyle.SingleShell) return Mathf.SmoothStep(0.0f, 1.0f, Mathf.Min(1.0f, _reloadTime * 5.0f)) * 0.7f;

            float t = ReloadProgress;
            return Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.0f, 0.15f, t)) *
                   Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(1.0f, 0.85f, t));
        }

        private Vector3 SupportPoint()
        {
            WeaponData data = Current.Data;
            Vector3 support = _visual.TransformPoint(data.SupportPoint);

            // the support hand rides the pump of a shotgun
            if (_cyclingPart != null && data.ManualCycle && data.Hold == HoldStyle.Rifle && data.Reload == ReloadStyle.SingleShell)
            {
                support += _visual.TransformVector(_cyclingPart.localPosition - _cyclingRestPosition);
            }

            if (data.Hold == HoldStyle.Pistol) support += _aimRotation * PistolSupportOffset;
            return support;
        }

        private Vector3 LeftHandTarget()
        {
            Vector3 support = SupportPoint();
            if (!IsReloading) return support;

            WeaponData data = Current.Data;
            Quaternion yaw = Quaternion.Euler(0.0f, transform.eulerAngles.y, 0.0f);
            Vector3 pouch = transform.position + yaw * new Vector3(-0.12f, 0.95f, 0.12f);

            if (data.Reload == ReloadStyle.SingleShell)
            {
                // loop: hand to the belt, then to the loading port
                Vector3 port = _visual.TransformPoint(data.EjectPoint) + Vector3.down * 0.06f;
                float cycle = (_reloadTime % data.ShellInsertTime) / data.ShellInsertTime;
                if (cycle < 0.4f) return Vector3.Lerp(support, pouch, Mathf.SmoothStep(0, 1, cycle / 0.4f));
                if (cycle < 0.75f) return Vector3.Lerp(pouch, port, Mathf.SmoothStep(0, 1, (cycle - 0.4f) / 0.35f));
                return Vector3.Lerp(port, support, Mathf.SmoothStep(0, 1, (cycle - 0.75f) / 0.25f));
            }

            Vector3 well = _magazine != null ? _visual.TransformPoint(_magazineCenter) : support;
            Vector3 charge = _cyclingPart != null
                ? _visual.TransformPoint(_cyclingCenter + _cyclingPart.localPosition - _cyclingRestPosition)
                : support;
            float t = ReloadProgress;

            if (t < 0.18f) return Vector3.Lerp(support, well, Mathf.SmoothStep(0, 1, t / 0.18f));
            if (t < 0.4f) return Vector3.Lerp(well, pouch, Mathf.SmoothStep(0, 1, (t - 0.18f) / 0.22f));
            if (t < 0.55f) return Vector3.Lerp(pouch, well, Mathf.SmoothStep(0, 1, (t - 0.4f) / 0.15f));
            if (t < 0.65f) return well;
            if (_reloadWasEmpty && data.Hold == HoldStyle.Rifle)
            {
                if (t < 0.72f) return Vector3.Lerp(well, charge, Mathf.SmoothStep(0, 1, (t - 0.65f) / 0.07f));
                if (t < 0.8f) return charge;
                return Vector3.Lerp(charge, support, Mathf.SmoothStep(0, 1, (t - 0.8f) / 0.2f));
            }

            return Vector3.Lerp(well, support, Mathf.SmoothStep(0, 1, (t - 0.65f) / 0.35f));
        }

        // the gun's place on the body (see WeaponHolder.Gear): holster, back or chest by weapon class
        private void BackPose(WeaponData data, int slot, out Vector3 position, out Quaternion rotation)
        {
            CarryPose(data, slot, out position, out rotation);
        }

        private void PoseBackWeapon()
        {
            if (_backVisual == null) return;
            CarryPose(_backData, _backSide, out Vector3 position, out Quaternion rotation);
            _backVisual.SetPositionAndRotation(position, rotation);
        }

        // ---------------------------------------------------------------- firing

        private void UpdateFiring(float dt)
        {
            WeaponData data = Current.Data;
            _fireCooldown -= dt;

            bool pressed = TriggerHeld && !_triggerWasHeld;
            _triggerWasHeld = TriggerHeld;
            if (pressed) _queuedPressTime = Time.time;

            float baseSpread = Mathf.Lerp(data.HipSpread, data.AimSpread, AimBlend) + data.MoveSpread * MoveSpeed01;
            CurrentSpread = baseSpread + _bloom;

            if (Holstered || Busy) return;

            if (IsReloading)
            {
                // a shotgun can stop loading shells to fire straight away
                if (pressed && data.Reload == ReloadStyle.SingleShell && Current.Magazine > 0) _cancelReload = true;
                return;
            }

            // semi-auto presses are buffered briefly so fast clicking never gets lost
            bool queued = _queuedPressTime >= 0.0f && Time.time - _queuedPressTime < 0.2f;
            bool wantsToFire = data.Mode == FireMode.Auto ? TriggerHeld : queued;
            bool ready = _fireCooldown <= 0.0f && _equipBlend > 0.7f && _sprintBlend < 0.35f && _manualCycleStart < 0.0f;
            if (!wantsToFire || !ready) return;

            _queuedPressTime = -1.0f;
            if (Current.Magazine > 0)
            {
                Shoot();
            }
            else
            {
                _fireCooldown = 0.25f;
                if (CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(data.DrySound, MuzzlePosition, 0.8f);
                StartReload();
            }
        }

        private void Shoot()
        {
            WeaponData data = Current.Data;
            Current.Magazine--;
            _fireCooldown = data.FireInterval;

            Vector3 muzzle = MuzzlePosition;
            Vector3 barrel = -_visual.forward;

            // if the muzzle is poking through a wall, fire from the shoulder instead
            Vector3 origin = muzzle;
            if (Physics.Linecast(_shoulder, muzzle, out RaycastHit blocked, ShotMask(), QueryTriggerInteraction.Ignore) &&
                !IsOwnCollider(blocked.collider))
            {
                origin = _shoulder;
            }

            Vector3 toTarget = AimPoint - origin;
            Vector3 direction = toTarget.sqrMagnitude > 1.0f ? toTarget.normalized : barrel;
            for (int i = 0; i < Mathf.Max(1, data.Pellets); i++)
            {
                FireRay(origin, muzzle, RandomInCone(direction, CurrentSpread));
            }

            _bloom = Mathf.Min(data.MaxBloom, _bloom + data.SpreadPerShot);

            // recoil: kick back, muzzle climb and a random twist, softer when aiming
            float kick = Mathf.Lerp(1.0f, 0.6f, AimBlend);
            _recoilPositionVelocity += new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0.25f, -data.KickBack * 30.0f) * kick;
            _recoilRotationVelocity += new Vector3(-data.KickPitch * 25.0f,
                UnityEngine.Random.Range(-1.0f, 1.0f) * data.KickYaw * 25.0f,
                UnityEngine.Random.Range(-1.0f, 1.0f) * data.KickYaw * 15.0f) * kick;

            CombatEffects fx = CombatEffects.Instance;
            if (fx != null)
            {
                float flashSize = data.Pellets > 1 || data.HasScope ? 1.5f : data.Hold == HoldStyle.Pistol ? 0.8f : 1.0f;
                fx.MuzzleFlash(_visual, muzzle, Quaternion.LookRotation(barrel), flashSize);
                if (data.ShotSounds != null && data.ShotSounds.Length > 0)
                {
                    fx.PlaySound(data.ShotSounds[UnityEngine.Random.Range(0, data.ShotSounds.Length)], muzzle,
                        IsPlayer ? 0.9f : 1.0f, 0.04f, IsPlayer ? 0.6f : 1.0f, 4.0f, 140.0f);
                }
            }

            if (data.ManualCycle)
            {
                // the pump / bolt is worked a moment after the shot
                StartManualCycle(0.18f, true);
            }
            else
            {
                _cycleKick = 1.0f;
                EjectShell();
            }

            Fired?.Invoke(Current);
            CombatEvents.ReportGunshot(muzzle, _health != null ? _health.Team : 0, data.Suppressed, gameObject);
        }

        private void StartManualCycle(float delay, bool ejectShell)
        {
            _manualCycleStart = Time.time + delay;
            _manualCycleSoundPlayed = false;
            _manualCycleShellEjected = !ejectShell;
        }

        private void UpdateManualCycle()
        {
            if (_manualCycleStart < 0.0f || Time.time < _manualCycleStart) return;

            float t = (Time.time - _manualCycleStart) / 0.45f;
            if (!_manualCycleSoundPlayed)
            {
                _manualCycleSoundPlayed = true;
                if (CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(Current.Data.CycleSound, MuzzlePosition, 0.8f);
            }

            if (t >= 0.5f && !_manualCycleShellEjected)
            {
                _manualCycleShellEjected = true;
                EjectShell();
            }

            if (t >= 1.0f) _manualCycleStart = -1.0f;
        }

        private void EjectShell()
        {
            if (CombatEffects.Instance == null) return;

            WeaponData data = Current.Data;
            Vector3 port = _visual.TransformPoint(data.EjectPoint);
            Vector3 right = -_visual.right; // the model's barrel points down -Z, so its right side is -X
            Vector3 velocity = right * UnityEngine.Random.Range(1.5f, 2.5f) + Vector3.up * UnityEngine.Random.Range(1.0f, 2.0f) +
                               _visual.forward * UnityEngine.Random.Range(0.0f, 0.5f);
            CombatEffects.Instance.EjectShell(port, _visual.rotation, velocity, data.Pellets > 1 ? 1.6f : data.HasScope ? 1.8f : 1.0f);
        }

        private int ShotMask()
        {
            return CombatLayers.Shootable & ~(1 << gameObject.layer);
        }

        private bool IsOwnCollider(Collider col)
        {
            return col.transform.IsChildOf(transform);
        }

        private void FireRay(Vector3 origin, Vector3 muzzle, Vector3 direction)
        {
            WeaponData data = Current.Data;
            int count = Physics.RaycastNonAlloc(origin, direction, HitBuffer, data.Range, ShotMask(), QueryTriggerInteraction.Ignore);

            // nearest hit that isn't part of the shooter
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (IsOwnCollider(HitBuffer[i].collider)) continue;
                if (best < 0 || HitBuffer[i].distance < HitBuffer[best].distance) best = i;
            }

            Vector3 end = origin + direction * data.Range;
            CombatEffects fx = CombatEffects.Instance;

            if (best >= 0)
            {
                RaycastHit hit = HitBuffer[best];
                end = hit.point;
                Hitbox hitbox = hit.collider.GetComponent<Hitbox>();

                if (hitbox != null && hitbox.Owner != null)
                {
                    bool wasDead = hitbox.Owner.IsDead;
                    float multiplier = IsPlayer || hitbox.Owner.Team == Factions.Player ? DamageMultiplier : DamageMultiplierVsAI;
                    float damage = data.Damage * hitbox.Multiplier * multiplier;
                    hitbox.Owner.TakeDamage(new DamageInfo
                    {
                        Amount = damage,
                        Point = hit.point,
                        Direction = direction,
                        Force = data.HitForce,
                        Instigator = gameObject,
                        HitBody = hitbox.Body,
                        Headshot = hitbox.IsHead
                    });

                    if (!wasDead)
                    {
                        Hit?.Invoke(new HitReport { Target = hitbox.Owner, Damage = damage, Headshot = hitbox.IsHead, Killed = hitbox.Owner.IsDead });
                    }

                    if (hitbox.Body != null && !hitbox.Body.isKinematic)
                    {
                        hitbox.Body.AddForceAtPosition(direction * data.HitForce * 3.0f, hit.point, ForceMode.Impulse);
                    }

                    if (fx != null)
                    {
                        if (hitbox.Flesh) fx.FleshImpact(hit.point, hit.normal, direction);
                        else fx.Impact(hit.point, hit.normal, true, hit.collider.transform);
                    }
                }
                else
                {
                    if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                    {
                        hit.rigidbody.AddForceAtPosition(direction * data.HitForce, hit.point, ForceMode.Impulse);
                    }

                    if (fx != null) fx.Impact(hit.point, hit.normal, false, hit.collider.transform);
                }
            }

            if (fx == null) return;

            fx.Tracer(muzzle, end, data.Pellets > 1 ? 0.012f : 0.022f);

            // enemy bullets that fly close by whiz past the player's ears
            if (!IsPlayer && Camera.main != null && UnityEngine.Random.value < 0.6f)
            {
                Vector3 listener = Camera.main.transform.position;
                Vector3 segment = end - muzzle;
                float along = Mathf.Clamp01(Vector3.Dot(listener - muzzle, segment) / Mathf.Max(segment.sqrMagnitude, 0.001f));
                Vector3 closest = muzzle + segment * along;
                if (along > 0.05f && along < 0.98f && (closest - listener).sqrMagnitude < 4.0f)
                {
                    fx.PlayRandom(fx.BulletWhizSounds, closest, 0.7f);
                }
            }
        }

        private static Vector3 RandomInCone(Vector3 direction, float angle)
        {
            if (angle <= 0.0f) return direction;

            // gaussian-ish spread: most shots near the center
            Vector2 offset = (UnityEngine.Random.insideUnitCircle + UnityEngine.Random.insideUnitCircle) * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(direction);
            return rotation * Quaternion.Euler(offset.y * angle, offset.x * angle, 0.0f) * Vector3.forward;
        }

        // ---------------------------------------------------------------- reload

        private void UpdateReload(float dt)
        {
            WeaponData data = Current.Data;
            CombatEffects fx = CombatEffects.Instance;
            _reloadTime += dt;

            if (data.Reload == ReloadStyle.SingleShell)
            {
                int inserted = Mathf.FloorToInt(_reloadTime / data.ShellInsertTime + 0.4f);
                if (inserted > _reloadStage && !_cancelReload)
                {
                    _reloadStage = inserted;
                    if (Current.InfiniteReserve || Current.Reserve > 0)
                    {
                        Current.Magazine++;
                        if (!Current.InfiniteReserve) Current.Reserve--;
                        if (fx != null) fx.PlaySound(data.ShellInsertSound, MuzzlePosition, 0.8f);
                    }
                }

                ReloadProgress = (float)Current.Magazine / data.MagazineSize;
                bool done = Current.Magazine >= data.MagazineSize || (!Current.InfiniteReserve && Current.Reserve <= 0);
                bool cycleFinished = (_reloadTime % data.ShellInsertTime) / data.ShellInsertTime > 0.9f;
                if ((done || _cancelReload) && (cycleFinished || _cancelReload))
                {
                    FinishReload();
                    // chamber a round after loading an empty gun
                    if (_reloadWasEmpty) StartManualCycle(0.05f, false);
                }

                return;
            }

            ReloadProgress = Mathf.Clamp01(_reloadTime / data.ReloadTime);

            // magazine reload, driven by stages along the normalized time
            if (_reloadStage == 0 && ReloadProgress >= 0.18f)
            {
                _reloadStage = 1;
                if (fx != null) fx.PlaySound(data.MagOutSound, MuzzlePosition, 0.8f);
                DropMagazine();
            }
            else if (_reloadStage == 1 && ReloadProgress >= 0.4f)
            {
                // a fresh magazine appears in the hand at the belt
                _reloadStage = 2;
                _magazineInHand = true;
                if (_magazine != null) _magazine.gameObject.SetActive(true);
            }
            else if (_reloadStage == 2 && ReloadProgress >= 0.55f)
            {
                _reloadStage = 3;
                _magazineInHand = false;
                if (fx != null) fx.PlaySound(data.MagInSound, MuzzlePosition, 0.9f);
                int needed = data.MagazineSize - Current.Magazine;
                int taken = Current.InfiniteReserve ? needed : Mathf.Min(needed, Current.Reserve);
                Current.Magazine += taken;
                if (!Current.InfiniteReserve) Current.Reserve -= taken;
            }
            else if (_reloadStage == 3 && ReloadProgress >= 0.68f)
            {
                _reloadStage = 4;
                if (_reloadWasEmpty && fx != null)
                {
                    fx.PlaySound(data.Hold == HoldStyle.Pistol ? data.CycleSound : data.ChargeSound, MuzzlePosition, 0.9f);
                    if (data.Hold == HoldStyle.Pistol) _cycleKick = 1.0f;
                }
            }

            if (_magazine != null)
            {
                if (_magazineInHand)
                {
                    // move the part so its center (not its pivot) sits in the hand
                    _magazine.localPosition = _magazineRestPosition;
                    _magazine.localRotation = _magazineRestRotation;
                    Vector3 centerWorld = _visual.TransformPoint(_magazineCenter);
                    _magazine.position += _leftHandTarget + Vector3.down * 0.04f - centerWorld;
                }
                else if (_reloadStage != 1)
                {
                    _magazine.localPosition = _magazineRestPosition;
                    _magazine.localRotation = _magazineRestRotation;
                }
            }

            if (ReloadProgress >= 1.0f) FinishReload();
        }

        private void DropMagazine()
        {
            if (_magazine == null) return;

            // the empty magazine falls to the ground
            GameObject dropped = Instantiate(_magazine.gameObject, _magazine.position, _magazine.rotation);
            dropped.layer = CombatLayers.Debris;
            foreach (Transform t in dropped.GetComponentsInChildren<Transform>()) t.gameObject.layer = CombatLayers.Debris;
            var col = dropped.AddComponent<BoxCollider>();
            var body = dropped.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            body.linearVelocity = Vector3.down * 1.0f + transform.forward * 0.3f;
            body.angularVelocity = UnityEngine.Random.insideUnitSphere * 4.0f;
            Destroy(dropped, 8.0f);

            _magazine.gameObject.SetActive(false);
        }

        private void FinishReload()
        {
            IsReloading = false;
            ReloadProgress = 1.0f;
            _magazineInHand = false;
            if (_magazine != null)
            {
                _magazine.gameObject.SetActive(true);
                _magazine.localPosition = _magazineRestPosition;
                _magazine.localRotation = _magazineRestRotation;
            }

            Reloaded?.Invoke(Current);
        }

        // ---------------------------------------------------------------- IK

        private void OnAnimatorIK(int layerIndex)
        {
            if (_ikWeight <= 0.01f || Current == null || _visual == null) return;

            WeaponData data = Current.Data;
            float weight = _ikWeight;

            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight * 0.8f);
            _animator.SetIKPosition(AvatarIKGoal.RightHand, _visual.TransformPoint(data.GripPoint));
            _animator.SetIKRotation(AvatarIKGoal.RightHand, _handFrame * Quaternion.Euler(RightHandRotation));

            Vector3 leftRotation = data.Hold == HoldStyle.Pistol ? PistolLeftHandRotation : LeftHandRotation;
            float leftRotationWeight = IsReloading ? 0.3f : 0.8f;
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight * _supportHandWeight);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight * leftRotationWeight * _supportHandWeight);
            if (Busy) GearBodyIK();
            _animator.SetIKPosition(AvatarIKGoal.LeftHand, _leftHandTarget);
            _animator.SetIKRotation(AvatarIKGoal.LeftHand, _handFrame * Quaternion.Euler(leftRotation));

            // keep the elbows down and out
            Quaternion yaw = Quaternion.Euler(0.0f, transform.eulerAngles.y, 0.0f);
            Vector3 chest = transform.position + Vector3.up * (ShoulderHeight - CrouchDrop);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight * 0.6f);
            _animator.SetIKHintPosition(AvatarIKHint.RightElbow, chest + yaw * new Vector3(0.35f, -0.35f, -0.05f));
            _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, weight * 0.6f);
            _animator.SetIKHintPosition(AvatarIKHint.LeftElbow, chest + yaw * new Vector3(-0.35f, -0.4f, 0.1f));

            // chest and head follow the aim
            float look = weight * Mathf.Lerp(0.7f, 1.0f, AimBlend) * (1.0f - _lowerBlend * 0.6f) * (1.0f - _sprintBlend * 0.7f);
            _animator.SetLookAtWeight(look, Mathf.Lerp(0.3f, 0.55f, AimBlend), 0.85f, 0.0f, 0.5f);
            _animator.SetLookAtPosition(AimPoint);
        }
    }
}
