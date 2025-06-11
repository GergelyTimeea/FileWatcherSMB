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
using Microsoft.Extensions.Logging;

namespace FileWatcherSMB.src.Watchers
{
    public class FileWatcherWrapper : IFileWatcher, IDisposable
    {
        private readonly FileSystemWatcher _watcher; //Obiectul care urmărește modificările din sistemul de fișiere (creare, modificare, redenumire etc).
        private readonly IConcurrentHashSet _eventMap;// Un set thread-safe pentru a colecta și deduplica evenimentele 
        // (fiecare cale de fișier modificat e stocată o singură dată).
        private readonly ITempFileFilter _filter;//Un filtru care decide dacă un fișier trebuie ignorat (ex: fișiere temporare sau de sistem).
        private readonly ILogger<FileWatcherWrapper> _logger;//Pentru logarea/înregistrarea erorilor sau a informațiilor utile.

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
                Filter = "*.*", //se monitorizeaza toate tipurile de fisiere
                IncludeSubdirectories = true, //monitirizeaza și subdirectoarele
                EnableRaisingEvents = false //Inițial, nu pornește monitorizarea
            };
            _watcher.Changed += OnFileChanged; //Atașează metodele care tratează evenimentele: Changed, Created, Renamed și Error.
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
            if (_filter.IsTemporaryOrIgnoredFile(e.FullPath)) return; //Dacă fișierul este temporar sau ignorat (verificat de _filter)
            // , nu se face nimic.
            _eventMap.Add(e.FullPath); //Dacă nu, se adaugă calea fișierului în _eventMap (setul de evenimente),
            //  pentru a fi procesat ulterior.
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
            _logger.LogError(e.GetException(), "Eroare la monitorizarea fișierului."); //Dacă apare o eroare la monitorizare, 
            // loghează eroarea pentru depanare.
        }

        public void Dispose()
        {
            _watcher.Dispose(); //Eliberează resursele ocupate de FileSystemWatcher când obiectul nu mai e folosit.

        }
    }
}