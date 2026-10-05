using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using AllLive.Core.Danmaku;
using AllLive.Core.Interface;
using Newtonsoft.Json;
using WebSocketSharp;

class Program
{
    static void Check(bool ok,string text) { if(!ok) throw new Exception(text); }
    static async Task<int> Main(string[] args)
    {
        var results=new List<object>();int failures=0;
        using var key=RSA.Create(2048);
        var request=new CertificateRequest("CN=fixture.invalid",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
        foreach(var name in new[]{"BiliBili","Douyu","Huya","Douyin"})
        foreach(var valid in new[]{false,true})
        {
            string error=null;ILiveDanmaku site=null;int headersRecorded=0;
            try
            {
                WebSocket.Reset(socket=>{
                    var callback=socket.SslConfiguration.ServerCertificateValidationCallback;
                    Check(callback!=null,"Missing explicit validator before Connect");
                    Check(socket.SslConfiguration.EnabledSslProtocols==System.Security.Authentication.SslProtocols.Tls12,"TLS protocol not configured");
                    var errors=valid?SslPolicyErrors.None:SslPolicyErrors.RemoteCertificateNameMismatch|SslPolicyErrors.RemoteCertificateChainErrors;
                    bool accepted=callback(socket,certificate,null,errors);
                    Check(accepted==valid,"Certificate decision differs from expected platform result");
                    if(!accepted){socket.FailTls();return;}
                    if(socket.CustomHeaders!=null && socket.CustomHeaders.ContainsKey("Cookie"))headersRecorded++;
                    socket.Open();
                });
                object startArgs;
                switch(name)
                {
                    case "BiliBili":site=new BiliBiliDanmaku();startArgs=new BiliDanmakuArgs{RoomId=1,Cookie="fixture-cookie-only"};break;
                    case "Douyu":site=new DouyuDanmaku();startArgs="1";break;
                    case "Huya":site=new HuyaDanmaku();startArgs=new HuyaDanmakuArgs(1,2,3);break;
                    default:var douyin=new DouyinDanmaku();douyin.SetSignatureProvider((r,u)=>Task.FromResult("fixture-sign"));site=douyin;startArgs=new DouyinDanmakuArgs{RoomId="1",UserId="2",Cookie="fixture-cookie-only"};break;
                }
                await site.Start(startArgs);await Task.Delay(30);
                Check(WebSocket.ConnectCalls==1,"TLS failure caused an immediate retry/downgrade");
                if(!valid)Check(headersRecorded==0,"Cookie headers transmitted before certificate accepted");
                if(valid && (name=="BiliBili"||name=="Douyin"))Check(headersRecorded==1,"Valid TLS did not retain cookie header");
                await site.Stop();
            }
            catch(Exception e){error=e.ToString();failures++;if(site!=null)await site.Stop();}
            string status=error==null?"PASS":"FAIL";
            Console.WriteLine($"{status} {name} {(valid?"valid certificate":"invalid certificate")} headersRecorded={headersRecorded}");
            if(error!=null)Console.WriteLine(error);
            results.Add(new {site=name,validCertificate=valid,status,error,headersRecorded});
        }
        File.WriteAllText(args.Length>0?args[0]:"results.json",JsonConvert.SerializeObject(new {tests=8,passed=8-failures,failed=failures,scope="All four real production danmaku classes with no-network transport and HTTP fixtures; shared production validator invoked before simulated HTTP headers; not a real TLS handshake or MITM",results},Formatting.Indented));
        return failures==0?0:1;
    }
}
