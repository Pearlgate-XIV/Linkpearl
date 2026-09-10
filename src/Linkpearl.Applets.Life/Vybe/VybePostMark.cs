using Linkpearl.Net;

namespace Linkpearl.Applets.Life.Vybe;

internal enum ComposeSheet : byte
{
    None = 0,
    SwitchVybe = 1,
    ConfirmVybe = 2,
    ConfirmPlus = 3,
    ConfirmStory = 4,
}

internal enum ContentRating : byte
{
    None = 0,
    Sfw = 1,
    Mature = 2,
    Nudity = 3,
    Explicit = 4,
    GraphicExplicit = 5,
}

internal static class VybePostMark
{
    public const string Vybe = "vybe";
    public const string Plus = "vybe_plus";

    public static readonly (ContentRating Rating, string Title, string Line)[] PlusRatings =
    {
        (ContentRating.Mature, "Mature / Suggestive",
            "Suggestive themes, revealing clothing, or implied adult content."),
        (ContentRating.Nudity, "Nudity",
            "Visible nudity without explicit sexual activity."),
        (ContentRating.Explicit, "Explicit",
            "Explicit sexual imagery or sexual activity."),
        (ContentRating.GraphicExplicit, "Graphic Explicit",
            "Highly graphic or intense explicit imagery."),
    };

    public static readonly string[] Descriptors =
    {
        "Kink / Fetish", "BDSM", "Roleplay", "Violence", "Gore", "Mature Themes", "Other",
    };

    public static bool IsPlus(PearlPost post)
    {
        if (string.Equals(post.Destination, Plus, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(post.Destination, Vybe, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (post.Id.StartsWith("plus-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return PlusRatingPicked(Read(post.ContentRating));
    }

    public static string Wire(ContentRating rating) => rating switch
    {
        ContentRating.Mature => "mature",
        ContentRating.Nudity => "nudity",
        ContentRating.Explicit => "explicit",
        ContentRating.GraphicExplicit => "graphic_explicit",
        _ => "sfw",
    };

    public static string Label(ContentRating rating) => rating switch
    {
        ContentRating.Mature => "Mature / Suggestive",
        ContentRating.Nudity => "Nudity",
        ContentRating.Explicit => "Explicit",
        ContentRating.GraphicExplicit => "Graphic Explicit",
        ContentRating.Sfw => "SFW",
        _ => "Not set",
    };

    public static ContentRating Read(string? wire) => wire switch
    {
        "mature" => ContentRating.Mature,
        "nudity" => ContentRating.Nudity,
        "explicit" => ContentRating.Explicit,
        "graphic_explicit" => ContentRating.GraphicExplicit,
        "sfw" => ContentRating.Sfw,
        _ => ContentRating.None,
    };

    public static bool PlusRatingPicked(ContentRating rating) =>
        rating is ContentRating.Mature or ContentRating.Nudity or ContentRating.Explicit
            or ContentRating.GraphicExplicit;
}
