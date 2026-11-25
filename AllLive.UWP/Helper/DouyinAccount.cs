using AllLive.Core;
using AllLive.UWP.ViewModels;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage;

namespace AllLive.UWP.Helper
{
    public class DouyinAccount
    {
        private const string SessionFileName = "douyin-session.v1";
        private static readonly DouyinAccount instance = new DouyinAccount();
        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
        private Task initTask;

        public static DouyinAccount Instance => instance;
        public event EventHandler OnAccountChanged;
        public bool Logined { get; set; }
        public string Cookie { get; private set; } = "";

        public Task InitLoginInfo() => initTask ?? (initTask = LoadLoginInfoAsync());

        private async Task LoadLoginInfoAsync()
        {
            try
            {
                var item = await ApplicationData.Current.LocalCacheFolder.TryGetItemAsync(SessionFileName);
                if (item == null)
                {
                    ApplyCookie(SettingHelper.GetValue<string>(SettingHelper.DOUYIN_COOKIE, ""));
                    return;
                }

                var file = (StorageFile)item;
                var protectedBuffer = await FileIO.ReadBufferAsync(file);
                var buffer = await new DataProtectionProvider().UnprotectAsync(protectedBuffer);
                var content = CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, buffer);
                if (!content.StartsWith("1", StringComparison.Ordinal))
                    throw new InvalidOperationException("Invalid Douyin session file.");
                // An empty protected value represents logout and must not fall back to an old setting.
                ApplyCookie(content.Substring(1));
            }
            catch (Exception ex)
            {
                LogHelper.Log($"加载本地抖音会话失败 (0x{ex.HResult:X8})", LogType.ERROR);
            }
        }

        public async Task SetCookie(string cookie)
        {
            await InitLoginInfo();
            await writeLock.WaitAsync();
            try { await WriteCookieAsync(cookie); }
            finally { writeLock.Release(); }
        }

        private async Task WriteCookieAsync(string cookie)
        {
            var folder = ApplicationData.Current.LocalCacheFolder;
            var buffer = CryptographicBuffer.ConvertStringToBinary("1" + cookie, BinaryStringEncoding.Utf8);
            var protectedBuffer = await new DataProtectionProvider("LOCAL=user").ProtectAsync(buffer);
            var staged = await folder.CreateFileAsync(SessionFileName + ".tmp", CreationCollisionOption.ReplaceExisting);
            try
            {
                await FileIO.WriteBufferAsync(staged, protectedBuffer);
                await staged.RenameAsync(SessionFileName, NameCollisionOption.ReplaceExisting);
            }
            catch
            {
                try { await staged.DeleteAsync(StorageDeleteOption.PermanentDelete); }
                catch (Exception ex)
                {
                    LogHelper.Log($"清理抖音会话临时文件失败 (0x{ex.HResult:X8})", LogType.ERROR);
                }
                throw;
            }

            ApplyCookie(cookie);
            try
            {
                ApplicationData.Current.LocalSettings.Values.Remove(SettingHelper.DOUYIN_COOKIE);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"清理旧抖音会话设置失败 (0x{ex.HResult:X8})", LogType.ERROR);
            }

            try { OnAccountChanged?.Invoke(this, null); }
            catch (Exception ex)
            {
                LogHelper.Log($"刷新抖音登录状态失败 (0x{ex.HResult:X8})", LogType.ERROR);
            }
        }

        private void ApplyCookie(string cookie)
        {
            Cookie = cookie ?? "";
            Logined = !string.IsNullOrEmpty(Cookie);
            SetDouyinSiteCookie();
        }

        public void SetDouyinSiteCookie()
        {
            var site = MainVM.Sites.FirstOrDefault(x => x.SiteType == LiveSite.Douyin);
            if (site?.LiveSite is Douyin douyin)
                douyin.SearchCookie = Cookie;
        }

        public Task Logout() => SetCookie("");
    }
}
