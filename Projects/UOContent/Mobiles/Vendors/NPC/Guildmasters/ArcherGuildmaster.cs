using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0)]
    public partial class ArcherGuildmaster : BaseGuildmaster
    {
        [Constructible]
        public ArcherGuildmaster() : base("archer")
        {
            SetSkill(SkillName.Fletching, 80.0, 100.0);
            SetSkill(SkillName.Archery, 80.0, 100.0);
            SetSkill(SkillName.Tactics, 80.0, 100.0);
        }

        public override NpcGuild NpcGuild => NpcGuild.ArchersGuild;

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new FeatheredHat(Utility.RandomNeutralHue()));
            AddItem(new Bow());
        }
    }
}
