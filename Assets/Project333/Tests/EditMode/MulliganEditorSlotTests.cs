using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class MulliganEditorSlotTests
    {
        [Test]
        public void EditorMaterialization_AddsFifthSlotWithoutReplacingExistingSlots_AndSavesReferences()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Battle_VSlice.unity", OpenSceneMode.Additive);
            var savedPath = "Assets/__MulliganEditorSlot_" + Guid.NewGuid().ToString("N") + ".unity";
            try
            {
                var screen = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<BattleScreenPresenter>(true)).Single();
                var overlay = screen.TutorialMulligan;
                var data = new SerializedObject(overlay);
                var original = Enumerable.Range(0, data.FindProperty("_cardButtons").arraySize)
                    .Select(i => data.FindProperty("_cardButtons").GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
                var group = overlay.GetComponent<CanvasGroup>();
                var originalAlpha = group.alpha;
                var originalActive = overlay.gameObject.activeSelf;
                screen.MaterializePersistentBattleUiForEditor();
                data.Update();
                Assert.That(data.FindProperty("_cardButtons").arraySize, Is.EqualTo(5));
                for (var i = 0; i < original.Length; i++)
                    Assert.That(data.FindProperty("_cardButtons").GetArrayElementAtIndex(i).objectReferenceValue, Is.SameAs(original[i]));
                AssertFifthReferences(overlay);
                var count = overlay.GetComponentsInChildren<Transform>(true).Length;
                screen.MaterializePersistentBattleUiForEditor();
                Assert.That(overlay.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(group.alpha, Is.EqualTo(originalAlpha));
                Assert.That(overlay.gameObject.activeSelf, Is.EqualTo(originalActive));
                Assert.That(scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<BattleBootstrapper>(true)).Single().CurrentBattleState, Is.Null);
                Assert.That(EditorSceneManager.SaveScene(scene, savedPath, true), Is.True);
                var saved = EditorSceneManager.OpenPreviewScene(savedPath);
                try { AssertFifthReferences(saved.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<BattleMulliganOverlayPresenter>(true)).Single()); }
                finally { EditorSceneManager.ClosePreviewScene(saved); }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(savedPath);
            }
        }

        private static void AssertFifthReferences(BattleMulliganOverlayPresenter overlay)
        {
            var data = new SerializedObject(overlay);
            var slots = data.FindProperty("_cardButtons");
            Assert.That(slots.arraySize, Is.EqualTo(5));
            var fifth = (Button)slots.GetArrayElementAtIndex(4).objectReferenceValue;
            Assert.That(fifth, Is.Not.Null);
            Assert.That(fifth.name, Is.EqualTo("MulliganCard_04"));
            Assert.That(overlay.GetComponentsInChildren<Button>(true).Count(x => x.name == "MulliganCard_04"), Is.EqualTo(1));
            foreach (var field in new[] { "_cardArtworkImages", "_selectionOverlays", "_fallbackCardIdTexts", "_attackValueTexts", "_hpValueTexts" })
            {
                var refs = data.FindProperty(field);
                Assert.That(refs.arraySize, Is.EqualTo(5), field);
                var component = refs.GetArrayElementAtIndex(4).objectReferenceValue as Component;
                Assert.That(component, Is.Not.Null, field);
                Assert.That(component.transform.IsChildOf(fifth.transform), Is.True, field);
            }
        }
    }
}
