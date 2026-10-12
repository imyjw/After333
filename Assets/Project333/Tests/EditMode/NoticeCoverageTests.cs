using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Settings;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public class NoticeCoverageTests
    {
        private readonly List<GameObject> _objects = new();
        private GameObject Root(string name)
        {
            var root=new GameObject(name,typeof(RectTransform)); root.SetActive(false);_objects.Add(root);return root;
        }
        private static (RectTransform rect, Image image, Text text) Panel(Transform parent,string name)
        {
            var node=new GameObject(name,typeof(RectTransform),typeof(Image));node.transform.SetParent(parent,false);
            var label=new GameObject("Text",typeof(RectTransform),typeof(Text));label.transform.SetParent(node.transform,false);
            var rect=(RectTransform)node.transform;rect.sizeDelta=new Vector2(360,80);
            return (rect,node.GetComponent<Image>(),label.GetComponent<Text>());
        }
        [TearDown] public void Cleanup(){foreach(var root in _objects) if(root!=null) Object.DestroyImmediate(root);_objects.Clear();}
        private static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
        private static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
        private static void AssertTheme(Image image,Text text)
        {
            Assert.That(image.color,Is.EqualTo(NoticeTheme.PanelColor));
            Assert.That(image.sprite,Is.Not.Null);Assert.That(image.type,Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.preserveAspect,Is.False);
            Assert.That(image.GetComponent<Outline>().effectDistance,Is.EqualTo(new Vector2(1,-1)));
            Assert.That(text.font,Is.SameAs(NoticeTheme.Font));Assert.That(text.fontStyle,Is.EqualTo(FontStyle.Normal));
            Assert.That(text.raycastTarget,Is.False);
        }

        [TestCase(false,false,true,20)]
        [TestCase(false,false,true,5)]
        [TestCase(false,false,false,0)]
        [TestCase(true,false,true,10)]
        [TestCase(true,true,false,5)]
        public void PhaseTimer_AlwaysUsesThemeAcrossCountdownChanges(bool mulligan,bool confirmed,bool local,int seconds)
        {
            var root=Root("Timer"); var bootstrap=root.AddComponent<BattleBootstrapper>();var p=Panel(root.transform,"TimerPanel");
            Set(bootstrap,"_onlineTurnTimerPanelRect",p.rect);Set(bootstrap,"_onlineTurnTimerPanelImage",p.image);
            Set(bootstrap,"_onlineTurnTimerTextRect",p.text.rectTransform);Set(bootstrap,"_onlineTurnTimerText",p.text);
            Set(bootstrap,"_onlineTurnTimerUsesSceneLayout",true);
            p.rect.anchoredPosition=new Vector2(50,100); p.rect.sizeDelta=new Vector2(360,54);
            Call(bootstrap,"ApplyOnlineTurnTimerValue",mulligan,confirmed,local,seconds,60);
            AssertTheme(p.image,p.text);Assert.That(p.rect.anchoredPosition,Is.EqualTo(new Vector2(50,100)));
            Assert.That(p.image.raycastTarget,Is.False);Assert.That(p.text.text,Is.Not.Empty);
            var generation=p.text.GetGenerationSettings(new Vector2(p.rect.rect.width-40,0));
            var contentHeight=p.text.cachedTextGeneratorForLayout.GetPreferredHeight(p.text.text,generation)/p.text.pixelsPerUnit;
            Assert.That(p.rect.rect.height-16,Is.GreaterThanOrEqualTo(contentHeight));
            Call(bootstrap,"ApplyOnlineTurnTimerValue",false,false,true,1,60);
            AssertTheme(p.image,p.text);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TurnBanner_RestoresOriginalGraphicAndRetainsNonBlockingLayout(bool sceneLayout)
        {
            var root=Root("Banner");var bootstrap=root.AddComponent<BattleBootstrapper>();var p=Panel(root.transform,"BannerPanel");
            var group=root.AddComponent<CanvasGroup>();var canvas=root.AddComponent<Canvas>();
            p.text.name="NoticeLabel";p.text.text="내 턴";
            NoticeTheme.Inline(p.rect,p.text);
            var original=Resources.Load<Sprite>("Project333/BattleUI/YourTurnBanner");
            Assert.That(original,Is.Not.Null);
            Set(bootstrap,"_yourTurnBannerCanvasGroup",group);
            Set(bootstrap,"_yourTurnBannerRect",p.rect);Set(bootstrap,"_yourTurnBannerImage",p.image);
            Set(bootstrap,"_yourTurnBannerUsesSceneLayout",sceneLayout);
            p.rect.anchoredPosition=new Vector2(50,100);
            Set(bootstrap,"_yourTurnBannerOffset",new Vector2(12,34));
            Call(bootstrap,"ApplyYourTurnBannerSettings");Call(bootstrap,"ApplyYourTurnBannerSettings");
            Assert.That(p.image.sprite,Is.SameAs(original));Assert.That(p.image.overrideSprite,Is.SameAs(original));
            Assert.That(p.image.type,Is.EqualTo(Image.Type.Simple));Assert.That(p.image.color,Is.EqualTo(Color.white));
            Assert.That(p.image.preserveAspect,Is.True);Assert.That(p.image.raycastTarget,Is.False);
            Assert.That(p.image.GetComponent<Outline>().enabled,Is.False);
            Assert.That(p.rect.GetComponentsInChildren<Text>().Length,Is.Zero);
            Assert.That(p.rect.sizeDelta,Is.EqualTo(new Vector2(760,254)));
            Assert.That(p.rect.anchoredPosition,Is.EqualTo(sceneLayout?new Vector2(50,100):new Vector2(12,34)));
            Assert.That(group.interactable,Is.False);Assert.That(group.blocksRaycasts,Is.False);
            Assert.That(canvas.sortingOrder,Is.EqualTo(5500));
        }

        [Test]
        public void RewardNotice_UsesThemeAndPreservesListAndDismissSurface()
        {
            var root=Root("Reward");var draft=root.AddComponent<DraftSceneController>();var p=Panel(root.transform,"RewardPanel");
            var click=new GameObject("Dismiss",typeof(RectTransform),typeof(Image),typeof(Button));click.transform.SetParent(root.transform,false);
            Set(draft,"_rewardClaimToastPanelRect",p.rect);Set(draft,"_rewardClaimToastPanelImage",p.image);
            Set(draft,"_rewardClaimToastTextRect",p.text.rectTransform);Set(draft,"_rewardClaimToastText",p.text);
            Set(draft,"_rewardClaimToastClickSurfaceImage",click.GetComponent<Image>());
            p.text.text="보상 획득!\n골드 +10\n티켓 +1";
            Call(draft,"ApplyRewardClaimToastSettings");AssertTheme(p.image,p.text);
            Assert.That(p.text.text,Does.Contain("골드 +10"));Assert.That(p.text.alignment,Is.EqualTo(TextAnchor.UpperLeft));
            Assert.That(click.GetComponent<Image>().raycastTarget,Is.True);Assert.That(p.image.raycastTarget,Is.False);
        }

        [Test]
        public void DraftMatchmaking_ReopeningKeepsThemeAndCancelBinding()
        {
            var view=DraftPvpMatchmakingOverlayView.GetOrCreate(UnityEngine.SceneManagement.SceneManager.GetActiveScene());_objects.Add(view.gameObject);
            var cancelled=0;view.BindCancel(()=>cancelled++);view.ShowWaiting(false);
            var panel=view.transform.Find("Panel");var text=panel.Find("StatusText").GetComponent<Text>();var button=panel.GetComponentInChildren<Button>();
            AssertTheme(panel.GetComponent<Image>(),text);button.onClick.Invoke();Assert.That(cancelled,Is.EqualTo(1));
            view.ShowCancelling();Assert.That(button.interactable,Is.False);AssertTheme(panel.GetComponent<Image>(),text);
            view.ShowFailure("연결 실패");Assert.That(button.interactable,Is.True);AssertTheme(panel.GetComponent<Image>(),text);
            view.Hide();Assert.That(view.gameObject.activeSelf,Is.False);
        }

        [Test]
        public void SettingsConfirmation_RetainsActionWithThemedDialog()
        {
            var root=Root("Settings");var settings=root.AddComponent<GlobalSettingsMenuController>();settings.EnsureEditableHierarchy();
            var accepted=0;Call(settings,"ShowConfirmation","게임 종료","종료하시겠습니까?","종료",(Action)(()=>accepted++));
            var panel=(RectTransform)typeof(GlobalSettingsMenuController).GetField("_confirmationPanel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(settings);
            foreach(var text in panel.GetComponentsInChildren<Text>(true)) Assert.That(text.font,Is.SameAs(NoticeTheme.Font));
            Assert.That(panel.GetComponent<Image>().color,Is.EqualTo(NoticeTheme.PanelColor));
            Call(settings,"AcceptConfirmation");Assert.That(accepted,Is.EqualTo(1));
        }
    }
}
