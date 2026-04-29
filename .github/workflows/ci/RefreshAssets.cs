using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace CI
{
    public static class RefreshAssets
    {
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            var localPackages = UnityEditor.PackageManager.PackageInfo
                .GetAllRegisteredPackages()
                .Where(p => p.source == UnityEditor.PackageManager.PackageSource.Local
                            || p.source == UnityEditor.PackageManager.PackageSource.Embedded)
                .ToArray();

            foreach (var package in localPackages)
            {
                ForceImportAllFolders(package.assetPath, package.resolvedPath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();

            UpmMetaPublisher.Run();
        }

        private static void ForceImportAllFolders(string packageAssetPath, string packageResolvedPath)
        {
            if (string.IsNullOrWhiteSpace(packageAssetPath) || string.IsNullOrWhiteSpace(packageResolvedPath))
            {
                return;
            }

            var root = Path.GetFullPath(packageResolvedPath);
            if (!Directory.Exists(root))
            {
                return;
            }

            AssetDatabase.ImportAsset(packageAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            foreach (var directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                var relative = directory.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.IsNullOrEmpty(relative))
                {
                    continue;
                }

                var assetPath = packageAssetPath + "/" + relative.Replace('\\', '/');
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
