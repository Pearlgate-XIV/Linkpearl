using Linkpearl.Applets.Life.Calendar;
using Linkpearl.Applets.Life.Market;
using Linkpearl.Chat;
using Linkpearl.Destinations;
using Linkpearl.Destinations.Social;
using Linkpearl.Pearls;
using Linkpearl.Persistence;
using Linkpearl.Phone;
using Linkpearl.Platform;

namespace Linkpearl.Host.Composition;

internal sealed class CharacterStateDesk : IDisposable
{
    private readonly IGameSession game;
    private readonly CharacterMigration migration;
    private readonly CalendarBook calendar;
    private readonly PearlLedger pearls;
    private readonly IHandsetLine line;
    private readonly FriendBook friends;
    private readonly MarketApplet market;
    private readonly ChatMarks marks;
    private readonly NoticeLedger notices;
    private readonly bool development;
    private ulong wanted;
    private bool reload;

    public CharacterStateDesk(IGameSession game, CharacterMigration migration, CalendarBook calendar,
        PearlLedger pearls, IHandsetLine line, FriendBook friends, MarketApplet market, ChatMarks marks,
        NoticeLedger notices, bool development)
    {
        this.game = game;
        this.migration = migration;
        this.calendar = calendar;
        this.pearls = pearls;
        this.line = line;
        this.friends = friends;
        this.market = market;
        this.marks = marks;
        this.notices = notices;
        this.development = development;
        game.CharacterChanged += OnCharacterChanged;
        game.LoggedOut += OnLoggedOut;
        migration.Changed += OnMigrationChanged;
        migration.FlushOutgoing += OnFlushOutgoing;
        Apply(game.Character.ContentId);
    }

    public void Tick()
    {
        if (reload || wanted != calendar.BoundId || wanted != pearls.BoundId || wanted != friends.BoundId ||
            wanted != market.BoundId || wanted != marks.BoundId || wanted != notices.BoundId)
        {
            TryApply();
        }
    }

    public void Dispose()
    {
        game.CharacterChanged -= OnCharacterChanged;
        game.LoggedOut -= OnLoggedOut;
        migration.Changed -= OnMigrationChanged;
        migration.FlushOutgoing -= OnFlushOutgoing;
        calendar.Bind(0UL);
        pearls.Bind(0UL, false);
        line.BindCharacter(0UL);
        friends.Bind(0UL);
        market.Bind(0UL);
        marks.Bind(0UL);
        notices.BindCharacter(0UL);
    }

    private void OnCharacterChanged(CharacterIdentity identity) => Apply(identity.ContentId);

    private void OnLoggedOut()
    {
        migration.Wake();
        Apply(0UL);
    }

    private void OnMigrationChanged()
    {
        reload = true;
        Apply(game.Character.ContentId);
    }

    private void OnFlushOutgoing()
    {
        calendar.Flush();
        pearls.Flush();
        line.Flush();
        friends.Flush();
        market.Flush();
        marks.Flush();
    }

    private void Apply(ulong contentId)
    {
        wanted = contentId;
        TryApply();
    }

    private void TryApply()
    {
        notices.BindCharacter(wanted);

        if (wanted != 0UL && !migration.HasClaimant && !migration.Refused)
        {
            return;
        }

        if (reload)
        {
            if (!calendar.Bind(0UL) || !pearls.Bind(0UL, false) || !line.BindCharacter(0UL) ||
                !friends.Bind(0UL) || !market.Bind(0UL) || !marks.Bind(0UL) ||
                !notices.BindCharacter(0UL))
            {
                return;
            }

            reload = false;
        }

        if (wanted != 0UL && migration.HasClaimant &&
            string.Equals(migration.Claimant, CharacterStatePaths.Hex(wanted), StringComparison.Ordinal))
        {
            migration.Resume(wanted);
        }

        if (!calendar.Bind(wanted) || !pearls.Bind(wanted, development) || !line.BindCharacter(wanted) ||
            !friends.Bind(wanted) || !market.Bind(wanted) || !marks.Bind(wanted) ||
            !notices.BindCharacter(wanted))
        {
            return;
        }
    }
}
