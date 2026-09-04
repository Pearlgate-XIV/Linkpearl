using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Time;

namespace Linkpearl.Device.Shell.Studio;

internal sealed class StudioWeather
{
    private const int ForecastHours = 8;
    private const int StripCells = 6;

    private readonly IGameSession game;
    private readonly IClock clock;
    private readonly IWeatherOracle weather;

    public StudioWeather(IGameSession game, IClock clock, IWeatherOracle weather)
    {
        this.game = game;
        this.clock = clock;
        this.weather = weather;
    }

    public void Draw(in AppletFrame frame, Rect row, Action<Rect> open)
    {
        StudioChrome.DrawPanel(frame, row);
        var gold = frame.Theme.Palette.WarmAccent;
        var ink = frame.Theme.Palette.Ink;
        var muted = frame.Theme.Palette.InkMuted;
        var bells = EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds());
        var night = bells.Hour is < 6 or >= 18;
        var hours = game.IsLoggedIn && game.TerritoryId != 0
            ? weather.Forecast((ushort)game.TerritoryId, ForecastHours) ?? Array.Empty<WeatherWindow>()
            : Array.Empty<WeatherWindow>();
        var current = hours.Count > 0 ? hours[0] : default;
        var condition = NonEmpty(game.IsLoggedIn ? game.WeatherName : null,
            NonEmpty(current.Name, "Unknown skies"));
        var place = NonEmpty(game.ZoneName, NonEmpty(game.Character.WorldName, "Not logged in"));
        var world = NonEmpty(game.Character.WorldName, string.Empty);

