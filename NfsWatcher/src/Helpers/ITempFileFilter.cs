using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWatcherSMB.src.Helpers
{
    public interface ITempFileFilter
    {
        bool IsTemporaryOrIgnoredFile(string fullPath); //returnează true dacă fișierul dat (după cale) e temporar sau trebuie ignorat.
    }
}
//Interfață pentru o clasă care decide dacă un fișier e temporar sau trebuie ignorat.