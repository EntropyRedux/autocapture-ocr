using System.CommandLine;
using AutoCaptureOCR.CLI.Output;
using AutoCaptureOCR.CLI.Services;
using Spectre.Console;

namespace AutoCaptureOCR.CLI.Commands;

public static class ProjectCommands
{
    public static Command CreateProjectCommand()
    {
        var projectCommand = new Command("project", "Manage projects");

        // autocapture project list
        var listCommand = new Command("list", "List all projects");
        listCommand.SetHandler(ListProjects);

        // autocapture project create <name>
        var createCommand = new Command("create", "Create a new project");
        var nameArg = new Argument<string>("name", "Project name");
        var descOption = new Option<string?>("--description", "Project description");

        createCommand.AddArgument(nameArg);
        createCommand.AddOption(descOption);

        createCommand.SetHandler((string name, string? description) =>
        {
            CreateProject(name, description);
        }, nameArg, descOption);

        // autocapture project info <name>
        var infoCommand = new Command("info", "Show project details");
        var projectArg = new Argument<string>("project", "Project name or ID");
        infoCommand.AddArgument(projectArg);

        infoCommand.SetHandler((string project) =>
        {
            ShowProjectInfo(project);
        }, projectArg);

        // autocapture project delete <name>
        var deleteCommand = new Command("delete", "Delete a project");
        var deleteProjectArg = new Argument<string>("project", "Project name or ID");
        var forceOption = new Option<bool>("--force", () => false, "Skip confirmation");

        deleteCommand.AddArgument(deleteProjectArg);
        deleteCommand.AddOption(forceOption);

        deleteCommand.SetHandler((string project, bool force) =>
        {
            DeleteProject(project, force);
        }, deleteProjectArg, forceOption);

        projectCommand.AddCommand(listCommand);
        projectCommand.AddCommand(createCommand);
        projectCommand.AddCommand(infoCommand);
        projectCommand.AddCommand(deleteCommand);

        return projectCommand;
    }

    private static void ListProjects()
    {
        try
        {
            var context = new CLIContext();
            var projects = context.ProjectService.GetAllProjects();

            if (projects.Count == 0)
            {
                ConsoleFormatter.Warning("No projects found");
                return;
            }

            var table = new Table();
            table.AddColumn("Name");
            table.AddColumn("Description");
            table.AddColumn("Sessions");
            table.AddColumn("Captures");
            table.AddColumn("Modified");

            foreach (var project in projects)
            {
                var captureCount = project.Sessions.Sum(s => s.Captures.Count);

                table.AddRow(
                    Markup.Escape(project.Name),
                    Markup.Escape(project.Description),
                    project.Sessions.Count.ToString(),
                    captureCount.ToString(),
                    project.Modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                );
            }

            AnsiConsole.Write(table);
            ConsoleFormatter.Success($"Total: {projects.Count} project(s)");
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Error($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void CreateProject(string name, string? description)
    {
        try
        {
            var context = new CLIContext();

            // Check if project already exists
            var existing = context.GetProject(name);
            if (existing != null)
            {
                ConsoleFormatter.Error($"Project '{name}' already exists");
                Environment.Exit(1);
                return;
            }

            var project = context.ProjectService.CreateProject(name, description ?? "");

            ConsoleFormatter.Success($"Created project: {project.Name}");
            ConsoleFormatter.Info($"ID: {project.Id}");
            ConsoleFormatter.Info($"Path: {project.SavePath}");
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Error($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void ShowProjectInfo(string projectName)
    {
        try
        {
            var context = new CLIContext();
            var project = context.GetProject(projectName);

            if (project == null)
            {
                ConsoleFormatter.Error($"Project not found: {projectName}");
                Environment.Exit(1);
                return;
            }

            ConsoleFormatter.WriteHeader($"Project: {project.Name}");

            ConsoleFormatter.WriteKeyValue("ID", project.Id.ToString());
            ConsoleFormatter.WriteKeyValue("Description", project.Description);
            ConsoleFormatter.WriteKeyValue("Created", project.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            ConsoleFormatter.WriteKeyValue("Modified", project.Modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            ConsoleFormatter.WriteKeyValue("Save Path", project.SavePath);
            ConsoleFormatter.WriteKeyValue("Sessions", project.Sessions.Count.ToString());

            var totalCaptures = project.Sessions.Sum(s => s.Captures.Count);
            ConsoleFormatter.WriteKeyValue("Total Captures", totalCaptures.ToString());

            if (project.Sessions.Any())
            {
                AnsiConsole.WriteLine();
                var table = new Table();
                table.Title = new TableTitle("Sessions");
                table.AddColumn("Name");
                table.AddColumn("Created");
                table.AddColumn("Captures");

                foreach (var session in project.Sessions.OrderByDescending(s => s.Created))
                {
                    table.AddRow(
                        Markup.Escape(session.Name),
                        session.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                        session.Captures.Count.ToString()
                    );
                }

                AnsiConsole.Write(table);
            }
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Error($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void DeleteProject(string projectName, bool force)
    {
        try
        {
            var context = new CLIContext();
            var project = context.GetProject(projectName);

            if (project == null)
            {
                ConsoleFormatter.Error($"Project not found: {projectName}");
                Environment.Exit(1);
                return;
            }

            // Confirm deletion
            if (!force)
            {
                var captureCount = project.Sessions.Sum(s => s.Captures.Count);
                var confirm = AnsiConsole.Confirm(
                    $"Delete project '{project.Name}' with {project.Sessions.Count} session(s) and {captureCount} capture(s)?",
                    false
                );

                if (!confirm)
                {
                    ConsoleFormatter.Info("Deletion cancelled");
                    return;
                }
            }

            context.ProjectService.DeleteProject(project);
            ConsoleFormatter.Success($"Deleted project: {project.Name}");
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Error($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
