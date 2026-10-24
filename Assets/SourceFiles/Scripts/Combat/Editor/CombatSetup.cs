using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Combat.EditorTools
{
    /* One-click setup of the combat sandbox: layers, URP materials, effect assets,
     * weapon data, ragdolls, enemy prefabs, the zombie animator and the scene objects.
     * Safe to run again: existing assets are updated in place.
     */
    public static partial class CombatSetup
    {
        private const string Root = "Assets/SourceFiles/Combat";
        private const string WeaponModels = "Assets/Low Poly Weapons VOL.1/Prefabs/";
        private const string Sounds = "Assets/SourceFiles/SoundFX/Weapons/";

        [MenuItem("Tools/Combat/Setup")]
        public static void RunAll()
        {
            SetupLayers();
            ConvertMaterials();
            CreateEffectAssets();
            CreateWeapons();
            CreateZombieController();
            CreateEnemyPrefabs();
            SetupPlayer();
            SetupScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Combat setup finished");
        }

        // ---------------------------------------------------------------- layers

        public static void SetupLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SetLayerName(layers, CombatLayers.Player, "Player");
            SetLayerName(layers, CombatLayers.Enemy, "Enemy");
            SetLayerName(layers, CombatLayers.Hitbox, "Hitbox");
            SetLayerName(layers, CombatLayers.Debris, "Debris");
            tagManager.ApplyModifiedProperties();

            // casings, magazines and dropped guns only bounce off the level
            Physics.IgnoreLayerCollision(CombatLayers.Debris, CombatLayers.Player, true);
            Physics.IgnoreLayerCollision(CombatLayers.Debris, CombatLayers.Enemy, true);
            Physics.IgnoreLayerCollision(CombatLayers.Debris, CombatLayers.Hitbox, true);
            Physics.IgnoreLayerCollision(CombatLayers.Enemy, CombatLayers.Enemy, true);
        }

        private static void SetLayerName(SerializedProperty layers, int index, string name)
        {
            SerializedProperty element = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(element.stringValue)) element.stringValue = name;
        }

        // ---------------------------------------------------------------- materials

        public static void ConvertMaterials()
        {
            Material gun = AssetDatabase.LoadAssetAtPath<Material>("Assets/Low Poly Weapons VOL.1/Gun_MAT.mat");
            ToUrpLit(gun, AssetDatabase.LoadAssetAtPath<Texture>("Assets/Low Poly Weapons VOL.1/Textures_Guns.png"), 0.45f);

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Survivalist/Materials" }))
            {
                ToUrpLit(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)), null, 0.3f);
            }

            ToUrpLit(AssetDatabase.LoadAssetAtPath<Material>("Assets/Kevin Iglesias/Zombie Animations/Materials/ZombieMaterial.mat"), null, 0.15f);
            ToUrpLit(AssetDatabase.LoadAssetAtPath<Material>("Assets/Kevin Iglesias/Zombie Animations/Materials/ZombieFloor.mat"), null, 0.1f);
        }

        private static void ToUrpLit(Material material, Texture baseMap, float smoothness)
        {
            if (material == null || material.shader.name == "Universal Render Pipeline/Lit") return;

            Texture albedo = baseMap != null ? baseMap : Get(material, "_MainTex");
            Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            Texture normal = Get(material, "_BumpMap");
            Texture metallic = Get(material, "_MetallicGlossMap");
            Texture occlusion = Get(material, "_OcclusionMap");

            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            if (metallic != null)
            {
                material.SetTexture("_MetallicGlossMap", metallic);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (occlusion != null)
            {
                material.SetTexture("_OcclusionMap", occlusion);
                material.EnableKeyword("_OCCLUSIONMAP");
            }

            EditorUtility.SetDirty(material);
        }

        private static Texture Get(Material material, string property)
        {
            return material.HasProperty(property) ? material.GetTexture(property) : null;
        }

        // ---------------------------------------------------------------- effects

        public static void CreateEffectAssets()
        {
            EnsureFolder(Root + "/Effects");

            Texture2D dot = TextureAsset("FX_SoftDot", 64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1.0f - r), 2.0f));
            });
            Texture2D flash = TextureAsset("FX_Flash", 128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float angle = Mathf.Atan2(v, u);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3.0f)), 8.0f) * Mathf.Clamp01(1.0f - r);
                float core = Mathf.Pow(Mathf.Clamp01(1.0f - r * 1.6f), 1.5f);
                float a = Mathf.Clamp01(core + spikes * 0.8f);
                return new Color(1.0f, Mathf.Lerp(0.6f, 1.0f, core), Mathf.Lerp(0.2f, 0.9f, core), a);
            });
            Texture2D tracer = TextureAsset("FX_Tracer", 64, (u, v) => new Color(1, 1, 1, Mathf.Exp(-v * v * 12.0f)));
            Texture2D hole = TextureAsset("FX_BulletHole", 64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float angle = Mathf.Atan2(v, u);
                float cracks = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 5.0f + Mathf.Sin(angle * 13.0f))), 30.0f) * Mathf.Clamp01(1.0f - r) * 0.7f;
                float center = Mathf.Clamp01((0.35f - r) * 12.0f);
                float rim = Mathf.Clamp01(1.0f - Mathf.Abs(r - 0.42f) * 10.0f) * 0.5f;
                float a = Mathf.Clamp01(center + rim + cracks);
                return new Color(0.05f, 0.04f, 0.035f, a);
            });

            FxMaterial("FX_Spark", "Universal Render Pipeline/Particles/Unlit", dot, Color.white, true);
            FxMaterial("FX_Flash", "Universal Render Pipeline/Particles/Unlit", flash, Color.white, true);
            FxMaterial("FX_Tracer", "Universal Render Pipeline/Particles/Unlit", tracer, Color.white, true);
            FxMaterial("FX_Smoke", "Universal Render Pipeline/Particles/Unlit", dot, Color.white, false);
            FxMaterial("FX_Decal", "Universal Render Pipeline/Unlit", hole, Color.white, false);

            string brassPath = Root + "/Effects/FX_Brass.mat";
            Material brass = AssetDatabase.LoadAssetAtPath<Material>(brassPath);
            if (brass == null)
            {
                brass = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(brass, brassPath);
            }

            brass.SetColor("_BaseColor", new Color(0.85f, 0.62f, 0.25f));
            brass.SetFloat("_Metallic", 0.9f);
            brass.SetFloat("_Smoothness", 0.7f);
            EditorUtility.SetDirty(brass);
        }

        private static Texture2D TextureAsset(string name, int size, System.Func<float, float, Color> pixel)
        {
            string path = $"{Root}/Effects/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2.0f - 1.0f;
                    float v = (y + 0.5f) / size * 2.0f - 1.0f;
                    texture.SetPixel(x, y, pixel(u, v));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void FxMaterial(string name, string shader, Texture texture, Color color, bool additive)
        {
            string path = $"{Root}/Effects/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find(shader);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", additive ? 2.0f : 0.0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_Cull", 0.0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
        }

        // ---------------------------------------------------------------- weapons

        public static WeaponData[] CreateWeapons()
        {
            EnsureFolder(Root + "/Weapons");

            WeaponData pistol = Weapon("M1911", "M1911", HoldStyle.Pistol, FireMode.Semi, w =>
            {
                w.FireInterval = 0.14f; w.Damage = 28; w.HitForce = 3;
                w.HipSpread = 1.4f; w.AimSpread = 0.35f; w.MoveSpread = 1.0f; w.SpreadPerShot = 0.8f; w.MaxBloom = 3;
                w.MagazineSize = 12; w.StartReserve = 48; w.ReloadTime = 1.5f;
                w.KickBack = 0.05f; w.KickPitch = 5.85f; w.KickYaw = 1.75f; w.CameraPitchKick = 0.6f; w.CameraYawKick = 0.15f;
                w.AimFov = 32; w.MagazinePart = "Magazin"; w.CyclingPart = "Slide"; w.CyclingTravel = new Vector3(0, 0, 0.035f);
                w.ShotSounds = Clips("Shot_Pistol"); w.CycleSound = Clip("Mech_SlideRack");
            });
            WeaponData uzi = Weapon("Uzi", "Uzi", HoldStyle.Rifle, FireMode.Auto, w =>
            {
                w.FireInterval = 0.065f; w.Damage = 14; w.HitForce = 2;
                w.HipSpread = 3.2f; w.AimSpread = 1.6f; w.MoveSpread = 1.2f; w.SpreadPerShot = 0.25f; w.MaxBloom = 3;
                w.MagazineSize = 32; w.StartReserve = 128; w.ReloadTime = 1.7f;
                w.KickBack = 0.035f; w.KickPitch = 2.27f; w.KickYaw = 1.75f; w.CameraPitchKick = 0.23f; w.CameraYawKick = 0.17f;
                w.AimFov = 32; w.MagazinePart = "Magazin"; w.CyclingPart = "Bolt"; w.CyclingTravel = new Vector3(0, 0, 0.04f);
                w.ShotSounds = Clips("Shot_Uzi"); w.ChargeSound = Clip("Mech_BoltRack");
            });
            WeaponData ak = Weapon("AK74", "AK-74", HoldStyle.Rifle, FireMode.Auto, w =>
            {
                w.FireInterval = 0.1f; w.Damage = 26; w.HitForce = 4;
                w.HipSpread = 2.6f; w.AimSpread = 0.45f; w.MoveSpread = 1.6f; w.SpreadPerShot = 0.35f; w.MaxBloom = 3.5f;
                w.MagazineSize = 30; w.StartReserve = 90; w.ReloadTime = 2.3f;
                w.KickBack = 0.07f; w.KickPitch = 3.9f; w.KickYaw = 1.75f; w.CameraPitchKick = 0.45f; w.CameraYawKick = 0.23f;
                w.AimFov = 28; w.MagazinePart = "Mag"; w.CyclingPart = "Bolt"; w.CyclingTravel = new Vector3(0, 0, 0.06f);
                w.ShotSounds = Clips("Shot_AK74"); w.ChargeSound = Clip("Mech_BoltRack");
            });
            WeaponData m4 = Weapon("M4_8", "M4", HoldStyle.Rifle, FireMode.Auto, w =>
            {
                w.FireInterval = 0.08f; w.Damage = 22; w.HitForce = 3.5f;
                w.HipSpread = 2.2f; w.AimSpread = 0.35f; w.MoveSpread = 1.4f; w.SpreadPerShot = 0.28f; w.MaxBloom = 3;
                w.MagazineSize = 30; w.StartReserve = 90; w.ReloadTime = 2.0f;
                w.KickBack = 0.055f; w.KickPitch = 2.93f; w.KickYaw = 1.26f; w.CameraPitchKick = 0.35f; w.CameraYawKick = 0.15f;
                w.AimFov = 26; w.MagazinePart = "Mag"; w.CyclingPart = "Bolt"; w.CyclingTravel = new Vector3(0, 0, 0.05f);
                w.ShotSounds = Clips("Shot_M4"); w.ChargeSound = Clip("Mech_BoltRack");
            });
            WeaponData shotgun = Weapon("Bennelli_M4", "Benelli M4", HoldStyle.Rifle, FireMode.Semi, w =>
            {
                w.FireInterval = 0.85f; w.Damage = 13; w.Pellets = 9; w.Range = 45; w.HitForce = 9;
                w.HipSpread = 6.5f; w.AimSpread = 4.5f; w.MoveSpread = 1.0f; w.SpreadPerShot = 0.0f; w.MaxBloom = 0;
                w.MagazineSize = 7; w.StartReserve = 28; w.Reload = ReloadStyle.SingleShell; w.ShellInsertTime = 0.5f;
                w.KickBack = 0.12f; w.KickPitch = 9.1f; w.KickYaw = 2.1f; w.CameraPitchKick = 1.2f; w.CameraYawKick = 0.3f;
                w.AimFov = 32; w.CyclingPart = "Pump"; w.CyclingTravel = new Vector3(0, 0, 0.1f); w.ManualCycle = true;
                w.ShotSounds = Clips("Shot_Shotgun"); w.CycleSound = Clip("Mech_Pump"); w.ShellInsertSound = Clip("Mech_ShellInsert");
            });
            WeaponData sniper = Weapon("M107", "M107 Sniper", HoldStyle.Rifle, FireMode.Semi, w =>
            {
                w.FireInterval = 1.25f; w.Damage = 110; w.Range = 300; w.HitForce = 14;
                w.HipSpread = 5.0f; w.AimSpread = 0.0f; w.MoveSpread = 2.5f; w.SpreadPerShot = 0.0f; w.MaxBloom = 0;
                w.MagazineSize = 5; w.StartReserve = 15; w.ReloadTime = 2.8f;
                w.KickBack = 0.16f; w.KickPitch = 10.4f; w.KickYaw = 2.1f; w.CameraPitchKick = 1.75f; w.CameraYawKick = 0.4f;
                w.AimFov = 9; w.HasScope = true; w.MagazinePart = "Magazin"; w.CyclingPart = "Bolt";
                w.Attachment = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponModels + "SR_Scope_00.prefab");
                w.CyclingTravel = new Vector3(0, 0, 0.09f); w.ManualCycle = true;
                w.ShotSounds = Clips("Shot_Sniper"); w.CycleSound = Clip("Mech_BoltAction"); w.ChargeSound = Clip("Mech_BoltAction");
            });

            return new[] { pistol, uzi, ak, m4, shotgun, sniper };
        }

        private static WeaponData Weapon(string model, string displayName, HoldStyle hold, FireMode mode, System.Action<WeaponData> configure)
        {
            string path = $"{Root}/Weapons/Weapon_{model}.asset";
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.DisplayName = displayName;
            data.Model = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponModels + model + ".prefab");
            data.Hold = hold;
            data.Mode = mode;
            data.MagOutSound = Clip("Mech_MagOut");
            data.MagInSound = Clip("Mech_MagIn");
            data.DrySound = Clip("Mech_DryFire");
            configure(data);
            MeasureWeapon(data);
            EditorUtility.SetDirty(data);
            return data;
        }

        // finds the grip, support hand, muzzle and ejection port from the model itself
        private static void MeasureWeapon(WeaponData data)
        {
            GameObject model = Object.Instantiate(data.Model);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var vertices = new List<Vector3>();
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                foreach (Vector3 v in filter.sharedMesh.vertices) vertices.Add(filter.transform.TransformPoint(v));
            }

            Transform trigger = Find(model.transform, "Trigger");
            Vector3 triggerPos = trigger != null ? trigger.position : Vector3.zero;
            // the trigger's pivot can sit at the origin, use its mesh center when it has one
            if (trigger != null && trigger.GetComponent<Renderer>() != null) triggerPos = trigger.GetComponent<Renderer>().bounds.center;

            float minZ = float.MaxValue;
            foreach (Vector3 v in vertices) minZ = Mathf.Min(minZ, v.z);
            Vector3 muzzle = Vector3.zero;
            int count = 0;
            foreach (Vector3 v in vertices)
            {
                if (v.z > minZ + 0.015f) continue;
                muzzle += v;
                count++;
            }

            muzzle = count > 0 ? muzzle / count : new Vector3(0, 0, minZ);
            muzzle.z = minZ - 0.01f;
            data.MuzzlePoint = muzzle;

            // the hand wraps the pistol grip just behind and below the trigger
            data.GripPoint = triggerPos + new Vector3(0.0f, -0.045f, 0.05f);

            if (data.Hold == HoldStyle.Pistol)
            {
                data.SupportPoint = data.GripPoint;
            }
            else
            {
                // under the handguard, between the magazine and the muzzle
                Transform pump = Find(model.transform, data.CyclingPart);
                float z = data.ManualCycle && pump != null && data.Reload == ReloadStyle.SingleShell
                    ? pump.GetComponent<Renderer>().bounds.center.z
                    : Mathf.Lerp(triggerPos.z, minZ, data.Model.name == "Uzi" ? 0.45f : 0.42f);
                float lowest = float.MaxValue;
                foreach (Vector3 v in vertices)
                {
                    if (Mathf.Abs(v.z - z) < 0.025f) lowest = Mathf.Min(lowest, v.y);
                }

                data.SupportPoint = new Vector3(0.0f, (lowest == float.MaxValue ? triggerPos.y : lowest) + 0.005f, z);
            }

            Transform cycling = Find(model.transform, data.CyclingPart);
            Vector3 port = cycling != null && cycling.GetComponent<Renderer>() != null ? cycling.GetComponent<Renderer>().bounds.center : triggerPos + new Vector3(0, 0.05f, -0.03f);
            data.EjectPoint = new Vector3(-0.03f, port.y, Mathf.Min(port.z, triggerPos.z));

            float gripZ = data.GripPoint.z;
            if (data.Attachment == null)
            {
                // iron sights: look along the top of the gun, just in front of the grip
                float top = float.MinValue;
                foreach (Vector3 v in vertices)
                {
                    if (Mathf.Abs(v.x) < 0.015f && v.z > minZ + 0.02f && v.z < gripZ + 0.06f) top = Mathf.Max(top, v.y);
                }

                data.SightPoint = new Vector3(0.0f, (top == float.MinValue ? data.GripPoint.y + 0.1f : top) + 0.01f, gripZ - 0.03f);
                data.AttachmentOffset = Vector3.zero;
            }
            else
            {
                // scope: sit it on the rail above the receiver and look through its middle
                float mountZ = gripZ - 0.2f;
                float rail = float.MinValue;
                foreach (Vector3 v in vertices)
                {
                    if (Mathf.Abs(v.x) < 0.02f && Mathf.Abs(v.z - mountZ) < 0.08f) rail = Mathf.Max(rail, v.y);
                }

                GameObject scope = Object.Instantiate(data.Attachment);
                scope.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Bounds scopeBounds = default;
                bool first = true;
                foreach (Renderer r in scope.GetComponentsInChildren<Renderer>())
                {
                    if (first) scopeBounds = r.bounds;
                    else scopeBounds.Encapsulate(r.bounds);
                    first = false;
                }

                Object.DestroyImmediate(scope);
                Vector3 offset = new Vector3(0.0f, rail - scopeBounds.min.y, mountZ - scopeBounds.center.z);
                data.AttachmentOffset = offset;
                data.SightPoint = new Vector3(0.0f, offset.y + scopeBounds.center.y, offset.z + scopeBounds.max.z + 0.01f);
            }

            Object.DestroyImmediate(model);
        }

        private static AudioClip Clip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + name + ".wav");
        }

        private static AudioClip[] Clips(string prefix)
        {
            var clips = new List<AudioClip>();
            for (int i = 1; i <= 4; i++)
            {
                AudioClip clip = Clip($"{prefix}_{i}");
                if (clip != null) clips.Add(clip);
            }

            return clips.ToArray();
        }

        private static Transform Find(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name == name) return t;
            }

            return null;
        }

        // ---------------------------------------------------------------- zombie animator

        public static AnimatorController CreateZombieController()
        {
            EnsureFolder(Root + "/Enemies");
            string path = Root + "/Enemies/Zombie.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = "WalkSpeedScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1.0f });
            controller.AddParameter("CycleOffset", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idle = sm.AddState("Idle", new Vector3(250, 0, 0));
            idle.motion = ZombieClip("Idle01", "Zombie@Idle01");
            AnimatorState walk = sm.AddState("Walk", new Vector3(250, 120, 0));
            walk.motion = ZombieClip("Walk01", "Zombie@Walk01");
            walk.speedParameterActive = true;
            walk.speedParameter = "WalkSpeedScale";
            walk.cycleOffsetParameterActive = true;
            walk.cycleOffsetParameter = "CycleOffset";
            AnimatorState attack = sm.AddState("Attack", new Vector3(550, 0, 0));
            attack.motion = ZombieClip("Attack01", "Zombie@Attack01");
            AnimatorState hit = sm.AddState("Hit", new Vector3(550, 120, 0));
            hit.motion = ZombieClip("Damage01", "Zombie@Damage01");
            AnimatorState death = sm.AddState("Death", new Vector3(550, 240, 0));
            death.motion = ZombieClip("Death01_A", "Zombie@Death01_A");
            sm.defaultState = idle;

            Transition(idle, walk, 0.25f, t => t.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed"));
            Transition(walk, idle, 0.3f, t => t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"));
            AnyTransition(sm, attack, 0.15f, "Attack");
            AnyTransition(sm, hit, 0.1f, "Hit");
            AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            toDeath.canTransitionToSelf = false;
            toDeath.duration = 0.15f;
            ExitTo(attack, walk, 0.9f);
            ExitTo(hit, walk, 0.8f);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip ZombieClip(string file, string clipName)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath($"Assets/Kevin Iglesias/Zombie Animations/Animations/Zombie@{file}.fbx"))
            {
                if (o is AnimationClip clip && clip.name == clipName) return clip;
            }

            return null;
        }

        private static void Transition(AnimatorState from, AnimatorState to, float duration, System.Action<AnimatorStateTransition> conditions)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            conditions(t);
        }

        private static void AnyTransition(AnimatorStateMachine sm, AnimatorState to, float duration, string trigger)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(to);
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            t.canTransitionToSelf = false;
            t.duration = duration;
        }

        private static void ExitTo(AnimatorState from, AnimatorState to, float exitTime)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = 0.2f;
        }

        // ---------------------------------------------------------------- enemies

        public static void CreateEnemyPrefabs()
        {
            EnsureFolder("Assets/Prefabs/Enemies");
            WeaponData[] weapons = CreateWeapons();
            var loadout = new[] { weapons[1], weapons[2], weapons[3], weapons[0], weapons[2], weapons[3] };
            var locomotion = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/SourceFiles/StarterAssets/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller");

            for (int i = 1; i <= 4; i++)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Survivalist/Prefab/Survivalist ({i}).prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = $"Soldier_{i}";
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var animator = go.GetComponent<Animator>();
                animator.runtimeAnimatorController = locomotion;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var agent = go.AddComponent<NavMeshAgent>();
                agent.radius = 0.35f;
                agent.height = 1.9f;
                agent.acceleration = 14.0f;
                agent.angularSpeed = 0.0f;
                agent.stoppingDistance = 0.3f;
                agent.autoBraking = true;

                var health = go.AddComponent<Health>();
                health.MaxHealth = 100.0f;
                health.Team = 1;

                var holder = go.AddComponent<WeaponHolder>();
                holder.ShoulderHeight = 1.47f;
                holder.ShoulderSide = 0.16f;

                var ai = go.AddComponent<EnemyShooterAI>();
                ai.Loadout = loadout;
                ai.EyeHeight = 1.75f;
                ai.HurtSounds = new[] { Clip("Soldier_Hurt_1"), Clip("Soldier_Hurt_2"), Clip("Soldier_Hurt_3") };
                ai.DeathSound = Clip("Soldier_Death");

                SetLayer(go, CombatLayers.Enemy);
                BuildRagdoll(animator, health, 75.0f, true, 2.5f);
                go.AddComponent<RagdollController>();

                PrefabUtility.SaveAsPrefabAsset(go, $"Assets/Prefabs/Enemies/Soldier_{i}.prefab");
                Object.DestroyImmediate(go);
            }

            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kevin Iglesias/Zombie Animations/Prefabs/Zombie.prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = "Zombie";
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var animator = go.GetComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "/Enemies/Zombie.controller");
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var agent = go.AddComponent<NavMeshAgent>();
                agent.radius = 0.4f;
                agent.height = 2.0f;
                agent.acceleration = 8.0f;
                agent.angularSpeed = 0.0f;
                agent.stoppingDistance = 0.2f;

                var health = go.AddComponent<Health>();
                health.MaxHealth = 90.0f;
                health.Team = 1;

                var ai = go.AddComponent<ZombieAI>();
                ai.WalkAnimationSpeed = 0.9f;
                ai.AttackHitTime = 0.55f;
                ai.GroanSounds = new[] { Clip("Zombie_Groan_1"), Clip("Zombie_Groan_2"), Clip("Zombie_Groan_3"), Clip("Zombie_Groan_4") };
                ai.AttackSound = Clip("Zombie_Attack");
                ai.HurtSounds = new[] { Clip("Zombie_Hurt_1"), Clip("Zombie_Hurt_2"), Clip("Zombie_Hurt_3") };
                ai.DeathSound = Clip("Zombie_Death");

                SetLayer(go, CombatLayers.Enemy);
                BuildRagdoll(animator, health, 70.0f, true, 3.0f);
                go.AddComponent<RagdollController>();

                PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/Enemies/Zombie.prefab");
                Object.DestroyImmediate(go);
            }
        }

        // ---------------------------------------------------------------- ragdoll

        // capsules / boxes on the main bones, joined together; they double as hitboxes
        public static void BuildRagdoll(Animator animator, Health owner, float totalMass, bool flesh, float headMultiplier)
        {
            Transform t = animator.transform;
            Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);

            Transform hips = Bone(HumanBodyBones.Hips);
            Transform chest = Bone(HumanBodyBones.UpperChest) ?? Bone(HumanBodyBones.Chest) ?? Bone(HumanBodyBones.Spine);
            Transform neck = Bone(HumanBodyBones.Neck) ?? Bone(HumanBodyBones.Head);
            Transform head = Bone(HumanBodyBones.Head);

            // clear a previous ragdoll: only the bodies on bones, the character's own colliders stay
            foreach (Rigidbody r in t.GetComponentsInChildren<Rigidbody>())
            {
                if (r.transform == t) continue;
                foreach (CharacterJoint j in r.GetComponents<CharacterJoint>()) Object.DestroyImmediate(j);
                foreach (Hitbox h in r.GetComponents<Hitbox>()) Object.DestroyImmediate(h);
                foreach (Collider c in r.GetComponents<Collider>()) Object.DestroyImmediate(c);
                Object.DestroyImmediate(r);
            }

            float height = head.position.y - t.position.y;
            float scale = height / 1.55f;

            // some avatars map the hips to a root bone on the floor; the thighs' parent is the real pelvis
            if (hips.position.y - t.position.y < height * 0.3f) hips = Bone(HumanBodyBones.LeftUpperLeg).parent;

            Rigidbody hipsBody = Box(hips, Bone(HumanBodyBones.Spine) ?? chest, t, new Vector2(0.3f, 0.2f) * scale, totalMass * 0.18f, null, owner, flesh, 1.0f, 0.1f * scale);
            Rigidbody chestBody = Box(chest, neck, t, new Vector2(0.36f, 0.22f) * scale, totalMass * 0.2f, hipsBody, owner, flesh, 1.0f, 0.0f);
            if (Bone(HumanBodyBones.Spine) != null && Bone(HumanBodyBones.Spine) != chest)
            {
                // the lower spine has no body of its own, extend the chest box down to it
                BoxCollider box = chest.GetComponent<BoxCollider>();
                ExtendBox(box, chest, Bone(HumanBodyBones.Spine).position);
            }

            Rigidbody headBody = Sphere(head, 0.12f * scale, totalMass * 0.07f, chestBody, owner, flesh, headMultiplier, t);

            foreach (bool left in new[] { true, false })
            {
                Transform upperArm = Bone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                Transform lowerArm = Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                Transform hand = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                Transform upperLeg = Bone(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                Transform lowerLeg = Bone(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                Transform foot = Bone(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);

                Rigidbody ua = Capsule(upperArm, lowerArm.position, 0.055f * scale, totalMass * 0.03f, chestBody, owner, flesh, 0.75f, t, 0.0f);
                Capsule(lowerArm, hand.position + (hand.position - lowerArm.position).normalized * 0.08f * scale, 0.045f * scale, totalMass * 0.02f, ua, owner, flesh, 0.75f, t, 0.0f);
                Rigidbody ul = Capsule(upperLeg, lowerLeg.position, 0.08f * scale, totalMass * 0.1f, hipsBody, owner, flesh, 0.75f, t, 0.0f);
                Capsule(lowerLeg, foot.position, 0.06f * scale, totalMass * 0.06f, ul, owner, flesh, 0.75f, t, 0.0f);
            }

            foreach (Rigidbody body in t.GetComponentsInChildren<Rigidbody>())
            {
                body.gameObject.layer = CombatLayers.Hitbox;
                body.isKinematic = true;
            }
        }

        private static Rigidbody Body(Transform bone, float mass, Rigidbody parent, Health owner, bool flesh, float multiplier, bool isHead, Transform root)
        {
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.5f;
            body.isKinematic = true;

            var hitbox = bone.gameObject.AddComponent<Hitbox>();
            hitbox.Owner = owner;
            hitbox.Multiplier = multiplier;
            hitbox.IsHead = isHead;
            hitbox.Flesh = flesh;

            if (parent != null)
            {
                var joint = bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parent;
                joint.enableProjection = true;
                Vector3 twist = bone.InverseTransformDirection(bone.position - parent.transform.position).normalized;
                if (twist.sqrMagnitude < 0.5f) twist = Vector3.up;
                joint.axis = twist;
                Vector3 swing = Vector3.Cross(twist, bone.InverseTransformDirection(root.forward));
                joint.swingAxis = swing.sqrMagnitude > 0.01f ? swing.normalized : Vector3.forward;
                joint.lowTwistLimit = new SoftJointLimit { limit = -30.0f };
                joint.highTwistLimit = new SoftJointLimit { limit = 30.0f };
                joint.swing1Limit = new SoftJointLimit { limit = isHead ? 30.0f : 50.0f };
                joint.swing2Limit = new SoftJointLimit { limit = isHead ? 30.0f : 40.0f };
            }

            return body;
        }

        private static Rigidbody Capsule(Transform bone, Vector3 end, float radius, float mass, Rigidbody parent, Health owner, bool flesh, float multiplier, Transform root, float extra)
        {
            float scale = bone.lossyScale.x;
            Vector3 localEnd = bone.InverseTransformPoint(end);
            int axis = DominantAxis(localEnd);
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = axis;
            capsule.center = localEnd * 0.5f;
            capsule.height = localEnd.magnitude + radius / scale;
            capsule.radius = radius / scale;
            return Body(bone, mass, parent, owner, flesh, multiplier, false, root);
        }

        private static Rigidbody Box(Transform bone, Transform end, Transform root, Vector2 widthDepth, float mass, Rigidbody parent, Health owner, bool flesh, float multiplier, float extendDown)
        {
            float scale = bone.lossyScale.x;
            Vector3 localEnd = bone.InverseTransformPoint(end.position);
            int axis = DominantAxis(localEnd);
            int widthAxis = DominantAxis(bone.InverseTransformDirection(root.right));
            if (widthAxis == axis) widthAxis = (axis + 1) % 3;
            int depthAxis = 3 - axis - widthAxis;

            var box = bone.gameObject.AddComponent<BoxCollider>();
            Vector3 size = Vector3.zero;
            size[axis] = Mathf.Abs(localEnd[axis]) + extendDown / scale;
            size[widthAxis] = widthDepth.x / scale;
            size[depthAxis] = widthDepth.y / scale;
            Vector3 center = localEnd * 0.5f;
            center[axis] -= Mathf.Sign(localEnd[axis]) * extendDown / scale * 0.5f;
            box.size = size;
            box.center = center;
            return Body(bone, mass, parent, owner, flesh, multiplier, false, root);
        }

        private static void ExtendBox(BoxCollider box, Transform bone, Vector3 worldPoint)
        {
            if (box == null) return;
            var bounds = new Bounds(box.center, box.size);
            Vector3 local = bone.InverseTransformPoint(worldPoint);
            int axis = DominantAxis(local);
            Vector3 point = bounds.center;
            point[axis] = local[axis];
            bounds.Encapsulate(point);
            box.center = bounds.center;
            box.size = bounds.size;
        }

        private static Rigidbody Sphere(Transform bone, float radius, float mass, Rigidbody parent, Health owner, bool flesh, float multiplier, Transform root)
        {
            var sphere = bone.gameObject.AddComponent<SphereCollider>();
            float scale = bone.lossyScale.x;
            sphere.radius = radius / scale;
            sphere.center = bone.InverseTransformPoint(bone.position + root.up * radius * 0.8f);
            return Body(bone, mass, parent, owner, flesh, multiplier, true, root);
        }

        private static int DominantAxis(Vector3 v)
        {
            Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x > a.y && a.x > a.z ? 0 : a.y > a.z ? 1 : 2;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        // ---------------------------------------------------------------- player

        public static void SetupPlayer()
        {
            const string path = "Assets/Prefabs/PlayerRobot.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            var robot = root.GetComponentInChildren<StarterAssets.ThirdPersonController>(true).gameObject;
            var animator = robot.GetComponent<Animator>();

            Health health = Ensure<Health>(robot);
            health.MaxHealth = 100.0f;
            health.Team = 0;
            health.RegenDelay = 5.0f;
            health.RegenRate = 15.0f;

            // the character controller capsule is what enemy bullets hit
            Hitbox capsuleHitbox = Ensure<Hitbox>(robot);
            capsuleHitbox.Owner = health;
            capsuleHitbox.Flesh = false;

            WeaponHolder holder = Ensure<WeaponHolder>(robot);
            holder.ShoulderHeight = 1.36f;
            holder.ShoulderSide = 0.12f;
            holder.IsPlayer = true;
            Ensure<PlayerCombat>(robot);

            BuildRagdoll(animator, health, 60.0f, false, 2.0f);
            Ensure<RagdollController>(robot);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        // ---------------------------------------------------------------- scene

        // where things go in a level
        private struct MapLayout
        {
            public Vector3 Center;
            public Vector3 NavMeshSize;
            public Vector3[] EnemySpawns;
            // pistol, uzi, ak, m4, shotgun, sniper
            public Vector3[] WeaponSpots;
            public int MaxSoldiers;
            public int MaxZombies;
            public float MinSpawnDistance;
            public bool SpawnAroundPlayer;
        }

        private static readonly MapLayout TutorialArena = new MapLayout
        {
            Center = new Vector3(0.0f, 4.98f, 0.0f),
            NavMeshSize = new Vector3(32.0f, 16.0f, 32.0f),
            EnemySpawns = new[]
            {
                new Vector3(-12, 0, 12), new Vector3(12, 0, 12), new Vector3(12, 0, -12), new Vector3(-12, 0, -12),
                new Vector3(-12, 0, 4), new Vector3(12, 0, 3), new Vector3(4, 0, -12), new Vector3(-4, 0, 13)
            },
            WeaponSpots = new[]
            {
                new Vector3(-1.5f, 4.98f, -1.5f), new Vector3(4.0f, 4.98f, 3.0f), new Vector3(-8.0f, 4.98f, 1.5f),
                new Vector3(5.5f, 4.98f, -9.5f), new Vector3(-5.5f, 4.98f, -3.5f), new Vector3(1.4f, 8.47f, 8.7f)
            },
            MaxSoldiers = 2,
            MaxZombies = 4,
            MinSpawnDistance = 10.0f
        };

        // the eastern part of the Military Base Pack demo: hangars, garages and towers
        private static readonly MapLayout MilitaryBase = new MapLayout
        {
            Center = new Vector3(-15.0f, 0.0f, 10.0f),
            NavMeshSize = new Vector3(200.0f, 30.0f, 190.0f),
            EnemySpawns = new[]
            {
                new Vector3(-85, 0, -55), new Vector3(-80, 0, 70), new Vector3(-15, 0, 80), new Vector3(50, 0, 75),
                new Vector3(65, 0, 15), new Vector3(55, 0, -45), new Vector3(15, 0, -55), new Vector3(-45, 0, 75)
            },
            WeaponSpots = new[]
            {
                new Vector3(-12, 0, -8), new Vector3(-22, 0, -25), new Vector3(-75, 0, -5),
                new Vector3(35, 0, 25), new Vector3(-40, 0, -40), new Vector3(55, 0, -10)
            },
            MaxSoldiers = 3,
            MaxZombies = 6,
            MinSpawnDistance = 28.0f,
            SpawnAroundPlayer = true
        };

        public static void SetupScene()
        {
            SetupScene(TutorialArena);
        }

        private static void SetupScene(MapLayout layout)
        {
            WeaponData[] weapons = CreateWeapons();

            GameObject systems = FindOrCreate("CombatSystems");
            CombatEffects effects = Ensure<CombatEffects>(systems);
            effects.SparkMaterial = Mat("FX_Spark");
            effects.FlashMaterial = Mat("FX_Flash");
            effects.TracerMaterial = Mat("FX_Tracer");
            effects.SmokeMaterial = Mat("FX_Smoke");
            effects.DecalMaterial = Mat("FX_Decal");
            effects.BrassMaterial = Mat("FX_Brass");
            effects.GroundImpactSounds = Clips("Impact_Ground");
            effects.MetalImpactSounds = Clips("Impact_Metal");
            effects.FleshImpactSounds = Clips("Impact_Flesh");
            effects.ShellDropSounds = Clips("Shell_Drop");
            effects.BulletWhizSounds = Clips("Bullet_Whiz");
            effects.HitMarkerSound = Clip("UI_HitMarker");
            effects.HeadshotSound = Clip("UI_Headshot");
            effects.KillSound = Clip("UI_Kill");
            effects.PickupSound = Clip("Weapon_Pickup");
            effects.SwitchSound = Clip("Weapon_Switch");
            effects.RobotHurtSounds = Clips("Robot_Hurt");
            effects.RobotDeathSound = Clip("Robot_Death");
            effects.PlayerHurtSounds = Clips("Player_Hurt");
            effects.AlertSound = Clip("Robot_Alert");
            Ensure<CombatHUD>(systems);
            EditorUtility.SetDirty(systems);

            // enemies spawn around the edges of the arena
            GameObject spawner = FindOrCreate("EnemySpawner");
            spawner.transform.position = layout.Center;
            EnemySpawner enemySpawner = Ensure<EnemySpawner>(spawner);
            enemySpawner.SoldierPrefabs = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                enemySpawner.SoldierPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/Soldier_{i + 1}.prefab");
            }

            enemySpawner.ZombiePrefabs = new[] { AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Zombie.prefab") };
            enemySpawner.NavMeshSize = layout.NavMeshSize;
            enemySpawner.MaxSoldiers = layout.MaxSoldiers;
            enemySpawner.MaxZombies = layout.MaxZombies;
            enemySpawner.MinDistanceFromPlayer = layout.MinSpawnDistance;
            enemySpawner.SpawnAroundPlayer = layout.SpawnAroundPlayer;
            enemySpawner.MaxDistanceFromPlayer = layout.SpawnAroundPlayer ? 45.0f : 100.0f;
            Vector3[] spawnPoints = layout.EnemySpawns;
            ClearChildren(spawner.transform);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var point = new GameObject($"SpawnPoint_{i + 1}");
                point.transform.SetParent(spawner.transform, false);
                point.transform.localPosition = spawnPoints[i];
            }

            EditorUtility.SetDirty(spawner);

            // weapons on the ground
            GameObject pickups = FindOrCreate("WeaponSpawns");
            ClearChildren(pickups.transform);
            for (int i = 0; i < weapons.Length && i < layout.WeaponSpots.Length; i++)
            {
                var point = new GameObject("Spawn_" + weapons[i].DisplayName);
                point.transform.SetParent(pickups.transform, false);
                point.transform.position = layout.WeaponSpots[i];
                point.AddComponent<WeaponSpawnPoint>().Weapon = weapons[i];
            }

            EditorUtility.SetDirty(pickups);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(systems.scene);
        }

        // ---------------------------------------------------------------- military base map

        private const string BaseDemoScene = "Assets/Tiny Teacup Studio/Military Base Pack/Demo/DemoScene.unity";
        public const string BaseCombatScene = "Assets/Scenes/MilitaryBase_Combat.unity";

        [MenuItem("Tools/Combat/Setup Military Base Map")]
        public static void SetupMilitaryBase()
        {
            // the pack ships Built-in materials, convert them for URP
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Tiny Teacup Studio/Military Base Pack/Materials" }))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                ToUrpLit(material, null, 0.2f);
                if (emission.maxColorComponent > 0.01f)
                {
                    material.SetColor("_EmissionColor", emission);
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                    EditorUtility.SetDirty(material);
                }
            }

            // the enemies' navigation mesh is built at runtime from these meshes, which needs read access in a build
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Tiny Teacup Studio/Military Base Pack/Models" }))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (importer == null || importer.isReadable) continue;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            // a copy of the demo scene, so the pack's own demo stays untouched
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BaseCombatScene) == null) AssetDatabase.CopyAsset(BaseDemoScene, BaseCombatScene);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BaseCombatScene, UnityEditor.SceneManagement.OpenSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // the demo's flythrough camera and a volume with a missing script
                if (root.name == "Camera" || root.name == "Post Processing Volume")
                {
                    Object.DestroyImmediate(root);
                    continue;
                }

                // a few ground tiles sit on the layer now used by the player
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.gameObject.layer >= CombatLayers.Player && t.gameObject.layer <= CombatLayers.Debris &&
                        t.GetComponentInParent<Combat.PlayerCombat>() == null)
                    {
                        t.gameObject.layer = 0;
                    }
                }
            }

            if (Object.FindAnyObjectByType<PlayerCombat>() == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerRobot.prefab");
                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                player.transform.SetPositionAndRotation(new Vector3(-15.0f, 0.0f, -5.0f), Quaternion.identity);
            }

            SetupScene(MilitaryBase);

            // play this map first, keep the tutorial scene in the build
            var buildScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(BaseCombatScene, true) };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != BaseCombatScene) buildScenes.Add(existing);
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- menu, waves and roll

        private const string LocomotionController = "Assets/SourceFiles/StarterAssets/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";

        [MenuItem("Tools/Combat/Setup Menu, Waves and Roll")]
        public static void SetupGameModes()
        {
            AnimationClip roll = CreateRollAnimation();
            AddRollToAnimator(roll);

            const string playerPath = "Assets/Prefabs/PlayerRobot.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(playerPath);
            var combat = root.GetComponentInChildren<PlayerCombat>(true);
            combat.RollSound = Clip("Roll_Whoosh");
            PrefabUtility.SaveAsPrefabAsset(root, playerPath);
            PrefabUtility.UnloadPrefabContents(root);

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != BaseCombatScene)
            {
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BaseCombatScene, UnityEditor.SceneManagement.OpenSceneMode.Single);
            }

            GameObject flowObject = FindOrCreate("GameFlow");
            GameFlow flow = Ensure<GameFlow>(flowObject);
            flow.MenuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SourceFiles/SoundFX/Music_Suspenseful.ogg");
            flow.GameMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SourceFiles/SoundFX/Music_Exciting.ogg");
            flow.ClickSound = Clip("UI_Click");
            flow.WaveStartSound = Clip("Wave_Start");
            flow.WaveClearedSound = Clip("Wave_Cleared");
            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                spawner.UseWaves = true;
                flow.FlyoverCenter = spawner.transform;
                EditorUtility.SetDirty(spawner);
            }

            EditorUtility.SetDirty(flowObject);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            PlayFromGameScene.Apply();
        }

        private static AnimationClip CreateRollAnimation()
        {
            const string path = "Assets/SourceFiles/Animation/Climbing/Roll.anim";
            AnimationClip idle = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath("Assets/SourceFiles/StarterAssets/ThirdPersonController/Character/Animations/Stand--Idle.anim.fbx"))
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) idle = c;
            }

            // idle pose at t=0 as the base: muscles only, root centered
            var basePose = new Dictionary<string, float>();
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(idle))
            {
                string n = b.propertyName;
                if (n.Contains("FootT") || n.Contains("FootQ") || n.Contains("HandT") || n.Contains("HandQ") || n.Contains("TDOF")) continue;
                basePose[n] = AnimationUtility.GetEditorCurve(idle, b).Evaluate(0);
            }

            basePose["RootT.x"] = 0; basePose["RootT.z"] = 0;
            basePose["RootQ.x"] = 0; basePose["RootQ.y"] = 0; basePose["RootQ.z"] = 0; basePose["RootQ.w"] = 1;

            // crouch going in, a tight tuck through the spin, and back out to standing
            var crouch = Muscles(("Spine Front-Back", 0.4f), ("Chest Front-Back", 0.3f),
                ("Left Upper Leg Front-Back", -0.3f), ("Right Upper Leg Front-Back", -0.2f),
                ("Left Lower Leg Stretch", 0.0f), ("Right Lower Leg Stretch", 0.1f),
                ("Left Arm Down-Up", -0.4f), ("Right Arm Down-Up", -0.4f), ("Left Arm Front-Back", -0.6f), ("Right Arm Front-Back", -0.6f));
            var tuck = Muscles(("Spine Front-Back", 0.9f), ("Chest Front-Back", 0.7f), ("UpperChest Front-Back", 0.5f),
                ("Neck Nod Down-Up", 0.6f), ("Head Nod Down-Up", 0.5f),
                ("Left Upper Leg Front-Back", -1.0f), ("Right Upper Leg Front-Back", -1.0f),
                ("Left Lower Leg Stretch", -1.0f), ("Right Lower Leg Stretch", -1.0f),
                ("Left Arm Down-Up", -0.2f), ("Right Arm Down-Up", -0.2f), ("Left Arm Front-Back", -0.9f), ("Right Arm Front-Back", -0.9f),
                ("Left Forearm Stretch", -0.5f), ("Right Forearm Stretch", -0.5f));
            var rise = Muscles(("Spine Front-Back", 0.35f), ("Chest Front-Back", 0.2f),
                ("Left Upper Leg Front-Back", -0.1f), ("Right Upper Leg Front-Back", 0.3f),
                ("Left Lower Leg Stretch", 0.2f), ("Right Lower Leg Stretch", 0.6f));

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.ClearCurves();
            var keys = new List<(float time, Dictionary<string, float> pose)>
            {
                (0.0f, crouch), (0.1f, tuck), (0.45f, tuck), (0.58f, rise), (0.65f, new Dictionary<string, float>())
            };
            foreach (string n in basePose.Keys)
            {
                var curve = new AnimationCurve();
                foreach (var (time, pose) in keys) curve.AddKey(new Keyframe(time, pose.TryGetValue(n, out float v) ? v : basePose[n]));
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }

                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), n), curve);
            }

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static Dictionary<string, float> Muscles(params (string name, float value)[] values)
        {
            var pose = new Dictionary<string, float>();
            foreach (var (name, value) in values) pose[name] = value;
            return pose;
        }

        private static void AddRollToAnimator(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(LocomotionController);
            bool hasParameter = false;
            foreach (AnimatorControllerParameter p in controller.parameters) hasParameter |= p.name == "Roll";
            if (!hasParameter) controller.AddParameter("Roll", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState roll = null, locomotion = null;
            foreach (ChildAnimatorState s in sm.states)
            {
                if (s.state.name == "Roll") roll = s.state;
                if (s.state.name == "Idle Walk Run Blend") locomotion = s.state;
            }

            if (roll == null) roll = sm.AddState("Roll", new Vector3(650, 450, 0));
            roll.motion = clip;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            {
                if (t.destinationState == roll) sm.RemoveAnyStateTransition(t);
            }

            foreach (AnimatorStateTransition t in roll.transitions) roll.RemoveTransition(t);

            AnimatorStateTransition into = sm.AddAnyStateTransition(roll);
            into.AddCondition(AnimatorConditionMode.If, 0, "Roll");
            into.canTransitionToSelf = false;
            into.hasExitTime = false;
            into.duration = 0.06f;
            AnimatorStateTransition outOf = roll.AddTransition(locomotion);
            outOf.hasExitTime = true;
            outOf.exitTime = 0.92f;
            outOf.duration = 0.12f;
            EditorUtility.SetDirty(controller);
        }

        private static Material Mat(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Effects/{name}.mat");
        }

        private static GameObject FindOrCreate(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go : new GameObject(name);
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
