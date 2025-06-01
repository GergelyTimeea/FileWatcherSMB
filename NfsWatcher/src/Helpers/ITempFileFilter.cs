using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWatcherSMB.src.Helpers
{
    public interface ITempFileFilter
    {
        bool IsTemporaryOrIgnoredFile(string fullPath);
    }
}
