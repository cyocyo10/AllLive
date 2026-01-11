
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
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
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
                LogHelper.Log("Unhandled exception in background", LogType.ERROR, e.Exception);
                Utils.ShowMessageToast("An error occurred, logged");
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
                LogHelper.Log("Unhandled exception in app", LogType.ERROR, e.Exception);
                Utils.ShowMessageToast("An error occurred, logged");
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Initialize database
            TraceRedirector.EnsureInitialized();
            await DatabaseHelper.InitializeDatabase();
            // Initialize DPI
            NSDanmaku.Controls.Danmaku.InitDanmakuDpi();
            if (Utils.IsXbox)
            {
                //bool result = Windows.UI.ViewManagement.ApplicationViewScaling.TrySetDisableLayoutScaling(true);
                Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);
                App.Current.Resources["GridViewDesiredWidth"] = 200.0;
                App.Current.Resources["GridViewItemHeight"] = 148.0;
            }

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;
                rootFrame.Navigated += RootFrame_Navigated;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: Load state from previously suspended application
                }
                rootFrame.RequestedTheme = (ElementTheme)SettingHelper.GetValue<int>(SettingHelper.THEME, 0);
                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(BaseFramePage), e.Arguments);
                }
                // Ensure the current window is active
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
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended. Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            // TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        private void InitializeDouyinRuntime()
        {
            try
            {
                var dispatcher = Window.Current?.Dispatcher ?? CoreApplication.MainView?.Dispatcher;
                AllLive.Core.Helper.DouyinScriptRuntime.Current = new LoggingDouyinScriptRunner(new WebViewDouyinScriptRunner(dispatcher));
            }
            catch (Exception ex)
            {
                LogHelper.Log("Failed to initialize DouyinScriptRuntime", LogType.ERROR, ex);
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
                LogHelper.Log("Failed to initialize DouyuSignRuntime", LogType.ERROR, ex);
            }
        }
    }
}
