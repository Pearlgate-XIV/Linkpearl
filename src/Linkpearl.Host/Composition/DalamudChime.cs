using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Linkpearl.Platform;
using Linkpearl.Preferences;

namespace Linkpearl.Host.Composition;

public sealed class DalamudChime : IChime
{
    private readonly IChatGui chat;
    private readonly DisplayPreferences display;
    private readonly IGameSession game;

    public DalamudChime(IChatGui chat, DisplayPreferences display, IGameSession game)
    {
        this.chat = chat;
        this.display = display;
        this.game = game;
    }

    public void Ring(string title, string body)
    {
        if (display.Hushed(game.IsInDuty || game.IsInCutscene))
        {
            return;
        }

        var line = title.Length == 0 ? body : title + " — " + body;
        chat.Print(new XivChatEntry
        {
            Type = XivChatType.Echo,
            Message = new SeStringBuilder().AddText("[Linkpearl] " + line).BuiltString,
        });
    }
}
