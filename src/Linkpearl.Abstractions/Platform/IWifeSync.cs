namespace Linkpearl.Platform;

public interface IWifeSync
{
    bool IsPresent { get; }

    bool IsOn { get; }

    string PluginName { get; }

    void SetOn(bool enabled);
}
