using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EngineAudio : MonoBehaviour
{
    [Header("Audio Components")]
    [SerializeField] private AudioSource startingSound;
    // Start is called before the first frame update
    void Start()
    {
        startingSound.Play();
    }

    // Update is called once per frame
    void Update()
    {
    }
}
