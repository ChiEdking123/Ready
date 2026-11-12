# Ready!

> 好游戏，从一句灵感开始。

> [!IMPORTANT]
> **本项目由 AI 全程制作。**
> 从需求梳理、界面设计、代码实现到文档撰写，全部由 AI 编程助手完成，人类只负责提出方向、验收结果与做决定。
> 代码经过自动化测试与真实设备验证，但请以「AI 生成的独立项目」而非「有维护团队的产品」来看待它：没有正式的质量承诺、没有安全审计，遇到问题请提 Issue，不要假定有人兜底。

Ready! 是一个面向游戏创作者的**本地优先**构思工作台。**Android 是主目标**（.NET 10 / .NET MAUI + Blazor Hybrid），Windows 由独立的 WinForms 宿主承载同一套 Blazor 界面。

完全离线：不联网、不登录、不上传任何数据，Android 清单里没有 `INTERNET` 权限。

[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![CI](https://github.com/ChiEdking123/Ready-/actions/workflows/ci.yml/badge.svg)](https://github.com/ChiEdking123/Ready-/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

## 它解决什么问题

想到一个游戏点子的时候，人通常只有一句话。大多数笔记工具会让你先建文档、想标题、选分类——等你理顺结构，那点灵感已经凉了。

Ready! 的做法是：打开就能打字。一句话总结是唯一必填项，其余全是可选的标签，点两下就能给这份想法一个轮廓。详细设计默认折叠，等你真的想往下想的时候再展开。

## 功能

- **一句话总结 + 快速标签**：类型、视角、画面风格、背景设定、角色，全部支持多选与自定义。
- **角色有顺序**：按选择顺序排列，第一个就是主角；取消选择后新的首项自动补位。
- **详细的在后**：参考作品、平台、受众、时长、核心循环、情绪、输入、规模、资源、不做什么、商业模式，默认折叠。
- **构思库**：搜索、置顶、编辑、复制、删除、排序。进入页面直接是搜索栏，没有统计卡挤在前面。
- **完全本地**：JSON 持久化，原子写入、损坏备份、导入导出。写入串行排队，落盘失败会回滚内存并如实报错——不会出现「界面说已保存、重启却是旧内容」。
- **外观**：浅色 / 深色 / 跟随系统，5 种主题色，界面比例五档（桌面端还支持 `Ctrl` + 滚轮）。
- **可访问**：弹出层可全键盘操作（`Tab` 循环、`Esc` 取消），移动端按钮纵向堆叠、可点区域不小于 40px。
- **中文输入友好**：输入框只在失焦时同步，组合输入期间不会因为重渲染吃掉候选窗或未提交的文字。

## 截图

> 待补。如果你已经装了应用，欢迎通过 Issue 提供截图。

| 捕捉 | 构思库 | 设置 |
| --- | --- | --- |
| _待补_ | _待补_ | _待补_ |

## 下载

到 [Releases](https://github.com/ChiEdking123/Ready-/releases) 取最新版本。

- **Android**：`app.ready.gamedesign-*-Signed.apk`，仅 arm64，Android 6.0（API 23）及以上。
- **Windows**：目前需自行构建，见下节。

## 从源码构建

```powershell
git clone https://github.com/ChiEdking123/Ready-.git
cd Ready-
```

> 仓库名结尾的连字符是 GitHub 上真实的仓库名，不是笔误。

### 环境

1. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（`global.json` 已钉住 10.0.100，允许 `latestFeature` 前滚）。
2. MAUI Android 工作负载（管理员终端可能需要）：
   ```powershell
   dotnet workload install maui-android
   ```
3. Android 构建需要 JDK 17 与 Android SDK（SDK 36，最低运行 Android 6.0 / API 23）。Windows 端只需要 WinForms + WebView2 运行时，**不需要** MAUI 工作负载。

### Windows

界面代码只有一份：`src/Ready.WinForms` 把 `src/Ready` 下的 `Components`、`Models`、`Services`、`wwwroot` **直接链接**过来，不复制。

```powershell
dotnet restore Ready.slnx
dotnet build src/Ready.WinForms/Ready.WinForms.csproj
dotnet run --project src/Ready.WinForms/Ready.WinForms.csproj
```

产物在 `src/Ready.WinForms/bin/Debug/net10.0-windows/Ready.exe`，unpackaged 模式，双击即可运行。

> **为什么是 WinForms 而不是 WPF / WinUI3**：官方三个 Blazor 宿主里，只有 WinForms 版走 HWND。另外两个是 visual hosting，第三方输入法拿不到光标矩形、候选窗根本不画。细节见 [docs/windows-ime.md](docs/windows-ime.md)。

### Android

```powershell
# Debug（日常开发；不裁剪，增量构建快）
dotnet build src/Ready/Ready.csproj

# Release（真发货的一份：arm64 only、SdkOnly link、资源裁剪）
dotnet build src/Ready/Ready.csproj -c Release
```

生成的 APK 位于 `src/Ready/bin/<Configuration>/net10.0-android/`。发布版本请配置自己的签名密钥，**不要将私钥提交到仓库**（`.gitignore` 已挡住 `*.jks` / `*.keystore`）。

体积开关只在 Release 生效：`AndroidLinkMode=SdkOnly` 与 `AndroidLinkResources=true` 都在 `Condition="'$(Configuration)' == 'Release'"` 之下。实测同一份代码 Debug 86 MB → Release 17.75 MB（arm64）。

关于 SdkOnly 而不是 Full、JSON 源生成、内嵌程序集这几个坑，见 [docs/android-build.md](docs/android-build.md)。

> 中国大陆网络环境下 `dl.google.com` 不可达、或仓库路径含中文导致 `APT2265` 时，见 [docs/android-build-cn.md](docs/android-build-cn.md)。

### 测试

```powershell
dotnet test tests/Ready.Tests/Ready.Tests.csproj
```

测试项目直接编译核心模型与存储服务，不依赖 Android SDK，因此 CI 在 `ubuntu-latest` 上几十秒就能跑完。

### 图标

应用图标的唯一设计源是 `logo/512x512.png`，派生素材全部由脚本生成，**不要手改**生成出来的文件：

```powershell
pwsh -File tools/make-icons.ps1
```

尺寸推导与「不裁切」约束见 [docs/icon-pipeline.md](docs/icon-pipeline.md)。

## 使用提示

- **`Ctrl + 滚轮`**：缩放整个界面（85% – 140%），与设置里的「画面比例」是同一个值。仅桌面端。
- **主题色**：切换时中性色（背景、分割线、正文）会跟着偏向该色相，深色模式也是同一套色阶。
- **关闭行为**：点关闭会问「留在后台还是彻底关闭」，默认只对这一次生效；勾「下次不再询问」才记住。仅 Windows。
- **安卓返回键**：先关浮层、再回捕捉页；在捕捉页按一次提示「再按一次返回键退出」，2 秒内再按才真的退出。有未创建的草稿时先弹确认。

## 数据与隐私

应用数据保存在操作系统提供的应用专属数据目录下的 `ready-data.json`。不需要网络、登录或遥测。

JSON 备份**仅包括已创建的构思**；自定义标签和设置包含在应用本地数据中，但不包含在构思导出里。

## 项目结构

```text
src/Ready/            # Android 头（net10.0-android，主目标）
  Models/             # 构思、设置、标签目录与导出模型
  Services/           # 本地持久化、构思管理、源生成 JSON 上下文
  Components/         # 共享 Blazor UI（Windows 头链接同一份）
  Platforms/Android/  # 安卓原生入口、安全区、系统栏配色
  wwwroot/            # 响应式样式与快捷键
src/Ready.WinForms/   # Windows 头（WinForms + WebView2，HWND 托管）
tests/Ready.Tests/    # 直接编译模型与存储层，不需要 Android SDK
docs/                 # 构建、输入法与图标流水线的细节说明
```

## 设计取舍

**移动端优先意味着首屏只放一件事。** 窄屏下捕捉页的标题区整块隐藏，目标是一打开应用就能开始写字。首屏那条横幅是一条开发者语录，窄屏也保留，只是压成紧凑版。

标签允许同一构思存在多个方向，适合灵感捕捉阶段。角色列表是唯一有顺序语义的标签组。详细设计和执行规划默认折叠，避免打断快速记录。

## 参与

欢迎 Issue 和 PR。开始之前请读 [CONTRIBUTING.md](CONTRIBUTING.md) 与 [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)。安全问题请按 [SECURITY.md](SECURITY.md) 私下报告。

## 许可

以 [MIT License](LICENSE) 发布。

第三方组件与其许可见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

本项目的代码与文档由 AI 生成，因此仓库里不存在「作者本人为此承担质量担保」的隐含承诺；MIT 的免责条款在此处是字面意思。

首屏展示的开发者语录版权归各原作者所有，本项目仅作短句引用并注明出处，中文译文由本项目翻译；个别条目为流传说法、原始出处待考。若权利人认为使用不妥，请通过 Issue 联系，我们会立即移除。
