using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Ads;
using Project333.Runtime.Presentation;
using UnityEngine;
using UnityEngine.Networking;

const string url="http://synthetic.invalid";
var owner=Guid.NewGuid().ToString("D");
var id=Guid.NewGuid().ToString("D");
var ad=new FakeAd();
var notices=0;
var passes=0;
RewardedAdRecovery.Resolved+=(_,_,_)=>notices++;
Reset();
PendingRewardedAd.Remember(url,owner,id);
Require(PlayerPrefs.Saves>0 && PendingRewardedAd.IsBlocking(url,owner),"Attempt must be durable and blocked before show.");
RewardedAdRecovery.ObserveShow(ad,url,owner,id);
ad.TryShow("placement","opaque",out _);
ad.EmitReward();ad.EmitClose();
Require(PendingRewardedAd.Load(url,owner).Rewarded && PendingRewardedAd.IsBlocking(url,owner),"Completion lost on close.");
Require(AccountSessionState.Applied==0,"SDK callback granted wallet locally.");
Pass("attempt saved before show; rewarded then closed keeps verification pending and grants nothing locally");

var worker=new RewardedAdRecovery();
Reply("pending");
for(var i=0;i<20;i++)await worker.RecoverAsync();
Require(PendingRewardedAd.Load(url,owner).AttemptId==id && AccountSessionState.Applied==0,"Repeated pending checks cleared state or granted reward.");
foreach(var status in new[]{0,401,429,500,503})
{
    UnityWebRequest.OnSend=r=>{RequireGet(r);r.result=UnityWebRequest.Result.ConnectionError;r.responseCode=status;r.error="unavailable";};
    await worker.RecoverAsync();
    Require(PendingRewardedAd.IsBlocking(url,owner),"Transport/auth/rate-limit failure lost pending ad.");
}
Pass("repeated pending and network/auth/rate-limit/server failures retain the same attempt");

var json=System.Text.Json.JsonSerializer.Serialize(PlayerPrefs.Values);
PlayerPrefs.Values=System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(json);
Reply("granted");
await new RewardedAdRecovery().RecoverAsync();
Require(!PendingRewardedAd.HasPending(url,owner) && AccountSessionState.Wallet.tickets==7 && AccountSessionState.Applied==1 && notices==1,"Restored attempt did not apply server wallet once.");
var sends=UnityWebRequest.Sends;
await new RewardedAdRecovery().RecoverAsync();
Require(UnityWebRequest.Sends==sends && AccountSessionState.Applied==1,"Resolved reward was reapplied.");
Pass("storage reload/new worker restores granted server balance exactly once without ad replay");

foreach(var terminal in new[]{"expired","rejected"})
{
    Reset();PendingRewardedAd.Remember(url,owner,id);Reply(terminal);
    await new RewardedAdRecovery().RecoverAsync();
    Require(!PendingRewardedAd.HasPending(url,owner) && AccountSessionState.Applied==0 && notices==1,"Terminal rejection failed to release pending without reward.");
}
Pass("server expired/rejected statuses release pending without increasing wallet");

Reset();PendingRewardedAd.Remember(url,owner,id);
foreach(var body in new[]{"not-json","{}",$"{{\"attemptId\":\"{Guid.NewGuid():D}\",\"status\":\"granted\",\"wallet\":{{\"tickets\":7}}}}",$"{{\"attemptId\":\"{id}\",\"status\":\"granted\"}}",$"{{\"attemptId\":\"{id}\",\"status\":\"unknown\"}}"})
{
    UnityWebRequest.OnSend=r=>{RequireGet(r);r.result=UnityWebRequest.Result.Success;r.downloadHandler.text=body;};
    await new RewardedAdRecovery().RecoverAsync();
    Require(PendingRewardedAd.HasPending(url,owner) && AccountSessionState.Applied==0,"Invalid response cleared attempt or granted reward.");
}
Pass("malformed/mismatched/unknown/missing-wallet responses preserve pending work");

foreach(var change in new[]{"account","token","server"})
{
    Reset();PendingRewardedAd.Remember(url,owner,id);
    Reply("granted",()=>{
        if(change=="account")AccountSessionState.AccountId="another-account";
        if(change=="token")AccountSessionState.SessionToken="new-session";
        if(change=="server")Project333ServerEndpointSettings.Url="http://other.invalid";
    });
    await new RewardedAdRecovery().RecoverAsync();
    Require(AccountSessionState.Applied==0 && notices==0 && PendingRewardedAd.HasPending(url,owner),"Stale response applied after identity/endpoint changed.");
}
Pass("account/token/server switching ignores stale responses and retains original account work");

