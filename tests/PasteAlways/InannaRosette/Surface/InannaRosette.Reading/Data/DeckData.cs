using InannaRosette.Reading.Models;

namespace InannaRosette.Reading.Data;

/// <summary>
/// The forty cards of the Rosette of Inanna: twenty-four Great Goddesses, the Eight Gates
/// of the Descent, and the eight Sacred Emblems. Content only — no UI, no I/O.
/// </summary>
public static class DeckData
{
    /// <summary>Terse list builder so the card literals below stay readable.</summary>
    private static string[] L(params string[] items) => items;

    // Built on first access: the suit arrays below are static field initializers, which run
    // in textual order, so the list cannot be assembled in a field initializer up here.
    private static IReadOnlyList<Card>? _cards;

    /// <summary>All forty cards, ordered by Id (1..40): Goddess 1..24, Gate 1..8, Emblem 1..8.</summary>
    public static IReadOnlyList<Card> Cards => _cards ??= Build();

    /// <summary>Look up a card by its Id, or null when no such card exists.</summary>
    public static Card? ById(int id) => id >= 1 && id <= Cards.Count ? Cards[id - 1] : null;

    /// <summary>All cards of one suit, in Number order.</summary>
    public static IReadOnlyList<Card> BySuit(CardSuit suit) => Cards.Where(c => c.Suit == suit).ToList();

    private static IReadOnlyList<Card> Build()
    {
        var all = new List<Card>(40);
        all.AddRange(GoddessesA);
        all.AddRange(GoddessesB);
        all.AddRange(GoddessesC);
        all.AddRange(GoddessesD);
        all.AddRange(Gates);
        all.AddRange(Emblems);
        return all;
    }

    #region The Great Goddesses I..VI

    private static readonly Card[] GoddessesA =
    [
        new Card(
            Id: 1, Suit: CardSuit.Goddess, Number: 1,
            Name: "Inanna",
            Epithet: "Queen of Heaven and Earth",
            Transliteration: "dInanna / Ištar",
            Emblem: Emblem.AltarRosette,
            AccentColor: "#E3B23C", SecondaryColor: "#6D33B8",
            Domains: L("desire", "sovereignty", "love", "war", "the planet Venus"),
            Lore: """Inanna of Uruk, whose house was the Eanna, was the greatest goddess of Sumer, and as Ištar the greatest of Akkad, Babylon and Assyria. She is the planet Venus, morning star and evening star, and so belongs to both edges of the day. Her hymns refuse to make her consistent: she is love and she is battle, the tavern and the throne room. Enheduanna, daughter of Sargon and high priestess at Ur, wrote the Exaltation of Inanna and named her mistress of the divine powers that An himself had yielded. In the poem Inanna and Enki she carries the me home to Uruk by boat. In the Descent she goes down, and hangs on a hook, and rises.""",
            UprightKeywords: L("open desire", "sovereignty", "audacity", "eros", "the rising star"),
            UprightMeaning: """You are being asked to want something out loud. Inanna does not bargain with her own desire; she dresses in the seven powers and walks toward what she intends, and the doors open because she expects them to open. Say the plain thing. Ask for the raise, the answer, the person, the room. What you have been calling arrogance in yourself is very probably accuracy, and pretending otherwise has already cost you more than you admit. This card carries heat as well: in her, love and quarrel share one doorway, and you may meet both. Stand at the front of your own life. The star that sets is the same star that rises.""",
            ReversedKeywords: L("performance", "craving approval", "burnout", "scattered heat"),
            ReversedMeaning: """Desire has turned into performance. You are asking to be noticed rather than asking for the thing itself, or you have fixed your heart on a door that only opens from inside somebody else's house. Reversed, she is also the grey stretch after brilliance, when the applause stops and the work remains and you cannot feel your own pulse. Stop chasing the sensation of being chosen. Withdraw for a season, take off the ornaments you put on for other people, and find out what you would still want if no one were watching.""",
            Invocation: """Lady of the two horizons, who rises before the sun and still burns after it — set my name in the mouth of the morning."""),

        new Card(
            Id: 2, Suit: CardSuit.Goddess, Number: 2,
            Name: "Ereshkigal",
            Epithet: "Queen of the Great Below",
            Transliteration: "dEreš-ki-gal",
            Emblem: Emblem.Throne,
            AccentColor: "#7B1E1E", SecondaryColor: "#0B0B0F",
            Domains: L("the underworld", "grief", "irreversible law", "the unseen"),
            Lore: """Ereshkigal, whose name means Lady of the Great Earth, rules Irkalla, the land of no return, where the dead eat dust and the gates are bolted. She is the elder sister of Inanna, and in the Descent it is she who orders Neti the gatekeeper to strip the visitor of one ornament at each of seven gates, then fastens on her the eye of death. The Akkadian tale of Nergal and Ereshkigal tells how she came to share her throne. She is not cruelty; she is what cannot be argued with. When her own grief is echoed back to her by two mourners sent from Eridu, she gives up the corpse.""",
            UprightKeywords: L("descent", "grief honoured", "hard truth", "necessary ending", "stillness"),
            UprightMeaning: """Something in your life is over, and the honest work now is not repair but burial. Ereshkigal does not accept bargaining. She will not let you keep the crown, the argument, the version of the story in which you were never at fault. What she offers instead is rare: a place where your grief is not an inconvenience. Go down. Say the loss plainly, to yourself first. Sit in the dark room with it for as long as it actually takes, and refuse the friends who want you cheerful by Thursday. She is echoed back into mercy by one thing only, which is somebody willing to feel what she feels.""",
            ReversedKeywords: L("avoidance", "frozen mourning", "punishing yourself", "denial"),
            ReversedMeaning: """You are standing at the top of the stair with the door open, making conversation. Reversed, Ereshkigal shows grief that has been postponed until it hardened, or mourning kept going past its season because letting it end would feel like a second betrayal. There may also be a punishment you are administering to yourself under the name of accountability. Nothing down there is asking for your suffering. It is asking for your attention. Name the loss, set a limit on the vigil, and let somebody sit with you.""",
            Invocation: """Queen of the bolted gate, who counts what will not come back — hear the one cry that is not a bargain."""),

        new Card(
            Id: 3, Suit: CardSuit.Goddess, Number: 3,
            Name: "Ninhursag",
            Epithet: "Lady of the Sacred Mountain",
            Transliteration: "dNin-ḫur-saĝ / Ninmaḫ / Nintu",
            Emblem: Emblem.Mountain,
            AccentColor: "#B5651D", SecondaryColor: "#2E2015",
            Domains: L("birth", "the making of bodies", "healing", "foothills and pasture"),
            Lore: """Ninhursag is the great mother of the Sumerian pantheon, called Ninmah, the exalted lady, and Nintu, the lady who gives birth. Her temple at Kesh was praised in one of the oldest Sumerian hymns, and kings called themselves nourished by her milk. In Enki and Ninhursag, set in clean Dilmun, Enki eats the eight plants she has grown and sickens in eight parts of his body; she returns from her anger and bears eight healing deities, one for each pain. In Enki and Ninmah the two of them drink and compete at shaping human beings from clay, and she finds a place even for the creatures made wrong.""",
            UprightKeywords: L("gestation", "repair", "patience", "belonging", "fertile ground"),
            UprightMeaning: """What you are carrying is not late; it is forming. Ninhursag works at the speed of bodies, and bodies cannot be hurried by argument. If you are building something real — a pregnancy, a practice, a recovery, a household — the task this season is to protect the conditions rather than inspect the results. Eat properly. Sleep. Let the people who feed you feed you. She is also the great mender: there is a specific hurt, named in a specific place, that can be treated now instead of endured. And she is the one who makes room for the ill-made. Whatever in you was shaped crookedly still has a use and a seat.""",
            ReversedKeywords: L("depletion", "forcing growth", "unmothered", "neglected body"),
            ReversedMeaning: """You are trying to harvest a field you have not watered. Reversed, Ninhursag points at depletion — giving from an empty vessel, mothering everyone while nobody asks after you, or pushing a slow thing to finish on an invented deadline. Sometimes she marks the older wound of not having been tended when you were small, which now shows up as an inability to accept care without earning it. Withdraw the effort. Attend to the body first: the sleep, the food, the appointment you keep moving. Growth resumes when the ground does.""",
            Invocation: """Lady of the foothills, whose hands know the shape of what is not yet born — keep the ground warm over me."""),

        new Card(
            Id: 4, Suit: CardSuit.Goddess, Number: 4,
            Name: "Nammu",
            Epithet: "Mother of the Primeval Sea",
            Transliteration: "dNammu",
            Emblem: Emblem.Wave,
            AccentColor: "#0E4D52", SecondaryColor: "#04252A",
            Domains: L("origin", "the unformed", "the Apsu", "creation from clay"),
            Lore: """Nammu is the oldest figure in Sumerian cosmology, the sea that was there before there was anything to be beside it. Her name is written with a sign read as engur, the deep freshwater abyss, and she is called the mother who gave birth to heaven and earth and the mother of Enki. In the myth of Enki and Ninmah it is Nammu who rouses her sleeping son out of the deep and tells him that the gods are complaining of their labour, and at her word he mixes clay from above the Apsu and the first human beings are shaped. She has few temples and no long myths of her own; she is the water under everything.""",
            UprightKeywords: L("beginnings", "the deep source", "rest before making", "instinct"),
            UprightMeaning: """Before the shape, the water. This card comes when you are reaching for a plan too early, wanting a title and a schedule for something that has not yet decided what it is. Nammu says the formless stage is not a failure of discipline. Float a while. Sleep on it, walk beside water, keep the notebook but stop demanding conclusions from it. She is also the one who wakes the maker: if you have been submerged long enough, expect the nudge, and expect it to arrive as somebody else's need rather than as your own inspiration. What you make from this depth will be made of honest material. Let it come up slowly.""",
            ReversedKeywords: L("drifting", "formlessness", "no boundary", "stalled beginning"),
            ReversedMeaning: """The deep has stopped being restful and become a place to hide. Reversed, Nammu is drift: endless preparation, a project permanently in its early stage, feelings so unshaped that nobody, including you, can act on them. It can also mean too little edge between you and other people, so that their moods come in like tide. Draw one line today. Name the thing, however badly. Give it a date and a container. The water is not your enemy, but it will take any shape you fail to give it.""",
            Invocation: """Mother of the engur, older than the first word — lift me from the water with my hands already full."""),

        new Card(
            Id: 5, Suit: CardSuit.Goddess, Number: 5,
            Name: "Ki",
            Epithet: "The Earth Herself",
            Transliteration: "dKi / Uraš",
            Emblem: Emblem.Bull,
            AccentColor: "#7A6A4F", SecondaryColor: "#2B2419",
            Domains: L("ground", "place", "endurance", "separation of heaven and earth"),
            Lore: """Ki is the earth as a divine principle, named beside An the sky in the oldest Sumerian god lists. The prologue to Gilgamesh, Enkidu and the Netherworld tells that after heaven and earth were parted, An carried off the sky and Enlil carried off the earth. Under the name Uraš she is sometimes An's consort, and the pairing an-ki, heaven-earth, is simply the Sumerian word for the universe. She has little myth and almost no cult of her own, and that is the point of her: she is the surface everyone stands on and nobody addresses. What the gods quarrel over is always, in the end, her ground.""",
            UprightKeywords: L("grounding", "practical reality", "place", "stamina", "the long view"),
            UprightMeaning: """Put your hand on something solid. Ki answers the question of what is actually here — the lease, the body, the bank balance, the hours in the day — and she answers it without drama. If you have been living in the argument about your situation rather than the situation, this card returns you to the floor. Walk the property. Count the money. Ask what is true regardless of how you feel about it. There is enormous relief in this, and enormous power: nothing can be built on a story, and everything can be built on a fact. She also says stay. The place you are standing is more fertile than the place you keep imagining.""",
            ReversedKeywords: L("uprooted", "abstraction", "restlessness", "neglecting the practical"),
            ReversedMeaning: """You are floating an inch above your own life. Reversed, Ki shows plans made without reference to money, time or geography, or a restlessness that treats every ordinary difficulty as a sign to move on. It can also mean a genuine uprooting — a home lost, a country left — where the first task is not meaning but footing. Do the unglamorous thing. Fix the document, pay the bill, unpack the boxes. Solid ground is not the opposite of a spiritual life; it is the condition for one.""",
            Invocation: """Earth beneath the argument, floor beneath the throne — hold me while I learn to stand."""),

        new Card(
            Id: 6, Suit: CardSuit.Goddess, Number: 6,
            Name: "Tiamat",
            Epithet: "The Bitter Sea, Mother of Monsters",
            Transliteration: "dTi-amat",
            Emblem: Emblem.Serpent,
            AccentColor: "#1B6B5A", SecondaryColor: "#0A1F26",
            Domains: L("raw force", "chaos", "wrath", "the world's raw material"),
            Lore: """Tiamat is the salt sea of the Babylonian Enuma Elish, mingled at the beginning with Apsu the sweet water so that the two were one body. From them the gods are born, and the young gods are loud, and Apsu plans to kill them for the sake of quiet; Ea kills him first. Roused to war, Tiamat breeds eleven monsters, raises Qingu to lead them and gives him the tablet of destinies. Marduk faces her with the winds, sends a gale into her open mouth, splits her like a shellfish and builds the sky from one half and the earth from the other. The Tigris and Euphrates are said to run from her eyes.""",
            UprightKeywords: L("raw force", "creative chaos", "rightful rage", "the old order"),
            UprightMeaning: """There is a great deal of power in you that has not been made polite yet, and this card says do not apologise for its size. Tiamat is the material the world gets made out of: anger with a real cause, appetite, grief with teeth. The danger is never that you feel it, only that you discharge it at the first available target. Give the force a channel worthy of it. Write the letter and do not send it for a week. Take the fight to the actual policy rather than to the nearest person. Something enormous is being cut into shape now, and the shape will hold for years. Be present at the cutting.""",
            ReversedKeywords: L("destructive rage", "swallowed by chaos", "scorched earth", "old resentment"),
            ReversedMeaning: """The sea is over the wall. Reversed, Tiamat is force without a channel: the quarrel that burns the house to win the room, the resentment kept so long it has bred creatures of its own, the plan that destroys more than it replaces. It can also be the opposite face, being swamped by somebody else's chaos and calling it loyalty. Get out of the water before you decide anything. Slow the breath, delay the message, put one day between the feeling and the act. What is monstrous here is only unshaped.""",
            Invocation: """Bitter sea whose halves became the sky — teach me to be vast without drowning what I love."""),
    ];

