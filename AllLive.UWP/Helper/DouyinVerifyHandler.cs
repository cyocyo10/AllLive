using AllLive.Core.Interface;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace AllLive.UWP.Helper
{
    /// <summary>
    /// 抖音验证处理器 - 使用 WebView 让用户完成验证
    /// </summary>
    public class DouyinVerifyHandler : IDouyinVerifyHandler
    {
        public async Task<string> VerifyAsync(string url)
        {
            var tcs = new TaskCompletionSource<string>();
            
            await Windows.ApplicationModel.Core.CoreApplication.MainView.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    var dialog = new DouyinVerifyDialog(url);
                    var result = await dialog.ShowAsync();
                    
                    if (result == ContentDialogResult.Primary)
                    {
                        tcs.TrySetResult(dialog.VerifiedCookie);
                    }
                    else
                    {
                        tcs.TrySetResult(null);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Log($"DouyinVerifyHandler error: {ex.Message}", LogType.ERROR, ex);
                    tcs.TrySetResult(null);
                }
            });
            
            return await tcs.Task;
        }
    }
}
