using Tasks.Models;
using TaskStatus = Tasks.Models.TaskStatus;

namespace Tasks.Tests;

public class TaskItemTests
{
    [Fact]
    public void TaskItem_DefaultsToTodoStatus()
    {
        var task = new TaskItem();

        Assert.Equal(TaskStatus.Todo, task.Status);
    }

    [Fact]
    public void TaskItem_DefaultsToMediumPriority()
    {
        var task = new TaskItem();

        Assert.Equal(TaskPriority.Medium, task.Priority);
    }

    [Fact]
    public void TaskItem_GeneratesUniqueId()
    {
        var task1 = new TaskItem();
        var task2 = new TaskItem();

        Assert.NotEqual(task1.Id, task2.Id);
    }

    [Fact]
    public void TaskItem_IdIs8Characters()
    {
        var task = new TaskItem();

        Assert.Equal(8, task.Id.Length);
    }

    [Fact]
    public void TaskItem_DefaultsEmptyTags()
    {
        var task = new TaskItem();

        Assert.NotNull(task.Tags);
        Assert.Empty(task.Tags);
    }

    [Fact]
    public void TaskItem_SetsCreatedAtToNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var task = new TaskItem();
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(task.CreatedAt, before, after);
    }
}

public class ProjectTests
{
    [Fact]
    public void Project_GeneratesUniqueId()
    {
        var project1 = new Project();
        var project2 = new Project();

        Assert.NotEqual(project1.Id, project2.Id);
    }

    [Fact]
    public void Project_IdIs8Characters()
    {
        var project = new Project();

        Assert.Equal(8, project.Id.Length);
    }

    [Fact]
    public void Project_SetsCreatedAtToNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var project = new Project();
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(project.CreatedAt, before, after);
    }
}
