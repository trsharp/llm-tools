using Tasks.Models;

namespace Tasks.Tests;

public class ConfigTests : IDisposable
{
    private readonly string _testConfigPath;

    public ConfigTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"tsk_config_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testConfigPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testConfigPath))
            Directory.Delete(_testConfigPath, true);
    }

    [Fact]
    public void Options_HasCorrectDefaults()
    {
        var options = new TasksOptions();

        Assert.Null(options.DataPath);
        Assert.Null(options.DefaultProject);
        Assert.Equal("Medium", options.DefaultPriority);
    }

    [Fact]
    public void GetDataDirectory_ReturnsDefaultPath_WhenNotSet()
    {
        var options = new TasksOptions();

        var path = TskConfigHelper.GetDataDirectory(options);

        Assert.Contains(".tsk", path);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void GetDataDirectory_ReturnsCustomPath_WhenSet()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"tsk-test-{Guid.NewGuid():N}");
        try
        {
            var options = new TasksOptions { DataPath = tempDir };

            var path = TskConfigHelper.GetDataDirectory(options);

            Assert.Equal(tempDir, path);
            Assert.True(Directory.Exists(path));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Get_ReturnsCorrectValue()
    {
        var options = new TasksOptions
        {
            DataPath = "/test/path",
            DefaultPriority = "High"
        };

        Assert.Equal("/test/path", TskConfigHelper.Get(options, "datapath"));
        Assert.Equal("High", TskConfigHelper.Get(options, "defaultpriority"));
    }

    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var options = new TasksOptions { DataPath = "/test" };

        Assert.Equal("/test", TskConfigHelper.Get(options, "DATAPATH"));
        Assert.Equal("/test", TskConfigHelper.Get(options, "DataPath"));
        Assert.Equal("/test", TskConfigHelper.Get(options, "datapath"));
    }

    [Fact]
    public void Get_ReturnsNull_ForUnknownKey()
    {
        var options = new TasksOptions();

        Assert.Null(TskConfigHelper.Get(options, "unknownkey"));
    }

    [Fact]
    public void GetPriority_ParsesCorrectly()
    {
        var options = new TasksOptions { DefaultPriority = "High" };

        Assert.Equal(TaskPriority.High, options.GetPriority());
    }

    [Fact]
    public void GetPriority_ReturnsMedium_ForInvalid()
    {
        var options = new TasksOptions { DefaultPriority = "invalid" };

        Assert.Equal(TaskPriority.Medium, options.GetPriority());
    }

    [Fact]
    public void GetAll_ReturnsAllSettings()
    {
        var options = new TasksOptions
        {
            DataPath = "/test",
            DefaultProject = "proj1",
            DefaultPriority = "Critical"
        };

        var all = TskConfigHelper.GetAll(options);

        Assert.Equal("/test", all["dataPath"]);
        Assert.Equal("proj1", all["defaultProject"]);
        Assert.Equal("Critical", all["defaultPriority"]);
    }

    [Fact]
    public void TaskStore_UsesOptionsDataPath()
    {
        var testDataDir = Path.Combine(_testConfigPath, "custom_data");
        var options = new TasksOptions { DataPath = testDataDir };

        var store = new TaskStore(testDataDir, options);
        store.Add("Test Task");

        // Should create _default.json for tasks without a project
        Assert.True(File.Exists(Path.Combine(testDataDir, "_default.json")));
    }
}
