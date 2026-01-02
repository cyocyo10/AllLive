using System;
using System.Threading.Tasks;

namespace AllLive.Core.Helper
{
    /// <summary>
    /// 使用 DouyinScriptRuntime 生成 a_bogus，避免直接依赖外部服务。
    /// </summary>
    public static class DouyinABogusHelper
    {
        public static async Task<string> GenerateAsync(string queryString, string userAgent)
        {
            try
            {
                return await DouyinScriptRuntime.Current
                    .GenerateABogusAsync(queryString, userAgent)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"DouyinABogusHelper fallback: {ex}");
                return string.Empty;
            }
        }
    }
}
