using AllLive.Core;
using AllLive.UWP.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AllLive.UWP.Helper
{
    public class BiliAccount
    {
        public event EventHandler OnAccountChanged;
        private static readonly BiliAccount instance = new BiliAccount();
        public static BiliAccount Instance => instance;
        private int revision;
        private readonly Func<string, string, CancellationToken, Task<string>> getString;
        public BiliAccount() : this(LoginHttp.GetStringAsync) { }
        internal BiliAccount(Func<string, string, CancellationToken, Task<string>> getString)
        {
            this.getString = getString ?? throw new ArgumentNullException(nameof(getString));
        }
        public bool Logined { get; private set; }
        public string UserName { get; private set; } = "";
        public AccountVerificationState VerificationState { get; private set; } = AccountVerificationState.SignedOut;
        public string StatusMessage { get; private set; } = "登录可享受高清直播";
        public long UserId => SettingHelper.GetValue<long>(SettingHelper.BILI_USER_ID, 0L);
        public string Cookie => SettingHelper.GetValue<string>(SettingHelper.BILI_COOKIE, "");
        public bool HasSavedSession => !string.IsNullOrWhiteSpace(Cookie);

        public Task InitLoginInfo() { return LoadUserInfo(); }
        public async Task LoadUserInfo()
        {
            if (!HasSavedSession) { Logout(); return; }
            // Sync can replace the stored cookie before invoking this method. Never
            // carry the previous account's success/name across that identity change.
            Logined = false;
            UserName = "";
            var site = MainVM.Sites?.FirstOrDefault(x => x.SiteType == LiveSite.Bilibili)?.LiveSite as BiliBili;
            if (site != null) { site.Cookie = ""; site.UserId = 0; }
            await TryLoginAsync(Cookie, CancellationToken.None);
        }

        public async Task<bool> TryLoginAsync(string candidate, CancellationToken token)
        {
            var current = ++revision;
            if (!LoginCookiePolicy.HasSessionCookie(candidate, "bilibili.com"))
            {
                SetState(AccountVerificationState.Failed, "未检测到有效登录会话，请重新扫码");
                return false;
            }
            SetState(AccountVerificationState.Verifying, "正在验证哔哩哔哩账号…");
            try
            {
                var response = await getString("https://api.bilibili.com/x/member/web/account", candidate, token);
                token.ThrowIfCancellationRequested();
                if (current != revision) return false;
                var json = JObject.Parse(response);
                var code = (int?)json["code"];
                var id = (long?)json["data"]?["mid"];
                var name = (string)json["data"]?["uname"];
                if (code == 0 && id > 0 && !string.IsNullOrWhiteSpace(name))
                {
                    SettingHelper.SetValue(SettingHelper.BILI_COOKIE, candidate);
                    SettingHelper.SetValue(SettingHelper.BILI_USER_ID, id.Value);
                    UserName = name;
                    Logined = true;
                    SetBiliSiteCookie();
                    SetState(AccountVerificationState.Verified, "已登录：" + name);
                    return true;
                }
                if (code == -101)
                {
                    // Do not delete a different saved account when a new candidate is invalid.
                    if (Cookie == candidate) ClearSavedSession();
                    SetState(AccountVerificationState.Expired, "登录已失效，请重新扫码");
                }
                else SetState(AccountVerificationState.Failed, "账号验证失败，请重试；未保存新会话");
            }
            catch (OperationCanceledException)
            {
                if (current == revision) SetState(HasSavedSession ? AccountVerificationState.Unverified : AccountVerificationState.SignedOut,
                    HasSavedSession ? "已保存会话，尚未验证" : "已取消登录");
            }
            catch (Exception)
            {
                if (current == revision && !token.IsCancellationRequested)
                    SetState(AccountVerificationState.Failed, "账号验证失败，请检查网络后重试；未保存新会话");
            }
            return false;
        }

        private void SetState(AccountVerificationState state, string message)
        {
            VerificationState = state;
            StatusMessage = message;
            OnAccountChanged?.Invoke(this, EventArgs.Empty);
        }
        public void SetBiliSiteCookie()
        {
            var site = MainVM.Sites?.FirstOrDefault(x => x.SiteType == LiveSite.Bilibili)?.LiveSite as BiliBili;
            if (site == null) return;
            site.Cookie = Cookie;
            site.UserId = UserId;
        }
        private void ClearSavedSession()
        {
            Logined = false;
            UserName = "";
            SettingHelper.SetValue(SettingHelper.BILI_COOKIE, "");
            SettingHelper.SetValue(SettingHelper.BILI_USER_ID, 0L);
            SetBiliSiteCookie();
        }
        public void Logout()
        {
            ++revision;
            ClearSavedSession();
            SetState(AccountVerificationState.SignedOut, "登录可享受高清直播");
        }
    }
}
