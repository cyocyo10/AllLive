using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AllLive.Core;
using AllLive.Core.Helper;
using AllLive.Core.Models;
using AllLive.UWP.ViewModels;
using CoreUtils=AllLive.Core.Helper.Utils;

namespace AuditHarness
{
    class Program
    {
        const string Commit="c6951df959b7ead0d93edc087188788b49f1e273";
        static readonly List<(string Id, string Description, Func<Task> Run)> Tests=new();
        static readonly List<object> Results=new();
        static string Observation;
        static void Add(string id,string description,Func<Task> run)=>Tests.Add((id,description,run));
        static void Sync(string id,string description,Action run)=>Add(id,description,()=>{run();return Task.CompletedTask;});
        static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
        static void Equal<T>(T expected,T actual,string name) => Check(EqualityComparer<T>.Default.Equals(expected,actual),$"{name}: expected={expected}, actual={actual}");
        static string MD5Text(string s)=>Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
        static Dictionary<string,string> Form(string s)=>s.Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>p[0],p=>Uri.UnescapeDataString(p[1]));
        static JObject Key(long expiry=0,int rounds=1,int special=0)=>new JObject { ["expire_at"]=expiry==0?CoreUtils.GetTimestamp()+3600:expiry, ["enc_time"]=rounds,["key"]="secret",["rand_str"]="random",["enc_data"]="a+/= &中文%25",["is_special"]=special };
        static string KeyResponse(JObject key=null)=>new JObject { ["data"]=key??Key() }.ToString(Formatting.None);
        static string PlayResponse(JObject data,int error=0)=>new JObject { ["error"]=error,["data"]=data }.ToString(Formatting.None);
        static JObject Play(string live="https://fixture.invalid/live.flv?a=1&b=2")=>new JObject { ["rtmp_live"]=live,["multirates"]=new JArray(new JObject { ["name"]="HD",["rate"]=1 }),["rtmp_cdn"]="main" };
        static FieldInfo SignField(string name)=>typeof(DouyuSignHelper).GetField(name,BindingFlags.NonPublic|BindingFlags.Static);
        static void ResetSign(JObject key=null,long fetched=0)
        {
            SignField("_encKey").SetValue(null,key);SignField("_encKeyFetchedAtSeconds").SetValue(null,fetched);DouyuSignHelper.AccountCookie="";
            HttpUtil.Reset();
        }
        static object CallPrivate(Type type,string method,object obj,params object[] args)
        {
            try { return type.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance).Invoke(obj,args); }
            catch(TargetInvocationException e) { throw e.InnerException; }
        }
        static bool Usable(JObject key,long now)=> (bool)CallPrivate(typeof(DouyuSignHelper),"IsEncryptionKeyUsable",null,key,now,30);
        static string Parse(JObject data)=>(string)CallPrivate(typeof(Douyu),"ParsePlayUrl",null,data);
        static int Count(string method)=>HttpUtil.Calls.Count(r=>r.Method==method);
        static void FakeResponses(Func<HttpUtil.Request,string> post=null)=>HttpUtil.Reset(r=>Task.FromResult(r.Method=="GET"?KeyResponse():post?.Invoke(r)??PlayResponse(Play())));
        static async Task Throws<T>(Func<Task> action,string label) where T:Exception
        {
            try { await action(); } catch(T) { return; } throw new Exception(label+": expected "+typeof(T).Name);
        }
        static async Task Until(Func<bool> predicate,string label)
        {
            var deadline=DateTime.UtcNow.AddSeconds(3);
            while(!predicate() && DateTime.UtcNow<deadline) await Task.Delay(5);
            Check(predicate(),"Timed out waiting for "+label);
        }
        static LiveRoomVM VM()=>new LiveRoomVM(new SettingVM()) { Dispatcher=new Windows.UI.Core.CoreDispatcher() };
        static void SetupVm(LiveRoomVM vm,FakeSite site)
        {
            typeof(LiveRoomVM).GetField("Site",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(vm,site);
            vm.detail=new LiveRoomDetail { RoomID="fixture",Status=true };
        }
        static void Register()
        {
            Sync("TLS-001","application WebSocket configuration must reject certificate errors (original security assertion)",()=>{
                using var socket=new WebSocketSharp.WebSocket("wss://fixture.invalid/sub");
                WebSocketSecurity.Configure(socket);
                var callback=socket.SslConfiguration.ServerCertificateValidationCallback;
                var errors=System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors | System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch;
                using var rsa=RSA.Create(2048);
                var request=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=wrong.invalid",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
                using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
                using var chain=new System.Security.Cryptography.X509Certificates.X509Chain();
                bool accepted=callback(new object(),certificate,chain,errors);
                var assembly=typeof(WebSocketSharp.Net.ClientSslConfiguration).Assembly;
                Check(callback.Method.Module.Assembly==typeof(WebSocketSecurity).Assembly,"Application validator was not installed");
                var callbackIl=Convert.ToHexString(callback.Method.GetMethodBody().GetILAsByteArray());
                Observation="callback="+callback.Method.Name+", IL="+callbackIl+", dependencyAssembly="+assembly.FullName+", validatorAssembly="+callback.Method.Module.Assembly.GetName().Name+", synthetic certificate="+certificate.Subject+", certificate errors="+errors+", accepted="+accepted;
                Check(!accepted,"Application certificate validator accepted chain/name errors");
            });
            Sync("TLS-002","valid platform certificate result is accepted, TLS 1.2 retained",()=>{
                using var socket=new WebSocketSharp.WebSocket("wss://fixture.invalid/sub");
                WebSocketSecurity.Configure(socket);
                using var rsa=RSA.Create(2048);
                var request=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=fixture.invalid",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
                using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
                Check(socket.SslConfiguration.ServerCertificateValidationCallback(socket,cert,null,System.Net.Security.SslPolicyErrors.None),"valid platform result rejected");
                Equal(System.Security.Authentication.SslProtocols.Tls12,socket.SslConfiguration.EnabledSslProtocols,"protocol");
            });
            Sync("TLS-003","name, chain, self-signed and missing certificates fail closed individually",()=>{
                using var socket=new WebSocketSharp.WebSocket("wss://fixture.invalid/sub");WebSocketSecurity.Configure(socket);
                using var rsa=RSA.Create(2048);
                var request=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=wrong.invalid",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
                using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
                using var chain=new System.Security.Cryptography.X509Certificates.X509Chain();
                chain.ChainPolicy.RevocationMode=System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;
                Check(!chain.Build(cert),"self-signed fixture unexpectedly trusted");
                var callback=socket.SslConfiguration.ServerCertificateValidationCallback;
                foreach(var errors in new[]{System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch,System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors,System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable})
                    Check(!callback(socket,cert,chain,errors),"accepted "+errors);
                Check(!callback(socket,null,null,System.Net.Security.SslPolicyErrors.None),"accepted absent certificate without errors");
            });
            Sync("TLS-004","genuine offline private-CA leaf chain validates without changing system trust",()=>{
                using var rootKey=RSA.Create(2048);
                var rootRequest=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=AllLive Offline Root",rootKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
                rootRequest.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension(true,false,0,true));
                rootRequest.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyCertSign,true));
                using var root=rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2),DateTimeOffset.UtcNow.AddDays(2));
                using var leafKey=RSA.Create(2048);
                var leafRequest=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=fixture.invalid",leafKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
                var san=new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();san.AddDnsName("fixture.invalid");leafRequest.CertificateExtensions.Add(san.Build());
                using var leaf=leafRequest.Create(root,DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1),new byte[]{1,2,3});
                using var chain=new System.Security.Cryptography.X509Certificates.X509Chain();
                chain.ChainPolicy.TrustMode=System.Security.Cryptography.X509Certificates.X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(root);chain.ChainPolicy.RevocationMode=System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;
                Check(chain.Build(leaf),"valid local root/leaf chain rejected");
                using var socket=new WebSocketSharp.WebSocket("wss://fixture.invalid/sub");WebSocketSecurity.Configure(socket);
                Check(socket.SslConfiguration.ServerCertificateValidationCallback(socket,leaf,chain,System.Net.Security.SslPolicyErrors.None),"valid certificate rejected");
            });
            Sync("SIGN-001","30-second expiry safety rejects boundary and accepts boundary+1",()=>{
                long now=1000000;Check(!Usable(Key(now+30),now),"expiry==now+30 accepted");Check(Usable(Key(now+31),now),"expiry==now+31 rejected");Check(!Usable(Key(now-1),now),"expired accepted");
            });
            Sync("SIGN-002","descriptor required fields and encryption-round bounds",()=>{
                Check(!Usable(null,1000),"null accepted"); foreach(int n in new[]{0,-1,17})Check(!Usable(Key(2000,n),1000),"invalid rounds "+n);
                foreach(int n in new[]{1,16})Check(Usable(Key(2000,n),1000),"valid rounds "+n);
                foreach(string field in new[]{"key","rand_str","enc_data"}){var k=Key(2000);k.Remove(field);Check(!Usable(k,1000),"missing "+field+" accepted");}
            });
            Sync("SIGN-003","cookie normalization replaces old device IDs without altering account value",()=>{
                DouyuSignHelper.AccountCookie=" Cookie: dy_did=old; ACF_DID=old2; token=a=b; ignored; empty=; =bad";
                var h=DouyuSignHelper.CookieHeader();Observation=h.Replace("token=a=b","token=<fixture>");
                Check(Regex.IsMatch(DouyuSignHelper.DeviceId,"^[0-9a-f]{32}$"),"invalid did");
                Check(h.Contains("dy_did="+DouyuSignHelper.DeviceId)&&h.Contains("acf_did="+DouyuSignHelper.DeviceId),"did mismatch");
                Check(!h.Contains("old")&&h.Contains("token=a=b")&&h.Contains("empty="),"normalization mismatch");DouyuSignHelper.AccountCookie="";
            });
            Add("SIGN-004","real MD5/form utilities sign ordinary descriptor and encode once",async()=>{
                ResetSign();FakeResponses();var f=Form(await DouyuSignHelper.BuildFormAsync("123",2,"cdn +/&"));
                Equal("a+/= &中文%25",f["enc_data"],"decoded enc_data");Equal("cdn +/&",f["cdn"],"decoded cdn");Equal("2",f["rate"],"rate");
                Equal(MD5Text(MD5Text("randomsecret")+"secret123"+f["tt"]),f["auth"],"auth");Equal(DouyuSignHelper.DeviceId,f["did"],"did");Equal(1,Count("GET"),"GET count");
            });
            Add("SIGN-005","special descriptor omits room/time salt",async()=>{
                ResetSign();HttpUtil.Reset(r=>Task.FromResult(KeyResponse(Key(rounds:2,special:1))));var f=Form(await DouyuSignHelper.BuildFormAsync("123"));
                Equal(MD5Text(MD5Text(MD5Text("randomsecret")+"secret")+"secret"),f["auth"],"special auth");
            });
            Add("SIGN-006","warm cache reuses descriptor",async()=>{
                ResetSign();FakeResponses();await DouyuSignHelper.BuildFormAsync("123");await DouyuSignHelper.BuildFormAsync("123");Equal(1,Count("GET"),"GET count");
            });
            Add("SIGN-007","cache age 299 seconds reused, 300 seconds refreshed",async()=>{
                bool stable=false;
                for(int attempt=0;attempt<10&&!stable;attempt++){
                    long now=CoreUtils.GetTimestamp();ResetSign(Key(),now-299);FakeResponses();await DouyuSignHelper.BuildFormAsync("123");
                    if(CoreUtils.GetTimestamp()==now){Equal(0,Count("GET"),"299s GET");stable=true;}
                }
                Check(stable,"inconclusive real-clock boundary");long at=CoreUtils.GetTimestamp();ResetSign(Key(),at-300);FakeResponses();await DouyuSignHelper.BuildFormAsync("123");Equal(1,Count("GET"),"300s GET");
            });
            Add("SIGN-008","eight concurrent cold-cache callers share one descriptor fetch",async()=>{
                ResetSign();var gate=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);HttpUtil.Reset(r=>gate.Task);
                var tasks=Enumerable.Range(0,8).Select(i=>DouyuSignHelper.BuildFormAsync("123")).ToArray();Equal(1,Count("GET"),"GETs before release");gate.SetResult(KeyResponse());await Task.WhenAll(tasks);Equal(1,Count("GET"),"total GETs");
            });
            Add("SIGN-009","invalid fetched descriptor throws and releases semaphore for next attempt",async()=>{
                ResetSign();HttpUtil.Reset(r=>Task.FromResult(KeyResponse(Key(CoreUtils.GetTimestamp()+29))));await Throws<Exception>(()=>DouyuSignHelper.BuildFormAsync("123"),"invalid key");
                FakeResponses();await DouyuSignHelper.BuildFormAsync("123");Equal(1,Count("GET"),"successful retry GET");
            });
            Add("SIGN-010","forced refresh fetches despite valid warm cache",async()=>{
                ResetSign(Key(),CoreUtils.GetTimestamp());FakeResponses();await DouyuSignHelper.BuildFormAsync("123",forceRefresh:true);Equal(1,Count("GET"),"forced GET");
            });
            Sync("URL-001","relative stream joined with base and HTML entity decoded",()=>{
                Equal("https://fixture.invalid/live/room.flv?a=1&b=2",Parse(new JObject{["rtmp_url"]="https://fixture.invalid/live/",["rtmp_live"]="/room.flv?a=1&amp;b=2"}),"URL");
            });
            Sync("URL-002","base directory alone rejected",()=>{Equal("",Parse(new JObject{["rtmp_url"]="https://fixture.invalid/live/"}),"base-only URL");});
            Sync("URL-003","direct flv_url media accepted, directory rejected",()=>{
                Equal("https://fixture.invalid/a.m3u8",Parse(new JObject{["flv_url"]="https://fixture.invalid/a.m3u8"}),"media");Equal("",Parse(new JObject{["flv_url"]="https://fixture.invalid/live/"}),"directory");
            });
            Sync("URL-004","rtmp_live bare CDN directory must not be playable (regression)",()=>{
                var actual=Parse(new JObject{["rtmp_live"]="https://fixture.invalid/live/"});Observation="actual="+actual;Equal("",actual,"bare directory URL");
            });
            Sync("URL-008","host-only, escaped slash, query-only directory rejected; signed stream path accepted",()=>{
                foreach(var url in new[]{"https://fixture.invalid", "https://fixture.invalid/?token=fixture", "https://fixture.invalid/live/?token=fixture", "https://fixture.invalid/live%2F"})
                    Equal("",Parse(new JObject{["rtmp_live"]=url,["rtmp_url"]="https://base.invalid/live/"}),"directory "+url);
                Equal("https://fixture.invalid/live/stream?token=fixture",Parse(new JObject{["rtmp_live"]="https://fixture.invalid/live/stream?token=fixture"}),"extensionless stream");
                Equal("rtmp://fixture.invalid/live/stream",Parse(new JObject{["rtmp_live"]="rtmp://fixture.invalid/live/stream"}),"RTMP stream");
            });
            Sync("URL-005","unsupported file URL is rejected",()=>{Equal("",Parse(new JObject{["rtmp_live"]="file:///tmp/local.flv"}),"file URL");});
            Sync("URL-006","empty CDN list provides default-route placeholder",()=>{
                var codes=(List<string>)CallPrivate(typeof(Douyu),"ParseCdnCodes",null,new JObject());Check(codes.SequenceEqual(new[]{""}),"default CDN missing");
            });
            Sync("URL-007","CDN codes trimmed and deduplicated, current code inserted",()=>{
                var d=new JObject{["cdnsWithName"]=new JArray(new JObject{["cdn"]=" one "},new JObject{["cdn"]="one"},new JObject{["cdn"]=""}),["rtmp_cdn"]="two"};
                var codes=(List<string>)CallPrivate(typeof(Douyu),"ParseCdnCodes",null,d);Check(codes.SequenceEqual(new[]{"two","one"}),"CDN order="+string.Join(",",codes));
            });
            Add("API-001","nonzero API errors limited to two attempts, second refreshes descriptor",async()=>{
                ResetSign();FakeResponses(r=>"{\"error\":500,\"msg\":\"fixture failure\"}");await Throws<Exception>(()=>new Douyu().GetPlayQuality(new LiveRoomDetail{RoomID="123"}),"API failure");Equal(2,Count("POST"),"POSTs");Equal(2,Count("GET"),"GETs");
            });
            Add("API-002","malformed JSON limited to two attempts",async()=>{
                ResetSign();FakeResponses(r=>"not json");await Throws<Exception>(()=>new Douyu().GetPlayQuality(new LiveRoomDetail{RoomID="123"}),"parse error");Equal(2,Count("POST"),"POSTs");Equal(2,Count("GET"),"GETs");
            });
            Add("API-003","successful response without data rejected after two attempts",async()=>{
                ResetSign();FakeResponses(r=>"{\"error\":0}");await Throws<Exception>(()=>new Douyu().GetPlayQuality(new LiveRoomDetail{RoomID="123"}),"missing data");Equal(2,Count("POST"),"POSTs");
            });
            Add("API-004","missing multirates returns empty quality list",async()=>{
                ResetSign();FakeResponses(r=>PlayResponse(new JObject()));var result=await new Douyu().GetPlayQuality(new LiveRoomDetail{RoomID="123"});Equal(0,result.Count,"quality count");Equal(1,Count("POST"),"POSTs");
            });
            Add("API-005","one failed CDN does not discard successful CDN URLs",async()=>{
                ResetSign();FakeResponses(r=>Form(r.Body)["cdn"]=="bad"?"{\"error\":500}":PlayResponse(Play("https://fixture.invalid/"+Form(r.Body)["cdn"]+".flv")));
                var quality=new LivePlayQuality{Data=new KeyValuePair<int,List<string>>(1,new(){"first","bad","last"})};var urls=await new Douyu().GetPlayUrls(new LiveRoomDetail{RoomID="123"},quality);
                Check(urls.SequenceEqual(new[]{"https://fixture.invalid/first.flv","https://fixture.invalid/last.flv"}),"URLs="+string.Join(",",urls));Equal(4,Count("POST"),"POST count");
            });
            Add("API-006","all failed CDNs return empty list with two requests per CDN",async()=>{
                ResetSign();FakeResponses(r=>"{\"error\":500}");var quality=new LivePlayQuality{Data=new KeyValuePair<int,List<string>>(1,new(){"a","b","c"})};var urls=await new Douyu().GetPlayUrls(new LiveRoomDetail{RoomID="123"},quality);
                Equal(0,urls.Count,"URLs");Equal(6,Count("POST"),"POST count");Observation="GET descriptors="+Count("GET")+", POST attempts="+Count("POST");
            });
            Add("API-007","empty recommendations and search are safe",async()=>{
                ResetSign();HttpUtil.Reset(r=>Task.FromResult("{\"data\":{}}"));var d=new Douyu();Equal(0,(await d.GetRecommendRooms()).Rooms.Count,"recommendations");Equal(0,(await d.Search("fixture")).Rooms.Count,"search");Equal(0,(await d.GetCategores()).Count,"categories");
            });
            Add("VM-001","same VM should load successfully after Stop (regression)",async()=>{
                var vm=VM();var site=new FakeSite();vm.LoadData(site,"one");await Until(()=>!vm.Loading,"initial load");Equal(1,site.Danmaku.Starts,"initial starts");vm.Stop();
                vm.LoadData(site,"two");await Until(()=>!vm.Loading,"reload");Observation="LastError="+(vm.LastError?.GetType().Name??"none")+", danmaku.Starts="+site.Danmaku.Starts+", qualityCalls="+site.QualityCalls;
                Check(vm.LastError==null,"Reload failed: "+vm.LastError?.GetType().Name+" "+vm.LastError?.Message);Equal(2,site.Danmaku.Starts,"reload starts");
            });
            Add("VM-002","older quality response must not replace latest quality URL (regression)",async()=>{
                var vm=VM();var site=new FakeSite();SetupVm(vm,site);var a=new TaskCompletionSource<List<string>>();var b=new TaskCompletionSource<List<string>>();
                site.Urls=(room,q)=>q.Quality=="A"?a.Task:b.Task;var qa=new LivePlayQuality{Quality="A"};var qb=new LivePlayQuality{Quality="B"};
                vm.CurrentQuality=qa;vm.CurrentQuality=qb;b.SetResult(new(){"https://fixture.invalid/B.flv"});await Until(()=>vm.CurrentLine?.Url?.EndsWith("B.flv")==true,"B completion");
                a.SetResult(new(){"https://fixture.invalid/A.flv"});await Task.Delay(30);Observation="CurrentQuality="+vm.CurrentQuality.Quality+", CurrentLine="+vm.CurrentLine.Url;
                Equal("https://fixture.invalid/B.flv",vm.CurrentLine.Url,"latest URL");vm.Stop();
            });
            Add("VM-003","completed URL request must not emit playback after Stop (regression)",async()=>{
                var vm=VM();var site=new FakeSite();SetupVm(vm,site);var pending=new TaskCompletionSource<List<string>>();site.Urls=(room,q)=>pending.Task;
                int events=0;vm.ChangedPlayUrl+=(s,u)=>events++;vm.CurrentQuality=new LivePlayQuality{Quality="A"};vm.Stop();pending.SetResult(new(){"https://fixture.invalid/late.flv"});await Task.Delay(30);
                Observation="ChangedPlayUrl events after Stop="+events;Equal(0,events,"late playback events");
            });
            Add("VM-004","empty URL response preserves existing line and shows an error",async()=>{
                var vm=VM();var site=new FakeSite();SetupVm(vm,site);var old=new PlayurlLine{Url="https://fixture.invalid/old.flv"};vm.Lines=new(){old};vm.CurrentLine=old;
                int before=AllLive.UWP.Helper.Utils.Toasts.Count;vm.CurrentQuality=new LivePlayQuality{Quality="Empty"};await Task.Delay(10);Equal(old,vm.CurrentLine,"current line");Equal(before+1,AllLive.UWP.Helper.Utils.Toasts.Count,"toast count");vm.Stop();
            });
            Add("VM-005","stopped room-detail response cannot start danmaku or update room",async()=>{
                var vm=VM();var site=new FakeSite();var pending=new TaskCompletionSource<LiveRoomDetail>();site.RoomDetails=room=>pending.Task;
                vm.LoadData(site,"late");vm.Stop();pending.SetResult(new LiveRoomDetail{RoomID="late",Status=true});await Task.Delay(30);
                Equal(0,site.Danmaku.Starts,"late start");Check(vm.RoomID!="late","late room written");Check(!vm.Loading,"loading remained true");
            });
            Add("VM-006","new room result wins over older pending room result",async()=>{
                var vm=VM();var site=new FakeSite();var a=new TaskCompletionSource<LiveRoomDetail>();site.RoomDetails=room=>room.ToString()=="old"?a.Task:Task.FromResult(new LiveRoomDetail{RoomID="new",Status=true});
                vm.LoadData(site,"old");vm.LoadData(site,"new");await Until(()=>!vm.Loading,"new room");a.SetResult(new LiveRoomDetail{RoomID="old",Status=true});await Task.Delay(30);
                Equal("new",vm.RoomID,"room");Equal(1,site.Danmaku.Starts,"start count");vm.Stop();
            });
            Add("VM-007","reload waits for old danmaku Stop and survives late stop completion",async()=>{
                var vm=VM();var site=new FakeSite();vm.LoadData(site,"one");await Until(()=>!vm.Loading,"first room");var stop=new TaskCompletionSource<bool>();site.Danmaku.StopHandler=()=>stop.Task;
                vm.Stop();vm.LoadData(site,"two");Equal(1,site.Danmaku.Starts,"starts before stop release");stop.SetResult(true);await Until(()=>!vm.Loading,"second room");
                Equal(2,site.Danmaku.Starts,"starts after stop release");Check(typeof(LiveRoomVM).GetField("LiveDanmaku",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm)!=null,"late Stop cleared new danmaku");vm.Stop();
            });
            Add("VM-008","old failed URL request cannot emit a stale error toast",async()=>{
                var vm=VM();var site=new FakeSite();SetupVm(vm,site);var old=new TaskCompletionSource<List<string>>();site.Urls=(r,q)=>q.Quality=="A"?old.Task:Task.FromResult(new List<string>{"https://fixture.invalid/B.flv"});
                vm.CurrentQuality=new LivePlayQuality{Quality="A"};vm.CurrentQuality=new LivePlayQuality{Quality="B"};int before=AllLive.UWP.Helper.Utils.Toasts.Count;
                old.SetException(new Exception("old failure"));await Task.Delay(30);Equal(before,AllLive.UWP.Helper.Utils.Toasts.Count,"stale toast count");Equal("https://fixture.invalid/B.flv",vm.CurrentLine.Url,"new URL");vm.Stop();
            });
            Add("VM-009","queued old message batch is discarded after Stop and reload",async()=>{
                var vm=VM();var site=new FakeSite();vm.LoadData(site,"one");await Until(()=>!vm.Loading,"first room");
                var timer=(System.Timers.Timer)typeof(LiveRoomVM).GetField("_messageProcessTimer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm);timer.Stop();vm.Dispatcher.Defer=true;
                int events=0;vm.AddDanmaku+=(s,m)=>events++;site.Danmaku.Emit(new LiveMessage{Type=LiveMessageType.Chat,Message="old"});CallPrivate(typeof(LiveRoomVM),"ProcessMessageQueue",vm,timer,null);
                Check(vm.Dispatcher.Pending.Count>0,"fixture did not queue a batch");vm.Stop();vm.LoadData(site,"two");await Until(()=>!vm.Loading,"reload");vm.Dispatcher.Drain();
                Equal(0,events,"old batch playback events");Check(!vm.Messages.Any(m=>m.Message=="old"),"old chat appended after reload");vm.Stop();
            });
            Add("VM-010","queued old online event cannot modify a restarted room",async()=>{
                var vm=VM();var site=new FakeSite();vm.LoadData(site,"one");await Until(()=>!vm.Loading,"first room");vm.Dispatcher.Defer=true;
                site.Danmaku.Emit(new LiveMessage{Type=LiveMessageType.Online,Data=123456L});vm.Stop();vm.LoadData(site,"two");await Until(()=>!vm.Loading,"reload");vm.Dispatcher.Drain();
                Check(vm.Online!=123456L,"old online value replaced restarted room");vm.Stop();
            });
            Add("VM-011","SC countdown works when settings load before room navigation",async()=>{
                AllLive.UWP.Helper.SettingHelper.KeepSuperChat=false;var vm=VM();
                try {
                    vm.SetSCTimer();var site=new FakeSite();vm.LoadData(site,"one");await Until(()=>!vm.Loading,"room after settings");
                    var item=new SuperChatItem(new LiveSuperChatMessage{EndTime=DateTime.Now.AddSeconds(5)},true){CountdownTime=2};vm.SuperChatMessages.Add(item);
                    await Until(()=>item.CountdownTime<2,"SC countdown after settings-before-room");
                    await Until(()=>!vm.SuperChatMessages.Contains(item),"SC expiration removal");
                } finally {vm.Stop();AllLive.UWP.Helper.SettingHelper.KeepSuperChat=null;}
            });
            Add("VM-012","SC countdown timer is recreated after Stop and refresh",async()=>{
                AllLive.UWP.Helper.SettingHelper.KeepSuperChat=false;var vm=VM();
                try {
                    var site=new FakeSite();vm.LoadData(site,"one");await Until(()=>!vm.Loading,"initial room");vm.Stop();vm.LoadData(site,"two");await Until(()=>!vm.Loading,"refreshed room");
                    var timer=(System.Timers.Timer)typeof(LiveRoomVM).GetField("scTimer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm);Check(timer!=null&&timer.Enabled,"SC timer not restarted");
                    var item=new SuperChatItem(new LiveSuperChatMessage{EndTime=DateTime.Now.AddSeconds(5)},true){CountdownTime=2};vm.SuperChatMessages.Add(item);
                    await Until(()=>item.CountdownTime<2,"SC countdown after refresh");
                } finally {vm.Stop();AllLive.UWP.Helper.SettingHelper.KeepSuperChat=null;}
            });
            Add("VM-013","old message enqueued after Stop remains stale in the new timer",async()=>{
                var vm=VM();var site=new FakeSite();vm.LoadData(site,"one");await Until(()=>!vm.Loading,"first room");
                int oldGeneration=(int)typeof(LiveRoomVM).GetField("_loadGeneration",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm);
                vm.Stop();vm.LoadData(site,"two");await Until(()=>!vm.Loading,"new room");
                var timer=(System.Timers.Timer)typeof(LiveRoomVM).GetField("_messageProcessTimer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm);timer.Stop();
                var queue=typeof(LiveRoomVM).GetField("_messageQueue",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm);
                var message=new LiveMessage{Type=LiveMessageType.Chat,Message="late-old-enqueue"};
                queue.GetType().GetMethod("Enqueue").Invoke(queue,new object[]{new KeyValuePair<int,LiveMessage>(oldGeneration,message)});
                int emitted=0;vm.AddDanmaku+=(s,m)=>emitted++;CallPrivate(typeof(LiveRoomVM),"ProcessMessageQueue",vm,timer,null);
                Equal(0,emitted,"old late enqueued message");Check(!vm.Messages.Any(m=>m.Message==message.Message),"old late enqueue displayed");vm.Stop();
            });
            Add("VM-014","queued old SC tick cannot decrement refreshed room messages",async()=>{
                AllLive.UWP.Helper.SettingHelper.KeepSuperChat=false;var vm=VM();
                try {
                    var site=new FakeSite();vm.LoadData(site,"one");await Until(()=>!vm.Loading,"initial room");
                    var item=new SuperChatItem(new LiveSuperChatMessage{EndTime=DateTime.Now.AddMinutes(1)},true){CountdownTime=60};vm.SuperChatMessages.Add(item);vm.Dispatcher.Defer=true;
                    await Until(()=>vm.Dispatcher.Pending.Count>0,"queued old SC tick");vm.Stop();vm.LoadData(site,"two");await Until(()=>!vm.Loading,"refreshed room");
                    var oldTick=vm.Dispatcher.Pending.Dequeue();oldTick();Equal(60,item.CountdownTime,"old SC tick mutated refreshed room");vm.Dispatcher.Drain();
                } finally {vm.Stop();AllLive.UWP.Helper.SettingHelper.KeepSuperChat=null;}
            });
        }
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("AllLive offline source harness | commit="+Commit);
            Console.WriteLine("Patched production targets are linked; HTTP/WebSocket/database/UI are isolation stubs. No platform playback is claimed.");
            Console.WriteLine("Runtime="+System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription+" OS="+System.Runtime.InteropServices.RuntimeInformation.OSDescription);
            Register();int failures=0;
            foreach(var test in Tests){Observation=null;string status="PASS",error=null;
                try { await test.Run(); } catch(Exception e) {status="FAIL";failures++;error=e.ToString();}
                Console.WriteLine(status+" "+test.Id+" "+test.Description+(Observation==null?"":" | "+Observation));
                if(error!=null)Console.WriteLine("  "+error.Split('\n')[0]);
                Results.Add(new {id=test.Id,description=test.Description,status,observation=Observation,error});
            }
            var report=new {commit=Commit,utc=DateTime.UtcNow,scope="Linked patched production C# under .NET 8; dependencies stubbed; no UWP runtime or external requests",tests=Tests.Count,passed=Tests.Count-failures,failed=failures,results=Results};
            var output=args.Length>0?args[0]:"results.json";File.WriteAllText(output,JsonConvert.SerializeObject(report,Formatting.Indented));
            Console.WriteLine($"SUMMARY total={Tests.Count} passed={Tests.Count-failures} failed={failures}; full UWP build/runtime: NOT RUN");return failures==0?0:1;
        }
    }
}
