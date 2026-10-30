[CmdletBinding()]
param([ValidateSet('Install','Restore')][string]$Action='Install',[string]$ShortcutPath)
$ErrorActionPreference='Stop'
$appRoot=$PSScriptRoot
$stateRoot=Join-Path $appRoot '.integration'
$stateFile=Join-Path $stateRoot 'state.json'
$bootExe=Join-Path $appRoot '动画启动.exe'
$ws=New-Object -ComObject WScript.Shell
function HashFile([string]$p){$sha=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($p);try{[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}}
function WriteState($value){$tmp=$stateFile+'.tmp';$value | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $tmp -Encoding UTF8;Move-Item -LiteralPath $tmp -Destination $stateFile -Force}
if($Action -eq 'Restore'){
 if(!(Test-Path -LiteralPath $stateFile)){Write-Output '还没有接入过启动图标，无需恢复。';exit 0}
 $state=Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8 | ConvertFrom-Json
 $restored=0;$preserved=0
 foreach($entry in $state.Entries){
  if($entry.Restored){continue}
  if(!(Test-Path -LiteralPath $entry.Path) -or (HashFile $entry.Path) -ne $entry.InstalledHash){$preserved++;continue}
  if($entry.Backup){
   $backup=[IO.Path]::GetFullPath($entry.Backup);$prefix=[IO.Path]::GetFullPath($stateRoot)+[IO.Path]::DirectorySeparatorChar
   if(!$backup.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or (HashFile $backup) -ne $entry.OriginalHash){throw '备份校验失败，未覆盖快捷方式。'}
   Copy-Item -LiteralPath $backup -Destination $entry.Path -Force
  }else{Remove-Item -LiteralPath $entry.Path}
  $entry.Restored=$true;$restored++;WriteState $state
 }
 Write-Output ('已恢复 {0} 个图标；另有 {1} 个被移动或修改的图标予以保留。' -f $restored,$preserved);exit 0
}
if(!(Test-Path -LiteralPath $bootExe)){throw '缺少动画启动.exe。'}
$config=Get-Content -LiteralPath (Join-Path $appRoot 'settings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$video=Join-Path $appRoot $config.Video
if(!(Test-Path -LiteralPath $video -PathType Leaf)){throw '请先导入视频并保存设置，再接入图标。'}
New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null
$state=if(Test-Path -LiteralPath $stateFile){Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8 | ConvertFrom-Json}else{[pscustomobject]@{Entries=@()}}
$targets=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
if($config.Target -like 'shell:AppsFolder\*'){
 $appId=$config.Target.Substring('shell:AppsFolder\'.Length);$family,$application=$appId.Split('!')
 try{$packages=@(Get-AppxPackage -ErrorAction Stop | Where-Object {$_.PackageFamilyName -eq $family})}catch{$packages=@()}
 foreach($pkg in $packages){
  $manifest=Get-AppxPackageManifest -Package $pkg.PackageFullName
  foreach($app in @($manifest.Package.Applications.Application | Where-Object {$_.Id -eq $application})){if($app.Executable){$null=$targets.Add((Join-Path $pkg.InstallLocation $app.Executable))}}
 }
}else{$null=$targets.Add($config.Target)}
function MatchesTarget([string]$path,$link){
 if($targets.Contains($link.TargetPath)){return $true}
 if($link.TargetPath -ieq $config.Target){return $true}
 if($link.TargetPath -ieq (Join-Path $env:WINDIR 'explorer.exe') -and $link.Arguments.Trim('"') -ieq $config.Target){return $true}
 if(!$link.TargetPath -and $appId){$sh=New-Object -ComObject Shell.Application;$folder=$sh.NameSpace([IO.Path]::GetDirectoryName($path));return $folder.ParseName([IO.Path]::GetFileName($path)).ExtendedProperty('System.AppUserModel.ID') -ieq $appId}
 return $false
}
$candidates=@()
if($ShortcutPath){$candidates=@([IO.Path]::GetFullPath($ShortcutPath))}else{
 foreach($root in @([Environment]::GetFolderPath('DesktopDirectory'),[Environment]::GetFolderPath('Programs'))){if(Test-Path -LiteralPath $root){$candidates+=@(Get-ChildItem -LiteralPath $root -File -Filter '*.lnk' -Recurse | Select-Object -ExpandProperty FullName)}}
}
$changed=0
foreach($path in $candidates){
 $owned=@($state.Entries | Where-Object {$_.Path -ieq $path -and !$_.Restored})
 if($owned.Count){continue}
 if(!(Test-Path -LiteralPath $path)){continue}
 if(([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0){continue}
 $old=$ws.CreateShortcut($path);if(!(MatchesTarget $path $old)){continue}
 $before=HashFile $path;$backup=Join-Path $stateRoot ([guid]::NewGuid().ToString('N')+'.lnk');Copy-Item -LiteralPath $path -Destination $backup
 if((HashFile $backup) -ne $before){throw '备份校验失败。'}
 $temp=Join-Path ([IO.Path]::GetDirectoryName($path)) ([guid]::NewGuid().ToString('N')+'.lnk')
 try{
  $link=$ws.CreateShortcut($temp);$link.TargetPath=$bootExe;$link.WorkingDirectory=$appRoot;$link.IconLocation=$old.IconLocation;$link.Hotkey=$old.Hotkey;$link.WindowStyle=$old.WindowStyle;$link.Description='播放自定义动画，然后进入 Codex';$link.Save()
  $record=[pscustomobject]@{Path=$path;Backup=$backup;OriginalHash=$before;InstalledHash=(HashFile $temp);Restored=$false}
  if((HashFile $path) -ne $before){throw '图标已被其他程序修改，操作取消。'}
  $state.Entries+=@($record);WriteState $state;Move-Item -LiteralPath $temp -Destination $path -Force
  if((HashFile $path) -ne $record.InstalledHash){throw '图标写入校验失败。'};$changed++
 }finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp}}
}
$active=@($state.Entries | Where-Object {!$_.Restored}).Count
if($active -eq 0){throw '没有找到匹配的桌面或开始菜单快捷方式。请先为 Codex 创建桌面快捷方式，再接入。'}
Write-Output ('已接入 {0} 个新图标（共 {1} 个）。以后点击这些 Codex 图标即可播放。删除启动器前请先恢复图标。' -f $changed,$active)
