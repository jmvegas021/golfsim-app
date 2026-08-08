namespace GsproLighting.Tests;

internal sealed class TestDirectory : IDisposable
{
    public TestDirectory()
    {
        RootPath = Path.Combine(
            Path.GetTempPath(),
            $"gspro-lighting-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string GetPath(string fileName) =>
        Path.Combine(RootPath, fileName);

    public string Write(string fileName, string content)
    {
        var filePath = GetPath(fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(RootPath, recursive: true);
        }
        catch
        {
            // Temporary test cleanup must not hide the test result.
        }
    }
}
