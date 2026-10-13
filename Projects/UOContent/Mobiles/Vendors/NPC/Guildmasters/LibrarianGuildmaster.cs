using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0)]
    public partial class LibrarianGuildmaster : BaseGuildmaster
    {
        [Constructible]
        public LibrarianGuildmaster() : base("librarian")
        {
            SetSkill(SkillName.Inscribe, 60.0, 83.0);
            SetSkill(SkillName.MagicResist, 65.0, 88.0);
            SetSkill(SkillName.Wrestling, 60.0, 80.0);
            SetSkill(SkillName.ItemID, 64.0, 100.0);
        }

        public override NpcGuild NpcGuild => NpcGuild.LibrariansGuild;

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new Robe(Utility.RandomBlueHue()));
        }
    }
}
