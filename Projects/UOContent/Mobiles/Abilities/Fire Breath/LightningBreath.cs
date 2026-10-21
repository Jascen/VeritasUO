namespace Server.Mobiles;

public class LightningBreath : FireBreath
{
    public override int FireDamage => 0;
    public override int EnergyDamage => 100;

    public override int BreathEffectItemID => 0x379F;
    public override int BreathEffectSound => 0x20A;
}
