using System.Threading.Tasks;

namespace AllLive.Core.Interface
{
    /// <summary>
    /// 抖音验证处理接口
    /// </summary>
    public interface IDouyinVerifyHandler
    {
        /// <summary>
        /// 执行验证，返回验证后的 Cookie
        /// </summary>
        /// <param name="url">需要验证的页面 URL</param>
        /// <returns>验证成功后的 Cookie，失败返回 null</returns>
        Task<string> VerifyAsync(string url);
    }
}
