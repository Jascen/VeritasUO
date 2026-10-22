namespace Server.Items;

public enum PageRarity
{
    Common,
    Uncommon,
    Rare,
    UltraRare
}

/// <summary>A run of consecutive pages that drops as one item; each page is +1 skill.</summary>
public readonly record struct TomeSection(int FirstPage, int PageCount, PageRarity Rarity)
{
    public int LastPage => FirstPage + PageCount - 1;

    // Bit (n - 1) stands for page n.
    public int Mask => ((1 << PageCount) - 1) << (FirstPage - 1);
}

public static class SkillTomeInfo
{
    public const int PageCount = 20;

    // 5 common + 4 uncommon single pages, 2 rare 3-page folios, 1 ultra rare 5-page folio = 20 pages.
    public static readonly TomeSection[] Sections =
    [
        new(1, 1, PageRarity.Common),
        new(2, 1, PageRarity.Common),
        new(3, 1, PageRarity.Common),
        new(4, 1, PageRarity.Common),
        new(5, 1, PageRarity.Common),
        new(6, 1, PageRarity.Uncommon),
        new(7, 1, PageRarity.Uncommon),
        new(8, 1, PageRarity.Uncommon),
        new(9, 1, PageRarity.Uncommon),
        new(10, 3, PageRarity.Rare),
        new(13, 3, PageRarity.Rare),
        new(16, 5, PageRarity.UltraRare)
    ];

    public static bool IsValidSection(int section) => section >= 0 && section < Sections.Length;

    /// <summary>Indexes into <see cref="Sections"/> of the first and last section of a rarity.</summary>
    public static (int First, int Last) GetSectionRange(PageRarity rarity) => rarity switch
    {
        PageRarity.Common   => (0, 4),
        PageRarity.Uncommon => (5, 8),
        PageRarity.Rare     => (9, 10),
        _                   => (11, 11)
    };

    // What a drop physically is, so the multi-page rare and ultra rare drops read as more than a single sheet.
    public static string GetMaterialName(PageRarity rarity) => rarity switch
    {
        PageRarity.Common   => "Scribblings",
        PageRarity.Uncommon => "Torn Journal Page",
        PageRarity.Rare     => "Bundled Notes",
        _                   => "Lost Manuscript"
    };

    public static string GetRarityName(PageRarity rarity) => rarity switch
    {
        PageRarity.Common   => "Common",
        PageRarity.Uncommon => "Uncommon",
        PageRarity.Rare     => "Rare",
        _                   => "Ultra Rare"
    };

    public static int GetRarityHue(PageRarity rarity) => rarity switch
    {
        PageRarity.Common   => 0x47E,
        PageRarity.Uncommon => 0x59,
        PageRarity.Rare     => 0x2B,
        _                   => 0x501
    };

    public static string GetRarityColor(PageRarity rarity) => rarity switch
    {
        PageRarity.Common   => "#FFFFFF",
        PageRarity.Uncommon => "#4FA3FF",
        PageRarity.Rare     => "#FFD700",
        _                   => "#FF66FF"
    };

    public static bool IsAvailable(SkillName skill) => skill switch
    {
        SkillName.Necromancy or SkillName.Focus or SkillName.Chivalry  => Core.AOS,
        SkillName.Bushido or SkillName.Ninjitsu                        => Core.SE,
        SkillName.Spellweaving                                         => Core.ML,
        SkillName.Mysticism or SkillName.Imbuing or SkillName.Throwing => Core.SA,
        _                                                              => true
    };

    public static string GetSkillName(SkillName skill) => SkillInfo.Table[(int)skill].Name;

