﻿using FileWatcherSMB.Helpers;
using FileWatcherSMB.src.Helpers;
using FileWatcherSMB.Watchers;
using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.Extensions.Logging;

namespace FileWatcherSMB.src.Watchers
{
    public class FileWatcherWrapper : IFileWatcher, IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly IConcurrentHashSet _eventMap;
        private readonly ITempFileFilter _filter;
        private readonly ILogger<FileWatcherWrapper> _logger;

        public FileWatcherWrapper(string pathToWatch, IConcurrentHashSet eventMap, ITempFileFilter filter, ILogger<FileWatcherWrapper> logger)
        {
            _eventMap = eventMap;
            _filter = filter;
            _logger = logger;

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
            if (_filter.IsTemporaryOrIgnoredFile(e.FullPath)) return;
            _eventMap.Add(e.FullPath);
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (_filter.IsTemporaryOrIgnoredFile(e.FullPath)) return;
            _eventMap.Add(e.FullPath);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (_filter.IsTemporaryOrIgnoredFile(e.OldFullPath) || _filter.IsTemporaryOrIgnoredFile(e.FullPath))
                return;

            _eventMap.Add(e.FullPath);
        }

        private void OnFileError(object sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "Eroare la monitorizarea fișierului.");
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }
    }
}