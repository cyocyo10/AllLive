using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AllLive.Core.Danmaku;
using AllLive.Core.Helper;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Linq;

/*
 * 斗鱼取流签名参考 pure_live 纯 Dart 实现(getEncryption 描述符 + MD5):
 * https://github.com/liuchuancong/pure_live/blob/master/lib/core/site/douyu/douyu_utils.dart
 */
namespace AllLive.Core
{
    public class Douyu : ILiveSite
    {
        public string Name => "斗鱼直播";
        public ILiveDanmaku GetDanmaku() => new DouyuDanmaku();
        public async Task<List<LiveCategory>> GetCategores()
        {
            List<LiveCategory> categories = new List<LiveCategory>();
            var result = await HttpUtil.GetString("https://m.douyu.com/api/cate/list");
            var obj = JObject.Parse(result);
            var cate1 = obj["data"]?["cate1Info"] as JArray;
            var cate2 = obj["data"]?["cate2Info"] as JArray;
            if (cate1 == null) return new List<LiveCategory>();
            foreach (var item in cate1)
            {
                var cate1Id = item["cate1Id"]?.ToString() ?? "";
                var cate1Name = item["cate1Name"]?.ToString() ?? "";
                List<LiveSubCategory> subCategories = new List<LiveSubCategory>();
                if (cate2 != null)
                {
                    cate2.Where(x => x["cate1Id"]?.ToString() == cate1Id).ToList().ForEach(element =>
                    {
                        subCategories.Add(new LiveSubCategory()
                        {
                            Pic = element["icon"]?.ToString() ?? "",
                            ID = element["cate2Id"]?.ToString() ?? "",
                            ParentID = cate1Id,
                            Name = element["cate2Name"]?.ToString() ?? "",
                        });
                    });
                }
               
                categories.Add(
                  new LiveCategory()
                  {
                      ID = cate1Id,
                      Name = cate1Name,
                      // 只取前30个子分类
                      Children = subCategories.Take(30).ToList()
                  }
                );
            }
            categories.Sort((x, y) => x.ID.CompareTo(y.ID));
            return categories;
        }

      
        public async Task<LiveCategoryResult> GetCategoryRooms(LiveSubCategory category, int page = 1)
        {
            LiveCategoryResult categoryResult = new LiveCategoryResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            var result = await HttpUtil.GetString($"https://www.douyu.com/gapi/rkc/directory/mixList/2_{ category.ID}/{page}");
            var obj = JObject.Parse(result);

            var rl = obj["data"]?["rl"] as JArray;
            if (rl != null)
            {
                foreach (var item in rl)
                {
                    if ((item["type"]?.ToObject<int>() ?? 0) == 1)
                        categoryResult.Rooms.Add(new LiveRoomItem()
                        {
                            Cover = item["rs16"]?.ToString() ?? "",
                            Online = item["ol"]?.ToObject<int>() ?? 0,
                            RoomID = item["rid"]?.ToString() ?? "",
                            Title = item["rn"]?.ToString() ?? "",
                            UserName = item["nn"]?.ToString() ?? "",
                        });
                }
            }
            categoryResult.HasMore = page < (obj["data"]?["pgcnt"]?.ToObject<int>() ?? 0);
            return categoryResult;
        }
        public async Task<LiveCategoryResult> GetRecommendRooms(int page = 1)
        {
            LiveCategoryResult categoryResult = new LiveCategoryResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            var result = await HttpUtil.GetString($"https://www.douyu.com/japi/weblist/apinc/allpage/6/{page}");
            var obj = JObject.Parse(result);
            var recRl = obj["data"]?["rl"] as JArray;
            if (recRl != null)
            {
                foreach (var item in recRl)
                {
                    categoryResult.Rooms.Add(new LiveRoomItem()
                    {
                        Cover = item["rs16"]?.ToString() ?? "",
                        Online = item["ol"]?.ToObject<int>() ?? 0,
                        RoomID = item["rid"]?.ToString() ?? "",
                        Title = item["rn"]?.ToString() ?? "",
                        UserName = item["nn"]?.ToString() ?? "",
                    });
                }
            }
            categoryResult.HasMore = page < (obj["data"]?["pgcnt"]?.ToObject<int>() ?? 0);
            return categoryResult;
        }
        public async Task<LiveRoomDetail> GetRoomDetail(object roomId)
        {
            var roomInfo = await GetRoomInfo(roomId.ToString());

            return new LiveRoomDetail()
            {
                Cover = roomInfo["room_pic"].ToString(),
                Online = ParseHotNum(roomInfo["room_biz_all"]["hot"].ToString()),
                RoomID = roomInfo["room_id"].ToString(),
                Title = roomInfo["room_name"].ToString(),
                UserName = roomInfo["owner_name"].ToString(),
                UserAvatar = roomInfo["owner_avatar"].ToString(),
                Introduction =roomInfo["show_details"].ToString(),
                Notice = "",
                Status = roomInfo["show_status"].ToInt32() == 1 && roomInfo["videoLoop"].ToInt32() != 1,
                DanmakuData = roomInfo["room_id"].ToString(),
                Data = roomInfo["room_id"].ToString(),
                Url = "https://www.douyu.com/" + roomId,
                IsRecord= roomInfo["videoLoop"].ToInt32() == 1,
            };
        }


