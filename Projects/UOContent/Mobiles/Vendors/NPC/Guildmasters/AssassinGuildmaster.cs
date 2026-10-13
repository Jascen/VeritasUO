using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0)]
    public partial class AssassinGuildmaster : BaseGuildmaster
    {
        [Constructible]
        public AssassinGuildmaster() : base("assassin")
        {
            SetSkill(SkillName.DetectHidden, 75.0, 98.0);
            SetSkill(SkillName.Hiding, 65.0, 88.0);
            SetSkill(SkillName.Fencing, 75.0, 98.0);
            SetSkill(SkillName.Stealth, 85.0, 100.0);
            SetSkill(SkillName.Poisoning, 85.0, 100.0);
        }

        public override NpcGuild NpcGuild => NpcGuild.AssassinsGuild;

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new Cloak(0x455));
            AddItem(new Dagger());
        }
    }
}
