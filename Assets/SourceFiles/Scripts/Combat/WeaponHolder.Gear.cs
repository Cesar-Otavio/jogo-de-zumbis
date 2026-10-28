using System;
using UnityEngine;

namespace Combat
{
    /* Moving weapons between the hands and the body, as physical actions instead of models popping in and out.
     *
     *   Stowing  the gun in the hands travels to its carry socket (pistol: holster on the right hip; long guns:
     *            across the back; an SMG optionally across the chest). The right hand rides the grip, the
     *            support hand lets go early. At the end the same model becomes the carried one.
     *   Drawing  the right hand reaches the carried gun first (it doesn't move), then the gun travels from the
     *            socket to the ready pose; the support hand comes back at the end.
     *   Grabbing like drawing, but from where a weapon lies in the world (a pickup): the body dips a little.
     *
     * The carry sockets follow the hips and chest bones, so carried guns move with the body (rolls included).
     * Nothing is ever destroyed and rebuilt in between, so a gun can't be seen twice or vanish. An interrupted
     * sequence (roll, climb, death, drop) completes instantly to its end state. Firing waits until Ready.
     * No animation clip is needed: the motion is IK-driven, with timings tuned for weight.
     */
    public partial class WeaponHolder
    {
        public enum GearState
        {
            Ready,
            Stowing,
            Drawing
        }

        [Header("Carrying")]
        public float StowTime = 0.32f;
        public float DrawTime = 0.4f;
        public float GrabTime = 0.6f;
        [Tooltip("Right-hip holster, in the hips' frame (right, up, forward)")]
        public Vector3 HolsterOffset = new Vector3(0.2f, -0.06f, 0.02f);
        [Tooltip("Centre of a long gun slung on the back, in the chest's frame")]
        public Vector3 BackOffset = new Vector3(0.0f, 0.02f, -0.2f);
        [Tooltip("Carry submachine guns across the chest instead of on the back")]
        public bool SubmachineGunOnChest = true;
        public Vector3 ChestOffset = new Vector3(0.0f, -0.12f, 0.2f);

        public GearState Gear { get; private set; } = GearState.Ready;
        public bool Busy => Gear != GearState.Ready;
        public event Action<WeaponData> GearSound;

        private float _gearTime;
        private float _gearDuration;
        private WeaponInstance _pendingDraw;
        private bool _drawFromWorld;
        private Pose _drawFrom;
        private bool _drawFromCarry;
        private int _drawCarrySide;
        private int _stowSide;
        private bool _stowSoundPlayed;
        private bool _grabSoundPlayed;
        private float _supportHandWeight = 1.0f;
        private float _bodyDip;

        private Transform _hips;
        private Transform _chest;
        private Quaternion _hipsRest = Quaternion.identity;
        private Quaternion _chestRest = Quaternion.identity;
        private bool _restCaptured;

        // ---------------------------------------------------------------- requests

        /* Puts the gun in the hands away and draws `next` (already carried). `next` null just holsters. */
        public void SwitchTo(WeaponInstance next)
        {
            FinishGear();
            CancelReloadForGear();
            if (Current == null)
            {
                if (next == null) return;
                // the carried model is the one that comes to the hands
                Transform carried = _backVisual;
                WeaponData carriedData = _backData;
                int carriedSide = _backSide;
                _backVisual = null;
                _backData = null;
                BeginDraw(next, false, default, carried, carriedData, carriedSide);
                return;
            }

            _pendingDraw = next;
            _drawFromWorld = false;
            BeginStow();
        }

        /* Takes `weapon` from a pose in the world (a pickup); if a gun is in the hands it is stowed first,
         * unless it was already dropped by the caller. */
        public void Grab(WeaponInstance weapon, Pose from)
        {
            FinishGear();
            CancelReloadForGear();
            if (Current == null)
            {
                BeginDraw(weapon, true, from);
                return;
            }

            _pendingDraw = weapon;
            _drawFromWorld = true;
            _drawFrom = from;
            BeginStow();
        }

