using System.Globalization;

namespace Linkpearl.Preferences;

public readonly record struct PhoneLanguage(string Id, string Culture, string Native, string English);

public static partial class PhoneLanguages
{
    public const string DefaultId = "en-US";

    public static readonly PhoneLanguage[] All =
    {
        new("en-US", "en-US", "English (United States)", "English"),
        new("en-GB", "en-GB", "English (United Kingdom)", "English"),
        new("ja-JP", "ja-JP", "日本語", "Japanese"),
        new("de-DE", "de-DE", "Deutsch", "German"),
        new("fr-FR", "fr-FR", "Français", "French"),
        new("es-ES", "es-ES", "Español (España)", "Spanish"),
        new("es-MX", "es-MX", "Español (México)", "Spanish"),
        new("pt-BR", "pt-BR", "Português (Brasil)", "Portuguese"),
        new("it-IT", "it-IT", "Italiano", "Italian"),
        new("zh-CN", "zh-CN", "简体中文", "Chinese Simplified"),
        new("zh-TW", "zh-TW", "繁體中文", "Chinese Traditional"),
        new("ko-KR", "ko-KR", "한국어", "Korean"),
        new("ru-RU", "ru-RU", "Русский", "Russian"),
        new("nl-NL", "nl-NL", "Nederlands", "Dutch"),
        new("pl-PL", "pl-PL", "Polski", "Polish"),
        new("tr-TR", "tr-TR", "Türkçe", "Turkish"),
        new("th-TH", "th-TH", "ไทย", "Thai"),
        new("vi-VN", "vi-VN", "Tiếng Việt", "Vietnamese"),
        new("id-ID", "id-ID", "Bahasa Indonesia", "Indonesian"),
        new("sv-SE", "sv-SE", "Svenska", "Swedish"),
        new("da-DK", "da-DK", "Dansk", "Danish"),
        new("nb-NO", "nb-NO", "Norsk bokmål", "Norwegian"),
        new("fi-FI", "fi-FI", "Suomi", "Finnish"),
        new("cs-CZ", "cs-CZ", "Čeština", "Czech"),
        new("hu-HU", "hu-HU", "Magyar", "Hungarian"),
        new("uk-UA", "uk-UA", "Українська", "Ukrainian"),
        new("el-GR", "el-GR", "Ελληνικά", "Greek"),
        new("ro-RO", "ro-RO", "Română", "Romanian"),
        new("hi-IN", "hi-IN", "हिन्दी", "Hindi"),
    };

    public static readonly string[] MenuLabels = BuildMenuLabels();

    public static string CurrentId { get; private set; } = DefaultId;

    public static string Sanitize(string? id)
    {
        var raw = (id ?? string.Empty).Trim();
        return IndexOf(raw) >= 0 ? All[IndexOf(raw)].Id : DefaultId;
    }

    public static int IndexOf(string id)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    public static PhoneLanguage Current => All[Math.Max(0, IndexOf(CurrentId))];

    public static void Apply(string? id)
    {
        var next = Sanitize(id);
        if (string.Equals(CurrentId, next, StringComparison.Ordinal))
        {
            return;
        }

        CurrentId = next;
        try
        {
            var culture = CultureInfo.GetCultureInfo(All[IndexOf(next)].Culture);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            var fallback = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentCulture = fallback;
            CultureInfo.CurrentUICulture = fallback;
        }
    }

    public static string T(string key)
    {
        if (TryRow(Pack, key, out var text) || TryRow(Ui, key, out text))
        {
            return text;
        }

        return key;
    }

    private static bool TryRow(Dictionary<string, Dictionary<string, string>> pack, string key, out string text)
    {
        text = string.Empty;
        if (!pack.TryGetValue(key, out var rows))
        {
            return false;
        }

        if (rows.TryGetValue(Family(CurrentId), out var localized) && localized.Length > 0)
        {
            text = localized;
            return true;
        }

        if (rows.TryGetValue("en", out var english) && english.Length > 0)
        {
            text = english;
            return true;
        }

        return false;
    }

    public static string App(string id, string fallback = "")
    {
        var text = T("app." + id);
        return text.StartsWith("app.", StringComparison.Ordinal) ? (fallback.Length > 0 ? fallback : id) : text;
    }

