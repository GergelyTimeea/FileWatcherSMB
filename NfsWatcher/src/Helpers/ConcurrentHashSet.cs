using System.Collections.Concurrent;

namespace FileWatcherSMB.Helpers
{
    public class ConcurrentHashSet : IConcurrentHashSet
    {
        private readonly ConcurrentDictionary<string, byte> _dict = new(); //dictionar thread safe, unde cheia este string-ul (calea unui fisier), iar valoarea byte nu conteaza, se pune mereu 0.

        public bool Add(string item) => _dict.TryAdd(item, 0); //se incearca adaugarea cheii in dictionar, returneaza true daca s-a reusit.
        public bool Contains(string item) => _dict.ContainsKey(item); //verifica daca cheia exista 
        public bool Remove(string item) => _dict.TryRemove(item, out _); //se incearca stergerea cheii din dictionar, returneaza true daca s-a reusit.
        public IEnumerable<string> Items => _dict.Keys; //returneaza toate cheile din dictionar, adica toate fisierele care sunt in setul de hash concurent.
    }
}
//Pentru a ține evidența fișierelor unice, chiar dacă mai multe fire de execuție adaugă/șterg simultan