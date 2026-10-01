using System;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Core.Editor
{
    public static class CorePackage
    {
        public const string Name = "games.engine-room.foundation";
        public const string Root = "Packages/" + Name;

        public static bool IsWritable(string assetPath)
        {
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return true;
            }

            var package = PackageInfo.FindForAssetPath(assetPath);
            return package != null && (package.source == PackageSource.Embedded || package.source == PackageSource.Local);
        }
    }
}
