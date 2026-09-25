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

        public bool Logined { get; set; } = false;

        public string Cookie
        {
            get
            {
                return SettingHelper.GetValue<string>(SettingHelper.DOUYU_COOKIE, "");
            }
        }

        public void InitLoginInfo()
        {
            Logined = !string.IsNullOrEmpty(Cookie);
            if (Logined)
            {
                SetDouyuSiteCookie();
            }
        }

        public void SetCookie(string cookie)
        {
            SettingHelper.SetValue(SettingHelper.DOUYU_COOKIE, cookie);
            Logined = !string.IsNullOrEmpty(cookie);
            SetDouyuSiteCookie();
            OnAccountChanged?.Invoke(this, null);
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
            SettingHelper.SetValue(SettingHelper.DOUYU_COOKIE, "");
            SetDouyuSiteCookie();
            OnAccountChanged?.Invoke(this, null);
        }
    }
}
