using System.Text.Json;
namespace Project333.Runtime.Presentation
{
    public static class Project333ServerEndpointSettings
    {
        public const string LocalHttpUrl="http://synthetic.invalid";
        public static string Url=LocalHttpUrl;
        public static string ResolveHttpUrl(string value)=>Url;
    }
}
// Unity storage, transport, DTOs and /me application are substitute boundaries.
// Actual pending metadata, recovery, transport and shop client code are linked verbatim.
namespace UnityEngine
{
    public class MonoBehaviour { protected static void DontDestroyOnLoad(object o){} }
    public class GameObject(string name){ public T AddComponent<T>() where T:new()=>new T(); }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad, AfterSceneLoad, SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type):Attribute {}
    public static class Time { public static float unscaledTime; }
    public enum NetworkReachability { NotReachable, ReachableViaLocalAreaNetwork }
    public static class Application { public static NetworkReachability internetReachability; }
    static class PlayerPrefs
    {
        public static Dictionary<string,string> Values = new();
        public static int Saves;
        public static bool HasKey(string key)=>Values.ContainsKey(key);
        public static string GetString(string key,string fallback="")=>Values.TryGetValue(key,out var value)?value:fallback;
        public static void SetString(string key,string value)=>Values[key]=value;
        public static void DeleteKey(string key)=>Values.Remove(key);
        public static void Save()=>Saves++;
    }
    static class JsonUtility
    {
        static readonly JsonSerializerOptions Options=new(){IncludeFields=true};
        public static string ToJson<T>(T value)=>JsonSerializer.Serialize(value,Options);
        public static T FromJson<T>(string value)=>JsonSerializer.Deserialize<T>(value,Options);
    }
}
namespace UnityEngine.Networking
{
    public class UploadHandlerRaw(byte[] bytes){public byte[] Bytes=bytes;}
    public class DownloadHandlerBuffer{public string text;}
    public class UnityWebRequest : IDisposable
    {
        public const string kHttpVerbPOST="POST";
        public enum Result{Success,ConnectionError,ProtocolError,DataProcessingError}
        public static Action<UnityWebRequest> OnSend;
        public UploadHandlerRaw uploadHandler;
        public DownloadHandlerBuffer downloadHandler;
        public int timeout;
        public long responseCode;
        public Result result;
        public string Url, Method;
        public readonly Dictionary<string,string> Headers=new();
        public UnityWebRequest(string url,string verb){Url=url;Method=verb;}
        public static UnityWebRequest Get(string url)=>new(url,"GET"){downloadHandler=new DownloadHandlerBuffer()};
        public void SetRequestHeader(string name,string value)=>Headers[name]=value;
        public bool CompleteOnSend = true; public int SendCount, AbortCount; private Operation operation;
        public Operation SendWebRequest(){SendCount++;operation=new Operation();OnSend(this);operation.isDone=CompleteOnSend;return operation;}
        public void Abort(){AbortCount++;if(operation!=null)operation.isDone=true;}
        public void Dispose(){}
        public class Operation{public bool isDone;}
    }
}
namespace Project333.Runtime.Application.Accounts
{
    static class AccountSessionState
    {
        public static string AccountId, SessionToken="test";
        public static bool Authenticated=true;
        public static bool IsAuthenticated=>Authenticated;
        public static IEnumerable<string> OwnedCardIds=>new[]{"firebolt","Firewall"};
        public static int Applied;
        public static MeResponse LastApplied;
        public static void ApplyMeResponse(MeResponse me){Applied++;LastApplied=me;}
    }
    public class MeResponse
    {
        public AccountDto ResolvedAccount = new();
        public object ResolvedWallet = new(), ResolvedCollectionSummary = new();
    }
    public class AccountDto { public string ResolvedId; }
    public class AuthAccountDto
    {
        public string Id,id;
        public string ResolvedId=>Id??id;
    }
    public class WalletDto
    {
        public long ResourceGold,resourceGold;
        public long ResolvedResourceGold=>ResourceGold!=0?ResourceGold:resourceGold;
    }
    public class GuestAuthClient(string url)
    {
        public static Func<Task<MeResponse>> OnGetMe;
        public static Func<string,string,CancellationToken,Task<MeResponse>> OnGetMeWithContext;
        public static int GetMeCount;
        public Task<MeResponse> GetMeAsync(string token,CancellationToken ct)
        {
            GetMeCount++;
            return OnGetMeWithContext!=null?OnGetMeWithContext(url,token,ct):OnGetMe();
        }
    }
    class ApiErrorEnvelope{public ApiError error;public ApiError ResolvedError=>error;}
    class ApiError{public string code;public string ResolvedCode=>code;}
}