    #endregion

    #region The Great Goddesses VII..XII

    private static readonly Card[] GoddessesB =
    [
        new Card(
            Id: 7, Suit: CardSuit.Goddess, Number: 7,
            Name: "Ninlil",
            Epithet: "Lady of the Open Field",
            Transliteration: "dNin-líl / Sud",
            Emblem: Emblem.Bird,
            AccentColor: "#9FB8C8", SecondaryColor: "#33424D",
            Domains: L("loyalty", "chosen exchange", "wind and grain", "the road down"),
            Lore: """Ninlil was the great lady of Nippur, worshipped beside Enlil in the Ekur. The poem Enlil and Ninlil tells how he took her at the canal against the warning of her mother, was tried by the assembly and banished to the netherworld, and how she followed him down. On the road she bore Nanna the moon, and then three more children who remained below in his place. In the Marriage of Sud she is Sud of Eresh, daughter of the scribal goddess Nisaba, courted properly with gifts and negotiation, and it is at her wedding that she is given the name Ninlil. Two poems, two versions of how a woman enters power.""",
            UprightKeywords: L("following through", "chosen loyalty", "fertile exchange", "quiet strength"),
            UprightMeaning: """You are being asked to go with something all the way, and to make it your own decision rather than a thing that happened to you. Ninlil follows Enlil down the road and, in the going, changes the story: what began as loss becomes a lineage. If a relationship or a commitment has reached the part where nobody is watching and there is no applause, this is the card of staying. But she is also Sud, who was courted with gifts and terms; she does not counsel endurance without negotiation. Ask for the contract, the vow, the clear word. Loyalty that is chosen out loud is a different substance from loyalty that is merely habitual.""",
            ReversedKeywords: L("consent blurred", "drifting along", "duty without choice", "silent resentment"),
            ReversedMeaning: """Something is happening to you that you have not agreed to, and you are keeping the peace by not mentioning it. Reversed, Ninlil marks the murky place where habit is being mistaken for devotion, where a woman follows because leaving is unthinkable rather than because staying is chosen. It can also be the pressure to be gracious about a thing that was taken. Stop, and put words on it. Say the terms you would need in order to stay gladly. Then find out whether they can be met.""",
            Invocation: """Lady of the open field, who walked down the road and made it a lineage — teach me to choose what I carry."""),

        new Card(
            Id: 8, Suit: CardSuit.Goddess, Number: 8,
            Name: "Ningal",
            Epithet: "Great Lady of the Moon",
            Transliteration: "dNin-gal",
            Emblem: Emblem.Crescent,
            AccentColor: "#D8DCE3", SecondaryColor: "#1E2A45",
            Domains: L("lament", "dreams", "reed marshes", "witness", "a city loved"),
            Lore: """Ningal, whose name simply means Great Lady, was the wife of the moon god Nanna-Suen at Ur, and mother of Inanna and of the sun god Utu. Her cult ran from Ur to Harran and lasted into the first millennium. She is best known from the Lament for the Destruction of Ur, in which she pleads through the night with An and Enlil to revoke the sentence on her city, is refused, and afterwards will not enter the ruined house: she sits outside it, describing what she sees. The literature also gives her dreams and their reading. Her power is not rescue. It is unflinching witness, and the refusal to pretend.""",
            UprightKeywords: L("witness", "lament", "tenderness", "night vision", "staying with what is"),
            UprightMeaning: """You do not have to fix this to be of use. Ningal pleaded and was refused, and then did the harder thing: she stayed and looked and said exactly what had happened. Someone near you is in trouble you cannot solve, or you are, and the temptation is either to manufacture optimism or to leave the room. Do neither. Sit with it in plain language. Say the loss aloud, keep the vigil, write it down. Grief spoken truly is a form of repair that nothing else substitutes for. Watch your dreams this month as well; something is being shown to you sideways, in the moon's light rather than the sun's.""",
            ReversedKeywords: L("bright-siding", "numbness", "looking away", "unspoken sorrow"),
            ReversedMeaning: """The house is damaged and everyone is talking about something else. Reversed, Ningal shows the family, team or friendship that has agreed not to name the obvious wound, and the flatness that follows — not peace, but anaesthesia. It may be your own sorrow you are managing at arm's length with busyness and cheerful updates. Nothing heals in that climate. Go back to the place you have been avoiding, look at it directly, and describe it to one person who will not immediately try to make you feel better.""",
            Invocation: """Great Lady who would not enter the ruined house, and would not lie about it — give me eyes that stay open in the dark."""),

        new Card(
            Id: 9, Suit: CardSuit.Goddess, Number: 9,
            Name: "Nisaba",
            Epithet: "Lady of the Stylus and the Grain",
            Transliteration: "dNisaba / Nidaba",
            Emblem: Emblem.Tablet,
            AccentColor: "#2F5DA8", SecondaryColor: "#E8E1D2",
            Domains: L("writing", "measurement", "accounting", "grain", "learning"),
            Lore: """Nisaba of Eresh is the goddess of grain and of the written sign, and the Sumerians saw no strangeness in the pairing: both are counted, stored and shared out. Scribes ended their tablets with the praise Nisaba be praised, and the schools of Nippur were under her hand. Hymns give her the lapis lazuli tablet on which the plan of heaven is set down, and the reed stylus and measuring line of the surveyor. She is the mother of Sud, who becomes Ninlil. As Sumerian was overtaken by Akkadian her offices passed largely to the god Nabu of Borsippa, but the colophons kept her name for centuries.""",
            UprightKeywords: L("clarity", "study", "write it down", "accurate reckoning", "craft"),
            UprightMeaning: """Write it down. Nisaba's whole teaching is that a thing you can count is a thing you can govern, and that the mind clears the moment it stops holding everything at once. Make the list, open the spreadsheet, draft the document you have been rehearsing in your head for three weeks. If you are studying or learning a craft, this card promises that the slow accumulation is working even though it does not feel like it. She also guards accuracy in speech: say the number, name the date, decline to be vague where vagueness would flatter you. A record is a kindness to the person you will be in a year.""",
            ReversedKeywords: L("muddle", "vagueness", "overthinking", "records neglected"),
            ReversedMeaning: """The account does not balance and you have not looked. Reversed, Nisaba is the unopened envelope, the deadline half-remembered, the agreement everyone recalls differently because nobody wrote it down. It can also invert into her other failure — analysis as avoidance, a study of the problem so thorough that it never becomes an act. Either way, the remedy is small and immediate. Spend one hour reconciling what is actually true, on paper, and then do the first thing on the list rather than the research about the list.""",
            Invocation: """Lady of the lapis tablet, who set the plan of heaven in a line of reeds — let my hand write what my mouth must keep."""),

        new Card(
            Id: 10, Suit: CardSuit.Goddess, Number: 10,
            Name: "Nanshe",
            Epithet: "Lady of Justice and the Marshes",
            Transliteration: "dNanše",
            Emblem: Emblem.Fish,
            AccentColor: "#7FB5A6", SecondaryColor: "#134E4A",
            Domains: L("social justice", "dream interpretation", "fish and birds", "weights and measures"),
            Lore: """Nanshe of Nina, in the territory of Lagash, is a goddess of the marsh and its fish and birds, of dream interpretation, and above all of justice measured in ordinary transactions. Her great hymn describes her knowing the orphan and the widow, the man who cheats with a false weight, the one who says I did not know. At the new year she reckons accounts and the wrongdoers are set apart. In the cylinders of Gudea it is Nanshe who reads the ruler's dream and tells him what the gods require. The Home of the Fish is a charming poem in her circle, coaxing every kind of fish indoors.""",
            UprightKeywords: L("fairness", "dreams that instruct", "advocacy", "honest measure", "care for the small"),
            UprightMeaning: """Look at who is being short-changed, and check that it is not you. Nanshe attends to the exact weight in the exact transaction: the invoice, the split of labour at home, the credit given for work in a meeting. This is not a card of grand causes; it is a card of correcting one specific unfairness that everyone has agreed to overlook. Speak for the person with the least standing in the room, and, if that person is you, speak anyway and with numbers. She is also the reader of dreams. Something you dreamed recently, or a repeated image that will not leave, is a message about this very matter. Take it seriously.""",
            ReversedKeywords: L("unfair bargain", "ignored warning", "silence in the face of wrong", "bad faith"),
            ReversedMeaning: """The scales are off and the discrepancy is being explained away. Reversed, Nanshe shows an arrangement that survives only because nobody says the number out loud — unpaid work, a friendship where one person always pays, a workplace rewarding the loudest. It can also mean a warning already given, in a dream or by a person you respect, that you decided not to hear. Go back to it. Write down the actual figures or the actual hours, and take the smallest concrete step toward correction today.""",
            Invocation: """Lady of the reed beds, who knows the false weight in the closed hand — read my dream, and make my measure true."""),

        new Card(
            Id: 11, Suit: CardSuit.Goddess, Number: 11,
            Name: "Gula",
            Epithet: "The Great Physician",
            Transliteration: "dGula / dBau",
            Emblem: Emblem.Vessel,
            AccentColor: "#D6B45A", SecondaryColor: "#274240",
            Domains: L("healing", "convalescence", "the sickbed", "oaths and curses", "dogs"),
            Lore: """Gula is the great healing goddess of Mesopotamia, worshipped at Isin, Nippur and Babylon, and closely bound up with Bau of Lagash and Ninisina, with whom she was eventually merged. Her animal is the dog: a dog cemetery with dozens of careful burials was excavated at her Isin temple, and dog figurines were buried under thresholds for protection. The long hymn of Bullutsa-rabi has her list her own powers in the first person — she holds the scalpel and the bandage, she reads the body, she raises the dead man's head from the pillow. She is also invoked in curses, because the one who can heal can withhold.""",
            UprightKeywords: L("healing", "convalescence", "diagnosis", "practical care", "recovery"),
            UprightMeaning: """This is the card of getting properly well, which is slower and more boring than being cured. Gula works with hands and instruments, not only with prayer: make the appointment, take the full course, tell the practitioner the embarrassing symptom you have been editing out. If the illness is not of the body, the method is the same — name it exactly, get skilled help, and accept the unglamorous regimen. She also blesses the convalescent stage that people rush: the weeks when you are no longer ill but not yet strong, when doing half of what you could is the treatment. Guard that interval. Something in you is knitting.""",
            ReversedKeywords: L("neglected symptom", "relapse", "refusing help", "chronic strain"),
            ReversedMeaning: """You are managing a condition instead of treating it. Reversed, Gula points at the symptom you have normalised, the appointment postponed for a year, the recovery abandoned as soon as the pain eased. It can also mean help refused out of pride, or care so consumed by other people's illnesses that your own has no room. There is no honour in endurance here. Go back to the beginning of the regimen without shame, ask one person for practical assistance, and treat the cause rather than the noise it makes.""",
            Invocation: """Great physician, whose dog sleeps at the threshold of the sickroom — set your hand where the pain has no name."""),

        new Card(
            Id: 12, Suit: CardSuit.Goddess, Number: 12,
            Name: "Ninisina",
            Epithet: "Lady of Isin, Midwife of the Gods",
            Transliteration: "dNin-Isin(a)",
            Emblem: Emblem.Lamp,
            AccentColor: "#8FBFAE", SecondaryColor: "#1F3B36",
            Domains: L("midwifery", "the healing arts", "advocacy before power", "thresholds of life"),
            Lore: """Ninisina, the Lady of Isin, was the city goddess of Isin and a healer whose priests were practising physicians; her songs call her the great doctor of the black-headed people and set her in the birthing room as well as at the bedside. In one hymn she speaks of herself as an asu, holding the lancet and the herbs, and in others she goes before the assembly of the gods to plead for her city and her people. Over time she was drawn together with Gula and Bau until the names became interchangeable, but the Isin texts keep her distinct: the goddess of the city whose dynasty ruled Sumer after Ur fell.""",
            UprightKeywords: L("bringing through", "skilled hands", "intercession", "crisis attended", "safe passage"),
            UprightMeaning: """Something is being born and it needs a competent attendant more than it needs a philosophy. Ninisina is the professional in the room at the hard hour: she knows what is normal, what is dangerous, and when to simply wait. If you are in the middle of a transition that cannot be paused — a birth, a move, a diagnosis, a launch — accept that the job now is to get through it well rather than to get through it beautifully. She also intercedes. Somebody with standing will speak for you if you ask them plainly, and you should ask this week rather than next. Passage is possible. Take the hand that is offered.""",
            ReversedKeywords: L("labouring alone", "no advocate", "panic", "amateur hour"),
            ReversedMeaning: """You are attempting a difficult passage without anyone experienced beside you. Reversed, Ninisina shows the crisis handled by improvisation and pride: no advocate engaged, no specialist called, no one told how bad it has actually become. It can also be the aftermath of a hard passage that nobody acknowledged, so that you came out of it alone. Name the situation to one competent person this week — a lawyer, a midwife, a doctor, a mentor. Skilled help is not a confession of weakness; it is the ordinary equipment of a hard hour.""",
            Invocation: """Lady of Isin, who stands between the cry and the silence — bring me through, and let someone speak my name in the assembly."""),
    ];

