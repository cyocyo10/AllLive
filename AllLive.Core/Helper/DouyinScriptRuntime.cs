using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using System.Text;

namespace AllLive.Core.Helper
{
    public interface IDouyinScriptRunner
    {
        Task<string> EvaluateSignatureAsync(string msStub, string userAgent);

        Task<string> GenerateABogusAsync(string queryString, string userAgent);
    }

    public static class DouyinScriptRuntime
    {
        private static IDouyinScriptRunner _current;

        public static IDouyinScriptRunner Current
        {
            get => _current;
            set => _current = value ?? throw new ArgumentNullException(nameof(value));
        }

        static DouyinScriptRuntime()
        {
#if WINDOWS_UWP
            _current = new NullDouyinScriptRunner();
#else
            _current = new QuickJSDouyinScriptRunner();
#endif
        }
    }

    internal sealed class NullDouyinScriptRunner : IDouyinScriptRunner
    {
        public Task<string> EvaluateSignatureAsync(string msStub, string userAgent) => Task.FromResult(string.Empty);

        public Task<string> GenerateABogusAsync(string queryString, string userAgent) => Task.FromResult(string.Empty);
    }

#if !WINDOWS_UWP
    internal sealed class QuickJSDouyinScriptRunner : IDouyinScriptRunner
    {
        private static readonly Lazy<string> MsSdkScript = new Lazy<string>(() => LoadEmbeddedScript("webmssdk.js"));
        private static readonly Lazy<string> ABogusScript = new Lazy<string>(() => LoadEmbeddedScript("a_bogus.js"));

        public Task<string> EvaluateSignatureAsync(string msStub, string userAgent)
        {
            try
            {
                using (var runtime = new QuickJS.QuickJSRuntime())
                using (var context = runtime.CreateContext())
                {
                    context.Eval(MsSdkScript.Value, "webmssdk.js", QuickJS.JSEvalFlags.Global);
                    var escapedStub = EscapeJavaScriptString(msStub);
                    var escapedUserAgent = EscapeJavaScriptString(userAgent);
                    var result = context.Eval(
                        $"getMSSDKSignature('{escapedStub}','{escapedUserAgent}')",
                        "signature.js",
                        QuickJS.JSEvalFlags.Global);
                    return Task.FromResult(result?.ToString() ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"QuickJSDouyinScriptRunner.EvaluateSignature error: {ex}");
                return Task.FromResult(string.Empty);
            }
        }

        public Task<string> GenerateABogusAsync(string queryString, string userAgent)
        {
            try
            {
                using (var runtime = new QuickJS.QuickJSRuntime())
                using (var context = runtime.CreateContext())
                {
                    context.Eval(ABogusScript.Value, "a_bogus.js", QuickJS.JSEvalFlags.Global);
                    var escapedParams = EscapeJavaScriptString(queryString ?? string.Empty);
                    var escapedUserAgent = EscapeJavaScriptString(userAgent ?? string.Empty);
                    var result = context.Eval(
                        $"getABogus('{escapedParams}','{escapedUserAgent}')",
                        "a_bogus.js",
                        QuickJS.JSEvalFlags.Global);
                    return Task.FromResult(result?.ToString() ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"QuickJSDouyinScriptRunner.GenerateABogus error: {ex}");
                return Task.FromResult(string.Empty);
            }
        }

        private static string LoadEmbeddedScript(string suffix)
        {
            var assembly = typeof(QuickJSDouyinScriptRunner).GetTypeInfo().Assembly;
            var resourceName = FindResourceName(assembly, suffix);
            if (string.IsNullOrEmpty(resourceName))
            {
                throw new FileNotFoundException($"{suffix} resource not found.");
            }

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream ?? throw new InvalidOperationException($"Failed to open {suffix} resource."), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static string FindResourceName(Assembly assembly, string suffix)
        {
            var resources = assembly.GetManifestResourceNames();
            foreach (var resource in resources)
            {
                if (resource.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return resource;
                }
            }

            return null;
        }

        private static string EscapeJavaScriptString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("'", "\\'");
        }
    }
#endif
}



