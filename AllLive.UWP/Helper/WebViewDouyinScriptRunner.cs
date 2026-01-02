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
            LogHelper.Log($"[WebViewRunner] GenerateABogusAsync 被调用", LogType.DEBUG);
            LogHelper.Log($"[WebViewRunner] queryString长度: {queryString?.Length ?? 0}", LogType.DEBUG);
            return ExecuteScriptAsync("getABogus", queryString ?? string.Empty, userAgent ?? string.Empty);
        }

        private Task<string> ExecuteScriptAsync(string functionName, string arg1, string arg2)
        {
            return ExecuteScriptInternalAsync(functionName, arg1, arg2, retryOnFailure: true);
        }

        private async Task<string> ExecuteScriptInternalAsync(string functionName, string arg1, string arg2, bool retryOnFailure)
        {
            LogHelper.Log($"[WebViewRunner] ExecuteScriptInternalAsync: {functionName}, retry={retryOnFailure}", LogType.DEBUG);
            try
            {
                await EnsureInitializedAsync().ConfigureAwait(false);
                LogHelper.Log($"[WebViewRunner] WebView已初始化, _initialized={_initialized}", LogType.DEBUG);

                var escapedArg1 = EscapeJsString(arg1);
                var escapedArg2 = EscapeJsString(arg2);
                var script = $"(function(){{ try {{ return {functionName}('{escapedArg1}','{escapedArg2}'); }} catch(e) {{ return \"ERROR:\" + e.message; }} }})()";

                return await RunOnUiThreadAsync(async () =>
                {
                    LogHelper.Log($"[WebViewRunner] 执行JS: {functionName}(...)", LogType.DEBUG);
                    
                    if (_webView == null)
                    {
                        LogHelper.Log("[WebViewRunner] 错误: WebView为null", LogType.ERROR);
                        throw new InvalidOperationException("WebView is null");
                    }
                    
                    var result = await _webView.InvokeScriptAsync("eval", new[] { script });
                    LogHelper.Log($"[WebViewRunner] JS执行结果: {Truncate(result ?? "null", 100)}", LogType.DEBUG);
                    
                    if (!string.IsNullOrEmpty(result) && result.StartsWith("ERROR:"))
                    {
                        LogHelper.Log($"[WebViewRunner] JS执行错误: {result}", LogType.ERROR);
                        throw new InvalidOperationException(result);
                    }
                    
                    return result ?? string.Empty;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"[WebViewRunner] {functionName} 异常: {ex.Message}", LogType.ERROR, ex);
                
                if (retryOnFailure)
                {
                    LogHelper.Log($"[WebViewRunner] 重试中...", LogType.DEBUG);
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
                LogHelper.Log("[WebViewRunner] ========== 初始化开始 ==========", LogType.DEBUG);
                var scripts = await LoadScriptsAsync().ConfigureAwait(false);
                LogHelper.Log($"[WebViewRunner] 脚本加载完成, 长度={scripts?.Length ?? 0}", LogType.DEBUG);

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
                    LogHelper.Log("[WebViewRunner] 注入脚本中...", LogType.DEBUG);
                    var evalResult = await _webView.InvokeScriptAsync("eval", new[] { _scripts });
                    LogHelper.Log($"[WebViewRunner] 脚本注入结果: {Truncate(evalResult ?? "null", 50)}", LogType.DEBUG);
                    
                    // 验证函数是否存在
                    var checkABogus = await _webView.InvokeScriptAsync("eval", new[] { "typeof getABogus" });
                    LogHelper.Log($"[WebViewRunner] typeof getABogus = {checkABogus}", LogType.DEBUG);
                    
                    var checkSignature = await _webView.InvokeScriptAsync("eval", new[] { "typeof getMSSDKSignature" });
                    LogHelper.Log($"[WebViewRunner] typeof getMSSDKSignature = {checkSignature}", LogType.DEBUG);
                    
                    if (checkABogus != "function")
                    {
                        LogHelper.Log($"[WebViewRunner] 警告: getABogus 不是函数!", LogType.ERROR);
                    }
                    
                    LogHelper.Log("[WebViewRunner] ========== 初始化完成 ==========", LogType.DEBUG);
                    _initialized = true;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"[WebViewRunner] 初始化异常: {ex}", LogType.ERROR, ex);
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
                LogHelper.Log($"WebViewDouyinScriptRunner NavigateToBlank completed: Success={args.IsSuccess}", LogType.DEBUG);
                tcs.TrySetResult(true);
            };

            _webView.NavigationCompleted += handler;
            // 添加一个简单的 window 模拟，因为 a_bogus.js 可能需要
            _webView.NavigateToString(@"<html>
<head>
<meta charset='utf-8'>
<script>
if (typeof window === 'undefined') { var window = this; }
if (typeof navigator === 'undefined') { var navigator = { userAgent: '' }; }
if (typeof document === 'undefined') { var document = { cookie: '' }; }
</script>
</head>
<body></body>
</html>");

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
