using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Web.WebView2.Core
{
    public sealed class CoreWebView2Cookie
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Domain { get; set; }
        public bool IsSession { get; set; } = true;
        public double Expires { get; set; }
    }
    public sealed class CoreWebView2CookieManager
    {
        public readonly List<CoreWebView2Cookie> Cookies = new List<CoreWebView2Cookie>();
        public readonly List<CoreWebView2Cookie> Deleted = new List<CoreWebView2Cookie>();
        public Func<string, Task<IReadOnlyList<CoreWebView2Cookie>>> Read;
        public int Reading, PeakReading;
        public async Task<IReadOnlyList<CoreWebView2Cookie>> GetCookiesAsync(string uri)
        {
            Reading++; PeakReading = Math.Max(PeakReading, Reading);
            try
            {
                if (Read != null) return await Read(uri);
                return Cookies.Where(x => uri == "" || (new Uri(uri).Host == x.Domain.TrimStart('.') || new Uri(uri).Host.EndsWith(x.Domain.StartsWith(".") ? x.Domain : "." + x.Domain))).ToArray();
            }
            finally { Reading--; }
        }
        public void DeleteCookie(CoreWebView2Cookie cookie) { Deleted.Add(cookie); Cookies.Remove(cookie); }
    }
    public sealed class CoreWebView2NavigationCompletedEventArgs : EventArgs { public bool IsSuccess { get; set; } }
    public sealed class FakeSettings { public string UserAgent { get; set; } }
    public sealed class CoreWebView2
    {
        public CoreWebView2CookieManager CookieManager { get; } = new CoreWebView2CookieManager();
        public FakeSettings Settings { get; } = new FakeSettings();
        public List<string> Navigations { get; } = new List<string>();
        public bool Stopped;
        public void Navigate(string url) { Navigations.Add(url); }
        public void Stop() { Stopped = true; }
    }
}
namespace Microsoft.UI.Xaml.Controls
{
    using Microsoft.Web.WebView2.Core;
    public class WebView2 : Windows.UI.Xaml.Controls.Control
    {
        public static Action<WebView2> OnCreated;
        public WebView2() { OnCreated?.Invoke(this); }
        public CoreWebView2 CoreWebView2 { get; set; } = new CoreWebView2();
        public Func<Task> Initialize;
        public bool IsClosed;
        public event Action<WebView2, CoreWebView2NavigationCompletedEventArgs> NavigationCompleted;
        public Task EnsureCoreWebView2Async() { return Initialize?.Invoke() ?? Task.CompletedTask; }
        public void CompleteNavigation(bool success) { NavigationCompleted?.Invoke(this, new CoreWebView2NavigationCompletedEventArgs { IsSuccess = success }); }
        public void Close() { IsClosed = true; }
    }
}
namespace Windows.UI.Xaml
{
    public enum Visibility { Visible, Collapsed }
    public sealed class RoutedEventArgs : EventArgs { }
    public delegate void RoutedEventHandler(object sender, RoutedEventArgs args);
    public sealed partial class Window
    {
        public static Window Current { get; } = new Window();
        public Bounds Bounds { get; } = new Bounds();
    }
    public class Bounds { public double Width = 1200, Height = 800; }
}
namespace Windows.UI.Xaml.Input
{
    public class TappedRoutedEventArgs : EventArgs { }
    public class PointerRoutedEventArgs : EventArgs
    {
        public object Pointer { get; } = new object();
        public PointerPoint GetCurrentPoint(object relative) { return new PointerPoint(); }
    }
    public class PointerPoint { public Point Position { get; } = new Point(); }
    public class Point { public double X, Y; }
}
namespace Windows.UI.Xaml.Controls
{
    using Windows.UI.Xaml;
    public partial class Control
    {
        public event RoutedEventHandler Loaded, Unloaded;
        public Visibility Visibility { get; set; }
        public bool IsEnabled { get; set; } = true;
        public void RaiseLoaded() { Loaded?.Invoke(this, new RoutedEventArgs()); }
        public void RaiseUnloaded() { Unloaded?.Invoke(this, new RoutedEventArgs()); }
        public void CapturePointer(object pointer) { }
        public void ReleasePointerCapture(object pointer) { }
    }
    public class UserControl : Control { }
    public class ContentDialog : Control
    {
        public bool IsPrimaryButtonEnabled { get; set; }
        public bool Hidden;
        public Task ShowAsync() { return Task.CompletedTask; }
        public event Action<ContentDialog, ContentDialogClosingEventArgs> Closing;
        public event Action<ContentDialog, EventArgs> Closed;
        public void RaiseClosing() { Closing?.Invoke(this, new ContentDialogClosingEventArgs()); }
        public void Hide()
        {
            Hidden = true; Closing?.Invoke(this, new ContentDialogClosingEventArgs()); Closed?.Invoke(this, EventArgs.Empty);
        }
    }
    public class ContentDialogButtonClickEventArgs : EventArgs { public bool Cancel { get; set; } }
    public class ContentDialogClosingEventArgs : EventArgs { public Deferral GetDeferral() { return new Deferral(); } }
    public class Deferral { public void Complete() { } }
    public class TextBlock : Control { public string Text { get; set; } }
    public class Image : Control { public object Source { get; set; } }
    public class Button : Control { public object Content; }
    public class Grid : Control { public List<Control> Children = new List<Control>(); }
    public class Popup
    {
        private bool open;
        public double HorizontalOffset, VerticalOffset;
        public event EventHandler Closed;
        public bool IsOpen { get => open; set { bool old = open; open = value; if (old && !value) Closed?.Invoke(this, EventArgs.Empty); } }
    }
}
namespace ZXing
{
    public enum BarcodeFormat { QR_CODE }
    public sealed class BarcodeWriter { public BarcodeFormat Format; public Common.EncodingOptions Options; public object Write(string value) { return new object(); } }
}
namespace ZXing.Common { public sealed class EncodingOptions { public int Width, Height, Margin; } }
namespace AllLive.Core
{
    public enum LiveSite { Bilibili, Douyin }
    public sealed class BiliBili { public string Cookie; public long UserId; }
    public sealed class Douyin { public string SearchCookie; }
}
namespace AllLive.Core.Helper { public static class DouyuSignHelper { public static string AccountCookie; } }
namespace AllLive.UWP.ViewModels
{
    public sealed class SiteModel { public AllLive.Core.LiveSite SiteType; public object LiveSite; }
    public static class MainVM { public static List<SiteModel> Sites = new List<SiteModel>(); }
}
namespace AllLive.UWP.Helper
{
    public static partial class SettingHelper
    {
        private static readonly Dictionary<string, object> Data = new Dictionary<string, object>();
        public const string BILI_COOKIE = "bili", BILI_USER_ID = "bili-id", DOUYU_COOKIE = "douyu", DOUYIN_COOKIE = "douyin";
        public static T GetValue<T>(string key, T fallback) { return Data.TryGetValue(key, out var value) ? (T)value : fallback; }
        public static void SetValue<T>(string key, T value) { Data[key] = value; }
        public static void Reset() { Data.Clear(); }
    }
    public static partial class Utils { public static readonly List<string> Toasts = new List<string>(); public static void ShowMessageToast(string text) { Toasts.Add(text); } }
}
namespace AllLive.UWP.Controls
{
    using Windows.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls;
    public sealed partial class BiliLoginDialog
    {
        public Image imgQR = new Image(); public Control loaddingImage = new Control(); public TextBlock txtStatus = new TextBlock();
        private void InitializeComponent() { }
    }
    public sealed partial class DouyuLoginDialog
    {
        public WebView2 webView = new WebView2(); public Popup loginPopup = new Popup(); public TextBlock txtStatus = new TextBlock();
        public Button BtnDone = new Button(); public Grid TitleBar = new Grid();
        private void InitializeComponent() { }
    }
    public sealed partial class DouyinLoginDialog
    {
        public WebView2 webView = new WebView2(); public TextBlock txtStatus = new TextBlock();
        private void InitializeComponent() { }
    }
}
