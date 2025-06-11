using FileWatcherSMB.src.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWatcherSMB.Tests
{
    public class TempFileFilterTests
    {
        [Theory] //Indică faptul că metoda de test poate primi mai multe seturi de date de intrare (este testată cu mai multe exemple).
        [InlineData("file.tmp", true)]
        [InlineData("~$test.docx", true)]
        [InlineData("myfile.txt", false)]
        public void IsIgnored_ShouldMatchExpectedResults(string filePath, bool expected)
        { //Testează dacă metoda IsTemporaryOrIgnoredFile din TempFileFilter returnează rezultatul corect pentru fiecare exemplu.
            var patterns = new List<string> { @"^~\$", @"\.tmp$" }; //Definim două reguli: una pentru fișierele temporare (care se termină cu .tmp) și alta pentru fișierele de tip "temporary" (care încep cu ~$).
            var filter = new TempFileFilter(patterns); //Creează un obiect care va folosi aceste reguli pentru a decide ce fișiere se ignoră.

            var result = filter.IsTemporaryOrIgnoredFile(filePath); //Apelează metoda cu numele de fișier din exemplu.

            Assert.Equal(expected, result); //Verifică dacă rezultatul obținut este exact cel așteptat (true sau false).
        }
    }
}
