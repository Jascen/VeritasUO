namespace Server.Mobiles;

public class FrostBreath : FireBreath
{
    public override int PhysicalDamage => 50;
    public override int FireDamage => 0;
    public override int ColdDamage => 50;

    public override int BreathEffectHue => 0x480;
}
