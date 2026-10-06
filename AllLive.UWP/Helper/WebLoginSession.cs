using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AllLive.UWP.Helper
{
    /// <summary>Official web login UI with single-flight detection and scoped cookie cleanup.</summary>
    internal sealed class WebLoginSession
    {
        private readonly WebView2 view;
        private readonly string domain;
        private readonly string loginUrl;
        private readonly Action<string, bool> status;
        private readonly TimeSpan cleanupTimeout;
        private readonly LoginAttempt attempt = new LoginAttempt();
        private readonly SemaphoreSlim inspection = new SemaphoreSlim(1, 1);
        private CancellationToken token;
        private Task initialization;
        private Task cleanup;
        private bool closed;
        private bool ready;
        private string candidate;
        private int pageRevision;
        public bool Saved { get; private set; }

        public WebLoginSession(WebView2 view, string domain, string loginUrl, Action<string, bool> status, TimeSpan? cleanupTimeout = null)
        {
            this.view = view;
            this.domain = domain;
            this.loginUrl = loginUrl;
            this.status = status;
            this.cleanupTimeout = cleanupTimeout ?? TimeSpan.FromSeconds(5);
        }

        public Task StartAsync()
        {
            if (closed) return Task.CompletedTask;
            if (initialization != null) return initialization;
            token = attempt.Begin();
            initialization = InitializeAsync();
            return initialization;
        }

        private async Task InitializeAsync()
        {
            status("正在加载官方登录页…", false);
            try
            {
                await view.EnsureCoreWebView2Async();
                token.ThrowIfCancellationRequested();
                // A fresh official flow also recovers cleanup after an interrupted app exit.
                await ClearCookiesAsync(view, domain, token, cleanupTimeout);
                token.ThrowIfCancellationRequested();
                view.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";
                view.NavigationCompleted += NavigationCompleted;
                ready = true;
                view.CoreWebView2.Navigate(loginUrl);
                _ = PollAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (!closed) status("登录页初始化失败，请关闭后重试，并确认已安装 Edge WebView2 Runtime。", false);
            }
        }

        private void NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (closed || Saved) return;
            if (!args.IsSuccess)
                status("页面加载失败，请点击“重试页面”。", false);
            else if (candidate == null)
                status("请在官方页面扫码并确认；如需验证码，请直接在页面完成。", false);
        }

        public void Reload()
        {
            if (closed || !ready || Saved) return;
            ++pageRevision;
            candidate = null;
            status("正在重新加载官方登录页…", false);
            view.CoreWebView2.Navigate(loginUrl);
        }

        private async Task PollAsync()
        {
            try
            {
                while (!token.IsCancellationRequested && !Saved)
                {
                    await InspectAsync(false);
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task<bool> InspectAsync(bool reportMissing)
        {
            if (closed || !ready || Saved || !await inspection.WaitAsync(0)) return false;
            var inspectedPage = pageRevision;
            try
            {
                var cookies = await view.CoreWebView2.CookieManager.GetCookiesAsync("https://www." + domain);
                token.ThrowIfCancellationRequested();
                if (inspectedPage != pageRevision) return false;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var value = string.Join(";", cookies.Where(x => LoginCookiePolicy.IsPlatformDomain(x.Domain, domain)
                    && (x.IsSession || x.Expires > now)).Select(x => x.Name + "=" + x.Value));
                if (!LoginCookiePolicy.HasSessionCookie(value, domain))
                {
                    var hadCandidate = candidate != null;
                    candidate = null;
                    if (hadCandidate || reportMissing) status("尚未检测到有效会话Cookie，请完成扫码确认；二维码过期可在官方页面刷新。", false);
                    return false;
                }
                candidate = value;
                status("已检测到会话，尚未验证账号有效性。可点击“保存会话”用于现有功能。", true);
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception)
            {
                if (!closed) { candidate = null; status("会话读取失败，请重试；未保存登录信息。", false); }
                return false;
            }
            finally { inspection.Release(); }
        }

        public async Task<bool> SaveAsync(Action<string> save)
        {
            if (!await InspectAsync(true) || closed || token.IsCancellationRequested || Saved) return false;
            // No await between the last lifecycle check and the commit on the UI thread.
            save(candidate);
            candidate = null;
            Saved = true;
            status("会话已保存，账号有效性尚未验证。", false);
            return true;
        }

        public Task CloseAsync()
        {
            if (cleanup != null) return cleanup;
            closed = true;
            candidate = null;
            attempt.Cancel();
            cleanup = CleanupAsync();
            return cleanup;
        }

        private async Task CleanupAsync()
        {
            // Close even while EnsureCoreWebView2Async is pending. It cannot be
            // cancelled, but its continuation checks our token before any navigation.
            // A stalled Runtime must not trap the user in a cancelled dialog.
            view.NavigationCompleted -= NavigationCompleted;
            try
            {
                if (view.CoreWebView2 != null)
                {
                    view.CoreWebView2.Stop();
                    view.CoreWebView2.Navigate("about:blank");
                    await RunBoundedCleanupAsync(t => ClearCookiesAsync(view.CoreWebView2.CookieManager, domain, t), CancellationToken.None, cleanupTimeout);
                }
            }
            finally { view.Close(); }
        }

        public static Task ClearCookiesAsync(WebView2 view, string domain,
            CancellationToken token = default(CancellationToken), TimeSpan? timeout = null)
        {
            return RunBoundedCleanupAsync(async cleanupToken =>
            {
                await view.EnsureCoreWebView2Async();
                cleanupToken.ThrowIfCancellationRequested();
                await ClearCookiesAsync(view.CoreWebView2.CookieManager, domain, cleanupToken);
            }, token, timeout ?? TimeSpan.FromSeconds(5));
        }

        private static async Task ClearCookiesAsync(CoreWebView2CookieManager manager, string domain, CancellationToken token)
        {
            // Empty URI enumerates the app profile, including HttpOnly cookies and all
            // paths/subdomains. Delete only exact platform domains, never DeleteAllCookies.
            var cookies = await manager.GetCookiesAsync("");
            token.ThrowIfCancellationRequested();
            foreach (var cookie in cookies)
            {
                token.ThrowIfCancellationRequested();
                if (LoginCookiePolicy.IsPlatformDomain(cookie.Domain, domain)) manager.DeleteCookie(cookie);
            }
        }

        private static async Task RunBoundedCleanupAsync(Func<CancellationToken, Task> action,
            CancellationToken token, TimeSpan timeout)
        {
            using (var budget = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var operation = action(budget.Token);
                var deadline = Task.Delay(timeout, budget.Token);
                if (await Task.WhenAny(operation, deadline) != operation)
                {
                    budget.Cancel();
                    // The platform operation itself may not cancel. Its continuation
                    // checks the abandoned token before deleting a newer login's cookies.
                    _ = ObserveAbandonedCleanupAsync(operation);
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException("Platform cookie cleanup timed out.");
                }
                budget.Cancel();
                await operation;
            }
        }
        private static async Task ObserveAbandonedCleanupAsync(Task operation)
        {
            try { await operation; } catch (Exception) { }
        }
    }
}
