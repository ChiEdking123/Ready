# 中国大陆网络环境下的 Android 构建

**仅适用于中国大陆网络环境。** 海外网络通常不需要这一步。

## `dl.google.com` 不可达

`-t:InstallAndroidDependencies` 会在 SSL 握手阶段失败（`XAIAD7000`）。可以改用镜像手动铺 SDK。

从 <https://mirrors.cloud.tencent.com/AndroidSDK/> 取这三个包：

- `platform-36_rXX.zip` → `%ANDROID_HOME%\platforms\android-36`
- `build-tools_r36_windows.zip` → `%ANDROID_HOME%\build-tools\36.0.0`
- `platform-tools_rXX-win.zip` → `%ANDROID_HOME%\platform-tools`

然后在 `%ANDROID_HOME%\licenses\android-sdk-license` 写入许可哈希。

## 仓库路径含非 ASCII 字符

Android 的 AAPT2 无法处理中文等字符的路径，会报 `APT2265`。把仓库放到纯英文路径，或建一个 ASCII 目录联接再构建：

```powershell
mklink /J C:\readybuild\repo "<你的仓库路径>"
dotnet build C:\readybuild\repo\src\Ready\Ready.csproj -f net10.0-android
```

## 缺少 `JavaSdkDirectory`

若 `ANDROID_HOME` / `JAVA_HOME` 未设置，构建会报 `XA5300`（找不到 Java SDK 目录）。直接在命令行指定即可：

```powershell
dotnet build src/Ready/Ready.csproj -c Release -f net10.0-android `
  -p:JavaSdkDirectory="<JDK 17 路径>" `
  -p:AndroidSdkDirectory="<Android SDK 路径>"
```
