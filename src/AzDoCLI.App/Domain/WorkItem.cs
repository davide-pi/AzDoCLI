namespace AzDoCLI.App.Domain;

public class WorkItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double? CompletedWork { get; set; } // nullable, only for tasks
}
