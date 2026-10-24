using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Combat.EditorTools
{
    /* Survival horror pass: surface footsteps and noise for the AI, stealth and perception,
     * factions, the power grid with generators and sheds, doors, weather, environmental events,
     * close combat, injuries, special infected and the tension director. Safe to run again.
     */
    public static partial class CombatSetup
    {
        private const string HorrorSounds = "Assets/SourceFiles/SoundFX/Horror/";
        private const string HorrorRoot = Root + "/Horror";

        [MenuItem("Tools/Combat/Setup Horror Systems")]
        public static void SetupHorror()
        {
            EnsureFolder(HorrorRoot);
            ImportHorrorSounds();
            FootstepLibrary steps = CreateFootstepLibrary();
            ZombieVariant[] variants = CreateZombieVariants();
            SetupHorrorPlayer(steps);
            SetupHorrorEnemies(steps);
            SetupHorrorScene(variants);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Horror systems set up");
        }

        private static AudioClip HorrorClip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(HorrorSounds + name + ".wav");
        }

        private static AudioClip[] HorrorClips(string prefix)
        {
            var clips = new List<AudioClip>();
            for (int i = 1; i <= 6; i++)
            {
                AudioClip clip = HorrorClip($"{prefix}_{i}");
                if (clip != null) clips.Add(clip);
            }

            return clips.ToArray();
        }

        // ---------------------------------------------------------------- audio

        private static void ImportHorrorSounds()
        {
            AssetDatabase.Refresh();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { HorrorSounds.TrimEnd('/') }))
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
                // 3D sounds are mono anyway; this keeps memory down
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
        }

        private static FootstepLibrary CreateFootstepLibrary()
        {
            string path = HorrorRoot + "/FootstepLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<FootstepLibrary>(path);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<FootstepLibrary>();
                AssetDatabase.CreateAsset(library, path);
            }

            // loudness: how far the AI hears steps on it; metal rings out, grass muffles
            (SurfaceType surface, float loudness, float volume)[] table =
            {
                (SurfaceType.Concrete, 1.0f, 0.8f), (SurfaceType.Metal, 1.6f, 0.9f), (SurfaceType.Dirt, 0.8f, 0.8f),
                (SurfaceType.Grass, 0.6f, 0.7f), (SurfaceType.Water, 1.3f, 0.9f), (SurfaceType.Wood, 1.1f, 0.85f)
            };
            library.Surfaces = new FootstepLibrary.SurfaceSounds[table.Length];
            for (int i = 0; i < table.Length; i++)
            {
                var (surface, loudness, volume) = table[i];
                library.Surfaces[i] = new FootstepLibrary.SurfaceSounds
                {
                    Surface = surface,
                    Steps = HorrorClips($"Step_{surface}"),
                    Lands = HorrorClips($"Land_{surface}"),
                    Loudness = loudness,
                    Volume = volume
                };
            }

            library.WetLayer = HorrorClips("Step_WetLayer");
            EditorUtility.SetDirty(library);
            return library;
        }

        // ---------------------------------------------------------------- special infected

        private static ZombieVariant[] CreateZombieVariants()
        {
            EnsureFolder(HorrorRoot + "/Variants");
            AudioClip[] groans = Clips("Zombie_Groan");

            ZombieVariant runner = Variant("Runner", v =>
            {
                v.Kind = ZombieKind.Runner;
                v.DisplayName = "Corredor";
                v.BonusPoints = 50;
                v.Scale = 0.95f;
                v.Tint = new Color(0.85f, 0.78f, 0.72f);
                v.Animator = CreateRunnerController();
                v.MoveAnimationSpeed = 5.0f;
                v.Health = 0.6f;
                v.WanderSpeed = 1.3f;
                v.ChaseSpeed = 3.6f;
                v.Damage = 0.8f;
                v.AttackCooldown = 0.7f;
                v.AttackHitTime = 0.45f;
                v.AttackRange = 1.4f;
                v.SightRange = 26.0f;
                v.Hearing = 1.4f;
                v.DetectionSpeed = 2.6f;
                v.Groans = groans;
                v.VoicePitch = 1.25f;
                v.DetectSound = HorrorClip("Runner_Shriek");
            });

            ZombieVariant tank = Variant("Tank", v =>
            {
                v.Kind = ZombieKind.Tank;
                v.DisplayName = "Tanque";
                v.BonusPoints = 250;
                v.Scale = 1.35f;
                v.Height = 2.4f;
                v.Tint = new Color(0.55f, 0.48f, 0.48f);
                v.Health = 4.5f;
                v.WanderSpeed = 0.8f;
                v.ChaseSpeed = 0.8f;
                v.Damage = 2.6f;
                v.AttackCooldown = 1.5f;
                // the long, slow wind-up is the tell: back off when it roars
                v.AttackHitTime = 1.1f;
                v.AttackRange = 2.0f;
                // weak spot: the head
                v.HeadMultiplier = 2.5f;
                v.SightRange = 20.0f;
                v.Hearing = 0.8f;
                v.DetectionSpeed = 1.2f;
                v.ShoveResistance = 0.9f;
                v.BreaksDoors = true;
                v.DoorDamage = 45.0f;
                v.Groans = HorrorClips("Tank_Groan");
                v.VoicePitch = 0.6f;
                v.DetectSound = HorrorClip("Tank_Roar");
                v.WindupSound = HorrorClip("Tank_Roar");
            });

            ZombieVariant screamer = Variant("Screamer", v =>
            {
                v.Kind = ZombieKind.Screamer;
                v.DisplayName = "Gritador";
                v.BonusPoints = 120;
                v.Scale = 0.92f;
                v.Tint = new Color(0.95f, 0.88f, 0.65f);
                v.Health = 0.55f;
                v.ChaseSpeed = 1.1f;
                v.Damage = 0.6f;
                v.SightRange = 28.0f;
                v.Hearing = 1.3f;
                v.DetectionSpeed = 2.0f;
                v.Groans = groans;
                v.VoicePitch = 1.35f;
                v.DetectSound = HorrorClip("Screamer_Scream");
                v.WindupSound = HorrorClip("Screamer_Charge");
            });

            ZombieVariant crawler = Variant("Crawler", v =>
            {
                v.Kind = ZombieKind.Crawler;
                v.DisplayName = "Rastejante";
                v.BonusPoints = 80;
                v.Height = 0.8f;
                v.Tint = new Color(0.6f, 0.6f, 0.55f);
                v.Animator = CreateCrawlerController();
                v.MoveAnimationSpeed = 0.7f;
                v.Health = 0.5f;
                v.WanderSpeed = 0.7f;
                v.ChaseSpeed = 1.5f;
                v.Damage = 0.9f;
                v.AttackHitTime = 0.4f;
                v.AttackRange = 1.3f;
                v.SightRange = 16.0f;
                v.DetectionSpeed = 1.8f;
                v.ShoveResistance = 0.3f;
                v.Ambusher = true;
                v.Groans = HorrorClips("Crawler_Hiss");
                v.VoicePitch = 0.9f;
                v.DetectSound = HorrorClip("Crawler_Hiss_1");
            });

            return new[] { runner, tank, screamer, crawler };
        }

        private static ZombieVariant Variant(string name, System.Action<ZombieVariant> configure)
        {
            string path = $"{HorrorRoot}/Variants/Zombie_{name}.asset";
            var variant = AssetDatabase.LoadAssetAtPath<ZombieVariant>(path);
            if (variant == null)
            {
                variant = ScriptableObject.CreateInstance<ZombieVariant>();
                AssetDatabase.CreateAsset(variant, path);
            }

            configure(variant);
            EditorUtility.SetDirty(variant);
            return variant;
        }

        // the common zombie controller with a walk-to-run blend (the humanoid run of the starter assets)
        private static AnimatorController CreateRunnerController()
        {
            string path = Root + "/Enemies/Zombie_Runner.controller";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(Root + "/Enemies/Zombie.controller", path);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            AnimationClip run = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath("Assets/SourceFiles/StarterAssets/ThirdPersonController/Character/Animations/Locomotion--Run_N.anim.fbx"))
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) run = c;
            }

            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (child.state.name != "Walk") continue;
                var tree = new BlendTree { name = "WalkRun", blendParameter = "Speed", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(tree, controller);
                tree.AddChild(child.state.motion, 0.9f);
                if (run != null) tree.AddChild(run, 3.5f);
                child.state.motion = tree;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // a crawl made from muscle curves: body low and pitched forward, arms pulling in turns, legs dragging
        private static AnimatorController CreateCrawlerController()
        {
            AnimationClip crawl = CreateCrawlClip();
            string path = Root + "/Enemies/Zombie_Crawler.controller";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(Root + "/Enemies/Zombie.controller", path);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (child.state.name == "Walk" || child.state.name == "Idle") child.state.motion = crawl;
                if (child.state.name == "Idle") child.state.speed = 0.15f;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip CreateCrawlClip()
        {
            string path = HorrorRoot + "/Zombie_Crawl.anim";
            AnimationClip idle = ZombieClip("Idle01", "Zombie@Idle01");

            // the zombie's idle pose as the base: muscles only
            var basePose = new Dictionary<string, float>();
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(idle))
            {
                string n = b.propertyName;
                if (n.Contains("FootT") || n.Contains("FootQ") || n.Contains("HandT") || n.Contains("HandQ") || n.Contains("TDOF")) continue;
                basePose[n] = AnimationUtility.GetEditorCurve(idle, b).Evaluate(0);
            }

            float bodyHeight = basePose.TryGetValue("RootT.y", out float h) ? h : 1.0f;
            // prone: the body low and pitched about 70 degrees forward
            Quaternion prone = Quaternion.Euler(72.0f, 0.0f, 0.0f);
            var common = Muscles(("Neck Nod Down-Up", 0.7f), ("Head Nod Down-Up", 0.6f), ("Spine Front-Back", -0.3f), ("Chest Front-Back", -0.2f),
                ("Left Upper Leg Front-Back", 0.35f), ("Right Upper Leg Front-Back", 0.35f),
                ("Left Lower Leg Stretch", 0.3f), ("Right Lower Leg Stretch", -0.2f),
                ("Left Upper Leg In-Out", 0.3f), ("Right Upper Leg In-Out", 0.3f));
            common["RootT.x"] = 0.0f;
            common["RootT.y"] = bodyHeight * 0.32f;
            common["RootT.z"] = 0.0f;
            common["RootQ.x"] = prone.x;
            common["RootQ.y"] = prone.y;
            common["RootQ.z"] = prone.z;
            common["RootQ.w"] = prone.w;

            // left arm reaches while the right pulls, then the other way round
            Dictionary<string, float> Reach(float side)
            {
                var pose = new Dictionary<string, float>(common);
                pose["Left Arm Front-Back"] = side > 0 ? 1.0f : 0.1f;
                pose["Right Arm Front-Back"] = side > 0 ? 0.1f : 1.0f;
                pose["Left Arm Down-Up"] = side > 0 ? 0.3f : -0.3f;
                pose["Right Arm Down-Up"] = side > 0 ? -0.3f : 0.3f;
                pose["Left Forearm Stretch"] = side > 0 ? 0.8f : -0.4f;
                pose["Right Forearm Stretch"] = side > 0 ? -0.4f : 0.8f;
                pose["Left Lower Leg Stretch"] = side > 0 ? 0.3f : -0.2f;
                pose["Right Lower Leg Stretch"] = side > 0 ? -0.2f : 0.3f;
                pose["Spine Left-Right"] = side * 0.25f;
                return pose;
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.ClearCurves();
            var keys = new List<(float time, Dictionary<string, float> pose)> { (0.0f, Reach(1)), (0.6f, Reach(-1)), (1.2f, Reach(1)) };
            var names = new HashSetLike(basePose.Keys);
            foreach (var (_, pose) in keys) names.AddRange(pose.Keys);
            foreach (string n in names.Items)
            {
                var curve = new AnimationCurve();
                foreach (var (time, pose) in keys)
                {
                    float value = pose.TryGetValue(n, out float v) ? v : basePose.TryGetValue(n, out float b) ? b : 0.0f;
                    curve.AddKey(new Keyframe(time, value));
                }

                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }

                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), n), curve);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        // keeps insertion order without duplicates
        private class HashSetLike
        {
            public readonly List<string> Items = new List<string>();
            private readonly Dictionary<string, bool> _seen = new Dictionary<string, bool>();

            public HashSetLike(IEnumerable<string> items)
            {
                AddRange(items);
            }

            public void AddRange(IEnumerable<string> items)
            {
                foreach (string item in items)
                {
                    if (_seen.ContainsKey(item)) continue;
                    _seen[item] = true;
                    Items.Add(item);
                }
            }
        }

        // ---------------------------------------------------------------- player

        private static void SetupHorrorPlayer(FootstepLibrary steps)
        {
            const string path = "Assets/Prefabs/PlayerRobot.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            var controller = root.GetComponentInChildren<StarterAssets.ThirdPersonController>(true);
            GameObject player = controller.gameObject;

            controller.RollCooldown = 0.6f;
            controller.AirJumpStaminaCost = 12.0f;
            controller.CrouchSpeed = 1.4f;
            controller.CrouchHeight = 1.2f;
            controller.CrouchCameraDrop = 0.5f;
            controller.CrouchBodyDrop = 0.42f;

            var health = player.GetComponent<Health>();
            // regeneration only patches you up to half; past that, medkits
            health.RegenCap = 0.5f;
            health.RegenDelay = 6.0f;

            SurfaceFootstepSystem footsteps = Ensure<SurfaceFootstepSystem>(player);
            footsteps.Library = steps;
            footsteps.EmitNoise = true;
            footsteps.UseAnimationEvents = true;
            footsteps.GroundLayers = controller.GroundLayers;
            footsteps.Volume = 0.55f;
            footsteps.RunSpeed = controller.MoveSpeed + 1.0f;

            Ensure<PerceptionTarget>(player);

            PlayerCloseCombat close = Ensure<PlayerCloseCombat>(player);
            close.SwingSounds = HorrorClips("Melee_Swing");
            close.StrikeHitSounds = HorrorClips("Melee_Hit");
            close.ShoveSound = HorrorClip("Melee_Shove");
            close.EffortSounds = HorrorClips("Melee_Effort");

            PlayerInjuryFeedback injury = Ensure<PlayerInjuryFeedback>(player);
            injury.BreathingLoop = HorrorClip("Injury_Breathing_Loop");
            injury.HeartbeatLoop = HorrorClip("Injury_Heartbeat_Loop");

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ---------------------------------------------------------------- enemies

        private static void SetupHorrorEnemies(FootstepLibrary steps)
        {
            EditPrefab("Assets/Prefabs/Enemies/Zombie.prefab", root =>
            {
                root.GetComponent<Health>().Team = Factions.Infected;
                EnemyPerception perception = Ensure<EnemyPerception>(root);
                perception.SightRange = 22.0f;
                perception.FieldOfView = 140.0f;
                perception.DetectionSpeed = 1.6f;
                perception.HearingMultiplier = 1.0f;
                perception.LoseSightTime = 3.5f;
                perception.SearchTime = 12.0f;

                SurfaceFootstepSystem footsteps = Ensure<SurfaceFootstepSystem>(root);
                footsteps.Library = steps;
                footsteps.EmitNoise = false;
                footsteps.UseAnimationEvents = false;
                footsteps.StrideLength = 0.9f;
                footsteps.Volume = 0.45f;
                footsteps.MaxDistance = 22.0f;
                footsteps.GroundLayers = 1;
            });

            for (int i = 1; i <= 4; i++)
            {
                EditPrefab($"Assets/Prefabs/Enemies/Soldier_{i}.prefab", root =>
                {
                    root.GetComponent<Health>().Team = Factions.Soldiers;
                    EnemyPerception perception = Ensure<EnemyPerception>(root);
                    perception.SightRange = 35.0f;
                    perception.FieldOfView = 130.0f;
                    perception.DetectionSpeed = 2.2f;
                    perception.HearingMultiplier = 1.1f;
                    perception.LoseSightTime = 2.5f;
                    perception.SearchTime = 14.0f;

                    var shooter = root.GetComponent<EnemyShooterAI>();
                    shooter.DamageMultiplierVsInfected = 0.8f;

                    SurfaceFootstepSystem footsteps = Ensure<SurfaceFootstepSystem>(root);
                    footsteps.Library = steps;
                    footsteps.EmitNoise = false;
                    footsteps.UseAnimationEvents = true;
                    footsteps.StrideLength = 1.3f;
                    footsteps.Volume = 0.5f;
                    footsteps.MaxDistance = 25.0f;
                    footsteps.GroundLayers = 1;
                });
            }
        }

        private static void EditPrefab(string path, System.Action<GameObject> edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ---------------------------------------------------------------- scene

        private static void SetupHorrorScene(ZombieVariant[] variants)
        {
            Physics.SyncTransforms();

            // a wider camera collision so the view doesn't slip into walls in tight spots like the huts
            var follow = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineThirdPersonFollow>();
            if (follow != null)
            {
                follow.AvoidObstacles.CameraRadius = 0.3f;
                EditorUtility.SetDirty(follow);
            }

            GameObject root = FindOrCreate("HorrorSystems");
            ClearChildren(root.transform);
            foreach (Component c in root.GetComponents<Component>())
            {
                if (!(c is Transform)) Object.DestroyImmediate(c);
            }

            EnsureFolder(HorrorRoot + "/Materials");
            Material concrete = HorrorMaterial("Shed_Concrete", new Color(0.32f, 0.31f, 0.29f), 0.0f, 0.15f, Color.black);
            Material doorMetal = HorrorMaterial("Shed_Door", new Color(0.17f, 0.2f, 0.17f), 0.6f, 0.35f, Color.black);
            // the yellow band over the doors glows faintly so the huts can be spotted at night
            Material warning = HorrorMaterial("Shed_Warning", new Color(0.75f, 0.55f, 0.05f), 0.2f, 0.3f, new Color(0.45f, 0.3f, 0.03f));
            Material generatorBody = HorrorMaterial("Generator_Body", new Color(0.24f, 0.27f, 0.15f), 0.5f, 0.35f, Color.black);
            Material darkMetal = HorrorMaterial("Generator_Metal", new Color(0.08f, 0.08f, 0.09f), 0.8f, 0.4f, Color.black);
            Material statusLamp = HorrorMaterial("Generator_Status", new Color(0.2f, 1.0f, 0.3f), 0.0f, 0.6f, new Color(0.6f, 3.0f, 0.9f));

            // power grid: four sectors, one per quadrant of the base
            var gridObject = new GameObject("PowerGrid");
            gridObject.transform.SetParent(root.transform, false);
            var grid = gridObject.AddComponent<PowerGridManager>();
            grid.HumLoop = HorrorClip("Power_Hum_Loop");
            grid.AlarmLoop = HorrorClip("Power_Alarm_Loop");
            grid.PowerDownSound = HorrorClip("Power_Down");
            grid.PowerUpSound = HorrorClip("Power_Up");

            Vector3 c0 = MilitaryBase.Center;
            Vector3 half = MilitaryBase.NavMeshSize * 0.5f;
            var sectors = new[]
            {
                new PowerSector { Name = "Setor Noroeste", Center = c0 + new Vector3(-half.x * 0.5f, 0, half.z * 0.5f), Size = new Vector3(half.x, 40, half.z) },
                new PowerSector { Name = "Setor Nordeste", Center = c0 + new Vector3(half.x * 0.5f, 0, half.z * 0.5f), Size = new Vector3(half.x, 40, half.z) },
                new PowerSector { Name = "Setor Sudoeste", Center = c0 + new Vector3(-half.x * 0.5f, 0, -half.z * 0.5f), Size = new Vector3(half.x, 40, half.z) },
                new PowerSector { Name = "Setor Sudeste", Center = c0 + new Vector3(half.x * 0.5f, 0, -half.z * 0.5f), Size = new Vector3(half.x, 40, half.z) }
            };
            grid.Sectors = sectors;

            // a generator shed in each sector, placed in open ground near the sector's lamps;
            // two sectors also get an armory whose electronic door needs the power on
            var used = new List<Vector3>();
            foreach (Transform loot in FindOrCreate("Loot").transform) used.Add(loot.position);
            var player = Object.FindAnyObjectByType<PlayerCombat>();
            if (player != null) used.Add(player.transform.position);

            for (int i = 0; i < sectors.Length; i++)
            {
                if (!FindShedSpot(sectors[i], used, out Vector3 spot, out Quaternion facing))
                {
                    Debug.LogWarning($"No room for the generator shed in {sectors[i].Name}");
                    continue;
                }

                Transform shed = BuildShed(root.transform, $"GeneratorShed_{i + 1}", spot, facing, concrete, doorMetal, warning, false);
                var generatorObject = new GameObject("Generator");
                generatorObject.transform.SetParent(shed, false);
                generatorObject.transform.localPosition = new Vector3(0.0f, 0.15f, -1.1f);
                PowerGenerator generator = BuildGenerator(generatorObject, generatorBody, darkMetal, statusLamp);
                sectors[i].Generator = generator;

                if (i == 1 || i == 2)
                {
                    if (FindShedSpot(sectors[i], used, out Vector3 armorySpot, out Quaternion armoryFacing))
                    {
                        Transform armory = BuildShed(root.transform, $"Armory_{i + 1}", armorySpot, armoryFacing, concrete, doorMetal, warning, true);
                        var chest = new GameObject("Chest");
                        chest.transform.SetParent(armory, false);
                        chest.transform.localPosition = new Vector3(0.0f, 0.15f, -1.0f);
                        var crate = chest.AddComponent<LootCrate>();
                        WeaponData[] weapons = CreateWeapons();
                        crate.LootTable = weapons;
                        // the armory favours the strong guns
                        crate.Weights = new[] { 0.0f, 1.0f, 3.0f, 3.0f, 2.0f, 2.0f };
                        crate.MedkitChance = 1.0f;
                        crate.AmmoChance = 1.0f;
                    }
                }
            }

            EditorUtility.SetDirty(grid);

            // weather
            var weatherObject = new GameObject("Weather");
            weatherObject.transform.SetParent(root.transform, false);
            var weather = weatherObject.AddComponent<WeatherController>();
            weather.Moon = RenderSettings.sun;
            weather.RainLoop = HorrorClip("Rain_Loop");
            weather.WindLoop = HorrorClip("Wind_Loop");
            weather.ThunderSounds = HorrorClips("Thunder");
            weather.RainMaterial = RainMaterial();
            weather.DustMaterial = Mat("FX_Smoke");

            // environmental events
            var eventsObject = new GameObject("EnvironmentalEvents");
            eventsObject.transform.SetParent(root.transform, false);
            var events = eventsObject.AddComponent<EnvironmentalEventDirector>();
            events.DoorSlams = HorrorClips("Event_DoorSlam");
            events.Footsteps = HorrorClips("Event_Footsteps");
            var thuds = new List<AudioClip>(HorrorClips("Event_ContainerThud"));
            AudioClip oldThumps = Clip("Ambient_Thumps");
            if (oldThumps != null) thuds.Add(oldThumps);
            events.Thuds = thuds.ToArray();
            var moans = new List<AudioClip>(HorrorClips("Event_Moan"));
            moans.AddRange(Clips("Zombie_Groan"));
            events.Moans = moans.ToArray();
            events.RadioStatic = HorrorClips("Event_Radio");
            events.FallingObjects = HorrorClips("Event_Falling");
            var scrapes = new List<AudioClip>(HorrorClips("Event_Scrape"));
            AudioClip groan = Clip("Ambient_MetalGroan");
            if (groan != null) scrapes.Add(groan);
            events.Scrapes = scrapes.ToArray();
            events.Shuffles = HorrorClips("Event_Shuffle");
            events.SpotlightOn = HorrorClip("Event_SpotlightOn");
            events.SilhouettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Zombie.prefab");

            var tensionObject = new GameObject("TensionDirector");
            tensionObject.transform.SetParent(root.transform, false);
            tensionObject.AddComponent<TensionDirector>();

            // special infected in the waves
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                spawner.ZombieVariants = new[]
                {
                    new EnemySpawner.VariantSpawn { Variant = variants[0], Weight = 3.0f, MinWave = 2 },
                    new EnemySpawner.VariantSpawn { Variant = variants[3], Weight = 2.0f, MinWave = 2 },
                    new EnemySpawner.VariantSpawn { Variant = variants[2], Weight = 1.5f, MinWave = 3 },
                    new EnemySpawner.VariantSpawn { Variant = variants[1], Weight = 1.0f, MinWave = 4 }
                };
                EditorUtility.SetDirty(spawner);
            }

            // the inside lights of the sheds belong to the grid like the street lamps
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static Material HorrorMaterial(string name, Color color, float metallic, float smoothness, Color emission)
        {
            string path = $"{HorrorRoot}/Materials/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0.0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material RainMaterial()
        {
            Texture2D streak = TextureAsset("FX_RainStreak", 32, (u, v) => new Color(1, 1, 1, Mathf.Exp(-u * u * 18.0f) * Mathf.Clamp01(1.0f - Mathf.Abs(v))));
            FxMaterial("FX_Rain", "Universal Render Pipeline/Particles/Unlit", streak, new Color(0.7f, 0.75f, 0.85f, 0.45f), false);
            return Mat("FX_Rain");
        }

        // open, flat ground in the sector, near its lamps, away from loot and other buildings
        private static bool FindShedSpot(PowerSector sector, List<Vector3> used, out Vector3 spot, out Quaternion facing)
        {
            spot = Vector3.zero;
            facing = Quaternion.identity;

            // the lamps of this sector: their middle is where the sector "lives"
            Vector3 lampCenter = Vector3.zero;
            int lamps = 0;
            foreach (LightFlicker lamp in Object.FindObjectsByType<LightFlicker>(FindObjectsInactive.Include))
            {
                if (!sector.Contains(lamp.transform.position)) continue;
                lampCenter += lamp.transform.position;
                lamps++;
            }

            Vector3 target = lamps > 0 ? lampCenter / lamps : sector.Center;
            float best = float.MaxValue;
            bool found = false;
            for (float x = -sector.Size.x * 0.45f; x <= sector.Size.x * 0.45f; x += 3.0f)
            {
                for (float z = -sector.Size.z * 0.45f; z <= sector.Size.z * 0.45f; z += 3.0f)
                {
                    Vector3 candidate = new Vector3(sector.Center.x + x, 40.0f, sector.Center.z + z);
                    if (!Physics.Raycast(candidate, Vector3.down, out RaycastHit hit, 60.0f, 1, QueryTriggerInteraction.Ignore)) continue;
                    bool ground = hit.collider is TerrainCollider || hit.collider.name.StartsWith("Tile");
                    if (!ground || hit.point.y > 0.5f || hit.normal.y < 0.97f) continue;
                    // room for the shed and a walkway around it
                    if (Physics.CheckBox(hit.point + Vector3.up * 1.8f, new Vector3(4.5f, 1.5f, 4.5f), Quaternion.identity, 1, QueryTriggerInteraction.Ignore)) continue;

                    bool crowded = false;
                    foreach (Vector3 other in used) crowded |= (other - hit.point).sqrMagnitude < 8.0f * 8.0f;
                    if (crowded) continue;

                    float score = Vector3.Distance(new Vector3(hit.point.x, 0, hit.point.z), new Vector3(target.x, 0, target.z));
                    if (score < best)
                    {
                        best = score;
                        spot = hit.point;
                        found = true;
                    }
                }
            }

            if (!found) return false;
            used.Add(spot);
            // the door faces the middle of the base
            Vector3 toCenter = MilitaryBase.Center - spot;
            toCenter.y = 0.0f;
            facing = Quaternion.LookRotation(toCenter.sqrMagnitude > 0.1f ? toCenter.normalized : Vector3.forward);
            return true;
        }

        // a small concrete hut, 4.4 m square, with a metal door on its front (+Z)
        private static Transform BuildShed(Transform parent, string name, Vector3 position, Quaternion rotation,
            Material concrete, Material doorMetal, Material warning, bool electronic)
        {
            var shed = new GameObject(name).transform;
            shed.SetParent(parent, false);
            shed.SetPositionAndRotation(position, rotation);

            const float size = 4.4f;
            const float wall = 0.2f;
            const float height = 2.8f;
            const float doorWidth = 1.3f;
            const float doorHeight = 2.2f;

            Block(shed, "ShedFloor", new Vector3(0, 0.075f, 0), new Vector3(size, 0.15f, size), concrete);
            Block(shed, "ShedRoof", new Vector3(0, height + 0.075f, 0), new Vector3(size + 0.3f, 0.15f, size + 0.3f), concrete);
            Block(shed, "ShedWallBack", new Vector3(0, height * 0.5f, -size * 0.5f + wall * 0.5f), new Vector3(size, height, wall), concrete);
            Block(shed, "ShedWallLeft", new Vector3(-size * 0.5f + wall * 0.5f, height * 0.5f, 0), new Vector3(wall, height, size), concrete);
            Block(shed, "ShedWallRight", new Vector3(size * 0.5f - wall * 0.5f, height * 0.5f, 0), new Vector3(wall, height, size), concrete);
            float side = (size - doorWidth) * 0.5f;
            Block(shed, "ShedWallFrontL", new Vector3(-(doorWidth + side) * 0.5f, height * 0.5f, size * 0.5f - wall * 0.5f), new Vector3(side, height, wall), concrete);
            Block(shed, "ShedWallFrontR", new Vector3((doorWidth + side) * 0.5f, height * 0.5f, size * 0.5f - wall * 0.5f), new Vector3(side, height, wall), concrete);
            Block(shed, "ShedLintel", new Vector3(0, (doorHeight + height) * 0.5f, size * 0.5f - wall * 0.5f), new Vector3(doorWidth, height - doorHeight, wall), concrete);
            // a yellow band over the door so the building reads at night
            Block(shed, "ShedSign", new Vector3(0, doorHeight + 0.3f, size * 0.5f + 0.02f), new Vector3(doorWidth + 0.6f, 0.25f, 0.04f), warning, false);

            // the door: a root in the doorway, a hinge on the left edge, the leaf on the hinge
            var doorRoot = new GameObject(electronic ? "SecurityDoor" : "Door").transform;
            doorRoot.SetParent(shed, false);
            doorRoot.localPosition = new Vector3(0, 0.15f, size * 0.5f - wall * 0.5f);
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(doorRoot, false);
            hinge.localPosition = new Vector3(-doorWidth * 0.5f, 0, 0);
            Block(hinge, "DoorLeaf", new Vector3(doorWidth * 0.5f, (doorHeight - 0.15f) * 0.5f, 0), new Vector3(doorWidth - 0.04f, doorHeight - 0.17f, 0.08f), doorMetal);
            var door = doorRoot.gameObject.AddComponent<InteractiveDoor>();
            door.Leaf = hinge;
            // opens outwards
            door.OpenAngle = -100.0f;
            door.RequiresPower = electronic;
            door.DisplayName = electronic ? "porta do arsenal" : "porta";
            door.MaxHealth = electronic ? 300.0f : 150.0f;
            door.OpenSound = HorrorClip("Door_Open");
            door.CloseSound = HorrorClip("Door_Close");
            door.LockedSound = HorrorClip("Door_Locked");
            door.HitSounds = HorrorClips("Door_Hit");
            door.BreakSound = HorrorClip("Door_Break");

            // a lamp inside, fed by the grid like every other lamp
            var lampObject = new GameObject("ShedLight");
            lampObject.transform.SetParent(shed, false);
            lampObject.transform.localPosition = new Vector3(0, height - 0.25f, 0);
            lampObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var light = lampObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 140.0f;
            light.range = 5.0f;
            light.intensity = 6.0f;
            light.color = new Color(1.0f, 0.82f, 0.6f);
            light.shadows = LightShadows.None;
            var flicker = lampObject.AddComponent<LightFlicker>();
            flicker.Flicker = 0.15f;
            flicker.BlackoutChance = 0.05f;

            return shed;
        }

        private static GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, bool collider = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(block.GetComponent<Collider>());
            return block;
        }

        // a diesel generator from boxes and cylinders: body, skids, control panel, exhaust
        private static PowerGenerator BuildGenerator(GameObject root, Material body, Material metal, Material status)
        {
            var bodyRoot = new GameObject("GeneratorBody").transform;
            bodyRoot.SetParent(root.transform, false);
            Block(bodyRoot, "GeneratorSkidL", new Vector3(-0.45f, 0.06f, 0), new Vector3(0.12f, 0.12f, 1.7f), metal);
            Block(bodyRoot, "GeneratorSkidR", new Vector3(0.45f, 0.06f, 0), new Vector3(0.12f, 0.12f, 1.7f), metal);
            Block(bodyRoot, "GeneratorHousing", new Vector3(0, 0.62f, 0), new Vector3(1.1f, 1.0f, 1.6f), body);
            Block(bodyRoot, "GeneratorPanel", new Vector3(0, 0.75f, 0.81f), new Vector3(0.7f, 0.45f, 0.04f), metal, false);
            GameObject lamp = Block(bodyRoot, "GeneratorStatusLamp", new Vector3(0.22f, 0.88f, 0.84f), new Vector3(0.08f, 0.08f, 0.04f), status, false);
            GameObject exhaust = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            exhaust.name = "GeneratorExhaust";
            exhaust.transform.SetParent(bodyRoot, false);
            exhaust.transform.localPosition = new Vector3(0.35f, 1.35f, -0.5f);
            exhaust.transform.localScale = new Vector3(0.1f, 0.25f, 0.1f);
            exhaust.GetComponent<MeshRenderer>().sharedMaterial = metal;
            Object.DestroyImmediate(exhaust.GetComponent<Collider>());

            var statusLight = new GameObject("StatusLight").AddComponent<Light>();
            statusLight.transform.SetParent(bodyRoot, false);
            statusLight.transform.localPosition = new Vector3(0.22f, 0.88f, 1.0f);
            statusLight.type = LightType.Point;
            statusLight.range = 2.5f;
            statusLight.intensity = 1.5f;
            statusLight.shadows = LightShadows.None;

            var generator = root.AddComponent<PowerGenerator>();
            generator.Body = bodyRoot;
            generator.Exhaust = exhaust.transform;
            generator.StatusLamp = lamp.GetComponent<Renderer>();
            generator.StatusLight = statusLight;
            generator.CrankSound = HorrorClip("Generator_Crank");
            generator.RunningLoop = HorrorClip("Generator_Loop");
            generator.FailSound = HorrorClip("Generator_Fail");
            return generator;
        }
    }
}
