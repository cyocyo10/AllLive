using AllLive.UWP.Helper;
using AllLive.UWP.ViewModels;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace AllLive.UWP.Views
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        readonly SettingVM settingVM;
        private bool pageLoaded;
        private bool accountBusy;
        private Action cancelLogin;
        public SettingsPage()
        {
            settingVM = new SettingVM();
            this.InitializeComponent();
            if (Utils.IsXbox)
            {
                SettingsPaneDiaplsyMode.Visibility = Visibility.Collapsed;
                SettingsMouseClosePage.Visibility = Visibility.Collapsed;
                SettingsFontSize.Visibility = Visibility.Collapsed;
                SettingsAutoClean.Visibility = Visibility.Collapsed;
                SettingsXboxMode.Visibility = Visibility.Visible;
                SettingsNewWindow.Visibility = Visibility.Collapsed;
            }
            Loaded += SettingsPage_Loaded;
            LoadUI();
            
            // 页面卸载时取消事件订阅
            this.Unloaded += SettingsPage_Unloaded;
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            BiliAccount.Instance.OnAccountChanged -= Account_OnAccountChanged;
            DouyuAccount.Instance.OnAccountChanged -= Account_OnAccountChanged;
            DouyinAccount.Instance.OnAccountChanged -= Account_OnAccountChanged;
            pageLoaded = false;
            cancelLogin?.Invoke();
            cancelLogin = null;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (pageLoaded) return;
            pageLoaded = true;
            BiliAccount.Instance.OnAccountChanged += Account_OnAccountChanged;
            DouyuAccount.Instance.OnAccountChanged += Account_OnAccountChanged;
            DouyinAccount.Instance.OnAccountChanged += Account_OnAccountChanged;
            RefreshAccountUI();
        }

        private async void Account_OnAccountChanged(object sender, EventArgs e)
        {
            if (!pageLoaded) return;
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (pageLoaded) RefreshAccountUI();
            });
        }

        private void RefreshAccountUI()
        {
            txtBili.Text = BiliAccount.Instance.StatusMessage;
            txtDouyu.Text = DouyuAccount.Instance.StatusMessage;
            txtDouyin.Text = DouyinAccount.Instance.StatusMessage;
            BtnLoginBili.Content = BiliAccount.Instance.HasSavedSession ? "重新登录" : "立即登录";
            BtnLoginDouyu.Content = DouyuAccount.Instance.HasSavedSession ? "重新登录" : "立即登录";
            BtnLoginDouyin.Content = DouyinAccount.Instance.HasSavedSession ? "重新登录" : "立即登录";
            BtnLogoutBili.Visibility = BiliAccount.Instance.HasSavedSession ? Visibility.Visible : Visibility.Collapsed;
            BtnLogoutDouyu.Visibility = DouyuAccount.Instance.HasSavedSession ? Visibility.Visible : Visibility.Collapsed;
            BtnLogoutDouyin.Visibility = DouyinAccount.Instance.HasSavedSession ? Visibility.Visible : Visibility.Collapsed;
            BtnLoginBili.IsEnabled = BtnLoginDouyu.IsEnabled = BtnLoginDouyin.IsEnabled = !accountBusy;
            BtnLogoutBili.IsEnabled = BtnLogoutDouyu.IsEnabled = BtnLogoutDouyin.IsEnabled = !accountBusy;
        }

        private void LoadUI()
        {
            //主题
            cbTheme.SelectedIndex = SettingHelper.GetValue<int>(SettingHelper.THEME, 0);
            cbTheme.Loaded += new RoutedEventHandler((sender, e) =>
            {
                cbTheme.SelectionChanged += new SelectionChangedEventHandler((obj, args) =>
                {
                    SettingHelper.SetValue(SettingHelper.THEME, cbTheme.SelectedIndex);
                    Frame rootFrame = Window.Current.Content as Frame;
                    switch (cbTheme.SelectedIndex)
                    {
                        case 1:
                            rootFrame.RequestedTheme = ElementTheme.Light;
                            break;
                        case 2:
                            rootFrame.RequestedTheme = ElementTheme.Dark;
                            break;
                        default:
                            rootFrame.RequestedTheme = ElementTheme.Default;
                            break;
                    }
                    App.SetTitleBar();
                });
            });

            // xbox操作模式
            cbXboxMode.SelectedIndex = SettingHelper.GetValue<int>(SettingHelper.XBOX_MODE, 0);
            cbXboxMode.Loaded += new RoutedEventHandler((sender, e) =>
            {
                cbXboxMode.SelectionChanged += new SelectionChangedEventHandler((obj, args) =>
                {
                    SettingHelper.SetValue(SettingHelper.XBOX_MODE, cbXboxMode.SelectedIndex);
                    Utils.ShowMessageToast("重启应用生效");
                });
            });

            //导航栏显示模式
            cbPaneDisplayMode.SelectedIndex = SettingHelper.GetValue<int>(SettingHelper.PANE_DISPLAY_MODE, 0);
            cbPaneDisplayMode.Loaded += new RoutedEventHandler((sender, e) =>
            {
                cbPaneDisplayMode.SelectionChanged += new SelectionChangedEventHandler((obj, args) =>
                {
                    SettingHelper.SetValue(SettingHelper.PANE_DISPLAY_MODE, cbPaneDisplayMode.SelectedIndex);
                    MessageCenter.UpdatePanelDisplayMode();
                });
            });

            //鼠标侧键返回
            swMouseClosePage.IsOn = SettingHelper.GetValue<bool>(SettingHelper.MOUSE_BACK, true);
            swMouseClosePage.Loaded += new RoutedEventHandler((sender, e) =>
            {
                swMouseClosePage.Toggled += new RoutedEventHandler((obj, args) =>
                {
                    SettingHelper.SetValue(SettingHelper.MOUSE_BACK, swMouseClosePage.IsOn);
                });
            });
            //视频解码
            cbDecoder.SelectedIndex = SettingHelper.GetValue<int>(SettingHelper.VIDEO_DECODER, 0);
            cbDecoder.Loaded += new RoutedEventHandler((sender, e) =>
            {
                cbDecoder.SelectionChanged += new SelectionChangedEventHandler((obj, args) =>
                {
                    SettingHelper.SetValue(SettingHelper.VIDEO_DECODER, cbDecoder.SelectedIndex);
                });
            });

            numFontsize.Value = SettingHelper.GetValue<double>(SettingHelper.MESSAGE_FONTSIZE, 14.0);
            numFontsize.Loaded += new RoutedEventHandler((sender, e) =>
            {
                numFontsize.ValueChanged += new TypedEventHandler<NumberBox, NumberBoxValueChangedEventArgs>((obj, args) =>
                {
                    SettingHelper.SetValue(SettingHelper.MESSAGE_FONTSIZE, args.NewValue);
                });
            });

            //新窗口打开
            swNewWindow.IsOn = SettingHelper.GetValue<bool>(SettingHelper.NEW_WINDOW_LIVEROOM, false);
            swNewWindow.Loaded += new RoutedEventHandler((sender, e) =>
            {
                swNewWindow.Toggled += new RoutedEventHandler((obj, args) =>
                {
                    SettingHelper.SetValue(SettingHelper.NEW_WINDOW_LIVEROOM, swNewWindow.IsOn);
                });
            });
            //弹幕开关
            var state = SettingHelper.GetValue<bool>(SettingHelper.LiveDanmaku.SHOW, true);
            DanmuSettingState.IsOn = state;
            DanmuSettingState.Toggled += new RoutedEventHandler((e, args) =>
            {
                SettingHelper.SetValue(SettingHelper.LiveDanmaku.SHOW, DanmuSettingState.IsOn);
            });

            // 保留醒目留言
            var keepSC = SettingHelper.GetValue<bool>(SettingHelper.LiveDanmaku.KEEP_SUPER_CHAT, true);
            SettingKeepSC.IsOn = keepSC;
            SettingKeepSC.Toggled += new RoutedEventHandler((e, args) =>
            {
                SettingHelper.SetValue(SettingHelper.LiveDanmaku.KEEP_SUPER_CHAT, SettingKeepSC.IsOn);
            });

            //弹幕清理
            numCleanCount.Value = SettingHelper.GetValue<int>(SettingHelper.LiveDanmaku.DANMU_CLEAN_COUNT, 200);
            numCleanCount.Loaded += new RoutedEventHandler((sender, e) =>
            {
                numCleanCount.ValueChanged += new TypedEventHandler<NumberBox, NumberBoxValueChangedEventArgs>((obj, args) =>
                {
                    SettingHelper.SetValue(SettingHelper.LiveDanmaku.DANMU_CLEAN_COUNT, Convert.ToInt32(args.NewValue));
                });
            });
            //弹幕关键词
            LiveDanmuSettingListWords.ItemsSource = settingVM.ShieldWords;


            RefreshAccountUI();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            version.Text = $"{SystemInformation.Instance.ApplicationVersion.Major}.{SystemInformation.Instance.ApplicationVersion.Minor}.{SystemInformation.Instance.ApplicationVersion.Build}";
        }
        private void RemoveLiveDanmuWord_Click(object sender, RoutedEventArgs e)
        {
            var word = (sender as AppBarButton).DataContext as string;
            settingVM.ShieldWords.Remove(word);
            SettingHelper.SetValue(SettingHelper.LiveDanmaku.SHIELD_WORD, JsonConvert.SerializeObject(settingVM.ShieldWords));
        }

        private void LiveDanmuSettingTxtWord_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrEmpty(LiveDanmuSettingTxtWord.Text))
            {
                Utils.ShowMessageToast("关键字不能为空");
                return;
            }
            if (!settingVM.ShieldWords.Contains(LiveDanmuSettingTxtWord.Text))
            {
                settingVM.ShieldWords.Add(LiveDanmuSettingTxtWord.Text);
                SettingHelper.SetValue(SettingHelper.LiveDanmaku.SHIELD_WORD, JsonConvert.SerializeObject(settingVM.ShieldWords));
            }

            LiveDanmuSettingTxtWord.Text = "";
            SettingHelper.SetValue(SettingHelper.LiveDanmaku.SHIELD_WORD, JsonConvert.SerializeObject(settingVM.ShieldWords));
        }

        private async void BtnGithub_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/xiaoyaocz/AllLive"));
        }

        private async void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var logFolder = await storageFolder.CreateFolderAsync("log", Windows.Storage.CreationCollisionOption.OpenIfExists);
            await Launcher.LaunchFolderAsync(logFolder);
        }

        private async Task RunLoginAsync(Func<Task> show, Action cancel)
        {
            if (accountBusy) return;
            accountBusy = true;
            cancelLogin = cancel;
            RefreshAccountUI();
            try { await show(); }
            catch (Exception) { Utils.ShowMessageToast("无法打开登录窗口，请关闭其他弹窗后重试"); }
            finally
            {
                cancelLogin = null;
                accountBusy = false;
                if (pageLoaded) RefreshAccountUI();
            }
        }

        private async void BtnLoginBili_Click(object sender, RoutedEventArgs e)
        {
            if (accountBusy) return;
            var dialog = new AllLive.UWP.Controls.BiliLoginDialog();
            await RunLoginAsync(async () => { await dialog.ShowAsync(); }, () => { dialog.Cancel(); dialog.Hide(); });
        }
        private async void BtnLoginDouyin_Click(object sender, RoutedEventArgs e)
        {
            if (accountBusy) return;
            var dialog = new AllLive.UWP.Controls.DouyinLoginDialog();
            await RunLoginAsync(async () => { await dialog.ShowAsync(); }, dialog.Cancel);
        }
        private async void BtnLoginDouyu_Click(object sender, RoutedEventArgs e)
        {
            if (accountBusy) return;
            var dialog = new AllLive.UWP.Controls.DouyuLoginDialog();
            await RunLoginAsync(async () => { await dialog.ShowAsync(); }, dialog.Cancel);
        }
        private async Task LogoutAsync(Action logout, string domain, TextBlock status)
        {
            if (accountBusy) return;
            accountBusy = true;
            logout();
            RefreshAccountUI();
            status.Text = "正在清理本平台网页会话…";
            var cleanupView = new Microsoft.UI.Xaml.Controls.WebView2
            {
                Width = 1, Height = 1, Opacity = 0, IsHitTestVisible = false, IsTabStop = false
            };
            SettingsRoot.Children.Add(cleanupView);
            bool failed = false;
            try { await WebLoginSession.ClearCookiesAsync(cleanupView, domain); }
            catch (Exception) { failed = true; }
            finally
            {
                cleanupView.Close();
                SettingsRoot.Children.Remove(cleanupView);
                accountBusy = false;
                if (pageLoaded)
                {
                    RefreshAccountUI();
                    if (failed) status.Text = "已清除应用会话；网页会话清理失败，下次登录前将重试";
                }
            }
        }
        private async void BtnLogoutBili_Click(object sender, RoutedEventArgs e)
        {
            await LogoutAsync(BiliAccount.Instance.Logout, "bilibili.com", txtBili);
        }
        private async void BtnLogoutDouyu_Click(object sender, RoutedEventArgs e)
        {
            await LogoutAsync(DouyuAccount.Instance.Logout, "douyu.com", txtDouyu);
        }
        private async void BtnLogoutDouyin_Click(object sender, RoutedEventArgs e)
        {
            await LogoutAsync(DouyinAccount.Instance.Logout, "douyin.com", txtDouyin);
        }
    }
}
