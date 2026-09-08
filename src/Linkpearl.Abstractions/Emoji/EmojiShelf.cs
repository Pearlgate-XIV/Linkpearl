namespace Linkpearl.Emoji;

public static class EmojiShelf
{
    public static readonly (EmojiGroup Group, string Mark, string Title)[] Groups =
    {
        (EmojiGroup.Recent, "🕘", "Recent"),
        (EmojiGroup.Faces, "😀", "Smileys & Emotion"),
        (EmojiGroup.People, "👋", "People & Body"),
        (EmojiGroup.Nature, "🐱", "Animals & Nature"),
        (EmojiGroup.Food, "🍕", "Food & Drink"),
        (EmojiGroup.Play, "⚽", "Activities"),
        (EmojiGroup.Travel, "🚗", "Travel & Places"),
        (EmojiGroup.Things, "💡", "Objects"),
        (EmojiGroup.Signs, "❤️", "Symbols"),
        (EmojiGroup.Flags, "🏁", "Flags"),
    };

    public static IReadOnlyList<EmojiMark> All { get; } = Build();

    public static IReadOnlyList<EmojiMark> Of(EmojiGroup group)
    {
        var hits = new List<EmojiMark>();
        for (var index = 0; index < All.Count; index++)
        {
            if (All[index].Group == group)
            {
                hits.Add(All[index]);
            }
        }

        return hits;
    }

