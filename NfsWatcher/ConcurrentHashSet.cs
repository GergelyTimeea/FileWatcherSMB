using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyNamespace
{
    public class ConcurrentHashSet
    {
        private readonly ConcurrentDictionary<string, byte> _dict = new();

        public bool Add(string item) => _dict.TryAdd(item, 0);

        public bool Contains(string item) => _dict.ContainsKey(item);

        public bool Remove(string item) => _dict.TryRemove(item, out _);

        public IEnumerable<string> Items => _dict.Keys;
    }
}
