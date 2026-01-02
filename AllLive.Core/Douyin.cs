using AllLive.Core.Danmaku;
using AllLive.Core.Helper;
using AllLive.Core.Interface;
using AllLive.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using System.Security.Cryptography;

namespace AllLive.Core
{
    public class Douyin : ILiveSite
    {
        public string Name => "抖音直播";
        public ILiveDanmaku GetDanmaku() => new DouyinDanmaku();

        private const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36 Edg/125.0.0.0";
        private const string REFERER = "https://live.douyin.com";
        private const string AUTHORITY = "live.douyin.com";

        Dictionary<string, string> headers = new Dictionary<string, string>
        {
            { "User-Agent", USER_AGENT },
            { "Referer", REFERER },
            { "Authority", AUTHORITY }
        };

        private async Task<Dictionary<string, string>> GetRequestHeaders(bool forceRefresh = false)
        {
            if (!forceRefresh && (headers.ContainsKey("Cookie") || headers.ContainsKey("cookie")))
            {
                return headers;
            }
            
            // 强制刷新时，先清除旧的 Cookie，避免使用过期的 __ac_nonce
            if (forceRefresh)
            {
                headers.Remove("Cookie");
                headers.Remove("cookie");
            }
            
            try
            {
                // 创建一个不带 Cookie 的 headers 副本用于请求，确保获取全新的 Cookie
                var requestHeaders = new Dictionary<string, string>
                {
                    { "User-Agent", USER_AGENT },
                    { "Referer", REFERER },
                    { "Authority", AUTHORITY }
                };
                
                var resp = await HttpUtil.Head("https://live.douyin.com", requestHeaders);
                var cookieBuilder = new StringBuilder();
                foreach (var item in resp.Headers.GetValues("Set-Cookie"))
                {
                    var cookie = item.Split(';')[0];
                    if (cookie.Contains("ttwid") || cookie.Contains("__ac_nonce") || cookie.Contains("msToken"))
                    {
                        cookieBuilder.Append(cookie).Append(';');
                    }
                }
                if (cookieBuilder.Length > 0)
                {
                    headers["Cookie"] = cookieBuilder.ToString().TrimEnd(';');
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return headers;
        }

        public async Task<List<LiveCategory>> GetCategores()
        {
            List<LiveCategory> categories = new List<LiveCategory>();
            var resp = await HttpUtil.GetString("https://live.douyin.com/", await GetRequestHeaders());

            Regex regex = new Regex("\\{\\\\\"pathname\\\\\":\\\\\"\\/\\\\\",\\\\\"categoryData.*?\\]\\\\n", RegexOptions.Singleline);
            Match match = regex.Match(resp);
            string renderData = match.Success ? match.Groups[0].Value : "";
            if (string.IsNullOrEmpty(renderData))
            {
                throw new Exception("无法读取分类数据");
            }
            renderData = renderData.Trim().Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("]\\n", "");
            // 解析JSON数据
            var renderDataJson = JObject.Parse(renderData);
            foreach (var item in renderDataJson["categoryData"])
            {
                List<LiveSubCategory> subs = new List<LiveSubCategory>();
                var id = $"{item["partition"]["id_str"]},{item["partition"]["type"]}";
                foreach (var subItem in item["sub_partition"])
                {
                    var subCategory = new LiveSubCategory()
                    {
                        ID = $"{subItem["partition"]["id_str"]},{subItem["partition"]["type"]}",
                        Name = subItem["partition"]["title"].ToString(),
                        ParentID = id,
                        Pic = "",
                    };
                    subs.Add(subCategory);
                }
                var category = new LiveCategory()
                {
                    Children = subs,
                    ID = id,
                    Name = item["partition"]["title"].ToString(),
                };
                subs.Insert(0, new LiveSubCategory() { ID = category.ID, Name = category.Name, ParentID = category.ID, Pic = "" });
                categories.Add(category);
            }
            return categories;
        }

        public async Task<LiveCategoryResult> GetCategoryRooms(LiveSubCategory category, int page = 1)
        {
            var ids = category.ID.Split(',');
            var partitionId = ids[0];
            var partitionType = ids[1];
            var reqParams = new Dictionary<string, string> {
                    {"aid","6383"},
                    {"app_name","douyin_web" },
                    {"live_id", "1"},
                    {"device_platform","web" },
                    { "language", "zh-CN"},
                    { "enter_from", "link_share"},
                    { "cookie_enabled", "true"},
                    { "screen_width", "1980"},
                    { "screen_height", "1080"},
                    { "browser_language", "zh-CN"},
                    { "browser_platform", "Win32"},
                    { "browser_name", "Edge"},
                    { "browser_version", "125.0.0.0"},
                    {"browser_online", "true"},
                    { "count","15" },
                    { "offset", ((page - 1) * 15).ToString()},
                    {"partition",partitionId},
                    {"partition_type",partitionType},
                    {"req_from","2" }
                };
            var url = $"https://live.douyin.com/webcast/web/partition/detail/room/v2/?{Utils.BuildQueryString(reqParams)}";

            var requestUrl = await GetABougs(url);
            var resp = await HttpUtil.GetString(requestUrl,
                headers: await GetRequestHeaders()
            );
            Trace.WriteLine($"Douyin.GetCategoryRooms url: {requestUrl}");
            if (string.IsNullOrWhiteSpace(resp) || !resp.TrimStart().StartsWith("{"))
            {
                Trace.WriteLine($"Douyin.GetCategoryRooms 无效响应: {resp}");
                return new LiveCategoryResult()
                {
                    HasMore = false,
                    Rooms = new List<LiveRoomItem>()
                };
            }
            var json = JObject.Parse(resp);
            var hasMore = (json["data"]["data"] as JArray).Count >= 15;
            var items = new List<LiveRoomItem>();
            foreach (var item in json["data"]["data"])
            {
                var roomItem = new LiveRoomItem()
                {
                    RoomID = item["web_rid"].ToString(),
                    Title = item["room"]["title"].ToString(),
                    Cover = item["room"]["cover"]["url_list"][0].ToString(),
                    UserName = item["room"]["owner"]["nickname"].ToString(),
                    Online = item["room"]["room_view_stats"]?["display_value"]?.ToObject<int>() ?? 0,
                };
                items.Add(roomItem);
            }
            return new LiveCategoryResult()
            {
                HasMore = hasMore,
                Rooms = items
            };
        }
        public async Task<LiveCategoryResult> GetRecommendRooms(int page = 1)
        {
            var reqParams = new Dictionary<string, string> {
                    {"aid","6383"},
                    {"app_name","douyin_web" },
                    {"live_id", "1"},
                    {"device_platform","web" },
                    { "language", "zh-CN"},
                    { "enter_from", "link_share"},
                    { "cookie_enabled", "true"},
                    { "screen_width", "1980"},
                    { "screen_height", "1080"},
                    { "browser_language", "zh-CN"},
                    { "browser_platform", "Win32"},
                    { "browser_name", "Edge"},
                    { "browser_version", "125.0.0.0"},
                    {"browser_online", "true"},
                    { "count","15" },
                    { "offset", ((page - 1) * 15).ToString()},
                    {"partition","720" },
                    {"partition_type","1"},
                    {"req_from","2" }
                };
            var url = $"https://live.douyin.com/webcast/web/partition/detail/room/v2/?{Utils.BuildQueryString(reqParams)}";

            var requestUrl = await GetABougs(url);
            var resp = await HttpUtil.GetString(requestUrl,
                headers: await GetRequestHeaders()
            );
            Trace.WriteLine($"Douyin.GetRecommendRooms url: {requestUrl}");
            if (string.IsNullOrWhiteSpace(resp) || !resp.TrimStart().StartsWith("{"))
            {
                Trace.WriteLine($"Douyin.GetRecommendRooms 无效响应: {resp}");
                return new LiveCategoryResult()
                {
                    HasMore = false,
                    Rooms = new List<LiveRoomItem>()
                };
            }
            var json = JObject.Parse(resp);
            var hasMore = (json["data"]["data"] as JArray).Count >= 15;
            var items = new List<LiveRoomItem>();
            foreach (var item in json["data"]["data"])
            {
                var roomItem = new LiveRoomItem()
                {
                    RoomID = item["web_rid"].ToString(),
                    Title = item["room"]["title"].ToString(),
                    Cover = item["room"]["cover"]["url_list"][0].ToString(),
                    UserName = item["room"]["owner"]["nickname"].ToString(),
                    Online = item["room"]["room_view_stats"]?["display_value"]?.ToObject<int>() ?? 0,
                };
                items.Add(roomItem);
            }
            return new LiveCategoryResult()
            {
                HasMore = hasMore,
                Rooms = items
            };
        }
        public async Task<LiveRoomDetail> GetRoomDetail(object roomId)
        {
            // 有两种roomId，一种是webRid，一种是roomId
            // roomId是一次性的，用户每次重新开播都会生成一个新的roomId
            // roomId一般长度为19位，例如：7376429659866598196
            // webRid是固定的，用户每次开播都是同一个webRid
            // webRid一般长度为11-12位，例如：416144012050
            // 这里简单进行判断，如果roomId长度小于15，则认为是webRid
            if (roomId.ToString().Length <= 16)
            {
                var webRid = roomId as string;
                return await GetRoomDetailByWebRid(webRid);
            }

            return await GetRoomDetailByRoomID(roomId as string);
        }
        /// <summary>
        /// 通过RoomId获取直播间详情
        /// </summary>
        /// <param name="roomId">
        /// roomId是一次性的，用户每次重新开播都会生成一个新的roomId。
        /// roomId一般长度为19位，例如：7376429659866598196
        /// </param>
        /// <returns></returns>
        private async Task<LiveRoomDetail> GetRoomDetailByRoomID(string roomId)
        {
            var roomData = await GetRoomDataByRoomID(roomId);
            // 通过房间信息获取WebRid
            var webRid = roomData["data"]["room"]["owner"]["web_rid"].ToString();
            // 读取用户唯一ID，用于弹幕连接
            // 似乎这个参数不是必须的，先随机生成一个
            //var userUniqueId = await GetUserUniqueId(webRid);
            var userUniqueId = GenerateRandomNumber(12).ToString();
            var room = roomData["data"]["room"];
            var owner = room["owner"];
            var status = room["status"].ToObject<int>();
            // roomId是一次性的，用户每次重新开播都会生成一个新的roomId
            // 所以如果roomId对应的直播间状态不是直播中，就通过webRid获取直播间信息
            if (status == 4)
            {
                var result = await GetRoomDetailByWebRid(webRid);
                return result;
            }
            var roomStatus = status == 2;
            // 主要是为了获取cookie,用于弹幕websocket连接
            var headers = await GetRequestHeaders(forceRefresh: true);
            return new LiveRoomDetail()
            {
                RoomID = webRid,
                Title = room["title"].ToString(),
                Cover = roomStatus ? room["cover"]["url_list"][0].ToString() : "",
                UserName = owner["nickname"].ToString(),
                UserAvatar = owner["avatar_thumb"]["url_list"][0].ToString(),
                Online = roomStatus
                  ? (room["room_view_stats"]?["display_value"]?.ToObject<int>() ?? 0)
                  : 0,
                Status = roomStatus,
                Url = $"https://live.douyin.com/{webRid}",
                Introduction = owner?["signature"]?.ToString() ?? "",
                Notice = "",
                DanmakuData = new DouyinDanmakuArgs()
                {
                    WebRid = webRid,
                    RoomId = roomId,
                    UserId = userUniqueId,
                    Cookie = headers["Cookie"],
                },
                Data = roomStatus ? room["stream_url"] : null,
            };

        }

        /// <summary>
        /// 通过webRid获取直播间详情
        /// </summary>
        /// <param name="webRid">
        /// webRid是固定的，用户每次开播都是同一个webRid
        /// webRid一般长度为11-12位，例如：416144012050
        /// </param>
        /// <returns></returns>
        private async Task<LiveRoomDetail> GetRoomDetailByWebRid(string webRid)
        {
            try
            {
                var result = await GetRoomDetailByWebRidApi(webRid);
                return result;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
            return await GetRoomDetailByWebRidHtml(webRid);
        }

        private async Task<LiveRoomDetail> GetRoomDetailByWebRidApi(string webRid)
        {
            Trace.WriteLine($"========== GetRoomDetailByWebRidApi 开始 ==========");
            Trace.WriteLine($"[RoomDetail] webRid={webRid}");
            
            // 读取房间信息
            //var data = await _getRoomDataByApi(webRid);
            var data = await GetRoomDataApi(webRid);
            var roomData = data["data"][0];

            var userData = data["user"];
            var roomId = roomData["id_str"].ToString();
            Trace.WriteLine($"[RoomDetail] roomId={roomId}");

            // 读取用户唯一ID，用于弹幕连接
            // 似乎这个参数不是必须的，先随机生成一个
            //var userUniqueId = await GetUserUniqueId(webRid);
            var userUniqueId = GenerateRandomNumber(12).ToString();
            Trace.WriteLine($"[RoomDetail] userUniqueId={userUniqueId}");

            var owner = roomData["owner"];

            var roomStatus = roomData["status"].ToObject<int>() == 2;
            Trace.WriteLine($"[RoomDetail] roomStatus={roomStatus}");

            // 主要是为了获取cookie,用于弹幕websocket连接
            Trace.WriteLine($"[RoomDetail] 获取Cookie (forceRefresh=true)...");
            var headers = await GetRequestHeaders(forceRefresh: true);
            var cookie = headers.ContainsKey("Cookie") ? headers["Cookie"] : "";
            Trace.WriteLine($"[RoomDetail] Cookie长度={cookie.Length}");
            Trace.WriteLine($"[RoomDetail] Cookie预览={cookie.Substring(0, Math.Min(100, cookie.Length))}...");
            
            Trace.WriteLine($"========== GetRoomDetailByWebRidApi 结束 ==========");
            return new LiveRoomDetail()
            {
                RoomID = webRid,
                Title = roomData["title"].ToString(),
                Cover = roomStatus ? roomData["cover"]["url_list"][0].ToString() : "",
                UserName = roomStatus
                    ? owner["nickname"].ToString()
                    : userData["nickname"].ToString(),
                UserAvatar = roomStatus
                    ? owner["avatar_thumb"]["url_list"][0].ToString()
                    : userData["avatar_thumb"]["url_list"][0].ToString(),
                Online = roomStatus
                    ? (roomData["room_view_stats"]?["display_value"]?.ToObject<int>() ?? 0)
                    : 0,
                Status = roomStatus,
                Url = $"https://live.douyin.com/{webRid}",
                Introduction = owner?["signature"]?.ToString() ?? "",
                Notice = "",
                DanmakuData = new DouyinDanmakuArgs()
                {
                    WebRid = webRid,
                    RoomId = roomId,
                    UserId = userUniqueId,
                    Cookie = headers["Cookie"],
                },
                Data = roomStatus ? roomData["stream_url"] : null,
            };

        }

        private async Task<LiveRoomDetail> GetRoomDetailByWebRidHtml(string webRid)
        {
            var roomData = await GetRoomDataHtml(webRid);
            var roomId = roomData["roomStore"]["roomInfo"]["room"]["id_str"].ToString();
            var userUniqueId =
                roomData["userStore"]["odin"]["user_unique_id"].ToString();

            var room = roomData["roomStore"]["roomInfo"]["room"];
            var owner = room["owner"];
            var anchor = roomData["roomStore"]["roomInfo"]["anchor"];
            var roomStatus = room["status"].ToObject<int>() == 2;

            // 主要是为了获取cookie,用于弹幕websocket连接
            var headers = await GetRequestHeaders(forceRefresh: true);
            return new LiveRoomDetail()
            {
                RoomID = webRid,
                Title = room["title"].ToString(),
                Cover = roomStatus ? room["cover"]["url_list"][0].ToString() : "",
                UserName = roomStatus
                    ? owner["nickname"].ToString()
                    : anchor["nickname"].ToString(),
                UserAvatar = roomStatus
                    ? owner["avatar_thumb"]["url_list"][0].ToString()
                    : anchor["avatar_thumb"]["url_list"][0].ToString(),
                Online = roomStatus
                    ? (room["room_view_stats"]?["display_value"]?.ToObject<int>() ?? 0)
                    : 0,
                Status = roomStatus,
                Url = $"https://live.douyin.com/{webRid}",
                Introduction = owner?["signature"]?.ToString() ?? "",
                Notice = "",
                DanmakuData = new DouyinDanmakuArgs()
                {
                    WebRid = webRid,
                    RoomId = roomId,
                    UserId = userUniqueId,
                    Cookie = headers["Cookie"],
                },
                Data = roomStatus ? room["stream_url"] : null,
            };
        }
        /// <summary>
        ///  进入直播间前需要先获取cookie
        /// </summary>
        /// <param name="webRid">直播间RID</param>
        /// <returns></returns>
        private async Task<string> GetWebCookie(string webRid)
        {
            var resp = await HttpUtil.Head($"https://live.douyin.com/{webRid}",
                headers: await GetRequestHeaders()
            );
            var dyCookie = "";
            foreach (var item in resp.Headers.GetValues("Set-Cookie"))
            {
                var cookie = item.Split(';')[0];
                if (cookie.Contains("ttwid") || cookie.Contains("__ac_nonce") || cookie.Contains("msToken"))
                {
                    dyCookie += $"{cookie};";
                }
            }
            return dyCookie;
        }

        /// <summary>
        /// 读取用户的唯一ID
        /// 暂时无用
        /// </summary>
        /// <param name="webRid"></param>
        /// <returns></returns>
        private async Task<string> GetUserUniqueId(string webRid)
        {
            var webInfo = await GetRoomDataHtml(webRid);
            return webInfo["userStore"]["odin"]["user_unique_id"].ToString();
        }

        private async Task<JToken> GetRoomDataHtml(string webRid)
        {
            var dyCookie = await GetWebCookie(webRid);
            var resp = await HttpUtil.GetString($"https://live.douyin.com/{webRid}",
                headers: new Dictionary<string, string>
                {
                    { "User-Agent", USER_AGENT },
                    { "Referer", REFERER },
                    { "Authority", AUTHORITY },
                    { "Cookie", dyCookie }
                }
            );
            Regex regex = new Regex("\\{\\\\\"state\\\\\":\\{\\\\\"appStore.*?\\]\\\\n", RegexOptions.Singleline);
            Match match = regex.Match(resp);
            string json = match.Success ? match.Groups[0].Value : "";
            if (string.IsNullOrEmpty(json))
            {
                throw new Exception("无法读取直播间数据");
            }
            json = json.Trim().Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("]\\n", "");
            return JObject.Parse(json)["state"];
        }

        private async Task<JToken> GetRoomDataApi(string webRid)
        {
            var reqParams = new Dictionary<string, string> {
                    {"aid","6383" },
                    {"app_name","douyin_web" },
                    {"live_id","1" },
                    {"device_platform","web" },
                    {"enter_from","web_live" },
                    {"web_rid",webRid },
                    {"room_id_str","" },
                    {"enter_source","" },
                    {"Room-Enter-User-Login-Ab","0" },
                    {"is_need_double_stream","false" },
                    {"cookie_enabled","true" },
                    {"screen_width","1980" },
                    {"screen_height","1080" },
                    {"browser_language","zh-CN" },
                    {"browser_platform","Win32" },
                    {"browser_name","Edge" },
                    {"browser_version","125.0.0.0" },
                    {"a_bogus","0" }
                };
            var url = $"https://live.douyin.com/webcast/room/web/enter/?{Utils.BuildQueryString(reqParams)}";

            var requestUrl = await GetABougs(url);
            var resp = await HttpUtil.GetString(requestUrl,
                headers: await GetRequestHeaders()
            );


           
            return JObject.Parse(resp)["data"];
        }

        private async Task<JToken> GetRoomDataByRoomID(string roomId)
        {
            var resp = await HttpUtil.GetString($"https://webcast.amemv.com/webcast/room/reflow/info/",
                headers: await GetRequestHeaders(),
                queryParameters: new Dictionary<string, string>
                {
                    {"type_id","0" },
                    {"live_id","1" },
                    {"room_id",roomId },
                    {"sec_user_id","" },
                    {"version_code","99.99.99" },
                    {"app_id","6383" },
                }
            );
            return JObject.Parse(resp);
        }

        public Task<List<LivePlayQuality>> GetPlayQuality(LiveRoomDetail roomDetail)
        {
            List<LivePlayQuality> qualities = new List<LivePlayQuality>();
            if (roomDetail.Data == null)
            {
                return Task.FromResult(qualities);
            }
            var data = roomDetail.Data as JToken;
            var qulityList = data["live_core_sdk_data"]["pull_data"]["options"]["qualities"];
            var streamData = data["live_core_sdk_data"]["pull_data"]["stream_data"].ToString();

            if (!streamData.StartsWith("{"))
            {
                var flvList = (data["flv_pull_url"] as JToken).Values().Select(c => c.ToString()).ToList();
                var hlsList = (data["hls_pull_url_map"] as JToken).Values().Select(c => c.ToString()).ToList();
                foreach (var quality in qulityList)
                {
                    int level = quality["level"].ToObject<int>();
                    List<String> urls = new List<string>();
                    var flvIndex = flvList.Count - level;
                    if (flvIndex >= 0 && flvIndex < flvList.Count)
                    {
                        urls.Add(flvList[flvIndex]);
                    }
                    var hlsIndex = hlsList.Count - level;
                    if (hlsIndex >= 0 && hlsIndex < hlsList.Count)
                    {
                        urls.Add(hlsList[hlsIndex]);
                    }
                    var qualityItem = new LivePlayQuality()
                    {
                        Quality = quality["name"].ToString(),
                        Sort = level,
                        Data = urls,
                    };
                    if (urls.Count > 0)
                    {
                        qualities.Add(qualityItem);
                    }
                }
            }
            else
            {
                var qualityData = JObject.Parse(streamData)["data"] as JObject;
                foreach (var quality in qulityList)
                {
                    List<string> urls = new List<string>();

                    var flvUrl =
                        qualityData[quality["sdk_key"].ToString()]?["main"]?["flv"]?.ToString();

                    if (flvUrl != null && flvUrl.Length > 0)
                    {
                        urls.Add(flvUrl);
                    }
                    var hlsUrl =
                        qualityData[quality["sdk_key"].ToString()]?["main"]?["hls"]?.ToString();
                    if (hlsUrl != null && hlsUrl.Length > 0)
                    {
                        urls.Add(hlsUrl);
                    }
                    var qualityItem = new LivePlayQuality()
                    {
                        Quality = quality["name"].ToString(),
                        Sort = quality["level"].ToObject<int>(),
                        Data = urls,
                    };
                    if (urls.Count > 0)
                    {
                        qualities.Add(qualityItem);
                    }
                }
            }
            // var qualityData = json.decode(
            //     detail.data["live_core_sdk_data"]["pull_data"]["stream_data"])["data"];

            //qualities.sort((a, b) => b.sort.compareTo(a.sort));
            qualities = qualities.OrderByDescending(q => q.Sort).ToList();
            return Task.FromResult(qualities);
        }

        public Task<List<string>> GetPlayUrls(LiveRoomDetail roomDetail, LivePlayQuality qn)
        {
            return Task.FromResult(qn.Data as List<string>);
        }

        public async Task<LiveSearchResult> Search(string keyword, int page = 1)
        {
            Trace.WriteLine($"========== Douyin.Search 开始 ==========");
            Trace.WriteLine($"[Search] keyword={keyword}, page={page}");
            var query = new Dictionary<string, string>
            {
                { "device_platform", "webapp" },
                { "aid", "6383" },
                { "channel", "channel_pc_web" },
                { "search_channel", "aweme_live" },
                { "keyword", keyword },
                { "search_source", "switch_tab" },
                { "query_correct_type", "1" },
                { "is_filter_search", "0" },
                { "from_group_id", "" },
                { "offset", ((page - 1) * 10).ToString() },
                { "count", "10" },
                { "pc_client_type", "1" },
                { "version_code", "170400" },
                { "version_name", "17.4.0" },
                { "cookie_enabled", "true" },
                { "screen_width", "1980" },
                { "screen_height", "1080" },
                { "browser_language", "zh-CN" },
                { "browser_platform", "Win32" },
                { "browser_name", "Edge" },
                { "browser_version", "125.0.0.0" },
                { "browser_online", "true" },
                { "engine_name", "Blink" },
                { "engine_version", "125.0.0.0" },
                { "os_name", "Windows" },
                { "os_version", "10" },
                { "cpu_core_num", "12" },
                { "device_memory", "8" },
                { "platform", "PC" },
                { "downlink", "10" },
                { "effective_type", "4g" },
                { "round_trip_time", "100" },
                { "webid", "7382872326016435738" }
            };

            var requestUrl = $"https://www.douyin.com/aweme/v1/web/live/search/?{Utils.BuildQueryString(query)}";
            Trace.WriteLine($"[Search] 原始URL: {TruncateForLog(requestUrl, 150)}");
            
            requestUrl = await GetABougs(requestUrl);
            Trace.WriteLine($"[Search] 签名后URL: {TruncateForLog(requestUrl, 200)}");
            
            var cookie = (await GetRequestHeaders(forceRefresh: true))["Cookie"];
            Trace.WriteLine($"[Search] Cookie: {TruncateForLog(cookie, 100)}");
            var searchHeaders = new Dictionary<string, string>
            {
                { "authority", "www.douyin.com" },
                { "accept", "application/json, text/plain, */*" },
                { "accept-language", "zh-CN,zh;q=0.9,en;q=0.8" },
                { "cookie", cookie },
                { "priority", "u=1, i" },
                { "referer", $"https://www.douyin.com/search/{Uri.EscapeDataString(keyword)}?type=live" },
                { "sec-ch-ua", "\"Microsoft Edge\";v=\"125\", \"Chromium\";v=\"125\", \"Not.A/Brand\";v=\"24\"" },
                { "sec-ch-ua-mobile", "?0" },
                { "sec-ch-ua-platform", "\"Windows\"" },
                { "sec-fetch-dest", "empty" },
                { "sec-fetch-mode", "cors" },
                { "sec-fetch-site", "same-origin" },
                { "user-agent", USER_AGENT }
            };

            Trace.WriteLine($"[Search] 开始请求...");
            var resp = await HttpUtil.GetString(requestUrl, searchHeaders);
            Trace.WriteLine($"[Search] 响应长度: {resp?.Length ?? 0}");
            Trace.WriteLine($"[Search] 响应预览: {TruncateForLog(resp, 300)}");

            if (string.IsNullOrWhiteSpace(resp))
            {
                Trace.WriteLine("[Search] 错误: 响应为空");
                return new LiveSearchResult()
                {
                    HasMore = false,
                    Rooms = new List<LiveRoomItem>()
                };
            }

            JObject json;
            try
            {
                json = JObject.Parse(resp);
            }
            catch (Exception parseEx)
            {
                Trace.WriteLine($"[Search] JSON解析失败: {parseEx.Message}");
                Trace.WriteLine($"[Search] 原始响应: {TruncateForLog(resp)}");
                return new LiveSearchResult()
                {
                    HasMore = false,
                    Rooms = new List<LiveRoomItem>()
                };
            }

            var statusCode = json["status_code"]?.ToObject<int?>() ?? 0;
            var statusMsg = json["status_msg"]?.ToString();
            Trace.WriteLine($"[Search] API状态: code={statusCode}, msg={statusMsg}");

            var dataToken = json["data"];
            JArray livesArray = dataToken as JArray;
            if (livesArray == null)
            {
                livesArray = dataToken?["data"] as JArray;
            }

            if (livesArray == null)
            {
                Trace.WriteLine("[Search] 错误: 数据结构异常，找不到直播列表");
                Trace.WriteLine($"[Search] JSON结构: {TruncateForLog(json.ToString(Formatting.None))}");
                return new LiveSearchResult()
                {
                    HasMore = false,
                    Rooms = new List<LiveRoomItem>()
                };
            }

            Trace.WriteLine($"[Search] 找到 {livesArray.Count} 条数据");
            var items = new List<LiveRoomItem>();
            foreach (var item in livesArray)
            {
                var rawData = item["lives"]?["rawdata"]?.ToString();
                if (string.IsNullOrEmpty(rawData))
                {
                    continue;
                }

                var itemData = JObject.Parse(rawData);
                var roomItem = new LiveRoomItem()
                {
                    RoomID = itemData["owner"]?["web_rid"]?.ToString() ?? string.Empty,
                    Title = itemData["title"]?.ToString() ?? string.Empty,
                    Cover = itemData["cover"]?["url_list"]?.FirstOrDefault()?.ToString() ?? string.Empty,
                    UserName = itemData["owner"]?["nickname"]?.ToString() ?? string.Empty,
                    Online = itemData["stats"]?["total_user"]?.ToObject<int>() ?? 0,
                };
                if (!string.IsNullOrEmpty(roomItem.RoomID))
                {
                    items.Add(roomItem);
                }
            }

            var hasMoreToken = (dataToken as JObject)?["has_more"];
            var hasMore = hasMoreToken?.ToObject<int?>() == 1 || items.Count >= 10;

            Trace.WriteLine($"[Search] 解析完成: 有效结果={items.Count}, hasMore={hasMore}");
            Trace.WriteLine($"========== Douyin.Search 结束 ==========");
            return new LiveSearchResult()
            {
                HasMore = hasMore,
                Rooms = items
            };
        }
        public async Task<LiveStatusType> GetLiveStatus(object roomId)
        {
            var result = await GetRoomDetail(roomId: roomId);
            return result.Status ? LiveStatusType.Live : LiveStatusType.Offline;
        }
        public Task<List<LiveSuperChatMessage>> GetSuperChatMessages(object roomId)
        {
            return Task.FromResult(new List<LiveSuperChatMessage>());
        }

        private static string TruncateForLog(string value, int max = 400)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
            {
                return value;
            }

            return value.Substring(0, max) + "...";
        }

        private string GenerateRandomNumber(int length)
        {
            var random = new Random();
            var sb = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                // 第一位不能为0
                if (i == 0)
                {
                    sb.Append(random.Next(1, 9));
                }
                else
                {
                    sb.Append(random.Next(0, 9));
                }
            }
            return sb.ToString();
        }

        private async Task<string> GetABougs(string url)
        {
            Trace.WriteLine($"[GetABougs] 开始生成签名");
            try
            {
                var uri = new Uri(url);
                var baseUrl = uri.GetLeftPart(UriPartial.Path);
                var rawQuery = uri.Query.TrimStart('?');
                var msToken = GenerateMsToken();
                var queryForSign = string.IsNullOrEmpty(rawQuery)
                    ? $"msToken={msToken}"
                    : $"{rawQuery}&msToken={msToken}";

                Trace.WriteLine($"[GetABougs] queryForSign长度={queryForSign.Length}");
                Trace.WriteLine($"[GetABougs] 调用 DouyinABogusHelper.GenerateAsync...");
                
                var aBogus = await DouyinABogusHelper.GenerateAsync(queryForSign, USER_AGENT).ConfigureAwait(false);
                
                Trace.WriteLine($"[GetABougs] a_bogus结果: '{aBogus}'");
                Trace.WriteLine($"[GetABougs] a_bogus长度: {aBogus?.Length ?? 0}");
                Trace.WriteLine($"[GetABougs] a_bogus是否为空: {string.IsNullOrEmpty(aBogus)}");
                
                if (string.IsNullOrEmpty(aBogus))
                {
                    Trace.WriteLine("[GetABougs] 警告: a_bogus为空，使用无签名URL");
                    var fallbackQuery = string.IsNullOrEmpty(rawQuery)
                        ? $"msToken={Uri.EscapeDataString(msToken)}"
                        : $"{rawQuery}&msToken={Uri.EscapeDataString(msToken)}";
                    return $"{baseUrl}?{fallbackQuery}";
                }

                var finalQuery = string.IsNullOrEmpty(rawQuery)
                    ? $"msToken={Uri.EscapeDataString(msToken)}&a_bogus={Uri.EscapeDataString(aBogus)}"
                    : $"{rawQuery}&msToken={Uri.EscapeDataString(msToken)}&a_bogus={Uri.EscapeDataString(aBogus)}";

                Trace.WriteLine($"[GetABougs] 签名成功");
                return $"{baseUrl}?{finalQuery}";
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[GetABougs] 异常: {ex.Message}");
                Trace.WriteLine($"[GetABougs] StackTrace: {ex.StackTrace}");
                return url;
            }
        }

        private static string GenerateMsToken(int length = 107)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var buffer = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                sb.Append(chars[buffer[i] % chars.Length]);
            }
            return sb.ToString();
        }
    }
}

