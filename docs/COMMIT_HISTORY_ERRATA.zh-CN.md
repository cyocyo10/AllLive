# AllLive 提交历史勘误

本文以追加文档的方式，记录15个历史提交说明与当时改动不一致的地方。原提交SHA、提交顺序、原作者与提交者信息、日期以及原始消息全部保留；上游历史不作改写。

**本文是提交说明勘误，不是源码修复记录。** 文中“准确表述”只描述该提交当时实际完成的工作，不表示列出的问题已在当前代码中修复，也不表示相关功能已通过运行验证。后续源码修复应以独立提交及其验证结果为准。

## 核查范围

核查日期：2026-10-06。

- 上游边界：[xiaoyaocz/AllLive 6d513c040003875d1adc0915813f7009b5d62840](https://github.com/xiaoyaocz/AllLive/commit/6d513c040003875d1adc0915813f7009b5d62840)。该提交及其祖先不属于本次勘误范围。
- 历史终点：[c6951df959b7ead0d93edc087188788b49f1e273](https://github.com/cyocyo10/AllLive/commit/c6951df959b7ead0d93edc087188788b49f1e273)。从上游边界之后至该提交，共83个连续、无merge的fork提交。
- 仓库快照：[0e80f7a943c53eb46df15f1ab453049b10ef4076](https://github.com/cyocyo10/AllLive/commit/0e80f7a943c53eb46df15f1ab453049b10ef4076)。它紧接c6951df，仅删除内容为空对象的`.vscode/settings.json`；其tree为`7ccf284244e8f294a0f41fdfce6188e258a3c9ec`。本次文案核查对象为前述83项，不把这次删除算作旧说明问题。

结果：15项确证勘误，65项未见确证说明错误，另3项只有数字标题、信息不足。文中“序号”是上述83项按先后顺序排列的核查序号，不是替换提交SHA的新编号。附录列出全部83项。

判断以每个提交自己的父子diff及该SHA下的源码为准：当时真实实现、后来被回滚的功能，不因此判为原说明错误；之后其他分支新增的修复，也不倒写进旧提交。

### 核查边界

- 本文是静态说明核查，没有在每个历史版本的Windows/UWP环境中重新构建或复现平台播放、登录、安装行为。没有发现说明错误，不等于代码没有缺陷。
- 第1项新增的大型`webmssdk.js`没有可用文本patch，已读取该SHA的完整文件并核对入口与出口；没有对压缩脚本进行完整算法审计。其余可用patch的增删行数均与提交统计吻合，未发现截断。
- 早期部分源文件采用GBK编码。diff中的替换字符不能直接当作源码乱码，相关判断已结合该SHA的源码内容核对。
- 第26项“恢复抖音搜索a_bogus签名支持”证据不足以确认为错：父版本已有调用，但不能排除本次Cookie或Referer调整恢复了原本失效的功能。因此不作纠错。

## 勘误索引

| 核查序号 | 原提交 | 勘误主题 |
| ---: | --- | --- |
| 4 | [b728f75b5](https://github.com/cyocyo10/AllLive/commit/b728f75b5c04c2f4d940f12023a775ac80814b8a) | 本地签名仍保留远程回退 |
| 27 | [e2fb9de11](https://github.com/cyocyo10/AllLive/commit/e2fb9de1118f6f8eaca0b3b5a021511597e59588) | 请求释放与响应释放混淆 |
| 30 | [4760545e4](https://github.com/cyocyo10/AllLive/commit/4760545e4e8562e5eb9f4b3e76bccd0cf192b5a4) | timer原本就在锁内 |
| 43 | [b6ff2766c](https://github.com/cyocyo10/AllLive/commit/b6ff2766cb3bfcbb8f1c737d1df6591cbadd3c15) | 收藏格式化误归到后一提交 |
| 49 | [d7c886977](https://github.com/cyocyo10/AllLive/commit/d7c8869777cb085b8535c1bf6a0c7c73c3a55ad9) | FavoriteVM修复误归到后一提交 |
| 57 | [da98f24c0](https://github.com/cyocyo10/AllLive/commit/da98f24c037c58f75bd05fe9a050f76d00ca8d08) | 数据库编码改动的原因与作用范围 |
| 60 | [73eda5fd3](https://github.com/cyocyo10/AllLive/commit/73eda5fd3da14525ba1bf9acd7585c5de213cd62) | PlaybackSession事件数量 |
| 61 | [7e4bd221e](https://github.com/cyocyo10/AllLive/commit/7e4bd221eaf2b5a307b815b8e59704eea227cb7a) | 缓存页面生命周期与重复清理 |
| 67 | [0c42e5366](https://github.com/cyocyo10/AllLive/commit/0c42e536606693a975ce7a900239d57905ff8934) | 单行类型转换误写为三项修复 |
| 70 | [d672b0606](https://github.com/cyocyo10/AllLive/commit/d672b0606d6313a67bc26c993dca6277b4c8b91a) | Brotli降级时机与表单失败条件 |
| 72 | [3fcb08f6b](https://github.com/cyocyo10/AllLive/commit/3fcb08f6bac1fc199f6439c80368a1ae8f0442f9) | JSON及数组保护范围夸大 |
| 76 | [10c53d63e](https://github.com/cyocyo10/AllLive/commit/10c53d63e42db7d48d6a6aab51ad09c4d394cbaa) | 异步范围 删除循环及日志格式 |
| 78 | [47ce2b0c6](https://github.com/cyocyo10/AllLive/commit/47ce2b0c6e0e43f1ec73ad003bfae9af8976252d) | 斗鱼请求头及播放地址校验范围 |
| 79 | [4f505ac99](https://github.com/cyocyo10/AllLive/commit/4f505ac99b5ff5c4f0b4c7a28fbe339c7a7676d1) | 历史页await续体的因果解释 |
| 83 | [c6951df95](https://github.com/cyocyo10/AllLive/commit/c6951df959b7ead0d93edc087188788b49f1e273) | 关注页await续体的因果解释 |

## 逐条勘误

### 4 本地签名仍保留远程回退

- 原提交：[b728f75b5c04c2f4d940f12023a775ac80814b8a](https://github.com/cyocyo10/AllLive/commit/b728f75b5c04c2f4d940f12023a775ac80814b8a)
- 原主题：feat: run douyu and douyin flows fully offline
- 原作者：cyocyo10；作者时间：2025-11-14T15:45:46Z。

**需勘误的原文**

> feat: run douyu and douyin flows fully offline

**当时diff与源码证据**

“fully offline”即使仅指签名流程也过于绝对：本地抖音弹幕签名为空或返回00000000时，仍会进入GetSign远程回退。斗鱼UWP与抖音a_bogus接入本地运行时的改动是真实存在的。

- [本地签名失败后调用GetSign](https://github.com/cyocyo10/AllLive/blob/b728f75b5c04c2f4d940f12023a775ac80814b8a/AllLive.Core/Danmaku/DouyinDanmaku.cs#L336-L348)：DefaultSignatureProvider中的fallback调用。
- [远程签名端点](https://github.com/cyocyo10/AllLive/blob/b728f75b5c04c2f4d940f12023a775ac80814b8a/AllLive.Core/Danmaku/DouyinDanmaku.cs#L478-L485)：GetSign继续调用https://dy.nsapps.cn/signature。
- [本提交完整diff](https://github.com/cyocyo10/AllLive/commit/b728f75b5c04c2f4d940f12023a775ac80814b8a)：斗鱼UWP改为DouyuSignRuntime.Current.GenerateSignAsync。

**准确表述**

```text
feat: 接入斗鱼和抖音本地签名运行时

斗鱼 UWP 签名与抖音 a_bogus 改为本地执行；抖音弹幕本地签名失败时仍保留远程回退。
```

### 27 请求释放与响应释放混淆

- 原提交：[e2fb9de1118f6f8eaca0b3b5a021511597e59588](https://github.com/cyocyo10/AllLive/commit/e2fb9de1118f6f8eaca0b3b5a021511597e59588)
- 原主题：fix: 修复多个潜在bug
- 原作者：cyocyo10；作者时间：2026-01-02T14:38:22Z。

**需勘误的原文**

> - 修复HttpUtil.Head方法可能导致response被提前dispose的问题

**当时diff与源码证据**

父版本的using包裹HttpRequestMessage，而不是返回的HttpResponseMessage。删除该using不能被解释为修复响应被提前释放；原响应对象不由该using释放。

- [本提交HttpUtil.Head](https://github.com/cyocyo10/AllLive/blob/e2fb9de1118f6f8eaca0b3b5a021511597e59588/AllLive.Core/Helper/HttpUtil.cs#L141-L155)：移除request的using，response仍由SendAsync创建并直接返回。
- [官方HttpRequestMessage实现](https://github.com/dotnet/corefx/blob/release/2.0.0/src/System.Net.Http/src/System/Net/Http/HttpRequestMessage.cs)：Dispose释放请求自身的_content，没有释放响应对象。

**准确表述**

```text
fix: 修复多个潜在bug

- 修复抖音弹幕WebSocket Header拼写错误 (User-Agnet -> User-Agent)
- 移除HttpUtil.Head中对request的using作用域
- 修复FavoriteVM并发加载状态时的线程安全问题 (使用Interlocked)
- 修复所有弹幕实现中Stop方法的空引用异常 (添加null检查)
```

### 30 timer原本就在锁内

- 原提交：[4760545e4e8562e5eb9f4b3e76bccd0cf192b5a4](https://github.com/cyocyo10/AllLive/commit/4760545e4e8562e5eb9f4b3e76bccd0cf192b5a4)
- 原主题：fix: 修复抖音弹幕连接问题
- 原作者：cyocyo10；作者时间：2026-01-02T14:52:42Z。

**需勘误的原文**

> - 修复ConnectAsync中timer操作的缩进问题（之前在lock块外）

**当时diff与源码证据**

父版本的timer停止、释放和重建语句已经位于lock(connectionLock)的大括号中。本提交只调整这些语句的缩进，没有改变临界区。

- [父版本ConnectAsync](https://github.com/cyocyo10/AllLive/blob/684d68a89ce534ab503287aeb7dfd7539bb57a46/AllLive.Core/Danmaku/DouyinDanmaku.cs#L341-L380)：lock从350行开始；timer语句在368–370行，376行调用Connect，377行才关闭lock。
- [本提交完整diff](https://github.com/cyocyo10/AllLive/commit/4760545e4e8562e5eb9f4b3e76bccd0cf192b5a4)：timer相关三行仅前导空格改变。

**准确表述**

```text
fix: 修复抖音弹幕连接问题

- 修复ConnectAsync中timer操作的缩进问题
- 增强WebViewDouyinScriptRunner的错误处理和重试逻辑
- 添加更多调试日志帮助定位问题
```

### 43 收藏格式化误归到后一提交

- 原提交：[b6ff2766cb3bfcbb8f1c737d1df6591cbadd3c15](https://github.com/cyocyo10/AllLive/commit/b6ff2766cb3bfcbb8f1c737d1df6591cbadd3c15)
- 原主题：feat: 历史记录添加导出导入功能 - 支持导出为格式化JSON - 支持从JSON导入 - 收藏导出也改为格式化JSON
- 原作者：cyocyo10；作者时间：2026-01-04T13:34:36Z。

**需勘误的原文**

> 收藏导出也改为格式化JSON

**当时diff与源码证据**

收藏JSON格式化已在紧邻的父提交b5cabe138完成。本提交新增的是历史记录导入导出；父子版本的FavoriteVM.cs blob相同，不能将收藏格式化再次记作本提交的改动。

- [父提交已格式化收藏JSON](https://github.com/cyocyo10/AllLive/blob/b5cabe13805fce4793123779b32966163f8b38b3/AllLive.UWP/ViewModels/FavoriteVM.cs#L214-L224)：SerializeObject使用Formatting.Indented。
- [本提交同一文件](https://github.com/cyocyo10/AllLive/blob/b6ff2766cb3bfcbb8f1c737d1df6591cbadd3c15/AllLive.UWP/ViewModels/FavoriteVM.cs#L214-L224)：父子blob均为bb87ebaae25cf47a166986360e7acfcf997c71af。
- [本提交完整diff](https://github.com/cyocyo10/AllLive/commit/b6ff2766cb3bfcbb8f1c737d1df6591cbadd3c15)：只改HistoryVM.cs和HistoryPage.xaml。

**准确表述**

```text
feat: 历史记录添加导出导入功能 - 支持导出为格式化JSON - 支持从JSON导入
```

### 49 FavoriteVM修复误归到后一提交

- 原提交：[d7c8869777cb085b8535c1bf6a0c7c73c3a55ad9](https://github.com/cyocyo10/AllLive/commit/d7c8869777cb085b8535c1bf6a0c7c73c3a55ad9)
- 原主题：fix: 修复日志乱码问题，修复FavoriteVM线程安全问题
- 原作者：cyocyo10；作者时间：2026-01-11T09:30:54Z。

**需勘误的原文**

> 修复FavoriteVM线程安全问题

**当时diff与源码证据**

FavoriteVM集合更新的Dispatcher改动已在父提交9ce2bb2c完成。本提交只改动DouyinDanmaku.cs和Douyin.cs；父子FavoriteVM.cs blob相同。

- [父提交的UI调度](https://github.com/cyocyo10/AllLive/blob/9ce2bb2c23ac752b501028b3c07ccdd72e1ea748/AllLive.UWP/ViewModels/FavoriteVM.cs#L101-L115)：集合更新已位于CoreApplication.MainView.Dispatcher.RunAsync中。
- [本提交同一文件](https://github.com/cyocyo10/AllLive/blob/d7c8869777cb085b8535c1bf6a0c7c73c3a55ad9/AllLive.UWP/ViewModels/FavoriteVM.cs#L101-L115)：父子blob均为ba308f2fa5206b8276adacc923561d2324fbfe87。
- [本提交完整diff](https://github.com/cyocyo10/AllLive/commit/d7c8869777cb085b8535c1bf6a0c7c73c3a55ad9)：只改两个抖音源码文件，没有FavoriteVM改动。

**准确表述**

```text
fix: 修复日志乱码问题
```

### 57 数据库编码改动的原因与作用范围

- 原提交：[da98f24c037c58f75bd05fe9a050f76d00ca8d08](https://github.com/cyocyo10/AllLive/commit/da98f24c037c58f75bd05fe9a050f76d00ca8d08)
- 原主题：修复数据库字符编码问题 - 添加 UTF-8 支持
- 原作者：cyocyo10；作者时间：2026-01-17T11:01:01Z。

**需勘误的原文**

> 修复数据库字符编码问题 - 添加 UTF-8 支持

> 原因：SQLite 连接字符串缺少编码设置

> 3. 防止新数据出现乱码

> 注意：此修复只影响新数据，已有的乱码数据需要清理后重新添加

**当时diff与源码证据**

SqliteConnectionStringBuilder只设置DataSource和Mode，未新增字符转换。PRAGMA encoding只决定尚未创建的数据库采用何种编码，不会转换已有数据库，也不改变已有库之后插入记录的编码。该改动不能证明“缺少连接字符串编码设置”就是乱码原因，更不能泛称防止新数据乱码。

- [连接与PRAGMA改动](https://github.com/cyocyo10/AllLive/blob/da98f24c037c58f75bd05fe9a050f76d00ca8d08/AllLive.UWP/Helper/DatabaseHelper.cs#L19-L35)：构建器只设置DataSource和Mode。
- [写入语句](https://github.com/cyocyo10/AllLive/blob/da98f24c037c58f75bd05fe9a050f76d00ca8d08/AllLive.UWP/Helper/DatabaseHelper.cs#L67-L75)：仍直接绑定item.SiteName，未增加乱码转换。
- [SQLite编码规则](https://www.sqlite.org/pragma.html#pragma_encoding)：数据库创建后encoding不能改变；再设置会被忽略。

**准确表述**

```text
使用连接字符串构建器并显式设置新建数据库的 UTF-8 编码

- 使用 SqliteConnectionStringBuilder 设置数据库路径和读写创建模式
- 在建表前执行 PRAGMA encoding = 'UTF-8'

此设置针对新建数据库；不会改变已有数据库的编码或修复已存储的乱码。
```

### 60 PlaybackSession事件数量

- 原提交：[73eda5fd3da14525ba1bf9acd7585c5de213cd62](https://github.com/cyocyo10/AllLive/commit/73eda5fd3da14525ba1bf9acd7585c5de213cd62)
- 原主题：修复页面事件未取消订阅导致的内存泄漏
- 原作者：cyocyo10；作者时间：2026-01-17T11:07:58Z。

**需勘误的原文**

> - 取消 PlaybackSession 的 5 个事件

**当时diff与源码证据**

该版本订阅和取消订阅的PlaybackSession事件均为4个：PlaybackStateChanged、BufferingStarted、BufferingProgressChanged、BufferingEnded。MediaPlayer本身的3个事件另计。

- [当时订阅的事件](https://github.com/cyocyo10/AllLive/blob/73eda5fd3da14525ba1bf9acd7585c5de213cd62/AllLive.UWP/Views/LiveRoomPage.xaml.cs#L79-L82)：4个PlaybackSession事件。
- [当时取消的事件](https://github.com/cyocyo10/AllLive/blob/73eda5fd3da14525ba1bf9acd7585c5de213cd62/AllLive.UWP/Views/LiveRoomPage.xaml.cs#L663-L669)：4个PlaybackSession事件，以及另3个MediaPlayer事件。

**准确表述**

```text
修复页面事件未取消订阅导致的资源持有问题

- LiveRoomPage.OnNavigatingFrom 取消4个 PlaybackSession 事件和3个 MediaPlayer 事件
- 取消相关 Timer.Tick、窗口 Consolidated 和 KeyDown 事件
- FavoritePage 在 Unloaded 时取消 MessageCenter.UpdateFavoriteEvent
- SettingsPage 在 Unloaded 时取消 BiliAccount.OnAccountChanged
```

### 61 缓存页面生命周期与重复清理

- 原提交：[7e4bd221eaf2b5a307b815b8e59704eea227cb7a](https://github.com/cyocyo10/AllLive/commit/7e4bd221eaf2b5a307b815b8e59704eea227cb7a)
- 原主题：修复事件取消订阅的潜在问题
- 原作者：cyocyo10；作者时间：2026-01-17T11:09:38Z。

**需勘误的原文**

> - Unloaded 事件在页面缓存时不会触发

> - 避免重复取消订阅

**当时diff与源码证据**

NavigationCacheMode控制页面实例复用，不意味着离开可视树时不触发Unloaded。另外，抽出CleanupEventSubscriptions共用方法不等于增加防重复执行保护；该提交的方法没有清理标志或提前返回，两个退出回调均可调用它。

- [FavoritePage导航处理](https://github.com/cyocyo10/AllLive/blob/7e4bd221eaf2b5a307b815b8e59704eea227cb7a/AllLive.UWP/Views/FavoritePage.xaml.cs)：新增订阅标志，在Back导航时取消订阅并禁用缓存。
- [Consolidated及共用清理函数](https://github.com/cyocyo10/AllLive/blob/7e4bd221eaf2b5a307b815b8e59704eea227cb7a/AllLive.UWP/Views/LiveRoomPage.xaml.cs#L114-L170)：共用函数没有防重复执行标志。
- [另一清理入口](https://github.com/cyocyo10/AllLive/blob/7e4bd221eaf2b5a307b815b8e59704eea227cb7a/AllLive.UWP/Views/LiveRoomPage.xaml.cs#L706-L714)：OnNavigatingFrom同样调用CleanupEventSubscriptions。
- [微软Unloaded说明](https://learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.frameworkelement.unloaded)：Unloaded对应元素离开主对象树，而非实例销毁。

**准确表述**

```text
调整页面事件订阅与清理逻辑

- FavoritePage 改用 OnNavigatedTo/OnNavigatedFrom 和订阅标志管理事件，在 Back 导航时取消订阅并禁用缓存
- LiveRoomPage 提取共用的 CleanupEventSubscriptions()，在 OnNavigatingFrom 和窗口 Consolidated 中调用
- 为部分清理操作增加空值检查，并捕获取消窗口事件订阅时的异常
```

### 67 单行类型转换误写为三项修复

- 原提交：[0c42e536606693a975ce7a900239d57905ff8934](https://github.com/cyocyo10/AllLive/commit/0c42e536606693a975ce7a900239d57905ff8934)
- 原主题：fix: Huya stream fix, search pagination, favorites bugfix
- 原作者：cyocyo10；作者时间：2026-03-04T13:17:28Z。

**需勘误的原文**

> fix: Huya stream fix, search pagination, favorites bugfix

**当时diff与源码证据**

该提交的完整diff只有HYGetCdnTokenExReq.ReadFrom中的一行显式类型转换，没有搜索分页或收藏的改动。标题沿用了前一提交的较大修复范围。

- [本提交完整diff](https://github.com/cyocyo10/AllLive/commit/0c42e536606693a975ce7a900239d57905ff8934)：唯一变化为tId = (HuyaUserId)_is.Read(tId, 3, false)，1行增加、1行删除。
- [ReadFrom实现](https://github.com/cyocyo10/AllLive/blob/0c42e536606693a975ce7a900239d57905ff8934/AllLive.Core/Models/Tars/HYGetCdnTokenExReq.cs#L16-L24)：增加HuyaUserId显式转换。

**准确表述**

```text
fix: 为虎牙令牌请求中的用户对象补充显式类型转换

在 HYGetCdnTokenExReq.ReadFrom 中将 tId 的读取结果转换为 HuyaUserId。
```

### 70 Brotli降级时机与表单失败条件

- 原提交：[d672b0606d6313a67bc26c993dca6277b4c8b91a](https://github.com/cyocyo10/AllLive/commit/d672b0606d6313a67bc26c993dca6277b4c8b91a)
- 原主题：fix: 修复两批暗病（崩溃、资源泄漏、并发、递归安全）
- 原作者：cyocyo；作者时间：2026-03-27T18:24:44Z。

**需勘误的原文**

> 并实现 Brotli 解压，平台不支持时自动降级 protover=2

> - HttpUtil.PostString: Split('=') 改为只按首个等号切分，防值含等号时崩溃

**当时diff与源码证据**

Brotli不可用时只设置_forceLegacyProtover、提示重新进入房间并返回空消息体；当前连接不会自动重新握手，下次Ws_OnOpen才会选择protover=2。表单原实现对值中多个等号会截断值；真正触发splits[1]越界的是字段缺少等号。

- [握手选择](https://github.com/cyocyo10/AllLive/blob/d672b0606d6313a67bc26c993dca6277b4c8b91a/AllLive.Core/Danmaku/BiliBiliDanmaku.cs#L55-L74)：仅在Ws_OnOpen读取_forceLegacyProtover。
- [Brotli失败处理](https://github.com/cyocyo10/AllLive/blob/d672b0606d6313a67bc26c993dca6277b4c8b91a/AllLive.Core/Danmaku/BiliBiliDanmaku.cs#L342-L380)：设置兼容标志并提示重新进入，没有重连或重新握手。
- [表单切分实现](https://github.com/cyocyo10/AllLive/blob/d672b0606d6313a67bc26c993dca6277b4c8b91a/AllLive.Core/Helper/HttpUtil.cs#L110-L116)：限定为两段以保留值中等号；长度检查防止缺等号字段越界。

**准确表述**

```text
fix: 修复两批暗病（崩溃、资源泄漏、并发、递归安全）

第一批（CRITICAL/HIGH）：
- BiliBiliDanmaku: 事件调用改 ?.Invoke 防空引用；握手支持 protover=3
  并实现 Brotli 解压；平台不支持时设置兼容标志并提示重新进入房间，
  后续握手使用 protover=2
- HttpUtil.PostString: Split('=') 改为只按首个等号切分，保留值中的等号，
  并为缺少等号的字段提供空值以防越界
- Douyin.GetRequestHeaders: 返回 headers 副本，隔离调用方写入污染
- DatabaseHelper: 引入统一锁 + using 释放 SqliteCommand + 真异步包装
- 新增 System.IO.Compression.Brotli 4.7.0 包引用

第二批（MEDIUM）：
- SiteParser: 正则字符类修正、空短链守卫、递归深度上限 MaxRedirectDepth=5
- DouyuDanmaku: SttToJObject 加 tokens 长度守卫防越界，异常输出改为含消息
- BiliBili: GetWbiKeys/GetAssessId 用 SemaphoreSlim 保护懒初始化
```

### 72 JSON及数组保护范围夸大

- 原提交：[3fcb08f6bac1fc199f6439c80368a1ae8f0442f9](https://github.com/cyocyo10/AllLive/commit/3fcb08f6bac1fc199f6439c80368a1ae8f0442f9)
- 原主题：fix: 全面修复弹幕泄漏、API空安全、HTTP资源释放
- 原作者：cyocyo；作者时间：2026-03-28T16:07:31Z。

**需勘误的原文**

> 所有 JSON 深层访问添加 ?. 安全导航和 ?? 默认值

> 数组索引访问前检查 Count

**当时diff与源码证据**

改动增加了多处空值导航，但未覆盖所有JSON深层访问。同一提交新增的stream?[0]、format?[0]和codec?[0]也没有Count检查；空值导航不能保护非null空数组的索引。

- [新版画质解析](https://github.com/cyocyo10/AllLive/blob/3fcb08f6bac1fc199f6439c80368a1ae8f0442f9/AllLive.Core/BiliBili.cs#L250-L279)：acceptQn访问stream?[0]、format?[0]、codec?[0]，没有Count判断。
- [未覆盖的深层访问](https://github.com/cyocyo10/AllLive/blob/3fcb08f6bac1fc199f6439c80368a1ae8f0442f9/AllLive.Core/BiliBili.cs#L384-L464)：GetLiveStatus、GetSuperChatMessages、GetWbiKeys仍有obj["data"][...]直接访问。
- [本提交完整diff](https://github.com/cyocyo10/AllLive/commit/3fcb08f6bac1fc199f6439c80368a1ae8f0442f9)：确认实际增加的事件解绑、Timer释放和HTTP using范围。

**准确表述**

```text
fix: 改进弹幕清理、API空值检查和HTTP资源释放

弹幕生命周期（BiliBili/Douyu/Huya）:
- Stop() 中取消 WebSocket 事件订阅，停止并释放 Timer 后置空
- BiliBili/Huya 消息解析异常添加 Trace 日志

API 空安全（Douyu/BiliBili/Huya）:
- 为分类、推荐、搜索和部分取流 JSON 访问添加 ?.、默认值及集合空值检查
- BiliBili 新版画质解析增加空值导航和画质字典键检查

HTTP 资源释放:
- HttpUtil: GetString/GetUtf8String/PostString/PostJsonString 的 response 包裹 using
- HttpUtil.Head: request 包裹 using
- SiteParser.GetLocation: Head 响应包裹 using
- BiliLoginDialog.PollQRStatus: Get 响应包裹 using
```

### 76 异步范围 删除循环及日志格式

- 原提交：[10c53d63e42db7d48d6a6aab51ad09c4d394cbaa](https://github.com/cyocyo10/AllLive/commit/10c53d63e42db7d48d6a6aab51ad09c4d394cbaa)
- 原主题：perf: 全面性能修复（39项）+ 日志改善
- 原作者：cyocyo；作者时间：2026-03-28T17:45:09Z。

**需勘误的原文**

> LiveRoomVM: SuperChat反向循环修复越界 + Timer重复启动防护

> DouyinDanmaku: SemaphoreSlim替代lock+Task.Run防死锁 + 全面异步化

> 全局: 统一[ClassName.MethodName]日志格式

**当时diff与源码证据**

SuperChat原来的正向循环每轮以i < Count检查边界，删除后i++会跳过相邻项；反向遍历修复的是漏处理，而不是据此证明修复了越界。DouyinDanmaku仅将互斥等待改成SemaphoreSlim.WaitAsync，仍同步调用WebSocket.Connect/Send。日志也未全局统一为[ClassName.MethodName]。

- [本提交完整diff](https://github.com/cyocyo10/AllLive/commit/10c53d63e42db7d48d6a6aab51ad09c4d394cbaa)：LiveRoomVM的SuperChat循环由正向改为反向。
- [当时DouyinDanmaku](https://github.com/cyocyo10/AllLive/blob/10c53d63e42db7d48d6a6aab51ad09c4d394cbaa/AllLive.Core/Danmaku/DouyinDanmaku.cs)：异步等待信号量后仍调用ws.Send及ws.Connect。
- [当时WebView运行器](https://github.com/cyocyo10/AllLive/blob/10c53d63e42db7d48d6a6aab51ad09c4d394cbaa/AllLive.UWP/Helper/WebViewDouyinScriptRunner.cs)：仍有[WebViewRunner]与不含方法名的[WebViewDouyinScriptRunner]前缀。
- [当时SyncVM](https://github.com/cyocyo10/AllLive/blob/10c53d63e42db7d48d6a6aab51ad09c4d394cbaa/AllLive.UWP/ViewModels/SyncVM.cs)：新增日志仍含[SyncVM]前缀，不能称全局统一。

“39项”没有独立编号基准，本条不按主观计数将该数字判错；以下准确表述不重复这一未经独立核定的计数。

**准确表述**

```text
perf: 优化网络、弹幕与界面处理并改善日志

Core层修复（9文件）:
- TupHttpHelper: 静态共享HttpClient防socket耗尽 + HttpResponseMessage using包裹
- HttpUtil: StringBuilder替代字符串拼接构造URL + 空参数守卫
- Douyu: Task.WhenAll并行CDN请求替代串行await
- BiliBiliDanmaku: 预编译Regex + 4096缓冲区 + Heartbeat异步化
- DouyuDanmaku/HuyaDanmaku: Heartbeat异步化 + 事件处理器异常保护
- DouyinDanmaku: SemaphoreSlim替代lock+Task.Run，异步等待连接互斥；连接和发送仍调用同步WebSocket API
- Douyin/Huya: Random单例化

UWP层修复（7文件）:
- LiveRoomVM: SuperChat反向遍历，避免删除过期项时跳过相邻条目 + Timer重复启动防护
- SyncVM: SignalR await DisposeAsync + 事件订阅IDisposable管理
- FavoriteVM/HistoryVM: ObservableCollection一次性赋值 + Task.WhenAll加载状态
- DatabaseHelper: 添加room_id/site_name复合索引
- WebView脚本运行器: Popup资源清理 + InvokeScript超时保护（10-30s）

多处日志补充[ClassName.MethodName]前缀和异常信息
```

### 78 斗鱼请求头及播放地址校验范围

- 原提交：[47ce2b0c6e0e43f1ec73ad003bfae9af8976252d](https://github.com/cyocyo10/AllLive/commit/47ce2b0c6e0e43f1ec73ad003bfae9af8976252d)
- 原主题：fix: 斗鱼取流签名重构(getEncryption+MD5) + 斗鱼账号Cookie登录
- 原作者：cyocyo；作者时间：2026-09-25T11:56:35Z。

**需勘误的原文**

> 进程级随机 did(32位hex),签名/播放/录制请求头 Cookie 与 did 强一致

> 描述符缓存 5 分钟 + 30 秒安全窗 + 失败强制刷新重签(治 wsAuth 短签名断流)

> URL 解析兼容 rtmp_live 完整地址/相对路径双形态,拦截裸 CDN 目录

**当时diff与源码证据**

统一Cookie/did仅在签名和取流API请求中实现，没有接入实际播放器的斗鱼请求头。取流失败重试也只发生在API请求阶段，不能单凭此改动宣称解决所有短签名断流。IsPlayableUrl只验证绝对URL、host和协议，会接受裸CDN根目录或目录路径；只有最后的flv_url分支检查媒体扩展名。

- [当时签名辅助类](https://github.com/cyocyo10/AllLive/blob/47ce2b0c6e0e43f1ec73ad003bfae9af8976252d/AllLive.Core/Helper/DouyuSignHelper.cs)：RequestHeaders用于描述符请求，CookieHeader统一其中的did。
- [当时播放器配置](https://github.com/cyocyo10/AllLive/blob/47ce2b0c6e0e43f1ec73ad003bfae9af8976252d/AllLive.UWP/Views/LiveRoomPage.xaml.cs#L587-L622)：只有B站与虎牙专用headers分支，没有斗鱼Cookie/did分支。
- [当时斗鱼取流及地址解析](https://github.com/cyocyo10/AllLive/blob/47ce2b0c6e0e43f1ec73ad003bfae9af8976252d/AllLive.Core/Douyu.cs)：RequestPlayData只重试API；IsPlayableUrl只检查URL、host及http/https/rtmp协议。

**准确表述**

```text
fix: 斗鱼取流签名重构(getEncryption+MD5) + 斗鱼账号Cookie登录

- 移除取流主流程中的 homeH5Enc + QuickJS/WebView 两段式 JS 签名，
  改用 getEncryption 服务端描述符 + 纯托管 MD5，取流接口切换到 getH5PlayV1
- 生成进程级随机 did（32位hex），统一签名表单与签名、取流API请求Cookie中的 did
- 描述符最多缓存5分钟，使用30秒过期安全窗；取流API失败后强制刷新描述符并重试一次
- URL解析兼容 rtmp_live 完整地址和相对路径，并尝试多个备用字段
- 新增设置页斗鱼WebView2登录、Cookie保存和注销，将账号Cookie用于签名与取流API请求
```

### 79 历史页await续体的因果解释

- 原提交：[4f505ac99b5ff5c4f0b4c7a28fbe339c7a7676d1](https://github.com/cyocyo10/AllLive/commit/4f505ac99b5ff5c4f0b4c7a28fbe339c7a7676d1)
- 原主题：fix: 历史页直播状态 UI 线程赋值与排序
- 原作者：cyocyo；作者时间：2026-09-25T11:56:46Z。

**需勘误的原文**

> - LoadLiveStatusAsync 的 continuation 在线程池线程上执行(GetLiveStatus 内部
>   ConfigureAwait(false)),直接在后台设置 LiveStatus 会向 XAML 绑定发送
>   非 UI 线程的 PropertyChanged,导致直播中标记不显示/不排序,甚至
>   RPC_E_WRONG_THREAD 异常;改为后台仅取状态,统一回 UI 线程赋值+排序

**当时diff与源码证据**

调用方的普通await会按照自身捕获的同步上下文恢复。GetLiveStatus内部使用ConfigureAwait(false)，不会把这一选择传递给调用方；它本身也不保证切换到线程池。因此原说明对“为什么一定在后台赋值”的因果解释不成立。显式Dispatcher批量赋值和排序键修改是真实改动；本条并不否认其他无UI上下文路径可能出现跨线程异常。

- [该提交HistoryVM差分](https://github.com/cyocyo10/AllLive/commit/4f505ac99b5ff5c4f0b4c7a28fbe339c7a7676d1)：原await site.LiveSite.GetLiveStatus(...)未设置ConfigureAwait(false)；本次改为返回条目与状态，再调度赋值。
- [当时HistoryPage入口](https://github.com/cyocyo10/AllLive/blob/4f505ac99b5ff5c4f0b4c7a28fbe339c7a7676d1/AllLive.UWP/Views/HistoryPage.xaml.cs)：OnNavigatedTo调用historyVM.LoadData。
- [微软ConfigureAwait说明](https://devblogs.microsoft.com/dotnet/configureawait-faq/)：普通await捕获自己的上下文；ConfigureAwait(false)只影响对应await，也不保证切到线程池。

**准确表述**

```text
fix: 历史页直播状态 UI 线程赋值与排序

- LoadLiveStatusAsync 改为返回条目与状态；等待全部查询后，显式通过 Dispatcher 在 UI 线程批量赋值并排序
- 失败路径也通过 Dispatcher 清除加载状态
- 排序从按 LiveStatus 枚举值降序改为直播中 > 回放中 > 未直播，再按观看时间倒序
```

### 83 关注页await续体的因果解释

- 原提交：[c6951df959b7ead0d93edc087188788b49f1e273](https://github.com/cyocyo10/AllLive/commit/c6951df959b7ead0d93edc087188788b49f1e273)
- 原主题：fix: 关注页直播状态 UI 线程赋值与排序(同历史页问题)
- 原作者：cyocyo；作者时间：2026-09-25T13:09:12Z。

**需勘误的原文**

> - LoadLiveStatusAsync 的 continuation 在线程池线程上执行,直接在后台
>   设置 LiveStatus 会触发非 UI 线程的 PropertyChanged,导致直播中徽章
>   不显示或 COM 异常;上次 dfd33d7 只修了 Refresh 调度,此处漏网;
>   改为后台仅取状态,统一回 UI 线程赋值+排序

**当时diff与源码证据**

LoadLiveStatusAsync中的await没有设置ConfigureAwait(false)，而页面正常导航及更新回调是在UI上下文中启动加载。不能将续体一概描述为在线程池执行。准确的改动是查询方法返回状态，再经Dispatcher批量赋值及排序，而非从被调用方法的内部await推定调用方线程。

- [该提交FavoriteVM差分](https://github.com/cyocyo10/AllLive/commit/c6951df959b7ead0d93edc087188788b49f1e273)：原查询await没有ConfigureAwait(false)；本次添加显式Dispatcher批量赋值与排序。
- [当时FavoritePage入口](https://github.com/cyocyo10/AllLive/blob/c6951df959b7ead0d93edc087188788b49f1e273/AllLive.UWP/Views/FavoritePage.xaml.cs)：OnNavigatedTo在UI回调启动加载，UpdateFavoriteEvent也已调度Refresh。
- [微软ConfigureAwait说明](https://devblogs.microsoft.com/dotnet/configureawait-faq/)：调用方续体由调用方await的上下文决定。

**准确表述**

```text
fix: 关注页直播状态 UI 线程赋值与排序

- LoadLiveStatusAsync 改为返回条目与状态；等待全部查询后，显式通过 Dispatcher 在 UI 线程批量赋值并排序
- 失败路径也通过 Dispatcher 清除加载状态
- 排序从按 LiveStatus 枚举值降序改为直播中 > 回放中 > 未直播
```

## 附录一 数字标题的信息不足

以下3项的完整message只有数字，没有足够信息描述改动。它们不计入15项事实勘误，原提交及作者信息保留。

### 1 8eecb9645

- 原提交：[8eecb96459c818fb0158c75bd38f373011b5173e](https://github.com/cyocyo10/AllLive/commit/8eecb96459c818fb0158c75bd38f373011b5173e)；原作者：zhangchao；原message：`11`。

根据该提交diff可补充理解为：

```text
feat: 增加抖音弹幕本地签名和断线重连

- 嵌入 webmssdk.js，通过 QuickJS 计算签名，并保留远程签名回退
- 增加弹幕连接重试、备用端点切换与资源清理
- 添加 AllLive.Core 的 .NET CI 工作流
```

### 2 60d441835

- 原提交：[60d441835c9e5746318c12d66f707fa8265699da](https://github.com/cyocyo10/AllLive/commit/60d441835c9e5746318c12d66f707fa8265699da)；原作者：zhangchao；原message：`1`。

根据该提交diff可补充理解为：

```text
fix: 明确抖音弹幕定时器类型，消除 Timer 命名歧义
```

### 3 441301cdd

- 原提交：[441301cdd2247326184d64ee71b3ac2df93aedce](https://github.com/cyocyo10/AllLive/commit/441301cdd2247326184d64ee71b3ac2df93aedce)；原作者：zhangchao；原message：`1`。

根据该提交diff可补充理解为：

```text
ci: 上传 AllLive.Core Release 构建产物
```

## 附录二 全部83项核查登记

“未见确证错误”只表示没有找到原说明与该提交当时改动之间的确定矛盾，不代表功能测试全部通过。

| 序号 | 原SHA | 结论 | 原主题 |
| ---: | --- | --- | --- |
| 1 | [8eecb9645](https://github.com/cyocyo10/AllLive/commit/8eecb96459c818fb0158c75bd38f373011b5173e) | 数字标题 信息不足 | 11 |
| 2 | [60d441835](https://github.com/cyocyo10/AllLive/commit/60d441835c9e5746318c12d66f707fa8265699da) | 数字标题 信息不足 | 1 |
| 3 | [441301cdd](https://github.com/cyocyo10/AllLive/commit/441301cdd2247326184d64ee71b3ac2df93aedce) | 数字标题 信息不足 | 1 |
| 4 | [b728f75b5](https://github.com/cyocyo10/AllLive/commit/b728f75b5c04c2f4d940f12023a775ac80814b8a) | 见正文勘误 | feat: run douyu and douyin flows fully offline |
| 5 | [e61ec3556](https://github.com/cyocyo10/AllLive/commit/e61ec3556ff5a03deb3f816554232d6ed32db90a) | 未见确证错误 | Add GitHub Actions workflow for UWP build |
| 6 | [fa6aff377](https://github.com/cyocyo10/AllLive/commit/fa6aff3774c80b1ee943e7fbba468ed6e656e0fd) | 未见确证错误 | Fix UWP build workflow - use windows-2022 and Windows SDK |
| 7 | [ec521f64c](https://github.com/cyocyo10/AllLive/commit/ec521f64c915a4547fc2273caa75796e60b5a115) | 未见确证错误 | Add certificate signing to UWP build workflow |
| 8 | [a180388d7](https://github.com/cyocyo10/AllLive/commit/a180388d77acb0a3f0e2693a27b7295d9118928d) | 未见确证错误 | Fix Huya: use new mp.huya.com API for room info |
| 9 | [f5afbb76c](https://github.com/cyocyo10/AllLive/commit/f5afbb76cdec74cfa7c1f04162b4c4315fc6a056) | 未见确证错误 | Add userAgent parameter to TupHttpHelper constructor |
| 10 | [2bd22e315](https://github.com/cyocyo10/AllLive/commit/2bd22e3153033f36bbdf68e5d2c3175dc32e7226) | 未见确证错误 | Remove duplicate dotnet workflow |
| 11 | [46610a824](https://github.com/cyocyo10/AllLive/commit/46610a8244091ba16b1e6709d5803eef6755805b) | 未见确证错误 | Add fallback for GetRealUrl when tup request fails |
| 12 | [90e8eca24](https://github.com/cyocyo10/AllLive/commit/90e8eca24af97e54e23aed3a1a644848bcc6019b) | 未见确证错误 | Fix Huya: use correct User-Agent for tup requests |
| 13 | [f4236ea16](https://github.com/cyocyo10/AllLive/commit/f4236ea162f67c6794efc807358c1a229b0392ac) | 未见确证错误 | fix: add null checks to prevent crash in Huya |
| 14 | [0ab936814](https://github.com/cyocyo10/AllLive/commit/0ab9368147aea593bd2279f68aaad81db14e9a39) | 未见确证错误 | refactor: rewrite Huya based on pure_live implementation |
| 15 | [4a81d0c86](https://github.com/cyocyo10/AllLive/commit/4a81d0c86bacc556dee38df69fa9066112952df3) | 未见确证错误 | fix: add fallback for empty tup response in GetPlayUrl |
| 16 | [dc65a228a](https://github.com/cyocyo10/AllLive/commit/dc65a228af6c0f597e817bb767c977c3e6918e5a) | 未见确证错误 | fix: add requestId and Content-Length to tup request |
| 17 | [b4f6f4179](https://github.com/cyocyo10/AllLive/commit/b4f6f41791037df98f0fcfac8a2e72571fb77276) | 未见确证错误 | debug: add logging to Huya and TupHttpHelper |
| 18 | [4da769928](https://github.com/cyocyo10/AllLive/commit/4da76992824fafb6023bec0c1c69d20e629a4922) | 未见确证错误 | test: use original FlvAntiCode without tup request |
| 19 | [b18146a1a](https://github.com/cyocyo10/AllLive/commit/b18146a1a440fcc75f8427c6b8c519aabdc75f76) | 未见确证错误 | fix: rewrite Huya based on SlotSun/dart_simple_live ef4bfe1 |
| 20 | [3b2e433d4](https://github.com/cyocyo10/AllLive/commit/3b2e433d4f9737bff747115dec08dd77e035cdae) | 未见确证错误 | fix: lazy init tupClient to prevent crash |
| 21 | [7360055f2](https://github.com/cyocyo10/AllLive/commit/7360055f2acc2d8e65f135f759d4e4fa69fade8e) | 未见确证错误 | fix: use normal UA for HTTP requests, HYSDK_UA only for tup |
| 22 | [2f71dd465](https://github.com/cyocyo10/AllLive/commit/2f71dd465ce76bc86bed94486429cfa0916f6893) | 未见确证错误 | fix: improve Huya room detail parsing and danmaku error handling |
| 23 | [082e7be69](https://github.com/cyocyo10/AllLive/commit/082e7be69f71e62343fe4f1199e89a02d76bd619) | 未见确证错误 | fix: use PC User-Agent for Huya web page |
| 24 | [56619ef83](https://github.com/cyocyo10/AllLive/commit/56619ef83e8f7d13b9eb80073ca112e489b2681e) | 未见确证错误 | fix: use mp.huya.com API instead of PC web page for stream info |
| 25 | [c09e8bd44](https://github.com/cyocyo10/AllLive/commit/c09e8bd44195dbd06edf1430f8be09d1204a1fbb) | 未见确证错误 | fix: improve GetPlayUrl with better fallback handling |
| 26 | [5d1465275](https://github.com/cyocyo10/AllLive/commit/5d14652757e3004349e48ef2448a2499b3f9070a) | 未见确证错误 | feat: 多项功能改进和bug修复 |
| 27 | [e2fb9de11](https://github.com/cyocyo10/AllLive/commit/e2fb9de1118f6f8eaca0b3b5a021511597e59588) | 见正文勘误 | fix: 修复多个潜在bug |
| 28 | [b3676e48c](https://github.com/cyocyo10/AllLive/commit/b3676e48c9eb06d82c7487c9f14952981d956c65) | 未见确证错误 | fix: 修复抖音弹幕二次进入不显示的问题 |
| 29 | [684d68a89](https://github.com/cyocyo10/AllLive/commit/684d68a89ce534ab503287aeb7dfd7539bb57a46) | 未见确证错误 | chore: 添加dart_simple_live到gitignore |
| 30 | [4760545e4](https://github.com/cyocyo10/AllLive/commit/4760545e4e8562e5eb9f4b3e76bccd0cf192b5a4) | 见正文勘误 | fix: 修复抖音弹幕连接问题 |
| 31 | [6c92786a1](https://github.com/cyocyo10/AllLive/commit/6c92786a14defc9f7d8a64af283baa260949fd8a) | 未见确证错误 | debug: 添加抖音a_bogus和WebView调试日志 |
| 32 | [e27e33e90](https://github.com/cyocyo10/AllLive/commit/e27e33e90726e11c7642203a58ce02d76eac13c3) | 未见确证错误 | 添加抖音搜索和弹幕连接的详细调试日志 |
| 33 | [af34a3065](https://github.com/cyocyo10/AllLive/commit/af34a3065031ec8f90522c4c091859c79dbfeaf3) | 未见确证错误 | 移除参考项目 dart_simple_live |
| 34 | [f285760d5](https://github.com/cyocyo10/AllLive/commit/f285760d5fe5f4d53c8b71f957eb3c12e155815b) | 未见确证错误 | 优化CI: 自动递增版本号支持覆盖安装，精简artifact只保留安装包 |
| 35 | [b0d4e65cb](https://github.com/cyocyo10/AllLive/commit/b0d4e65cb78347e6eb494b9a02c8d56733496110) | 未见确证错误 | 统一日志输出到文件: Debug.WriteLine改为Trace.WriteLine |
| 36 | [709e4702a](https://github.com/cyocyo10/AllLive/commit/709e4702a8538acc779ce45ab1861fae4b6f532f) | 未见确证错误 | 添加抖音搜索验证功能: 检测到风控时弹出WebView让用户完成验证 |
| 37 | [2d0633399](https://github.com/cyocyo10/AllLive/commit/2d0633399a533407f6e0420547757983b60dd2be) | 未见确证错误 | 修复: 添加 DouyinVerifyDialog 到 csproj |
| 38 | [12854fb25](https://github.com/cyocyo10/AllLive/commit/12854fb258f1b26d4316737728dbfe8661cdbc25) | 未见确证错误 | 调大验证对话框尺寸 |
| 39 | [047dccda6](https://github.com/cyocyo10/AllLive/commit/047dccda68ba129ad49a32e1ebdd67b8dddc6e7b) | 未见确证错误 | fix: 加大验证对话框尺寸到1400x900 |
| 40 | [e5d976c31](https://github.com/cyocyo10/AllLive/commit/e5d976c3133a5b864bd76f51791b148d94f13756) | 未见确证错误 | feat: 验证对话框改用WebView2 - 使用Microsoft.UI.Xaml.Controls.WebView2 - 基于Chromium内核，支持现代网页 |
| 41 | [bd1a73a4a](https://github.com/cyocyo10/AllLive/commit/bd1a73a4a8582f0d91bc240a375b8211fb3d41b2) | 未见确证错误 | feat: 完善解析页面 - 支持手动输入直播间链接或房间号 - 支持自动识别平台或手动选择 - 支持抖音/B站/虎牙/斗鱼 |
| 42 | [b5cabe138](https://github.com/cyocyo10/AllLive/commit/b5cabe13805fce4793123779b32966163f8b38b3) | 未见确证错误 | fix: 导出收藏JSON格式化缩进，便于阅读 |
| 43 | [b6ff2766c](https://github.com/cyocyo10/AllLive/commit/b6ff2766cb3bfcbb8f1c737d1df6591cbadd3c15) | 见正文勘误 | feat: 历史记录添加导出导入功能 - 支持导出为格式化JSON - 支持从JSON导入 - 收藏导出也改为格式化JSON |
| 44 | [8a7989d57](https://github.com/cyocyo10/AllLive/commit/8a7989d57601a3f0706c6ed487cae6dff0afa41c) | 未见确证错误 | fix: 移除重复的HistoryJsonItem类定义，使用SyncVM中的定义 |
| 45 | [b07fe9a99](https://github.com/cyocyo10/AllLive/commit/b07fe9a99a109f027663e546cacd6d936ed9db16) | 未见确证错误 | fix: 修复ParsePage编译错误 - 使用完整命名空间Helper.LiveSite - 使用switch语句替代switch表达式(C#7.3兼容) |
| 46 | [1ea4257da](https://github.com/cyocyo10/AllLive/commit/1ea4257dab98c36650f7d6e2e1f6157fd4d9cdeb) | 未见确证错误 | feat: 抖音搜索改为房间号/链接直接进入，历史页面添加顶部导出按钮 |
| 47 | [69848db97](https://github.com/cyocyo10/AllLive/commit/69848db97e58db70ccb3b5112031a6851cdd0bc1) | 未见确证错误 | fix: 移除抖音验证相关代码 |
| 48 | [9ce2bb2c2](https://github.com/cyocyo10/AllLive/commit/9ce2bb2c23ac752b501028b3c07ccdd72e1ea748) | 未见确证错误 | fix: FavoriteVM在UI线程上更新集合，修复COM对象转换错误 |
| 49 | [d7c886977](https://github.com/cyocyo10/AllLive/commit/d7c8869777cb085b8535c1bf6a0c7c73c3a55ad9) | 见正文勘误 | fix: 修复日志乱码问题，修复FavoriteVM线程安全问题 |
| 50 | [8f7d6387e](https://github.com/cyocyo10/AllLive/commit/8f7d6387edee0569ffd60c45c950bc328ac80b4d) | 未见确证错误 | fix: 修复所有乱码注释和日志消息 |
| 51 | [241e8c27f](https://github.com/cyocyo10/AllLive/commit/241e8c27f239376be275df611b74bf079b559a12) | 未见确证错误 | fix: 修复数据库操作空值检查，防止其他平台进入直播间报错 |
| 52 | [9cfcccaff](https://github.com/cyocyo10/AllLive/commit/9cfcccaff0b572c2d7912b50f4fe28a9a51084c0) | 未见确证错误 | 修复历史记录页面点击直播间时的空引用异常 |
| 53 | [52f933b79](https://github.com/cyocyo10/AllLive/commit/52f933b79c30977dd7f2f9bd61953d4c72868377) | 未见确证错误 | 修复多个页面的空引用异常问题 |
| 54 | [5c35e3eca](https://github.com/cyocyo10/AllLive/commit/5c35e3eca03495f881551ab5c9d2283b5a8c6729) | 未见确证错误 | 修复 Timer 资源泄漏问题 |
| 55 | [aef4a08ae](https://github.com/cyocyo10/AllLive/commit/aef4a08ae4de4dc9e45cf15521c7c8d79c7c93fa) | 未见确证错误 | 添加历史记录和收藏页面的调试日志 |
| 56 | [bffb2fe31](https://github.com/cyocyo10/AllLive/commit/bffb2fe31743f389bcf93ffe72ac3ac5c8c7edc8) | 未见确证错误 | 改进调试信息显示 - 在Toast中显示详细的站点匹配信息 |
| 57 | [da98f24c0](https://github.com/cyocyo10/AllLive/commit/da98f24c037c58f75bd05fe9a050f76d00ca8d08) | 见正文勘误 | 修复数据库字符编码问题 - 添加 UTF-8 支持 |
| 58 | [b8e644881](https://github.com/cyocyo10/AllLive/commit/b8e644881fb4a0a78bbc3ad9f59b6b7c241e1fa0) | 未见确证错误 | 添加数据库乱码数据自动清理功能 |
| 59 | [0c8a1aad9](https://github.com/cyocyo10/AllLive/commit/0c8a1aad90751efba64e3bbd58c86b0e718e9d09) | 未见确证错误 | 添加乱码数据修复功能和 SQL 脚本 |
| 60 | [73eda5fd3](https://github.com/cyocyo10/AllLive/commit/73eda5fd3da14525ba1bf9acd7585c5de213cd62) | 见正文勘误 | 修复页面事件未取消订阅导致的内存泄漏 |
| 61 | [7e4bd221e](https://github.com/cyocyo10/AllLive/commit/7e4bd221eaf2b5a307b815b8e59704eea227cb7a) | 见正文勘误 | 修复事件取消订阅的潜在问题 |
| 62 | [9280b24b0](https://github.com/cyocyo10/AllLive/commit/9280b24b0f8c89ba2f67ac140793af2e6553d075) | 未见确证错误 | 回滚不当修改，确保不影响原有功能 |
| 63 | [dfc8926b3](https://github.com/cyocyo10/AllLive/commit/dfc8926b378dc49f65daa7c974dc1d609678d713) | 未见确证错误 | 添加代码修改 Review 检查清单 |
| 64 | [988cc2e8c](https://github.com/cyocyo10/AllLive/commit/988cc2e8c1df903562e544abbfed78982ef62ca8) | 未见确证错误 | 修复编码问题和数据库操作bug |
| 65 | [346a01da5](https://github.com/cyocyo10/AllLive/commit/346a01da5eb1258aca9f7bbfddf6ea486e6d880e) | 未见确证错误 | 修复抖音直播API问题：添加默认Cookie支持 |
| 66 | [f55cb4434](https://github.com/cyocyo10/AllLive/commit/f55cb44346f3b9f0d182c1778efb0d15077dc714) | 未见确证错误 | 修复虎牙直播断流、关注报错、搜索分页问题 |
| 67 | [0c42e5366](https://github.com/cyocyo10/AllLive/commit/0c42e536606693a975ce7a900239d57905ff8934) | 见正文勘误 | fix: Huya stream fix, search pagination, favorites bugfix |
| 68 | [14c088b54](https://github.com/cyocyo10/AllLive/commit/14c088b5429001d18069d7fa9284c947cf081917) | 未见确证错误 | fix: 修复抖音直播无法打开的问题(UA被拦截/Cookie获取失败/API参数错误) |
| 69 | [b44862f31](https://github.com/cyocyo10/AllLive/commit/b44862f3114f55c570f6c9174960be904c02ca41) | 未见确证错误 | 添加抖音登录功能，修复WebView白屏问题 |
| 70 | [d672b0606](https://github.com/cyocyo10/AllLive/commit/d672b0606d6313a67bc26c993dca6277b4c8b91a) | 见正文勘误 | fix: 修复两批暗病（崩溃、资源泄漏、并发、递归安全） |
| 71 | [7a3e139b2](https://github.com/cyocyo10/AllLive/commit/7a3e139b218dcc712d676d3f211af155e0aca4a5) | 未见确证错误 | feat(douyin): 搜索 Cookie 隔离 + API 空安全加固 |
| 72 | [3fcb08f6b](https://github.com/cyocyo10/AllLive/commit/3fcb08f6bac1fc199f6439c80368a1ae8f0442f9) | 见正文勘误 | fix: 全面修复弹幕泄漏、API空安全、HTTP资源释放 |
| 73 | [ef0468e8b](https://github.com/cyocyo10/AllLive/commit/ef0468e8b4716533055b42de1460310ae7d4b33c) | 未见确证错误 | fix: 替换 Brotli 包为 BrotliSharpLib 修复构建 |
| 74 | [dfd33d736](https://github.com/cyocyo10/AllLive/commit/dfd33d7369fcee66ef6549594c747ba11b4fab9c) | 未见确证错误 | fix: 修复抖音登录白屏和收藏页 COM 跨线程异常 |
| 75 | [e9e67f813](https://github.com/cyocyo10/AllLive/commit/e9e67f813f8c1246cc6dba2495c1084591a8e191) | 未见确证错误 | feat: 历史记录直播状态 + 抖音WebView2登录 + 版本号升级 |
| 76 | [10c53d63e](https://github.com/cyocyo10/AllLive/commit/10c53d63e42db7d48d6a6aab51ad09c4d394cbaa) | 见正文勘误 | perf: 全面性能修复（39项）+ 日志改善 |
| 77 | [6e5f12d85](https://github.com/cyocyo10/AllLive/commit/6e5f12d854a490eef3dae490fa3e3c81222233cd) | 未见确证错误 | fix: 对齐 pure_live 修复虎牙断流与播放器自动重连 |
| 78 | [47ce2b0c6](https://github.com/cyocyo10/AllLive/commit/47ce2b0c6e0e43f1ec73ad003bfae9af8976252d) | 见正文勘误 | fix: 斗鱼取流签名重构(getEncryption+MD5) + 斗鱼账号Cookie登录 |
| 79 | [4f505ac99](https://github.com/cyocyo10/AllLive/commit/4f505ac99b5ff5c4f0b4c7a28fbe339c7a7676d1) | 见正文勘误 | fix: 历史页直播状态 UI 线程赋值与排序 |
| 80 | [43914ceca](https://github.com/cyocyo10/AllLive/commit/43914cecac239123c035fce6f58857bfbfc14b28) | 未见确证错误 | chore: 版本号升级 2.4.0.0 -> 2.5.0.0 |
| 81 | [f1ec7a770](https://github.com/cyocyo10/AllLive/commit/f1ec7a770af5585f032d02f8eca373b13d7caafd) | 未见确证错误 | fix: 斗鱼登录框改为可拖动 Popup,不再居中遮挡 |
| 82 | [8030e1359](https://github.com/cyocyo10/AllLive/commit/8030e1359148b8deced54b6fd85bc3d91f120e29) | 未见确证错误 | fix: 斗鱼登录框直接打开独立登录页 |
| 83 | [c6951df95](https://github.com/cyocyo10/AllLive/commit/c6951df959b7ead0d93edc087188788b49f1e273) | 见正文勘误 | fix: 关注页直播状态 UI 线程赋值与排序(同历史页问题) |

本文不变更原作者的署名，不重写上游或fork历史，也不将上述静态证据扩展为对当前分支修复状态的结论。
