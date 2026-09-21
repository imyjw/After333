using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Project333.Editor;
using Project333.Runtime.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class Project333ClientBuildInfoTests
    {
        private string _root;
        private string _assetRoot;
        private static readonly DateTimeOffset BuildTime =
            new DateTimeOffset(2026, 9, 13, 10, 11, 12, TimeSpan.FromHours(9));

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "After333BuildIdentityTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Write("Assets/Card.cs", "public class Card { }");
            Write("Assets/Card.cs.meta", "guid: 0123456789abcdef0123456789abcdef");
            Write("Packages/manifest.json", "{\"dependencies\":{}}");
            Write("ProjectSettings/ProjectSettings.asset", "bundleVersion: 1.0");
        }

        [TearDown]
        public void TearDown()
        {
            if (_assetRoot != null)
            {
                var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                var target = Path.GetFullPath(Path.Combine(projectRoot, _assetRoot));
                var allowedPrefix = Path.GetFullPath(UnityEngine.Application.dataPath) + Path.DirectorySeparatorChar;
                Assert.That(target.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(target).StartsWith("After333BuildIdentityTests-", StringComparison.Ordinal), Is.True);
                AssetDatabase.DeleteAsset(_assetRoot);
                _assetRoot = null;
            }

            if (_root != null && Directory.Exists(_root))
            {
                var target = Path.GetFullPath(_root);
                var allowedPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                Assert.That(target.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(target).StartsWith("After333BuildIdentityTests-", StringComparison.Ordinal), Is.True);
                Directory.Delete(target, recursive: true);
                _root = null;
            }
        }

        [Test]
        public void Capture_RoundTripsBuildIdentityAndConvertsTimestampToUtc()
        {
            var metadata = Capture();
            Project333ClientBuildProcessor.WriteMetadata(_root, metadata);
            var json = File.ReadAllText(Path.Combine(_root, Project333ClientBuildProcessor.GeneratedAssetPath));
            Assert.That(Project333ClientBuildInfo.TryParseMetadata(json, out var parsed), Is.True);
            Assert.That(parsed.BuildId, Is.EqualTo(metadata.BuildId));
            Assert.That(parsed.BuiltAtUtc, Is.EqualTo("2026-09-13T01:11:12.0000000+00:00"));
            Assert.That(parsed.ApplicationVersion, Is.EqualTo("1.2.3"));
            Assert.That(parsed.CompatibilityVersion, Is.EqualTo(Project333ClientBuildInfo.CurrentClientVersion));
            Assert.That(parsed.Target, Is.EqualTo("Android"));
            Assert.That(parsed.BuildOptions, Is.EqualTo("Development"));
            Assert.That(parsed.UnityVersion, Is.EqualTo("6000.3.12f1"));
            Assert.That(parsed.SourceSha256, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(Project333ClientBuildInfo.FormatDisplay(parsed), Does.Contain(parsed.BuildId));
            Assert.That(Project333ClientBuildInfo.FormatDisplay(parsed), Does.Contain("2026-09-13 01:11 UTC"));
        }

        [Test]
        public void Capture_SameSourceAndTimeStillProduceDistinctBuildIds()
        {
            var first = Capture();
            var second = Capture();
            Assert.That(second.BuildId, Is.Not.EqualTo(first.BuildId));
            Assert.That(second.BuiltAtUtc, Is.EqualTo(first.BuiltAtUtc));
            Assert.That(second.SourceSha256, Is.EqualTo(first.SourceSha256));
        }

        [Test]
        public void Capture_WithoutGitRepositoryKeepsUsableSourceFingerprint()
        {
            var metadata = Capture();
            Assert.That(metadata.GitCommit, Is.EqualTo("unavailable"));
            Assert.That(metadata.GitState, Is.EqualTo("unavailable"));
            Assert.That(metadata.SourceSha256, Is.EqualTo(Project333ClientBuildProcessor.ComputeSourceHash(_root)));
            var json = JsonUtility.ToJson(metadata);
            Assert.That(json, Does.Not.Contain(_root));
        }

        [TestCase("Assets/Card.cs")]
        [TestCase("Assets/Card.cs.meta")]
        [TestCase("Packages/manifest.json")]
        [TestCase("ProjectSettings/ProjectSettings.asset")]
        public void SourceFingerprint_ChangesWhenBuildInputChanges(string relativePath)
        {
            var before = Project333ClientBuildProcessor.ComputeSourceHash(_root);
            File.AppendAllText(Path.Combine(_root, relativePath), "\nchanged", new UTF8Encoding(false));
            Assert.That(Project333ClientBuildProcessor.ComputeSourceHash(_root), Is.Not.EqualTo(before));
        }

        [Test]
        public void SourceFingerprint_ChangesForNewFileAndRename()
        {
            var before = Project333ClientBuildProcessor.ComputeSourceHash(_root);
            Write("Assets/NewCard.cs", "public class NewCard { }");
            var afterAddition = Project333ClientBuildProcessor.ComputeSourceHash(_root);
            Assert.That(afterAddition, Is.Not.EqualTo(before));
            File.Move(Path.Combine(_root, "Assets/NewCard.cs"), Path.Combine(_root, "Assets/RenamedCard.cs"));
            Assert.That(Project333ClientBuildProcessor.ComputeSourceHash(_root), Is.Not.EqualTo(afterAddition));
        }

        [Test]
        public void SourceFingerprint_IgnoresGeneratedMetadataAndItsMetaFile()
        {
            var before = Project333ClientBuildProcessor.ComputeSourceHash(_root);
            Write(Project333ClientBuildProcessor.GeneratedAssetPath, "first identity");
            Write(Project333ClientBuildProcessor.GeneratedAssetPath + ".meta", "first guid");
            Assert.That(Project333ClientBuildProcessor.ComputeSourceHash(_root), Is.EqualTo(before));
            Write(Project333ClientBuildProcessor.GeneratedAssetPath, "replacement identity");
            Write(Project333ClientBuildProcessor.GeneratedAssetPath + ".meta", "replacement guid");
            Assert.That(Project333ClientBuildProcessor.ComputeSourceHash(_root), Is.EqualTo(before));
        }

        [Test]
        public void SourceFingerprint_IsIndependentOfCreationOrderAndTimestamps()
        {
            Write("Assets/Z.cs", "Z");
            Write("Assets/A.cs", "A");
            var before = Project333ClientBuildProcessor.ComputeSourceHash(_root);
            File.Delete(Path.Combine(_root, "Assets/Z.cs"));
            File.Delete(Path.Combine(_root, "Assets/A.cs"));
            Write("Assets/A.cs", "A");
            Write("Assets/Z.cs", "Z");
            File.SetLastWriteTimeUtc(Path.Combine(_root, "Assets/A.cs"), new DateTime(2020, 1, 1));
            Assert.That(Project333ClientBuildProcessor.ComputeSourceHash(_root), Is.EqualTo(before));
        }

        [Test]
        public void SourceFingerprint_FailsForMissingRequiredProjectFolder()
        {
            Directory.Move(Path.Combine(_root, "Packages"), Path.Combine(_root, "Packages-absent"));
            Assert.Throws<DirectoryNotFoundException>(() => Project333ClientBuildProcessor.ComputeSourceHash(_root));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-json")]
        [TestCase("{}")]
        public void Parse_RejectsMissingOrMalformedIdentity(string json)
        {
            Assert.That(Project333ClientBuildInfo.TryParseMetadata(json, out var metadata), Is.False);
            Assert.That(metadata, Is.Null);
        }

        [TestCase("schema")]
        [TestCase("id")]
        [TestCase("application")]
        [TestCase("timestamp")]
        public void Parse_RejectsInvalidRequiredField(string field)
        {
            var metadata = Capture();
            if (field == "schema") metadata.SchemaVersion = 99;
            if (field == "id") metadata.BuildId = " ";
            if (field == "application") metadata.ApplicationVersion = null;
            if (field == "timestamp") metadata.BuiltAtUtc = "yesterday";
            Assert.That(Project333ClientBuildInfo.TryParseMetadata(JsonUtility.ToJson(metadata), out var parsed), Is.False);
            Assert.That(parsed, Is.Null);
        }

        [Test]
        public void SynchronousImport_ResourcesLoadSeesReplacementMetadata()
        {
            var uniqueName = "After333BuildIdentityTests-" + Guid.NewGuid().ToString("N");
            _assetRoot = "Assets/" + uniqueName;
            var assetPath = _assetRoot + "/Resources/" + uniqueName + "/Identity.json";
            var resourcePath = uniqueName + "/Identity";
            var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var absolutePath = Path.Combine(projectRoot, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            var first = Capture();
            File.WriteAllText(absolutePath, JsonUtility.ToJson(first), new UTF8Encoding(false));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var firstAsset = Resources.Load<TextAsset>(resourcePath);
            Assert.That(firstAsset, Is.Not.Null);
            Assert.That(Project333ClientBuildInfo.TryParseMetadata(firstAsset.text, out var firstLoaded), Is.True);
            Assert.That(firstLoaded.BuildId, Is.EqualTo(first.BuildId));

            var second = Capture();
            File.WriteAllText(absolutePath, JsonUtility.ToJson(second), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var secondAsset = Resources.Load<TextAsset>(resourcePath);
            Assert.That(secondAsset, Is.Not.Null);
            Assert.That(Project333ClientBuildInfo.TryParseMetadata(secondAsset.text, out var secondLoaded), Is.True);
            Assert.That(secondLoaded.BuildId, Is.EqualTo(second.BuildId));
            Assert.That(secondLoaded.BuildId, Is.Not.EqualTo(firstLoaded.BuildId));
        }

        [Test]
        public void LogHandler_PreservesFormatArgumentsAndContext()
        {
            var inner = new RecordingLogHandler();
            var handler = new ClientBuildLogHandler(inner, "test-build");
            var context = new GameObject("BuildLogContext");
            try
            {
                var arguments = new object[] { 42, "value {with braces}" };
                handler.LogFormat(LogType.Warning, context, "Count {0}: {1}", arguments);
                Assert.That(handler.Inner, Is.SameAs(inner));
                Assert.That(inner.Events, Is.EqualTo(new[] { "format" }));
                Assert.That(inner.Type, Is.EqualTo(LogType.Warning));
                Assert.That(inner.Context, Is.SameAs(context));
                Assert.That(inner.Arguments, Is.SameAs(arguments));
                Assert.That(string.Format(CultureInfo.InvariantCulture, inner.Format, inner.Arguments),
                    Is.EqualTo("[After333 build=test-build] Count 42: value {with braces}"));
            }
            finally { Object.DestroyImmediate(context); }
        }

        [Test]
        public void LogHandler_EmitsBuildMarkerBeforeOriginalException()
        {
            var inner = new RecordingLogHandler();
            var handler = new ClientBuildLogHandler(inner, "exception-build");
            var context = new GameObject("BuildExceptionContext");
            try
            {
                Exception original;
                try { throw new InvalidOperationException("original failure"); }
                catch (InvalidOperationException exception) { original = exception; }
                var originalStack = original.StackTrace;
                handler.LogException(original, context);
                Assert.That(inner.Events, Is.EqualTo(new[] { "format", "exception" }));
                Assert.That(inner.Format, Does.Contain("[After333 build=exception-build]"));
                Assert.That(inner.Exception, Is.SameAs(original));
                Assert.That(inner.Exception.StackTrace, Is.EqualTo(originalStack));
                Assert.That(inner.Context, Is.SameAs(context));
                Assert.That(inner.ExceptionContext, Is.SameAs(context));
            }
            finally { Object.DestroyImmediate(context); }
        }

        [Test]
        public void EditorInitialization_DiscardsStalePlayerIdentityWithoutWrappingLogger()
        {
            var metadataField = typeof(Project333ClientBuildInfo).GetField("s_metadata", BindingFlags.NonPublic | BindingFlags.Static);
            var initialize = typeof(Project333ClientBuildInfo).GetMethod("InitializeDiagnostics", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(metadataField, Is.Not.Null);
            Assert.That(initialize, Is.Not.Null);
            var previousMetadata = metadataField.GetValue(null);
            var previousLogger = Debug.unityLogger.logHandler;
            try
            {
                metadataField.SetValue(null, Capture());
                initialize.Invoke(null, null);
                Assert.That(Project333ClientBuildInfo.Metadata.BuildId, Is.EqualTo("editor"));
                Assert.That(Project333ClientBuildInfo.BuildDisplayText, Does.Contain("Unity Editor"));
                Assert.That(Debug.unityLogger.logHandler, Is.SameAs(previousLogger));
                initialize.Invoke(null, null);
                Assert.That(Debug.unityLogger.logHandler, Is.SameAs(previousLogger));
            }
            finally { metadataField.SetValue(null, previousMetadata); }
        }

        [Test]
        public void BuildOptions_RejectScriptsOnlyButAllowNormalBuildAndRun()
        {
            Assert.Throws<InvalidOperationException>(() => Project333ClientBuildProcessor.RequireSupportedOptions(
                BuildOptions.Development | BuildOptions.BuildScriptsOnly));
            Assert.DoesNotThrow(() => Project333ClientBuildProcessor.RequireSupportedOptions(BuildOptions.None));
            Assert.DoesNotThrow(() => Project333ClientBuildProcessor.RequireSupportedOptions(
                BuildOptions.Development | BuildOptions.AutoRunPlayer));
        }

        [Test]
        public void SourceFingerprint_ExcludesMediaContentButIncludesImportSettings()
        {
            var before = Project333ClientBuildProcessor.ComputeSourceHash(_root);
            Write("Assets/Portrait.PNG", "first image");
            Assert.That(Project333ClientBuildProcessor.ComputeSourceHash(_root), Is.EqualTo(before));
            Write("Assets/Portrait.PNG", "changed image");
            Assert.That(Project333ClientBuildProcessor.ComputeSourceHash(_root), Is.EqualTo(before));
            Write("Assets/Portrait.PNG.meta", "changed importer");
            Assert.That(Project333ClientBuildProcessor.ComputeSourceHash(_root), Is.Not.EqualTo(before));
        }
        private ClientBuildMetadata Capture() => Project333ClientBuildProcessor.Capture(
            _root, "Android", "1.2.3", "6000.3.12f1", BuildTime, "Development");

        private void Write(string relativePath, string contents)
        {
            var path = Path.Combine(_root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }

        private sealed class RecordingLogHandler : ILogHandler
        {
            public readonly List<string> Events = new List<string>();
            public LogType Type;
            public Object Context;
            public string Format;
            public object[] Arguments;
            public Exception Exception;
            public Object ExceptionContext;

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                Events.Add("format");
                Type = logType;
                Context = context;
                Format = format;
                Arguments = args;
            }

            public void LogException(Exception exception, Object context)
            {
                Events.Add("exception");
                Exception = exception;
                ExceptionContext = context;
            }
        }
    }
}
