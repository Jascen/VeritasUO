using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0)]
    public partial class DruidGuildmaster : BaseGuildmaster
    {
        [Constructible]
        public DruidGuildmaster() : base("druid")
        {
            SetSkill(SkillName.Herding, 80.0, 100.0);
            SetSkill(SkillName.Camping, 80.0, 100.0);
            SetSkill(SkillName.Cooking, 80.0, 100.0);
            SetSkill(SkillName.Alchemy, 80.0, 100.0);
            SetSkill(SkillName.AnimalLore, 85.0, 100.0);
            SetSkill(SkillName.AnimalTaming, 90.0, 100.0);
            SetSkill(SkillName.Veterinary, 90.0, 100.0);
        }

        public override NpcGuild NpcGuild => NpcGuild.DruidsGuild;

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new DeerMask());
            AddItem(
                Utility.Random(4) switch
                {
                    0 => new GnarledStaff(),
                    1 => new BlackStaff(),
                    2 => new QuarterStaff(),
                    _ => new ShepherdsCrook()
                }
            );
        }
    }
}
