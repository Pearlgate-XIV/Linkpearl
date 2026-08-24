using Linkpearl.Diagnostics;

namespace Linkpearl.Modules;

public sealed class ModuleContext
{
    public ModuleContext(HostPaths paths, ILinkpearlLog log, HostEnvironment environment)
    {
        Paths = paths;
        Log = log;
        Environment = environment;
    }

    public HostPaths Paths { get; }

    public ILinkpearlLog Log { get; }

    public HostEnvironment Environment { get; }
}

public sealed class HostPaths
{
    public HostPaths(string assemblyDirectory, string configDirectory)
    {
        AssemblyDirectory = assemblyDirectory;
        ConfigDirectory = configDirectory;
        CacheDirectory = Path.Combine(configDirectory, "cache");
        StateDirectory = Path.Combine(configDirectory, "state");
    }

    public string AssemblyDirectory { get; }

    public string ConfigDirectory { get; }

    public string CacheDirectory { get; }

    public string StateDirectory { get; }

    public string Asset(string relativePath) => Path.Combine(AssemblyDirectory, relativePath);

    public string Cache(string relativePath) => Path.Combine(CacheDirectory, relativePath);

    public string State(string relativePath) => Path.Combine(StateDirectory, relativePath);
}

public sealed class HostEnvironment
{
    public HostEnvironment(string version, bool isDevelopment, bool isWine)
    {
        Version = version;
        IsDevelopment = isDevelopment;
        IsWine = isWine;
    }

    public string Version { get; }

    public bool IsDevelopment { get; }

    public bool IsWine { get; }
}
