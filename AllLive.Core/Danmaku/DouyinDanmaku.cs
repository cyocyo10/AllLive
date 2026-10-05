using AllLive.Core.Helper;
using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using AllLive.Core.Danmaku.Proto;
using ProtoBuf;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AllLive.Core.Danmaku
{
    public class DouyinDanmakuArgs
    {
        public string WebRid { get; set; }
        public string RoomId { get; set; }
        public string UserId { get; set; }
        public string Cookie { get; set; }
    }
    public class DouyinDanmaku : ILiveDanmaku
    {
        public int HeartbeatTime => 10 * 1000;

        public event EventHandler<LiveMessage> NewMessage;
        public event EventHandler<string> OnClose;
        private string baseUrl = "wss://webcast3-ws-web-lq.douyin.com/webcast/im/push/v2/";

        System.Timers.Timer timer;
        WebSocket ws;
        DouyinDanmakuArgs danmakuArgs;
        private string ServerUrl { get; set; }
        private string BackupUrl { get; set; }

        private const int MaxReconnectAttempts = 5;
        private int reconnectAttempts;
        private bool isStopping;
        private bool useBackupEndpoint;
        private CancellationTokenSource reconnectTokenSource;
        private readonly SemaphoreSlim _connectionSemaphore = new SemaphoreSlim(1, 1);
        private Func<string, string, Task<string>> signatureProvider;
        private readonly object stateLock = new object();
        private long sessionGeneration;
        private long socketGeneration;

        public DouyinDanmaku()
        {
            signatureProvider = DefaultSignatureProvider;
        }

        public void SetSignatureProvider(Func<string, string, Task<string>> provider)
        {
            signatureProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task Start(object args)
        {
            var suppliedArgs = args as DouyinDanmakuArgs ?? throw new ArgumentException("args must be DouyinDanmakuArgs", nameof(args));
            var startArgs = new DouyinDanmakuArgs
            {
                WebRid = suppliedArgs.WebRid,
                RoomId = suppliedArgs.RoomId,
                UserId = suppliedArgs.UserId,
                Cookie = suppliedArgs.Cookie
            };
            long session;
            lock (stateLock)
            {
                session = ++sessionGeneration;
                danmakuArgs = startArgs;
                isStopping = false;
                reconnectAttempts = 0;
                useBackupEndpoint = false;
                ServerUrl = null;
                BackupUrl = null;
                CancelReconnect();
            }

            await _connectionSemaphore.WaitAsync();
            try
            {
                lock (stateLock)
                {
                    if (!IsCurrentSession(session)) return;
                }
                CleanupWebSocket();
            }
            finally
            {
                _connectionSemaphore.Release();
            }

            var ts = Utils.GetTimestampMs();
            var query = new Dictionary<string, string>()
            {
                { "app_name", "douyin_web" },
                { "version_code", "180800" },
                { "webcast_sdk_version", "1.3.0" },
                { "update_version_code", "1.3.0" },
                { "compress", "gzip" },
                { "cursor", $"h-1_t-{ts}_r-1_d-1_u-1" },
                { "host", "https://live.douyin.com" },
                { "aid", "6383" },
                { "live_id", "1" },
                { "did_rule", "3" },
                { "debug", "false" },
                { "maxCacheMessageNumber", "20" },
                { "endpoint", "live_pc" },
                { "support_wrds", "1" },
                { "im_path", "/webcast/im/fetch/" },
                { "user_unique_id", startArgs.UserId },
                { "device_platform", "web" },
                { "cookie_enabled", "true" },
                { "screen_width", "1920" },
                { "screen_height", "1080" },
                { "browser_language", "zh-CN" },
                { "browser_platform", "Win32" },
                { "browser_name", "Mozilla" },
                { "browser_version", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36 Edg/125.0.0.0" },
                { "browser_online", "true" },
                { "tz_name", "Asia/Shanghai" },
                { "identity", "audience" },
                { "room_id", startArgs.RoomId },
                { "heartbeatDuration", "0" },
            };

            var sign = await signatureProvider(startArgs.RoomId, startArgs.UserId);
            Trace.WriteLine($"[Danmaku] Signature: {sign}");
            query.Add("signature", sign);

            var url = $"{baseUrl}?{Utils.BuildQueryString(query)}";
            lock (stateLock)
            {
                // Signing can finish after Stop or a newer Start has invalidated this session.
                if (!IsCurrentSession(session)) return;
                ServerUrl = url;
                BackupUrl = url.Replace("webcast3-ws-web-lq", "webcast5-ws-web-lf");
            }
            Trace.WriteLine($"[Danmaku] WebSocket URL: {url.Substring(0, Math.Min(150, url.Length))}...");
            Trace.WriteLine($"[Danmaku] Connecting WebSocket...");
            await ConnectAsync(session, useBackup: false).ConfigureAwait(false);
        }

        private bool IsCurrentSession(long session)
        {
            return !isStopping && sessionGeneration == session;
        }

        private bool IsCurrentSocket(WebSocket socket, long session)
        {
            return socket != null && IsCurrentSession(session) &&
                socketGeneration == session && ReferenceEquals(ws, socket);
        }

        private bool TryGetCurrentSocket(object sender, out WebSocket socket, out long session)
        {
            lock (stateLock)
            {
                socket = sender as WebSocket;
                session = socketGeneration;
                return IsCurrentSocket(socket, session);
            }
        }

        private async void Ws_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (!TryGetCurrentSocket(sender, out var socket, out var session)) return;
                lock (stateLock)
                {
                    if (!IsCurrentSocket(socket, session)) return;
                    reconnectAttempts = 0;
                    useBackupEndpoint = false;
                    CancelReconnect();
                }
                await SendHeartBeatDataAsync(socket, session).ConfigureAwait(false);
                lock (stateLock)
                {
                    if (IsCurrentSocket(socket, session)) timer?.Start();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DouyinDanmaku.Ws_OnOpen] error: {ex.Message}");
            }
        }

        private async void Ws_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                if (!TryGetCurrentSocket(sender, out var socket, out var session)) return;
                lock (stateLock)
                {
                    if (!IsCurrentSocket(socket, session)) return;
                    reconnectAttempts = 0;
                }
                var wssPackage = DeserializeProto<PushFrame>(e.RawData);
                var logId = wssPackage.logId;
                var decompressed = GzipDecompress(wssPackage.Payload);
                var payloadPackage = DeserializeProto<Response>(decompressed);
                if (payloadPackage.needAck ?? false)
                {
                    await SendACKDataAsync(socket, session, logId ?? 0, payloadPackage.internalExt).ConfigureAwait(false);
                }

                foreach (var msg in payloadPackage.messagesLists)
                {
                    lock (stateLock)
                    {
                        if (!IsCurrentSocket(socket, session)) return;
                    }
                    if (msg.Method == "WebcastChatMessage")
                    {
                        UnPackWebcastChatMessage(msg.Payload, socket, session);
                    }
                    else if (msg.Method == "WebcastRoomUserSeqMessage")
                    {
                        UnPackWebcastRoomUserSeqMessage(msg.Payload, socket, session);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DouyinDanmaku.Ws_OnMessage] error: {ex.Message}");
            }
        }

        private void PublishMessage(WebSocket socket, long session, LiveMessage message)
        {
            lock (stateLock)
            {
                if (IsCurrentSocket(socket, session)) NewMessage?.Invoke(this, message);
            }
        }

        private void UnPackWebcastChatMessage(byte[] payload, WebSocket socket, long session)
        {
            try
            {
                var chatMessage = DeserializeProto<ChatMessage>(payload);
                PublishMessage(socket, session, new LiveMessage()
                {
                    Type = LiveMessageType.Chat,
                    Color = DanmakuColor.White,
                    Message = chatMessage.Content,
                    UserName = chatMessage.User.nickName,
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DouyinDanmaku.UnPackWebcastChatMessage] error: {ex.Message}");
            }
        }

        private void UnPackWebcastRoomUserSeqMessage(byte[] payload, WebSocket socket, long session)
        {
            try
            {
                var roomUserSeqMessage = DeserializeProto<RoomUserSeqMessage>(payload);
                PublishMessage(socket, session, new LiveMessage()
                {
                    Type = LiveMessageType.Online,
                    Data = roomUserSeqMessage.totalUser,
                    Color = DanmakuColor.White,
                    Message = "",
                    UserName = "",
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DouyinDanmaku.UnPackWebcastRoomUserSeqMessage] error: {ex.Message}");
            }
        }

        private void Ws_OnClose(object sender, CloseEventArgs e)
        {
            try
            {
                if (TryGetCurrentSocket(sender, out var socket, out var session))
                {
                    HandleConnectionFailure(session, string.IsNullOrEmpty(e.Reason) ? "Danmaku server closed" : e.Reason, socket);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DouyinDanmaku.Ws_OnClose] error: {ex.Message}");
            }
        }

        private void Ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            try
            {
                if (TryGetCurrentSocket(sender, out var socket, out var session))
                {
                    HandleConnectionFailure(session, e.Message, socket);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DouyinDanmaku.Ws_OnError] error: {ex.Message}");
            }
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            WebSocket socket;
            long session;
            lock (stateLock)
            {
                if (!ReferenceEquals(sender, timer)) return;
                socket = ws;
                session = socketGeneration;
            }
            _ = SendHeartBeatDataAsync(socket, session);
        }

        public void Heartbeat()
        {
            WebSocket socket;
            long session;
            lock (stateLock)
            {
                socket = ws;
                session = socketGeneration;
            }
            _ = SendHeartBeatDataAsync(socket, session);
        }

        public async Task Stop()
        {
            long stoppedSession;
            lock (stateLock)
            {
                stoppedSession = ++sessionGeneration;
                isStopping = true;
                reconnectAttempts = 0;
                useBackupEndpoint = false;
                CancelReconnect();
            }
            await _connectionSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (stateLock)
                {
                    // A newer Start owns cleanup if it overtook this Stop while waiting.
                    if (sessionGeneration != stoppedSession) return;
                }
                CleanupWebSocket();
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private async Task SendHeartBeatDataAsync(WebSocket socket, long session)
        {
            try
            {
                await SendDataAsync(socket, session, new PushFrame { payloadType = "hb" }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DouyinDanmaku.Heartbeat] error: {ex.Message}");
            }
        }

        private async Task SendACKDataAsync(WebSocket socket, long session, ulong logId, string internalExt)
        {
            if (string.IsNullOrEmpty(internalExt)) return;
            await SendDataAsync(socket, session, new PushFrame
            {
                logId = logId,
                payloadType = internalExt
            }).ConfigureAwait(false);
        }

        private async Task SendDataAsync(WebSocket socket, long session, PushFrame frame)
        {
            await _connectionSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (stateLock)
                {
                    if (!IsCurrentSocket(socket, session)) return;
                }
                // Never resolve ws again after an await: it may belong to a newer session.
                socket.Send(SerializeProto(frame));
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        public static byte[] GzipDecompress(byte[] bytes)
        {
            using (var memoryStream = new MemoryStream(bytes))
            {
                using (var outputStream = new MemoryStream())
                {
                    using (var decompressStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                    {
                        decompressStream.CopyTo(outputStream);
                    }
                    return outputStream.ToArray();
                }
            }
        }

        private static byte[] SerializeProto(object obj)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    Serializer.Serialize(ms, obj);
                    var buffer = ms.GetBuffer();
                    var dataBuffer = new byte[ms.Length];
                    Array.Copy(buffer, dataBuffer, ms.Length);
                    ms.Dispose();
                    return dataBuffer;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DouyinDanmaku.SerializeProto] error: {ex.Message}");
                return null;
            }
        }

        private static T DeserializeProto<T>(byte[] bufferData)
        {
            using (MemoryStream ms = new MemoryStream(bufferData))
            {
                return Serializer.Deserialize<T>(ms);
            }
        }

        /// <summary>
        /// Get Websocket signature
        /// </summary>
        private async Task<string> DefaultSignatureProvider(string roomId, string uniqueId)
        {
            var signature = await DouyinSignHelper.GetSignatureAsync(roomId, uniqueId);
            Trace.WriteLine("DouyinDanmaku signature result: " + signature);
            if (!string.IsNullOrEmpty(signature) && signature != "00000000")
            {
                return signature;
            }

            var fallback = await GetSign(roomId, uniqueId);
            Trace.WriteLine("DouyinDanmaku fallback signature result: " + fallback);
            return fallback;
        }

        private async Task ConnectAsync(long session, bool useBackup, CancellationTokenSource reconnect = null)
        {
            await _connectionSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (stateLock)
                {
                    if (!IsCurrentSession(session) ||
                        (reconnect != null && !ReferenceEquals(reconnectTokenSource, reconnect))) return;
                }
                CleanupWebSocket();

                WebSocket socket;
                lock (stateLock)
                {
                    if (!IsCurrentSession(session) ||
                        (reconnect != null && !ReferenceEquals(reconnectTokenSource, reconnect))) return;
                    // The delayed task is now running. A failure must be free to schedule the next retry.
                    if (reconnect != null) reconnectTokenSource = null;
                    var targetUrl = useBackup && !string.IsNullOrEmpty(BackupUrl) ? BackupUrl : ServerUrl;
                    socket = new WebSocket(targetUrl);
                    WebSocketSecurity.Configure(socket);
                    socket.CustomHeaders = new Dictionary<string, string>()
                    {
                        {"Origin", "https://live.douyin.com"},
                        {"Cookie", danmakuArgs.Cookie},
                        {"User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36 Edg/125.0.0.0"}
                    };
                    ws = socket;
                    socketGeneration = session;
                    socket.OnOpen += Ws_OnOpen;
                    socket.OnError += Ws_OnError;
                    socket.OnMessage += Ws_OnMessage;
                    socket.OnClose += Ws_OnClose;
                    timer = new System.Timers.Timer(HeartbeatTime) { AutoReset = true };
                    timer.Elapsed += Timer_Elapsed;
                }
                socket.Connect();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DouyinDanmaku.ConnectAsync] exception: {ex.Message}");
                CleanupWebSocket();
                HandleConnectionFailure(session, ex.Message);
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private void CleanupWebSocket()
        {
            WebSocket socket;
            System.Timers.Timer heartbeatTimer;
            lock (stateLock)
            {
                socket = ws;
                ws = null;
                socketGeneration = 0;
                heartbeatTimer = timer;
                timer = null;
            }
            if (socket != null)
            {
                socket.OnOpen -= Ws_OnOpen;
                socket.OnError -= Ws_OnError;
                socket.OnMessage -= Ws_OnMessage;
                socket.OnClose -= Ws_OnClose;
                try
                {
                    socket.Close();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DouyinDanmaku.CleanupWebSocket] Close exception (ignored): {ex.Message}");
                }
            }
            if (heartbeatTimer != null)
            {
                heartbeatTimer.Elapsed -= Timer_Elapsed;
                heartbeatTimer.Stop();
                heartbeatTimer.Dispose();
            }
        }

        private void HandleConnectionFailure(long session, string reason, WebSocket socket = null)
        {
            lock (stateLock)
            {
                if (!IsCurrentSession(session) || (socket != null && !IsCurrentSocket(socket, session))) return;
                if (reconnectTokenSource != null) return;
                timer?.Stop();
                if (reconnectAttempts >= MaxReconnectAttempts)
                {
                    isStopping = true;
                    OnClose?.Invoke(this, string.IsNullOrEmpty(reason) ? "Reconnect failed" : reason);
                    return;
                }

                reconnectAttempts++;
                useBackupEndpoint = !useBackupEndpoint && !string.IsNullOrEmpty(BackupUrl);
                ScheduleReconnect(session, useBackupEndpoint);
                OnClose?.Invoke(this, $"Connection lost, reconnecting ({reconnectAttempts}/{MaxReconnectAttempts})");
            }
        }

        private void ScheduleReconnect(long session, bool useBackup)
        {
            var source = new CancellationTokenSource();
            reconnectTokenSource = source;
            var token = source.Token;
            // Do not pass token to Task.Run: even cancellation before scheduling must run finally.
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                    await ConnectAsync(session, useBackup, source).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Stop or a newer session canceled this delayed retry.
                }
                finally
                {
                    lock (stateLock)
                    {
                        if (ReferenceEquals(reconnectTokenSource, source)) reconnectTokenSource = null;
                    }
                    source.Dispose();
                }
            });
        }

        // Call only while holding stateLock; the scheduled task owns disposal.
        private void CancelReconnect()
        {
            var source = reconnectTokenSource;
            reconnectTokenSource = null;
            source?.Cancel();
        }

        private async Task<string> GetSign(string roomId, string uniqueId)
        {
            try
            {
                var body = JsonConvert.SerializeObject(new { roomId, uniqueId });
                var result = await HttpUtil.PostJsonString("https://dy.nsapps.cn/signature", body);
                var json = JObject.Parse(result);
                return json["data"]?["signature"]?.ToString() ?? "00000000";
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DouyinDanmaku.GetSign] error: {ex.Message}");
                return "00000000";
            }
        }
    }
}
