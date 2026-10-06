using AllLive.Core;
using AllLive.UWP.ViewModels;
using System;
using System.Linq;

namespace AllLive.UWP.Helper
{
    public class DouyinAccount
    {
        public event EventHandler OnAccountChanged;

        private static DouyinAccount instance;
        public static DouyinAccount Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DouyinAccount();
                }
                return instance;
            }
        }

        // Legacy compatibility: a saved session is available to existing requests.
        // UI must use VerificationState; this flag does not prove server-side login.
        public bool Logined { get; private set; } = false;
        public bool HasSavedSession => !string.IsNullOrWhiteSpace(Cookie);
        public AccountVerificationState VerificationState { get; private set; } = AccountVerificationState.SignedOut;
        public string StatusMessage => HasSavedSession ? "已保存会话，未验证" : "尚未保存登录会话";

        public string Cookie
        {
            get
            {
                return SettingHelper.GetValue<string>(SettingHelper.DOUYIN_COOKIE, "");
            }
        }

        public void InitLoginInfo()
        {
            Logined = HasSavedSession;
            VerificationState = Logined ? AccountVerificationState.Unverified : AccountVerificationState.SignedOut;
            if (Logined)
            {
                SetDouyinSiteCookie();
            }
            OnAccountChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetCookie(string cookie)
        {
            SettingHelper.SetValue(SettingHelper.DOUYIN_COOKIE, cookie);
            Logined = HasSavedSession;
            VerificationState = Logined ? AccountVerificationState.Unverified : AccountVerificationState.SignedOut;
            SetDouyinSiteCookie();
            OnAccountChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetDouyinSiteCookie()
        {
            var site = MainVM.Sites?.FirstOrDefault(x => x.SiteType == LiveSite.Douyin)?.LiveSite as Douyin;
            if (site != null) site.SearchCookie = Cookie;
        }

        public void Logout()
        {
            Logined = false;
            VerificationState = AccountVerificationState.SignedOut;
            SettingHelper.SetValue(SettingHelper.DOUYIN_COOKIE, "");
            SetDouyinSiteCookie();
            OnAccountChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
