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
        private bool _initializing = false;
        private bool _finishing = false;
        private bool _dragging = false;
        private double _dragOffsetX = 0;
        private double _dragOffsetY = 0;

        public DouyuLoginDialog()
        {
            this.InitializeComponent();
            // Only the Popup child enters the visual tree; this unattached UserControl does not.
            webView.Loaded += DouyuLoginDialog_Loaded;
            loginPopup.Closed += (sender, args) =>
            {
                // 兜底:弹层关闭时确保等待方被唤醒
                _tcs?.TrySetResult(false);
            };
        }

        /// <summary>打开登录框,返回登录是否成功(取消或关闭返回 false)。</summary>
        public Task<bool> ShowAsync()
        {
            if (_tcs != null && !_tcs.Task.IsCompleted) return _tcs.Task;
            _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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
            // Resolve success before Closed can resolve the same task as false.
            _tcs?.TrySetResult(success);
            loginPopup.IsOpen = false;
            webView.Close();
        }

        private async void DouyuLoginDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Popup 打开后内容才进入可视化树,Loaded 触发时初始化 WebView2
            if (_webViewInitialized || _initializing || !loginPopup.IsOpen)
            {
                return;
            }
            _initializing = true;
            BtnDone.IsEnabled = false;
            try
            {
                txtStatus.Text = "正在初始化 WebView2...";
                var initialize = webView.EnsureCoreWebView2Async().AsTask();
                if (await Task.WhenAny(initialize, Task.Delay(TimeSpan.FromSeconds(20))) != initialize)
                {
                    // Observe a late failure without touching a closed dialog or logging sensitive data.
                    _ = initialize.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    if (loginPopup.IsOpen) txtStatus.Text = "WebView2 初始化超时，请取消后重新打开登录窗口";
                    return;
                }
                await initialize;
                if (!loginPopup.IsOpen) return;
                webView.CoreWebView2.Settings.UserAgent = CHROME_UA;
                webView.NavigationCompleted += WebView_NavigationCompleted;
                // 直接打开独立登录页,只显示登录框(扫码/账号登录),不带门户内容
                txtStatus.Text = "正在加载斗鱼登录页面...";
                webView.CoreWebView2.Navigate("https://passport.douyu.com/");
                _webViewInitialized = true;
                BtnDone.IsEnabled = true;
            }
            catch (Exception ex)
            {
                LogHelper.Log($"斗鱼登录初始化失败 HRESULT={ex.HResult}", LogType.ERROR);
                if (loginPopup.IsOpen) txtStatus.Text = $"WebView2 初始化失败 ({ex.HResult})，请检查 Edge WebView2 Runtime";
            }
            finally { _initializing = false; }
        }

        private void WebView_NavigationCompleted(Microsoft.UI.Xaml.Controls.WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!loginPopup.IsOpen) return;
            if (args.IsSuccess)
            {
                txtStatus.Text = "请使用斗鱼App扫码登录，或切换账号密码登录，成功后点击「完成登录」";
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
            if (_finishing || !loginPopup.IsOpen) return;
            _finishing = true;
            BtnDone.IsEnabled = false;
            try
            {
                if (webView.CoreWebView2 == null)
                {
                    txtStatus.Text = "WebView2 未初始化";
                    return;
                }

                var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync("https://www.douyu.com");

                if (!loginPopup.IsOpen) return;
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
                // UID alone is not proof of login. Web login uses dy_auth; H5 may use JWT.
                bool hasLoginToken = cookies.Any(c => !string.IsNullOrWhiteSpace(c.Value) &&
                    (c.Name == "dy_auth" || c.Name == "acf_auth" || c.Name == "acf_jwt_token"));

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
                LogHelper.Log($"获取斗鱼登录状态失败 HRESULT={ex.HResult}", LogType.ERROR);
                if (loginPopup.IsOpen) txtStatus.Text = $"读取登录状态失败 ({ex.HResult})";
            }
            finally
            {
                _finishing = false;
                if (loginPopup.IsOpen) BtnDone.IsEnabled = _webViewInitialized;
            }
        }
    }
}
