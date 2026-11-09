# 开发与构建说明

[返回项目首页](../README.md)

本文按当前仓库配置整理。功能修复分支可能包含不同的代码和测试，构建前先确认分支与提交号。

## 开发环境

- Windows 10 1903 或以上用于运行 UWP 客户端。
- Visual Studio 2022 与 UWP 构建工具、MSBuild、NuGet。
- Windows SDK `10.0.19041.0`，与 [UWP 项目](../AllLive.UWP/AllLive.UWP.csproj)的 `TargetPlatformVersion` 一致。最低平台版本为 `10.0.18362.0`。
- [.NET SDK 配置](../global.json)请求 `9.0.306`，允许 `latestFeature` 滚动。核心库目标为 `netstandard2.0`，控制台目标为 `net8.0`，两者不要与 SDK 版本混淆。
- 斗鱼、抖音的 WebView2 登录需要可用的 Edge WebView2 Runtime。

完整 UWP 构建依赖 Windows 工具链，不能用 Linux 上的 `dotnet build` 代替。项目保留多架构配置；当前 CI 只验证和打包 x64。

## 从哪里开始

1. 获取仓库并切到要检查的分支，记录 `git rev-parse HEAD`。
2. 在 Visual Studio 中打开 [AllLive.sln](../AllLive.sln)。
3. 以 `AllLive.UWP` 为启动项目，核对配置和目标架构。需要本地部署时，按 Windows 与 Visual Studio 的要求配置开发环境和签名。
4. 在修改前确认现有登录、播放、收藏和历史行为，避免把不同分支的结果混在一起。

三个项目的用途：

- [AllLive.Core](../AllLive.Core/AllLive.Core.csproj)：各平台接口、弹幕和签名处理。
- [AllLive.ConsoleApp](../AllLive.Console/AllLive.ConsoleApp.csproj)：调用核心库的控制台示例。它是手动检查入口，不是自动回归测试套件。
- [AllLive.UWP](../AllLive.UWP/AllLive.UWP.csproj)：页面、播放器、账号、设置与本地数据。

## UWP 构建

以仓库中的 [build-uwp.yml](../.github/workflows/build-uwp.yml) 为可追溯的构建入口。当前流程使用 `windows-2022`，按以下顺序运行：

1. 检出代码，准备 MSBuild 和 NuGet。
2. 从仓库已有的 `SIGNING_CERTIFICATE` 配置还原签名文件。
3. 用工作流运行号替换 `Package.appxmanifest` 版本号的最后一段。
4. 还原解决方案依赖，构建核心库，再生成 x64 UWP 侧载包。
5. 上传 `AllLive-UWP-x64` 产物。

在已准备好工具链和有效签名证书的 Windows Developer PowerShell 中，可按同一顺序执行构建部分：

```powershell
nuget restore AllLive.sln
msbuild AllLive.Core/AllLive.Core.csproj /p:Configuration=Release /p:Platform="Any CPU"
msbuild AllLive.UWP/AllLive.UWP.csproj /p:Configuration=Release /p:Platform=x64 /p:AppxBundlePlatforms="x64" /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly /p:AppxPackageSigningEnabled=true /p:PackageCertificateKeyFile="AllLive.UWP_TemporaryKey.pfx"
```

上述命令不负责生成证书，也不会像 CI 一样自动更新版本号。证书文件名是现有项目配置使用的名称，不表示仓库包含私钥。

### 安装与签名

- `.pfx` 私钥、密码和账号 Cookie 不应提交到仓库，也不要粘贴到 issue、日志或文档中。
- 签名证书的发布者必须与包清单一致；项目设置了固定证书指纹时，也必须与实际构建证书一致。证书过期或指纹不匹配应单独处理，不能把关闭签名校验当作安装解决方案。
- 当前工作流上传 `AllLive.UWP/AppPackages` 下的 `.msix` 和 `.cer` 文件。只有核实发布者和来源后，才按 Windows 的提示安装、信任对应证书。
- 覆盖安装还受包身份、签名与版本约束。先保留旧版本和用户数据，不能仅凭产物生成就保证可以覆盖安装。

### 按分支核对签名与构建证据

以下记录核对于 **2026-11-09**，用于区分历史构建结果，不代表当前所有分支都可成功签名：

