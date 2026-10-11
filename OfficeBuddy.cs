using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Windows.Forms;

namespace OfficeBuddy {
    [DataContract] public class Day {
        [DataMember] public string Date;
        [DataMember] public long[] Buttons = new long[5];
        [DataMember] public int Earned;
        public long Total { get { return Buttons.Sum(); } }
    }
    [DataContract] public class SaveData {
        [DataMember] public int Version = 1;
        [DataMember] public long[] Lifetime = new long[5];
        [DataMember] public List<Day> Days = new List<Day>();
        [DataMember] public int Coins;
        [DataMember] public List<string> Owned = new List<string>();
        [DataMember] public List<string> Equipped;
        [DataMember] public string Theme = "pixel";
        [DataMember] public int Zoom = 100;
        [DataMember] public int PetScale = 55;
        [DataMember] public UserSettings Settings = new UserSettings();
        [DataMember] public bool HasPosition;
        [DataMember] public int PetX,PetY;
        [DataMember] public string LastOffworkDay="";
    }
    public class Item {
        public string Id, Name, Description; public int Cost;
        public Item(string id, string name, string desc, int cost) { Id=id; Name=name; Description=desc; Cost=cost; }
    }
    public class Model {
        public static readonly Item[] Items = {
            new Item("cup", "专属马克杯", "忙里偷闲，喝一口", 2),
            new Item("plant", "小小绿植", "工位也有小森林", 4),
            new Item("lamp", "暖光台灯", "给日常一点温度", 6),
            new Item("clock", "下班时钟", "快乐倒数的仪式感", 8),
            new Item("cushion", "软软坐垫", "坐得舒服，心也软软", 10)
        };
        public SaveData Data; public bool Paused;
        public Model(SaveData data) { Data=data; }
        public Day Today(DateTime now) {
            string key=now.ToString("yyyy-MM-dd");
            Day day=Data.Days.Find(d => d.Date==key);
            if(day==null) { day=new Day { Date=key }; Data.Days.Add(day); }
            return day;
        }
        public int Click(int button, DateTime now,bool own=false) {
            if(Paused || button<0 || button>4 || (own&&Data.Settings!=null&&!Data.Settings.CountOwnClicks)) return 0;
            Day d=Today(now); d.Buttons[button]++; Data.Lifetime[button]++;
            int target=(int)Math.Min(20L,d.Total/100);
            int gained=Math.Max(0,target-d.Earned); d.Earned+=gained; Data.Coins+=gained;
            return gained;
        }
        public bool Buy(Item item) {
            if(Data.Owned.Contains(item.Id) || Data.Coins<item.Cost) return false;
            Data.Coins-=item.Cost; Data.Owned.Add(item.Id);
            if(Data.Equipped!=null)Data.Equipped.Add(item.Id);
            return true;
        }
        public bool IsPlaced(string id){return (Data.Equipped??Data.Owned).Contains(id);}
        public void TogglePlacement(string id) {
            if(!Data.Owned.Contains(id))return;
            if(Data.Equipped==null)Data.Equipped=new List<string>(Data.Owned);
            if(Data.Equipped.Contains(id))Data.Equipped.Remove(id);else Data.Equipped.Add(id);
        }
        public static IEnumerable<int> Buttons(ushort flags) {
            ushort[] down={1,4,16,64,256};
            for(int i=0;i<down.Length;i++) if((flags&down[i])!=0) yield return i;
        }
    }
    public class Store {
        public string Path; public string Notice="";
        public Store(string path) { Path=path; }
        public static SaveData Read(string path) {
            using(FileStream s=File.OpenRead(path)) {
                var data=(SaveData)new DataContractJsonSerializer(typeof(SaveData)).ReadObject(s);
                if(data==null || data.Version!=1 || data.Lifetime==null || data.Lifetime.Length!=5 ||
                   data.Lifetime.Any(x=>x<0) || data.Days==null || data.Owned==null || data.Coins<0 ||
                   data.Days.Any(d=>d==null || !ValidDate(d.Date) || d.Buttons==null || d.Buttons.Length!=5 ||
                       d.Buttons.Any(x=>x<0) || d.Earned<0 || d.Earned>20) ||
                   data.Days.Select(d=>d.Date).Distinct().Count()!=data.Days.Count)
                    throw new InvalidDataException("存档结构不完整。");
                if(!BuddyForm.Themes.Contains(data.Theme))data.Theme="pixel";
                if(data.Zoom!=120&&data.Zoom!=140)data.Zoom=100;
                if(data.PetScale!=40&&data.PetScale!=70)data.PetScale=55;
                data.Equipped=(data.Equipped??data.Owned).Intersect(data.Owned).Distinct().ToList();
                data.Settings=data.Settings??new UserSettings();data.Settings.Normalize();
                return data;
            }
        }
        static bool ValidDate(string value){DateTime date;return DateTime.TryParseExact(value,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out date);}
        public SaveData Load() {
            if(!File.Exists(Path)) return new SaveData();
            try { return Read(Path); }
            catch(Exception) {
                string damaged=Path+".damaged-"+DateTime.Now.ToString("yyyyMMddHHmmssfff");
                File.Copy(Path,damaged,true);
                if(File.Exists(Path+".bak")) {
                    try { var backup=Read(Path+".bak"); Notice="存档已从备份恢复；损坏文件已保留。"; return backup; }
                    catch(Exception) { }
                }
                Notice="旧存档无法读取，已保留损坏文件，开始新的记录。";
                return new SaveData();
            }
        }
        public void Save(SaveData data) {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string temp=Path+".tmp";
            using(FileStream s=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
                new DataContractJsonSerializer(typeof(SaveData)).WriteObject(s,data); s.Flush(true);
            }
            if(File.Exists(Path)) File.Replace(temp,Path,Path+".bak"); else File.Move(temp,Path);
        }
    }
    static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct Device { public ushort Page,Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)] public struct Header { public uint Type,Size; public IntPtr Device,WParam; }
        [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterRawInputDevices(Device[] devices,uint count,uint size);
        [DllImport("user32.dll",SetLastError=true)] public static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint headerSize);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wparam,IntPtr lparam);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll",EntryPoint="GetWindowLong")] public static extern int GetStyle(IntPtr hwnd,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLong")] public static extern int SetStyle(IntPtr hwnd,int index,int value);
        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; public Point(int x,int y){X=x;Y=y;} }
        [StructLayout(LayoutKind.Sequential)] public struct Size { public int Width,Height; public Size(int w,int h){Width=w;Height=h;} }
        [StructLayout(LayoutKind.Sequential,Pack=1)] public struct Blend {public byte Op,Flags,Alpha,Format;}
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr dst,ref Point location,ref Size size,IntPtr src,ref Point source,uint key,ref Blend blend,uint flags);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point p);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern uint RegisterWindowMessage(string text);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp);
        public static readonly uint ThemeMessage=RegisterWindowMessage("OfficeBuddy.Theme.v03");
        public delegate bool WindowCallback(IntPtr hwnd,IntPtr parameter);
        [DllImport("user32.dll")] public static extern bool EnumWindows(WindowCallback callback,IntPtr parameter);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd,System.Text.StringBuilder title,int count);
        public static bool DispatchTheme(int choice) {
            int mine=System.Diagnostics.Process.GetCurrentProcess().Id;
            foreach(var process in System.Diagnostics.Process.GetProcessesByName("OfficeBuddyStudio")) {
                if(process.Id==mine){process.Dispose();continue;}
                uint target=(uint)process.Id;process.Dispose();IntPtr window=IntPtr.Zero;
                EnumWindows(delegate(IntPtr hwnd,IntPtr parameter){uint pid;GetWindowThreadProcessId(hwnd,out pid);if(pid==target){var text=new System.Text.StringBuilder(256);GetWindowText(hwnd,text,256);if(text.ToString().StartsWith("上班搭子")){window=hwnd;return false;}}return true;},IntPtr.Zero);
                if(window!=IntPtr.Zero && PostMessage(window,ThemeMessage,new IntPtr(choice),IntPtr.Zero))return true;
            }return false;
        }
        public static void EnableDpi() {try{if(SetProcessDpiAwarenessContext(new IntPtr(-4)))return;}catch(EntryPointNotFoundException){}SetProcessDPIAware();}
        public static float Dpi(IntPtr hwnd) {try {return Math.Max(96,GetDpiForWindow(hwnd))/96f;}catch(EntryPointNotFoundException){return 1;}}
    }
    public partial class BuddyForm : Form {
        readonly Model model; readonly Store store; readonly bool renderOnly;
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly List<Tuple<RectangleF,Action>> hits=new List<Tuple<RectangleF,Action>>();
        NotifyIcon tray; ContextMenuStrip trayMenu;
        bool compact=true,through,dirty,rawOk,dragging,layerOk,exitRequested,notifications=true; string mode="work", bubble="来了？今天也一起混个脸熟。", saveError="",layerError="";
        Point dragOrigin,windowOrigin;
        double previewPhase; int renders; bool previewQuiet,located,panelHovered,previewPointerDriven;
        DateTime lastInput=DateTime.UtcNow, bubbleUntil=DateTime.MinValue, lastSave=DateTime.UtcNow;
        double anim,leftTap,rightTap; PointF previewGaze; bool? previewBlink; int lastButton; string dayKey="";
        float scale=1; PointF hover=new PointF(-1,-1);
        Color ink=ColorTranslator.FromHtml("#293C37"), muted=ColorTranslator.FromHtml("#78867C"),
            green=ColorTranslator.FromHtml("#407561"), cream=ColorTranslator.FromHtml("#F6F4EC"), orange=ColorTranslator.FromHtml("#DA8D50");
        string activeTab="overview";
        static readonly Bitmap catSprite=LoadSprite("cat"),waterSprite=LoadSprite("water"),rigSprite=LoadSprite("rig"),rabbitSprite=LoadSprite("rabbit"),nightSprite=LoadSprite("night"),rabbitRig=LoadSprite("rabbit-rig"),nightRig=LoadSprite("night-rig");
        SpriteMotion rabbitMotion,nightMotion;
        static Bitmap LoadSprite(string name){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("OfficeBuddy."+name+".png")){if(stream==null)return null;using(var original=new Bitmap(stream))return new Bitmap(original);}}
        public static bool ArtworkReady {get{return catSprite!=null&&waterSprite!=null&&rigSprite!=null&&rabbitSprite!=null&&nightSprite!=null&&rabbitRig!=null&&nightRig!=null&&rabbitSprite.Size==rabbitRig.Size&&nightSprite.Size==nightRig.Size&&rabbitSprite.GetPixel(0,0).A==0&&nightSprite.GetPixel(0,0).A==0&&rigSprite.GetPixel(0,0).A==0&&waterSprite.GetPixel(0,0).A==0;}}
        string Skin {get{return Themes.Contains(model.Data.Theme)?model.Data.Theme:"pixel";}}
        string SkinName {get{return ThemeNames[Array.IndexOf(Themes,Skin)];}}
        Color Surface {get{return Skin=="midnight"?C("#1F2930"):Skin=="cream"||Skin=="nailong"?C("#FFF9F0"):C("#FCFCF9");}}
        Color PrimaryText {get{return Skin=="midnight"?C("#F2F5F3"):C("#303A35");}}
        Color Accent {get{return Skin=="nailong"?C("#976117"):Skin=="pixel"?C("#4D7160"):Skin=="cream"?C("#995B40"):C("#8ED7BC");}}
        static Color C(string hex){return ColorTranslator.FromHtml(hex);}
        public BuddyForm(Model m,Store s,bool preview) {
            model=m; store=s; renderOnly=preview;InitializeFeatures();ApplyPalette();if(model.Data.Zoom!=120&&model.Data.Zoom!=140)model.Data.Zoom=100;
            Text="上班搭子 · 桌面宠物";
            AutoScaleMode=AutoScaleMode.None; FormBorderStyle=FormBorderStyle.None; MaximizeBox=false;
            ClientSize=new Size(320,360);TopMost=true;ShowInTaskbar=false;
            StartPosition=FormStartPosition.Manual;Rectangle area=Screen.PrimaryScreen.WorkingArea;
            Location=new Point(area.Right-ClientSize.Width-30,area.Bottom-ClientSize.Height-16); BackColor=cream;
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
            Icon=MakeIcon();
            MouseMove+=(o,e)=>{if(dragging){Point p=Cursor.Position;justDragged|=Math.Abs(p.X-dragOrigin.X)+Math.Abs(p.Y-dragOrigin.Y)>4;Location=PetPlacement.Constrain(new Point(windowOrigin.X+p.X-dragOrigin.X,windowOrigin.Y+p.Y-dragOrigin.Y),ClientSize,WorkingAreas);}bool changed=compact?UpdatePointerHover():false;if(!compact)hover=new PointF(e.X/scale,e.Y/scale);Cursor=hits.Any(h=>h.Item1.Contains(hover))?Cursors.Hand:compact&&!Preferences.PositionLocked?Cursors.SizeAll:Cursors.Default;if(changed)RenderLayer();};
            MouseLeave+=(o,e)=>{if(compact){if(UpdatePointerHover())RenderLayer();}else hover=new PointF(-1,-1);};
            MouseClick+=(o,e)=>{if(e.Button==MouseButtons.Left) { var h=hits.LastOrDefault(x=>x.Item1.Contains(new PointF(e.X/scale,e.Y/scale))); if(h!=null) h.Item2();else if(compact&&!justDragged){if(Skin=="nailong"){Say("收到了，一起慢慢忙。",2);RenderLayer();}else{companion.Happy(DateTime.UtcNow);Say("摸摸头，好啦，一起忙。",3);RenderLayer();}} }};
            MouseDown+=(o,e)=>{justDragged=false;if(compact && !Preferences.PositionLocked && e.Button==MouseButtons.Left && e.Y/scale>100 && e.Y/scale<358 && !hits.Any(h=>h.Item1.Contains(new PointF(e.X/scale,e.Y/scale)))) {dragging=true;dragOrigin=Cursor.Position;windowOrigin=Location;Capture=true;}};
            MouseUp+=(o,e)=>{if(dragging&&justDragged){SavePetPosition();Persist();}dragging=false;Capture=false;};
            MouseClick+=(o,e)=>{if(e.Button==MouseButtons.Right && trayMenu!=null) trayMenu.Show(this,e.Location);};
            Shown+=(o,e)=>{if(compact&&!renderOnly)RenderLayer();};
            Resize+=(o,e)=>{if(WindowState==FormWindowState.Minimized && !renderOnly) { Hide(); if(tray!=null) tray.ShowBalloonTip(1800,"搭子还在陪你","后台继续统计。双击托盘图标回来。",ToolTipIcon.Info); }};
            FormClosing+=(o,e)=>{ if(!renderOnly) { if(!compact&&!exitRequested&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;ToggleCompact();return;} SavePetPosition();if(!Persist()) { if(MessageBox.Show("记录保存失败："+saveError+"\n仍要退出吗？", "保存失败",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.No) { e.Cancel=true; return; } } timer.Stop(); if(tray!=null) tray.Dispose(); } };
            if(!preview) { CreateTray(); timer.Interval=40; timer.Tick+=(o,e)=>Tick(); timer.Start(); }
        }
        protected override CreateParams CreateParams {get {var p=base.CreateParams;if(compact)p.ExStyle|=0x80000|0x80|0x08000000;return p;}}
        protected override bool ShowWithoutActivation {get{return compact;}}
        void ApplyPalette(){ink=C("#303A35");green=Skin=="nailong"?C("#976117"):Skin=="cream"?C("#936A50"):Skin=="midnight"?C("#496B7D"):C("#4D7160");cream=C("#F5F5F0");muted=C("#68736C");orange=C("#BD8C53");}
        void ResizeDpi(float dpi){float factor=dpi*(compact?model.Data.Zoom/100f:1);if(!compact){Rectangle a=Screen.FromControl(this).WorkingArea;factor=Math.Min(factor,Math.Min((a.Width-40)/720f,(a.Height-60)/560f));}ClientSize=new Size((int)((compact?320:720)*factor),(int)((compact?360:560)*factor));}
        void ChooseTheme(string theme){model.Data.Theme=theme;ApplyPalette();dirty=true;Persist();Say("换好啦，今天也一起忙。",4);if(compact)RenderLayer();else Invalidate();}
        void ChooseZoom(int zoom){model.Data.Zoom=zoom;dirty=true;ResizeDpi(Native.Dpi(Handle));Rectangle a=Screen.FromControl(this).WorkingArea;Location=new Point(Math.Max(a.Left,Math.Min(Left,a.Right-Width)),Math.Max(a.Top,Math.Min(Top,a.Bottom-Height)));Persist();if(compact)RenderLayer();else Invalidate();}
        void ChoosePetSize(int size){model.Data.PetScale=size;dirty=true;Persist();if(compact)RenderLayer();else Invalidate();}
        void RenderLayer() {
            if(!compact || !IsHandleCreated || renderOnly || !Visible) return;
            IntPtr screen=IntPtr.Zero,memory=IntPtr.Zero,bitmap=IntPtr.Zero,previous=IntPtr.Zero;
            try {
                using(Bitmap b=new Bitmap(ClientSize.Width,ClientSize.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) {
                    using(Graphics g=Graphics.FromImage(b)) OnPaint(new PaintEventArgs(g,new Rectangle(Point.Empty,ClientSize)));
                    screen=Native.GetDC(IntPtr.Zero);memory=Native.CreateCompatibleDC(screen);
                    bitmap=b.GetHbitmap(Color.FromArgb(0));previous=Native.SelectObject(memory,bitmap);
                    Native.Point dst=new Native.Point(Left,Top),src=new Native.Point(0,0);
                    Native.Size size=new Native.Size(ClientSize.Width,ClientSize.Height);
                    Native.Blend blend=new Native.Blend{Op=0,Flags=0,Alpha=255,Format=1};
                    layerOk=Native.UpdateLayeredWindow(Handle,screen,ref dst,ref size,memory,ref src,0,ref blend,2);
                    if(!layerOk) layerError="透明窗口未能显示（"+Marshal.GetLastWin32Error()+"），请从托盘打开工位。";
                    else {layerError="";renders++;}
                }
            } finally {
                if(previous!=IntPtr.Zero)Native.SelectObject(memory,previous);
                if(bitmap!=IntPtr.Zero)Native.DeleteObject(bitmap);
                if(memory!=IntPtr.Zero)Native.DeleteDC(memory);
                if(screen!=IntPtr.Zero)Native.ReleaseDC(IntPtr.Zero,screen);
            }
        }
        Icon MakeIcon() {
            using(Bitmap b=new Bitmap(32,32)) { using(Graphics g=Graphics.FromImage(b)) { g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(Color.Transparent); Fill(g,green,0,0,32,32,10); g.FillEllipse(Brushes.White,5,5,22,22); g.FillEllipse(new SolidBrush(ink),11,13,3,3); g.FillEllipse(new SolidBrush(ink),19,13,3,3); } IntPtr h=b.GetHicon(); Icon result=(Icon)Icon.FromHandle(h).Clone(); DestroyIcon(h); return result; }
        }
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        void CreateTray() {
            trayMenu=new ContextMenuStrip();
            trayMenu.Opening+=(o,e)=>{trayMenu.Items.Clear();
                trayMenu.Items.Add("查看统计 / 布置工位",null,(a,b)=>{SetThrough(false); if(compact) ToggleCompact(); ShowBuddy();});
                trayMenu.Items.Add(compact?"一起开工":"回到桌面搭子",null,(a,b)=>{SetThrough(false); if(!compact)ToggleCompact();Mode("work");ShowBuddy();});
                trayMenu.Items.Add("喝口水",null,(a,b)=>Mode("water"));
                trayMenu.Items.Add("午休一下",null,(a,b)=>Mode("rest"));
                trayMenu.Items.Add("设置…",null,(a,b)=>OpenSettings(0));
                trayMenu.Items.Add("月度统计 / 导出",null,(a,b)=>{SetThrough(false);if(compact)ToggleCompact();activeTab="month";ShowBuddy();Invalidate();});
                var locked=new ToolStripMenuItem("锁定位置"){Checked=Preferences.PositionLocked};locked.Click+=(a,b)=>{Preferences.PositionLocked=!Preferences.PositionLocked;dirty=true;Persist();};trayMenu.Items.Add(locked);
                var dnd=new ToolStripMenuItem("勿扰 · 不弹出提醒"){Checked=Preferences.DoNotDisturb};dnd.Click+=(a,b)=>ToggleDnd();trayMenu.Items.Add(dnd);
                var skins=new ToolStripMenuItem("外观 · "+SkinName);
                foreach(string name in Themes) {string choice=name;var item=new ToolStripMenuItem(ThemeNames[Array.IndexOf(Themes,name)]);item.Checked=Skin==name;item.Click+=(a,b)=>ChooseTheme(choice);skins.DropDownItems.Add(item);}trayMenu.Items.Add(skins);
                if(Skin=="nailong")trayMenu.Items.Add("捧腹大笑一下",null,(a,b)=>{Mode("work");LaughNow();});
                var petSize=new ToolStripMenuItem("角色大小");foreach(int value in new[]{40,55,70}){int choice=value;var item=new ToolStripMenuItem(value==40?"迷你":value==55?"小号 · 默认":"中号");item.Checked=model.Data.PetScale==value;item.Click+=(a,b)=>ChoosePetSize(choice);petSize.DropDownItems.Add(item);}trayMenu.Items.Add(petSize);
                var sizeMenu=new ToolStripMenuItem("文字大小");foreach(int value in new[]{100,120,140}){int choice=value;var item=new ToolStripMenuItem(value+"%"+(value==100?" · 标准":value==120?" · 大字":" · 更大"));item.Checked=model.Data.Zoom==value;item.Click+=(a,b)=>ChooseZoom(choice);sizeMenu.DropDownItems.Add(item);}trayMenu.Items.Add(sizeMenu);
                trayMenu.Items.Add(model.Paused?"继续统计":"暂停统计",null,(a,b)=>Pause());
                if(compact) trayMenu.Items.Add(through?"关闭鼠标穿透":"开启鼠标穿透",null,(a,b)=>SetThrough(!through));
                trayMenu.Items.Add(Visible?"隐藏搭子":"显示搭子",null,(a,b)=>{if(Visible) Hide(); else ShowBuddy();});
                trayMenu.Items.Add("关于与数据",null,(a,b)=>About());
                trayMenu.Items.Add("退出（保存记录）",null,(a,b)=>{exitRequested=true;Close();});
            };
            tray=new NotifyIcon { Icon=Icon, Text="上班搭子 · 本地点击计数", Visible=true, ContextMenuStrip=trayMenu };
            tray.DoubleClick+=(o,e)=>{SetThrough(false);ShowBuddy();};
        }
        void ShowBuddy() { Show(); WindowState=FormWindowState.Normal; if(compact)RenderLayer();else Activate(); }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            if(!renderOnly) { ResizeDpi(Native.Dpi(Handle));if(compact&&!located){RestorePetPosition();located=true;}RegisterShortcut();var d=new Native.Device {Page=1,Usage=2,Flags=0x100,Target=Handle}; rawOk=Native.RegisterRawInputDevices(new[]{d},1,(uint)Marshal.SizeOf(typeof(Native.Device))); if(!rawOk) Say("鼠标监听未启动，请重启搭子。",30); }
        }
        protected override void WndProc(ref Message msg) {
            if(msg.Msg==0x312&&!renderOnly&&msg.WParam.ToInt32()==0x4F42){ToggleVisibility();return;}
            if(msg.Msg==0x7E&&!renderOnly&&compact){Location=PetPlacement.Constrain(Location,ClientSize,WorkingAreas);SavePetPosition();RenderLayer();}
            if((uint)msg.Msg==Native.ThemeMessage&&!renderOnly){int choice=msg.WParam.ToInt32();ChooseTheme(choice>=0&&choice<Themes.Length?Themes[choice]:"pixel");ShowBuddy();return;}
            if(msg.Msg==0x2E0&&!renderOnly){ResizeDpi((msg.WParam.ToInt64()&0xFFFF)/96f);Rectangle area=Screen.FromControl(this).WorkingArea;Location=new Point(Math.Max(area.Left,Math.Min(Left,area.Right-Width)),Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)));if(compact)RenderLayer();return;}
            if(msg.Msg==0xFF && !renderOnly) {
                uint size=0,hs=(uint)Marshal.SizeOf(typeof(Native.Header));
                uint result=Native.GetRawInputData(msg.LParam,0x10000003,IntPtr.Zero,ref size,hs);
                if(result!=uint.MaxValue && size>=hs+24 && size<=4096) {
                    IntPtr buffer=Marshal.AllocHGlobal((int)size);
                    try { uint read=Native.GetRawInputData(msg.LParam,0x10000003,buffer,ref size,hs);
                        if(read!=uint.MaxValue && read>=hs+24 && Marshal.ReadInt32(buffer)==0) {
                            ushort flags=(ushort)Marshal.ReadInt16(buffer,(int)hs+4);
                            foreach(int button in Model.Buttons(flags)) Receive(button);
                        }
                    } finally { Marshal.FreeHGlobal(buffer); }
                }
            }
            base.WndProc(ref msg);
        }
        void Receive(int button) {
            bool own=!Preferences.CountOwnClicks&&IsOwnClick();if(model.Paused||own)return;
            int gain=model.Click(button,DateTime.Now,own);
            dirty=true;lastInput=DateTime.UtcNow;lastButton=button;anim=1;
            if(Skin=="nailong"&&mode=="work"&&nailong.Click(NailongNow,button))Say("哈哈哈，肚子都笑圆了。",4);
            if(button==0||button==2||button==3)leftTap=1;
            if(button==1||button==2||button==4)rightTap=1;
            timer.Interval=Skin=="nailong"?16:25;
            if(gain>0) Say("叮，攒到一枚搭子币！",4);
        }
        void Tick() {
            FeatureTick();
            anim=Math.Max(0,anim-0.08);leftTap=Math.Max(0,leftTap-0.085);rightTap=Math.Max(0,rightTap-0.085);
            string key=DateTime.Now.ToString("yyyy-MM-dd");
            if(dayKey!=key) {dayKey=key;model.Today(DateTime.Now);dirty=true;}
            if(dirty && (DateTime.UtcNow-lastSave).TotalSeconds>=5) Persist();
            if(Visible && WindowState!=FormWindowState.Minimized) {if(compact){UpdatePointerHover();RenderLayer();}else Invalidate();}
            timer.Interval=Skin=="nailong"&&nailong.Animating(NailongNow)?16:anim>0||leftTap>0||rightTap>0?25:Visible&&compact?60:Visible?100:1000;
        }
        bool Persist() {try {store.Save(model.Data);dirty=false;lastSave=DateTime.UtcNow;saveError="";return true;} catch(Exception e) {saveError=e.Message;lastSave=DateTime.UtcNow;return false;}}
        void Say(string text,int seconds) { bubble=text;bubbleUntil=DateTime.UtcNow.AddSeconds(seconds);Invalidate(); }
        void Pause() {model.Paused=!model.Paused; Say(model.Paused?"暂停记数，我陪着。":"好啦，继续一起忙。",6);}
        void Mode(string next) {mode=next;Say(next=="water"?"喝口水，再慢慢忙。":next=="rest"?"正在充电，稍后开工。":"开工，今天也慢慢来。",10);}
        void ToggleCompact() {
            if(compact){SavePetPosition();Persist();}
            SetThrough(false);compact=!compact;Hide();TopMost=compact;
            FormBorderStyle=compact?FormBorderStyle.None:FormBorderStyle.FixedSingle;
            ShowInTaskbar=!compact;RecreateHandle();ResizeDpi(Native.Dpi(Handle));
            if(compact) RestorePetPosition();else CenterToScreen();
            hover=new PointF(-1,-1);panelHovered=false;ShowBuddy();Invalidate();
        }
        void SetThrough(bool enabled) { through=enabled && compact;
            int style=Native.GetStyle(Handle,-20); Native.SetStyle(Handle,-20,through?(style|0x20):(style&~0x20));
            if(through){panelHovered=false;hover=new PointF(-1,-1);RenderLayer();}
            if(through && tray!=null && notifications) tray.ShowBalloonTip(2500,"鼠标穿透已开启","双击托盘图标即可恢复操作。",ToolTipIcon.Info);
        }
        void Buy(Item item) {
            if(model.Data.Owned.Contains(item.Id)) {Say(""+item.Name+"已经在工位上啦。",5);return;}
            if(!model.Buy(item)) {Say("还差 "+Math.Max(0,item.Cost-model.Data.Coins)+" 枚搭子币，慢慢攒就好。",6);return;}
            dirty=true;Persist();Say("收到了！我的"+item.Name+"，谢谢你。",8);
            companion.Happy(DateTime.UtcNow);
        }
        void About() {MessageBox.Show("上班搭子 v0.10.2\n\n右键菜单可切换外观；角色大小与文字大小独立调整。\n默认透明背景，无边框，不抢当前窗口焦点。\n拖动搭子移动位置；靠近时显示计数和互动按钮。\n右键搭子或托盘可打开菜单，查看统计及布置工位。\n\n左右中键、两个侧键：按下一次计一次。\n双击计两次，滚动和松开不计数。软件自身的点击默认计数，可在设置中关闭。\n每 100 次点击获得 1 枚搭子币，每日最多 20 枚。\n暂停时停止计数；喝水与午休只切换动作。\n隐藏或最小化后继续统计；关闭工位窗口会回到桌宠。\n要结束程序，请右键菜单选择“退出”。\n\n仅记录每日各按键次数、累计次数和装饰。\n不记录坐标、窗口标题或键盘，不联网；开机启动可在设置中选择。\n每日按电脑本地日期结算，每 5 秒保存一次。\n\n存档："+store.Path+"\n\n背景透明部分可直接操作下方窗口。托盘还可开启整个搭子的鼠标穿透。\n全屏独占游戏、远程桌面及特殊输入设备尚未逐一验证。"+(layerError!=""?"\n\n"+layerError:""), "关于上班搭子",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            hits.Clear();scale=ClientSize.Width/(compact?320f:720f);g.ScaleTransform(scale,scale);g.Clear(compact?Color.Transparent:cream);
            if(compact) DrawCompact(g);else DrawFull(g);
        }
        void Txt(Graphics g,string text,float x,float y,float size,Color color,bool bold) {using(Font f=new Font("Microsoft YaHei UI",size, bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))using(Brush b=new SolidBrush(color))g.DrawString(text,f,b,x,y);}
        void Center(Graphics g,string text,RectangleF rect,float size,Color color,bool bold) {using(Font f=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))using(Brush b=new SolidBrush(color))using(StringFormat sf=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(text,f,b,rect,sf);}
        static GraphicsPath Round(float x,float y,float w,float h,float r) {GraphicsPath p=new GraphicsPath();float d=r*2;p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;}
        static void Fill(Graphics g,Color c,float x,float y,float w,float h,float r) {using(var p=Round(x,y,w,h,r))using(var b=new SolidBrush(c))g.FillPath(b,p);}
        void Line(Graphics g,Color c,float width,float x,float y,float x2,float y2) {using(Pen p=new Pen(c,width)){p.StartCap=LineCap.Round;p.EndCap=LineCap.Round;g.DrawLine(p,x,y,x2,y2);}}
        void Ellipse(Graphics g,Color c,float x,float y,float w,float h) {using(Brush b=new SolidBrush(c))g.FillEllipse(b,x,y,w,h);}
        void Button(Graphics g,string label,float x,float y,float w,float h,Action action,bool active) {
            RectangleF r=new RectangleF(x,y,w,h);bool over=r.Contains(hover);
            Fill(g,active?green:over?C("#E9EEE7"):C("#F0F2EC"),x,y,w,h,9);
            Center(g,label,r,14,active?Color.White:ink,true);hits.Add(Tuple.Create(r,action));
        }
        string Speech() {
            if(DateTime.UtcNow<bubbleUntil) return bubble;
            if(Skin=="nailong"&&mode=="work"&&!AutoClosed&&nailong.Laughing(NailongNow))return "哈哈哈，肚子都笑圆了。";
            if(model.Paused) return "先歇一下，我在这里。";
            if(companion.Mood==CompanionMood.Sleeping)return "搭子睡着了，回来时一起开工。";
            if(companion.Mood==CompanionMood.Yawning)return "打个哈欠，眯一小会儿。";
            if(companion.Mood==CompanionMood.Waking)return "你回来啦，一起继续。";
            if(companion.Mood==CompanionMood.Happy)return "收到你的摸摸，元气补上啦。";
            if(mode=="water") return "喝口水，再慢慢忙。";
            if(mode=="rest") return "正在充电，稍后开工。";
            if((DateTime.UtcNow-lastInput).TotalSeconds>180) return "你忙你的，我陪着。";
            return "今天也一起慢慢来。";
        }
        void Panel(Graphics g,float x,float y,float w,float h) {
            Fill(g,Color.White,x,y,w,h,16);using(var path=Round(x,y,w,h,16))using(var pen=new Pen(C("#E3E8DF"),1))g.DrawPath(pen,path);
        }
        void DrawFull(Graphics g) {
            Txt(g,"上班搭子",24,20,24,ink,true);Txt(g,"把普通的工作日，过得可爱一点。",25,52,13,muted,false);
            Button(g,"概览",258,28,70,34,()=>{activeTab="overview";Invalidate();},activeTab=="overview"||activeTab=="month");
            Button(g,"工位",336,28,70,34,()=>{activeTab="shop";Invalidate();},activeTab=="shop");
            Button(g,"外观",414,28,70,34,()=>{activeTab="style";Invalidate();},activeTab=="style");
            Button(g,"设置",492,28,70,34,()=>{activeTab="settings";Invalidate();},activeTab=="settings");
            Button(g,"回到桌面",574,28,122,34,ToggleCompact,true);
            if(activeTab=="shop")DrawShopPage(g);else if(activeTab=="style")DrawStylePage(g);else if(activeTab=="settings")DrawSettingsPage(g);else if(activeTab=="month")DrawMonth(g);else DrawOverview(g);
            string footer=saveError!=""?"保存失败 · 右键菜单查看数据路径":!rawOk&&!renderOnly?"鼠标监听未启动 · 请重启":store.Notice!=""?store.Notice:"仅本地记录 · 隐藏搭子后继续统计";
            Ellipse(g,saveError!=""?Color.Firebrick:green,26,529,6,6);Txt(g,footer,39,522,12,muted,false);
            Txt(g,"v0.10.2",649,522,12,muted,false);
        }
        void DrawOverview(Graphics g) {
            Day d=model.Today(DateTime.Now);Panel(g,24,88,672,160);
            Txt(g,"今日点击",44,107,15,muted,false);
            Txt(g,d.Total.ToString("N0"),40,127,48,ink,true);
            Txt(g,"累计 "+model.Data.Lifetime.Sum().ToString("N0")+" 次",44,187,13,muted,false);
            Fill(g,C("#EFF3E9"),458,102,212,130,18);PetArt(g,482,108,0.54f);
            string[] names={"左键","右键","中键","侧键"};long[] nums={d.Buttons[0],d.Buttons[1],d.Buttons[2],d.Buttons[3]+d.Buttons[4]};
            for(int i=0;i<4;i++){float x=44+i*97;Txt(g,names[i],x,218,12,muted,false);Txt(g,nums[i].ToString("N0"),x+31,215,15,ink,true);}
            Panel(g,24,264,412,234);Txt(g,"这一周",44,284,18,ink,true);Txt(g,"每天的忙碌，都有被记住。",44,315,13,muted,false);
            Button(g,"查看月度",312,278,104,30,()=>{activeTab="month";Invalidate();},false);
            Chart(g,45,370,370,86);
            Panel(g,452,264,244,234);Txt(g,"今日状态",472,284,18,ink,true);
            Button(g,"一起开工",472,324,204,38,()=>Mode("work"),mode=="work");
            Button(g,"喝口水",472,370,98,38,()=>Mode("water"),mode=="water");
            Button(g,"午休一下",578,370,98,38,()=>Mode("rest"),mode=="rest");
            Button(g,model.Paused?"继续记数":"暂停记数",472,416,204,38,Pause,model.Paused);
            Txt(g,"休息动作不会暂停计数",474,469,12,muted,false);
        }
        void DrawShopPage(Graphics g) {
            Txt(g,"给小工位添点东西",24,99,22,ink,true);
            Txt(g,"每 100 次点击攒 1 币，每天最多 20 币。",25,135,14,muted,false);
            Fill(g,C("#F3EADB"),549,98,147,36,10);Center(g,model.Data.Coins+" 枚搭子币",new RectangleF(549,98,147,36),15,C("#805B35"),true);
            for(int i=0;i<Model.Items.Length;i++)Shop(g,Model.Items[i],24+(i%3)*228,179+(i/3)*160);
        }
        void DrawStylePage(Graphics g) {
            Txt(g,"选一个今天的搭子",24,99,22,ink,true);Txt(g,"小小一只，字可以大一点。",25,135,14,muted,false);
            string original=Skin;
            for(int i=0;i<Themes.Length;i++){float x=24+i*172;Panel(g,x,178,156,182);Fill(g,C(i==3?"#FFF3D8":"#F2F4ED"),x+10,190,136,113,12);model.Data.Theme=Themes[i];PetArt(g,x+22,200,0.42f);model.Data.Theme=original;string choice=Themes[i];Button(g,ThemeNames[i]+(original==choice?" ✓":""),x+10,315,136,32,()=>ChooseTheme(choice),original==choice);}
            Panel(g,24,378,672,120);Txt(g,"角色大小",44,399,14,ink,true);int j=0;foreach(int value in new[]{40,55,70}){int v=value;Button(g,value==40?"迷你":value==55?"小号":"中号",163+j++*75,393,67,32,()=>ChoosePetSize(v),model.Data.PetScale==value);}
            Txt(g,"文字大小",44,446,14,ink,true);j=0;foreach(int value in new[]{100,120,140}){int v=value;Button(g,value+"%",163+j++*75,440,67,32,()=>ChooseZoom(v),model.Data.Zoom==value);}
            Txt(g,"文字与角色独立缩放",465,427,13,muted,false);
        }
        void Chart(Graphics g,float x,float y,float w,float h) {
            var days=Enumerable.Range(0,7).Select(i=>DateTime.Today.AddDays(i-6)).ToArray();
            long[] values=days.Select(date=>{Day d=model.Data.Days.Find(a=>a.Date==date.ToString("yyyy-MM-dd"));return d==null?0:d.Total;}).ToArray();
            long max=Math.Max(1,values.Max());
            Line(g,Color.FromArgb(237,239,232),1,x,y+h,x+w,y+h);
            for(int i=0;i<7;i++) {float xx=x+i*(w/7)+7,bar=(float)(values[i]/(double)max)*(h-12); if(values[i]>0) Fill(g,i==6?green:Color.FromArgb(197,213,184),xx,y+h-Math.Max(3,bar),19,Math.Max(3,bar),3);else Ellipse(g,Color.FromArgb(218,223,211),xx+7,y+h-4,4,4);
                Center(g,days[i].ToString("dd"),new RectangleF(xx-7,y+h+4,34,17),10,i==6?green:muted,i==6);
                if(values[i]>0) Center(g,values[i]>=10000?(values[i]/1000)+"k":values[i].ToString(),new RectangleF(xx-8,y+h-bar-16,37,14),9,muted,false);
            }
        }
        void Shop(Graphics g,Item item,float x,float y) {
            bool owned=model.Data.Owned.Contains(item.Id);Panel(g,x,y,216,144);
            Fill(g,C("#F2F4ED"),x+14,y+16,53,55,12);Decor(g,item.Id,x+17,y+19,0.95f);
            Txt(g,item.Name,x+80,y+19,16,ink,true);Txt(g,item.Description,x+80,y+48,12,muted,false);
            bool placed=model.IsPlaced(item.Id);
            Button(g,owned?(placed?"已布置 ✓ · 收起":"已拥有 · 摆上"):item.Cost+" 币 · 布置",x+14,y+93,188,35,()=>{if(owned){model.TogglePlacement(item.Id);dirty=true;Persist();if(compact)RenderLayer();else Invalidate();}else Buy(item);},owned?placed:model.Data.Coins>=item.Cost);
        }
        RectangleF HoverBridge() {
            float ps=model.Data.PetScale/100f/(model.Data.Zoom/100f),petTop=352-225*ps,y=Math.Max(12,petTop-190);
            float headTop=petTop+(40)*ps;
            return new RectangleF(24,y+170,272,Math.Max(0,headTop+2-y-170));
        }
        bool ApplyPointerHover(PointF point,bool hitsWindow) {
            bool next=!through&&(hitsWindow||(panelHovered&&HoverBridge().Contains(point)));
            bool changed=panelHovered!=next;panelHovered=next;hover=next?point:new PointF(-1,-1);return changed;
        }
        bool UpdatePointerHover() {
            if(renderOnly||!IsHandleCreated)return false;
            Point cursor=Cursor.Position,point=PointToClient(cursor);
            return ApplyPointerHover(new PointF(point.X/scale,point.Y/scale),Native.WindowFromPoint(new Native.Point(cursor.X,cursor.Y))==Handle);
        }
        void DrawCompact(Graphics g) {
            bool controls=(renderOnly&&!previewQuiet&&!previewPointerDriven)||panelHovered;
            float ps=model.Data.PetScale/100f/(model.Data.Zoom/100f), petTop=352-225*ps;
            PetArt(g,160-134*ps,petTop,ps);
            if(controls&&!previewQuiet) {
                float y=Math.Max(12,petTop-190);Card(g,Surface,24,y,272,170);
                Txt(g,Skin=="pixel"?"薄荷 · 上班搭子":SkinName,40,y+12,15,PrimaryText,true);
                Fill(g,Skin=="midnight"?C("#344C40"):C("#EAF0E4"),231,y+12,49,22,7);
                Center(g,model.Paused?"暂停":mode=="rest"?"午休":mode=="water"?"喝水":AutoClosed?"睡眠":"在线",new RectangleF(231,y+12,49,22),12,Accent,true);
                Txt(g,saveError!=""?"记录保存失败，请查看菜单。":Speech(),40,y+41,14,PrimaryText,false);
                Txt(g,"今日点击",40,y+77,13,PrimaryText,false);
                Center(g,model.Today(DateTime.Now).Total.ToString("N0"),new RectangleF(109,y+65,122,43),29,Accent,true);
                Txt(g,"次",253,y+81,13,PrimaryText,false);
                Line(g,Skin=="midnight"?C("#43504A"):C("#E2E8DD"),1,40,y+115,280,y+115);
                GameButton(g,model.Paused?"继续":"暂停",36,y+127,56,30,Pause,model.Paused);
                GameButton(g,"喝水",100,y+127,56,30,()=>Mode(mode=="water"?"work":"water"),mode=="water");
                GameButton(g,"午休",164,y+127,56,30,()=>Mode(mode=="rest"?"work":"rest"),mode=="rest");
                GameButton(g,"工位",228,y+127,56,30,ToggleCompact,false);
                using(var b=new SolidBrush(Surface))g.FillPolygon(b,new[]{new PointF(152,y+169),new PointF(168,y+169),new PointF(160,y+179)});
            }
        }
        void Card(Graphics g,Color color,float x,float y,float w,float h) {
            Fill(g,Color.FromArgb(12,28,39,29),x-3,y+2,w+6,h+7,18);
            Fill(g,Color.FromArgb(18,28,39,29),x,y+3,w,h+2,16);
            Fill(g,color,x,y,w,h,14);
            using(var path=Round(x,y,w,h,14))using(var pen=new Pen(Skin=="midnight"?C("#43524B"):C("#DDE5D7"),1))g.DrawPath(pen,path);
        }
        void GameButton(Graphics g,string label,float x,float y,float w,float h,Action action,bool active) {
            RectangleF r=new RectangleF(x,y,w,h);bool over=r.Contains(hover);
            Color bg=active?Accent:Skin=="midnight"?C(over?"#3B5148":"#2F3D35"):C(over?"#E1EADC":"#EEF2E8");
            Fill(g,bg,x,y,w,h,8);Center(g,label,r,14,active?(Skin=="midnight"?C("#253B2E"):Color.White):PrimaryText,true);hits.Add(Tuple.Create(r,action));
        }
        void Block(Graphics g,Color color,float x,float y,float w,float h){using(Brush b=new SolidBrush(color))g.FillRectangle(b,x,y,w,h);}
        void P(Graphics g,Color color,float x,float y,float w,float h){Block(g,color,x*4,y*4,w*4,h*4);}
        PointF Gaze(){if(renderOnly)return PointF.Empty;Point p=Cursor.Position;return new PointF((float)Math.Max(-2.5,Math.Min(2.5,(p.X-Left-160*scale)/180)),(float)Math.Max(-1.5,Math.Min(1.5,(p.Y-Top-295*scale)/200)));}
        void PetArt(Graphics g,float x,float y,float s) {
            var state=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(s,s);
            double phase=renderOnly?previewPhase:DateTime.UtcNow.TimeOfDay.TotalSeconds;
            PixelCompanion(g,phase);
            g.Restore(state);
        }
        static float HandLift(double value) {return value<=0?0:(float)(Math.Sin(Math.PI*value)*18+value*3);}
        PointF EyeGaze(Graphics g) {
            if(renderOnly)return previewGaze;
            PointF[] center={new PointF(139,127)};using(var transform=g.Transform)transform.TransformPoints(center);
            Point mouse=Cursor.Position;
            Point origin=PointToScreen(Point.Empty);
            return new PointF(MotionStrength*(float)Math.Max(-3.5,Math.Min(3.5,(mouse.X-origin.X-center[0].X)/90)),MotionStrength*(float)Math.Max(-1.8,Math.Min(1.8,(mouse.Y-origin.Y-center[0].Y)/130)));
        }
        void PixelShape(Graphics g,Color color,PointF[] points){using(var brush=new SolidBrush(color))g.FillPolygon(brush,points);}
        void CatEye(Graphics g,float x,float y,float size,bool closed,PointF gaze) {
            PixelEye.Draw(g,new RectangleF(x,y,24*size,13*size),closed,gaze,C("#654251"),C("#533448"));
        }
        void CatPaw(Graphics g,float x,float y,float size,float lift) {
            var state=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(size,size);g.SmoothingMode=SmoothingMode.None;
            if(lift>1){Block(g,C(Skin=="cream"?"#A56F59":Skin=="midnight"?"#686280":"#536C4C"),1,-lift+8,13,lift+4);Block(g,C(Skin=="cream"?"#DCA487":Skin=="midnight"?"#A49ABA":"#91A476"),3,-lift+8,9,lift+1);}
            g.TranslateTransform(0,-lift);
            PixelShape(g,C("#3E2939"),new[]{new PointF(0,7),new PointF(3,7),new PointF(3,3),new PointF(7,3),new PointF(7,0),new PointF(17,0),new PointF(17,2),new PointF(22,2),new PointF(22,6),new PointF(25,6),new PointF(25,13),new PointF(21,13),new PointF(21,17),new PointF(3,17),new PointF(3,14),new PointF(0,14)});
            Block(g,C(Skin=="midnight"?"#61768A":"#FFE7C8"),3,7,19,6);Block(g,C(Skin=="midnight"?"#61768A":"#FFE7C8"),7,3,10,11);Block(g,C(Skin=="midnight"?"#8D9BAD":"#FFF4DE"),8,3,7,3);Block(g,C(Skin=="midnight"?"#46586E":"#F1C4A4"),6,12,3,3);Block(g,C(Skin=="midnight"?"#46586E":"#F1C4A4"),12,12,3,3);Block(g,C(Skin=="midnight"?"#46586E":"#F1C4A4"),18,10,3,3);
            g.Restore(state);
        }
        void PixelCompanion(Graphics g,double phase) {
            if(Skin=="nailong"){DrawNailong(g,phase);return;}
            Bitmap body=Skin=="pixel"?rigSprite:null;
            if(model.IsPlaced("cushion"))Decor(g,"cushion",56,122,2.2f);
            if(model.IsPlaced("lamp"))Decor(g,"lamp",263,181,0.75f);
            if(model.IsPlaced("clock"))Decor(g,"clock",21,190,0.58f);
            var st=g.Save();float bounce=companion.Mood==CompanionMood.Happy?-(float)Math.Abs(Math.Sin(phase*6))*4:companion.Mood==CompanionMood.Waking?-(float)Math.Abs(Math.Sin(phase*9))*3:0;g.TranslateTransform(0,MotionStrength*((float)Math.Sin(phase*2)*1.2f+bounce));
            g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            bool waterPhoto=Skin=="pixel"&&mode=="water";
            Bitmap sprite=waterPhoto?waterSprite:body;
            if(sprite!=null){float height=232f*sprite.Height/sprite.Width;g.DrawImage(sprite,new RectangleF(18,217-height,232,height));}
            else DrawUserCharacter(g,phase);
            if(!waterPhoto&&Skin=="pixel") {
                bool blink=mode=="rest"||mode=="water"||AutoClosed||(renderOnly&&previewBlink.HasValue?previewBlink.Value:phase%4.4>4.13);
                PointF gaze=EyeGaze(g);CatEye(g,112,117,1,blink,gaze);CatEye(g,157,126,0.80f,blink,gaze);
                float idle=mode=="work"&&phase%9>7.8?(float)Math.Sin((phase%9-7.8)*Math.PI/1.2)*3:0;
                CatPaw(g,117,mode=="water"?144:167,1,mode=="work"&&!AutoClosed?MotionStrength*(HandLift(leftTap)+idle):0);
                CatPaw(g,151,167,0.70f,mode=="work"&&!AutoClosed?MotionStrength*HandLift(rightTap):0);
                if(mode=="water")Decor(g,"cup",117,125,0.62f);
            }
            DrawYawn(g);g.Restore(st);
            if(mode=="rest"||companion.Mood==CompanionMood.Sleeping){Txt(g,"z",225,34,20,C("#6A8274"),true);Txt(g,"z",240,18,15,C("#6A8274"),true);}
            DrawEmotion(g);
            Decorations(g);
        }
        RectangleF UserSpriteBounds {get{return new RectangleF(28,7,210,210);}}
        Bitmap UserSprite {get{return Skin=="cream"?rabbitSprite:nightSprite;}}
        Rectangle UserEye(bool right) {
            if(Skin=="cream")return right?new Rectangle(746,727,111,77):new Rectangle(492,677,145,86);
            return right?new Rectangle(790,682,134,107):new Rectangle(493,625,179,105);
        }
        Rectangle UserHand(bool right) {
            if(Skin=="cream")return right?new Rectangle(684,1003,105,93):new Rectangle(509,1014,145,104);
            return right?new Rectangle(721,949,113,104):new Rectangle(543,963,175,132);
        }
        void DrawUserCharacter(Graphics g,double phase) {
            Bitmap original=UserSprite;
            if(original==null)return;
            SpriteMotion motion=Skin=="cream"?rabbitMotion:nightMotion;
            if(motion==null){motion=new SpriteMotion(original,Skin=="cream"?rabbitRig:nightRig,210,UserEye(false),UserEye(true),UserHand(false),UserHand(true));if(Skin=="cream")rabbitMotion=motion;else nightMotion=motion;}
            bool closed=mode=="rest"||mode=="water"||AutoClosed||(renderOnly&&previewBlink.HasValue?previewBlink.Value:phase%4.4>4.13);
            PointF gaze=EyeGaze(g);
            float left=mode=="work"&&!AutoClosed?MotionStrength*HandLift(leftTap):mode=="water"?22:0,right=mode=="work"&&!AutoClosed?MotionStrength*HandLift(rightTap):0;
            g.DrawImage(motion.Render(closed,gaze,left,right),UserSpriteBounds);
            if(mode=="water")Decor(g,"cup",119,142,0.62f);
        }
        protected override void Dispose(bool disposing) {
            if(disposing){if(reminderWindow!=null)reminderWindow.Close();if(rabbitMotion!=null)rabbitMotion.Dispose();if(nightMotion!=null)nightMotion.Dispose();if(nailongAtlas!=null)nailongAtlas.Dispose();timer.Dispose();}
            base.Dispose(disposing);
        }
        void Decorations(Graphics g) {
            if(model.IsPlaced("plant"))Decor(g,"plant",-12,181,0.78f);
            if(model.IsPlaced("cup")&&mode!="water")Decor(g,"cup",223,186,0.68f);
        }
        void Decor(Graphics g,string id,float x,float y,float s) {
            PixelDecor.Draw(g,id,x,y,s,Skin=="midnight");
        }
        public void SetPreviewTab(string name){activeTab=new[]{"shop","style","settings","month"}.Contains(name)?name:"overview";}
        public void Catalog(string path) {
            string original=Skin;model.Data.Owned.Clear();mode="work";previewPhase=0.5;previewBlink=false;
            using(var b=new Bitmap(1100,570,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) {
                using(var g=Graphics.FromImage(b)) {
                    g.Clear(C("#F5F5F0"));g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    Txt(g,"同一个小工位，三位上班搭子",30,22,24,ink,true);
                    string[] themes={"pixel","cream","midnight"};
                    for(int i=0;i<3;i++) {
                        float x=30+i*350;model.Data.Theme=themes[i];Panel(g,x,76,340,292);
                        PetArt(g,x+36,85,1);Center(g,SkinName,new RectangleF(x,318,340,30),18,ink,true);
                    }
                    model.Data.Theme=original;Txt(g,"像素小工位 · 五件日常好物",30,396,18,ink,true);
                    for(int i=0;i<Model.Items.Length;i++) {
                        float x=30+i*210;Panel(g,x,437,200,108);Decor(g,Model.Items[i].Id,x+75,446,1);
                        Center(g,Model.Items[i].Name,new RectangleF(x,510,200,25),14,ink,true);
                    }
                }
                b.Save(path,System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        bool PreviewPanelVisible() {
            float ps=model.Data.PetScale/100f/(model.Data.Zoom/100f),y=Math.Max(12,352-225*ps-190);
            using(var b=new Bitmap(320,360,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) {
                using(var g=Graphics.FromImage(b))OnPaint(new PaintEventArgs(g,new Rectangle(0,0,320,360)));
                return b.GetPixel(40,(int)y+20).A>0;
            }
        }
        public bool CheckHoverRendering() {
            compact=true;ClientSize=new Size(320,360);previewPointerDriven=true;previewQuiet=false;
            foreach(string theme in Themes)foreach(int size in new[]{40,55,70})foreach(int zoom in new[]{100,120,140}) {
                model.Data.Theme=theme;model.Data.PetScale=size;model.Data.Zoom=zoom;panelHovered=false;through=false;
                if(PreviewPanelVisible())return false;
                if(!ApplyPointerHover(new PointF(160,295),true)||!PreviewPanelVisible())return false;
                RectangleF bridge=HoverBridge();ApplyPointerHover(new PointF(160,bridge.Top+bridge.Height/2),false);
                if(!PreviewPanelVisible())return false;
                ApplyPointerHover(new PointF(40,bridge.Top-100),true);if(!PreviewPanelVisible())return false;
                if(!ApplyPointerHover(new PointF(315,30),false)||PreviewPanelVisible())return false;
                Say("叮，攒到一枚搭子币！",4);if(PreviewPanelVisible())return false;
                ApplyPointerHover(new PointF(160,295),true);through=true;ApplyPointerHover(new PointF(160,295),true);
                if(PreviewPanelVisible())return false;
            }
            return true;
        }
        public void Screenshot(string path,bool small,bool quiet=false,float density=1) { previewQuiet=quiet;compact=small;ClientSize=new Size((int)((small?320:720)*density),(int)((small?360:560)*density));using(Bitmap b=new Bitmap(ClientSize.Width,ClientSize.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) {using(Graphics g=Graphics.FromImage(b)) OnPaint(new PaintEventArgs(g,new Rectangle(Point.Empty,ClientSize)));b.Save(path,System.Drawing.Imaging.ImageFormat.Png);} }
        public void MotionFrames(string folder) {
            if(Skin=="nailong"){NailongPoses(folder);return;}
            Directory.CreateDirectory(folder);previewPhase=0.5;previewQuiet=true;previewBlink=false;mode="work";
            foreach(string pose in new[]{"neutral","look-left","look-right","look-up","look-down","blink","left-hand","right-hand","both-hands","rest"}) {
                previewGaze=pose=="look-left"?new PointF(-3.5f,0):pose=="look-right"?new PointF(3.5f,0):pose=="look-up"?new PointF(0,-1.8f):pose=="look-down"?new PointF(0,1.8f):PointF.Empty;
                previewBlink=pose=="blink";mode=pose=="rest"?"rest":"work";
                leftTap=pose=="left-hand"||pose=="both-hands"?0.55:0;rightTap=pose=="right-hand"||pose=="both-hands"?0.55:0;
                Screenshot(Path.Combine(folder,pose+".png"),true,true,2);
            }
            if(Skin!="pixel") {
                Rectangle eyes=Rectangle.Union(MotionRegion(0,false),MotionRegion(1,false));
                File.WriteAllText(Path.Combine(folder,"regions.json"),"{\"eyes\":"+RegionJson(eyes)+",\"left\":"+RegionJson(MotionRegion(2,true))+",\"right\":"+RegionJson(MotionRegion(3,true))+"}");
            }
        }
        Rectangle MotionRegion(int part,bool hand) {
            Rectangle bounds=(Skin=="cream"?rabbitMotion:nightMotion).PartBounds(part);
            float ps=model.Data.PetScale/(float)model.Data.Zoom;
            float x=2*(160-134*ps+(28+bounds.X)*ps),y=2*(352-225*ps+(7+bounds.Y)*ps);
            float w=2*bounds.Width*ps,h=2*bounds.Height*ps;
            return Rectangle.FromLTRB((int)Math.Floor(x-(hand?0:3)),(int)Math.Floor(y-(hand?24*ps*2:3)),(int)Math.Ceiling(x+w+(hand?0:3)),(int)Math.Ceiling(y+h+3));
        }
        static string RegionJson(Rectangle rect){return "["+rect.Left+","+rect.Top+","+rect.Right+","+rect.Bottom+"]";}
        public void Animation(string folder) {
            Directory.CreateDirectory(folder);previewBlink=null;
            for(int i=0;i<144;i++){
                previewPhase=i/12.0;mode=i<96?"work":i<120?"water":"rest";
                previewGaze=new PointF((float)Math.Sin(previewPhase*1.1)*3.5f,(float)Math.Cos(previewPhase)*1.8f);
                bool left=i%24<12;double pulse=1-(i%12)/12.0;leftTap=mode=="work"&&left?pulse:0;rightTap=mode=="work"&&!left?pulse:0;
                Screenshot(Path.Combine(folder,i.ToString("D3")+".png"),true,true,1.5f);
            }
        }
        public void MotionShowcase(string folder) {
            Directory.CreateDirectory(folder);
            for(int i=0;i<160;i++) {
                previewPhase=0.5+i/20.0;mode=i>=140?"rest":"work";previewGaze=PointF.Empty;previewBlink=false;leftTap=rightTap=0;
                string label="轻轻呼吸";
                if(i>=20&&i<40){previewGaze=new PointF(-3.5f,0);label="眼神跟随 · 向左";}
                else if(i>=40&&i<60){previewGaze=new PointF(3.5f,0);label="眼神跟随 · 向右";}
                else if(i>=60&&i<80){previewBlink=i%10<6;label="自然眨眼";}
                else if(i>=80&&i<110){leftTap=1-(i%10)/10.0;label="左键 → 左手敲击";}
                else if(i>=110&&i<140){rightTap=1-(i%10)/10.0;label="右键 → 右手敲击";}
                else if(i>=140){label="午休 · 闭眼放松";}
                using(var bitmap=new Bitmap(320,260,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) {
                    using(var g=Graphics.FromImage(bitmap)) {
                        g.Clear(C("#F5F5F0"));g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        Center(g,SkinName+" · 动效演示",new RectangleF(0,8,320,28),15,ink,true);
                        PetArt(g,53,39,0.80f);Center(g,label,new RectangleF(0,225,320,25),14,green,true);
                    }
                    bitmap.Save(Path.Combine(folder,i.ToString("D3")+".png"),System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }
        public string CheckOverlayLifecycle() {
            var report=new List<string>();
            if(!compact || FormBorderStyle!=FormBorderStyle.None || ShowInTaskbar || !TopMost || !rawOk || !layerOk)throw new Exception("Overlay startup failed: raw="+rawOk+", layer="+layerOk+", error="+layerError);
            report.Add("PASS: transparent startup, borderless, topmost, no taskbar entry, Raw Input, UpdateLayeredWindow");
            SetThrough(true);if((Native.GetStyle(Handle,-20)&0x20)==0)throw new Exception("click through style missing");SetThrough(false);
            if((Native.GetStyle(Handle,-20)&0x20)!=0)throw new Exception("click through style remained");
            report.Add("PASS: click-through enable / restore");
            ToggleCompact();if(compact||FormBorderStyle==FormBorderStyle.None||!ShowInTaskbar||!rawOk||(Native.GetStyle(Handle,-20)&0x80000)!=0)throw new Exception("dashboard transition failed");
            report.Add("PASS: dashboard transition and mouse re-registration");
            ToggleCompact();RenderLayer();if(!compact||!rawOk||!layerOk)throw new Exception("return to pet failed: "+layerError);
            report.Add("PASS: pet return and alpha rendering after handle recreation");
            Application.DoEvents();
            if(Native.WindowFromPoint(new Native.Point(Left+2,Top+2))==Handle)throw new Exception("transparent background intercepts input");
            bool characterHit=false;for(int attempt=0;attempt<12&&!characterHit;attempt++){Application.DoEvents();characterHit=Native.WindowFromPoint(new Native.Point(Left+(int)(160*scale),Top+(int)(295*scale)))==Handle;if(!characterHit)Thread.Sleep(20);}
            if(!characterHit){IntPtr found=Native.WindowFromPoint(new Native.Point(Left+(int)(160*scale),Top+(int)(295*scale)));uint pid;Native.GetWindowThreadProcessId(found,out pid);throw new Exception("visible character not interactive after window transition; expected="+Handle+", found="+found+", pid="+pid+", location="+Location+", size="+ClientSize+", scale="+scale+", dpi="+Native.Dpi(Handle)+", style="+Native.GetStyle(Handle,-20));}
            report.Add("PASS: transparent pixels pass input to underlying window; opaque character accepts input");
            ToggleCompact();Close();if(!compact||IsDisposed)throw new Exception("closing dashboard failed to return to pet");
            report.Add("PASS: closing dashboard returns to desktop pet");
            string old=Skin;foreach(string theme in Themes){ChooseTheme(theme);RenderLayer();if(!layerOk)throw new Exception("theme render failed: "+theme);}ChooseTheme(old);report.Add("PASS: all four themes render and switch at runtime");
            foreach(string state in new[]{"work","water","rest"}){Mode(state);RenderLayer();if(!layerOk)throw new Exception("companion state render failed: "+state);}Mode("work");report.Add("PASS: work, water and rest states render at runtime");
            leftTap=rightTap=0;long before=Total;Receive(0);if(leftTap!=1||rightTap!=0||Total!=before+1)throw new Exception("left input animation routing failed");
            Receive(1);if(leftTap!=1||rightTap!=1||Total!=before+2)throw new Exception("alternating input lost a hand animation");
            int startRenders=renders;model.Paused=true;var motionClock=System.Diagnostics.Stopwatch.StartNew();
            while(motionClock.ElapsedMilliseconds<70){Application.DoEvents();Thread.Sleep(5);}
            if(leftTap<=0||rightTap<=0||leftTap>=1||rightTap>=1)throw new Exception("live animation timer did not advance hands");
            while(motionClock.ElapsedMilliseconds<700){Application.DoEvents();Thread.Sleep(5);}model.Paused=false;
            if(leftTap!=0||rightTap!=0||renders-startRenders<8)throw new Exception("live motion did not settle or repaint");
            report.Add("PASS: click input triggers independent hands; real UI timer advances, repaints and settles motion");
            float petPixels=scale*model.Data.PetScale/model.Data.Zoom;ChooseZoom(120);if(Math.Abs(ClientSize.Width-320*Native.Dpi(Handle)*1.2)>2)throw new Exception("DPI/zoom sizing failed");if(Math.Abs(scale*model.Data.PetScale/model.Data.Zoom-petPixels)>0.02)throw new Exception("text zoom changed pet size");ChooseZoom(100);report.Add("PASS: DPI-aware text sizing leaves pet size unchanged");
            report.Add(CheckFeatureWindows());report.Add("successful alpha renders="+renders);return string.Join("\n",report);
        }
        public bool RawReady {get {return rawOk;}}
        public void SuppressNotifications(){notifications=false;}
        public long Total {get {return model.Data.Lifetime.Sum();}}
    }
    static class Program {
        [STAThread] static int Main(string[] args) {
            Native.EnableDpi();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try {
                if(args.Length>0 && args[0]=="--self-test") {Tests.Run(args[1]);return 0;}
                if(args.Length>0 && args[0]=="--nailong-poses") {using(var f=new BuddyForm(new Model(new SaveData{Theme="nailong"}),new Store(""),true))f.NailongPoses(args[1]);return 0;}
                if(args.Length>0 && args[0]=="--nailong-motion-frames") {BuddyForm.NailongMotionFrames(args[1]);return 0;}
                if(args.Length>0 && args[0]=="--nailong-video-frames") {using(var f=new BuddyForm(new Model(new SaveData{Theme="nailong"}),new Store(""),true))f.NailongVideo(args[1]);return 0;}
                if(args.Length>0 && args[0]=="--settings-preview") {using(var f=new BuddyForm(new Model(new SaveData()),new Store(""),true))f.SettingsPreview(args[1],args.Length>2?int.Parse(args[2]):0);return 0;}
                if(args.Length>0 && args[0]=="--feature-poses") {using(var f=new BuddyForm(new Model(new SaveData{Theme=args.Length>2?args[2]:"pixel"}),new Store(""),true))f.FeaturePose(args[1]);return 0;}
                if(args.Length>0 && args[0]=="--companion-showcase") {using(var f=new BuddyForm(new Model(new SaveData{Theme=args.Length>2?args[2]:"pixel"}),new Store(""),true))f.CompanionShowcase(args[1]);return 0;}
                if(args.Length>0 && args[0]=="--catalog") {using(var f=new BuddyForm(new Model(new SaveData()),new Store(""),true))f.Catalog(args[1]);return 0;}
                if(args.Length>0 && args[0]=="--overlay-test") {OverlayTest(args[1],args.Length>2?args[2]:"pixel");return File.ReadAllText(args[1]).StartsWith("PASS:")?0:1;}
                if(args.Length>0 && args[0]=="--motion-frames") {using(var f=new BuddyForm(new Model(new SaveData{Theme=args.Length>2?args[2]:"pixel"}),new Store(""),true))f.MotionFrames(args[1]);return 0;}
                if(args.Length>0 && args[0]=="--motion-showcase") {using(var f=new BuddyForm(new Model(new SaveData{Theme=args.Length>2?args[2]:"pixel"}),new Store(""),true))f.MotionShowcase(args[1]);return 0;}
                if(args.Length>0 && args[0]=="--animation") {var data=new SaveData{Theme=args.Length>2?args[2]:"pixel"};using(var f=new BuddyForm(new Model(data),new Store(""),true))f.Animation(args[1]);return 0;}
                if(args.Length>0 && args[0]=="--screenshot") {
                    Model m=new Model(new SaveData());for(int i=0;i<7;i++){var day=m.Today(DateTime.Today.AddDays(i-6));day.Buttons=new long[]{320+i*61,51+i*17,8,2,1};}
                    m.Data.Lifetime=new long[]{12410,2230,148,84,36};m.Data.Coins=7;m.Data.Owned.AddRange(new[]{"cup","plant"});m.Data.Theme=args.Length>3?args[3]:"pixel";
                    if(args.Length>5&&args[5]=="all")m.Data.Owned=Model.Items.Select(item=>item.Id).ToList();
                    using(BuddyForm f=new BuddyForm(m,new Store(""),true)){f.SetPreviewTab(args.Length>2?args[2]:"overview");f.Screenshot(args[1],args.Length>2&&(args[2]=="compact"||args[2]=="quiet"),args.Length>2&&args[2]=="quiet",args.Length>4?float.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture):1);}return 0;
                }
                if(args.Length>0 && args[0]=="--input-probe") {Probe(args[1],int.Parse(args[2]));return 0;}
                bool created;using(Mutex mutex=new Mutex(true,"Local\\OfficeBuddy-Yapeng-v01",out created)) {
                    if(!created){if(args.Length>1&&args[0]=="--theme"){bool sent=false;for(int i=0;i<10&&!sent;i++){sent=Native.DispatchTheme(Math.Max(0,Array.IndexOf(BuddyForm.Themes,args[1])));if(!sent)Thread.Sleep(100);}if(!sent){MessageBox.Show("请先从旧版搭子的右键菜单正常退出，再启动新版。","上班搭子");return 2;}}else MessageBox.Show("搭子已经在运行。请从右键菜单切换外观。","上班搭子");return 0;}
                    string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OfficeBuddy","save.json");
                    Store s=new Store(path);SaveData data;
                    try {data=s.Load();}catch(Exception ex){MessageBox.Show("无法读取存档："+ex.Message,"上班搭子");return 1;}
                    if(args.Length>1&&args[0]=="--theme")data.Theme=args[1];
                    using(BuddyForm f=new BuddyForm(new Model(data),s,false))Application.Run(f);
                }return 0;
            }catch(Exception ex){if(args.Length>0) File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostic-error.txt"),ex.ToString());else MessageBox.Show(ex.Message,"搭子遇到了一点问题");return 1;}
        }
        static void OverlayTest(string output,string theme) {
            using(var f=new BuddyForm(new Model(new SaveData{Theme=theme}),new Store(Path.Combine(Path.GetDirectoryName(output),"overlay-test-save.json")),false)) {
                f.SuppressNotifications();
                var t=new System.Windows.Forms.Timer{Interval=200};
                t.Tick+=(o,e)=>{t.Stop();try{File.WriteAllText(output,f.CheckOverlayLifecycle());}catch(Exception ex){File.WriteAllText(output,"FAIL: "+ex.ToString());}finally{f.Close();}};
                f.Shown+=(o,e)=>t.Start();Application.Run(f);t.Dispose();
            }
        }
        static void Probe(string output,int seconds) {
            Store s=new Store(Path.Combine(Path.GetDirectoryName(output),"probe-save.json"));
            using(BuddyForm f=new BuddyForm(new Model(new SaveData()),s,false)) {
                var t=new System.Windows.Forms.Timer{Interval=seconds*1000};t.Tick+=(o,e)=>{t.Stop();File.WriteAllText(output,"rawReady="+f.RawReady+"\nclicks="+f.Total);f.Close();};
                f.Shown+=(o,e)=>{f.Hide();t.Start();};Application.Run(f);t.Dispose();
            }
        }
    }
    static class Tests {
        static int count;static void Assert(bool ok,string name) {if(!ok)throw new Exception("FAIL: "+name);count++;}
        static void CheckFixedEyes() {
            using(var neutral=new Bitmap(96,52))using(var tracking=new Bitmap(96,52)) {
                using(var g=Graphics.FromImage(neutral))PixelEye.Draw(g,new RectangleF(0,0,96,52),false,PointF.Empty,Color.FromArgb(101,66,81),Color.FromArgb(83,52,72));
                foreach(var gaze in new[]{new PointF(-3.5f,0),new PointF(3.5f,0),new PointF(0,-1.8f),new PointF(0,1.8f)}) {
                    using(var g=Graphics.FromImage(tracking)){g.Clear(Color.Transparent);PixelEye.Draw(g,new RectangleF(0,0,96,52),false,gaze,Color.FromArgb(101,66,81),Color.FromArgb(83,52,72));}
                    bool changed=false,fixedLid=true,fixedSilhouette=true;
                    for(int y=0;y<52;y++)for(int x=0;x<96;x++) {
                        Color a=neutral.GetPixel(x,y),b=tracking.GetPixel(x,y);
                        changed|=a.ToArgb()!=b.ToArgb();fixedSilhouette&=a.A==b.A;
                        if(y<4)fixedLid&=a.ToArgb()==b.ToArgb();
                    }
                    Assert(changed&&fixedLid&&fixedSilhouette,"pupil moves while eyelid and silhouette stay fixed: "+gaze);
                }
            }
        }
        public static void Run(string output) {
            FeatureChecks.Run(Assert,Path.GetDirectoryName(output));
            using(var f=new BuddyForm(new Model(new SaveData{Theme="nailong"}),new Store(""),true))f.CheckNailong(Assert,Path.GetDirectoryName(output));
            CheckFixedEyes();
            Assert(BuddyForm.ArtworkReady,"body layer and companion assets embedded with transparent corners");
            using(var f=new BuddyForm(new Model(new SaveData()),new Store(""),true))Assert(f.CheckHoverRendering(),"hover shows panel, bridge preserves access, leaving hides immediately, messages stay hidden; all themes and sizes");
            DateTime now=new DateTime(2026,10,7,12,0,0);Model m=new Model(new SaveData());
            Assert(Model.Buttons(0xAAA).Count()==0,"release/wheel events ignored");
            Assert(Model.Buttons(0x155).SequenceEqual(new[]{0,1,2,3,4}),"all five down flags");
            m.Click(0,now);m.Click(0,now);Assert(m.Today(now).Total==2,"double-click is two presses");
            m.Paused=true;m.Click(1,now);Assert(m.Today(now).Total==2,"pause");m.Paused=false;
            for(int i=0;i<98;i++)m.Click(1,now);Assert(m.Data.Coins==1&&m.Today(now).Earned==1,"100 presses award a coin");
            for(int i=0;i<2400;i++)m.Click(i%5,now);Assert(m.Data.Coins==20&&m.Today(now).Earned==20&&m.Today(now).Total==2500,"daily cap counts all presses");
            Assert(m.Buy(Model.Items[0])&&m.Data.Coins==18,"purchase");Assert(!m.Buy(Model.Items[0])&&m.Data.Coins==18,"no duplicate purchase");
            m.TogglePlacement("cup");Assert(m.Data.Owned.Contains("cup")&&!m.IsPlaced("cup")&&m.Data.Coins==18,"hide decoration without losing ownership or spending coins");
            m.TogglePlacement("cup");Assert(m.IsPlaced("cup")&&m.Data.Coins==18,"restore owned decoration without charging again");
            m.TogglePlacement("plant");Assert(!m.IsPlaced("plant"),"cannot place an unowned decoration");
            m.Data.Coins=0;Assert(!m.Buy(Model.Items[1]),"insufficient funds");m.Data.Theme="midnight";m.Data.Zoom=140;m.Data.PetScale=70;
            for(int i=0;i<100;i++)m.Click(2,now.AddDays(1));Assert(m.Today(now.AddDays(1)).Total==100&&m.Data.Coins==1&&m.Data.Lifetime.Sum()==2600,"midnight rollover");
            string folder=Path.Combine(Path.GetDirectoryName(output),"test-store-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);Store s=new Store(Path.Combine(folder,"save.json"));
            s.Save(m.Data);var copy=s.Load();Assert(copy.Lifetime.Sum()==2600&&copy.Owned.Contains("cup"),"serialization round trip");Assert(copy.Theme=="midnight"&&copy.Zoom==140&&copy.PetScale==70,"theme, text and pet size persisted");
            m.TogglePlacement("cup");s.Save(m.Data);copy=s.Load();Assert(copy.Owned.Contains("cup")&&!copy.Equipped.Contains("cup"),"hidden decoration persists after reload");
            s.Save(m.Data);Assert(File.Exists(s.Path+".bak"),"atomic replacement and backup");
            File.WriteAllText(s.Path,"{bad json");copy=s.Load();Assert(copy.Lifetime.Sum()==2600&&s.Notice!=""&&Directory.GetFiles(folder,"*.damaged-*").Length==1,"corrupt save recovery");
            Store empty=new Store(Path.Combine(folder,"new.json"));Assert(empty.Load().Lifetime.Sum()==0,"fresh installation starts at zero");
            File.WriteAllText(empty.Path,"{\"Version\":1,\"Lifetime\":[0,0,0,0,0],\"Days\":[],\"Coins\":0,\"Owned\":[\"lamp\"]}");var legacy=empty.Load();Assert(legacy.Theme=="pixel"&&legacy.Zoom==100&&legacy.PetScale==55,"legacy save migration to small pet");
            Assert(legacy.Equipped.SequenceEqual(new[]{"lamp"}),"old saves preserve existing decoration placement");
            File.WriteAllText(output,"PASS: "+count+" assertions\nPlacement, settings migration, idle moods, reminders, DND, snooze, month statistics, CSV, safe restore, hover, counting, rewards and persistence verified.");
        }
    }
}
