using FileWatcherSMB.Helpers;
using FileWatcherSMB.src.Helpers;
using FileWatcherSMB.Watchers;
using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FileWatcherSMB.src.Watchers
{
    public class FileWatcherWrapper : IFileWatcher, IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly IConcurrentHashSet _eventMap;
        private readonly ITempFileFilter _filter;

        public FileWatcherWrapper(string pathToWatch, IConcurrentHashSet eventMap, ITempFileFilter filter)
        {
            _eventMap = eventMap;
            _filter = filter;
            _watcher = new FileSystemWatcher(pathToWatch)
            {
                NotifyFilter = NotifyFilters.Attributes
                             | NotifyFilters.CreationTime
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.FileName
                             | NotifyFilters.LastAccess
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Security
                             | NotifyFilters.Size,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = false
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileCreated;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnFileError;
        }

        public void Start()
        {
            _watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_filter.IsIgnored(e.FullPath)) return;
            _eventMap.Add(e.FullPath);
        }
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (_filter.IsIgnored(e.FullPath)) return;
            _eventMap.Add(e.FullPath);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (_filter.IsIgnored(e.OldFullPath) || _filter.IsIgnored(e.FullPath))
                return;
            _eventMap.Add(e.FullPath);
        }

        private static void OnFileError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine($"[EROARE] {e.GetException()?.Message}");
        }

        public void Dispose()
        { 
            _watcher.Dispose(); 
        }


    }
}
