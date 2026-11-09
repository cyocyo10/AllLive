<p align="center">
    <img width="128" src="/Assets/Logo.png" alt="All Live logo">
</p>
<h2 align="center">聚合直播</h2>
<p align="center">
聚合多个站点，简简单单看直播。
</p>


![Screenshot](/Assets/UWP2.png)

## 关于本仓库

这是 [xiaoyaocz/AllLive](https://github.com/xiaoyaocz/AllLive) 的个人维护分支，保留 C# 核心库、Windows UWP 客户端和控制台示例。保留上游 [LICENSE](LICENSE)、参考项目、声明和原作者赞助入口。

- 使用本仓库的构建：见下方“获取与安装”。
- 阅读代码：见“项目结构”。
- 本地构建、签名和验证：见 [开发与构建说明](docs/BUILDING.zh-CN.md)。
- 核对旧提交说明：见 [提交历史勘误](docs/COMMIT_HISTORY_ERRATA.zh-CN.md)。

## 支持的直播平台

- 🐅 虎牙直播

- 🐟 斗鱼直播

- 📺 哔哩哔哩直播

- 🎵 抖音直播

## 获取与安装

### 本仓库构建

打开 [Build UWP 工作流](https://github.com/cyocyo10/AllLive/actions/workflows/build-uwp.yml)，先核对运行记录的分支、提交号和构建结果，再从成功记录的 Artifacts 中下载 `AllLive-UWP-x64`。

当前工作流生成 x64 侧载包。安装包、公开证书与安装条件见 [安装与签名说明](docs/BUILDING.zh-CN.md#安装与签名)。不要把不同分支的构建混用，也不要仅凭构建成功认定登录、播放和覆盖安装已验证。

本仓库的修复分支与 `master` 分开维护。以下为 **2026-11-09 核对时**的状态，后续以 GitHub 分支和 PR 为准：

- `master`：默认主线，不包含下列未合并修复。
- `fix/douyu-login-session-20261104`：斗鱼登录、签名会话和打包修复；已包含 `fix/douyu-expiry-20261012` 的断流修复。
- `dev/audit-reliability-fixes`：独立的可靠性修复与回归测试，尚未合入主线。

本次文档整理不合并以上修复，也不代表已发布新的功能版本。

### 上游 Microsoft Store 版本

下方商店入口属于原作者发布的版本，与本仓库的 Actions 构建分别维护。

<p align="left">
    <a title="从 Microsoft Store 中获取" href="https://apps.microsoft.com/detail/9n1twg2g84vd" target="_blank">
        <img src="https://get.microsoft.com/images/en-US%20dark.svg" width=120 />
    </a>
</p>

可以在Microsoft Store中搜索“[聚合直播](https://apps.microsoft.com/detail/9n1twg2g84vd)”下载安装。

免费试用永不过期，购买应用视为无偿赞助作者。


系统要求：Windows 10 1903及以上版本。

项目保留 x86、x64、ARM、ARM64 配置；本仓库当前 CI 仅打包 x64，其他架构需单独构建和验证。

如果你想在其他系统上使用，请查看[Simple Live](https://github.com/xiaoyaocz/dart_simple_live)项目。

## 项目结构

- [AllLive.Core](AllLive.Core/)：`netstandard2.0` 核心库，包含各平台直播信息、取流、弹幕、签名和公共模型。
- [AllLive.Console](AllLive.Console/)：`AllLive.ConsoleApp` 项目，`net8.0` 控制台示例，调用核心库获取直播信息和弹幕。
- [AllLive.UWP](AllLive.UWP/)：Windows UWP 客户端。`Views` / `ViewModels` 负责页面和状态，`Controls` 放登录等控件，`Helper` 包含账号、设置、数据库和脚本运行器。
- [Assets](Assets/)：README 使用的项目图标和截图；应用安装资源位于 `AllLive.UWP/Assets`。
- [.github/workflows](.github/workflows/)：GitHub Actions 构建流程。
- [docs](docs/)：开发说明与历史勘误。

解决方案入口是 [AllLive.sln](AllLive.sln)。`dart_simple_live`、`pure_live` 为另行维护的参考项目，不属于此解决方案。


## 参考及引用
[https://github.com/wbt5/real-url](https://github.com/wbt5/real-url)

[https://github.com/lovelyyoshino/Bilibili-Live-API/blob/master/API.WebSocket.md](https://github.com/lovelyyoshino/Bilibili-Live-API/blob/master/API.WebSocket.md)

[https://github.com/IsoaSFlus/danmaku](https://github.com/IsoaSFlus/danmaku)

[https://www.cnblogs.com/sdflysha/p/20210117-douyu-barrage-with-dotnet.html](https://www.cnblogs.com/sdflysha/p/20210117-douyu-barrage-with-dotnet.html)

[https://github.com/BacooTang/huya-danmu](https://github.com/BacooTang/huya-danmu)

[https://github.com/TarsCloud/Tars](https://github.com/TarsCloud/Tars)

## 声明

本项目的所有功能都是基于互联网上公开的资料开发，无任何破解、逆向工程等行为。

本项目仅用于学习交流编程技术，严禁将本项目用于商业目的。如有任何商业行为，均与本项目无关。

如果本项目存在侵犯您的合法权益的情况，请及时与开发者联系，开发者将会及时删除有关内容。

## 赞助

如果觉得此项目有用，可以考虑赞助我喝杯咖啡❤

[爱发电](https://afdian.net/a/xiaoyaocz)

[应用商店购买](https://apps.microsoft.com/detail/9n1twg2g84vd)

## 维护记录

- [开发与构建说明](docs/BUILDING.zh-CN.md)：开发环境、UWP 构建、签名、产物和验证边界。
- [提交历史勘误](docs/COMMIT_HISTORY_ERRATA.zh-CN.md)：保留原提交编号，逐条说明历史提交描述与当时改动的差异。
