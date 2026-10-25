using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Combat
{
    /* Two-instance testing without a person at the second machine. Does nothing unless the game is
     * started with these command-line arguments:
     *   -net-join 127.0.0.1:7777   join that host as soon as the menu is up, and get ready
     *   -net-name Teste            player name
     *   -net-log C:\path\log.txt   write what this client sees (players, infected, health, hits) once a second,
     *                              and take screenshots next to it
     *   -net-hit                   once a replica infected is near, hit it through the same entry point a
     *                              bullet uses (its Health, which hands the hit to the host)
     *   -net-revive                when a teammate is down, walk up to them and revive them through the same
     *                              calls the HUD makes while the interact key is held (the host checks it)
     *   -net-world2                build and take down a barricade, crank the generator of a sector the host
     *                              blacked out, craft a bandage from cloth taken through the host, then try a
     *                              craft with cloth made up locally (the host must refuse that one)
     *   -net-race HH:mm:ss         at that clock time, take the material nearest the base's centre (two clients
     *                              doing it at once: only one may get it)
     *   -net-world                 once the match runs, walk to a closed chest and open it, take what falls
     *                              out, use the nearest door and buy ammo in the break, the way the interact
     *                              key and the shop would (all of it is the host's to allow)
     */
    public class NetAutoTest : MonoBehaviour
    {
        private string _join;
        private string _name = "Teste";
        private string _log;
        private bool _hit;
        private bool _revive;
        private bool _world;
        private bool _world2;
        private int _world2Step;
        private float _nextWorld2;
        private BarricadePoint _barricade;
        private int _sector;
        private WorldItem _clothTarget;
        private int _clothTries;
        private DateTime _raceAt = DateTime.MaxValue;
        private bool _raceDone;
        private WorldItem _raceItem;
        private string _raceId;
        private int _raceBefore;
        private float _raceCheckAt;
        private bool _racePlaced;
        private int _worldStep;
        private float _nextWorldStep;
        private InteractiveDoor _doorTested;
        private bool _doorWasOpen;
        private NetPlayer _reviving;
        private float _reviveDone;
        private float _reviveBegin;
        private float _nextLine;
        private float _nextHit;
        private int _shots;
        private bool _joined;
        private bool _readied;
        private int _screens;
        private float _startHealth = -1.0f;

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-net-join": _join = next; break;
                    case "-net-name": _name = next; break;
                    case "-net-log": _log = next; break;
                    case "-net-hit": _hit = true; break;
                    case "-net-revive": _revive = true; break;
                    case "-net-world": _world = true; break;
                    case "-net-world2": _world2 = true; break;
                    case "-net-race":
                        if (next != null && DateTime.TryParse(DateTime.Now.ToString("yyyy-MM-dd") + " " + next, out DateTime at)) _raceAt = at;
                        break;
                }
            }

            if (_join == null) enabled = false;
            else Write($"teste iniciado, versão {Application.version}, protocolo {NetSession.Protocol}");
        }

        private void Update()
        {
            if (!_joined && GameFlow.Instance != null && NetSession.Instance != null && Time.unscaledTime > 2.0f)
            {
                _joined = true;
                string[] parts = _join.Split(':');
                ushort port = parts.Length > 1 && ushort.TryParse(parts[1], out ushort p) ? p : NetSession.DefaultPort;
                bool ok = NetSession.Instance.Join(parts[0], port, _name, "");
                Write($"conectando a {parts[0]}:{port} -> {ok}");
            }

            if (!_readied && NetPlayer.Local != null && NetPlayer.Local.IsSpawned)
            {
                _readied = true;
                NetPlayer.Local.Ready.Value = true;
                Write("no lobby, pronto");
                Screenshot("lobby");
            }

            if (Time.unscaledTime >= _nextLine)
            {
                _nextLine = Time.unscaledTime + 1.0f;
                Report();
            }

            if (_hit && Net.Active) HitNearest();
            if (_revive && Net.Active) ReviveDowned();
            if (_world && Net.Active) WorldStep();
            if (_world2 && Net.Active) World2Step();
            if (Net.Active) RaceStep();
        }

        private void Report()
        {
            if (NetSession.Instance == null) return;
            PlayerCombat me = FindAnyObjectByType<PlayerCombat>();
            var line = new StringBuilder($"estado {NetSession.Instance.Current} '{NetSession.Instance.Message}'");
            if (NetGame.Instance != null) line.Append($" | partida {NetGame.Instance.Started} jogadores {NetPlayer.All.Count}");
            if (me != null && Net.Active)
            {
                if (_startHealth < 0.0f) _startHealth = me.Health.Current;
                line.Append($" | eu {me.transform.position} vida {me.Health.Current:F0}");
            }

            if (Net.Active)
            {
                line.Append(" | estados");
                foreach (NetPlayer p in NetPlayer.All)
                    line.Append($" {(p.IsOwner ? "eu" : p.DisplayName)}={p.Condition.Value}{(p.Condition.Value == CoopCondition.Downed ? $"({p.BleedOutLeft:F0}s, revive {p.ReviveProgress:P0})" : "")} quedas {p.Downs.Value} ping {p.Ping.Value}");
                if (me != null) line.Append($" | caído {me.IsDowned} morto {me.Health.IsDead}");
            }

            foreach (NetPlayer player in NetPlayer.All)
            {
                if (player.IsOwner || player.Puppet == null) continue;
                line.Append($" | boneco {player.DisplayName} em {player.Puppet.transform.position} vida {player.Puppet.Health.Current:F0} arma {player.State.Value.Weapon}");
            }

            if (Net.Active)
            {
                int open = 0;
                foreach (LootCrate crate in LootCrate.All) if (crate.IsOpen) open++;
                int doors = 0;
                foreach (InteractiveDoor door in InteractiveDoor.All) if (door.IsOpen) doors++;
                line.Append($" | baús abertos {open}/{LootCrate.All.Count} portas abertas {doors}/{InteractiveDoor.All.Count}");
                line.Append($" | no chão: armas {FindObjectsByType<WeaponPickup>().Length} munição/kits {FindObjectsByType<ConsumablePickup>().Length} itens {FindObjectsByType<ItemPickup>().Length} materiais {FindObjectsByType<WorldItem>().Length}");
                int soldiers = 0, bosses = 0;
                foreach (EnemyReplica r in FindObjectsByType<EnemyReplica>())
                {
                    if (r.Kind == EnemyKind.Soldier) soldiers++;
                    if (r.Kind == EnemyKind.Boss) bosses++;
                }

                line.Append($" | réplicas soldado {soldiers} chefe {bosses} (tiros vistos {EnemyReplica.ShotsSeen}, gritos {EnemyReplica.ScreamsSeen}, barra de chefe {(EnemyReplica.ActiveBoss != null ? EnemyReplica.ActiveBoss.Boss.DisplayName : "-")})");
            }

            EnemyReplica[] replicas = FindObjectsByType<EnemyReplica>();
            int dead = 0;
            foreach (EnemyReplica r in replicas) if (r.GetComponent<Health>().IsDead) dead++;
            line.Append($" | réplicas {replicas.Length} (mortas {dead}) zumbis locais com IA {FindObjectsByType<ZombieAI>().Length} tiros {_shots}");
            if (GameFlow.Instance != null) line.Append($" | abates {RunState.Kills} pontos {GameFlow.Instance.Score} créditos {RunState.Credits}");
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner != null && Net.Active)
                line.Append($" | onda {spawner.Wave} ativa {spawner.WaveInProgress} intervalo {spawner.InIntermission} ({spawner.IntermissionLeft:F0} s) restantes {spawner.RemainingInWave}");
            Write(line.ToString());

            if (Net.Active && _screens < 3 && replicas.Length > 0)
            {
                _screens++;
                Screenshot("partida" + _screens);
            }
        }

        private void HitNearest()
        {
            if (Time.unscaledTime < _nextHit) return;
            PlayerCombat me = FindAnyObjectByType<PlayerCombat>();
            if (me == null) return;
            EnemyReplica target = null;
            float best = 40.0f;
            foreach (EnemyReplica replica in FindObjectsByType<EnemyReplica>())
            {
                Health health = replica.GetComponent<Health>();
                if (health.IsDead) continue;
                float d = Vector3.Distance(replica.transform.position, me.transform.position);
                if (d < best)
                {
                    best = d;
                    target = replica;
                }
            }

            if (target == null) return;
            _nextHit = Time.unscaledTime + 0.4f;
            // what an M1911 body shot does, through the same door a bullet comes in by
            Vector3 chest = target.transform.position + Vector3.up * 1.3f;
            Vector3 from = me.transform.position + Vector3.up * 1.4f;
            target.GetComponent<Health>().TakeDamage(new DamageInfo
            {
                Amount = 28.0f,
                Point = chest,
                Direction = (chest - from).normalized,
                Force = 3.0f,
                Instigator = me.gameObject
            });
            if (NetPlayer.Local != null) NetPlayer.Local.ReportShot(from, chest);
            _shots++;
            Write($"tiro {_shots} na réplica {target.Id} a {best:F1} m");
        }

        private void ReviveDowned()
        {
            NetPlayer me = NetPlayer.Local;
            PlayerCombat body = FindAnyObjectByType<PlayerCombat>();
            if (me == null || body == null || !me.IsUp) return;

            if (_reviving != null)
            {
                if (_reviveBegin > 0.0f && Time.unscaledTime >= _reviveBegin)
                {
                    me.BeginRevive(_reviving);
                    _reviveBegin = 0.0f;
                    _reviveDone = Time.unscaledTime + NetPlayer.ReviveTime + 0.3f;
                }

                if (_reviving.Condition.Value != CoopCondition.Downed)
                {
                    Write($"reanimação de {_reviving.DisplayName}: agora {_reviving.Condition.Value}");
                    _reviving = null;
                }
                else if (Time.unscaledTime >= _reviveDone)
                {
                    me.FinishRevive(_reviving);
                    Write($"reanimação de {_reviving.DisplayName} terminada (pedido ao host)");
                    _reviveDone = float.MaxValue;
                }

                return;
            }

            foreach (NetPlayer mate in NetPlayer.All)
            {
                if (mate.IsOwner || mate.Condition.Value != CoopCondition.Downed || mate.Puppet == null) continue;
                // walk up to them (a teleport: nobody is at the keyboard)
                var controller = body.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                body.transform.position = mate.Puppet.InteractionPoint + Vector3.right * 1.0f;
                if (controller != null) controller.enabled = true;
                // the host checks the distance with the position it has, which arrives a moment later
                _reviving = mate;
                _reviveBegin = Time.unscaledTime + 0.5f;
                _reviveDone = float.MaxValue;
                Write($"reanimando {mate.DisplayName} (sangrando há {NetPlayer.BleedOutTimes[0] - mate.BleedOutLeft:F0} s)");
                Screenshot("reanimando");
                return;
            }
        }

        private void WorldStep()
        {
            if (Time.unscaledTime < _nextWorldStep) return;
            _nextWorldStep = Time.unscaledTime + 3.0f;
            PlayerCombat me = FindAnyObjectByType<PlayerCombat>();
            if (me == null || me.Health.IsDead) return;

            switch (_worldStep)
            {
                case 0:
                    LootCrate crate = null;
                    float best = float.MaxValue;
                    foreach (LootCrate c in LootCrate.All)
                    {
                        float d = Vector3.Distance(c.transform.position, me.transform.position);
                        if (!c.IsOpen && d < best)
                        {
                            best = d;
                            crate = c;
                        }
                    }

                    if (crate == null) return;
                    Teleport(me, crate.transform.position - crate.transform.forward * 1.3f);
                    _worldStep++;
                    _nextWorldStep = Time.unscaledTime + 1.0f;
                    Write($"indo ao baú em {crate.transform.position} ({best:F0} m)");
                    return;
                case 1:
                    foreach (LootCrate c in LootCrate.All)
                    {
                        if (c.IsOpen || Vector3.Distance(c.Center, me.transform.position) > 3.0f) continue;
                        Write($"abrindo baú (créditos antes {RunState.Credits})");
                        c.Open(me);
                        break;
                    }

                    _worldStep++;
                    return;
                case 2:
                case 3:
                case 4:
                    // what fell out: step on the boxes, use the rest
                    Vector3 here = me.transform.position;
                    foreach (WorldItem item in FindObjectsByType<WorldItem>())
                    {
                        if (Vector3.Distance(item.transform.position, here) > 4.0f) continue;
                        Write($"pegando material {item.ItemId}");
                        item.Interact(me);
                        break;
                    }

                    foreach (ConsumablePickup box in FindObjectsByType<ConsumablePickup>())
                    {
                        if (Vector3.Distance(box.transform.position, here) > 4.0f) continue;
                        Write($"pegando {box.Type}");
                        Teleport(me, box.transform.position);
                        break;
                    }

                    foreach (WeaponPickup gun in FindObjectsByType<WeaponPickup>())
                    {
                        if (Vector3.Distance(gun.transform.position, here) > 4.0f) continue;
                        Write($"pegando arma {gun.Data.DisplayName}");
                        Teleport(me, gun.transform.position);
                        me.GetComponent<StarterAssets.StarterAssetsInputs>().interact = true;
                        break;
                    }

                    _worldStep++;
                    return;
                case 5:
                    InteractiveDoor door = null;
                    float nearest = float.MaxValue;
                    foreach (InteractiveDoor d in InteractiveDoor.All)
                    {
                        float dist = Vector3.Distance(d.InteractionPoint, me.transform.position);
                        if (!d.IsBroken && !d.Locked && d.HasPower && dist < nearest)
                        {
                            nearest = dist;
                            door = d;
                        }
                    }

                    if (door != null)
                    {
                        Teleport(me, door.InteractionPoint - door.transform.forward * 1.2f);
                        _doorTested = door;
                        _doorWasOpen = door.IsOpen;
                    }

                    _worldStep++;
                    _nextWorldStep = Time.unscaledTime + 1.0f;
                    return;
                case 6:
                    if (_doorTested != null)
                    {
                        Write($"porta usada (estava {(_doorWasOpen ? "aberta" : "fechada")})");
                        _doorTested.Interact(me);
                    }

                    _worldStep++;
                    return;
                case 7:
                    if (_doorTested != null) Write($"porta agora {(_doorTested.IsOpen ? "aberta" : "fechada")} (mudou: {_doorTested.IsOpen != _doorWasOpen})");
                    _worldStep++;
                    return;
                case 8:
                    ShopManager shop = ShopManager.Instance;
                    EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
                    if (shop == null || spawner == null || !spawner.InIntermission) return;
                    if (!ShopManager.IsOpen) shop.Open();
                    foreach (ShopCatalog.Entry entry in shop.Catalog.Entries)
                    {
                        if (entry.Kind != ShopItemKind.Ammo || shop.Unavailable(entry) != null) continue;
                        Write($"comprando {entry.Id} por {shop.PriceOf(entry)} (créditos {RunState.Credits})");
                        shop.TryBuy(entry);
                        break;
                    }

                    _worldStep++;
                    return;
                case 9:
                    Write($"depois da compra: créditos {RunState.Credits}");
                    if (ShopManager.IsOpen && ShopManager.Instance != null) ShopManager.Instance.Close();
                    _worldStep++;
                    return;
            }
        }

        // ---------------------------------------------------------------- -net-world2: barricade, generator, crafting

        private void World2Step()
        {
            if (Time.unscaledTime < _nextWorld2) return;
            _nextWorld2 = Time.unscaledTime + 3.0f;
            PlayerCombat me = FindAnyObjectByType<PlayerCombat>();
            InventoryManager inventory = InventoryManager.Player;
            if (me == null || me.Health.IsDead || inventory == null) return;

            switch (_world2Step)
            {
                case 0:
                    _barricade = null;
                    float best = float.MaxValue;
                    foreach (BarricadePoint point in FindObjectsByType<BarricadePoint>())
                    {
                        float d = Vector3.Distance(point.transform.position, me.transform.position);
                        if (!point.Built && d < best)
                        {
                            best = d;
                            _barricade = point;
                        }
                    }

                    if (_barricade == null)
                    {
                        Write("barricada: nenhum ponto livre");
                        _world2Step = 4;
                        return;
                    }

                    if (inventory.Count(QuickItem.Barricade) == 0) inventory.Add(QuickItem.Barricade, 1, false);
                    Teleport(me, _barricade.InteractionPoint - _barricade.transform.forward * 1.2f);
                    _world2Step++;
                    _nextWorld2 = Time.unscaledTime + 1.0f;
                    return;
                case 1:
                    Write($"barricada: montando (kits {inventory.Count(QuickItem.Barricade)}, montada {_barricade.Built})");
                    _barricade.Interact(me);
                    _world2Step++;
                    return;
                case 2:
                    Write($"barricada: agora montada {_barricade.Built}, vida {_barricade.Health:F0}, kits {inventory.Count(QuickItem.Barricade)}; desmontando");
                    _barricade.Interact(me);
                    _world2Step++;
                    return;
                case 3:
                    Write($"barricada: depois de desmontar montada {_barricade.Built}, kits {inventory.Count(QuickItem.Barricade)}");
                    _world2Step++;
                    return;
                case 4:
                    // the host puts a sector in the dark for this test; go and crank its generator
                    PowerGridManager grid = PowerGridManager.Instance;
                    if (grid == null) return;
                    for (int i = 0; i < grid.Sectors.Length; i++)
                    {
                        if (grid.Sectors[i].State == PowerState.Normal || grid.Sectors[i].Generator == null) continue;
                        _sector = i;
                        PowerGenerator generator = grid.Sectors[i].Generator;
                        Write($"energia: setor {grid.Sectors[i].Name} em {grid.Sectors[i].State} aqui; indo ao gerador");
                        Teleport(me, generator.InteractionPoint - generator.transform.forward * 1.2f);
                        _world2Step++;
                        _nextWorld2 = Time.unscaledTime + 1.0f;
                        return;
                    }

                    return;
                case 5:
                    PowerGenerator crank = PowerGridManager.Instance.Sectors[_sector].Generator;
                    Write("energia: ligando o gerador");
                    crank.Interact(me);
                    _world2Step++;
                    _nextWorld2 = Time.unscaledTime + 7.0f;
                    return;
                case 6:
                    Write($"energia: setor agora {PowerGridManager.Instance.Sectors[_sector].State}");
                    _world2Step++;
                    return;
                case 7:
                case 8:
                case 9:
                    // two pieces of cloth from the ground, through the host: walk up first (the host checks the
                    // distance with the position it has), ask on the next step
                    if (Backpack.Count("cloth") >= 2 || _clothTries >= 6)
                    {
                        _world2Step = 10;
                        return;
                    }

                    if (_clothTarget != null)
                    {
                        Write("fabricação: pegando tecido");
                        _clothTarget.Interact(me);
                        _clothTarget = null;
                        _clothTries++;
                        _nextWorld2 = Time.unscaledTime + 1.5f;
                        return;
                    }

                    WorldItem cloth = null;
                    float nearest = float.MaxValue;
                    foreach (WorldItem item in FindObjectsByType<WorldItem>())
                    {
                        float d = Vector3.Distance(item.transform.position, me.transform.position);
                        if (item.ItemId == "cloth" && d < nearest)
                        {
                            nearest = d;
                            cloth = item;
                        }
                    }

                    if (cloth != null)
                    {
                        Teleport(me, cloth.transform.position + Vector3.right * 0.8f);
                        Write($"fabricação: indo ao tecido ({nearest:F0} m)");
                        _clothTarget = cloth;
                        _nextWorld2 = Time.unscaledTime + 1.0f;
                    }
                    else _world2Step = 10;

                    return;
                case 10:
                    CraftingRecipe bandage = Recipe("bandage");
                    Write($"fabricação: tecido {Backpack.Count("cloth")}, bandagens {Count("bandage")}; fabricando");
                    if (bandage != null) Crafting.Craft(bandage, null);
                    _world2Step++;
                    return;
                case 11:
                    Write($"fabricação: depois tecido {Backpack.Count("cloth")}, bandagens {Count("bandage")}");
                    // now cheat: two pieces of cloth that never came from the host
                    Backpack.Add("cloth", 2);
                    CraftingRecipe again = Recipe("bandage");
                    Write($"fabricação: tecido inventado aqui {Backpack.Count("cloth")}; fabricando de novo");
                    if (again != null) Crafting.Craft(again, null);
                    _world2Step++;
                    return;
                case 12:
                    Write($"fabricação: depois da trapaça tecido {Backpack.Count("cloth")}, bandagens {Count("bandage")}");
                    _world2Step++;
                    return;
            }
        }

        private static int Count(string id)
        {
            if (Backpack.TryQuickItem(id, out QuickItem quick) && InventoryManager.Player != null) return InventoryManager.Player.Count(quick);
            return Backpack.Count(id);
        }

        private static CraftingRecipe Recipe(string product)
        {
            if (CraftingCatalog.Instance == null) return null;
            foreach (CraftingRecipe recipe in CraftingCatalog.Instance.Recipes)
                if (recipe != null && recipe.ProductId == product) return recipe;
            return null;
        }

        // ---------------------------------------------------------------- -net-race: the same thing at the same moment

        private void RaceStep()
        {
            // in place a few seconds early, so the host already has this player next to it
            if (_raceDone || DateTime.Now < _raceAt.AddSeconds(-4)) return;
            PlayerCombat me = FindAnyObjectByType<PlayerCombat>();
            if (me == null) return;
            if (!_racePlaced)
            {
                // everyone picks the same one: the material nearest the base's centre
                float best = float.MaxValue;
                foreach (WorldItem item in FindObjectsByType<WorldItem>())
                {
                    float d = item.transform.position.sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        _raceItem = item;
                    }
                }

                if (_raceItem == null) return;
                _raceId = _raceItem.ItemId;
                _raceBefore = Count(_raceId);
                Teleport(me, _raceItem.transform.position + Vector3.right * 0.8f);
                Write($"corrida: no lugar, ao lado de {_raceId} em {_raceItem.transform.position}");
                _racePlaced = true;
                return;
            }

            if (_raceCheckAt <= 0.0f)
            {
                if (DateTime.Now < _raceAt) return;
                if (_raceItem != null) _raceItem.Interact(me);
                _raceCheckAt = Time.unscaledTime + 2.5f;
                Write($"corrida: pedindo às {DateTime.Now:HH:mm:ss.fff}");
                return;
            }

            if (Time.unscaledTime < _raceCheckAt) return;
            int after = Count(_raceId);
            Write(after > _raceBefore ? $"corrida: GANHEI ({_raceId} {_raceBefore} -> {after})" : $"corrida: perdi ({_raceId} continua {after})");
            _raceDone = true;
        }

        private static void Teleport(PlayerCombat body, Vector3 position)
        {
            var controller = body.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            body.transform.position = position;
            if (controller != null) controller.enabled = true;
        }

        private void Screenshot(string label)
        {
            if (string.IsNullOrEmpty(_log)) return;
            string dir = Path.GetDirectoryName(_log) ?? ".";
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"cliente_{label}.png"));
        }

        private void Write(string text)
        {
            string line = $"{DateTime.Now:HH:mm:ss} {text}";
            Debug.Log("[NetTest] " + line);
            if (string.IsNullOrEmpty(_log)) return;
            try
            {
                File.AppendAllText(_log, line + "\n");
            }
            catch (IOException)
            {
                // a busy log file is not worth stopping the test for
            }
        }
    }
}
