using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
///     Staff test creature for <see cref="OmniAI"/>. <c>[add OmniAITester n]</c> picks the school:
///     0 Magery, 1 Necromancy, 2 Bushido, 3 Ninjitsu, 4 Chivalry, 5 Bard; anything else trains all six.
/// </summary>
[SerializationGenerator(0)]
public partial class OmniAITester : BaseCreature
{
    [Constructible]
    public OmniAITester() : this(-1)
    {
    }

    [Constructible]
    public OmniAITester(int school) : base(AIType.AI_Melee, FightMode.Closest)
    {
        Body = 400;
        Hue = 1175;

        SetStr(1000);
        SetDex(150);
        SetInt(1500);

        SetDamage(25, 30);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50);
        SetResistance(ResistanceType.Fire, 50);
        SetResistance(ResistanceType.Cold, 50);
        SetResistance(ResistanceType.Poison, 50);
        SetResistance(ResistanceType.Energy, 50);

        var all = school is < 0 or > 5;

        if (all || school == 0)
        {
            SetSkill(SkillName.EvalInt, 120.0);
            SetSkill(SkillName.Magery, 120.0);
            SetSkill(SkillName.Meditation, 100.0);
        }

        if (all || school == 1)
        {
            SetSkill(SkillName.Necromancy, 120.0);
            SetSkill(SkillName.SpiritSpeak, 120.0);
        }

        if (all || school == 2)
        {
            SetSkill(SkillName.Bushido, 120.0);
            SetSkill(SkillName.Parry, 120.0);
        }

        if (all || school == 3)
        {
            SetSkill(SkillName.Ninjitsu, 120.0);
            SetSkill(SkillName.Hiding, 120.0);
            SetSkill(SkillName.Stealth, 120.0);
        }

        if (all || school == 4)
        {
            SetSkill(SkillName.Chivalry, 120.0);
        }

        if (all || school == 5)
        {
            SetSkill(SkillName.Musicianship, 120.0);
            SetSkill(SkillName.Discordance, 120.0);
            SetSkill(SkillName.Provocation, 120.0);
            SetSkill(SkillName.Peacemaking, 120.0);
        }

        SetSkill(SkillName.Anatomy, 100.0);
        SetSkill(SkillName.MagicResist, 120.0);
        SetSkill(SkillName.Swords, 100.0);
        SetSkill(SkillName.Tactics, 100.0);

        Fame = 32000;
        Karma = -32000;

        VirtualArmor = 80;

        EquipImmovable(new DragonHelm());
        EquipImmovable(new PlateGorget());
        EquipImmovable(new DragonChest());
        EquipImmovable(new DragonLegs());
        EquipImmovable(new DragonArms());
        EquipImmovable(new DragonGloves());
        EquipImmovable(new Longsword { ItemID = 9934 });
    }

    public override string CorpseName => "a corpse";
    public override string DefaultName => "a dark knight";

    protected override BaseAI ForcedAI => new OmniAI(this);

    public override bool AlwaysMurderer => true;
    public override bool ShowFameTitle => false;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 35);
        AddLoot(LootPack.Gems, 15);
    }

    private void EquipImmovable(Item item)
    {
        item.Hue = Hue;
        item.Movable = false;
        EquipItem(item);
    }
}