    public static bool CanTone(string glyph)
    {
        var key = EmojiBits.BaseOf(glyph);
        for (var index = 0; index < All.Count; index++)
        {
            if (All[index].Tones && string.Equals(All[index].Value, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static List<EmojiMark> Build()
    {
        var shelf = new List<EmojiMark>(320);
        Face(shelf);
        People(shelf);
        Nature(shelf);
        Food(shelf);
        Play(shelf);
        Travel(shelf);
        Things(shelf);
        Signs(shelf);
        Flags(shelf);
        return shelf;
    }

    private static void Face(List<EmojiMark> shelf)
    {
        Add(shelf, EmojiGroup.Faces, false,
            "😀 grin happy smile", "😃 smile happy", "😄 happy laugh", "😁 grin teeth",
            "😆 laugh happy", "😅 sweat smile", "😂 joy laugh cry funny tears", "🤣 rofl laugh funny",
            "😊 blush smile kind", "😇 halo angel", "🙂 smile slight", "🙃 upside sarcasm",
            "😉 wink flirt", "😌 relieved calm", "😍 heart eyes love", "🥰 smiling hearts love",
            "😘 kiss love", "😗 kiss", "😙 kiss smile", "😚 kiss closed",
            "😋 yum tasty", "😛 tongue", "😜 wink tongue", "🤪 zany wild",
            "😝 tongue closed", "🤑 money", "🤗 hug", "🤭 oops hand",
            "🤫 shush quiet", "🤔 think hmm", "🫡 salute yes", "🤐 zipper quiet",
            "🤨 raised brow", "😐 neutral", "😑 expressionless", "😶 silent",
            "😏 smirk", "😒 unamused bored", "🙄 eye roll", "😬 grimace",
            "🤥 lie pinocchio", "😔 sad pensive", "😪 sleepy", "🤤 drool",
            "😴 sleep zzz", "😷 mask sick", "🤒 thermometer sick", "🤕 hurt bandage",
            "🤢 nauseated sick", "🤮 vomit sick", "🥵 hot", "🥶 cold",
            "🥴 woozy", "😵 dizzy", "🤯 mind blown shocked", "🤠 cowboy",
            "🥳 party celebrate", "😎 cool sunglasses", "🤓 nerd", "🧐 monocle",
            "😕 confused", "😟 worried", "🙁 frown", "☹️ frown sad",
            "😮 surprise wow", "😯 hushed", "😲 astonished", "😳 flushed",
            "🥺 pleading puppy", "🥹 tears hold", "😦 frown open", "😧 anguished",
            "😨 fear", "😰 anxious sweat", "😥 sad relieved", "😢 cry sad",
            "😭 sob cry tears", "😱 scream fear", "😖 confounded", "😣 persevering",
            "😞 disappointed sad", "😓 down sweat", "😩 weary", "😫 tired",
            "🥱 yawn bored", "😤 huff steam", "😡 angry mad", "😠 angry",
            "🤬 swear mad", "😈 devil smile", "👿 angry devil", "💀 skull dead",
            "☠️ skull bones", "💩 poop", "🤡 clown", "👻 ghost",
            "👽 alien", "👾 invader", "🤖 robot", "😺 cat smile",
            "😸 cat grin", "😹 cat joy laugh", "😻 cat love", "😼 cat smirk",
            "😽 cat kiss", "🙀 cat scream", "😿 cat cry", "😾 cat pout",
            "🙈 see no", "🙉 hear no", "🙊 speak no", "💋 kiss mark",
            "💌 love letter", "💘 heart arrow", "💝 heart ribbon", "💖 sparkle heart",
            "💗 growing heart", "💓 beating heart", "💞 revolving hearts", "💕 two hearts",
            "💟 heart decoration", "❣️ heart exclaim", "💔 broken heart", "❤️ red heart love",
            "🩷 pink heart", "🧡 orange heart", "💛 yellow heart", "💚 green heart",
            "💙 blue heart", "💜 purple heart", "🖤 black heart", "🤍 white heart",
            "🤎 brown heart", "💯 hundred score", "💢 anger", "💥 boom",
            "💫 dizzy star", "💦 sweat drops", "💨 dash", "🕳️ hole",
            "💣 bomb", "💬 speech", "👁️‍🗨️ eye speech", "🗯️ anger bubble",
            "💭 thought", "💤 zzz sleep");
    }

    private static void People(List<EmojiMark> shelf)
    {
        Add(shelf, EmojiGroup.People, true,
            "👋 wave hello", "🤚 raised back", "🖐️ raised hand", "✋ hand stop",
            "🖖 vulcan", "👌 ok", "🤌 pinch", "🤏 pinch small",
            "✌️ peace", "🤞 luck", "🤟 love you", "🤘 horns",
            "🤙 call me", "👈 left", "👉 right", "👆 up",
            "👇 down", "☝️ index", "👍 like yes thumb", "👎 dislike no",
            "✊ fist", "👊 punch", "🤛 left fist", "🤜 right fist",
            "👏 clap", "🙌 hooray", "🫶 heart hands love", "👐 open hands",
            "🤲 palms", "🤝 handshake", "🙏 pray thanks please", "✍️ write",
            "💅 nails", "🤳 selfie", "💪 muscle strong", "🦵 leg",
            "🦶 foot", "👂 ear", "👃 nose");
        Add(shelf, EmojiGroup.People, false,
            "👀 eyes look", "👁️ eye", "👅 tongue", "👄 lips",
            "🫦 bite", "👶 baby", "👧 girl", "👦 boy",
            "👩 woman", "👨 man", "👱 blond", "🧔 beard",
            "🧓 older", "👮 cop", "🕵️ detective", "💂 guard",
            "🥷 ninja", "👷 worker", "👸 princess", "🤴 prince",
            "👳 turban", "👲 gua pi", "🧕 hijab", "🤵 tuxedo",
            "👰 bride", "🤰 pregnant", "👼 angel", "🎅 santa",
            "🤶 mrs claus", "🦸 hero", "🦹 villain", "🧙 mage",
            "🧚 fairy", "🧛 vampire", "🧜 mermaid", "🧝 elf",
            "🧞 genie", "🧟 zombie", "💆 massage", "💇 haircut",
            "🚶 walk", "🧍 stand", "🏃 run", "💃 dance",
            "🕺 dance man", "🕴️ levitate", "👯 dancers", "🧘 yoga",
            "🛀 bath", "🛌 sleep bed", "🧑‍💻 coder dev", "👩‍💻 woman coder",
            "👨‍💻 man coder", "👨‍👩‍👧 family", "👩‍❤️‍👩 women couple", "🏳️‍🌈 pride rainbow");
    }

    private static void Nature(List<EmojiMark> shelf)
    {
        Add(shelf, EmojiGroup.Nature, false,
            "🐶 dog", "🐱 cat", "🐭 mouse", "🐹 hamster",
            "🐰 rabbit", "🦊 fox", "🐻 bear", "🐼 panda",
            "🐨 koala", "🐯 tiger", "🦁 lion", "🐮 cow",
            "🐷 pig", "🐸 frog", "🐵 monkey", "🙈 monkey see",
            "🐔 chicken", "🐧 penguin", "🐦 bird", "🐤 chick",
            "🦆 duck", "🦅 eagle", "🦉 owl", "🦇 bat",
            "🐺 wolf", "🐗 boar", "🐴 horse", "🦄 unicorn",
            "🐝 bee", "🐛 bug", "🦋 butterfly", "🐌 snail",
            "🐞 ladybug", "🐜 ant", "🪲 beetle", "🐢 turtle",
            "🐍 snake", "🦎 lizard", "🐙 octopus", "🦑 squid",
            "🦐 shrimp", "🦞 lobster", "🦀 crab", "🐡 blowfish",
            "🐠 fish tropical", "🐟 fish", "🐬 dolphin", "🐳 whale",
            "🦈 shark", "🐊 croc", "🐅 tiger", "🐆 leopard",
            "🦓 zebra", "🦍 gorilla", "🦧 orangutan", "🐘 elephant",
            "🦣 mammoth", "🦏 rhino", "🦛 hippo", "🐪 camel",
            "🦒 giraffe", "🦘 kangaroo", "🦬 bison", "🐃 buffalo",
            "🌸 blossom", "💮 white flower", "🏵️ rosette", "🌹 rose",
            "🥀 wilted", "🌺 hibiscus", "🌻 sunflower", "🌼 blossom",
            "🌷 tulip", "🌱 seedling", "🌲 evergreen", "🌳 deciduous",
            "🌴 palm", "🌵 cactus", "🌾 sheaf", "🌿 herb",
            "☘️ shamrock", "🍀 clover luck", "🍁 maple", "🍂 fallen",
            "🍃 leaves", "🍄 mushroom", "🌙 moon", "☀️ sun",
            "⭐ star", "🌟 glow star", "✨ sparkles", "⚡ zap lightning",
            "🔥 fire flame", "💥 boom", "❄️ snow", "🌈 rainbow",
            "☁️ cloud", "⛅ sun cloud", "🌧️ rain", "⛈️ storm",
            "🌩️ lightning", "🌨️ snow cloud", "💧 droplet", "🌊 ocean wave");
    }

    private static void Food(List<EmojiMark> shelf)
    {
        Add(shelf, EmojiGroup.Food, false,
            "🍇 grapes", "🍈 melon", "🍉 watermelon", "🍊 tangerine",
            "🍋 lemon", "🍌 banana", "🍍 pineapple", "🥭 mango",
            "🍎 apple red", "🍏 apple green", "🍐 pear", "🍑 peach",
            "🍒 cherries", "🍓 strawberry", "🫐 blueberry", "🥝 kiwi",
            "🍅 tomato", "🫒 olive", "🥥 coconut", "🥑 avocado",
            "🍆 eggplant", "🥔 potato", "🥕 carrot", "🌽 corn",
            "🌶️ pepper hot", "🫑 bell pepper", "🥒 cucumber", "🥬 leafy",
            "🥦 broccoli", "🧄 garlic", "🧅 onion", "🍄 mushroom",
            "🍞 bread", "🥐 croissant", "🥖 baguette", "🫓 flatbread",
            "🥨 pretzel", "🥯 bagel", "🥞 pancakes", "🧇 waffle",
            "🧀 cheese", "🍖 meat", "🍗 poultry", "🥩 steak",
            "🥓 bacon", "🍔 burger", "🍟 fries", "🍕 pizza",
            "🌭 hotdog", "🥪 sandwich", "🌮 taco", "🌯 burrito",
            "🫔 tamale", "🥙 stuffed", "🧆 falafel", "🥚 egg",
            "🍳 cooking", "🥘 paella", "🍲 stew", "🫕 fondue",
            "🥣 bowl", "🥗 salad", "🍿 popcorn", "🧈 butter",
            "🧂 salt", "🥫 canned", "🍱 bento", "🍘 rice cracker",
            "🍙 rice ball", "🍚 rice", "🍛 curry", "🍜 ramen noodles",
            "🍝 pasta", "🍠 sweet potato", "🍢 oden", "🍣 sushi",
            "🍤 shrimp fry", "🍥 fish cake", "🥮 moon cake", "🍡 dango",
            "🥟 dumpling", "🥠 fortune", "🥡 takeout", "🦀 crab",
            "🍦 ice cream", "🍧 shaved ice", "🍨 ice cream cup", "🍩 donut",
            "🍪 cookie", "🎂 cake birthday", "🍰 shortcake", "🧁 cupcake",
            "🥧 pie", "🍫 chocolate", "🍬 candy", "🍭 lollipop",
            "🍮 custard", "🍯 honey", "🍼 bottle", "🥛 milk",
            "☕ coffee", "🫖 teapot", "🍵 tea", "🍶 sake",
            "🍾 champagne", "🍷 wine", "🍸 cocktail", "🍹 tropical drink",
            "🍺 beer", "🍻 cheers beers", "🥂 toast", "🥃 tumbler",
            "🥤 cup straw", "🧋 boba tea", "🧃 juice box", "🧉 mate");
    }

    private static void Play(List<EmojiMark> shelf)
    {
        Add(shelf, EmojiGroup.Play, false,
            "⚽ soccer", "🏀 basketball", "🏈 football", "⚾ baseball",
            "🥎 softball", "🎾 tennis", "🏐 volleyball", "🏉 rugby",
            "🥏 frisbee", "🎱 8ball pool", "🪀 yo yo", "🏓 ping pong",
            "🏸 badminton", "🥅 goal", "⛳ golf", "🪁 kite",
            "🏹 bow", "🎣 fishing", "🤿 dive", "🥊 boxing",
            "🥋 martial", "🎽 running shirt", "🛹 skate", "🛼 roller",
            "🛷 sled", "⛸️ ice skate", "🥌 curling", "🎿 ski",
            "⛷️ skier", "🏂 snowboard", "🪂 parachute", "🏋️ lift",
            "🤼 wrestle", "🤸 cartwheel", "⛹️ ball", "🤺 fence",
            "🤾 handball", "🏌️ golf play", "🏇 horse race", "🧘 yoga",
            "🏄 surf", "🏊 swim", "🤽 water polo", "🚣 row",
            "🧗 climb", "🚵 mountain bike", "🚴 bike", "🏆 trophy",
            "🥇 gold medal", "🥈 silver", "🥉 bronze", "🏅 sports medal",
            "🎖️ military medal", "🏵️ rosette", "🎗️ reminder", "🎫 ticket",
            "🎟️ tickets", "🎪 circus", "🤹 juggle", "🎭 masks theater",
            "🩰 ballet", "🎨 art palette", "🎬 clapper movie", "🎤 mic",
            "🎧 headphones", "🎼 score music", "🎹 piano", "🥁 drums",
            "🎷 sax", "🎺 trumpet", "🎸 guitar", "🪕 banjo",
            "🎻 violin", "🎲 dice", "♟️ chess", "🎯 dart bullseye",
            "🎳 bowling", "🎮 game controller", "🕹️ joystick", "🎰 slots",
            "🧩 puzzle", "🧸 teddy", "🪅 pinata", "🪩 disco",
            "🪄 wand", "🎉 party popper", "🎊 confetti", "🎈 balloon");
    }

    private static void Travel(List<EmojiMark> shelf)
    {
        Add(shelf, EmojiGroup.Travel, false,
            "🚗 car", "🚕 taxi", "🚙 suv", "🚌 bus",
            "🚎 trolley", "🏎️ race car", "🚓 police car", "🚑 ambulance",
            "🚒 fire truck", "🚐 van", "🛻 truck pickup", "🚚 delivery",
            "🚛 lorry", "🚜 tractor", "🏍️ motorcycle", "🛵 scooter",
            "🚲 bike", "🛴 kick scooter", "🛹 skateboard", "🚏 bus stop",
            "🛣️ motorway", "🛤️ railway", "⛽ fuel", "🚨 siren",
            "🚥 traffic light", "🚦 vertical lights", "🛑 stop", "⚓ anchor",
            "⛵ sail", "🛶 canoe", "🚤 speedboat", "🛳️ cruise",
            "⛴️ ferry", "🛥️ motor boat", "🚢 ship", "✈️ airplane",
            "🛫 depart", "🛬 arrive", "🪂 parachute", "💺 seat",
            "🚁 helicopter", "🚟 railway", "🚠 cable", "🚡 gondola",
            "🛰️ satellite", "🚀 rocket", "🛸 ufo", "🛎️ bellhop",
            "🧳 luggage", "⌛ hourglass", "⏳ timer", "⌚ watch",
            "⏰ alarm", "⏱️ stopwatch", "🗺️ map", "🗾 japan map",
            "🏔️ snow mountain", "⛰️ mountain", "🌋 volcano", "🗻 fuji",
            "🏕️ camp", "🏖️ beach", "🏜️ desert", "🏝️ island",
            "🏞️ park", "🏟️ stadium", "🏛️ classical", "🏗️ construction",
            "🏘️ houses", "🏚️ derelict", "🏠 house", "🏡 garden house",
            "🏢 office", "🏣 post jp", "🏤 post eu", "🏥 hospital",
            "🏦 bank", "🏨 hotel", "🏩 love hotel", "🏪 convenience",
            "🏫 school", "🏬 department", "🏭 factory", "🏯 castle jp",
            "🏰 castle", "💒 wedding", "🗼 tokyo tower", "🗽 liberty",
            "⛪ church", "🕌 mosque", "🛕 hindu temple", "🕍 synagogue",
            "🕋 kaaba", "⛲ fountain", "⛺ tent", "🌁 foggy",
            "🌃 night stars", "🏙️ cityscape", "🌄 sunrise mountain", "🌅 sunrise",
            "🌆 dusk", "🌇 sunset", "🌉 bridge night", "♨️ hotspring",
            "🎠 carousel", "🎡 ferris", "🎢 coaster", "💈 barber",
            "🎪 circus tent");
    }

    private static void Things(List<EmojiMark> shelf)
    {
        Add(shelf, EmojiGroup.Things, false,
            "⌚ watch", "📱 phone", "📲 phone arrow", "💻 laptop",
            "⌨️ keyboard", "🖥️ desktop", "🖨️ printer", "🖱️ mouse",
            "🖲️ trackball", "💽 minidisc", "💾 floppy", "💿 cd",
            "📀 dvd", "🎥 movie camera", "🎞️ film", "📽️ projector",
            "🎬 clapper", "📺 tv", "📷 camera", "📸 camera flash",
            "📹 video camera", "📼 vhs", "🔍 search left", "🔎 search right",
            "🕯️ candle", "💡 bulb idea", "🔦 flashlight", "🏮 paper lantern",
            "📔 notebook", "📕 closed book", "📖 open book", "📗 green book",
            "📘 blue book", "📙 orange book", "📚 books", "📓 notebook",
            "📃 page curl", "📜 scroll", "📄 page", "📰 newspaper",
            "📑 bookmark tabs", "🔖 bookmark", "🏷️ tag", "💰 money bag",
            "🪙 coin", "💴 yen", "💵 dollar", "💶 euro",
            "💷 pound", "💸 money wings", "💳 card", "🧾 receipt",
            "✉️ envelope", "📧 email", "📨 incoming", "📩 envelope arrow",
            "📤 outbox", "📥 inbox", "📦 package", "📫 mailbox",
            "📝 memo note", "💼 briefcase", "📁 folder", "📂 open folder",
            "📅 calendar", "📆 tear calendar", "🗒️ notepad", "🗓️ spiral calendar",
            "📇 card index", "📈 chart up", "📉 chart down", "📊 bar chart",
            "📋 clipboard", "📌 pin", "📍 round pin", "📎 paperclip",
            "🖇️ clips", "📏 ruler", "📐 triangle", "✂️ scissors",
            "🗃️ file box", "🗄️ cabinet", "🗑️ trash", "🔒 lock",
            "🔓 unlock", "🔑 key", "🗝️ old key", "🔨 hammer",
            "🪓 axe", "⛏️ pick", "🛠️ tools", "🗡️ dagger",
            "⚔️ swords", "🔫 water gun", "🪃 boomerang", "🏹 bow",
            "🛡️ shield", "🪚 saw", "🔧 wrench", "🪛 screwdriver",
            "🔩 nut bolt", "⚙️ gear", "🗜️ clamp", "⚖️ scale",
            "🦯 cane", "🔗 link", "⛓️ chains", "🧰 toolbox",
            "🧲 magnet", "🪜 ladder", "⚗️ alembic", "🧪 test tube",
            "🧫 petri", "🧬 dna", "🔬 microscope", "🔭 telescope",
            "📡 antenna", "💉 syringe", "🩸 blood", "💊 pill",
            "🩹 bandage", "🩺 stethoscope", "🚪 door", "🪞 mirror",
            "🪟 window", "🛏️ bed", "🛋️ couch", "🪑 chair",
            "🚽 toilet", "🪠 plunger", "🚿 shower", "🛁 bathtub",
            "🧴 lotion", "🧷 safety pin", "🧹 broom", "🧺 basket",
            "🧻 roll", "🪣 bucket", "🧼 soap", "🪥 brush",
            "🧽 sponge", "🧯 extinguisher", "🛒 cart", "🚬 cigarette",
            "⚰️ coffin", "🪦 headstone", "⚱️ urn", "🗿 moai");
    }

    private static void Signs(List<EmojiMark> shelf)
    {
        Add(shelf, EmojiGroup.Signs, false,
            "❤️ red heart", "🧡 orange heart", "💛 yellow heart", "💚 green heart",
            "💙 blue heart", "💜 purple heart", "🖤 black heart", "🤍 white heart",
            "💔 broken", "❣️ heart exclaim", "💕 two hearts", "💞 revolving",
            "💓 beating", "💗 growing", "💖 sparkle", "💘 arrow heart",
            "💝 gift heart", "💟 decoration", "☮️ peace", "✝️ cross",
            "☪️ star crescent", "🕉️ om", "☸️ dharma", "✡️ star david",
            "🔯 six star", "🕎 menorah", "☯️ yin yang", "☦️ orthodox",
            "🛐 worship", "⛎ ophiuchus", "♈ aries", "♉ taurus",
            "♊ gemini", "♋ cancer", "♌ leo", "♍ virgo",
            "♎ libra", "♏ scorpio", "♐ sagittarius", "♑ capricorn",
            "♒ aquarius", "♓ pisces", "🆔 id", "⚛️ atom",
            "🉑 accept", "☢️ radioactive", "☣️ biohazard", "📴 phone off",
            "📳 vibrate", "🈶 not free", "🈚 free", "🈸 application",
            "🈺 open", "🈷️ monthly", "✴️ eight star", "🆚 vs",
            "💮 white flower", "🉐 bargain", "㊙️ secret", "㊗️ congratulations",
            "🈴 passing", "🈵 full", "🈹 discount", "🈲 prohibited",
            "🅰️ a button", "🅱️ b button", "🆎 ab", "🆑 cl",
            "🅾️ o button", "🆘 sos", "❌ x", "⭕ circle",
            "🛑 stop", "⛔ no entry", "📛 name badge", "🚫 prohibited",
            "💯 hundred", "💢 anger", "♨️ hot", "🚷 no pedestrians",
            "🚯 no litter", "🚳 no bicycles", "🚱 no water", "🔞 underage",
            "📵 no phones", "🚭 no smoking", "❗ exclaim", "❓ question",
            "❕ white exclaim", "❔ white question", "‼️ bangbang", "⁉️ interrobang",
            "〰️ wave dash", "💱 currency", "💲 dollar sign", "⚕️ medical",
            "♻️ recycle", "⚜️ fleur", "🔱 trident", "📛 badge",
            "🔰 beginner", "⭕ hollow", "✅ check", "☑️ ballot",
            "✔️ heavy check", "❌ cross", "❎ cross box", "➰ curly",
            "➿ double curly", "〽️ part alternation", "✳️ eight spoke", "✴️ eight star",
            "❇️ sparkle", "©️ copyright", "®️ registered", "™️ tm",
            "#️⃣ hash key", "*️⃣ star key", "0️⃣ zero", "1️⃣ one",
            "2️⃣ two", "3️⃣ three", "4️⃣ four", "5️⃣ five",
            "6️⃣ six", "7️⃣ seven", "8️⃣ eight", "9️⃣ nine",
            "🔟 ten", "🔠 caps", "🔡 small", "🔢 numbers",
            "🔣 symbols", "🔤 abc", "🅰️ a", "🆎 ab",
            "🅱️ b", "🆑 cl", "🆒 cool", "🆓 free",
            "ℹ️ info", "🆔 id", "Ⓜ️ m", "🆕 new",
            "🆖 ng", "🅾️ o", "🆗 ok", "🅿️ p",
            "🆘 sos", "🆙 up", "🆚 vs", "🈁 here",
            "🈂️ service", "🈷️ month", "🈶 not free", "🈯 reserved",
            "🉐 bargain", "🈹 discount", "🈚 free", "🈲 prohibited",
            "🉑 accept", "🈸 apply", "🈴 pass", "🈳 vacant",
            "㊗️ congrats", "㊙️ secret", "🈺 open", "🈵 full",
            "🔴 red circle", "🟠 orange circle", "🟡 yellow circle", "🟢 green circle",
            "🔵 blue circle", "🟣 purple circle", "⚫ black circle", "⚪ white circle",
            "🟥 red square", "🟧 orange square", "🟨 yellow square", "🟩 green square",
            "🟦 blue square", "🟪 purple square", "⬛ black square", "⬜ white square",
            "◼️ black med", "◻️ white med", "◾ black small", "◽ white small",
            "▪️ black tiny", "▫️ white tiny", "🔶 orange diamond", "🔷 blue diamond",
            "🔸 small orange", "🔹 small blue", "🔺 red up", "🔻 red down",
            "💠 diamond dot", "🔘 radio", "🔳 white square button", "🔲 black square button");
    }

    private static void Flags(List<EmojiMark> shelf)
    {
        Add(shelf, EmojiGroup.Flags, false,
            "🏁 checkered", "🚩 triangular flag", "🎌 crossed flags", "🏴 black flag",
            "🏳️ white flag", "🏳️‍🌈 pride rainbow", "🏳️‍⚧️ trans", "🏴‍☠️ pirate",
            "🇺🇸 us usa america", "🇬🇧 gb uk britain", "🇨🇦 canada", "🇦🇺 australia",
            "🇳🇿 new zealand", "🇯🇵 japan", "🇰🇷 korea", "🇨🇳 china",
            "🇹🇼 taiwan", "🇭🇰 hong kong", "🇸🇬 singapore", "🇮🇳 india",
            "🇩🇪 germany", "🇫🇷 france", "🇮🇹 italy", "🇪🇸 spain",
            "🇵🇹 portugal", "🇳🇱 netherlands", "🇧🇪 belgium", "🇨🇭 switzerland",
            "🇦🇹 austria", "🇸🇪 sweden", "🇳🇴 norway", "🇩🇰 denmark",
            "🇫🇮 finland", "🇮🇪 ireland", "🇵🇱 poland", "🇨🇿 czech",
            "🇬🇷 greece", "🇹🇷 turkey", "🇷🇺 russia", "🇺🇦 ukraine",
            "🇧🇷 brazil", "🇲🇽 mexico", "🇦🇷 argentina", "🇨🇱 chile",
            "🇨🇴 colombia", "🇵🇪 peru", "🇿🇦 south africa", "🇪🇬 egypt",
            "🇳🇬 nigeria", "🇰🇪 kenya", "🇲🇦 morocco", "🇸🇦 saudi",
            "🇦🇪 uae", "🇮🇱 israel", "🇵🇭 philippines", "🇹🇭 thailand",
            "🇻🇳 vietnam", "🇮🇩 indonesia", "🇲🇾 malaysia");
    }

    private static void Add(List<EmojiMark> shelf, EmojiGroup group, bool tones, params string[] rows)
    {
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var space = row.IndexOf(' ');
            if (space <= 0)
            {
                continue;
            }

            var glyph = row[..space];
            var keys = row[(space + 1)..].ToLowerInvariant();
            var name = keys;
            shelf.Add(new EmojiMark(EmojiKind.Unicode, glyph, name, keys, group, tones));
        }
    }
}
