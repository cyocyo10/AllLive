#pragma warning disable CS0067
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace System.Runtime.InteropServices.WindowsRuntime { }
namespace Windows.Foundation.Collections { }
namespace Windows.UI.Xaml.Controls.Primitives { }
namespace Windows.UI.Xaml.Data { }
namespace Windows.UI.Xaml.Media { }
namespace Windows.Foundation { public delegate void TypedEventHandler<T, U>(T sender, U args); }
namespace Windows.UI.Core
{
    public enum CoreDispatcherPriority { Normal }
    public class CoreDispatcher { public Task RunAsync(CoreDispatcherPriority priority, Action action) { action(); return Task.CompletedTask; } }
}
namespace Windows.UI.Xaml
{
    public enum ElementTheme { Light, Dark, Default }
    public sealed partial class Window { public object Content = new Controls.Frame(); }
}
namespace Windows.UI.Xaml.Navigation { public class NavigationEventArgs : EventArgs { } }
namespace Windows.UI.Xaml.Controls
{
    public partial class Control
    {
        public Windows.UI.Core.CoreDispatcher Dispatcher = new Windows.UI.Core.CoreDispatcher();
        public double Width, Height, Opacity;
        public bool IsHitTestVisible, IsTabStop;
        public object DataContext;
    }
    public class Page : Control { protected virtual void OnNavigatedTo(Navigation.NavigationEventArgs e) { } }
    public class Frame : Control { public ElementTheme RequestedTheme; }
    public delegate void SelectionChangedEventHandler(object sender, EventArgs args);
    public class ComboBox : Control { public int SelectedIndex; public event SelectionChangedEventHandler SelectionChanged; }
    public class ToggleSwitch : Control { public bool IsOn; public event RoutedEventHandler Toggled; }
    public class AppBarButton : Button { }
    public class AutoSuggestBox : Control { public string Text; }
    public class AutoSuggestBoxQuerySubmittedEventArgs : EventArgs { }
    public class ListView : Control { public object ItemsSource; }
    public class ItemsControl : Control { public object ItemsSource; }
}
namespace Microsoft.UI.Xaml.Controls
{
    public class NumberBox : Windows.UI.Xaml.Controls.Control
    {
        public double Value;
        public event Windows.Foundation.TypedEventHandler<NumberBox, NumberBoxValueChangedEventArgs> ValueChanged;
    }
    public class NumberBoxValueChangedEventArgs : EventArgs { public double NewValue; }
}
namespace Windows.System
{
    public static class Launcher
    {
        public static Task LaunchUriAsync(Uri url) { return Task.CompletedTask; }
        public static Task LaunchFolderAsync(Windows.Storage.StorageFolder folder) { return Task.CompletedTask; }
    }
}
namespace Windows.Storage
{
    public enum CreationCollisionOption { OpenIfExists }
    public class StorageFolder { public Task<StorageFolder> CreateFolderAsync(string name, CreationCollisionOption option) { return Task.FromResult(this); } }
    public class ApplicationData { public static ApplicationData Current = new ApplicationData(); public StorageFolder LocalFolder = new StorageFolder(); }
}
namespace Microsoft.Toolkit.Uwp.Helpers
{
    public class SystemInformation { public static SystemInformation Instance = new SystemInformation(); public Version ApplicationVersion = new Version(1, 0, 0); }
}
namespace AllLive.UWP { public static class App { public static void SetTitleBar() { } } }
namespace AllLive.UWP.Helper
{
    public static partial class Utils { public static bool IsXbox = false; }
    public static class MessageCenter { public static void UpdatePanelDisplayMode() { } }
    public static partial class SettingHelper
    {
        public const string THEME = "theme", XBOX_MODE = "xbox", PANE_DISPLAY_MODE = "pane", MOUSE_BACK = "mouse", VIDEO_DECODER = "decoder", MESSAGE_FONTSIZE = "font", NEW_WINDOW_LIVEROOM = "window";
        public static class LiveDanmaku { public const string SHOW = "show", KEEP_SUPER_CHAT = "sc", DANMU_CLEAN_COUNT = "clean", SHIELD_WORD = "shield"; }
    }
}

