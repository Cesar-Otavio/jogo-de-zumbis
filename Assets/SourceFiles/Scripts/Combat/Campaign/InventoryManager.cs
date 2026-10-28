using System;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat
{
    public enum QuickItem
    {
        Medkit,
        Grenade,
        Molotov,
        Flare,
        Barricade,
        Decoy
    }

    /* A small, limited quick bar of consumables. 1-6 or Z choose, X uses: medkits heal, barricade
     * kits go on the nearest door or passage, throwables show their arc while X is held and fly
     * on release. Counts are capped; the shop can raise the caps a little.
     */
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Player { get; private set; }

        public static readonly string[] Names = { "Kit médico", "Granada", "Molotov", "Sinalizador", "Barricada", "Distração" };
        public static readonly string[] Short = { "KIT", "GRAN", "MOLOT", "SINAL", "BARR", "DIST" };

        [Tooltip("How many of each item fit, in QuickItem order")]
        public int[] Capacity = { 3, 3, 2, 3, 2, 3 };
        public int[] Starting = { 1, 1, 0, 1, 1, 0 };
        [Tooltip("Extra room per item bought in the shop")]
        [NonSerialized] public int ExtraCapacity;

        [Header("Medkit")]
        public float MedkitHeal = 50.0f;

        [Header("Throwing")]
        public float ThrowSpeed = 15.0f;
        public float ThrowLift = 3.5f;
        public float PreviewTime = 2.5f;

        public QuickItem Selected { get; private set; } = QuickItem.Medkit;
        public bool Aiming { get; private set; }
        public event Action Changed;

        private readonly int[] _counts = new int[6];
        private PlayerCombat _player;
        private StarterAssetsInputs _input;
        private ThirdPersonController _controller;
        private ClimbingSystem _climbing;
        private LineRenderer _arc;
        private float _nextUse;

        private void Awake()
        {
            Player = this;
            _player = GetComponent<PlayerCombat>();
            _input = GetComponent<StarterAssetsInputs>();
            _controller = GetComponent<ThirdPersonController>();
            _climbing = GetComponent<ClimbingSystem>();
            BuildArc();
        }

        private void OnDestroy()
        {
            if (Player == this) Player = null;
        }

        private void BuildArc()
        {
            var go = new GameObject("ThrowArc");
            go.transform.SetParent(transform, false);
            _arc = go.AddComponent<LineRenderer>();
            _arc.positionCount = 0;
            _arc.widthMultiplier = 0.04f;
            _arc.numCapVertices = 2;
            _arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material material = CombatEffects.Instance != null ? CombatEffects.Instance.TracerMaterial : null;
            if (material == null) material = FindAnyObjectByType<CombatEffects>()?.TracerMaterial;
            _arc.sharedMaterial = material;
            _arc.startColor = new Color(1.0f, 0.85f, 0.4f, 0.8f);
            _arc.endColor = new Color(1.0f, 0.85f, 0.4f, 0.1f);
            _arc.enabled = false;
        }

        // ---------------------------------------------------------------- contents

        public int Count(QuickItem item) => _counts[(int)item];
        public int CapacityOf(QuickItem item) => Capacity[(int)item] + ExtraCapacity;
        public bool IsFull(QuickItem item) => Count(item) >= CapacityOf(item);

        // returns how many were kept: on the quick bar first, what doesn't fit goes into the backpack
        public int Add(QuickItem item, int amount, bool notify)
        {
            int room = CapacityOf(item) - Count(item);
            int added = Mathf.Clamp(amount, 0, room);
            _counts[(int)item] += added;
            int stored = Backpack.Add(ItemCatalog.IdOf(item), amount - added);
            if (added + stored > 0)
            {
                if (notify) PickupFeed.Quick(item, added + stored);
                if (notify && stored > 0 && _player != null) _player.ShowNotice($"Barra cheia: {stored} na mochila");
                Changed?.Invoke();
            }
            else if (notify && _player != null)
            {
                PickupFeed.Full(ItemCatalog.NameOf(ItemCatalog.IdOf(item), Names[(int)item]), ItemCatalog.IconOf(item));
            }

            return added + stored;
        }

        // room on the bar and in the backpack together
        public bool CanTake(QuickItem item) => !IsFull(item) || Backpack.RoomFor(ItemCatalog.IdOf(item)) > 0;

        public bool Take(QuickItem item)
        {
            if (Count(item) <= 0) return false;
            _counts[(int)item]--;
            // a spare from the backpack takes the used one's place
            if (Backpack.Remove(ItemCatalog.IdOf(item), 1)) _counts[(int)item]++;
            Changed?.Invoke();
            return true;
        }

        // backpack -> quick bar, as many as fit; returns how many moved
        public int MoveFromBackpack(QuickItem item)
        {
            string id = ItemCatalog.IdOf(item);
            int moved = Mathf.Min(Backpack.Count(id), CapacityOf(item) - Count(item));
            if (moved <= 0 || !Backpack.Remove(id, moved)) return 0;
            _counts[(int)item] += moved;
            Changed?.Invoke();
            return moved;
        }

        // quick bar -> backpack, one at a time
        public bool MoveToBackpack(QuickItem item)
        {
            if (Count(item) <= 0 || Backpack.Add(ItemCatalog.IdOf(item), 1) < 1) return false;
            _counts[(int)item]--;
            Changed?.Invoke();
            return true;
        }

        public void ResetForRun()
        {
            for (int i = 0; i < _counts.Length; i++) _counts[i] = Mathf.Min(Starting[i], Capacity[i]);
            ExtraCapacity = 0;
            Selected = QuickItem.Medkit;
            Changed?.Invoke();
        }

        public void Select(QuickItem item)
        {
            Selected = item;
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- use

        private bool Busy => _player.Health.IsDead || !_player.enabled || _controller.IsRolling || (_climbing != null && _climbing.IsClimbing) || ShopManager.IsOpen;

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.InventoryManager.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (_input.nextItem)
            {
                _input.nextItem = false;
                Select((QuickItem)(((int)Selected + 1) % 6));
            }

            // number keys pick directly, unless a mission choice is waiting for them
            Keyboard keyboard = Keyboard.current;
            bool choicePending = MissionManager.Instance != null && MissionManager.Instance.PendingChoice.a != null;
            if (keyboard != null && !choicePending)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Select(QuickItem.Medkit);
                if (keyboard.digit2Key.wasPressedThisFrame) Select(QuickItem.Grenade);
                if (keyboard.digit3Key.wasPressedThisFrame) Select(QuickItem.Molotov);
                if (keyboard.digit4Key.wasPressedThisFrame) Select(QuickItem.Flare);
                if (keyboard.digit5Key.wasPressedThisFrame) Select(QuickItem.Barricade);
                if (keyboard.digit6Key.wasPressedThisFrame) Select(QuickItem.Decoy);
            }

            if (Busy)
            {
                Aiming = false;
                _arc.enabled = false;
                return;
            }

            bool throwable = IsThrowable(Selected);
            // on a gamepad LB also leans while aiming a gun: then it isn't an item
            if (LeanController.Player != null && LeanController.Player.Holding) _input.useItem = false;
            if (_input.useItem)
            {
                if (throwable && Count(Selected) > 0)
                {
                    Aiming = true;
                    DrawArc();
                }
                else if (!throwable && Time.time >= _nextUse)
                {
                    _nextUse = Time.time + 0.5f;
                    UseInstant();
                    _input.useItem = false;
                }
            }
            else if (Aiming)
            {
                // released: throw
                Aiming = false;
                _arc.enabled = false;
                Throw();
            }
        }

        public static bool IsThrowable(QuickItem item)
        {
            return item == QuickItem.Grenade || item == QuickItem.Molotov || item == QuickItem.Flare || item == QuickItem.Decoy;
        }

        private void UseInstant()
        {
            if (Selected == QuickItem.Medkit)
            {
                if (Count(QuickItem.Medkit) <= 0)
                {
                    _player.ShowNotice("Sem kits médicos");
                    return;
                }

                if (_player.Health.Current >= _player.Health.MaxHealth - 0.5f)
                {
                    _player.ShowNotice("Vida cheia");
                    return;
                }

                Take(QuickItem.Medkit);
                RunState.BasicOnly = false;
                _player.Health.Heal(MedkitHeal);
                CampaignAssets.Play(a => a.MedkitUse, transform.position + Vector3.up, 0.8f, 15.0f);
                _player.ShowNotice($"<color=#66FF88>+{Mathf.RoundToInt(MedkitHeal)} vida</color>");
                return;
            }

            if (Selected == QuickItem.Barricade)
            {
                if (Count(QuickItem.Barricade) <= 0)
                {
                    _player.ShowNotice("Sem kits de barricada");
                    return;
                }

                // the nearest door or passage within reach
                if (TryBarricade()) return;
                _player.ShowNotice("Chegue perto de uma porta ou ponto de barricada");
            }
        }

        private bool TryBarricade()
        {
            Vector3 position = transform.position;
            foreach (IInteractable interactable in Interactables.All)
            {
                if (Vector3.Distance(interactable.InteractionPoint, position + Vector3.up) > 2.8f) continue;
                if (interactable is BarricadePoint point)
                {
                    point.Interact(_player);
                    return true;
                }

                if (interactable is InteractiveDoor door && !door.IsBroken && door.Barricade < door.MaxBarricade - 1.0f)
                {
                    Take(QuickItem.Barricade);
                    door.AddBarricade(100.0f);
                    CampaignAssets.Play(a => a.BarricadeBuild, door.InteractionPoint, 1.0f, 30.0f);
                    _player.ShowNotice($"Porta reforçada ({Mathf.CeilToInt(door.Barricade)}/{Mathf.CeilToInt(door.MaxBarricade)})");
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- throwing

        private void LaunchState(out Vector3 origin, out Vector3 velocity)
        {
            Transform cam = Camera.main.transform;
            Vector3 forward = cam.forward;
            origin = transform.position + Vector3.up * 1.5f + transform.right * 0.25f + Vector3.ProjectOnPlane(forward, Vector3.up).normalized * 0.4f;
            velocity = forward * ThrowSpeed + Vector3.up * ThrowLift;
        }

        // the same ballistic path the projectile will follow, stopped where it hits something
        private void DrawArc()
        {
            LaunchState(out Vector3 position, out Vector3 velocity);
            const float step = 0.04f;
            int max = Mathf.CeilToInt(PreviewTime / step);
            var points = new Vector3[max + 1];
            int count = 0;
            points[count++] = position;
            int mask = CombatLayers.Sight & ~(1 << CombatLayers.Player);
            for (int i = 0; i < max; i++)
            {
                Vector3 next = position + velocity * step + 0.5f * Physics.gravity * step * step;
                velocity += Physics.gravity * step;
                if (Physics.Linecast(position, next, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore))
                {
                    points[count++] = hit.point;
                    break;
                }

                points[count++] = next;
                position = next;
            }

            _arc.positionCount = count;
            _arc.SetPositions(points);
            _arc.enabled = true;
        }

        private void Throw()
        {
            if (!Take(Selected)) return;
            RunState.BasicOnly = false;
            LaunchState(out Vector3 origin, out Vector3 velocity);
            ThrownItem.Launch(Selected, origin, velocity, gameObject);
            _player.EnterCombatStance();
            _player.Holder.MeleeThrust();
            CampaignAssets.Play(a => Selected == QuickItem.Grenade ? a.PinPull : a.ThrowWhoosh, origin, 0.8f, 20.0f);
        }
    }
}
