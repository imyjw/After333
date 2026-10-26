using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Settings;
using Project333.Runtime.Presentation.Startup;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class AccountLoginGateLayoutTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(1920,1080,0,0)]
        [TestCase(2340,1080,92,0)]
        [TestCase(2340,1080,0,92)]
        [TestCase(2560,1080,120,24)]
        [TestCase(1280,720,50,20)]
        public void SafeAreaNestedLogin_BackdropCoversEntireRootCanvas(int width,int height,int left,int right)
        {
            var owner = new GameObject("FullScreen", typeof(RectTransform), typeof(Canvas));
            try
            {
                var canvas=owner.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var root=(RectTransform)owner.transform;root.sizeDelta=new Vector2(width,height);root.localScale=Vector3.one;
                var safe=new GameObject("SafeArea",typeof(RectTransform)).GetComponent<RectTransform>();safe.SetParent(root,false);
                safe.anchorMin=new Vector2((float)left/width,0);safe.anchorMax=new Vector2(1f-(float)right/width,1);safe.offsetMin=Vector2.zero;safe.offsetMax=Vector2.zero;
                var loginObject=new GameObject("AccountLoginGateCanvas",typeof(RectTransform));loginObject.transform.SetParent(safe,false);
                var login=loginObject.AddComponent<AccountLoginGateView>();login.EnsureEditableHierarchy();
                canvas.renderMode=RenderMode.WorldSpace;root.sizeDelta=new Vector2(width,height);root.localScale=Vector3.one;
                foreach(var inset in new[]{new Vector2(left,right),new Vector2(right,left),Vector2.zero})
                {
                    safe.anchorMin=new Vector2(inset.x/width,0);safe.anchorMax=new Vector2(1f-inset.y/width,1);
                    Canvas.ForceUpdateCanvases();typeof(AccountLoginGateView).GetMethod("LateUpdate",Hidden)?.Invoke(login,null);
                    Canvas.ForceUpdateCanvases();
                    var backdrop=(Image)Get(login,"_backdrop");
                    var corners=new Vector3[4];backdrop.rectTransform.GetWorldCorners(corners);
                    var a=root.InverseTransformPoint(corners[0]);var b=root.InverseTransformPoint(corners[2]);
                    Assert.That(a.x,Is.EqualTo(root.rect.xMin).Within(.02f),"Backdrop must extend past an asymmetric safe area.");
                    Assert.That(a.y,Is.EqualTo(root.rect.yMin).Within(.02f));
                    Assert.That(b.x,Is.EqualTo(root.rect.xMax).Within(.02f));Assert.That(b.y,Is.EqualTo(root.rect.yMax).Within(.02f));
                    Assert.That(backdrop.raycastTarget,Is.True);
                    login.EnsureEditableHierarchy();canvas.renderMode=RenderMode.WorldSpace;root.sizeDelta=new Vector2(width,height);
                }
            }
            finally{Object.DestroyImmediate(owner);}
        }

        [TestCase(1920,1080,0,0)]
        [TestCase(2340,1080,92,0)]
        [TestCase(2560,1080,120,24)]
        [TestCase(1280,720,50,20)]
        public void NativeStart_LoginBlocksTutorialGiftWalletButSettingsRemainClickable(int width,int height,int left,int right)
        {
            var scene=EditorSceneManager.OpenScene("Assets/GameStart_VSlice.unity",OpenSceneMode.Additive);
            var cameraObject=new GameObject("LoginViewportCamera");var camera=cameraObject.AddComponent<Camera>();
            camera.transform.position=new Vector3(0,0,-1000);camera.orthographic=true;camera.orthographicSize=height/2f;
            camera.nearClipPlane=.1f;camera.farClipPlane=2000;
            var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
            try
            {
                var scripts=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                foreach(var script in scripts)if(script!=null && script.GetType().Assembly==typeof(AccountLoginGateView).Assembly)script.enabled=false;
                foreach(var cam in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>(true)))cam.enabled=false;
                var start=scripts.OfType<GameStartSceneController>().Single();
                var settings=scripts.OfType<GlobalSettingsMenuController>().Single();settings.EnsureEditableHierarchy();
                typeof(GlobalSettingsMenuController).GetField("_sceneName",Hidden).SetValue(settings,scene.name);
                typeof(GlobalSettingsMenuController).GetMethod("BindUi",Hidden).Invoke(settings,null);
                var gift=scripts.OfType<GiftInboxController>().Single();gift.EnsureEditableHierarchy();gift.Close();
                typeof(GameStartSceneController).GetMethod("EnsureTutorialEntryButton",Hidden).Invoke(start,null);
                var login=scripts.OfType<AccountLoginGateView>().Single();login.EnsureEditableHierarchy();login.SetVisible(true);login.SetState(false,"로그인 방법을 선택해주세요.",true);
                settings.CloseMenu();
                var rootCanvas=start.GetComponentInParent<Canvas>();
                foreach(var canvas in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Canvas>(true)).Where(x=>x.isRootCanvas))
                {
                    var scaler=canvas.GetComponent<CanvasScaler>();if(scaler!=null)scaler.enabled=false;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;
                    var rect=(RectTransform)canvas.transform;rect.localScale=Vector3.one;rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition3D=Vector3.zero;
                }
                foreach(var canvas in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Canvas>(true)))canvas.worldCamera=camera;
                var content=(RectTransform)start.transform;
                content.anchorMin=new Vector2((float)left/width,0);content.anchorMax=new Vector2(1f-(float)right/width,1);content.offsetMin=Vector2.zero;content.offsetMax=Vector2.zero;
                Canvas.ForceUpdateCanvases();typeof(AccountLoginGateView).GetMethod("LateUpdate",Hidden)?.Invoke(login,null);Canvas.ForceUpdateCanvases();camera.Render();
                var events=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<EventSystem>(true)).Single();
                foreach(var raycaster in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<BaseRaycaster>(true)))
                    if(raycaster.isActiveAndEnabled)typeof(BaseRaycaster).GetMethod("OnEnable",Hidden).Invoke(raycaster,null);
                Vector2 Center(Component c){var r=(RectTransform)c.transform;return RectTransformUtility.WorldToScreenPoint(camera,r.TransformPoint(r.rect.center));}
                RaycastResult Hit(Vector2 point){var hits=new List<RaycastResult>();events.RaycastAll(new PointerEventData(events){position=point},hits);Assert.That(hits,Is.Not.Empty);return hits[0];}
                void Capture(string state)
                {
                    Canvas.ForceUpdateCanvases();camera.Render();
                    var directory=Environment.GetEnvironmentVariable("AFTER333_LOGIN_CAPTURE_DIR");
                    if(string.IsNullOrWhiteSpace(directory))return;
                    Directory.CreateDirectory(directory);
                    var old=RenderTexture.active;RenderTexture.active=target;
                    var image=new Texture2D(width,height,TextureFormat.RGBA32,false);
                    try{image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,state+"-"+width+"x"+height+".png"),image.EncodeToPNG());}
                    finally{RenderTexture.active=old;Object.DestroyImmediate(image);}
                }
                Capture("login");
                var backdrop=(Image)Get(login,"_backdrop");
                var tutorial=(Button)Get(start,"_tutorialButton");var giftButton=(Button)Get(gift,"_giftButton");
                var wallet=settings.transform.Find("SafeAreaRoot/WalletPanel");var gear=(Button)Get(settings,"_gearButton");
                Assert.That(wallet.GetComponent<Canvas>().sortingOrder, Is.LessThan(login.GetComponent<Canvas>().sortingOrder));
                foreach (var control in new Component[] { tutorial, giftButton, wallet })
                {
                    // The fixed-size login panel can also cover a menu control on
                    // a small viewport. Both panel and backdrop must consume input.
                    var hit = Hit(Center(control));
                    Assert.That(hit.gameObject.transform.IsChildOf(login.transform), Is.True,
                        control.name + " must be dimmed and blocked by login.");
                }
                foreach(var point in new[]{new Vector2(2,2),new Vector2(width-2,2),new Vector2(2,height-2)})
                    Assert.That(Hit(point).gameObject,Is.EqualTo(backdrop.gameObject),"Notch/outer-screen input must be blocked.");
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hit(Center(gear)).gameObject),Is.EqualTo(gear.gameObject));
                var pointer=new PointerEventData(events){position=Center(gear),button=PointerEventData.InputButton.Left};
                ExecuteEvents.Execute(gear.gameObject,pointer,ExecuteEvents.pointerClickHandler);
                Assert.That(((RectTransform)Get(settings,"_modalRoot")).gameObject.activeSelf,Is.True);
                Capture("settings");
                var close=(Button)Get(settings,"_closeButton");
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hit(Center(close)).gameObject),Is.EqualTo(close.gameObject));
                settings.CloseMenu();
                login.SetVisible(false);Canvas.ForceUpdateCanvases();camera.Render();
                Capture("menu");
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hit(Center(tutorial)).gameObject),Is.EqualTo(tutorial.gameObject));
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hit(Center(giftButton)).gameObject),Is.EqualTo(giftButton.gameObject));
                login.SetVisible(true);login.EnsureEditableHierarchy();
                rootCanvas.renderMode=RenderMode.ScreenSpaceCamera;rootCanvas.worldCamera=camera;Canvas.ForceUpdateCanvases();
            }
            finally{Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(target);EditorSceneManager.CloseScene(scene,true);}
        }
        private static object Get(object owner,string field)=>owner.GetType().GetField(field,Hidden).GetValue(owner);
    }
}
