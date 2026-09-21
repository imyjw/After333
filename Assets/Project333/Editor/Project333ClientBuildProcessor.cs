using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Project333.Runtime.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Project333.Editor
{
    public sealed class Project333ClientBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string GeneratedAssetPath = "Assets/Project333/Resources/Project333/ClientBuild.json";
        public int callbackOrder => 10000;
        private static readonly HashSet<string> MediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".psb", ".gif", ".webp", ".bmp", ".tif", ".tiff", ".exr", ".hdr",
            ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".mp4", ".mov", ".avi", ".webm", ".mpg",
            ".ttf", ".otf", ".woff", ".woff2", ".aseprite", ".pdf"
        };

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                RequireSupportedOptions(report.summary.options);
                var root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                var metadata = Capture(root, report.summary.platform.ToString(), PlayerSettings.bundleVersion,
                    UnityEngine.Application.unityVersion, DateTimeOffset.UtcNow, report.summary.options.ToString());
                WriteMetadata(root, metadata);
                AssetDatabase.ImportAsset(GeneratedAssetPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var imported = AssetDatabase.LoadAssetAtPath<TextAsset>(GeneratedAssetPath);
                if (imported == null || !Project333ClientBuildInfo.TryParseMetadata(imported.text, out var verified) ||
                    verified.BuildId != metadata.BuildId)
                    throw new InvalidOperationException("Generated build identity was not imported.");
                Debug.Log($"After333 build identity prepared: {metadata.BuildId}, source={metadata.SourceSha256}, git={metadata.GitState}");
            }
            catch (Exception ex)
            {
                // Never silently package the identity from a previous build after a generation failure.
                throw new BuildFailedException("After333 build identity generation failed: " + ex.Message);
            }
        }

        public static void RequireSupportedOptions(BuildOptions options)
        {
            if ((options & BuildOptions.BuildScriptsOnly) != 0)
                throw new InvalidOperationException("Scripts Only Build cannot update embedded build identity. Use normal Build or Build And Run.");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            // Unity may still report Unknown while postprocessors are running.
            if (report.summary.result == BuildResult.Failed || report.summary.result == BuildResult.Cancelled) return;
            try
            {
                var json = File.ReadAllText(GeneratedAssetPath);
                if (!Project333ClientBuildInfo.TryParseMetadata(json, out _))
                    throw new InvalidOperationException("Build identity is invalid.");
                // Sibling file works for APK, executable, .app and exported project directories.
                var sidecar = report.summary.outputPath.TrimEnd('/', '\\') + ".build.json";
                File.WriteAllText(sidecar, json, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("After333 embedded build identity is prepared, but its companion file could not be saved: " + ex.Message);
            }
        }

        public static ClientBuildMetadata Capture(string root, string target, string applicationVersion,
            string unityVersion, DateTimeOffset now, string buildOptions = "None")
        {
            var utc = now.ToUniversalTime();
            var metadata = new ClientBuildMetadata
            {
                BuildId = utc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                BuiltAtUtc = utc.ToString("O", CultureInfo.InvariantCulture),
                ApplicationVersion = applicationVersion,
                CompatibilityVersion = Project333ClientBuildInfo.CurrentClientVersion,
                Target = target,
                BuildOptions = buildOptions,
                UnityVersion = unityVersion,
                SourceSha256 = ComputeSourceHash(root),
                SourceHashScope = "Assets code/data/scenes/import settings (media content excluded) + Packages + ProjectSettings; excludes generated ClientBuild.json/meta",
                GitCommit = "unavailable",
                GitState = "unavailable"
            };
            // A copied project inside another repository must not inherit that parent's commit.
            var gitRoot = RunGit(root, "rev-parse --show-toplevel");
            if (gitRoot != null && string.Equals(Path.GetFullPath(gitRoot.Trim()).TrimEnd('/', '\\'),
                    Path.GetFullPath(root).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase))
            {
                var commit = RunGit(root, "rev-parse HEAD")?.Trim();
                var status = RunGit(root, "status --porcelain=v1 --untracked-files=normal");
                if (commit != null && (commit.Length == 40 || commit.Length == 64) && commit.All(Uri.IsHexDigit))
                    metadata.GitCommit = commit;
                if (status != null) metadata.GitState = string.IsNullOrWhiteSpace(status) ? "clean" : "modified";
            }
            return metadata;
        }

        public static void WriteMetadata(string root, ClientBuildMetadata metadata)
        {
            var path = Path.Combine(root, GeneratedAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(metadata, true), new UTF8Encoding(false));
        }

        public static string ComputeSourceHash(string root)
        {
            root = Path.GetFullPath(root);
            var paths = new List<string>();
            foreach (var folder in new[] { "Assets", "Packages", "ProjectSettings" })
            {
                var directory = Path.Combine(root, folder);
                if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Missing project folder: " + folder);
                paths.AddRange(Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                    .Select(path => path.Substring(root.TrimEnd('/', '\\').Length + 1).Replace('\\', '/'))
                    .Where(path => path != GeneratedAssetPath && path != GeneratedAssetPath + ".meta")
                    .Where(path => !path.StartsWith("Assets/", StringComparison.Ordinal) || !MediaExtensions.Contains(Path.GetExtension(path))));
            }
            paths.Sort(StringComparer.Ordinal);
            using var summary = new MemoryStream();
            using var writer = new BinaryWriter(summary, Encoding.UTF8, leaveOpen: true);
            using var sha = SHA256.Create();
            foreach (var path in paths)
            {
                // Length-prefixed UTF8 path followed by an exactly32-byte content digest.
                writer.Write(path);
                using var input = File.OpenRead(Path.Combine(root, path));
                writer.Write(sha.ComputeHash(input));
            }
            writer.Flush();
            summary.Position = 0;
            return BitConverter.ToString(sha.ComputeHash(summary)).Replace("-", "").ToLowerInvariant();
        }

        private static string RunGit(string root, string arguments)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo("git", arguments)
                    {
                        WorkingDirectory = root,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(3000)) { process.Kill(); return null; }
                return process.ExitCode == 0 ? output.GetAwaiter().GetResult() : null;
            }
            catch (Exception) { return null; }
        }
    }
}