    #endregion

    #region The Great Goddesses XIII..XVIII

    private static readonly Card[] GoddessesC =
    [
        new Card(
            Id: 13, Suit: CardSuit.Goddess, Number: 13,
            Name: "Ninsun",
            Epithet: "The Wild Cow, Mother of Heroes",
            Transliteration: "dNin-sún",
            Emblem: Emblem.Cow,
            AccentColor: "#E8DCC0", SecondaryColor: "#5C4B2E",
            Domains: L("wise counsel", "dream reading", "mothering the powerful", "adoption"),
            Lore: """Ninsun, whose name means Lady Wild Cow, was worshipped at Uruk and Kullab as the wife of Lugalbanda and the mother of Gilgamesh. In the Babylonian epic she is the one person the king listens to: he brings her his two strange dreams and she reads them, telling him that a companion is coming whom he will love as a wife. Before he leaves for the Cedar Forest she climbs to the roof, offers incense to Shamash, complains to the sun god about the restless heart he gave her son, and then adopts Enkidu as her own, hanging an amulet at his neck. She is wisdom that takes a practical form.""",
            UprightKeywords: L("wise counsel", "reading the signs", "chosen kin", "steady influence"),
            UprightMeaning: """Take the advice. Ninsun is the voice that is not impressed by your reputation and not frightened of your temper, and she is usually right. If an older colleague, a parent, a teacher or a plain-speaking friend has told you something you have been arguing with internally, revisit it this week — the resistance you feel is a good sign it landed. She also blesses the family you assemble rather than inherit: the friend adopted as kin, the mentor who takes responsibility for you, the household made by choice. Say out loud who belongs to you. And bring your dreams and your dread to someone skilled, rather than turning them over alone at three in the morning.""",
            ReversedKeywords: L("advice refused", "smothering", "unheeded warning", "borrowed judgement"),
            ReversedMeaning: """Counsel is going wrong in one direction or the other. Reversed, Ninsun is either the warning you have decided to treat as nagging, or care that has become control — a mother, mentor or manager who cannot let the hero leave for the forest. It can also mean you are outsourcing your judgement entirely, asking everyone and deciding nothing. Separate the two questions: what did the wise person actually say, and what is your own reading? Then act on your own, having genuinely heard theirs.""",
            Invocation: """Wild Cow of the sheepfold, who climbed to the roof to argue with the sun — read the dream I cannot read alone."""),

        new Card(
            Id: 14, Suit: CardSuit.Goddess, Number: 14,
            Name: "Geshtinanna",
            Epithet: "Lady of the Vine, Scribe of the Below",
            Transliteration: "dĜeštin-an-na",
            Emblem: Emblem.Tree,
            AccentColor: "#6B2D5B", SecondaryColor: "#2A1526",
            Domains: L("sisterhood", "dream interpretation", "willing sacrifice", "wine and the turning year"),
            Lore: """Geshtinanna, the Vine of Heaven, is the sister of Dumuzi and one of the great loyal figures of Sumerian literature. In Dumuzi's Dream he wakes in terror and she interprets the omens one by one, telling him the demons are coming; then she hides him, and when the galla beat and question her she will not say where he is. In the closing of Inanna's Descent, after Dumuzi is taken as the substitute, Geshtinanna offers herself, and the sentence is divided: he is below for half the year and she for the other half. She is also named as a scribe and dream-reader of the netherworld.""",
            UprightKeywords: L("loyalty", "sharing the burden", "reading omens", "seasonal turn", "quiet courage"),
            UprightMeaning: """Someone's burden is about to become partly yours, and you are going to offer, not be conscripted. Geshtinanna is the card of the sister who takes the other half of the year — genuine sacrifice, freely chosen, with a limit on it. Notice the limit: she does not take the whole sentence, and the myth is careful about that. If you are carrying someone now, agree the terms and the season. If you are the one being carried, let it happen and say thank you plainly. The card also sharpens interpretation: the dream, the odd coincidence, the thing you noticed and dismissed this week is worth ten minutes of honest reading.""",
            ReversedKeywords: L("martyrdom", "burden without limit", "betrayal", "omens ignored"),
            ReversedMeaning: """You have taken the whole sentence when half was asked. Reversed, Geshtinanna is sacrifice that has stopped being a gift and become an identity — the sibling who absorbs the family's trouble, the friend who is only ever the rescuer. It may also mark a loyalty tested and failed, by you or against you, where someone did say where the other was hiding. Put a season on what you are carrying. Say the date when it ends, and mean it, and let the other half be carried by whoever it actually belongs to.""",
            Invocation: """Vine of Heaven, who would not tell the demons where he lay — divide the year with me, and keep my word unbroken."""),

        new Card(
            Id: 15, Suit: CardSuit.Goddess, Number: 15,
            Name: "Ninshubur",
            Epithet: "The Faithful Minister",
            Transliteration: "dNin-šubur",
            Emblem: Emblem.Flame,
            AccentColor: "#D98E32", SecondaryColor: "#2C2A3F",
            Domains: L("service", "steadfastness", "asking on another's behalf", "the plan agreed in advance"),
            Lore: """Ninshubur is the sukkal of Inanna, her minister and envoy, feminine in the Sumerian tradition. Before the Descent, Inanna gives her precise instructions: if I do not return in three days, set up a lament for me at the ruin mounds, beat the drum, tear your eye and your mouth, dress as a pauper and go to Enlil at Nippur, then to Nanna at Ur, then to Enki at Eridu. She does exactly this. Enlil and Nanna refuse; Enki is troubled and makes the two creatures who go down with the food and water of life. Later, when the galla demand a substitute, Inanna refuses to give them Ninshubur.""",
            UprightKeywords: L("faithful service", "the agreed plan", "asking for help", "persistence", "trust earned"),
            UprightMeaning: """Do the thing you said you would do, in the order you said you would do it. Ninshubur's power is not brilliance; it is that she follows the instruction exactly when everything has gone wrong and improvisation would feel more heroic. If you have made a plan for your own bad days — who to call, what to stop, when to go to the door — this is the week to honour it. She is also the one who keeps knocking after two refusals. Ask the second person. Ask the third. And notice who has been steady for you without being thanked; go and thank them by name, this week, in words they can keep.""",
            ReversedKeywords: L("loyalty unreturned", "over-functioning", "no plan", "asking no one"),
            ReversedMeaning: """You are holding a post nobody has acknowledged, or you are the one who never asks. Reversed, Ninshubur is service without reciprocity — the colleague who covers everything and is named in no credit, the friend who arrives for every crisis and has never once had somebody arrive for theirs. It can also be the absence of any plan at all, so that the bad day finds you with no number to call. Fix the smaller thing first: write down three names and what you would ask each of them for.""",
            Invocation: """Minister of the Queen, who kept the promise at the ruin mounds — stand at my door when I cannot open it myself."""),

        new Card(
            Id: 16, Suit: CardSuit.Goddess, Number: 16,
            Name: "Ninkasi",
            Epithet: "Lady Who Fills the Mouth",
            Transliteration: "dNin-kasi",
            Emblem: Emblem.BeerJar,
            AccentColor: "#E0A33E", SecondaryColor: "#6B3E17",
            Domains: L("brewing", "fermentation", "hospitality", "pleasure", "the shared table"),
            Lore: """Ninkasi is the Sumerian goddess of beer, and the Hymn to Ninkasi is both a song and a working recipe: the bappir loaf baked and dried, the malt spread out, the mash in the vat, the sweet wort filtered into the collector like the Tigris and Euphrates meeting. The refrain names her as the one who handles the dough with a big shovel and mixes the bappir with date-honey. Beer in Sumer was daily food, rationed to workers and offered to the gods, drunk through reed straws from a communal jar. Another poem sets a drinking song beside the hymn, so praise and pleasure arrive together.""",
            UprightKeywords: L("pleasure", "hospitality", "fermentation", "patience with process", "conviviality"),
            UprightMeaning: """Set the jar in the middle of the table. Ninkasi says that ordinary shared pleasure is not a distraction from your serious life, it is part of the apparatus that keeps you alive — feed people, accept the invitation, sit down with the friends who make you laugh in the old way. She also governs fermentation, which is the art of doing nothing while something transforms. If a project, a grief or a relationship is in its quiet stage, stop stirring it. The work is done; now the sugars turn. Warmth and time will do what effort cannot. Something bitter in you is becoming something you can actually drink.""",
            ReversedKeywords: L("numbing", "excess", "hollow hospitality", "impatience with process"),
            ReversedMeaning: """The drink has stopped being pleasure and become an anaesthetic. Reversed, Ninkasi marks any use of comfort to skip a feeling — alcohol, yes, but equally the scroll, the spend, the constant company that leaves no hour alone. It also shows the opposite failure: opening the vat too early because you could not bear to wait, and pouring something raw. Ask honestly what you are drinking at. Then keep the table and change the terms: invite people for the day rather than the night, and let the slow thing finish fermenting.""",
            Invocation: """Lady who fills the mouth, whose vat sings like two rivers meeting — let what is bitter in me turn sweet with time."""),

        new Card(
            Id: 17, Suit: CardSuit.Goddess, Number: 17,
            Name: "Nanaya",
            Epithet: "Mistress of Charm and Longing",
            Transliteration: "dNa-na-a",
            Emblem: Emblem.Mirror,
            AccentColor: "#D45D79", SecondaryColor: "#3B1F33",
            Domains: L("erotic love", "allure", "courtship", "music and delight"),
            Lore: """Nanaya was a goddess of love and erotic charm, first attested at Uruk in the Ur III period and close enough to Inanna that hymns shuttle between them, though the cities kept them distinct. Her worship spread to Borsippa, Kish and later far beyond Mesopotamia, surviving under variants of her name for well over two thousand years — one of the longest-lived cults of the ancient Near East. Sumerian and Akkadian love lyrics give her praise of the body, of ornament, of the lover's approach; a well-known composition has her declare her own sweetness attribute by attribute. Where Inanna is sovereignty, Nanaya is the particular, personal pull between two people.""",
            UprightKeywords: L("attraction", "flirtation", "being wanted", "delight in the body", "romance"),
            UprightMeaning: """Let yourself be charming. Nanaya is not about a life partner or a destiny; she is about the specific, delicious pull between two people in a room, and the permission to enjoy it. Dress for your own pleasure. Reply to the message. Say the compliment you have been swallowing because it might mean something. If you have been living in the neck up for a season — all analysis, no body — she prescribes music, touch, dancing badly, good fabric against your skin. For those already partnered, this is a card of courtship resumed: stop being efficient with each other for one evening and be interesting instead.""",
            ReversedKeywords: L("vanity", "chasing validation", "disconnected from the body", "flirtation as evasion"),
            ReversedMeaning: """The charm is running without you inside it. Reversed, Nanaya shows allure used as currency — collecting attention to prove something, keeping several people mildly interested so that none of them can actually reach you, or an image maintained at real cost to the body underneath. It can also mean numbness, a long stretch with no desire at all and shame about it. Neither is solved by trying harder to be wanted. Come back to sensation on your own: taste, music, water, sleep. Desire returns to a body that is being inhabited.""",
            Invocation: """Mistress of longing, who makes the ordinary street worth walking down — put delight back into my two hands."""),

        new Card(
            Id: 18, Suit: CardSuit.Goddess, Number: 18,
            Name: "Ishara",
            Epithet: "Lady of the Oath",
            Transliteration: "dIš-ḫa-ra",
            Emblem: Emblem.Scorpion,
            AccentColor: "#B03A2E", SecondaryColor: "#2A1A16",
            Domains: L("oaths", "binding love", "justice against the forsworn", "the scorpion"),
            Lore: """Ishara came into Mesopotamia from the Syrian west, attested at Ebla and Mari and taken up in Sumerian and Akkadian cult as well as Hurrian and Hittite. Two offices cling to her: love, so that she stands close to Ishtar in wedding contexts, and the oath, so that she is called on to witness treaties and to punish those who break them. Her animal is the scorpion, and the constellation we call Scorpius was associated with her. Kings swearing to one another named her among the divine witnesses. She holds together the two things an oath actually is — a bond of affection and a thing with a sting in it.""",
            UprightKeywords: L("vow", "binding commitment", "integrity", "consequences named", "sworn love"),
            UprightMeaning: """Make the promise properly or do not make it. Ishara governs the moment a feeling becomes an obligation — the contract signed, the vow spoken, the yes that other people will now build on. If you are near such a moment, slow down and read the terms, including the ones that will bind you when the warmth has cooled. Say out loud what happens if you fail. That is not pessimism; it is what makes a promise worth anything. She also defends the one who kept faith with someone who did not: if you have been wronged in an agreement, this card says the matter is legitimate and you may pursue it. Be precise, and be unafraid of the sting.""",
            ReversedKeywords: L("broken word", "loophole", "commitment feared", "oath used as a cage"),
            ReversedMeaning: """A promise is being held in bad faith. Reversed, Ishara shows the letter kept while the spirit is emptied out, the agreement quietly not honoured on the assumption nobody will make a scene, or a vow being used to hold someone who wants to leave. It may also be your own fear of binding yourself, so that everything stays provisional and therefore unreal. Say the true position. Either renegotiate the terms openly or keep them fully. What cannot continue is the middle, where the words say one thing and the conduct another.""",
            Invocation: """Lady of the oath, witness with the scorpion at your feet — hold me to what I swore, and hold them to it too."""),
    ];

