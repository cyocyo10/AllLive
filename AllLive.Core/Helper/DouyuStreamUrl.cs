using System;

namespace AllLive.Core.Helper
{
    internal static class DouyuStreamUrl
    {
        // Upstream: liuchuancong/pure_live a858550bb8753f47bc279a4e45f5cb2707cf5937.
        // Keep the original signed URL intact; only override the WS 300s lease.
        internal static string Normalize(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            var fragmentAt = url.IndexOf('#');
            var main = fragmentAt < 0 ? url : url.Substring(0, fragmentAt);
            var fragment = fragmentAt < 0 ? "" : url.Substring(fragmentAt);
            var queryAt = main.IndexOf('?');
            if (queryAt < 0) return url;
            string cdn = null, expiry = null;
            foreach (var part in main.Substring(queryAt + 1).Split('&'))
            {
                var equalsAt = part.IndexOf('=');
                if (equalsAt < 0) continue;
                var key = part.Substring(0, equalsAt);
                var value = part.Substring(equalsAt + 1);
                if (key == "fcdn") cdn = value;
                if (key == "expire") expiry = value;
            }
            if (cdn != "ws" || expiry != "300") return url;
            return main + (main.EndsWith("&", StringComparison.Ordinal) ? "" : "&") + "expire=0" + fragment;
        }
    }
}
