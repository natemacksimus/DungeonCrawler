using System.Collections.Generic;

namespace DungeonCrawler.Core
{
    /// <summary>Bounded scrollback of combat and flavour lines, newest last.</summary>
    public sealed class MessageLog
    {
        readonly List<string> _lines = new List<string>();

        public MessageLog(int capacity = 200)
        {
            Capacity = capacity;
        }

        public int Capacity { get; private set; }
        public IReadOnlyList<string> Lines { get { return _lines; } }
        public int Count { get { return _lines.Count; } }

        public void Add(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _lines.Add(message);
            if (_lines.Count > Capacity) _lines.RemoveRange(0, _lines.Count - Capacity);
        }

        public void Clear()
        {
            _lines.Clear();
        }

        /// <summary>The last <paramref name="count"/> lines, oldest first — what the HUD shows.</summary>
        public IEnumerable<string> Tail(int count)
        {
            int start = _lines.Count - count;
            if (start < 0) start = 0;
            for (int i = start; i < _lines.Count; i++) yield return _lines[i];
        }

        public string LastLine
        {
            get { return _lines.Count == 0 ? string.Empty : _lines[_lines.Count - 1]; }
        }
    }
}