        private async Task<JToken> GetRoomInfo(string roomId)
        {
            var result = await HttpUtil.GetString($"https://www.douyu.com/betard/{roomId}", new Dictionary<string, string>()
            {
                { "referer", $"https://www.douyu.com/{roomId}"},
                { "user-agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36 Edg/114.0.1823.43" },
            });
            var obj = JObject.Parse(result);
            return obj["room"];
        }

        public async Task<LiveSearchResult> Search(string keyword, int page = 1)
        {
            LiveSearchResult searchResult = new LiveSearchResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            var result = await HttpUtil.GetString($"https://www.douyu.com/japi/search/api/searchShow?kw={ Uri.EscapeDataString(keyword)}&page={ page}&pageSize=20");
            var obj = JObject.Parse(result);

            var relateShow = obj["data"]?["relateShow"] as JArray;
            if (relateShow != null)
            {
                foreach (var item in relateShow)
                {
                    searchResult.Rooms.Add(new LiveRoomItem()
                    {
                        Cover = item["roomSrc"]?.ToString() ?? "",
                        Online = ParseHotNum(item["hot"]?.ToString() ?? "0"),
                        RoomID = item["rid"]?.ToString() ?? "",
                        Title = item["roomName"]?.ToString() ?? "",
                        UserName = item["nickName"]?.ToString() ?? "",
                    });
                }
                searchResult.HasMore = relateShow.Count > 0;
            }
            return searchResult;
        }
        public async Task<List<LivePlayQuality>> GetPlayQuality(LiveRoomDetail roomDetail)
        {
            List<LivePlayQuality> qualities = new List<LivePlayQuality>();
            var playData = await RequestPlayData(roomDetail.RoomID, rate: -1);
            var cdns = ParseCdnCodes(playData);
            // scdn 线路稳定性较差，移到列表末尾
            var normalCdns = cdns.Where(c => !c.StartsWith("scdn")).ToList();
            normalCdns.AddRange(cdns.Where(c => c.StartsWith("scdn")));
            cdns = normalCdns;

            var multirates = playData["multirates"] as JArray;
            if (multirates != null)
            {
                foreach (var item in multirates)
                {
                    qualities.Add(new LivePlayQuality()
                    {
                        Quality = item["name"]?.ToString() ?? "",
                        Data = new KeyValuePair<int, List<string>>(item["rate"]?.ToObject<int>() ?? 0, cdns),
                    });
                }
            }
            return qualities;
        }
        public async Task<List<string>> GetPlayUrls(LiveRoomDetail roomDetail, LivePlayQuality qn)
        {
            var data = (KeyValuePair<int, List<string>>)qn.Data;
            List<string> urls = new List<string>();
            var tasks = data.Value.Select(item => GetUrl(roomDetail.RoomID, data.Key, item)).ToArray();
            var results = await Task.WhenAll(tasks);
            urls.AddRange(results.Where(u => !string.IsNullOrEmpty(u)));
            System.Diagnostics.Trace.WriteLine($"[Douyu.GetPlayUrls] fetched {urls.Count} URLs in parallel from {data.Value.Count} CDNs");
            return urls;
        }

