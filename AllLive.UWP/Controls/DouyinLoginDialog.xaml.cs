using AllLive.UWP.Helper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace AllLive.UWP.Controls
{
    public sealed partial class DouyinLoginDialog : ContentDialog
    {
        private const string LoginPage = "https://www.douyin.com/";
        private const string SearchPage = "https://www.douyin.com/aweme/v1/web/live/search/";
        private bool initialized;
        private bool closed;
        private bool saving;
        private bool pageReady;
        private ulong navigationId;
        private long navigationVersion;

        public bool LoginSuccess { get; private set; }

        public DouyinLoginDialog()
        {
            InitializeComponent();
            IsPrimaryButtonEnabled = false;
            Loaded += DouyinLoginDialog_Loaded;
            Closing += (_, __) => closed = true;
        }

        private async void DouyinLoginDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (initialized || closed)
                return;

            initialized = true;
            try
            {
                txtStatus.Text = "正在初始化 WebView2...";
                await webView.EnsureCoreWebView2Async();
                if (closed)
                    return;

                webView.CoreWebView2.NavigationStarting += (_, args) =>
                {
                    navigationId = args.NavigationId;
                    navigationVersion++;
                    pageReady = false;
                    IsPrimaryButtonEnabled = false;
                    txtStatus.Text = "正在打开抖音网页...";
                };
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.CoreWebView2.Navigate(LoginPage);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"抖音 WebView2 初始化失败 (0x{ex.HResult:X8})", LogType.ERROR);
                if (!closed)
                    txtStatus.Text = "无法打开抖音网页。请确认已安装 Edge WebView2 Runtime 后重试。";
            }
        }

        private void WebView_NavigationCompleted(Microsoft.UI.Xaml.Controls.WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (closed || args.NavigationId != navigationId)
                return;

            pageReady = args.IsSuccess && IsDouyinPage(webView.CoreWebView2?.Source);
            IsPrimaryButtonEnabled = pageReady && !saving;
            if (pageReady)
                txtStatus.Text = "在抖音网页内点击「登录」。登录后，再点击下方「完成登录」。";
            else if (!args.IsSuccess)
                txtStatus.Text = $"抖音网页加载失败（{args.WebErrorStatus}）。请关闭窗口后重试。";
            else
                txtStatus.Text = "当前不是抖音网页，无法保存登录状态。";
        }

        private static bool IsDouyinPage(string source)
        {
            return Uri.TryCreate(source, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && uri.Host.Equals("www.douyin.com", StringComparison.OrdinalIgnoreCase);
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            var core = webView.CoreWebView2;
            if (closed || saving || !pageReady || core == null || !IsDouyinPage(core.Source))
                return;

            saving = true;
            IsPrimaryButtonEnabled = false;
            long checkedNavigation = navigationVersion;
            try
            {
                // Only obtain cookies applicable to the existing Douyin search request.
                var cookies = await core.CookieManager.GetCookiesAsync(SearchPage);
                if (closed || !pageReady || checkedNavigation != navigationVersion || !IsDouyinPage(core.Source))
                    return;

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var usable = cookies.Where(cookie =>
                    !string.IsNullOrWhiteSpace(cookie.Value)
                    && (cookie.IsSession || cookie.Expires > now)).ToList();

                // A session cookie is a local signal, not proof of server acceptance.
                if (!usable.Any(cookie => cookie.Name == "sessionid" || cookie.Name == "sessionid_ss"))
                {
                    txtStatus.Text = "没有检测到有效期内的抖音登录会话。请先在网页里登录，再点击完成。";
                    return;
                }

                DouyinAccount.Instance.SetCookie(string.Join("; ", usable.Select(cookie => $"{cookie.Name}={cookie.Value}")));
                LoginSuccess = true;
                Utils.ShowMessageToast("已保存抖音登录会话");
                Hide();
            }
            catch (Exception ex)
            {
                LogHelper.Log($"读取抖音登录会话失败 (0x{ex.HResult:X8})", LogType.ERROR);
                if (!closed && checkedNavigation == navigationVersion)
                    txtStatus.Text = "读取抖音登录会话失败，请重试。";
            }
            finally
            {
                saving = false;
                if (!closed && !LoginSuccess)
                    IsPrimaryButtonEnabled = pageReady;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
