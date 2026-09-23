using System;
using System.IO;

const string TargetFolderName = "EnvironmentWebview";
const string LogFileName      = "purge-error.log";
const int    DelayMs          = 3000; // 3 secondes

try
{
    var targetDir = Path.Combine(AppContext.BaseDirectory, TargetFolderName);

    if (Directory.Exists(targetDir))
    {
        // Laisse le temps au processus parent (WebView2) de libérer ses handles
        await Task.Delay(DelayMs);

        ForceDeleteDirectory(targetDir);
    }
}
catch (Exception ex)
{
    WriteLog(ex);
}

return 0;

// ---------- Helpers ----------

static void ForceDeleteDirectory(string path)
{
    // Retire les attributs ReadOnly/Hidden/System qui bloqueraient la suppression
    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
    {
        try { File.SetAttributes(file, FileAttributes.Normal); }
        catch { /* on tentera quand même */ }
    }

    // Le dossier peut encore être verrouillé quelques instants : on retente plusieurs fois
    const int maxAttempts = 5;
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            Directory.Delete(path, recursive: true);
            return; // succès
        }
        catch (IOException) when (attempt < maxAttempts)
        {
            Thread.Sleep(500); // petite pause avant nouvelle tentative
        }
        catch (UnauthorizedAccessException) when (attempt < maxAttempts)
        {
            Thread.Sleep(500);
        }
    }

    // Dernière tentative : suppression manuelle puis dossier
    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
    {
        try { File.Delete(file); } catch { }
    }
    foreach (var sub in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
    {
        try { Directory.Delete(sub, true); } catch { }
    }
    Directory.Delete(path, recursive: true);
}

static void WriteLog(Exception ex)
{
    try
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, LogFileName);
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().Name}: {ex.Message}" +
                   $"{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}";
        File.AppendAllText(logPath, line);
    }
    catch
    {
        // Échec silencieux, conformément à l'exigence
    }
}