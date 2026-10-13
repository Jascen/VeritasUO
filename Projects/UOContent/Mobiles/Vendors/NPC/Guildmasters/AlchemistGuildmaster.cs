using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0)]
    public partial class AlchemistGuildmaster : BaseGuildmaster
    {
        [Constructible]
        public AlchemistGuildmaster() : base("alchemist")
        {
            SetSkill(SkillName.Alchemy, 85.0, 100.0);
            SetSkill(SkillName.TasteID, 65.0, 88.0);
            SetSkill(SkillName.Poisoning, 65.0, 88.0);
        }

        public override NpcGuild NpcGuild => NpcGuild.AlchemistsGuild;

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new Robe(Utility.RandomPinkHue()));
        }
    }
}