    private static string Family(string id) =>
        id.StartsWith("en-", StringComparison.OrdinalIgnoreCase) ? "en" :
        id.StartsWith("es-", StringComparison.OrdinalIgnoreCase) ? "es" :
        id.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase) ? "zh-CN" :
        id.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ? "zh-TW" :
        id.Length >= 2 ? id[..2].ToLowerInvariant() : "en";

    private static string[] BuildMenuLabels()
    {
        var labels = new string[All.Length];
        for (var index = 0; index < All.Length; index++)
        {
            var language = All[index];
            labels[index] = string.Equals(language.Native, language.English, StringComparison.Ordinal)
                ? language.Native
                : language.Native + "  (" + language.English + ")";
        }

        return labels;
    }

    private static Dictionary<string, string> Lang(params (string Code, string Text)[] rows)
    {
        var map = new Dictionary<string, string>(rows.Length, StringComparer.Ordinal);
        for (var index = 0; index < rows.Length; index++)
        {
            map[rows[index].Code] = rows[index].Text;
        }

        return map;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Pack =
        new(StringComparer.Ordinal)
        {
            ["nav.home"] = Lang(("en", "Home"), ("ja", "ホーム"), ("de", "Start"), ("fr", "Accueil"),
                ("es", "Inicio"), ("pt", "Início"), ("it", "Home"), ("zh-CN", "主屏幕"), ("zh-TW", "主畫面"),
                ("ko", "홈"), ("ru", "Домой")),
            ["nav.explore"] = Lang(("en", "Explore"), ("ja", "探す"), ("de", "Entdecken"), ("fr", "Explorer"),
                ("es", "Explorar"), ("pt", "Explorar"), ("it", "Esplora"), ("zh-CN", "探索"), ("zh-TW", "探索"),
                ("ko", "탐색"), ("ru", "Обзор")),
            ["nav.social"] = Lang(("en", "Social"), ("ja", "ソーシャル"), ("de", "Sozial"), ("fr", "Social"),
                ("es", "Social"), ("pt", "Social"), ("it", "Social"), ("zh-CN", "社交"), ("zh-TW", "社交"),
                ("ko", "소셜"), ("ru", "Соцсети")),
            ["nav.you"] = Lang(("en", "You"), ("ja", "あなた"), ("de", "Du"), ("fr", "Vous"),
                ("es", "Tú"), ("pt", "Você"), ("it", "Tu"), ("zh-CN", "我的"), ("zh-TW", "我的"),
                ("ko", "나"), ("ru", "Вы")),
            ["nav.settings"] = Lang(("en", "Settings"), ("ja", "設定"), ("de", "Einstellungen"), ("fr", "Réglages"),
                ("es", "Ajustes"), ("pt", "Ajustes"), ("it", "Impostazioni"), ("zh-CN", "设置"), ("zh-TW", "設定"),
                ("ko", "설정"), ("ru", "Настройки")),
            ["nav.messages"] = Lang(("en", "Messages"), ("ja", "メッセージ"), ("de", "Nachrichten"), ("fr", "Messages"),
                ("es", "Mensajes"), ("pt", "Mensagens"), ("it", "Messaggi"), ("zh-CN", "信息"), ("zh-TW", "訊息"),
                ("ko", "메시지"), ("ru", "Сообщения")),
            ["set.credits"] = Lang(("en", "Credits"), ("ja", "クレジット"), ("de", "Mitwirkende"), ("fr", "Crédits"),
                ("es", "Créditos"), ("pt", "Créditos"), ("it", "Crediti"), ("zh-CN", "制作人员"), ("zh-TW", "製作人員"),
                ("ko", "크레딧"), ("ru", "Авторы")),
            ["set.language"] = Lang(("en", "Language & Time"), ("ja", "言語と時刻"), ("de", "Sprache & Zeit"),
                ("fr", "Langue et heure"), ("es", "Idioma y hora"), ("pt", "Idioma e hora"),
                ("it", "Lingua e ora"), ("zh-CN", "语言与时间"), ("zh-TW", "語言與時間"),
                ("ko", "언어 및 시간"), ("ru", "Язык и время")),
            ["set.language.blurb"] = Lang(
                ("en", "Language and date and time"),
                ("ja", "言語、日付、時刻"),
                ("de", "Sprache, Datum und Uhrzeit"),
                ("fr", "Langue, date et heure"),
                ("es", "Idioma, fecha y hora"),
                ("pt", "Idioma, data e hora"),
                ("it", "Lingua, data e ora"),
                ("zh-CN", "语言、日期和时间"),
                ("zh-TW", "語言、日期與時間"),
                ("ko", "언어, 날짜, 시간"),
                ("ru", "Язык, дата и время")),
            ["set.app.language"] = Lang(("en", "Phone language"), ("ja", "端末の言語"), ("de", "Telefonsprache"),
                ("fr", "Langue du téléphone"), ("es", "Idioma del teléfono"), ("pt", "Idioma do telefone"),
                ("it", "Lingua del telefono"), ("zh-CN", "手机语言"), ("zh-TW", "手機語言"),
                ("ko", "휴대폰 언어"), ("ru", "Язык телефона")),
            ["set.app.language.hint"] = Lang(
                ("en", "Menus, dates, and app names on this phone."),
                ("ja", "この端末のメニュー、日付、アプリ名。"),
                ("de", "Menüs, Daten und App-Namen auf diesem Telefon."),
                ("fr", "Menus, dates et noms d’applis sur ce téléphone."),
                ("es", "Menús, fechas y nombres de apps en este teléfono."),
                ("pt", "Menus, datas e nomes de apps neste telefone."),
                ("it", "Menu, date e nomi delle app su questo telefono."),
                ("zh-CN", "此手机上的菜单、日期和应用名称。"),
                ("zh-TW", "此手機上的選單、日期與應用程式名稱。"),
                ("ko", "이 휴대폰의 메뉴, 날짜, 앱 이름."),
                ("ru", "Меню, даты и названия приложений на этом телефоне.")),
            ["set.hour12"] = Lang(("en", "Use 12-hour format"), ("ja", "12時間表示"), ("de", "12-Stunden-Format"),
                ("fr", "Format 12 heures"), ("es", "Formato de 12 horas"), ("pt", "Formato de 12 horas"),
                ("it", "Formato 12 ore"), ("zh-CN", "12 小时制"), ("zh-TW", "12 小時制"),
                ("ko", "12시간제"), ("ru", "12-часовой формат")),
            ["set.hour24"] = Lang(("en", "Use 24-hour format"), ("ja", "24時間表示"), ("de", "24-Stunden-Format"),
                ("fr", "Format 24 heures"), ("es", "Formato de 24 horas"), ("pt", "Formato de 24 horas"),
                ("it", "Formato 24 ore"), ("zh-CN", "24 小时制"), ("zh-TW", "24 小時制"),
                ("ko", "24시간제"), ("ru", "24-часовой формат")),
            ["set.clock.local"] = Lang(("en", "Local time"), ("ja", "現地時間"), ("de", "Ortszeit"),
                ("fr", "Heure locale"), ("es", "Hora local"), ("pt", "Hora local"),
                ("it", "Ora locale"), ("zh-CN", "本地时间"), ("zh-TW", "當地時間"),
                ("ko", "현지 시간"), ("ru", "Местное время")),
            ["set.clock.eorzea"] = Lang(("en", "Eorzea time"), ("ja", "エオルゼア時間"), ("de", "Eorzea-Zeit"),
                ("fr", "Heure d’Éorzéa"), ("es", "Hora de Eorzea"), ("pt", "Hora de Eorzea"),
                ("it", "Ora di Eorzea"), ("zh-CN", "艾欧泽亚时间"), ("zh-TW", "艾歐澤亞時間"),
                ("ko", "에오르제아 시간"), ("ru", "Время Эорзеи")),
            ["set.clock.both"] = Lang(("en", "Both clocks"), ("ja", "両方の時計"), ("de", "Beide Uhren"),
                ("fr", "Les deux horloges"), ("es", "Ambos relojes"), ("pt", "Os dois relógios"),
                ("it", "Entrambi gli orologi"), ("zh-CN", "双时钟"), ("zh-TW", "雙時鐘"),
                ("ko", "둘 다 표시"), ("ru", "Оба часа")),
            ["set.general"] = Lang(("en", "General"), ("ja", "一般"), ("de", "Allgemein"), ("fr", "Général"),
                ("es", "General"), ("pt", "Geral"), ("it", "Generale"), ("zh-CN", "通用"), ("zh-TW", "一般"),
                ("ko", "일반"), ("ru", "Основные")),
            ["set.appearance"] = Lang(("en", "Appearance"), ("ja", "外観"), ("de", "Darstellung"), ("fr", "Apparence"),
                ("es", "Apariencia"), ("pt", "Aparência"), ("it", "Aspetto"), ("zh-CN", "外观"), ("zh-TW", "外觀"),
                ("ko", "모양"), ("ru", "Оформление")),
            ["set.sounds"] = Lang(("en", "Sounds"), ("ja", "サウンド"), ("de", "Töne"), ("fr", "Sons"),
                ("es", "Sonidos"), ("pt", "Sons"), ("it", "Suoni"), ("zh-CN", "声音"), ("zh-TW", "聲音"),
                ("ko", "사운드"), ("ru", "Звуки")),
            ["set.notifications"] = Lang(("en", "Notifications"), ("ja", "通知"), ("de", "Mitteilungen"),
                ("fr", "Notifications"), ("es", "Notificaciones"), ("pt", "Notificações"),
                ("it", "Notifiche"), ("zh-CN", "通知"), ("zh-TW", "通知"),
                ("ko", "알림"), ("ru", "Уведомления")),
            ["set.feed"] = Lang(("en", "Feed"), ("ja", "フィード"), ("de", "Feed"), ("fr", "Fil"),
                ("es", "Feed"), ("pt", "Feed"), ("it", "Feed"), ("zh-CN", "动态"), ("zh-TW", "動態"),
                ("ko", "피드"), ("ru", "Лента")),
            ["set.calls"] = Lang(("en", "Phone calls"), ("ja", "通話"), ("de", "Anrufe"), ("fr", "Appels"),
                ("es", "Llamadas"), ("pt", "Chamadas"), ("it", "Chiamate"), ("zh-CN", "电话"), ("zh-TW", "電話"),
                ("ko", "전화"), ("ru", "Звонки")),
            ["set.tos"] = Lang(("en", "Terms of service"), ("ja", "利用規約"), ("de", "Nutzungsbedingungen"),
                ("fr", "Conditions d’utilisation"), ("es", "Términos del servicio"), ("pt", "Termos de serviço"),
                ("it", "Termini di servizio"), ("zh-CN", "服务条款"), ("zh-TW", "服務條款"),
                ("ko", "이용 약관"), ("ru", "Условия использования")),
            ["set.patreon"] = Lang(("en", "Support us on Patreon"), ("ja", "Patreonで支援"),
                ("de", "Unterstütze uns auf Patreon"), ("fr", "Soutenez-nous sur Patreon"),
                ("es", "Apóyanos en Patreon"), ("pt", "Apoie-nos no Patreon"),
                ("it", "Supportaci su Patreon"), ("zh-CN", "在 Patreon 上支持我们"), ("zh-TW", "在 Patreon 上支持我們"),
                ("ko", "Patreon에서 후원하기"), ("ru", "Поддержать нас на Patreon")),
            ["set.discord"] = Lang(("en", "Join our Discord"), ("ja", "Discordに参加"), ("de", "Discord beitreten"),
                ("fr", "Rejoindre Discord"), ("es", "Únete a Discord"), ("pt", "Entre no Discord"),
                ("it", "Unisciti a Discord"), ("zh-CN", "加入 Discord"), ("zh-TW", "加入 Discord"),
                ("ko", "Discord 참여"), ("ru", "Наш Discord")),
            ["set.version"] = Lang(("en", "Version"), ("ja", "バージョン"), ("de", "Version"), ("fr", "Version"),
                ("es", "Versión"), ("pt", "Versão"), ("it", "Versione"), ("zh-CN", "版本"), ("zh-TW", "版本"),
                ("ko", "버전"), ("ru", "Версия")),
            ["app.pearlchat"] = Lang(("en", "PearlChat"), ("ja", "パールチャット"), ("de", "PearlChat"),
                ("fr", "PearlChat"), ("es", "PearlChat"), ("pt", "PearlChat"), ("it", "PearlChat"),
                ("zh-CN", "PearlChat"), ("zh-TW", "PearlChat"), ("ko", "PearlChat"), ("ru", "PearlChat")),
            ["app.phone"] = Lang(("en", "Phone"), ("ja", "電話"), ("de", "Telefon"), ("fr", "Téléphone"),
                ("es", "Teléfono"), ("pt", "Telefone"), ("it", "Telefono"), ("zh-CN", "电话"), ("zh-TW", "電話"),
                ("ko", "전화"), ("ru", "Телефон")),
            ["app.music"] = Lang(("en", "Music"), ("ja", "ミュージック"), ("de", "Musik"), ("fr", "Musique"),
                ("es", "Música"), ("pt", "Música"), ("it", "Musica"), ("zh-CN", "音乐"), ("zh-TW", "音樂"),
                ("ko", "음악"), ("ru", "Музыка")),
            ["app.vybe"] = Lang(("en", "VYBE"), ("ja", "VYBE"), ("de", "VYBE"), ("fr", "VYBE"),
                ("es", "VYBE"), ("pt", "VYBE"), ("it", "VYBE"), ("zh-CN", "VYBE"), ("zh-TW", "VYBE"),
                ("ko", "VYBE"), ("ru", "VYBE")),
            ["app.afterdark"] = Lang(("en", "VYBE"), ("ja", "VYBE"), ("de", "VYBE"), ("fr", "VYBE"),
                ("es", "VYBE"), ("pt", "VYBE"), ("it", "VYBE"), ("zh-CN", "VYBE"), ("zh-TW", "VYBE"),
                ("ko", "VYBE"), ("ru", "VYBE")),
            ["app.weather"] = Lang(("en", "Weather"), ("ja", "天気"), ("de", "Wetter"), ("fr", "Météo"),
                ("es", "Tiempo"), ("pt", "Clima"), ("it", "Meteo"), ("zh-CN", "天气"), ("zh-TW", "天氣"),
                ("ko", "날씨"), ("ru", "Погода")),
            ["app.calendar"] = Lang(("en", "Calendar"), ("ja", "カレンダー"), ("de", "Kalender"), ("fr", "Calendrier"),
                ("es", "Calendario"), ("pt", "Calendário"), ("it", "Calendario"), ("zh-CN", "日历"), ("zh-TW", "日曆"),
                ("ko", "캘린더"), ("ru", "Календарь")),
            ["app.wallet"] = Lang(("en", "Pearls"), ("ja", "パール"), ("de", "Perlen"), ("fr", "Perles"),
                ("es", "Perlas"), ("pt", "Pérolas"), ("it", "Perle"), ("zh-CN", "珍珠"), ("zh-TW", "珍珠"),
                ("ko", "진주"), ("ru", "Жемчуг")),
            ["app.camera"] = Lang(("en", "Camera"), ("ja", "カメラ"), ("de", "Kamera"), ("fr", "Appareil photo"),
                ("es", "Cámara"), ("pt", "Câmera"), ("it", "Fotocamera"), ("zh-CN", "相机"), ("zh-TW", "相機"),
                ("ko", "카메라"), ("ru", "Камера")),
            ["app.friends"] = Lang(("en", "Friends"), ("ja", "フレンド"), ("de", "Freunde"), ("fr", "Amis"),
                ("es", "Amigos"), ("pt", "Amigos"), ("it", "Amici"), ("zh-CN", "好友"), ("zh-TW", "好友"),
                ("ko", "친구"), ("ru", "Друзья")),
            ["app.market"] = Lang(("en", "Market"), ("ja", "マーケット"), ("de", "Markt"), ("fr", "Marché"),
                ("es", "Mercado"), ("pt", "Mercado"), ("it", "Mercato"), ("zh-CN", "市场"), ("zh-TW", "市場"),
                ("ko", "마켓"), ("ru", "Рынок")),
            ["app.venues"] = Lang(("en", "Venues"), ("ja", "会場"), ("de", "Venues"), ("fr", "Lieux"),
                ("es", "Locales"), ("pt", "Locais"), ("it", "Locali"), ("zh-CN", "会场"), ("zh-TW", "會場"),
                ("ko", "베뉴"), ("ru", "Заведения")),
            ["app.appstore"] = Lang(("en", "App Store"), ("ja", "App Store"), ("de", "App Store"),
                ("fr", "App Store"), ("es", "App Store"), ("pt", "App Store"), ("it", "App Store"),
                ("zh-CN", "应用商店"), ("zh-TW", "App Store"), ("ko", "App Store"), ("ru", "Магазин")),
            ["app.events"] = Lang(("en", "Events"), ("ja", "イベント"), ("de", "Ereignisse"), ("fr", "Événements"),
                ("es", "Eventos"), ("pt", "Eventos"), ("it", "Eventi"), ("zh-CN", "活动"), ("zh-TW", "活動"),
                ("ko", "이벤트"), ("ru", "События")),
            ["app.eorzea"] = Lang(("en", "Eorzea"), ("ja", "エオルゼア"), ("de", "Eorzea"), ("fr", "Éorzéa"),
                ("es", "Eorzea"), ("pt", "Eorzea"), ("it", "Eorzea"), ("zh-CN", "艾欧泽亚"), ("zh-TW", "艾歐澤亞"),
                ("ko", "에오르제아"), ("ru", "Эорзея")),
            ["app.settings"] = Lang(("en", "Settings"), ("ja", "設定"), ("de", "Einstellungen"), ("fr", "Réglages"),
                ("es", "Ajustes"), ("pt", "Ajustes"), ("it", "Impostazioni"), ("zh-CN", "设置"), ("zh-TW", "設定"),
                ("ko", "설정"), ("ru", "Настройки")),
            ["app.feedback"] = Lang(("en", "Feedback"), ("ja", "フィードバック"), ("de", "Feedback"), ("fr", "Avis"),
                ("es", "Comentarios"), ("pt", "Feedback"), ("it", "Feedback"), ("zh-CN", "反馈"), ("zh-TW", "意見回饋"),
                ("ko", "피드백"), ("ru", "Отзыв")),
            ["app.notes"] = Lang(("en", "Notes"), ("ja", "メモ"), ("de", "Notizen"), ("fr", "Notes"),
                ("es", "Notas"), ("pt", "Notas"), ("it", "Note"), ("zh-CN", "备忘录"), ("zh-TW", "備忘錄"),
                ("ko", "메모"), ("ru", "Заметки")),
            ["app.alarms"] = Lang(("en", "Alarms"), ("ja", "アラーム"), ("de", "Wecker"), ("fr", "Alarmes"),
                ("es", "Alarmas"), ("pt", "Alarmes"), ("it", "Sveglie"), ("zh-CN", "闹钟"), ("zh-TW", "鬧鐘"),
                ("ko", "알람"), ("ru", "Будильник")),
            ["app.clock"] = Lang(("en", "Clock"), ("ja", "時計"), ("de", "Uhr"), ("fr", "Horloge"),
                ("es", "Reloj"), ("pt", "Relógio"), ("it", "Orologio"), ("zh-CN", "时钟"), ("zh-TW", "時鐘"),
                ("ko", "시계"), ("ru", "Часы")),
            ["app.calculator"] = Lang(("en", "Calculator"), ("ja", "電卓"), ("de", "Rechner"), ("fr", "Calculatrice"),
                ("es", "Calculadora"), ("pt", "Calculadora"), ("it", "Calcolatrice"), ("zh-CN", "计算器"),
                ("zh-TW", "計算機"), ("ko", "계산기"), ("ru", "Калькулятор")),
            ["app.timer"] = Lang(("en", "Timer"), ("ja", "タイマー"), ("de", "Timer"), ("fr", "Minuteur"),
                ("es", "Temporizador"), ("pt", "Temporizador"), ("it", "Timer"), ("zh-CN", "计时器"),
                ("zh-TW", "計時器"), ("ko", "타이머"), ("ru", "Таймер")),
            ["app.stopwatch"] = Lang(("en", "Stopwatch"), ("ja", "ストップウォッチ"), ("de", "Stoppuhr"),
                ("fr", "Chronomètre"), ("es", "Cronómetro"), ("pt", "Cronômetro"), ("it", "Cronometro"),
                ("zh-CN", "秒表"), ("zh-TW", "碼錶"), ("ko", "스톱워치"), ("ru", "Секундомер")),
        };
}
