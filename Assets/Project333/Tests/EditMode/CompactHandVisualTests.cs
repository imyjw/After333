using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class CompactHandVisualTests
    {
        [TestCase(1)] [TestCase(6)] [TestCase(10)]
        public void HandFitsBottomCenter_ExpandedRestoresExistingLayout(int count)
        {
            var root = new GameObject("HandPreview",typeof(RectTransform),typeof(Canvas));
            var cameraGo = new GameObject("PreviewCamera",typeof(Camera));
            var rt = new RenderTexture(1920,1080,24);
            var camera=cameraGo.GetComponent<Camera>(); var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=rt; camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.14f,.18f);
                camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                var layoutRoot=new GameObject("RuntimeHandRoot",typeof(RectTransform));layoutRoot.transform.SetParent(root.transform,false);
                var rect=(RectTransform)layoutRoot.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(.5f,0);rect.sizeDelta=new Vector2(0,440);
                var presenter=root.AddComponent<HandPresenter>(); var cards=new HandCardView[count];
                for(int i=0;i<count;i++){
                    var slot=new GameObject("Card"+i,typeof(RectTransform),typeof(HandCardView),typeof(HandCardTextView));slot.transform.SetParent(root.transform,false);
                    cards[i]=slot.GetComponent<HandCardView>();cards[i].Configure(i);cards[i].Present(i%2==0?"BlueDragon":"Gwangma");
                    slot.GetComponent<HandCardTextView>().BindRuntimeHandRoot(rect);
                }
                Set(presenter,"_handCardViews",cards);Set(presenter,"_runtimeHandRoot",rect);
                Canvas.ForceUpdateCanvases();
                Assert.That(presenter.IsExpanded,Is.False);
                Invoke(presenter,"ApplyCurvedLayout");Canvas.ForceUpdateCanvases();
                var compact=rect.Cast<Transform>().Select(t=>((RectTransform)t).localScale.x).ToArray();
                Assert.That(compact.All(x=>x<.51f),Is.True);
                if(count==6) Capture(camera,rt,"compact");
                presenter.SetExpanded(true);Invoke(presenter,"ApplyCurvedLayout");Canvas.ForceUpdateCanvases();
                var expanded=rect.Cast<Transform>().Select(t=>((RectTransform)t).localScale.x).ToArray();
                for(int i=0;i<count;i++) Assert.That(expanded[i],Is.GreaterThan(compact[i]*2));
                if(count==6) Capture(camera,rt,"expanded");
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(root);Object.DestroyImmediate(cameraGo);}
        }
        private static void Capture(Camera camera,RenderTexture rt,string name){
            camera.Render();RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            try{image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();var dir=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../TempBuild/CompactHandPreview"));Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());}
            finally{Object.DestroyImmediate(image);}
        }
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    }
}
