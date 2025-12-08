using System.Collections.Generic; 
using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource m_ambienceSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip m_ambieceClip;
    [SerializeField] private AudioClip m_cabinClip;

    private void Awake()
    {
        // Configurar ambience
        if (m_ambienceSource != null && m_ambieceClip != null)
        {
            m_ambienceSource.clip = m_ambieceClip;
            m_ambienceSource.loop = true;
            m_ambienceSource.volume = 0.5f; // Volumen inicial
            m_ambienceSource.playOnAwake = true;
            m_ambienceSource.Play();
        }
    }

    public void PlayCabinSound()
    {
        m_ambienceSource.clip = m_cabinClip;
        m_ambienceSource.Play();
    }

    public void PlayAmbienceSound()
    {
        m_ambienceSource.clip = m_ambieceClip;
        m_ambienceSource.Play();
    }
}
