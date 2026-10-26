using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Combat
{
    // a pickup as the clients need to recreate it
    public struct PickupInfo : INetworkSerializable
    {
        public const byte Consumable = 1;
        public const byte Item = 2;
        public const byte Material = 3;
        public const byte Weapon = 4;

        public ushort Id;
        public byte Type;
        // consumable kind, quick item, or the weapon in the network weapon list
        public byte Sub;
        // a material's item id
        public FixedString32Bytes Name;
        // amount, or a weapon's magazine
        public short Amount;
        // a weapon's reserve
        public short Extra;
        public Vector3 Position;
        public float Yaw;
        public float Lifetime;
        public bool Floating;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref Sub);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Amount);
            serializer.SerializeValue(ref Extra);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Lifetime);
            serializer.SerializeValue(ref Floating);
        }
    }

    /* The shared world of a co-op match, kept by the host (on the NetGame object):
     *
     *   pickups  - ammo boxes, medkits, quick items, crafting materials and guns on the ground exist only
     *              where the host says: it gives each one an id and tells the clients to make it. Taking one
     *              is a claim: the first claim the host receives wins, the others see it vanish. A player's
     *              own drop (a gun, a discarded material) is asked of the host, which makes it for everyone.
     *   crafting - every material a client has came through a claim here, so the host keeps a book of each
     *              client's materials; a craft is a request the host checks against the book (and the bench
     *              nearby) before the client makes it, and a material dropped comes off the book.
     *   places   - doors, barricades, chests and the power sectors are found in the scene in the same order
     *              on every machine. The host sends their state when it changes; what a client does to one
     *              (open a door, build a barricade, open a chest, crank a generator) is a request the host
     *              carries out, answering with what that player gets back (a barricade kit, a message).
     */
    public class NetWorld : NetworkBehaviour
    {
        public static NetWorld Instance { get; private set; }

        [Tooltip("World state checks per second (sent only when something changed)")]
        public float StateRate = 4.0f;

        private const int MaxDropsPerSecond = 6;

        // ---- pickups (both sides)
        private readonly Dictionary<ushort, MonoBehaviour> _pickups = new Dictionary<ushort, MonoBehaviour>();
        private readonly Dictionary<MonoBehaviour, ushort> _pickupIds = new Dictionary<MonoBehaviour, ushort>();
        private ushort _nextPickup = 1;
        // client: claims waiting for the host, and claims it granted
        private readonly Dictionary<ushort, Action> _pending = new Dictionary<ushort, Action>();
        private readonly HashSet<ushort> _granted = new HashSet<ushort>();
        // host: pickups already given to someone. Marked at once: Destroy only happens at the end of the
        // frame, and a second claim arriving in the same frame must not find the pickup still there
        private readonly HashSet<ushort> _taken = new HashSet<ushort>();
        private readonly Dictionary<ulong, (float windowStart, int drops)> _dropBudget = new Dictionary<ulong, (float, int)>();
        // host: the materials each client player has (claims in, crafts and drops out)
        private readonly Dictionary<ulong, Dictionary<string, int>> _materials = new Dictionary<ulong, Dictionary<string, int>>();

        // ---- places (both sides, same order everywhere)
        private InteractiveDoor[] _doors = new InteractiveDoor[0];
        private BarricadePoint[] _barricades = new BarricadePoint[0];
        private LootCrate[] _crates = new LootCrate[0];
        private byte[] _sentDoors, _sentBarricades, _sentCrates, _sentSectors;
        private float _nextState;
        private bool _indexed;

        public static bool Active => Net.Active && Instance != null && Instance.IsSpawned;
        private static bool HostSide => Active && Instance.IsServer;
        private static bool ClientSide => Active && !Instance.IsServer;

        public override void OnNetworkSpawn()
        {
            Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- match start

        // called on every machine when the match starts (NetMatch.Begin), after the scene was reset
        public static void Begin()
        {
            if (Instance == null) return;
            Instance.Index();
            if (Instance.IsServer) Instance.AnnounceExisting();
            else Instance.ClearLocalPickups();
            Instance._materials.Clear();
            Instance._sentDoors = Instance._sentBarricades = Instance._sentCrates = Instance._sentSectors = null;
        }

        private void Index()
        {
            _doors = Sorted(InteractiveDoor.All.ToArray());
            _barricades = Sorted(FindObjectsByType<BarricadePoint>());
            _crates = Sorted(LootCrate.All.ToArray());
            _indexed = true;
        }

        private static T[] Sorted<T>(T[] items) where T : Component
        {
            Array.Sort(items, (a, b) =>
            {
                Vector3 p = a.transform.position, q = b.transform.position;
                int c = Mathf.RoundToInt(p.x * 100).CompareTo(Mathf.RoundToInt(q.x * 100));
                if (c == 0) c = Mathf.RoundToInt(p.z * 100).CompareTo(Mathf.RoundToInt(q.z * 100));
                if (c == 0) c = Mathf.RoundToInt(p.y * 100).CompareTo(Mathf.RoundToInt(q.y * 100));
                return c;
            });
            return items;
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.NetWorld.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (!IsSpawned || !IsServer || !Net.Active || !_indexed) return;
            if (Time.unscaledTime < _nextState) return;
            _nextState = Time.unscaledTime + 1.0f / StateRate;
            SendStateIfChanged();
        }

        // ================================================================ pickups

        // every pickup reports here as it starts (ConsumablePickup, ItemPickup, WorldItem, WeaponPickup)
        public static void Register(MonoBehaviour pickup)
        {
            if (!Active || pickup == null || Instance._pickupIds.ContainsKey(pickup)) return;
            if (Instance.IsServer) Instance.Announce(pickup);
            // a client's own pickups aren't real: only the host's are (a player's drop is asked of it)
            else Destroy(pickup.gameObject);
        }

        public static void Unregister(MonoBehaviour pickup)
        {
            if (Instance == null || pickup == null || !Instance._pickupIds.TryGetValue(pickup, out ushort id)) return;
            Instance._pickupIds.Remove(pickup);
            Instance._pickups.Remove(id);
            Instance._pending.Remove(id);
            Instance._granted.Remove(id);
            Instance._taken.Remove(id);
            // gone from the host: gone for everyone
            if (Instance.IsServer && Instance.IsSpawned) Instance.PickupGoneRpc(id);
        }

        // may this machine's player take it now? the host: yes unless already claimed; a client: only once
        // the host granted it (the first call asks, and `retry` runs when the answer comes)
        public static bool MayTake(MonoBehaviour pickup, Action retry = null)
        {
            if (!Active || pickup == null) return true;
            if (!Instance._pickupIds.TryGetValue(pickup, out ushort id)) return !ClientSide;
            // the host's own player: the same first-come rule as everyone's claims
            if (Instance.IsServer) return Instance._taken.Add(id);
            if (Instance._granted.Remove(id)) return true;
            if (Instance._pending.ContainsKey(id)) return false;
            Instance._pending[id] = retry;
            Debug.Log($"[Net] asking the host for pickup {id} ({pickup.name})");
            Instance.ClaimRpc(id);
            return false;
        }

        // a respawning ammo box or medkit (a fixed one in the scene) went out of sight on the host
        public static void Hidden(MonoBehaviour pickup)
        {
            if (HostSide && Instance._pickupIds.TryGetValue(pickup, out ushort id)) Instance.PickupGoneRpc(id);
        }

        // ... and came back
        public static void Shown(MonoBehaviour pickup)
        {
            if (HostSide && Instance._pickupIds.TryGetValue(pickup, out ushort id) && Describe(pickup, out PickupInfo info))
            {
                Instance._taken.Remove(id);
                info.Id = id;
                Instance.SpawnPickupRpc(info);
            }
        }

        // a gun or an item this machine's player dropped: on a client it becomes a request to the host
        public static void PlayerDropped(MonoBehaviour pickup)
        {
            if (!ClientSide || pickup == null) return;
            if (Describe(pickup, out PickupInfo info)) Instance.DropRpc(info);
            Destroy(pickup.gameObject);
        }

        private void Announce(MonoBehaviour pickup)
        {
            if (!Describe(pickup, out PickupInfo info)) return;
            ushort id = _nextPickup++;
            Bind(pickup, id);
            info.Id = id;
            SpawnPickupRpc(info);
        }

        private void AnnounceExisting()
        {
            var all = new List<MonoBehaviour>();
            all.AddRange(FindObjectsByType<ConsumablePickup>());
            all.AddRange(FindObjectsByType<ItemPickup>());
            all.AddRange(FindObjectsByType<WorldItem>());
            all.AddRange(FindObjectsByType<WeaponPickup>());
            foreach (MonoBehaviour pickup in all)
                if (!_pickupIds.ContainsKey(pickup)) Announce(pickup);
        }

        private void ClearLocalPickups()
        {
            var all = new List<MonoBehaviour>();
            all.AddRange(FindObjectsByType<ConsumablePickup>());
            all.AddRange(FindObjectsByType<ItemPickup>());
            all.AddRange(FindObjectsByType<WorldItem>());
            all.AddRange(FindObjectsByType<WeaponPickup>());
            foreach (MonoBehaviour pickup in all)
                if (!_pickupIds.ContainsKey(pickup)) Destroy(pickup.gameObject);
        }

        private void Bind(MonoBehaviour pickup, ushort id)
        {
            _pickups[id] = pickup;
            _pickupIds[pickup] = id;
        }

        private static bool Describe(MonoBehaviour pickup, out PickupInfo info)
        {
            info = new PickupInfo { Position = pickup.transform.position, Yaw = pickup.transform.eulerAngles.y };
            switch (pickup)
            {
                case ConsumablePickup consumable:
                    info.Type = PickupInfo.Consumable;
                    info.Sub = (byte)consumable.Type;
                    info.Lifetime = consumable.Lifetime;
                    return true;
                case ItemPickup item:
                    info.Type = PickupInfo.Item;
                    info.Sub = (byte)item.Item;
                    info.Amount = (short)item.Amount;
                    info.Lifetime = item.Lifetime;
                    return true;
                case WorldItem material:
                    info.Type = PickupInfo.Material;
                    info.Name = material.ItemId;
                    info.Amount = (short)material.Amount;
                    return true;
                case WeaponPickup weapon:
                    int index = NetMatch.WeaponIndex(weapon.Data);
                    if (index == 255) return false;
                    info.Type = PickupInfo.Weapon;
                    info.Sub = (byte)index;
                    info.Amount = (short)weapon.Magazine;
                    info.Extra = (short)weapon.Reserve;
                    info.Lifetime = weapon.Lifetime;
                    info.Floating = weapon.Floating;
                    return true;
            }

            return false;
        }

        private MonoBehaviour Create(PickupInfo info)
        {
            switch (info.Type)
            {
                case PickupInfo.Consumable:
                    return ConsumablePickup.Spawn((ConsumablePickup.Kind)info.Sub, info.Position, info.Lifetime);
                case PickupInfo.Item:
                    return ItemPickup.Spawn((QuickItem)info.Sub, Mathf.Max(1, (int)info.Amount), info.Position, info.Lifetime);
                case PickupInfo.Material:
                    return WorldItem.Spawn(info.Name.ToString(), Mathf.Max(1, (int)info.Amount), info.Position);
                case PickupInfo.Weapon:
                    WeaponData data = NetMatch.Weapon(info.Sub);
                    if (data == null) return null;
                    var go = new GameObject("NetPickup_" + data.DisplayName);
                    go.transform.SetPositionAndRotation(info.Position, Quaternion.Euler(0.0f, info.Yaw, 0.0f));
                    var weapon = go.AddComponent<WeaponPickup>();
                    weapon.Data = data;
                    weapon.Magazine = info.Amount;
                    weapon.Reserve = info.Extra;
                    weapon.Floating = info.Floating;
                    weapon.Lifetime = info.Lifetime;
                    return weapon;
            }

            return null;
        }

        [Rpc(SendTo.NotServer)]
        private void SpawnPickupRpc(PickupInfo info)
        {
            if (_pickups.TryGetValue(info.Id, out MonoBehaviour existing) && existing != null) return;
            MonoBehaviour pickup = Create(info);
            if (pickup != null) Bind(pickup, info.Id);
        }

        [Rpc(SendTo.NotServer)]
        private void PickupGoneRpc(ushort id)
        {
            _pending.Remove(id);
            if (!_pickups.TryGetValue(id, out MonoBehaviour pickup)) return;
            _pickups.Remove(id);
            if (pickup == null) return;
            _pickupIds.Remove(pickup);
            Destroy(pickup.gameObject);
        }

        [Rpc(SendTo.Server)]
        private void ClaimRpc(ushort id, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            // the first claim wins; the pickup then leaves the host, which tells everyone it's gone
            if (!_pickups.TryGetValue(id, out MonoBehaviour pickup) || pickup == null || _taken.Contains(id))
            {
                Debug.Log($"[Net] pickup {id} claimed by {sender}: already gone");
                ClaimAnswerRpc(id, false, RpcTarget.Single(sender, RpcTargetUse.Temp));
                return;
            }

            NetPlayer player = NetPlayer.For(sender);
            if (player == null || !player.IsUp || Vector3.Distance(player.Position, pickup.transform.position) > 6.0f)
            {
                Debug.Log($"[Net] pickup {id} claimed by {sender}: refused (up {player != null && player.IsUp}, {(player != null ? Vector3.Distance(player.Position, pickup.transform.position) : -1):F1} m)");
                ClaimAnswerRpc(id, false, RpcTarget.Single(sender, RpcTargetUse.Temp));
                return;
            }

            _taken.Add(id);
            Debug.Log($"[Net] pickup {id} ({pickup.name}) goes to {sender}");
            if (pickup is WorldItem material) AddMaterial(sender, material.ItemId, material.Amount);
            ClaimAnswerRpc(id, true, RpcTarget.Single(sender, RpcTargetUse.Temp));
            if (pickup is ConsumablePickup consumable && consumable.RespawnDelay > 0.0f) consumable.TakenRemotely();
            else Destroy(pickup.gameObject);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ClaimAnswerRpc(ushort id, bool granted, RpcParams rpc)
        {
            _pending.TryGetValue(id, out Action retry);
            _pending.Remove(id);
            if (!granted) return;
            _granted.Add(id);
            retry?.Invoke();
        }

        [Rpc(SendTo.Server)]
        private void DropRpc(PickupInfo info, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            NetPlayer player = NetPlayer.For(sender);
            if (player == null || !SpendDrop(sender)) return;
            // dropped where that player is, and only things that exist
            if (Vector3.Distance(player.Position, info.Position) > 4.0f) info.Position = player.Position + Vector3.up * 0.3f;
            if (info.Type == PickupInfo.Weapon && NetMatch.Weapon(info.Sub) == null) return;
            if (info.Type == PickupInfo.Material && ItemCatalog.Find(info.Name.ToString()) == null) return;
            if (info.Type != PickupInfo.Weapon && info.Type != PickupInfo.Material) return;
            info.Amount = (short)Mathf.Clamp(info.Amount, 0, 500);
            // only materials that player has can be dropped (no making things out of thin air)
            if (info.Type == PickupInfo.Material && !TakeMaterial(sender, info.Name.ToString(), info.Amount))
            {
                Debug.Log($"[Net] {sender} dropping {info.Amount} {info.Name} refused: the book says they don't have it");
                return;
            }

            info.Extra = (short)Mathf.Clamp(info.Extra, 0, 999);
            // made here like any host pickup: its Start announces it to everyone
            Create(info);
        }

        private bool SpendDrop(ulong sender)
        {
            float now = Time.unscaledTime;
            if (!_dropBudget.TryGetValue(sender, out var budget) || now - budget.windowStart > 1.0f) budget = (now, 0);
            budget.drops++;
            _dropBudget[sender] = budget;
            return budget.drops <= MaxDropsPerSecond;
        }

        // ================================================================ crafting

        private void AddMaterial(ulong client, string id, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(id)) return;
            if (!_materials.TryGetValue(client, out var book)) _materials[client] = book = new Dictionary<string, int>();
            book[id] = (book.TryGetValue(id, out int have) ? have : 0) + amount;
        }

        private bool TakeMaterial(ulong client, string id, int amount)
        {
            if (!_materials.TryGetValue(client, out var book) || !book.TryGetValue(id, out int have) || have < amount) return false;
            book[id] = have - amount;
            return true;
        }

        public int MaterialsOf(ulong client, string id) => _materials.TryGetValue(client, out var book) && book.TryGetValue(id, out int have) ? have : 0;

        // a client's craft: ask the host first (true = this machine waits for the answer)
        public static bool RedirectCraft(CraftingRecipe recipe, CraftingStationType? station)
        {
            if (!ClientSide || recipe == null || CraftingCatalog.Instance == null) return false;
            int index = System.Array.IndexOf(CraftingCatalog.Instance.Recipes, recipe);
            if (index < 0) return false;
            Instance.CraftRpc((ushort)index, station.HasValue ? (byte)station.Value : (byte)255);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void CraftRpc(ushort index, byte station, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            NetPlayer player = NetPlayer.For(sender);
            CraftingCatalog catalog = CraftingCatalog.Instance;
            string refusal = null;
            CraftingRecipe recipe = catalog != null && index < catalog.Recipes.Length ? catalog.Recipes[index] : null;
            if (player == null || !player.IsUp || recipe == null) refusal = "Receita inválida";
            else if (recipe.Station != CraftingStationType.Anywhere && !NearStation(player, recipe.Station)) refusal = $"Requer {Crafting.StationName(recipe.Station)}";
            else
            {
                foreach (CraftingRecipe.Ingredient ingredient in recipe.Ingredients)
                    if (MaterialsOf(sender, ingredient.ItemId) < ingredient.Count) refusal = "O host não registrou esses materiais";
            }

            if (refusal == null)
                foreach (CraftingRecipe.Ingredient ingredient in recipe.Ingredients) TakeMaterial(sender, ingredient.ItemId, ingredient.Count);
            Debug.Log($"[Net] {sender} crafting {(recipe != null ? recipe.name : "?")}: {(refusal ?? "allowed")}");
            CraftAnswerRpc(index, station, refusal ?? "", RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        private static bool NearStation(NetPlayer player, CraftingStationType type)
        {
            foreach (CraftingStation bench in CraftingStation.All)
                if (bench != null && bench.Type == type && Vector3.Distance(bench.InteractionPoint, player.Position) <= 4.0f) return true;
            return false;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void CraftAnswerRpc(ushort index, byte station, FixedString128Bytes refusal, RpcParams rpc)
        {
            CraftingCatalog catalog = CraftingCatalog.Instance;
            if (catalog == null || index >= catalog.Recipes.Length) return;
            string text = refusal.ToString();
            if (text.Length > 0)
            {
                PlayerCombat player = FindAnyObjectByType<PlayerCombat>();
                if (player != null) player.ShowNotice(text);
                return;
            }

            Crafting.RemoteApproved = true;
            Crafting.Craft(catalog.Recipes[index], station == 255 ? (CraftingStationType?)null : (CraftingStationType)station);
            Crafting.RemoteApproved = false;
        }

        // ================================================================ places

        public const byte ActDoor = 1;
        public const byte ActBarricadeBuild = 2;
        public const byte ActBarricadeReinforce = 3;
        public const byte ActBarricadeRemove = 4;
        public const byte ActCrate = 5;
        public const byte ActGenerator = 6;

        // a client's player used one of the shared places: ask the host (true = this machine does nothing)
        public static bool Redirect(Component place, byte action)
        {
            if (!ClientSide || place == null || !Instance._indexed) return false;
            int index = Instance.IndexOf(place, action);
            if (index < 0)
            {
                Debug.LogWarning($"[Net] {place.name} isn't one of the shared places");
                return false;
            }

            Debug.Log($"[Net] asking the host to use {place.name} (action {action}, #{index})");
            Instance.ActRpc(action, (ushort)index);
            return true;
        }

        private int IndexOf(Component place, byte action)
        {
            switch (action)
            {
                case ActDoor: return Array.IndexOf(_doors, place as InteractiveDoor);
                case ActBarricadeBuild:
                case ActBarricadeReinforce:
                case ActBarricadeRemove: return Array.IndexOf(_barricades, place as BarricadePoint);
                case ActCrate: return Array.IndexOf(_crates, place as LootCrate);
                case ActGenerator:
                    var generator = place as PowerGenerator;
                    return generator != null ? generator.Sector : -1;
            }

            return -1;
        }

        [Rpc(SendTo.Server)]
        private void ActRpc(byte action, ushort index, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            NetPlayer player = NetPlayer.For(sender);
            Debug.Log($"[Net] {sender} asks for action {action} on #{index} (up {player != null && player.IsUp}, at {(player != null ? player.Position : Vector3.zero)})");
            if (player == null || !player.IsUp) return;
            string notice = null;
            byte give = 0;

            switch (action)
            {
                case ActDoor:
                    if (index >= _doors.Length || _doors[index] == null || !Near(player, _doors[index].InteractionPoint)) return;
                    give = _doors[index].RemoteInteract(out notice) ? GiveBarricadeKit : (byte)0;
                    break;
                case ActBarricadeBuild:
                case ActBarricadeReinforce:
                case ActBarricadeRemove:
                    if (index >= _barricades.Length || _barricades[index] == null || !Near(player, _barricades[index].InteractionPoint)) return;
                    give = _barricades[index].RemoteInteract(action, out notice);
                    break;
                case ActCrate:
                    if (index >= _crates.Length || _crates[index] == null || !Near(player, _crates[index].Center)) return;
                    if (_crates[index].IsOpen) return;
                    // the chest's things appear for everyone; its credits go into that player's account
                    _crates[index].Open(null, false);
                    player.GrantCredits(LootCrate.Credits);
                    give = GiveCrateFeed;
                    break;
                case ActGenerator:
                    PowerGridManager grid = PowerGridManager.Instance;
                    if (grid == null || index >= grid.Sectors.Length || grid.Sectors[index].Generator == null) return;
                    PowerGenerator generator = grid.Sectors[index].Generator;
                    if (!Near(player, generator.InteractionPoint)) return;
                    generator.Interact(null);
                    break;
            }

            if (notice != null || give != 0) ActAnswerRpc(notice ?? "", give, RpcTarget.Single(sender, RpcTargetUse.Temp));
            SendStateIfChanged();
        }

        public const byte GiveBarricadeKit = 1;
        public const byte TakeBarricadeKit = 2;
        public const byte GiveCrateFeed = 3;

        private static bool Near(NetPlayer player, Vector3 point)
        {
            bool near = Vector3.Distance(player.Position, point) <= 4.0f;
            if (!near) Debug.Log($"[Net] refused: {player.DisplayName} is {Vector3.Distance(player.Position, point):F1} m from it");
            return near;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ActAnswerRpc(FixedString128Bytes notice, byte give, RpcParams rpc)
        {
            PlayerCombat player = FindAnyObjectByType<PlayerCombat>();
            InventoryManager inventory = InventoryManager.Player;
            if (give == GiveBarricadeKit && inventory != null) inventory.Add(QuickItem.Barricade, 1, false);
            if (give == TakeBarricadeKit && inventory != null) inventory.Take(QuickItem.Barricade);
            if (give == GiveCrateFeed)
            {
                CombatEffects fx = CombatEffects.Instance;
                if (fx != null && player != null) fx.PlaySound(fx.CrateOpenSound, player.transform.position, 0.9f);
                ItemCatalog catalog = ItemCatalog.Instance;
                PickupFeed.Report("credits", catalog != null ? catalog.CreditsIcon : null, "Créditos militares", LootCrate.Credits, PickupFeed.TintOf(ItemCategory.Currency));
            }

            string text = notice.ToString();
            if (text.Length > 0 && player != null) player.ShowNotice(text);
        }

        // ---------------------------------------------------------------- state (host -> clients)

        private void SendStateIfChanged()
        {
            if (!_indexed) return;
            byte[] doors = new byte[_doors.Length];
            for (int i = 0; i < _doors.Length; i++) doors[i] = _doors[i] != null ? _doors[i].PackState() : (byte)0;
            byte[] barricades = new byte[_barricades.Length];
            for (int i = 0; i < _barricades.Length; i++) barricades[i] = _barricades[i] != null ? _barricades[i].PackState() : (byte)0;
            byte[] crates = new byte[_crates.Length];
            for (int i = 0; i < _crates.Length; i++) crates[i] = (byte)(_crates[i] != null && _crates[i].IsOpen ? 1 : 0);
            PowerGridManager grid = PowerGridManager.Instance;
            byte[] sectors = new byte[grid != null && grid.Sectors != null ? grid.Sectors.Length : 0];
            for (int i = 0; i < sectors.Length; i++) sectors[i] = (byte)grid.Sectors[i].State;

            if (Same(doors, _sentDoors) && Same(barricades, _sentBarricades) && Same(crates, _sentCrates) && Same(sectors, _sentSectors)) return;
            _sentDoors = doors;
            _sentBarricades = barricades;
            _sentCrates = crates;
            _sentSectors = sectors;
            WorldStateRpc(doors, barricades, crates, sectors);
        }

        private static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        [Rpc(SendTo.NotServer)]
        private void WorldStateRpc(byte[] doors, byte[] barricades, byte[] crates, byte[] sectors)
        {
            if (!_indexed) Index();
            if (doors.Length != _doors.Length || barricades.Length != _barricades.Length || crates.Length != _crates.Length)
                Debug.LogWarning($"[Net] world differs from the host's: doors {_doors.Length}/{doors.Length}, barricades {_barricades.Length}/{barricades.Length}, chests {_crates.Length}/{crates.Length}");

            for (int i = 0; i < doors.Length && i < _doors.Length; i++) if (_doors[i] != null) _doors[i].MirrorState(doors[i]);
            for (int i = 0; i < barricades.Length && i < _barricades.Length; i++) if (_barricades[i] != null) _barricades[i].MirrorState(barricades[i]);
            for (int i = 0; i < crates.Length && i < _crates.Length; i++) if (_crates[i] != null) _crates[i].MirrorOpen(crates[i] != 0);
            PowerGridManager grid = PowerGridManager.Instance;
            if (grid != null && grid.Sectors != null)
                for (int i = 0; i < sectors.Length && i < grid.Sectors.Length; i++) grid.SetState(i, (PowerState)sectors[i]);
        }
    }
}
