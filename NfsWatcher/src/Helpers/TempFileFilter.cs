using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileWatcherSMB.src.Helpers
{
    public class TempFileFilter : ITempFileFilter
    {
        private readonly Regex[] _patterns; //o listă de expresii regulate, 
        //  fiecare fiind un pattern după care să verifici dacă un fișier e temporar/ignorabil.

        public TempFileFilter(IEnumerable<string> patterns)
        {
            _patterns = patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
                .ToArray();
        } //Primește o listă de pattern-uri (stringuri), le transformă în Regex (ignorând cazurile goale sau whitespace).

        public bool IsTemporaryOrIgnoredFile(string fullPath)
        {
            var fileName = Path.GetFileName(fullPath); //Ia doar numele fișierului (Path.GetFileName), din calea completa
            return _patterns.Any(r => r.IsMatch(fileName)); //Verifică dacă numele de fișier se potrivește cu vreunul din pattern-urile Regex din listă.
            //Dacă da, returnează true (fișierul trebuie ignorat).
        }
    }
}
