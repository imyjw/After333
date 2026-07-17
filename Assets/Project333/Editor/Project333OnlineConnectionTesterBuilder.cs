using Project333.Runtime.Presentation.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project333.Editor
{
    public static class Project333OnlineConnectionTesterBuilder
    {
        private const string TesterName = "OnlineBattleConnectionTester";
        private const string PlayerATesterName = "OnlineBattleConnectionTester_PlayerA";
        private const string PlayerBTesterName = "OnlineBattleConnectionTester_PlayerB";
        private const string DefaultServerUrl = "ws://127.0.0.1:7333/battle";
        private const string DefaultMatchId = "local-test";

        [MenuItem("Tools/Project333/Online/Create Connection Tester")]
        public static void CreateConnectionTester()
        {
            var existing = Object.FindFirstObjectByType<OnlineBattleConnectionTester>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            var testerObject = new GameObject(TesterName);
            testerObject.AddComponent<OnlineBattleConnectionTester>();
            Selection.activeObject = testerObject;
            EditorGUIUtility.PingObject(testerObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Project333/Online/Create Two Local Testers")]
        public static void CreateTwoLocalTesters()
        {
            var playerA = GetOrCreateTester(PlayerATesterName);
            playerA.ConfigureForLocalTest(DefaultServerUrl, DefaultMatchId, "player-a", PlayerId.Player, presentStateViewsToBattleScreen: true);
            EditorUtility.SetDirty(playerA);

            var playerB = GetOrCreateTester(PlayerBTesterName);
            playerB.ConfigureForLocalTest(DefaultServerUrl, DefaultMatchId, "player-b", PlayerId.AI, presentStateViewsToBattleScreen: false);
            EditorUtility.SetDirty(playerB);

            var battleBootstrapper = Object.FindFirstObjectByType<BattleBootstrapper>();
            if (battleBootstrapper != null)
            {
                battleBootstrapper.ConfigureOnlineInput(playerA, sendPlayerActionsToOnlineGateway: true);
                EditorUtility.SetDirty(battleBootstrapper);
            }

            Selection.objects = new Object[] { playerA.gameObject, playerB.gameObject };
            EditorGUIUtility.PingObject(playerA.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static OnlineBattleConnectionTester GetOrCreateTester(string objectName)
        {
            var existingObject = GameObject.Find(objectName);
            if (existingObject != null && existingObject.TryGetComponent<OnlineBattleConnectionTester>(out var existingTester))
            {
                return existingTester;
            }

            var testerObject = existingObject != null
                ? existingObject
                : new GameObject(objectName);

            return testerObject.GetComponent<OnlineBattleConnectionTester>()
                ?? testerObject.AddComponent<OnlineBattleConnectionTester>();
        }
    }
}
