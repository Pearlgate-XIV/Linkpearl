using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Time;

namespace Linkpearl.Destinations.Home;

// The glass weather pane: zone and live sky on top, the Eorzean bell as the hero figure over a
// painted sky, then the shift countdown and a bell-by-bell strip. Every figure on it is read from
// the running game — nothing here is decorative data.
internal sealed class HomeWeatherCard
{
    private const int StripCells = 6;
    private const int ForecastHours = 9;

    private static readonly Vector2[] Stars =
    {
        new(0.10f, 0.22f),
        new(0.19f, 0.13f),
        new(0.31f, 0.30f),
        new(0.44f, 0.16f),
        new(0.78f, 0.14f),
        new(0.90f, 0.26f),
    };

    private readonly IGameSession game;
    private readonly IClock clock;
    private readonly IWeatherOracle weather;

    public HomeWeatherCard(IGameSession game, IClock clock, IWeatherOracle weather)
    {
        this.game = game;
        this.clock = clock;
        this.weather = weather;
    }

    public void Draw(in AppletFrame frame, Rect row)
    {
        var palette = frame.Theme.Palette;
        var gold = palette.WarmAccent;
        var ink = palette.Ink;
        var muted = palette.InkMuted;

        var bells = EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds());
        var night = bells.Hour is < 6 or >= 18;
        var hours = game.IsLoggedIn && game.TerritoryId != 0
            ? weather.Forecast((ushort)game.TerritoryId, ForecastHours) ?? Array.Empty<WeatherWindow>()
            : Array.Empty<WeatherWindow>();
        var current = hours.Count > 0 ? hours[0] : default;
        var condition = NonEmpty(game.IsLoggedIn ? game.WeatherName : null,
            NonEmpty(SkyName(current), "Unknown skies"));
        var place = NonEmpty(game.ZoneName, NonEmpty(game.Character.WorldName, "Not logged in"));

        CardChrome.DrawGold(frame, row);
        PaintSky(frame, row, condition, night);

        var inset = row.Inset(new Edges(frame.Units(13f), frame.Units(7f), frame.Units(13f), frame.Units(6f)));
        var stack = new Stack(inset, StackAxis.Vertical, 0f);
        DrawTop(frame, stack.Take(frame.Units(13f)), place, condition, current.IconId, bells, gold, ink);
        DrawHero(frame, stack.Take(frame.Units(33f)), bells, gold, ink, muted);
        DrawPulse(frame, stack.Take(frame.Units(12f)), hours, bells, night, gold, muted);
        DrawStrip(frame, stack.TakeRemaining(), hours, bells, gold, muted);

