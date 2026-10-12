using System.IO;
using NUnit.Framework;
using Project333.Runtime.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public class NoticeThemeTests
    {
        [Test]
        public void FontAndResponsiveNotice_RenderKoreanAndWrap()
        {
            var root = new GameObject("Preview", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("Camera",typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var rt = new RenderTexture(1280,720,24);
            Texture2D capture = null;
            var previous = RenderTexture.active;
            try
            {
                Assert.That(NoticeTheme.Font, Is.Not.Null);
                NoticeTheme.Font.RequestCharactersInTexture("이 유닛은 이미 공격했습니다.확인취소",28);
                foreach(var c in "이유닛은이미공격했습니다확인취소") Assert.That(NoticeTheme.Font.HasCharacter(c), Is.True, c.ToString());
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.15f,.19f);
                camera.transform.position=new Vector3(0,0,-10); camera.orthographic=true; camera.targetTexture=rt;
                var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
                var panel=new GameObject("Toast",typeof(RectTransform),typeof(Image)); panel.transform.SetParent(root.transform,false);
                var label=new GameObject("Message",typeof(RectTransform),typeof(Text)); label.transform.SetParent(panel.transform,false);
                var text=label.GetComponent<Text>(); text.text="이 유닛은 이미 공격했습니다.";
                Canvas.ForceUpdateCanvases(); NoticeTheme.Toast((RectTransform)panel.transform,text); Canvas.ForceUpdateCanvases();
                var shortHeight=((RectTransform)panel.transform).rect.height;
                Assert.That(text.raycastTarget,Is.False); Assert.That(panel.GetComponent<Image>().raycastTarget,Is.False);
                Assert.That(text.font,Is.SameAs(NoticeTheme.Font));
                text.text="서버와 연결이 끊어졌습니다. 잠시 후 다시 연결합니다. "+new string('가',80);
                NoticeTheme.Toast((RectTransform)panel.transform,text); Canvas.ForceUpdateCanvases();
                Assert.That(((RectTransform)panel.transform).rect.height,Is.GreaterThan(shortHeight));
                text.text="이 유닛은 이미 공격했습니다."; NoticeTheme.Toast((RectTransform)panel.transform,text);
                var modal=new GameObject("Confirmation",typeof(RectTransform),typeof(Image)); modal.transform.SetParent(root.transform,false);
                var mr=(RectTransform)modal.transform; mr.anchorMin=mr.anchorMax=new Vector2(.5f,.45f); mr.sizeDelta=new Vector2(620,230);
                var body=new GameObject("Text",typeof(RectTransform),typeof(Text)); body.transform.SetParent(modal.transform,false);
                var br=(RectTransform)body.transform; br.anchorMin=new Vector2(0,.4f); br.anchorMax=Vector2.one; br.offsetMin=new Vector2(28,0); br.offsetMax=new Vector2(-28,-10);
                body.GetComponent<Text>().text="게임을 종료하시겠습니까?";
                for(int i=0;i<2;i++){
                    var button=new GameObject("Button",typeof(RectTransform),typeof(Image),typeof(Button)); button.transform.SetParent(modal.transform,false);
                    var b=(RectTransform)button.transform; b.anchorMin=b.anchorMax=new Vector2(.5f,.23f); b.anchoredPosition=new Vector2(i==0?-112:112,0); b.sizeDelta=new Vector2(200,58);
                    var t=new GameObject("Label",typeof(RectTransform),typeof(Text)); t.transform.SetParent(button.transform,false);
                    var tr=(RectTransform)t.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
                    t.GetComponent<Text>().text=i==0?"취소":"확인";
                }
                NoticeTheme.Dialog(mr);
                var timer=new GameObject("Timer",typeof(RectTransform),typeof(Image));timer.transform.SetParent(root.transform,false);
                var timerRect=(RectTransform)timer.transform;timerRect.anchorMin=timerRect.anchorMax=new Vector2(.5f,.97f);timerRect.pivot=new Vector2(.5f,1);timerRect.sizeDelta=new Vector2(360,54);
                var timerLabel=new GameObject("TimerLabel",typeof(RectTransform),typeof(Text));timerLabel.transform.SetParent(timer.transform,false);
                timerLabel.GetComponent<Text>().text="상대 멀리건 대기 중... 10";
                NoticeTheme.Inline(timerRect,timerLabel.GetComponent<Text>());
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=rt;
                capture=new Texture2D(1280,720,TextureFormat.RGBA32,false);capture.ReadPixels(new Rect(0,0,1280,720),0,0);capture.Apply();
                var dir=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../TempBuild/NoticeThemePreview"));Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"preview.png"),capture.EncodeToPNG());
            }
            finally {RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(rt);if(capture!=null)Object.DestroyImmediate(capture);Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);}
        }
    }
}
