using Spectre.Console;

namespace AutoCaptureOCR.CLI.Output;

/// <summary>
/// Handles formatted console output
/// </summary>
public static class ConsoleFormatter
{
    public static void Success(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    public static void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }

    public static void Warning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
    }

    public static void Info(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
    }

    public static void WriteJson(object obj)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
        AnsiConsole.WriteLine(json);
    }

    public static void WriteYaml(object obj)
    {
        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(obj);
        AnsiConsole.WriteLine(yaml);
    }

    public static void WriteKeyValue(string key, string value)
    {
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(key)}:[/] {Markup.Escape(value)}");
    }

    public static void WriteHeader(string text)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(text)}[/]").LeftJustified());
        AnsiConsole.WriteLine();
    }
}
