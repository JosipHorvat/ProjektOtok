/// <summary>Describes data-driven use actions that future item handlers may execute.</summary>
public enum ItemUseBehavior
{
    None = 0,
    RestoreHealth = 1,
    RestoreAmmunition = 2,
    ApplyStatusEffect = 3,
    Deploy = 4,
    TriggerQuestAction = 5,
    Custom = 6,
}
