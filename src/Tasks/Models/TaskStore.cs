using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Tasks.Models;

/// <summary>
/// Data stored in each project file
/// </summary>
public class ProjectData
{
    public Project? Project { get; set; }
    public List<TaskItem> Tasks { get; set; } = new();
}

public class TaskStore
{
    private readonly string _dataDir;
    private readonly TasksOptions _options;
    private const string DefaultProjectFile = "_default.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Constructor for DI with IOptions
    /// </summary>
    public TaskStore(IOptions<TasksOptions> options)
    {
        _options = options.Value;
        _dataDir = TskConfigHelper.GetDataDirectory(_options);
        EnsureDirectoryExists();
    }

    /// <summary>
    /// Constructor for testing or manual instantiation
    /// </summary>
    public TaskStore(string dataDir, TasksOptions? options = null)
    {
        _options = options ?? new TasksOptions();
        _dataDir = dataDir;
        EnsureDirectoryExists();
    }

    private void EnsureDirectoryExists()
    {
        Directory.CreateDirectory(_dataDir);
    }

    public TasksOptions Options => _options;

    private string GetProjectFilePath(string? projectId) =>
        Path.Combine(_dataDir, string.IsNullOrEmpty(projectId) ? DefaultProjectFile : $"{projectId}.json");

    private ProjectData LoadProjectData(string? projectId)
    {
        var path = GetProjectFilePath(projectId);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ProjectData>(json, JsonOptions) ?? new();
        }
        return new();
    }

    private void SaveProjectData(string? projectId, ProjectData data)
    {
        var path = GetProjectFilePath(projectId);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(path, json);
    }

    private List<string> GetAllProjectFiles()
    {
        if (!Directory.Exists(_dataDir))
            return new();
        return Directory.GetFiles(_dataDir, "*.json").ToList();
    }

    // ===== PROJECT OPERATIONS =====

    public Project AddProject(string name, string? description = null)
    {
        var project = new Project
        {
            Name = name,
            Description = description
        };

        var data = new ProjectData
        {
            Project = project,
            Tasks = new()
        };
        SaveProjectData(project.Id, data);
        return project;
    }

    public Project? GetProject(string id)
    {
        // Try to load the project file directly
        var path = GetProjectFilePath(id);
        if (File.Exists(path))
        {
            var data = LoadProjectData(id);
            return data.Project;
        }

        // Search all project files for a matching ID prefix
        foreach (var file in GetAllProjectFiles())
        {
            if (Path.GetFileName(file) == DefaultProjectFile) continue;
            var data = LoadProjectDataFromFile(file);
            if (data.Project?.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase) == true)
                return data.Project;
        }
        return null;
    }

    private ProjectData LoadProjectDataFromFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ProjectData>(json, JsonOptions) ?? new();
        }
        return new();
    }

    public Project? GetProjectByName(string name)
    {
        foreach (var file in GetAllProjectFiles())
        {
            if (Path.GetFileName(file) == DefaultProjectFile) continue;
            var data = LoadProjectDataFromFile(file);
            if (data.Project?.Name.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                return data.Project;
        }
        return null;
    }

    public List<Project> ListProjects()
    {
        var projects = new List<Project>();
        foreach (var file in GetAllProjectFiles())
        {
            if (Path.GetFileName(file) == DefaultProjectFile) continue;
            var data = LoadProjectDataFromFile(file);
            if (data.Project != null)
                projects.Add(data.Project);
        }
        return projects.OrderBy(p => p.Name).ToList();
    }

    public Project? UpdateProject(string id, string? name = null, string? description = null)
    {
        var project = GetProject(id) ?? GetProjectByName(id);
        if (project == null) return null;

        var data = LoadProjectData(project.Id);
        if (data.Project == null) return null;

        if (name != null) data.Project.Name = name;
        if (description != null) data.Project.Description = description;
        data.Project.UpdatedAt = DateTime.UtcNow;

        SaveProjectData(project.Id, data);
        return data.Project;
    }

    public bool DeleteProject(string id, bool cascade = false)
    {
        var project = GetProject(id) ?? GetProjectByName(id);
        if (project == null) return false;

        if (!cascade)
        {
            // Move tasks to default (unassigned)
            var projectData = LoadProjectData(project.Id);
            var defaultData = LoadProjectData(null);
            foreach (var task in projectData.Tasks)
            {
                task.ProjectId = null;
                defaultData.Tasks.Add(task);
            }
            SaveProjectData(null, defaultData);
        }

        // Delete the project file
        var path = GetProjectFilePath(project.Id);
        if (File.Exists(path))
            File.Delete(path);
        return true;
    }

    // ===== TASK OPERATIONS =====

    public TaskItem Add(string title, string? description = null, TaskPriority priority = TaskPriority.Medium,
        List<string>? tags = null, DateTime? dueDate = null, string? parentId = null, string? projectId = null)
    {
        TaskItem? parent = null;
        if (!string.IsNullOrEmpty(parentId))
        {
            parent = Get(parentId);
            if (parent == null)
                throw new ArgumentException($"Parent task not found: {parentId}");
            projectId ??= parent.ProjectId;
        }

        string? resolvedProjectId = null;
        if (!string.IsNullOrEmpty(projectId))
        {
            var project = GetProject(projectId) ?? GetProjectByName(projectId);
            if (project == null)
                throw new ArgumentException($"Project not found: {projectId}");
            resolvedProjectId = project.Id;
        }

        var data = LoadProjectData(resolvedProjectId);
        var siblings = data.Tasks.Where(t => t.ParentId == parent?.Id);
        var order = siblings.Any() ? siblings.Max(t => t.Order) + 1 : 0;

        var task = new TaskItem
        {
            Title = title,
            Description = description,
            Priority = priority,
            Tags = tags ?? new(),
            DueDate = dueDate,
            ParentId = parent?.Id,
            ProjectId = resolvedProjectId,
            Order = order
        };
        data.Tasks.Add(task);
        SaveProjectData(resolvedProjectId, data);
        return task;
    }

    public TaskItem? Get(string id)
    {
        // Search all project files
        foreach (var file in GetAllProjectFiles())
        {
            var data = LoadProjectDataFromFile(file);
            var task = data.Tasks.FirstOrDefault(t => t.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
            if (task != null) return task;
        }
        return null;
    }

    public List<TaskItem> List(TaskStatus? status = null, TaskPriority? priority = null, string? tag = null,
        string? projectId = null, bool includeCompleted = false)
    {
        IEnumerable<TaskItem> query;

        if (!string.IsNullOrEmpty(projectId))
        {
            var project = GetProject(projectId) ?? GetProjectByName(projectId);
            if (project != null)
            {
                var data = LoadProjectData(project.Id);
                query = data.Tasks;
            }
            else
            {
                return new();
            }
        }
        else
        {
            // Get tasks from all project files
            var allTasks = new List<TaskItem>();
            foreach (var file in GetAllProjectFiles())
            {
                var data = LoadProjectDataFromFile(file);
                allTasks.AddRange(data.Tasks);
            }
            query = allTasks;
        }

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        else if (!includeCompleted)
            query = query.Where(t => t.Status != TaskStatus.Done && t.Status != TaskStatus.Cancelled);

        if (priority.HasValue)
            query = query.Where(t => t.Priority == priority.Value);

        if (!string.IsNullOrEmpty(tag))
            query = query.Where(t => t.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

        return query.OrderByDescending(t => t.Priority).ThenBy(t => t.Order).ThenBy(t => t.CreatedAt).ToList();
    }

    public List<TaskItem> GetTree(string? projectId = null, bool includeCompleted = false)
    {
        var tasks = List(projectId: projectId, includeCompleted: includeCompleted);
        return BuildTree(tasks);
    }

    public List<TaskItem> GetSubtasks(string parentId, bool recursive = false)
    {
        var parent = Get(parentId);
        if (parent == null) return new();

        var data = LoadProjectData(parent.ProjectId);
        var direct = data.Tasks.Where(t => t.ParentId == parent.Id).OrderBy(t => t.Order).ToList();
        if (!recursive) return direct;

        var result = new List<TaskItem>();
        foreach (var child in direct)
        {
            result.Add(child);
            result.AddRange(GetSubtasks(child.Id, true));
        }
        return result;
    }

    private List<TaskItem> BuildTree(List<TaskItem> tasks)
    {
        var lookup = tasks.ToDictionary(t => t.Id);
        var roots = new List<TaskItem>();

        foreach (var task in tasks)
        {
            task.Children = new List<TaskItem>();
            task.Depth = 0;
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

        void SetDepth(TaskItem item, int depth)
        {
            item.Depth = depth;
            foreach (var child in item.Children.OrderBy(c => c.Order))
            {
                SetDepth(child, depth + 1);
            }
        }

        foreach (var root in roots.OrderBy(r => r.Order))
        {
            SetDepth(root, 0);
        }

        return roots;
    }

    public List<TaskItem> Flatten(List<TaskItem> tree)
    {
        var result = new List<TaskItem>();
        void Walk(TaskItem item)
        {
            result.Add(item);
            foreach (var child in item.Children.OrderBy(c => c.Order))
            {
                Walk(child);
            }
        }
        foreach (var root in tree)
        {
            Walk(root);
        }
        return result;
    }

    public TaskItem? Update(string id, string? title = null, string? description = null, TaskStatus? status = null,
        TaskPriority? priority = null, List<string>? tags = null, DateTime? dueDate = null,
        string? parentId = null, int? order = null)
    {
        var task = Get(id);
        if (task == null) return null;

        var data = LoadProjectData(task.ProjectId);
        var taskInData = data.Tasks.FirstOrDefault(t => t.Id == task.Id);
        if (taskInData == null) return null;

        if (title != null) taskInData.Title = title;
        if (description != null) taskInData.Description = description;
        if (status.HasValue)
        {
            taskInData.Status = status.Value;
            if (status == TaskStatus.Done)
                taskInData.CompletedAt = DateTime.UtcNow;
            else
                taskInData.CompletedAt = null;
        }
        if (priority.HasValue) taskInData.Priority = priority.Value;
        if (tags != null) taskInData.Tags = tags;
        if (dueDate.HasValue) taskInData.DueDate = dueDate;
        if (order.HasValue) taskInData.Order = order.Value;

        if (parentId != null)
        {
            if (parentId == "" || parentId.ToLower() == "none" || parentId.ToLower() == "null")
            {
                taskInData.ParentId = null;
            }
            else
            {
                var newParent = Get(parentId);
                if (newParent != null && !WouldCreateCycle(taskInData.Id, newParent.Id))
                {
                    taskInData.ParentId = newParent.Id;
                }
            }
        }

        taskInData.UpdatedAt = DateTime.UtcNow;
        SaveProjectData(task.ProjectId, data);
        return taskInData;
    }

    private bool WouldCreateCycle(string taskId, string newParentId)
    {
        var current = Get(newParentId);
        while (current != null)
        {
            if (current.Id == taskId) return true;
            current = current.ParentId != null ? Get(current.ParentId) : null;
        }
        return false;
    }

    public bool Delete(string id, bool cascade = false)
    {
        var task = Get(id);
        if (task == null) return false;

        var data = LoadProjectData(task.ProjectId);
        var taskInData = data.Tasks.FirstOrDefault(t => t.Id == task.Id);
        if (taskInData == null) return false;

        if (cascade)
        {
            var descendants = GetSubtasks(task.Id, recursive: true);
            foreach (var desc in descendants)
            {
                data.Tasks.RemoveAll(t => t.Id == desc.Id);
            }
        }
        else
        {
            foreach (var child in data.Tasks.Where(t => t.ParentId == task.Id))
            {
                child.ParentId = task.ParentId;
            }
        }

        data.Tasks.Remove(taskInData);
        SaveProjectData(task.ProjectId, data);
        return true;
    }

    public bool Complete(string id, bool recursive = false)
    {
        var result = Update(id, status: TaskStatus.Done) != null;

        if (result && recursive)
        {
            foreach (var child in GetSubtasks(id, recursive: true))
            {
                Update(child.Id, status: TaskStatus.Done);
            }
        }

        return result;
    }

    public bool Start(string id)
    {
        return Update(id, status: TaskStatus.InProgress) != null;
    }

    public bool Block(string id)
    {
        return Update(id, status: TaskStatus.Blocked) != null;
    }

    // ===== BULK OPERATIONS =====

    public List<TaskItem> AddMany(List<TaskItemInput> items, string? projectId = null, string? parentId = null)
    {
        var result = new List<TaskItem>();
        foreach (var item in items)
        {
            var task = Add(
                item.Title,
                item.Description,
                item.Priority ?? TaskPriority.Medium,
                item.Tags,
                item.DueDate,
                item.ParentId ?? parentId,
                item.ProjectId ?? projectId
            );
            result.Add(task);

            if (item.Subtasks?.Count > 0)
            {
                var children = AddMany(item.Subtasks, projectId ?? task.ProjectId, task.Id);
                result.AddRange(children);
            }
        }
        return result;
    }

    public void Reorder(string id, int newOrder)
    {
        var task = Get(id);
        if (task == null) return;

        var data = LoadProjectData(task.ProjectId);
        var taskInData = data.Tasks.FirstOrDefault(t => t.Id == task.Id);
        if (taskInData == null) return;

        var siblings = data.Tasks
            .Where(t => t.ParentId == taskInData.ParentId && t.Id != taskInData.Id)
            .OrderBy(t => t.Order)
            .ToList();

        var idx = 0;
        foreach (var sibling in siblings)
        {
            if (idx == newOrder) idx++;
            sibling.Order = idx++;
        }
        taskInData.Order = newOrder;

        SaveProjectData(task.ProjectId, data);
    }

    public void MoveToProject(string taskId, string? projectId)
    {
        var task = Get(taskId);
        if (task == null) return;

        string? resolvedProjectId = null;
        if (!string.IsNullOrEmpty(projectId))
        {
            var project = GetProject(projectId) ?? GetProjectByName(projectId);
            resolvedProjectId = project?.Id;
        }

        // Get all tasks to move (task + descendants)
        var subtasks = GetSubtasks(taskId, recursive: true);
        var tasksToMove = new List<TaskItem> { task };
        tasksToMove.AddRange(subtasks);

        // Remove from source project
        var sourceData = LoadProjectData(task.ProjectId);
        foreach (var t in tasksToMove)
        {
            sourceData.Tasks.RemoveAll(st => st.Id == t.Id);
        }
        SaveProjectData(task.ProjectId, sourceData);

        // Update project IDs and add to target project
        var targetData = LoadProjectData(resolvedProjectId);
        foreach (var t in tasksToMove)
        {
            t.ProjectId = resolvedProjectId;
        }
        task.ParentId = null; // Root task in new project
        targetData.Tasks.AddRange(tasksToMove);
        SaveProjectData(resolvedProjectId, targetData);
    }

    // ===== STATISTICS =====

    public TaskStats GetStats(string? projectId = null)
    {
        List<TaskItem> list;

        if (!string.IsNullOrEmpty(projectId))
        {
            var project = GetProject(projectId) ?? GetProjectByName(projectId);
            if (project != null)
            {
                var data = LoadProjectData(project.Id);
                list = data.Tasks;
            }
            else
            {
                list = new();
            }
        }
        else
        {
            // Get all tasks from all project files
            list = new List<TaskItem>();
            foreach (var file in GetAllProjectFiles())
            {
                var data = LoadProjectDataFromFile(file);
                list.AddRange(data.Tasks);
            }
        }

        return new TaskStats
        {
            Total = list.Count,
            Todo = list.Count(t => t.Status == TaskStatus.Todo),
            InProgress = list.Count(t => t.Status == TaskStatus.InProgress),
            Done = list.Count(t => t.Status == TaskStatus.Done),
            Blocked = list.Count(t => t.Status == TaskStatus.Blocked),
            Cancelled = list.Count(t => t.Status == TaskStatus.Cancelled),
            ByPriority = list.GroupBy(t => t.Priority).ToDictionary(g => g.Key, g => g.Count())
        };
    }

    // ===== DEPENDENCY OPERATIONS =====

    public bool AddDependency(string taskId, string dependsOnId)
    {
        var task = Get(taskId);
        var dependency = Get(dependsOnId);
        if (task == null || dependency == null) return false;

        // Prevent self-dependency
        if (task.Id == dependency.Id) return false;

        // Prevent circular dependencies
        if (WouldCreateCircularDependency(task.Id, dependency.Id)) return false;

        var data = LoadProjectData(task.ProjectId);
        var taskInData = data.Tasks.FirstOrDefault(t => t.Id == task.Id);
        if (taskInData == null) return false;

        if (!taskInData.DependsOn.Contains(dependency.Id))
        {
            taskInData.DependsOn.Add(dependency.Id);
            taskInData.UpdatedAt = DateTime.UtcNow;
            SaveProjectData(task.ProjectId, data);
        }
        return true;
    }

    public bool RemoveDependency(string taskId, string dependsOnId)
    {
        var task = Get(taskId);
        var dependency = Get(dependsOnId);
        if (task == null || dependency == null) return false;

        var data = LoadProjectData(task.ProjectId);
        var taskInData = data.Tasks.FirstOrDefault(t => t.Id == task.Id);
        if (taskInData == null) return false;

        var removed = taskInData.DependsOn.Remove(dependency.Id);
        if (removed)
        {
            taskInData.UpdatedAt = DateTime.UtcNow;
            SaveProjectData(task.ProjectId, data);
        }
        return removed;
    }

    public List<TaskItem> GetDependencies(string taskId)
    {
        var task = Get(taskId);
        if (task == null) return new();

        var results = new List<TaskItem>();
        foreach (var depId in task.DependsOn)
        {
            var dep = Get(depId);
            if (dep != null) results.Add(dep);
        }
        return results;
    }

    public List<TaskItem> GetDependents(string taskId)
    {
        var task = Get(taskId);
        if (task == null) return new();

        var results = new List<TaskItem>();
        foreach (var file in GetAllProjectFiles())
        {
            var data = LoadProjectDataFromFile(file);
            results.AddRange(data.Tasks.Where(t => t.DependsOn.Contains(task.Id)));
        }
        return results;
    }

    public List<string> GetBlockingDependencies(string taskId)
    {
        var task = Get(taskId);
        if (task == null) return new();

        return task.DependsOn
            .Where(depId =>
            {
                var dep = Get(depId);
                return dep != null && dep.Status != TaskStatus.Done && dep.Status != TaskStatus.Cancelled;
            })
            .ToList();
    }

    public bool CanStart(string taskId)
    {
        return GetBlockingDependencies(taskId).Count == 0;
    }

    public bool CanComplete(string taskId)
    {
        return GetBlockingDependencies(taskId).Count == 0;
    }

    private bool WouldCreateCircularDependency(string taskId, string newDependencyId)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(newDependencyId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == taskId) return true;
            if (visited.Contains(current)) continue;
            visited.Add(current);

            var task = Get(current);
            if (task != null)
            {
                foreach (var depId in task.DependsOn)
                {
                    queue.Enqueue(depId);
                }
            }
        }
        return false;
    }

    public void PopulateDependencyInfo(TaskItem task)
    {
        var blocking = GetBlockingDependencies(task.Id);
        task.HasBlockingDependencies = blocking.Count > 0;
        task.BlockingDependencyIds = blocking;
    }

    public void PopulateDependencyInfo(List<TaskItem> tasks)
    {
        foreach (var task in tasks)
        {
            PopulateDependencyInfo(task);
        }
    }
}

public class TaskItemInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority? Priority { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? DueDate { get; set; }
    public string? ParentId { get; set; }
    public string? ProjectId { get; set; }
    public List<TaskItemInput>? Subtasks { get; set; }
}

public class TaskStats
{
    public int Total { get; set; }
    public int Todo { get; set; }
    public int InProgress { get; set; }
    public int Done { get; set; }
    public int Blocked { get; set; }
    public int Cancelled { get; set; }
    public Dictionary<TaskPriority, int> ByPriority { get; set; } = new();
}
