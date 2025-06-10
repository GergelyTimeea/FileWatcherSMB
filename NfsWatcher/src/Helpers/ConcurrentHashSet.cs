using System.Collections.Concurrent;

namespace FileWatcherSMB.Helpers
{
    public class ConcurrentHashSet : IConcurrentHashSet
    {
        private readonly ConcurrentDictionary<string, byte> _dict = new();

        public bool Add(string item) => _dict.TryAdd(item, 0);
        public bool Contains(string item) => _dict.ContainsKey(item);
        public bool Remove(string item) => _dict.TryRemove(item, out _);
        public IEnumerable<string> Items => _dict.Keys;
    }
}