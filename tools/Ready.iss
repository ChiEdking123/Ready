; ============================================================================
;  Ready! Windows 安装包脚本（Inno Setup 6）
;
;  不要直接用 ISCC 编译本文件：走 tools\build-installer.ps1。
;  脚本会先 dotnet publish，再把语言包下载到本地，然后传路径进来。
;
;  设计取舍：
;  - 自包含发布：目标机不需要预装 .NET 10 桌面运行时。
;  - 装到 {localappdata}：不弹 UAC；且与 ReadyPaths.DataDirectory
;    （%LOCALAPPDATA%\Ready）分开，卸载默认不动用户的构思。
;  - WebView2 只检测不内置：Win11 与近年的 Win10 基本已自带，
;    内置离线包会让安装包再涨约 130MB。
; ============================================================================

#define AppName        "Ready!"
#define AppVersion     "0.1.0"
#define AppPublisher   "Ready! contributors"
#define AppUrl         "https://github.com/ChiEdking123/Ready"
#define AppExeName     "Ready.exe"

#ifndef PublishDir
  #define PublishDir "..\artifacts\installer\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif
#ifndef IconFile
  #define IconFile "..\src\Ready.WinForms\Assets\appicon.ico"
#endif
#ifndef LicenseFile
  #define LicenseFile "..\LICENSE"
#endif
#ifndef LangDir
  #define LangDir "..\artifacts\installer\lang"
#endif

[Setup]
; AppId 一旦发布就不要再改：它决定新版本是「升级」还是「并存装两份」。
AppId={{7C4E1A62-9D3B-4F58-B6A0-2E5D8C71F430}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}

; 装到用户目录：不触发 UAC，卸载也不需要管理员。
DefaultDirName={localappdata}\Programs\Ready
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; 64 位程序，不允许装进 32 位系统。
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputDir={#OutputDir}
OutputBaseFilename=Ready-{#AppVersion}-win-x64-setup
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
LicenseFile={#LicenseFile}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=auto
; Windows 10 1809 起才有稳定的 WebView2 与 PerMonitorV2 行为。
MinVersion=10.0.17763

[Languages]
Name: "zh"; MessagesFile: "{#LangDir}\ChineseSimplified.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; 自包含发布的全部产物。排除调试符号与 XML 文档，省几 MB。
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 只清理 WebView2 用户数据缓存。构思数据在别处，不在此列。
Type: filesandordirs; Name: "{app}\EBWebView"

[Code]
const
  WebView2RegKey = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  WebView2DownloadUrl = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';

var
  DeleteUserData: Boolean;

{ 从指定注册表根读 WebView2 版本，读不到就返回空串。 }
function ReadWebView2From(RootKey: Integer): String;
var
  Version: String;
begin
  Result := '';
  if RegQueryStringValue(RootKey, WebView2RegKey, 'pv', Version)
     and (Version <> '') and (Version <> '0.0.0.0') then
    Result := Version
  else
    Result := '';
end;

{ 读取 WebView2 运行时版本；没装或版本为 0.0.0.0 时返回空串。

  三个根都要查，缺一不可：
  - HKLM32：整机安装的 WebView2 落在 WOW6432Node 下。这是最常见的形态。
  - HKLM64：少数情况下装在 64 位原生视图。
  - HKCU：当前用户安装（按用户装了 Edge 的用户多半在这里）。

  这里必须用 HKLM32/HKLM64 而不是裸 HKLM：本脚本设了
  ArchitecturesInstallIn64BitMode，Setup 会切到 64 位安装模式，
  裸 HKLM 随之被重定向到 64 位视图，正好漏掉 WOW6432Node——
  早期版本就是这么误报「没装 WebView2」的。 }
function GetWebView2Version(): String;
begin
  Result := ReadWebView2From(HKLM32);
  if Result <> '' then
  begin
    Log('WebView2 runtime ' + Result + ' (HKLM32)');
    Exit;
  end;

  Result := ReadWebView2From(HKLM64);
  if Result <> '' then
  begin
    Log('WebView2 runtime ' + Result + ' (HKLM64)');
    Exit;
  end;

  Result := ReadWebView2From(HKCU);
  if Result <> '' then
  begin
    Log('WebView2 runtime ' + Result + ' (HKCU)');
    Exit;
  end;

  Log('WebView2 runtime not found in HKLM32, HKLM64 or HKCU');
end;

{ 缺 WebView2 不拦安装，只在装完给出下载入口。
   Ready! 的界面完全靠它渲染，缺了就白屏，必须说清楚而不是静默放过。 }
procedure CheckWebView2();
var
  ErrorCode: Integer;
begin
  if GetWebView2Version() <> '' then
    Exit;

  { 静默安装（/SILENT、/VERYSILENT、CI）下不能弹框，否则会一直挂着等人点。 }
  if WizardSilent() then
    Exit;

  if MsgBox('Ready! 的界面基于 Microsoft Edge WebView2 渲染。' + #13#10 + #13#10 +
            '这台电脑上没检测到 WebView2 运行时，安装完成后程序会打不开（白屏）。' + #13#10 + #13#10 +
            '现在打开官方下载页吗？（下载后双击安装，装完不用重装 Ready!）',
            mbConfirmation, MB_YESNO) = IDYES then
  begin
    ShellExec('open', WebView2DownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    CheckWebView2();
end;

{ 卸载时问一句要不要连构思数据一起删，默认按钮是「否」。
   本地优先应用的底线是「卸载软件不该毁掉用户写下的东西」，
   所以删数据只能是用户主动选「是」，且删的是 %LOCALAPPDATA%\Ready 而不是安装目录。
   返回值恒为 True：问的只是「要不要删数据」，从未打算拦截卸载本身。 }
function InitializeUninstall(): Boolean;
var
  DataDir: String;
begin
  Result := True;
  DeleteUserData := False;

  DataDir := ExpandConstant('{localappdata}\Ready');
  if not DirExists(DataDir) then
    Exit;

  { 静默卸载必须不弹框：带 /SUPPRESSMSGBOXES 时 MsgBox 会被抑制并返回默认值，
     不带则会一直挂在那里等人点。两种都不该出现，直接跳过询问按「保留」处理。 }
  if UninstallSilent() then
    Exit;

  DeleteUserData := MsgBox(
    '你的构思数据保存在：' + #13#10 + DataDir + #13#10 + #13#10 +
    '重新安装 Ready! 之后可以直接继续使用。' + #13#10 + #13#10 +
    '要同时删除这些构思数据吗？此操作不可恢复。',
    mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and DeleteUserData then
    DelTree(ExpandConstant('{localappdata}\Ready'), True, True, True);
end;
