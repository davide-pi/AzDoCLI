using System.CommandLine;
using AzDoCLI.App.Infrastructure;
using AzDoCLI.App.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace AzDoCLI.App;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<AzDoConfig>(context.Configuration.GetSection("AzureDevOps"));
                services.AddSingleton<AzDoConfig>(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzDoConfig>>().Value);
                services.AddSingleton<IWorkItemService, AzureDevOpsWorkItemService>();
            });

        var host = builder.Build();
        var workItemService = host.Services.GetRequiredService<IWorkItemService>();

        var rootCommand = new RootCommand("Azure DevOps CLI");
        var listCommand = new Command("list-completed", "List completed work items assigned to you today");
        listCommand.SetHandler(async () =>
        {
            var roots = (await workItemService.ListWorkItemTreeAsync()).ToList();
            if (!roots.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No work items found.[/]");
                return;
            }

            // Calculate total completed work for all tasks
            double totalCompletedWork = 0;
            void SumCompletedWork(WorkItemTreeNode node)
            {
                if (node.Item.Type == "Task" && node.Item.CompletedWork.HasValue)
                    totalCompletedWork += node.Item.CompletedWork.Value;
                foreach (var child in node.Children)
                    SumCompletedWork(child);
            }
            foreach (var root in roots)
                SumCompletedWork(root);

            // Calculate completed work for parent nodes recursively (sum only children's CompletedWork, not own if parent is also a Task)
            double CalculateAndSetParentCompletedWork(WorkItemTreeNode node)
            {
                if (node.Children.Count == 0)
                    return node.Item.Type == "Task" && node.Item.CompletedWork.HasValue ? node.Item.CompletedWork.Value : 0;
                double sum = 0;
                foreach (var child in node.Children)
                    sum += CalculateAndSetParentCompletedWork(child);
                node.Item.CompletedWork = sum > 0 ? sum : null;
                return sum;
            }
            foreach (var root in roots)
                CalculateAndSetParentCompletedWork(root);

            var typeColors = new Dictionary<string, Color>
            {
                { "Task", Color.Yellow },
                { "Feature", Color.Purple },
                { "Epic", Color.Orange3 },
                { "Bug", Color.Red },
                { "Tech", Color.Grey },
                { "Impediment", Color.Pink1 }
            };
            var stateColors = new Dictionary<string, Color>
            {
                { "Done", Color.Green },
                { "Closed", Color.Green },
                { "Active", Color.Yellow },
                { "Committed", Color.Aqua },
                { "Ready", Color.Orange3 },
                { "To Do", Color.Grey }
            };

            var tree = new Tree($"[bold]Work Items[/] [grey](Total Completed: {totalCompletedWork})[/]");
            foreach (var root in roots)
            {
                var rootNode = BuildTreeNode(root, typeColors, stateColors, host.Services.GetRequiredService<AzDoConfig>());
                tree.AddNode(rootNode);
            }
            AnsiConsole.Write(tree);
        });
        rootCommand.AddCommand(listCommand);

        // Add help command
        var helpCommand = new Command("help", "Show help and list available commands");
        helpCommand.SetHandler(() =>
        {
            AnsiConsole.MarkupLine("[bold yellow]Available Commands:[/]");
            foreach (var cmd in rootCommand.Children.OfType<Command>())
            {
                AnsiConsole.MarkupLine($"[green]{cmd.Name}[/]: {cmd.Description}");
            }
        });
        rootCommand.AddCommand(helpCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static TreeNode BuildTreeNode(WorkItemTreeNode node, Dictionary<string, Color> typeColors, Dictionary<string, Color> stateColors, AzDoConfig config)
    {
        var item = node.Item;
        var typeColor = typeColors.TryGetValue(item.Type, out var tc) ? tc : Color.White;
        var stateColor = stateColors.TryGetValue(item.State, out var sc) ? sc : Color.White;
        var itemUrl = $"https://dev.azure.com/{config.Organization}/{config.Project}/_workitems/edit/{item.Id}";
        var treeNode = new TreeNode(new Markup($"[bold {typeColor}][link={itemUrl}]{item.Id}[/][/] {item.Title} [bold {stateColor}][[{item.State}]][/] [grey]({item.CompletedWork})[/]"));
        foreach (var child in node.Children)
        {
            treeNode.AddNode(BuildTreeNode(child, typeColors, stateColors, config));
        }
        return treeNode;
    }
}
