using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Linkpearl.Painting;
using Linkpearl.Preferences;

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
    private string displayFace = FounderFaces.Inter;
    private float scale = 1f;
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

    public void Rescale(float nextScale)
    {
        Build(nextScale);
    }

    public void SetDisplayFace(string faceId)
    {
        var id = FounderFaces.Sanitize(faceId);
        if (string.Equals(id, displayFace, StringComparison.Ordinal) && handles.Count > 0)
        {
            return;
        }

        displayFace = id;
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

    private void Build(float nextScale = 1f)
    {
        scale = nextScale;
        using var suppression = atlas.SuppressAutoRebuild();
        foreach (var handle in handles.Values)
        {
            handle.Dispose();
        }

        handles.Clear();

        foreach (var face in Faces)
        {
            var relative = face.Role == FontRole.Display ? FounderFaces.RelativeFile(displayFace) : face.File;
            var path = Path.Combine(fontDirectory, relative);
            if (!File.Exists(path))
            {
                path = Path.Combine(fontDirectory, face.File);
            }

            var pixelSize = UiBuilder.DefaultFontSizePx * face.SizeMultiplier * scale;
            var file = path;
            handles[face.Role] = atlas.NewDelegateFontHandle(entry => entry.OnPreBuild(tools =>
            {
                var config = new SafeFontConfig { SizePx = pixelSize };
                var built = tools.AddFontFromFile(file, config);
                config.MergeFont = built;
                config.GlyphRanges = FancyGlyphs;
                foreach (var extra in FallbackFaces())
                {
                    tools.AddFontFromFile(extra, config);
                }

                tools.Font = built;
            }));
        }

        _ = atlas.BuildFontsAsync().ContinueWith(_ => Rebuilt?.Invoke());
    }

    private static readonly ushort[] FancyGlyphs =
    {
        0x0020, 0x024F,
        0x0250, 0x02FF,
        0x1D00, 0x1DBF,
        0x2000, 0x206F,
        0x2070, 0x209F,
        0x20A0, 0x20CF,
        0x2100, 0x214F,
        0x2190, 0x21FF,
        0x2E00, 0x2E7F,
        0x2600, 0x27BF,
        0,
    };

    private static IEnumerable<string> FallbackFaces()
    {
        var windows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        var names = new[] { "segoeui.ttf", "arial.ttf", "seguisym.ttf", "seguili.ttf", "cambria.ttf" };
        for (var index = 0; index < names.Length; index++)
        {
            var path = Path.Combine(windows, names[index]);
            if (File.Exists(path))
            {
                yield return path;
            }
        }
    }
}