    #endregion

    #region The Great Goddesses XIX..XXIV

    private static readonly Card[] GoddessesD =
    [
        new Card(
            Id: 19, Suit: CardSuit.Goddess, Number: 19,
            Name: "Sarpanit",
            Epithet: "The Shining One, Creatress of Seed",
            Transliteration: "dZarpanītu / Ṣarpānītu",
            Emblem: Emblem.Spindle,
            AccentColor: "#C0C4C8", SecondaryColor: "#2B3A55",
            Domains: L("marriage", "lineage", "intercession", "the shining name", "Babylon"),
            Lore: """Sarpanit is the consort of Marduk and the great lady of Babylon, housed with him in the Esagil. Babylonian scholars read her name two ways, as the silvery shining one and as zer-banitu, creatress of seed, and both readings stuck: she is brightness and she is the continuance of a line. At the akitu, the new year festival, her procession and her marriage to Marduk belonged to the ceremonies by which the year and the kingship were renewed. Prayers approach her as the one who speaks to Marduk on the worshipper's behalf, the voice beside the throne rather than the one on it.""",
            UprightKeywords: L("partnership", "intercession", "lineage", "quiet influence", "renewal"),
            UprightMeaning: """Influence here works through relationship rather than position. Sarpanit is beside the throne, and the whole point of her is that the word spoken privately at the right hour changes the decree. If you need something from an institution or a powerful person, stop composing the formal appeal and find the human being who already has their ear. Equally, this card blesses a real partnership and asks you to tend it deliberately: the shared ledger, the shared name, the thing you two are building for people who come after. Renewal is available — an anniversary, a re-founding, a decision to begin the year properly rather than let it start by drift.""",
            ReversedKeywords: L("influence without a voice", "eclipsed", "dynastic pressure", "hollow ceremony"),
            ReversedMeaning: """You are standing beside the throne and getting nothing for it. Reversed, Sarpanit is the partner whose contribution is invisible, the colleague whose ideas arrive in the room under someone else's name, or a family expectation about marriage and children pressing on a life that does not want that shape. It can also mark ritual emptied of meaning, an anniversary observed with nothing behind it. Ask for the credit in plain words. Say what you actually want the next year to contain, even if the answer breaks the ceremony.""",
            Invocation: """Shining one of the Esagil, whose word turns the word of the king — speak for me when the doors are closed."""),

        new Card(
            Id: 20, Suit: CardSuit.Goddess, Number: 20,
            Name: "Aya",
            Epithet: "Bride of the Dawn",
            Transliteration: "dA-a kallatu",
            Emblem: Emblem.Sun,
            AccentColor: "#F0C987", SecondaryColor: "#8A4F5E",
            Domains: L("daybreak", "mercy", "intercession", "the first light", "second chances"),
            Lore: """Aya is the wife of Shamash the sun god, worshipped with him in the Ebabbar temples at Sippar and Larsa, and her standing title is kallatu, the bride or daughter-in-law. She belongs to the dawn, the moment before the sun is fully out, and her recurring role in prayer is intercession: the petitioner asks Aya to speak to Shamash on their behalf, to make him favourable, to have him look kindly when he rises and sees. Since Shamash is also the god of justice and of the oracle, this makes her the softening voice within the law, the plea for mercy attached to a true verdict.""",
            UprightKeywords: L("dawn", "mercy", "a fresh start", "advocacy", "warmth returning"),
            UprightMeaning: """The night has an end and it is closer than you think. Aya is the hour just before the light, and she asks you to act as though morning is coming even before you can see it: get up, wash, open the curtains, do the first small ordinary thing. She is also mercy inside a fair judgement. If you have been hard on yourself about a real failure, the verdict may stand while the sentence changes — you can be honest about what you did and still stop punishing yourself for it. Somebody is willing to speak for you. Let them. And extend the same to one person you have been holding to an impossible standard.""",
            ReversedKeywords: L("false dawn", "mercy withheld", "sleepless", "unforgiven"),
            ReversedMeaning: """You are waiting for the light and refusing to prepare for it. Reversed, Aya is the cycle of premature hope followed by collapse, or a mercy you will not accept because accepting it would mean the debt is closed and you still feel it should not be. It can also be the mercy you will not extend, a grudge maintained past its usefulness. Look at the actual ledger. If the matter is genuinely settled, say so aloud and stop reopening it. If it is not, ask for what would settle it.""",
            Invocation: """Bride of the daybreak, who softens the eye of the judge — let the sun find me forgiven and awake."""),

        new Card(
            Id: 21, Suit: CardSuit.Goddess, Number: 21,
            Name: "Shala",
            Epithet: "Lady of the Furrow",
            Transliteration: "dŠala",
            Emblem: Emblem.GrainSheaf,
            AccentColor: "#C8A951", SecondaryColor: "#4A5B3C",
            Domains: L("the ploughed field", "yield", "rain and storm", "the weather you cannot control"),
            Lore: """Shala is the consort of Adad, the god of storm and rain, worshipped in northern Babylonia and Assyria and at Karkara. In art and in the star lists she is shown holding an ear of grain, and the constellation the Babylonians called Absin, the Furrow, is identified with her; it corresponds to our Virgo, and the star Spica is her ear of barley. The pairing is exact and unsentimental: in a land farmed by irrigation and visited by violent storms, the same weather that soaks the field can flatten it. She is the goddess of what the sky does to the work of your hands.""",
            UprightKeywords: L("yield", "weather beyond control", "harvest", "working with conditions", "abundance"),
            UprightMeaning: """Some of this is yours to determine and some of it is the weather. Shala asks you to be exact about which is which, because most exhaustion comes from trying to control the sky. Plough your furrow well: do the preparation, put the work in the ground, keep the channel clear. Then accept that rain and timing belong to somebody else, and stop rereading the forecast. There is real yield in this card — a harvest arriving from work you did seasons ago, possibly larger than you expect. Take it without apology, store some of it, and notice that the labour was not wasted even in the years it seemed to be.""",
            ReversedKeywords: L("washed out", "blaming the weather", "poor timing", "hoarding"),
            ReversedMeaning: """The storm took it, or you are treating the storm as a verdict on your worth. Reversed, Shala marks work undone by conditions nobody controlled — a market, an illness, a decision made elsewhere — and the shame that attaches to that as though it were failure. It can also be the opposite: blaming circumstances for a furrow you never actually ploughed. Distinguish honestly. Then do the small repair that is available, put something aside for the next lean stretch, and let the rest of it go without making it mean something about you.""",
            Invocation: """Lady of the furrow, whose ear of grain hangs in the summer sky — let the rain come at the hour my work is ready."""),

        new Card(
            Id: 22, Suit: CardSuit.Goddess, Number: 22,
            Name: "Damkina",
            Epithet: "Great Wife of the Deep",
            Transliteration: "dDam-gal-nun-na / Damkina",
            Emblem: Emblem.Turtle,
            AccentColor: "#3C7A89", SecondaryColor: "#12333B",
            Domains: L("the sheltered depth", "partnership with the wise", "hidden nurture", "Eridu"),
            Lore: """Damgalnunna, the great wife of the prince, called Damkina in Akkadian, is the consort of Enki-Ea and was worshipped beside him at Eridu, the oldest of the Sumerian cities. In the Enuma Elish she and Ea dwell in the Apsu, the chamber built over the vanquished fresh water, and there she bears Marduk, who is described as suckled by goddesses and perfect from birth. Her presence in the literature is quiet and structural rather than dramatic: she is the household of wisdom, the place where cleverness lives and is fed, and the mother of the god who will divide the sea and set the constellations in order.""",
            UprightKeywords: L("shelter", "the unseen support", "partnership", "raising something remarkable"),
            UprightMeaning: """Behind every visible cleverness there is a household that keeps it running, and this card asks you to value yours — either as the one who provides it or the one who is being carried by it. Damkina is the chamber in the deep where a thing can be raised out of sight until it is strong. If you are nurturing something early, keep it private a while longer; too much daylight now costs you nothing but momentum. If somebody has been the quiet structure under your work, this is the week to say so concretely, with money or time or a real change in the arrangement. Support that is never named tends to erode.""",
            ReversedKeywords: L("invisible labour", "self-erasure", "over-sheltered", "credit denied"),
            ReversedMeaning: """The household is holding everything and nobody has noticed. Reversed, Damkina is domestic or emotional work that has become genuinely invisible, including to the person doing it, until there is no self left outside the function. It can also be the other error — a talent kept sheltered so long that it never has to survive contact with the world. Ask which one is yours. Either name the labour and ask for the arrangement to change, or take the thing you have been protecting and let it out into the daylight.""",
            Invocation: """Great wife of the deep, whose quiet house holds the wisdom of the world — let what I shelter grow strong enough to leave."""),

        new Card(
            Id: 23, Suit: CardSuit.Goddess, Number: 23,
            Name: "Belet-Seri",
            Epithet: "Lady of the Steppe, Scribe of the Dead",
            Transliteration: "dBēlet-ṣēri",
            Emblem: Emblem.Gazelle,
            AccentColor: "#C2B280", SecondaryColor: "#3A3326",
            Domains: L("the record", "wilderness", "reckoning", "what is written down below"),
            Lore: """Belet-seri, the Lady of the Steppe, is the scribe of the netherworld in Akkadian tradition, and the Sumerian tradition identifies her with Geshtinanna. In Enkidu's dream in the Epic of Gilgamesh, the house of dust holds the kings with their crowns set aside, and there Belet-seri kneels before Ereshkigal and reads aloud from the tablet in her hand. The steppe in her name is the open country beyond the irrigated fields, the place of shepherds and of the dead; the same wilderness where Dumuzi is seized. She keeps the account of who has come down. Her office is not judgement. It is the making of an exact, unforgettable record.""",
            UprightKeywords: L("reckoning", "the record", "wilderness", "honest inventory", "memory"),
            UprightMeaning: """Take stock. Belet-seri does not scold and does not console; she reads out what is on the tablet, and there is a strange peace in that. Sit down and make the true list — what you have actually done this year, what was actually spent, who has actually gone, what you actually promised. Leave the interpretation until afterwards. This card often arrives when you are between things, out in the open country with no structure around you, and it says the emptiness is not wasted time if you use it to see clearly. Write your own account of the last chapter before someone else's version of it becomes the one you remember.""",
            ReversedKeywords: L("lost thread", "unrecorded", "wandering", "revising the past"),
            ReversedMeaning: """The record is being edited. Reversed, Belet-seri shows a history quietly rewritten to be more bearable — your part in it softened, someone else's part sharpened — or a stretch of life that has gone entirely unrecorded and now feels as if it did not happen. It can also be aimless wandering, the open country entered without water or intention. Go back to the primary sources: old messages, the calendar, the bank record, a friend who was there. Truth is duller than the story, and considerably easier to stand on.""",
            Invocation: """Lady of the open country, who reads the tablet in the house of dust — write my name down as it truly was."""),

        new Card(
            Id: 24, Suit: CardSuit.Goddess, Number: 24,
            Name: "Ashnan",
            Epithet: "Grain, Who Clothes and Feeds",
            Transliteration: "dAšnan",
            Emblem: Emblem.Loaves,
            AccentColor: "#D9B44A", SecondaryColor: "#6E5626",
            Domains: L("sustenance", "daily bread", "civilisation", "modest sufficiency"),
            Lore: """Ashnan is grain itself, made a goddess. The Sumerian Debate between Grain and Sheep tells how the Anuna gods, having no bread and no clothing, brought Ashnan and her sister Lahar down to the earth so that humankind could be fed and clothed, and how the two then quarrelled at length about which of them was greater. Enlil and Enki settle it in Ashnan's favour: the storehouse and the bread win, though the debate is generous to both. Grain in Sumer was money as well as food, reckoned in measures, lent, taxed and stored, so she stands at the root of the whole civil apparatus.""",
            UprightKeywords: L("sufficiency", "sustenance", "the ordinary good", "provision", "enough"),
            UprightMeaning: """There is enough, and the card wants you to actually feel that rather than merely concede it. Ashnan is not wealth; she is the loaf, the stocked cupboard, the rent covered for another month, the plain repeated meals that hold a life together. If you have been measuring your situation against an imagined one, stop and count what is genuinely on the shelf. Then do the small provisioning act: the grocery run, the standing order into savings, the meal cooked for the week. She also blesses the unglamorous work that feeds you while the interesting work matures. That job is not a betrayal of your ambitions. It is the granary they are standing in.""",
            ReversedKeywords: L("scarcity thinking", "waste", "never enough", "provision neglected"),
            ReversedMeaning: """The store is fine and the fear is not. Reversed, Ashnan is scarcity as a habit of mind — hoarding, or spending in bursts against a dread that never resolves, or the conviction that any amount would be too little. It can also be genuine neglect of provision, a life run with no reserve at all, where one small failure becomes an emergency. Both are treated the same way: count precisely, put something aside on a schedule rather than a mood, and eat proper meals at proper hours while you do it.""",
            Invocation: """Grain who came down to feed the gods and stayed to feed us — let my hand find the loaf and my house find enough."""),
    ];

