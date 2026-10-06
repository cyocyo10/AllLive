// Harness-only UI/SignalR dependencies. Database calls use the real Microsoft.Data.Sqlite engine in a temporary file; no service, account, or UWP storage is used.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace AllLive.UWP.ViewModels
{
    public class BaseNotifyPropertyChanged { protected void DoPropertyChanged(string name) {} }
    public class BaseViewModel : BaseNotifyPropertyChanged {}
}
namespace AllLive.Core.Models { public enum LiveStatusType { Offline, Live, Replay } }
namespace AllLive.Core.Danmaku.Proto {}
namespace Windows.UI.Xaml {}
namespace Windows.System {}
namespace Windows.UI.Core
{
    public enum CoreDispatcherPriority { Normal }
    public class CoreDispatcher
    {
        public Task RunAsync(CoreDispatcherPriority priority, Action action) { action(); return Task.CompletedTask; }
    }
}
namespace Windows.UI.Popups
{
    public class UICommand { public UICommand(string label, object handler, object id) { Id=id; } public object Id {get;} }
    public class MessageDialog
    {
        public MessageDialog(string text, string title) { throw new InvalidOperationException("Dialogs are outside this offline harness"); }
        public List<UICommand> Commands {get;} = new List<UICommand>();
        public Task<UICommand> ShowAsync() => throw new InvalidOperationException("UI not enabled");
    }
}
namespace Microsoft.Toolkit.Uwp.Helpers
{
    public class SystemInformation
    {
        public static SystemInformation Instance {get;} = new SystemInformation();
        public Version ApplicationVersion {get;} = new Version(0,0,0);
    }
}
namespace Microsoft.AspNetCore.SignalR.Client
{
    public enum HubConnectionState { Connected, Disconnected }
    public class HubConnectionBuilder
    {
        public HubConnectionBuilder WithUrl(string url) => throw new InvalidOperationException("Network is disabled in harness");
        public HubConnection Build() => throw new InvalidOperationException("Network is disabled in harness");
    }
    public class HubConnection
    {
        public event Func<Exception,Task> Closed;
        public async Task RaiseClosedAsync(Exception error)
        {
            var handlers = Closed;
            if (handlers == null) return;
            foreach (Func<Exception, Task> handler in handlers.GetInvocationList())
                await handler(error);
        }
        public string ConnectionId {get;} = "not-connected";
        public HubConnectionState State {get;} = HubConnectionState.Disconnected;
        public Task StartAsync() => throw new InvalidOperationException("Network is disabled in harness");
        public Task DisposeAsync() => Task.CompletedTask;
        public IDisposable On<T1,T2>(string name, Action<T1,T2> callback) => throw new InvalidOperationException("Network is disabled in harness");
        public IDisposable On<T>(string name, Action<T> callback) => throw new InvalidOperationException("Network is disabled in harness");
        public Task<T> InvokeAsync<T>(string name, params object[] args) => throw new InvalidOperationException("Network is disabled in harness");
    }
}
namespace AllLive.UWP.Helper
{
    public static class Utils { public static bool IsXbox => false; public static List<string> Toasts=new List<string>(); public static void ShowMessageToast(string s) => Toasts.Add(s); }
    public enum LogType { DEBUG, ERROR }
    public static class LogHelper { public static void Log(string s, LogType t, Exception e=null) {} }
    public static class MessageCenter { public static int Updates; public static void UpdateFavorite() { Updates++; } }
    public static class SettingHelper
    {
        public const string BILI_COOKIE="cookie";
        public static class LiveDanmaku { public const string SHIELD_WORD="shield"; }
        public static T GetValue<T>(string key, T fallback) => throw new InvalidOperationException("Settings unavailable in harness");
        public static void SetValue(string key, object value) => throw new InvalidOperationException("Settings unavailable in harness");
    }
    public class BiliAccount
    {
        public static BiliAccount Instance {get;} = new BiliAccount();
        public Task LoadUserInfo() => throw new InvalidOperationException("Accounts disabled in harness");
    }
}
namespace Windows.Storage
{
    public enum CreationCollisionOption { OpenIfExists }
    public class ApplicationData
    {
        public static ApplicationData Current => throw new InvalidOperationException("Real UWP storage disabled in harness");
        public StorageFolder LocalFolder => throw new InvalidOperationException("Real UWP storage disabled in harness");
    }
    public class StorageFolder
    {
        public string Path => throw new InvalidOperationException("Real storage disabled");
        public Task CreateFileAsync(string name, CreationCollisionOption mode) => throw new InvalidOperationException("Real storage disabled");
    }
}
