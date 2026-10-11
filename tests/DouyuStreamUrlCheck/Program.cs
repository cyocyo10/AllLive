using AllLive.Core.Helper;
var cases = new (string Input, string Expected)[] {
    ("https://cdn/live.flv?expire=300&fcdn=ws", "https://cdn/live.flv?expire=300&fcdn=ws&expire=0"),
    ("https://cdn/live.flv?fcdn=ws&expire=300&token=a%2Fb+Z#part", "https://cdn/live.flv?fcdn=ws&expire=300&token=a%2Fb+Z&expire=0#part"),
    ("https://cdn/live.flv?expire=300&fcdn=ws&", "https://cdn/live.flv?expire=300&fcdn=ws&expire=0"),
    ("", ""),
    ("https://cdn/live.flv", "https://cdn/live.flv"),
    ("https://cdn/live.flv?expire=300&fcdn=hw", "https://cdn/live.flv?expire=300&fcdn=hw"),
    ("https://cdn/live.flv?expire=3000&fcdn=ws", "https://cdn/live.flv?expire=3000&fcdn=ws"),
    ("https://cdn/live.flv?expire=300&fcdn=ws2", "https://cdn/live.flv?expire=300&fcdn=ws2"),
    ("https://cdn/live.flv?xexpire=300&fcdn=ws", "https://cdn/live.flv?xexpire=300&fcdn=ws"),
    ("https://cdn/live.flv?expire=300&fcdn=ws&expire=0", "https://cdn/live.flv?expire=300&fcdn=ws&expire=0"),
    ("https://cdn/live.flv?expire=300&fcdn=ws&fcdn=hw", "https://cdn/live.flv?expire=300&fcdn=ws&fcdn=hw"),
    ("https://cdn/live.flv#expire=300&fcdn=ws", "https://cdn/live.flv#expire=300&fcdn=ws"),
    ("https://cdn/live.flv?note=expire=300&fcdn=ws", "https://cdn/live.flv?note=expire=300&fcdn=ws"),
};
foreach (var c in cases) {
 var actual = DouyuStreamUrl.Normalize(c.Input);
 if (actual != c.Expected || DouyuStreamUrl.Normalize(actual) != actual) throw new Exception("URL normalization mismatch");
}
Console.WriteLine($"{cases.Length} URL cases plus idempotence passed");
