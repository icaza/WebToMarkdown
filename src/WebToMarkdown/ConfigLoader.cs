using System.Text.Json;

namespace WebToMarkdown;

internal static class ConfigLoader
{
    public static AiConfig LoadOrCreate(string path, string logPath)
    {
        try
        {
            if (!File.Exists(path))
            {
                var defaults = new AiConfig();
                var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json + Environment.NewLine);
                return defaults;
            }

            var text = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AiConfig>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AiConfig();

            config.BaseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "http://localhost:11434" : config.BaseUrl.Trim();
            config.Model = string.IsNullOrWhiteSpace(config.Model) ? "qwen2.5:1.5b" : config.Model.Trim();
            config.TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, 1, 300);

            var apiKey = Environment.GetEnvironmentVariable("OLLAMA_API_KEY");

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                config.ApiKey = apiKey;
            }
            else
            {
                config.ApiKey ??= string.Empty;
            }

            return config;
        }
        catch (Exception ex)
        {
            Logger.Error(logPath, "Unable to load config.json. AI will be disabled for this run.", ex);
            return new AiConfig { Enabled = false };
        }
    }
}
