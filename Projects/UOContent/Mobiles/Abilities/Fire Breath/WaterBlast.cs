namespace Server.Mobiles;

public class WaterBlast : FireBreath
{
    public override int PhysicalDamage => 50;
    public override int FireDamage => 0;
    public override int ColdDamage => 50;

    public override int BreathEffectHue => 0x5D;
    public override int BreathEffectSound => 0x026;
}
