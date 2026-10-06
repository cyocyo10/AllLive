using AllLive.UWP.Helper;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace AllLive.UWP.Controls
{
    public sealed partial class BiliLoginDialog : ContentDialog
    {
        private readonly LoginAttempt attempt = new LoginAttempt();
        private bool open;
        private bool loading;
        public BiliLoginDialog()
        {
            InitializeComponent();
            Loaded += (s, e) => { open = true; LoadQRCode(); };
            Unloaded += (s, e) => Cancel();
            Closing += (s, e) => Cancel();
            Closed += (s, e) => Cancel();
        }
        public void Cancel() { open = false; attempt.Cancel(); }
        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            LoadQRCode();
        }
        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) { Cancel(); }
        private void imgQR_Tapped(object sender, TappedRoutedEventArgs e) { LoadQRCode(); }
        private async void LoadQRCode()
        {
            if (!open || loading) return;
            var token = attempt.Begin();
            loading = true;
            IsPrimaryButtonEnabled = false;
            loaddingImage.Visibility = Visibility.Visible;
            txtStatus.Text = "正在获取二维码…";
            imgQR.Source = null;
            try
            {
                var json = JObject.Parse(await LoginHttp.GetStringAsync("https://passport.bilibili.com/x/passport-login/web/qrcode/generate", null, token));
                token.ThrowIfCancellationRequested();
                var key = (string)json["data"]?["qrcode_key"];
                var url = (string)json["data"]?["url"];
                if ((int?)json["code"] != 0 || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(url))
                    throw new InvalidOperationException("Invalid QR response");
                var writer = new ZXing.BarcodeWriter
                {
                    Format = ZXing.BarcodeFormat.QR_CODE,
                    Options = new ZXing.Common.EncodingOptions { Width = 260, Height = 260, Margin = 4 }
                };
                imgQR.Source = writer.Write(url);
                txtStatus.Text = "等待扫描";
                _ = PollAsync(key, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!token.IsCancellationRequested) txtStatus.Text = "二维码加载失败，请检查网络后刷新"; }
            finally
            {
                loading = false;
                if (!token.IsCancellationRequested)
                {
                    IsPrimaryButtonEnabled = true;
                    loaddingImage.Visibility = Visibility.Collapsed;
                }
            }
        }
        private async Task PollAsync(string key, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                    using (var response = await LoginHttp.GetAsync("https://passport.bilibili.com/x/passport-login/web/qrcode/poll?qrcode_key=" + Uri.EscapeDataString(key), null, token))
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        token.ThrowIfCancellationRequested();
                        var json = JObject.Parse(body);
                        if ((int?)json["code"] != 0) throw new InvalidOperationException("QR status unavailable");
                        var code = (int?)json["data"]?["code"];
                        if (code == 0)
                        {
                            txtStatus.Text = "扫码已确认，正在验证账号…";
                            System.Collections.Generic.IEnumerable<string> values;
                            if (!response.Headers.TryGetValues("Set-Cookie", out values)) throw new InvalidOperationException("Missing session");
                            var cookie = string.Join(";", values.Select(x => x.Split(';')[0]));
                            if (await BiliAccount.Instance.TryLoginAsync(cookie, token))
                            {
                                token.ThrowIfCancellationRequested();
                                txtStatus.Text = BiliAccount.Instance.StatusMessage;
                                Utils.ShowMessageToast("哔哩哔哩登录成功");
                                Hide();
                                Cancel();
                            }
                            else if (!token.IsCancellationRequested) txtStatus.Text = BiliAccount.Instance.StatusMessage;
                            return;
                        }
                        if (code == 86038) { txtStatus.Text = "二维码已过期，请刷新"; return; }
                        if (code == 86090) txtStatus.Text = "已扫描，请在哔哩哔哩App确认登录";
                        else if (code == 86101) txtStatus.Text = "等待扫描";
                        else throw new InvalidOperationException("Unknown QR status");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!token.IsCancellationRequested) txtStatus.Text = "登录检测失败，请检查网络后刷新"; }
        }
    }
}
