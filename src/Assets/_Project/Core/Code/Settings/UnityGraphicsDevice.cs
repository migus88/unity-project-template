using UnityEngine;

namespace Core.Settings
{
    public sealed class UnityGraphicsDevice : IGraphicsDevice
    {
        public int QualityLevel => QualitySettings.GetQualityLevel();
        public int QualityLevelCount => QualitySettings.count;
        public FullScreenMode FullScreenMode => Screen.fullScreenMode;
        public bool VSync => QualitySettings.vSyncCount > 0;

        public Resolution Resolution => new()
        {
            width = Screen.width,
            height = Screen.height,
            refreshRateRatio = Screen.currentResolution.refreshRateRatio,
        };

        public void SetQualityLevel(int qualityLevel)
        {
            if (qualityLevel != QualitySettings.GetQualityLevel())
            {
                QualitySettings.SetQualityLevel(qualityLevel, true);
            }
        }

        public void SetVSync(bool isEnabled)
        {
            if (isEnabled != VSync)
            {
                QualitySettings.vSyncCount = isEnabled ? 1 : 0;
            }
        }

        public void SetScreen(Resolution resolution, FullScreenMode fullScreenMode)
        {
            if (resolution.width == Screen.width && resolution.height == Screen.height && fullScreenMode == Screen.fullScreenMode)
            {
                return;
            }

            Screen.SetResolution(resolution.width, resolution.height, fullScreenMode, resolution.refreshRateRatio);
        }
    }
}
