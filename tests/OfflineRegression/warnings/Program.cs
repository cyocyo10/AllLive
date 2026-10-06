using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AllLive.Core;
using AllLive.Core.Danmaku;
using AllLive.Core.Helper;
using AllLive.UWP.Helper;
using AllLive.UWP.ViewModels;

internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly List<object> Results = new List<object>();
    private static int failures;

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private static async Task Test(string id, Func<Task> action)
    {
        string error = null;
        try { await action(); }
        catch (Exception ex) { error = ex.ToString(); failures++; }
        finally { LogHelper.Reset(); }
        Results.Add(new { id, status = error == null ? "PASS" : "FAIL", error });
        Console.WriteLine((error == null ? "PASS " : "FAIL ") + id);
        if (error != null) Console.WriteLine(error);
    }

    private static Task Test(string id, Action action) => Test(id, () => { action(); return Task.CompletedTask; });

    private static void Raise(object command)
    {
        var method = command.GetType().GetMethod("RaiseCanExecuteChanged");
        Check(method != null, "Command must expose an explicit state-change notification.");
        method.Invoke(command, null);
    }

    private static void CheckNotifications(ICommand command)
    {
        int first = 0, second = 0;
        int callerThread = Environment.CurrentManagedThreadId;
        EventHandler handler = (sender, args) =>
        {
            Check(ReferenceEquals(sender, command), "Notification sender changed.");
            Check(ReferenceEquals(args, EventArgs.Empty), "Notification args changed.");
            Check(Environment.CurrentManagedThreadId == callerThread, "Notification changed threads.");
            first++;
        };
        EventHandler other = (sender, args) => second++;
        Raise(command); // No subscribers is safe.
        command.CanExecuteChanged += handler;
        command.CanExecuteChanged += other;
        command.Execute(7);
        command.CanExecute(7);
        Check(first == 0 && second == 0, "Execute/CanExecute must not implicitly raise the event.");
        Raise(command);
        Check(first == 1 && second == 1, "Each subscriber must be called once.");
        command.CanExecuteChanged -= handler;
        Raise(command);
        Check(first == 1 && second == 2, "Unsubscription must remain effective.");
        command.CanExecuteChanged -= other;
        Raise(command);
        Check(second == 2, "Removed subscribers were retained.");
    }

    private static Dictionary<string, string> Headers(Douyin site, bool force = false)
    {
        object result = typeof(Douyin).GetMethod("GetRequestHeaders", PrivateInstance).Invoke(site, new object[] { force });
        // Both baseline and repaired private implementations run against the same assertions.
        if (result is Task<Dictionary<string, string>> task) return task.GetAwaiter().GetResult();
        return (Dictionary<string, string>)result;
    }

    private static string ReadResource(string suffix)
    {
        var assembly = typeof(DouyinSignHelper).Assembly;
        string name = assembly.GetManifestResourceNames().Single(x => x.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        using (var reader = new StreamReader(assembly.GetManifestResourceStream(name), Encoding.UTF8))
            return reader.ReadToEnd();
    }

    private sealed class ThrowingType : TypeDelegator
    {
        private readonly Exception failure;
        public ThrowingType(Exception failure) : base(typeof(int)) { this.failure = failure; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public override string ToString() { throw failure; }
    }

    private sealed class ThrowingUniPacket : Tup.UniPacket
    {
        private readonly Exception failure;
        public ThrowingUniPacket(Exception failure) { this.failure = failure; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public override void ReadFrom(Tup.Tars.TarsInputStream stream) { throw failure; }
    }

    private sealed class ThrowingMemoryStream : MemoryStream
    {
        private readonly Exception failure;
        public ThrowingMemoryStream(Exception failure) : base(new byte[] { 0x11, 0 }) { this.failure = failure; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public override int ReadByte() { throw failure; }
    }

    private static void CheckRethrow(Exception original, Action action, string origin)
    {
        try { action(); }
        catch (IOException ex)
        {
            Check(ReferenceEquals(original, ex), "Rethrow replaced the original exception.");
            Check(ex.StackTrace != null && ex.StackTrace.Contains(origin), "Rethrow lost its original stack frame: " + ex.StackTrace);
            return;
        }
        throw new InvalidOperationException("Expected the original I/O fixture failure.");
    }

    private static async Task<int> Main(string[] args)
    {
        await Test("WARN-001 command notification subscription lifecycle", () => CheckNotifications(new RelayCommand(() => { })));
        await Test("WARN-002 generic command notification subscription lifecycle", () => CheckNotifications(new RelayCommand<int>(_ => { })));
        await Test("WARN-003 command execution and legacy predicate behavior", () =>
        {
            int executions = 0, predicateCalls = 0;
            var command = new RelayCommand(() => executions++);
            Check(command.CanExecute(null), "Default command must remain executable.");
            command.Execute(null);
            var legacy = new RelayCommand(() => executions++, _ => predicateCalls++);
            Check(!legacy.CanExecute(null) && predicateCalls == 0, "Legacy Action<bool> constructor semantics changed.");
            legacy.Execute(null);
            Check(executions == 2, "Execute must keep invoking its delegate.");
        });
        await Test("WARN-004 generic predicate observes state and parameter", () =>
        {
            int limit = 2, value = 0;
            var command = new RelayCommand<int>(x => value = x, x => x > limit);
            Check(!command.CanExecute(1) && command.CanExecute(3), "Predicate input changed.");
            limit = 4;
            Check(!command.CanExecute(3), "Predicate state was cached.");
            command.Execute(7);
            Check(value == 7, "Execution parameter changed.");
            Throws<InvalidCastException>(() => command.CanExecute("wrong type"));
            Throws<InvalidCastException>(() => command.Execute("wrong type"));
        });
        await Test("WARN-005 null command validation remains synchronous", () =>
        {
            Throws<ArgumentException>(() => new RelayCommand(null));
            Throws<ArgumentException>(() => new RelayCommand<int>(null));
        });
        await Test("WARN-006 headers retain defaults and copy isolation", () =>
        {
            var site = new Douyin { SearchCookie = "fixture-private-search-session" };
            var first = Headers(site);
            string cookie = first["Cookie"];
            Check(first["Referer"] == "https://live.douyin.com" && first.ContainsKey("User-Agent"), "Default headers changed.");
            Check(!first.Values.Contains(site.SearchCookie), "Search session leaked to general headers.");
            first["Referer"] = "fixture-room"; first["Cookie"] = "fixture-mutated";
            var second = Headers(site);
            Check(!ReferenceEquals(first, second) && second["Cookie"] == cookie && second["Referer"] == "https://live.douyin.com", "Header copies share mutable state.");
        });
        await Test("WARN-007 force refresh and lowercase cookie remain compatible", () =>
        {
            var site = new Douyin();
            var field = typeof(Douyin).GetField("headers", PrivateInstance);
            var source = (Dictionary<string, string>)field.GetValue(site);
            source["cookie"] = "fixture-lowercase";
            Check(Headers(site)["cookie"] == "fixture-lowercase", "Existing lowercase cookie is not recognized.");
            var refreshed = Headers(site, true);
            Check(refreshed.ContainsKey("Cookie") && refreshed["Cookie"] != "fixture-lowercase", "Forced defaults were not restored.");
            Check(source["cookie"] == "fixture-lowercase", "Refresh changed the existing lowercase entry.");
        });
        await Test("WARN-008 public async header failure stays task-based", async () =>
        {
            var site = new Douyin();
            typeof(Douyin).GetField("headers", PrivateInstance).SetValue(site, null);
            Task<List<AllLive.Core.Models.LiveCategory>> task = null;
            try { task = site.GetCategores(); }
            catch { throw new InvalidOperationException("Public method threw synchronously instead of returning a faulted task."); }
            Check(task.IsFaulted, "Header error must fault the returned task before any HTTP request.");
            try { await task; throw new InvalidOperationException("Expected header failure."); }
            catch (NullReferenceException) { }
        });
        await Test("WARN-009 actual embedded scripts preserve exact order and text", async () =>
        {
            string expected = ReadResource("webmssdk.js") + "\n" + ReadResource("a_bogus.js");
            Check(await LoggingDouyinScriptRunner.ReadScriptsAsync() == expected, "Script concatenation changed.");
            Check(LogHelper.Levels.Count > 1 && LogHelper.Levels.All(x => x == LogType.DEBUG), "Successful resource logging changed.");
        });
        await Test("WARN-010 script load failure keeps empty fallback", async () =>
        {
            LogHelper.OnLog = level => { if (level == LogType.DEBUG) throw new IOException("fixture read setup failure"); };
            Check(await LoggingDouyinScriptRunner.ReadScriptsAsync() == string.Empty, "Caught resource setup failure must return empty text.");
            Check(LogHelper.Levels.SequenceEqual(new[] { LogType.DEBUG, LogType.ERROR }), "Failure was not logged once.");
        });
        await Test("WARN-011 error logger exception stays task-based", async () =>
        {
            var failure = new IOException("fixture logging failure");
            LogHelper.OnLog = level => { throw failure; };
            Task<string> task = null;
            try { task = LoggingDouyinScriptRunner.ReadScriptsAsync(); }
            catch { throw new InvalidOperationException("Script method threw synchronously."); }
            try { await task; throw new InvalidOperationException("Expected logging failure."); }
            catch (IOException ex) { Check(ReferenceEquals(ex, failure) && task.IsFaulted, "Original task exception was not preserved."); }
        });
        await Test("WARN-012 error logger cancellation stays canceled task", async () =>
        {
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                LogHelper.OnLog = level => { if (level == LogType.DEBUG) throw new IOException("fixture setup"); source.Token.ThrowIfCancellationRequested(); };
                var task = LoggingDouyinScriptRunner.ReadScriptsAsync();
                try { await task; throw new InvalidOperationException("Expected cancellation."); }
                catch (OperationCanceledException ex) { Check(task.IsCanceled && ex.CancellationToken == source.Token, "Cancellation task semantics changed."); }
            }
        });
        await Test("WARN-013 real Brotli decompression preserves payload", () =>
        {
            byte[] expected = Encoding.UTF8.GetBytes("fixture danmaku payload \u6d4b\u8bd5");
            byte[] compressed;
            using (var buffer = new MemoryStream())
            {
                using (var compressor = new System.IO.Compression.BrotliStream(buffer, System.IO.Compression.CompressionMode.Compress, true))
                    compressor.Write(expected, 0, expected.Length);
                compressed = buffer.ToArray();
            }
            var method = typeof(BiliBiliDanmaku).GetMethod("DecompressDataWithBrotli", PrivateInstance);
            var result = (byte[])method.Invoke(new BiliBiliDanmaku(), new object[] { compressed });
            Check(result.SequenceEqual(expected), "Brotli payload changed.");
        });
        await Test("WARN-014 existing Brotli fallback stays idempotent", () =>
        {
            var site = new BiliBiliDanmaku();
            var method = typeof(BiliBiliDanmaku).GetMethod("HandleBrotliUnavailable", PrivateInstance);
            var flag = typeof(BiliBiliDanmaku).GetField("_forceLegacyProtover", BindingFlags.NonPublic | BindingFlags.Static);
            bool original = (bool)flag.GetValue(null);
            try
            {
                flag.SetValue(null, false);
                Check(((byte[])method.Invoke(site, null)).Length == 0, "Fallback no longer returns an empty payload.");
                Check((bool)flag.GetValue(null), "Fallback did not select legacy protocol.");
                Check(((byte[])method.Invoke(site, null)).Length == 0, "Repeated fallback changed.");
            }
            finally { flag.SetValue(null, original); }
        });

        await Test("WARN-015 request packet preserves original read exception and stack", () =>
        {
            var failure = new IOException("fixture request read failure");
            using (var stream = new ThrowingMemoryStream(failure))
                CheckRethrow(failure, () => new Tup.RequestPacket().ReadFrom(new Tup.Tars.TarsInputStream(stream)), "ThrowingMemoryStream.ReadByte");
        });
        await Test("WARN-016 uni packet preserves original decode exception and stack", () =>
        {
            var failure = new IOException("fixture packet decode failure");
            CheckRethrow(failure, () => new ThrowingUniPacket(failure).Decode(new byte[4]), "ThrowingUniPacket.ReadFrom");
        });
        await Test("WARN-017 object creation preserves original exception and stack", () =>
        {
            var failure = new IOException("fixture type inspection failure");
            CheckRethrow(failure, () => Tup.BasicClassTypeUtil.CreateObject(new ThrowingType(failure)), "ThrowingType.ToString");
        });

        var report = new { tests = Results.Count, passed = Results.Count - failures, failed = failures,
            scope = "Actual Core assembly, RelayCommand and LoggingDouyinScriptRunner; logging-only double; embedded scripts, in-memory headers and Brotli fixtures; no network, credentials or Windows UI.", results = Results };
        File.WriteAllText(args.Length > 0 ? args[0] : "results.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
