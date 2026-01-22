using Tasks.Models;

namespace Tasks.Tests;

public class DependencyTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly TaskStore _store;

    public DependencyTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"tsk_test_{Guid.NewGuid():N}.json");
        _store = new TaskStore(_testFilePath);
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }

    [Fact]
    public void AddDependency_CreatesDependency()
    {
        var task1 = _store.Add("Task 1");
        var task2 = _store.Add("Task 2");

        var result = _store.AddDependency(task1.Id, task2.Id);

        Assert.True(result);
        var deps = _store.GetDependencies(task1.Id);
        Assert.Single(deps);
        Assert.Equal(task2.Id, deps[0].Id);
    }

    [Fact]
    public void AddDependency_PreventsSelfDependency()
    {
        var task = _store.Add("Self Task");

        var result = _store.AddDependency(task.Id, task.Id);

        Assert.False(result);
    }

    [Fact]
    public void AddDependency_PreventsCircularDependency()
    {
        var task1 = _store.Add("Task 1");
        var task2 = _store.Add("Task 2");
        var task3 = _store.Add("Task 3");

        // Create chain: task1 -> task2 -> task3
        _store.AddDependency(task1.Id, task2.Id);
        _store.AddDependency(task2.Id, task3.Id);

        // Try to create cycle: task3 -> task1
        var result = _store.AddDependency(task3.Id, task1.Id);

        Assert.False(result);
    }

    [Fact]
    public void AddDependency_AllowsMultipleDependencies()
    {
        var task1 = _store.Add("Task 1");
        var task2 = _store.Add("Task 2");
        var task3 = _store.Add("Task 3");

        _store.AddDependency(task1.Id, task2.Id);
        _store.AddDependency(task1.Id, task3.Id);

        var deps = _store.GetDependencies(task1.Id);
        Assert.Equal(2, deps.Count);
    }

    [Fact]
    public void RemoveDependency_RemovesDependency()
    {
        var task1 = _store.Add("Task 1");
        var task2 = _store.Add("Task 2");
        _store.AddDependency(task1.Id, task2.Id);

        var result = _store.RemoveDependency(task1.Id, task2.Id);

        Assert.True(result);
        var deps = _store.GetDependencies(task1.Id);
        Assert.Empty(deps);
    }

    [Fact]
    public void RemoveDependency_ReturnsFalseIfNotExists()
    {
        var task1 = _store.Add("Task 1");
        var task2 = _store.Add("Task 2");

        var result = _store.RemoveDependency(task1.Id, task2.Id);

        Assert.False(result);
    }

    [Fact]
    public void GetDependents_ReturnsTasksThatDependOnThis()
    {
        var task1 = _store.Add("Task 1");
        var task2 = _store.Add("Task 2");
        var task3 = _store.Add("Task 3");

        _store.AddDependency(task2.Id, task1.Id);
        _store.AddDependency(task3.Id, task1.Id);

        var dependents = _store.GetDependents(task1.Id);

        Assert.Equal(2, dependents.Count);
        Assert.Contains(dependents, t => t.Id == task2.Id);
        Assert.Contains(dependents, t => t.Id == task3.Id);
    }

    [Fact]
    public void GetBlockingDependencies_ReturnsIncompleteDependencies()
    {
        var task1 = _store.Add("Task 1");
        var task2 = _store.Add("Task 2");
        var task3 = _store.Add("Task 3");

        _store.AddDependency(task1.Id, task2.Id);
        _store.AddDependency(task1.Id, task3.Id);
        _store.Complete(task2.Id);

        var blocking = _store.GetBlockingDependencies(task1.Id);

        Assert.Single(blocking);
        Assert.Equal(task3.Id, blocking[0]);
    }

    [Fact]
    public void CanStart_ReturnsTrueWhenNoDependencies()
    {
        var task = _store.Add("Task");

        var canStart = _store.CanStart(task.Id);

        Assert.True(canStart);
    }

    [Fact]
    public void CanStart_ReturnsFalseWithIncompleteDependencies()
    {
        var task1 = _store.Add("Task 1");
        var task2 = _store.Add("Task 2");
        _store.AddDependency(task1.Id, task2.Id);

        var canStart = _store.CanStart(task1.Id);

        Assert.False(canStart);
    }

    [Fact]
    public void CanStart_ReturnsTrueWhenAllDependenciesComplete()
    {
        var task1 = _store.Add("Task 1");
        var task2 = _store.Add("Task 2");
        _store.AddDependency(task1.Id, task2.Id);
        _store.Complete(task2.Id);

        var canStart = _store.CanStart(task1.Id);

        Assert.True(canStart);
    }

    [Fact]
    public void Dependencies_PersistAcrossReload()
    {
        var task1 = _store.Add("Task 1");
        var task2 = _store.Add("Task 2");
        _store.AddDependency(task1.Id, task2.Id);

        var store2 = new TaskStore(_testFilePath);
        var deps = store2.GetDependencies(task1.Id);

        Assert.Single(deps);
        Assert.Equal(task2.Id, deps[0].Id);
    }
}
