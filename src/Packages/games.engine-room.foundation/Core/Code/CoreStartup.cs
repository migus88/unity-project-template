using System;
using System.Threading;
using Core.Logging;
using Core.Privacy;
using Core.Save;
using Core.Settings;
using Cysharp.Threading.Tasks;

namespace Core
{
    public sealed class CoreStartup
    {
        private const int DefaultSaveSlot = 0;

        private bool _hasRun;

        private readonly ISettingsLoader _settingsLoader;
        private readonly ISaveStore _saveStore;
        private readonly ConsentService _consent;

        public CoreStartup(ISettingsLoader settingsLoader, ISaveStore saveStore, ConsentService consent)
        {
            _settingsLoader = settingsLoader;
            _saveStore = saveStore;
            _consent = consent;
        }

        public async UniTask RunAsync(CancellationToken ct)
        {
            if (_hasRun)
            {
                throw new InvalidOperationException("Core startup has already run.");
            }

            _hasRun = true;
            await _settingsLoader.LoadAsync(ct);
            await _consent.LoadAsync(ct);
            await SelectDefaultSaveSlotAsync(ct);
        }

        private async UniTask SelectDefaultSaveSlotAsync(CancellationToken ct)
        {
            var selected = await _saveStore.SelectSlotAsync(DefaultSaveSlot, ct);

            if (selected.TryPickT1(out var error, out _))
            {
                Log.Warn(LogTags.Save, error.Message);
                return;
            }

            Log.Info(LogTags.Save, $"Save slot {DefaultSaveSlot} selected.");
        }
    }
}
