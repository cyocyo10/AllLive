using AllLive.UWP.Helper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace AllLive.UWP.Controls
{
    /// <summary>
    /// 可拖动的斗鱼登录框。基于 Popup 而非 ContentDialog,
    /// 按住标题栏可拖动位置,避免遮挡扫码或页面内容。
    /// </summary>
    public sealed partial class DouyuLoginDialog : UserControl
    {
        private const string CHROME_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";
        private const double DialogWidth = 520;
        private const double DialogHeight = 640;

        private TaskCompletionSource<bool> _tcs;
        private bool _webViewInitialized = false;
        private bool _dragging = false;
        private double _dragOffsetX = 0;
        private double _dragOffsetY = 0;

        public DouyuLoginDialog()
        {
            this.InitializeComponent();
            this.Loaded += DouyuLoginDialog_Loaded;
            loginPopup.Closed += (sender, args) =>
            {
                // 兜底:弹层关闭时确保等待方被唤醒
                _tcs?.TrySetResult(false);
            };
        }

        /// <summary>打开登录框,返回登录是否成功(取消或关闭返回 false)。</summary>
        public Task<bool> ShowAsync()
        {
            _tcs = new TaskCompletionSource<bool>();
            CenterPopup();
            loginPopup.IsOpen = true;
            return _tcs.Task;
        }

        private void CenterPopup()
        {
            var bounds = Window.Current.Bounds;
            loginPopup.HorizontalOffset = Math.Max(0, (bounds.Width - DialogWidth) / 2);
            loginPopup.VerticalOffset = Math.Max(0, (bounds.Height - DialogHeight) / 2);
        }

        private void Close(bool success)
        {
            loginPopup.IsOpen = false;
            _tcs?.TrySetResult(success);
        }

        private async void DouyuLoginDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Popup 打开后内容才进入可视化树,Loaded 触发时初始化 WebView2
            if (_webViewInitialized)
            {
                return;
            }
            try
            {
                txtStatus.Text = "正在初始化 WebView2...";
                await webView.EnsureCoreWebView2Async();
                webView.CoreWebView2.Settings.UserAgent = CHROME_UA;
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.CoreWebView2.Navigate("https://www.douyu.com");
                _webViewInitialized = true;
            }
            catch (Exception ex)
            {
                LogHelper.Log("WebView2初始化失败", LogType.ERROR, ex);
                txtStatus.Text = "WebView2 初始化失败，请确保已安装 Edge WebView2 Runtime\n" + ex.Message;
            }
        }

        private void WebView_NavigationCompleted(Microsoft.UI.Xaml.Controls.WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                txtStatus.Text = "请在页面中点击「登录」并完成登录，成功后点击「完成登录」";
            }
            else
            {
                txtStatus.Text = $"页面加载失败({args.WebErrorStatus})，请重试";
            }
        }

        private void TitleBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _dragging = true;
            var point = e.GetCurrentPoint(null).Position;
            _dragOffsetX = point.X - loginPopup.HorizontalOffset;
            _dragOffsetY = point.Y - loginPopup.VerticalOffset;
            TitleBar.CapturePointer(e.Pointer);
        }

        private void TitleBar_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }
            var point = e.GetCurrentPoint(null).Position;
            var bounds = Window.Current.Bounds;
            // 限制在窗口范围内,防止拖丢
            var x = Math.Max(-(DialogWidth - 60), Math.Min(point.X - _dragOffsetX, bounds.Width - 60));
            var y = Math.Max(0, Math.Min(point.Y - _dragOffsetY, bounds.Height - 48));
            loginPopup.HorizontalOffset = x;
            loginPopup.VerticalOffset = y;
        }

        private void TitleBar_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _dragging = false;
            TitleBar.ReleasePointerCapture(e.Pointer);
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            TryFinishLogin();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private async void TryFinishLogin()
        {
            try
            {
                if (webView.CoreWebView2 == null)
                {
                    txtStatus.Text = "WebView2 未初始化";
                    return;
                }

                var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync("https://www.douyu.com");

                var cookieParts = new List<string>();
                foreach (var cookie in cookies)
                {
                    cookieParts.Add($"{cookie.Name}={cookie.Value}");
                }

                if (cookieParts.Count == 0)
                {
                    txtStatus.Text = "未检测到Cookie，请先登录";
                    return;
                }

                // 检查是否包含登录态关键 Cookie(acf_auth 为斗鱼登录令牌)
                bool hasLoginToken = cookieParts.Any(c =>
                    c.StartsWith("acf_auth=") ||
                    c.StartsWith("acf_uid="));

                if (!hasLoginToken)
                {
                    txtStatus.Text = "似乎还未登录成功，请确认已登录后再点击完成";
                    return;
                }

                var cookieStr = string.Join(";", cookieParts);

                DouyuAccount.Instance.SetCookie(cookieStr);
                Utils.ShowMessageToast("斗鱼登录成功");
                Close(true);
            }
            catch (Exception ex)
            {
                LogHelper.Log("获取斗鱼Cookie失败", LogType.ERROR, ex);
                txtStatus.Text = "获取Cookie失败: " + ex.Message;
            }
        }
    }
}
