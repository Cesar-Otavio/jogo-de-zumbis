using System;
using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat
{
    /* Short camera sequences for the campaign: the intro, boss entrances, the ending. Each shot
     * moves a camera between two points looking at a target, with letterbox bars and a caption.
     * Built in code (no Timeline asset or animation needed); Space, Enter or Esc skips. The player
     * can't act or be hurt while it plays.
     */
    public class CinematicPlayer : MonoBehaviour
    {
        public struct Shot
        {
            public Vector3 From;
            public Vector3 To;
            public Vector3 LookFrom;
            public Vector3 LookTo;
            public float Duration;
            public string Title;
            public string Caption;
        }

        public static CinematicPlayer Instance { get; private set; }
        public static bool Playing { get; private set; }

        private Camera _camera;
        private RectTransform _root;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _caption;
        private TextMeshProUGUI _skip;
        private GameObject _canvas;
        private readonly List<Shot> _shots = new List<Shot>();
        private int _index;
        private float _shotStarted;
        private Action _onDone;
        private PlayerCombat _player;
        private CombatHUD _hud;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Playing = false;
        }

        private void Start()
        {
            var cameraObject = new GameObject("CinematicCamera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            _camera.fieldOfView = 45.0f;
            _camera.farClipPlane = 600.0f;
            _camera.depth = 40.0f;
            cameraObject.SetActive(false);

            _root = UiKit.Canvas("Cinematic", transform, 140);
            _canvas = _root.gameObject;
            UiKit.Panel("Top", _root, new Vector2(0.0f, 0.88f), Vector2.one, Color.black);
            UiKit.Panel("Bottom", _root, Vector2.zero, new Vector2(1.0f, 0.12f), Color.black);
            _title = UiKit.Text(_root, "", 64, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.3f), TextAlignmentOptions.Center, Color.white, true);
            _caption = UiKit.Text(_root, "", 28, new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.11f), TextAlignmentOptions.Center, new Color(1, 1, 1, 0.85f));
            _caption.textWrappingMode = TextWrappingModes.Normal;
            _skip = UiKit.Text(_root, "[Espaço] pular", 20, new Vector2(0.75f, 0.9f), new Vector2(0.98f, 0.98f), TextAlignmentOptions.Right, UiKit.Muted);
            _canvas.SetActive(false);
            _player = FindAnyObjectByType<PlayerCombat>();
            _hud = FindAnyObjectByType<CombatHUD>();
        }

        public void Play(IList<Shot> shots, Action onDone)
        {
            if (shots == null || shots.Count == 0)
            {
                onDone?.Invoke();
                return;
            }

            _shots.Clear();
            _shots.AddRange(shots);
            _onDone = onDone;
            _index = 0;
            _shotStarted = Time.unscaledTime;
            Playing = true;
            _camera.gameObject.SetActive(true);
            _canvas.SetActive(true);
            if (_hud != null) _hud.SetVisible(false);
            SetPlayerActive(false);
            ShowShot();
        }

        private void SetPlayerActive(bool active)
        {
            if (_player == null) return;
            var input = _player.GetComponent<StarterAssetsInputs>();
            if (input != null)
            {
                input.fire = false;
                input.aim = false;
                input.move = Vector2.zero;
            }

            _player.enabled = active;
            var controller = _player.GetComponent<ThirdPersonController>();
            if (controller != null) controller.enabled = active;
        }

        private void ShowShot()
        {
            Shot shot = _shots[_index];
            _title.text = shot.Title ?? "";
            _caption.text = shot.Caption ?? "";
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.CinematicPlayer.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (!Playing) return;

            // safe while watching
            if (_player != null) _player.Health.InvulnerableUntil = Mathf.Max(_player.Health.InvulnerableUntil, Time.time + 1.0f);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                Finish();
                return;
            }

            Shot shot = _shots[_index];
            float t = Mathf.Clamp01((Time.unscaledTime - _shotStarted) / Mathf.Max(shot.Duration, 0.01f));
            float eased = Mathf.SmoothStep(0.0f, 1.0f, t);
            _camera.transform.position = Vector3.Lerp(shot.From, shot.To, eased);
            _camera.transform.LookAt(Vector3.Lerp(shot.LookFrom, shot.LookTo, eased));
            float fade = Mathf.Clamp01(Mathf.Min(t * 4.0f, (1.0f - t) * 4.0f));
            _title.alpha = fade;
            _caption.alpha = fade;

            if (t < 1.0f) return;
            _index++;
            if (_index >= _shots.Count)
            {
                Finish();
                return;
            }

            _shotStarted = Time.unscaledTime;
            ShowShot();
        }

        private void Finish()
        {
            Playing = false;
            _camera.gameObject.SetActive(false);
            _canvas.SetActive(false);
            if (_hud != null && GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Playing) _hud.SetVisible(true);
            SetPlayerActive(true);
            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        // a camera circling a point, e.g. a boss entrance
        public static Shot Orbit(Vector3 center, float radius, float height, float fromAngle, float toAngle, float duration, string title, string caption)
        {
            Vector3 a = center + Quaternion.Euler(0.0f, fromAngle, 0.0f) * Vector3.forward * radius + Vector3.up * height;
            Vector3 b = center + Quaternion.Euler(0.0f, toAngle, 0.0f) * Vector3.forward * radius + Vector3.up * height;
            return new Shot { From = a, To = b, LookFrom = center + Vector3.up * 1.5f, LookTo = center + Vector3.up * 1.5f, Duration = duration, Title = title, Caption = caption };
        }
    }
}
