using AllLive.UWP.Helper;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace AllLive.UWP.Controls
{
    public sealed partial class DouyuLoginDialog : UserControl
    {
        private const double DialogWidth = 520;
        private const double DialogHeight = 640;
        private readonly WebLoginSession session;
        private TaskCompletionSource<bool> completion;
        private bool closing;
        private bool saving;
        private bool _dragging;
        private double _dragOffsetX;
        private double _dragOffsetY;

        public DouyuLoginDialog()
        {
            InitializeComponent();
            session = new WebLoginSession(webView, "douyu.com", "https://passport.douyu.com/", (message, canSave) =>
            {
                txtStatus.Text = message;
                BtnDone.IsEnabled = canSave && !saving;
            });
            // The Popup child, not this unparented UserControl, joins the visual tree.
            webView.Loaded += async (s, e) => await session.StartAsync();
            loginPopup.Closed += (s, e) => Cancel();
            Unloaded += (s, e) => Cancel();
        }
        public Task<bool> ShowAsync()
        {
            if (completion != null) return completion.Task;
            completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var bounds = Window.Current.Bounds;
            loginPopup.HorizontalOffset = Math.Max(0, (bounds.Width - DialogWidth) / 2);
            loginPopup.VerticalOffset = Math.Max(0, (bounds.Height - DialogHeight) / 2);
            loginPopup.IsOpen = true;
            return completion.Task;
        }
        public void Cancel() { _ = CloseAsync(false); }
        private async Task CloseAsync(bool saved)
        {
            if (closing) return;
            closing = true;
            BtnDone.IsEnabled = false;
            try { await session.CloseAsync(); }
            catch (Exception) { Utils.ShowMessageToast("斗鱼网页会话清理失败，下次登录前将重试清理"); }
            finally
            {
                // Resolve before Closed fires: a successful save must not become cancel.
                completion?.TrySetResult(saved);
                loginPopup.IsOpen = false;
            }
        }
        private void TitleBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _dragging = true;
            var point = e.GetCurrentPoint(null).Position;
            _dragOffsetX = point.X - loginPopup.HorizontalOffset;
            _dragOffsetY = point.Y - loginPopup.VerticalOffset;
            TitleBar.CapturePointer(e.Pointer);
        }

        private void TitleBar_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }
            var point = e.GetCurrentPoint(null).Position;
            var bounds = Window.Current.Bounds;
            // 限制在窗口范围内,防止拖丢
            var x = Math.Max(-(DialogWidth - 60), Math.Min(point.X - _dragOffsetX, bounds.Width - 60));
            var y = Math.Max(0, Math.Min(point.Y - _dragOffsetY, bounds.Height - 48));
            loginPopup.HorizontalOffset = x;
            loginPopup.VerticalOffset = y;
        }

        private void TitleBar_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _dragging = false;
            TitleBar.ReleasePointerCapture(e.Pointer);
        }

        private async void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            if (saving || closing) return;
            saving = true;
            BtnDone.IsEnabled = false;
            try
            {
                if (await session.SaveAsync(DouyuAccount.Instance.SetCookie)) await CloseAsync(true);
            }
            catch (Exception) { txtStatus.Text = "会话保存失败，请重试"; }
            finally { saving = false; }
        }
        private void BtnRetry_Click(object sender, RoutedEventArgs e) { session.Reload(); }
        private void BtnCancel_Click(object sender, RoutedEventArgs e) { Cancel(); }
    }
}
