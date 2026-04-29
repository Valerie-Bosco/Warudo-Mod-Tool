using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

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

            var packageFileUri = "file:" + packageSourceFullPath.Replace('\\', '/');
            var addRequest = Client.Add(packageFileUri);
            WaitForRequest(addRequest, "PackageManager.Client.Add");

            var embedRequest = Client.Embed(packageName);
            WaitForRequest(embedRequest, "PackageManager.Client.Embed");

            var embeddedPackagePath = GetEmbeddedPackagePath(packageName);
            SyncMetaFiles(embeddedPackagePath, packageSourceFullPath);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

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

        private static string GetEmbeddedPackagePath(string packageName)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(p => string.Equals(p.name, packageName, StringComparison.OrdinalIgnoreCase));

            if (packageInfo == null || string.IsNullOrWhiteSpace(packageInfo.resolvedPath))
            {
                throw new Exception($"Unable to resolve embedded package path for: {packageName}");
            }

            return Path.GetFullPath(packageInfo.resolvedPath);
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

        private static void WaitForRequest(Request request, string operation)
        {
            while (!request.IsCompleted)
            {
                Thread.Sleep(100);
            }

            if (request.Status == StatusCode.Failure)
            {
                throw new Exception($"{operation} failed: {request.Error?.message}");
            }
        }
    }
}
