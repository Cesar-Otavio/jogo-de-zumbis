using StarterAssets;
using UnityEngine;

namespace Combat
{
    /* Leaning (peeking) left and right while aiming: Q / E on the keyboard, the shoulder buttons on a gamepad.
     * The same keys keep their usual job when not aiming (switch weapon / interact); PlayerCombat drops those
     * presses while a lean is held, so one key never does two things at once.
     *
     *   - the camera pivot slides sideways; the first-person sights are anchored on that pivot, so the
     *     view, the gun and the line of fire move together and stay coherent
     *   - the spine bends towards the lean (bone rotations before IK, so the hands stay on the gun)
     *   - a sphere cast limits the lean: you can't put your head or the muzzle through a wall
     */
    public class LeanController : MonoBehaviour
    {
        public float Distance = 0.42f;
        public float SpineAngle = 16.0f;
        [Tooltip("Roll of the view and the gun at full lean")]
        public float ViewRoll = 7.0f;
        public float Speed = 7.0f;
        public float ProbeRadius = 0.22f;

        // -1 full left .. 1 full right, already limited by walls
        public float Amount { get; private set; }
        public bool Active => Mathf.Abs(Amount) > 0.01f;
        public static LeanController Player { get; private set; }

        private StarterAssetsInputs _input;
        private ThirdPersonController _controller;
        private PlayerCombat _combat;
        private ClimbingSystem _climbing;
        private Animator _animator;
        private Health _health;
        private float _wanted;

        private void Awake()
        {
            _input = GetComponent<StarterAssetsInputs>();
            _controller = GetComponent<ThirdPersonController>();
            _combat = GetComponent<PlayerCombat>();
            _climbing = GetComponent<ClimbingSystem>();
            _animator = GetComponent<Animator>();
            _health = GetComponent<Health>();
            Player = this;
        }

        // the lean keys only count while aiming with a weapon in hand
        public bool Holding => _input != null && _input.aim && (_input.leanLeft || _input.leanRight) && CanLean();

        private bool CanLean()
        {
            if (_health != null && _health.IsDead) return false;
            if (_combat == null || _combat.Current == null) return false;
            if (_controller != null && (_controller.IsRolling || _controller.IsSprinting || !_controller.Grounded)) return false;
            if (_climbing != null && _climbing.IsClimbing) return false;
            return true;
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.LeanController.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            float dt = Time.deltaTime;
            float target = 0.0f;
            if (Holding) target = (_input.leanRight ? 1.0f : 0.0f) - (_input.leanLeft ? 1.0f : 0.0f);

            // how far there is room to go on that side
            if (target != 0.0f && _controller != null && _controller.CinemachineCameraTarget != null)
            {
                Transform pivot = _controller.CinemachineCameraTarget.transform;
                Vector3 side = transform.right * Mathf.Sign(target);
                int mask = CombatLayers.Sight & ~(1 << gameObject.layer);
                if (Physics.SphereCast(pivot.position, ProbeRadius, side, out RaycastHit hit, Distance, mask, QueryTriggerInteraction.Ignore))
                {
                    target *= Mathf.Clamp01(hit.distance / Distance);
                }
            }

            _wanted = Mathf.MoveTowards(_wanted, target, dt * Speed * 0.6f);
            Amount = Mathf.Lerp(Amount, _wanted, 1.0f - Mathf.Exp(-Speed * dt));
            if (Mathf.Abs(Amount) < 0.001f) Amount = 0.0f;

            if (_controller != null) _controller.CameraTargetOffset = Vector3.right * (Amount * Distance) + Vector3.down * (Mathf.Abs(Amount) * 0.06f);
        }

        // the roll of the view and the sights at the current lean
        public Quaternion ViewRollRotation => Quaternion.Euler(0.0f, 0.0f, -Amount * ViewRoll);

        // the upper body tilts around the hips; the feet stay planted by IK (the same approach as crouching).
        // bodyRotation is rebuilt from the animation every frame, so nothing accumulates
        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || !_animator.isHuman || !Active) return;

            Vector3 leftFoot = _animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 rightFoot = _animator.GetIKPosition(AvatarIKGoal.RightFoot);
            Quaternion leftRotation = _animator.GetIKRotation(AvatarIKGoal.LeftFoot);
            Quaternion rightRotation = _animator.GetIKRotation(AvatarIKGoal.RightFoot);

            _animator.bodyRotation = Quaternion.AngleAxis(-Amount * SpineAngle, transform.forward) * _animator.bodyRotation;
            _animator.bodyPosition += transform.right * (Amount * 0.05f);

            float weight = Mathf.Clamp01(Mathf.Abs(Amount) * 2.0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, weight);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, weight);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, weight);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, weight);
            _animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFoot);
            _animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFoot);
            _animator.SetIKRotation(AvatarIKGoal.LeftFoot, leftRotation);
            _animator.SetIKRotation(AvatarIKGoal.RightFoot, rightRotation);
        }
    }
}
