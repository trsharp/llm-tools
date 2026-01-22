using System.Text.Json;
using Microsoft.Extensions.Options;
using Tsk.Models;

namespace Tsk;

public class CliHandler
{
    private readonly TaskStore _store;
    private readonly TasksOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public CliHandler(TaskStore store, IOptions<TasksOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    public Task HandleAsync(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return Task.CompletedTask;
        }

        var command = args[0].ToLowerInvariant();
        var cmdArgs = args.Skip(1).ToArray();

        try
        {
            switch (command)
            {
                // Project commands
                case "project":
                case "proj":
                case "p":
                    HandleProject(cmdArgs);
                    break;

                // Task commands
                case "add":
                case "a":
                    AddTask(cmdArgs);
                    break;
                case "list":
                case "ls":
                case "l":
                    ListTasks(cmdArgs);
                    break;
                case "tree":
                case "t":
                    ShowTree(cmdArgs);
                    break;
                case "get":
                case "show":
                case "g":
                    GetTask(cmdArgs);
                    break;
                case "done":
                case "complete":
                case "d":
                    CompleteTask(cmdArgs);
                    break;
                case "start":
                case "s":
                    StartTask(cmdArgs);
                    break;
                case "block":
                case "b":
                    BlockTask(cmdArgs);
                    break;
                case "update":
                case "u":
                    UpdateTask(cmdArgs);
                    break;
                case "delete":
                case "rm":
                case "remove":
                    DeleteTask(cmdArgs);
                    break;
                case "move":
                case "mv":
                    MoveTask(cmdArgs);
                    break;
                case "stats":
                    ShowStats(cmdArgs);
                    break;
                case "dep":
                case "deps":
                case "depend":
                case "dependency":
                    HandleDependency(cmdArgs);
                    break;
                case "config":
                case "cfg":
                    HandleConfig(cmdArgs);
                    break;
                case "help":
                case "h":
                case "--help":
                case "-h":
                    ShowHelp();
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    ShowHelp();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    // ===== PROJECT COMMANDS =====

    private void HandleProject(string[] args)
    {
        if (args.Length == 0)
        {
            ListProjects();
            return;
        }

        var subCmd = args[0].ToLowerInvariant();
        var subArgs = args.Skip(1).ToArray();

        switch (subCmd)
        {
            case "add":
            case "create":
            case "new":
                if (subArgs.Length == 0)
                {
                    Console.WriteLine("Usage: tsk project add <name> [-d description]");
                    return;
                }
                var name = subArgs[0];
                string? desc = null;
                for (int i = 1; i < subArgs.Length; i++)
                {
                    if ((subArgs[i] == "-d" || subArgs[i] == "--description") && i + 1 < subArgs.Length)
                        desc = subArgs[++i];
                }
                var project = _store.AddProject(name, desc);
                Console.WriteLine($"✓ Created project {project.Id}: {project.Name}");
                break;

            case "list":
            case "ls":
                ListProjects();
                break;

            case "delete":
            case "rm":
                if (subArgs.Length == 0)
                {
                    Console.WriteLine("Usage: tsk project delete <id> [--cascade]");
                    return;
                }
                var cascade = subArgs.Contains("--cascade");
                if (_store.DeleteProject(subArgs[0], cascade))
                    Console.WriteLine($"✓ Deleted project {subArgs[0]}");
                else
                    Console.WriteLine($"Project not found: {subArgs[0]}");
                break;

            default:
                // Treat as project name to show details
                var proj = _store.GetProject(subCmd) ?? _store.GetProjectByName(subCmd);
                if (proj != null)
                {
                    Console.WriteLine($"\nProject: {proj.Id}");
                    Console.WriteLine($"Name: {proj.Name}");
                    if (!string.IsNullOrEmpty(proj.Description))
                        Console.WriteLine($"Description: {proj.Description}");
                    Console.WriteLine($"Created: {proj.CreatedAt:yyyy-MM-dd HH:mm}");

                    var stats = _store.GetStats(proj.Id);
                    Console.WriteLine($"\nTasks: {stats.Total} total, {stats.Done} done, {stats.InProgress} in progress");
                }
                else
                {
                    Console.WriteLine($"Unknown project command or project not found: {subCmd}");
                }
                break;
        }
    }

    private void ListProjects()
    {
        var projects = _store.ListProjects();
        if (projects.Count == 0)
        {
            Console.WriteLine("No projects found");
            return;
        }

        Console.WriteLine($"\n{"ID",-10} {"Name",-30} {"Tasks"}");
        Console.WriteLine(new string('-', 50));

        foreach (var proj in projects)
        {
            var stats = _store.GetStats(proj.Id);
            Console.WriteLine($"{proj.Id,-10} {proj.Name,-30} {stats.Total} ({stats.Done} done)");
        }
        Console.WriteLine();
    }

    // ===== TASK COMMANDS =====

    private void AddTask(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: tsk add <title> [-d description] [-p priority] [-t tag1,tag2] [--parent id] [--project id]");
            return;
        }

        var title = args[0];
        string? description = null;
        var priority = TaskPriority.Medium;
        List<string>? tags = null;
        DateTime? dueDate = null;
        string? parentId = null;
        string? projectId = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-d":
                case "--description":
                    if (i + 1 < args.Length) description = args[++i];
                    break;
                case "-p":
                case "--priority":
                    if (i + 1 < args.Length) priority = ParsePriority(args[++i]);
                    break;
                case "-t":
                case "--tags":
                    if (i + 1 < args.Length) tags = args[++i].Split(',').ToList();
                    break;
                case "--due":
                    if (i + 1 < args.Length) dueDate = DateTime.Parse(args[++i]);
                    break;
                case "--parent":
                    if (i + 1 < args.Length) parentId = args[++i];
                    break;
                case "--project":
                case "--proj":
                    if (i + 1 < args.Length) projectId = args[++i];
                    break;
            }
        }

        var task = _store.Add(title, description, priority, tags, dueDate, parentId, projectId);
        var parentInfo = task.ParentId != null ? $" (subtask of {task.ParentId})" : "";
        Console.WriteLine($"✓ Created task {task.Id}: {task.Title}{parentInfo}");
    }

