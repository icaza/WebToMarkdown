using System.Text;

namespace WebToMarkdown;

internal static class Logger
{
    static readonly Lock Sync = new();

    public static void Info(string path, string message) => Write(path, "INFO", message, null);
    public static void Warning(string path, string message) => Write(path, "WARN", message, null);
    public static void Error(string path, string message, Exception? exception = null) => Write(path, "ERROR", message, exception);

    static void Write(string path, string level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var sb = new StringBuilder();
            sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            sb.Append(" [").Append(level).Append("] ");
            sb.AppendLine(message);

            if (exception is not null)
            {
                sb.AppendLine(exception.ToString());
            }

            lock (Sync)
            {
                File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
        }
        catch
        {
            // 
        }
    }
}
