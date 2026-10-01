using UnityEngine;

namespace Core
{
    public sealed class ApplicationService : IApplicationService
    {
        public string Version => Application.version;
        public RuntimePlatform Platform => Application.platform;

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
