using System;
using System.Net.Security;
using System.Security.Authentication;
using WebSocketSharp;

namespace AllLive.Core.Helper
{
    // WebSocketSharp 1.0.3 accepts all certificates by default. Every secure
    // danmaku connection must install the platform validation result instead.
    internal static class WebSocketSecurity
    {
        internal static void Configure(WebSocket socket)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            socket.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
            socket.SslConfiguration.ServerCertificateValidationCallback =
                (sender, certificate, chain, errors) =>
                    certificate != null && errors == SslPolicyErrors.None;
        }
    }
}
