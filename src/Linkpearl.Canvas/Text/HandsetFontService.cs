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
    private IFontHandle? dreamsDisplay;
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

    public IFontHandle Handle(FontRole role)
    {
        if (role == FontRole.Display &&
            string.Equals(displayFace, FounderFaces.Dreams, StringComparison.Ordinal) &&
            dreamsDisplay is { Available: true })
        {
            return dreamsDisplay;
        }

        return handles[role];
    }

    public void Rescale(float nextScale)
    {
        Build(nextScale);
    }

    public void SetDisplayFace(string faceId)
    {
        displayFace = FounderFaces.Sanitize(faceId);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ForgetHandles();
    }

    private void Build(float nextScale = 1f)
    {
        scale = nextScale;
        using var suppression = atlas.SuppressAutoRebuild();
        ForgetHandles();

        foreach (var face in Faces)
        {
            handles[face.Role] = NewHandle(face.File, face.SizeMultiplier);
        }

        var dreams = FounderFaces.RelativeFile(FounderFaces.Dreams);
        if (File.Exists(Path.Combine(fontDirectory, dreams)))
        {
            dreamsDisplay = NewHandle(dreams, 2.00f);
        }

        _ = atlas.BuildFontsAsync().ContinueWith(_ => Rebuilt?.Invoke());
    }

    private IFontHandle NewHandle(string relative, float sizeMultiplier)
    {
        var path = Path.Combine(fontDirectory, relative);
        if (!File.Exists(path))
        {
            path = Path.Combine(fontDirectory, "Inter-Bold.ttf");
        }

        var file = path;
        var pixelSize = UiBuilder.DefaultFontSizePx * sizeMultiplier * scale;
        return atlas.NewDelegateFontHandle(entry => entry.OnPreBuild(tools =>
        {
            var built = tools.AddFontFromFile(file, new SafeFontConfig { SizePx = pixelSize });

            // Inter keeps Basic Latin through Extended-B. Fallbacks only fill
            // phonetic, symbols, and other blocks Inter does not ship.
            var extras = new SafeFontConfig
            {
                SizePx = pixelSize,
                MergeFont = built,
                GlyphRanges = ExtraGlyphs,
            };
            foreach (var extra in RangedFaces(fontDirectory))
            {
                tools.AddFontFromFile(extra, extras);
            }

            var cambria = Path.Combine(WindowsFonts, "cambria.ttc");
            if (File.Exists(cambria))
            {
                tools.AddFontFromFile(cambria, new SafeFontConfig
                {
                    SizePx = pixelSize,
                    MergeFont = built,
                    GlyphRanges = ExtraGlyphs,
                    FontNo = 0,
                });
            }

            // Symbol-only faces have no Latin to clash with Inter; take every glyph.
            var symbols = new SafeFontConfig { SizePx = pixelSize, MergeFont = built };
            foreach (var extra in SymbolFaces(fontDirectory))
            {
                tools.AddFontFromFile(extra, symbols);
            }

            tools.AddGameSymbol(new SafeFontConfig { SizePx = pixelSize, MergeFont = built });
            tools.AttachExtraGlyphsForDalamudLanguage(new SafeFontConfig
            {
                SizePx = pixelSize,
                MergeFont = built,
            });
            tools.Font = built;
        }));
    }

    private void ForgetHandles()
    {
        foreach (var handle in handles.Values)
        {
            handle.Dispose();
        }

        handles.Clear();
        dreamsDisplay?.Dispose();
        dreamsDisplay = null;
    }

    private static readonly string WindowsFonts =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

    // Start at IPA (U+0250) so Segoe / Noto / Cambria never replace Inter Latin.
    private static readonly ushort[] ExtraGlyphs =
    {
        0x0250, 0x036F,
        0x1AB0, 0x1AFF,
        0x1D00, 0x1DBF,
        0x1DC0, 0x1DFF,
        0x2000, 0x206F,
        0x2070, 0x218F,
        0x2190, 0x23FF,
        0x2460, 0x24FF,
        0x2500, 0x27BF,
        0x2900, 0x2BFF,
        0x2C60, 0x2C7F,
        0x2E00, 0x2E7F,
        0xA720, 0xA7FF,
        0xAB30, 0xAB6F,
        0xD83C, 0xD83E,
        0xDC00, 0xDFFF,
        0xFE00, 0xFE0F,
        0xFF00, 0xFFEF,
        0,
    };

    private static IEnumerable<string> RangedFaces(string bundled)
    {
        foreach (var name in new[] { "NotoSans-Regular.ttf", "NotoEmoji-Regular.ttf" })
        {
            var path = Path.Combine(bundled, name);
            if (File.Exists(path))
            {
                yield return path;
            }
        }

        foreach (var name in new[]
                 {
                     "segoeui.ttf", "arial.ttf", "times.ttf", "constan.ttf", "seguisym.ttf", "seguiemj.ttf",
                 })
        {
            var path = Path.Combine(WindowsFonts, name);
            if (File.Exists(path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> SymbolFaces(string bundled)
    {
        var path = Path.Combine(bundled, "NotoSansSymbols2-Regular.ttf");
        if (File.Exists(path))
        {
            yield return path;
        }
    }
}
