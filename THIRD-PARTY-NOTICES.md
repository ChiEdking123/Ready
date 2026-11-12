# 第三方组件许可声明

Ready! 自身以 MIT License 发布（见 `LICENSE`）。下表列出随二进制分发的直接依赖及其许可。完整许可文本见各 NuGet 包内的 `LICENSE` / `LICENSE.txt`。

## 运行时依赖

| 组件 | 版本 | 许可 |
| --- | --- | --- |
| Microsoft.Maui.Controls | 10.0.0 | MIT |
| Microsoft.AspNetCore.Components.WebView.Maui | 10.0.0 | MIT |
| Microsoft.AspNetCore.Components.WebView.WindowsForms | 10.0.0 | MIT |
| Microsoft.Extensions.Logging.Debug | 10.0.0 | MIT |
| Microsoft.Web.WebView2 | 1.0.3179.45 | Microsoft 专有许可（可再分发） |
| .NET Runtime 与基础类库 | 10.0 | MIT |
| Mono / .NET for Android 运行时 | 随工作负载 | MIT |

## 构建与测试依赖（不随二进制分发）

| 组件 | 版本 | 许可 |
| --- | --- | --- |
| xunit.v3 | 3.0.1 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |

## 素材

- `logo/512x512.png` 及全部派生的应用图标、启动屏、托盘图标由本项目自绘，与代码同以 MIT 提供。
- `src/Ready/Components/Icon.razor` 中的 27 个图标为手写 SVG path，未复制任何第三方图标库的数据。
- 界面字体使用系统字体栈，不分发任何字体文件。

## 首屏开发者语录

捕捉页展示的开发者语录版权归各原作者所有，本项目仅作短句引用并注明出处，中文译文由本项目翻译。个别条目为流传说法、原始出处待考，已在 `src/Ready/Models/GameQuotes.cs` 中如实标注。若权利人认为使用不妥，请通过 Issue 联系，我们会立即移除。
