using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Shell;
using Linkpearl.Theming;

namespace Linkpearl.Applets;

public readonly struct AppletFrame
{
    public Rect Content { get; }
    public IPaintSurface Paint { get; }
    public ITextPainter Text { get; }
    public IInputProbe Input { get; }
    public ITheme Theme { get; }
    public IRouter Router { get; }
    public ITextField TextField { get; }
    public ITextureSource Textures { get; }
    public HostPaths Paths { get; }
    public float Scale { get; }
    public float DeltaSeconds { get; }

    public AppletFrame(Rect content, IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme,
        IRouter router, ITextField textField, ITextureSource textures, HostPaths paths, float scale, float deltaSeconds)
    {
        Content = content;
        Paint = paint;
        Text = text;
        Input = input;
        Theme = theme;
        Router = router;
        TextField = textField;
        Textures = textures;
        Paths = paths;
        Scale = scale;
        DeltaSeconds = deltaSeconds;
    }

    public AppletFrame WithContent(Rect content) =>
        new(content, Paint, Text, Input, Theme, Router, TextField, Textures, Paths, Scale, DeltaSeconds);

    public AppletFrame WithInput(IInputProbe input) =>
        new(Content, Paint, Text, input, Theme, Router, TextField, Textures, Paths, Scale, DeltaSeconds);

    public float Units(float designUnits) => designUnits * Scale;
}