        PaintWash(frame, row, condition, night);
        var inset = row.Inset(new Edges(frame.Units(12f), frame.Units(8f), frame.Units(12f), frame.Units(8f)));
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(4f));
        StudioChrome.DrawHeader(frame, stack.Take(frame.Units(14f)), "WEATHER", true, out var more);
        if (frame.Input.ConsumeClick(more))
        {
            open(row);
        }

        DrawHero(frame, stack.Take(MathF.Max(frame.Units(40f), inset.Height * 0.30f)), bells, condition,
            current.IconId, gold, ink, muted);
        DrawPlace(frame, stack.Take(frame.Units(28f)), place, world, condition, gold, ink, muted);
        DrawMeta(frame, stack.Take(frame.Units(16f)), hours, bells, night, gold, muted);
        DrawStrip(frame, stack.TakeRemaining(), hours, bells, gold, muted);

        if (frame.Input.ConsumeClick(row))
        {
            open(row);
        }
    }

    private static void DrawHero(in AppletFrame frame, Rect area, EorzeaTime bells, string condition, uint iconId,
        Vector4 gold, Vector4 ink, Vector4 muted)
    {
        var figure = MathF.Min(area.Height, frame.Units(56f));
        var icon = Rect.FromSize(new Vector2(area.Min.X, area.Center.Y - figure * 0.5f), new Vector2(figure));
        DrawSkyMark(frame, icon.Inset(frame.Units(4f)), condition, iconId, gold, bells);

        var rest = area.Inset(new Edges(figure + frame.Units(8f), 0f, 0f, 0f));
        var pillW = MathF.Min(frame.Units(62f), rest.Width * 0.34f);
        DrawHorizon(frame, rest.RightSlice(pillW), bells, gold, ink, muted);

        var clockBox = rest.Inset(new Edges(0f, 0f, pillW + frame.Units(8f), 0f));
        frame.Text.DrawFitted(clockBox.TopSlice(clockBox.Height * 0.72f), bells.Format(),
            new TextStyle(FontRole.Display, ink));
        frame.Text.DrawIn(clockBox.BottomSlice(frame.Units(14f)), "ET · " + Phase(bells),
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Left, 1f, 0.86f));
    }

    private static void DrawPlace(in AppletFrame frame, Rect area, string place, string world, string condition,
        Vector4 gold, Vector4 ink, Vector4 muted)
    {
        StudioMarks.Draw(frame.Paint, area.LeftSlice(frame.Units(14f)), StudioMark.Pin, gold);
        var copy = area.Inset(new Edges(frame.Units(18f), 0f, 0f, 0f));
        var title = place.ToUpperInvariant();
        if (world.Length > 0 && !string.Equals(world, place, StringComparison.OrdinalIgnoreCase))
        {
            title += " · " + world;
        }

        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(14f)), title,
            new TextStyle(FontRole.CaptionStrong, gold));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(13f)), condition,
            new TextStyle(FontRole.Caption, ink with { W = 0.90f }));
        _ = muted;
    }

    private void DrawMeta(in AppletFrame frame, Rect area, IReadOnlyList<WeatherWindow> hours, EorzeaTime bells,
        bool night, Vector4 gold, Vector4 muted)
    {
        StudioMarks.Draw(frame.Paint, area.LeftSlice(frame.Units(14f)),
            night ? StudioMark.Moon : StudioMark.Sun, muted);
        var shift = ShiftLabel(hours, bells);
        frame.Text.DrawEllipsized(area.Inset(new Edges(frame.Units(18f), 0f, 0f, 0f)), shift,
            new TextStyle(FontRole.Caption, gold, TextAlign.Left, 1f, 0.90f));
    }

    private static void DrawHorizon(in AppletFrame frame, Rect pill, EorzeaTime bells, Vector4 gold, Vector4 ink,
        Vector4 muted)
    {
        if (pill.Width < frame.Units(28f) || pill.Height < frame.Units(20f))
        {
            return;
        }

        var radius = MathF.Min(frame.Units(10f), pill.Height * 0.22f);
        frame.Paint.Fill(pill, frame.Theme.Palette.SurfaceRaised with { W = 0.42f }, radius);
        frame.Paint.Stroke(pill, gold with { W = 0.22f }, frame.Theme.Metrics.Hairline, radius);
        var dawnNext = bells.Hour is >= 18 or < 6;
        var body = pill.Inset(new Edges(frame.Units(6f), frame.Units(3f)));
        DrawHorizonRow(frame, body.TopSlice(body.Height * 0.5f), "Dawn", "06:00", dawnNext, gold, ink, muted);
        DrawHorizonRow(frame, body.BottomSlice(body.Height * 0.5f), "Dusk", "18:00", !dawnNext, gold, ink, muted);
    }

    private static void DrawHorizonRow(in AppletFrame frame, Rect row, string label, string bells, bool lit,
        Vector4 gold, Vector4 ink, Vector4 muted)
    {
        frame.Text.DrawEllipsized(row.LeftSlice(row.Width * 0.46f), label,
            new TextStyle(FontRole.Caption, lit ? gold : muted, TextAlign.Left, 1f, 0.78f));
        frame.Text.DrawIn(row.RightSlice(row.Width * 0.54f), bells,
            new TextStyle(FontRole.CaptionStrong, lit ? ink : muted, TextAlign.Right, 1f, 0.80f));
    }

    private static void DrawStrip(in AppletFrame frame, Rect area, IReadOnlyList<WeatherWindow> hours,
        EorzeaTime bells, Vector4 gold, Vector4 muted)
    {
        if (area.Height < frame.Units(22f) || area.Width < frame.Units(70f))
        {
            return;
        }

        var radius = MathF.Min(frame.Units(10f), area.Height * 0.28f);
        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceSunken with { W = 0.38f }, radius);
        frame.Paint.Stroke(area, gold with { W = 0.16f }, frame.Theme.Metrics.Hairline, radius);
        var inner = area.Inset(new Edges(frame.Units(5f), frame.Units(3f)));
        var cellW = inner.Width / StripCells;
        var previous = hours.Count > 0 ? hours[0].Name ?? string.Empty : string.Empty;
        for (var index = 0; index < StripCells; index++)
        {
            var cell = Rect.FromSize(new Vector2(inner.Min.X + cellW * index, inner.Min.Y),
                new Vector2(cellW, inner.Height));
            var when = bells.AddHours(index);
            var name = index < hours.Count ? hours[index].Name ?? string.Empty : string.Empty;
            var turning = index > 0 && name.Length > 0 &&
                !string.Equals(name, previous, StringComparison.OrdinalIgnoreCase);
            if (name.Length > 0)
            {
                previous = name;
            }

            var lit = index == 0 || turning;
            var labelH = MathF.Min(frame.Units(11f), cell.Height * 0.34f);
            frame.Text.DrawIn(cell.TopSlice(labelH), index == 0 ? "NOW" : ClockLabel(when.Hour),
                new TextStyle(FontRole.Caption, lit ? gold : muted with { W = muted.W * 0.75f }, TextAlign.Center, 1f,
                    0.70f));
            var mark = cell.Inset(new Edges(0f, labelH, 0f, 0f));
            var side = MathF.Min(mark.Height, frame.Units(18f));
            DrawSkyMark(frame, mark.Inset(new Edges(MathF.Max(0f, (mark.Width - side) * 0.5f), 0f)), name,
                index < hours.Count ? hours[index].IconId : 0u, lit ? gold : gold with { W = 0.70f }, when);
        }
    }

    private static void PaintWash(in AppletFrame frame, Rect row, string condition, bool night)
    {
        var art = row.Inset(frame.Units(2f));
        if (art.IsEmpty)
        {
            return;
        }

        frame.Paint.PushClip(art);
        var top = night ? new Vector4(0.08f, 0.09f, 0.16f, 0.42f) : new Vector4(0.16f, 0.18f, 0.26f, 0.28f);
        var bottom = night ? new Vector4(0.04f, 0.05f, 0.09f, 0.58f) : new Vector4(0.07f, 0.09f, 0.14f, 0.46f);
        frame.Paint.FillGradient(art, top, bottom, GradientAxis.Vertical);
        var lamp = new Vector2(art.Min.X + art.Width * 0.78f, art.Min.Y + art.Height * 0.28f);
        var radius = MathF.Max(8f, art.Height * 0.18f);
        if (night)
        {
            frame.Paint.Glow(Rect.FromSize(lamp - new Vector2(radius * 1.6f), new Vector2(radius * 3.2f)),
                new Vector4(0.78f, 0.84f, 1f, 0.10f), radius * 1.4f, radius);
            frame.Paint.FillCircle(lamp, radius * 0.42f, new Vector4(0.94f, 0.96f, 1f, 0.20f));
        }
        else
        {
            frame.Paint.Glow(Rect.FromSize(lamp - new Vector2(radius * 1.6f), new Vector2(radius * 3.2f)),
                new Vector4(1f, 0.84f, 0.48f, 0.12f), radius * 1.5f, radius);
            frame.Paint.FillCircle(lamp, radius * 0.38f, new Vector4(1f, 0.90f, 0.62f, 0.22f));
        }

        if (Wet(condition))
        {
            var stroke = MathF.Max(1f, frame.Units(1.1f));
            var tint = new Vector4(0.82f, 0.88f, 1f, 0.16f);
            for (var drop = 0; drop < 7; drop++)
            {
                var x = art.Min.X + art.Width * (0.12f + drop * 0.11f);
                var y = art.Min.Y + art.Height * (0.38f + (drop % 3) * 0.10f);
                frame.Paint.Line(new Vector2(x, y), new Vector2(x + frame.Units(3f), y + frame.Units(8f)), tint,
                    stroke);
            }
        }

        frame.Paint.PopClip();
    }

    private static void DrawSkyMark(in AppletFrame frame, Rect area, string name, uint iconId, Vector4 color,
        EorzeaTime bells)
    {
        if (iconId != 0)
        {
            var texture = frame.Textures.GameIcon(iconId);
            if (texture is { IsReady: true })
            {
                frame.Paint.Image(texture, CoverFit.Contained(texture.Size, area), Vector4.One);
                return;
            }
        }

        StudioMarks.Draw(frame.Paint, area, SkyGlyph(name, bells), color);
    }

    private string ShiftLabel(IReadOnlyList<WeatherWindow> hours, EorzeaTime bells)
    {
        if (hours.Count > 1)
        {
            var opening = hours[0].Name ?? string.Empty;
            for (var index = 1; index < hours.Count; index++)
            {
                var name = hours[index].Name ?? string.Empty;
                if (name.Length == 0 || string.Equals(name, opening, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var minutes = (hours[index].Starts - clock.UtcNow).TotalMinutes;
                return minutes < 1.0
                    ? name + " now"
                    : name + " in " + ((int)MathF.Round((float)minutes)).ToString(CultureInfo.InvariantCulture) +
                      " min";
            }

            if (opening.Length > 0)
            {
                return "Steady skies through the next bells";
            }
        }

        var left = bells.HoursUntilNextWeather();
        return "Next shift in " + left.ToString(CultureInfo.InvariantCulture) + (left == 1 ? " bell" : " bells");
    }

    private static StudioMark SkyGlyph(string weather, EorzeaTime bells)
    {
        if (Wet(weather))
        {
            return StudioMark.Rain;
        }

        if (Overcast(weather))
        {
            return StudioMark.Cloud;
        }

        return bells.Hour is < 6 or >= 18 ? StudioMark.Moon : StudioMark.Sun;
    }

    private static string Phase(EorzeaTime bells) => bells.Hour switch
    {
        < 5 => "Late night",
        < 6 => "Before dawn",
        < 12 => "Morning",
        < 17 => "Afternoon",
        < 18 => "Evening",
        _ => "Night",
    };

    private static string ClockLabel(int hour)
    {
        var wrapped = ((hour % 24) + 24) % 24;
        var period = wrapped >= 12 ? "pm" : "am";
        var twelve = wrapped % 12;
        if (twelve == 0)
        {
            twelve = 12;
        }

        return twelve.ToString(CultureInfo.InvariantCulture) + period;
    }

    private static bool Wet(string weather) =>
        Contains(weather, "rain") || Contains(weather, "shower") || Contains(weather, "thunder") ||
        Contains(weather, "storm") || Contains(weather, "snow");

    private static bool Overcast(string weather) =>
        Contains(weather, "cloud") || Contains(weather, "fog") || Contains(weather, "overcast") ||
        Contains(weather, "gloom") || Contains(weather, "dust");

    private static string NonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
