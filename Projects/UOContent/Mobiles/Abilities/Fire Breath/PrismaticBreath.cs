namespace Server.Mobiles;

public class PrismaticBreath : FireBreath
{
    public override int PhysicalDamage => 20;
    public override int FireDamage => 20;
    public override int ColdDamage => 20;
    public override int PoisonDamage => 20;
    public override int EnergyDamage => 20;

    public override int BreathEffectHue => 0xB71;
}
