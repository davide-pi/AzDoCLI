using System.Text.Json;

namespace AzDoCLI.App.Infrastructure;

public static class AzDoConfigLoader
{
    public static AzDoConfig Load()
    {
        // Try to load from environment variables first
        var org = Environment.GetEnvironmentVariable("AZDO_ORG");
        var proj = Environment.GetEnvironmentVariable("AZDO_PROJECT");
        var pat = Environment.GetEnvironmentVariable("AZDO_PAT");
        var user = Environment.GetEnvironmentVariable("AZDO_USER_EMAIL");
        if (!string.IsNullOrWhiteSpace(org) && !string.IsNullOrWhiteSpace(proj) && !string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(user))
        {
            return new AzDoConfig
            {
                Organization = org,
                Project = proj,
                PersonalAccessToken = pat,
                UserEmail = user
            };
        }
        // Fallback: try to load from appsettings.json
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AzDoConfig>(json);
            if (config != null) return config;
        }
        throw new InvalidOperationException("Azure DevOps configuration not found. Set environment variables or provide appsettings.json.");
    }
}
