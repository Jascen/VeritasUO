using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0)]
    public partial class CulinaryGuildmaster : BaseGuildmaster
    {
        [Constructible]
        public CulinaryGuildmaster() : base("culinary")
        {
            SetSkill(SkillName.Cooking, 90.0, 100.0);
            SetSkill(SkillName.TasteID, 75.0, 98.0);
        }

        public override NpcGuild NpcGuild => NpcGuild.CulinariansGuild;

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new HalfApron());
            AddItem(new Cleaver());
        }
    }
}
