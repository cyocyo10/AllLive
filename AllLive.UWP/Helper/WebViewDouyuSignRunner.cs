using AllLive.Core.Helper;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace AllLive.UWP.Helper
{
    public sealed class WebViewDouyuSignRunner : IDouyuSignRunner
    {
        private readonly CoreDispatcher _dispatcher;
        private readonly object _initSync = new object();

        private bool _initialized;
        private Task _initializationTask;
        private WebView _webView;
        private Popup _hostPopup;
        private Grid _hostContainer;

        public WebViewDouyuSignRunner(CoreDispatcher dispatcher = null)
        {
            _dispatcher = dispatcher ?? CoreApplication.MainView?.Dispatcher ?? throw new InvalidOperationException("UI dispatcher is not available.");
        }

        public Task<string> GenerateSignAsync(string html, string rid)
        {
            html = html ?? string.Empty;
            rid = rid ?? string.Empty;
            return ExecuteInternalAsync(html, rid, retryOnFailure: true);
        }

        private async Task<string> ExecuteInternalAsync(string html, string rid, bool retryOnFailure)
        {
            try
            {
                await EnsureInitializedAsync().ConfigureAwait(false);

                if (string.IsNullOrEmpty(html) || string.IsNullOrEmpty(rid))
                {
                    return string.Empty;
                }

                var did = "10000000000000000000000000001501";
                var time = AllLive.Core.Helper.Utils.GetTimestamp();

                return await RunOnUiThreadAsync(async () =>
                {
                    LogHelper.Log("WebViewDouyuSignRunner eval html", LogType.DEBUG);
                    await _webView.InvokeScriptAsync("eval", new[] { html });
                    var jsCode = await _webView.InvokeScriptAsync("eval", new[] { "ub98484234()" });
                    if (string.IsNullOrEmpty(jsCode))
                    {
                        return string.Empty;
                    }

                    var v = Regex.Match(jsCode, @"v=(\d+)").Groups[1].Value;
                    var rb = AllLive.Core.Helper.Utils.ToMD5(rid + did + time + v);

                    var jsCode2 = Regex.Replace(jsCode, @"return rt;}\);?", "return rt;}");
                    jsCode2 = Regex.Replace(jsCode2, @"\(function \(", "function sign(");
                    jsCode2 = Regex.Replace(jsCode2, @"CryptoJS\.MD5\(cb\)\.toString\(\)", $@"""{rb}""");
                    await _webView.InvokeScriptAsync("eval", new[] { jsCode2 });

                    var escapedRid = EscapeJsString(rid);
                    var script = $"(function(){{ try {{ return sign('{escapedRid}','{did}','{time}'); }} catch(e) {{ return \"ERROR:\" + e.message; }} }})()";
                    var result = await _webView.InvokeScriptAsync("eval", new[] { script });
                    LogHelper.Log("WebViewDouyuSignRunner result: " + (result ?? string.Empty), LogType.DEBUG);
                    return result ?? string.Empty;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (retryOnFailure && IsRecoverableWebViewException(ex))
                {
                    LogHelper.Log("WebViewDouyuSignRunner recoverable error, resetting WebView", LogType.DEBUG);
                    await ResetWebViewAsync().ConfigureAwait(false);
                    return await ExecuteInternalAsync(html, rid, retryOnFailure: false).ConfigureAwait(false);
                }

                LogHelper.Log($"WebViewDouyuSignRunner error: {ex}", LogType.ERROR, ex);
                return string.Empty;
            }
        }

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
            }).ConfigureAwait(false);

            _initialized = true;
        }

        private async Task ResetWebViewAsync()
        {
            await RunOnUiThreadAsync(() =>
            {
                if (_webView != null)
                {
                    _hostContainer.Children.Remove(_webView);
                    _webView = null;
                }

                _initialized = false;
                _initializationTask = null;
            }).ConfigureAwait(false);

            await EnsureInitializedAsync().ConfigureAwait(false);
        }

        private Task NavigateToBlankAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            TypedEventHandler<WebView, WebViewNavigationCompletedEventArgs> handler = null;
            handler = (s, e) =>
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

        private static string EscapeJsString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
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
                return hr == unchecked((int)0x80020101) || hr == unchecked((int)0x8001010E);
            }

            return IsRecoverableWebViewException(ex.InnerException);
        }

        private Task RunOnUiThreadAsync(Action action)
        {
            if (_dispatcher == null)
            {
                throw new InvalidOperationException("UI dispatcher is not available.");
            }

            if (_dispatcher.HasThreadAccess)
            {
                action();
                return Task.CompletedTask;
            }

            return _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action()).AsTask();
        }

        private Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> func)
        {
            if (_dispatcher == null)
            {
                throw new InvalidOperationException("UI dispatcher is not available.");
            }

            if (_dispatcher.HasThreadAccess)
            {
                return func();
            }

            var tcs = new TaskCompletionSource<T>();
            _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
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

            return tcs.Task;
        }
    }
}
