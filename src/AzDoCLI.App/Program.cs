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

        var validTypes = new[] { "completed", "active", "all" };
        var listTypeArgument = new Argument<string>(
            name: "type",
            description: "Type of work items to list: completed, active, or all (default: all)",
            getDefaultValue: () => "all"
        );
        var listCommand = new Command("list", "List work items: completed, active, or all")
        {
            listTypeArgument,
            periodOption
        };
        listCommand.SetHandler(async (string type, string period) =>
        {
            if (!string.IsNullOrWhiteSpace(type) && !validTypes.Contains(type.ToLower()))
            {
                AnsiConsole.MarkupLine($"[red]Invalid type: '{type}'. Valid values are: completed, active, all.[/]");
                return;
            }
            string wiqlPeriod = period.ToLower() switch
            {
                "day" => "@StartOfDay",
                "week" => "@StartOfWeek",
                "month" => "@StartOfMonth",
                _ => "@StartOfDay"
            };
            var config = host.Services.GetRequiredService<AzDoConfig>();
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
            List<WorkItemTreeNode> roots = new();
            if (type.Equals("completed", StringComparison.OrdinalIgnoreCase))
            {
                roots = (await ((AzureDevOpsWorkItemService)workItemService).ListCompletedWorkItemTreeAsync(wiqlPeriod)).ToList();
            }
            else if (type.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                roots = (await ((AzureDevOpsWorkItemService)workItemService).ListActiveWorkItemTreeAsync(wiqlPeriod)).ToList();
            }
            else // all or default
            {
                var completed = (await ((AzureDevOpsWorkItemService)workItemService).ListCompletedWorkItemTreeAsync(wiqlPeriod)).ToList();
                var active = (await ((AzureDevOpsWorkItemService)workItemService).ListActiveWorkItemTreeAsync(wiqlPeriod)).ToList();
                var allRoots = new Dictionary<int, WorkItemTreeNode>();
                void AddTree(WorkItemTreeNode node)
                {
                    if (!allRoots.ContainsKey(node.Item.Id))
                        allRoots[node.Item.Id] = node;
                }
                foreach (var node in completed) AddTree(node);
                foreach (var node in active) AddTree(node);
                roots = allRoots.Values.ToList();
            }
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
            var tree = new Tree($"[bold]Work Items[/] [grey](Total Completed: {totalCompletedWork})[/]");
            foreach (var root in roots)
            {
                var rootNode = BuildTreeNode(root, typeColors, stateColors, config);
                tree.AddNode(rootNode);
            }
            AnsiConsole.Write(tree);
        }, listTypeArgument, periodOption);
        rootCommand.AddCommand(listCommand);

        AddHelpCommand(rootCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static void AddHelpCommand(RootCommand rootCommand)
    {
        var helpCommand = new Command("help", "Show help and list available commands");
        helpCommand.SetHandler(() =>
        {
            foreach (var cmd in rootCommand.Children.OfType<Command>())
            {
                var content = new Markup($"[italic]{cmd.Description}[/]");
                if (cmd.Name == "list")
                {
                    // Arguments subpanel
                    var argsPanel = new Panel(new Rows(
                            new Markup("[blue]type[/]: Type of work items to list. Possible values: [yellow]completed[/], [yellow]active[/], [yellow]all[/] (default)")
                        ))
                        .Header("Arguments", Justify.Left)
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Blue)
                        .Padding(1,0,1,0);
                    // Options subpanel
                    var optsPanel = new Panel(new Rows(
                            new Markup("[green]--period[/], [green]-p[/]: Period to list work items. Possible values: [yellow]day[/] (default), [yellow]week[/], [yellow]month[/]")
                        ))
                        .Header("Options", Justify.Left)
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Green)
                        .Padding(1,0,1,0);
                    // Usage subpanel
                    var usagePanel = new Panel(new Rows(
                            new Markup("[grey]dotnet run --project src/AzDoCLI.App list completed --period week[/]"),
                            new Markup("[grey]dotnet run --project src/AzDoCLI.App list active[/]"),
                            new Markup("[grey]dotnet run --project src/AzDoCLI.App list all --period month[/]")
                        ))
                        .Header("Usage", Justify.Left)
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Grey)
                        .Padding(1,0,1,0);

                    // Nest subpanels inside the main command panel
                    var mainPanel = new Panel(new Rows(
                        content,
                        new Markup(string.Empty),
                        argsPanel,
                        optsPanel,
                        usagePanel
                    ))
                    .Header($"[bold yellow]{cmd.Name}[/]", Justify.Left)
                    .Border(BoxBorder.Double)
                    .Padding(1,1,1,1);
                    AnsiConsole.Write(mainPanel);
                }
                else
                {
                    var panel = new Panel(content)
                        .Header($"[bold yellow]{cmd.Name}[/]", Justify.Left)
                        .Border(BoxBorder.Double)
                        .Padding(1,1,1,1);
                    AnsiConsole.Write(panel);
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
