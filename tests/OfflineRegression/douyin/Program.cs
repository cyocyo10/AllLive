using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AllLive.Core.Danmaku;
using AllLive.Core.Danmaku.Proto;
using AllLive.Core.Helper;
using ProtoBuf;
using WebSocketSharp;

namespace DouyinLifecycleRegression
{
    internal static class Program
    {
        private const string Commit = "c6951df959b7ead0d93edc087188788b49f1e273";
        private static readonly List<TestResult> Results = new();
        private sealed record TestResult(string Name, string Status, long DurationMs,
            string ExpectedSafeBehavior, Dictionary<string, object> Observed, string Error);

        private static async Task<int> Main(string[] args)
        {
            var outputDirectory = Path.GetFullPath(args.Length > 0 ? args[0] : "results");
            Directory.CreateDirectory(outputDirectory);
            using var trace = new TextWriterTraceListener(Path.Combine(outputDirectory, "production-trace.log"));
            Trace.Listeners.Add(trace);
            Trace.AutoFlush = true;
            Console.WriteLine("Offline .NET source-linked lifecycle regression tests; no Windows/UWP or live network test.");
            Console.WriteLine("Repaired shared production source based on " + Commit);
            Console.WriteLine("Production Task.Delay(5 seconds) is not replaced or shortened.");

            await Run("pending-signature-completes-after-stop", "A completed Stop prevents an earlier Start from creating or opening a socket.", PendingSignatureAfterStop);
            await Run("normal-start-then-stop-control", "Stop closes an established socket and clears socket/timer state; TLS is configured.", NormalStartStop);
            await Run("retry-connect-exceptions-exhaust-five-attempts", "Five retries run at five-second intervals, alternate endpoints, and terminate once.", () => RetryExhausts(true));
            await Run("retry-error-events-exhaust-five-attempts", "Error-event failures receive five retries and a terminal notification.", () => RetryExhausts(false));
            await Run("stop-cancels-pending-retry-control", "Stop before the five-second retry prevents all retry connections.", StopCancelsRetry);
            await Run("old-signature-after-stop-and-start", "Late signing from an earlier session cannot replace new session URL, cookie, or socket.", () => OldSignatureAfterNewStart(true));
            await Run("old-signature-after-newer-start", "Overlapping Start calls leave only the latest session connected.", () => OldSignatureAfterNewStart(false));
            await Run("queued-old-socket-and-timer-events", "Callbacks queued before cleanup cannot reset, cancel, publish, send, or restart the current session.", QueuedOldEvents);
            await Run("old-pending-ack-after-stop-and-start", "An old message suspended at ACK cannot send to, publish into, or clean up the new session.", PendingAckAfterRestart);
            await Run("canceled-old-retry-after-new-start", "The old reconnect task cannot replace a new session after its original deadline.", OldRetryAfterRestart);
            await Run("retry-until-recovery-and-retry-again", "Retries reach a healthy socket, reset the budget, and can recover from a later disconnect.", RetryRecovers);

            var report = new
            {
                GeneratedUtc = DateTimeOffset.UtcNow,
                BaselineProductionCommit = Commit,
                Source = "../source (repaired files, not audit snapshots)",
                Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                Scope = "Source-linked lifecycle + TLS callback regression; fake transport, injected signature, fixture protobuf; no Windows/UWP or live-service execution.",
                HTTPCalls = HttpUtil.Calls,
                DefaultSignatureCalls = DouyinSignHelper.Calls,
                Results
            };
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "results.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Trace.Flush();
            Console.WriteLine($"PASS={Results.Count(r => r.Status == "PASS")} FAIL={Results.Count(r => r.Status == "FAIL")}");
            return Results.All(r => r.Status == "PASS") ? 0 : 1;
        }

        private static async Task Run(string name, string expected, Func<Task<Dictionary<string, object>>> body)
        {
            var clock = Stopwatch.StartNew();
            Trace.WriteLine("===== TEST " + name + " =====");
            Serializer.DeserializeFixture = null;
            Serializer.DeserializeCalls = 0;
            try
            {
                var observed = await body();
                Require(HttpUtil.Calls == 0 && DouyinSignHelper.Calls == 0, "Unexpected HTTP/default signature dependency called");
                var result = new TestResult(name, "PASS", clock.ElapsedMilliseconds, expected, observed, null);
                Results.Add(result);
                Console.WriteLine(JsonSerializer.Serialize(result));
            }
            catch (Exception error)
            {
                var result = new TestResult(name, "FAIL", clock.ElapsedMilliseconds, expected, new(), error.ToString());
                Results.Add(result);
                Console.WriteLine(JsonSerializer.Serialize(result));
            }
        }