    #endregion

    #region The Eight Gates (Suit Gate, I..VIII)

    private static readonly Card[] Gates =
    [
        new Card(
            Id: 25, Suit: CardSuit.Gate, Number: 1,
            Name: "The Shugurra Crown",
            Epithet: "First Gate: the Crown of the Steppe",
            Transliteration: "šu-gur-ra — the crown of the open country",
            Emblem: Emblem.Crown,
            AccentColor: "#D4AF37", SecondaryColor: "#2B2040",
            Domains: L("status", "the title you answer to", "first surrender", "authority laid down"),
            Lore: """At the first gate of the netherworld Neti, the chief doorman, takes the shugurra, the crown of the steppe, from Inanna's head. She asks what this means, and is told only that the ways of the underworld are perfect and may not be questioned. The sequence matters: before she loses anything intimate she loses the sign that announces her rank at a distance, the thing visible from across a courtyard. In the Sumerian poem she had put on all seven of the divine powers deliberately before setting out, dressing for the journey as for a coronation. The stripping is therefore not ambush. It is the toll.""",
            UprightKeywords: L("laying down rank", "anonymity", "first threshold", "humility chosen"),
            UprightMeaning: """The first thing asked of you is the title. Not the work, not the love, not the body — the label you use to be recognised before you have said anything. You are entering a place where being the senior one, the clever one, the well-reviewed one buys you nothing, and the sooner you take it off at the door the less it will be torn off later. Go somewhere you are nobody. Begin a discipline in which you are a beginner. Let a conversation happen without mentioning what you do. There is real loss in this and the card does not pretend otherwise, but underneath the crown is a head, and it has been a long time since you felt the air on it.""",
            ReversedKeywords: L("clinging to status", "identity threatened", "unable to begin again", "rank as armour"),
            ReversedMeaning: """You are trying to carry the crown through a door it does not fit. Reversed, this gate is the refusal to be ordinary: name-dropping to strangers, declining the class where you would be worst in the room, an identity so fused to a role that losing the role would feel like dying. It can also mark a rank taken from you unwillingly — a redundancy, a demotion, a retirement — and a grief that is being called something else. Say plainly what the title was doing for you. Then find out who you are in a room that has never heard of it.""",
            Invocation: """Doorman of the first gate, who lifts the crown before he lifts the bolt — let me set it down before it is taken."""),

        new Card(
            Id: 26, Suit: CardSuit.Gate, Number: 2,
            Name: "The Lapis Beads",
            Epithet: "Second Gate: the Measure at the Throat",
            Transliteration: "na4za-gìn di4-di4-lá — the small lapis beads",
            Emblem: Emblem.Beads,
            AccentColor: "#2A4B8D", SecondaryColor: "#1A1A2E",
            Domains: L("voice", "what you say about yourself", "wealth worn", "second surrender"),
            Lore: """At the second gate the small lapis beads are taken from Inanna's neck. Lapis lazuli reached Sumer from the mountains of Afghanistan along a trade route thousands of kilometres long, and in the poetry it is the standard of the precious: the beard of a god, the tablet of heaven, the neck of the beloved. Worn at the throat, the beads are wealth in its most portable and most visible form, and they sit exactly where the voice comes out. Again she asks what this means, and again the answer is that the ways of the netherworld are perfect. The gates do not explain themselves.""",
            UprightKeywords: L("plain speech", "letting go of ornament", "the unadorned voice", "second threshold"),
            UprightMeaning: """What goes next is the ornament at your throat — the way you have learned to say things so that they land well. This gate asks for speech with nothing hung on it: no hedging, no charm, no carefully assembled account of yourself. Say the sentence you have been decorating. Tell one person the unembellished version, including the part where you do not come off well. There is also a plainer reading: an expense you have been carrying for appearance's sake can go, and you will miss it less than you fear. What remains when the beads are off is your actual voice, which is lower and stranger than the one you use, and far more persuasive.""",
            ReversedKeywords: L("performed speech", "spin", "silenced", "keeping up appearances"),
            ReversedMeaning: """You are talking beautifully and saying nothing. Reversed, this gate is the polished version told so often it has replaced the memory, or a self-presentation kept up at a cost you no longer notice — the spending, the curating, the exhausting brightness. It can also be the opposite: a throat closed entirely, the true thing swallowed because the room rewards agreement. Both are the same refusal. Find the one person you can be unimpressive in front of, and be unimpressive there this week, out loud.""",
            Invocation: """Gate of the lapis at the throat, which takes the stone and leaves the voice — let me be heard without the blue."""),

        new Card(
            Id: 27, Suit: CardSuit.Gate, Number: 3,
            Name: "The Double Strand",
            Epithet: "Third Gate: the Beads upon the Breast",
            Transliteration: "nunuz-tab-ba — the twin egg-shaped beads of the breast",
            Emblem: Emblem.Necklace,
            AccentColor: "#3E6FB0", SecondaryColor: "#16162A",
            Domains: L("the heart's guard", "what you show of your feeling", "third surrender", "attachment"),
            Lore: """At the third gate the double strand of beads is removed from Inanna's breast. The Sumerian names them as twin egg-shaped stones, worn over the heart, and their doubling is part of their sense: two of a kind, a pairing, the ornament of a woman who is bound to somebody. Each gate has taken her a layer closer to the skin, from the crown at a distance, to the throat, to the chest. She protests at each, and is answered with the same sentence. The poem is constructed so that the reader feels the descent as an undressing that no one consents to twice.""",
            UprightKeywords: L("dropping the guard", "exposed feeling", "letting an attachment go", "third threshold"),
            UprightMeaning: """The covering over your heart is what is asked for now. That may mean an attachment you have worn so long it reads as part of your body — a relationship that ended years ago and is still furnishing your inner life, or a loyalty to a version of somebody who no longer exists. It may also mean simply being seen feeling something, in front of a person, without managing their reaction. Both are the same gate. Take the double strand off: say that you miss them, or that you are frightened, or that it mattered more than you let on. You will be colder for a while. You will also, for the first time in a long time, be touchable.""",
            ReversedKeywords: L("armoured heart", "old attachment kept", "performing composure", "unavailable"),
            ReversedMeaning: """The guard has been on so long that you have mistaken it for character. Reversed, this gate shows composure maintained through things that deserved to break it, intimacy managed at a fixed distance, or an old love kept polished precisely because a present one would require something of you. People near you report being fond of you and not knowing you. Start with one sentence of the unprotected kind, to one person, this week. The strand comes off in your own hands or eventually at the door.""",
            Invocation: """Gate of the twin stones above the heart — take the covering, and leave the beating where it is."""),

        new Card(
            Id: 28, Suit: CardSuit.Gate, Number: 4,
            Name: "The Breastplate",
            Epithet: "Fourth Gate: Come, Man, Come",
            Transliteration: "gaba-ĝu10 ĝá-nu ĝá-nu — the breastplate called 'come, man, come'",
            Emblem: Emblem.Breastplate,
            AccentColor: "#B08D57", SecondaryColor: "#14141F",
            Domains: L("allure as power", "seduction", "the fourth surrender", "protection removed"),
            Lore: """At the fourth gate the breastplate is taken from Inanna's chest. The Sumerian poem gives the ornament a name, which is a line of speech: come, man, come. It is at once armour and invitation, the paradox the goddess herself embodies, since the same figure is the mistress of battle and of desire. Scholars read it as a pectoral of the kind worn by rulers and by the divine in Mesopotamian art. In losing it she loses the piece that did two jobs at once, both shielding her chest and announcing her power to summon. Beyond this gate she has no defence that also flatters.""",
            UprightKeywords: L("giving up seduction", "disarming", "power set aside", "fourth threshold"),
            UprightMeaning: """You are being asked to stop winning this the way you usually win things. The breastplate is whatever in you both protects and attracts — charm, competence, being needed, the ability to make a room turn. It has worked, which is why it is hard to put down, and it is exactly what is not admitted through this door. Approach the person or the situation without the technique. Ask directly instead of making them want to offer. Say I need help rather than arranging to be indispensable. This is the most frightening of the early gates, because what is being taken is the strategy, and what is left is just you asking.""",
            ReversedKeywords: L("manipulation", "armour that seduces", "indirect asking", "fear of being plain"),
            ReversedMeaning: """The invitation is still doing the work of the request. Reversed, this gate is the indirect approach kept up past its usefulness: hinting rather than asking, being charming at people you are actually angry with, making yourself necessary so that nobody can leave. It can also be armour that has fused to allure so completely that you no longer know whether you want the person or want to be chosen by them. Try the flat sentence. Say the want, plainly, once, and let the answer be an answer.""",
            Invocation: """Gate of the breastplate that both called and shielded — take the invitation, and leave me my plain hands."""),

        new Card(
            Id: 29, Suit: CardSuit.Gate, Number: 5,
            Name: "The Gold Ring",
            Epithet: "Fifth Gate: the Ring from the Hand",
            Transliteration: "ḫar kù-sig17 — the golden ring of the hand",
            Emblem: Emblem.Ring,
            AccentColor: "#E0B84C", SecondaryColor: "#101018",
            Domains: L("agency", "the bond", "what you can still do", "fifth surrender"),
            Lore: """At the fifth gate the gold ring is taken from Inanna's hand. In Mesopotamian usage a ring is both ornament and instrument: rings of precious metal were measured by weight and functioned as wealth, and the ring and rod appear together in divine investiture scenes as the emblems handed to a ruler. The hand is the part of the body that acts, seals and signs. Having already lost rank, voice, heart and allure, she now loses the token of her capacity to transact at all. The poem's order is a careful dismantling: outward signs first, then feeling, then power, then measure.""",
            UprightKeywords: L("surrendering control", "hands empty", "a bond released", "fifth threshold"),
            UprightMeaning: """This gate takes the ring off the hand that acts. You are in a situation where doing more is no longer available to you, and the work is to stop trying to grip it. Perhaps you have submitted the application, said the thing, made the offer, and the outcome now belongs to other people. Perhaps a bond is ending and the signing hand has to be opened. Either way the instruction is the same and it is bodily: unclench. Put the phone down, close the file, stop composing follow-ups. An empty hand is not a defeated hand. It is the only kind that can be given anything.""",
            ReversedKeywords: L("grasping", "control at any cost", "meddling", "unable to release"),
            ReversedMeaning: """The hand is closed around something that has already been decided. Reversed, this gate shows control exercised past the point of usefulness — rechecking, renegotiating, managing people who did not ask to be managed, a bond held onto by administration alone. It exhausts you and it insults them. It can also be the panic of losing agency in a real way, through illness or dependence. The response is not resignation but accuracy: list what is genuinely still in your hands, do those things well, and physically let go of the rest.""",
            Invocation: """Gate of the ring, which takes the hand's authority and leaves the hand — teach me to hold with my palm open."""),

        new Card(
            Id: 30, Suit: CardSuit.Gate, Number: 6,
            Name: "The Measuring Rod and Line",
            Epithet: "Sixth Gate: the Lapis Rule",
            Transliteration: "gi-diš-nindan za-gìn u3 eš-gána — the lapis rod and the measuring line",
            Emblem: Emblem.MeasuringRod,
            AccentColor: "#1F3A93", SecondaryColor: "#0B0B12",
            Domains: L("judgement", "standards", "the right to measure", "sixth surrender"),
            Lore: """At the sixth gate the lapis measuring rod and line are taken from Inanna's hand. The rod and line are the surveyor's tools, used to lay out fields, canals and temple foundations, and in Mesopotamian iconography they are handed by a god to a king as the emblems of just rule: the power to establish boundaries and to say what is straight. To hold them is to be the one who decides the measure. Their removal is the last of the seven that carries authority; only the robe remains. She has come down asking questions at every gate, and has been told seven times that the rites of the netherworld are perfect.""",
            UprightKeywords: L("suspending judgement", "not knowing", "standards released", "sixth threshold"),
            UprightMeaning: """You must go the rest of the way without the ruler. This gate takes your right to evaluate — to grade your own day, to rank the people involved, to decide whether this is going well. You are too close and too tired to measure accurately anyway, and the measuring itself has become the suffering. For a defined period, refuse the question. Do not ask whether you are behind, whether it was worth it, whether you are doing better than last year. Simply continue and observe. Something is being built in you to dimensions you did not set, and the only way to find out what it is will be to stop checking whether it matches the plan.""",
            ReversedKeywords: L("harsh measuring", "comparison", "perfectionism", "grading the ungradeable"),
            ReversedMeaning: """You are running the rule over a thing that has no straight edge. Reversed, this gate is perfectionism and comparison: the daily audit of your worth, the scoreboard against people whose circumstances you do not know, standards applied to grief or love or recovery as though they were masonry. It can also be judgement turned outward, a habit of appraising everyone. Put the rod down for a stated period. Replace every verdict with a description, and see how much of the pain was in the measuring.""",
            Invocation: """Gate of the lapis rule, which takes the right to judge — let me walk unmeasured to the bottom of the stair."""),

        new Card(
            Id: 31, Suit: CardSuit.Gate, Number: 7,
            Name: "The Royal Robe",
            Epithet: "Seventh Gate: the Pala of Ladyship",
            Transliteration: "túg-pala3 — the pala robe of ladyship",
            Emblem: Emblem.Robe,
            AccentColor: "#7A2E4E", SecondaryColor: "#08080C",
            Domains: L("nakedness", "the end of protection", "total exposure", "seventh surrender"),
            Lore: """At the seventh gate the pala robe, the garment of ladyship, is taken from Inanna's body, and she enters the throne room naked and bowed low. Ereshkigal rises; Inanna sits on the throne; the seven Anuna judges fix their eyes on her, and Ereshkigal fastens on her the eye of death, speaks against her the word of wrath, utters against her the cry of guilt, and she is turned into a corpse and hung on a hook. Three days and three nights pass. It is the flattest, most terrible passage in Sumerian literature, told without any softening at all.""",
            UprightKeywords: L("nakedness", "nothing left to lose", "the bottom", "seventh threshold", "stark truth"),
            UprightMeaning: """Here you have nothing on. This is the card of the moment when the last covering goes — the job, the marriage, the health, the belief you were managing — and you find yourself in the room with no version of the story that saves you. It is genuinely terrible and it is genuinely a gate. What the myth insists on, and what you will not be able to feel from inside it, is that this is a place and not a permanent condition. Do not decide anything here. Do not sign, move, promise or renounce. Simply survive the three days, keep breathing, eat something, and let someone know where you are. The bottom is where the rescue is sent.""",
            ReversedKeywords: L("dread of exposure", "hiding at the last door", "shame", "postponed collapse"),
            ReversedMeaning: """You are at the seventh door with one hand on the robe. Reversed, this gate is the enormous effort of keeping the last covering on — the debt nobody knows about, the drinking, the marriage that ended internally two years ago — and the exhaustion of maintaining a front over a hollow. Shame is doing the work here, and shame is very expensive. Tell one person the whole of it. Not the room: one person, chosen well. What you are dreading has already happened; all that remains is whether you go through it alone.""",
            Invocation: """Gate of the robe, last door, plainest hour — let me be seen with nothing on and still be carried out."""),

        new Card(
            Id: 32, Suit: CardSuit.Gate, Number: 8,
            Name: "The Return",
            Epithet: "Eighth Gate: the Water and the Food of Life",
            Transliteration: "a nam-til3-la u3 ú nam-til3-la — the water of life and the food of life",
            Emblem: Emblem.Gate,
            AccentColor: "#F2E8D5", SecondaryColor: "#123A34",
            Domains: L("rescue", "return", "empathy", "the substitute", "the price of coming back"),
            Lore: """After three days Ninshubur keeps her instructions, and Enki alone acts. From the dirt under his fingernails he makes the kurgarra and the galatur, two creatures of no gender and no standing, and sends them down through the cracks in the door with the food of life and the water of life. They find Ereshkigal groaning in labour pains, and they do the only thing asked of them: they echo her. When she cries her insides, they cry her insides. Disarmed, she offers gifts; they ask instead for the corpse on the hook. Inanna rises, but the galla come up with her, and someone must go down in her place.""",
            UprightKeywords: L("return", "being echoed", "rescue arrives", "the cost of coming back", "mercy"),
            UprightMeaning: """You come back up, and you do not come back unchanged or unaccompanied. This card promises the turn — the help arriving from an unlikely quarter, small and unofficial, not the powerful friends who refused but the odd ones who simply sat with you and said your grief back to you until it softened. Accept that kind of help; it is the kind that works. Then look at the cost honestly. Something has to be left behind or given up for this return to hold: a habit, a role, a relationship that belongs to the underworld part of your life. The galla come up with you. Decide deliberately what you send back down.""",
            ReversedKeywords: L("returning unchanged", "someone else pays", "false recovery", "unpaid cost"),
            ReversedMeaning: """You are up, but the account has not been settled. Reversed, this gate is the recovery declared too early — back at work, back in the relationship, back to normal, with nothing actually relinquished — or a return whose price has quietly been paid by somebody else, as Dumuzi paid for Inanna. Look at who is carrying what you put down. If you have been rescued, do the plainer thing: name what must change, change it, and go and sit with the person who sat with you.""",
            Invocation: """Two who came through the cracks in the door with nothing but an echo — cry my grief back to me until it lets me go."""),
    ];

