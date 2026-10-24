using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* The survivor's clothes: each category picks which clothing meshes are shown and with which material.
     * The choice is saved, so it carries over between games.
     */
    public class PlayerOutfit : MonoBehaviour
    {
        [Tooltip("Every material the outfit options can use, looked up by name")]
        public Material[] Palette;

        public class Option
        {
            public string Label;
            // clothing mesh name -> materials for its slots
            public readonly Dictionary<string, string[]> Show = new Dictionary<string, string[]>();
        }

        public class Category
        {
            public string Label;
            // meshes this category controls; those not shown by the chosen option are hidden
            public string[] Parts;
            public readonly List<Option> Options = new List<Option>();
        }

        public readonly List<Category> Categories = new List<Category>();

        private readonly Dictionary<string, SkinnedMeshRenderer> _parts = new Dictionary<string, SkinnedMeshRenderer>();
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private int[] _choice;

        private const string PrefsKey = "Combat.Outfit.";

        private void Awake()
        {
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true)) _parts[renderer.name] = renderer;
            if (Palette != null)
            {
                foreach (Material material in Palette)
                {
                    if (material != null) _materials[material.name] = material;
                }
            }

            DefineCategories();
            _choice = new int[Categories.Count];
            for (int i = 0; i < Categories.Count; i++)
            {
                _choice[i] = Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey + i, DefaultChoice(i)), 0, Categories[i].Options.Count - 1);
                // a look that isn't earned (any more) falls back to the default one
                if (!IsUnlocked(i, _choice[i])) _choice[i] = DefaultChoice(i);
            }

            ApplyAll();
        }

        private void DefineCategories()
        {
            Category shirt = Add("CAMISA", "SK_Military_Shirt3");
            for (int i = 1; i <= 3; i++) shirt.Options.Add(One($"Estilo {i}", "SK_Military_Shirt3", $"Shirt3_Skin{i}"));

            Category pants = Add("CALÇA", "SK_Military_Pants2");
            for (int i = 1; i <= 3; i++) pants.Options.Add(One($"Estilo {i}", "SK_Military_Pants2", $"Pants2_Skin{i}"));

            Category boots = Add("BOTAS", "SK_Military_Boots3");
            for (int i = 1; i <= 3; i++) boots.Options.Add(One($"Estilo {i}", "SK_Military_Boots3", $"Boots3_Skin{i}"));

            Category cap = Add("BONÉ", "SK_Military_Cap1");
            cap.Options.Add(new Option { Label = "Nenhum" });
            for (int i = 1; i <= 4; i++) cap.Options.Add(One($"Boné {i}", "SK_Military_Cap1", $"Cap1_Skin{i}"));

            Category vest = Add("COLETE", "SK_Military_Vest1", "SK_Military_Vest1_4");
            vest.Options.Add(new Option { Label = "Nenhum" });
            for (int i = 1; i <= 3; i++) vest.Options.Add(One($"Colete {i}", "SK_Military_Vest1", $"Vest1_Skin{i}"));
            for (int i = 1; i <= 3; i++)
            {
                var tactical = new Option { Label = $"Tático {i}" };
                tactical.Show["SK_Military_Vest1_4"] = new[] { $"Vest1_Skin{i}", $"Pouches1_Skin{i}" };
                vest.Options.Add(tactical);
            }

            Category backpack = Add("MOCHILA", "SK_Military_Backpack1", "SK_Military_Backpack1_vest");
            backpack.Options.Add(new Option { Label = "Nenhuma" });
            for (int i = 1; i <= 4; i++) backpack.Options.Add(One($"Mochila {i}", "SK_Military_Backpack1", $"Backpack1_Skin{i}"));
            for (int i = 1; i <= 4; i++) backpack.Options.Add(One($"Cargueira {i}", "SK_Military_Backpack1_vest", $"Backpack1_Skin{i}"));

            Category hands = Add("LUVAS", "SK_Military_Male_Arms1", "SK_Military_Male_Arms1_Gloves1");
            hands.Options.Add(One("Sem luvas", "SK_Military_Male_Arms1", "Hands1_Skin1"));
            for (int i = 1; i <= 2; i++)
            {
                var gloves = new Option { Label = $"Luvas {i}" };
                gloves.Show["SK_Military_Male_Arms1_Gloves1"] = new[] { $"Gloves1_Skin{i}", "Hands1_Skin1" };
                hands.Options.Add(gloves);
            }
        }

        private Category Add(string label, params string[] parts)
        {
            var category = new Category { Label = label, Parts = parts };
            Categories.Add(category);
            return category;
        }

        private static Option One(string label, string part, string material)
        {
            var option = new Option { Label = label };
            option.Show[part] = new[] { material };
            return option;
        }

        // a survivor look out of the box: cap, tactical vest, gloves
        private static int DefaultChoice(int category)
        {
            switch (category)
            {
                case 3: return 2;
                case 4: return 4;
                case 6: return 1;
                default: return 0;
            }
        }

        public string CurrentLabel(int category)
        {
            return Categories[category].Options[_choice[category]].Label;
        }

        public bool IsUnlocked(int category, int option)
        {
            return Progression.OutfitUnlocked(Categories[category].Label, Categories[category].Options[option].Label, out _);
        }

        // how many looks of a category are still locked, and what the next one asks for
        public string LockedHint(int category)
        {
            int locked = 0;
            string requirement = null;
            Category c = Categories[category];
            for (int i = 0; i < c.Options.Count; i++)
            {
                if (Progression.OutfitUnlocked(c.Label, c.Options[i].Label, out string needs)) continue;
                locked++;
                requirement ??= needs;
            }

            return locked == 0 ? "" : $"{locked} bloqueado(s): {requirement}";
        }

        public void Cycle(int category, int step)
        {
            int count = Categories[category].Options.Count;
            // locked looks are skipped
            int next = _choice[category];
            for (int i = 0; i < count; i++)
            {
                next = (next + step + count) % count;
                if (IsUnlocked(category, next)) break;
            }

            _choice[category] = next;
            PlayerPrefs.SetInt(PrefsKey + category, _choice[category]);
            PlayerPrefs.Save();
            Apply(category);
        }

        private void ApplyAll()
        {
            for (int i = 0; i < Categories.Count; i++) Apply(i);
        }

        private void Apply(int index)
        {
            Category category = Categories[index];
            Option option = category.Options[_choice[index]];
            foreach (string partName in category.Parts)
            {
                if (!_parts.TryGetValue(partName, out SkinnedMeshRenderer part)) continue;

                bool shown = option.Show.TryGetValue(partName, out string[] materialNames);
                part.gameObject.SetActive(shown);
                if (!shown) continue;

                Material[] materials = part.sharedMaterials;
                for (int slot = 0; slot < materials.Length && slot < materialNames.Length; slot++)
                {
                    if (_materials.TryGetValue(materialNames[slot], out Material material)) materials[slot] = material;
                }

                part.sharedMaterials = materials;
            }
        }
    }
}
