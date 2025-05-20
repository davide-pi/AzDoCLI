using System.Text.Json;

namespace AzDoCLI.App.Infrastructure;

public static class AzDoConfigLoader
{
    public static AzDoConfig Load()
    {
        // Try to load from environment variables
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

        throw new InvalidOperationException("Azure DevOps configuration not found. Set environment variables.");
    }
}