- `master` 的 [`bba0e2a` 工程配置](https://github.com/cyocyo10/AllLive/blob/bba0e2ae934cc3abb3c433ab40391a4e73b3f5ba/AllLive.UWP/AllLive.UWP.csproj)固定证书指纹为 `5F0BEA425CA140436E7E0440F480831C0792452C`。本整理分支继承该配置，未修改签名设置；查到的 `master` 最近一次成功 Build UWP 是 [2026-10-06 的构建](https://github.com/cyocyo10/AllLive/actions/runs/37449438691)，对应同一提交。
- `fix/douyu-login-session-20261104` 的 [`c044526` 工程配置](https://github.com/cyocyo10/AllLive/blob/c04452606f947262a5dc7b7947b842fa8568ac1a/AllLive.UWP/AllLive.UWP.csproj)使用另一指纹 `B029F0743A6568E6D1E3F601FA8BB8DEE95E01F6`，其 [2026-11-05 的构建](https://github.com/cyocyo10/AllLive/actions/runs/41950425218)成功。该结果仅证明该提交在当次构建条件下通过，不能作为 `master` 或本整理分支当前可签名的证据。

重新构建时，应按**目标分支和提交号**核对工程指纹、实际使用证书的公开指纹、有效期与发布者，以及该次构建日志和产物。上述历史记录不能证明当前 `SIGNING_CERTIFICATE` 的内容，也不能据此断言它与 `master` 匹配或不匹配。不要输出或提交证书私钥、密码或 secret 内容。

本次文档与文件整理只做静态检查，未运行 Windows 构建、签名或安装验证；签名设置差异仍保持原样，未将修复分支合入。

### Release 中的 QuickJS 配置

[AllLive.Core.csproj](../AllLive.Core/AllLive.Core.csproj) 已设置 `QuickJS.NET` 的 `PrivateAssets=all`，并在 `Release|AnyCPU` 下定义 `WINDOWS_UWP`。旧版 UWP README 中要求手工添加这两项的步骤已不适用。

相关实现入口：

- [DouyinScriptRuntime.cs](../AllLive.Core/Helper/DouyinScriptRuntime.cs)：按编译条件选择脚本运行器。
- [App.xaml.cs](../AllLive.UWP/App.xaml.cs)：为 UWP 注册 WebView 脚本运行器。
- [UWP 简要说明](../AllLive.UWP/README.md)。

## 版本与更新入口

- 安装包身份和基础版本以 [Package.appxmanifest](../AllLive.UWP/Package.appxmanifest) 为准；CI 会在构建时调整版本号的最后一段。
- [version.json](../AllLive.UWP/version.json) 是原有更新信息文件。当前 `Helper/Utils.cs` 的版本查询指向上游仓库对应文件，客户端另有商店更新检查，不会自动将本仓库 Actions 构建识别成新版本。
- 获取本仓库构建时，应自行核对 Actions 记录的分支、提交号和产物。本文不修改更新机制或应用身份。

## 验证范围

`master` 当前没有独立的 `tests` 目录，也没有统一的测试命令。其他分支已有的测试不会因为本次文档整理自动进入主线：

- `fix/douyu-login-session-20261104` 包含斗鱼签名、传输和流地址检查。
- `dev/audit-reliability-fixes` 包含登录和离线回归套件及对应 CI。

需要使用这些测试时，先切到对应分支阅读其测试说明，不要在 `master` 上照搬不存在的命令，也不要为了整理文件直接混合分支代码。

功能改动的验证至少区分以下结果：

1. 代码或静态检查是否通过。
2. Windows 编译、打包和签名是否通过。
3. 实机安装或覆盖安装是否通过。
4. 登录取消、重复打开、账号过期、切换直播间、刷新、长时间播放等行为是否实际验证。

只完成前两项时，记录“构建通过”，不要写成“登录和播放问题已解决”。高分辨率、高帧率播放问题还需保留平台、清晰度、线路、时间与脱敏日志等复现信息。

## 文件维护

- 构建输出和个人环境文件沿用 [.gitignore](../.gitignore)，不要把 `bin`、`obj`、安装包、私钥和本机配置作为源码保存。
- 第三方协议实现、嵌入脚本及其说明保留原路径和来源；本次不调整它们的实现。
- 清理历史备份文件前，应检查解决方案、工程文件、资源与代码中的引用；用途不明的文件先保留。
- [历史提交勘误](COMMIT_HISTORY_ERRATA.zh-CN.md)保留原内容和提交证据，它不代表相关源码问题已经修复。