        private static DouyinDanmakuArgs FixtureArgs(string suffix = "") => new()
        {
            RoomId = "offline-room" + suffix, WebRid = "offline-web-rid" + suffix,
            UserId = "offline-user" + suffix, Cookie = "offline-fixture-cookie" + suffix
        };
        private static DouyinDanmaku NewClient()
        {
            var client = new DouyinDanmaku();
            client.SetSignatureProvider((room, user) => Task.FromResult("offline-signature"));
            return client;
        }
        private static object Field(DouyinDanmaku client, string name) =>
            typeof(DouyinDanmaku).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(client);
        private static int Attempts(DouyinDanmaku client) => (int)Field(client, "reconnectAttempts");
        private static WebSocket Socket(DouyinDanmaku client) => (WebSocket)Field(client, "ws");
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static async Task WaitUntil(Func<bool> predicate, TimeSpan timeout, string message)
        {
            var clock = Stopwatch.StartNew();
            while (!predicate())
            {
                if (clock.Elapsed > timeout) throw new TimeoutException(message);
                await Task.Delay(10);
            }
        }
        private static Task Started(DouyinDanmaku client, string suffix = "") => client.Start(FixtureArgs(suffix)).WaitAsync(TimeSpan.FromSeconds(2));
        private static Task Stopped(DouyinDanmaku client) => client.Stop().WaitAsync(TimeSpan.FromSeconds(2));
        private static Task HeartbeatSent(WebSocket socket) => WaitUntil(() => Volatile.Read(ref socket.InstanceSendCalls) >= 1,
            TimeSpan.FromSeconds(2), "Opening heartbeat did not complete");

        private static async Task<Dictionary<string, object>> PendingSignatureAfterStop()
        {
            WebSocket.Reset(socket => socket.Open());
            var signature = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var client = NewClient();
            client.SetSignatureProvider((room, user) => signature.Task);
            var start = client.Start(FixtureArgs());
            try
            {
                Require(!start.IsCompleted && WebSocket.ConnectCalls == 0, "Start did not pause at signature");
                await Stopped(client);
                signature.SetResult("offline-late-signature");
                await start.WaitAsync(TimeSpan.FromSeconds(2));
                Require(WebSocket.ConnectCalls == 0 && WebSocket.Instances.IsEmpty && Field(client, "ws") == null &&
                    Field(client, "timer") == null && WebSocket.SendCalls == 0, "An earlier Start revived after Stop");
                return new() { ["ConnectCallsAfterLateSignature"] = WebSocket.ConnectCalls, ["SendCalls"] = WebSocket.SendCalls,
                    ["SocketCleared"] = true, ["TimerCleared"] = true };
            }
            finally { signature.TrySetResult("cleanup"); await start.WaitAsync(TimeSpan.FromSeconds(2)); await Stopped(client); }
        }

