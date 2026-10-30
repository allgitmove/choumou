$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$wpf=Join-Path $framework 'WPF'
$exe=Join-Path $projectRoot '绸缪.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$PSScriptRoot/icon.ico" "/out:$exe" "/r:$wpf/PresentationFramework.dll" "/r:$wpf/PresentationCore.dll" "/r:$wpf/WindowsBase.dll" "/resource:$PSScriptRoot/Dashboard.xaml,Dashboard.xaml" "/resource:$PSScriptRoot/icon.png,AppIcon.png" /r:System.Xaml.dll /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'Launcher.cs')
if($LASTEXITCODE -ne 0){throw '应用编译失败。请先退出绸缪设置和播放窗口。'}
Copy-Item -LiteralPath $exe -Destination (Join-Path $projectRoot '动画启动.exe') -Force
Write-Output $exe
