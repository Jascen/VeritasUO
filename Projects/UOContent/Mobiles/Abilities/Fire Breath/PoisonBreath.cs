namespace Server.Mobiles;

public class PoisonBreath : FireBreath
{
    public override int FireDamage => 0;
    public override int PoisonDamage => 100;

    public override int BreathEffectHue => 0x3F;

    public override void OnBreathHit(BaseCreature source, Mobile target)
    {
        target.ApplyPoison(source, Poison.Greater);
    }
}