        // an interrupted sequence lands on its final state at once
        public void FinishGear()
        {
            if (Gear == GearState.Stowing) CompleteStow();
            if (Gear == GearState.Drawing) CompleteDraw();
        }

        private void CancelReloadForGear()
        {
            if (!IsReloading) return;
            // the magazine never got in: no ammo is added (that only happens in FinishReload)
            IsReloading = false;
            _magazineInHand = false;
            if (_magazine != null)
            {
                _magazine.gameObject.SetActive(true);
                _magazine.localPosition = _magazineRestPosition;
                _magazine.localRotation = _magazineRestRotation;
            }
        }

        private void BeginStow()
        {
            Gear = GearState.Stowing;
            _gearTime = 0.0f;
            _gearDuration = StowTime * (Current.Data.Class == WeaponClass.Pistol ? 0.85f : 1.0f);
            _stowSoundPlayed = false;
            // two long guns on the back sit on opposite diagonals
            _stowSide = _backData != null && IsBackCarried(_backData) && IsBackCarried(Current.Data) ? 1 - BackSideOf(_backData) : 0;
        }

        private void CompleteStow()
        {
            WeaponInstance next = _pendingDraw;
            _pendingDraw = null;
            Gear = GearState.Ready;

            // the gun that was drawable on the body becomes the next one in the hands
            Transform nextVisual = _backVisual;
            WeaponData nextVisualData = _backData;
            int nextSide = _backSide;

            // the stowed gun is now the carried one
            _backVisual = _visual;
            _backData = Current?.Data;
            _backSide = _stowSide;
            _visual = null;
            Current = null;

            if (next == null)
            {
                if (nextVisual != null) Destroy(nextVisual.gameObject);
                return;
            }

            if (_drawFromWorld)
            {
                // the old carried gun was dropped by the caller, or there was none
                if (nextVisual != null) Destroy(nextVisual.gameObject);
                BeginDraw(next, true, _drawFrom);
                return;
            }

            BeginDraw(next, false, default, nextVisual, nextVisualData, nextSide);
        }

        private void BeginDraw(WeaponInstance weapon, bool fromWorld, Pose from, Transform carriedVisual = null, WeaponData carriedData = null, int carriedSide = 0)
        {
            Current = weapon;
            _fireCooldown = 0.25f;
            _bloom = 0.0f;
            _manualCycleStart = -1.0f;
            IsReloading = false;
            _magazineInHand = false;

            Transform visual = carriedVisual != null && WeaponUpgrades.SameWeapon(carriedData, weapon.Data) ? carriedVisual : null;
            if (visual == null)
            {
                if (carriedVisual != null) Destroy(carriedVisual.gameObject);
                visual = BuildVisual(weapon.Data, "Weapon_" + weapon.Data.DisplayName);
            }

            _drawFromCarry = !fromWorld;
            _drawCarrySide = carriedSide;
            if (fromWorld)
            {
                _drawFrom = from;
            }
            else
            {
                CarryPose(weapon.Data, carriedSide, out Vector3 p, out Quaternion r);
                _drawFrom = new Pose(p, r);
            }

            visual.SetPositionAndRotation(_drawFrom.position, _drawFrom.rotation);
            visual.localScale = Vector3.one;
            AdoptVisual(visual);

            Gear = GearState.Drawing;
            _gearTime = 0.0f;
            _gearDuration = fromWorld ? GrabTime : DrawTime * (weapon.Data.Class == WeaponClass.Pistol ? 0.8f : 1.0f);
            _grabSoundPlayed = false;
            _equipBlend = 1.0f;
            _aimRotation = Quaternion.LookRotation(transform.forward);
        }

        private void CompleteDraw()
        {
            Gear = GearState.Ready;
            _gearTime = 0.0f;
        }

        // ---------------------------------------------------------------- per frame

