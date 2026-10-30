using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

static class SetupProgram {
 const string InstallRoot=@"Y:\ARTWORK SUPREME\绸缪";
 static bool IsUnder(string file,string root){return Path.GetFullPath(file).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
 static int Extract(string destination){
  Directory.CreateDirectory(destination);int count=0;
  using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
  using(ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Read))foreach(ZipArchiveEntry entry in archive.Entries){
   if(String.IsNullOrEmpty(entry.Name))continue;
   string file=Path.GetFullPath(Path.Combine(destination,entry.FullName));if(!IsUnder(file,destination))throw new IOException("安装包路径无效。");
   if(entry.FullName.Split('/','\\').Any(part=>part.Equals("media",StringComparison.OrdinalIgnoreCase)||part.Equals(".integration",StringComparison.OrdinalIgnoreCase))||entry.Name.Equals("settings.json",StringComparison.OrdinalIgnoreCase))throw new IOException("安装包包含不应覆盖的用户数据。");
   Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+".installing";
   using(Stream source=entry.Open())using(FileStream output=File.Create(temp))source.CopyTo(output);
   if(new FileInfo(temp).Length!=entry.Length)throw new IOException("安装文件校验失败。");
   if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);count++;
  }return count;
 }
 static string Install(){
  if(!Directory.Exists(@"Y:\"))throw new IOException("未找到 Y: 盘。请先连接该磁盘，再安装到固定目录。");
  foreach(Process p in Process.GetProcesses())using(p){try{if(p.Id==Process.GetCurrentProcess().Id)continue;if(p.ProcessName!="绸缪"&&p.ProcessName!="动画启动"&&p.ProcessName!="MP4 Codex Launcher"&&p.ProcessName!="开场白")continue;string path=p.MainModule.FileName;if(IsUnder(path,InstallRoot)){p.CloseMainWindow();if(!p.WaitForExit(5000))throw new IOException("请关闭绸缪的设置或播放窗口后重试。");}}catch(System.ComponentModel.Win32Exception){}}
  int count=Extract(InstallRoot);
  string ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
  var info=new ProcessStartInfo(ps,"-NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(InstallRoot,"安装.ps1")+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=InstallRoot};
  info.EnvironmentVariables["PSModulePath"]=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),@"WindowsPowerShell\Modules")+";"+Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"WindowsPowerShell\Modules")+";"+Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\Modules");
  using(Process process=Process.Start(info)){Task<string> output=process.StandardOutput.ReadToEndAsync(),error=process.StandardError.ReadToEndAsync();process.WaitForExit();if(process.ExitCode!=0)throw new IOException(error.Result);string details=output.Result;return "安装完成。\r\n"+InstallRoot+"\r\n桌面和开始菜单入口已更新，视频与设置已保留。";}
 }
 [STAThread] static int Main(string[] args){
  if(args.Length==2&&args[0]=="--verify"){try{int count=Extract(Path.GetFullPath(args[1]));Console.WriteLine("Verified payload files: "+count);return count>0?0:1;}catch(Exception e){File.WriteAllText(Path.Combine(Path.GetTempPath(),"choumou-setup-verify-error.txt"),e.ToString());return 1;}}
  if(args.Length==1&&args[0]=="--install-silent"){try{string message=Install();File.WriteAllText(Path.Combine(InstallRoot,@".integration\last-install.txt"),message,Encoding.UTF8);return 0;}catch(Exception e){File.WriteAllText(Path.Combine(Path.GetTempPath(),"choumou-install-error.txt"),e.ToString());return 1;}}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  Form form=new Form{Text="绸缪 · 一键安装",Width=540,Height=300,StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,BackColor=Color.FromArgb(245,245,242),Font=new Font("Microsoft YaHei UI",10)};
  form.Icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
  Label title=new Label{Text="绸缪",Font=new Font("Microsoft YaHei UI",22,FontStyle.Bold),Location=new Point(26,23),AutoSize=true};
  Label status=new Label{Text="正在安装到固定目录…\r\n"+InstallRoot,Location=new Point(29,85),Size=new Size(465,85)};
  ProgressBar progress=new ProgressBar{Location=new Point(30,178),Width=465,Height=6,Style=ProgressBarStyle.Marquee};
  Button close=new Button{Text="完成",Location=new Point(395,210),Width=100,Enabled=false};close.Click+=delegate{form.Close();};form.Controls.AddRange(new Control[]{title,status,progress,close});
  form.Shown+=async delegate{try{string message=await Task.Run(()=>Install());status.Text=message;progress.Style=ProgressBarStyle.Continuous;progress.Value=100;}catch(Exception e){status.Text="安装未完成："+e.Message;progress.Style=ProgressBarStyle.Continuous;}finally{close.Enabled=true;}};
  Application.Run(form);return 0;
 }
}
