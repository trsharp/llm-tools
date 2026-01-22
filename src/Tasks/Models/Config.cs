using System.Text.Json;

namespace Tasks.Models;

/// <summary>
/// Configuration options for tsk, bound from appsettings.json "Tsk" section
/// </summary>
public class TasksOptions
{
    public const string SectionName = "Tasks";

    public string? DataPath { get; set; }

    public string? DefaultProject { get; set; }

    public string DefaultPriority { get; set; } = "Medium";

    public TaskPriority GetPriority() =>
        Enum.TryParse<TaskPriority>(DefaultPriority, true, out var p) ? p : TaskPriority.Medium;
}

/// <summary>
/// Helper for resolving paths and saving config
/// </summary>
public static class TskConfigHelper
{
    private static readonly string ExeDir = Path.GetDirectoryName(
        Environment.ProcessPath ?? AppContext.BaseDirectory) ?? AppContext.BaseDirectory;

    private static readonly string ConfigPath = Path.Combine(ExeDir, "appsettings.json");

    private static readonly string DefaultDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".tsk");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string GetConfigPath() => ConfigPath;

    public static string GetDataDirectory(TasksOptions options)
    {
        if (!string.IsNullOrEmpty(options.DataPath))
        {
            var expanded = Environment.ExpandEnvironmentVariables(options.DataPath);
            if (!Path.IsPathRooted(expanded))
                expanded = Path.Combine(ExeDir, expanded);
            Directory.CreateDirectory(expanded);
            return expanded;
        }
        Directory.CreateDirectory(DefaultDataDir);
        return DefaultDataDir;
    }

    public static string? Get(TasksOptions options, string key)
    {
        return key.ToLower() switch
        {
            "datapath" => options.DataPath,
            "defaultproject" => options.DefaultProject,
            "defaultpriority" => options.DefaultPriority,
            _ => null
        };
    }

    public static bool Set(TasksOptions options, string key, string value)
    {
        switch (key.ToLower())
        {
            case "datapath":
                options.DataPath = string.IsNullOrEmpty(value) ? null : value;
                break;
            case "defaultproject":
                options.DefaultProject = string.IsNullOrEmpty(value) ? null : value;
                break;
            case "defaultpriority":
                if (Enum.TryParse<TaskPriority>(value, true, out _))
                    options.DefaultPriority = value;
                else
                    return false;
                break;
            default:
                return false;
        }
        Save(options);
        return true;
    }

    public static Dictionary<string, string?> GetAll(TasksOptions options)
    {
        return new Dictionary<string, string?>
        {
            ["dataPath"] = options.DataPath,
            ["defaultProject"] = options.DefaultProject,
            ["defaultPriority"] = options.DefaultPriority
        };
    }

    public static void Save(TasksOptions options)
    {
        Dictionary<string, object?> root;
        if (File.Exists(ConfigPath))
        {
            var existingJson = File.ReadAllText(ConfigPath);
            root = JsonSerializer.Deserialize<Dictionary<string, object?>>(existingJson, JsonOptions)
                   ?? new Dictionary<string, object?>();
        }
        else
        {
            root = new Dictionary<string, object?>();
        }

        root["Tasks"] = new Dictionary<string, object?>
        {
            ["DataPath"] = options.DataPath,
            ["DefaultProject"] = options.DefaultProject,
            ["DefaultPriority"] = options.DefaultPriority
        };

        var json = JsonSerializer.Serialize(root, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
