using System;
using System.Text;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http.Filters;

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
            
            webView.NavigationStarting += WebView_NavigationStarting;
            webView.NavigationCompleted += WebView_NavigationCompleted;
            
            // 加载验证页面
            webView.Navigate(new Uri(url));
        }

        private void WebView_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            loadingRing.IsActive = true;
        }

        private void WebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            loadingRing.IsActive = false;
            
            // 获取当前页面的 Cookie
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
                LogHelper.Log($"[DouyinVerify] 获取到Cookie: {VerifiedCookie?.Substring(0, Math.Min(100, VerifiedCookie?.Length ?? 0))}...", LogType.DEBUG);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"[DouyinVerify] 获取Cookie失败: {ex.Message}", LogType.ERROR, ex);
            }
        }
    }
}
