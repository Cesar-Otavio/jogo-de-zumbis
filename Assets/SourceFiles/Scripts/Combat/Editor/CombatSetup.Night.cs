using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Combat.EditorTools
{
    /* Night horror pass on the military base: fixed collisions, night lighting with lamps and fog,
     * post processing, the survivor as player character with a wardrobe, chests and loot, and audio.
     */
    public static partial class CombatSetup
    {
        private const string EnvironmentFolder = Root + "/Environment";

        [MenuItem("Tools/Combat/Setup Night, Loot and Survivor")]
        public static void SetupNightUpdate()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != BaseCombatScene)
            {
                EditorSceneManager.SaveOpenScenes();
                scene = EditorSceneManager.OpenScene(BaseCombatScene, OpenSceneMode.Single);
            }

            FixMapColliders();
            CreateWeapons();
            SetupNight();
            SetupLootAssets();
            SetupSurvivorPlayer();
            PlaceLoot();
            WireGameAudio();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- collisions

        public static int FixMapColliders()
        {
            int fixedCount = 0;
            foreach (MeshCollider meshCollider in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include))
            {
                if (meshCollider.sharedMesh != null) continue;
                var filter = meshCollider.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                meshCollider.sharedMesh = filter.sharedMesh;
                EditorUtility.SetDirty(meshCollider);
                fixedCount++;
            }

            // anything solid in the level that still has no collider gets one
            foreach (string rootName in new[] { "Props", "Buildings", "Fence" })
            {
                GameObject root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || filter.GetComponentInParent<Collider>() != null || filter.GetComponent<Collider>() != null) continue;
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    fixedCount++;
                }
            }

            Debug.Log($"Map colliders fixed: {fixedCount}");
            return fixedCount;
        }

        // ---------------------------------------------------------------- night

        private static void SetupNight()
        {
            EnsureFolder(EnvironmentFolder);

            // the demo's baked daylight would light the night scene
            Lightmapping.lightingDataAsset = null;
            LightmapSettings.lightmaps = new LightmapData[0];

            string skyPath = EnvironmentFolder + "/NightSky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }

            sky.SetFloat("_SunDisk", 1.0f);
            sky.SetFloat("_SunSize", 0.025f);
            sky.SetFloat("_SunSizeConvergence", 8.0f);
            sky.SetFloat("_AtmosphereThickness", 0.35f);
            sky.SetColor("_SkyTint", new Color(0.12f, 0.15f, 0.24f));
            sky.SetColor("_GroundColor", new Color(0.02f, 0.02f, 0.025f));
            sky.SetFloat("_Exposure", 0.1f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.18f, 0.26f);
            RenderSettings.ambientEquatorColor = new Color(0.09f, 0.1f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.035f, 0.045f);
            RenderSettings.reflectionIntensity = 0.2f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.08f);

            // cold moonlight
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type != LightType.Directional) continue;
                light.color = new Color(0.5f, 0.58f, 0.82f);
                light.intensity = 0.5f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.75f;
                light.transform.rotation = Quaternion.Euler(28.0f, 160.0f, 0.0f);
                RenderSettings.sun = light;
                EditorUtility.SetDirty(light);
            }

            SetupLampLights();
            SetupPostProcessing();
            UseForwardPlus();
        }

        // Forward allows 8 lights per object, and the terrain is one object: Forward+ lets every lamp light the ground
        private static void UseForwardPlus()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            var serialized = new SerializedObject(pipeline);
            SerializedProperty renderers = serialized.FindProperty("m_RendererDataList");
            for (int i = 0; i < renderers.arraySize; i++)
            {
                if (renderers.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData data && data.renderingMode != RenderingMode.ForwardPlus)
                {
                    data.renderingMode = RenderingMode.ForwardPlus;
                    EditorUtility.SetDirty(data);
                    Debug.Log($"Renderer {data.name} switched to Forward+");
                }
            }
        }

        private static void SetupLampLights()
        {
            int index = 0;
            foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                if (!renderer.name.StartsWith("Lamp")) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                // re-runs replace the light made last time
                Transform previous = renderer.transform.Find("LampLight");
                if (previous != null) Object.DestroyImmediate(previous.gameObject);

                Material[] materials = renderer.sharedMaterials;
                int bulb = -1;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].name.Contains("Light")) bulb = i;
                }

                // batched lamp meshes carry hundreds of submeshes, so only trust the bulb submesh on a plain mesh
                Vector3 bulbPosition = renderer.bounds.center + Vector3.up * renderer.bounds.extents.y;
                if (bulb >= 0 && bulb < filter.sharedMesh.subMeshCount && filter.sharedMesh.subMeshCount <= materials.Length)
                {
                    Bounds local = filter.sharedMesh.GetSubMesh(bulb).bounds;
                    bulbPosition = renderer.transform.TransformPoint(local.center);
                }

                var lightObject = new GameObject("LampLight");
                lightObject.transform.SetParent(renderer.transform, true);
                lightObject.transform.position = bulbPosition + Vector3.down * 0.2f;
                lightObject.transform.rotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1.0f, 0.74f, 0.45f);
                light.intensity = 70.0f;
                light.range = 26.0f;
                light.spotAngle = 115.0f;
                light.innerSpotAngle = 55.0f;
                light.shadows = LightShadows.None;

                // a third of the lamps flicker, one in seven is burnt out
                var flicker = lightObject.AddComponent<LightFlicker>();
                flicker.Bulb = renderer;
                flicker.BulbMaterialIndex = bulb;
                flicker.Dead = index % 7 == 3;
                flicker.Flicker = index % 3 == 0 ? 0.6f : 0.05f;
                flicker.BlackoutChance = index % 3 == 0 ? 0.35f : 0.0f;
                if (bulb >= 0 && materials[bulb].HasProperty("_EmissionColor")) flicker.BulbEmission = materials[bulb].GetColor("_EmissionColor");
                index++;
            }

            Debug.Log($"Lamp lights: {index}");
        }

        private static void SetupPostProcessing()
        {
            string path = EnvironmentFolder + "/NightProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.45f);
            vignette.smoothness.Override(0.55f);
            vignette.color.Override(Color.black);

            FilmGrain grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.45f);
            grain.response.Override(0.8f);

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.6f);
            color.contrast.Override(18.0f);
            color.saturation.Override(-35.0f);
            color.colorFilter.Override(new Color(0.86f, 0.92f, 1.0f));

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.9f);
            bloom.threshold.Override(0.85f);
            bloom.scatter.Override(0.65f);

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            ChromaticAberration aberration = profile.Add<ChromaticAberration>(true);
            aberration.intensity.Override(0.12f);

            foreach (VolumeComponent component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);

            GameObject volumeObject = FindOrCreate("NightVolume");
            var volume = Ensure<Volume>(volumeObject);
            volume.isGlobal = true;
            volume.priority = 10.0f;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
        }

        // ---------------------------------------------------------------- loot

        private static void SetupLootAssets()
        {
            CombatEffects fx = Object.FindAnyObjectByType<CombatEffects>();
            if (fx == null) return;

            EnsureFolder(Root + "/Effects");
            fx.CrateMaterial = LitMaterial("Loot_Crate", new Color(0.22f, 0.25f, 0.14f), 0.0f, 0.3f, Color.black);
            fx.CrateMetalMaterial = LitMaterial("Loot_Metal", new Color(0.15f, 0.15f, 0.16f), 0.8f, 0.5f, Color.black);
            fx.AmmoBoxMaterial = LitMaterial("Loot_AmmoBox", new Color(0.2f, 0.23f, 0.12f), 0.2f, 0.35f, Color.black);
            fx.MedkitMaterial = LitMaterial("Loot_Medkit", new Color(0.85f, 0.84f, 0.8f), 0.0f, 0.4f, Color.black);
            fx.CrossMaterial = LitMaterial("Loot_Cross", new Color(0.75f, 0.05f, 0.05f), 0.0f, 0.3f, new Color(1.6f, 0.08f, 0.08f));
            fx.CrateOpenSound = Clip("Crate_Open");
            fx.AmmoPickupSound = Clip("Pickup_Ammo");
            fx.HealthPickupSound = Clip("Pickup_Health");
            EditorUtility.SetDirty(fx);
        }

        private static Material LitMaterial(string name, Color color, float metallic, float smoothness, Color emission)
        {
            string path = $"{Root}/Effects/{name}.mat";
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
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void PlaceLoot()
        {
            WeaponData[] weapons = CreateWeapons();
            // the colliders fixed above must reach the physics scene before the placement raycasts
            Physics.SyncTransforms();
            GameObject root = FindOrCreate("Loot");
            ClearChildren(root.transform);
            var used = new List<Vector3>();
            var random = new System.Random(1234);

            // pistol, uzi, ak, m4, shotgun, sniper: the strong guns are rarer in chests
            float[] weights = { 1.0f, 3.0f, 3.0f, 3.0f, 2.0f, 1.0f };

            // weapons, chests and ammo on top of containers: climb up to get them
            var tops = new List<Vector3>();
            foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>())
            {
                if (!renderer.name.StartsWith("Container")) continue;
                Bounds bounds = renderer.bounds;
                if (!InCombatArea(bounds.center)) continue;
                Vector3 top = new Vector3(bounds.center.x, bounds.max.y + 2.0f, bounds.center.z);
                if (Physics.Raycast(top, Vector3.down, out RaycastHit hit, 3.0f, 1, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.8f)
                {
                    tops.Add(hit.point);
                }
            }

            for (int i = 0; i < tops.Count; i++)
            {
                Vector3 point = tops[i];
                used.Add(point);
                switch (i % 3)
                {
                    case 0:
                        AddWeaponSpawn(root.transform, weapons[1 + random.Next(weapons.Length - 1)], point);
                        break;
                    case 1:
                        AddCrate(root.transform, weapons, weights, point, random);
                        break;
                    default:
                        AddConsumable(root.transform, random.NextDouble() < 0.5 ? ConsumablePickup.Kind.Ammo : ConsumablePickup.Kind.Health, point);
                        break;
                }
            }

            // scattered over the ground in open spots
            for (int i = 0; i < 12; i++)
            {
                if (TryGroundSpot(random, used, out Vector3 point)) AddCrate(root.transform, weapons, weights, point, random);
            }

            for (int i = 0; i < 20; i++)
            {
                if (TryGroundSpot(random, used, out Vector3 point)) AddConsumable(root.transform, ConsumablePickup.Kind.Ammo, point);
            }

            for (int i = 0; i < 12; i++)
            {
                if (TryGroundSpot(random, used, out Vector3 point)) AddConsumable(root.transform, ConsumablePickup.Kind.Health, point);
            }

            for (int i = 0; i < 8; i++)
            {
                if (TryGroundSpot(random, used, out Vector3 point)) AddWeaponSpawn(root.transform, weapons[random.Next(weapons.Length)], point);
            }

            Debug.Log($"Loot placed: {root.transform.childCount} ({tops.Count} on top of containers)");
        }

        private static bool InCombatArea(Vector3 point)
        {
            return Mathf.Abs(point.x - MilitaryBase.Center.x) < MilitaryBase.NavMeshSize.x * 0.45f &&
                   Mathf.Abs(point.z - MilitaryBase.Center.z) < MilitaryBase.NavMeshSize.z * 0.45f;
        }

        private static bool TryGroundSpot(System.Random random, List<Vector3> used, out Vector3 point)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float x = MilitaryBase.Center.x + ((float)random.NextDouble() - 0.5f) * MilitaryBase.NavMeshSize.x * 0.85f;
                float z = MilitaryBase.Center.z + ((float)random.NextDouble() - 0.5f) * MilitaryBase.NavMeshSize.z * 0.85f;
                if (!Physics.Raycast(new Vector3(x, 40.0f, z), Vector3.down, out RaycastHit hit, 60.0f, 1, QueryTriggerInteraction.Ignore)) continue;

                // only the open ground, not roofs or props
                bool ground = hit.collider is TerrainCollider || hit.collider.name.StartsWith("Tile");
                if (!ground || hit.point.y > 0.5f) continue;
                if (Physics.CheckSphere(hit.point + Vector3.up * 0.8f, 0.6f, 1, QueryTriggerInteraction.Ignore)) continue;

                bool crowded = false;
                foreach (Vector3 other in used) crowded |= (other - hit.point).sqrMagnitude < 36.0f;
                if (crowded) continue;

                point = hit.point;
                used.Add(point);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private static void AddWeaponSpawn(Transform parent, WeaponData weapon, Vector3 point)
        {
            var go = new GameObject("Weapon_" + weapon.DisplayName);
            go.transform.SetParent(parent, false);
            go.transform.position = point;
            var spawn = go.AddComponent<WeaponSpawnPoint>();
            spawn.Weapon = weapon;
            spawn.RespawnDelay = 40.0f;
        }

        private static void AddCrate(Transform parent, WeaponData[] weapons, float[] weights, Vector3 point, System.Random random)
        {
            var go = new GameObject("Chest");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(point, Quaternion.Euler(0.0f, (float)random.NextDouble() * 360.0f, 0.0f));
            var crate = go.AddComponent<LootCrate>();
            crate.LootTable = weapons;
            crate.Weights = weights;
        }

        private static void AddConsumable(Transform parent, ConsumablePickup.Kind kind, Vector3 point)
        {
            var go = new GameObject(kind == ConsumablePickup.Kind.Ammo ? "AmmoBox" : "Medkit");
            go.transform.SetParent(parent, false);
            go.transform.position = point;
            var pickup = go.AddComponent<ConsumablePickup>();
            pickup.Type = kind;
            pickup.RespawnDelay = kind == ConsumablePickup.Kind.Ammo ? 40.0f : 60.0f;
        }

        // ---------------------------------------------------------------- survivor

        private const string SurvivorSource = "Assets/Survivalist/Prefab/Survivalist (4).prefab";

        private static void SetupSurvivorPlayer()
        {
            const string path = "Assets/Prefabs/PlayerRobot.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            var controller = root.GetComponentInChildren<StarterAssets.ThirdPersonController>(true);
            GameObject robot = controller.gameObject;
            var animator = robot.GetComponent<Animator>();

            // swap the robot's body for the survivor's
            Transform oldGeometry = robot.transform.Find("Geometry");
            if (oldGeometry != null) Object.DestroyImmediate(oldGeometry.gameObject);
            Transform oldSkeleton = robot.transform.Find("Skeleton");
            if (oldSkeleton != null) Object.DestroyImmediate(oldSkeleton.gameObject);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SurvivorSource);
            var survivor = (GameObject)Object.Instantiate(source);
            var geometry = new GameObject("Geometry").transform;
            geometry.SetParent(robot.transform, false);
            var children = new List<Transform>();
            foreach (Transform child in survivor.transform) children.Add(child);
            foreach (Transform child in children)
            {
                if (child.name == "FPS_HANDS")
                {
                    Object.DestroyImmediate(child.gameObject);
                    continue;
                }

                child.SetParent(geometry, false);
            }

            animator.avatar = source.GetComponent<Animator>().avatar;
            animator.applyRootMotion = false;
            Object.DestroyImmediate(survivor);
            foreach (Transform t in geometry.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CombatLayers.Player;
            animator.Rebind();

            var characterController = robot.GetComponent<CharacterController>();
            characterController.height = 1.85f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0.0f, 0.95f, 0.0f);
            var trigger = robot.GetComponent<CapsuleCollider>();
            if (trigger != null)
            {
                trigger.height = 1.85f;
                trigger.center = new Vector3(0.0f, 0.95f, 0.0f);
            }

            controller.MoveSpeed = 2.4f;
            controller.SprintSpeed = 7.2f;
            controller.RollVisual = geometry;
            controller.RollPivotHeight = 0.6f;
            controller.CinemachineCameraTarget.transform.localPosition = new Vector3(0.0f, 1.55f, 0.0f);

            var holder = robot.GetComponent<WeaponHolder>();
            holder.ShoulderHeight = 1.47f;
            holder.ShoulderSide = 0.16f;

            var climbing = robot.GetComponent<StarterAssets.ClimbingSystem>();
            if (climbing != null)
            {
                // measured: the survivor's hands are 1.89 m up in the hang pose
                climbing.HangHeight = 1.92f;
                climbing.MaxLedgeHeight = 2.2f;
            }

            var combat = robot.GetComponent<PlayerCombat>();
            combat.HurtSounds = new[] { Clip("Soldier_Hurt_1"), Clip("Soldier_Hurt_2"), Clip("Soldier_Hurt_3") };
            combat.DeathSound = Clip("Soldier_Death");
            combat.FlashlightSound = Clip("Flashlight_Click");
            combat.SightOffsetFromHead = new Vector3(0.0f, 0.12f, 0.34f);

            Health health = robot.GetComponent<Health>();
            var capsuleHitbox = robot.GetComponent<Hitbox>();
            capsuleHitbox.Owner = health;
            capsuleHitbox.Flesh = true;
            BuildRagdoll(animator, health, 75.0f, true, 2.0f);

            var outfit = Ensure<PlayerOutfit>(robot);
            var palette = new List<Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Survivalist/Materials" }))
            {
                palette.Add(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            outfit.Palette = palette.ToArray();

            // the game camera renders the night grade
            foreach (UniversalAdditionalCameraData data in root.GetComponentsInChildren<UniversalAdditionalCameraData>(true))
            {
                data.renderPostProcessing = true;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ---------------------------------------------------------------- audio

        private static void WireGameAudio()
        {
            string musicPath = Sounds + "Music_Horror_Loop.wav";
            var importer = (AudioImporter)AssetImporter.GetAtPath(musicPath);
            if (importer != null)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }

            GameFlow flow = Object.FindAnyObjectByType<GameFlow>();
            if (flow == null) return;
            flow.GameMusic = Clip("Music_Horror_Loop");
            flow.MenuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SourceFiles/SoundFX/Music_Suspenseful.ogg");
            flow.MusicVolume = 0.6f;
            flow.AmbientSounds = new[]
            {
                Clip("Zombie_Groan_1"), Clip("Zombie_Groan_2"), Clip("Zombie_Groan_3"), Clip("Zombie_Groan_4"),
                Clip("Ambient_MetalGroan"), Clip("Ambient_DistantCry"), Clip("Ambient_Thumps")
            };
            EditorUtility.SetDirty(flow);
        }
    }
}
