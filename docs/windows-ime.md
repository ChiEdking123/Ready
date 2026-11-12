# 为什么桌面端是 WinForms 而不是 WPF / WinUI3

留档以免回退。

MAUI / WinUI3 的 `Microsoft.UI.Xaml.Controls.WebView2` 走 **visual hosting**（`CoreWebView2CompositionController`）：没有真实的焦点窗口，输入法拿不到可用的光标矩形。微软拼音在这种情况下会退化到「画在屏幕原点或另一块屏」，第三方输入法（如微信输入法）的选择是**干脆不画**——表现就是拼音能打进输入框、候选框永远不出现，而且最大化、拖动边框、换显示器都唤不出来。

## 实测对照

同一台机器、同一输入法：

| 宿主 | WebView2 托管方式 | 第三方输入法候选窗 |
| --- | --- | --- |
| Edge | HWND | 正常 |
| Windows 11 记事本 | 无 WebView2（WinUI3 原生控件） | 正常 |
| 旧 Windows 头（MAUI/WinUI3，已删除） | visual hosting | 不出现 |
| 现 Windows 头（WinForms） | HWND | 正常 |

`Microsoft.AspNetCore.Components.WebView.Wpf` 10.0.0 内部用的也是 `WebView2CompositionControl`，和 WinUI3 一样属于 visual hosting，换过去并不解决问题。

## 已排除的可能性

把整个 DOM 换成裸 `<textarea>` 仍然没有候选窗，所以问题不在 CSS、Blazor 或 app.js。窗口采样也确认过，候选窗对象在旧宿主下从未被 `show`。

之前的规避方案（窗口几何变化后对输入框做 `blur` → 等 60ms → `focus` 的焦点往返）已全部删除：它在 visual hosting 下实测无效，而且会打断正在进行的输入。`Platforms/Windows/ImeAnchorHost.cs` 与 `app.js` 里的 `ready.requestImeAnchor` / `ready.primeIme` 都已移除。

**结论：桌面宿主必须是走 HWND 的 WinForms。** 这也是 `src/Ready` 的 `net10.0-windows` 目标被删除的原因——留着只会让人误以为桌面版可以走 MAUI。
