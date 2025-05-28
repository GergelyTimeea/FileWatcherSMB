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
        private readonly Regex[] _patterns;

        public TempFileFilter(IEnumerable<string> patterns)
        {
            _patterns = patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
                .ToArray();
        }

        public bool IsIgnored(string fullPath)
        {
            var fileName = Path.GetFileName(fullPath);
            return _patterns.Any(r => r.IsMatch(fileName));
        }
    }
}
