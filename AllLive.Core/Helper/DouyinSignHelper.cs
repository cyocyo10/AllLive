using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AllLive.Core.Helper
{
    /// <summary>
    /// 抖音签名辅助类，负责生成 msStub 并调用脚本运行时计算 signature。
    /// </summary>
    public static class DouyinSignHelper
    {
        private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36 Edg/125.0.0.0";
        private const int MaxSignatureAttempts = 5;

        public static async Task<string> GetSignatureAsync(string roomId, string uniqueId, string userAgent = DefaultUserAgent)
        {
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(uniqueId))
            {
                return "00000000";
            }

            return await GenerateSignatureAsync(roomId, uniqueId, userAgent).ConfigureAwait(false);
        }

        private static async Task<string> GenerateSignatureAsync(string roomId, string uniqueId, string userAgent)
        {
            try
            {
                var msStub = BuildMsStub(roomId, uniqueId);
                var signature = string.Empty;
                for (var attempt = 0; attempt < MaxSignatureAttempts; attempt++)
                {
                    signature = await DouyinScriptRuntime.Current
                        .EvaluateSignatureAsync(msStub, userAgent)
                        .ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(signature) && !signature.Contains("-") && !signature.Contains("="))
                    {
                        break;
                    }
                }

                return string.IsNullOrEmpty(signature) ? "00000000" : signature;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"DouyinSignHelper fallback: {ex}");
                return "00000000";
            }
        }

        private static string BuildMsStub(string roomId, string uniqueId)
        {
            var builder = new StringBuilder();
            builder.Append("live_id=1,aid=6383,version_code=180800,webcast_sdk_version=1.3.0,");
            builder.Append("room_id=").Append(roomId).Append(',');
            builder.Append("sub_room_id=,");
            builder.Append("sub_channel_id=,");
            builder.Append("did_rule=3,");
            builder.Append("user_unique_id=").Append(uniqueId).Append(',');
            builder.Append("device_platform=web,");
            builder.Append("device_type=,");
            builder.Append("ac=,");
            builder.Append("identity=audience");

            return ComputeMd5(builder.ToString());
        }

        private static string ComputeMd5(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = md5.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}
