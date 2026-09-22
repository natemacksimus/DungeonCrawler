using System.Collections.Generic;

namespace DungeonCrawler.Core
{
    /// <summary>Fixed-slot pack. Gold never takes a slot; it goes straight to the purse.</summary>
    public sealed class Inventory
    {
        readonly List<Item> _items = new List<Item>();

        public Inventory(int capacity = 10)
        {
            Capacity = capacity;
        }

        public int Capacity { get; private set; }
        public int Count { get { return _items.Count; } }
        public bool IsFull { get { return _items.Count >= Capacity; } }
        public IReadOnlyList<Item> Items { get { return _items; } }

        public Item this[int index] { get { return _items[index]; } }

        public bool TryAdd(Item item)
        {
            if (item == null || IsFull) return false;
            _items.Add(item);
            return true;
        }

        public Item RemoveAt(int index)
        {
            if (index < 0 || index >= _items.Count) return null;
            Item item = _items[index];
            _items.RemoveAt(index);
            return item;
        }

        public bool Remove(Item item)
        {
            return _items.Remove(item);
        }

        public void Clear()
        {
            _items.Clear();
        }

        /// <summary>Index of the first item of a kind, or -1. Used by the self-play agent.</summary>
        public int FindFirst(ItemKind kind)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Def.Kind == kind) return i;
            return -1;
        }
    }
}
