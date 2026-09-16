using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Modules;

public interface ILinkpearlModule
{
    ModuleIdentity Identity { get; }

    void Configure(IServiceCollection services, ModuleContext context);
}

public interface IModuleCatalog
{
    IReadOnlyList<ModuleIdentity> Registered { get; }

    bool Contains(string moduleId);
}

public readonly struct ModuleIdentity : IEquatable<ModuleIdentity>
{
    public string Id { get; }
    public string DisplayName { get; }
    public int Order { get; }

    public ModuleIdentity(string id, string displayName, int order = 0)
    {
        Id = id;
        DisplayName = displayName;
        Order = order;
    }

    public bool Equals(ModuleIdentity other) => string.Equals(Id, other.Id, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ModuleIdentity other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);

    public override string ToString() => Id;

    public static bool operator ==(ModuleIdentity left, ModuleIdentity right) => left.Equals(right);

    public static bool operator !=(ModuleIdentity left, ModuleIdentity right) => !left.Equals(right);
}
