namespace Server.Mobiles;

public class BoulderThrow : FireBreath
{
    public override int PhysicalDamage => 100;
    public override int FireDamage => 0;

    public override int BreathEffectItemID => 0x1365;
}
