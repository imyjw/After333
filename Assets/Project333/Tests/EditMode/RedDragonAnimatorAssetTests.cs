using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    // Asset integration checks: no runtime replacement animation and no content mutations.
    public sealed class RedDragonAnimatorAssetTests
    {
        private const string Folder = "Assets/Unit/RedDragon/";
        private static readonly string[] Motions = { "Idle", "Attack", "BeAttacked", "Run", "Death" };

        [TestCase("Idle", 6f, true)]
        [TestCase("Attack", 12f, false)]
        [TestCase("BeAttacked", 15f, false)]
        [TestCase("Run", 12f, true)]
        [TestCase("Death", 8f, false)]
        public void ControllerClipsReferenceSixFull512CellsWithoutTransformScale(string motion, float fps, bool loop)
        {
            var state = Controller().layers[0].stateMachine.states.Single(s => s.state.name == motion).state;
            var clip = state.motion as AnimationClip;
            Assert.That(clip, Is.Not.Null, motion);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(Folder + motion + ".anim"));
            Assert.That(clip.frameRate, Is.EqualTo(fps));
            Assert.That(clip.isLooping, Is.EqualTo(loop));
            Assert.That(clip.length, Is.EqualTo(6f / fps).Within(.0001f));
            Assert.That(state.speed, Is.EqualTo(1f));
            Assert.That(state.transitions, Is.Empty, "Battle presentation owns animation completion and interrupts.");
            Assert.That(AnimationUtility.GetCurveBindings(clip), Is.Empty, "No implicit scale/position/color animation.");
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.That(bindings.Length, Is.EqualTo(1));
            Assert.That(bindings[0].type, Is.EqualTo(typeof(SpriteRenderer)));
            Assert.That(bindings[0].propertyName, Is.EqualTo("m_Sprite"));
            Assert.That(bindings[0].path, Is.Empty);
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            Assert.That(keys.Length, Is.EqualTo(6));
            for (int i = 0; i < keys.Length; i++)
            {
                var sprite = keys[i].value as Sprite;
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.name, Is.EqualTo(motion + "_" + i));
                Assert.That(sprite.rect, Is.EqualTo(new Rect(i * 512, 0, 512, 512)));
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(256, 256)));
                Assert.That(sprite.texture.width, Is.EqualTo(3072));
                Assert.That(sprite.texture.height, Is.EqualTo(512));
                Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(Folder + motion + ".png"));
                Assert.That(keys[i].time, Is.EqualTo(i / fps).Within(.0001f));
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + motion + ".png");
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(3072));
        }

        [TestCase("Idle")]
        [TestCase("Attack")]
        [TestCase("BeAttacked")]
        [TestCase("Run")]
        [TestCase("Death")]
        public void AuthoredCellsHaveGroundMarginAndTransparentEdges(string motion)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(Folder + motion + ".png"), false), Is.True);
                Assert.That(texture.width, Is.EqualTo(3072)); Assert.That(texture.height, Is.EqualTo(512));
                var pixels = texture.GetPixels32();
                for (int frame = 0; frame < 6; frame++)
                {
                    int minX = 512, minY = 512, maxX = -1, maxY = -1;
                    for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++)
                    {
                        if (pixels[y * texture.width + frame * 512 + x].a < 16) continue;
                        minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                    }
                    string label = motion + "/" + frame;
                    Assert.That(maxX, Is.GreaterThanOrEqualTo(0), label + " must not be blank.");
                    Assert.That(minY, Is.InRange(8, 24), label + " bottom margin should be about16px.");
                    Assert.That(minX, Is.GreaterThanOrEqualTo(1), label + " left boundary.");
                    Assert.That(maxX, Is.LessThanOrEqualTo(510), label + " right boundary.");
                    Assert.That(maxY, Is.LessThanOrEqualTo(510), label + " top boundary.");
                }
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void OriginalControllerAndIdleReferenceRemainCompatibleWithExistingCard()
        {
            Assert.That(AssetDatabase.AssetPathToGUID(Folder + "RedDragon.controller"), Is.EqualTo("96d2d0d396818d24dbc6380f8d565c28"));
            Assert.That(AssetDatabase.AssetPathToGUID(Folder + "Idle.png"), Is.EqualTo("344e1e15725a74e4e8b22f54f47c25cd"));
            var idle = AssetDatabase.LoadAllAssetsAtPath(Folder + "Idle.png").OfType<Sprite>().Single(s => s.name == "Idle_0");
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(idle, out string _, out long localId), Is.True);
            Assert.That(localId, Is.EqualTo(954118651L), "Keep the already serialized card BoardSprite valid.");
            Assert.That(Controller().layers[0].stateMachine.defaultState.name, Is.EqualTo("Idle"));
            CollectionAssert.AreEquivalent(Motions.Concat(new[] { "Drained" }), Controller().layers[0].stateMachine.states.Select(s => s.state.name));
        }

        [Test]
        public void DrainedHoldsIdleFirstSprite()
        {
            var machine = Controller().layers[0].stateMachine;
            var clip = machine.states.Single(s => s.state.name == "Drained").state.motion as AnimationClip;
            Assert.That(clip, Is.Not.Null); Assert.That(clip.isLooping, Is.True);
            var frames = AnimationUtility.GetObjectReferenceCurve(clip, AnimationUtility.GetObjectReferenceCurveBindings(clip).Single());
            Assert.That(frames.Length, Is.EqualTo(1));
            Assert.That(frames[0].value, Is.SameAs(AssetDatabase.LoadAllAssetsAtPath(Folder + "Idle.png").OfType<Sprite>().Single(s => s.name == "Idle_0")));
        }

        private static AnimatorController Controller()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "RedDragon.controller");
            Assert.That(controller, Is.Not.Null);
            return controller;
        }
    }
}