using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CI
{
    public static class PackageImportAndPublish
    {
        public static void Run()
        {
            var packageSourceArg = GetCommandLineArg("-packageSource");
            if (string.IsNullOrWhiteSpace(packageSourceArg))
            {
                throw new Exception("Missing required argument: -packageSource");
            }

            var packageSourceFullPath = Path.GetFullPath(packageSourceArg);
            var packageJsonPath = Path.Combine(packageSourceFullPath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                throw new DirectoryNotFoundException($"Could not find package.json at path: {packageJsonPath}");
            }

            var packageName = ReadPackageName(packageJsonPath);

            var embeddedPackagePath = StageAsEmbeddedPackage(packageSourceFullPath, packageName);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            SyncMetaFiles(embeddedPackagePath, packageSourceFullPath);

            UpmMetaPublisher.Run();
        }

        private static string ReadPackageName(string packageJsonPath)
        {
            var json = File.ReadAllText(packageJsonPath);
            var match = Regex.Match(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");
            if (!match.Success)
            {
                throw new Exception($"Unable to read package name from: {packageJsonPath}");
            }

            return match.Groups[1].Value;
        }

        private static string StageAsEmbeddedPackage(string sourceRoot, string packageName)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var packagesRoot = Path.Combine(projectRoot, "Packages");
            var safeName = packageName.Replace('/', '.').Replace('\\', '.');
            var stagedRoot = Path.Combine(packagesRoot, $"__ci_embed_{safeName}");

            if (Directory.Exists(stagedRoot))
            {
                Directory.Delete(stagedRoot, true);
            }

            var stagedMeta = stagedRoot + ".meta";
            if (File.Exists(stagedMeta))
            {
                File.Delete(stagedMeta);
            }

            CopyDirectory(sourceRoot, stagedRoot);

            return stagedRoot;
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (var filePath in Directory.GetFiles(sourceDir))
            {
                var destinationPath = Path.Combine(destinationDir, Path.GetFileName(filePath));
                File.Copy(filePath, destinationPath, true);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destinationSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destinationSubDir);
            }
        }

        private static void SyncMetaFiles(string fromRoot, string toRoot)
        {
            if (!Directory.Exists(fromRoot))
            {
                throw new DirectoryNotFoundException($"Embedded package folder not found: {fromRoot}");
            }

            foreach (var sourceMeta in Directory.GetFiles(fromRoot, "*.meta", SearchOption.AllDirectories))
            {
                var relative = sourceMeta.Substring(fromRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destinationMeta = Path.Combine(toRoot, relative);
                var destinationDir = Path.GetDirectoryName(destinationMeta);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                File.Copy(sourceMeta, destinationMeta, true);
            }

            var rootMeta = fromRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".meta";
            var destinationRootMeta = toRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".meta";
            if (File.Exists(rootMeta))
            {
                File.Copy(rootMeta, destinationRootMeta, true);
            }
        }

        private static string GetCommandLineArg(string argName)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

    }
}
