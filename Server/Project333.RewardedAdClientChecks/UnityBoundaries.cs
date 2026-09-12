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
namespace UnityEngine
{
    public class MonoBehaviour { protected static void DontDestroyOnLoad(object o){} }
    public class GameObject { public GameObject(string name){} public T AddComponent<T>() where T:new()=>new T(); }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){} }
    public static class Time { public static float unscaledTime; }
    public enum NetworkReachability { NotReachable, ReachableViaLocalAreaNetwork }
    public static class Application { public static NetworkReachability internetReachability; }
    public static class PlayerPrefs
    {
        public static Dictionary<string,string> Values=new(); public static int Saves;
        public static bool HasKey(string key)=>Values.ContainsKey(key);
        public static string GetString(string key)=>Values[key];
        public static void SetString(string key,string value)=>Values[key]=value;
        public static void DeleteKey(string key)=>Values.Remove(key);
        public static void Save()=>Saves++;
    }
    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options=new(){IncludeFields=true};
        public static string ToJson<T>(T value)=>JsonSerializer.Serialize(value,Options);
        public static T FromJson<T>(string value)=>JsonSerializer.Deserialize<T>(value,Options);
    }
}
namespace UnityEngine.Networking
{
    public class UploadHandlerRaw { public byte[] Bytes; public UploadHandlerRaw(byte[] bytes){Bytes=bytes;} }
    public class DownloadHandlerBuffer { public string text; }
    public class UnityWebRequest : IDisposable
    {
        public const string kHttpVerbPOST="POST", kHttpVerbGET="GET";
        public enum Result { Success,ConnectionError,ProtocolError,DataProcessingError }
        public static Action<UnityWebRequest> OnSend; public static int Sends;
        public UploadHandlerRaw uploadHandler; public DownloadHandlerBuffer downloadHandler;
        public int timeout; public long responseCode; public Result result; public string error;
        public string Url, Verb; public Operation operation = new();
        public UnityWebRequest(string url,string verb){Url=url;Verb=verb;}
        public static string EscapeURL(string value)=>Uri.EscapeDataString(value);
        public void SetRequestHeader(string name,string value){}
        public Operation SendWebRequest(){Sends++;OnSend(this);return operation;}
        public void Abort(){operation.isDone=true;}
        public void Dispose(){}
        public class Operation { public bool isDone=true; }
    }
}
namespace Project333.Runtime.Application.Accounts
{
    public static class AccountSessionState
    {
        public static string AccountId,SessionToken="session";
        public static bool IsAuthenticated=>!string.IsNullOrWhiteSpace(SessionToken);
        public static int Applied; public static WalletDto Wallet;
        public static void ApplyRewardedAdWallet(WalletDto wallet){Applied++;Wallet=wallet;}
    }
    public class WalletDto { public int tickets; }
    public class ApiErrorEnvelope { public ApiError error; public ApiError ResolvedError=>error; }
    public class ApiError { public string code,message; public string ResolvedCode=>code; public string ResolvedMessage=>message; }
}