using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0)]
    public partial class NecromancerGuildmaster : BaseGuildmaster
    {
        [Constructible]
        public NecromancerGuildmaster() : base("necromancer")
        {
            SetSkill(SkillName.SpiritSpeak, 65.0, 88.0);
            SetSkill(SkillName.Inscribe, 60.0, 83.0);
            SetSkill(SkillName.Meditation, 60.0, 83.0);
            SetSkill(SkillName.MagicResist, 65.0, 88.0);
            SetSkill(SkillName.Necromancy, 64.0, 100.0);
            SetSkill(SkillName.Forensics, 82.0, 100.0);
        }

        public override NpcGuild NpcGuild => NpcGuild.NecromancersGuild;

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new Robe(0x455));
            AddItem(new BlackStaff());
        }
    }
}
