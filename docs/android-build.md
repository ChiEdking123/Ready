# Android 构建细节

## 体积开关只在 Release 生效

`Ready.csproj` 里 `AndroidLinkMode=SdkOnly` 与 `AndroidLinkResources=true` 都在 `Condition="'$(Configuration)' == 'Release'"` 之下：Debug 保留完整程序集，单步调试时类型和源码对得上；Release 才裁剪。

实测同一份代码：Debug 86 MB（含 x86_64 与 PDB）→ Release 17.75 MB（arm64）。

## 为什么是 SdkOnly 而不是 Full

Full 能再省约 1.5 MB，但在 .NET 10 的 MAUI Blazor Hybrid 上会让 JS↔.NET 消息通道静默失效。

实测模拟器：Full 包启动后停在 `index.html` 的加载文案上，控制台**零报错**，`window.external` 桥 / `Blazor` / `DotNet` 三个全局对象都齐全，只是渲染永远不开始；同一份代码用 SdkOnly 构建就完全正常。用 `TrimmerRootAssembly` 保住四个 Blazor 程序集也救不回来，说明被裁掉的不是 `RootComponent` 那几个类型。

**改这个开关必须先在真机上跑一次首屏渲染**——构建成功、安装成功、控制台没错误，全都不等于能渲染。

## 裁剪与 JSON 序列化

即使 SdkOnly，`System.Text.Json` 的反射重载也会被 ILLink 判为不可静态分析，可能把字段裁掉——表现是「保存成功但重开少了几项」。

所有持久化与导出都走 `Services/ReadyJson.cs` 里的源生成 `JsonSerializerContext`，**不要再新增裸 `JsonSerializer.Serialize<T>` 调用**。

## Debug 包必须内嵌程序集才能手动安装

Android 的 Debug 构建默认开启 Fast Deployment——托管程序集不打进 APK，而是由 `dotnet build -t:Install` 通过 adb 单独推送到设备。这样产出的 APK 拷到手机上安装后，一启动就会因为找不到程序集而直接退出。

项目已在 `Ready.csproj` 里固定 `EmbedAssembliesIntoApk=true`，所以常规 `dotnet build` 出来的 Debug APK 可以直接 adb install。

## `blazor.webview.js` 必须带 `autostart="false"`

MAUI 的 `BlazorWebView` 要先注入 `window.external` 桥，再自己调 `Blazor.start()`。少了这个属性，脚本会在桥就绪前自启动并抛 `TypeError: window.external.receiveMessage is not a function`；原生侧随后再启动又撞上「Blazor has already started」。两次都失败，页面就永远停在 `index.html` 里那段静态加载文案上——**一片空白，但没有任何崩溃日志**。

排查入口是 WebView 远程调试：

```powershell
adb forward tcp:9222 localabstract:webview_devtools_remote_<pid>
# 然后看 http://127.0.0.1:9222/json/list 里的控制台
```

## 发布签名

仓库里没有签名密钥（`.gitignore` 挡住 `*.jks` 与 `*.keystore`）。发布包在构建机上用仓库外的密钥签名：

```powershell
dotnet build src/Ready/Ready.csproj -c Release -f net10.0-android `
  -p:JavaSdkDirectory="$env:JAVA_HOME" `
  -p:AndroidSdkDirectory="$env:ANDROID_HOME" `
  -p:AndroidKeyStore=true `
  -p:AndroidSigningKeyStore="$env:READY_KEYSTORE" `
  -p:AndroidSigningKeyAlias=ready `
  -p:AndroidSigningStorePass="$env:READY_KS_PASS" `
  -p:AndroidSigningKeyPass="$env:READY_KS_PASS"
```

**密钥不要进仓库、不要上传、不要贴进 Issue**：Android 用签名判定「同一个应用」，换密钥会让已安装的用户无法覆盖升级，只能卸载重装并丢数据。目前的发布密钥固定为 `CN=Ready!, OU=Ready Contributors`，不会轮换。

构建完务必用 keytool 复核，别只看构建成功：

```powershell
keytool -printcert -jarfile app.ready.gamedesign-Signed.apk | Select-String "CN=|SHA256:"
```

如果出现 `CN=Android Debug`，说明这次构建落回了调试密钥，不能发布。

## 验证包是否完整

```powershell
unzip -l app.ready.gamedesign-Signed.apk | grep -c "lib_.*\.dll\.so"
```

.NET 10 把每个程序集编成一个 ELF blob（如 `lib/arm64-v8a/lib_Ready.dll.so`），不再是 `assemblies/` 目录；这个计数为 0 就说明包是残缺的。

**比体积务必两边都清 `bin`/`obj`**。增量构建会跳过依赖包的 Kotlin linkdata 打包，条目数从 1319 掉到 965，体积看起来小了几十万字节，但那是没重新打包，不是省出来的。
