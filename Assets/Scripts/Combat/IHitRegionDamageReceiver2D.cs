/// <summary>Receives a fully resolved hit so body parts can apply crit- and health-dependent reactions.</summary>
public interface IHitRegionDamageReceiver2D
{
    /// <summary>Receives the resolved damage, hit region, critical result, and pre-hit health context.</summary>
    void ReceiveHitRegionDamage(CombatHitData hitData);
}
