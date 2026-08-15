using UnityEngine;
using UnityEngine.Audio;

/// <summary>Persistent audio settings service for master, music, and SFX mixer channels.</summary>
public sealed class AudioManager : MonoBehaviour
{
    private const string MasterVolumeKey = "Audio.MasterVolume";
    private const string MusicVolumeKey = "Audio.MusicVolume";
    private const string SFXVolumeKey = "Audio.SFXVolume";

    private const float DefaultMasterVolume = 1f;
    private const float DefaultMusicVolume = 1f;
    private const float DefaultSFXVolume = 1f;
    private const float MinimumDecibels = -80f;

    public static AudioManager Instance { get; private set; }

    [Header("Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterVolumeParameter = "MasterVol";
    [SerializeField] private string musicVolumeParameter = "MusicVol";
    [SerializeField] private string sfxVolumeParameter = "SFXVol";

    [Header("Pause Music")]
    [SerializeField, Range(0f, 1f)] private float pausedMusicMultiplier = 0.2f;

    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public float SFXVolume { get; private set; }
    public bool IsMusicPaused { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSettings();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Sets and saves the master volume. The value is normalized from 0 to 1.</summary>
    public void SetMasterVolume(float value)
    {
        MasterVolume = NormalizeVolume(value, DefaultMasterVolume);
        ApplyMixerVolume(masterVolumeParameter, MasterVolume);
        SaveFloat(MasterVolumeKey, MasterVolume);
    }

    /// <summary>Sets and saves the music volume. The value is normalized from 0 to 1.</summary>
    public void SetMusicVolume(float value)
    {
        MusicVolume = NormalizeVolume(value, DefaultMusicVolume);
        ApplyMusicVolume();
        SaveFloat(MusicVolumeKey, MusicVolume);
    }

    /// <summary>Sets and saves the SFX volume. The value is normalized from 0 to 1.</summary>
    public void SetSFXVolume(float value)
    {
        SFXVolume = NormalizeVolume(value, DefaultSFXVolume);
        ApplyMixerVolume(sfxVolumeParameter, SFXVolume);
        SaveFloat(SFXVolumeKey, SFXVolume);
    }

    /// <summary>Loads saved audio settings or applies the default values.</summary>
    public void LoadSettings()
    {
        MasterVolume = NormalizeVolume(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume), DefaultMasterVolume);
        MusicVolume = NormalizeVolume(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume), DefaultMusicVolume);
        SFXVolume = NormalizeVolume(PlayerPrefs.GetFloat(SFXVolumeKey, DefaultSFXVolume), DefaultSFXVolume);

        ApplyMixerVolume(masterVolumeParameter, MasterVolume);
        ApplyMusicVolume();
        ApplyMixerVolume(sfxVolumeParameter, SFXVolume);
    }

    /// <summary>Lowers or mutes the music channel while gameplay is paused.</summary>
    public void PauseMusic()
    {
        IsMusicPaused = true;
        ApplyMusicVolume();
    }

    /// <summary>Restores the music channel after gameplay resumes.</summary>
    public void ResumeMusic()
    {
        IsMusicPaused = false;
        ApplyMusicVolume();
    }

    /// <summary>Restores all audio settings to their defaults and saves them.</summary>
    public void ResetSettings()
    {
        SetMasterVolume(DefaultMasterVolume);
        SetMusicVolume(DefaultMusicVolume);
        SetSFXVolume(DefaultSFXVolume);
    }

    private void ApplyMusicVolume()
    {
        float effectiveVolume = IsMusicPaused ? MusicVolume * pausedMusicMultiplier : MusicVolume;
        ApplyMixerVolume(musicVolumeParameter, effectiveVolume);
    }

    private void ApplyMixerVolume(string parameter, float normalizedValue)
    {
        if (audioMixer == null || string.IsNullOrWhiteSpace(parameter))
        {
            return;
        }

        float decibels = normalizedValue > 0f
            ? Mathf.Log10(normalizedValue) * 20f
            : MinimumDecibels;

        if (!audioMixer.SetFloat(parameter, decibels))
        {
            Debug.LogWarning($"AudioManager: Mixer parameter '{parameter}' is not exposed.", this);
        }
    }

    private static float NormalizeVolume(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value)
            ? fallback
            : Mathf.Clamp01(value);
    }

    private static void SaveFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }
}