        private void UpdateGear(float dt)
        {
            float target = 1.0f;
            if (Gear == GearState.Ready)
            {
                _bodyDip = Mathf.MoveTowards(_bodyDip, 0.0f, dt * 3.0f);
            }
            else
            {
                // a roll, a climb or death doesn't wait for the hands
                if (Holstered || (_health != null && _health.IsDead))
                {
                    FinishGear();
                    return;
                }

                _gearTime += dt;
                float t = Mathf.Clamp01(_gearTime / _gearDuration);
                if (Gear == GearState.Stowing)
                {
                    target = Mathf.Clamp01(1.0f - t * 3.0f);
                    if (!_stowSoundPlayed && t > 0.75f)
                    {
                        _stowSoundPlayed = true;
                        PlayGearSound(Current.Data);
                    }

                    if (t >= 1.0f) CompleteStow();
                }
                else if (Gear == GearState.Drawing)
                {
                    target = Mathf.Clamp01((t - 0.7f) / 0.3f);
                    if (!_grabSoundPlayed && t > GrabPoint)
                    {
                        _grabSoundPlayed = true;
                        PlayGearSound(Current.Data);
                    }

                    _bodyDip = !_drawFromCarry ? Mathf.Sin(Mathf.Clamp01(t / (GrabPoint + 0.2f)) * Mathf.PI) * WorldDipDepth() : 0.0f;
                    if (t >= 1.0f) CompleteDraw();
                }
            }

            _supportHandWeight = Mathf.MoveTowards(_supportHandWeight, target, dt * 6.0f);
        }

        // the moment the hand closes on the gun, as a fraction of the draw
        private float GrabPoint => _drawFromCarry ? 0.32f : 0.42f;

        // reaching a gun on the ground means bending down, a little
        private float WorldDipDepth()
        {
            float height = _drawFrom.position.y - transform.position.y;
            return Mathf.Clamp(0.55f - height * 0.45f, 0.0f, 0.35f);
        }

        private void PlayGearSound(WeaponData data)
        {
            GearSound?.Invoke(data);
            if (CombatEffects.Instance == null) return;
            AudioClip clip = CombatEffects.Instance.SwitchSound;
            CombatEffects.Instance.PlaySound(clip, transform.position + Vector3.up * 1.1f, 0.55f, 0.06f, 1.0f, 2.0f, 18.0f);
        }

        /* Bends the pose computed by PoseWeapon (the ready pose) towards or from the carry pose. */
        private void ApplyGearMotion(ref Vector3 position, ref Quaternion rotation, float dt)
        {
            if (Gear == GearState.Ready || Current == null) return;
            float t = Mathf.Clamp01(_gearTime / Mathf.Max(0.01f, _gearDuration));

            if (Gear == GearState.Stowing)
            {
                CarryPose(Current.Data, _stowSide, out Vector3 carry, out Quaternion carryRotation);
                float s = Mathf.SmoothStep(0.0f, 1.0f, t);
                // a little lift over the shoulder for the back, a short drop for the holster
                Vector3 arc = Vector3.up * (Mathf.Sin(s * Mathf.PI) * (IsBackCarried(Current.Data) ? 0.18f : 0.04f));
                position = Vector3.Lerp(position, carry, s) + arc;
                rotation = Quaternion.Slerp(rotation, carryRotation, s);
            }
            else
            {
                Vector3 from = _drawFrom.position;
                Quaternion fromRotation = _drawFrom.rotation;
                if (_drawFromCarry)
                {
                    // the carried gun moves with the body until it's grabbed
                    CarryPose(Current.Data, _drawCarrySide, out from, out fromRotation);
                }

                float s = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(GrabPoint, 1.0f, t));
                Vector3 arc = Vector3.up * (Mathf.Sin(s * Mathf.PI) * (_drawFromCarry && IsBackCarried(Current.Data) ? 0.16f : 0.06f));
                position = Vector3.Lerp(from, position, s) + arc;
                rotation = Quaternion.Slerp(fromRotation, rotation, s);
            }

            // the right hand follows the gun wherever it is
            _handFrame = rotation * Quaternion.Euler(0.0f, 180.0f, 0.0f);
        }

