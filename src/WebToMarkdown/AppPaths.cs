namespace WebToMarkdown;

internal sealed class AppPaths
{
    public string BaseDirectory { get; }
    public string ReportsDirectory { get; }
    public string LogsDirectory { get; }
    public string ConfigPath { get; }
    public string LogFilePath { get; }

    private AppPaths(string baseDirectory)
    {
        BaseDirectory = baseDirectory;
        ReportsDirectory = Path.Combine(baseDirectory, "reports_");
        LogsDirectory = Path.Combine(baseDirectory, "logs_");
        ConfigPath = Path.Combine(baseDirectory, "config.json");
        LogFilePath = Path.Combine(LogsDirectory, "app.log");
    }

    public static AppPaths Initialize()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var paths = new AppPaths(baseDirectory);
        Directory.CreateDirectory(paths.ReportsDirectory);
        Directory.CreateDirectory(paths.LogsDirectory);
        return paths;
    }
}
