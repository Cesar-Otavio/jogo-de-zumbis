 using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("How many extra jumps the player can do in the air")]
        public int MaxAirJumps = 1;

        [Tooltip("The height of each jump done in the air")]
        public float AirJumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        public Vector2  LookSensitivity = new Vector2(7.5f, 5.0f);

        [Header("Combat")]
        [Tooltip("Face StrafeYaw instead of the movement direction (while aiming a weapon)")]
        public bool StrafeMode;

        public float StrafeYaw;

        [Tooltip("Scales the move speed, e.g. slower while aiming")]
        public float SpeedMultiplier = 1.0f;

        public bool BlockSprint;

        [Tooltip("Scales the look sensitivity, e.g. lower while zoomed in")]
        public float LookSensitivityScale = 1.0f;

        [Tooltip("Share of the recoil kick that stays; the rest springs back on its own")]
        [Range(0, 1)] public float RecoilKept = 0.2f;
        [Tooltip("How fast the view settles back after recoil")]
        public float RecoilRecovery = 9.0f;

        [Header("Stamina")]
        public float MaxStamina = 100.0f;
        [Tooltip("Stamina per second while sprinting")]
        public float SprintStaminaCost = 18.0f;
        [Tooltip("Stamina per second while recovering")]
        public float StaminaRegen = 25.0f;
        [Tooltip("Seconds after using stamina before it recovers")]
        public float StaminaRegenDelay = 0.8f;
        [Tooltip("After running out, stamina has to recover this much before sprinting again")]
        public float ExhaustedRecovery = 30.0f;

        [Header("Roll")]
        public float RollDistance = 4.5f;
        public float RollDuration = 0.65f;
        public float RollStaminaCost = 25.0f;
        public float RollCooldown = 0.6f;
        [Tooltip("Model root spun during the roll, Geometry by default")]
        public Transform RollVisual;
        [Tooltip("Height of the spin's pivot above the feet")]
        public float RollPivotHeight = 0.55f;

        [Header("Crouch")]
        public float CrouchSpeed = 1.4f;
        [Tooltip("Capsule height while crouched")]
        public float CrouchHeight = 1.2f;
        [Tooltip("How far the camera and the shoulders drop")]
        public float CrouchCameraDrop = 0.5f;
        [Tooltip("How far the hips drop, the feet stay planted")]
        public float CrouchBodyDrop = 0.42f;
        public float CrouchTransitionSpeed = 7.0f;

        [Tooltip("Stamina spent by the jump in the air")]
        public float AirJumpStaminaCost = 12.0f;
        [Tooltip("Scales the sprint speed, e.g. lowered when badly hurt")]
        public float SprintLimit = 1.0f;

        // extra offset of the camera target, e.g. leaning around a corner (local to the camera target's parent)
        [System.NonSerialized] public Vector3 CameraTargetOffset;
        public bool IsCrouching { get; private set; }
        public float CrouchBlend { get; private set; }

        public float Stamina { get; private set; }
        public bool Exhausted { get; private set; }
        public bool IsRolling { get; private set; }
        private Quaternion _rollTurnFrom = Quaternion.identity;
        public bool IsSprinting { get; private set; }

        // raised when a roll starts, so the combat code can grant invulnerability
        public event System.Action Rolled;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;
        private float _recoilPitch;
        private float _recoilYaw;
        private float _lastRecoilTime;

        // Camera starting position and rotation
private Vector3 _cameraStartingPosition;
private Quaternion _cameraStartingRotation;

