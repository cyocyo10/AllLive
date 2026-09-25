using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;

namespace AllLive.Core.Helper
{
    public static class HttpUtil
    {
        // 复用 HttpClient 实例，避免 Socket 耗尽
        private static readonly Lazy<HttpClient> _sharedClient = new Lazy<HttpClient>(() =>
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            return client;
        });

        private static HttpClient SharedClient => _sharedClient.Value;

        public static async Task<string> GetString(string url, IDictionary<string, string> headers = null, IDictionary<string, string> queryParameters = null)
        {
            if (queryParameters != null && queryParameters.Count > 0)
            {
                var sb = new StringBuilder(url);
                sb.Append("?");
                foreach (var item in queryParameters)
                {
                    sb.Append(item.Key);
                    sb.Append("=");
                    sb.Append(Uri.EscapeDataString(item.Value));
                    sb.Append("&");
                }
                sb.Length--; // remove trailing &
                url = sb.ToString();
            }

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                    }
                }
                using (var result = await SharedClient.SendAsync(request).ConfigureAwait(false))
                {
                    result.EnsureSuccessStatusCode();
                    return await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 返回原始 HttpResponseMessage，调用方负责 Dispose。
        /// 推荐用 using 包裹返回值。
        /// </summary>
        public static async Task<HttpResponseMessage> Get(string url, IDictionary<string, string> headers = null, IDictionary<string, string> queryParameters = null)
        {
            if (queryParameters != null && queryParameters.Count > 0)
            {
                var sb = new StringBuilder(url);
                sb.Append("?");
                foreach (var item in queryParameters)
                {
                    sb.Append(item.Key);
                    sb.Append("=");
                    sb.Append(Uri.EscapeDataString(item.Value));
                    sb.Append("&");
                }
                sb.Length--; // remove trailing &
                url = sb.ToString();
            }

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                    }
                }
                var result = await SharedClient.SendAsync(request).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
                return result;
            }
        }

        public static async Task<string> GetUtf8String(string url, IDictionary<string, string> headers = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                    }
                }
                using (var result = await SharedClient.SendAsync(request).ConfigureAwait(false))
                {
                    result.EnsureSuccessStatusCode();
                    return Encoding.UTF8.GetString(await result.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
                }
            }
        }

        public static async Task<string> PostString(string url, string data, IDictionary<string, string> headers = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                    }
                }
                List<KeyValuePair<string, string>> body = new List<KeyValuePair<string, string>>();
                foreach (var item in data.Split('&'))
                {
                    var splits = item.Split(new char[] { '=' }, 2);
                    body.Add(new KeyValuePair<string, string>(splits[0], splits.Length > 1 ? splits[1] : ""));
                }
                request.Content = new FormUrlEncodedContent(body);
                using (var result = await SharedClient.SendAsync(request).ConfigureAwait(false))
                {
                    result.EnsureSuccessStatusCode();
                    return await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// POST 预编码的表单字符串(application/x-www-form-urlencoded)，内容原样发送，不做二次编码。
        /// </summary>
        public static async Task<string> PostFormUrlEncodedString(string url, string formData, IDictionary<string, string> headers = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                    }
                }
                request.Content = new StringContent(formData, Encoding.UTF8, "application/x-www-form-urlencoded");
                using (var result = await SharedClient.SendAsync(request).ConfigureAwait(false))
                {
                    result.EnsureSuccessStatusCode();
                    return await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        public static async Task<string> PostJsonString(string url, string data, IDictionary<string, string> headers = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                    }
                }
                request.Content = new StringContent(data, Encoding.UTF8, "application/json");
                using (var result = await SharedClient.SendAsync(request).ConfigureAwait(false))
                {
                    result.EnsureSuccessStatusCode();
                    return await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 返回原始 HttpResponseMessage，调用方负责 Dispose。
        /// </summary>
        public static async Task<HttpResponseMessage> Head(string url, IDictionary<string, string> headers = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Head, url))
            {
                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                    }
                }
                var response = await SharedClient.SendAsync(request).ConfigureAwait(false);
                return response;
            }
        }
    }
}