    /// <summary>
    /// The writings behind each skill's tome: its author, the bound tome's title, and a lore line.
    /// </summary>
    public static (string Author, string Title, string Lore) GetFlavor(SkillName skill) => skill switch
    {
        SkillName.Alchemy       => ("Paracelsus", "Paracelsus' Book of Elixirs", "Paracelsus may have brewed these tinctures in secret."),
        SkillName.Anatomy       => ("Andreas Vesalius", "Vesalius' Anatomical Atlas", "Andreas Vesalius may have drawn these from a body no one claimed."),
        SkillName.AnimalLore    => ("Pliny the Elder", "Pliny's Lost Bestiary", "Pliny the Elder may have left these beasts out of his Natural History."),
        SkillName.ItemID        => ("Marco Polo", "Marco Polo's Merchant Journal", "Marco Polo may have valued eastern treasures with this eye."),
        SkillName.ArmsLore      => ("Leonardo da Vinci", "Leonardo's Codex of Arms", "Leonardo da Vinci may have sketched these weapons in a margin."),
        SkillName.Parry         => ("Johannes Liechtenauer", "Liechtenauer's Book of Guards", "Johannes Liechtenauer may have taught these guards to his students."),
        SkillName.Begging       => ("Diogenes", "Diogenes' Book of Pleas", "Diogenes may have begged alms from a statue with these lines."),
        SkillName.Blacksmith    => ("Masamune", "Masamune's Forge Journal", "Masamune may have folded steel by this secret method."),
        SkillName.Fletching     => ("Roger Ascham", "Ascham's Treatise on the Shaft", "Roger Ascham may have fletched his arrows by this method."),
        SkillName.Peacemaking   => ("Confucius", "Confucius' Book of Harmony", "Confucius may have stilled a quarrel with these gentle airs."),
        SkillName.Camping       => ("Xenophon", "Xenophon's March Journal", "Xenophon may have made camp this way on the long march home."),
        SkillName.Carpentry     => ("Thomas Chippendale", "Chippendale's Pattern Book", "Thomas Chippendale may have cut these joints for a royal cabinet."),
        SkillName.Cartography   => ("Gerardus Mercator", "Mercator's Atlas", "Gerardus Mercator may have mapped the world by this method."),
        SkillName.Cooking       => ("Apicius", "Apicius' Book of Recipes", "Apicius may have served these dishes at an emperor's feast."),
        SkillName.DetectHidden  => ("Eugene Vidocq", "Vidocq's Casebook", "Eugene Vidocq may have found many a hidden thief this way."),
        SkillName.Discordance   => ("Nero", "Nero's Book of Jarring Songs", "Nero may have played these jarring tunes while Rome burned."),
        SkillName.EvalInt       => ("Aristotle", "Aristotle's Treatise on the Soul", "Aristotle may have judged the minds of men by this method."),
        SkillName.Healing       => ("Hippocrates", "Hippocrates' Book of Remedies", "Hippocrates may have whispered these cures to a single student."),
        SkillName.Fishing       => ("Izaak Walton", "Walton's Angling Journal", "Izaak Walton may have landed his finest catch with these casts."),
        SkillName.Forensics     => ("Song Ci", "Song Ci's Casebook", "Song Ci may have uncovered a murder by this method."),
        SkillName.Herding       => ("Columella", "Columella's Shepherd's Manual", "Columella may have driven his flocks by these calls."),
        SkillName.Hiding        => ("Nicholas Owen", "Owen's Book of Hiding Places", "Nicholas Owen may have built a priest hole by these plans."),
        SkillName.Provocation   => ("Niccolo Machiavelli", "Machiavelli's Treatise on Discord", "Niccolo Machiavelli may have turned rivals on each other this way."),
        SkillName.Inscribe      => ("Johannes Gutenberg", "Gutenberg's Book of Letters", "Johannes Gutenberg may have set these letters before his press."),
        SkillName.Lockpicking   => ("Harry Houdini", "Houdini's Escape Journal", "Harry Houdini may have slipped many locks with these tricks."),
        SkillName.Magery        => ("Merlin", "Merlin's Grimoire", "Merlin may have scrawled these incantations in a margin."),
        SkillName.MagicResist   => ("Odysseus", "Odysseus' Journal of Wards", "Odysseus may have shrugged off Circe's spells with this secret."),
        SkillName.Tactics       => ("Sun Tzu", "Sun Tzu's Lost Treatise", "Sun Tzu may have left these lessons out of The Art of War."),
        SkillName.Snooping      => ("Francis Walsingham", "Walsingham's Cipher Book", "Francis Walsingham may have read many a sealed letter this way."),
        SkillName.Musicianship  => ("Orpheus", "Orpheus' Songbook", "Orpheus may have played these tunes at the gates of the underworld."),
        SkillName.Poisoning     => ("Locusta", "Locusta's Book of Draughts", "Locusta may have mixed these draughts for an emperor."),
        SkillName.Archery       => ("Yang Youji", "Yang Youji's Archery Manual", "Yang Youji may have split a willow leaf with this draw."),
        SkillName.SpiritSpeak   => ("the Pythia", "The Pythia's Book of Utterances", "The Oracle of Delphi may have heard the dead speak these words."),
        SkillName.Stealing      => ("Robin Hood", "Robin Hood's Ledger", "Robin Hood may have lightened many purses with these tricks."),
        SkillName.Tailoring     => ("Arachne", "Arachne's Pattern Book", "Arachne may have woven these patterns before she angered Athena."),
        SkillName.AnimalTaming  => ("Androcles", "Androcles' Journal of the Lion", "Androcles may have calmed his lion with this gentle method."),
        SkillName.TasteID       => ("Mithridates", "Mithridates' Book of Antidotes", "Mithridates may have tasted out poison by this method."),
        SkillName.Tinkering     => ("Hero of Alexandria", "Hero of Alexandria's Book of Devices", "Hero of Alexandria may have built his marvels by these designs."),
        SkillName.Tracking      => ("Daniel Boone", "Daniel Boone's Trail Journal", "Daniel Boone may have followed these trails through the wilderness."),
        SkillName.Veterinary    => ("Vegetius", "Vegetius' Manual of Beast Healing", "Vegetius may have set down these cures for ailing beasts."),
        SkillName.Swords        => ("Miyamoto Musashi", "Musashi's Book of Two Heavens", "Miyamoto Musashi may have written this novel swordsman technique."),
        SkillName.Macing        => ("Fiore dei Liberi", "Fiore's Flower of Battle", "Fiore dei Liberi may have sketched these crushing blows."),
        SkillName.Fencing       => ("Ridolfo Capo Ferro", "Capo Ferro's Treatise on the Lunge", "Ridolfo Capo Ferro may have perfected this thrust in secret."),
        SkillName.Wrestling     => ("Milo of Croton", "Milo of Croton's Training Journal", "Milo of Croton may have pinned his rivals with these holds."),
        SkillName.Lumberjacking => ("Paul Bunyan", "Paul Bunyan's Woodsman Journal", "Paul Bunyan may have felled a forest with this swing."),
        SkillName.Mining        => ("Georgius Agricola", "Agricola's Treatise on Metals", "Georgius Agricola may have found a rich vein by this method."),
        SkillName.Meditation    => ("Marcus Aurelius", "The Lost Meditations", "Marcus Aurelius may have written these in a quiet hour on campaign."),
        SkillName.Stealth       => ("Natori Masazumi", "Masazumi's Shoninki", "Natori Masazumi may have hidden these lessons from his own students."),
        SkillName.RemoveTrap    => ("Howard Carter", "Howard Carter's Tomb Journal", "Howard Carter may have passed a tomb's traps with this care."),
        SkillName.Necromancy    => ("Edward Kelley", "Kelley's Book of Graveyard Rites", "Edward Kelley may have raised the dead with these rites."),
        SkillName.Focus         => ("Epictetus", "Epictetus' Discourses on Attention", "Epictetus may have held his mind steady by this practice."),
        SkillName.Chivalry      => ("Geoffroi de Charny", "De Charny's Book of Chivalry", "Geoffroi de Charny may have sworn by these knightly vows."),
        SkillName.Bushido       => ("Yamamoto Tsunetomo", "Tsunetomo's Lost Hagakure", "Yamamoto Tsunetomo may have spoken these lessons on the way of the warrior."),
        SkillName.Ninjitsu      => ("Hattori Hanzo", "Hattori Hanzo's Shinobi Manual", "Hattori Hanzo may have taught this art to only one student."),
        SkillName.Spellweaving  => ("Taliesin", "Taliesin's Book of Woven Verse", "Taliesin may have woven magic into these verses."),
        SkillName.Mysticism     => ("Nostradamus", "Nostradamus' Book of Quatrains", "Nostradamus may have glimpsed the future in these verses."),
        SkillName.Imbuing       => ("Nicolas Flamel", "Flamel's Book of Enchantment", "Nicolas Flamel may have bound power into metal this way."),
        SkillName.Throwing      => ("Phayllus of Croton", "Phayllus of Croton's Training Journal", "Phayllus of Croton may have hurled the discus with this form."),
        _                       => ("an unknown scholar", "A Tome of Forgotten Lore", null)
    };
}
