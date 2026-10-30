using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("绸缪")]
[assembly: System.Reflection.AssemblyProduct("绸缪 · Choumou")]
[assembly: System.Reflection.AssemblyDescription("Personal cinematic launcher for Codex")]
namespace PersonalLauncher {
 public static class Branding {
  public static System.Windows.Media.Imaging.BitmapFrame LoadIcon(){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.png")){var image=System.Windows.Media.Imaging.BitmapFrame.Create(stream,System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);image.Freeze();return image;}}
 }
 public class Settings {
  public string Video="";
  public string Target="shell:AppsFolder\\OpenAI.Codex_2p2nqsd0c76g0!App";
  public double Volume=0.5;
  public bool Fullscreen=false;
 }
 public static class Store {
  public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
  public static readonly string Config=Path.Combine(Root,"settings.json");
  public static void Log(string message){try{File.AppendAllText(Path.Combine(Root,"launcher.log"),DateTime.Now.ToString("s")+" "+message+Environment.NewLine);}catch{}}
  public static Settings Read() {
   if(!File.Exists(Config))return new Settings();
   Settings s=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(Config,Encoding.UTF8));
   if(s==null)throw new Exception("设置文件为空。");
   if(Double.IsNaN(s.Volume)||Double.IsInfinity(s.Volume))s.Volume=0.5;
   s.Volume=Math.Max(0,Math.Min(1,s.Volume));return s;
  }
  public static void Save(Settings s) {
   string temp=Config+".tmp";
   File.WriteAllText(temp,new JavaScriptSerializer().Serialize(s),Encoding.UTF8);
   if(File.Exists(Config))File.Replace(temp,Config,Config+".bak");else File.Move(temp,Config);
  }
  public static string VideoPath(Settings s) {
   return String.IsNullOrWhiteSpace(s.Video)?"":Path.GetFullPath(Path.Combine(Root,s.Video));
  }
  public static void Import(Settings s,string source) {
   if(!String.Equals(Path.GetExtension(source),".mp4",StringComparison.OrdinalIgnoreCase))throw new Exception("请选择 MP4 文件。");
   if(!File.Exists(source)||new FileInfo(source).Length==0)throw new Exception("视频不存在或文件为空。");
   string media=Path.Combine(Root,"media");Directory.CreateDirectory(media);
   string name=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".mp4";
   string destination=Path.Combine(media,name);
   File.Copy(source,destination,false);
   if(new FileInfo(source).Length!=new FileInfo(destination).Length)throw new Exception("视频复制不完整，请重新导入。");
   string previous=s.Video;s.Video=Path.Combine("media",name);
   try { Save(s); }catch { s.Video=previous;throw; }
  }
 }
 public static class TargetApp {
  [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int cmd);
  [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
  [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,int id);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h,uint flags,StringBuilder path,ref int count);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
  static string ProcessFile(Process p) {
   IntPtr h=OpenProcess(0x1000,false,p.Id);if(h==IntPtr.Zero)return "";
   try { StringBuilder b=new StringBuilder(4096);int n=b.Capacity;return QueryFullProcessImageName(h,0,b,ref n)?b.ToString():""; }finally{CloseHandle(h);}
  }
  public static void Validate(string target) {
   if(String.IsNullOrWhiteSpace(target))throw new Exception("请设置 Codex 启动入口。");
   if(target.StartsWith("shell:AppsFolder\\",StringComparison.OrdinalIgnoreCase)) {
    if(!System.Text.RegularExpressions.Regex.IsMatch(target,@"^shell:AppsFolder\\[A-Za-z0-9_.-]+![A-Za-z0-9_.-]+$"))throw new Exception("应用 ID 格式无效。");
   }else if(!Path.IsPathRooted(target)||!target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(target))throw new Exception("请选择现有的 EXE，或填写 shell:AppsFolder 应用 ID。");
  }
  public static IntPtr Find(string target) {
   string family="";bool packaged=target.StartsWith("shell:AppsFolder\\",StringComparison.OrdinalIgnoreCase);
   if(packaged)family=target.Substring("shell:AppsFolder\\".Length).Split('!')[0];
   foreach(Process p in Process.GetProcesses())using(p) {
    try { if(packaged&&p.ProcessName!="ChatGPT"&&p.ProcessName!="Codex")continue;if(p.MainWindowHandle==IntPtr.Zero)continue;string path=ProcessFile(p);
     if((packaged&&path.IndexOf("\\"+family.Split('_')[0]+"_",StringComparison.OrdinalIgnoreCase)>=0)||(!packaged&&String.Equals(path,target,StringComparison.OrdinalIgnoreCase)))return p.MainWindowHandle;
    }catch{}
   }return IntPtr.Zero;
  }
  public static void Launch(string target) {
   Validate(target);if(Find(target)!=IntPtr.Zero)return;
   if(target.StartsWith("shell:AppsFolder\\",StringComparison.OrdinalIgnoreCase))Process.Start(new ProcessStartInfo("explorer.exe",target){UseShellExecute=true});
   else Process.Start(new ProcessStartInfo(target){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(target)});
  }
  public static bool Focus(string target) {
   IntPtr h=Find(target);if(h==IntPtr.Zero)return false;if(IsIconic(h))ShowWindow(h,9);return SetForegroundWindow(h);
  }
 }
 public static class Handoff {
  [StructLayout(LayoutKind.Sequential)] public struct Box {public int Left,Top,Right,Bottom;public int Width{get{return Right-Left;}}public int Height{get{return Bottom-Top;}}}
  [StructLayout(LayoutKind.Sequential)] public struct ThumbnailProperties {public uint Flags;public Box Destination,Source;public byte Opacity;[MarshalAs(UnmanagedType.Bool)] public bool Visible;[MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;}
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Box rect);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h,out Box rect);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
  [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(IntPtr destination,IntPtr source,out IntPtr thumbnail);
  [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail,ref ThumbnailProperties properties);
  [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
  public static double Accelerate(double progress){progress=Math.Max(0,Math.Min(1,progress));return progress*progress*progress;}
  public static Box Interpolate(Box from,Box to,double progress){double t=Accelerate(progress);return new Box{Left=(int)Math.Round(from.Left+(to.Left-from.Left)*t),Top=(int)Math.Round(from.Top+(to.Top-from.Top)*t),Right=(int)Math.Round(from.Right+(to.Right-from.Right)*t),Bottom=(int)Math.Round(from.Bottom+(to.Bottom-from.Bottom)*t)};}
 }
 public static class Theme {
  public static Brush Bg=new SolidColorBrush(Color.FromRgb(15,18,28));
  public static Brush Panel=new SolidColorBrush(Color.FromRgb(25,29,43));
  public static Brush Muted=new SolidColorBrush(Color.FromRgb(155,166,188));
  public static TextBlock Text(string text,double size,Brush color) { return new TextBlock{Text=text,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)}; }
  public static Button Button(string label,bool primary) {
   Button b=new Button{Content=label,Padding=new Thickness(18,10,18,10),Margin=new Thickness(0,0,10,0),Cursor=System.Windows.Input.Cursors.Hand,FontSize=14,Foreground=Brushes.White,Background=primary?new SolidColorBrush(Color.FromRgb(111,85,235)):new SolidColorBrush(Color.FromRgb(47,54,74)),BorderThickness=new Thickness(0),MinHeight=42};
   var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(9));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.MarginProperty,new TemplateBindingExtension(Control.PaddingProperty));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);template.VisualTree=border;b.Template=template;return b;
  }
 }
 public sealed class Player:Window {
  readonly Settings settings;readonly bool preview;readonly MediaElement media;readonly Action<string> onDone;
  readonly DispatcherTimer timer=new DispatcherTimer();readonly Stopwatch clock=new Stopwatch();bool finishing,opened,ended;double endedAt;string result="";
  readonly DispatcherTimer zoomTimer=new DispatcherTimer(DispatcherPriority.Render);IntPtr thumbnail;
  public Player(Settings s,bool onlyPreview,Action<string> callback) {
   settings=s;preview=onlyPreview;onDone=callback;Icon=Branding.LoadIcon();
   Title=preview?"视频预览 · 绸缪":"启动动画 · 绸缪";
   Width=Math.Min(1120,SystemParameters.WorkArea.Width);Height=Math.Min(670,SystemParameters.WorkArea.Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Brushes.Black;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;Topmost=!preview;
   if(s.Fullscreen)WindowState=WindowState.Maximized;
   Grid g=new Grid{ClipToBounds=true,Background=Brushes.Black};media=new MediaElement{LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Manual,Stretch=Stretch.UniformToFill,Volume=s.Volume};g.Children.Add(media);Content=g;
   PreviewMouseLeftButtonDown+=delegate(object sender,System.Windows.Input.MouseButtonEventArgs e){if(e.ClickCount==2){e.Handled=true;Store.Log("double-click-skip");Finish("");}};
   PreviewKeyDown+=delegate(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Escape){e.Handled=true;Finish("");}};
   media.MediaOpened+=delegate{opened=true;clock.Restart();if(!s.Fullscreen&&media.NaturalVideoWidth>0&&media.NaturalVideoHeight>0){Size fit=FitVideo(media.NaturalVideoWidth,media.NaturalVideoHeight,Math.Min(1120,SystemParameters.WorkArea.Width),Math.Min(670,SystemParameters.WorkArea.Height));Width=fit.Width;Height=fit.Height;Left=SystemParameters.WorkArea.Left+(SystemParameters.WorkArea.Width-Width)/2;Top=SystemParameters.WorkArea.Top+(SystemParameters.WorkArea.Height-Height)/2;}Store.Log(preview?"preview-media-opened":"launch-media-opened");};
   media.MediaEnded+=delegate{Store.Log("media-ended");ended=true;endedAt=clock.Elapsed.TotalSeconds;if(preview||TargetApp.Find(s.Target)!=IntPtr.Zero)Finish("");};
   media.MediaFailed+=delegate(object sender,ExceptionRoutedEventArgs e){Finish("视频无法播放。建议使用 H.264 / AAC 编码的 MP4。\n"+e.ErrorException.Message);};
   timer.Interval=TimeSpan.FromMilliseconds(500);timer.Tick+=delegate{
    if(!opened&&clock.Elapsed.TotalSeconds>15)Finish("视频加载超时，请更换编码或重新导入。");
    if(ended&&!preview){if(TargetApp.Find(s.Target)!=IntPtr.Zero)Finish("");else if(clock.Elapsed.TotalSeconds-endedAt>20)Finish("没有找到 Codex 窗口。请检查启动入口；Codex 可能仍在启动。");}
   };
   Loaded+=delegate{
    if(!preview)try{TargetApp.Launch(s.Target);}catch(Exception ex){Finish("启动 Codex 失败："+ex.Message);return;}
    clock.Start();media.Source=new Uri(Store.VideoPath(s));media.Play();timer.Start();Activate();
   };
   Closed+=delegate{timer.Stop();zoomTimer.Stop();if(thumbnail!=IntPtr.Zero){Handoff.DwmUnregisterThumbnail(thumbnail);thumbnail=IntPtr.Zero;}media.Stop();media.Close();if(!preview)Store.Log("target-focus="+TargetApp.Focus(s.Target));onDone(result);};
  }
  public static Size FitVideo(double width,double height,double maxWidth,double maxHeight){double scale=Math.Min(maxWidth/width,maxHeight/height);return new Size(width*scale,height*scale);}
  void Finish(string message) {
   if(finishing)return;finishing=true;result=message;timer.Stop();Store.Log("player-closing; ended="+ended+"; error="+(!String.IsNullOrEmpty(message)));
   media.Volume=0;media.Pause();
   if(!preview&&String.IsNullOrEmpty(message)&&StartZoomHandoff())return;
   DoubleAnimation fade=new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(200));fade.Completed+=delegate{Close();};BeginAnimation(OpacityProperty,fade);
  }
  bool StartZoomHandoff(){
   IntPtr destination=TargetApp.Find(settings.Target),host=new System.Windows.Interop.WindowInteropHelper(this).Handle;Handoff.Box start,end;
   if(destination==IntPtr.Zero||host==IntPtr.Zero||!Handoff.GetWindowRect(host,out start))return false;
   Topmost=true;TargetApp.Focus(settings.Target);
   if(!Handoff.GetWindowRect(destination,out end)||end.Width<100||end.Height<100)return false;
   // Work in physical screen pixels, including negative coordinates on another monitor.
   // Restore the player before animating, so fullscreen does not constrain its size.
   if(WindowState!=WindowState.Normal)WindowState=WindowState.Normal;
   Handoff.SetWindowPos(host,new IntPtr(-1),start.Left,start.Top,start.Width,start.Height,0x10);
   bool live=Handoff.DwmRegisterThumbnail(host,destination,out thumbnail)==0&&thumbnail!=IntPtr.Zero;
   Store.Log("zoom-handoff-start; duration=720ms; easing=cubic-in; live="+live+"; from="+start.Width+"x"+start.Height+"; to="+end.Width+"x"+end.Height);
   Stopwatch transition=Stopwatch.StartNew();zoomTimer.Interval=TimeSpan.FromMilliseconds(16);
   zoomTimer.Tick+=delegate{
    double progress=Math.Min(1,transition.Elapsed.TotalMilliseconds/720.0);Handoff.Box current=Handoff.Interpolate(start,end,progress);
    if(!Handoff.SetWindowPos(host,new IntPtr(-1),current.Left,current.Top,current.Width,current.Height,0x10)){zoomTimer.Stop();Close();return;}
    double blend=Math.Max(0,Math.Min(1,(progress-.22)/.78));blend=blend*blend*(3-2*blend);
    if(live){Handoff.Box client;Handoff.GetClientRect(host,out client);var properties=new Handoff.ThumbnailProperties{Flags=1|4|8|16,Destination=client,Opacity=(byte)Math.Round(255*blend),Visible=true,ClientOnly=false};
     if(Handoff.DwmUpdateThumbnailProperties(thumbnail,ref properties)!=0){Handoff.DwmUnregisterThumbnail(thumbnail);thumbnail=IntPtr.Zero;live=false;Store.Log("zoom-live-unavailable; using-opacity-fallback");}
    }
    if(!live)Opacity=1-blend;
    if(progress>=1){zoomTimer.Stop();Store.Log("zoom-handoff-completed; live="+live+"; elapsed-ms="+transition.ElapsedMilliseconds);Close();}
   };
   zoomTimer.Start();return true;
  }
 }
 public sealed class Dashboard:Window {
  Settings settings;TextBlock videoName,notice;TextBox target;Slider volume;CheckBox fullscreen;bool busy;
  public Dashboard(Settings s,string initialError) {
   settings=s;Title="绸缪 · Choumou";Width=1000;Height=790;MinWidth=850;MinHeight=700;
   WindowStartupLocation=WindowStartupLocation.CenterScreen;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.CanResize;
   FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");UseLayoutRounding=true;SnapsToDevicePixels=true;
   var chrome=new System.Windows.Shell.WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(7),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(16)};
   System.Windows.Shell.WindowChrome.SetWindowChrome(this,chrome);
   Border frame;
   using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Dashboard.xaml"))frame=(Border)System.Windows.Markup.XamlReader.Load(stream);
   Content=frame;Icon=Branding.LoadIcon();((Image)frame.FindName("BrandIcon")).Source=Icon;
   videoName=(TextBlock)frame.FindName("VideoName");notice=(TextBlock)frame.FindName("Notice");target=(TextBox)frame.FindName("Target");volume=(Slider)frame.FindName("Volume");fullscreen=(CheckBox)frame.FindName("Fullscreen");
   target.Text=s.Target;volume.Value=s.Volume*100;fullscreen.IsChecked=s.Fullscreen;
   TextBlock volumeLabel=(TextBlock)frame.FindName("VolumeLabel");volumeLabel.Text=((int)volume.Value)+"%";volume.ValueChanged+=delegate{volumeLabel.Text=((int)volume.Value)+"%";};
   ((Button)frame.FindName("CloseButton")).Click+=delegate{Close();};
   ((Button)frame.FindName("MinButton")).Click+=delegate{WindowState=WindowState.Minimized;};
   Action toggle=delegate{WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;};
   ((Button)frame.FindName("MaxButton")).Click+=delegate{toggle();};
   StateChanged+=delegate{bool max=WindowState==WindowState.Maximized;frame.Margin=new Thickness(max?0:12);frame.CornerRadius=new CornerRadius(max?0:16);};
   System.Windows.Input.MouseButtonEventHandler drag=delegate(object sender,System.Windows.Input.MouseButtonEventArgs e){if(e.ChangedButton!=System.Windows.Input.MouseButton.Left)return;if(e.ClickCount==2){toggle();return;}if(e.LeftButton==System.Windows.Input.MouseButtonState.Pressed)DragMove();};
   ((Grid)frame.FindName("Header")).MouseLeftButtonDown+=drag;((Grid)frame.FindName("SideHeader")).MouseLeftButtonDown+=drag;
   ((Button)frame.FindName("ImportButton")).Click+=delegate{var d=new OpenFileDialog{Title="选择你的启动动画",Filter="MP4 视频|*.mp4",CheckFileExists=true};if(d.ShowDialog(this)==true)Import(d.FileName);};
   ((Button)frame.FindName("PreviewButton")).Click+=delegate{Play(true);};
   ((Button)frame.FindName("BrowseButton")).Click+=delegate{var d=new OpenFileDialog{Filter="应用程序|*.exe",Title="选择要启动的 Codex 程序"};if(d.ShowDialog(this)==true)target.Text=d.FileName;};
   ((Button)frame.FindName("StartButton")).Click+=delegate{Play(false);};
   ((Button)frame.FindName("SaveButton")).Click+=delegate{try{Save();Message("设置已保存。");}catch(Exception ex){Message(ex.Message);}};
   ((Button)frame.FindName("IntegrateButton")).Click+=delegate{ManageEntrypoint(false);};
   ((Button)frame.FindName("RestoreButton")).Click+=delegate{ManageEntrypoint(true);};
   notice.Text=initialError??"导入影片后，双击「动画启动.exe」即可一键开启。";
   AllowDrop=true;DragOver+=delegate(object sender,DragEventArgs e){e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
   Drop+=delegate(object sender,DragEventArgs e){string[] files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null&&files.Length==1)Import(files[0]);else Message("请一次拖入一个 MP4。");};Refresh();
  }
  async void ManageEntrypoint(bool restore){if(busy)return;try{Save();busy=true;Message(restore?"正在恢复原来的启动图标…":"正在为 Codex 图标接入动画…");string script=Path.Combine(Store.Root,"入口管理.ps1");if(!File.Exists(script))throw new Exception("缺少入口管理.ps1，请保持整个文件夹完整。");string ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell/v1.0/powershell.exe");var info=new ProcessStartInfo(ps,"-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -Action "+(restore?"Restore":"Install")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};using(Process p=Process.Start(info)){var output=p.StandardOutput.ReadToEndAsync();var errors=p.StandardError.ReadToEndAsync();await System.Threading.Tasks.Task.Run(()=>p.WaitForExit());string text=await output;string err=await errors;if(p.ExitCode!=0)throw new Exception(err);Message(text.Trim());}}catch(Exception ex){Message("入口操作未完成："+ex.Message);}finally{busy=false;}}
  void Message(string m){notice.Text=m;}
  void Refresh(){string p=Store.VideoPath(settings);bool exists=File.Exists(p);videoName.Text=exists?Path.GetFileName(p):"把喜欢的画面，放在开场。";var frame=Content as Border;if(frame!=null){var meta=(TextBlock)frame.FindName("VideoMeta");meta.Text=exists?"MP4  ·  "+(new FileInfo(p).Length/1048576.0).ToString("0.0")+" MB":"尚未添加影片";}}
  void Save(){TargetApp.Validate(target.Text.Trim());settings.Target=target.Text.Trim();settings.Volume=volume.Value/100;settings.Fullscreen=fullscreen.IsChecked==true;Store.Save(settings);}
  void Import(string file){if(busy)return;try{Cursor=System.Windows.Input.Cursors.Wait;Store.Import(settings,file);Refresh();Message("导入成功。先点「预览」确认视频和声音。");}catch(Exception ex){Message("导入失败："+ex.Message);}finally{Cursor=null;}}
  void Play(bool preview){if(busy)return;try{Save();if(!File.Exists(Store.VideoPath(settings)))throw new Exception("请先导入一个 MP4 视频。");busy=true;Hide();new Player(settings,preview,delegate(string result){busy=false;Show();Message(String.IsNullOrEmpty(result)?(preview?"预览结束。":"动画播放完毕，已尝试切换到 Codex。"):result);if(!preview&&String.IsNullOrEmpty(result)){WindowState=WindowState.Minimized;TargetApp.Focus(settings.Target);}else Activate();}).Show();}catch(Exception ex){busy=false;Show();Message(ex.Message);}}
 }
 public static class Program {
  [STAThread] public static int Main(string[] args){
   if(args.Contains("--self-test"))return SelfTest();
   Settings s;string error=null;try{s=Store.Read();}catch(Exception ex){s=new Settings();error="设置读取失败，已使用默认值："+ex.Message;}
   Application app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   app.DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e){e.Handled=true;MessageBox.Show("发生错误："+e.Exception.Message,"MP4 Codex Launcher");app.Shutdown(1);};
   bool direct=args.Contains("--launch")||String.Equals(Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location),"动画启动",StringComparison.OrdinalIgnoreCase);
   if(direct&&File.Exists(Store.VideoPath(s))&&error==null){
    new Player(s,false,delegate(string result){if(!String.IsNullOrEmpty(result)){var d=new Dashboard(s,result);d.Closed+=delegate{app.Shutdown();};d.Show();}else app.Shutdown();}).Show();
   }else{var d=new Dashboard(s,error);d.Closed+=delegate{app.Shutdown();};d.Show();}return app.Run();
  }
  static int SelfTest(){try{
   TargetApp.Validate("shell:AppsFolder\\OpenAI.Codex_2p2nqsd0c76g0!App");
   foreach(string bad in new[]{"","https://example.com","cmd.exe /c anything","shell:AppsFolder\\bad!App & calc"}){bool rejected=false;try{TargetApp.Validate(bad);}catch{rejected=true;}if(!rejected)throw new Exception("Unsafe launch target accepted");}
   Settings s=new Settings();string json=new JavaScriptSerializer().Serialize(s);Settings round=new JavaScriptSerializer().Deserialize<Settings>(json);if(round.Volume!=0.5||round.Fullscreen||round.Target!=s.Target)throw new Exception("Settings roundtrip failed");
   foreach(Size video in new[]{new Size(1920,1080),new Size(1080,1920),new Size(2560,1080)}){Size fit=Player.FitVideo(video.Width,video.Height,1120,670);if(Math.Abs(fit.Width/fit.Height-video.Width/video.Height)>0.0001||fit.Width>1120.01||fit.Height>670.01)throw new Exception("Video aspect ratio test failed");}
   if(Handoff.Accelerate(0)!=0||Handoff.Accelerate(1)!=1||Handoff.Accelerate(.9)-Handoff.Accelerate(.8)<=Handoff.Accelerate(.2)-Handoff.Accelerate(.1))throw new Exception("Accelerating motion test failed");
   var from=new Handoff.Box{Left=-1920,Top=100,Right=-800,Bottom=730};var to=new Handoff.Box{Left=200,Top=20,Right=1600,Bottom=1000};var first=Handoff.Interpolate(from,to,0);var last=Handoff.Interpolate(from,to,1);if(first.Left!=from.Left||first.Right!=from.Right||last.Left!=to.Left||last.Top!=to.Top||last.Right!=to.Right||last.Bottom!=to.Bottom)throw new Exception("Zoom geometry endpoint test failed");
   File.WriteAllText(Path.Combine(Store.Root,"self-test-result.txt"),"PASS: settings serialization; default app ID; invalid launch target rejection. No video playback or target app launched.");return 0;
  }catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"self-test-result.txt"),ex.ToString());return 1;}}
 }
}
