using System.Collections.Generic;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Shared.UI
{
    public sealed class DropdownView : MonoBehaviour
    {
        public Observable<int> SelectedIndexChanged => _dropdown.onValueChanged.AsObservable(destroyCancellationToken);

        [SerializeField, Required] private TMP_Dropdown _dropdown = null!;

        public void SetOptions(IReadOnlyList<string> options, int selectedIndex)
        {
            _dropdown.ClearOptions();
            _dropdown.AddOptions(new List<string>(options));
            _dropdown.SetValueWithoutNotify(selectedIndex);
        }

        public void SetInteractable(bool isInteractable)
        {
            _dropdown.interactable = isInteractable;
        }
    }
}