    private void ListTasks(string[] args)
    {
        Models.TaskStatus? status = null;
        TaskPriority? priority = null;
        string? tag = null;
        string? projectId = null;
        bool includeCompleted = false;
        bool markdown = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-s":
                case "--status":
                    if (i + 1 < args.Length) status = ParseStatus(args[++i]);
                    break;
                case "-p":
                case "--priority":
                    if (i + 1 < args.Length) priority = ParsePriority(args[++i]);
                    break;
                case "-t":
                case "--tag":
                    if (i + 1 < args.Length) tag = args[++i];
                    break;
                case "--project":
                case "--proj":
                    if (i + 1 < args.Length) projectId = args[++i];
                    break;
                case "-a":
                case "--all":
                    includeCompleted = true;
                    break;
                case "--md":
                case "--markdown":
                    markdown = true;
                    break;
                case "todo":
                    status = Models.TaskStatus.Todo;
                    break;
                case "done":
                    status = Models.TaskStatus.Done;
                    includeCompleted = true;
                    break;
                case "inprogress":
                case "wip":
                    status = Models.TaskStatus.InProgress;
                    break;
                case "blocked":
                    status = Models.TaskStatus.Blocked;
                    break;
            }
        }

        var tasks = _store.List(status, priority, tag, projectId, includeCompleted);

        if (tasks.Count == 0)
        {
            Console.WriteLine("No tasks found");
            return;
        }

        if (markdown)
        {
            Console.WriteLine(FormatListMarkdown(tasks, projectId));
            return;
        }

        Console.WriteLine($"\n{"ID",-10} {"Status",-12} {"Pri",-8} {"Title"}");
        Console.WriteLine(new string('-', 70));

        foreach (var task in tasks)
        {
            var statusStr = $"{StatusIcon(task.Status)} {task.Status}";
            var priStr = $"{PriorityIcon(task.Priority)} {task.Priority}";
            var indent = task.ParentId != null ? "  " : "";
            Console.WriteLine($"{task.Id,-10} {statusStr,-12} {priStr,-8} {indent}{task.Title}");
        }

        Console.WriteLine();
    }

    private void ShowTree(string[] args)
    {
        string? projectId = null;
        bool includeCompleted = false;
        bool markdown = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                case "--proj":
                    if (i + 1 < args.Length) projectId = args[++i];
                    break;
                case "-a":
                case "--all":
                    includeCompleted = true;
                    break;
                case "--md":
                case "--markdown":
                    markdown = true;
                    break;
                default:
                    // Treat as project ID if not a flag
                    if (!args[i].StartsWith("-"))
                        projectId = args[i];
                    break;
            }
        }

        var tree = _store.GetTree(projectId, includeCompleted);

        if (tree.Count == 0)
        {
            Console.WriteLine("No tasks found");
            return;
        }

        if (markdown)
        {
            Console.WriteLine(FormatTreeMarkdown(tree, projectId));
            return;
        }

        Console.WriteLine();
        PrintTree(tree, "", true);
        Console.WriteLine();
    }

    private void PrintTree(List<TaskItem> items, string prefix, bool isRoot)
    {
        var sorted = items.OrderBy(t => t.Order).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            var task = sorted[i];
            var isLast = i == sorted.Count - 1;
            var connector = isRoot ? "" : (isLast ? "└── " : "├── ");
            var statusIcon = StatusIcon(task.Status);
            var priIcon = PriorityIcon(task.Priority);

            Console.WriteLine($"{prefix}{connector}[{task.Id}] {statusIcon} {priIcon} {task.Title}");

            if (task.Children.Count > 0)
            {
                var childPrefix = prefix + (isRoot ? "" : (isLast ? "    " : "│   "));
                PrintTree(task.Children, childPrefix, false);
            }
        }
    }

    private void GetTask(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: tsk get <id>");
            return;
        }

        var task = _store.Get(args[0]);
        if (task == null)
        {
            Console.WriteLine($"Task not found: {args[0]}");
            return;
        }

        Console.WriteLine($"\nTask: {task.Id}");
        Console.WriteLine($"Title: {task.Title}");
        Console.WriteLine($"Status: {StatusIcon(task.Status)} {task.Status}");
        Console.WriteLine($"Priority: {PriorityIcon(task.Priority)} {task.Priority}");
        if (!string.IsNullOrEmpty(task.Description))
            Console.WriteLine($"Description: {task.Description}");
        if (task.Tags.Count > 0)
            Console.WriteLine($"Tags: {string.Join(", ", task.Tags)}");
        if (task.ProjectId != null)
        {
            var proj = _store.GetProject(task.ProjectId);
            Console.WriteLine($"Project: {proj?.Name ?? task.ProjectId}");
        }
        if (task.ParentId != null)
        {
            var parent = _store.Get(task.ParentId);
            Console.WriteLine($"Parent: [{task.ParentId}] {parent?.Title ?? ""}");
        }
        if (task.DueDate.HasValue)
            Console.WriteLine($"Due: {task.DueDate:yyyy-MM-dd}");
        Console.WriteLine($"Created: {task.CreatedAt:yyyy-MM-dd HH:mm}");
        if (task.CompletedAt.HasValue)
            Console.WriteLine($"Completed: {task.CompletedAt:yyyy-MM-dd HH:mm}");

        var subtasks = _store.GetSubtasks(task.Id);
        if (subtasks.Count > 0)
        {
            Console.WriteLine($"\nSubtasks ({subtasks.Count}):");
            foreach (var sub in subtasks)
            {
                Console.WriteLine($"  [{sub.Id}] {StatusIcon(sub.Status)} {sub.Title}");
            }
        }
        Console.WriteLine();
    }

    private void CompleteTask(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: tsk done <id> [--recursive]");
            return;
        }

        var recursive = args.Contains("--recursive") || args.Contains("-r");
        var id = args.First(a => !a.StartsWith("-"));

        if (_store.Complete(id, recursive))
        {
            var msg = recursive ? $"✓ Completed task {id} and all subtasks" : $"✓ Completed task {id}";
            Console.WriteLine(msg);
        }
        else
            Console.WriteLine($"Task not found: {id}");
    }

    private void StartTask(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: tsk start <id>");
            return;
        }

        if (_store.Start(args[0]))
            Console.WriteLine($"◐ Started task {args[0]}");
        else
            Console.WriteLine($"Task not found: {args[0]}");
    }

    private void BlockTask(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: tsk block <id>");
            return;
        }

        if (_store.Block(args[0]))
            Console.WriteLine($"⊘ Blocked task {args[0]}");
        else
            Console.WriteLine($"Task not found: {args[0]}");
    }

    private void UpdateTask(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: tsk update <id> [-t title] [-d description] [-s status] [-p priority] [--parent id] [--project id]");
            return;
        }

        var id = args[0];
        string? title = null;
        string? description = null;
        Models.TaskStatus? status = null;
        TaskPriority? priority = null;
        List<string>? tags = null;
        string? parentId = null;
        string? projectId = null;
        int? order = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-t":
                case "--title":
                    if (i + 1 < args.Length) title = args[++i];
                    break;
                case "-d":
                case "--description":
                    if (i + 1 < args.Length) description = args[++i];
                    break;
                case "-s":
                case "--status":
                    if (i + 1 < args.Length) status = ParseStatus(args[++i]);
                    break;
                case "-p":
                case "--priority":
                    if (i + 1 < args.Length) priority = ParsePriority(args[++i]);
                    break;
                case "--tags":
                    if (i + 1 < args.Length) tags = args[++i].Split(',').ToList();
                    break;
                case "--parent":
                    if (i + 1 < args.Length) parentId = args[++i];
                    break;
                case "--project":
                case "--proj":
                    if (i + 1 < args.Length) projectId = args[++i];
                    break;
                case "--order":
                    if (i + 1 < args.Length) order = int.Parse(args[++i]);
                    break;
            }
        }

        // Handle project change
        if (projectId != null)
        {
            _store.MoveToProject(id, projectId == "none" || projectId == "" ? null : projectId);
        }

        var task = _store.Update(id, title, description, status, priority, tags, null, parentId, order);
        if (task != null)
            Console.WriteLine($"✓ Updated task {task.Id}");
        else
            Console.WriteLine($"Task not found: {id}");
    }

    private void DeleteTask(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: tsk delete <id> [--cascade]");
            return;
        }

        var cascade = args.Contains("--cascade");
        var id = args.First(a => !a.StartsWith("-"));

        if (_store.Delete(id, cascade))
        {
            var msg = cascade ? $"✓ Deleted task {id} and all subtasks" : $"✓ Deleted task {id}";
            Console.WriteLine(msg);
        }
        else
            Console.WriteLine($"Task not found: {id}");
    }

    private void MoveTask(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: tsk move <id> --project <project_id>");
            return;
        }

        var id = args[0];
        string? projectId = null;

        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--project" || args[i] == "--proj") && i + 1 < args.Length)
                projectId = args[++i];
        }

        _store.MoveToProject(id, projectId);
        Console.WriteLine($"✓ Moved task {id} to project {projectId ?? "(none)"}");
    }

    private void ShowStats(string[] args)
    {
        string? projectId = null;
        if (args.Length > 0)
            projectId = args[0];

        var stats = _store.GetStats(projectId);

        Console.WriteLine($"\nTask Statistics{(projectId != null ? $" (Project: {projectId})" : "")}");
        Console.WriteLine(new string('═', 40));
        Console.WriteLine($"Total:       {stats.Total}");
        Console.WriteLine($"Todo:        {stats.Todo}");
        Console.WriteLine($"In Progress: {stats.InProgress}");
        Console.WriteLine($"Done:        {stats.Done}");
        Console.WriteLine($"Blocked:     {stats.Blocked}");
        Console.WriteLine($"Cancelled:   {stats.Cancelled}");
        Console.WriteLine();
        Console.WriteLine("By Priority:");
        Console.WriteLine($"  Critical:  {stats.ByPriority.GetValueOrDefault(TaskPriority.Critical, 0)}");
        Console.WriteLine($"  High:      {stats.ByPriority.GetValueOrDefault(TaskPriority.High, 0)}");
        Console.WriteLine($"  Medium:    {stats.ByPriority.GetValueOrDefault(TaskPriority.Medium, 0)}");
        Console.WriteLine($"  Low:       {stats.ByPriority.GetValueOrDefault(TaskPriority.Low, 0)}");
        Console.WriteLine();
    }

    private void ShowHelp()
    {
        Console.WriteLine(@"
tsk - Hierarchical Task Management CLI & MCP Server

USAGE:
    tsk [command] [options]
    tsk --mcp              Run as MCP server over stdio

PROJECT COMMANDS:
    project, proj, p       List projects
    project add <name>     Create a new project
        -d, --description  Project description
    project delete <id>    Delete a project
        --cascade          Also delete all tasks in project

TASK COMMANDS:
    add, a <title>         Add a new task
        -d, --description  Task description
        -p, --priority     Priority (low, medium, high, critical)
        -t, --tags         Comma-separated tags
        --due              Due date (YYYY-MM-DD)
        --parent           Parent task ID (creates subtask)
        --project          Project ID or name

    list, ls, l            List tasks
        -s, --status       Filter by status
        -p, --priority     Filter by priority
        -t, --tag          Filter by tag
        --project          Filter by project
        -a, --all          Include completed tasks
        todo/done/wip      Quick status filter

    tree, t [project]      Show tasks as tree
        -a, --all          Include completed tasks

    get, show, g <id>      Show task details with subtasks
    done, d <id>           Mark task as done
        -r, --recursive    Also complete all subtasks
    start, s <id>          Mark task as in-progress
    block, b <id>          Mark task as blocked
    delete, rm <id>        Delete a task
        --cascade          Also delete all subtasks

    update, u <id>         Update a task
        -t, --title        New title
        -d, --description  New description
        -s, --status       New status
        -p, --priority     New priority
        --tags             New tags (comma-separated)
        --parent           New parent (or 'none')
        --order            New order among siblings

    move, mv <id>          Move task to project
        --project          Target project ID

    stats [project]        Show task statistics

DEPENDENCY COMMANDS:
    dep, deps              List dependencies for a task
    dep add <id> <depId>   Add dependency (id depends on depId)
    dep rm <id> <depId>    Remove dependency
    dep check <id>         Check if task can start (deps satisfied)
    dep blocking <id>      Show blocking (incomplete) dependencies

CONFIG COMMANDS:
    config, cfg            Show all config values
    config get <key>       Get a config value
    config set <key> <val> Set a config value
    config path            Show config file path

    Config keys: dataPath, defaultProject, defaultPriority

    help, h                Show this help

MARKDOWN OUTPUT:
    tsk tree --md          Output as markdown checklist
    tsk list --md          Output as markdown

EXAMPLES:
    tsk project add ""API Refactor""
    tsk add ""Design API"" --project ""API Refactor"" -p high
    tsk add ""Define endpoints"" --parent abc123
    tsk tree ""API Refactor""
    tsk tree --md > plan.md
    tsk done abc --recursive
    tsk dep add task1 task2     # task1 depends on task2
    tsk config set dataPath ""~/projects/tasks.json""
");
    }

    // ===== HELPERS =====

    private static TaskPriority ParsePriority(string value) => value.ToLowerInvariant() switch
    {
        "low" or "l" => TaskPriority.Low,
        "high" or "h" => TaskPriority.High,
        "critical" or "c" => TaskPriority.Critical,
        _ => TaskPriority.Medium
    };

    private static Models.TaskStatus? ParseStatus(string value) => value.ToLowerInvariant() switch
    {
        "todo" => Models.TaskStatus.Todo,
        "inprogress" or "wip" or "started" => Models.TaskStatus.InProgress,
        "done" or "complete" => Models.TaskStatus.Done,
        "blocked" => Models.TaskStatus.Blocked,
        "cancelled" or "canceled" => Models.TaskStatus.Cancelled,
        _ => null
    };

    private static string StatusIcon(Models.TaskStatus status) => status switch
    {
        Models.TaskStatus.Todo => "○",
        Models.TaskStatus.InProgress => "◐",
        Models.TaskStatus.Done => "●",
        Models.TaskStatus.Blocked => "⊘",
        Models.TaskStatus.Cancelled => "✕",
        _ => "?"
    };

    private static string PriorityIcon(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => "↓",
        TaskPriority.Medium => "→",
        TaskPriority.High => "↑",
        TaskPriority.Critical => "⚠",
        _ => "-"
    };

    // ===== MARKDOWN FORMATTING =====

    private string FormatListMarkdown(List<TaskItem> tasks, string? filterProjectId)
    {
        var sb = new System.Text.StringBuilder();

        // Group tasks by project
        var grouped = tasks.GroupBy(t => t.ProjectId).OrderBy(g => g.Key == null ? 1 : 0);
        var projects = _store.ListProjects();

        sb.AppendLine("# Tasks");
        sb.AppendLine();

        var allStats = _store.GetStats(filterProjectId);
        sb.AppendLine($"**Overall Progress:** {allStats.Done}/{allStats.Total} completed");
        sb.AppendLine();

        foreach (var group in grouped)
        {
            var project = group.Key != null ? projects.FirstOrDefault(p => p.Id == group.Key) : null;
            var projectName = project?.Name ?? "Unassigned";

            sb.AppendLine($"## {projectName}");
            sb.AppendLine();

            // Build tree for this group to get proper nesting
            var groupTasks = group.ToList();
            var tree = BuildTreeFromTasks(groupTasks);

            void WriteTask(TaskItem task, int depth)
            {
                var indent = new string(' ', depth * 2);
                var checkbox = task.Status == Models.TaskStatus.Done ? "[x]" : "[ ]";
                var priority = task.Priority >= TaskPriority.High ? $" **[{task.Priority}]**" : "";
                var status = task.Status switch
                {
                    Models.TaskStatus.InProgress => " 🔄",
                    Models.TaskStatus.Blocked => " ⚠️ BLOCKED",
                    Models.TaskStatus.Cancelled => " ~~cancelled~~",
                    _ => ""
                };

                sb.AppendLine($"{indent}- {checkbox} {task.Title}{priority}{status}");

                if (!string.IsNullOrEmpty(task.Description))
                    sb.AppendLine($"{indent}  > {task.Description}");

                foreach (var child in task.Children.OrderBy(c => c.Order))
                {
                    WriteTask(child, depth + 1);
                }
            }

            foreach (var root in tree.OrderBy(t => t.Order))
            {
                WriteTask(root, 0);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private List<TaskItem> BuildTreeFromTasks(List<TaskItem> tasks)
    {
        var lookup = tasks.ToDictionary(t => t.Id);
        var roots = new List<TaskItem>();

        foreach (var task in tasks)
        {
            task.Children = new List<TaskItem>();
        }

        foreach (var task in tasks)
        {
            if (task.ParentId != null && lookup.TryGetValue(task.ParentId, out var parent))
            {
                parent.Children.Add(task);
            }
            else
            {
                roots.Add(task);
            }
        }

        return roots;
    }

    private string FormatTreeMarkdown(List<TaskItem> tree, string? projectId)
    {
        var sb = new System.Text.StringBuilder();

        // If no project filter, group by project
        if (projectId == null)
        {
            var flat = _store.Flatten(tree);
            var grouped = flat.GroupBy(t => t.ProjectId).OrderBy(g => g.Key == null ? 1 : 0);
            var projects = _store.ListProjects();

            sb.AppendLine("# Task Plan");
            sb.AppendLine();

            var done = flat.Count(t => t.Status == Models.TaskStatus.Done);
            var pct = flat.Count > 0 ? (done * 100 / flat.Count) : 0;
            sb.AppendLine($"**Overall Progress:** {done}/{flat.Count} tasks ({pct}%)");
            sb.AppendLine();

            // List projects
            if (projects.Count > 0)
            {
                sb.AppendLine("## Projects");
                sb.AppendLine();
                foreach (var proj in projects)
                {
                    var projStats = _store.GetStats(proj.Id);
                    var projPct = projStats.Total > 0 ? (projStats.Done * 100 / projStats.Total) : 0;
                    sb.AppendLine($"- **{proj.Name}** - {projStats.Done}/{projStats.Total} ({projPct}%)");
                }
                sb.AppendLine();
            }

            foreach (var group in grouped)
            {
                var project = group.Key != null ? projects.FirstOrDefault(p => p.Id == group.Key) : null;
                var projectName = project?.Name ?? "Unassigned";

                sb.AppendLine($"## {projectName}");
                sb.AppendLine();

                // Get tree roots for this project
                var projectRoots = tree.Where(t => t.ProjectId == group.Key).ToList();

                void WriteTask(TaskItem task, int depth)
                {
                    var indent = new string(' ', depth * 2);
                    var checkbox = task.Status == Models.TaskStatus.Done ? "[x]" : "[ ]";
                    var priority = task.Priority >= TaskPriority.High ? $" **[{task.Priority}]**" : "";
                    var status = task.Status switch
                    {
                        Models.TaskStatus.InProgress => " 🔄",
                        Models.TaskStatus.Blocked => " ⚠️ BLOCKED",
                        Models.TaskStatus.Cancelled => " ~~cancelled~~",
                        _ => ""
                    };

                    sb.AppendLine($"{indent}- {checkbox} {task.Title}{priority}{status}");

                    if (!string.IsNullOrEmpty(task.Description))
                        sb.AppendLine($"{indent}  > {task.Description}");

                    foreach (var child in task.Children.OrderBy(c => c.Order))
                    {
                        WriteTask(child, depth + 1);
                    }
                }

                foreach (var root in projectRoots.OrderBy(t => t.Order))
                {
                    WriteTask(root, 0);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        // Single project view
        var singleProjectName = _store.GetProject(projectId)?.Name ?? _store.GetProjectByName(projectId)?.Name ?? projectId;

        sb.AppendLine($"# {singleProjectName}");
        sb.AppendLine();

        var allFlat = _store.Flatten(tree);
        var allDone = allFlat.Count(t => t.Status == Models.TaskStatus.Done);
        var allPct = allFlat.Count > 0 ? (allDone * 100 / allFlat.Count) : 0;
        sb.AppendLine($"**Progress:** {allDone}/{allFlat.Count} tasks ({allPct}%)");
        sb.AppendLine();

        void WriteSingleTask(TaskItem task, int depth)
        {
            var indent = new string(' ', depth * 2);
            var checkbox = task.Status == Models.TaskStatus.Done ? "[x]" : "[ ]";
            var priority = task.Priority >= TaskPriority.High ? $" **[{task.Priority}]**" : "";
            var status = task.Status switch
            {
                Models.TaskStatus.InProgress => " 🔄",
                Models.TaskStatus.Blocked => " ⚠️ BLOCKED",
                Models.TaskStatus.Cancelled => " ~~cancelled~~",
                _ => ""
            };

            sb.AppendLine($"{indent}- {checkbox} {task.Title}{priority}{status}");

            if (!string.IsNullOrEmpty(task.Description))
                sb.AppendLine($"{indent}  > {task.Description}");

            foreach (var child in task.Children.OrderBy(c => c.Order))
            {
                WriteSingleTask(child, depth + 1);
            }
        }

        foreach (var root in tree.OrderBy(t => t.Order))
        {
            WriteSingleTask(root, 0);
        }

        return sb.ToString();
    }

    // ===== DEPENDENCY COMMANDS =====

    private void HandleDependency(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: tsk dep <command> [args]");
            Console.WriteLine("Commands: add, rm, check, blocking, list");
            return;
        }

        var subCommand = args[0].ToLowerInvariant();
        var subArgs = args.Skip(1).ToArray();

        switch (subCommand)
        {
            case "add":
                AddDependency(subArgs);
                break;
            case "rm":
            case "remove":
            case "delete":
                RemoveDependency(subArgs);
                break;
            case "check":
                CheckDependencies(subArgs);
                break;
            case "blocking":
                ShowBlockingDependencies(subArgs);
                break;
            case "list":
            case "ls":
            default:
                // If it looks like an ID, show deps for that task
                if (subCommand.Length >= 3 && !subCommand.StartsWith("-"))
                    ShowTaskDependencies(subCommand);
                else if (subArgs.Length > 0)
                    ShowTaskDependencies(subArgs[0]);
                else
                    Console.WriteLine("Usage: tsk dep <taskId> or tsk dep list <taskId>");
                break;
        }
    }

    private void AddDependency(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: tsk dep add <taskId> <dependsOnId>");
            Console.WriteLine("  Creates dependency: taskId depends on dependsOnId");
            return;
        }

        var taskId = args[0];
        var depId = args[1];

        var task = _store.Get(taskId);
        var dep = _store.Get(depId);

        if (task == null)
        {
            Console.WriteLine($"✗ Task not found: {taskId}");
            return;
        }
        if (dep == null)
        {
            Console.WriteLine($"✗ Dependency task not found: {depId}");
            return;
        }

        if (_store.AddDependency(task.Id, dep.Id))
        {
            Console.WriteLine($"✓ Added dependency: '{task.Title}' depends on '{dep.Title}'");
        }
        else
        {
            Console.WriteLine($"✗ Could not add dependency (may create circular reference)");
        }
    }

    private void RemoveDependency(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: tsk dep rm <taskId> <dependsOnId>");
            return;
        }

        var taskId = args[0];
        var depId = args[1];

        var task = _store.Get(taskId);
        var dep = _store.Get(depId);

        if (task == null)
        {
            Console.WriteLine($"✗ Task not found: {taskId}");
            return;
        }

        if (_store.RemoveDependency(task.Id, dep?.Id ?? depId))
        {
            Console.WriteLine($"✓ Removed dependency");
        }
        else
        {
            Console.WriteLine($"✗ Dependency not found");
        }
    }

    private void ShowTaskDependencies(string taskId)
    {
        var task = _store.Get(taskId);
        if (task == null)
        {
            Console.WriteLine($"✗ Task not found: {taskId}");
            return;
        }

        var deps = _store.GetDependencies(task.Id);
        var dependents = _store.GetDependents(task.Id);

        Console.WriteLine($"Task: {task.Title} [{task.Id[..8]}]");
        Console.WriteLine();

        if (deps.Any())
        {
            Console.WriteLine("Depends on:");
            foreach (var dep in deps)
            {
                var status = dep.Status == Models.TaskStatus.Done ? "✓" : "○";
                Console.WriteLine($"  {status} {dep.Title} [{dep.Id[..8]}]");
            }
        }
        else
        {
            Console.WriteLine("Depends on: (none)");
        }

        Console.WriteLine();

        if (dependents.Any())
        {
            Console.WriteLine("Required by:");
            foreach (var d in dependents)
            {
                Console.WriteLine($"  → {d.Title} [{d.Id[..8]}]");
            }
        }
        else
        {
            Console.WriteLine("Required by: (none)");
        }
    }

    private void CheckDependencies(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: tsk dep check <taskId>");
            return;
        }

        var task = _store.Get(args[0]);
        if (task == null)
        {
            Console.WriteLine($"✗ Task not found: {args[0]}");
            return;
        }

        var canStart = _store.CanStart(task.Id);
        var blocking = _store.GetBlockingDependencies(task.Id);

        if (canStart)
        {
            Console.WriteLine($"✓ Task '{task.Title}' is ready to start (no blocking dependencies)");
        }
        else
        {
            Console.WriteLine($"⚠ Task '{task.Title}' has {blocking.Count} blocking dependencies:");
            foreach (var depId in blocking)
            {
                var dep = _store.Get(depId);
                if (dep != null)
                    Console.WriteLine($"  ○ {dep.Title} [{dep.Id[..8]}] - {dep.Status}");
            }
        }
    }

    private void ShowBlockingDependencies(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: tsk dep blocking <taskId>");
            return;
        }

        var task = _store.Get(args[0]);
        if (task == null)
        {
            Console.WriteLine($"✗ Task not found: {args[0]}");
            return;
        }

        var blocking = _store.GetBlockingDependencies(task.Id);

        if (!blocking.Any())
        {
            Console.WriteLine($"✓ No blocking dependencies for '{task.Title}'");
            return;
        }

        Console.WriteLine($"Blocking dependencies for '{task.Title}':");
        foreach (var depId in blocking)
        {
            var dep = _store.Get(depId);
            if (dep != null)
                Console.WriteLine($"  ○ {dep.Title} [{dep.Id[..8]}] - {dep.Status}");
        }
    }

    // ===== CONFIG COMMANDS =====

    private void HandleConfig(string[] args)
    {
        if (args.Length == 0)
        {
            ShowAllConfig();
            return;
        }

        var subCommand = args[0].ToLowerInvariant();
        var subArgs = args.Skip(1).ToArray();

        switch (subCommand)
        {
            case "get":
                if (subArgs.Length == 0)
                    ShowAllConfig();
                else
                    ShowConfigValue(subArgs[0]);
                break;
            case "set":
                if (subArgs.Length < 2)
                    Console.WriteLine("Usage: tsk config set <key> <value>");
                else
                    SetConfigValue(subArgs[0], string.Join(" ", subArgs.Skip(1)));
                break;
            case "path":
                Console.WriteLine(TskConfigHelper.GetConfigPath());
                break;
            default:
                // Might be a key name
                ShowConfigValue(subCommand);
                break;
        }
    }

    private void ShowAllConfig()
    {
        Console.WriteLine("Configuration:");
        Console.WriteLine($"  dataPath:        {_options.DataPath ?? "(default: ~/.tsk)"}");
        Console.WriteLine($"  defaultProject:  {_options.DefaultProject ?? "(none)"}");
        Console.WriteLine($"  defaultPriority: {_options.DefaultPriority}");
        Console.WriteLine();
        Console.WriteLine($"Config file: {TskConfigHelper.GetConfigPath()}");
        Console.WriteLine($"Data dir:    {TskConfigHelper.GetDataDirectory(_options)}");
    }

    private void ShowConfigValue(string key)
    {
        var value = TskConfigHelper.Get(_options, key);
        if (value != null)
            Console.WriteLine(value);
        else
            Console.WriteLine($"Unknown config key: {key}");
    }

    private void SetConfigValue(string key, string value)
    {
        if (TskConfigHelper.Set(_options, key, value))
        {
            Console.WriteLine($"✓ Set {key} = {value}");
        }
        else
        {
            Console.WriteLine($"✗ Invalid config key or value: {key}");
            Console.WriteLine("  Valid keys: dataPath, defaultProject, defaultPriority");
        }
    }
}
