using Xunit;
using FileWatcherSMB.src.Helpers;

public class TempFileFilterTests
{
    [Theory]
    [InlineData("file.tmp", true)]
    [InlineData("~$test.docx", true)]
    [InlineData("myfile.txt", false)]
    public void IsIgnored_ShouldMatchExpectedResults(string filePath, bool expected)
{
    var patterns = new List<string> { @"^~\$", @"\.tmp$" };
    var filter = new TempFileFilter(patterns);

    var result = filter.IsTemporaryOrIgnoredFile(filePath);

    Assert.Equal(expected, result);
}
}
