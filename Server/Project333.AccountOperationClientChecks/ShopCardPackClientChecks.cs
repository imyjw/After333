using System.Reflection;
using System.Text;
using System.Text.Json;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation;
using UnityEngine;
using UnityEngine.Networking;

internal static class ShopCardPackClientChecks
{
    private const string Url="http://synthetic.invalid";
    private const string Token="synthetic-card-pack-token";
    private static readonly string Owner=Guid.NewGuid().ToString("D");
    private static readonly string[] Cards={"A-301","firebolt","A-301","Meteor","RedDragon","Goblin","ManaCannon","Nuclear","GaebangChief","Goblin"};
    private static readonly List<Case> Results=new();
    private sealed record Case(string Name,bool Passed,string Detail);

    internal static async Task RunAsync()
    {
        await Check("Intent and unopened presentation marker are durable before purchase HTTP",async()=>
        {
            Reset();string id=null;
            UnityWebRequest.OnSend=r=>
            {
                id=RequestId(r);
                Require(r.Method=="POST"&&r.Url==Url+"/shop/purchase-card-pack","Wrong purchase route or method.");
                Require(PendingAccountOperation.HasPending(Url,ServerShopClient.OperationType),"Purchase intent not saved before HTTP.");
                Require(PendingCardPackPresentation.GetRequestId(Url,Owner)==id&&PlayerPrefs.Saves>=3,"Unread marker not saved before HTTP.");
                Require(AccountSessionState.Applied==0&&GuestAuthClient.GetMeCount==0,"Receipt changed collection before /me.");
                Success(r,Receipt(id));
            };
            var me=Me();GuestAuthClient.OnGetMe=()=>Task.FromResult(me);
            var result=await new ServerShopClient(Url).PurchaseCardPackAsync(Token,default);
            Require(result.ResolvedRequestId==id&&!PendingAccountOperation.HasPending(Url,ServerShopClient.OperationType),"Successful matching intent did not complete.");
            Require(PendingCardPackPresentation.GetRequestId(Url,Owner)==id,"Receipt confirmation prematurely dismissed unread pack.");
            Require(AccountSessionState.Applied==1&&ReferenceEquals(AccountSessionState.LastApplied,me),"Collection did not come solely from /me.");
        });
        await Check("Lost purchase response retains original request and unread marker across storage reload",async()=>
        {
            foreach(var status in new[]{0,401,429,500,503})
            {
                Reset();string id=null;
                UnityWebRequest.OnSend=r=>{id=RequestId(r);r.result=status==0?UnityWebRequest.Result.ConnectionError:UnityWebRequest.Result.ProtocolError;r.responseCode=status;r.downloadHandler.text=JsonSerializer.Serialize(new{error=new{code=status==503?"card_pack_pool_unavailable":"synthetic_uncertain_error"}});};
                await Expect<InvalidOperationException>(()=>new ServerShopClient(Url).PurchaseCardPackAsync(Token,default));
                var saved=JsonSerializer.Serialize(PlayerPrefs.Values);PlayerPrefs.Values=JsonSerializer.Deserialize<Dictionary<string,string>>(saved);
                using var restored=PendingAccountOperation.Begin(Url,ServerShopClient.OperationType);
                Require(restored.RequestId==id&&PendingCardPackPresentation.GetRequestId(Url,Owner)==id,"Response loss regenerated identity or erased marker: "+status);
                Require(AccountSessionState.Applied==0&&GuestAuthClient.GetMeCount==0,"Failed response applied fabricated collection: "+status);
            }
        });
        await Check("Second purchase and reconciliation skip original operation while its account refresh is in flight",async()=>
        {
            Reset();var pendingMe=new TaskCompletionSource<MeResponse>();var sends=0;string id=null;
            UnityWebRequest.OnSend=r=>{sends++;id=RequestId(r);Success(r,Receipt(id));};
            GuestAuthClient.OnGetMe=()=>pendingMe.Task;
            var purchase=new ServerShopClient(Url).PurchaseCardPackAsync(Token,default);
            Require(GuestAuthClient.GetMeCount==1&&AccountSessionState.Applied==0,"Purchase did not wait for authoritative refresh.");
            await Expect<InvalidOperationException>(()=>new ServerShopClient(Url).PurchaseCardPackAsync(Token,default));
            await new AccountOperationRecovery().RecoverAsync();
            Require(sends==1&&PendingAccountOperation.IsInFlight(Url,ServerShopClient.OperationType,null),"Duplicate or recovery resent charge.");
            Require(PendingCardPackPresentation.GetRequestId(Url,Owner)==id,"Duplicate changed pending presentation.");
            pendingMe.SetResult(Me());await purchase;
            Require(sends==1&&AccountSessionState.Applied==1&&!PendingAccountOperation.IsInFlight(Url,ServerShopClient.OperationType,null),"In-flight state did not release after refresh.");
        });
        await Check("Receipt keeps ten original ordered copies including duplicates without adding local inventory",async()=>
        {
            Reset();var authoritativeCollection=new object();
            UnityWebRequest.OnSend=r=>Success(r,Receipt(RequestId(r)));
            GuestAuthClient.OnGetMe=()=>Task.FromResult(new MeResponse{ResolvedAccount=new AccountDto{ResolvedId=Owner},ResolvedCollectionSummary=authoritativeCollection});
            var response=await new ServerShopClient(Url).PurchaseCardPackAsync(Token,default);
            Require(response.ResolvedCardIds.SequenceEqual(Cards),"Receipt reordered, grouped or deduplicated card copies.");
            Require(ReferenceEquals(AccountSessionState.LastApplied.ResolvedCollectionSummary,authoritativeCollection)&&AccountSessionState.Applied==1,"Receipt applied a local card grant.");
        });
        await Check("Malformed or mismatched purchase receipts keep both markers and never refresh collection",async()=>
        {
            foreach(var defect in new[]{"empty","malformed","null","request","account","cost","count","card","space","wallet"})
            {
                Reset();string id=null;
                UnityWebRequest.OnSend=r=>{id=RequestId(r);Success(r,DefectiveReceipt(id,defect));};
                await Expect<Exception>(()=>new ServerShopClient(Url).PurchaseCardPackAsync(Token,default));
                Require(PendingAccountOperation.HasPending(Url,ServerShopClient.OperationType)&&PendingCardPackPresentation.GetRequestId(Url,Owner)==id,"Invalid receipt cleared pending state: "+defect);
                Require(AccountSessionState.Applied==0&&GuestAuthClient.GetMeCount==0,"Invalid receipt refreshed/applied cards: "+defect);
            }
        });
        await Check("Valid purchase receipt with failed or incomplete /me does not complete or apply",async()=>
        {
            foreach(var defect in new[]{"failure","account","wallet","collection"})
            {
                Reset();string id=null;
                UnityWebRequest.OnSend=r=>{id=RequestId(r);Success(r,Receipt(id));};
                GuestAuthClient.OnGetMe=()=>defect=="failure"?Task.FromException<MeResponse>(new IOException("Synthetic lost refresh")):
                    Task.FromResult(new MeResponse{ResolvedAccount=new AccountDto{ResolvedId=defect=="account"?Guid.NewGuid().ToString("D"):Owner},ResolvedWallet=defect=="wallet"?null:new object(),ResolvedCollectionSummary=defect=="collection"?null:new object()});
                await Expect<Exception>(()=>new ServerShopClient(Url).PurchaseCardPackAsync(Token,default));
                Require(AccountSessionState.Applied==0&&PendingAccountOperation.HasPending(Url,ServerShopClient.OperationType)&&PendingCardPackPresentation.GetRequestId(Url,Owner)==id,"Unconfirmed refresh completed purchase: "+defect);
            }
        });
        await Check("Purchase account token endpoint logout and cancellation changes prevent late application",async()=>
        {
            foreach(var change in new[]{"account","token","endpoint","logout","cancel"})
            {
                Reset();using var cancelled=new CancellationTokenSource();string id=null;
                UnityWebRequest.OnSend=r=>{id=RequestId(r);Success(r,Receipt(id));};
                GuestAuthClient.OnGetMe=()=>{Change(change,cancelled);return Task.FromResult(Me());};
                await Expect<OperationCanceledException>(()=>new ServerShopClient(Url).PurchaseCardPackAsync(Token,cancelled.Token));
                RestoreContext();
                Require(AccountSessionState.Applied==0&&PendingAccountOperation.HasPending(Url,ServerShopClient.OperationType)&&PendingCardPackPresentation.GetRequestId(Url,Owner)==id,"Late purchase changed old context: "+change);
            }
        });
        await Check("Pre-cancelled or wrong initial account token endpoint prevents purchase HTTP and metadata",async()=>
        {
            foreach(var change in new[]{"token","endpoint","logout","cancel"})
            {
                Reset();using var cancelled=new CancellationTokenSource();var client=new ServerShopClient(Url);Change(change,cancelled);var sends=0;
                UnityWebRequest.OnSend=_=>sends++;
                await Expect<OperationCanceledException>(()=>client.PurchaseCardPackAsync(Token,cancelled.Token));
                RestoreContext();Require(sends==0&&PlayerPrefs.Values.Count==0&&AccountSessionState.Applied==0,"Invalid initial context sent purchase: "+change);
            }
        });
        await Check("Unopened pack restoration uses only receipt GET and authoritative /me with original order",async()=>
        {
            Reset();var id=Guid.NewGuid().ToString("D");PendingCardPackPresentation.Remember(Url,Owner,id);var sends=0;
            UnityWebRequest.OnSend=r=>{sends++;Require(r.Method=="GET"&&r.Url==Url+"/shop/card-pack-purchases/"+id&&r.uploadHandler==null,"Receipt recovery resent purchase.");Success(r,Receipt(id));};
            GuestAuthClient.OnGetMeWithContext=(baseUrl,token,ct)=>{Require(baseUrl==Url&&token==Token,"/me refresh changed scope.");return Task.FromResult(Me());};
            var receipt=await new ServerShopClient(Url).GetCardPackPurchaseAsync(Token,id,default);
            Require(sends==1&&receipt.ResolvedCardIds.SequenceEqual(Cards)&&AccountSessionState.Applied==1,"Read-only restoration changed original copies.");
            Require(PendingCardPackPresentation.GetRequestId(Url,Owner)==id&&!PendingAccountOperation.HasAnyPending(Url),"Read-only GET created mutation or acknowledged unopened result.");
        });
        await Check("Inspector custom API address is allowed while global endpoint snapshot remains stable",async()=>
        {
            Reset();const string custom="http://inspector.synthetic.invalid";var client=new ServerShopClient(custom);string id=null;var purchaseSends=0;var receiptGets=0;
            UnityWebRequest.OnSend=r=>
            {
                if(r.Method=="POST"){purchaseSends++;id=RequestId(r);Require(r.Url==custom+"/shop/purchase-card-pack","Inspector purchase used unrelated global endpoint.");}
                else{receiptGets++;Require(r.Method=="GET"&&r.Url==custom+"/shop/card-pack-purchases/"+id,"Inspector receipt used unrelated endpoint.");}
                Success(r,Receipt(id));
            };
            GuestAuthClient.OnGetMeWithContext=(baseUrl,token,ct)=>{Require(baseUrl==custom&&token==Token,"Inspector refresh used unrelated endpoint.");return Task.FromResult(Me());};
            var response=await client.PurchaseCardPackAsync(Token,default);await client.GetCardPackPurchaseAsync(Token,response.ResolvedRequestId,default);
            Require(purchaseSends==1&&receiptGets==1&&PendingCardPackPresentation.GetRequestId(custom,Owner)==id&&PendingCardPackPresentation.GetRequestId(Url,Owner)=="","Inspector scope was mixed with global endpoint or resent charge.");
        });
        await Check("Invalid restored receipt never clears unread marker or applies collection",async()=>
        {
            foreach(var defect in new[]{"malformed","request","account","cost","count","space","wallet"})
            {
                Reset();var id=Guid.NewGuid().ToString("D");PendingCardPackPresentation.Remember(Url,Owner,id);
                UnityWebRequest.OnSend=r=>{Require(r.Method=="GET","Invalid GET recovery posted purchase.");Success(r,DefectiveReceipt(id,defect));};
                await Expect<Exception>(()=>new ServerShopClient(Url).GetCardPackPurchaseAsync(Token,id,default));
                Require(PendingCardPackPresentation.GetRequestId(Url,Owner)==id&&AccountSessionState.Applied==0&&GuestAuthClient.GetMeCount==0,"Invalid restored receipt applied or cleared: "+defect);
            }
        });
        await Check("Receipt GET account token endpoint and cancellation changes prevent late collection application",async()=>
        {
            foreach(var change in new[]{"account","token","endpoint","cancel"})
            {
                Reset();using var cancelled=new CancellationTokenSource();var id=Guid.NewGuid().ToString("D");PendingCardPackPresentation.Remember(Url,Owner,id);
                UnityWebRequest.OnSend=r=>Success(r,Receipt(id));
                GuestAuthClient.OnGetMe=()=>{Change(change,cancelled);return Task.FromResult(Me());};
                await Expect<OperationCanceledException>(()=>new ServerShopClient(Url).GetCardPackPurchaseAsync(Token,id,cancelled.Token));
                RestoreContext();Require(AccountSessionState.Applied==0&&PendingCardPackPresentation.GetRequestId(Url,Owner)==id,"Late GET applied or cleared marker: "+change);
            }
        });
        await Check("Invalid restoration identities do not reach HTTP",async()=>
        {
            Reset();var sends=0;UnityWebRequest.OnSend=_=>sends++;
            foreach(var id in new[]{"",null,"not-a-guid",Guid.Empty.ToString("D"),Guid.NewGuid().ToString("N")})
                await Expect<ArgumentException>(()=>new ServerShopClient(Url).GetCardPackPurchaseAsync(Token,id,default));
            Require(sends==0&&PlayerPrefs.Values.Count==0,"Invalid restoration sent HTTP or created metadata.");
        });
        await Check("Unread marker account endpoint canonical identity and matching acknowledgement separation",()=>
        {
            Reset();var id=Guid.NewGuid();var another=Guid.NewGuid().ToString("D");
            PendingCardPackPresentation.Remember(Url+"/",Owner.ToUpperInvariant(),id.ToString("B"));
            Require(PendingCardPackPresentation.GetRequestId(Url,Owner)==id.ToString("D"),"Marker did not normalize scope/identity.");
            Require(PendingCardPackPresentation.GetRequestId("http://other.invalid",Owner)==""&&PendingCardPackPresentation.GetRequestId(Url,Guid.NewGuid().ToString("D"))=="","Marker leaked across scope.");
            PendingCardPackPresentation.Clear(Url,Owner,another);Require(PendingCardPackPresentation.GetRequestId(Url,Owner)==id.ToString("D"),"Unmatched acknowledgement cleared marker.");
            PendingCardPackPresentation.Remember(Url,Owner,another);PendingCardPackPresentation.Clear(Url,Owner,id.ToString("D"));
            Require(PendingCardPackPresentation.GetRequestId(Url,Owner)==another,"Old acknowledgement cleared new marker.");
            PendingCardPackPresentation.Clear(Url,Owner,another);Require(PendingCardPackPresentation.GetRequestId(Url,Owner)=="","Matching acknowledgement failed.");
            return Task.CompletedTask;
        });
        await Check("Corrupt presentation metadata cannot create a purchase or reveal authority",async()=>
        {
            Reset();var id=Guid.NewGuid().ToString("D");PendingCardPackPresentation.Remember(Url,Owner,id);
            var key=PlayerPrefs.Values.Single().Key;PlayerPrefs.Values[key]="corrupt";var saves=PlayerPrefs.Saves;var sends=0;UnityWebRequest.OnSend=_=>sends++;
            Require(PendingCardPackPresentation.GetRequestId(Url,Owner)=="","Corrupt marker exposed bogus receipt identity.");
            PendingCardPackPresentation.Clear(Url,Owner,id);
            await Expect<ArgumentException>(()=>new ServerShopClient(Url).GetCardPackPurchaseAsync(Token,PendingCardPackPresentation.GetRequestId(Url,Owner),default));
            Require(sends==0&&PlayerPrefs.Saves==saves&&AccountSessionState.Applied==0&&!PendingAccountOperation.HasAnyPending(Url),"Corrupt marker generated mutation/application.");
        });
        await Check("Pack pending enumeration preserves legacy ticket and owned-card lookup semantics",()=>
        {
            Reset();using(var pending=PendingAccountOperation.Begin(Url,ServerShopClient.OperationType)){}
            Require(PendingAccountOperation.HasAnyPending(Url),"Pack was omitted from generic pending gate.");
            Require(!PendingAccountOperation.PendingCards(Url).Contains(ServerShopClient.OperationType),"Legacy card enumeration acquired pack operation as a card.");
            var work=PendingAccountOperation.PendingWork(Url).ToArray();
            Require(work.Any(w=>w.Operation==ServerShopClient.OperationType)&&work.Any(w=>w.Operation=="ticket_purchase")&&work.Any(w=>w.Operation=="card_upgrade"&&w.CardId=="firebolt"),"Pending work omitted existing or new operation.");
            return Task.CompletedTask;
        });
        await Check("Completed common recovery uses resolver plus /me and restores unread pack without purchase POST",async()=>
        {
            Reset();string id;using(var pending=PendingAccountOperation.Begin(Url,ServerShopClient.OperationType))id=pending.RequestId;
            var events=0;Action<string,string,bool,MeResponse> observer=(kind,card,completed,me)=>{if(kind==ServerShopClient.OperationType){Require(completed,"Wrong completion event.");events++;}};
            AccountOperationRecovery.Resolved+=observer;
            try{SetResolution(id,"completed");await new AccountOperationRecovery().RecoverAsync();}
            finally{AccountOperationRecovery.Resolved-=observer;}
            Require(!PendingAccountOperation.HasAnyPending(Url)&&PendingCardPackPresentation.GetRequestId(Url,Owner)==id&&events==1&&AccountSessionState.Applied==1,"Completed recovery lost pending reveal or reapplied incorrectly.");
        });
        await Check("Cancelled common recovery clears matching marker but preserves newer unread pack",async()=>
        {
            foreach(var newer in new[]{false,true})
            {
                Reset();string id;using(var pending=PendingAccountOperation.Begin(Url,ServerShopClient.OperationType))id=pending.RequestId;
                var visible=newer?Guid.NewGuid().ToString("D"):id;PendingCardPackPresentation.Remember(Url,Owner,visible);
                SetResolution(id,"cancelled");await new AccountOperationRecovery().RecoverAsync();
                Require(!PendingAccountOperation.HasAnyPending(Url)&&PendingCardPackPresentation.GetRequestId(Url,Owner)==(newer?visible:""),"Cancelled recovery cleared newer or retained matching result.");
                Require(AccountSessionState.Applied==1,"Cancelled resolution did not refresh authoritative wallet.");
            }
        });
        await Check("Common recovery refresh loss preserves economic intent and unopened marker",async()=>
        {
            Reset();string id;using(var pending=PendingAccountOperation.Begin(Url,ServerShopClient.OperationType))id=pending.RequestId;
            PendingCardPackPresentation.Remember(Url,Owner,id);SetResolution(id,"completed");GuestAuthClient.OnGetMe=()=>Task.FromException<MeResponse>(new IOException("Synthetic lost refresh"));
            await new AccountOperationRecovery().RecoverAsync();
            Require(AccountSessionState.Applied==0&&PendingAccountOperation.HasAnyPending(Url)&&PendingCardPackPresentation.GetRequestId(Url,Owner)==id,"Failed recovery cleared uncertain purchase or applied state.");
        });
        await Check("Common recovery account token endpoint and lifetime cancellation stop old context application",async()=>
        {
            foreach(var change in new[]{"account","token","endpoint","lifetime"})
            {
                Reset();string id;using(var pending=PendingAccountOperation.Begin(Url,ServerShopClient.OperationType))id=pending.RequestId;
                PendingCardPackPresentation.Remember(Url,Owner,id);SetResolution(id,"completed");var recovery=new AccountOperationRecovery();
                GuestAuthClient.OnGetMe=()=>{if(change=="lifetime")typeof(AccountOperationRecovery).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(recovery,null);else Change(change,null);return Task.FromResult(Me());};
                await recovery.RecoverAsync();RestoreContext();
                Require(AccountSessionState.Applied==0&&PendingAccountOperation.HasAnyPending(Url)&&PendingCardPackPresentation.GetRequestId(Url,Owner)==id,"Recovery applied old context: "+change);
            }
        });
        await Check("Pre-commit business rejection releases economic and matching presentation markers",async()=>
        {
            foreach(var code in new[]{"insufficient_resource_gold","operation_cancelled"})
            {
                Reset();UnityWebRequest.OnSend=r=>{r.result=UnityWebRequest.Result.ProtocolError;r.responseCode=409;r.downloadHandler.text=JsonSerializer.Serialize(new{error=new{code}});};
                await Expect<InvalidOperationException>(()=>new ServerShopClient(Url).PurchaseCardPackAsync(Token,default));
                Require(!PendingAccountOperation.HasAnyPending(Url)&&PendingCardPackPresentation.GetRequestId(Url,Owner)==""&&AccountSessionState.Applied==0,"Definitive rejection retained unusable markers: "+code);
            }
        });
        await Check("Receipt absence corruption auth or transport errors never resubmit purchase or clear marker",async()=>
        {
            foreach(var code in new[]{"card_pack_purchase_not_found","card_pack_receipt_unavailable","unauthorized","transport"})
            {
                Reset();var id=Guid.NewGuid().ToString("D");PendingCardPackPresentation.Remember(Url,Owner,id);var sends=0;
                UnityWebRequest.OnSend=r=>{sends++;Require(r.Method=="GET","Receipt error recovery resent purchase.");r.result=code=="transport"?UnityWebRequest.Result.ConnectionError:UnityWebRequest.Result.ProtocolError;r.responseCode=code=="transport"?0:409;r.downloadHandler.text=JsonSerializer.Serialize(new{error=new{code}});};
                var error=await Expect<CardPackReceiptException>(()=>new ServerShopClient(Url).GetCardPackPurchaseAsync(Token,id,default));
                Require(error.Code==code&&sends==1&&AccountSessionState.Applied==0&&PendingCardPackPresentation.GetRequestId(Url,Owner)==id,"Receipt fault erased state or fabricated payment.");
            }
        });
        await Check("Purchase sends one request identity with auth headers while storage contains no token amounts or cards",async()=>
        {
            Reset();UnityWebRequest.OnSend=r=>
            {
                var id=RequestId(r);Require(r.Headers["Authorization"]=="Bearer "+Token&&r.Headers["Content-Type"]=="application/json","Purchase authentication headers wrong.");
                using var json=JsonDocument.Parse(Encoding.UTF8.GetString(r.uploadHandler.Bytes));Require(json.RootElement.EnumerateObject().Count()==1,"Client supplied purchase authority rather than request identity.");
                Require(PlayerPrefs.Values.All(p=>!p.Key.Contains(Token)&&!p.Value.Contains(Token)&&!Cards.Any(c=>p.Value.Contains('"'+c+'"'))),"Presentation storage captured secret or card rewards.");
                Success(r,Receipt(id));
            };
            await new ServerShopClient(Url).PurchaseCardPackAsync(Token,default);
        });
        WriteProof();Console.WriteLine($"PASS: {Results.Count} shop card-pack client boundary groups; actual linked shop/pending/recovery code, substitute Unity DTO/storage/transport and /me boundaries.");
    }

