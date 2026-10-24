using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Combat.EditorTools
{
    /* Phase 3 (immersion) pass. Each step is safe to run again:
     *   lighting   - the chest flashlight on the player, a lighter post stack, a little more ambient,
     *                and fewer shadow-casting lamps so the shadow atlas has room for the flashlight
     */
    public static partial class CombatSetup
    {
        [MenuItem("Tools/Combat/Phase 3/1 - Lighting and Flashlight")]
        public static void SetupPhase3Lighting()
        {
            EditPrefab("Assets/Prefabs/PlayerRobot.prefab", root =>
            {
                GameObject player = root.GetComponentInChildren<StarterAssets.ThirdPersonController>(true).gameObject;
                // re-added so the calibrated defaults in the script win over older saved values
                var old = player.GetComponent<TacticalFlashlight>();
                if (old != null) Object.DestroyImmediate(old, true);
                player.AddComponent<TacticalFlashlight>();
            });

            TuneNightProfile();
            TuneAmbient();
            TuneShadowBudget();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Phase 3 lighting set up");
        }

        // ---------------------------------------------------------------- player

        [MenuItem("Tools/Combat/Phase 3/3 - Player Movement and Weapons")]
        public static void SetupPhase3Player()
        {
            EditPrefab("Assets/Prefabs/PlayerRobot.prefab", root =>
            {
                GameObject player = root.GetComponentInChildren<StarterAssets.ThirdPersonController>(true).gameObject;
                Ensure<LeanController>(player);
            });

            AssetDatabase.SaveAssets();
            Debug.Log("Phase 3 player set up");
        }

        [MenuItem("Tools/Combat/Phase 3/5 - Enemy Reactions")]
        public static void SetupPhase3Enemies()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EditPrefab(path, root =>
                {
                    var animator = root.GetComponentInChildren<Animator>(true);
                    if (animator == null || root.GetComponentInChildren<Health>(true) == null) return;
                    Ensure<HitReactor>(animator.gameObject);
                    count++;
                });
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Hit reactions on {count} enemy prefabs");
        }

        // ---------------------------------------------------------------- items and icons

        private const string IconFolder = "Assets/SourceFiles/UI/Icons/Items";
        private const string WeaponIconFolder = "Assets/SourceFiles/UI/Icons/Weapons";
        private const string ItemFolder = Root + "/Items";
        private const string ResourcesFolder = "Assets/SourceFiles/Resources";

        [MenuItem("Tools/Combat/Phase 3/2 - Item Icons and Catalog")]
        public static void SetupPhase3Items()
        {
            EnsureFolder(ItemFolder);
            EnsureFolder(ResourcesFolder);
            EnsureFolder(WeaponIconFolder);
            ImportIcons(IconFolder);

            // id, name, description, category, icon, max stack
            var table = new (string id, string name, string text, ItemCategory category, string icon, int stack, ItemRarity rarity)[]
            {
                ("medkit", "Kit médico", "Recupera 50 de vida. Leva um momento para aplicar.", ItemCategory.Medical, "medkit", 3, ItemRarity.Common),
                ("grenade", "Granada", "Explode 2,5 s após o arremesso. Também fere quem a lançou.", ItemCategory.Tactical, "grenade", 3, ItemRarity.Uncommon),
                ("molotov", "Coquetel molotov", "Quebra no impacto e deixa uma área em chamas por alguns segundos.", ItemCategory.Tactical, "molotov", 2, ItemRarity.Uncommon),
                ("flare", "Sinalizador", "Ilumina a área por 40 s e atrai os infectados.", ItemCategory.Tactical, "flare", 3, ItemRarity.Common),
                ("barricade", "Kit de barricada", "Tábuas e pregos para reforçar uma porta ou passagem.", ItemCategory.Construction, "barricade", 2, ItemRarity.Common),
                ("decoy", "Distração sonora", "Apita por alguns segundos e desvia a atenção dos inimigos.", ItemCategory.Tactical, "decoy", 3, ItemRarity.Common),
                ("fuse", "Fusível", "Fusível de alta corrente do painel do gerador.", ItemCategory.Mission, "fuse", 9, ItemRarity.Story),
                ("keycard", "Cartão de acesso", "Cartão magnético do intendente. Abre o arsenal.", ItemCategory.Mission, "keycard", 1, ItemRarity.Story),
                ("radio_module", "Módulo de rádio", "Placa de comunicação da estação de rádio.", ItemCategory.Mission, "radio_module", 1, ItemRarity.Story),
                ("blackbox", "Caixa-preta", "Gravador de dados do laboratório. Contém o que aconteceu aqui.", ItemCategory.Mission, "blackbox", 1, ItemRarity.Story),
                ("armory_case", "Caixa de equipamento", "Equipamento do arsenal: munição e explosivos.", ItemCategory.Mission, "case", 1, ItemRarity.Story),
                ("evac_codes", "Códigos de evacuação", "Códigos de autenticação para chamar o resgate.", ItemCategory.Mission, "evac_codes", 1, ItemRarity.Story),
                ("lab_doc", "Registro científico", "Anotações da equipe do laboratório.", ItemCategory.Document, "document", 9, ItemRarity.Story),
                ("shift_log", "Diário de turno", "O último registro do turno da noite.", ItemCategory.Document, "document", 1, ItemRarity.Story),
                ("memo", "Memorando", "Memorando sobre o gerador.", ItemCategory.Document, "document", 1, ItemRarity.Story),
                ("note_armory", "Bilhete do intendente", "Um bilhete deixado perto da torre.", ItemCategory.Document, "document", 1, ItemRarity.Story),
                ("lab_procedure", "Procedimento de contenção", "A ordem correta das válvulas.", ItemCategory.Document, "document", 1, ItemRarity.Story),
                ("valve_key", "Chave de válvula", "Chave para as válvulas de contenção.", ItemCategory.Mission, "case", 1, ItemRarity.Story),
                ("ammo_box", "Caixa de munição", "Munição para as armas que você carrega.", ItemCategory.Ammo, "ammo_box", 99, ItemRarity.Common),
            };

            var items = new System.Collections.Generic.List<ItemDefinition>();
            foreach (var row in table)
            {
                string path = $"{ItemFolder}/Item_{row.id}.asset";
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<ItemDefinition>();
                    AssetDatabase.CreateAsset(item, path);
                }

                item.Id = row.id;
                item.DisplayName = row.name;
                item.Description = row.text;
                item.Category = row.category;
                item.Rarity = row.rarity;
                item.MaxStack = row.stack;
                item.Icon = Icon(row.icon);
                EditorUtility.SetDirty(item);
                items.Add(item);
            }

            string catalogPath = $"{ResourcesFolder}/{ItemCatalog.ResourcePath}.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }

            catalog.Items = items.ToArray();
            catalog.CreditsIcon = Icon("credits");
            catalog.HealthIcon = Icon("health");
            catalog.ArmorIcon = Icon("armor");
            catalog.FallbackIcon = Icon("unknown");
            EditorUtility.SetDirty(catalog);

            SetupWeaponPresentation();
            AssetDatabase.SaveAssets();
            Debug.Log($"Item catalog: {items.Count} items");
        }

        private static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{IconFolder}/{name}.png");

        private static void ImportIcons(string folder)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        // the caliber, ammo icon and carrying class of each gun, plus an icon rendered from its own model
        private static void SetupWeaponPresentation()
        {
            var specs = new (string asset, WeaponClass type, string ammo, string ammoIcon)[]
            {
                ("Weapon_M1911", WeaponClass.Pistol, ".45 ACP", "ammo_pistol"),
                ("Weapon_Uzi", WeaponClass.SubmachineGun, "9 mm", "ammo_pistol"),
                ("Weapon_AK74", WeaponClass.Rifle, "5,45 mm", "ammo_rifle"),
                ("Weapon_M4_8", WeaponClass.Rifle, "5,56 mm", "ammo_rifle"),
                ("Weapon_Bennelli_M4", WeaponClass.Shotgun, "calibre 12", "ammo_shotgun"),
                ("Weapon_M107", WeaponClass.Sniper, ".50 BMG", "ammo_sniper"),
            };

            foreach (var spec in specs)
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{Root}/Weapons/{spec.asset}.asset");
                if (weapon == null) continue;
                weapon.Class = spec.type;
                weapon.AmmoName = spec.ammo;
                weapon.AmmoIcon = Icon(spec.ammoIcon);
                string iconPath = $"{WeaponIconFolder}/{spec.asset.Replace("Weapon_", "")}.png";
                if (RenderWeaponIcon(weapon, iconPath)) weapon.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                EditorUtility.SetDirty(weapon);
            }
        }

        // A flat side-view silhouette of the weapon's own model, in the same style as the item icons:
        // light fill, dark outline, transparent background. Rendered twice (black and white backdrop)
        // so the coverage comes out right whatever the pipeline does with alpha.
        private static bool RenderWeaponIcon(WeaponData weapon, string path)
        {
            if (weapon.Model == null) return false;
            const int width = 512, height = 256, layer = 31;
            GameObject copy = Object.Instantiate(weapon.Model);
            copy.transform.position = new Vector3(0.0f, -2000.0f, 0.0f);
            copy.transform.rotation = Quaternion.identity;
            var white = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
            Bounds bounds = new Bounds(copy.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>())
            {
                renderer.gameObject.layer = layer;
                var mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = white;
                renderer.sharedMaterials = mats;
                if (!any) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds);
                any = true;
            }

            if (!any)
            {
                Object.DestroyImmediate(copy);
                return false;
            }

            // the barrel points along the model's -Z: looking from -X puts it on the right
            var camGo = new GameObject("IconCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50.0f;
            camGo.transform.position = bounds.center - Vector3.right * 10.0f;
            camGo.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            float aspect = (float)width / height;
            cam.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.z / aspect) * 1.12f;
            var urpData = camGo.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = false;
            urpData.antialiasing = AntialiasingMode.None;

            // a white unlit model on black: brightness is coverage. The first render can come out empty
            // while shaders compile asynchronously, so compile synchronously and warm up once
            bool async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            Capture(cam, Color.black, width * 2, height * 2);
            Color32[] onBlack = Capture(cam, Color.black, width * 2, height * 2);
            ShaderUtil.allowAsyncCompilation = async;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(copy);
            Object.DestroyImmediate(white);

            // coverage at 2x, then fill + outline, then down to 1x
            int w2 = width * 2, h2 = height * 2;
            var cover = new float[w2 * h2];
            for (int i = 0; i < cover.Length; i++) cover[i] = Mathf.Clamp01(onBlack[i].r / 245.0f);
            const int stroke = 5;
            var edge = new float[cover.Length];
            for (int y = 0; y < h2; y++)
            {
                for (int x = 0; x < w2; x++)
                {
                    float best = 0.0f;
                    for (int dy = -stroke; dy <= stroke && best < 1.0f; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= h2) continue;
                        for (int dx = -stroke; dx <= stroke; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= w2 || dx * dx + dy * dy > stroke * stroke) continue;
                            float c = cover[yy * w2 + xx];
                            if (c > best) best = c;
                        }
                    }

                    edge[y * w2 + x] = best;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            var fill = new Color(232 / 255.0f, 230 / 255.0f, 224 / 255.0f);
            var dark = new Color(12 / 255.0f, 13 / 255.0f, 15 / 255.0f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float f = 0.0f, e = 0.0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            int i = (y * 2 + sy) * w2 + x * 2 + sx;
                            f += cover[i];
                            e += edge[i];
                        }
                    }

                    f *= 0.25f;
                    e *= 0.25f;
                    float shade = 1.0f - 0.1f * (1.0f - (float)y / height);
                    Color rgb = Color.Lerp(dark, fill * shade, f);
                    pixels[y * width + x] = new Color(rgb.r, rgb.g, rgb.b, Mathf.Max(e * 0.82f, f));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            ImportIcons(System.IO.Path.GetDirectoryName(path).Replace("\\", "/"));
            return true;
        }

        private static Color32[] Capture(Camera cam, Color background, int width, int height)
        {
            cam.backgroundColor = background;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = target;
            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var read = new Texture2D(width, height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Color32[] pixels = read.GetPixels32();
            Object.DestroyImmediate(read);
            return pixels;
        }

        // ---------------------------------------------------------------- lighting

        // grain, vignette and desaturation were crushing everything below mid grey
        private static void TuneNightProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/Environment/NightProfile.asset");
            if (profile == null) return;

            if (profile.TryGet(out FilmGrain grain)) grain.intensity.Override(0.22f);
            if (profile.TryGet(out Vignette vignette))
            {
                vignette.intensity.Override(0.32f);
                vignette.smoothness.Override(0.6f);
            }

            if (profile.TryGet(out ColorAdjustments color))
            {
                color.saturation.Override(-22.0f);
                color.contrast.Override(12.0f);
            }

            if (profile.TryGet(out ChromaticAberration aberration)) aberration.intensity.Override(0.08f);
            EditorUtility.SetDirty(profile);
        }

        // a moonlit sky bounce: dark, but shapes read against the ground
        private static void TuneAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.17f, 0.19f, 0.27f);
            RenderSettings.ambientEquatorColor = new Color(0.11f, 0.12f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.055f, 0.055f, 0.065f);
        }

        // point lights cost six shadow maps each; lamps keep their light, only the flashlight
        // and the spot lamps cast shadows, and the atlas gets bigger
        private static void TuneShadowBudget()
        {
            int removed = 0;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type != LightType.Point || light.shadows == LightShadows.None) continue;
                Undo.RecordObject(light, "Point light shadows");
                light.shadows = LightShadows.None;
                removed++;
            }

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.additionalLightsShadowmapResolution = 4096;
                EditorUtility.SetDirty(urp);
            }

            Debug.Log($"Shadows removed from {removed} point lights");
        }
    }
}
