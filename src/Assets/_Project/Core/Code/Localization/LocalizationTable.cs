using System.Collections.Generic;
using UnityEngine;

namespace Core.Localization
{
    [CreateAssetMenu(menuName = "Core/Localization Table")]
    public sealed class LocalizationTable : ScriptableObject
    {
        [field: SerializeField] public string TableName { get; private set; } = string.Empty;

        public IReadOnlyList<LocalizationEntry> Entries => _entries;

        [SerializeField] private List<LocalizationEntry> _entries = new();
    }
}
