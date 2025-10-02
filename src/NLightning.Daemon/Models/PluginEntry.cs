namespace NLightning.Daemon.Models;

internal sealed record PluginEntry
{
    public string AssemblyPath { get; init; } = "";
    public string? TypeName { get; init; }
    public string? ConfigSection { get; init; }
}