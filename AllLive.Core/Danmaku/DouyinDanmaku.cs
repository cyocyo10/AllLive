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
        private readonly object connectionLock = new object();
        private Func<string, string, Task<string>> signatureProvider;

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
            danmakuArgs = args as DouyinDanmakuArgs ?? throw new ArgumentException("args must be DouyinDanmakuArgs", nameof(args));
            isStopping = false;
            reconnectAttempts = 0;
            useBackupEndpoint = false;
            CancelReconnect();
            var ts = Utils.GetTimestampMs();
            var query = new Dictionary<string, string>()
            {
            { "app_name", "douyin_web" },
            { "version_code", "180800" },
            { "webcast_sdk_version", "1.3.0" },
            { "update_version_code", "1.3.0" },
            { "compress", "gzip" },
            // {"internal_ext", $"internal_src:dim|wss_push_room_id:{danmakuArgs.roomId}|wss_push_did:{danmakuArgs.userId}|dim_log_id:20230626152702E8F63662383A350588E1|fetch_time:1687764422114|seq:1|wss_info:0-1687764422114-0-0|wrds_kvs:WebcastRoomRankMessage-1687764036509597990_InputPanelComponentSyncData-1687736682345173033_WebcastRoomStatsMessage-1687764414427812578"},
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
            { "user_unique_id", danmakuArgs.UserId },
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
            { "room_id", danmakuArgs.RoomId },
            { "heartbeatDuration", "0" },
            //{ "signature", "00000000" }
        };

            var sign = await signatureProvider(danmakuArgs.RoomId, danmakuArgs.UserId);
            query.Add("signature", sign);

            // 将参数拼接到url
            var url = $"{baseUrl}?{Utils.BuildQueryString(query)}";
            ServerUrl = url;
            BackupUrl = url.Replace("webcast3-ws-web-lq", "webcast5-ws-web-lf");
            await ConnectAsync(useBackup: false);
        }
        private async void Ws_OnOpen(object sender, EventArgs e)
        {
            reconnectAttempts = 0;
            useBackupEndpoint = false;
            CancelReconnect();
            await Task.Run(() =>
            {
                SendHeartBeatData();
            });
            timer?.Start();
        }

        private async void Ws_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                reconnectAttempts = 0;
                var message = e.RawData;
                var wssPackage = DeserializeProto<PushFrame>(message);
                var logId = wssPackage.logId;
                var decompressed = GzipDecompress(wssPackage.Payload);
                var payloadPackage = DeserializeProto<Response>(decompressed);
                if (payloadPackage.needAck ?? false)
                {
                    await Task.Run(() =>
                    {
                        SendACKData(logId ?? 0, payloadPackage.internalExt);
                    });

                }

                foreach (var msg in payloadPackage.messagesLists)
                {
                    if (msg.Method == "WebcastChatMessage")
                    {
                        UnPackWebcastChatMessage(msg.Payload);
                    }
                    else if (msg.Method == "WebcastRoomUserSeqMessage")
                    {
                        UnPackWebcastRoomUserSeqMessage(msg.Payload);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        private void UnPackWebcastChatMessage(byte[] payload)
        {
            try
            {
                var chatMessage = DeserializeProto<ChatMessage>(payload);
                NewMessage?.Invoke(this, new LiveMessage()
                {
                    Type = LiveMessageType.Chat,
                    Color = DanmakuColor.White,
                    Message = chatMessage.Content,
                    UserName = chatMessage.User.nickName,
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        void UnPackWebcastRoomUserSeqMessage(byte[] payload)
        {
            try
            {
                var roomUserSeqMessage = DeserializeProto<RoomUserSeqMessage>(payload);

                NewMessage?.Invoke(this, new LiveMessage()
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
                Debug.WriteLine(ex.Message);
            }

        }
        private void Ws_OnClose(object sender, CloseEventArgs e)
        {
            Trace.WriteLine($"DouyinDanmaku Ws_OnClose: Code={e.Code}, Reason={e.Reason}");
            if (isStopping)
            {
                return;
            }

            HandleConnectionFailure(string.IsNullOrEmpty(e.Reason) ? "服务器连接已关闭" : e.Reason);
        }

        private void Ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            Trace.WriteLine("DouyinDanmaku Ws_OnError: " + e.Message);
            if (isStopping)
            {
                return;
            }

            HandleConnectionFailure(e.Message);
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Heartbeat();
        }

        public void Heartbeat()
        {
            SendHeartBeatData();
        }


        public async Task Stop()
        {
            isStopping = true;
            CancelReconnect();
            await Task.Run(() =>
            {
                lock (connectionLock)
                {
                    reconnectAttempts = 0;
                    useBackupEndpoint = false;
                    CleanupWebSocket();
                }
            });
        }
        private void SendHeartBeatData()
        {
            var obj = new PushFrame();
            obj.payloadType = "hb";
            lock (connectionLock)
            {
                ws?.Send(SerializeProto(obj));
            }

        }
        private void SendACKData(ulong logId, string internalExt)
        {
            if (string.IsNullOrEmpty(internalExt))
            {
                return;
            }

            var obj = new PushFrame
            {
                logId = logId,
                payloadType = internalExt
            };

            lock (connectionLock)
            {
                ws?.Send(SerializeProto(obj));
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
            catch
            {
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
        /// 获取Websocket签名
        /// 服务端代码：https://github.com/lovelyyoshino/douyin_python
        /// </summary>
        /// <param name="roomId">房间ID</param>
        /// <param name="uniqueId">用户唯一ID</param>
        /// <returns></returns>
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

        private async Task ConnectAsync(bool useBackup)
        {
            var targetUrl = useBackup && !string.IsNullOrEmpty(BackupUrl) ? BackupUrl : ServerUrl;
            Trace.WriteLine("DouyinDanmaku connecting to: " + targetUrl);

            await Task.Run(() =>
            {
                try
                {
                    lock (connectionLock)
                    {
                        CleanupWebSocket();

                        ws = new WebSocket(targetUrl);
                        ws.CustomHeaders = new Dictionary<string, string>()
                        {
                            {"Origin","https://live.douyin.com" },
                            {"Cookie", danmakuArgs.Cookie},
                            {"User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36 Edg/125.0.0.0" }
                        };
                        ws.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;

                        ws.OnOpen += Ws_OnOpen;
                        ws.OnError += Ws_OnError;
                        ws.OnMessage += Ws_OnMessage;
                        ws.OnClose += Ws_OnClose;

                    timer?.Stop();
                    timer?.Dispose();
                    timer = new System.Timers.Timer(HeartbeatTime)
                        {
                            AutoReset = true
                        };
                        timer.Elapsed += Timer_Elapsed;

                        ws.Connect();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    CleanupWebSocket();
                    HandleConnectionFailure(ex.Message);
                }
            });
        }

        private void CleanupWebSocket()
        {
            if (ws != null)
            {
                ws.OnOpen -= Ws_OnOpen;
                ws.OnError -= Ws_OnError;
                ws.OnMessage -= Ws_OnMessage;
                ws.OnClose -= Ws_OnClose;
                try
                {
                    ws.Close();
                }
                catch
                {
                    // ignored: socket might already be closed
                }
                ws = null;
            }

            if (timer != null)
            {
                timer.Elapsed -= Timer_Elapsed;
                timer.Stop();
                timer.Dispose();
                timer = null;
            }
        }

        private void HandleConnectionFailure(string reason)
        {
            if (reconnectTokenSource != null)
            {
                return;
            }

            reconnectAttempts++;
            if (reconnectAttempts > MaxReconnectAttempts)
            {
                CancelReconnect();
                OnClose?.Invoke(this, string.IsNullOrEmpty(reason) ? "服务器连接失败" : reason);
                return;
            }

            OnClose?.Invoke(this, $"与服务器断开连接，正在尝试重连({reconnectAttempts}/{MaxReconnectAttempts})");
            useBackupEndpoint = !useBackupEndpoint && !string.IsNullOrEmpty(BackupUrl);
            ScheduleReconnect();
        }

        private void ScheduleReconnect()
        {
            CancelReconnect();
            reconnectTokenSource = new CancellationTokenSource();
            var token = reconnectTokenSource.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    if (!token.IsCancellationRequested)
                    {
                        await ConnectAsync(useBackupEndpoint);
                    }
                }
                catch (TaskCanceledException)
                {
                    // ignored
                }
            }, token);
        }

        private void CancelReconnect()
        {
            if (reconnectTokenSource != null)
            {
                reconnectTokenSource.Cancel();
                reconnectTokenSource.Dispose();
                reconnectTokenSource = null;
            }
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
                Debug.WriteLine(ex);
                return "00000000";
            }
        }
    }
}