        /* During a grab from the ground the hips drop a little and the feet stay put. */
        private void GearBodyIK()
        {
            if (_bodyDip <= 0.001f || _animator == null) return;
            Vector3 leftFoot = _animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 rightFoot = _animator.GetIKPosition(AvatarIKGoal.RightFoot);
            _animator.bodyPosition += Vector3.down * _bodyDip + transform.forward * (_bodyDip * 0.3f);
            _animator.bodyRotation = Quaternion.AngleAxis(_bodyDip * 60.0f, transform.right) * _animator.bodyRotation;
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1.0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1.0f);
            _animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFoot);
            _animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFoot);
        }

        // ---------------------------------------------------------------- carry sockets

        private int _backSide;

        private static bool IsBackCarried(WeaponData data) => data != null && data.Class != WeaponClass.Pistol;

        private int BackSideOf(WeaponData data) => data == _backData ? _backSide : 0;

        private void CaptureBodyFrames()
        {
            if (_restCaptured || _animator == null || !_animator.isHuman) return;
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _chest = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (_chest == null) _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            if (_hips == null || _chest == null) return;
            // how the bones sit relative to the character when standing: from then on, any extra
            // rotation of a bone (a roll, a lean) carries the sockets with it
            _hipsRest = Quaternion.Inverse(transform.rotation) * _hips.rotation;
            _chestRest = Quaternion.Inverse(transform.rotation) * _chest.rotation;
            _restCaptured = true;
        }

        private Quaternion BodyFrame(Transform bone, Quaternion rest)
        {
            Quaternion frame = bone.rotation * Quaternion.Inverse(rest);
            // keep the frame upright-ish: the walk sway shouldn't swing a holster around
            return Quaternion.Slerp(transform.rotation, frame, 0.65f);
        }

        /* Where a carried gun sits: position of its centre and its rotation (the barrel is the model's -Z). */
        private void CarryPose(WeaponData data, int side, out Vector3 position, out Quaternion rotation)
        {
            CaptureBodyFrames();
            Vector3 center = (data.GripPoint + data.MuzzlePoint) * 0.5f;
            if (!_restCaptured)
            {
                BackPoseFallback(data, side, out position, out rotation);
                return;
            }

            if (data.Class == WeaponClass.Pistol)
            {
                Quaternion frame = BodyFrame(_hips, _hipsRest);
                // barrel down, grip up and back, slide facing forward
                rotation = frame * Quaternion.LookRotation(Vector3.up, Vector3.forward) * Quaternion.Euler(0.0f, 0.0f, 0.0f);
                Vector3 anchor = _hips.position + frame * HolsterOffset;
                position = anchor - rotation * center;
                return;
            }

            Quaternion chestFrame = BodyFrame(_chest, _chestRest);
            if (data.Class == WeaponClass.SubmachineGun && SubmachineGunOnChest)
            {
                // across the chest, barrel down to the left
                Vector3 barrelChest = new Vector3(-0.6f, -0.55f, 0.0f);
                rotation = chestFrame * Quaternion.LookRotation(-barrelChest, Vector3.forward);
                position = _chest.position + chestFrame * ChestOffset - rotation * center;
                return;
            }

            // slung diagonally across the back, barrel up over a shoulder
            Vector3 barrel = new Vector3(side == 0 ? -0.55f : 0.55f, 0.85f, 0.0f);
            rotation = chestFrame * Quaternion.LookRotation(-barrel, Vector3.back);
            Vector3 back = _chest.position + chestFrame * (BackOffset + Vector3.back * (side * 0.04f));
            position = back - rotation * center;
        }

        private void BackPoseFallback(WeaponData data, int side, out Vector3 position, out Quaternion rotation)
        {
            Quaternion yaw = Quaternion.Euler(0.0f, transform.eulerAngles.y, 0.0f);
            Vector3 barrel = new Vector3(side == 0 ? -0.55f : 0.55f, 0.85f, 0.0f);
            rotation = yaw * Quaternion.LookRotation(-barrel, Vector3.back);
            Vector3 center = (data.GripPoint + data.MuzzlePoint) * 0.5f;
            position = transform.position + yaw * new Vector3(0.0f, 1.12f, -0.2f - side * 0.05f) - rotation * center;
        }
    }
}
