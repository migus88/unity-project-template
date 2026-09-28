using System;
using VContainer.Unity;

namespace Core.Localization
{
    internal sealed class LocalizationTableRegistration : IInitializable, IDisposable
    {
        private bool _isAdded;

        private readonly LocalizationService _localization;
        private readonly LocalizationTable _table;

        public LocalizationTableRegistration(LocalizationService localization, LocalizationTable table)
        {
            _localization = localization;
            _table = table;
        }

        public void Initialize()
        {
            _localization.AddTable(_table);
            _isAdded = true;
        }

        public void Dispose()
        {
            if (!_isAdded)
            {
                return;
            }

            _isAdded = false;
            _localization.RemoveTable(_table);
        }
    }
}
