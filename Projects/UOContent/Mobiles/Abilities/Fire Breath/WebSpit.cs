namespace Server.Mobiles;

public class WebSpit : FireBreath
{
    public override int PhysicalDamage => 50;
    public override int FireDamage => 0;
    public override int PoisonDamage => 50;

    public override int BreathEffectItemID => 0x10D4;
}
