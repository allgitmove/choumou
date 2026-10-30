# 绸缪 · Choumou

为 Codex 添加自己的 MP4 启动影片。导入视频，点击启动图标，播放结束或双击画面后，以加速缩放衔接到 Codex 窗口。

![Choumou](源码/icon.png)

## 功能

- 自定义 MP4、音量和全屏播放。
- macOS 风格设置界面。
- 双击播放画面跳过，约 720 毫秒加速缩放衔接。
- 创建启动快捷方式，并支持恢复原入口。
- 本地保存视频和设置，发布包不包含个人数据。

## 使用

适用于 Windows 10/11 x64，依赖 .NET Framework 4.8 和系统视频解码能力。需自行安装 Codex。

下载仓库内的「一键安装.exe」，运行后打开桌面「绸缪」导入自己的 MP4，再使用动画启动入口。

**当前版本固定安装到 `Y:\ARTWORK SUPREME\绸缪`，需要有 Y: 盘。** 已有视频和设置会保留。普通窗口按视频比例适配；全屏铺满可能裁切边缘。

## 构建

在 Windows PowerShell 中依次运行：

```powershell
& .\源码\build.ps1
& .\源码\Build-Installer.ps1
```

构建前关闭绸缪设置或播放器。编译器使用系统自带 .NET Framework C# 编译器。详细说明见 [使用说明](使用说明.md)。

## 本地数据

`settings.json`、`settings.json.bak`、`media/` 和 `.integration/` 为个人配置、影片和快捷方式恢复数据，不应上传。

这是独立的第三方启动器，与 OpenAI 无隶属关系。