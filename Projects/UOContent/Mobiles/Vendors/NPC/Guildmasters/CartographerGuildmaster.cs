using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0)]
    public partial class CartographerGuildmaster : BaseGuildmaster
    {
        [Constructible]
        public CartographerGuildmaster() : base("cartographer")
        {
            SetSkill(SkillName.Cartography, 90.0, 100.0);
        }

        public override NpcGuild NpcGuild => NpcGuild.CartographersGuild;

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new Robe(Utility.RandomNeutralHue()));
        }
    }
}
