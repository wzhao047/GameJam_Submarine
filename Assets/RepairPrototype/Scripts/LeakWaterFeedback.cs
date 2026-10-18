using UnityEngine;

public class LeakWaterFeedback : MonoBehaviour
{
    [Header("Â©Ë®Î¬ÐÞÂß¼­")]
    public LeakRepairInteraction repair;

    [Header("Â©Ë®Á£×Ó")]
    public ParticleSystem waterParticles;

    private void Update()
    {
        if (waterParticles == null) return;

        bool shouldLeak =
            repair != null && repair.IsLeaking;

        if (shouldLeak)
        {
            if (!waterParticles.isPlaying)
                waterParticles.Play(true);
        }
        else if (waterParticles.isPlaying)
        {
            StopWater();
        }
    }

    private void StopWater()
    {
        if (waterParticles == null) return;

        waterParticles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }

    private void OnDisable()
    {
        StopWater();
    }
}