using Tasks.Models;
using TaskStatus = Tasks.Models.TaskStatus;

namespace Tasks.Tests;

public class TaskStoreTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly TaskStore _store;

    public TaskStoreTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"tsk_test_{Guid.NewGuid():N}.json");
        _store = new TaskStore(_testFilePath);
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }

    #region Project Tests

    [Fact]
    public void AddProject_CreatesProjectWithName()
    {
        var project = _store.AddProject("Test Project", "A description");

        Assert.NotNull(project);
        Assert.Equal("Test Project", project.Name);
        Assert.Equal("A description", project.Description);
        Assert.Equal(8, project.Id.Length);
    }

    [Fact]
    public void GetProject_ReturnsProjectById()
    {
        var created = _store.AddProject("My Project");

        var found = _store.GetProject(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public void GetProject_SupportsPrefixMatching()
    {
        var created = _store.AddProject("Prefix Project");
        var prefix = created.Id[..4];

        var found = _store.GetProject(prefix);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public void GetProjectByName_ReturnsMatchingProject()
    {
        _store.AddProject("Named Project");

        var found = _store.GetProjectByName("Named Project");

        Assert.NotNull(found);
        Assert.Equal("Named Project", found.Name);
    }

    [Fact]
    public void GetProjectByName_IsCaseInsensitive()
    {
        _store.AddProject("Case Test");

        var found = _store.GetProjectByName("CASE TEST");

        Assert.NotNull(found);
    }

    [Fact]
    public void ListProjects_ReturnsOrderedByName()
    {
        _store.AddProject("Zebra");
        _store.AddProject("Alpha");
        _store.AddProject("Middle");

        var projects = _store.ListProjects();

        Assert.Equal(3, projects.Count);
        Assert.Equal("Alpha", projects[0].Name);
        Assert.Equal("Middle", projects[1].Name);
        Assert.Equal("Zebra", projects[2].Name);
    }

    [Fact]
    public void UpdateProject_ModifiesProject()
    {
        var project = _store.AddProject("Original");

        var updated = _store.UpdateProject(project.Id, name: "Updated", description: "New desc");

        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
        Assert.Equal("New desc", updated.Description);
    }

    [Fact]
    public void DeleteProject_RemovesProject()
    {
        var project = _store.AddProject("To Delete");

        var result = _store.DeleteProject(project.Id);

        Assert.True(result);
        Assert.Null(_store.GetProject(project.Id));
    }

    [Fact]
    public void DeleteProject_WithCascade_RemovesTasks()
    {
        var project = _store.AddProject("Cascade Test");
        _store.Add("Task 1", projectId: project.Id);
        _store.Add("Task 2", projectId: project.Id);

        _store.DeleteProject(project.Id, cascade: true);

        var tasks = _store.List(includeCompleted: true);
        Assert.Empty(tasks);
    }

    [Fact]
    public void DeleteProject_WithoutCascade_UnlinksTasksFromProject()
    {
        var project = _store.AddProject("Unlink Test");
        var task = _store.Add("Orphan Task", projectId: project.Id);

        _store.DeleteProject(project.Id, cascade: false);

        var found = _store.Get(task.Id);
        Assert.NotNull(found);
        Assert.Null(found.ProjectId);
    }

    #endregion

    #region Task Add Tests

    [Fact]
    public void Add_CreatesTaskWithTitle()
    {
        var task = _store.Add("Test Task");

        Assert.NotNull(task);
        Assert.Equal("Test Task", task.Title);
        Assert.Equal(TaskStatus.Todo, task.Status);
        Assert.Equal(TaskPriority.Medium, task.Priority);
    }

    [Fact]
    public void Add_CreatesTaskWithAllProperties()
    {
        var dueDate = DateTime.UtcNow.AddDays(7);
        var task = _store.Add(
            title: "Full Task",
            description: "Description",
            priority: TaskPriority.High,
            tags: new List<string> { "tag1", "tag2" },
            dueDate: dueDate
        );

        Assert.Equal("Full Task", task.Title);
        Assert.Equal("Description", task.Description);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Contains("tag1", task.Tags);
        Assert.Contains("tag2", task.Tags);
        Assert.Equal(dueDate, task.DueDate);
    }

    [Fact]
    public void Add_CreatesSubtask_WhenParentIdProvided()
    {
        var parent = _store.Add("Parent Task");
        var child = _store.Add("Child Task", parentId: parent.Id);

        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public void Add_InheritsProjectId_FromParent()
    {
        var project = _store.AddProject("Inherit Test");
        var parent = _store.Add("Parent", projectId: project.Id);
        var child = _store.Add("Child", parentId: parent.Id);

        Assert.Equal(project.Id, child.ProjectId);
    }

    [Fact]
    public void Add_ThrowsException_WhenParentNotFound()
    {
        Assert.Throws<ArgumentException>(() => _store.Add("Orphan", parentId: "nonexistent"));
    }

    [Fact]
    public void Add_ThrowsException_WhenProjectNotFound()
    {
        Assert.Throws<ArgumentException>(() => _store.Add("Orphan", projectId: "nonexistent"));
    }

    [Fact]
    public void Add_AssignsIncrementingOrder()
    {
        var task1 = _store.Add("Task 1");
        var task2 = _store.Add("Task 2");
        var task3 = _store.Add("Task 3");

        Assert.Equal(0, task1.Order);
        Assert.Equal(1, task2.Order);
        Assert.Equal(2, task3.Order);
    }

    #endregion

    #region Task Get/List Tests

    [Fact]
    public void Get_ReturnsTaskById()
    {
        var created = _store.Add("Find Me");

        var found = _store.Get(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public void Get_SupportsPrefixMatching()
    {
        var created = _store.Add("Prefix Task");
        var prefix = created.Id[..4];

        var found = _store.Get(prefix);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public void Get_ReturnsNull_WhenNotFound()
    {
        var found = _store.Get("nonexistent");

        Assert.Null(found);
    }

    [Fact]
    public void List_ExcludesCompletedByDefault()
    {
        _store.Add("Open Task");
        var completed = _store.Add("Completed Task");
        _store.Update(completed.Id, status: TaskStatus.Done);

        var tasks = _store.List();

        Assert.Single(tasks);
        Assert.Equal("Open Task", tasks[0].Title);
    }

    [Fact]
    public void List_IncludesCompleted_WhenRequested()
    {
        _store.Add("Open Task");
        var completed = _store.Add("Completed Task");
        _store.Update(completed.Id, status: TaskStatus.Done);

        var tasks = _store.List(includeCompleted: true);

        Assert.Equal(2, tasks.Count);
    }

    [Fact]
    public void List_FiltersByStatus()
    {
        _store.Add("Todo Task");
        var inProgress = _store.Add("In Progress Task");
        _store.Update(inProgress.Id, status: TaskStatus.InProgress);

        var tasks = _store.List(status: TaskStatus.InProgress);

        Assert.Single(tasks);
        Assert.Equal("In Progress Task", tasks[0].Title);
    }

    [Fact]
    public void List_FiltersByPriority()
    {
        _store.Add("Low", priority: TaskPriority.Low);
        _store.Add("High", priority: TaskPriority.High);

        var tasks = _store.List(priority: TaskPriority.High);

        Assert.Single(tasks);
        Assert.Equal("High", tasks[0].Title);
    }

    [Fact]
    public void List_FiltersByTag()
    {
        _store.Add("Tagged", tags: new List<string> { "important" });
        _store.Add("Untagged");

        var tasks = _store.List(tag: "important");

        Assert.Single(tasks);
        Assert.Equal("Tagged", tasks[0].Title);
    }

    [Fact]
    public void List_FiltersByProject()
    {
        var project = _store.AddProject("Filter Project");
        _store.Add("In Project", projectId: project.Id);
        _store.Add("No Project");

        var tasks = _store.List(projectId: project.Id);

        Assert.Single(tasks);
        Assert.Equal("In Project", tasks[0].Title);
    }

    #endregion

    #region Task Update Tests

    [Fact]
    public void Update_ModifiesTitle()
    {
        var task = _store.Add("Original Title");

        var updated = _store.Update(task.Id, title: "New Title");

        Assert.NotNull(updated);
        Assert.Equal("New Title", updated.Title);
    }

    [Fact]
    public void Update_SetsCompletedAt_WhenDone()
    {
        var task = _store.Add("Task");

        var updated = _store.Update(task.Id, status: TaskStatus.Done);

        Assert.NotNull(updated?.CompletedAt);
    }

    [Fact]
    public void Update_ClearsCompletedAt_WhenReopened()
    {
        var task = _store.Add("Task");
        _store.Update(task.Id, status: TaskStatus.Done);

        var updated = _store.Update(task.Id, status: TaskStatus.Todo);

        Assert.Null(updated?.CompletedAt);
    }

    [Fact]
    public void Update_ReturnsNull_WhenNotFound()
    {
        var updated = _store.Update("nonexistent", title: "Test");

        Assert.Null(updated);
    }

    #endregion

    #region Task Delete Tests

    [Fact]
    public void Delete_RemovesTask()
    {
        var task = _store.Add("Delete Me");

        var result = _store.Delete(task.Id);

        Assert.True(result);
        Assert.Null(_store.Get(task.Id));
    }

    [Fact]
    public void Delete_Cascade_RemovesSubtasks()
    {
        var parent = _store.Add("Parent");
        var child1 = _store.Add("Child 1", parentId: parent.Id);
        var child2 = _store.Add("Child 2", parentId: parent.Id);

        _store.Delete(parent.Id, cascade: true);

        Assert.Null(_store.Get(parent.Id));
        Assert.Null(_store.Get(child1.Id));
        Assert.Null(_store.Get(child2.Id));
    }

    [Fact]
    public void Delete_WithoutCascade_OrphansSubtasks()
    {
        var parent = _store.Add("Parent");
        var child = _store.Add("Child", parentId: parent.Id);

        _store.Delete(parent.Id, cascade: false);

        var orphan = _store.Get(child.Id);
        Assert.NotNull(orphan);
        Assert.Null(orphan.ParentId);
    }

    [Fact]
    public void Delete_ReturnsFalse_WhenNotFound()
    {
        var result = _store.Delete("nonexistent");

        Assert.False(result);
    }

    #endregion

    #region Tree Tests

    [Fact]
    public void GetTree_ReturnsHierarchy()
    {
        var parent = _store.Add("Parent");
        _store.Add("Child 1", parentId: parent.Id);
        _store.Add("Child 2", parentId: parent.Id);

        var tree = _store.GetTree();

        Assert.Single(tree);
        Assert.Equal(2, tree[0].Children.Count);
    }

    [Fact]
    public void GetTree_SetsDepthCorrectly()
    {
        var root = _store.Add("Root");
        var child = _store.Add("Child", parentId: root.Id);
        var grandchild = _store.Add("Grandchild", parentId: child.Id);

        var tree = _store.GetTree(includeCompleted: true);
        var flat = _store.Flatten(tree);

        Assert.Equal(0, flat.First(t => t.Id == root.Id).Depth);
        Assert.Equal(1, flat.First(t => t.Id == child.Id).Depth);
        Assert.Equal(2, flat.First(t => t.Id == grandchild.Id).Depth);
    }

    [Fact]
    public void GetSubtasks_ReturnsDirectChildren()
    {
        var parent = _store.Add("Parent");
        var child1 = _store.Add("Child 1", parentId: parent.Id);
        var child2 = _store.Add("Child 2", parentId: parent.Id);
        _store.Add("Grandchild", parentId: child1.Id);

        var subtasks = _store.GetSubtasks(parent.Id, recursive: false);

        Assert.Equal(2, subtasks.Count);
    }

    [Fact]
    public void GetSubtasks_ReturnsAllDescendants_WhenRecursive()
    {
        var parent = _store.Add("Parent");
        var child1 = _store.Add("Child 1", parentId: parent.Id);
        _store.Add("Child 2", parentId: parent.Id);
        _store.Add("Grandchild", parentId: child1.Id);

        var subtasks = _store.GetSubtasks(parent.Id, recursive: true);

        Assert.Equal(3, subtasks.Count);
    }

    #endregion

    #region Stats Tests

    [Fact]
    public void GetStats_ReturnsCorrectCounts()
    {
        _store.Add("Todo 1");
        _store.Add("Todo 2");
        var done = _store.Add("Done");
        _store.Update(done.Id, status: TaskStatus.Done);
        var inProgress = _store.Add("In Progress");
        _store.Update(inProgress.Id, status: TaskStatus.InProgress);

        var stats = _store.GetStats();

        Assert.Equal(4, stats.Total);
        Assert.Equal(2, stats.Todo);
        Assert.Equal(1, stats.InProgress);
        Assert.Equal(1, stats.Done);
    }

    #endregion

    #region Bulk Operations Tests

    [Fact]
    public void AddMany_CreatesHierarchy()
    {
        var project = _store.AddProject("Bulk Project");

        var tasks = _store.AddMany(new List<TaskItemInput>
        {
            new TaskItemInput
            {
                Title = "Parent Task",
                Priority = TaskPriority.High,
                Subtasks = new List<TaskItemInput>
                {
                    new TaskItemInput { Title = "Subtask 1" },
                    new TaskItemInput { Title = "Subtask 2" }
                }
            }
        }, project.Id);

        Assert.Equal(3, tasks.Count);

        var parent = tasks.First(t => t.Title == "Parent Task");
        var subtasks = _store.GetSubtasks(parent.Id);
        Assert.Equal(2, subtasks.Count);
    }

    #endregion

    #region MoveToProject Tests

    [Fact]
    public void MoveToProject_ChangesProject()
    {
        var project1 = _store.AddProject("Project 1");
        var project2 = _store.AddProject("Project 2");
        var task = _store.Add("Task", projectId: project1.Id);

        _store.MoveToProject(task.Id, project2.Id);

        var moved = _store.Get(task.Id);
        Assert.NotNull(moved);
        Assert.Equal(project2.Id, moved.ProjectId);
    }

    [Fact]
    public void MoveToProject_ClearsParentId()
    {
        var project = _store.AddProject("Project");
        var parent = _store.Add("Parent", projectId: project.Id);
        var child = _store.Add("Child", parentId: parent.Id);

        _store.MoveToProject(child.Id, project.Id);

        var moved = _store.Get(child.Id);
        Assert.NotNull(moved);
        Assert.Null(moved.ParentId);
    }

    [Fact]
    public void MoveToProject_MovesChildrenToo()
    {
        var project1 = _store.AddProject("Project 1");
        var project2 = _store.AddProject("Project 2");
        var parent = _store.Add("Parent", projectId: project1.Id);
        var child = _store.Add("Child", parentId: parent.Id);

        _store.MoveToProject(parent.Id, project2.Id);

        var movedChild = _store.Get(child.Id);
        Assert.NotNull(movedChild);
        Assert.Equal(project2.Id, movedChild.ProjectId);
    }

    #endregion

    #region Persistence Tests

    [Fact]
    public void Store_PersistsData()
    {
        var project = _store.AddProject("Persist Project");
        var task = _store.Add("Persist Task", projectId: project.Id);

        // Create new store with same file
        var store2 = new TaskStore(_testFilePath);

        Assert.NotNull(store2.GetProject(project.Id));
        Assert.NotNull(store2.Get(task.Id));
    }

    #endregion
}
