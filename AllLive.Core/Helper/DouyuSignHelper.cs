using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;

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
        private const int MaximumHashRounds = 4096;

        public const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

        private static readonly SemaphoreSlim Sync = new SemaphoreSlim(1, 1);
        private static JObject _encKey;
        private static long _encKeyFetchedAtSeconds;
        private static Session _encKeySession;
        private static readonly string AnonymousDeviceId = GenerateDeviceId();
        private static Session _session = new Session(string.Empty);

        private sealed class Session
        {
            public readonly string Cookie;
            public readonly string DeviceId;
            public readonly string Header;
            public Session(string cookie)
            {
                Cookie = cookie ?? string.Empty;
                var normalized = Cookie.Trim();
                if (normalized.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
                    normalized = normalized.Substring(7).TrimStart();
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in normalized.Split(';'))
                {
                    var separator = part.IndexOf('=');
                    if (separator <= 0 || part.Any(char.IsControl)) continue;
                    var name = part.Substring(0, separator).Trim();
                    var value = part.Substring(separator + 1).Trim();
                    if (name.Length > 0 && !fields.ContainsKey(name)) fields[name] = value;
                }
                string did;
                if (!fields.TryGetValue("dy_did", out did) || string.IsNullOrWhiteSpace(did))
                    fields.TryGetValue("acf_did", out did);
                DeviceId = string.IsNullOrWhiteSpace(did) ? AnonymousDeviceId : did;
                fields["dy_did"] = DeviceId;
                fields["acf_did"] = DeviceId;
                Header = string.Join("; ", fields.Select(field => field.Key + "=" + field.Value));
            }
        }

        /// <summary>Changing the cookie replaces the entire signing session and invalidates its descriptor.</summary>
        public static string AccountCookie
        {
            get => Volatile.Read(ref _session).Cookie;
            set => Interlocked.Exchange(ref _session, new Session(value));
        }
        public static string DeviceId => Volatile.Read(ref _session).DeviceId;

        public sealed class PlayRequest
        {
            public string Form { get; internal set; }
            public Dictionary<string, string> Headers { get; internal set; }
            internal object Identity { get; set; }
            public bool IsCurrent => ReferenceEquals(Identity, Volatile.Read(ref _session));
        }

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
        public static Dictionary<string, string> RequestHeaders(string roomId = "") =>
            RequestHeaders(Volatile.Read(ref _session), roomId);

        private static Dictionary<string, string> RequestHeaders(Session session, string roomId = "")
        {
            return new Dictionary<string, string>
            {
                { "accept", "application/json, text/plain, */*" },
                { "origin", "https://www.douyu.com" },
                { "referer", "https://www.douyu.com/" + roomId },
                { "user-agent", UserAgent },
                { "cookie", session.Header },
            };
        }

        public static string CookieHeader() => Volatile.Read(ref _session).Header;

        private static bool IsEncryptionKeyUsable(JObject key, long nowSeconds, int safetySeconds = ExpirySafetySeconds)
        {
            if (key == null) return false;
            if (!long.TryParse(key["expire_at"]?.ToString(), out var expiresAt) ||
                !int.TryParse(key["enc_time"]?.ToString(), out var encTime)) return false;
            return expiresAt > nowSeconds + safetySeconds
                && encTime > 0
                && encTime <= MaximumHashRounds
                && !string.IsNullOrEmpty(key["key"]?.ToString())
                && !string.IsNullOrEmpty(key["rand_str"]?.ToString())
                && !string.IsNullOrEmpty(key["enc_data"]?.ToString());
        }

        private static async Task<JObject> EnsureEncryptionKeyAsync(Session session, bool forceRefresh = false)
        {
            await Sync.WaitAsync().ConfigureAwait(false);
            try
            {
                var now = NowSeconds();
                if (!forceRefresh &&
                    ReferenceEquals(_encKeySession, session) &&
                    now >= _encKeyFetchedAtSeconds &&
                    now - _encKeyFetchedAtSeconds < MaximumCacheAgeSeconds &&
                    IsEncryptionKeyUsable(_encKey, now))
                {
                    return _encKey;
                }

                var json = await HttpUtil.GetString(
                    ApiGetEncryption,
                    RequestHeaders(session),
                    new Dictionary<string, string> { { "did", session.DeviceId } }, useCookies: false).ConfigureAwait(false);
                var root = JObject.Parse(json);
                var code = root["error"] ?? root["code"];
                if (code != null && (!int.TryParse(code.ToString(), out var error) || error != 0))
                    throw new Exception("斗鱼签名失败 [sign_api]：描述符接口未返回成功状态");
                var data = root["data"] as JObject;
                if (data == null || !IsEncryptionKeyUsable(data, NowSeconds()))
                {
                    if (long.TryParse(data?["expire_at"]?.ToString(), out var expiresAt) && expiresAt <= NowSeconds() + ExpirySafetySeconds)
                        throw new Exception($"斗鱼签名失败 [sign_expired]：描述符剩余秒数 {expiresAt - NowSeconds()}，请检查系统时间");
                    if (int.TryParse(data?["enc_time"]?.ToString(), out var rounds) && (rounds < 1 || rounds > MaximumHashRounds))
                        throw new Exception($"斗鱼签名失败 [sign_rounds]：签名轮数 {rounds} 超出支持范围");
                    throw new Exception("斗鱼签名失败 [sign_fields]：描述符字段无效");
                }
                _encKey = data;
                _encKeyFetchedAtSeconds = NowSeconds();
                _encKeySession = session;
                return data;
            }
            finally
            {
                Sync.Release();
            }
        }

        /// <summary>
        /// 构建 getH5PlayV1 的表单字符串，由 BuildQueryString 编码一次。
        /// </summary>
        public static async Task<string> BuildFormAsync(string roomId, int rate = -1, string cdn = "", bool forceRefresh = false)
        {
            return (await BuildRequestAsync(roomId, rate, cdn, forceRefresh).ConfigureAwait(false)).Form;
        }

        public static async Task<PlayRequest> BuildRequestAsync(string roomId, int rate = -1, string cdn = "", bool forceRefresh = false)
        {
            var session = Volatile.Read(ref _session);
            JObject key;
            try { key = await EnsureEncryptionKeyAsync(session, forceRefresh).ConfigureAwait(false); }
            catch (HttpRequestException) { throw new Exception("斗鱼签名失败 [sign_http]：描述符请求失败"); }
            catch (Newtonsoft.Json.JsonException) { throw new Exception("斗鱼签名失败 [sign_json]：描述符响应格式无效"); }
            if (!ReferenceEquals(session, Volatile.Read(ref _session)))
                throw new Exception("斗鱼签名失败 [session_changed]：账号已切换，请重试");
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
                { "did", session.DeviceId },
                { "auth", auth },
                { "cdn", cdn ?? string.Empty },
                { "rate", rate.ToString() },
                { "hevc", "0" },
                { "fa", "0" },
                { "ive", "0" },
                { "ver", "Douyu_new" },
                { "iar", "0" },
            };
            return new PlayRequest { Form = Utils.BuildQueryString(fields), Headers = RequestHeaders(session, roomId), Identity = session };
        }
    }
}
