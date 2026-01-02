using AllLive.Core.Helper;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

namespace AllLive.UWP.Helper
{
    public sealed class WebViewDouyinScriptRunner : IDouyinScriptRunner
    {
        private readonly object _initSync = new object();
        private readonly CoreDispatcher _dispatcher;
        private bool _initialized;
        private WebView _webView;
        private Popup _hostPopup;
        private Grid _hostContainer;

        private string _scripts;

        public WebViewDouyinScriptRunner(CoreDispatcher dispatcher = null)
        {
            _dispatcher = dispatcher ?? CoreApplication.MainView?.Dispatcher ?? throw new InvalidOperationException("UI dispatcher is not available.");
        }

        public Task<string> EvaluateSignatureAsync(string msStub, string userAgent)
        {
            return ExecuteScriptAsync("getMSSDKSignature", msStub, userAgent);
        }

        public Task<string> GenerateABogusAsync(string queryString, string userAgent)
        {
            return ExecuteScriptAsync("getABogus", queryString ?? string.Empty, userAgent ?? string.Empty);
        }

        private Task<string> ExecuteScriptAsync(string functionName, string arg1, string arg2)
        {
            return ExecuteScriptInternalAsync(functionName, arg1, arg2, retryOnFailure: true);
        }

        private async Task<string> ExecuteScriptInternalAsync(string functionName, string arg1, string arg2, bool retryOnFailure)
        {
            try
            {
                await EnsureInitializedAsync().ConfigureAwait(false);

                var escapedArg1 = EscapeJsString(arg1);
                var escapedArg2 = EscapeJsString(arg2);
                var script = $"(function(){{ try {{ return {functionName}('{escapedArg1}','{escapedArg2}'); }} catch(e) {{ return \"ERROR:\" + e.message; }} }})()";

                return await RunOnUiThreadAsync(async () =>
                {
                    LogHelper.Log("WebViewDouyinScriptRunner exec: " + script, LogType.DEBUG);
                    
                    // 检查 WebView 状态
                    if (_webView == null)
                    {
                        LogHelper.Log("WebViewDouyinScriptRunner: WebView is null, resetting", LogType.DEBUG);
                        throw new InvalidOperationException("WebView is null");
                    }
                    
                    var result = await _webView.InvokeScriptAsync("eval", new[] { script });
                    LogHelper.Log("WebViewDouyinScriptRunner result: " + Truncate(result ?? string.Empty), LogType.DEBUG);
                    
                    // 检查是否返回错误
                    if (!string.IsNullOrEmpty(result) && result.StartsWith("ERROR:"))
                    {
                        LogHelper.Log("WebViewDouyinScriptRunner JS error: " + result, LogType.ERROR);
                        // JS 执行出错，重置 WebView
                        throw new InvalidOperationException(result);
                    }
                    
                    return result ?? string.Empty;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"WebViewDouyinScriptRunner.{functionName} error: {ex.Message}", LogType.ERROR, ex);
                
                if (retryOnFailure)
                {
                    LogHelper.Log($"WebViewDouyinScriptRunner.{functionName} retrying after reset", LogType.DEBUG);
                    await ResetWebViewAsync().ConfigureAwait(false);
                    return await ExecuteScriptInternalAsync(functionName, arg1, arg2, retryOnFailure: false).ConfigureAwait(false);
                }

                return string.Empty;
            }
        }

        private Task _initializationTask;

        private Task EnsureInitializedAsync()
        {
            if (_initialized)
            {
                return Task.CompletedTask;
            }

            lock (_initSync)
            {
                if (_initialized)
                {
                    return Task.CompletedTask;
                }

                if (_initializationTask == null)
                {
                    _initializationTask = InitializeAsync();
                }

                return _initializationTask;
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                var scripts = await LoadScriptsAsync().ConfigureAwait(false);

                await RunOnUiThreadAsync(async () =>
                {
                    EnsureHostContainer();

                    _webView = new WebView(WebViewExecutionMode.SeparateThread)
                    {
                        Visibility = Visibility.Collapsed,
                        Width = 1,
                        Height = 1,
                        IsHitTestVisible = false
                    };

                    _hostContainer.Children.Add(_webView);

                    await NavigateToBlankAsync();

                    _scripts = scripts;
                    await _webView.InvokeScriptAsync("eval", new[] { _scripts });
                    LogHelper.Log("WebViewDouyinScriptRunner initialized", LogType.DEBUG);
                    _initialized = true;
                }).ConfigureAwait(false);
            }
            finally
            {
                if (!_initialized)
                {
                    lock (_initSync)
                    {
                        _initializationTask = null;
                    }
                }
            }
        }

        private async Task ResetWebViewAsync()
        {
            lock (_initSync)
            {
                _initialized = false;
                _initializationTask = null;
            }

            await RunOnUiThreadAsync(() =>
            {
                if (_webView != null)
                {
                    if (_hostContainer != null)
                    {
                        _hostContainer.Children.Remove(_webView);
                    }
                    _webView = null;
                }
            });
        }

        private Task NavigateToBlankAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            TypedEventHandler<WebView, WebViewNavigationCompletedEventArgs> handler = null;
            handler = (sender, args) =>
            {
                _webView.NavigationCompleted -= handler;
                tcs.TrySetResult(true);
            };

            _webView.NavigationCompleted += handler;
            _webView.NavigateToString("<html><head><meta charset='utf-8'></head><body></body></html>");

            return tcs.Task;
        }

        private void EnsureHostContainer()
        {
            if (_hostPopup != null)
            {
                return;
            }

            _hostContainer = new Grid
            {
                Width = 1,
                Height = 1,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };

            _hostPopup = new Popup
            {
                Child = _hostContainer,
                IsHitTestVisible = false,
                IsOpen = true
            };
        }

        private async Task<string> LoadScriptsAsync()
        {
            var scripts = await LoggingDouyinScriptRunner.ReadScriptsAsync().ConfigureAwait(false);
            LogHelper.Log("WebViewDouyinScriptRunner scripts loaded", LogType.DEBUG);
            return scripts;
        }

        private static string EscapeJsString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private async Task RunOnUiThreadAsync(Action action)
        {
            if (_dispatcher == null)
            {
                throw new InvalidOperationException("UI dispatcher is not available.");
            }

            if (_dispatcher.HasThreadAccess)
            {
                action();
            }
            else
            {
                await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action()).AsTask().ConfigureAwait(false);
            }
        }

        private Task RunOnUiThreadAsync(Func<Task> func)
        {
            return RunOnUiThreadAsync<bool>(async () =>
            {
                await func();
                return true;
            });
        }

        private static string Truncate(string value, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }

        private static bool IsRecoverableWebViewException(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            if (ex is COMException comEx)
            {
                var hr = comEx.HResult;
                return hr == unchecked((int)0x80020101) // JS E script
                    || hr == unchecked((int)0x8001010E); // wrong thread
            }

            return IsRecoverableWebViewException(ex.InnerException);
        }

        private async Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> func)
        {
            if (_dispatcher == null)
            {
                throw new InvalidOperationException("UI dispatcher is not available.");
            }

            if (_dispatcher.HasThreadAccess)
            {
                return await func();
            }

            var tcs = new TaskCompletionSource<T>();
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    var result = await func();
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }).AsTask();

            return await tcs.Task;
        }
    }
}
