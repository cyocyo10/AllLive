using System;
using System.Collections.Generic;
using System.Threading.Tasks;
// Isolation-only dependencies. Target production files are linked without editing.
// HttpUtil has no HttpClient and cannot perform HTTP. Any missing fixture throws.
using System.Collections.Concurrent;
using AllLive.Core.Interface;
using AllLive.Core.Models;

namespace AllLive.Core.Helper
{
    public static class HttpUtil
    {
        public record Request(string Method, string Url, string Body, IDictionary<string,string> Headers, IDictionary<string,string> Query);
        public static ConcurrentQueue<Request> Calls = new();
        public static Func<Request,Task<string>> Handler;
        public static void Reset(Func<Request,Task<string>> handler = null) { Calls.Clear(); Handler=handler; }
        static Task<string> Execute(Request r)
        {
            Calls.Enqueue(r);
            return Handler?.Invoke(r) ?? Task.FromException<string>(new InvalidOperationException("NETWORK DISABLED: no fixture for " + r.Method));
        }
        public static Task<string> GetString(string url, IDictionary<string,string> headers=null, IDictionary<string,string> queryParameters=null)
            => Execute(new Request("GET",url,null,headers,queryParameters));
        public static Task<string> PostFormUrlEncodedString(string url,string formData,IDictionary<string,string> headers=null)
            => Execute(new Request("POST",url,formData,headers,null));
    }
}
namespace AllLive.Core.Danmaku
{
    public class DouyuDanmaku : ILiveDanmaku
    {
        public event EventHandler<LiveMessage> NewMessage;
        public event EventHandler<string> OnClose;
        public int HeartbeatTime => 0;
        public int Starts;
        public int Stops;
        public Func<Task> StartHandler;
        public Func<Task> StopHandler;
        public void Heartbeat() { }
        public Task Start(object args) { Starts++; return StartHandler?.Invoke() ?? Task.CompletedTask; }
        public Task Stop() { Stops++; return StopHandler?.Invoke() ?? Task.CompletedTask; }
        public void Emit(LiveMessage m) => NewMessage?.Invoke(this,m);
        public void Close() => OnClose?.Invoke(this,"fake close");
    }
}
namespace Windows.UI.Core
{
    public enum CoreDispatcherPriority { Normal }
    public class CoreDispatcher
    {
        public bool Defer;
        public readonly Queue<Action> Pending = new();
        public void Drain() { while(Pending.Count>0) Pending.Dequeue()(); }
        public Task RunAsync(CoreDispatcherPriority p,Action a) { if(Defer) Pending.Enqueue(a); else a(); return Task.CompletedTask; }
    }
}
namespace Windows.UI.ViewManagement
{
    public class ApplicationView { public string Title {get;set;} public static ApplicationView GetForCurrentView() => new(); }
}
namespace Windows.UI.Xaml { public class StubMarker { } }
namespace Windows.ApplicationModel.Core { public class StubMarker { } }
namespace AllLive.UWP.Models
{
    public class FavoriteItem { public string Photo{get;set;} public string RoomID{get;set;} public string SiteName{get;set;} public string UserName{get;set;} }
    public class HistoryItem : FavoriteItem { }
}
namespace AllLive.UWP.Helper
{
    public enum LogType { DEBUG, ERROR }
    public static class LogHelper
    {
        public static readonly ConcurrentQueue<string> Messages=new();
        public static void Log(string message, LogType kind, Exception ex=null) => Messages.Enqueue(message+(ex==null?"":" | "+ex.GetType().Name+": "+ex.Message));
    }
    public static class SettingHelper
    {
        public const string NEW_WINDOW_LIVEROOM="window";
        public static class LiveDanmaku { public const string DANMU_CLEAN_COUNT="clean", KEEP_SUPER_CHAT="keep"; }
        public static bool? KeepSuperChat;
        public static T GetValue<T>(string key,T value=default) =>
            key==LiveDanmaku.KEEP_SUPER_CHAT && KeepSuperChat.HasValue ? (T)(object)KeepSuperChat.Value : value;
    }
    public static class Utils
    {
        public static bool IsXbox=false;
        public static readonly ConcurrentQueue<string> Toasts=new();
        public static void ShowMessageToast(string text) => Toasts.Enqueue(text);
    }
    public static class DatabaseHelper
    {
        public static long? CheckFavorite(string room,string site)=>null;
        public static void AddHistory(AllLive.UWP.Models.HistoryItem item) { }
        public static void AddFavorite(AllLive.UWP.Models.FavoriteItem item) { }
        public static void DeleteFavorite(long id) { }
    }
    public static class MessageCenter
    {
        public static void ChangeTitle(string t,ILiveSite site) { }
        public static void UpdateFavorite() { }
    }
}
namespace AllLive.UWP.ViewModels
{
    public class BaseViewModel
    {
        public bool Loading{get;set;}
        public Exception LastError{get;private set;}
        public virtual void DoPropertyChanged(string name) { }
        public virtual void HandleError(Exception e,string m="error") { LastError=e; }
    }
    public class SettingVM { public List<string> ShieldWords{get;set;}=new(); }
}
namespace AuditHarness
{
    public class FakeSite : ILiveSite
    {
        public string Name=>"Fake (offline only)";
        public AllLive.Core.Danmaku.DouyuDanmaku Danmaku=new();
        public int QualityCalls;
        public Func<LiveRoomDetail,LivePlayQuality,Task<List<string>>> Urls;
        public Func<object,Task<LiveRoomDetail>> RoomDetails;
        public List<LivePlayQuality> Qualities=new();
        public ILiveDanmaku GetDanmaku()=>Danmaku;
        public Task<LiveRoomDetail> GetRoomDetail(object room) => RoomDetails?.Invoke(room) ?? Task.FromResult(new LiveRoomDetail { RoomID=room.ToString(), Title="Fixture", UserName="Fixture", Status=true, DanmakuData=room });
        public Task<List<LivePlayQuality>> GetPlayQuality(LiveRoomDetail room) { QualityCalls++; return Task.FromResult(Qualities); }
        public Task<List<string>> GetPlayUrls(LiveRoomDetail room,LivePlayQuality quality)=>Urls?.Invoke(room,quality) ?? Task.FromResult(new List<string>());
        public Task<List<LiveSuperChatMessage>> GetSuperChatMessages(object room)=>Task.FromResult(new List<LiveSuperChatMessage>());
        public Task<List<LiveCategory>> GetCategores()=>Task.FromResult(new List<LiveCategory>());
        public Task<LiveSearchResult> Search(string s,int page=1)=>Task.FromResult(new LiveSearchResult());
        public Task<LiveCategoryResult> GetCategoryRooms(LiveSubCategory c,int page=1)=>Task.FromResult(new LiveCategoryResult());
        public Task<LiveCategoryResult> GetRecommendRooms(int page=1)=>Task.FromResult(new LiveCategoryResult());
        public Task<LiveStatusType> GetLiveStatus(object room)=>Task.FromResult(LiveStatusType.Live);
    }
}
