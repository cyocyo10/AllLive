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
            
            // ǿ��ˢ��ʱ��������ɵ� Cookie������ʹ�ù��ڵ� __ac_nonce
            if (forceRefresh)
            {
                headers.Remove("Cookie");
                headers.Remove("cookie");
            }
            
            try
            {
                // ����һ������ Cookie �� headers ������������ȷ����ȡȫ�µ� Cookie
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
                throw new Exception("�޷���ȡ��������");
            }
            renderData = renderData.Trim().Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("]\\n", "");
            // ����JSON����
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
                Trace.WriteLine($"Douyin.GetCategoryRooms ��Ч��Ӧ: {resp}");
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
                Trace.WriteLine($"Douyin.GetRecommendRooms ��Ч��Ӧ: {resp}");
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
            // ������roomId��һ����webRid��һ����roomId
            // roomId��һ���Եģ��û�ÿ�����¿�����������һ���µ�roomId
            // roomIdһ�㳤��Ϊ19λ�����磺7376429659866598196
            // webRid�ǹ̶��ģ��û�ÿ�ο�������ͬһ��webRid
            // webRidһ�㳤��Ϊ11-12λ�����磺416144012050
            // ����򵥽����жϣ����roomId����С��15������Ϊ��webRid
            if (roomId.ToString().Length <= 16)
            {
                var webRid = roomId as string;
                return await GetRoomDetailByWebRid(webRid);
            }

            return await GetRoomDetailByRoomID(roomId as string);
        }
        /// <summary>
        /// ͨ��RoomId��ȡֱ��������
        /// </summary>
        /// <param name="roomId">
        /// roomId��һ���Եģ��û�ÿ�����¿�����������һ���µ�roomId��
        /// roomIdһ�㳤��Ϊ19λ�����磺7376429659866598196
        /// </param>
        /// <returns></returns>
        private async Task<LiveRoomDetail> GetRoomDetailByRoomID(string roomId)
        {
            var roomData = await GetRoomDataByRoomID(roomId);
            // ͨ��������Ϣ��ȡWebRid
            var webRid = roomData["data"]["room"]["owner"]["web_rid"].ToString();
            // ��ȡ�û�ΨһID�����ڵ�Ļ����
            // �ƺ�����������Ǳ���ģ����������һ��
            //var userUniqueId = await GetUserUniqueId(webRid);
            var userUniqueId = GenerateRandomNumber(12).ToString();
            var room = roomData["data"]["room"];
            var owner = room["owner"];
            var status = room["status"].ToObject<int>();
            // roomId��һ���Եģ��û�ÿ�����¿�����������һ���µ�roomId
            // �������roomId��Ӧ��ֱ����״̬����ֱ���У���ͨ��webRid��ȡֱ������Ϣ
            if (status == 4)
            {
                var result = await GetRoomDetailByWebRid(webRid);
                return result;
            }
            var roomStatus = status == 2;
            // ��Ҫ��Ϊ�˻�ȡcookie,���ڵ�Ļwebsocket����
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
        /// ͨ��webRid��ȡֱ��������
        /// </summary>
        /// <param name="webRid">
        /// webRid�ǹ̶��ģ��û�ÿ�ο�������ͬһ��webRid
        /// webRidһ�㳤��Ϊ11-12λ�����磺416144012050
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
            Trace.WriteLine($"========== GetRoomDetailByWebRidApi ��ʼ ==========");
            Trace.WriteLine($"[RoomDetail] webRid={webRid}");
            
            // ��ȡ������Ϣ
            //var data = await _getRoomDataByApi(webRid);
            var data = await GetRoomDataApi(webRid);
            var roomData = data["data"][0];

            var userData = data["user"];
            var roomId = roomData["id_str"].ToString();
            Trace.WriteLine($"[RoomDetail] roomId={roomId}");

            // ��ȡ�û�ΨһID�����ڵ�Ļ����
            // �ƺ�����������Ǳ���ģ����������һ��
            //var userUniqueId = await GetUserUniqueId(webRid);
            var userUniqueId = GenerateRandomNumber(12).ToString();
            Trace.WriteLine($"[RoomDetail] userUniqueId={userUniqueId}");

            var owner = roomData["owner"];

            var roomStatus = roomData["status"].ToObject<int>() == 2;
            Trace.WriteLine($"[RoomDetail] roomStatus={roomStatus}");

            // ��Ҫ��Ϊ�˻�ȡcookie,���ڵ�Ļwebsocket����
            Trace.WriteLine($"[RoomDetail] ��ȡCookie (forceRefresh=true)...");
            var headers = await GetRequestHeaders(forceRefresh: true);
            var cookie = headers.ContainsKey("Cookie") ? headers["Cookie"] : "";
            Trace.WriteLine($"[RoomDetail] Cookie����={cookie.Length}");
            Trace.WriteLine($"[RoomDetail] CookieԤ��={cookie.Substring(0, Math.Min(100, cookie.Length))}...");
            
            Trace.WriteLine($"========== GetRoomDetailByWebRidApi ���� ==========");
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

            // ��Ҫ��Ϊ�˻�ȡcookie,���ڵ�Ļwebsocket����
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
        ///  ����ֱ����ǰ��Ҫ�Ȼ�ȡcookie
        /// </summary>
        /// <param name="webRid">ֱ����RID</param>
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
        /// ��ȡ�û���ΨһID
        /// ��ʱ����
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
                throw new Exception("�޷���ȡֱ��������");
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

        /// <summary>
        /// 搜索直播间 - 支持房间号、直播链接、短链接
        /// </summary>
        public async Task<LiveSearchResult> Search(string keyword, int page = 1)
        {
            // 只处理第一页，因为是单个房间查询
            if (page > 1)
            {
                return new LiveSearchResult() { HasMore = false, Rooms = new List<LiveRoomItem>() };
            }

            var roomId = await ParseRoomId(keyword);
            if (string.IsNullOrEmpty(roomId))
            {
                return new LiveSearchResult() { HasMore = false, Rooms = new List<LiveRoomItem>() };
            }

            try
            {
                var detail = await GetRoomDetail(roomId);
                var items = new List<LiveRoomItem>
                {
                    new LiveRoomItem()
                    {
                        RoomID = detail.RoomID,
                        Title = detail.Title,
                        Cover = detail.Cover,
                        UserName = detail.UserName,
                        Online = detail.Online,
                    }
                };
                return new LiveSearchResult() { HasMore = false, Rooms = items };
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Search] 获取房间信息失败: {ex.Message}");
                return new LiveSearchResult() { HasMore = false, Rooms = new List<LiveRoomItem>() };
            }
        }

        /// <summary>
        /// 解析房间号 - 支持多种格式
        /// </summary>
        private async Task<string> ParseRoomId(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            input = input.Trim();

            // 1. 纯数字房间号
            if (Regex.IsMatch(input, @"^\d+$"))
            {
                return input;
            }

            // 2. 抖音直播链接: https://live.douyin.com/123456789
            var liveMatch = Regex.Match(input, @"live\.douyin\.com/(\d+)");
            if (liveMatch.Success)
            {
                return liveMatch.Groups[1].Value;
            }

            // 3. 抖音短链接: https://v.douyin.com/xxxxx
            if (input.Contains("v.douyin.com"))
            {
                try
                {
                    var realUrl = await ResolveShortUrl(input);
                    if (!string.IsNullOrEmpty(realUrl))
                    {
                        var match = Regex.Match(realUrl, @"live\.douyin\.com/(\d+)");
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[ParseRoomId] 解析短链接失败: {ex.Message}");
                }
            }

            // 4. 从文本中提取链接
            var urlMatch = Regex.Match(input, @"https?://[^\s]+");
            if (urlMatch.Success && urlMatch.Value != input)
            {
                return await ParseRoomId(urlMatch.Value);
            }

            return null;
        }

        /// <summary>
        /// 解析抖音短链接
        /// </summary>
        private async Task<string> ResolveShortUrl(string shortUrl)
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = false
                };
                using (var client = new HttpClient(handler))
                {
                    client.DefaultRequestHeaders.Add("User-Agent", USER_AGENT);
                    var response = await client.GetAsync(shortUrl);
                    if (response.Headers.Location != null)
                    {
                        return response.Headers.Location.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[ResolveShortUrl] 异常: {ex.Message}");
            }
            return null;
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

        private string GenerateRandomNumber(int length)
        {
            var random = new Random();
            var sb = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                // ��һλ����Ϊ0
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
            Trace.WriteLine($"[GetABougs] ��ʼ����ǩ��");
            try
            {
                var uri = new Uri(url);
                var baseUrl = uri.GetLeftPart(UriPartial.Path);
                var rawQuery = uri.Query.TrimStart('?');
                var msToken = GenerateMsToken();
                var queryForSign = string.IsNullOrEmpty(rawQuery)
                    ? $"msToken={msToken}"
                    : $"{rawQuery}&msToken={msToken}";

                Trace.WriteLine($"[GetABougs] queryForSign����={queryForSign.Length}");
                Trace.WriteLine($"[GetABougs] ���� DouyinABogusHelper.GenerateAsync...");
                
                var aBogus = await DouyinABogusHelper.GenerateAsync(queryForSign, USER_AGENT).ConfigureAwait(false);
                
                Trace.WriteLine($"[GetABougs] a_bogus���: '{aBogus}'");
                Trace.WriteLine($"[GetABougs] a_bogus����: {aBogus?.Length ?? 0}");
                Trace.WriteLine($"[GetABougs] a_bogus�Ƿ�Ϊ��: {string.IsNullOrEmpty(aBogus)}");
                
                if (string.IsNullOrEmpty(aBogus))
                {
                    Trace.WriteLine("[GetABougs] ����: a_bogusΪ�գ�ʹ����ǩ��URL");
                    var fallbackQuery = string.IsNullOrEmpty(rawQuery)
                        ? $"msToken={Uri.EscapeDataString(msToken)}"
                        : $"{rawQuery}&msToken={Uri.EscapeDataString(msToken)}";
                    return $"{baseUrl}?{fallbackQuery}";
                }

                var finalQuery = string.IsNullOrEmpty(rawQuery)
                    ? $"msToken={Uri.EscapeDataString(msToken)}&a_bogus={Uri.EscapeDataString(aBogus)}"
                    : $"{rawQuery}&msToken={Uri.EscapeDataString(msToken)}&a_bogus={Uri.EscapeDataString(aBogus)}";

                Trace.WriteLine($"[GetABougs] ǩ���ɹ�");
                return $"{baseUrl}?{finalQuery}";
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[GetABougs] �쳣: {ex.Message}");
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

