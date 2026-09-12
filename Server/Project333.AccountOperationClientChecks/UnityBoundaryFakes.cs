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
// Only the storage/HTTP/JSON boundaries are faked. The production pending-request
// implementation is linked verbatim. This is not a Unity scene or native API test.

namespace UnityEngine
{
    public class MonoBehaviour { protected static void DontDestroyOnLoad(object o){} }
    public class GameObject(string name){ public T AddComponent<T>() where T:new()=>new T(); }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type):Attribute {}
    public static class Time { public static float unscaledTime; }
    public enum NetworkReachability { NotReachable, ReachableViaLocalAreaNetwork }
    public static class Application { public static NetworkReachability internetReachability; }
    static class PlayerPrefs
    {
        public static Dictionary<string,string> Values = new();
        public static int Saves;
        public static bool HasKey(string key)=>Values.ContainsKey(key);
        public static string GetString(string key)=>Values[key];
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
        public string Url;
        public UnityWebRequest(string url,string verb){ Url=url; }
        public void SetRequestHeader(string name,string value){ }
        public bool CompleteOnSend = true; public int SendCount, AbortCount; private Operation operation;
        public Operation SendWebRequest(){SendCount++; operation=new Operation(); OnSend(this); operation.isDone=CompleteOnSend; return operation;}
        public void Abort(){AbortCount++; if(operation!=null)operation.isDone=true;}
        public void Dispose(){ }
        public class Operation{public bool isDone;}
    }
}
namespace Project333.Runtime.Application.Accounts
{
    static class AccountSessionState
    {
        public static string AccountId, SessionToken="test";
        public static bool IsAuthenticated=>true;
        public static IEnumerable<string> OwnedCardIds=>new[]{"firebolt","Firewall"};
        public static int Applied;
        public static void ApplyMeResponse(MeResponse me)=>Applied++;
    }
    public class MeResponse
    {
        public AccountDto ResolvedAccount = new();
        public object ResolvedWallet = new(), ResolvedCollectionSummary = new();
    }
    public class AccountDto { public string ResolvedId; }
    public class GuestAuthClient(string url)
    {
        public static Func<Task<MeResponse>> OnGetMe;
        public Task<MeResponse> GetMeAsync(string token,CancellationToken ct)=>OnGetMe();
    }
    class ApiErrorEnvelope{public ApiError error;public ApiError ResolvedError=>error;}
    class ApiError{public string code;public string ResolvedCode=>code;}
}