        private async Task<string> GetUrl(string rid, int rate, string cdn = "")
        {
            try
            {
                var playData = await RequestPlayData(rid, rate, cdn);
                return ParsePlayUrl(playData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Douyu] GetUrl failed for rid={rid}: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// 请求 getH5PlayV1 取流接口。斗鱼 H5 流地址带 wsAuth 短签名(5 分钟)，
        /// 失败重试时强制刷新加密描述符重新签名。
        /// </summary>
        private async Task<JObject> RequestPlayData(string rid, int rate = -1, string cdn = "", bool forceRefresh = false)
        {
            Exception lastError = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var form = await DouyuSignHelper.BuildFormAsync(rid, rate, cdn, forceRefresh: attempt > 0 || forceRefresh);
                    var result = await HttpUtil.PostFormUrlEncodedString(
                        $"https://www.douyu.com/lapi/live/getH5PlayV1/{rid}",
                        form,
                        DouyuSignHelper.RequestHeaders(rid));
                    var obj = JObject.Parse(result);
                    var errorCode = obj["error"]?.ToObject<int>() ?? obj["code"]?.ToObject<int>() ?? -1;
                    if (errorCode != 0)
                    {
                        throw new Exception($"斗鱼取流接口返回错误 {errorCode}: {obj["msg"]}");
                    }
                    var data = obj["data"] as JObject;
                    if (data == null)
                    {
                        throw new Exception("斗鱼取流接口响应缺少 data");
                    }
                    return data;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }
            throw new Exception("斗鱼取流请求失败", lastError);
        }

        private static List<string> ParseCdnCodes(JObject data)
        {
            var result = new List<string>();
            var cdnsWithName = data["cdnsWithName"] as JArray;
            if (cdnsWithName != null)
            {
                foreach (var item in cdnsWithName)
                {
                    var code = item["cdn"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(code) && !result.Contains(code))
                    {
                        result.Add(code);
                    }
                }
            }
            var current = data["rtmp_cdn"]?.ToString()?.Trim() ?? "";
            if (!string.IsNullOrEmpty(current) && !result.Contains(current))
            {
                result.Insert(0, current);
            }
            if (result.Count == 0)
            {
                result.Add("");
            }
            return result;
        }

        /// <summary>
        /// 解析播放地址。rtmp_live 可能是完整签名地址或相对路径(需与 rtmp_url/flv_url 拼接)，
        /// 裸 CDN 目录不是合法播放输入，必须拦截。
        /// </summary>
        private static string ParsePlayUrl(JObject data)
        {
            var live = System.Net.WebUtility.HtmlDecode(data["rtmp_live"]?.ToString()?.Trim() ?? "");
            if (IsPlayableUrl(live)) return live;

            foreach (var baseKey in new[] { "rtmp_url", "flv_url" })
            {
                var baseUrl = System.Net.WebUtility.HtmlDecode(data[baseKey]?.ToString()?.Trim() ?? "");
                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(live)) continue;
                var combined = baseUrl.TrimEnd('/') + "/" + live.TrimStart('/');
                if (IsPlayableUrl(combined)) return combined;
            }

            foreach (var key in new[] { "player_1", "stream_url", "url" })
            {
                var value = System.Net.WebUtility.HtmlDecode(data[key]?.ToString()?.Trim() ?? "");
                if (IsPlayableUrl(value)) return value;
            }

            var flvUrl = System.Net.WebUtility.HtmlDecode(data["flv_url"]?.ToString()?.Trim() ?? "");
            if (IsDirectMediaUrl(flvUrl)) return flvUrl;
            return "";
        }

        private static bool IsPlayableUrl(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri)) return false;
            return !string.IsNullOrEmpty(uri.Host) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "rtmp");
        }

        private static bool IsDirectMediaUrl(string value)
        {
            if (!IsPlayableUrl(value)) return false;
            var path = new Uri(value).AbsolutePath.ToLowerInvariant();
            return path.EndsWith(".flv") || path.EndsWith(".m3u8") || path.EndsWith(".mp4");
        }
        public async Task<LiveStatusType> GetLiveStatus(object roomId)
        {
            var roomInfo = await GetRoomInfo(roomId.ToString());
            var showStatus = roomInfo["show_status"].ToInt32();
            var videoLoop = roomInfo["videoLoop"].ToInt32();
            
            if (showStatus == 1 && videoLoop != 1)
                return LiveStatusType.Live;
            if (videoLoop == 1)
                return LiveStatusType.Replay;
            return LiveStatusType.Offline;
        }
        public Task<List<LiveSuperChatMessage>> GetSuperChatMessages(object roomId)
        {
            return Task.FromResult(new List<LiveSuperChatMessage>());
        }

        private int ParseHotNum(string hn)
        {
            try
            {
                var num = double.Parse(hn.Replace("万", ""));
                if (hn.Contains("万"))
                {
                    num = num * 10000;
                }
                return int.Parse(num.ToString());
            }
            catch (Exception)
            {
                return -999;
            }

        }
    }


}