Reset();PendingRewardedAd.Remember(url,owner,id);
PendingRewardedAd.Remember("http://other.invalid",owner,Guid.NewGuid().ToString("D"));
PendingRewardedAd.Remember(url,"another-account",Guid.NewGuid().ToString("D"));
RewardedAdRecovery.ObserveShow(ad,url,owner,id);
AccountSessionState.AccountId="another-account";
ad.EmitReward();ad.EmitClose();
Require(PendingRewardedAd.Load(url,owner).Rewarded && !PendingRewardedAd.Load(url,"another-account").Rewarded,"SDK completion was attributed to active rather than original account.");
Require(PlayerPrefs.Values.Count==3 && AccountSessionState.Applied==0,"Account/server scopes collided.");
Pass("SDK callbacks stay bound to original account; account and server storage remain separate");

Reset();PendingRewardedAd.Remember(url,owner,id);
RewardedAdRecovery.ObserveShow(ad,url,owner,id);
ad.EmitClose();
Require(!PendingRewardedAd.IsBlocking(url,owner) && PendingRewardedAd.HasPending(url,owner),"Skipped display should retain an attempt that can be checked before retry.");
ad.EmitReward();
Require(PendingRewardedAd.IsBlocking(url,owner),"Late completion after close allowed replay.");
Pass("close-before-reward order retains attempt; late completion blocks another viewing");

Reset();PendingRewardedAd.Remember(url,owner,id);RewardedAdRecovery.ObserveShow(ad,url,owner,id);
ad.TryShow("p","t",out _);
var before=UnityWebRequest.Sends;
UnityWebRequest.OnSend=_=>throw new Exception("Polled during active showing.");
await new RewardedAdRecovery().RecoverAsync();
Require(UnityWebRequest.Sends==before,"Active showing was polled.");
ad.EmitClose();Reply("granted");
await new RewardedAdRecovery().RecoverAsync();
Require(!PendingRewardedAd.HasPending(url,owner) && AccountSessionState.Applied==1,"Server grant after close was not resolved.");
Pass("active ad is not polled; server grant after an unconfirmed close is still recovered");

Reset();PendingRewardedAd.Remember(url,owner,id);
var otherId=Guid.NewGuid().ToString("D");
Throws(()=>PendingRewardedAd.Remember(url,owner,otherId));
PendingRewardedAd.Clear(url,owner,otherId);
Require(PendingRewardedAd.Load(url,owner).AttemptId==id,"Unrelated attempt cleared unresolved work.");
PendingRewardedAd.Clear(url,owner,id);
Require(!PendingRewardedAd.HasPending(url,owner) && AccountSessionState.Applied==0,"Confirmed unshown attempt failed to clear.");
Pass("unresolved attempts cannot be overwritten; matching unshown cleanup never grants reward");

Reset();PendingRewardedAd.Remember(url,owner,id);
var key=PlayerPrefs.Values.Keys.Single();PlayerPrefs.Values[key]="corrupt";
Require(PendingRewardedAd.IsBlocking(url,owner),"Corrupt metadata silently allowed another viewing.");
UnityWebRequest.OnSend=_=>throw new Exception("Invalid identity was sent.");
await new RewardedAdRecovery().RecoverAsync();
Require(PendingRewardedAd.HasPending(url,owner),"Corrupt metadata was silently discarded.");
Pass("corrupt saved metadata fails closed without losing the record or sending invalid identity");

Console.WriteLine($"PASS: {passes} ad recovery groups; production storage/recovery/client/transport code, simulated Unity and SDK boundaries; no real ad, server or account used.");
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Throws(Action action){try{action();}catch(InvalidOperationException){return;}throw new Exception("Expected rejection.");}
void Pass(string message){passes++;Console.WriteLine("PASS: "+message);}
void RequireGet(UnityWebRequest r){Require(r.Verb=="GET" && r.Url.EndsWith("/"+id),"Recovery created another attempt or queried the wrong one.");}
void Reply(string status,Action change=null)
{
    UnityWebRequest.OnSend=r=>{RequireGet(r);r.result=UnityWebRequest.Result.Success;r.responseCode=200;r.downloadHandler.text=System.Text.Json.JsonSerializer.Serialize(new{attemptId=id,status,wallet=new{tickets=7},rewardTicketCount=1});change?.Invoke();};
}
void Reset()
{
    PlayerPrefs.Values.Clear();AccountSessionState.AccountId=owner;AccountSessionState.SessionToken="session";
    Project333ServerEndpointSettings.Url=url;AccountSessionState.Applied=0;notices=0;ad.IsShowing=false;
}
class FakeAd : IRewardedAdService
{
    public event Action StateChanged,Rewarded,Closed;
    public bool IsReady=>!IsShowing;
    public bool IsShowing{get;set;}
    public string StatusMessage=>"";
    public void Initialize(string a,string b,string c){}
    public bool TryShow(string a,string b,out string error){error="";IsShowing=true;return true;}
    public void EmitReward()=>Rewarded?.Invoke();
    public void EmitClose(){IsShowing=false;Closed?.Invoke();}
}