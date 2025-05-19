namespace AzDoCLI.App.Domain;

public interface IWorkItemService
{
    Task<IEnumerable<WorkItemTreeNode>> ListWorkItemTreeAsync();
    Task<WorkItem> CreateWorkItemAsync(string title, string type, string assignedTo);
    Task<WorkItem> UpdateWorkItemAsync(int id, string? title = null, string? state = null, string? assignedTo = null);
}
