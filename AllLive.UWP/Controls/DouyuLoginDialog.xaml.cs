using AllLive.UWP.Helper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace AllLive.UWP.Controls
{
    public sealed partial class DouyuLoginDialog : ContentDialog
    {
        private const string CHROME_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";
        public bool LoginSuccess { get; private set; } = false;

        public DouyuLoginDialog()
        {
            this.InitializeComponent();
            this.Loaded += DouyuLoginDialog_Loaded;
        }

        private async void DouyuLoginDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Text = "正在初始化 WebView2...";
                await webView.EnsureCoreWebView2Async();
                webView.CoreWebView2.Settings.UserAgent = CHROME_UA;
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.CoreWebView2.Navigate("https://www.douyu.com");
            }
            catch (Exception ex)
            {
                LogHelper.Log("WebView2初始化失败", LogType.ERROR, ex);
                txtStatus.Text = "WebView2 初始化失败，请确保已安装 Edge WebView2 Runtime\n" + ex.Message;
                txtStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red);
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

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
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
                LoginSuccess = true;
                Utils.ShowMessageToast("斗鱼登录成功");
                this.Hide();
            }
            catch (Exception ex)
            {
                LogHelper.Log("获取斗鱼Cookie失败", LogType.ERROR, ex);
                txtStatus.Text = "获取Cookie失败: " + ex.Message;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
