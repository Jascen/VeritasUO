using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0)]
    public partial class CarpenterGuildmaster : BaseGuildmaster
    {
        [Constructible]
        public CarpenterGuildmaster() : base("carpenter")
        {
            SetSkill(SkillName.Carpentry, 85.0, 100.0);
            SetSkill(SkillName.Lumberjacking, 60.0, 83.0);
        }

        public override NpcGuild NpcGuild => NpcGuild.CarpentersGuild;

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new HalfApron());
        }
    }
}
