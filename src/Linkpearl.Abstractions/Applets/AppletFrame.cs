using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Shell;
using Linkpearl.Theming;

namespace Linkpearl.Applets;

public readonly struct AppletFrame
{
    public readonly Rect Content;
    public readonly IPaintSurface Paint;
    public readonly ITextPainter Text;
    public readonly IInputProbe Input;
    public readonly ITheme Theme;
    public readonly IRouter Router;
    public readonly float Scale;
    public readonly float DeltaSeconds;

    public AppletFrame(Rect content, IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme,
        IRouter router, float scale, float deltaSeconds)
    {
        Content = content;
        Paint = paint;
        Text = text;
        Input = input;
        Theme = theme;
        Router = router;
        Scale = scale;
        DeltaSeconds = deltaSeconds;
    }

    public AppletFrame WithContent(Rect content) =>
        new(content, Paint, Text, Input, Theme, Router, Scale, DeltaSeconds);

    public float Units(float designUnits) => designUnits * Scale;
}
