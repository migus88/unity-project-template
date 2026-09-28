using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Localization
{
    [CreateAssetMenu(menuName = "Core/Localization Table")]
    public sealed class LocalizationTable : ScriptableObject
    {
        [field: SerializeField, Required] public string TableName { get; private set; } = string.Empty;

        public IReadOnlyList<LocalizationEntry> Entries => _entries;

        [SerializeField, TableList(AlwaysExpanded = true, ShowIndexLabels = false)] private List<LocalizationEntry> _entries = new();
    }
}
