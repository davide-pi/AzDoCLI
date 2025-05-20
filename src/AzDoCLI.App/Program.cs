using AzDoCLI.App.Domain;
using AzDoCLI.App.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.CommandLine;

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
            }).ConfigureServices((context, services) =>
            {
                services.AddSingleton<AzDoConfig>(_ => AzDoConfigLoader.Load());
                services.AddSingleton<IWorkItemService, AzureDevOpsWorkItemService>();
            });

        var host = builder.Build();
        var workItemService = host.Services.GetRequiredService<IWorkItemService>();

        var rootCommand = new RootCommand("Azure DevOps CLI");
        var periodOption = new Option<string>(
            name: "--period",
            description: "Period to list completed work items: day (@StartOfDay), week (@StartOfWeek), month (@StartOfMonth). Default is day.",
            getDefaultValue: () => "day"
        );
        periodOption.AddAlias("-p");

        AddListCompletedCommand(rootCommand, workItemService, host, periodOption);
        AddListActiveCommand(rootCommand, workItemService, host);
        AddListAllCommand(rootCommand, workItemService, host, periodOption);
        AddHelpCommand(rootCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static void AddListCompletedCommand(RootCommand rootCommand, IWorkItemService workItemService, IHost host, Option<string> periodOption)
    {
        var listCommand = new Command("list-completed", "List completed work items assigned to you for a period (default: today)")
        {
            periodOption
        };
        listCommand.SetHandler(async (string period) =>
        {
            string wiqlPeriod = period.ToLower() switch
            {
                "day" => "@StartOfDay",
                "week" => "@StartOfWeek",
                "month" => "@StartOfMonth",
                _ => "@StartOfDay"
            };

            var roots = (await ((AzureDevOpsWorkItemService)workItemService).ListCompletedWorkItemTreeAsync(wiqlPeriod)).ToList();
            if (!roots.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No work items found.[/]");
                return;
            }

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
                { "To Do", Color.Grey },
                { "In Progress", Color.Aqua },
                { "Removed", Color.Red },
                { "Implemented", Color.Pink1 }
            };

            var tree = new Tree($"[bold]Work Items[/] [grey](Total Completed: {totalCompletedWork})[/]");
            foreach (var root in roots)
            {
                var rootNode = BuildTreeNode(root, typeColors, stateColors, host.Services.GetRequiredService<AzDoConfig>());
                tree.AddNode(rootNode);
            }
            AnsiConsole.Write(tree);
        }, periodOption);
        rootCommand.AddCommand(listCommand);
    }

    private static void AddListActiveCommand(RootCommand rootCommand, IWorkItemService workItemService, IHost host)
    {
        var listActiveCommand = new Command("list-active", "List active work items assigned to you (default: today)");
        listActiveCommand.SetHandler(async () =>
        {
            var roots = (await ((AzureDevOpsWorkItemService)workItemService).ListActiveWorkItemTreeAsync()).ToList();
            if (!roots.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No active work items found.[/]");
                return;
            }

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
                { "To Do", Color.Grey },
                { "In Progress", Color.Aqua },
                { "Removed", Color.Red },
                { "Implemented", Color.Pink1 }
            };

            var tree = new Tree($"[bold]Work Items[/] [grey](Total Completed: {totalCompletedWork})[/]");
            foreach (var root in roots)
            {
                var rootNode = BuildTreeNode(root, typeColors, stateColors, host.Services.GetRequiredService<AzDoConfig>());
                tree.AddNode(rootNode);
            }
            AnsiConsole.Write(tree);
        });
        rootCommand.AddCommand(listActiveCommand);
    }

    private static void AddListAllCommand(RootCommand rootCommand, IWorkItemService workItemService, IHost host, Option<string> periodOption)
    {
        var listAllCommand = new Command("list-all", "List all (completed and active) work items assigned to you for a period (default: today)")
        {
            periodOption
        };
        listAllCommand.SetHandler(async (string period) =>
        {
            string wiqlPeriod = period.ToLower() switch
            {
                "day" => "@StartOfDay",
                "week" => "@StartOfWeek",
                "month" => "@StartOfMonth",
                _ => "@StartOfDay"
            };

            var completed = (await ((AzureDevOpsWorkItemService)workItemService).ListCompletedWorkItemTreeAsync(wiqlPeriod)).ToList();
            var active = (await ((AzureDevOpsWorkItemService)workItemService).ListActiveWorkItemTreeAsync(wiqlPeriod)).ToList();

            var allRoots = new Dictionary<int, WorkItemTreeNode>();
            void AddTree(WorkItemTreeNode node)
            {
                if (!allRoots.ContainsKey(node.Item.Id))
                {
                    allRoots[node.Item.Id] = node;
                }
            }
            foreach (var node in completed)
                AddTree(node);
            foreach (var node in active)
                AddTree(node);

            var mergedRoots = allRoots.Values.ToList();

            if (!mergedRoots.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No work items found.[/]");
                return;
            }

            double totalCompletedWork = 0;
            void SumCompletedWork(WorkItemTreeNode node)
            {
                if (node.Item.Type == "Task" && node.Item.CompletedWork.HasValue)
                    totalCompletedWork += node.Item.CompletedWork.Value;
                foreach (var child in node.Children)
                    SumCompletedWork(child);
            }
            foreach (var root in mergedRoots)
                SumCompletedWork(root);

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
            foreach (var root in mergedRoots)
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
                { "To Do", Color.Grey },
                { "In Progress", Color.Aqua },
                { "Removed", Color.Red },
                { "Implemented", Color.Pink1 }
            };

            var tree = new Tree($"[bold]Work Items[/] [grey](Total Completed: {totalCompletedWork})[/]");
            foreach (var root in mergedRoots)
            {
                var rootNode = BuildTreeNode(root, typeColors, stateColors, host.Services.GetRequiredService<AzDoConfig>());
                tree.AddNode(rootNode);
            }
            AnsiConsole.Write(tree);
        }, periodOption);
        rootCommand.AddCommand(listAllCommand);
    }

    private static void AddHelpCommand(RootCommand rootCommand)
    {
        var helpCommand = new Command("help", "Show help and list available commands");
        helpCommand.SetHandler(() =>
        {
            AnsiConsole.MarkupLine("[bold yellow]Available Commands:[/]");
            foreach (var cmd in rootCommand.Children.OfType<Command>())
            {
                AnsiConsole.MarkupLine($"[green]{cmd.Name}[/]: {cmd.Description}");
                if (cmd.Name == "list-completed" || cmd.Name == "list-all")
                {
                    AnsiConsole.MarkupLine("    [blue]--period[/], [blue]-p[/]: Period to list work items. Possible values: [yellow]day[/] (default), [yellow]week[/], [yellow]month[/]");
                }
            }
        });
        rootCommand.AddCommand(helpCommand);
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
