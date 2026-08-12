/// <summary>Contract for systems that contribute persistent state to a save slot.</summary>
public interface ISaveManager
{
    /// <summary>Applies loaded slot state to the runtime system.</summary>
    void LoadData(GameData data);

    /// <summary>Copies runtime system state into the slot data before serialization.</summary>
    void SaveData(ref GameData data);
}