        private static async Task<Dictionary<string, object>> NormalStartStop()
        {
            WebSocket.Reset(socket => socket.Open());
            var client = NewClient();
            try
            {
                await Started(client);
                var socket = Socket(client);
                await HeartbeatSent(socket);
                Require(socket.ReadyState == WebSocketState.Open, "Start did not open a socket");
                var ssl = socket.SslConfiguration;
                Require(ssl.EnabledSslProtocols == SslProtocols.Tls12 && ssl.ServerCertificateValidationCallback != null, "TLS helper was not applied");
                using var key = RSA.Create(2048);
                var certificateRequest = new CertificateRequest("CN=offline-fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
                Require(ssl.ServerCertificateValidationCallback(socket, certificate, null, SslPolicyErrors.None), "Clean certificate rejected");
                Require(!ssl.ServerCertificateValidationCallback(socket, certificate, null, SslPolicyErrors.RemoteCertificateNameMismatch) &&
                    !ssl.ServerCertificateValidationCallback(socket, certificate, null, SslPolicyErrors.RemoteCertificateChainErrors) &&
                    !ssl.ServerCertificateValidationCallback(socket, null, null, SslPolicyErrors.None), "Invalid or absent certificate accepted");
                await Stopped(client);
                Require(socket.ReadyState == WebSocketState.Closed && Socket(client) == null && Field(client, "timer") == null, "Stop cleanup failed");
                return new() { ["ConnectCalls"] = WebSocket.ConnectCalls, ["CloseCalls"] = WebSocket.CloseCalls,
                    ["SocketCleared"] = true, ["TimerCleared"] = true, ["TLSCallbackVerified"] = true };
            }
            finally { await Stopped(client); }
        }

        private static async Task<Dictionary<string, object>> RetryExhausts(bool throwFailure)
        {
            var clock = Stopwatch.StartNew();
            var connectTimes = new ConcurrentQueue<long>();
            WebSocket.Reset(socket =>
            {
                connectTimes.Enqueue(clock.ElapsedMilliseconds);
                if (throwFailure) throw new InvalidOperationException("offline connection failure");
                socket.Error("offline error event");
                socket.Close(); // Real transports can report both; this must not consume two retries.
            });
            var client = NewClient();
            var messages = new ConcurrentQueue<string>();
            client.OnClose += (_, message) => messages.Enqueue(message);
            try
            {
                await Started(client);
                Require(WebSocket.ConnectCalls == 1 && Attempts(client) == 1 && Field(client, "reconnectTokenSource") != null,
                    "Initial failure did not schedule retry 1");
                await WaitUntil(() => messages.Count == 6, TimeSpan.FromSeconds(30), "Five retries and terminal failure did not complete");
                await WaitUntil(() => Field(client, "reconnectTokenSource") == null, TimeSpan.FromSeconds(2), "Terminal retry token retained");
                await Task.Delay(200);
                var times = connectTimes.ToArray();
                var hosts = WebSocket.ConnectUrls.Select(url => new Uri(url).Host).ToArray();
                Require(WebSocket.ConnectCalls == 6 && Attempts(client) == 5 && times.Length == 6, "Retry count differs from initial + five retries");
                for (var i = 1; i < times.Length; i++)
                {
                    Require(times[i] - times[i - 1] >= 4900, "Production five-second delay shortened");
                    Require(hosts[i] != hosts[i - 1], "Primary/backup endpoints did not alternate");
                }
                var notifications = messages.ToArray();
                for (var i = 0; i < 5; i++) Require(notifications[i].Contains($"({i + 1}/5)"), "Missing ordered reconnect notification");
                Require(!notifications[5].Contains("reconnecting") && (bool)Field(client, "isStopping"), "Missing terminal state/notification");
                return new() { ["FailureFixture"] = throwFailure ? "Connect throws" : "OnError + OnClose",
                    ["ConnectCalls"] = WebSocket.ConnectCalls, ["ReconnectAttempts"] = Attempts(client), ["ConnectTimesMs"] = times,
                    ["EndpointHosts"] = hosts, ["OnCloseMessages"] = notifications, ["TokenCleared"] = true, ["Terminated"] = true };
            }
            finally { await Stopped(client); }
        }

        private static async Task<Dictionary<string, object>> StopCancelsRetry()
        {
            WebSocket.Reset(socket => throw new InvalidOperationException("offline initial failure"));
            var client = NewClient();
            try
            {
                await Started(client);
                Require(Field(client, "reconnectTokenSource") != null, "Retry was not scheduled");
                await Stopped(client);
                await Task.Delay(5300);
                Require(WebSocket.ConnectCalls == 1 && Field(client, "reconnectTokenSource") == null && Socket(client) == null, "Canceled retry still connected");
                return new() { ["ConnectCallsAfter5300Ms"] = WebSocket.ConnectCalls, ["TokenCleared"] = true, ["SocketCleared"] = true };
            }
            finally { await Stopped(client); }
        }

        private static async Task<Dictionary<string, object>> OldSignatureAfterNewStart(bool stopFirst)
        {
            WebSocket.Reset(socket => socket.Open());
            var signature = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var client = NewClient();
            client.SetSignatureProvider((room, user) => room.EndsWith("-old") ? signature.Task : Task.FromResult("new-signature"));
            var start = client.Start(FixtureArgs("-old"));
            try
            {
                if (stopFirst) await Stopped(client);
                await Started(client, "-new");
                var socket = Socket(client);
                await HeartbeatSent(socket);
                signature.SetResult("old-signature");
                await start.WaitAsync(TimeSpan.FromSeconds(2));
                Require(WebSocket.ConnectCalls == 1 && ReferenceEquals(Socket(client), socket) && socket.Url.Contains("offline-room-new") &&
                    socket.Url.Contains("new-signature") && !socket.Url.Contains("old-signature") &&
                    socket.CustomHeaders["Cookie"] == FixtureArgs("-new").Cookie, "Old signature overwrote the new session");
                return new() { ["StoppedBeforeRestart"] = stopFirst, ["ConnectCalls"] = WebSocket.ConnectCalls,
                    ["NewSocketRetained"] = true, ["NewRoomCookieAndSignatureRetained"] = true };
            }
            finally { signature.TrySetResult("cleanup"); await start.WaitAsync(TimeSpan.FromSeconds(2)); await Stopped(client); }
        }

        private static async Task<Dictionary<string, object>> QueuedOldEvents()
        {
            WebSocket.Reset(socket => socket.Open());
            var client = NewClient();
            var messages = new ConcurrentQueue<string>();
            client.OnClose += (_, text) => messages.Enqueue(text);
            var published = 0;
            client.NewMessage += (_, _) => Interlocked.Increment(ref published);
            try
            {
                await Started(client, "-old");
                var oldSocket = Socket(client);
                await HeartbeatSent(oldSocket);
                var queued = oldSocket.CaptureQueuedEvents();
                var oldTimer = Field(client, "timer");
                await Stopped(client);
                await Started(client, "-new");
                var current = Socket(client);
                await HeartbeatSent(current);
                current.Error("current failure");
                var token = Field(client, "reconnectTokenSource");
                Require(token != null && Attempts(client) == 1, "Current reconnect precondition missing");
                var sends = WebSocket.SendCalls;
                queued();
                typeof(DouyinDanmaku).GetMethod("Timer_Elapsed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(client, new[] { oldTimer, null });
                await Task.Delay(200);
                Require(ReferenceEquals(Socket(client), current) && ReferenceEquals(Field(client, "reconnectTokenSource"), token) &&
                    Attempts(client) == 1 && messages.Count == 1 && published == 0 && Serializer.DeserializeCalls == 0 &&
                    WebSocket.SendCalls == sends && !((System.Timers.Timer)Field(client, "timer")).Enabled, "Old events changed current state");
                return new() { ["OldSocketEventsDelivered"] = 4, ["OldTimerEventDelivered"] = true, ["CurrentRetryRetained"] = true,
                    ["ExtraSendCalls"] = WebSocket.SendCalls - sends, ["PublishedMessages"] = published, ["OldPayloadDecodeCalls"] = Serializer.DeserializeCalls };
            }
            finally { await Stopped(client); }
        }

        private static void SetChatFixture()
        {
            using var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionMode.Compress, true)) gzip.WriteByte(1);
            var bytes = compressed.ToArray();
            Serializer.DeserializeFixture = type =>
            {
                if (type == typeof(PushFrame)) return new PushFrame { logId = 1, Payload = bytes };
                if (type == typeof(Response)) return new Response { needAck = true, internalExt = "offline-ack",
                    messagesLists = new List<Message> { new() { Method = "WebcastChatMessage", Payload = new byte[] { 1 } } } };
                if (type == typeof(ChatMessage)) return new ChatMessage { Content = "offline-chat", User = new User { nickName = "offline-user" } };
                throw new InvalidOperationException("Unexpected fixture type " + type);
            };
        }

