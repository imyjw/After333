#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Project333.Editor
{
    public static class After333SecureStorageBuildProcessor
    {
        [PostProcessBuild(200)]
        public static void AddKeychainFramework(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            var projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(
                project.GetUnityMainTargetGuid(),
                "Security.framework",
                weak: false);
            File.WriteAllText(projectPath, project.WriteToString());
        }
    }
}
#endif
