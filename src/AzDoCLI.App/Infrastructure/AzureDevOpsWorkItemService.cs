using System.Net.Http.Headers;
using System.Text.Json;
using AzDoCLI.App.Domain;

namespace AzDoCLI.App.Infrastructure;

public class AzureDevOpsWorkItemService : IWorkItemService
{
    private readonly AzDoConfig _config;
    private readonly HttpClient _httpClient;

    public AzureDevOpsWorkItemService(AzDoConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();
        var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{_config.PersonalAccessToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
    }

    public async Task<IEnumerable<WorkItemTreeNode>> ListWorkItemTreeAsync()
    {
        var wiql = new
        {
            query = $@"SELECT [System.Id]
                  FROM WorkItemLinks
                  WHERE
                    (
                        [Target].[System.AssignedTo] = '{_config.UserEmail}'
                        AND [Target].[System.State] = 'Done'
                        AND [Target].[Closed Date] >= @StartOfDay
                        AND [Target].[System.WorkItemType] = 'Task'
                    )
                    AND [System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward'
                    AND [Source].[System.WorkItemType] <> ''"
        };
        var wiqlJson = JsonSerializer.Serialize(wiql);
        var url = $"https://dev.azure.com/{_config.Organization}/{_config.Project}/_apis/wit/wiql?api-version=7.0";
        var response = await _httpClient.PostAsync(url, new StringContent(wiqlJson, System.Text.Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Azure DevOps WIQL query failed: {response.StatusCode} - {error}");
        }
        var resultJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(resultJson);
        var workItemIds = new HashSet<int>();
        var linkIds = new List<(int? SourceId, int? TargetId)>();
        if (doc.RootElement.TryGetProperty("workItemRelations", out var relations))
        {
            foreach (var rel in relations.EnumerateArray())
            {
                int? sourceId = rel.TryGetProperty("source", out var src) && src.ValueKind == JsonValueKind.Object && src.TryGetProperty("id", out var srcIdProp) ? srcIdProp.GetInt32() : (int?)null;
                int? targetId = rel.TryGetProperty("target", out var tgt) && tgt.ValueKind == JsonValueKind.Object && tgt.TryGetProperty("id", out var tgtIdProp) ? tgtIdProp.GetInt32() : (int?)null;
                if (sourceId.HasValue) workItemIds.Add(sourceId.Value);
                if (targetId.HasValue) workItemIds.Add(targetId.Value);
                linkIds.Add((sourceId, targetId));
            }
        }
        if (!workItemIds.Any()) return Array.Empty<WorkItemTreeNode>();
        var idsStr = string.Join(",", workItemIds);
        var detailsUrl = $"https://dev.azure.com/{_config.Organization}/{_config.Project}/_apis/wit/workitems?ids={idsStr}&api-version=7.0";
        var detailsResp = await _httpClient.GetAsync(detailsUrl);
        if (!detailsResp.IsSuccessStatusCode)
        {
            var error = await detailsResp.Content.ReadAsStringAsync();
            throw new Exception($"Azure DevOps work item details failed: {detailsResp.StatusCode} - {error}");
        }
        var detailsJson = await detailsResp.Content.ReadAsStringAsync();
        using var detailsDoc = JsonDocument.Parse(detailsJson);
        var workItemsDict = new Dictionary<int, WorkItem>();
        foreach (var item in detailsDoc.RootElement.GetProperty("value").EnumerateArray())
        {
            var fields = item.GetProperty("fields");
            var wi = new WorkItem
            {
                Id = item.GetProperty("id").GetInt32(),
                Title = fields.GetProperty("System.Title").GetString() ?? string.Empty,
                State = fields.GetProperty("System.State").GetString() ?? string.Empty,
                AssignedTo = fields.TryGetProperty("System.AssignedTo", out var assigned) ? assigned.GetProperty("displayName").GetString() ?? string.Empty : string.Empty,
                Type = fields.GetProperty("System.WorkItemType").GetString() ?? string.Empty,
                CompletedWork = fields.TryGetProperty("Microsoft.VSTS.Scheduling.CompletedWork", out var completedWorkProp) ? completedWorkProp.GetDouble() : (double?)null
            };
            workItemsDict[wi.Id] = wi;
        }
        // If there are no parent-child links, treat all items as roots (flat tree)
        if (linkIds.Count == 0)
        {
            return workItemsDict.Values.Select(wi => new WorkItemTreeNode { Item = wi }).ToList();
        }
        // Build children lookup
        var childrenLookup = new Dictionary<int, List<int>>();
        foreach (var id in workItemsDict.Keys)
            childrenLookup[id] = new List<int>();
        foreach (var (parent, child) in linkIds)
        {
            if (parent.HasValue && child.HasValue && childrenLookup.ContainsKey(parent.Value))
                childrenLookup[parent.Value].Add(child.Value);
        }
        // Find root nodes (those that are not children)
        var allChildren = new HashSet<int>(linkIds.Where(l => l.SourceId.HasValue).Select(l => l.TargetId.Value));
        var roots = workItemsDict.Keys.Where(id => !allChildren.Contains(id)).ToList();
        // If no roots found, treat all as roots (defensive for orphaned nodes)
        if (roots.Count == 0)
            roots = workItemsDict.Keys.ToList();
        // Build tree
        var result = new List<WorkItemTreeNode>();
        foreach (var rootId in roots)
        {
            result.Add(BuildTreeNode(rootId, workItemsDict, childrenLookup));
        }
        return result;
    }

    private static WorkItemTreeNode BuildTreeNode(int id, Dictionary<int, WorkItem> items, Dictionary<int, List<int>> childrenLookup)
    {
        var node = new WorkItemTreeNode { Item = items[id] };
        foreach (var childId in childrenLookup[id])
        {
            node.Children.Add(BuildTreeNode(childId, items, childrenLookup));
        }
        return node;
    }
    public Task<WorkItem> CreateWorkItemAsync(string title, string type, string assignedTo)
        => throw new NotImplementedException();
    public Task<WorkItem> UpdateWorkItemAsync(int id, string? title = null, string? state = null, string? assignedTo = null)
        => throw new NotImplementedException();
}
