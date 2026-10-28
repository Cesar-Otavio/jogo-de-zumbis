using System.Collections;
using UnityEngine;

/* Ledge climbing for the ThirdPersonController.
 * While in the air, move towards the edge of any block within reach to grab it and hang.
 * While hanging: move towards the wall (or Jump) climbs up onto the block, move away lets go,
 * move sideways shimmies along the edge.
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(ThirdPersonController))]
    [DefaultExecutionOrder(100)] // run after ThirdPersonController so our animator values win
    public class ClimbingSystem : MonoBehaviour
    {
        [Header("Ledge Grab")]
        [Tooltip("Lowest ledge, measured from the feet, that can be grabbed")]
        public float MinLedgeHeight = 0.8f;

        [Tooltip("Highest ledge, measured from the feet, that can be grabbed")]
        public float MaxLedgeHeight = 2.1f;

        [Tooltip("Height of the ledge above the feet while hanging")]
        public float HangHeight = 1.9f;

        [Tooltip("How far in front of the capsule a wall can be grabbed")]
        public float GrabDistance = 0.35f;

        [Tooltip("Distance kept between the capsule surface and the wall while hanging")]
        public float WallOffset = 0.08f;

        [Tooltip("Max angle between the character's facing and the wall to grab it")]
        [Range(0f, 90f)]
        public float MaxGrabAngle = 50.0f;

        [Tooltip("What layers can be grabbed")]
        public LayerMask LedgeLayers = 1;

        [Header("Hanging")]
        [Tooltip("Sideways speed along the ledge in m/s")]
        public float ShimmySpeed = 1.2f;

        [Tooltip("How fast the character snaps into the hang position")]
        public float SnapSpeed = 8.0f;

        [Tooltip("Minimum time hanging before holding forward climbs up")]
        public float MinHangTime = 0.35f;

        [Tooltip("Duration of the climb onto the ledge")]
        public float ClimbUpDuration = 0.9f;

        [Tooltip("Time before the character can grab a ledge again after letting go")]
        public float RegrabTimeout = 0.4f;

        [Tooltip("Distance between the hands on the ledge")]
        public float HandSpacing = 0.45f;

        public bool IsClimbing { get; private set; }

        private struct Ledge
        {
            public Vector3 Point;      // on the top edge, in front of the character
            public Vector3 Normal;     // wall normal, pointing away from the wall
            public Vector3 HangPosition;
        }

        private CharacterController _controller;
        private ThirdPersonController _thirdPerson;
        private StarterAssetsInputs _input;
        private Animator _animator;
        private bool _hasAnimator;
        private GameObject _mainCamera;

        private Ledge _ledge;
        private bool _climbingUp;
        private bool _handsOnLedge;
        private float _hangTime;
        private float _regrabTimeoutDelta;
        private float _handIKWeight;

        // animation IDs
        private int _animIDHanging;
        private int _animIDClimbUp;

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _thirdPerson = GetComponent<ThirdPersonController>();
            _input = GetComponent<StarterAssetsInputs>();
            _hasAnimator = TryGetComponent(out _animator);
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");

            _animIDHanging = Animator.StringToHash("Hanging");
            _animIDClimbUp = Animator.StringToHash("ClimbUp");
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.ClimbingSystem.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (_regrabTimeoutDelta > 0.0f)
            {
                _regrabTimeoutDelta -= Time.deltaTime;
            }

            if (_climbingUp) return;

            if (IsClimbing)
            {
                Hang();
            }
            else if (_regrabTimeoutDelta <= 0.0f && !_thirdPerson.Grounded && _input.move.sqrMagnitude > 0.01f)
            {
                // move input is camera relative, but the character always turns towards it,
                // so any input while facing a wall means reaching for it
                if (FindLedge(transform.position, transform.forward, out Ledge ledge) &&
                    Vector3.Angle(transform.forward, -ledge.Normal) <= MaxGrabAngle)
                {
                    Grab(ledge);
                }
            }
        }

        private bool FindLedge(Vector3 feet, Vector3 direction, out Ledge ledge)
        {
            ledge = default;
            float reach = _controller.radius + GrabDistance;

            // there must be a wall in front, just below the lowest grabbable ledge
            Vector3 wallOrigin = feet + Vector3.up * (MinLedgeHeight - 0.1f);
            if (!Physics.Raycast(wallOrigin, direction, out RaycastHit wallHit, reach, LedgeLayers,
                    QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(wallHit.normal.y) > 0.3f) return false;

            Vector3 normal = new Vector3(wallHit.normal.x, 0.0f, wallHit.normal.z).normalized;

            // look down onto the top of the wall from above our reach
            Vector3 topOrigin = wallHit.point - normal * 0.15f;
            topOrigin.y = feet.y + MaxLedgeHeight + 0.3f;
            if (Physics.CheckSphere(topOrigin, 0.05f, LedgeLayers, QueryTriggerInteraction.Ignore)) return false;

            float castLength = MaxLedgeHeight - MinLedgeHeight + 0.4f;
            if (!Physics.Raycast(topOrigin, Vector3.down, out RaycastHit topHit, castLength, LedgeLayers,
                    QueryTriggerInteraction.Ignore)) return false;
            if (topHit.normal.y < 0.7f) return false;

            float height = topHit.point.y - feet.y;
            if (height < MinLedgeHeight || height > MaxLedgeHeight) return false;

            // the hang position must be free
            Vector3 hang = wallHit.point + normal * (_controller.radius + WallOffset);
            hang.y = topHit.point.y - HangHeight;
            if (!CapsuleFree(hang, 0.9f)) return false;

            ledge.Point = new Vector3(wallHit.point.x, topHit.point.y, wallHit.point.z);
            ledge.Normal = normal;
            ledge.HangPosition = hang;
            return true;
        }

        private bool CapsuleFree(Vector3 feet, float radiusScale)
        {
            float radius = _controller.radius * radiusScale;
            Vector3 bottom = feet + Vector3.up * (_controller.radius + 0.05f);
            Vector3 top = feet + Vector3.up * (_controller.height - _controller.radius);
            return !Physics.CheckCapsule(bottom, top, radius, LedgeLayers, QueryTriggerInteraction.Ignore);
        }

        private void Grab(Ledge ledge)
        {
            IsClimbing = true;
            _handsOnLedge = true;
            _hangTime = 0.0f;
            _ledge = ledge;
            _controller.enabled = false;
            _input.jump = false;
            _thirdPerson.ResetMotion(0.0f);

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDHanging, true);
            }
        }

        private void Hang()
        {
            Vector3 toWall = -_ledge.Normal;
            Vector3 right = Vector3.Cross(Vector3.up, toWall);

            // read the input relative to the wall, whatever way the camera faces
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y);
            float cameraYaw = _mainCamera != null ? _mainCamera.transform.eulerAngles.y : transform.eulerAngles.y;
            Vector3 worldInput = Quaternion.Euler(0.0f, cameraYaw, 0.0f) * inputDirection;
            float forward = Vector3.Dot(worldInput, toWall);
            float sideways = Vector3.Dot(worldInput, right);

            _hangTime += Time.deltaTime;

            if (_input.jump || (forward > 0.5f && _hangTime >= MinHangTime))
            {
                _input.jump = false;
                TryClimbUp();
                return;
            }

            if (forward < -0.5f)
            {
                Release();
                return;
            }

            // shimmy along the edge, but only while there is still an edge to hold
            if (Mathf.Abs(sideways) > 0.3f)
            {
                Vector3 next = _ledge.HangPosition + right * (Mathf.Sign(sideways) * ShimmySpeed * Time.deltaTime);
                Vector3 probe = next + right * (Mathf.Sign(sideways) * _controller.radius);
                probe.y = _ledge.Point.y - HangHeight;
                if (FindLedge(probe, toWall, out Ledge edgeAhead) &&
                    Mathf.Abs(edgeAhead.Point.y - _ledge.Point.y) < 0.3f &&
                    FindLedge(new Vector3(next.x, probe.y, next.z), toWall, out Ledge moved))
                {
                    _ledge = moved;
                }
            }

            transform.position = Vector3.MoveTowards(transform.position, _ledge.HangPosition, SnapSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toWall),
                SnapSpeed * Time.deltaTime);
        }

        private void TryClimbUp()
        {
            // there must be room to stand on top
            Vector3 standPosition = _ledge.Point - _ledge.Normal * (_controller.radius + 0.25f);
            if (!CapsuleFree(standPosition + Vector3.up * 0.02f, 1.0f)) return;

            StartCoroutine(ClimbUp(standPosition));
        }

        private IEnumerator ClimbUp(Vector3 standPosition)
        {
            _climbingUp = true;

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDHanging, false);
                _animator.SetTrigger(_animIDClimbUp);
            }

            Vector3 start = transform.position;
            Vector3 lip = new Vector3(start.x, _ledge.Point.y + 0.05f, start.z);
            float riseTime = ClimbUpDuration * 0.6f;
            float stepTime = ClimbUpDuration - riseTime;

            // pull the body up over the edge, then step forward onto the top
            for (float t = 0.0f; t < riseTime; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, lip, Mathf.SmoothStep(0.0f, 1.0f, t / riseTime));
                yield return null;
            }

            _handsOnLedge = false;

            for (float t = 0.0f; t < stepTime; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(lip, standPosition, Mathf.SmoothStep(0.0f, 1.0f, t / stepTime));
                yield return null;
            }

            transform.position = standPosition;
            _climbingUp = false;
            Release();
        }

        private void Release()
        {
            IsClimbing = false;
            _controller.enabled = true;
            _regrabTimeoutDelta = RegrabTimeout;
            _thirdPerson.ResetMotion(0.0f);

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDHanging, false);
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            // keep the hands on the edge while hanging and while pulling up
            float target = IsClimbing && _handsOnLedge ? 1.0f : 0.0f;
            _handIKWeight = Mathf.MoveTowards(_handIKWeight, target, Time.deltaTime * 6.0f);
            if (_handIKWeight <= 0.0f) return;

            Vector3 toWall = -_ledge.Normal;
            Vector3 right = Vector3.Cross(Vector3.up, toWall);
            Vector3 center = new Vector3(transform.position.x, _ledge.Point.y, transform.position.z) +
                             toWall * (_controller.radius + WallOffset + 0.02f);
            Quaternion handRotation = Quaternion.LookRotation(toWall, Vector3.up);

            SetHandIK(AvatarIKGoal.LeftHand, center - right * (HandSpacing * 0.5f), handRotation);
            SetHandIK(AvatarIKGoal.RightHand, center + right * (HandSpacing * 0.5f), handRotation);
        }

        private void SetHandIK(AvatarIKGoal goal, Vector3 position, Quaternion rotation)
        {
            _animator.SetIKPositionWeight(goal, _handIKWeight);
            _animator.SetIKRotationWeight(goal, _handIKWeight * 0.5f);
            _animator.SetIKPosition(goal, position);
            _animator.SetIKRotation(goal, rotation);
        }

        private void OnDrawGizmosSelected()
        {
            if (!IsClimbing) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_ledge.Point, 0.08f);
            Gizmos.DrawLine(_ledge.Point, _ledge.Point + _ledge.Normal * 0.5f);
        }
    }
}
