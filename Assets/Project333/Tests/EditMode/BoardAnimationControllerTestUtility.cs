using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    internal static class BoardAnimationControllerTestUtility
    {
        private static readonly List<ScriptableObject> TemporaryAssets = new List<ScriptableObject>();
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static AnimatorController Controller(string card)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Unit/" + card + "/" + card + ".controller");
            Assert.That(controller, Is.Not.Null, card);
            return controller;
        }
        internal static AnimationClip Clip(string card, string state)
        {
            var match = Controller(card).layers[0].stateMachine.states.Single(s => s.state.name == state).state;
            var clip = match.motion as AnimationClip;
            Assert.That(clip, Is.Not.Null, card + "/" + state);
            return clip;
        }
        internal static Sprite[] Frames(AnimationClip clip)
        {
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(b => b.propertyName == "m_Sprite");
            return AnimationUtility.GetObjectReferenceCurve(clip, binding).Select(k => k.value as Sprite).ToArray();
        }
        // Exercise the real card -> catalog -> bootstrapper -> tile binding path.
        // Host stays inactive so no login, HTTP, battle startup or scene mutation runs.
        internal static void Bind(TileTextView view, string card, RuntimeAnimatorController replacement = null)
        {
            var host = new GameObject("TestCardCatalogHost"); host.SetActive(false);
            host.transform.SetParent(view.transform, false);
            var bootstrapper = host.AddComponent<BattleBootstrapper>();
            var asset = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            asset.hideFlags = HideFlags.DontSave;
            asset.ConfigureBaseForTests(card, card, new ResourceSetData());
            asset.ConfigureBoardVisualsForTests(Frames(Clip(card, "Idle"))[0], replacement != null ? replacement : Controller(card));
            var catalog = ScriptableObject.CreateInstance<CardDefinitionCatalogAsset>();
            catalog.hideFlags = HideFlags.DontSave;
            catalog.ConfigureForTests(new CardDefinitionAsset[] { asset });
            TemporaryAssets.Add(asset); TemporaryAssets.Add(catalog);
            typeof(BattleBootstrapper).GetField("_cardCatalogAsset", Fields).SetValue(bootstrapper, catalog);
            typeof(TileTextView).GetField("_battleBootstrapper", Fields).SetValue(view, bootstrapper);
        }
        internal static void Cleanup()
        {
            foreach (var asset in TemporaryAssets) if (asset != null) Object.DestroyImmediate(asset);
            TemporaryAssets.Clear();
        }
    }
}