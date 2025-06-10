namespace FileWatcherSMB.Helpers
{
    public interface IConcurrentHashSet
    {
        bool Add(string item);
        bool Remove(string item);
        bool Contains(string item);
        IEnumerable<string> Items { get; }
    }
}