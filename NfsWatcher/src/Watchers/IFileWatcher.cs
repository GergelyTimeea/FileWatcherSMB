namespace FileWatcherSMB.Watchers
{
    public interface IFileWatcher
    {
        void Start();
        void Stop();
    }
}// orice clasă care implementează IFileWatcher trebuie să aibă metodele Start() și Stop() 