    #endregion

    #region The Sacred Emblems (Suit Emblem, I..VIII)

    private static readonly Card[] Emblems =
    [
        new Card(
            Id: 33, Suit: CardSuit.Emblem, Number: 1,
            Name: "The Eight-Pointed Star",
            Epithet: "Venus, Morning and Evening",
            Transliteration: "mul Dilbat — the star of Inanna",
            Emblem: Emblem.EightPointedStar,
            AccentColor: "#F0D27A", SecondaryColor: "#1B2A5E",
            Domains: L("Venus", "cycles", "brilliance", "the two faces of one thing"),
            Lore: """The eight-pointed star is the sign of Inanna-Ishtar and of the planet Venus, called Dilbat in Akkadian. It appears on cylinder seals, on boundary stones beside the crescent of Sin and the disc of Shamash, and on temple ornament across three thousand years. Babylonian astronomers tracked Venus closely; the Venus tablet of Ammisaduqa records its risings and settings as omens. The planet's behaviour is the theology: it appears as the morning star, vanishes, and returns as the evening star, so that the same body is both the herald of dawn and the light of the going-down. One goddess, two appearances, and an interval of absence between them.""",
            UprightKeywords: L("brilliance", "a cycle turning", "visibility", "both faces owned", "return"),
            UprightMeaning: """You are one thing with two appearances, and this card asks you to stop apologising for the second one. The tender version and the formidable version of you are the same planet on different sides of the sun. Bring them into the same room: be gentle where you are usually impressive, or firm where you are usually accommodating, and notice that nothing breaks. The star also marks timing. Something is due to reappear — a person, an ambition, a capacity that went dark — and the disappearance was never a failure, only the part of the cycle that happens below the horizon. Watch the sky at the right hour. It has kept better time than you have.""",
            ReversedKeywords: L("split self", "eclipsed", "dazzle without warmth", "mistimed"),
            ReversedMeaning: """The two faces have been separated and one is being kept hidden. Reversed, the star is a life divided by audience — a self for work and a self for home with no traffic between them — or a brilliance that dazzles and gives no heat, admired by people who do not know you. It can also mark a low interval mistaken for an ending, the weeks under the horizon when you conclude the light is gone for good. Wait it out and keep the record. The star has vanished before, on schedule, and come back on schedule.""",
            Invocation: """Star of the two horizons, one light in two skies — let my hidden half rise with my bright one."""),

        new Card(
            Id: 34, Suit: CardSuit.Emblem, Number: 2,
            Name: "The Rosette",
            Epithet: "The Whorl of Petals",
            Transliteration: "the rosette of the Lady",
            Emblem: Emblem.Rosette,
            AccentColor: "#C77B9E", SecondaryColor: "#33203F",
            Domains: L("wholeness", "pattern", "many petals one centre", "sacred ornament"),
            Lore: """The rosette is among the most persistent motifs in Mesopotamian art, worked in gold leaf and shell inlay, set into temple façades, carved on ivories and worn as jewellery, and it is associated above all with Inanna-Ishtar; Neo-Assyrian reliefs put rosettes on the king's wristbands and in the hands of protective figures. What it depicted has been argued over — a flower, a star seen as petals, a whorl of light — and the ambiguity is probably original. Its structure is its meaning: identical petals turning around a single fixed centre, the many held by the one. The spread you are reading takes its name and its shape from it.""",
            UprightKeywords: L("wholeness", "pattern recognised", "centre holding", "beauty as order"),
            UprightMeaning: """Step back far enough to see the pattern. What has felt like a series of unrelated difficulties this year is a set of petals around one centre, and the centre has a name you have been avoiding saying. Ask what single thing all of it is about, and be willing for the answer to be simple and slightly embarrassing. This card also asks you to trust structure. A rhythm, a rule, a repeated form is not a cage; it is the thing that lets the petals stay open. Set one regular practice this month — the same hour, the same walk, the same page — and let the beauty come from repetition rather than from inspiration.""",
            ReversedKeywords: L("fragmentation", "decoration over substance", "no centre", "same mistake repeating"),
            ReversedMeaning: """The petals have come loose from the middle. Reversed, the rosette is a life of good parts with nothing at the centre to hold them — worthy commitments in six directions, none of them chosen — or the appearance of order standing in for order itself. It can also be the darker pattern: the same relationship, the same argument, the same exit, repeating with new names. Look for the repetition and say it aloud. Once the shape is named it stops being fate and becomes a decision.""",
            Invocation: """Whorl of petals turning on one still point — gather my scattered days around their own centre."""),

        new Card(
            Id: 35, Suit: CardSuit.Emblem, Number: 3,
            Name: "The Reed-Bundle Gatepost",
            Epithet: "The Standard of Inanna",
            Transliteration: "mùš / šutum — the looped reed bundle",
            Emblem: Emblem.ReedBundle,
            AccentColor: "#C2A15B", SecondaryColor: "#4A3A22",
            Domains: L("threshold", "consecration", "boundary", "marking a door"),
            Lore: """The looped bundle of reeds is Inanna's oldest emblem, so closely identified with her that the cuneiform sign derived from it writes her name. It appears already in the archaic tablets from Uruk and on the great Uruk Vase, where a pair of the standards stand at the temple door as the procession approaches. In practice the ring-post was a real object: bundled marsh reeds bound and looped at the top, set at the entrance of a storehouse or a shrine, both a structural post and a claim of ownership. Made of the most ordinary material in southern Iraq, it marked the boundary between common ground and holy ground.""",
            UprightKeywords: L("marking a threshold", "consecration", "ordinary made sacred", "boundary"),
            UprightMeaning: """Put a post at the door. This card asks you to mark a boundary physically rather than merely intend one — a time that belongs to the work, a room nobody else enters, an hour when the phone is in another part of the house. The reeds were nothing special; the marking was everything. In the same spirit, take something plainly ordinary in your life and consecrate it by treating it as though it mattered: the meal, the morning, the desk. You are also being invited over a threshold. Something is genuinely beginning, and it wants a small ceremony rather than a slow slide. Name the day. Say aloud that this is where it starts.""",
            ReversedKeywords: L("no boundary", "everything porous", "profaned space", "an unmarked beginning"),
            ReversedMeaning: """There is no post at the door and everyone walks through. Reversed, the reed bundle is the missing boundary — work in the bed, family in the workday, a home that has become a thoroughfare — and the low, constant fatigue of never being off. It can also mean a beginning that was never marked, so that you cannot tell whether you have started or not. Build the post today, and make it visible: a closed door, a stated hour, a sentence other people can be told. Unmarked ground is treated as common ground.""",
            Invocation: """Reeds bound and looped at the temple door — stand at my threshold and let nothing cross without my word."""),

        new Card(
            Id: 36, Suit: CardSuit.Emblem, Number: 4,
            Name: "The Lion",
            Epithet: "The Beast Beneath Her Feet",
            Transliteration: "ur-maḫ — the lion of Ishtar",
            Emblem: Emblem.Lion,
            AccentColor: "#B5651D", SecondaryColor: "#2B1B12",
            Domains: L("courage", "controlled force", "royal fierceness", "the anger that serves"),
            Lore: """The lion is Ishtar's animal. Seals and reliefs show her standing on a lion or leading one on a leash, and the Ishtar Gate of Babylon was lined with striding lions in glazed brick along the processional way to the Esagil. Lions were real in Mesopotamia and genuinely dangerous, and the royal lion hunt was a standing image of the king's duty to hold chaos off the fields. Enheduanna's hymn compares the goddess herself to a lion loosed upon the land. The emblem's force comes from the leash: not the absence of ferocity, and not its riot, but a predator walking beside the one who owns it.""",
            UprightKeywords: L("courage", "force under command", "protective anger", "authority"),
            UprightMeaning: """Take the leash in your hand. There is anger in you with a legitimate cause, and the card says neither swallow it nor let it off into the street — walk it. Ferocity that has an owner is the most useful thing you can bring to this situation: it makes the complaint, defends the person who cannot defend themselves, ends the arrangement that has been quietly eating you. Say it in a level voice and do not soften the content. You may also simply need courage of the plain kind this week, the walking-into-the-room sort. Remember that the animal beside you is yours. It has always been yours, and it has been waiting to be asked.""",
            ReversedKeywords: L("rage off the leash", "intimidation", "cowardice", "borrowed ferocity"),
            ReversedMeaning: """The lion is loose or the lion is caged. Reversed, this card is force that has slipped its owner — the disproportionate response, the fury that lands on whoever is nearest and safest — or the opposite, a ferocity so feared that it has been locked away and now leaks out as sarcasm and sudden tears. It may also be someone using their temper to govern a room you are in. Name which of these it is. Then take the smallest real action: one level sentence, said in daylight, about the actual grievance.""",
            Invocation: """Lion at her heel, fury that answers to a name — walk beside me, and do not go ahead."""),

        new Card(
            Id: 37, Suit: CardSuit.Emblem, Number: 5,
            Name: "The Date Palm",
            Epithet: "The Tree That Feeds the City",
            Transliteration: "ĝišnimbar — the date palm",
            Emblem: Emblem.DatePalm,
            AccentColor: "#8FA33F", SecondaryColor: "#5A3A1E",
            Domains: L("sustenance over years", "sweetness", "long planting", "the orchard"),
            Lore: """The date palm was the great cultivated tree of southern Mesopotamia, giving fruit, syrup, fibre, timber and shade in a land with little of any of them, and it was propagated by hand: growers pollinated the female trees deliberately, and orchards were valuable enough to be regulated in the law collections. A palm takes years to bear and then bears for decades, which made orchards a form of inherited wealth regulated in the law collections. Dumuzi, Inanna's bridegroom, carries the epithet Amaushumgalanna, understood as the one great source of the date clusters, binding the shepherd-lover of the courtship songs to the harvest of the palm.""",
            UprightKeywords: L("long investment", "sweetness earned", "inheritance", "patience rewarded"),
            UprightMeaning: """Plant the slow thing. The date palm does not oblige the person who sets it, and that is precisely why it is worth setting: the study that takes four years, the savings that mean nothing for a decade, the relationship built on Tuesdays rather than on declarations. This card says your long commitments are sound and asks you not to dig them up to check the roots. It also brings real sweetness — a pleasure arriving now from work you have forgotten doing, or an inheritance in the wide sense, something handed to you by someone who planted without expecting to eat. Receive it consciously, and put something in the ground for whoever comes after.""",
            ReversedKeywords: L("impatience", "living off the orchard", "neglected roots", "sweetness deferred forever"),
            ReversedMeaning: """You are eating from a tree you are not tending. Reversed, the palm is inherited advantage taken as ordinary, a reputation or relationship coasting on old effort, or an orchard of half-started plantings none of which was ever watered long enough to bear. It can also be the sadder pattern of endless deferral, sweetness always scheduled for after the next thing. Choose one planting and commit to it for a stated number of years. Then take one pleasure today, without earning it, so that the future does not swallow the present entirely.""",
            Invocation: """Palm of the long patience, whose fruit comes to the children of the planter — let me set what I will not taste."""),

        new Card(
            Id: 38, Suit: CardSuit.Emblem, Number: 6,
            Name: "The Dove",
            Epithet: "The Bird of the Goddess",
            Transliteration: "tu-gur4mušen — the dove",
            Emblem: Emblem.Dove,
            AccentColor: "#E7E3DA", SecondaryColor: "#6E7B8B",
            Domains: L("tenderness", "the message sent out", "homing", "peace after a flood"),
            Lore: """Doves were associated with Ishtar across the Near East; clay and terracotta dove figurines turn up in her shrines, and the bird appears in her iconography and in temple offering lists. In the flood narrative of the Epic of Gilgamesh, Utnapishtim releases a dove from the ark, and it finds no resting place and comes back; then a swallow, which also returns; then a raven, which does not, and by that he knows the waters have gone down. The dove is thus the bird of return, sent out over a flooded world to learn whether the ordinary earth has come back yet.""",
            UprightKeywords: L("tenderness", "sending out a signal", "coming home", "gentleness after disaster"),
            UprightMeaning: """Send the dove. After a hard stretch the first useful act is small and exploratory: one message to the friend you went quiet on, one enquiry, one afternoon out of the house. You are not required to know whether the water has gone down. You are required to send something out and see what comes back, and it is entirely acceptable if the first bird returns having found nothing. This card also asks for gentleness as a method rather than a mood — with yourself first, in the specific way of lowering the standard for a week, and then with one other person who is having a worse time than they have said.""",
            ReversedKeywords: L("nothing sent", "withdrawal", "false peace", "tenderness withheld"),
            ReversedMeaning: """The bird is in the ark and the hatch is shut. Reversed, the dove is a withdrawal that began as recovery and has set into a habit — no messages sent, invitations declined by reflex, the world assumed to be still underwater because you have not checked. It can also be a peace kept by not raising anything, which is quiet but is not peace. Send one small thing this week, and let it be genuinely small. You do not have to explain the silence to begin ending it.""",
            Invocation: """Bird sent out over the water, who returns with or without the branch — carry my small word past the edge of the flood."""),

        new Card(
            Id: 39, Suit: CardSuit.Emblem, Number: 7,
            Name: "The Storehouse",
            Epithet: "The Ganun of the Shepherd",
            Transliteration: "ĝá-nun — the storehouse",
            Emblem: Emblem.Storehouse,
            AccentColor: "#C89F5C", SecondaryColor: "#4C3A24",
            Domains: L("abundance", "the shared household", "reserve", "the fruits of union"),
            Lore: """The courtship songs of Inanna and Dumuzi turn on the storehouse. He comes to her door as the shepherd with milk and cream and the first fruits, and the poems dwell on the filling of the ganun and on the prosperity of the land that follows their union; the sacred marriage texts make the fertility of the fields and folds the direct consequence of the bridegroom being received. In the Descent the same storehouse motif darkens, for Dumuzi is found not mourning but seated in state, and it is at the sheepfold that the galla take him. The storehouse is what union produces and what it can cost.""",
            UprightKeywords: L("abundance", "shared prosperity", "reserve built", "generosity", "union that yields"),
            UprightMeaning: """What you have built together is real, and this is the season to fill the storehouse rather than to spend it. The card points at partnership of any kind — a marriage, a business, a friendship of long standing — and says the arrangement is producing more than either of you would alone. Put some of it by. Make the boring provision: the savings, the contract, the written-down understanding of who does what. Generosity is also indicated, specifically the kind that gives from surplus rather than from the last of it. Open the door to somebody. A full storehouse that nobody enters turns, in the older story, into the place where the demons find you sitting.""",
            ReversedKeywords: L("hoarding", "complacency", "spent reserve", "enthroned while others grieve"),
            ReversedMeaning: """The doors are shut on a full house. Reversed, the storehouse is abundance that has stopped circulating — wealth, time or attention held against a fear, comfort so settled that it has made you incurious about anyone outside. In the myth this is Dumuzi on his throne while his wife is on a hook, and the reproach is not subtle. It can also be simple depletion, a reserve quietly spent while the outward display continues. Open the accounts and open the door. Give something specific to someone specific this week.""",
            Invocation: """Storehouse whose doors the shepherd opened at dawn — let what is gathered here be eaten by more than two."""),

        new Card(
            Id: 40, Suit: CardSuit.Emblem, Number: 8,
            Name: "The Boat of Heaven",
            Epithet: "The Vessel of the Me",
            Transliteration: "má-an-na — the boat of heaven",
            Emblem: Emblem.Boat,
            AccentColor: "#3FA8A0", SecondaryColor: "#123B4A",
            Domains: L("acquisition of power", "bringing the gift home", "wit", "holding what was won"),
            Lore: """In the Sumerian poem Inanna and Enki, the goddess travels to Eridu to visit Enki, the keeper of the me — the divine powers that hold civilisation together, from kingship and priesthood to the crafts, the arts, the sexual professions and the making of decisions. Enki drinks with her, and in his good cheer hands over the me one set after another. She loads them onto the Boat of Heaven and sets off for Uruk. Sobered, Enki sends his minister Isimud and successive waves of creatures to take the boat back, and seven times Ninshubur fights them off. The me arrive at Uruk, and the city rejoices.""",
            UprightKeywords: L("claiming what is offered", "transport", "wit over rank", "bringing it home", "audacity"),
            UprightMeaning: """Take what is being offered, and then get it home. This card arrives when an opportunity has been extended by someone who may reconsider — a promise made warmly, an offer floated in a good mood, a door opened by a person with the authority to open it. Do not wait for a better-worded invitation. Accept clearly, get it in writing, and start moving. The second half matters as much as the first: the me were won at Eridu but nearly lost on the water, and it was the steady minister who kept them. Whatever you have been given, secure the practical logistics and keep the loyal people close. Getting it is not the same as landing it.""",
            ReversedKeywords: L("the offer withdrawn", "no follow-through", "overreach", "cargo lost on the water"),
            ReversedMeaning: """Something was won and is being lost in transit. Reversed, the Boat of Heaven is the opportunity accepted and never acted on, the agreement that dissolved because nobody wrote it down, or the gift reclaimed by a giver who regretted it once sober. It can also be overreach — taking more than you can pilot, and being pursued for it. Look at the logistics rather than the prize. Confirm in writing, name the dates, and ask the one steady person you trust to guard the crossing with you.""",
            Invocation: """Boat of heaven riding low with the powers of the world — bring my cargo past the seventh pursuit and into the quay."""),
    ];

    #endregion
}
