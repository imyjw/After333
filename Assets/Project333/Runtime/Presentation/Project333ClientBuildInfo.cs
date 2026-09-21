using System;
using System.Globalization;
using UnityEngine;

namespace Project333.Runtime.Presentation
{
    public static class Project333ClientBuildInfo
    {
        // Compatibility is a separate contract; diagnostic build IDs never change it.
        public const string CurrentClientVersion = "0.1.0-dev";
        public const string LegacyDevelopmentClientVersion = "unity-dev";
        public const string MetadataResourcePath = "Project333/ClientBuild";
        private static ClientBuildMetadata s_metadata;

        public static ClientBuildMetadata Metadata => s_metadata ??= LoadMetadata();
        public static string BuildDisplayText => FormatDisplay(Metadata);

        public static string ResolveClientVersion(string inspectorOverride)
        {
            if (string.IsNullOrWhiteSpace(inspectorOverride) ||
                string.Equals(inspectorOverride.Trim(), LegacyDevelopmentClientVersion, StringComparison.OrdinalIgnoreCase))
                return CurrentClientVersion;
            return inspectorOverride.Trim();
        }

        private static ClientBuildMetadata LoadMetadata()
        {
#if UNITY_EDITOR
            // Editor Play Mode is not the last built player, including with domain reload disabled.
            return new ClientBuildMetadata { BuildId = "editor", ApplicationVersion = UnityEngine.Application.version };
#else
            var asset = Resources.Load<TextAsset>(MetadataResourcePath);
            if (asset != null && TryParseMetadata(asset.text, out var metadata)) return metadata;
            return new ClientBuildMetadata { BuildId = "unavailable", ApplicationVersion = UnityEngine.Application.version };
#endif
        }

        public static bool TryParseMetadata(string json, out ClientBuildMetadata metadata)
        {
            metadata = null;
            try
            {
                var candidate = JsonUtility.FromJson<ClientBuildMetadata>(json);
                if (candidate == null || candidate.SchemaVersion != 1 ||
                    string.IsNullOrWhiteSpace(candidate.BuildId) ||
                    string.IsNullOrWhiteSpace(candidate.ApplicationVersion) ||
                    !DateTimeOffset.TryParseExact(candidate.BuiltAtUtc, "O", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _)) return false;
                metadata = candidate;
                return true;
            }
            catch (Exception) { return false; }
        }

        public static string FormatDisplay(ClientBuildMetadata metadata)
        {
            var title = $"After333 {metadata.ApplicationVersion}";
            if (metadata.BuildId == "editor") return title + "\nUnity Editor · 빌드 전";
            if (metadata.BuildId == "unavailable") return title + "\n빌드 정보 없음";
            var timestamp = DateTimeOffset.ParseExact(metadata.BuiltAtUtc, "O", CultureInfo.InvariantCulture)
                .UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
            return $"{title}\n빌드 {metadata.BuildId}\n{timestamp}";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeDiagnostics()
        {
            s_metadata = null;
            var metadata = Metadata;
#if !UNITY_EDITOR
            // Unwrap our previous handler if initialization repeats; preserve any other handler.
            var handler = Debug.unityLogger.logHandler;
            if (handler is ClientBuildLogHandler previous) handler = previous.Inner;
            Debug.unityLogger.logHandler = new ClientBuildLogHandler(handler, metadata.BuildId);
#endif
            Debug.Log("After333 client build: " + JsonUtility.ToJson(metadata));
        }
    }

    [Serializable]
    public sealed class ClientBuildMetadata
    {
        public int SchemaVersion = 1;
        public string BuildId;
        public string BuiltAtUtc;
        public string ApplicationVersion;
        public string CompatibilityVersion;
        public string Target;
        public string BuildOptions;
        public string UnityVersion;
        public string GitCommit;
        public string GitState;
        public string SourceSha256;
        public string SourceHashScope;
    }

    // Constructed on the Unity thread; logging uses only immutable managed data afterward.
    public sealed class ClientBuildLogHandler : ILogHandler
    {
        public ILogHandler Inner { get; }
        private readonly string _prefix;

        public ClientBuildLogHandler(ILogHandler inner, string buildId)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _prefix = "[After333 build=" + buildId + "] ";
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            Inner.LogFormat(logType, context, _prefix + format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            // Keep original type and stack; put identity immediately before the exception.
            Inner.LogFormat(LogType.Log, context, _prefix + "exception context");
            Inner.LogException(exception, context);
        }
    }
}
