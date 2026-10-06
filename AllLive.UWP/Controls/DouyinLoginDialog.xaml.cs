using AllLive.UWP.Helper;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace AllLive.UWP.Controls
{
    public sealed partial class DouyinLoginDialog : ContentDialog
    {
        private readonly WebLoginSession session;
        private bool saving;
        private Task cleanup;
        private bool cleanupFailureReported;
        public bool SessionSaved => session.Saved;
        // Compatibility for existing callers; this means saved, not server-verified.
        public bool LoginSuccess => SessionSaved;
        public DouyinLoginDialog()
        {
            InitializeComponent();
            session = new WebLoginSession(webView, "douyin.com", "https://www.douyin.com/passport/general/login_guiding_strategy/?aid=6383", (message, canSave) =>
            {
                txtStatus.Text = message;
                IsPrimaryButtonEnabled = canSave && !saving;
            });
            Loaded += async (s, e) => await session.StartAsync();
            Closing += ContentDialog_Closing;
            Unloaded += (s, e) => { _ = CleanupAsync(); };
        }
        public void Cancel() { _ = CleanupAsync(); Hide(); }
        private async Task CleanupAsync()
        {
            if (cleanup == null) cleanup = session.CloseAsync();
            try { await cleanup; }
            catch (Exception)
            {
                if (cleanupFailureReported) return;
                cleanupFailureReported = true;
                Utils.ShowMessageToast("抖音网页会话清理失败，下次登录前将重试清理");
            }
        }
        private async void ContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            var deferral = args.GetDeferral();
            try { await CleanupAsync(); }
            finally { deferral.Complete(); }
        }
        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            if (saving) return;
            saving = true;
            IsPrimaryButtonEnabled = false;
            try
            {
                if (await session.SaveAsync(DouyinAccount.Instance.SetCookie)) Hide();
            }
            catch (Exception) { txtStatus.Text = "会话保存失败，请重试"; }
            finally { saving = false; }
        }
        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) { _ = CleanupAsync(); }
        private void Retry_Click(object sender, RoutedEventArgs e) { session.Reload(); }
    }
}
