using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Gameplay.Hud
{
    internal sealed class HudView : MonoBehaviour
    {
        [SerializeField, Required] private TMP_Text _scoreLabel = null!;
        [SerializeField, Required] private TMP_Text _timeLeftLabel = null!;

        public void SetScore(string score)
        {
            _scoreLabel.text = score;
        }

        public void SetTimeLeft(string timeLeft)
        {
            _timeLeftLabel.text = timeLeft;
        }
    }
}