    private static void Reset()
    {
        PlayerPrefs.Values.Clear();PlayerPrefs.Saves=0;RestoreContext();AccountSessionState.Applied=0;AccountSessionState.LastApplied=null;
        GuestAuthClient.GetMeCount=0;GuestAuthClient.OnGetMeWithContext=null;GuestAuthClient.OnGetMe=()=>Task.FromResult(Me());UnityWebRequest.OnSend=_=>throw new Exception("Unexpected synthetic HTTP.");
    }
    private static void RestoreContext(){AccountSessionState.AccountId=Owner;AccountSessionState.SessionToken=Token;AccountSessionState.Authenticated=true;Project333ServerEndpointSettings.Url=Url;}
    private static void Change(string change,CancellationTokenSource cancelled)
    {
        if(change=="account")AccountSessionState.AccountId=Guid.NewGuid().ToString("D");
        else if(change=="token")AccountSessionState.SessionToken="rotated-synthetic-token";
        else if(change=="endpoint")Project333ServerEndpointSettings.Url="http://changed.synthetic.invalid";
        else if(change=="logout")AccountSessionState.Authenticated=false;
        else if(change=="cancel")cancelled.Cancel();
    }
    private static MeResponse Me()=>new(){ResolvedAccount=new AccountDto{ResolvedId=Owner}};
    private static string RequestId(UnityWebRequest request)
    {
        using var json=JsonDocument.Parse(Encoding.UTF8.GetString(request.uploadHandler.Bytes));
        return json.RootElement.GetProperty("RequestId").GetString();
    }
    private static string Receipt(string id)=>JsonSerializer.Serialize(new{requestId=id,resourceGoldCost=33,account=new{id=Owner},wallet=new{resourceGold=67},cardIds=Cards});
    private static string DefectiveReceipt(string id,string defect)
    {
        if(defect=="empty")return "";if(defect=="malformed")return "not-json";if(defect=="null")return "null";
        var cards=Cards.ToArray();if(defect=="card")cards[2]="";if(defect=="space")cards[2]=" A-301";
        return JsonSerializer.Serialize(new{requestId=defect=="request"?Guid.NewGuid().ToString("D"):id,resourceGoldCost=defect=="cost"?32:33,account=new{id=defect=="account"?Guid.NewGuid().ToString("D"):Owner},wallet=defect=="wallet"?null:new{resourceGold=67},cardIds=defect=="count"?cards.Take(9).ToArray():cards});
    }
    private static void Success(UnityWebRequest request,string body){request.result=UnityWebRequest.Result.Success;request.responseCode=200;request.downloadHandler.text=body;}
    private static void SetResolution(string id,string status)=>UnityWebRequest.OnSend=r=>
    {
        Require(r.Method=="POST"&&r.Url==Url+"/account/operations/resolve","Common recovery resent purchase POST.");
        Require(RequestId(r)==id,"Common recovery changed original identity.");Success(r,JsonSerializer.Serialize(new{requestId=id,accountId=Owner,status}));
    };
    private static async Task Check(string name,Func<Task> action)
    {
        try{await action();Results.Add(new(name,true,""));Console.WriteLine("PASS: "+name);}
        catch(Exception exception){Results.Add(new(name,false,exception.ToString()));WriteProof();throw;}
    }
    private static void WriteProof()
    {
        var path=Environment.GetEnvironmentVariable("PROJECT333_CLIENT_CHECK_PROOF");if(string.IsNullOrWhiteSpace(path))return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonSerializer.Serialize(new{Cases=Results,Total=Results.Count,Passed=Results.Count(r=>r.Passed),Failed=Results.Count(r=>!r.Passed),ActualProductionShopClient=true,ActualProductionPendingPresentation=true,ActualProductionPendingIntentAndRecovery=true,NativeUnity=false,RealNetwork=false,ProductionDb=false},new JsonSerializerOptions{WriteIndented=true}));
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static async Task<T> Expect<T>(Func<Task> action) where T:Exception
    {
        try{await action();}catch(T exception){return exception;}throw new InvalidOperationException("Expected "+typeof(T).Name);
    }
}
