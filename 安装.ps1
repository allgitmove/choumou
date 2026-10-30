[CmdletBinding()]
param([string]$InstallRoot='Y:\ARTWORK SUPREME\绸缪',[switch]$NoShortcuts)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
$root=[IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
$main=Join-Path $root '绸缪.exe'
$boot=Join-Path $root '动画启动.exe'
if(!(Test-Path -LiteralPath $main) -or !(Test-Path -LiteralPath $boot)){throw '安装目录缺少程序文件。'}
$cfgPath=Join-Path $root 'settings.json'
if(!(Test-Path -LiteralPath $cfgPath)){
 @{Video='';Target='shell:AppsFolder\OpenAI.Codex_2p2nqsd0c76g0!App';Volume=0.5;Fullscreen=$false}|ConvertTo-Json|Set-Content -LiteralPath $cfgPath -Encoding UTF8
}
$cfg=Get-Content -LiteralPath $cfgPath -Raw -Encoding UTF8|ConvertFrom-Json
if($NoShortcuts){Write-Output '已验证程序文件并初始化设置；测试模式未改动桌面。';exit 0}
$ws=New-Object -ComObject WScript.Shell
$stateRoot=Join-Path $root '.integration';New-Item -ItemType Directory -Path $stateRoot -Force|Out-Null
$stateFile=Join-Path $stateRoot 'state.json'
$state=if(Test-Path -LiteralPath $stateFile){Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8|ConvertFrom-Json}else{[pscustomobject]@{Entries=@()}}
function Hash([string]$p){$sha=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($p);try{[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}}
function SaveState{$temp=$stateFile+'.tmp';$script:state|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $temp -Encoding UTF8;Move-Item -LiteralPath $temp -Destination $stateFile -Force}
$desktop=[Environment]::GetFolderPath('DesktopDirectory');$programs=[Environment]::GetFolderPath('Programs')
foreach($path in @((Join-Path $desktop '绸缪.lnk'),(Join-Path $programs '绸缪.lnk'))){
 if(Test-Path -LiteralPath $path){$existing=$ws.CreateShortcut($path);if([IO.Path]::GetFileName($existing.TargetPath) -notin @('绸缪.exe','开场白.exe','MP4 Codex Launcher.exe')){throw ('同名快捷方式属于其他程序，未覆盖：'+$path)}}
 $link=$ws.CreateShortcut($path);$link.TargetPath=$main;$link.Arguments='';$link.WorkingDirectory=$root;$link.IconLocation=$main+',0';$link.Description='绸缪 · 自定义 Codex 启动影片';$link.Save()
}
$legacy=(Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Documents\Codex\2026-10-24\jie\outputs\MP4-Codex-Launcher')
$ownedRoots=@($root,$legacy)
$candidates=@()
$taskbar=Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar'
foreach($folder in @($desktop,$programs,$taskbar)){if(Test-Path -LiteralPath $folder){$candidates+=@(Get-ChildItem -LiteralPath $folder -Filter '*.lnk' -File -Recurse|Select-Object -ExpandProperty FullName)}}
$matching=0;$rewritten=0
foreach($path in $candidates){
 $old=$ws.CreateShortcut($path);$oldTarget=$old.TargetPath
 $isOwned=$false;foreach($ownedRoot in $ownedRoots){if($oldTarget -ieq (Join-Path $ownedRoot '动画启动.exe')){$isOwned=$true}}
 $isOfficial=($oldTarget -ieq $cfg.Target) -or (($oldTarget -ieq (Join-Path $env:WINDIR 'explorer.exe')) -and ($old.Arguments.Trim('"') -ieq $cfg.Target))
 if(!$isOwned -and !$isOfficial){continue}
 $matching++
 $record=@($state.Entries|Where-Object {$_.Path -ieq $path -and !$_.Restored})|Select-Object -Last 1
 if($record -and $oldTarget -ieq $boot){if((Hash $path) -ne $record.InstalledHash){Write-Output ('保留用户已修改的入口：'+$path)};continue}
 if($record -and (Hash $path) -ne $record.InstalledHash){Write-Output ('保留用户已修改的入口：'+$path);continue}
 $before=Hash $path;$backup=Join-Path $stateRoot ([guid]::NewGuid().ToString('N')+'.lnk')
 if($isOfficial){Copy-Item -LiteralPath $path -Destination $backup}else{
  # Recover an official launch entry when migrating an older animation shortcut whose backup is missing.
  $baseline=$ws.CreateShortcut($backup);if($cfg.Target -like 'shell:AppsFolder\*'){$baseline.TargetPath=Join-Path $env:WINDIR 'explorer.exe';$baseline.Arguments=$cfg.Target}else{$baseline.TargetPath=$cfg.Target};$baseline.IconLocation=$old.IconLocation;$baseline.Hotkey=$old.Hotkey;$baseline.WindowStyle=$old.WindowStyle;$baseline.Save()
 }
 $temp=Join-Path ([IO.Path]::GetDirectoryName($path)) ([guid]::NewGuid().ToString('N')+'.lnk')
 try{
  $new=$ws.CreateShortcut($temp);$new.TargetPath=$boot;$new.WorkingDirectory=$root;$new.IconLocation=$old.IconLocation;$new.Hotkey=$old.Hotkey;$new.WindowStyle=$old.WindowStyle;$new.Description='播放绸缪启动影片，然后进入 Codex';$new.Save()
  if((Hash $path) -ne $before){throw ('快捷方式在操作期间被修改：'+$path)}
  $newRecord=[pscustomobject]@{Path=$path;Backup=$backup;OriginalHash=(Hash $backup);InstalledHash=(Hash $temp);Restored=$false}
  if($record){$record.Restored=$true};$state.Entries+=@($newRecord);SaveState
  Move-Item -LiteralPath $temp -Destination $path -Force
  if((Hash $path) -ne $newRecord.InstalledHash){throw '启动图标校验失败。'};$rewritten++
 }finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp}}
}
if($matching -eq 0){
 $path=Join-Path $desktop 'Codex（动画）.lnk';if(Test-Path -LiteralPath $path){throw '已有同名动画快捷方式，未覆盖。'}
 $link=$ws.CreateShortcut($path);$link.TargetPath=$boot;$link.WorkingDirectory=$root;$link.IconLocation=$main+',0';$link.Save()
 $state.Entries+=@([pscustomobject]@{Path=$path;Backup=$null;OriginalHash=$null;InstalledHash=(Hash $path);Restored=$false});SaveState
}
Write-Output ('安装完成。固定目录：'+$root)
Write-Output ('已创建桌面和开始菜单入口，更新 Codex 动画入口 '+$rewritten+' 个。')
Write-Output '视频和设置予以保留。首次使用请打开「绸缪」导入 MP4。'
