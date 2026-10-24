using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Combat.EditorTools
{
    /* Missions, survival and campaign pass: builds the mission sites on the base (generator rooms,
     * radio station, armory, gates, laboratory, watchtower, alarm panels, supply depot, helipad),
     * creates the data (missions, campaign, bosses, weapon upgrades, shop) and wires the systems.
     * The sites are simple placeholder structures with anchors, so real art can replace them.
     * Safe to run again.
     */
    public static partial class CombatSetup
    {
        private const string CampaignSounds = "Assets/SourceFiles/SoundFX/Campaign/";
        private const string CampaignRoot = Root + "/Campaign";

        [MenuItem("Tools/Combat/Setup Campaign and Missions")]
        public static void SetupCampaign()
        {
            EnsureFolder(CampaignRoot);
            EnsureFolder(CampaignRoot + "/Missions");
            ImportSoundsIn(CampaignSounds);
            WeaponData[] weapons = CreateWeapons();

            SetupCampaignPlayer();
            Physics.SyncTransforms();
            var sites = BuildMissionSites(weapons);
            BossDefinition e07 = CreateBoss("E07");
            BossDefinition aurora = CreateBoss("Aurora");
            Dictionary<string, MissionDefinition> missions = CreateMissions();
            CampaignDefinition campaign = CreateCampaignAsset(missions, e07, aurora);
            ShopCatalog catalog = CreateShop(weapons);
            WireCampaignScene(weapons, missions, campaign, catalog, e07, aurora, sites);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Campaign set up");
        }

        private static AudioClip CampaignClip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(CampaignSounds + name + ".wav");
        }

        private static void ImportSoundsIn(string folder)
        {
            AssetDatabase.Refresh();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;
                bool loop = path.Contains("_Loop");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                // the boss music keeps its stereo
                importer.forceToMono = !path.Contains("Music");
                importer.SaveAndReimport();
            }
        }

        private static void SetupCampaignPlayer()
        {
            EditPrefab("Assets/Prefabs/PlayerRobot.prefab", root =>
            {
                GameObject player = root.GetComponentInChildren<StarterAssets.ThirdPersonController>(true).gameObject;
                Ensure<InventoryManager>(player);
            });
        }

        // ---------------------------------------------------------------- sites

        private class Sites
        {
            public Transform Helipad;
        }

        private static Sites BuildMissionSites(WeaponData[] weapons)
        {
            var sites = new Sites();
            GameObject root = FindOrCreate("MissionSites");
            ClearChildren(root.transform);
            Material concrete = HorrorMaterial("Shed_Concrete", new Color(0.32f, 0.31f, 0.29f), 0.0f, 0.15f, Color.black);
            Material doorMetal = HorrorMaterial("Shed_Door", new Color(0.17f, 0.2f, 0.17f), 0.6f, 0.35f, Color.black);
            Material warning = HorrorMaterial("Shed_Warning", new Color(0.75f, 0.55f, 0.05f), 0.2f, 0.3f, new Color(0.45f, 0.3f, 0.03f));
            Material metal = HorrorMaterial("Generator_Metal", new Color(0.08f, 0.08f, 0.09f), 0.8f, 0.4f, Color.black);
            Material panel = HorrorMaterial("Mission_Panel", new Color(0.2f, 0.22f, 0.25f), 0.5f, 0.4f, Color.black);
            Material lamp = HorrorMaterial("Generator_Status", new Color(0.2f, 1.0f, 0.3f), 0.0f, 0.6f, new Color(0.6f, 3.0f, 0.9f));
            Material paint = HorrorMaterial("Helipad_Paint", new Color(0.85f, 0.85f, 0.8f), 0.0f, 0.2f, new Color(0.08f, 0.08f, 0.08f));
            Material labWall = HorrorMaterial("Lab_Wall", new Color(0.55f, 0.57f, 0.58f), 0.1f, 0.3f, Color.black);

            var used = new List<Vector3>();
            foreach (Transform loot in FindOrCreate("Loot").transform) used.Add(loot.position);
            foreach (Transform t in FindOrCreate("HorrorSystems").GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("GeneratorShed") || t.name.StartsWith("Armory")) used.Add(t.position);
            }

            var player = Object.FindAnyObjectByType<PlayerCombat>();
            Vector3 spawn = player != null ? player.transform.position : MilitaryBase.Center;
            used.Add(spawn);

            // the sectors of the power grid, for spreading the sites around
            var grid = Object.FindAnyObjectByType<PowerGridManager>();
            PowerSector[] sectors = grid != null ? grid.Sectors : new PowerSector[0];

            // --- first contact: an area just off the start, with the guard's diary
            if (OpenSpot(spawn, 18.0f, 28.0f, 2.0f, used, out Vector3 firstArea))
            {
                Anchor(root.transform, "first_area", "Área próxima", firstArea);
                Pickup(root.transform, "shift_log", "diário de turno", MissionPickup.Shape.Document, firstArea + Vector3.right, "doc_shift_log", false);
            }

            // --- main generator: the fuse box in the north-west generator room, two fuses around
            Transform shed1 = GameObject.Find("GeneratorShed_1")?.transform;
            if (shed1 != null)
            {
                Anchor(root.transform, "gen_room", "Sala do gerador", shed1.position);
                MissionDevice fuseBox = Device(shed1, "fuse_box", "caixa de fusíveis", "Instalar fusíveis na", shed1.TransformPoint(new Vector3(1.5f, 0.15f, -1.85f)), shed1.rotation, panel, lamp);
                fuseBox.RequiredItem = "fuse";
                fuseBox.RequiredCount = 2;
                fuseBox.WorkTime = 3.0f;
                var generator = shed1.GetComponentInChildren<PowerGenerator>();
                if (generator != null)
                {
                    generator.WaitsForDevice = "fuse_box";
                    EditorUtility.SetDirty(generator);
                }

                Pickup(root.transform, "memo", "memorando", MissionPickup.Shape.Document, shed1.position + shed1.forward * 3.2f + shed1.right * 1.5f, "doc_generator_memo", false);
                for (int i = 0; i < 2; i++)
                {
                    if (OpenSpot(shed1.position, 16.0f, 34.0f, 1.5f, used, out Vector3 fuse)) Pickup(root.transform, "fuse", "fusível", MissionPickup.Shape.Box, fuse, null, true);
                }
            }

            // --- radio station in the north-east with an antenna mast; the module lies on a container
            PowerSector northEast = sectors.Length > 1 ? sectors[1] : null;
            if (northEast != null && FindShedSpot(northEast, used, out Vector3 radioSpot, out Quaternion radioFacing))
            {
                Transform station = BuildShed(root.transform, "RadioStation", radioSpot, radioFacing, concrete, doorMetal, warning, false);
                Anchor(station, "radio_station", "Estação de rádio", station.position);
                MissionDevice console = Device(station, "radio_console", "console de rádio", "Instalar módulo no", station.TransformPoint(new Vector3(0.6f, 0.15f, -1.7f)), station.rotation, panel, lamp);
                console.RequiredItem = "radio_module";
                console.WorkTime = 4.0f;
                MissionDevice evac = Device(station, "evac_radio", "rádio de longo alcance", "Transmitir o código pelo", station.TransformPoint(new Vector3(-0.9f, 0.15f, -1.7f)), station.rotation, panel, lamp);
                evac.RequiredItem = "evac_codes";
                evac.WorkTime = 8.0f;
                evac.Noise = 20.0f;

                // the mast beside the hut
                Vector3 mastBase = station.TransformPoint(new Vector3(3.4f, 0.0f, 0.0f));
                var mast = new GameObject("AntennaMast").transform;
                mast.SetParent(station, true);
                mast.position = mastBase;
                Block(mast, "AntennaMast", new Vector3(0.0f, 4.5f, 0.0f), new Vector3(0.25f, 9.0f, 0.25f), metal);
                Block(mast, "AntennaDish", new Vector3(0.0f, 8.2f, 0.4f), new Vector3(1.2f, 1.2f, 0.15f), metal, false);
                var beacon = new GameObject("MastLight").AddComponent<Light>();
                beacon.transform.SetParent(mast, false);
                beacon.transform.localPosition = new Vector3(0.0f, 9.2f, 0.0f);
                beacon.type = LightType.Point;
                beacon.color = new Color(1.0f, 0.1f, 0.05f);
                beacon.range = 6.0f;
                beacon.intensity = 2.0f;
                MissionDevice antenna = Device(mast, "antenna", "antena", "Ativar a", mastBase + station.forward * 0.8f, station.rotation, panel, lamp);
                antenna.WorkTime = 30.0f;
                antenna.WorkRange = 8.0f;
                antenna.Noise = 30.0f;

                Vector3 module = ContainerTop(station.position, 25.0f, 70.0f, used, out bool onTop) ?? station.position + station.forward * 20.0f;
                if (!onTop && OpenSpot(station.position, 25.0f, 45.0f, 1.5f, used, out Vector3 ground)) module = ground;
                Pickup(root.transform, "radio_module", "módulo de rádio", MissionPickup.Shape.Module, module, null, true);
            }

            // --- armory: the south-west armory gets a card reader; its key card waits near a tower
            Transform armory = GameObject.Find("Armory_3")?.transform;
            if (armory != null)
            {
                var door = armory.GetComponentInChildren<InteractiveDoor>();
                if (door != null)
                {
                    door.Locked = true;
                    door.DisplayName = "porta do arsenal";
                    EditorUtility.SetDirty(door);
                    Anchor(root.transform, "armory", "Arsenal", door.transform.position + armory.forward * 0.5f);
                    MissionDevice reader = Device(armory, "armory_reader", "leitor de cartão", "Passar o cartão no", armory.TransformPoint(new Vector3(1.3f, 0.0f, 2.45f)), armory.rotation, panel, lamp);
                    reader.RequiredItem = "keycard";
                    reader.OnlyWhileNeeded = true;
                    reader.OnActivated = new[] { Effect(MissionEffectType.UnlockDoor, "armory") };
                }

                Pickup(armory, "armory_case", "caixa de equipamento", MissionPickup.Shape.Module, armory.TransformPoint(new Vector3(-1.2f, 0.15f, -1.2f)), null, true);
                Transform tower = NearestNamed("Tower", armory.position, 15.0f);
                Vector3 cardNear = tower != null ? tower.position : armory.position;
                if (OpenSpot(cardNear, 3.0f, 12.0f, 1.5f, used, out Vector3 card))
                {
                    Pickup(root.transform, "keycard", "cartão de acesso", MissionPickup.Shape.Card, card, null, true);
                    Pickup(root.transform, "note_armory", "bilhete do intendente", MissionPickup.Shape.Document, card + Vector3.forward * 0.8f, "doc_armory_code", false);
                }
            }

            // --- gates on three edges of the base: a lever, bars that close, and a passage to barricade
            Vector3 c = MilitaryBase.Center;
            Vector3 h = MilitaryBase.NavMeshSize * 0.4f;
            Vector3[] gatePoints = { c + new Vector3(0.0f, 0.0f, h.z), c + new Vector3(-h.x, 0.0f, 0.0f), c + new Vector3(h.x, 0.0f, -h.z * 0.5f) };
            for (int i = 0; i < gatePoints.Length; i++)
            {
                if (!OpenSpot(gatePoints[i], 0.0f, 18.0f, 3.5f, used, out Vector3 gate)) continue;
                Vector3 outward = gate - c;
                outward.y = 0.0f;
                Quaternion facing = Quaternion.LookRotation(outward.normalized);
                MissionAnchor anchor = Anchor(root.transform, $"gate_{i + 1}", $"Portão {i + 1}", gate);
                anchor.transform.rotation = facing;
                Block(anchor.transform, "GatePostL", new Vector3(-2.2f, 1.5f, 0.0f), new Vector3(0.35f, 3.0f, 0.35f), metal);
                Block(anchor.transform, "GatePostR", new Vector3(2.2f, 1.5f, 0.0f), new Vector3(0.35f, 3.0f, 0.35f), metal);
                Block(anchor.transform, "GateSign", new Vector3(0.0f, 3.1f, 0.0f), new Vector3(4.8f, 0.25f, 0.1f), warning, false);
                // the closed bars, shown when the lever is pulled
                var bars = new GameObject("Reveal").transform;
                bars.SetParent(anchor.transform, false);
                for (int b = 0; b < 9; b++) Block(bars, "GateBar", new Vector3(-2.0f + b * 0.5f, 1.4f, 0.0f), new Vector3(0.08f, 2.8f, 0.08f), metal);
                Block(bars, "GateRail", new Vector3(0.0f, 2.6f, 0.0f), new Vector3(4.2f, 0.12f, 0.12f), metal);
                bars.gameObject.SetActive(false);

                MissionDevice lever = Device(anchor.transform, $"gate_{i + 1}", "portão", "Fechar o", gate + facing * new Vector3(3.2f, 0.0f, -1.0f), facing, panel, lamp);
                lever.WorkTime = 4.0f;
                lever.Noise = 15.0f;
                lever.OnActivated = new[] { Effect(MissionEffectType.Reveal, $"gate_{i + 1}"), Effect(MissionEffectType.BlockSpawns, $"gate_{i + 1}", 300.0f) };

                // a vulnerable passage a little inside, to barricade
                if (OpenSpot(gate - outward.normalized * 8.0f, 0.0f, 8.0f, 2.0f, used, out Vector3 passage))
                {
                    var point = new GameObject($"Passage_{i + 1}").AddComponent<BarricadePoint>();
                    point.transform.SetParent(root.transform, false);
                    point.transform.SetPositionAndRotation(passage, facing);
                    point.Id = $"gatebar_{i + 1}";
                    point.PlankMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Effects/Loot_Crate.mat");
                }
            }

            // --- laboratory: a bigger dark building in the south-east with terminals, documents and the black box
            PowerSector southEast = sectors.Length > 3 ? sectors[3] : null;
            if (southEast != null && FindShedSpot(southEast, used, out Vector3 labSpot, out Quaternion labFacing))
            {
                Transform lab = BuildRoom(root.transform, "Laboratorio", labSpot, labFacing, 9.0f, 7.0f, 3.2f, labWall, doorMetal, warning);
                Anchor(lab, "lab", "Laboratório", lab.position);
                MissionDevice terminal = Device(lab, "lab_terminal", "terminal de pesquisa", "Baixar os dados do", lab.TransformPoint(new Vector3(-3.0f, 0.15f, -3.1f)), lab.rotation, panel, lamp);
                terminal.WorkTime = 10.0f;
                terminal.Noise = 12.0f;
                Device(lab, "security_console", "console de segurança", "Ativar o", lab.TransformPoint(new Vector3(3.0f, 0.15f, -3.1f)), lab.rotation, panel, lamp).WorkTime = 4.0f;
                Pickup(lab, "lab_doc", "registro científico", MissionPickup.Shape.Document, lab.TransformPoint(new Vector3(-3.4f, 1.0f, 1.5f)), "doc_lab_notes", true);
                Pickup(lab, "lab_doc", "relatório de contenção", MissionPickup.Shape.Document, lab.TransformPoint(new Vector3(3.4f, 1.0f, 1.0f)), "doc_e07_report", true);
                Pickup(lab, "blackbox", "caixa-preta", MissionPickup.Shape.Module, lab.TransformPoint(new Vector3(0.0f, 1.0f, -2.6f)), "doc_blackbox", true);
                Pickup(lab, "lab_procedure", "procedimento", MissionPickup.Shape.Document, lab.position + lab.forward * 4.6f + lab.right * 2.5f, "doc_containment", false);
                // benches inside
                Block(lab, "LabBench", new Vector3(-3.4f, 0.5f, 1.5f), new Vector3(1.0f, 1.0f, 3.0f), metal);
                Block(lab, "LabBench", new Vector3(3.4f, 0.5f, 1.0f), new Vector3(1.0f, 1.0f, 3.0f), metal);
                Block(lab, "LabBench", new Vector3(0.0f, 0.5f, -2.6f), new Vector3(3.0f, 1.0f, 0.9f), metal);
                // valves around the outside, to close in order
                for (int i = 0; i < 3; i++)
                {
                    Vector3 local = new Vector3(-4.0f + i * 4.0f, 0.0f, -4.6f);
                    MissionDevice valve = Device(lab, $"valve_{i + 1}", $"válvula {i + 1}", "Fechar a", lab.TransformPoint(local), lab.rotation * Quaternion.Euler(0.0f, 180.0f, 0.0f), panel, lamp);
                    valve.WorkTime = 3.0f;
                    GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    wheel.name = "ValveWheel";
                    wheel.transform.SetParent(valve.transform, false);
                    wheel.transform.localPosition = new Vector3(0.0f, 1.3f, -0.15f);
                    wheel.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
                    wheel.transform.localScale = new Vector3(0.5f, 0.03f, 0.5f);
                    wheel.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Effects/Loot_Cross.mat");
                    Object.DestroyImmediate(wheel.GetComponent<Collider>());
                }
            }

            // --- watchtower: one of the base's towers gets a breaker and floodlights
            PowerSector northWest = sectors.Length > 0 ? sectors[0] : null;
            Transform watch = NearestNamed("Tower", northWest != null ? northWest.Center : c, 200.0f);
            if (watch != null)
            {
                Bounds bounds = BoundsOf(watch);
                Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                if (OpenSpot(foot, bounds.extents.magnitude * 0.6f, bounds.extents.magnitude + 6.0f, 1.2f, used, out Vector3 basePoint))
                {
                    MissionAnchor anchor = Anchor(root.transform, "watchtower", "Torre de vigilância", basePoint);
                    for (int i = 0; i < 3; i++)
                    {
                        var flood = new GameObject("Floodlight").AddComponent<Light>();
                        flood.transform.SetParent(anchor.transform, false);
                        flood.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.5f, bounds.center.z);
                        flood.transform.rotation = Quaternion.Euler(50.0f, i * 120.0f, 0.0f);
                        flood.type = LightType.Spot;
                        flood.spotAngle = 70.0f;
                        flood.range = 45.0f;
                        flood.intensity = 120.0f;
                        flood.color = new Color(0.95f, 0.97f, 1.0f);
                        flood.enabled = false;
                    }

                    Device(anchor.transform, "tower_breaker", "quadro de força", "Restaurar a alimentação do", basePoint, Quaternion.identity, panel, lamp).WorkTime = 5.0f;
                    MissionDevice lights = Device(anchor.transform, "tower_lights", "refletores", "Ligar os", basePoint + Vector3.right * 1.2f, Quaternion.identity, panel, lamp);
                    lights.OnActivated = new[] { Effect(MissionEffectType.Floodlights, "watchtower") };
                }
            }

            // --- alarm panels in three sectors; the alarm sounds in the first
            for (int i = 0; i < 3 && i < sectors.Length; i++)
            {
                if (!OpenSpot(sectors[i].Center, 5.0f, 30.0f, 1.2f, used, out Vector3 panelSpot)) continue;
                if (i == 0) Anchor(root.transform, "alarm_zone", "Alarme", panelSpot);
                Device(root.transform, $"alarm_panel_{i + 1}", $"painel de alarme {i + 1}", "Desligar o", panelSpot, Quaternion.Euler(0.0f, Random.Range(0.0f, 360.0f), 0.0f), panel, lamp).WorkTime = 3.0f;
            }

            // --- supply depot: the container farthest from the start
            Transform depot = FarthestNamed("Container", spawn);
            if (depot != null)
            {
                Bounds bounds = BoundsOf(depot);
                if (OpenSpot(bounds.center, bounds.extents.magnitude * 0.5f, bounds.extents.magnitude + 5.0f, 1.2f, used, out Vector3 lockSpot))
                {
                    Anchor(root.transform, "depot", "Depósito de suprimentos", lockSpot);
                    MissionDevice lockDevice = Device(root.transform, "depot_lock", "contêiner de suprimentos", "Arrombar a trava do", lockSpot, Quaternion.LookRotation(Flat(bounds.center - lockSpot)), panel, lamp);
                    lockDevice.WorkTime = 12.0f;
                    lockDevice.Noise = 28.0f;
                    lockDevice.Reusable = false;
                    lockDevice.OnActivated = new[] { Effect(MissionEffectType.SpawnSupplies, "depot", 6.0f) };
                }
            }

            // --- helipad on open ground near the north edge, with lights that come on for the evacuation
            if (OpenSpot(c + new Vector3(0.0f, 0.0f, h.z * 0.6f), 0.0f, 50.0f, 7.0f, used, out Vector3 pad))
            {
                MissionAnchor anchor = Anchor(root.transform, "helipad", "Heliponto", pad);
                sites.Helipad = anchor.transform;
                Block(anchor.transform, "HelipadCircle", new Vector3(0.0f, 0.02f, 0.0f), new Vector3(11.0f, 0.04f, 11.0f), concrete, false);
                Block(anchor.transform, "HelipadH1", new Vector3(-1.6f, 0.05f, 0.0f), new Vector3(0.6f, 0.04f, 5.0f), paint, false);
                Block(anchor.transform, "HelipadH2", new Vector3(1.6f, 0.05f, 0.0f), new Vector3(0.6f, 0.04f, 5.0f), paint, false);
                Block(anchor.transform, "HelipadH3", new Vector3(0.0f, 0.05f, 0.0f), new Vector3(3.2f, 0.04f, 0.6f), paint, false);
                var lights = new GameObject("Reveal").transform;
                lights.SetParent(anchor.transform, false);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45.0f * Mathf.Deg2Rad;
                    var light = new GameObject("PadLight").AddComponent<Light>();
                    light.transform.SetParent(lights, false);
                    light.transform.localPosition = new Vector3(Mathf.Cos(angle) * 5.5f, 0.3f, Mathf.Sin(angle) * 5.5f);
                    light.type = LightType.Point;
                    light.color = new Color(0.3f, 1.0f, 0.4f);
                    light.range = 4.0f;
                    light.intensity = 4.0f;
                }

                var beacon = new GameObject("PadBeacon").AddComponent<Light>();
                beacon.transform.SetParent(lights, false);
                beacon.transform.localPosition = new Vector3(0.0f, 6.0f, 0.0f);
                beacon.type = LightType.Point;
                beacon.color = new Color(1.0f, 0.3f, 0.1f);
                beacon.range = 30.0f;
                beacon.intensity = 25.0f;
                lights.gameObject.SetActive(false);

                MissionDevice evacBeacon = Device(anchor.transform, "evac_beacon", "farol do heliponto", "Acender o", pad + Vector3.right * 6.5f, Quaternion.identity, panel, lamp);
                evacBeacon.WorkTime = 6.0f;
                evacBeacon.Noise = 35.0f;
                evacBeacon.OnActivated = new[] { Effect(MissionEffectType.Reveal, "helipad") };
            }

            // --- evacuation codes in the south generator room; a power relay near the middle (choice B)
            Transform shed3 = GameObject.Find("GeneratorShed_3")?.transform;
            if (shed3 != null) Pickup(shed3, "evac_codes", "códigos de evacuação", MissionPickup.Shape.Document, shed3.TransformPoint(new Vector3(-1.3f, 1.0f, -1.5f)), "doc_evac_codes", true);
            if (OpenSpot(c, 0.0f, 30.0f, 1.5f, used, out Vector3 relay))
            {
                MissionDevice relayDevice = Device(root.transform, "power_relay", "relé central de energia", "Religar o", relay, Quaternion.identity, panel, lamp);
                relayDevice.WorkTime = 15.0f;
                relayDevice.Noise = 25.0f;
                Anchor(root.transform, "power_relay_zone", "Relé central", relay);
            }

            Debug.Log($"Mission sites: {root.transform.childCount} objects, {Object.FindObjectsByType<MissionDevice>().Length} devices, {Object.FindObjectsByType<MissionPickup>().Length} items");
            return sites;
        }

        private static MissionAnchor Anchor(Transform parent, string id, string label, Vector3 position)
        {
            var go = new GameObject("Anchor_" + id);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var anchor = go.AddComponent<MissionAnchor>();
            anchor.Id = id;
            anchor.Label = label;
            return anchor;
        }

        // a wall panel on a post: box, status lamp and light
        private static MissionDevice Device(Transform parent, string id, string displayName, string verb, Vector3 position, Quaternion rotation, Material body, Material lamp)
        {
            var go = new GameObject("Device_" + id);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            Block(go.transform, "DevicePost", new Vector3(0.0f, 0.55f, 0.0f), new Vector3(0.12f, 1.1f, 0.12f), body);
            Block(go.transform, "DevicePanel", new Vector3(0.0f, 1.3f, 0.0f), new Vector3(0.55f, 0.65f, 0.18f), body);
            GameObject status = Block(go.transform, "DeviceLamp", new Vector3(0.18f, 1.52f, -0.1f), new Vector3(0.07f, 0.07f, 0.03f), lamp, false);
            var light = new GameObject("StatusLight").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0.18f, 1.52f, -0.3f);
            light.type = LightType.Point;
            light.range = 1.8f;
            light.intensity = 1.0f;
            light.shadows = LightShadows.None;

            var device = go.AddComponent<MissionDevice>();
            device.Id = id;
            device.DisplayName = displayName;
            device.Verb = verb;
            device.StatusLamp = status.GetComponent<Renderer>();
            device.StatusLight = light;
            device.UseSound = HorrorClip("Door_Locked");
            device.WorkLoop = HorrorClip("Generator_Loop");
            device.DoneSound = CampaignClip("Objective_Done");
            return device;
        }

        private static MissionPickup Pickup(Transform parent, string itemId, string displayName, MissionPickup.Shape shape, Vector3 position, string story, bool onlyWhileNeeded)
        {
            var go = new GameObject("Item_" + itemId);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var pickup = go.AddComponent<MissionPickup>();
            pickup.ItemId = itemId;
            pickup.DisplayName = displayName;
            pickup.Look = shape;
            pickup.StoryId = story;
            pickup.OnlyWhileNeeded = onlyWhileNeeded;
            pickup.GlowColor = story != null && !onlyWhileNeeded ? new Color(1.0f, 0.9f, 0.6f) : new Color(0.4f, 0.9f, 1.0f);
            return pickup;
        }

        // a room of any size with one door on its front (+Z): the laboratory
        private static Transform BuildRoom(Transform parent, string name, Vector3 position, Quaternion rotation, float width, float depth, float height,
            Material wallMaterial, Material doorMetal, Material sign)
        {
            var room = new GameObject(name).transform;
            room.SetParent(parent, false);
            room.SetPositionAndRotation(position, rotation);
            const float wall = 0.25f;
            const float doorWidth = 1.6f;
            const float doorHeight = 2.3f;
            Block(room, "LabFloor", new Vector3(0, 0.075f, 0), new Vector3(width, 0.15f, depth), wallMaterial);
            Block(room, "LabRoof", new Vector3(0, height + 0.1f, 0), new Vector3(width + 0.4f, 0.2f, depth + 0.4f), wallMaterial);
            Block(room, "LabWallBack", new Vector3(0, height * 0.5f, -depth * 0.5f + wall * 0.5f), new Vector3(width, height, wall), wallMaterial);
            Block(room, "LabWallLeft", new Vector3(-width * 0.5f + wall * 0.5f, height * 0.5f, 0), new Vector3(wall, height, depth), wallMaterial);
            Block(room, "LabWallRight", new Vector3(width * 0.5f - wall * 0.5f, height * 0.5f, 0), new Vector3(wall, height, depth), wallMaterial);
            float side = (width - doorWidth) * 0.5f;
            Block(room, "LabWallFrontL", new Vector3(-(doorWidth + side) * 0.5f, height * 0.5f, depth * 0.5f - wall * 0.5f), new Vector3(side, height, wall), wallMaterial);
            Block(room, "LabWallFrontR", new Vector3((doorWidth + side) * 0.5f, height * 0.5f, depth * 0.5f - wall * 0.5f), new Vector3(side, height, wall), wallMaterial);
            Block(room, "LabLintel", new Vector3(0, (doorHeight + height) * 0.5f, depth * 0.5f - wall * 0.5f), new Vector3(doorWidth, height - doorHeight, wall), wallMaterial);
            Block(room, "LabSign", new Vector3(0, doorHeight + 0.4f, depth * 0.5f + 0.02f), new Vector3(doorWidth + 1.0f, 0.3f, 0.04f), sign, false);

            var doorRoot = new GameObject("Door").transform;
            doorRoot.SetParent(room, false);
            doorRoot.localPosition = new Vector3(0, 0.15f, depth * 0.5f - wall * 0.5f);
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(doorRoot, false);
            hinge.localPosition = new Vector3(-doorWidth * 0.5f, 0, 0);
            Block(hinge, "DoorLeaf", new Vector3(doorWidth * 0.5f, (doorHeight - 0.15f) * 0.5f, 0), new Vector3(doorWidth - 0.04f, doorHeight - 0.17f, 0.08f), doorMetal);
            var door = doorRoot.gameObject.AddComponent<InteractiveDoor>();
            door.Leaf = hinge;
            door.OpenAngle = -100.0f;
            door.DisplayName = "porta do laboratório";
            door.MaxHealth = 200.0f;
            door.OpenSound = HorrorClip("Door_Open");
            door.CloseSound = HorrorClip("Door_Close");
            door.LockedSound = HorrorClip("Door_Locked");
            door.HitSounds = HorrorClips("Door_Hit");
            door.BreakSound = HorrorClip("Door_Break");

            // a failing light inside and a red one: the lab is never fully lit
            var lampObject = new GameObject("LabLight");
            lampObject.transform.SetParent(room, false);
            lampObject.transform.localPosition = new Vector3(0, height - 0.3f, 0);
            lampObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var light = lampObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 150.0f;
            light.range = 7.0f;
            light.intensity = 5.0f;
            light.color = new Color(0.8f, 0.9f, 1.0f);
            var flicker = lampObject.AddComponent<LightFlicker>();
            flicker.Flicker = 0.6f;
            flicker.BlackoutChance = 0.5f;
            var red = new GameObject("LabRedLight").AddComponent<Light>();
            red.transform.SetParent(room, false);
            red.transform.localPosition = new Vector3(3.0f, height - 0.4f, -2.5f);
            red.type = LightType.Point;
            red.color = new Color(1.0f, 0.1f, 0.05f);
            red.range = 5.0f;
            red.intensity = 2.5f;
            return room;
        }

        // ---------------------------------------------------------------- spot finding

        // open, flat, walkable ground at a distance from a point, away from things already placed
        private static bool OpenSpot(Vector3 near, float minDistance, float maxDistance, float clearance, List<Vector3> used, out Vector3 spot)
        {
            spot = Vector3.zero;
            float best = float.MaxValue;
            float target = (minDistance + maxDistance) * 0.5f;
            for (int ring = 0; ring <= 16; ring++)
            {
                float radius = Mathf.Lerp(minDistance, maxDistance, ring / 16.0f);
                int count = Mathf.Max(8, Mathf.RoundToInt(radius * 1.5f));
                for (int i = 0; i < count; i++)
                {
                    float angle = (i + ring * 0.37f) / count * Mathf.PI * 2.0f;
                    Vector3 candidate = near + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * radius;
                    if (!InCombatArea(candidate)) continue;
                    if (!Physics.Raycast(new Vector3(candidate.x, 40.0f, candidate.z), Vector3.down, out RaycastHit hit, 60.0f, 1, QueryTriggerInteraction.Ignore)) continue;
                    bool ground = hit.collider is TerrainCollider || hit.collider.name.StartsWith("Tile");
                    if (!ground || hit.point.y > 0.5f || hit.normal.y < 0.95f) continue;
                    if (Physics.CheckBox(hit.point + Vector3.up * 1.4f, new Vector3(clearance, 1.2f, clearance), Quaternion.identity, 1, QueryTriggerInteraction.Ignore)) continue;
                    bool crowded = false;
                    foreach (Vector3 other in used) crowded |= (other - hit.point).sqrMagnitude < Mathf.Max(4.0f, clearance * clearance * 4.0f);
                    if (crowded) continue;

                    // varied but deterministic: closest to the middle of the band, with a little spread by angle
                    float score = Mathf.Abs(Vector3.Distance(Flat(hit.point), Flat(near)) - target) + Mathf.Repeat(angle * 3.1f, 2.0f);
                    if (score < best)
                    {
                        best = score;
                        spot = hit.point;
                    }
                }
            }

            if (best == float.MaxValue) return false;
            used.Add(spot);
            return true;
        }

        // the flat top of a container at a distance, for items you have to climb for
        private static Vector3? ContainerTop(Vector3 near, float minDistance, float maxDistance, List<Vector3> used, out bool onTop)
        {
            onTop = false;
            foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>())
            {
                if (!renderer.name.StartsWith("Container")) continue;
                Bounds bounds = renderer.bounds;
                float distance = Vector3.Distance(Flat(bounds.center), Flat(near));
                if (distance < minDistance || distance > maxDistance) continue;
                Vector3 top = new Vector3(bounds.center.x, bounds.max.y + 2.0f, bounds.center.z);
                if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, 3.0f, 1, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.8f) continue;
                bool taken = false;
                foreach (Vector3 other in used) taken |= (other - hit.point).sqrMagnitude < 4.0f;
                if (taken) continue;
                onTop = true;
                used.Add(hit.point);
                return hit.point;
            }

            return null;
        }

        private static Transform NearestNamed(string prefix, Vector3 point, float maxDistance)
        {
            Transform best = null;
            float bestDistance = maxDistance;
            foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>())
            {
                if (!renderer.name.StartsWith(prefix)) continue;
                float distance = Vector3.Distance(Flat(renderer.bounds.center), Flat(point));
                if (distance < bestDistance)
                {
                    best = renderer.transform;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static Transform FarthestNamed(string prefix, Vector3 point)
        {
            Transform best = null;
            float bestDistance = 0.0f;
            foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>())
            {
                if (!renderer.name.StartsWith(prefix) || !InCombatArea(renderer.bounds.center)) continue;
                float distance = Vector3.Distance(renderer.bounds.center, point);
                if (distance > bestDistance)
                {
                    best = renderer.transform;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static Bounds BoundsOf(Transform t)
        {
            var renderer = t.GetComponent<Renderer>();
            return renderer != null ? renderer.bounds : new Bounds(t.position, Vector3.one);
        }

        private static Vector3 Flat(Vector3 v)
        {
            return new Vector3(v.x, 0.0f, v.z);
        }

        private static MissionEffect Effect(MissionEffectType type, string target, float value = 0.0f, string target2 = null)
        {
            return new MissionEffect { Type = type, Target = target, Value = value, Target2 = target2 };
        }

        // ---------------------------------------------------------------- bosses

        private static BossDefinition CreateBoss(string name)
        {
            string path = $"{CampaignRoot}/Boss_{name}.asset";
            var boss = AssetDatabase.LoadAssetAtPath<BossDefinition>(path);
            if (boss == null)
            {
                boss = ScriptableObject.CreateInstance<BossDefinition>();
                AssetDatabase.CreateAsset(boss, path);
            }

            boss.Animator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "/Enemies/Zombie_Runner.controller");
            if (name == "E07")
            {
                boss.Id = "e07";
                boss.DisplayName = "Experimento 07";
                boss.Title = "Sujeito militar do Protocolo Aurora — faça-o se chocar contra algo pesado";
                boss.MaxHealth = 1500.0f;
                boss.Scale = 1.7f;
                boss.Tint = new Color(0.8f, 0.5f, 0.48f);
                boss.ChaseSpeed = 2.4f;
                boss.MeleeDamage = 35.0f;
                boss.CanCharge = true;
                boss.Phases = new float[0];
                boss.Shockwave = false;
                boss.SummonCount = 0;
                boss.WeakToLight = false;
                boss.Credits = 500;
                boss.Experience = 400;
                boss.Score = 2500;
                boss.Achievement = "boss_e07";
            }
            else
            {
                boss.Id = "aurora";
                boss.DisplayName = "Aurora";
                boss.Title = "O sujeito primário — a luz é a sua fraqueza";
                boss.MaxHealth = 2600.0f;
                boss.Scale = 1.9f;
                boss.Tint = new Color(0.85f, 0.85f, 1.05f);
                boss.ChaseSpeed = 2.6f;
                boss.MeleeDamage = 40.0f;
                boss.CanCharge = true;
                boss.Phases = new[] { 0.66f, 0.33f };
                boss.Shockwave = true;
                boss.SummonCount = 3;
                boss.SummonVariant = AssetDatabase.LoadAssetAtPath<ZombieVariant>(HorrorRoot + "/Variants/Zombie_Runner.asset");
                boss.WeakToLight = true;
                boss.LightMultiplier = 1.8f;
                boss.Credits = 1000;
                boss.Experience = 800;
                boss.Score = 5000;
                boss.Achievement = "boss_aurora";
            }

            EditorUtility.SetDirty(boss);
            return boss;
        }

        // ---------------------------------------------------------------- missions

        private static ObjectiveDefinition Obj(ObjectiveType type, string text, string target = null, int count = 1, float duration = 0.0f,
            float radius = 5.0f, int pressure = 0, string radio = null, bool optional = false, int bonus = 0)
        {
            return new ObjectiveDefinition
            {
                Type = type, Text = text, Target = target, Count = count, Duration = duration, Radius = radius,
                PressureEnemies = pressure, Radio = radio, Optional = optional, BonusCredits = bonus
            };
        }

        private static MissionDefinition Mission(string id, string title, string briefing, System.Action<MissionDefinition> configure)
        {
            string path = $"{CampaignRoot}/Missions/{id}.asset";
            var mission = AssetDatabase.LoadAssetAtPath<MissionDefinition>(path);
            if (mission == null)
            {
                mission = ScriptableObject.CreateInstance<MissionDefinition>();
                AssetDatabase.CreateAsset(mission, path);
            }

            mission.Id = id;
            mission.Title = title;
            mission.Briefing = briefing;
            mission.Optional = false;
            mission.ChoiceGroup = null;
            mission.TimeLimit = 0.0f;
            mission.OnStart = new MissionEffect[0];
            mission.OnComplete = new MissionEffect[0];
            mission.OnFail = new MissionEffect[0];
            mission.RequiredIds = new string[0];
            mission.Credits = 150;
            mission.Experience = 100;
            mission.Score = 200;
            configure(mission);
            EditorUtility.SetDirty(mission);
            return mission;
        }

        private static Dictionary<string, MissionDefinition> CreateMissions()
        {
            var m = new Dictionary<string, MissionDefinition>();

            m["first_contact"] = Mission("m_first_contact", "Primeiro contato", "Você acorda na base. Arme-se e descubra o que aconteceu.", d =>
            {
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Armed, "Encontre uma arma (há uma pistola no chão por perto)", radio: "radio_wake"),
                    Obj(ObjectiveType.Investigate, "Investigue a área próxima", "first_area", radius: 6.0f),
                    Obj(ObjectiveType.Retrieve, "Leia o diário de turno", "shift_log", optional: true, bonus: 30),
                    Obj(ObjectiveType.SurviveWave, "Sobreviva ao primeiro ataque")
                };
                d.Credits = 100;
                d.Experience = 80;
            });

            m["generator"] = Mission("m_generator", "Gerador principal", "Um setor perdeu energia. Os fusíveis estão espalhados pela base.", d =>
            {
                d.RequiredIds = new[] { "gen_room", "fuse_box" };
                d.OnStart = new[] { Effect(MissionEffectType.CutPower, "gen_room", 1.0f), Effect(MissionEffectType.Radio, "radio_power") };
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Investigate, "Encontre a sala do gerador", "gen_room", radius: 6.0f),
                    Obj(ObjectiveType.Collect, "Recupere dois fusíveis", "fuse", 2),
                    Obj(ObjectiveType.Interact, "Instale os fusíveis", "fuse_box"),
                    Obj(ObjectiveType.RestorePower, "Ligue o gerador", "gen_room"),
                    Obj(ObjectiveType.Defend, "Defenda a posição durante a inicialização", "gen_room", duration: 40.0f, radius: 12.0f, pressure: 6)
                };
                d.OnComplete = new[] { Effect(MissionEffectType.GiveItem, "Grenade", 2.0f), Effect(MissionEffectType.SetFlag, "generator_fixed") };
                d.Credits = 200;
                d.Experience = 150;
            });

            m["signal"] = Mission("m_signal", "Sinal perdido", "O rádio recebeu uma mensagem incompleta. A estação de comunicação precisa de um módulo.", d =>
            {
                d.RequiredIds = new[] { "radio_station", "radio_console", "antenna" };
                d.OnStart = new[] { Effect(MissionEffectType.Radio, "radio_signal") };
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Investigate, "Localize a estação de comunicação", "radio_station", radius: 7.0f),
                    Obj(ObjectiveType.Collect, "Recupere o módulo eletrônico (talvez em cima de um contêiner)", "radio_module"),
                    Obj(ObjectiveType.Interact, "Instale o módulo no console", "radio_console"),
                    Obj(ObjectiveType.Interact, "Ative a antena e proteja a transmissão", "antenna", pressure: 6)
                };
                d.OnComplete = new[] { Effect(MissionEffectType.Radio, "radio_signal_ok"), Effect(MissionEffectType.Document, "doc_radio_order") };
                d.Credits = 200;
                d.Experience = 150;
            });

            m["armory"] = Mission("m_armory", "Arsenal bloqueado", "O depósito militar está trancado por cartão magnético.", d =>
            {
                d.RequiredIds = new[] { "armory", "armory_reader" };
                d.OnStart = new[] { Effect(MissionEffectType.Radio, "radio_armory"), Effect(MissionEffectType.LockDoor, "armory") };
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Collect, "Encontre o cartão de acesso (perto das torres)", "keycard"),
                    Obj(ObjectiveType.Interact, "Abra o arsenal com o cartão", "armory_reader"),
                    Obj(ObjectiveType.Door, "Entre no arsenal", "armory", 1),
                    Obj(ObjectiveType.Retrieve, "Recupere a caixa de equipamento", "armory_case")
                };
                d.OnComplete = new[]
                {
                    Effect(MissionEffectType.GiveAmmo, null, 4.0f), Effect(MissionEffectType.GiveItem, "Grenade", 2.0f),
                    Effect(MissionEffectType.SpawnWeapon, "armory", 0.0f, "M4")
                };
                d.Credits = 150;
                d.Experience = 150;
            });

            m["gates"] = Mission("m_gates", "Portões comprometidos", "A segurança acusa invasões. Feche os portões e reforce as passagens.", d =>
            {
                d.RequiredIds = new[] { "gate_1" };
                d.OnStart = new[] { Effect(MissionEffectType.Radio, "radio_gates"), Effect(MissionEffectType.GiveItem, "Barricade", 2.0f) };
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Interact, "Feche os portões", "gate", 3, pressure: 4),
                    Obj(ObjectiveType.Fortify, "Reforce duas passagens com barricadas (5 = barricada, X usar)", "gatebar", 2)
                };
                d.Credits = 180;
                d.Experience = 120;
            });

            m["blackbox"] = Mission("m_blackbox", "Caixa-preta", "Registros de uma experiência militar estão no laboratório.", d =>
            {
                d.RequiredIds = new[] { "lab", "lab_terminal" };
                d.OnStart = new[] { Effect(MissionEffectType.Radio, "radio_lab"), Effect(MissionEffectType.CutPower, "lab", 0.0f) };
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Investigate, "Localize o laboratório", "lab", radius: 6.0f),
                    Obj(ObjectiveType.Retrieve, "Encontre os registros científicos", "lab_doc", 2),
                    Obj(ObjectiveType.Interact, "Recupere os dados do terminal", "lab_terminal"),
                    Obj(ObjectiveType.Retrieve, "Pegue a caixa-preta", "blackbox")
                };
                d.OnComplete = new[] { Effect(MissionEffectType.SetFlag, "aurora_known") };
                d.Credits = 220;
                d.Experience = 200;
            });

            m["tower"] = Mission("m_tower", "Torre de vigilância", "Os refletores da torre seguram os infectados por um tempo.", d =>
            {
                d.RequiredIds = new[] { "watchtower" };
                d.OnStart = new[] { Effect(MissionEffectType.Radio, "radio_tower") };
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Investigate, "Chegue à torre", "watchtower", radius: 6.0f),
                    Obj(ObjectiveType.Interact, "Restaure a alimentação elétrica", "tower_breaker"),
                    Obj(ObjectiveType.Interact, "Ligue os refletores", "tower_lights"),
                    Obj(ObjectiveType.Defend, "Defenda a torre", "watchtower", duration: 50.0f, radius: 14.0f, pressure: 8)
                };
                d.Credits = 200;
                d.Experience = 150;
            });

            m["alarm"] = Mission("m_alarm", "Código vermelho", "Um alarme militar disparou e está atraindo a horda.", d =>
            {
                d.RequiredIds = new[] { "alarm_zone", "alarm_panel_1" };
                d.OnStart = new[] { Effect(MissionEffectType.StartAlarm, "alarm_zone"), Effect(MissionEffectType.Radio, "radio_alarm") };
                d.Objectives = new[] { Obj(ObjectiveType.Interact, "Desligue os painéis de alarme", "alarm_panel", 3, pressure: 5) };
                d.OnComplete = new[] { Effect(MissionEffectType.StopAlarm, "alarm_zone") };
                d.OnFail = new[] { Effect(MissionEffectType.StopAlarm, "alarm_zone") };
                d.TimeLimit = 240.0f;
                d.Credits = 160;
                d.Experience = 120;
            });

            m["supply"] = Mission("m_supply", "Último suprimento", "Um depósito no fundo da base guarda munição e remédios. Abrir faz barulho.", d =>
            {
                d.RequiredIds = new[] { "depot", "depot_lock" };
                d.OnStart = new[] { Effect(MissionEffectType.Radio, "radio_supply") };
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Investigate, "Chegue ao depósito", "depot", radius: 7.0f),
                    Obj(ObjectiveType.Interact, "Desbloqueie o contêiner (barulhento)", "depot_lock"),
                    Obj(ObjectiveType.Survive, "Recolha o que conseguir", duration: 8.0f)
                };
                d.Credits = 100;
                d.Experience = 100;
            });

            m["containment"] = Mission("m_containment", "Protocolo de contenção", "Os dutos estão abrindo. Feche as válvulas na ordem certa.", d =>
            {
                d.RequiredIds = new[] { "lab", "security_console", "valve_1" };
                d.OnStart = new[] { Effect(MissionEffectType.Radio, "radio_contain") };
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Interact, "Ative os sistemas de segurança", "security_console"),
                    Obj(ObjectiveType.Sequence, "Feche as válvulas na ordem 1, 2, 3", "valve", 3, pressure: 4),
                    Obj(ObjectiveType.Survive, "Segure a área enquanto os setores lacram", duration: 30.0f, pressure: 6)
                };
                d.OnComplete = new[] { Effect(MissionEffectType.BlockSpawns, "lab", 0.0f), Effect(MissionEffectType.SetFlag, "contained") };
                d.Credits = 220;
                d.Experience = 180;
            });

            m["evac_prep"] = Mission("m_evac_prep", "Protocolo de evacuação", "Os códigos finais estão numa das salas de gerador.", d =>
            {
                d.RequiredIds = new[] { "evac_radio", "helipad" };
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Retrieve, "Recupere os códigos de evacuação", "evac_codes"),
                    Obj(ObjectiveType.Interact, "Transmita o código pela estação de rádio", "evac_radio"),
                    Obj(ObjectiveType.Investigate, "Prepare a zona de extração", "helipad", radius: 8.0f)
                };
                d.OnComplete = new[] { Effect(MissionEffectType.Radio, "radio_evac") };
                d.Credits = 220;
                d.Experience = 200;
            });

            m["evacuation"] = Mission("m_evacuation", "Evacuação", "Acenda o farol, aguente a última onda e espere o helicóptero.", d =>
            {
                d.RequiredIds = new[] { "helipad", "evac_beacon" };
                d.OnStart = new[] { Effect(MissionEffectType.Radio, "radio_final") };
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Interact, "Acenda o farol do heliponto", "evac_beacon"),
                    Obj(ObjectiveType.SurviveWave, "Defenda a zona de extração"),
                    Obj(ObjectiveType.Defend, "Aguente até o helicóptero pousar", "helipad", duration: 45.0f, radius: 14.0f, pressure: 8)
                };
                d.Credits = 400;
                d.Experience = 400;
                d.Score = 2000;
            });

            m["e07"] = Mission("m_e07", "Experimento 07", "Algo enorme rompeu a contenção.", d =>
            {
                d.OnStart = new[] { Effect(MissionEffectType.Radio, "radio_e07") };
                d.Objectives = new[] { Obj(ObjectiveType.SurviveWave, "Sobreviva ao Experimento 07 e aos infectados") };
                d.OnComplete = new[] { Effect(MissionEffectType.Radio, "radio_e07_dead") };
                d.Credits = 150;
                d.Experience = 100;
            });

            // the choice between waves 4 and 5
            m["choice_armory"] = Mission("m_choice_armory", "Defender o arsenal", "Segure o arsenal contra saqueadores infectados.", d =>
            {
                d.ChoiceGroup = "arsenal_or_power";
                d.ChoiceSummary = "Mais munição e granadas agora, e armas 25% mais baratas na loja pelo resto da partida.";
                d.RequiredIds = new[] { "armory" };
                d.Objectives = new[]
                {
                    Obj(ObjectiveType.Investigate, "Vá ao arsenal", "armory", radius: 8.0f),
                    Obj(ObjectiveType.Defend, "Defenda o arsenal", "armory", duration: 45.0f, radius: 12.0f, pressure: 8)
                };
                d.OnComplete = new[]
                {
                    Effect(MissionEffectType.GiveAmmo, null, 5.0f), Effect(MissionEffectType.GiveItem, "Grenade", 3.0f),
                    Effect(MissionEffectType.ShopDiscount, null, 0.25f)
                };
                d.Credits = 150;
            });

            m["choice_power"] = Mission("m_choice_power", "Restaurar a energia", "Religue o relé central e estabilize a rede.", d =>
            {
                d.ChoiceGroup = "arsenal_or_power";
                d.ChoiceSummary = "Toda a base com luz, refletores da torre acesos, portas eletrônicas ativas e sem quedas de energia por 6 minutos.";
                d.RequiredIds = new[] { "power_relay" };
                d.Objectives = new[] { Obj(ObjectiveType.Interact, "Religue o relé central (barulhento)", "power_relay", pressure: 6) };
                d.OnComplete = new[]
                {
                    Effect(MissionEffectType.RestorePower, null), Effect(MissionEffectType.StablePower, null, 360.0f),
                    Effect(MissionEffectType.Floodlights, "watchtower")
                };
                d.Credits = 150;
            });

            // a plain wave for the chapters whose story mission doesn't launch one
            for (int i = 2; i <= 9; i++)
            {
                int wave = i;
                m["wave_" + i] = Mission($"m_wave_{i}", $"Onda {i}", "Eles estão vindo.", d =>
                {
                    d.Objectives = new[] { Obj(ObjectiveType.SurviveWave, "Sobreviva à onda") };
                    d.Credits = 50 + wave * 10;
                    d.Experience = 50;
                    d.Score = 100;
                });
            }

            return m;
        }

        private static CampaignDefinition CreateCampaignAsset(Dictionary<string, MissionDefinition> m, BossDefinition e07, BossDefinition aurora)
        {
            string path = CampaignRoot + "/Campaign_ProtocoloAurora.asset";
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(path);
            if (campaign == null)
            {
                campaign = ScriptableObject.CreateInstance<CampaignDefinition>();
                AssetDatabase.CreateAsset(campaign, path);
            }

            campaign.Title = "Protocolo Aurora";
            campaign.Intro = "Você acorda sozinho num posto de guarda. Ninguém responde. Algo deu muito errado nesta base.";
            campaign.Ending = "O helicóptero sobe enquanto a base queima atrás de você. O Protocolo Aurora acabou — mas alguém vai querer saber o que você viu.";

            CampaignDefinition.Chapter C(string title, string subtitle, MissionDefinition[] missions, int zombies, int soldiers, float health, float intermission)
            {
                return new CampaignDefinition.Chapter
                {
                    Title = title, Subtitle = subtitle, Missions = missions, SideMissions = new MissionDefinition[0],
                    Zombies = zombies, Soldiers = soldiers, EnemyHealth = health, Intermission = intermission, OnStart = new MissionEffect[0]
                };
            }

            var chapters = new List<CampaignDefinition.Chapter>
            {
                C("ONDA 1 — PRIMEIRO CONTATO", "O sobrevivente desperta na base.", new[] { m["first_contact"] }, 5, 0, 1.0f, 50.0f),
                C("ONDA 2 — SEM ENERGIA", "Um setor perdeu energia.", new[] { m["generator"], m["wave_2"] }, 7, 0, 1.0f, 60.0f),
                C("ONDA 3 — MENSAGEM DESCONHECIDA", "O rádio recebe uma transmissão.", new[] { m["signal"], m["wave_3"] }, 8, 1, 1.0f, 60.0f),
                C("ONDA 4 — ARSENAL", "Prepare-se para uma ameaça maior.", new[] { m["armory"], m["wave_4"] }, 9, 2, 1.05f, 75.0f),
                C("ONDA 5 — EXPERIMENTO 07", "O surto tem origem nesta base.", new[] { m["e07"] }, 6, 0, 1.05f, 75.0f),
                C("ONDA 6 — COMUNICAÇÃO COMPROMETIDA", "Restaure a torre e receba instruções.", new[] { m["tower"], m["wave_6"] }, 10, 2, 1.1f, 70.0f),
                C("ONDA 7 — LABORATÓRIO", "Descubra a origem da infecção.", new[] { m["blackbox"], m["wave_7"] }, 10, 3, 1.1f, 70.0f),
                C("ONDA 8 — FALHA DE CONTENÇÃO", "Impeça que os setores contaminados se abram.", new[] { m["containment"], m["wave_8"] }, 12, 2, 1.15f, 75.0f),
                C("ONDA 9 — PROTOCOLO DE EVACUAÇÃO", "Prepare a zona de extração.", new[] { m["evac_prep"], m["wave_9"] }, 13, 3, 1.15f, 80.0f),
                C("ONDA 10 — SOBREVIVA À NOITE", "O confronto final.", new[] { m["evacuation"] }, 14, 2, 1.2f, 0.0f)
            };

            chapters[0].Radio = "radio_wake";
            chapters[2].SideMissions = new[] { m["supply"] };
            chapters[3].ChoiceA = m["choice_armory"];
            chapters[3].ChoiceB = m["choice_power"];
            chapters[4].Boss = e07;
            chapters[5].SideMissions = new[] { m["gates"] };
            chapters[6].SideMissions = new[] { m["alarm"] };
            chapters[9].Boss = aurora;
            campaign.Chapters = chapters.ToArray();
            EditorUtility.SetDirty(campaign);
            return campaign;
        }

        // ---------------------------------------------------------------- shop

        private static WeaponUpgrade Upgrade(string id, string name, string description, UpgradeSlot slot, int price, int level, System.Action<WeaponUpgrade> configure)
        {
            string path = $"{CampaignRoot}/Upgrade_{id}.asset";
            var upgrade = AssetDatabase.LoadAssetAtPath<WeaponUpgrade>(path);
            if (upgrade == null)
            {
                upgrade = ScriptableObject.CreateInstance<WeaponUpgrade>();
                AssetDatabase.CreateAsset(upgrade, path);
            }

            upgrade.DisplayName = name;
            upgrade.Description = description;
            upgrade.Slot = slot;
            upgrade.Price = price;
            upgrade.RequiredLevel = level;
            upgrade.Pistols = upgrade.Rifles = upgrade.Scoped = upgrade.Shotguns = true;
            upgrade.Damage = upgrade.Magazine = upgrade.RecoilReduction = upgrade.Stability = upgrade.ReloadSpeed = upgrade.SightZoom = 0.0f;
            upgrade.Suppressor = false;
            configure(upgrade);
            EditorUtility.SetDirty(upgrade);
            return upgrade;
        }

        private static ShopCatalog CreateShop(WeaponData[] weapons)
        {
            WeaponUpgrade[] upgrades =
            {
                Upgrade("Barrel", "Cano reforçado", "+15% de dano.", UpgradeSlot.Barrel, 350, 1, u => u.Damage = 0.15f),
                Upgrade("Magazine", "Carregador estendido", "+40% de capacidade do pente.", UpgradeSlot.Magazine, 300, 1, u => u.Magazine = 0.4f),
                Upgrade("Grip", "Empunhadura vertical", "-30% de recuo, mais estável.", UpgradeSlot.Grip, 300, 1, u =>
                {
                    u.RecoilReduction = 0.3f;
                    u.Stability = 0.15f;
                    u.Pistols = false;
                }),
                Upgrade("Compensator", "Compensador", "-20% de recuo, -10% de dispersão.", UpgradeSlot.Muzzle, 250, 1, u =>
                {
                    u.RecoilReduction = 0.2f;
                    u.Stability = 0.1f;
                }),
                Upgrade("Suppressor", "Silenciador", "Tiros ouvidos de bem mais perto (furtividade).", UpgradeSlot.Muzzle, 400, 3, u =>
                {
                    u.Suppressor = true;
                    u.Damage = -0.05f;
                    u.Shotguns = false;
                }),
                Upgrade("Sight", "Mira holográfica", "Mira alternativa: mais zoom ao mirar, um pouco mais estável.", UpgradeSlot.Sight, 250, 2, u =>
                {
                    u.SightZoom = 0.18f;
                    u.Stability = 0.05f;
                    u.Scoped = false;
                }),
                Upgrade("Reload", "Mola de recarga rápida", "Recarrega 35% mais rápido.", UpgradeSlot.Mechanism, 300, 2, u => u.ReloadSpeed = 0.35f)
            };

            string path = CampaignRoot + "/ShopCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ShopCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            var entries = new List<ShopCatalog.Entry>();
            void Add(string id, string name, string description, ShopCategory category, ShopItemKind kind, int price, int level = 1, int limit = 0,
                WeaponData weapon = null, QuickItem item = QuickItem.Medkit, int amount = 1, WeaponUpgrade upgrade = null)
            {
                entries.Add(new ShopCatalog.Entry
                {
                    Id = id, DisplayName = name, Description = description, Category = category, Kind = kind, Price = price,
                    RequiredLevel = level, LimitPerRun = limit, Weapon = weapon, Item = item, Amount = amount, Upgrade = upgrade
                });
            }

            // pistol, uzi, ak, m4, shotgun, sniper
            Add("w_uzi", weapons[1].DisplayName, "Submetralhadora rápida, pouco alcance.", ShopCategory.Armas, ShopItemKind.Weapon, 400, weapon: weapons[1]);
            Add("w_shotgun", weapons[4].DisplayName, "Devastadora de perto.", ShopCategory.Armas, ShopItemKind.Weapon, 600, weapon: weapons[4]);
            Add("w_ak", weapons[2].DisplayName, "Fuzil potente, recuo forte.", ShopCategory.Armas, ShopItemKind.Weapon, 650, weapon: weapons[2]);
            Add("w_m4", weapons[3].DisplayName, "Fuzil preciso e controlável.", ShopCategory.Armas, ShopItemKind.Weapon, 650, 2, weapon: weapons[3]);
            Add("w_sniper", weapons[5].DisplayName, "Precisão de longe, com luneta.", ShopCategory.Armas, ShopItemKind.Weapon, 900, 3, weapon: weapons[5]);

            Add("ammo", "Pentes extras", "Dois pentes para cada arma que você carrega.", ShopCategory.Municao, ShopItemKind.Ammo, 60, amount: 2);
            Add("ammo_full", "Reabastecimento completo", "Pentes cheios e reservas no máximo.", ShopCategory.Municao, ShopItemKind.AmmoFull, 180);

            Add("vest", "Colete balístico", "Absorve metade do dano até 60 pontos.", ShopCategory.Protecao, ShopItemKind.Armor, 200, limit: 3);
            Add("heal", "Atendimento médico", "Vida cheia agora.", ShopCategory.Protecao, ShopItemKind.Heal, 150);

            Add("i_medkit", "Kit médico", "+50 de vida quando usado (tecla 1, X).", ShopCategory.Consumiveis, ShopItemKind.Consumable, 80, item: QuickItem.Medkit);
            Add("i_grenade", "Granada", "Explode 2,5 s depois do arremesso. Cuidado: te fere também (metade).", ShopCategory.Consumiveis, ShopItemKind.Consumable, 100, item: QuickItem.Grenade);
            Add("i_molotov", "Molotov", "Área de fogo por 8 s. Os inimigos desviam dela.", ShopCategory.Consumiveis, ShopItemKind.Consumable, 90, item: QuickItem.Molotov);
            Add("i_flare", "Sinalizador", "Luz vermelha por 40 s; atrai infectados para longe de você.", ShopCategory.Consumiveis, ShopItemKind.Consumable, 40, item: QuickItem.Flare);
            Add("i_barricade", "Kit de barricada", "Reforça uma porta ou passagem.", ShopCategory.Consumiveis, ShopItemKind.Consumable, 60, item: QuickItem.Barricade);
            Add("i_decoy", "Distração", "Apita por 10 s: os inimigos vão investigar.", ShopCategory.Consumiveis, ShopItemKind.Consumable, 50, item: QuickItem.Decoy);

            foreach (WeaponUpgrade upgrade in upgrades)
            {
                Add("u_" + upgrade.name, upgrade.DisplayName, upgrade.Description, ShopCategory.Melhorias, ShopItemKind.WeaponUpgrade, upgrade.Price, upgrade.RequiredLevel, upgrade: upgrade);
            }

            Add("stamina", "Treino de resistência", "+15 de estamina máxima nesta partida.", ShopCategory.Melhorias, ShopItemKind.Stamina, 250, 2, 2);
            Add("capacity", "Bolsa tática", "+1 de capacidade para cada item rápido.", ShopCategory.Melhorias, ShopItemKind.Capacity, 300, 1, 2);

            catalog.Entries = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        // ---------------------------------------------------------------- scene wiring

        private static void WireCampaignScene(WeaponData[] weapons, Dictionary<string, MissionDefinition> m, CampaignDefinition campaign,
            ShopCatalog catalog, BossDefinition e07, BossDefinition aurora, Sites sites)
        {
            GameObject root = FindOrCreate("CampaignSystems");
            foreach (Component c in root.GetComponents<Component>())
            {
                if (!(c is Transform)) Object.DestroyImmediate(c);
            }

            CampaignAssets assets = root.AddComponent<CampaignAssets>();
            assets.Explosions = new[] { CampaignClip("Explosion_1"), CampaignClip("Explosion_2") };
            assets.PinPull = CampaignClip("Grenade_Pin");
            assets.ThrowWhoosh = CampaignClip("Throw_Whoosh");
            assets.MolotovBreak = CampaignClip("Molotov_Break");
            assets.FireLoop = CampaignClip("Fire_Loop");
            assets.FlareLoop = CampaignClip("Flare_Loop");
            assets.DecoyBeep = CampaignClip("Decoy_Beep");
            assets.MedkitUse = CampaignClip("Medkit_Use");
            assets.BarricadeBuild = CampaignClip("Barricade_Build");
            assets.BarricadeHit = CampaignClip("Barricade_Hit");
            assets.BarricadeBreak = CampaignClip("Barricade_Break");
            assets.ObjectiveDone = CampaignClip("Objective_Done");
            assets.MissionDone = CampaignClip("Mission_Done");
            assets.RadioBlip = CampaignClip("Radio_Blip");
            assets.ShopBuy = CampaignClip("Shop_Buy");
            assets.ShopDeny = CampaignClip("Shop_Deny");
            assets.Achievement = CampaignClip("Achievement");
            assets.Helicopter = CampaignClip("Helicopter_Loop");
            assets.BossRoar = CampaignClip("Boss_Roar");
            assets.BossCharge = CampaignClip("Boss_Charge");
            assets.BossSlam = CampaignClip("Boss_Slam");
            assets.BossStunned = CampaignClip("Boss_Stunned");
            assets.BossDeath = CampaignClip("Boss_Death");
            assets.Shockwave = CampaignClip("Shockwave");
            assets.BossMusic = CampaignClip("Music_Boss_Loop");
            Texture2D dot = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Effects/FX_SoftDot.png");
            FxMaterial("FX_Fire", "Universal Render Pipeline/Particles/Unlit", dot, new Color(1.0f, 0.55f, 0.2f, 1.0f), true);
            FxMaterial("FX_Flare", "Universal Render Pipeline/Particles/Unlit", dot, new Color(1.0f, 0.25f, 0.15f, 1.0f), true);
            assets.FireMaterial = Mat("FX_Fire");
            assets.FlareMaterial = Mat("FX_Flare");

            MissionManager missions = root.AddComponent<MissionManager>();
            missions.Weapons = weapons;
            ShopManager shop = root.AddComponent<ShopManager>();
            shop.Catalog = catalog;
            CampaignManager manager = root.AddComponent<CampaignManager>();
            manager.Campaign = campaign;
            manager.EndingPoint = sites.Helipad;
            EndlessDirector endless = root.AddComponent<EndlessDirector>();
            endless.SideMissions = new[] { m["generator"], m["signal"], m["armory"], m["gates"], m["tower"], m["alarm"], m["supply"], m["containment"], m["blackbox"] };
            endless.ChoiceA = m["choice_armory"];
            endless.ChoiceB = m["choice_power"];
            root.AddComponent<CinematicPlayer>();
            root.AddComponent<CampaignHUD>();

            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                spawner.MidBoss = e07;
                spawner.FinalBoss = aurora;
                spawner.BossEvery = 5;
                spawner.IntermissionTime = 45.0f;
                EditorUtility.SetDirty(spawner);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
