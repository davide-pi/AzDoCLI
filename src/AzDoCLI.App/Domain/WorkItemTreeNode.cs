namespace AzDoCLI.App.Domain;

public class WorkItemTreeNode
{
    public WorkItem Item { get; set; } = null!;
    public List<WorkItemTreeNode> Children { get; set; } = new();
}