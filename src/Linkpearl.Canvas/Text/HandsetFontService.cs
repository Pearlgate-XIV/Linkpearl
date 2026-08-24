using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Linkpearl.Painting;

namespace Linkpearl.Canvas.Text;

public sealed class HandsetFontService : IDisposable
{
    private static readonly (FontRole Role, string File, float SizeMultiplier)[] Faces =
    {
        (FontRole.Caption, "Inter-Regular.ttf", 0.82f),
        (FontRole.CaptionStrong, "Inter-SemiBold.ttf", 0.82f),
        (FontRole.Body, "Inter-Regular.ttf", 1.00f),
        (FontRole.BodyStrong, "Inter-Medium.ttf", 1.00f),
        (FontRole.Title, "Inter-SemiBold.ttf", 1.35f),
        (FontRole.Display, "Inter-Bold.ttf", 2.00f),
        (FontRole.Numeric, "Inter-SemiBold.ttf", 1.00f),
    };

    private readonly IFontAtlas atlas;
    private readonly string fontDirectory;
    private readonly Dictionary<FontRole, IFontHandle> handles = new();
    private bool disposed;

    public HandsetFontService(IDalamudPluginInterface pluginInterface)
    {
        atlas = pluginInterface.UiBuilder.FontAtlas;
        fontDirectory = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Fonts");
        Build();
    }

    public event Action? Rebuilt;

    public bool Ready
    {
        get
        {
            foreach (var handle in handles.Values)
            {
                if (!handle.Available)
                {
                    return false;
                }
            }

            return handles.Count > 0;
        }
    }

    public IFontHandle Handle(FontRole role) => handles[role];

    public void Rescale(float scale)
    {
        Build(scale);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (var handle in handles.Values)
        {
            handle.Dispose();
        }

        handles.Clear();
    }

    private void Build(float scale = 1f)
    {
        using var suppression = atlas.SuppressAutoRebuild();
        foreach (var handle in handles.Values)
        {
            handle.Dispose();
        }

        handles.Clear();

        foreach (var face in Faces)
        {
            var path = Path.Combine(fontDirectory, face.File);
            var pixelSize = UiBuilder.DefaultFontSizePx * face.SizeMultiplier * scale;
            handles[face.Role] = atlas.NewDelegateFontHandle(entry => entry.OnPreBuild(tools =>
                tools.AddFontFromFile(path, new SafeFontConfig { SizePx = pixelSize })));
        }

        _ = atlas.BuildFontsAsync().ContinueWith(_ => Rebuilt?.Invoke());
    }
}
