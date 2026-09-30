using System;
using Core.Audio;
using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(menuName = "Gameplay/Gameplay Config")]
    internal sealed class GameplayConfig : ScriptableObject
    {
        [field: SerializeField, Min(1)] public float RoundDurationSeconds { get; private set; } = 30f;
        [field: SerializeField, Min(0.1f)] public float MoveSpeed { get; private set; } = 6f;
        [field: SerializeField, Min(1)] public int PointsPerCollectible { get; private set; } = 10;
        [field: SerializeField, Min(0.1f)] public float NearestCameraDistanceScale { get; private set; } = 0.5f;
        [field: SerializeField, Min(0.1f)] public float FarthestCameraDistanceScale { get; private set; } = 1.5f;
        [field: SerializeField] public AudioCue CollectCue { get; private set; } = null!;
        [field: SerializeField] public AudioCue WinCue { get; private set; } = null!;
        [field: SerializeField] public AudioCue LoseCue { get; private set; } = null!;

        public TimeSpan RoundDuration => TimeSpan.FromSeconds(RoundDurationSeconds);
    }
}
