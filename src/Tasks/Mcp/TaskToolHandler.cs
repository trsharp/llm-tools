using System.Text.Json;
using Tasks.Models;

namespace Tasks.Mcp;

public class TaskToolHandler
{
    private readonly TaskStore _store;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public TaskToolHandler(TaskStore store)
    {
        _store = store;
    }

    public List<ToolDefinition> GetToolDefinitions()
    {
        return new List<ToolDefinition>
        {
            // ===== PROJECT TOOLS =====
            new()
            {
                Name = "project_create",
                Description = "Create a new project to organize related tasks. Projects provide logical grouping for tasks. Each project is stored in its own data file. Parameters: 'name' (required) - unique project name, 'description' (optional) - detailed project description. Returns the created project with its generated ID.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "name": {
                            "type": "string",
                            "description": "The project name"
                        },
                        "description": {
                            "type": "string",
                            "description": "Optional project description"
                        }
                    },
                    "required": ["name"]
                }
                """).RootElement
            },
            new()
            {
                Name = "project_list",
                Description = "List all projects in the task store. Returns project IDs, names, descriptions, and timestamps. No parameters required. Use this to discover available projects before filtering tasks by project.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {}
                }
                """).RootElement
            },
            new()
            {
                Name = "project_delete",
                Description = "Delete a project and optionally its tasks. Parameters: 'id' (required) - project ID or name (partial ID matching supported), 'cascade' (optional, default: false) - if true, permanently deletes all tasks in the project; if false, tasks become unassigned and can still be accessed.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "id": {
                            "type": "string",
                            "description": "The project ID or name"
                        },
                        "cascade": {
                            "type": "boolean",
                            "description": "If true, delete all tasks in the project. If false, tasks become unassigned."
                        }
                    },
                    "required": ["id"]
                }
                """).RootElement
            },

            // ===== TASK TOOLS =====
            new()
            {
                Name = "task_add",
                Description = "Create a new task with optional hierarchy and project assignment. Parameters: 'title' (required) - task title, 'description' (optional) - detailed description, 'priority' (optional) - low/medium/high/critical (default: medium), 'tags' (optional) - array of string tags for categorization, 'dueDate' (optional) - ISO 8601 date string, 'parentId' (optional) - creates task as subtask of specified parent, 'projectId' (optional) - assigns to project by ID or name. Returns the created task with generated ID.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "title": {
                            "type": "string",
                            "description": "The title of the task"
                        },
                        "description": {
                            "type": "string",
                            "description": "Optional detailed description"
                        },
                        "priority": {
                            "type": "string",
                            "enum": ["low", "medium", "high", "critical"],
                            "description": "Task priority (default: medium)"
                        },
                        "tags": {
                            "type": "array",
                            "items": { "type": "string" },
                            "description": "Optional tags for categorization"
                        },
                        "dueDate": {
                            "type": "string",
                            "description": "Optional due date (ISO 8601 format)"
                        },
                        "parentId": {
                            "type": "string",
                            "description": "Parent task ID to create as a subtask"
                        },
                        "projectId": {
                            "type": "string",
                            "description": "Project ID or name to assign the task to"
                        }
                    },
                    "required": ["title"]
                }
                """).RootElement
            },
            new()
            {
                Name = "task_add_many",
                Description = "Bulk create multiple tasks with nested subtask hierarchies in a single operation. Ideal for setting up project structures or importing task lists. Parameters: 'tasks' (required) - array of task objects, each with 'title' (required), 'description', 'priority', 'tags', and 'subtasks' (recursive array for nesting), 'projectId' (optional) - assigns all tasks to this project, 'parentId' (optional) - creates all tasks under this parent. Returns all created tasks with their IDs.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "projectId": {
                            "type": "string",
                            "description": "Project ID or name to assign all tasks to"
                        },
                        "parentId": {
                            "type": "string",
                            "description": "Parent task ID to add all tasks under"
                        },
                        "tasks": {
                            "type": "array",
                            "description": "Array of tasks to create",
                            "items": {
                                "type": "object",
                                "properties": {
                                    "title": { "type": "string" },
                                    "description": { "type": "string" },
                                    "priority": { "type": "string", "enum": ["low", "medium", "high", "critical"] },
                                    "tags": { "type": "array", "items": { "type": "string" } },
                                    "subtasks": {
                                        "type": "array",
                                        "description": "Nested subtasks (recursive structure)",
                                        "items": { "$ref": "#" }
                                    }
                                },
                                "required": ["title"]
                            }
                        }
                    },
                    "required": ["tasks"]
                }
                """).RootElement
            },
            new()
            {
                Name = "task_list",
                Description = "Query and filter tasks with flexible criteria. Parameters: 'status' (optional) - filter by todo/inprogress/done/blocked/cancelled, 'priority' (optional) - filter by low/medium/high/critical, 'tag' (optional) - filter by tag name, 'projectId' (optional) - filter by project ID or name, 'includeCompleted' (optional, default: false) - include done/cancelled tasks, 'format' (optional) - output as 'text' (default), 'markdown' (checklist), or 'json'. Returns filtered task list sorted by priority then order.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "status": {
                            "type": "string",
                            "enum": ["todo", "inprogress", "done", "blocked", "cancelled"],
                            "description": "Filter by status"
                        },
                        "priority": {
                            "type": "string",
                            "enum": ["low", "medium", "high", "critical"],
                            "description": "Filter by priority"
                        },
                        "tag": {
                            "type": "string",
                            "description": "Filter by tag"
                        },
                        "projectId": {
                            "type": "string",
                            "description": "Filter by project ID or name"
                        },
                        "includeCompleted": {
                            "type": "boolean",
                            "description": "Include completed/cancelled tasks (default: false)"
                        },
                        "format": {
                            "type": "string",
                            "enum": ["text", "markdown", "json"],
                            "description": "Output format (default: text)"
                        }
                    }
                }
                """).RootElement
            },
            new()
            {
                Name = "task_tree",
                Description = "Display tasks in a hierarchical tree structure showing parent-child relationships with visual indentation. Parameters: 'projectId' (optional) - filter to specific project, 'includeCompleted' (optional, default: false) - include done/cancelled tasks, 'format' (optional) - 'text' (default, ASCII tree), 'markdown' (nested checklist), or 'json' (structured data). Best for visualizing task breakdown structures.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "projectId": {
                            "type": "string",
                            "description": "Filter by project ID or name"
                        },
                        "includeCompleted": {
                            "type": "boolean",
                            "description": "Include completed/cancelled tasks (default: false)"
                        },
                        "format": {
                            "type": "string",
                            "enum": ["text", "markdown", "json"],
                            "description": "Output format (default: text). Markdown provides a checklist format."
                        }
                    }
                }
                """).RootElement
            },
            new()
            {
                Name = "task_get",
                Description = "Retrieve detailed information about a specific task including all its properties and direct subtasks. Parameters: 'id' (required) - task ID, supports partial ID matching (e.g., first few characters). Returns task details: title, description, status, priority, tags, dates, parent/project assignments, and list of immediate subtasks.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "id": {
                            "type": "string",
                            "description": "The task ID (can be partial)"
                        }
                    },
                    "required": ["id"]
                }
                """).RootElement
            },
            new()
            {
                Name = "task_subtasks",
                Description = "Get all subtasks of a parent task, optionally including entire descendant tree. Parameters: 'id' (required) - parent task ID (partial matching supported), 'recursive' (optional, default: false) - if true, returns all descendants at all levels; if false, returns only direct children. Returns flat list of subtasks with their full details.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "id": {
                            "type": "string",
                            "description": "The parent task ID"
                        },
                        "recursive": {
                            "type": "boolean",
                            "description": "Include all descendants, not just direct children (default: false)"
                        }
                    },
                    "required": ["id"]
                }
                """).RootElement
            },
            new()
            {
                Name = "task_update",
                Description = "Modify any properties of an existing task. Parameters: 'id' (required) - task ID (partial matching supported), plus any fields to update: 'title', 'description', 'status' (todo/inprogress/done/blocked/cancelled), 'priority' (low/medium/high/critical), 'tags' (replaces all existing tags), 'dueDate' (ISO 8601), 'parentId' (new parent ID or 'none' to make root task), 'order' (integer position among siblings). Only specified fields are updated.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "id": {
                            "type": "string",
                            "description": "The task ID (can be partial)"
                        },
                        "title": {
                            "type": "string",
                            "description": "New title"
                        },
                        "description": {
                            "type": "string",
                            "description": "New description"
                        },
                        "status": {
                            "type": "string",
                            "enum": ["todo", "inprogress", "done", "blocked", "cancelled"],
                            "description": "New status"
                        },
                        "priority": {
                            "type": "string",
                            "enum": ["low", "medium", "high", "critical"],
                            "description": "New priority"
                        },
                        "tags": {
                            "type": "array",
                            "items": { "type": "string" },
                            "description": "New tags (replaces existing)"
                        },
                        "dueDate": {
                            "type": "string",
                            "description": "New due date (ISO 8601 format)"
                        },
                        "parentId": {
                            "type": "string",
                            "description": "New parent task ID, or 'none' to make it a root task"
                        },
                        "order": {
                            "type": "integer",
                            "description": "New order among siblings"
                        }
                    },
                    "required": ["id"]
                }
                """).RootElement
            },
            new()
            {
                Name = "task_delete",
                Description = "Permanently delete a task. Parameters: 'id' (required) - task ID (partial matching supported), 'cascade' (optional, default: false) - if true, also deletes all subtasks; if false, subtasks are reparented to the deleted task's parent (preserving the hierarchy). This action cannot be undone.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "id": {
                            "type": "string",
                            "description": "The task ID (can be partial)"
                        },
                        "cascade": {
                            "type": "boolean",
                            "description": "If true, delete all subtasks. If false, subtasks are reparented."
                        }
                    },
                    "required": ["id"]
                }
                """).RootElement
            },
            new()
            {
                Name = "task_complete",
                Description = "Mark a task as done, setting its status to 'done' and recording completion timestamp. Parameters: 'id' (required) - task ID (partial matching supported), 'recursive' (optional, default: false) - if true, also marks all subtasks as done. Use this instead of task_update for completing tasks to ensure proper timestamp handling.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "id": {
                            "type": "string",
                            "description": "The task ID (can be partial)"
                        },
                        "recursive": {
                            "type": "boolean",
                            "description": "Also complete all subtasks (default: false)"
                        }
                    },
                    "required": ["id"]
                }
                """).RootElement
            },
            new()
            {
                Name = "task_start",
                Description = "Mark a task as in-progress, changing its status from 'todo' to 'inprogress'. Parameters: 'id' (required) - task ID (partial matching supported). Use dependency_check first to verify all dependencies are satisfied before starting a task.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "id": {
                            "type": "string",
                            "description": "The task ID (can be partial)"
                        }
                    },
                    "required": ["id"]
                }
                """).RootElement
            },
            new()
            {
                Name = "task_block",
                Description = "Mark a task as blocked, indicating it cannot proceed due to external factors. Parameters: 'id' (required) - task ID (partial matching supported). Blocked tasks are excluded from normal task lists unless includeCompleted is true. Consider adding a dependency or updating the description to document why it's blocked.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "id": {
                            "type": "string",
                            "description": "The task ID (can be partial)"
                        }
                    },
                    "required": ["id"]
                }
                """).RootElement
            },
            new()
            {
                Name = "task_move",
                Description = "Move a task and all its subtasks to a different project. Parameters: 'id' (required) - task ID to move, 'projectId' (optional) - target project ID or name, or omit/empty to unassign from any project. The task becomes a root task in the target project (parentId is cleared). All descendant subtasks are also moved.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "id": {
                            "type": "string",
                            "description": "The task ID"
                        },
                        "projectId": {
                            "type": "string",
                            "description": "Target project ID or name (empty to unassign)"
                        }
                    },
                    "required": ["id"]
                }
                """).RootElement
            },
            new()
            {
                Name = "task_stats",
                Description = "Get aggregate statistics about tasks. Parameters: 'projectId' (optional) - limit stats to specific project. Returns counts for: total tasks, by status (todo/inprogress/done/blocked/cancelled), and by priority (low/medium/high/critical). Useful for project dashboards and progress tracking.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "projectId": {
                            "type": "string",
                            "description": "Filter by project ID or name"
                        }
                    }
                }
                """).RootElement
            },

            // ===== DEPENDENCY TOOLS =====
            new()
            {
                Name = "dependency_add",
                Description = "Create a dependency relationship where one task must be completed before another can start. Parameters: 'taskId' (required) - the dependent task (the one that must wait), 'dependsOnId' (required) - the prerequisite task (must be done first). Prevents circular dependencies and self-dependencies. Use dependency_check to verify a task can be started.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "taskId": {
                            "type": "string",
                            "description": "The task that will have the dependency"
                        },
                        "dependsOnId": {
                            "type": "string",
                            "description": "The task that must be completed first"
                        }
                    },
                    "required": ["taskId", "dependsOnId"]
                }
                """).RootElement
            },
            new()
            {
                Name = "dependency_remove",
                Description = "Remove an existing dependency relationship between two tasks. Parameters: 'taskId' (required) - the dependent task, 'dependsOnId' (required) - the prerequisite task to remove from dependencies. After removal, taskId will no longer be blocked by dependsOnId.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "taskId": {
                            "type": "string",
                            "description": "The task with the dependency"
                        },
                        "dependsOnId": {
                            "type": "string",
                            "description": "The dependency to remove"
                        }
                    },
                    "required": ["taskId", "dependsOnId"]
                }
                """).RootElement
            },
            new()
            {
                Name = "dependency_list",
                Description = "Show all dependency relationships for a task in both directions. Parameters: 'taskId' (required) - the task to inspect. Returns two lists: 'dependsOn' (tasks that must be completed before this one) and 'dependents' (tasks that are waiting for this one). Also shows which blocking dependencies are incomplete.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "taskId": {
                            "type": "string",
                            "description": "The task ID"
                        }
                    },
                    "required": ["taskId"]
                }
                """).RootElement
            },
            new()
            {
                Name = "dependency_check",
                Description = "Verify whether a task is ready to start by checking if all its dependencies are complete. Parameters: 'taskId' (required) - the task to check. Returns whether the task can start, and if not, lists the incomplete dependencies that are blocking it. A task can start when all its dependsOn tasks have status 'done' or 'cancelled'.",
                InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "taskId": {
                            "type": "string",
                            "description": "The task ID to check"
                        }
                    },
                    "required": ["taskId"]
                }
                """).RootElement
            }
        };
    }

    public Task<CallToolResult> HandleToolCallAsync(string toolName, JsonElement? arguments)
    {
        try
        {
            var result = toolName switch
            {
                // Project tools
                "project_create" => HandleProjectCreate(arguments),
                "project_list" => HandleProjectList(arguments),
                "project_delete" => HandleProjectDelete(arguments),

                // Task tools
                "task_add" => HandleTaskAdd(arguments),
                "task_add_many" => HandleTaskAddMany(arguments),
                "task_list" => HandleTaskList(arguments),
                "task_tree" => HandleTaskTree(arguments),
                "task_get" => HandleTaskGet(arguments),
                "task_subtasks" => HandleTaskSubtasks(arguments),
                "task_update" => HandleTaskUpdate(arguments),
                "task_delete" => HandleTaskDelete(arguments),
                "task_complete" => HandleTaskComplete(arguments),
                "task_start" => HandleTaskStart(arguments),
                "task_block" => HandleTaskBlock(arguments),
                "task_move" => HandleTaskMove(arguments),
                "task_stats" => HandleTaskStats(arguments),

                // Dependency tools
                "dependency_add" => HandleDependencyAdd(arguments),
                "dependency_remove" => HandleDependencyRemove(arguments),
                "dependency_list" => HandleDependencyList(arguments),
                "dependency_check" => HandleDependencyCheck(arguments),

                _ => new CallToolResult
                {
                    Content = new List<ToolContent> { new() { Text = $"Unknown tool: {toolName}" } },
                    IsError = true
                }
            };
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = $"Error: {ex.Message}" } },
                IsError = true
            });
        }
    }

    // ===== PROJECT HANDLERS =====

    private CallToolResult HandleProjectCreate(JsonElement? arguments)
    {
        var name = arguments?.GetProperty("name").GetString() ?? throw new ArgumentException("Name is required");
        var description = arguments?.TryGetProperty("description", out var descProp) == true ? descProp.GetString() : null;

        var project = _store.AddProject(name, description);

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = $"Created project {project.Id}: {project.Name}" },
                new() { Text = JsonSerializer.Serialize(project, JsonOptions) }
            }
        };
    }

    private CallToolResult HandleProjectList(JsonElement? arguments)
    {
        var projects = _store.ListProjects();

        if (projects.Count == 0)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = "No projects found" } }
            };
        }

        var summary = string.Join("\n", projects.Select(p => $"[{p.Id}] {p.Name}"));

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = $"Found {projects.Count} project(s):\n{summary}" },
                new() { Text = JsonSerializer.Serialize(projects, JsonOptions) }
            }
        };
    }

    private CallToolResult HandleProjectDelete(JsonElement? arguments)
    {
        var id = arguments?.GetProperty("id").GetString() ?? throw new ArgumentException("ID is required");
        var cascade = arguments?.TryGetProperty("cascade", out var cascadeProp) == true && cascadeProp.GetBoolean();

        var deleted = _store.DeleteProject(id, cascade);

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = deleted ? $"Deleted project {id}" : $"Project not found: {id}" }
            },
            IsError = !deleted
        };
    }

    // ===== TASK HANDLERS =====

    private CallToolResult HandleTaskAdd(JsonElement? arguments)
    {
        var title = arguments?.GetProperty("title").GetString() ?? throw new ArgumentException("Title is required");
        var description = arguments?.TryGetProperty("description", out var descProp) == true ? descProp.GetString() : null;
        var priority = ParsePriority(arguments?.TryGetProperty("priority", out var priProp) == true ? priProp.GetString() : null);
        var tags = arguments?.TryGetProperty("tags", out var tagsProp) == true
            ? tagsProp.EnumerateArray().Select(t => t.GetString()!).ToList()
            : null;
        var dueDate = arguments?.TryGetProperty("dueDate", out var dueProp) == true && dueProp.GetString() is string ds
            ? DateTime.Parse(ds)
            : (DateTime?)null;
        var parentId = arguments?.TryGetProperty("parentId", out var parentProp) == true ? parentProp.GetString() : null;
        var projectId = arguments?.TryGetProperty("projectId", out var projProp) == true ? projProp.GetString() : null;

        var task = _store.Add(title, description, priority, tags, dueDate, parentId, projectId);

        var parentInfo = task.ParentId != null ? $" (subtask of {task.ParentId})" : "";
        var projectInfo = task.ProjectId != null ? $" in project {task.ProjectId}" : "";

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = $"Created task {task.Id}: {task.Title}{parentInfo}{projectInfo}" },
                new() { Text = JsonSerializer.Serialize(task, JsonOptions) }
            }
        };
    }

    private CallToolResult HandleTaskAddMany(JsonElement? arguments)
    {
        var projectId = arguments?.TryGetProperty("projectId", out var projProp) == true ? projProp.GetString() : null;
        var parentId = arguments?.TryGetProperty("parentId", out var parentProp) == true ? parentProp.GetString() : null;
        var tasksJson = arguments?.GetProperty("tasks");

        if (tasksJson == null)
            throw new ArgumentException("Tasks array is required");

        var inputs = ParseTaskInputs(tasksJson.Value);
        var tasks = _store.AddMany(inputs, projectId, parentId);

        var tree = _store.GetTree(projectId, includeCompleted: true);
        var treeText = FormatTree(tree);

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = $"Created {tasks.Count} task(s):\n{treeText}" },
                new() { Text = JsonSerializer.Serialize(tasks, JsonOptions) }
            }
        };
    }

    private List<TaskItemInput> ParseTaskInputs(JsonElement tasksJson)
    {
        var result = new List<TaskItemInput>();

        foreach (var item in tasksJson.EnumerateArray())
        {
            var input = new TaskItemInput
            {
                Title = item.GetProperty("title").GetString() ?? "",
                Description = item.TryGetProperty("description", out var d) ? d.GetString() : null,
                Priority = item.TryGetProperty("priority", out var p) ? ParsePriority(p.GetString()) : null,
                Tags = item.TryGetProperty("tags", out var t)
                    ? t.EnumerateArray().Select(x => x.GetString()!).ToList()
                    : null
            };

            if (item.TryGetProperty("subtasks", out var subtasks))
            {
                input.Subtasks = ParseTaskInputs(subtasks);
            }

            result.Add(input);
        }

        return result;
    }

    private CallToolResult HandleTaskList(JsonElement? arguments)
    {
        var status = arguments?.TryGetProperty("status", out var statusProp) == true ? ParseStatus(statusProp.GetString()) : null;
        var priority = arguments?.TryGetProperty("priority", out var priProp) == true ? ParsePriority(priProp.GetString()) : (TaskPriority?)null;
        var tag = arguments?.TryGetProperty("tag", out var tagProp) == true ? tagProp.GetString() : null;
        var projectId = arguments?.TryGetProperty("projectId", out var projProp) == true ? projProp.GetString() : null;
        var includeCompleted = arguments?.TryGetProperty("includeCompleted", out var incProp) == true && incProp.GetBoolean();
        var format = arguments?.TryGetProperty("format", out var fmtProp) == true ? fmtProp.GetString() : "text";

        var tasks = _store.List(status, priority, tag, projectId, includeCompleted);

        if (tasks.Count == 0)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = "No tasks found" } }
            };
        }

        string output;
        if (format == "markdown")
        {
            output = FormatListMarkdown(tasks, projectId);
        }
        else if (format == "json")
        {
            output = JsonSerializer.Serialize(tasks, JsonOptions);
        }
        else
        {
            output = $"Found {tasks.Count} task(s):\n" + string.Join("\n", tasks.Select(t =>
            {
                var indent = t.ParentId != null ? "  └─ " : "";
                return $"{indent}[{t.Id}] {StatusIcon(t.Status)} {PriorityIcon(t.Priority)} {t.Title}";
            }));
        }

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = output },
                new() { Text = JsonSerializer.Serialize(tasks, JsonOptions) }
            }
        };
    }

    private CallToolResult HandleTaskTree(JsonElement? arguments)
    {
        var projectId = arguments?.TryGetProperty("projectId", out var projProp) == true ? projProp.GetString() : null;
        var includeCompleted = arguments?.TryGetProperty("includeCompleted", out var incProp) == true && incProp.GetBoolean();
        var format = arguments?.TryGetProperty("format", out var fmtProp) == true ? fmtProp.GetString() : "text";

        var tree = _store.GetTree(projectId, includeCompleted);

        if (tree.Count == 0)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = "No tasks found" } }
            };
        }

        var flat = _store.Flatten(tree);
        string output;

        if (format == "markdown")
        {
            output = FormatTreeMarkdown(tree, projectId);
        }
        else if (format == "json")
        {
            output = JsonSerializer.Serialize(flat, JsonOptions);
        }
        else
        {
            output = $"Task tree ({flat.Count} tasks):\n{FormatTree(tree)}";
        }

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = output },
                new() { Text = JsonSerializer.Serialize(flat, JsonOptions) }
            }
        };
    }

    private string FormatTree(List<TaskItem> tree)
    {
        var lines = new List<string>();

        void Walk(TaskItem item, string prefix, bool isLast)
        {
            var connector = isLast ? "└── " : "├── ";
            lines.Add($"{prefix}{connector}[{item.Id}] {StatusIcon(item.Status)} {PriorityIcon(item.Priority)} {item.Title}");

            var childPrefix = prefix + (isLast ? "    " : "│   ");
            var children = item.Children.OrderBy(c => c.Order).ToList();
            for (int i = 0; i < children.Count; i++)
            {
                Walk(children[i], childPrefix, i == children.Count - 1);
            }
        }

        var roots = tree.OrderBy(t => t.Order).ToList();
        for (int i = 0; i < roots.Count; i++)
        {
            Walk(roots[i], "", i == roots.Count - 1);
        }

        return string.Join("\n", lines);
    }

    private CallToolResult HandleTaskGet(JsonElement? arguments)
    {
        var id = arguments?.GetProperty("id").GetString() ?? throw new ArgumentException("ID is required");
        var task = _store.Get(id);

        if (task == null)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = $"Task not found: {id}" } },
                IsError = true
            };
        }

        var subtasks = _store.GetSubtasks(task.Id);
        var subtaskInfo = subtasks.Count > 0
            ? $"\n\nSubtasks ({subtasks.Count}):\n" + string.Join("\n", subtasks.Select(s => $"  [{s.Id}] {StatusIcon(s.Status)} {s.Title}"))
            : "";

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = JsonSerializer.Serialize(task, JsonOptions) + subtaskInfo }
            }
        };
    }

    private CallToolResult HandleTaskSubtasks(JsonElement? arguments)
    {
        var id = arguments?.GetProperty("id").GetString() ?? throw new ArgumentException("ID is required");
        var recursive = arguments?.TryGetProperty("recursive", out var recProp) == true && recProp.GetBoolean();

        var subtasks = _store.GetSubtasks(id, recursive);

        if (subtasks.Count == 0)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = $"No subtasks found for {id}" } }
            };
        }

        var summary = string.Join("\n", subtasks.Select(t =>
            $"[{t.Id}] {StatusIcon(t.Status)} {PriorityIcon(t.Priority)} {t.Title}"));

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = $"Found {subtasks.Count} subtask(s):\n{summary}" },
                new() { Text = JsonSerializer.Serialize(subtasks, JsonOptions) }
            }
        };
    }

    private CallToolResult HandleTaskUpdate(JsonElement? arguments)
    {
        var id = arguments?.GetProperty("id").GetString() ?? throw new ArgumentException("ID is required");
        var title = arguments?.TryGetProperty("title", out var titleProp) == true ? titleProp.GetString() : null;
        var description = arguments?.TryGetProperty("description", out var descProp) == true ? descProp.GetString() : null;
        var status = arguments?.TryGetProperty("status", out var statusProp) == true ? ParseStatus(statusProp.GetString()) : null;
        var priority = arguments?.TryGetProperty("priority", out var priProp) == true ? ParsePriority(priProp.GetString()) : (TaskPriority?)null;
        var tags = arguments?.TryGetProperty("tags", out var tagsProp) == true
            ? tagsProp.EnumerateArray().Select(t => t.GetString()!).ToList()
            : null;
        var dueDate = arguments?.TryGetProperty("dueDate", out var dueProp) == true && dueProp.GetString() is string ds
            ? DateTime.Parse(ds)
            : (DateTime?)null;
        var parentId = arguments?.TryGetProperty("parentId", out var parentProp) == true ? parentProp.GetString() : null;
        var order = arguments?.TryGetProperty("order", out var orderProp) == true ? orderProp.GetInt32() : (int?)null;

        var task = _store.Update(id, title, description, status, priority, tags, dueDate, parentId, order);

        if (task == null)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = $"Task not found: {id}" } },
                IsError = true
            };
        }

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = $"Updated task {task.Id}" },
                new() { Text = JsonSerializer.Serialize(task, JsonOptions) }
            }
        };
    }

    private CallToolResult HandleTaskDelete(JsonElement? arguments)
    {
        var id = arguments?.GetProperty("id").GetString() ?? throw new ArgumentException("ID is required");
        var cascade = arguments?.TryGetProperty("cascade", out var cascadeProp) == true && cascadeProp.GetBoolean();

        var deleted = _store.Delete(id, cascade);

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = deleted ? $"Deleted task {id}" + (cascade ? " and all subtasks" : "") : $"Task not found: {id}" }
            },
            IsError = !deleted
        };
    }

    private CallToolResult HandleTaskComplete(JsonElement? arguments)
    {
        var id = arguments?.GetProperty("id").GetString() ?? throw new ArgumentException("ID is required");
        var recursive = arguments?.TryGetProperty("recursive", out var recProp) == true && recProp.GetBoolean();

        var completed = _store.Complete(id, recursive);

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = completed ? $"Completed task {id}" + (recursive ? " and all subtasks" : "") : $"Task not found: {id}" }
            },
            IsError = !completed
        };
    }

    private CallToolResult HandleTaskStart(JsonElement? arguments)
    {
        var id = arguments?.GetProperty("id").GetString() ?? throw new ArgumentException("ID is required");
        var started = _store.Start(id);

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = started ? $"Started task {id}" : $"Task not found: {id}" }
            },
            IsError = !started
        };
    }

    private CallToolResult HandleTaskBlock(JsonElement? arguments)
    {
        var id = arguments?.GetProperty("id").GetString() ?? throw new ArgumentException("ID is required");
        var blocked = _store.Block(id);

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = blocked ? $"Blocked task {id}" : $"Task not found: {id}" }
            },
            IsError = !blocked
        };
    }

    private CallToolResult HandleTaskMove(JsonElement? arguments)
    {
        var id = arguments?.GetProperty("id").GetString() ?? throw new ArgumentException("ID is required");
        var projectId = arguments?.TryGetProperty("projectId", out var projProp) == true ? projProp.GetString() : null;

        _store.MoveToProject(id, projectId);

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = $"Moved task {id} to project {projectId ?? "(none)"}" }
            }
        };
    }

    private CallToolResult HandleTaskStats(JsonElement? arguments)
    {
        var projectId = arguments?.TryGetProperty("projectId", out var projProp) == true ? projProp.GetString() : null;

        var stats = _store.GetStats(projectId);

        var summary = $"""
            Task Statistics{(projectId != null ? $" (Project: {projectId})" : "")}
            ═══════════════════════════════════
            Total:       {stats.Total}
            Todo:        {stats.Todo}
            In Progress: {stats.InProgress}
            Done:        {stats.Done}
            Blocked:     {stats.Blocked}
            Cancelled:   {stats.Cancelled}

            By Priority:
              Critical:  {stats.ByPriority.GetValueOrDefault(TaskPriority.Critical, 0)}
              High:      {stats.ByPriority.GetValueOrDefault(TaskPriority.High, 0)}
              Medium:    {stats.ByPriority.GetValueOrDefault(TaskPriority.Medium, 0)}
              Low:       {stats.ByPriority.GetValueOrDefault(TaskPriority.Low, 0)}
            """;

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = summary },
                new() { Text = JsonSerializer.Serialize(stats, JsonOptions) }
            }
        };
    }

    // ===== DEPENDENCY HANDLERS =====

    private CallToolResult HandleDependencyAdd(JsonElement? arguments)
    {
        var taskId = arguments?.TryGetProperty("taskId", out var tidProp) == true ? tidProp.GetString() : null;
        var dependsOnId = arguments?.TryGetProperty("dependsOnId", out var depProp) == true ? depProp.GetString() : null;

        if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(dependsOnId))
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = "Both taskId and dependsOnId are required" } },
                IsError = true
            };
        }

        var task = _store.Get(taskId);
        var dep = _store.Get(dependsOnId);

        if (task == null)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = $"Task not found: {taskId}" } },
                IsError = true
            };
        }
        if (dep == null)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = $"Dependency task not found: {dependsOnId}" } },
                IsError = true
            };
        }

        if (_store.AddDependency(task.Id, dep.Id))
        {
            return new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new() { Text = $"Added dependency: '{task.Title}' now depends on '{dep.Title}'" },
                    new() { Text = JsonSerializer.Serialize(new { taskId = task.Id, dependsOnId = dep.Id }, JsonOptions) }
                }
            };
        }

        return new CallToolResult
        {
            Content = new List<ToolContent> { new() { Text = "Could not add dependency (may create circular reference or already exists)" } },
            IsError = true
        };
    }

    private CallToolResult HandleDependencyRemove(JsonElement? arguments)
    {
        var taskId = arguments?.TryGetProperty("taskId", out var tidProp) == true ? tidProp.GetString() : null;
        var dependsOnId = arguments?.TryGetProperty("dependsOnId", out var depProp) == true ? depProp.GetString() : null;

        if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(dependsOnId))
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = "Both taskId and dependsOnId are required" } },
                IsError = true
            };
        }

        var task = _store.Get(taskId);
        if (task == null)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = $"Task not found: {taskId}" } },
                IsError = true
            };
        }

        var dep = _store.Get(dependsOnId);
        if (_store.RemoveDependency(task.Id, dep?.Id ?? dependsOnId))
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = "Dependency removed" } }
            };
        }

        return new CallToolResult
        {
            Content = new List<ToolContent> { new() { Text = "Dependency not found" } },
            IsError = true
        };
    }

    private CallToolResult HandleDependencyList(JsonElement? arguments)
    {
        var taskId = arguments?.TryGetProperty("taskId", out var tidProp) == true ? tidProp.GetString() : null;

        if (string.IsNullOrEmpty(taskId))
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = "taskId is required" } },
                IsError = true
            };
        }

        var task = _store.Get(taskId);
        if (task == null)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = $"Task not found: {taskId}" } },
                IsError = true
            };
        }

        var dependencies = _store.GetDependencies(task.Id);
        var dependents = _store.GetDependents(task.Id);
        var blocking = _store.GetBlockingDependencies(task.Id);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Dependencies for: {task.Title} [{task.Id}]");
        sb.AppendLine();

        sb.AppendLine("Depends on:");
        if (dependencies.Any())
        {
            foreach (var dep in dependencies)
            {
                var status = dep.Status == Models.TaskStatus.Done ? "✓" : "○";
                var isBlocking = blocking.Contains(dep.Id) ? " (BLOCKING)" : "";
                sb.AppendLine($"  {status} {dep.Title} [{dep.Id}]{isBlocking}");
            }
        }
        else
        {
            sb.AppendLine("  (none)");
        }

        sb.AppendLine();
        sb.AppendLine("Required by:");
        if (dependents.Any())
        {
            foreach (var d in dependents)
            {
                sb.AppendLine($"  → {d.Title} [{d.Id}]");
            }
        }
        else
        {
            sb.AppendLine("  (none)");
        }

        var data = new
        {
            taskId = task.Id,
            taskTitle = task.Title,
            dependsOn = dependencies.Select(d => new { d.Id, d.Title, d.Status }),
            requiredBy = dependents.Select(d => new { d.Id, d.Title, d.Status }),
            blockingDependencies = blocking,
            canStart = blocking.Count == 0
        };

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = sb.ToString() },
                new() { Text = JsonSerializer.Serialize(data, JsonOptions) }
            }
        };
    }

    private CallToolResult HandleDependencyCheck(JsonElement? arguments)
    {
        var taskId = arguments?.TryGetProperty("taskId", out var tidProp) == true ? tidProp.GetString() : null;

        if (string.IsNullOrEmpty(taskId))
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = "taskId is required" } },
                IsError = true
            };
        }

        var task = _store.Get(taskId);
        if (task == null)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { new() { Text = $"Task not found: {taskId}" } },
                IsError = true
            };
        }

        var canStart = _store.CanStart(task.Id);
        var blocking = _store.GetBlockingDependencies(task.Id);

        var sb = new System.Text.StringBuilder();
        if (canStart)
        {
            sb.AppendLine($"✓ Task '{task.Title}' is ready to start (no blocking dependencies)");
        }
        else
        {
            sb.AppendLine($"⚠ Task '{task.Title}' has {blocking.Count} blocking dependencies:");
            foreach (var depId in blocking)
            {
                var dep = _store.Get(depId);
                if (dep != null)
                    sb.AppendLine($"  ○ {dep.Title} [{dep.Id}] - {dep.Status}");
            }
        }

        var data = new
        {
            taskId = task.Id,
            taskTitle = task.Title,
            canStart,
            blockingCount = blocking.Count,
            blockingDependencies = blocking.Select(id =>
            {
                var dep = _store.Get(id);
                return new { id, title = dep?.Title, status = dep?.Status.ToString() };
            })
        };

        return new CallToolResult
        {
            Content = new List<ToolContent>
            {
                new() { Text = sb.ToString() },
                new() { Text = JsonSerializer.Serialize(data, JsonOptions) }
            }
        };
    }

    // ===== HELPERS =====

    private static TaskPriority ParsePriority(string? value) => value?.ToLowerInvariant() switch
    {
        "low" => TaskPriority.Low,
        "high" => TaskPriority.High,
        "critical" => TaskPriority.Critical,
        _ => TaskPriority.Medium
    };

    private static Models.TaskStatus? ParseStatus(string? value) => value?.ToLowerInvariant() switch
    {
        "todo" => Models.TaskStatus.Todo,
        "inprogress" => Models.TaskStatus.InProgress,
        "done" => Models.TaskStatus.Done,
        "blocked" => Models.TaskStatus.Blocked,
        "cancelled" => Models.TaskStatus.Cancelled,
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
            var pct = flat.Count > 0 ? done * 100 / flat.Count : 0;
            sb.AppendLine($"**Overall Progress:** {done}/{flat.Count} tasks ({pct}%)");
            sb.AppendLine();

            // List projects summary
            if (projects.Count > 0)
            {
                sb.AppendLine("## Projects");
                sb.AppendLine();
                foreach (var proj in projects)
                {
                    var projStats = _store.GetStats(proj.Id);
                    var projPct = projStats.Total > 0 ? projStats.Done * 100 / projStats.Total : 0;
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

                var projectRoots = tree.Where(t => t.ProjectId == group.Key).ToList();

                void WriteGroupTask(TaskItem task, int depth)
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
                        WriteGroupTask(child, depth + 1);
                    }
                }

                foreach (var root in projectRoots.OrderBy(t => t.Order))
                {
                    WriteGroupTask(root, 0);
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
        var allPct = allFlat.Count > 0 ? allDone * 100 / allFlat.Count : 0;
        sb.AppendLine($"**Progress:** {allDone}/{allFlat.Count} tasks ({allPct}%)");
        sb.AppendLine();

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

        return sb.ToString();
    }
}
