using System.Net;
using System.Net.Sockets;
using System.Text;
using AllLive.Core.Helper;
var listener = new TcpListener(IPAddress.Loopback,0); listener.Start();
var url=$"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
var cookies=new List<string>();
var server=Task.Run(async()=>{
 for(int i=0;i<4;i++) {
  using var client=await listener.AcceptTcpClientAsync();
  using var stream=client.GetStream();
  using var reader=new StreamReader(stream,Encoding.ASCII,leaveOpen:true);
  string line; string cookie=""; int length=0;
  while(!string.IsNullOrEmpty(line=await reader.ReadLineAsync())) {
   if(line.StartsWith("Cookie:",StringComparison.OrdinalIgnoreCase)) cookie=line.Substring(7).Trim();
   if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)) length=int.Parse(line.Substring(15));
  }
  if(length>0) { var chars=new char[length]; await reader.ReadBlockAsync(chars,0,length); }
  cookies.Add(cookie);
  var response="HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\nSet-Cookie: old_account=server-token; Path=/\r\n\r\n{}";
  await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
 }
});
await HttpUtil.GetString(url); // Seed the legacy shared jar.
await HttpUtil.GetString(url,new Dictionary<string,string>{{"cookie","dy_did=B"}},useCookies:false);
await HttpUtil.PostFormUrlEncodedString(url,"rate=0",new Dictionary<string,string>{{"cookie","dy_did=C"}},useCookies:false);
await HttpUtil.GetString(url); // Other providers' legacy transport stays unchanged.
await server; listener.Stop();
if(cookies[1]!="dy_did=B" || cookies[2]!="dy_did=C") throw new Exception("Snapshot transport leaked inherited cookies");
if(!cookies[3].Contains("old_account=")) throw new Exception("Legacy transport behavior changed");
Console.WriteLine("PASS stateless GET excludes legacy and server cookies");
Console.WriteLine("PASS stateless POST excludes legacy and server cookies");
Console.WriteLine("PASS default transport preserves existing cookie behavior");
