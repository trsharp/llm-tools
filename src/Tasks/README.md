# tsk - Hierarchical Task Management CLI & MCP Server

A lightweight hierarchical task management tool for project planning that works both as a standalone CLI and as an MCP (Model Context Protocol) server over stdio. Perfect for AI-assisted project planning with nested tasks and subtasks.

## Features

- **Projects** - Organize tasks into projects
- **Hierarchical Tasks** - Unlimited nesting of subtasks
- **Tree View** - Visualize task hierarchy
- **Bulk Operations** - Add many tasks with nested structure at once
- **Recursive Completion** - Complete a task and all its subtasks
- **Status Tracking** - Todo, In Progress, Done, Blocked, Cancelled
- **Priority Levels** - Low, Medium, High, Critical
- **Statistics** - Track progress by project

## Installation

```bash
cd tsk
dotnet build
```

## Usage

### Standalone CLI

```bash
# Project management
tsk project add "API Refactor" -d "Refactor the REST API"
tsk project list
tsk project delete myproj --cascade

# Add tasks with hierarchy
tsk add "Design API" --project "API Refactor" -p high
tsk add "Define endpoints" --parent abc123
tsk add "GET /users" --parent def456

# View as tree
tsk tree
tsk tree "API Refactor"
tsk tree --all  # include completed

# Complete tasks
tsk done abc123
tsk done abc123 --recursive  # complete all subtasks too

# Task management
tsk start abc
tsk block abc
tsk update abc -p critical --parent xyz

# Statistics
tsk stats
tsk stats "API Refactor"
```

### MCP Server Mode

Run as an MCP server over stdio:

```bash
tsk --mcp
```

#### MCP Configuration

Add to your MCP client configuration:

```json
{
  "mcpServers": {
    "tsk": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/tsk"],
      "transport": "stdio"
    }
  }
}
```

Or with the built executable:

```json
{
  "mcpServers": {
    "tsk": {
      "command": "path/to/tsk.exe",
      "args": ["--mcp"],
      "transport": "stdio"
    }
  }
}
```

## MCP Tools

### Project Tools
| Tool | Description |
|------|-------------|
| `project_create` | Create a new project |
| `project_list` | List all projects |
| `project_delete` | Delete a project |

### Task Tools
| Tool | Description |
|------|-------------|
| `task_add` | Add a task (optionally as subtask) |
| `task_add_many` | Bulk add tasks with nested subtasks |
| `task_list` | List tasks with filters |
| `task_tree` | Get hierarchical task tree |
| `task_get` | Get task details with subtasks |
| `task_subtasks` | Get subtasks of a task |
| `task_update` | Update task properties |
| `task_delete` | Delete task (with cascade option) |
| `task_complete` | Mark done (with recursive option) |
| `task_start` | Mark as in-progress |
| `task_block` | Mark as blocked |
| `task_move` | Move to different project |
| `task_stats` | Get statistics |

### Bulk Add Example (MCP)

```json
{
  "name": "task_add_many",
  "arguments": {
    "projectId": "myproject",
    "tasks": [
      {
        "title": "Phase 1: Design",
        "priority": "high",
        "subtasks": [
          { "title": "Define requirements" },
          { "title": "Create mockups" }
        ]
      },
      {
        "title": "Phase 2: Implementation",
        "subtasks": [
          {
            "title": "Backend",
            "subtasks": [
              { "title": "API endpoints" },
              { "title": "Database schema" }
            ]
          },
          { "title": "Frontend" }
        ]
      }
    ]
  }
}
```

## Task Properties

- **id**: Unique identifier (8-character hex)
- **parentId**: Parent task ID (for subtasks)
- **projectId**: Associated project ID
- **title**: Task title (required)
- **description**: Detailed description
- **status**: todo, inprogress, done, blocked, cancelled
- **priority**: low, medium, high, critical
- **tags**: Array of tags
- **order**: Sort order among siblings
- **dueDate**: Optional due date
- **createdAt/updatedAt/completedAt**: Timestamps

## Data Storage

Data is stored in `~/.tsk/tasks.json`.

## License

MIT
