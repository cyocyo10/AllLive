
using AllLive.UWP.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AllLive.UWP
{
    /// <summary>
    /// �ṩ�ض���Ӧ�ó������Ϊ���Բ���Ĭ�ϵ�Ӧ�ó����ࡣ
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// ��ʼ����һʵ��Ӧ�ó����������ִ�еĴ�������ĵ�һ�У�
        /// ��ִ�У��߼��ϵ�ͬ�� main() �� WinMain()��
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            if (Utils.IsXbox && SettingHelper.GetValue<int>(SettingHelper.XBOX_MODE, 0) == 0)
            {
                this.RequiresPointerMode = Windows.UI.Xaml.ApplicationRequiresPointerMode.WhenRequested;
            }

            App.Current.UnhandledException += App_UnhandledException;
            this.Suspending += OnSuspending;
        }

        private void RegisterExceptionHandlingSynchronizationContext()
        {
            ExceptionHandlingSynchronizationContext
                .Register()
                .UnhandledException += SynchronizationContext_UnhandledException;
        }

        private void SynchronizationContext_UnhandledException(object sender, AysncUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            try
            {
                LogHelper.Log("�������г��ִ���", LogType.ERROR, e.Exception);
                Utils.ShowMessageToast("�������һ�������Ѽ�¼");
            }
            catch (Exception)
            {
            }
        }

        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            try
            {
                LogHelper.Log("�������г��ִ���", LogType.ERROR, e.Exception);
                Utils.ShowMessageToast("�������һ�������Ѽ�¼");
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// ��Ӧ�ó����������û��������ʱ���е��á�
        /// �������Ӧ�ó����Դ��ض��ļ��������ʹ�á�
        /// </summary>
        /// <param name="e">�й��������͹��̵���ϸ��Ϣ��</param>
        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {
            //��ʼ�����ݿ�
            TraceRedirector.EnsureInitialized();
            await DatabaseHelper.InitializeDatabase();
            //��ʼ����ĻDPI
            NSDanmaku.Controls.Danmaku.InitDanmakuDpi();
            if (Utils.IsXbox)
            {
                //bool result = Windows.UI.ViewManagement.ApplicationViewScaling.TrySetDisableLayoutScaling(true);
                Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);
                App.Current.Resources["GridViewDesiredWidth"] = 200.0;
                App.Current.Resources["GridViewItemHeight"] = 148.0;
            }

            Frame rootFrame = Window.Current.Content as Frame;

            // ��Ҫ�ڴ����Ѱ�������ʱ�ظ�Ӧ�ó����ʼ����
            // ֻ��ȷ�����ڴ��ڻ״̬
            if (rootFrame == null)
            {
                // ����Ҫ�䵱���������ĵĿ�ܣ�����������һҳ
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;
                rootFrame.Navigated += RootFrame_Navigated;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: ��֮ǰ�����Ӧ�ó������״̬
                }
                rootFrame.RequestedTheme = (ElementTheme)SettingHelper.GetValue<int>(SettingHelper.THEME, 0);
                // ����ܷ��ڵ�ǰ������
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // ��������ջ��δ��ԭʱ����������һҳ��
                    // ��ͨ����������Ϣ��Ϊ������������������
                    // ����
                    rootFrame.Navigate(typeof(BaseFramePage), e.Arguments);
                }
                // ȷ����ǰ���ڴ��ڻ״̬
                Window.Current.Activate();
            }

            SetTitleBar();
            InitializeDouyinRuntime();
            InitializeDouyuRuntime();
        }

        public static void SetTitleBar()
        {
            UISettings uISettings = new UISettings();
            var color = TitltBarButtonColor(uISettings);
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;

            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = color;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.BackgroundColor = Colors.Transparent;
            uISettings.ColorValuesChanged += new TypedEventHandler<UISettings, object>((setting, args) =>
            {
                titleBar.ButtonForegroundColor = TitltBarButtonColor(uISettings);
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.BackgroundColor = Colors.Transparent;
            });
        }

        private static Color TitltBarButtonColor(UISettings uISettings)
        {
            var settingTheme = SettingHelper.GetValue<int>(SettingHelper.THEME, 0);
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var color = uiSettings.GetColorValue(UIColorType.Foreground);
            if (settingTheme != 0)
            {
                color = settingTheme == 1 ? Colors.Black : Colors.White;

            }
            return color;
        }

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = (sender as Frame).CanGoBack ?
                AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        /// <summary>
        /// �������ض�ҳʧ��ʱ����
        /// </summary>
        ///<param name="sender">����ʧ�ܵĿ��</param>
        ///<param name="e">�йص���ʧ�ܵ���ϸ��Ϣ</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// �ڽ�Ҫ����Ӧ�ó���ִ��ʱ���á�  �ڲ�֪��Ӧ�ó���
        /// ����֪��Ӧ�ó���ᱻ��ֹ���ǻ�ָ���
        /// �����ڴ����ݱ��ֲ��䡣
        /// </summary>
        /// <param name="sender">����������Դ��</param>
        /// <param name="e">�йع����������ϸ��Ϣ��</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: ����Ӧ�ó���״̬��ֹͣ�κκ�̨�
            deferral.Complete();
        }

        private void InitializeDouyinRuntime()
        {
            try
            {
                var dispatcher = Window.Current?.Dispatcher ?? CoreApplication.MainView?.Dispatcher;
                AllLive.Core.Helper.DouyinScriptRuntime.Current = new LoggingDouyinScriptRunner(new WebViewDouyinScriptRunner(dispatcher));
                
                // 设置抖音验证处理器
                AllLive.Core.Douyin.VerifyHandler = new DouyinVerifyHandler();
            }
            catch (Exception ex)
            {
                LogHelper.Log("初始化 DouyinScriptRuntime 失败", LogType.ERROR, ex);
            }
        }

        private void InitializeDouyuRuntime()
        {
            try
            {
                var dispatcher = Window.Current?.Dispatcher ?? CoreApplication.MainView?.Dispatcher;
                AllLive.Core.Helper.DouyuSignRuntime.Current = new WebViewDouyuSignRunner(dispatcher);
            }
            catch (Exception ex)
            {
                LogHelper.Log("初始化 DouyuSignRuntime 失败", LogType.ERROR, ex);
            }
        }
    }
}
