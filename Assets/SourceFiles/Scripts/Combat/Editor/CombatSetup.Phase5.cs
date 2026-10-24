using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Combat.EditorTools
{
    /* Phase 5 (world and survival) setup. Each step can run again safely:
     *   1 - item catalog (with the crafting materials) and the recipes
     *   2 - the map: a top-down picture of the base baked from its colliders
     *   3 - the scene: map, minimap, backpack and bench screens, the bench, materials to find
     *   4 - the voice manifest of every dialogue line (CSV for recording, status per line)
     */
    public static partial class CombatSetup
    {
        private const string CraftingFolder = Root + "/Crafting";
        private const string MapFolder = "Assets/SourceFiles/UI/Map";

        // ---------------------------------------------------------------- 1. items and recipes

        [MenuItem("Tools/Combat/Phase 5/1 - Items and Recipes")]
        public static void SetupPhase5Items()
        {
            SetupPhase3Items();
            EnsureFolder(CraftingFolder);

            // id, name, description, product, amount, station, ingredients
            var table = new (string id, string name, string text, string product, int amount, CraftingStationType station, (string, int)[] ingredients)[]
            {
                ("bandage", "Bandagem", "Tiras de tecido limpo. Pouco, mas rápido.", "bandage", 1, CraftingStationType.Anywhere,
                    new[] { ("cloth", 2) }),
                ("medkit", "Kit médico improvisado", "Material médico embalado em tecido. Cura como um kit da enfermaria.", "medkit", 1, CraftingStationType.Workbench,
                    new[] { ("med_supplies", 2), ("cloth", 1) }),
                ("decoy", "Dispositivo de distração", "Um apito eletrônico numa lata. Os infectados vão atrás do barulho.", "decoy", 1, CraftingStationType.Workbench,
                    new[] { ("electronics", 1), ("scrap", 1), ("wiring", 1) }),
                ("flare", "Sinalizador improvisado", "Queima vermelho por 40 s. Também atrai.", "flare", 1, CraftingStationType.Workbench,
                    new[] { ("chemicals", 1), ("scrap", 1) }),
                ("barricade", "Kit de barricada", "Chapas e molas para reforçar uma porta.", "barricade", 1, CraftingStationType.Workbench,
                    new[] { ("scrap", 2), ("mech_parts", 1) }),
                ("vest_patch", "Reforço de colete", "Uma placa de sucata presa ao colete.", "vest_patch", 1, CraftingStationType.Workbench,
                    new[] { ("scrap", 2), ("tactical_parts", 1), ("cloth", 1) }),
            };

            var recipes = new List<CraftingRecipe>();
            foreach (var row in table)
            {
                string path = $"{CraftingFolder}/Recipe_{row.id}.asset";
                var recipe = AssetDatabase.LoadAssetAtPath<CraftingRecipe>(path);
                if (recipe == null)
                {
                    recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
                    AssetDatabase.CreateAsset(recipe, path);
                }

                recipe.Id = row.id;
                recipe.DisplayName = row.name;
                recipe.Description = row.text;
                recipe.ProductId = row.product;
                recipe.ProductAmount = row.amount;
                recipe.Station = row.station;
                recipe.RequiredLevel = 1;
                recipe.UnlockFlag = "";
                recipe.Ingredients = System.Array.ConvertAll(row.ingredients, i => new CraftingRecipe.Ingredient { ItemId = i.Item1, Count = i.Item2 });
                EditorUtility.SetDirty(recipe);
                recipes.Add(recipe);
            }

            string catalogPath = $"{ResourcesFolder}/{CraftingCatalog.ResourcePath}.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<CraftingCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CraftingCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }

            catalog.Recipes = recipes.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"Crafting: {recipes.Count} recipes");
        }

        // ---------------------------------------------------------------- 2. map

        [MenuItem("Tools/Combat/Phase 5/2 - Bake Map")]
        public static void BakePhase5Map()
        {
            const int size = 512;
            // the fence runs well past the outermost named places, the west depot above all
            const float margin = 62.0f;

            // the square around every named place, with room around it
            var anchors = Object.FindObjectsByType<MissionAnchor>(FindObjectsInactive.Include);
            if (anchors.Length == 0)
            {
                Debug.LogError("Bake Map: no MissionAnchor in the scene");
                return;
            }

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (MissionAnchor anchor in anchors)
            {
                Vector3 p = anchor.transform.position;
                min = Vector2.Min(min, new Vector2(p.x, p.z));
                max = Vector2.Max(max, new Vector2(p.x, p.z));
            }

            Vector2 center = (min + max) * 0.5f;
            float side = Mathf.Max(max.x - min.x, max.y - min.y) + margin * 2.0f;
            min = center - Vector2.one * side * 0.5f;
            max = center + Vector2.one * side * 0.5f;

            Physics.SyncTransforms();
            int mask = CombatLayers.Sight & ~(1 << CombatLayers.Player);
            Terrain terrain = Terrain.activeTerrain;
            var height = new float[size * size];
            var ground = new float[size * size];
            var hitSomething = new bool[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wx = min.x + (x + 0.5f) / size * side, wz = min.y + (y + 0.5f) / size * side;
                    var origin = new Vector3(wx, 200.0f, wz);
                    int i = y * size + x;
                    float g = terrain != null ? terrain.SampleHeight(new Vector3(wx, 0, wz)) + terrain.transform.position.y : 0.0f;
                    ground[i] = g;
                    if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400.0f, mask, QueryTriggerInteraction.Ignore))
                    {
                        height[i] = hit.point.y - g;
                        hitSomething[i] = true;
                    }
                }
            }

            // classes by height above the ground: open ground, low things, walls and containers, roofs
            var cls = new int[size * size];
            for (int i = 0; i < cls.Length; i++)
            {
                float h = height[i];
                cls[i] = !hitSomething[i] ? -1 : h > 3.0f ? 3 : h > 1.0f ? 2 : h > 0.3f ? 1 : 0;
            }

            Color[] palette =
            {
                new Color(0.115f, 0.12f, 0.13f),   // ground
                new Color(0.2f, 0.21f, 0.22f),     // low obstacles
                new Color(0.27f, 0.28f, 0.3f),     // walls, containers
                new Color(0.34f, 0.35f, 0.37f),    // roofs
            };

            var pixels = new Color[size * size];
            float metresPerPixel = side / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    int c = cls[i];
                    Color color = c < 0 ? new Color(0.06f, 0.065f, 0.07f) : palette[c];
                    // a light edge where something stands up from lower ground, so shapes read when small
                    if (c > 0)
                    {
                        bool edge = false;
                        for (int d = 0; d < 4 && !edge; d++)
                        {
                            int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                            if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                            edge = cls[ny * size + nx] < c;
                        }

                        if (edge) color += new Color(0.16f, 0.15f, 0.12f);
                    }

                    // a faint 25 m grid, the scale reference
                    float wx = (x + 0.5f) * metresPerPixel, wz = (y + 0.5f) * metresPerPixel;
                    if (c <= 0 && (Mathf.Repeat(wx, 25.0f) < metresPerPixel || Mathf.Repeat(wz, 25.0f) < metresPerPixel)) color += new Color(0.025f, 0.025f, 0.03f);
                    color.a = 1.0f;
                    pixels[i] = color;
                }
            }

            EnsureFolder(MapFolder);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            string pngPath = $"{MapFolder}/BaseMap.png";
            File.WriteAllBytes(pngPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(pngPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            EnsureFolder(ResourcesFolder);
            string definitionPath = $"{ResourcesFolder}/{MapDefinition.ResourcePath}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<MapDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MapDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            definition.Map = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            definition.Min = min;
            definition.Max = max;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Debug.Log($"Map baked: {side:F0} m square from ({min.x:F0}, {min.y:F0}), {metresPerPixel:F2} m per pixel");
        }

        // ---------------------------------------------------------------- 3. scene

        // the first bench goes in the supply depot, the place the campaign already calls "Depósito de suprimentos";
        // materials lying around, near the places that explain them (anchor id, offset, item, amount)
        private static readonly (string anchor, Vector3 offset, string item, int amount)[] MaterialSpots =
        {
            ("depot", new Vector3(-2.5f, 0, 3.0f), "scrap", 2),
            ("depot", new Vector3(4.5f, 0, -2.0f), "cloth", 2),
            ("depot", new Vector3(-4.0f, 0, -3.0f), "wiring", 1),
            ("gen_room", new Vector3(3.0f, 0, -2.5f), "mech_parts", 1),
            ("gen_room", new Vector3(-3.0f, 0, 2.0f), "wiring", 2),
            ("radio_station", new Vector3(2.5f, 0, 2.5f), "electronics", 1),
            ("lab", new Vector3(-3.0f, 0, 2.5f), "chemicals", 1),
            ("lab", new Vector3(3.5f, 0, -2.0f), "med_supplies", 2),
            ("armory", new Vector3(2.5f, 0, 3.0f), "tactical_parts", 1),
            ("first_area", new Vector3(4.0f, 0, 3.0f), "cloth", 1),
            ("first_area", new Vector3(-5.0f, 0, -2.0f), "scrap", 1),
            ("watchtower", new Vector3(3.0f, 0, -3.0f), "med_supplies", 1),
        };

        [MenuItem("Tools/Combat/Phase 5/3 - Scene (Map, Backpack, Bench, Materials)")]
        public static void SetupPhase5Scene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != BaseCombatScene)
            {
                Debug.LogError("Open " + BaseCombatScene + " first");
                return;
            }

            GameObject systems = FindOrCreate("ExplorationSystems");
            Ensure<MinimapView>(systems);
            Ensure<TacticalMapView>(systems);
            Ensure<InventoryView>(systems);

            var anchors = new Dictionary<string, MissionAnchor>();
            foreach (MissionAnchor anchor in Object.FindObjectsByType<MissionAnchor>(FindObjectsInactive.Include))
            {
                if (!string.IsNullOrEmpty(anchor.Id)) anchors[anchor.Id] = anchor;
            }

            // the bench
            GameObject benchRoot = FindOrCreate("Workbench_Depot");
            ClearChildren(benchRoot.transform);
            // its own collider from an earlier run would make every spot look taken
            var oldBox = benchRoot.GetComponent<BoxCollider>();
            if (oldBox != null) Object.DestroyImmediate(oldBox);
            if (anchors.TryGetValue("depot", out MissionAnchor depot))
            {
                benchRoot.transform.position = FreeSpot(depot.transform.position, new Vector3(1.1f, 0.55f, 0.6f), 2.5f);
                // the working side faces the middle of the depot
                benchRoot.transform.rotation = Quaternion.LookRotation(FlatDirection(depot.transform.position - benchRoot.transform.position));
            }

            BuildWorkbenchVisual(benchRoot.transform);
            var station = Ensure<CraftingStation>(benchRoot);
            station.Type = CraftingStationType.Workbench;
            station.DisplayName = "Bancada de trabalho";
            var marker = Ensure<MapMarker>(benchRoot);
            marker.Id = "workbench_depot";
            marker.Kind = MapMarkerKind.Workbench;
            marker.Label = "Bancada";

            // the materials
            GameObject materials = FindOrCreate("CraftingMaterials");
            ClearChildren(materials.transform);
            int placed = 0;
            foreach (var spot in MaterialSpots)
            {
                if (!anchors.TryGetValue(spot.anchor, out MissionAnchor anchor)) continue;
                var go = new GameObject($"Material_{spot.item}_{spot.anchor}");
                go.transform.SetParent(materials.transform, false);
                // never inside a container or a wall: the nearest clear spot to where it was meant to be
                go.transform.position = FreeSpot(anchor.transform.position + spot.offset, new Vector3(0.2f, 0.15f, 0.2f), 0.0f) + Vector3.up * 0.02f;
                var item = go.AddComponent<WorldItem>();
                item.ItemId = spot.item;
                item.Amount = spot.amount;
                placed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Phase 5 scene: systems, bench at {benchRoot.transform.position}, {placed} materials");
        }

        private static Vector3 FlatDirection(Vector3 v)
        {
            v.y = 0.0f;
            return v.sqrMagnitude < 0.01f ? Vector3.forward : v.normalized;
        }

        // the nearest spot around a place where a box of this size stands on the ground without touching anything
        private static Vector3 FreeSpot(Vector3 near, Vector3 halfExtents, float startRadius)
        {
            Physics.SyncTransforms();
            int mask = CombatLayers.Sight & ~(1 << CombatLayers.Player);
            for (float radius = startRadius; radius <= startRadius + 10.0f; radius += 1.0f)
            {
                for (int step = 0; step < (radius < 0.01f ? 1 : 12); step++)
                {
                    float angle = step * 30.0f * Mathf.Deg2Rad;
                    Vector3 ground = Grounded(near + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * radius);
                    // a little above the ground so the floor itself doesn't count, with room to walk around it
                    Vector3 center = ground + Vector3.up * (halfExtents.y + 0.1f);
                    if (!Physics.CheckBox(center, halfExtents + new Vector3(0.6f, 0.0f, 0.6f), Quaternion.identity, mask, QueryTriggerInteraction.Ignore)) return ground;
                }
            }

            return Grounded(near);
        }

        // drops a point onto whatever is below it
        private static Vector3 Grounded(Vector3 point)
        {
            Physics.SyncTransforms();
            int mask = CombatLayers.Sight & ~(1 << CombatLayers.Player);
            return Physics.Raycast(point + Vector3.up * 3.0f, Vector3.down, out RaycastHit hit, 10.0f, mask, QueryTriggerInteraction.Ignore) ? hit.point : point;
        }

        // a provisional bench made of primitives and the project's own materials (no furniture asset in the project)
        private static void BuildWorkbenchVisual(Transform root)
        {
            Material wood = Mat("Loot_Crate");
            Material metal = Mat("Loot_Metal");
            void Part(string name, Vector3 position, Vector3 scale, Material material, bool collider = true)
            {
                GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = name;
                part.transform.SetParent(root, false);
                part.transform.localPosition = position;
                part.transform.localScale = scale;
                if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;
                if (!collider) Object.DestroyImmediate(part.GetComponent<Collider>());
            }

            // one box for the whole bench: the player bumps into it, enemies walk around it
            var box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.5f, 0);
            box.size = new Vector3(1.8f, 1.0f, 0.8f);
            var obstacle = root.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle == null) obstacle = root.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = box.size;
            obstacle.carving = true;

            Part("Top", new Vector3(0, 0.9f, 0), new Vector3(1.8f, 0.08f, 0.8f), wood, false);
            Part("LegFL", new Vector3(-0.82f, 0.43f, 0.32f), new Vector3(0.08f, 0.86f, 0.08f), metal, false);
            Part("LegFR", new Vector3(0.82f, 0.43f, 0.32f), new Vector3(0.08f, 0.86f, 0.08f), metal, false);
            Part("LegBL", new Vector3(-0.82f, 0.43f, -0.32f), new Vector3(0.08f, 0.86f, 0.08f), metal, false);
            Part("LegBR", new Vector3(0.82f, 0.43f, -0.32f), new Vector3(0.08f, 0.86f, 0.08f), metal, false);
            Part("Shelf", new Vector3(0, 0.25f, 0), new Vector3(1.7f, 0.05f, 0.7f), wood, false);
            Part("Vice", new Vector3(-0.65f, 1.02f, 0.25f), new Vector3(0.22f, 0.16f, 0.16f), metal, false);
            Part("Toolbox", new Vector3(0.45f, 1.02f, -0.1f), new Vector3(0.45f, 0.18f, 0.22f), metal, false);
            Part("Board", new Vector3(0, 1.55f, -0.38f), new Vector3(1.6f, 0.9f, 0.04f), wood, false);

            // a work lamp over the bench: it reads as a place to stop, and makes it findable at night
            var lampObject = new GameObject("WorkLight");
            lampObject.transform.SetParent(root, false);
            lampObject.transform.localPosition = new Vector3(0, 2.1f, 0.1f);
            lampObject.transform.localRotation = Quaternion.Euler(80, 0, 0);
            var lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = new Color(1.0f, 0.86f, 0.62f);
            lamp.range = 6.0f;
            lamp.spotAngle = 95.0f;
            lamp.intensity = 6.0f;
            lamp.shadows = LightShadows.None;

            // the sign, as a TextMeshPro in the world
            var signObject = new GameObject("Sign", typeof(TMPro.TextMeshPro));
            signObject.transform.SetParent(root, false);
            // on the front of the board, readable from where the player stands (+z)
            signObject.transform.localPosition = new Vector3(0, 1.75f, -0.355f);
            signObject.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var sign = signObject.GetComponent<TMPro.TextMeshPro>();
            sign.text = "OFICINA";
            sign.fontSize = 3.0f;
            sign.alignment = TMPro.TextAlignmentOptions.Center;
            sign.color = new Color(1.0f, 0.78f, 0.3f);
            sign.rectTransform.sizeDelta = new Vector2(1.6f, 0.4f);
        }

        // ---------------------------------------------------------------- 4. voice manifest

        [MenuItem("Tools/Combat/Phase 5/4 - Export Dialogue Manifest")]
        public static void ExportDialogueManifest()
        {
            DialogueLibrary library = AssetDatabase.LoadAssetAtPath<DialogueLibrary>($"{ResourcesFolder}/{DialogueLibrary.ResourcePath}.asset");
            if (library == null)
            {
                Debug.LogError("No dialogue library: run Phase 3 > 4 first");
                return;
            }

            // relink voices dropped in the folder since the last run, without rebuilding the lines
            int linked = 0;
            foreach (DialogueLine line in library.Lines)
            {
                if (line.Clip != null) continue;
                line.Clip = FindVoiceClip(line.Id);
                if (line.Clip != null) linked++;
            }

            if (linked > 0)
            {
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }

            var csv = new StringBuilder();
            csv.AppendLine("id;personagem;missao;gatilho;prioridade;texto;arquivo;duracao_estimada_s;producao;revisao;clip");
            int recorded = 0;
            foreach (DialogueLine line in library.Lines)
            {
                bool has = line.Clip != null;
                if (has) recorded++;
                // reading pace of the director (15 characters per second) plus a breath
                float estimate = has ? line.Clip.length : Mathf.Max(2.0f, line.Text.Length / 15.0f + 0.6f);
                csv.AppendLine(string.Join(";",
                    line.Id, DialogueLibrary.SpeakerName(line.Speaker), line.MissionId ?? "", line.Trigger, line.Priority,
                    "\"" + line.Text.Replace("\"", "\"\"") + "\"",
                    $"SoundFX/Dialogue/pt-BR/{line.Id}.wav", estimate.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                    has ? "gravada" : "pendente", has ? "a revisar" : "-", has ? AssetDatabase.GetAssetPath(line.Clip) : ""));
            }

            EnsureFolder("Assets/SourceFiles/SoundFX/Dialogue/pt-BR");
            string docs = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "Docs");
            Directory.CreateDirectory(docs);
            File.WriteAllText(Path.Combine(docs, "FASE5_DIALOGOS_MANIFESTO.csv"), csv.ToString(), new UTF8Encoding(true));
            Debug.Log($"Dialogue manifest: {library.Lines.Length} lines, {recorded} with voice, {linked} newly linked");
        }
    }
}
