using Project333.Runtime.Application.Accounts;
using UnityEngine;
using UnityEngine.Networking;

const string url="http://synthetic.invalid";
const string kind="card_upgrade";
const string card="firebolt";
AccountSessionState.AccountId=Guid.NewGuid().ToString("D");
var owner=AccountSessionState.AccountId;
var passes=0;
string first;
using(var operation=PendingAccountOperation.Begin(url,kind,card,0))
{
    first=operation.RequestId;
    Require(PlayerPrefs.Saves>=1 && PendingAccountOperation.HasPending(url,kind,card),"Intent must persist before HTTP.");
    Throws(()=>PendingAccountOperation.Begin(url,kind,card,1));
    UnityWebRequest.OnSend=r=>{Require(PlayerPrefs.Saves>0,"Send preceded save.");r.result=UnityWebRequest.Result.ConnectionError;r.responseCode=0;};
    await Fails(()=>operation.PostAsync<object,Reply>(url,"/cards/upgrade","synthetic",new{},_=>"connection failed",default));
}
using(var retry=PendingAccountOperation.Begin(url,kind," FIREBOLT ",7))
    Require(retry.RequestId==first && retry.ExpectedUpgradeLevel==0,"Retry after reentering scene/refresh must retain id and original displayed level.");
Pass("intent is saved before send, in-flight duplicates block, transport failure preserves original id/level across new client instances");

// Simulate reloading durable storage rather than retaining the same dictionary object.
var stored=System.Text.Json.JsonSerializer.Serialize(PlayerPrefs.Values);
PlayerPrefs.Values=System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(stored);
using(var restored=PendingAccountOperation.Begin(url,kind,card,12))Require(restored.RequestId==first && restored.ExpectedUpgradeLevel==0,"Storage restoration lost pending intent.");
using(var other=PendingAccountOperation.Begin(url,kind,"Firewall",0))Require(other.RequestId!=first,"Cards shared pending intent.");
using(var other=PendingAccountOperation.Begin("http://other.invalid",kind,card,0))Require(other.RequestId!=first,"Servers shared pending intent.");
AccountSessionState.AccountId=Guid.NewGuid().ToString("D");
using(var other=PendingAccountOperation.Begin(url,kind,card,0))Require(other.RequestId!=first,"Accounts shared pending intent.");
AccountSessionState.AccountId=owner;
Pass("pending metadata survives storage reload and remains separated by account/server/card");

foreach(var status in new[]{0,401,429,500,503,409})
{
    using var operation=PendingAccountOperation.Begin(url,kind,card,4);
    UnityWebRequest.OnSend=r=>{r.result=UnityWebRequest.Result.ProtocolError;r.responseCode=status;r.downloadHandler.text="{\"error\":{\"code\":\"request_id_conflict\"}}";};
    await Fails(()=>operation.PostAsync<object,Reply>(url,"/cards/upgrade","synthetic",new{},_=>"failure",default));
    Require(PendingAccountOperation.HasPending(url,kind,card) && operation.RequestId==first,"Uncertain/auth/conflicting-id failure cleared intent.");
}
using(var operation=PendingAccountOperation.Begin(url,kind,card,4))
{
    using var canceled=new CancellationTokenSource();canceled.Cancel();
    UnityWebRequest.OnSend=_=>throw new Exception("Canceled send ran.");
    await Fails(()=>operation.PostAsync<object,Reply>(url,"/cards/upgrade","synthetic",new{},_=>"failure",canceled.Token));
    Require(PendingAccountOperation.HasPending(url,kind,card),"Cancellation cleared intent.");
}
Pass("cancellation, transport/auth/rate-limit/server failures and request-id conflicts preserve intent");

foreach(var body in new[]{"","not-json","{}"})
{
    using var operation=PendingAccountOperation.Begin(url,kind,card,4);
    UnityWebRequest.OnSend=r=>{r.result=UnityWebRequest.Result.Success;r.responseCode=200;r.downloadHandler.text=body;};
    await Fails(async()=>{var response=await operation.PostAsync<object,Reply>(url,"/cards/upgrade","synthetic",new{},_=>"failure",default);operation.Complete(response?.RequestId,response?.AccountId);});
    Require(PendingAccountOperation.HasPending(url,kind,card),"Invalid success response cleared intent.");
}
using(var operation=PendingAccountOperation.Begin(url,kind,card,4))
{
    Throws(()=>operation.Complete(Guid.NewGuid().ToString("D"),owner));
    Throws(()=>operation.Complete(first,Guid.NewGuid().ToString("D")));
    AccountSessionState.AccountId=Guid.NewGuid().ToString("D");
    Throws(()=>operation.Complete(first,owner));AccountSessionState.AccountId=owner;
    operation.Complete(first,owner);
}
Require(!PendingAccountOperation.HasPending(url,kind,card),"Matching receipt did not clear pending intent.");
using(var next=PendingAccountOperation.Begin(url,kind,card,4))Require(next.RequestId!=first && next.ExpectedUpgradeLevel==4,"Next explicit action reused completed intent.");
Pass("empty/malformed/unmatched success responses preserve intent; only the matching account/request confirmation clears it");

