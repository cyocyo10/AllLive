using AllLive.Core.Helper;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

if (args.Contains("--live")) {
 HttpUtil.Live=true;
 foreach(var room in new[]{"63136","6979222","252140"}) {
  var req=await DouyuSignHelper.BuildRequestAsync(room);
  using var client=new System.Net.Http.HttpClient();
  using var message=new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post,"https://www.douyu.com/lapi/live/getH5PlayV1/"+room);
  foreach(var h in req.Headers) message.Headers.TryAddWithoutValidation(h.Key,h.Value);
  message.Content=new System.Net.Http.StringContent(req.Form,Encoding.UTF8,"application/x-www-form-urlencoded");
  using var response=await client.SendAsync(message);
  var body=JObject.Parse(await response.Content.ReadAsStringAsync());
  Console.WriteLine($"room={room} HTTP={(int)response.StatusCode} API={body["error"]} qualities={((body["data"] as JObject)?["multirates"] as JArray)?.Count ?? 0}");
 }
 return;
}

var count = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
Dictionary<string,string> Form(string form) => form.Split('&').Select(x => x.Split('=',2)).ToDictionary(x=>x[0],x=>Uri.UnescapeDataString(x[1]));
DouyuSignHelper.AccountCookie = "Cookie: dy_did=login-A; dy_auth=test-token; bad=value\r\nInjected: x";
Check(DouyuSignHelper.DeviceId == "login-A", "preserve login DID");
Check(!DouyuSignHelper.CookieHeader().Contains('\r'), "drop control characters");
var first = await DouyuSignHelper.BuildRequestAsync("63136");
Check(Form(first.Form)["did"] == "login-A" && first.Headers["cookie"].Contains("dy_did=login-A"), "request identity snapshot");
Check(Form(first.Form)["enc_data"] == "+/=& test", "form encoded exactly once");
var second = await DouyuSignHelper.BuildRequestAsync("6979222");
Check(HttpUtil.Calls == 1, "descriptor reused in session");
DouyuSignHelper.AccountCookie = "dy_did=login-B; dy_auth=other-test-token";
Check(!first.IsCurrent, "reject stale playback response");
await DouyuSignHelper.BuildRequestAsync("63136");
Check(HttpUtil.Calls == 2 && HttpUtil.LastDid == "login-B", "invalidate descriptor on account change");
DouyuSignHelper.AccountCookie = "dy_did=login-B; dy_auth=new-test-token";
await DouyuSignHelper.BuildRequestAsync("63136");
Check(HttpUtil.Calls == 3, "same DID token replacement invalidates cache");
foreach (var rounds in new[]{1,16,17,1000,4096}) {
 HttpUtil.Rounds=rounds;
 var req=await DouyuSignHelper.BuildRequestAsync("63136", forceRefresh:true);
 var f=Form(req.Form); var secret="rand";
 for(int i=0;i<rounds;i++) secret=Utils.ToMD5(secret+"key");
 Check(f["auth"]==Utils.ToMD5(secret+"key"+"63136"+f["tt"]), "MD5 vector rounds="+rounds);
}
foreach (var rounds in new[]{0,-1,4097}) {
 HttpUtil.Rounds=rounds;
 try { await DouyuSignHelper.BuildRequestAsync("63136",forceRefresh:true); throw new Exception("accepted bad rounds"); }
 catch(Exception ex) when(ex.Message.Contains("sign_rounds")) {Check(true,"reject rounds="+rounds);}
}
HttpUtil.Rounds=1; HttpUtil.Expiry=-60;
try { await DouyuSignHelper.BuildRequestAsync("63136",forceRefresh:true); throw new Exception("accepted expired"); }
catch(Exception ex) when(ex.Message.Contains("sign_expired")){Check(true,"expiry diagnostic");}
HttpUtil.Expiry=600;
var gate = new TaskCompletionSource<bool>(); HttpUtil.Gate=gate.Task;
var pending=DouyuSignHelper.BuildRequestAsync("63136",forceRefresh:true);
DouyuSignHelper.AccountCookie="dy_did=login-C";
gate.SetResult(true);
try { await pending; throw new Exception("accepted stale descriptor"); }
catch(Exception ex) when(ex.Message.Contains("session_changed")){Check(true,"reject late descriptor after switch");}
HttpUtil.Gate=null;
DouyuSignHelper.AccountCookie="";
Check(DouyuSignHelper.DeviceId.Length==32 && DouyuSignHelper.DeviceId!="login-C", "logout anonymous identity");
Console.WriteLine($"{count} checks passed");

namespace AllLive.Core.Helper {
 public static class Utils {
  public static long GetTimestamp()=>DateTimeOffset.UtcNow.ToUnixTimeSeconds();
  public static string ToMD5(string s)=>Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
  public static string BuildQueryString(Dictionary<string,string> fields)=>string.Join("&",fields.Select(x=>x.Key+"="+Uri.EscapeDataString(x.Value)));
 }
 public static class HttpUtil {
  public static bool Live; public static int Calls, Rounds=1, Expiry=600; public static string LastDid=""; public static Task Gate;
  public static async Task<string> GetString(string url, IDictionary<string,string> headers, IDictionary<string,string> query, bool useCookies=true) {
   if(useCookies) throw new Exception("Signing request must not use shared cookie jar");
   if(Live) {
    using var client=new System.Net.Http.HttpClient();
    using var message=new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get,url+"?did="+Uri.EscapeDataString(query["did"]));
    foreach(var h in headers) message.Headers.TryAddWithoutValidation(h.Key,h.Value);
    using var response=await client.SendAsync(message); response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync();
   }
   Calls++; LastDid=query["did"]; if(Gate!=null) await Gate;
   return new JObject { ["error"]=0, ["data"]=new JObject { ["key"]="key",["rand_str"]="rand",["enc_data"]="+/=& test",["enc_time"]=Rounds.ToString(),["expire_at"]=(Utils.GetTimestamp()+Expiry).ToString(),["is_special"]=0 }}.ToString();
  }
 }
}
