using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Round
{
    internal sealed class RoundResultView : MonoBehaviour
    {
        [SerializeField, Required] private GameObject _panel = null!;
        [SerializeField, Required] private TMP_Text _titleLabel = null!;
        [SerializeField, Required] private TMP_Text _scoreLabel = null!;
        [SerializeField, Required] private TMP_Text _bestScoreLabel = null!;
        [SerializeField, Required] private GameObject _newBestScoreBadge = null!;
        [SerializeField, Required] private Button _continueButton = null!;

        public void Show(string title, string score, string bestScore, bool isNewBestScore)
        {
            _titleLabel.text = title;
            _scoreLabel.text = score;
            _bestScoreLabel.text = bestScore;
            _newBestScoreBadge.SetActive(isNewBestScore);
            _continueButton.interactable = true;
            _panel.SetActive(true);
        }

        public async UniTask WaitForContinueAsync(CancellationToken ct)
        {
            var clicks = await _continueButton.OnClickAsObservable().Take(1).CountAsync(ct);

            if (clicks == 0)
            {
                throw new OperationCanceledException("The round result view was destroyed.");
            }
        }

        public void SetInteractable(bool isInteractable)
        {
            _continueButton.interactable = isInteractable;
        }
    }
}
