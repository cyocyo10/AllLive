using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

// Test-only dependencies. No HTTP client, TCP socket, DNS, or real WebSocket exists here.
// The shared repaired DouyinDanmaku and TLS helper are linked directly from ../source.
namespace AllLive.Core.Helper
{
    public static class DouyinSignHelper
    {
        public static int Calls;
        public static Task<string> GetSignatureAsync(string roomId, string uniqueId)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromException<string>(new InvalidOperationException("OFFLINE: use the injected signature fixture"));
        }
    }
    public static class HttpUtil
    {
        public static int Calls;
        public static Task<string> PostJsonString(string url, string body)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromException<string>(new InvalidOperationException("OFFLINE: HTTP is disabled"));
        }
    }
}

namespace WebSocketSharp
{
    public enum WebSocketState { Connecting, Open, Closing, Closed }
    public sealed class SslConfiguration
    {
        public SslProtocols EnabledSslProtocols { get; set; }
        public RemoteCertificateValidationCallback ServerCertificateValidationCallback { get; set; }
    }
    public sealed class MessageEventArgs : EventArgs { public byte[] RawData { get; set; } = Array.Empty<byte>(); }
    public sealed class CloseEventArgs : EventArgs
    {
        public ushort Code { get; set; }
        public string Reason { get; set; }
    }
    public sealed class ErrorEventArgs : EventArgs { public string Message { get; set; } }

    public sealed class WebSocket
    {
        public static readonly ConcurrentQueue<WebSocket> Instances = new();
        public static readonly ConcurrentQueue<string> ConnectUrls = new();
        public static Action<WebSocket> ConnectBehavior;
        public static int ConnectCalls;
        public static int CloseCalls;
        public static int SendCalls;
        public static void Reset(Action<WebSocket> connectBehavior)
        {
            Instances.Clear();
            ConnectUrls.Clear();
            ConnectCalls = CloseCalls = SendCalls = 0;
            ConnectBehavior = connectBehavior;
        }

        public int InstanceSendCalls;
        public string Url { get; }
        public WebSocketState ReadyState { get; private set; } = WebSocketState.Connecting;
        public Dictionary<string, string> CustomHeaders { get; set; }
        public SslConfiguration SslConfiguration { get; } = new();
        public event EventHandler OnOpen;
        public event EventHandler<MessageEventArgs> OnMessage;
        public event EventHandler<CloseEventArgs> OnClose;
        public event EventHandler<ErrorEventArgs> OnError;

        public WebSocket(string url)
        {
            Url = url;
            Instances.Enqueue(this);
        }
        public void Connect()
        {
            ConnectUrls.Enqueue(Url);
            Interlocked.Increment(ref ConnectCalls);
            (ConnectBehavior ?? throw new InvalidOperationException("Missing explicit fake connect behavior"))(this);
        }
        // Capture handlers before unsubscribe to model callbacks already queued by a transport.
        public Action CaptureQueuedEvents()
        {
            var opened = OnOpen;
            var error = OnError;
            var closed = OnClose;
            var message = OnMessage;
            return () =>
            {
                opened?.Invoke(this, EventArgs.Empty);
                error?.Invoke(this, new ErrorEventArgs { Message = "queued old error" });
                closed?.Invoke(this, new CloseEventArgs { Code = 1006, Reason = "queued old close" });
                message?.Invoke(this, new MessageEventArgs { RawData = Array.Empty<byte>() });
            };
        }
        public void Open()
        {
            ReadyState = WebSocketState.Open;
            OnOpen?.Invoke(this, EventArgs.Empty);
        }
        public void Error(string text) => OnError?.Invoke(this, new ErrorEventArgs { Message = text });
        public void Emit(byte[] bytes) => OnMessage?.Invoke(this, new MessageEventArgs { RawData = bytes });
        public void Close()
        {
            ReadyState = WebSocketState.Closed;
            Interlocked.Increment(ref CloseCalls);
            OnClose?.Invoke(this, new CloseEventArgs { Code = 1000, Reason = "fake close" });
        }
        public void Send(byte[] bytes)
        {
            if (ReadyState != WebSocketState.Open) throw new InvalidOperationException("Fake WebSocket is not open");
            Interlocked.Increment(ref SendCalls);
            Interlocked.Increment(ref InstanceSendCalls);
        }
    }
}

namespace AllLive.Core.Danmaku.Proto
{
    public sealed class PushFrame
    {
        public ulong? logId { get; set; }
        public string payloadType { get; set; }
        public byte[] Payload { get; set; }
    }
    public sealed class Response
    {
        public bool? needAck { get; set; }
        public string internalExt { get; set; }
        public List<Message> messagesLists { get; set; } = new();
    }
    public sealed class Message
    {
        public string Method { get; set; }
        public byte[] Payload { get; set; }
    }
    public sealed class ChatMessage
    {
        public string Content { get; set; }
        public User User { get; set; }
    }
    public sealed class User { public string nickName { get; set; } }
    public sealed class RoomUserSeqMessage { public long totalUser { get; set; } }
}

namespace ProtoBuf
{
    public static class Serializer
    {
        // Heartbeat serialization is a harmless marker in lifecycle tests.
        // This deliberately makes no claim about actual protobuf compatibility.
        public static void Serialize(Stream stream, object value) => stream.WriteByte(0);
        public static Func<Type, object> DeserializeFixture;
        public static int DeserializeCalls;
        public static T Deserialize<T>(Stream stream)
        {
            Interlocked.Increment(ref DeserializeCalls);
            return (T)(DeserializeFixture ?? throw new NotSupportedException("A decoding fixture is required"))(typeof(T));
        }
    }
}
