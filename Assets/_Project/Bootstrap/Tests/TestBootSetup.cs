using System.IO;
using UnityEditor;
using UnityEngine.TestTools;

namespace Bootstrap.PlayModeTests
{
    public sealed class TestBootSetup : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup()
        {
            DeleteTestStorage();
            SessionState.SetBool(BootMode.TestKey, true);
        }

        public void Cleanup()
        {
            SessionState.EraseBool(BootMode.TestKey);
            DeleteTestStorage();
        }

        private static void DeleteTestStorage()
        {
            if (Directory.Exists(BootMode.TestStorageRoot))
            {
                Directory.Delete(BootMode.TestStorageRoot, recursive: true);
            }
        }
    }
}
