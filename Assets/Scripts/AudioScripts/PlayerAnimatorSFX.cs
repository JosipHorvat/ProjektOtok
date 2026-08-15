using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimatorSFX : MonoBehaviour
{
    [SerializeField] private List<RandomAudioPlayer> sfxList = new List<RandomAudioPlayer>();

    public void PlaySFX(int id)
    {
        if (id < 0 || id >= sfxList.Count)
        {
            Debug.LogWarning($"Invalid SFX id: {id}");
            return;
        }

        sfxList[id]?.PlayRandomSound();
    }
    
}