        if (frame.Input.ConsumeClick(row) && frame.Router is { } router && router.CanOpen("weather"))
        {
            router.OpenFrom("weather", row);
        }
    }

    private static void DrawTop(in AppletFrame frame, Rect area, string place, string condition, uint iconId,
        EorzeaTime bells, Vector4 gold, Vector4 ink)
    {
        HomeMarks.Draw(frame.Paint, area.LeftSlice(frame.Units(11f)), HomeMark.Pin, gold);

        var soft = ink with { W = ink.W * 0.86f };
        var mark = area.RightSlice(frame.Units(14f));
        DrawConditionMark(frame, mark, condition, iconId, soft, bells);

        var wanted = frame.Text.Measure(condition, FontRole.Caption).X + frame.Units(2f);
        var width = MathF.Min(area.Width * 0.44f, wanted);
        var conditionRow = new Rect(new Vector2(mark.Min.X - width - frame.Units(3f), area.Min.Y),
            new Vector2(mark.Min.X - frame.Units(3f), area.Max.Y));
        if (conditionRow.Width > frame.Units(8f))
        {
            frame.Text.DrawEllipsized(conditionRow, condition,
                new TextStyle(FontRole.Caption, soft, TextAlign.Right));
        }

        var zone = new Rect(new Vector2(area.Min.X + frame.Units(12f), area.Min.Y),
            new Vector2(conditionRow.Min.X - frame.Units(6f), area.Max.Y));
        if (zone.Width > frame.Units(12f))
        {
            frame.Text.DrawEllipsized(zone, place.ToUpperInvariant(),
                new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Left, 1f, 0.94f));
        }
    }

    private static void DrawHero(in AppletFrame frame, Rect area, EorzeaTime bells, Vector4 gold, Vector4 ink,
        Vector4 muted)
    {
        var pillWidth = MathF.Min(frame.Units(58f), area.Width * 0.32f);
        DrawSunPill(frame, area.RightSlice(pillWidth).Inset(new Edges(0f, frame.Units(2f), 0f, frame.Units(3f))),
            bells, gold, ink, muted);

        var box = area.LeftSlice(area.Width * 0.56f);
        var text = bells.Format();
        frame.Text.DrawFitted(box, text, new TextStyle(FontRole.Display, ink));

        var glyph = frame.Text.Measure(text, FontRole.Display);
        if (glyph.X < 1f || glyph.Y < 1f)
        {
            return;
        }

        var fit = MathF.Min(1f, MathF.Min(box.Width / glyph.X, box.Height / glyph.Y));
        var drawn = glyph * fit;
        var tag = new Rect(new Vector2(box.Min.X + drawn.X + frame.Units(4f), box.Center.Y - drawn.Y * 0.44f),
            new Vector2(box.Max.X, box.Center.Y));
        if (tag.Width > frame.Units(10f))
        {
            frame.Text.DrawIn(tag, "ET", new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Left, 1f, 0.82f));
        }
    }

    // Sunrise and sunset read like the high/low pair on a mundane weather app, and in Eorzea they
    // are fixed bells — so the pane leans on emphasis instead: whichever comes next is lit.
    private static void DrawSunPill(in AppletFrame frame, Rect pill, EorzeaTime bells, Vector4 gold, Vector4 ink,
        Vector4 muted)
    {
        if (pill.Width < frame.Units(30f) || pill.Height < frame.Units(18f))
        {
            return;
        }

        var radius = MathF.Min(frame.Units(9f), pill.Height * 0.34f);
        frame.Paint.Fill(pill, frame.Theme.Palette.SurfaceRaised with { W = 0.44f }, radius);
        frame.Paint.Stroke(pill, gold with { W = 0.22f }, frame.Theme.Metrics.Hairline, radius);

        var dawnNext = bells.Hour is >= 18 or < 6;
        var body = pill.Inset(new Edges(frame.Units(6f), frame.Units(2f), frame.Units(6f), frame.Units(2f)));
        var upper = body.TopSlice(body.Height * 0.5f);
        var lower = body.BottomSlice(body.Height * 0.5f);
        var stroke = MathF.Max(1f, frame.Units(1.2f));

        DrawArrow(frame.Paint, upper.LeftSlice(frame.Units(9f)), true, dawnNext ? gold : muted, stroke);
        DrawArrow(frame.Paint, lower.LeftSlice(frame.Units(9f)), false, dawnNext ? muted : gold, stroke);
        frame.Text.DrawIn(upper.Inset(new Edges(frame.Units(10f), 0f, 0f, 0f)), "06:00",
            new TextStyle(FontRole.CaptionStrong, dawnNext ? ink : muted, TextAlign.Right, 1f, 0.8f));
        frame.Text.DrawIn(lower.Inset(new Edges(frame.Units(10f), 0f, 0f, 0f)), "18:00",
            new TextStyle(FontRole.CaptionStrong, dawnNext ? muted : ink, TextAlign.Right, 1f, 0.8f));
    }

    private void DrawPulse(in AppletFrame frame, Rect area, IReadOnlyList<WeatherWindow> hours, EorzeaTime bells,
        bool night, Vector4 gold, Vector4 muted)
    {
        HomeMarks.Draw(frame.Paint, area.LeftSlice(frame.Units(11f)), night ? HomeMark.Moon : HomeMark.Sun, muted);

        var shift = ShiftLabel(hours, bells);
        var wanted = frame.Text.Measure(shift, FontRole.CaptionStrong).X * 0.86f + frame.Units(3f);
        var shiftRow = area.RightSlice(MathF.Min(area.Width * 0.54f, wanted));
        frame.Text.DrawEllipsized(shiftRow, shift,
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Right, 1f, 0.86f));

        var clockMark = new Rect(new Vector2(shiftRow.Min.X - frame.Units(13f), area.Min.Y),
            new Vector2(shiftRow.Min.X - frame.Units(3f), area.Max.Y));
        HomeMarks.Draw(frame.Paint, clockMark, HomeMark.Clock, gold with { W = 0.62f });

        var phaseRow = new Rect(new Vector2(area.Min.X + frame.Units(12f), area.Min.Y),
            new Vector2(clockMark.Min.X - frame.Units(4f), area.Max.Y));
        if (phaseRow.Width > frame.Units(12f))
        {
            frame.Text.DrawEllipsized(phaseRow, Phase(bells),
                new TextStyle(FontRole.Caption, muted, TextAlign.Left, 1f, 0.88f));
        }
    }

    private static void DrawStrip(in AppletFrame frame, Rect area, IReadOnlyList<WeatherWindow> hours,
        EorzeaTime bells, Vector4 gold, Vector4 muted)
    {
        if (area.Height < frame.Units(15f) || area.Width < frame.Units(80f))
        {
            return;
        }

        var radius = MathF.Min(frame.Units(9f), area.Height * 0.36f);
        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceSunken with { W = 0.34f }, radius);
        frame.Paint.Stroke(area, gold with { W = 0.14f }, frame.Theme.Metrics.Hairline, radius);

        var inner = area.Inset(new Edges(frame.Units(5f), frame.Units(2f), frame.Units(5f), frame.Units(2f)));
        var cellWidth = inner.Width / StripCells;
        var labelHeight = MathF.Min(frame.Units(8f), inner.Height * 0.38f);
        var previous = SkyName(hours.Count > 0 ? hours[0] : default);
        for (var index = 0; index < StripCells; index++)
        {
            var cell = Rect.FromSize(new Vector2(inner.Min.X + cellWidth * index, inner.Min.Y),
                new Vector2(cellWidth, inner.Height));
            var when = bells.AddHours(index);
            var name = index < hours.Count ? SkyName(hours[index]) : string.Empty;
            var turning = index > 0 && name.Length > 0 &&
                !string.Equals(name, previous, StringComparison.OrdinalIgnoreCase);
            if (name.Length > 0)
            {
                previous = name;
            }

            var lit = index == 0 || turning;
            frame.Text.DrawIn(cell.TopSlice(labelHeight), index == 0 ? "NOW" : ClockLabel(when.Hour),
                new TextStyle(FontRole.Caption, lit ? gold : muted with { W = muted.W * 0.8f }, TextAlign.Center, 1f,
                    0.7f));

            var below = cell.BottomSlice(MathF.Max(0f, cell.Height - labelHeight));
            var side = MathF.Min(below.Height, frame.Units(16f));
            var mark = below.Inset(new Edges(MathF.Max(0f, (below.Width - side) * 0.5f), 0f));
            DrawConditionMark(frame, mark, name, index < hours.Count ? hours[index].IconId : 0u,
                lit ? gold : gold with { W = gold.W * 0.72f }, when);
        }
    }

    private static void DrawConditionMark(in AppletFrame frame, Rect area, string name, uint iconId, Vector4 color,
        EorzeaTime now)
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

        HomeMarks.Draw(frame.Paint, area, Glyph(name, now), color);
    }

    // A painted sky rather than a flat panel: the bell decides moon or sun, the live weather name
    // decides whether cloud banks and rain sit in front of it.
    private static void PaintSky(in AppletFrame frame, Rect row, string condition, bool night)
    {
        var art = row.Inset(frame.Units(1.5f));
        if (art.IsEmpty)
        {
            return;
        }

        frame.Paint.PushClip(art);
        var top = night ? new Vector4(0.07f, 0.08f, 0.14f, 0.60f) : new Vector4(0.12f, 0.15f, 0.23f, 0.44f);
        var bottom = night ? new Vector4(0.03f, 0.04f, 0.07f, 0.76f) : new Vector4(0.06f, 0.08f, 0.13f, 0.60f);
        frame.Paint.FillGradient(art, top, bottom, GradientAxis.Vertical);

        var center = new Vector2(art.Min.X + art.Width * 0.615f, art.Min.Y + art.Height * 0.34f);
        var radius = MathF.Max(1f, art.Height * 0.24f);
        var disc = Rect.FromSize(center - new Vector2(radius * 2f), new Vector2(radius * 4f));
        if (night)
        {
            foreach (var star in Stars)
            {
                frame.Paint.FillCircle(new Vector2(art.Min.X + art.Width * star.X, art.Min.Y + art.Height * star.Y),
                    MathF.Max(0.7f, radius * 0.045f), new Vector4(0.92f, 0.94f, 1f, 0.22f));
            }

            frame.Paint.Glow(disc, new Vector4(0.84f, 0.88f, 0.99f, 0.10f), radius * 1.6f, radius * 1.1f);
            frame.Paint.FillCircle(center, radius, new Vector4(0.94f, 0.95f, 1f, 0.26f));
            frame.Paint.FillCircle(center + new Vector2(radius * 0.46f, -radius * 0.22f), radius * 0.94f,
                bottom with { W = 0.60f });
        }
        else
        {
            frame.Paint.Glow(disc, new Vector4(1f, 0.86f, 0.56f, 0.12f), radius * 1.6f, radius * 1.2f);
            frame.Paint.FillCircle(center, radius * 0.9f, new Vector4(1f, 0.90f, 0.66f, 0.24f));
        }

        if (Overcast(condition))
        {
            DrawClouds(frame.Paint, art, night);
        }

        if (Wet(condition))
        {
            DrawFall(frame.Paint, art);
        }

        frame.Paint.PopClip();
    }

    private static void DrawClouds(IPaintSurface paint, Rect art, bool night)
    {
        var tint = night ? new Vector4(0.80f, 0.85f, 0.96f, 0.09f) : new Vector4(1f, 1f, 1f, 0.10f);
        Puff(paint, new Vector2(art.Min.X + art.Width * 0.55f, art.Min.Y + art.Height * 0.46f), art.Height * 0.20f,
            tint);
        Puff(paint, new Vector2(art.Min.X + art.Width * 0.75f, art.Min.Y + art.Height * 0.33f), art.Height * 0.15f,
            tint with { W = tint.W * 0.78f });
        Puff(paint, new Vector2(art.Min.X + art.Width * 0.89f, art.Min.Y + art.Height * 0.49f), art.Height * 0.13f,
            tint with { W = tint.W * 0.6f });
    }

    private static void Puff(IPaintSurface paint, Vector2 center, float size, Vector4 color)
    {
        paint.Fill(
            Rect.FromSize(new Vector2(center.X - size, center.Y + size * 0.16f), new Vector2(size * 2f, size * 0.52f)),
            color, size * 0.26f);
        paint.FillCircle(center, size * 0.62f, color);
        paint.FillCircle(center + new Vector2(-size * 0.62f, size * 0.26f), size * 0.44f, color);
        paint.FillCircle(center + new Vector2(size * 0.68f, size * 0.24f), size * 0.4f, color);
    }

    private static void DrawFall(IPaintSurface paint, Rect art)
    {
        var color = new Vector4(0.78f, 0.86f, 1f, 0.14f);
        var thickness = MathF.Max(1f, art.Height * 0.012f);
        var length = art.Height * 0.11f;
        for (var index = 0; index < 7; index++)
        {
            var x = art.Min.X + art.Width * (0.52f + index * 0.065f);
            var y = art.Min.Y + art.Height * (index % 2 == 0 ? 0.56f : 0.62f);
            paint.Line(new Vector2(x, y), new Vector2(x - length * 0.35f, y + length), color, thickness);
        }
    }

    private static void DrawArrow(IPaintSurface paint, Rect area, bool up, Vector4 color, float thickness)
    {
        var center = area.Center;
        var reach = MathF.Min(area.Height, area.Width * 1.6f) * 0.34f;
        var wing = reach * 0.62f;
        var tip = new Vector2(center.X, up ? center.Y - reach : center.Y + reach);
        var tail = new Vector2(center.X, up ? center.Y + reach : center.Y - reach);
        var drop = up ? wing : -wing;
        paint.Line(tail, tip, color, thickness);
        paint.Line(tip, tip + new Vector2(-wing, drop), color, thickness);
        paint.Line(tip, tip + new Vector2(wing, drop), color, thickness);
    }

    // Weather windows are eight bells wide, so the pane reports the next distinct sky and how many
    // real minutes away it is — the forecast carries true wall-clock boundaries for that.
    private string ShiftLabel(IReadOnlyList<WeatherWindow> hours, EorzeaTime bells)
    {
        if (hours.Count > 1)
        {
            var opening = SkyName(hours[0]);
            for (var index = 1; index < hours.Count; index++)
            {
                var name = SkyName(hours[index]);
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
                return "Steady skies";
            }
        }

        var left = bells.HoursUntilNextWeather();
        return "Shift in " + left.ToString(CultureInfo.InvariantCulture) + (left == 1 ? " bell" : " bells");
    }

    private static string Phase(EorzeaTime bells) => bells.Hour switch
    {
        < 4 => "Eorzean deep night",
        < 6 => "Eorzean small hours",
        < 9 => "Eorzean dawn",
        < 12 => "Eorzean morning",
        < 17 => "Eorzean afternoon",
        < 19 => "Eorzean dusk",
        _ => "Eorzean night",
    };

    private static string ClockLabel(int hour)
    {
        var wrapped = ((hour % 24) + 24) % 24;
        return wrapped.ToString("00", CultureInfo.InvariantCulture);
    }

    private static HomeMark Glyph(string weather, EorzeaTime now)
    {
        if (Overcast(weather))
        {
            return HomeMark.Cloud;
        }

        if (Wet(weather))
        {
            return HomeMark.Rain;
        }

        if ((now.Hour < 6 || now.Hour >= 18) &&
            (Contains(weather, "fair") || Contains(weather, "clear") || weather.Length == 0))
        {
            return HomeMark.Moon;
        }

        return HomeMark.Sun;
    }

    private static bool Overcast(string weather) =>
        Contains(weather, "cloud") || Contains(weather, "fog") || Contains(weather, "overcast") ||
        Contains(weather, "gloom") || Contains(weather, "dust") || Contains(weather, "mist") ||
        Contains(weather, "wind") || Contains(weather, "gale");

    private static bool Wet(string weather) =>
        Contains(weather, "rain") || Contains(weather, "shower") || Contains(weather, "thunder") ||
        Contains(weather, "storm") || Contains(weather, "snow") || Contains(weather, "blizzard") ||
        Contains(weather, "sleet");

    private static string SkyName(WeatherWindow window) => window.Name ?? string.Empty;

    private static string NonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
