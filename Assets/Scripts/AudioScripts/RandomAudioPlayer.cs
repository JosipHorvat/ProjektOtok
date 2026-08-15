using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RandomAudioPlayer : MonoBehaviour
{
    public AudioClip[] clips;
    public bool randomizePitch = false;
    public float pitchRange = 0.2f;

    protected AudioSource m_Source;

    private void Awake()
    {
        m_Source = GetComponent<AudioSource>();
    }

    public void PlayRandomSound()
    {
        if (clips.Length == 0)
        {
            Debug.LogWarning("No audio clips assigned.");
            return;
        }

        int choice = Random.Range(0, clips.Length);

        if (randomizePitch)
            m_Source.pitch = Random.Range(1.0f - pitchRange, 1.0f + pitchRange);

        m_Source.PlayOneShot(clips[choice]);
    }

    public void PlayLoopingSound()
    {
        if (clips.Length == 0)
        {
            Debug.LogWarning("No audio clips assigned.");
            return;
        }

        int choice = Random.Range(0, clips.Length);

        if (randomizePitch)
            m_Source.pitch = Random.Range(1.0f - pitchRange, 1.0f + pitchRange);

        m_Source.clip = clips[choice];
        m_Source.loop = true;
        m_Source.Play();
    }

    public bool IsPlaying => m_Source != null && m_Source.isPlaying;

    public void Stop()
    {
        m_Source.Stop();
    }

    public float GetCurrentClipLength()
    {
        if (m_Source.clip != null)
            return m_Source.clip.length; 
        else
            return 0f; 
    }
}