        private static async Task<Dictionary<string, object>> PendingAckAfterRestart()
        {
            WebSocket.Reset(socket => socket.Open());
            SetChatFixture();
            var client = NewClient();
            var published = 0;
            client.NewMessage += (_, _) => Interlocked.Increment(ref published);
            var gate = (SemaphoreSlim)Field(client, "_connectionSemaphore");
            var ownsGate = false;
            try
            {
                await Started(client, "-old");
                var old = Socket(client);
                await HeartbeatSent(old);
                await gate.WaitAsync();
                ownsGate = true;
                old.Emit(new byte[] { 1 });
                Require(Serializer.DeserializeCalls == 2 && published == 0, "Message did not pause before ACK");
                var stop = client.Stop();
                var restart = client.Start(FixtureArgs("-new"));
                gate.Release();
                ownsGate = false;
                await Task.WhenAll(stop, restart).WaitAsync(TimeSpan.FromSeconds(2));
                var current = Socket(client);
                await HeartbeatSent(current);
                Require(!ReferenceEquals(old, current) && current.ReadyState == WebSocketState.Open && old.InstanceSendCalls == 1 &&
                    current.InstanceSendCalls == 1 && published == 0, "Suspended old ACK/message reached a new session");
                current.Emit(new byte[] { 1 });
                await WaitUntil(() => Volatile.Read(ref published) == 1, TimeSpan.FromSeconds(2), "Current session did not deliver its own message");
                Require(current.InstanceSendCalls == 2, "Current ACK not sent exactly once");
                return new() { ["OldSocketSendCalls"] = old.InstanceSendCalls, ["CurrentSocketSendCalls"] = current.InstanceSendCalls,
                    ["OldPublishedMessages"] = 0, ["CurrentPublishedMessages"] = published, ["NewSocketStillOpen"] = true };
            }
            finally { if (ownsGate) gate.Release(); await Stopped(client); }
        }

