# AllLive.UWP

Windows UWP 客户端，引用 `AllLive.Core`，使用 WinUI 2、WebView2 和 FFmpegInteropX。

- [项目总览](../README.md)
- [开发与构建说明](../docs/BUILDING.zh-CN.md)
- [UWP 项目配置](AllLive.UWP.csproj)
- [应用身份与平台要求](Package.appxmanifest)

## Release 与脚本运行器

当前 `AllLive.Core.csproj` 已包含以下配置，不需要再按旧说明手工替换：

- `QuickJS.NET` 引用设置 `PrivateAssets=all`。
- `Release|AnyCPU` 条件下定义 `TRACE;WINDOWS_UWP`。

`WINDOWS_UWP` 会影响核心库中的脚本运行器选择。UWP 应用启动时在 `App.xaml.cs` 注册 WebView 脚本运行器。修改构建方式前，应同时核对核心库配置与应用初始化，不要直接删掉脚本依赖或条件编译符号。

签名证书、包身份和发布配置见上面的构建说明；本目录的 `version.json` 不等于本仓库 Actions 构建的版本清单。
