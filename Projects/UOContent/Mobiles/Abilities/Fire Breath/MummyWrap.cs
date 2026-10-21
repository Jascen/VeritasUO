using System;

namespace Server.Mobiles;

public class MummyWrap : FireBreath
{
    public override int PhysicalDamage => 100;
    public override int FireDamage => 0;

    public override int BreathEffectItemID => 0xE21;

    public override void OnBreathHit(BaseCreature source, Mobile target)
    {
        target.Paralyze(TimeSpan.FromSeconds(3.0));
    }
}
