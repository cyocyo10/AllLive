using AllLive.Core.Helper;
using System;

namespace AllLive.UWP.Helper
{
    public class DouyuAccount
    {
        public event EventHandler OnAccountChanged;

        private static DouyuAccount instance;
        public static DouyuAccount Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DouyuAccount();
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
                return SettingHelper.GetValue<string>(SettingHelper.DOUYU_COOKIE, "");
            }
        }

        public void InitLoginInfo()
        {
            Logined = HasSavedSession;
            VerificationState = Logined ? AccountVerificationState.Unverified : AccountVerificationState.SignedOut;
            if (Logined)
            {
                SetDouyuSiteCookie();
            }
            OnAccountChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetCookie(string cookie)
        {
            SettingHelper.SetValue(SettingHelper.DOUYU_COOKIE, cookie);
            Logined = HasSavedSession;
            VerificationState = Logined ? AccountVerificationState.Unverified : AccountVerificationState.SignedOut;
            SetDouyuSiteCookie();
            OnAccountChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetDouyuSiteCookie()
        {
            // 斗鱼取流签名/播放/录制请求头统一读取该静态 Cookie;
            // 其中 dy_did/acf_did 会被签名进程 did 取代,避免会话冲突
            DouyuSignHelper.AccountCookie = Cookie;
        }

        public void Logout()
        {
            Logined = false;
            VerificationState = AccountVerificationState.SignedOut;
            SettingHelper.SetValue(SettingHelper.DOUYU_COOKIE, "");
            SetDouyuSiteCookie();
            OnAccountChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
