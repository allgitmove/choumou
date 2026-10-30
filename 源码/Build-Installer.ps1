$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$payload=Join-Path $env:TEMP ('choumou-payload-'+[guid]::NewGuid().ToString('N')+'.zip')
$files=@('绸缪.exe','动画启动.exe','安装.ps1','入口管理.ps1','使用说明.md','AGENTS.md','源码','文档','.cursor')|ForEach-Object{Join-Path $root $_}
try{
 Compress-Archive -LiteralPath $files -DestinationPath $payload -Force
 $fw=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
 & (Join-Path $fw 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$PSScriptRoot/icon.ico" "/resource:$payload,payload.zip" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/out:$root/一键安装.exe" (Join-Path $PSScriptRoot 'Setup.cs')
 if($LASTEXITCODE -ne 0){throw '安装包编译失败。'}
 Write-Output (Join-Path $root '一键安装.exe')
}finally{if(Test-Path -LiteralPath $payload){Remove-Item -LiteralPath $payload}}
