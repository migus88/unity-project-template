using UnityEngine;

namespace Core.Settings
{
    public interface IGraphicsDevice
    {
        int QualityLevel { get; }
        int QualityLevelCount { get; }
        FullScreenMode FullScreenMode { get; }
        Resolution Resolution { get; }
        bool VSync { get; }

        void SetQualityLevel(int qualityLevel);
        void SetVSync(bool isEnabled);
        void SetScreen(Resolution resolution, FullScreenMode fullScreenMode);
    }
}