namespace AllLive.UWP.Views
{
    public sealed partial class SettingsPage
    {
        public Windows.UI.Xaml.Controls.Grid SettingsRoot = new Windows.UI.Xaml.Controls.Grid();
        public Windows.UI.Xaml.Controls.ComboBox cbTheme = new Windows.UI.Xaml.Controls.ComboBox();
        public Windows.UI.Xaml.Controls.Grid SettingsXboxMode = new Windows.UI.Xaml.Controls.Grid();
        public Windows.UI.Xaml.Controls.ComboBox cbXboxMode = new Windows.UI.Xaml.Controls.ComboBox();
        public Windows.UI.Xaml.Controls.Grid SettingsPaneDiaplsyMode = new Windows.UI.Xaml.Controls.Grid();
        public Windows.UI.Xaml.Controls.ComboBox cbPaneDisplayMode = new Windows.UI.Xaml.Controls.ComboBox();
        public Windows.UI.Xaml.Controls.Grid SettingsMouseClosePage = new Windows.UI.Xaml.Controls.Grid();
        public Windows.UI.Xaml.Controls.ToggleSwitch swMouseClosePage = new Windows.UI.Xaml.Controls.ToggleSwitch();
        public Windows.UI.Xaml.Controls.Grid SettingsNewWindow = new Windows.UI.Xaml.Controls.Grid();
        public Windows.UI.Xaml.Controls.ToggleSwitch swNewWindow = new Windows.UI.Xaml.Controls.ToggleSwitch();
        public Windows.UI.Xaml.Controls.ComboBox cbDecoder = new Windows.UI.Xaml.Controls.ComboBox();
        public Windows.UI.Xaml.Controls.TextBlock txtBili = new Windows.UI.Xaml.Controls.TextBlock();
        public Windows.UI.Xaml.Controls.Button BtnLoginBili = new Windows.UI.Xaml.Controls.Button();
        public Windows.UI.Xaml.Controls.Button BtnLogoutBili = new Windows.UI.Xaml.Controls.Button();
        public Windows.UI.Xaml.Controls.TextBlock txtDouyin = new Windows.UI.Xaml.Controls.TextBlock();
        public Windows.UI.Xaml.Controls.Button BtnLoginDouyin = new Windows.UI.Xaml.Controls.Button();
        public Windows.UI.Xaml.Controls.Button BtnLogoutDouyin = new Windows.UI.Xaml.Controls.Button();
        public Windows.UI.Xaml.Controls.TextBlock txtDouyu = new Windows.UI.Xaml.Controls.TextBlock();
        public Windows.UI.Xaml.Controls.Button BtnLoginDouyu = new Windows.UI.Xaml.Controls.Button();
        public Windows.UI.Xaml.Controls.Button BtnLogoutDouyu = new Windows.UI.Xaml.Controls.Button();
        public Windows.UI.Xaml.Controls.ToggleSwitch DanmuSettingState = new Windows.UI.Xaml.Controls.ToggleSwitch();
        public Windows.UI.Xaml.Controls.ToggleSwitch SettingKeepSC = new Windows.UI.Xaml.Controls.ToggleSwitch();
        public Windows.UI.Xaml.Controls.Grid SettingsFontSize = new Windows.UI.Xaml.Controls.Grid();
        public Microsoft.UI.Xaml.Controls.NumberBox numFontsize = new Microsoft.UI.Xaml.Controls.NumberBox();
        public Windows.UI.Xaml.Controls.Grid SettingsAutoClean = new Windows.UI.Xaml.Controls.Grid();
        public Microsoft.UI.Xaml.Controls.NumberBox numCleanCount = new Microsoft.UI.Xaml.Controls.NumberBox();
        public Windows.UI.Xaml.Controls.AutoSuggestBox LiveDanmuSettingTxtWord = new Windows.UI.Xaml.Controls.AutoSuggestBox();
        public Windows.UI.Xaml.Controls.ListView LiveDanmuSettingListWords = new Windows.UI.Xaml.Controls.ListView();
        public Windows.UI.Xaml.Documents.Run version = new Windows.UI.Xaml.Documents.Run();
        public Windows.UI.Xaml.Controls.Button BtnLog = new Windows.UI.Xaml.Controls.Button();
        public Windows.UI.Xaml.Controls.Button BtnGithub = new Windows.UI.Xaml.Controls.Button();
        private void InitializeComponent() { }
    }
}
namespace Windows.UI.Xaml.Documents { public class Run { public string Text; } }
