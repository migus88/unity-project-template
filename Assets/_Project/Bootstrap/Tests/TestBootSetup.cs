using UnityEditor;
using UnityEngine.TestTools;

namespace Bootstrap.PlayModeTests
{
    public sealed class TestBootSetup : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup()
        {
            SessionState.SetBool(BootMode.TestKey, true);
        }

        public void Cleanup()
        {
            SessionState.EraseBool(BootMode.TestKey);
        }
    }
}