foreach(var code in new[]{"operation_cancelled","insufficient_resource_gold","insufficient_card_copies","card_upgrade_conflict","max_level_reached"})
{
    using var operation=PendingAccountOperation.Begin(url,kind,card,4);
    UnityWebRequest.OnSend=r=>{r.result=UnityWebRequest.Result.ProtocolError;r.responseCode=409;r.downloadHandler.text="{\"error\":{\"code\":\""+code+"\"}}";};
    await Fails(()=>operation.PostAsync<object,Reply>(url,"/cards/upgrade","synthetic",new{},_=>"rejected",default));
    Require(!PendingAccountOperation.HasPending(url,kind,card),"Definitive pre-commit rejection left an unusable pending intent.");
}
Pass("documented pre-commit business rejections release intent so a refreshed explicit action can proceed");

using(var ticket=PendingAccountOperation.Begin(url,"ticket_purchase"))
{
    var key=PlayerPrefs.Values.Single(x=>x.Value.Contains(ticket.RequestId)).Key;
    PlayerPrefs.Values[key]="corrupt";
}
Throws(()=>PendingAccountOperation.Begin(url,"ticket_purchase"));
Require(PendingAccountOperation.HasPending(url,"ticket_purchase"),"Corrupt saved intent must not silently generate a fresh charge.");
Pass("corrupt saved intent fails closed rather than generating a fresh request id");

PlayerPrefs.Values.Clear();
AccountSessionState.AccountId=owner;
var reconciler = new AccountOperationRecovery();
var resolvedCount=0;
AccountOperationRecovery.Resolved += (_,_,_,_)=>resolvedCount++;
foreach(var outcome in new[]{"completed","cancelled"})
{
    foreach(var opKind in new[]{"ticket_purchase","card_upgrade"})
    {
        var opCard=opKind=="ticket_purchase" ? "" : card;
        string id;
        using(var pending=PendingAccountOperation.Begin(url,opKind,opCard,2))id=pending.RequestId;
        UnityWebRequest.OnSend=r=>{
            Require(r.Url.EndsWith("/account/operations/resolve"),"Recovery resent a purchase/upgrade.");
            Require(System.Text.Encoding.UTF8.GetString(r.uploadHandler.Bytes).Contains(id),"Recovery changed request id.");
            r.result=UnityWebRequest.Result.Success;r.responseCode=200;
            r.downloadHandler.text=System.Text.Json.JsonSerializer.Serialize(new{requestId=id,accountId=owner,status=outcome});
        };
        GuestAuthClient.OnGetMe=()=>Task.FromResult(new MeResponse{ResolvedAccount=new AccountDto{ResolvedId=owner}});
        await reconciler.RecoverAsync();
        Require(!PendingAccountOperation.HasPending(url,opKind,opCard),"Terminal outcome was not cleared.");
    }
}
Require(resolvedCount==4 && AccountSessionState.Applied==4,"Recovery must refresh state and publish each terminal outcome.");
Pass("automatic completed/cancelled recovery for both operations only resolves and refreshes, never resends a charge");

string retained;
using(var pending=PendingAccountOperation.Begin(url,kind,card,3))retained=pending.RequestId;
UnityWebRequest.OnSend=r=>{
    r.result=UnityWebRequest.Result.Success;r.responseCode=200;
    r.downloadHandler.text=System.Text.Json.JsonSerializer.Serialize(new{RequestId=retained,AccountId=owner,Status="completed"});
};
GuestAuthClient.OnGetMe=()=>Task.FromException<MeResponse>(new IOException("lost refresh"));
await reconciler.RecoverAsync();
Require(PendingAccountOperation.HasPending(url,kind,card) && resolvedCount==4,"Refresh failure must retain pending work.");
GuestAuthClient.OnGetMe=()=>{
    AccountSessionState.AccountId="changed-account";
    return Task.FromResult(new MeResponse{ResolvedAccount=new AccountDto{ResolvedId=owner}});
};
await reconciler.RecoverAsync();
AccountSessionState.AccountId=owner;
Require(PendingAccountOperation.HasPending(url,kind,card) && resolvedCount==4,"Account change must not apply or clear previous account work.");
GuestAuthClient.OnGetMe=()=>Task.FromResult(new MeResponse{ResolvedAccount=new AccountDto{ResolvedId=owner}});
await new AccountOperationRecovery().RecoverAsync();
Require(!PendingAccountOperation.HasPending(url,kind,card) && resolvedCount==5,"New worker must resume retained work automatically.");
Pass("refresh failure and account switching retain work; a new worker resumes without a manual confirmation");

using(var pending=PendingAccountOperation.Begin(url,kind,card,3))
{
    UnityWebRequest.OnSend=_=>throw new Exception("In-flight operation must not be reconciled.");
    await reconciler.RecoverAsync();
    Require(PendingAccountOperation.HasPending(url,kind,card),"Recovery touched an active purchase.");
}
Pass("automatic recovery skips the original in-flight operation");
await AccountHttpTransportChecks.RunAsync();
Console.WriteLine($"PASS: {passes} client retry groups; production PendingAccountOperation code with in-memory Unity storage/HTTP/JSON boundaries, no real account or network access.");
void Pass(string text){passes++;Console.WriteLine("PASS: "+text);}
void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
void Throws(Action action){try{action();}catch(Exception){return;}throw new Exception("Expected rejection.");}
async Task Fails(Func<Task> action){try{await action();}catch(Exception){return;}throw new Exception("Expected request failure.");}
class Reply{public string RequestId;public string AccountId;}
