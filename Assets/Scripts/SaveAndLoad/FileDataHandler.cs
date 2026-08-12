using System;
using System.IO;
using UnityEngine;

/// <summary>Reads and writes one slot's JSON save file.</summary>
public sealed class FileDataHandler
{
    private readonly string dataDirectoryPath;
    private readonly string dataFileName;
    private readonly bool encryptData;
    private const string EncryptionKey = "josip";

    public FileDataHandler(string dataDirectoryPath, string dataFileName, bool encryptData)
    {
        this.dataDirectoryPath = dataDirectoryPath;
        this.dataFileName = dataFileName;
        this.encryptData = encryptData;
    }

    /// <summary>Serializes and writes save data to disk.</summary>
    public void Save(GameData data)
    {
        string fullPath = Path.Combine(dataDirectoryPath, dataFileName);
        try
        {
            Directory.CreateDirectory(dataDirectoryPath);
            string serializedData = JsonUtility.ToJson(data, true);
            if (encryptData) serializedData = EncryptDecrypt(serializedData);
            File.WriteAllText(fullPath, serializedData);
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveManager: Could not save '{fullPath}'.\n{exception}");
        }
    }

    /// <summary>Loads and deserializes save data from disk, or returns null when no file exists.</summary>
    public GameData Load()
    {
        string fullPath = Path.Combine(dataDirectoryPath, dataFileName);
        if (!File.Exists(fullPath)) return null;

        try
        {
            string serializedData = File.ReadAllText(fullPath);
            if (encryptData) serializedData = EncryptDecrypt(serializedData);
            return JsonUtility.FromJson<GameData>(serializedData);
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveManager: Could not load '{fullPath}'.\n{exception}");
            return null;
        }
    }

    /// <summary>Deletes this slot's save file if it exists.</summary>
    public void Delete()
    {
        string fullPath = Path.Combine(dataDirectoryPath, dataFileName);
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    private static string EncryptDecrypt(string data)
    {
        char[] characters = data.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
            characters[index] = (char)(characters[index] ^ EncryptionKey[index % EncryptionKey.Length]);
        return new string(characters);
    }
}
