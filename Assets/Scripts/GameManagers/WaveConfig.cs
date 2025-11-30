using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration for a single wave with multiple sub-waves
/// Create via: Assets → Create → Game/Wave Config
/// </summary>
[CreateAssetMenu(fileName = "Wave", menuName = "Game/WaveConfig")]
public class WaveConfig : ScriptableObject
{
    [System.Serializable]
    public class SubWave
    {
        [Header("SubWave Info")]
        public string subWaveName = "SubWave 1";

        [Header("Timing")]
        [Tooltip("Delay before this sub-wave spawns (seconds)")]
        public float spawnDelay = 0f;

        [Header("Enemies")]
        public List<EnemyGroup> enemyGroups = new List<EnemyGroup>();
    }

    [System.Serializable]
    public class EnemyGroup
    {
        public EEnemyType enemyType;
        public int count = 5;

        [Tooltip("Time between spawning each enemy in this group (seconds)")]
        public float spawnInterval = 0.5f;
    }

    [Header("Wave Info")]
    public string waveName = "Wave 1";

    [TextArea(2, 4)]
    public string waveDescription = "Wave description here";

    [Header("Sub-Waves")]
    public List<SubWave> subWaves = new List<SubWave>();

    /// <summary>
    /// Calculate total enemy count in this wave
    /// </summary>
    public int GetTotalEnemyCount()
    {
        int total = 0;
        foreach (var subWave in subWaves)
        {
            foreach (var group in subWave.enemyGroups)
            {
                total += group.count;
            }
        }
        return total;
    }
}