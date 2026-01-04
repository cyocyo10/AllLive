using System;
using System.Text;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http.Filters;
using Microsoft.Web.WebView2.Core;

namespace AllLive.UWP.Helper
{
    public sealed partial class DouyinVerifyDialog : ContentDialog
    {
        public string VerifiedCookie { get; private set; }
        private readonly string _url;

        public DouyinVerifyDialog(string url)
        {
            this.InitializeComponent();
            _url = url;
            
            InitWebView();
        }

        private async void InitWebView()
        {
            try
            {
                await webView2.EnsureCoreWebView2Async();
                
                webView2.NavigationStarting += WebView2_NavigationStarting;
                webView2.NavigationCompleted += WebView2_NavigationCompleted;
                
                webView2.Source = new Uri(_url);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"[DouyinVerify] WebView2初始化失败: {ex.Message}", LogType.ERROR, ex);
                loadingRing.IsActive = false;
                tipText.Text = "WebView2初始化失败，请确保已安装Edge WebView2运行时";
            }
        }

        private void WebView2_NavigationStarting(Microsoft.UI.Xaml.Controls.WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            loadingRing.IsActive = true;
        }

        private void WebView2_NavigationCompleted(Microsoft.UI.Xaml.Controls.WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            loadingRing.IsActive = false;
            
            try
            {
                var filter = new HttpBaseProtocolFilter();
                var cookieManager = filter.CookieManager;
                var cookies = cookieManager.GetCookies(new Uri("https://www.douyin.com"));
                
                var sb = new StringBuilder();
                foreach (var cookie in cookies)
                {
                    sb.Append($"{cookie.Name}={cookie.Value};");
                }
                
                VerifiedCookie = sb.ToString().TrimEnd(';');
                LogHelper.Log($"[DouyinVerify] Cookie长度: {VerifiedCookie?.Length ?? 0}", LogType.DEBUG);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"[DouyinVerify] 获取Cookie失败: {ex.Message}", LogType.ERROR, ex);
            }
        }
    }
}