// Variable to indicate if we are resetting the camera
public bool IsRespawning { get; set; } = false;


        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;
        private int _airJumpsRemaining;

        // stamina and roll
        private float _lastStaminaUse;
        private float _rollTime;
        private float _rollReadyAt;
        private Vector3 _rollDirection;
        private Vector3 _rollVisualPosition;
        private Quaternion _rollVisualRotation;
        private int _animIDRoll;
        private bool _hasRollParameter;
        private static readonly int _animIDMoveX = Animator.StringToHash("MoveX");
        private static readonly int _animIDMoveY = Animator.StringToHash("MoveY");
        private static readonly int _animIDCrouched = Animator.StringToHash("Crouched");
        private static readonly int _animIDProne = Animator.StringToHash("Prone");
        private bool _hasDirectionalParameters;
        private bool _hasCrouchAnimation;
        private bool _hasProneAnimation;
        // prone only exists when its animations do (see Docs/FASE3_ANIMACOES.md)
        public bool ProneAvailable => _hasProneAnimation;
        public bool IsProne { get; private set; }
        [Header("Prone (needs the prone clips)")]
        public float ProneSpeed = 0.7f;
        public float ProneHeight = 0.55f;
        public float ProneHoldTime = 0.6f;
        private float _crouchHeldSince = -1.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;
        private int _animStateJumpStart;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private ClimbingSystem _climbing;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        // crouch
        private float _standHeight;
        private Vector3 _standCenter;
        private Vector3 _cameraTargetLocal;
        private bool _cameraTargetLocalSet;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }


        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
{
    _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

    _hasAnimator = TryGetComponent(out _animator);
    _controller = GetComponent<CharacterController>();
    _input = GetComponent<StarterAssetsInputs>();
    _climbing = GetComponent<ClimbingSystem>();
#if ENABLE_INPUT_SYSTEM
    _playerInput = GetComponent<PlayerInput>();
#else
	Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

    AssignAnimationIDs();

    // Save the starting camera position and rotation
    _cameraStartingPosition = CinemachineCameraTarget.transform.position;
    _cameraStartingRotation = CinemachineCameraTarget.transform.rotation;

    // reset our timeouts on start
    _jumpTimeoutDelta = JumpTimeout;
    _fallTimeoutDelta = FallTimeout;

    Stamina = MaxStamina;
    if (RollVisual == null) RollVisual = transform.Find("Geometry");
    if (RollVisual != null)
    {
        _rollVisualPosition = RollVisual.localPosition;
        _rollVisualRotation = RollVisual.localRotation;
    }

    _standHeight = _controller.height;
    _standCenter = _controller.center;
    if (CinemachineCameraTarget != null && CinemachineCameraTarget.transform.parent == transform)
    {
        _cameraTargetLocal = CinemachineCameraTarget.transform.localPosition;
        _cameraTargetLocalSet = true;
    }

    _animIDRoll = Animator.StringToHash("Roll");
    if (_hasAnimator)
    {
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.nameHash == _animIDRoll) _hasRollParameter = true;
            // optional parameters of the 2D locomotion controller (Tools > Combat > Phase 3 > 6):
            // without them the 1D Speed blend keeps working as before
            if (parameter.nameHash == _animIDMoveX) _hasDirectionalParameters = true;
            if (parameter.nameHash == _animIDCrouched) _hasCrouchAnimation = true;
            if (parameter.nameHash == _animIDProne) _hasProneAnimation = true;
        }
    }
}

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            // while climbing, movement is driven by the ClimbingSystem
            if (_climbing != null && _climbing.isActiveAndEnabled && _climbing.IsClimbing)
            {
                GroundedCheck();
                _input.roll = false;
                _input.crouch = false;
                if (IsCrouching) SetCrouch(false, true);
                UpdateCrouchShape();
                return;
            }

            UpdateProneInput();
            if (_input.crouch)
            {
                _input.crouch = false;
                // a press while prone gets up to a crouch (if there's room); otherwise it toggles crouch
                if (IsProne) SetProne(false);
                else SetCrouch(!IsCrouching, false);
            }

            if (_input.roll)
            {
                _input.roll = false;
                // no rolling flat on the ground
                if (!IsProne) TryRoll();
            }

            if (IsRolling)
            {
                UpdateRoll();
                GroundedCheck();
                UpdateStamina(false);
                return;
            }

            // no jumping from flat on the ground
            if (IsProne) _input.jump = false;

            // jumping or sprinting gets out of a crouch, if there is room above
            if (IsCrouching && (_input.jump || (_input.sprint && _input.move != Vector2.zero)))
            {
                _input.jump = false;
                SetCrouch(false, false);
            }

            UpdateCrouchShape();
            JumpAndGravity();
            GroundedCheck();
            Move();
        }

        // ---------------------------------------------------------------- crouch

        // holding crouch for a moment goes prone, only when the prone animations exist
        private void UpdateProneInput()
        {
            if (!_hasProneAnimation) return;
            if (_input.proneHeld)
            {
                if (_crouchHeldSince < 0.0f) _crouchHeldSince = Time.time;
                if (!IsProne && Time.time - _crouchHeldSince >= ProneHoldTime && Grounded && !IsRolling) SetProne(true);
            }
            else
            {
                _crouchHeldSince = -1.0f;
            }
        }

        public void SetProne(bool prone)
        {
            if (prone == IsProne) return;
            if (prone)
            {
                IsProne = true;
                IsCrouching = true;
                return;
            }

            // getting up from the ground needs crouch room
            if (!HasRoomFor(CrouchHeight)) return;
            IsProne = false;
        }

        private bool HasRoomFor(float height)
        {
            float radius = _controller.radius * 0.9f;
            Vector3 bottom = transform.position + Vector3.up * (_controller.height + radius * 0.2f);
            Vector3 top = transform.position + Vector3.up * (height - radius);
            if (top.y <= bottom.y) return true;
            int mask = GroundLayers & ~(1 << gameObject.layer);
            return !Physics.CheckCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
        }

        // force: stand up even without room (climbing, respawn)
        public void SetCrouch(bool crouch, bool force)
        {
            if (crouch == IsCrouching) return;
            if (!crouch && !force && !HasHeadroom()) return;
            IsCrouching = crouch;
        }

        private bool HasHeadroom()
        {
            float radius = _controller.radius * 0.9f;
            Vector3 bottom = transform.position + Vector3.up * (_controller.height + radius * 0.2f);
            Vector3 top = transform.position + Vector3.up * (_standHeight - radius);
            if (top.y <= bottom.y) return true;
            int mask = GroundLayers & ~(1 << gameObject.layer);
            return !Physics.CheckCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
        }

        private void UpdateCrouchShape()
        {
            CrouchBlend = Mathf.MoveTowards(CrouchBlend, IsCrouching ? 1.0f : 0.0f, Time.deltaTime * CrouchTransitionSpeed);
            float blend = Mathf.SmoothStep(0.0f, 1.0f, CrouchBlend);

            // the capsule shrinks from the top, the feet stay on the ground
            float height = Mathf.Lerp(_standHeight, IsProne ? ProneHeight : CrouchHeight, blend);
            _controller.height = height;
            _controller.center = new Vector3(_standCenter.x, _standCenter.y - (_standHeight - height) * 0.5f, _standCenter.z);

            if (_cameraTargetLocalSet)
            {
                CinemachineCameraTarget.transform.localPosition = _cameraTargetLocal + Vector3.down * (CrouchCameraDrop * blend) + CameraTargetOffset;
            }
        }

        // lowers the hips while the feet stay where the animation put them, bending the knees
        private void OnAnimatorIK(int layerIndex)
        {
            if (!_hasAnimator || CrouchBlend <= 0.001f || IsRolling || _hasCrouchAnimation || IsProne) return;
            if (_climbing != null && _climbing.IsClimbing) return;

            float blend = Mathf.SmoothStep(0.0f, 1.0f, CrouchBlend);
            Vector3 leftFoot = _animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 rightFoot = _animator.GetIKPosition(AvatarIKGoal.RightFoot);
            Quaternion leftRotation = _animator.GetIKRotation(AvatarIKGoal.LeftFoot);
            Quaternion rightRotation = _animator.GetIKRotation(AvatarIKGoal.RightFoot);

            _animator.bodyPosition += Vector3.down * (CrouchBodyDrop * blend) + transform.forward * (0.08f * blend);
            // lean the upper body forward a little
            _animator.bodyRotation = Quaternion.AngleAxis(12.0f * blend, transform.right) * _animator.bodyRotation;

            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, blend);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, blend);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, blend);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, blend);
            _animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFoot);
            _animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFoot);
            _animator.SetIKRotation(AvatarIKGoal.LeftFoot, leftRotation);
            _animator.SetIKRotation(AvatarIKGoal.RightFoot, rightRotation);

            // knees point forward and a little out
            Vector3 knees = transform.position + Vector3.up * 0.45f + transform.forward * 0.6f;
            _animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, blend);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, blend);
            _animator.SetIKHintPosition(AvatarIKHint.LeftKnee, knees - transform.right * 0.25f);
            _animator.SetIKHintPosition(AvatarIKHint.RightKnee, knees + transform.right * 0.25f);
        }

        // e.g. melee and shoving
        public bool TrySpendStamina(float amount)
        {
            if (Stamina < amount) return false;
            Stamina -= amount;
            _lastStaminaUse = Time.time;
            return true;
        }

        // ---------------------------------------------------------------- stamina

        private void UpdateStamina(bool sprinting)
        {
            IsSprinting = sprinting;
            if (sprinting)
            {
                Stamina = Mathf.Max(0.0f, Stamina - SprintStaminaCost * Time.deltaTime);
                _lastStaminaUse = Time.time;
                if (Stamina <= 0.0f) Exhausted = true;
            }
            else if (Time.time - _lastStaminaUse >= StaminaRegenDelay)
            {
                Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegen * Time.deltaTime);
            }

            if (Exhausted && Stamina >= ExhaustedRecovery) Exhausted = false;
        }

        // ---------------------------------------------------------------- roll

        private void TryRoll()
        {
            if (!Grounded || IsRolling || Time.time < _rollReadyAt || Stamina < RollStaminaCost) return;

            // roll the way the stick points, relative to the camera, or straight ahead
            if (_input.move != Vector2.zero)
            {
                Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;
                float yaw = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
                _rollDirection = Quaternion.Euler(0.0f, yaw, 0.0f) * Vector3.forward;
            }
            else
            {
                _rollDirection = transform.forward;
            }

            // the body swings round to the roll direction over the first moments instead of snapping
            _rollTurnFrom = transform.rotation;
            _targetRotation = Quaternion.LookRotation(_rollDirection).eulerAngles.y;
            if (IsCrouching) SetCrouch(false, true);
            IsRolling = true;
            _rollTime = 0.0f;
            Stamina -= RollStaminaCost;
            _lastStaminaUse = Time.time;

            if (_hasAnimator && _hasRollParameter) _animator.SetTrigger(_animIDRoll);
            Rolled?.Invoke();
        }

        private void UpdateRoll()
        {
            float previous = Mathf.Clamp01(_rollTime / RollDuration);
            _rollTime += Time.deltaTime;
            float progress = Mathf.Clamp01(_rollTime / RollDuration);

            // fast start, slowing to a stop
            float travelled = RollEase(progress) - RollEase(previous);
            _verticalVelocity = Grounded ? -2.0f : _verticalVelocity + Gravity * Time.deltaTime;
            _controller.Move(_rollDirection * (travelled * RollDistance) + Vector3.up * (_verticalVelocity * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(_rollTurnFrom, Quaternion.LookRotation(_rollDirection), Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(_rollTime / 0.12f)));

            // spin the model a full turn around its middle
            if (RollVisual != null)
            {
                float angle = 360.0f * Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.08f, 0.85f, progress));
                Quaternion spin = Quaternion.Euler(angle, 0.0f, 0.0f);
                Vector3 pivot = Vector3.up * RollPivotHeight;
                RollVisual.localRotation = spin * _rollVisualRotation;
                RollVisual.localPosition = _rollVisualPosition + pivot - spin * pivot;
            }

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, 0.0f);
                _animator.SetBool(_animIDGrounded, true);
            }

            if (progress >= 1.0f) EndRoll();
        }

        private void OnDisable()
        {
            // e.g. dying mid-roll: don't leave the model spun
            if (IsRolling) EndRoll();
        }

        private static float RollEase(float t)
        {
            return 1.0f - (1.0f - t) * (1.0f - t);
        }

        private void EndRoll()
        {
            IsRolling = false;
            _rollReadyAt = Time.time + RollCooldown;
            _speed = MoveSpeed;
            _animationBlend = MoveSpeed;
            if (RollVisual != null)
            {
                RollVisual.localRotation = _rollVisualRotation;
                RollVisual.localPosition = _rollVisualPosition;
            }
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            _animStateJumpStart = Animator.StringToHash("JumpStart");
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
{
    // if respawning, reset to starting position and rotation
    if (IsRespawning)
    {
        _cinemachineTargetYaw = 0f; // Reset yaw to zero (or configure as needed)
        _cinemachineTargetPitch = 0f;

        // Reset Cinemachine Camera Target to its starting state
        CinemachineCameraTarget.transform.position = _cameraStartingPosition;
        CinemachineCameraTarget.transform.rotation = _cameraStartingRotation;

        IsRespawning = false; // Reset the respawning flag
        return;
    }

    // if there is an input and camera position is not fixed
    if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
    {
        float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

        _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier * LookSensitivity.x * LookSensitivityScale;
        _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier * LookSensitivity.y * LookSensitivityScale;
    }

    _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
    _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

    // recoil settles back a moment after the last shot
    if (Time.time - _lastRecoilTime > 0.08f)
    {
        float settle = 1.0f - Mathf.Exp(-RecoilRecovery * Time.deltaTime);
        _recoilPitch = Mathf.Lerp(_recoilPitch, 0.0f, settle);
        _recoilYaw = Mathf.Lerp(_recoilYaw, 0.0f, settle);
    }

    CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
        Mathf.Clamp(_cinemachineTargetPitch + _recoilPitch, BottomClamp, TopClamp) + CameraAngleOverride,
        _cinemachineTargetYaw + _recoilYaw,
        0.0f
    );
}

        private void Move()
        {
            // set target speed based on move speed, sprint speed and if sprint is pressed; sprinting spends stamina
            bool sprinting = _input.sprint && !BlockSprint && !Exhausted && !IsCrouching && !IsProne && _input.move != Vector2.zero && Grounded;
            UpdateStamina(sprinting);
            float targetSpeed = (sprinting ? SprintSpeed * SprintLimit : MoveSpeed) * SpeedMultiplier;
            if (IsCrouching) targetSpeed = Mathf.Min(targetSpeed, CrouchSpeed * SpeedMultiplier);
            if (IsProne) targetSpeed = Mathf.Min(targetSpeed, ProneSpeed * SpeedMultiplier);

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
            }

            if (StrafeMode)
            {
                // face the aim, turning the hips a little towards the movement so the legs don't slide sideways
                float lean = 0.0f;
                if (_input.move != Vector2.zero)
                {
                    float relative = Mathf.DeltaAngle(StrafeYaw, _targetRotation);
                    if (Mathf.Abs(relative) < 135.0f) lean = Mathf.Clamp(relative, -45.0f, 45.0f);
                }

                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, StrafeYaw + lean, ref _rotationVelocity,
                    RotationSmoothTime * 0.5f);
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }
            else if (_input.move != Vector2.zero)
            {
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                // rotate to face input direction relative to camera position
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }


            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                if (_hasDirectionalParameters)
                {
                    // the movement in the character's own frame, for the 2D blend tree (strafe, backwards, diagonals)
                    Vector3 local = transform.InverseTransformDirection(targetDirection.normalized * _animationBlend);
                    _animator.SetFloat(_animIDMoveX, local.x, 0.08f, Time.deltaTime);
                    _animator.SetFloat(_animIDMoveY, local.z, 0.08f, Time.deltaTime);
                }

                if (_hasCrouchAnimation) _animator.SetBool(_animIDCrouched, IsCrouching);
                if (_hasProneAnimation) _animator.SetBool(_animIDProne, IsProne);
                // the run clip is authored for 5.3 m/s: play it faster at higher sprint speeds so the feet keep up
                float playback = Mathf.Max(1.0f, _animationBlend / 5.335f);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude * playback);
            }
        }

        // weapon recoil: pushes the view up and to a side
        public void AddCameraRecoil(float pitchUp, float yaw)
        {
            // a small part of the kick is kept, the rest is a temporary offset that springs back
            _cinemachineTargetPitch -= pitchUp * RecoilKept;
            _cinemachineTargetYaw += yaw * RecoilKept;
            _recoilPitch = Mathf.Max(_recoilPitch - pitchUp * (1.0f - RecoilKept), -12.0f);
            _recoilYaw = Mathf.Clamp(_recoilYaw + yaw * (1.0f - RecoilKept), -4.0f, 4.0f);
            _lastRecoilTime = Time.time;
        }

        // turns the view without recoil handling, e.g. scripted aiming
        public void TurnView(float pitchDown, float yaw)
        {
            _cinemachineTargetPitch += pitchDown;
            _cinemachineTargetYaw += yaw;
        }

        // clears horizontal momentum and sets the vertical velocity, e.g. when grabbing or leaving a wall
        public void ResetMotion(float verticalVelocity)
        {
            _verticalVelocity = verticalVelocity;
            _speed = 0.0f;
            _animationBlend = 0.0f;
            _targetRotation = transform.eulerAngles.y;
            _fallTimeoutDelta = FallTimeout;
            _airJumpsRemaining = MaxAirJumps;
        }

        public void RefillStamina()
        {
            Stamina = MaxStamina;
            Exhausted = false;
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;
                _airJumpsRemaining = MaxAirJumps;

                // update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    // consume the press so it doesn't also trigger the air jump
                    _input.jump = false;

                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // double jump: a fresh press in the air replaces the vertical velocity
                if (_input.jump && _airJumpsRemaining > 0 && Stamina >= AirJumpStaminaCost)
                {
                    _airJumpsRemaining--;
                    Stamina -= AirJumpStaminaCost;
                    _lastStaminaUse = Time.time;
                    _verticalVelocity = Mathf.Sqrt(AirJumpHeight * -2f * Gravity);
                    _fallTimeoutDelta = FallTimeout;

                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, false);
                        _animator.CrossFadeInFixedTime(_animStateJumpStart, 0.1f);
                    }
                }

                // if we are not grounded, do not jump
                _input.jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private bool HasSurfaceFootsteps => TryGetComponent(out Combat.SurfaceFootstepSystem _);

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f && !HasSurfaceFootsteps)
            {
                if (FootstepAudioClips != null && FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f && LandingAudioClip != null && !HasSurfaceFootsteps)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }
        public void ResetCameraRotation(float targetYaw)
{
    // Reset the yaw and pitch to default values (targetYaw for Y rotation, and 0 for pitch)
    _cinemachineTargetYaw = targetYaw;
    _cinemachineTargetPitch = 0f;

    // Reset the camera target's rotation explicitly
    CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0f);

    Debug.Log($"Camera Yaw reset to {targetYaw} degrees.");
}
    }


}
