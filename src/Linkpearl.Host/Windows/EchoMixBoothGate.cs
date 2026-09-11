using Linkpearl.Audio;

namespace Linkpearl.Host.Windows;

internal sealed class EchoMixBoothGate : IEchoMixBooth
{
    private IEchoMixBooth? inner;

    public bool IsOpen => inner?.IsOpen ?? false;

    public bool Mixing => inner?.Mixing ?? false;

    public void Attach(IEchoMixBooth booth) => inner = booth;

    public void Open() => inner?.Open();

    public void Close() => inner?.Close();
}
