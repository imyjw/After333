using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class CardDrawOriginCacheTests
    {
        private static int Count(CardDrawOriginCache cache) => (int)typeof(CardDrawOriginCache)
            .GetProperty("RecalculationCount",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cache);

        [Test] public void UnchangedGeometryReusesOneProjectionForOneHundredReads()
        {
            using(var f=new Fixture())
            {
                var previousConversions=0;
                for(var i=0;i<100;i++) {Assert.That(CardDrawFlight.Pose.TryCapture(f.Source,f.Parent,out _),Is.True);previousConversions++;}
                for(var i=0;i<100;i++) f.AssertPose();
                Assert.That(Count(f.Cache),Is.EqualTo(1));
                TestContext.WriteLine("Unchanged origin reads=100; previous full projections="+previousConversions+"; cached full projections="+Count(f.Cache));
                f.Image.color=Color.red;f.AssertPose();Assert.That(Count(f.Cache),Is.EqualTo(1),"Tint does not change source geometry.");
            }
        }

        [Test] public void SourceParentSpriteAndCanvasChangesInvalidateOncePerChange()
        {
            using(var f=new Fixture())
            {
                f.AssertPose();
                foreach(var mutate in new Action[]{
                    ()=>f.Source.anchoredPosition+=new Vector2(75,20),
                    ()=>f.Source.localRotation=Quaternion.Euler(0,0,17),
                    ()=>f.Source.sizeDelta+=new Vector2(30,40),
                    ()=>f.Parent.localScale=Vector3.one*.5f,
                    ()=>f.Parent.sizeDelta+=new Vector2(50,60),
                    ()=>f.SourceCanvas.scaleFactor=1.25f,
                    ()=>f.SourceCanvas.pixelPerfect=!f.SourceCanvas.pixelPerfect,
                    ()=>f.Image.sprite=f.Alternate,
                    ()=>f.Image.preserveAspect=false,
                    // SafeAreaFitter changes ancestors/anchors; those resulting transforms
                    // invalidate even if the physical display is unchanged in an Editor test.
                    ()=>f.Source.anchorMin=f.Source.anchorMax=new Vector2(.8f,.1f)})
                {
                    var before=Count(f.Cache);mutate();Canvas.ForceUpdateCanvases();f.AssertPose();
                    Assert.That(Count(f.Cache),Is.EqualTo(before+1));
                    for(var i=0;i<10;i++)f.AssertPose();
                    Assert.That(Count(f.Cache),Is.EqualTo(before+1),"Settled layout must not keep projecting.");
                }
            }
        }

        [Test] public void ProjectionViewportAndCanvasHierarchyChangesRefreshCachedOrigin()
        {
            using(var f=new Fixture())
            {
                var camera=f.CreateCamera();f.SourceCanvas.worldCamera=camera;f.TargetCanvas.worldCamera=camera;
                f.AssertPose();
                foreach(var mutate in new Action[]{
                    ()=>camera.transform.position+=new Vector3(1,2,0),
                    ()=>camera.orthographicSize+=2,
                    ()=>camera.pixelRect=new Rect(0,0,320,240),
                    ()=>{var canvas=f.CreateCanvas("ReparentedSource");canvas.worldCamera=camera;f.Source.SetParent(canvas.transform,true);}})
                {
                    var before=Count(f.Cache);mutate();Canvas.ForceUpdateCanvases();f.AssertPose();
                    Assert.That(Count(f.Cache),Is.EqualTo(before+1));f.AssertPose();Assert.That(Count(f.Cache),Is.EqualTo(before+1));
                }
            }
        }

        [Test] public void HiddenFinalDeckLayerKeepsPoseAndClearStartsNewCacheLifetime()
        {
            using(var f=new Fixture())
            {
                f.AssertPose();f.Source.gameObject.SetActive(false);f.AssertPose();Assert.That(Count(f.Cache),Is.EqualTo(1));
                f.Source.anchoredPosition+=new Vector2(-30,15);f.AssertPose();Assert.That(Count(f.Cache),Is.EqualTo(2));
                f.Cache.Clear();Assert.That(Count(f.Cache),Is.Zero);
                f.AssertPose();f.AssertPose();Assert.That(Count(f.Cache),Is.EqualTo(1));
            }
        }

        private sealed class Fixture:IDisposable
        {
            internal readonly CardDrawOriginCache Cache=new CardDrawOriginCache();
            internal readonly Canvas SourceCanvas,TargetCanvas;
            internal readonly RectTransform Source,Parent;
            internal readonly UnityEngine.UI.Image Image;
            internal readonly Sprite Alternate;
            private readonly List<GameObject> _roots=new List<GameObject>();
            private readonly Texture2D _texture;
            private readonly Sprite _sprite;
            internal Fixture()
            {
                SourceCanvas=CreateCanvas("OriginCanvas");TargetCanvas=CreateCanvas("HandCanvas");Parent=(RectTransform)TargetCanvas.transform;
                var source=new GameObject("DeckCardBack",typeof(RectTransform),typeof(UnityEngine.UI.Image));source.transform.SetParent(SourceCanvas.transform,false);
                Source=source.GetComponent<RectTransform>();Source.sizeDelta=new Vector2(100,150);Source.anchoredPosition=new Vector2(200,-100);
                _texture=new Texture2D(16,16);_sprite=Sprite.Create(_texture,new Rect(0,0,8,16),Vector2.one*.5f);Alternate=Sprite.Create(_texture,new Rect(0,0,16,8),Vector2.one*.5f);
                Image=source.GetComponent<UnityEngine.UI.Image>();Image.sprite=_sprite;Image.preserveAspect=true;Canvas.ForceUpdateCanvases();
            }
            internal Canvas CreateCanvas(string name)
            {
                var root=new GameObject(name,typeof(RectTransform),typeof(Canvas));_roots.Add(root);
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                root.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,600);return canvas;
            }
            internal Camera CreateCamera()
            {
                var root=new GameObject("ProjectionCamera",typeof(Camera));_roots.Add(root);
                var camera=root.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=500;camera.transform.position=new Vector3(0,0,-10);return camera;
            }
            internal void AssertPose()
            {
                Assert.That(Cache.TryGetPose(Source,Parent,out var actual),Is.True);
                Assert.That(CardDrawFlight.Pose.TryCapture(Source,Parent,out var expected),Is.True);
                Assert.That(Vector2.Distance(actual.Center,expected.Center),Is.LessThan(.001f));
                Assert.That(Vector2.Distance(actual.Size,expected.Size),Is.LessThan(.001f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(actual.Angle,expected.Angle)),Is.LessThan(.001f));
            }
            public void Dispose()
            {foreach(var root in _roots)if(root!=null)Object.DestroyImmediate(root);Object.DestroyImmediate(_sprite);Object.DestroyImmediate(Alternate);Object.DestroyImmediate(_texture);}
        }
    }
}
