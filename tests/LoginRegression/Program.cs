using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AllLive.UWP.Helper;
using AllLive.UWP.Controls;
using AllLive.UWP.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

internal static class Program
{
    private static int passed;
    private static readonly List<string> results = new List<string>();
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Test(string name, Func<Task> run)
    {
        SettingHelper.Reset();
        MainVM.Sites.Clear();
        await run(); passed++; results.Add(name); Console.WriteLine("PASS " + name);
    }
    private static CoreWebView2Cookie Cookie(string name, string domain = ".douyu.com", string value = "FAKE_TEST_VALUE")
        => new CoreWebView2Cookie { Name = name, Domain = domain, Value = value };
    private static TaskCompletionSource<T> Pending<T>() => new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    private static string Identity(long id, string name) => "{\"code\":0,\"data\":{\"mid\":" + id + ",\"uname\":\"" + name + "\"}}";
    private static async Task WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 1200 && !condition(); i++) await Task.Delay(5);
        Assert(condition(), "Async test did not reach expected state");
    }
    public static async Task Main(string[] args)
    {
        await Test("platform cookie scope accepts root/subdomain and rejects suffix attacks", () =>
        {
            Assert(LoginCookiePolicy.IsPlatformDomain(".douyu.com", "douyu.com"), "root");
            Assert(LoginCookiePolicy.IsPlatformDomain("PASSPORT.DOUYU.COM", "douyu.com"), "subdomain");
            foreach (var domain in new[] { "evildouyu.com", "douyu.com.evil", ".douyin.com", "", null })
                Assert(!LoginCookiePolicy.IsPlatformDomain(domain, "douyu.com"), "bad domain " + domain);
            return Task.CompletedTask;
        });
        await Test("candidate detection rejects uid-only empty prefix and sid_guard-only cookies", () =>
        {
            Assert(LoginCookiePolicy.HasSessionCookie("acf_auth=FAKE", "douyu.com"), "auth");
            Assert(!LoginCookiePolicy.HasSessionCookie("acf_uid=7", "douyu.com"), "uid not authentication");
            Assert(!LoginCookiePolicy.HasSessionCookie("acf_auth= ;acf_uid=7", "douyu.com"), "empty");
            Assert(LoginCookiePolicy.HasSessionCookie("sessionid=FAKE", "douyin.com"), "session");
            Assert(!LoginCookiePolicy.HasSessionCookie("sessionid_backup=FAKE;sid_guard=FAKE", "douyin.com"), "exact names");
            return Task.CompletedTask;
        });
        await Test("refresh and close invalidate old attempt tokens", () =>
        {
            using (var attempt = new LoginAttempt())
            {
                var old = attempt.Begin(); var next = attempt.Begin();
                Assert(old.IsCancellationRequested && !next.IsCancellationRequested, "refresh cancellation");
                attempt.Cancel(); Assert(next.IsCancellationRequested, "close cancellation");
                attempt.Cancel();
            }
            return Task.CompletedTask;
        });
        await Test("Bili saves only a validated identity and propagates exact account", async () =>
        {
            var site = new AllLive.Core.BiliBili(); MainVM.Sites.Add(new SiteModel { SiteType = AllLive.Core.LiveSite.Bilibili, LiveSite = site });
            var account = new BiliAccount((u, c, t) => Task.FromResult(Identity(42, "fake-user")));
            Assert(await account.TryLoginAsync("SESSDATA=FAKE_A", CancellationToken.None), "successful account");
            Assert(account.Logined && account.VerificationState == AccountVerificationState.Verified && account.UserId == 42 && account.UserName == "fake-user", "verified identity");
            Assert(site.Cookie == "SESSDATA=FAKE_A" && site.UserId == 42, "live site identity");
        });
        await Test("Bili late cancelled success cannot persist credentials", async () =>
        {
            var response = Pending<string>(); var account = new BiliAccount((u, c, t) => response.Task);
            var cancellation = new CancellationTokenSource(); var pending = account.TryLoginAsync("SESSDATA=FAKE_A", cancellation.Token);
            cancellation.Cancel(); response.SetResult(Identity(42, "fake-user"));
            Assert(!await pending && !account.Logined && !account.HasSavedSession, "cancelled write blocked");
        });
        await Test("Bili logout rejects an already in-flight identity response", async () =>
        {
            var response = Pending<string>(); var account = new BiliAccount((u, c, t) => response.Task);
            var pending = account.TryLoginAsync("SESSDATA=FAKE_A", CancellationToken.None);
            account.Logout(); response.SetResult(Identity(42, "fake-user"));
            Assert(!await pending && !account.Logined && !account.HasSavedSession && account.VerificationState == AccountVerificationState.SignedOut, "logout wins");
        });
        await Test("Bili newer account wins over stale prior-account response", async () =>
        {
            var older = Pending<string>();
            var account = new BiliAccount((u, c, t) => c.EndsWith("A") ? older.Task : Task.FromResult(Identity(22, "new-account")));
            var oldAttempt = account.TryLoginAsync("SESSDATA=FAKE_A", CancellationToken.None);
            Assert(await account.TryLoginAsync("SESSDATA=FAKE_B", CancellationToken.None), "new success");
            older.SetResult(Identity(11, "old-account"));
            Assert(!await oldAttempt && account.Cookie == "SESSDATA=FAKE_B" && account.UserId == 22, "new identity retained");
        });
        await Test("Bili network failure does not erase saved session or claim verification", async () =>
        {
            SettingHelper.SetValue(SettingHelper.BILI_COOKIE, "SESSDATA=FAKE_EXISTING");
            var account = new BiliAccount((u, c, t) => throw new InvalidOperationException("FAKE_NETWORK_FAILURE"));
            await account.InitLoginInfo();
            Assert(!account.Logined && account.HasSavedSession && account.VerificationState == AccountVerificationState.Failed, "unverified preserved");
        });
        await Test("Bili explicit expired response clears only matching saved session", async () =>
        {
            SettingHelper.SetValue(SettingHelper.BILI_COOKIE, "SESSDATA=FAKE_EXISTING");
            var account = new BiliAccount((u, c, t) => Task.FromResult("{\"code\":-101}"));
            Assert(!await account.TryLoginAsync("SESSDATA=FAKE_NEW", CancellationToken.None) && account.HasSavedSession, "different existing session preserved");
            await account.LoadUserInfo();
            Assert(!account.HasSavedSession && account.VerificationState == AccountVerificationState.Expired, "matching expired session cleared");
        });
        await Test("Bili HTTP-success malformed identity is never treated as authenticated", async () =>
        {
            foreach (var response in new[] { "{\"code\":0,\"data\":{}}", "{\"code\":0,\"data\":{\"mid\":0,\"uname\":\"x\"}}", "{}", "not-json" })
            {
                var account = new BiliAccount((u, c, t) => Task.FromResult(response));
                Assert(!await account.TryLoginAsync("SESSDATA=FAKE", CancellationToken.None) && !account.HasSavedSession, "invalid identity accepted");
            }
        });
        await Test("Bili externally replaced sync cookie cannot inherit prior verified account", async () =>
        {
            var site = new AllLive.Core.BiliBili(); MainVM.Sites.Add(new SiteModel { SiteType = AllLive.Core.LiveSite.Bilibili, LiveSite = site });
            var account = new BiliAccount((u, c, t) => c.EndsWith("A") ? Task.FromResult(Identity(42, "first")) : Task.FromException<string>(new Exception("FAKE_NETWORK")));
            await account.TryLoginAsync("SESSDATA=FAKE_A", CancellationToken.None);
            SettingHelper.SetValue(SettingHelper.BILI_COOKIE, "SESSDATA=FAKE_B");
            await account.LoadUserInfo();
            Assert(!account.Logined && account.UserName == "" && account.VerificationState == AccountVerificationState.Failed, "old account leaked");
            Assert(site.Cookie == "" && site.UserId == 0, "old downstream account leaked");
        });
        await Test("Douyu and Douyin preserve saved-session compatibility but remain unverified", () =>
        {
            var douyu = new DouyuAccount(); var douyin = new DouyinAccount();
            var site = new AllLive.Core.Douyin(); MainVM.Sites.Add(new SiteModel { SiteType = AllLive.Core.LiveSite.Douyin, LiveSite = site });
            douyu.SetCookie("acf_auth=FAKE_DY"); douyin.SetCookie("sessionid=FAKE_DYIN");
            douyu.InitLoginInfo(); douyin.InitLoginInfo();
            Assert(douyu.Logined && douyin.Logined && douyu.VerificationState == AccountVerificationState.Unverified && douyin.VerificationState == AccountVerificationState.Unverified, "compatible but unverified");
            Assert(AllLive.Core.Helper.DouyuSignHelper.AccountCookie == douyu.Cookie && site.SearchCookie == douyin.Cookie, "source compatibility");
            douyu.Logout(); douyin.Logout();
            Assert(!douyu.HasSavedSession && !douyin.HasSavedSession && site.SearchCookie == "" && AllLive.Core.Helper.DouyuSignHelper.AccountCookie == "", "logout propagation");
            return Task.CompletedTask;
        });
        await Test("web init removes platform cookies including subdomains but preserves other sites", async () =>
        {
            var view = new WebView2(); var manager = view.CoreWebView2.CookieManager;
            manager.Cookies.AddRange(new[] { Cookie("acf_auth"), Cookie("login", ".passport.douyu.com"), Cookie("sessionid", ".douyin.com"), Cookie("external", ".evildouyu.com") });
            var flow = new WebLoginSession(view, "douyu.com", "https://passport.douyu.com/", (s, b) => { });
            await flow.StartAsync();
            Assert(manager.Deleted.Count == 2 && manager.Cookies.Count == 2, "scoped reset");
            await flow.CloseAsync(); Assert(view.IsClosed && view.CoreWebView2.Stopped, "disposed view");
        });
        await Test("web candidate detection never automatically saves and manual save returns pending", async () =>
        {
            var view = new WebView2(); string status = null; bool canSave = false; int writes = 0;
            var flow = new WebLoginSession(view, "douyu.com", "https://passport.douyu.com/", (s, b) => { status = s; canSave = b; });
            await flow.StartAsync(); view.CoreWebView2.CookieManager.Cookies.Add(Cookie("acf_auth"));
            // The actual recurring detection loop is exercised, without external requests.
            await WaitFor(asyncCandidate);
            bool asyncCandidate() => canSave;
            Assert(writes == 0 && !flow.Saved && status.Contains("尚未验证"), "auto-save/false-success");
            Assert(await flow.SaveAsync(c => writes++) && writes == 1 && flow.Saved, "manual save");
            Assert(!await flow.SaveAsync(c => writes++) && writes == 1, "double save");
            await flow.CloseAsync(); Assert(view.CoreWebView2.CookieManager.Cookies.Count == 0, "saved flow web cleanup");
        });
        await Test("expired or empty web session is rejected", async () =>
        {
            var view = new WebView2(); var flow = new WebLoginSession(view, "douyu.com", "https://passport.douyu.com/", (s, b) => { });
            await flow.StartAsync(); var expired = Cookie("acf_auth"); expired.IsSession = false; expired.Expires = 1; view.CoreWebView2.CookieManager.Cookies.Add(expired);
            Assert(!await flow.SaveAsync(c => throw new Exception("must not write")), "expired session");
            view.CoreWebView2.CookieManager.Cookies.Clear(); view.CoreWebView2.CookieManager.Cookies.Add(Cookie("acf_auth", value: ""));
            Assert(!await flow.SaveAsync(c => throw new Exception("must not write")), "empty session"); await flow.CloseAsync();
        });
        await Test("late web-cookie read after cancellation cannot save", async () =>
        {
            var view = new WebView2(); var flow = new WebLoginSession(view, "douyu.com", "https://passport.douyu.com/", (s, b) => { });
            await flow.StartAsync(); var read = Pending<IReadOnlyList<CoreWebView2Cookie>>();
            view.CoreWebView2.CookieManager.Read = uri => uri == "" ? Task.FromResult<IReadOnlyList<CoreWebView2Cookie>>(Array.Empty<CoreWebView2Cookie>()) : read.Task;
            int writes = 0; var pending = flow.SaveAsync(c => writes++); await flow.CloseAsync(); read.SetResult(new[] { Cookie("acf_auth") });
            Assert(!await pending && writes == 0 && !flow.Saved && view.IsClosed, "late callback wrote");
        });
        await Test("duplicate web checks are single-flight and commit once", async () =>
        {
            var view = new WebView2(); var flow = new WebLoginSession(view, "douyu.com", "https://passport.douyu.com/", (s, b) => { }); await flow.StartAsync();
            var read = Pending<IReadOnlyList<CoreWebView2Cookie>>(); view.CoreWebView2.CookieManager.Read = uri => uri == "" ? Task.FromResult<IReadOnlyList<CoreWebView2Cookie>>(Array.Empty<CoreWebView2Cookie>()) : read.Task;
            int writes = 0; var one = flow.SaveAsync(c => writes++); var two = flow.SaveAsync(c => writes++); read.SetResult(new[] { Cookie("acf_auth") });
            Assert(await one && !await two && writes == 1 && view.CoreWebView2.CookieManager.PeakReading == 1, "overlapping reads/save"); await flow.CloseAsync();
        });
        await Test("page retry invalidates a prior in-flight cookie snapshot", async () =>
        {
            var view = new WebView2(); var flow = new WebLoginSession(view, "douyu.com", "https://passport.douyu.com/", (s, b) => { }); await flow.StartAsync();
            var read = Pending<IReadOnlyList<CoreWebView2Cookie>>(); view.CoreWebView2.CookieManager.Read = uri => uri == "" ? Task.FromResult<IReadOnlyList<CoreWebView2Cookie>>(Array.Empty<CoreWebView2Cookie>()) : read.Task;
            int writes = 0; var pending = flow.SaveAsync(c => writes++); flow.Reload(); read.SetResult(new[] { Cookie("acf_auth") });
            Assert(!await pending && writes == 0, "stale page committed"); await flow.CloseAsync();
        });
        await Test("cancel during WebView initialization prevents navigation and still disposes", async () =>
        {
            var view = new WebView2(); var initialized = Pending<bool>(); view.Initialize = () => initialized.Task;
            var flow = new WebLoginSession(view, "douyu.com", "https://passport.douyu.com/", (s, b) => { });
            var start = flow.StartAsync(); var close = flow.CloseAsync(); await close; initialized.SetResult(true); await start;
            Assert(view.IsClosed && !view.CoreWebView2.Navigations.Any(x => x.Contains("douyu")), "late navigation");
        });
        await Test("web initialization and cookie-read failures do not save", async () =>
        {
            var broken = new WebView2 { Initialize = () => Task.FromException(new Exception("FAKE_RUNTIME")) }; string status = null;
            var flow = new WebLoginSession(broken, "douyu.com", "https://passport.douyu.com/", (s, b) => status = s); await flow.StartAsync();
            Assert(status.Contains("初始化失败") && !await flow.SaveAsync(c => { }), "initialization failure");
            try { await flow.CloseAsync(); } catch { }
            var view = new WebView2(); flow = new WebLoginSession(view, "douyu.com", "https://passport.douyu.com/", (s, b) => status = s); await flow.StartAsync();
            view.CoreWebView2.CookieManager.Read = uri => Task.FromException<IReadOnlyList<CoreWebView2Cookie>>(new Exception("FAKE_READ"));
            Assert(!await flow.SaveAsync(c => { }) && status.Contains("读取失败"), "read failure");
            view.CoreWebView2.CookieManager.Read = null; await flow.CloseAsync();
        });
        await Test("Douyu popup repeated ShowAsync uses same completion and success beats Closed fallback", async () =>
        {
            var dialog = new DouyuLoginDialog(); var show = dialog.ShowAsync(); Assert(ReferenceEquals(show, dialog.ShowAsync()), "duplicate show replaced task");
            dialog.webView.RaiseLoaded(); dialog.webView.CoreWebView2.CookieManager.Cookies.Add(Cookie("acf_auth"));
            typeof(DouyuLoginDialog).GetMethod("BtnDone_Click", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(dialog, new object[] { null, new Windows.UI.Xaml.RoutedEventArgs() });
            Assert(await show && !dialog.loginPopup.IsOpen && dialog.webView.IsClosed, "successful popup returned false");
        });
        await Test("Douyu popup close cancels without saving and releases waiter", async () =>
        {
            var dialog = new DouyuLoginDialog(); var show = dialog.ShowAsync(); dialog.webView.RaiseLoaded(); dialog.Cancel();
            Assert(!await show && dialog.webView.IsClosed, "cancel result/disposal");
        });
        await Test("Settings subscribes once to all accounts and re-subscribes after navigation", () =>
        {
            DouyuAccount.Instance.Logout(); DouyinAccount.Instance.Logout(); BiliAccount.Instance.Logout();
            var page = new AllLive.UWP.Views.SettingsPage(); page.RaiseLoaded(); page.RaiseLoaded();
            var field = typeof(DouyuAccount).GetField("OnAccountChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert(((Delegate)field.GetValue(DouyuAccount.Instance)).GetInvocationList().Length == 1, "duplicate subscription");
            DouyuAccount.Instance.SetCookie("acf_auth=FAKE_PAGE");
            Assert(page.txtDouyu.Text.Contains("未验证") && page.BtnLogoutDouyu.Visibility == Windows.UI.Xaml.Visibility.Visible && (string)page.BtnLoginDouyu.Content == "重新登录", "pending management state");
            page.RaiseUnloaded(); page.txtDouyu.Text = "unchanged"; DouyuAccount.Instance.Logout();
            Assert(page.txtDouyu.Text == "unchanged", "unloaded page updated");
            page.RaiseLoaded(); Assert(page.BtnLogoutDouyu.Visibility == Windows.UI.Xaml.Visibility.Collapsed, "reload state stale");
            page.RaiseUnloaded(); return Task.CompletedTask;
        });
        await Test("Settings rejects repeated login clicks and cancels active flow on unload", async () =>
        {
            var page = new AllLive.UWP.Views.SettingsPage(); page.RaiseLoaded(); var completed = Pending<bool>(); int shows = 0, cancels = 0;
            var run = typeof(AllLive.UWP.Views.SettingsPage).GetMethod("RunLoginAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Func<Task> show = () => { shows++; return completed.Task; }; Action cancel = () => cancels++;
            var first = (Task)run.Invoke(page, new object[] { show, cancel }); var second = (Task)run.Invoke(page, new object[] { show, cancel }); await second;
            Assert(shows == 1 && !page.BtnLoginBili.IsEnabled && !page.BtnLoginDouyin.IsEnabled, "duplicate login or missing gate");
            page.RaiseUnloaded(); Assert(cancels == 1, "navigation failed to cancel"); completed.SetResult(true); await first;
            page.RaiseLoaded(); Assert(page.BtnLoginBili.IsEnabled, "gate not released"); page.RaiseUnloaded();
        });
        await Test("Settings logout clears app plus scoped browser cookies and releases cleanup view", async () =>
        {
            var page = new AllLive.UWP.Views.SettingsPage(); page.RaiseLoaded(); DouyuAccount.Instance.SetCookie("acf_auth=FAKE_PAGE"); WebView2 cleanup = null;
            WebView2.OnCreated = v => { cleanup = v; v.CoreWebView2.CookieManager.Cookies.AddRange(new[] { Cookie("acf_auth"), Cookie("other", ".douyin.com") }); };
            var logout = typeof(AllLive.UWP.Views.SettingsPage).GetMethod("LogoutAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)logout.Invoke(page, new object[] { (Action)DouyuAccount.Instance.Logout, "douyu.com", page.txtDouyu }); WebView2.OnCreated = null;
            Assert(!DouyuAccount.Instance.HasSavedSession && cleanup.CoreWebView2.CookieManager.Cookies.Count == 1 && cleanup.CoreWebView2.CookieManager.Cookies[0].Domain == ".douyin.com", "scope cleanup");
            Assert(cleanup.IsClosed && page.SettingsRoot.Children.Count == 0 && page.BtnLoginDouyu.IsEnabled, "cleanup view/gate leak"); page.RaiseUnloaded();
        });
        await Test("Settings reports browser cleanup failure without restoring app credentials", async () =>
        {
            var page = new AllLive.UWP.Views.SettingsPage(); page.RaiseLoaded(); DouyinAccount.Instance.SetCookie("sessionid=FAKE_PAGE");
            WebView2.OnCreated = v => v.Initialize = () => Task.FromException(new Exception("FAKE_RUNTIME"));
            var logout = typeof(AllLive.UWP.Views.SettingsPage).GetMethod("LogoutAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)logout.Invoke(page, new object[] { (Action)DouyinAccount.Instance.Logout, "douyin.com", page.txtDouyin }); WebView2.OnCreated = null;
            Assert(!DouyinAccount.Instance.HasSavedSession && page.txtDouyin.Text.Contains("清理失败") && page.BtnLoginDouyin.IsEnabled, "cleanup failure reporting"); page.RaiseUnloaded();
        });
        await Test("Bili Escape or Back closing transition immediately cancels pending attempt", () =>
        {
            var dialog = new BiliLoginDialog();
            var attempt = (LoginAttempt)typeof(BiliLoginDialog).GetField("attempt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog);
            var token = attempt.Begin(); dialog.RaiseClosing();
            Assert(token.IsCancellationRequested, "Closing left login active until Closed");
            return Task.CompletedTask;
        });
        await Test("stalled browser cleanup times out and its late response cannot delete new cookies", async () =>
        {
            var view = new WebView2(); var manager = view.CoreWebView2.CookieManager;
            var flow = new WebLoginSession(view, "douyu.com", "https://passport.douyu.com/", (s, b) => { }, TimeSpan.FromMilliseconds(30));
            await flow.StartAsync(); var read = Pending<IReadOnlyList<CoreWebView2Cookie>>(); manager.Read = uri => read.Task;
            bool timedOut = false; try { await flow.CloseAsync(); } catch (TimeoutException) { timedOut = true; }
            Assert(timedOut && view.IsClosed, "cleanup trapped dialog");
            var newer = Cookie("acf_auth", value: "FAKE_NEW_LOGIN"); manager.Cookies.Add(newer); read.SetResult(new[] { newer });
            await Task.Delay(50); Assert(manager.Cookies.Contains(newer) && manager.Deleted.Count == 0, "abandoned cleanup deleted new login");
        });
        await Test("logout cleanup deadline covers initialization and blocks late deletions", async () =>
        {
            var view = new WebView2(); var initialized = Pending<bool>(); view.Initialize = () => initialized.Task;
            bool timedOut = false;
            try { await WebLoginSession.ClearCookiesAsync(view, "douyu.com", CancellationToken.None, TimeSpan.FromMilliseconds(30)); }
            catch (TimeoutException) { timedOut = true; }
            var newer = Cookie("acf_auth", value: "FAKE_NEW_LOGIN"); view.CoreWebView2.CookieManager.Cookies.Add(newer); initialized.SetResult(true);
            await Task.Delay(50); Assert(timedOut && view.CoreWebView2.CookieManager.Deleted.Count == 0, "late initialized cleanup deleted new login");
        });
        await Test("cancelled pre-login cleanup cannot erase a later login", async () =>
        {
            var view = new WebView2(); var manager = view.CoreWebView2.CookieManager; var oldRead = Pending<IReadOnlyList<CoreWebView2Cookie>>(); int reads = 0;
            manager.Read = uri => ++reads == 1 ? oldRead.Task : Task.FromResult<IReadOnlyList<CoreWebView2Cookie>>(Array.Empty<CoreWebView2Cookie>());
            var flow = new WebLoginSession(view, "douyu.com", "https://passport.douyu.com/", (s, b) => { });
            var start = flow.StartAsync(); await flow.CloseAsync();
            var newer = Cookie("acf_auth", value: "FAKE_NEW_LOGIN"); manager.Cookies.Add(newer); oldRead.SetResult(new[] { newer }); await start; await Task.Delay(50);
            Assert(manager.Deleted.Count == 0 && !view.CoreWebView2.Navigations.Any(x => x.Contains("douyu")), "cancelled pre-login cleanup leaked");
        });
        Console.WriteLine("RESULT " + passed + "/" + passed + " passed; no real platform login, no real cookies, no UWP runtime execution.");
        System.IO.File.WriteAllText(args.Length == 0 ? System.IO.Path.Combine(AppContext.BaseDirectory, "test-results.json") : args[0], System.Text.Json.JsonSerializer.Serialize(new { passed, tests = results, scope = "Production C# linked with fake WebView2/UWP; real LoginHttp compiled but not called" }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}
