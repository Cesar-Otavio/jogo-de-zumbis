using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Combat
{
    /* A hinged door: opened and closed with the interact key, can be locked, can need power
     * (electronic doors), and can be battered and broken by the infected. While closed it carves
     * the navigation mesh, so enemies path around it or attack it. Barricades (for later) are a
     * layer of extra health that has to go before the door itself takes damage.
     */
    public class InteractiveDoor : MonoBehaviour, IInteractable, IBreakable
    {
        [Tooltip("The moving part, pivoting on its hinge")]
        public Transform Leaf;
        public float OpenAngle = 100.0f;
        [Tooltip("Fraction of the swing per second")]
        public float SwingSpeed = 2.2f;
        public bool StartsOpen;

        [Header("Access")]
        public bool Locked;
        [Tooltip("Electronic door: needs its power sector to be running")]
        public bool RequiresPower;
        [Tooltip("Power grid sector, -1 = work it out from the position")]
        public int Sector = -1;
        public string DisplayName = "porta";

        [Header("Toughness")]
        public float MaxHealth = 120.0f;
        [Tooltip("Ordinary infected can break it too, not only the ones that break doors")]
        public bool BreakableByAnyInfected;

        [Header("Barricade")]
        public float MaxBarricade = 150.0f;

        [Header("Audio")]
        public AudioClip OpenSound;
        public AudioClip CloseSound;
        public AudioClip LockedSound;
        public AudioClip[] HitSounds;
        public AudioClip BreakSound;

        public static readonly List<InteractiveDoor> All = new List<InteractiveDoor>();

        public bool IsOpen { get; private set; }
        public bool IsBroken { get; private set; }
        public float Health { get; private set; }
        public float Barricade { get; private set; }
        public bool IsBarricaded => Barricade > 0.0f;
        public bool HasPower => !RequiresPower || PowerGridManager.Instance == null || PowerGridManager.Instance.GetState(Sector) == PowerState.Normal;

        public event Action<InteractiveDoor> Broken;

        public Vector3 InteractionPoint => transform.position + Vector3.up * 1.0f;
        public Vector3 Position => transform.position;
        public bool BlocksPath => !IsBroken && (!IsOpen || IsBarricaded);
        public float InteractionRange => 2.2f;

        private float _openAmount;
        private Quaternion _closedRotation;
        private NavMeshObstacle _obstacle;
        private float _lastHitSubtitle;

        public string Prompt
        {
            get
            {
                if (IsBroken) return "";
                if (IsBarricaded) return Interactables.Key + $"Remover barricada ({Mathf.CeilToInt(Barricade)}/{Mathf.CeilToInt(MaxBarricade)})";
                if (Locked) return $"<color=#FF8866>{Capitalized} trancada</color>";
                if (!HasPower) return $"<color=#FF8866>Sem energia</color> — ligue o gerador do setor";
                return Interactables.Key + (IsOpen ? "Fechar " : "Abrir ") + DisplayName;
            }
        }

        private string Capitalized => DisplayName.Length > 0 ? char.ToUpperInvariant(DisplayName[0]) + DisplayName.Substring(1) : DisplayName;

        private void Awake()
        {
            if (Leaf == null && transform.childCount > 0) Leaf = transform.GetChild(0);
            if (Leaf != null) _closedRotation = Leaf.localRotation;
            Health = MaxHealth;
            IsOpen = StartsOpen;
            _openAmount = StartsOpen ? 1.0f : 0.0f;

            _obstacle = GetComponent<NavMeshObstacle>();
            if (_obstacle == null && Leaf != null)
            {
                _obstacle = gameObject.AddComponent<NavMeshObstacle>();
                var bounds = LeafBounds();
                _obstacle.shape = NavMeshObstacleShape.Box;
                _obstacle.center = transform.InverseTransformPoint(bounds.center);
                _obstacle.size = new Vector3(Mathf.Max(bounds.size.x, 0.4f), bounds.size.y, Mathf.Max(bounds.size.z, 0.4f));
            }

            if (_obstacle != null) _obstacle.carving = true;
            ApplyPose();
        }

        private void Start()
        {
            if (Sector < 0 && PowerGridManager.Instance != null) Sector = PowerGridManager.Instance.SectorAt(transform.position);
        }

        private void OnEnable()
        {
            All.Add(this);
            Interactables.Register(this);
            Breakables.Register(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
            Interactables.Unregister(this);
            Breakables.Unregister(this);
        }

        private Bounds LeafBounds()
        {
            var renderers = Leaf.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Leaf.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            // the obstacle is in the door's local space, the bounds are in world space
            Vector3 size = transform.InverseTransformVector(bounds.size);
            return new Bounds(bounds.center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
        }

        private void Update()
        {
            if (IsBroken || Leaf == null) return;
            float target = IsOpen ? 1.0f : 0.0f;
            if (Mathf.Approximately(_openAmount, target)) return;
            _openAmount = Mathf.MoveTowards(_openAmount, target, Time.deltaTime * SwingSpeed);
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (Leaf != null) Leaf.localRotation = _closedRotation * Quaternion.Euler(0.0f, OpenAngle * Mathf.SmoothStep(0.0f, 1.0f, _openAmount), 0.0f);
            // closed (or barricaded): the navigation mesh is cut here
            if (_obstacle != null) _obstacle.enabled = !IsBroken && (!IsOpen || IsBarricaded);
        }

        // ---------------------------------------------------------------- use

        public void Interact(PlayerCombat player)
        {
            if (IsBroken) return;

            // the player's own barricade can always come down again: a route is never sealed for good
            if (IsBarricaded)
            {
                bool refund = Barricade >= MaxBarricade * 0.5f;
                Barricade = 0.0f;
                ApplyPose();
                InventoryManager inventory = player != null ? player.GetComponent<InventoryManager>() : null;
                if (refund && inventory != null) inventory.Add(QuickItem.Barricade, 1, false);
                if (player != null) player.ShowNotice(refund ? "Barricada removida (kit recuperado)" : "Barricada removida");
                return;
            }

            if (Locked || !HasPower)
            {
                Play(LockedSound, 0.8f);
                if (player != null) player.ShowNotice(Locked ? $"{Capitalized} trancada" : "Sem energia");
                return;
            }

            SetOpen(!IsOpen);
            // doors aren't quiet
            NoiseSystem.Emit(transform.position, 9.0f, NoiseCategory.Door, player != null ? Factions.Player : Factions.Environment, player != null ? player.gameObject : gameObject);
        }

        public void SetOpen(bool open)
        {
            if (IsBroken || open == IsOpen) return;
            IsOpen = open;
            Play(open ? OpenSound : CloseSound, 0.9f);
            ApplyPose();
        }

        // e.g. a mission handing out the key, or the power coming back on
        public void Unlock()
        {
            Locked = false;
        }

        public void Lock()
        {
            SetOpen(false);
            Locked = true;
        }

        // ---------------------------------------------------------------- damage

        // an infected battering it; only some of them can actually break it
        public void Hit(float damage, bool canBreak, GameObject attacker)
        {
            if (IsBroken) return;

            Play(HitSounds, 1.0f);
            NoiseSystem.Emit(transform.position, NoiseSystem.DefaultRadius(NoiseCategory.Door), NoiseCategory.Door, Factions.Environment, gameObject);
            if (Time.time - _lastHitSubtitle > 3.0f)
            {
                _lastHitSubtitle = Time.time;
                Subtitles.Show("[pancadas na porta]", transform.position, 2.5f);
            }

            if (IsBarricaded)
            {
                Barricade = Mathf.Max(0.0f, Barricade - damage);
                ApplyPose();
                return;
            }

            if (!canBreak && !BreakableByAnyInfected) return;
            Health -= damage;
            if (Health <= 0.0f) Break(attacker != null ? (transform.position - attacker.transform.position).normalized : transform.forward);
        }

        // for the barricade system: planks or furniture add health in front of the door
        public void AddBarricade(float amount)
        {
            if (IsBroken) return;
            SetOpen(false);
            Barricade = Mathf.Min(MaxBarricade, Barricade + amount);
            ApplyPose();
        }

        private void Break(Vector3 direction)
        {
            IsBroken = true;
            IsOpen = true;
            if (_obstacle != null) _obstacle.enabled = false;
            Play(BreakSound, 1.0f);
            Subtitles.Show("[porta arrombada]", transform.position, 3.0f);

            // the leaf comes off its hinges
            if (Leaf != null)
            {
                Leaf.SetParent(null, true);
                var body = Leaf.gameObject.AddComponent<Rigidbody>();
                body.mass = 40.0f;
                body.AddForce((direction + Vector3.up * 0.3f) * 250.0f, ForceMode.Impulse);
                body.AddTorque(UnityEngine.Random.insideUnitSphere * 40.0f, ForceMode.Impulse);
                foreach (Transform t in Leaf.GetComponentsInChildren<Transform>()) t.gameObject.layer = CombatLayers.Debris;
            }

            Broken?.Invoke(this);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null && CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(clip, InteractionPoint, volume, 0.06f, 1.0f, 2.0f, 40.0f);
        }

        private void Play(AudioClip[] clips, float volume)
        {
            if (clips != null && clips.Length > 0) Play(clips[UnityEngine.Random.Range(0, clips.Length)], volume);
        }

        // a closed door close to `from` that stands between it and `to`
        public static InteractiveDoor Blocking(Vector3 from, Vector3 to, float radius)
        {
            Vector3 toTarget = to - from;
            toTarget.y = 0.0f;
            foreach (InteractiveDoor door in All)
            {
                if (door.IsBroken || (door.IsOpen && !door.IsBarricaded)) continue;
                Vector3 toDoor = door.transform.position - from;
                toDoor.y = 0.0f;
                if (toDoor.magnitude > radius + 0.8f) continue;
                if (toTarget.sqrMagnitude > 0.01f && Vector3.Dot(toDoor.normalized, toTarget.normalized) < 0.2f) continue;
                return door;
            }

            return null;
        }
    }
}
