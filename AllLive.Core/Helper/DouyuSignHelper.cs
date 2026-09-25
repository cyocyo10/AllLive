using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AllLive.Core.Helper
{
    /// <summary>
    /// 斗鱼 H5 取流签名：getEncryption 服务端描述符 + 纯托管 MD5，无 JS 依赖。
    /// 斗鱼 H5 流地址为 wsAuth 短签名(5 分钟) URL，断流重连需重新走签名取流。
    /// </summary>
    public static class DouyuSignHelper
    {
        private const string ApiGetEncryption = "https://www.douyu.com/wgapi/livenc/liveweb/websec/getEncryption";
        private const int ExpirySafetySeconds = 30;
        private const int MaximumCacheAgeSeconds = 5 * 60;

        public const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

        private static readonly SemaphoreSlim Sync = new SemaphoreSlim(1, 1);
        private static JObject _encKey;
        private static long _encKeyFetchedAtSeconds;
        private static string _sessionDeviceId = GenerateDeviceId();

        /// <summary>
        /// 可选的斗鱼账号 Cookie(浏览器登录斗鱼后复制整段 Cookie)，为空时保持匿名取流。
        /// 粘贴值中的 dy_did/acf_did 会被进程级签名 did 取代，避免会话不一致。
        /// </summary>
        public static string AccountCookie { get; set; } = string.Empty;

        /// <summary>进程级随机设备 ID，签名与请求 Cookie 始终一致。</summary>
        public static string DeviceId => _sessionDeviceId;

        private static long NowSeconds() => Utils.GetTimestamp();

        public static string GenerateDeviceId()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            var sb = new StringBuilder(32);
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>组装请求头，Cookie 中的 dy_did/acf_did 始终与签名 did 一致。</summary>
        public static Dictionary<string, string> RequestHeaders(string roomId = "")
        {
            var referer = string.IsNullOrEmpty(roomId) ? "https://www.douyu.com/" : $"https://www.douyu.com/{roomId}";
            return new Dictionary<string, string>
            {
                { "accept", "application/json, text/plain, */*" },
                { "origin", "https://www.douyu.com" },
                { "referer", referer },
                { "user-agent", UserAgent },
                { "cookie", CookieHeader() },
            };
        }

        public static string CookieHeader()
        {
            var fields = new List<string> { $"dy_did={DeviceId}", $"acf_did={DeviceId}" };
            var normalized = (AccountCookie ?? string.Empty).Trim();
            if (normalized.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("Cookie:".Length).TrimStart();
            }
            foreach (var piece in normalized.Split(';'))
            {
                var sep = piece.IndexOf('=');
                if (sep <= 0) continue;
                var name = piece.Substring(0, sep).Trim();
                if (name.Length == 0) continue;
                // dy_did/acf_did 必须与签名 did 一致，丢弃粘贴值中的旧 did
                if (string.Equals(name, "dy_did", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "acf_did", StringComparison.OrdinalIgnoreCase)) continue;
                fields.Add($"{name}={piece.Substring(sep + 1).Trim()}");
            }
            return string.Join("; ", fields);
        }

        private static bool IsEncryptionKeyUsable(JObject key, long nowSeconds, int safetySeconds = ExpirySafetySeconds)
        {
            if (key == null) return false;
            var expiresAt = key["expire_at"]?.ToObject<long>() ?? 0;
            var encTime = key["enc_time"]?.ToObject<long>() ?? 0;
            return expiresAt > nowSeconds + safetySeconds
                && encTime > 0
                && encTime <= 16
                && !string.IsNullOrEmpty(key["key"]?.ToString())
                && !string.IsNullOrEmpty(key["rand_str"]?.ToString())
                && !string.IsNullOrEmpty(key["enc_data"]?.ToString());
        }

        private static async Task EnsureEncryptionKeyAsync(bool forceRefresh = false)
        {
            var now = NowSeconds();
            if (!forceRefresh &&
                now - _encKeyFetchedAtSeconds < MaximumCacheAgeSeconds &&
                IsEncryptionKeyUsable(_encKey, now))
            {
                return;
            }

            await Sync.WaitAsync().ConfigureAwait(false);
            try
            {
                now = NowSeconds();
                if (!forceRefresh &&
                    now - _encKeyFetchedAtSeconds < MaximumCacheAgeSeconds &&
                    IsEncryptionKeyUsable(_encKey, now))
                {
                    return;
                }

                var json = await HttpUtil.GetString(
                    ApiGetEncryption,
                    RequestHeaders(),
                    new Dictionary<string, string> { { "did", DeviceId } }).ConfigureAwait(false);
                var data = JObject.Parse(json)?["data"] as JObject;
                if (data == null || !IsEncryptionKeyUsable(data, NowSeconds()))
                {
                    throw new Exception("斗鱼加密描述符无效或已过期");
                }
                _encKey = data;
                _encKeyFetchedAtSeconds = NowSeconds();
            }
            finally
            {
                Sync.Release();
            }
        }

        /// <summary>
        /// 构建 getH5PlayV1 的表单字符串。值不做预编码，由 PostFormAsync 统一编码一次。
        /// </summary>
        public static async Task<string> BuildFormAsync(string roomId, int rate = -1, string cdn = "", bool forceRefresh = false)
        {
            await EnsureEncryptionKeyAsync(forceRefresh).ConfigureAwait(false);
            var key = _encKey;
            var randStr = key["rand_str"].ToString();
            var encTime = key["enc_time"].ToObject<int>();
            var isSpecial = key["is_special"]?.ToObject<int>() ?? 0;
            var tt = NowSeconds();
            var salt = isSpecial == 1 ? string.Empty : roomId + tt.ToString();

            var secret = randStr;
            for (var i = 0; i < encTime; i++)
            {
                secret = Utils.ToMD5(secret + key["key"]);
            }
            var auth = Utils.ToMD5(secret + key["key"] + salt);

            var fields = new Dictionary<string, string>
            {
                { "enc_data", key["enc_data"].ToString() },
                { "tt", tt.ToString() },
                { "did", DeviceId },
                { "auth", auth },
                { "cdn", cdn ?? string.Empty },
                { "rate", rate.ToString() },
                { "hevc", "0" },
                { "fa", "0" },
                { "ive", "0" },
                { "ver", "Douyu_new" },
                { "iar", "0" },
            };
            return Utils.BuildQueryString(fields);
        }
    }
}