        private static async Task<Dictionary<string, object>> OldRetryAfterRestart()
        {
            WebSocket.Reset(socket =>
            {
                if (WebSocket.ConnectCalls == 1) throw new InvalidOperationException("old failure");
                socket.Open();
            });
            var client = NewClient();
            try
            {
                await Started(client, "-old");
                Require(Field(client, "reconnectTokenSource") != null, "Old retry was not scheduled");
                await Stopped(client);
                await Started(client, "-new");
                var current = Socket(client);
                await HeartbeatSent(current);
                await Task.Delay(5300);
                Require(WebSocket.ConnectCalls == 2 && ReferenceEquals(Socket(client), current) && Attempts(client) == 0 &&
                    Field(client, "reconnectTokenSource") == null, "Old retry replaced/restarted the new session");
                return new() { ["ConnectCallsAfter5300Ms"] = WebSocket.ConnectCalls, ["NewSocketRetained"] = true, ["NewRetryBudget"] = Attempts(client) };
            }
            finally { await Stopped(client); }
        }

        private static async Task<Dictionary<string, object>> RetryRecovers()
        {
            WebSocket.Reset(socket =>
            {
                if (WebSocket.ConnectCalls == 1) { socket.Error("initial failure"); socket.Close(); }
                else if (WebSocket.ConnectCalls == 2) throw new InvalidOperationException("first retry failed");
                else socket.Open();
            });
            var client = NewClient();
            var notifications = new ConcurrentQueue<string>();
            client.OnClose += (_, message) => notifications.Enqueue(message);
            try
            {
                await Started(client);
                await WaitUntil(() => Volatile.Read(ref WebSocket.ConnectCalls) == 3 && Attempts(client) == 0,
                    TimeSpan.FromSeconds(13), "Retry did not recover on second attempt");
                var recovered = Socket(client);
                await HeartbeatSent(recovered);
                await Task.Delay(5300);
                Require(WebSocket.ConnectCalls == 3 && Field(client, "reconnectTokenSource") == null && notifications.Count == 2,
                    "Recovery left a duplicate retry scheduled");
                recovered.Error("later disconnect");
                await WaitUntil(() => Volatile.Read(ref WebSocket.ConnectCalls) == 4 && Attempts(client) == 0,
                    TimeSpan.FromSeconds(8), "New disconnect could not recover after resetting budget");
                await HeartbeatSent(Socket(client));
                Require(notifications.Last().Contains("(1/5)") && Field(client, "reconnectTokenSource") == null,
                    "Recovered session did not get a fresh retry budget");
                return new() { ["ConnectCalls"] = WebSocket.ConnectCalls, ["FirstRecoveryAfterRetries"] = 2,
                    ["LaterRecoveryAfterRetries"] = 1, ["OnCloseMessages"] = notifications.ToArray(), ["FinalRetryCount"] = Attempts(client), ["FinalState"] = Socket(client).ReadyState.ToString() };
            }
            finally { await Stopped(client); }
        }
    }
}
