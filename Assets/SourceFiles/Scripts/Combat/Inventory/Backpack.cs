using System;
using UnityEngine;

namespace Combat
{
    [Serializable]
    public class ItemStack
    {
        public string Id;
        public int Count;

        public ItemDefinition Definition => ItemCatalog.Find(Id);
        public int MaxStack => Mathf.Max(1, Definition != null ? Definition.MaxStack : 1);
    }

    /* The backpack: a fixed number of slots holding stacks of catalog items (materials, spare
     * quick items, bandages...). It lives for one run like RunState; the quick bar
     * (InventoryManager) and the two guns keep their own rules, and mission items stay in
     * RunState so a full backpack can never block the story. Adding is all-or-what-fits and
     * removing is all-or-nothing, so nothing is lost or duplicated half way.
     */
    public static class Backpack
    {
        public const int DefaultSlots = 18;

        private static ItemStack[] _slots = new ItemStack[DefaultSlots];
        public static event Action Changed;

        public static int SlotCount => _slots.Length;
        public static ItemStack Slot(int index) => index >= 0 && index < _slots.Length ? _slots[index] : null;

        public static int UsedSlots
        {
            get
            {
                int used = 0;
                foreach (ItemStack stack in _slots) if (stack != null) used++;
                return used;
            }
        }

        public static void Reset(int slots = DefaultSlots)
        {
            _slots = new ItemStack[Mathf.Max(1, slots)];
            Changed?.Invoke();
        }

        public static int Count(string id)
        {
            int total = 0;
            foreach (ItemStack stack in _slots) if (stack != null && stack.Id == id) total += stack.Count;
            return total;
        }

        // how many of this item still fit, topping up stacks first
        public static int RoomFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            int max = MaxStackOf(id);
            int room = 0;
            foreach (ItemStack stack in _slots)
            {
                if (stack == null) room += max;
                else if (stack.Id == id) room += Mathf.Max(0, max - stack.Count);
            }

            return room;
        }

        // returns how many went in
        public static int Add(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return 0;
            int max = MaxStackOf(id);
            int left = amount;
            foreach (ItemStack stack in _slots)
            {
                if (left <= 0) break;
                if (stack == null || stack.Id != id || stack.Count >= max) continue;
                int put = Mathf.Min(left, max - stack.Count);
                stack.Count += put;
                left -= put;
            }

            for (int i = 0; i < _slots.Length && left > 0; i++)
            {
                if (_slots[i] != null) continue;
                int put = Mathf.Min(left, max);
                _slots[i] = new ItemStack { Id = id, Count = put };
                left -= put;
            }

            int added = amount - left;
            if (added > 0) Changed?.Invoke();
            return added;
        }

        // all or nothing, taken from the last stacks first so the first slots stay full
        public static bool Remove(string id, int amount)
        {
            if (amount <= 0) return true;
            if (Count(id) < amount) return false;
            int left = amount;
            for (int i = _slots.Length - 1; i >= 0 && left > 0; i--)
            {
                ItemStack stack = _slots[i];
                if (stack == null || stack.Id != id) continue;
                int take = Mathf.Min(left, stack.Count);
                stack.Count -= take;
                left -= take;
                if (stack.Count <= 0) _slots[i] = null;
            }

            Changed?.Invoke();
            return true;
        }

        public static bool RemoveAt(int index, int amount)
        {
            ItemStack stack = Slot(index);
            if (stack == null || amount <= 0 || stack.Count < amount) return false;
            stack.Count -= amount;
            if (stack.Count <= 0) _slots[index] = null;
            Changed?.Invoke();
            return true;
        }

        // half of the stack into the first free slot
        public static bool Split(int index)
        {
            ItemStack stack = Slot(index);
            if (stack == null || stack.Count < 2) return false;
            int free = Array.IndexOf(_slots, null);
            if (free < 0) return false;
            int half = stack.Count / 2;
            stack.Count -= half;
            _slots[free] = new ItemStack { Id = stack.Id, Count = half };
            Changed?.Invoke();
            return true;
        }

        // drag and drop: onto the same item they merge, otherwise they swap places
        public static void Move(int from, int to)
        {
            if (from == to || Slot(from) == null || to < 0 || to >= _slots.Length) return;
            ItemStack source = _slots[from];
            ItemStack target = _slots[to];
            if (target != null && target.Id == source.Id)
            {
                int put = Mathf.Min(source.Count, target.MaxStack - target.Count);
                target.Count += put;
                source.Count -= put;
                if (source.Count <= 0) _slots[from] = null;
            }
            else
            {
                _slots[to] = source;
                _slots[from] = target;
            }

            Changed?.Invoke();
        }

        // merges partial stacks and groups by category, then by name
        public static void Sort()
        {
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (ItemStack stack in _slots)
            {
                if (stack == null) continue;
                counts.TryGetValue(stack.Id, out int c);
                counts[stack.Id] = c + stack.Count;
            }

            var ids = new System.Collections.Generic.List<string>(counts.Keys);
            ids.Sort((a, b) =>
            {
                ItemDefinition da = ItemCatalog.Find(a), db = ItemCatalog.Find(b);
                int ca = da != null ? (int)da.Category : 99, cb = db != null ? (int)db.Category : 99;
                return ca != cb ? ca.CompareTo(cb) : string.Compare(ItemCatalog.NameOf(a), ItemCatalog.NameOf(b), StringComparison.Ordinal);
            });

            Array.Clear(_slots, 0, _slots.Length);
            int slot = 0;
            foreach (string id in ids)
            {
                int left = counts[id];
                int max = MaxStackOf(id);
                while (left > 0 && slot < _slots.Length)
                {
                    int put = Mathf.Min(left, max);
                    _slots[slot++] = new ItemStack { Id = id, Count = put };
                    left -= put;
                }
            }

            Changed?.Invoke();
        }

        public static int MaxStackOf(string id)
        {
            ItemDefinition definition = ItemCatalog.Find(id);
            return Mathf.Max(1, definition != null ? definition.MaxStack : 1);
        }

        // the quick bar item an id stands for, if it is one
        public static bool TryQuickItem(string id, out QuickItem item)
        {
            foreach (QuickItem candidate in (QuickItem[])Enum.GetValues(typeof(QuickItem)))
            {
                if (ItemCatalog.IdOf(candidate) == id)
                {
                    item = candidate;
                    return true;
                }
            }

            item = default;
            return false;
        }
    }
}
