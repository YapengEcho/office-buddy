using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OfficeBuddy {
    static class FeatureNative {
        [StructLayout(LayoutKind.Sequential)] struct LastInput {public uint Size,Tick;}
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput input);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd,int id);
        public static double IdleSeconds(){var input=new LastInput{Size=(uint)Marshal.SizeOf(typeof(LastInput))};if(!GetLastInputInfo(ref input))return 0;uint elapsed=unchecked((uint)Environment.TickCount-input.Tick);return Math.Min(elapsed/1000.0,86400);}
    }
    static class StartupEntry {
        const string Key="Software\\Microsoft\\Windows\\CurrentVersion\\Run",Name="OfficeBuddy";
        public static string Command(string path){return "\""+path+"\"";}
        public static bool Enabled {get{using(var key=Registry.CurrentUser.OpenSubKey(Key)){return key!=null&&string.Equals(key.GetValue(Name) as string,Command(Application.ExecutablePath),StringComparison.OrdinalIgnoreCase);}}}
        public static void Set(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey(Key)){if(enabled)key.SetValue(Name,Command(Application.ExecutablePath));else key.DeleteValue(Name,false);}}
    }
    public partial class BuddyForm {
        readonly CompanionState companion=new CompanionState();
        ReminderEngine reminders;
        ReminderForm reminderWindow;
        bool justDragged,featureDialog,hotkeyRegistered;
        DateTime nextFeatureTick=DateTime.MinValue,selectedMonth=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1);
        string hotkeyStatus="未注册";
        UserSettings Preferences {get{return model.Data.Settings;}}
        float MotionStrength {get{return Preferences.MotionPercent/100f;}}
        bool AutoClosed {get{return companion.Closed;}}
        Rectangle[] WorkingAreas {get{return Screen.AllScreens.Select(s=>s.WorkingArea).ToArray();}}
        void InitializeFeatures(){model.Data.Settings=model.Data.Settings??new UserSettings();Preferences.Normalize();reminders=new ReminderEngine(Preferences,DateTime.Now){LastOffworkDay=model.Data.LastOffworkDay??""};}
        void SavePetPosition(){if(renderOnly||!compact)return;model.Data.HasPosition=true;model.Data.PetX=Left;model.Data.PetY=Top;dirty=true;}
        void RestorePetPosition() {
            Rectangle area=Screen.FromControl(this).WorkingArea;
            Point desired=model.Data.HasPosition?new Point(model.Data.PetX,model.Data.PetY):new Point(area.Right-Width-30,area.Bottom-Height-16);
            Location=PetPlacement.Constrain(desired,ClientSize,WorkingAreas);
        }
        void RecoverPosition(){Rectangle a=Screen.FromControl(this).WorkingArea;model.Data.HasPosition=true;model.Data.PetX=Math.Max(a.Left,a.Right-(int)(320*Native.Dpi(Handle)*model.Data.Zoom/100f)-30);model.Data.PetY=Math.Max(a.Top,a.Bottom-(int)(360*Native.Dpi(Handle)*model.Data.Zoom/100f)-16);if(compact){RestorePetPosition();RenderLayer();}dirty=true;Persist();}
        void RegisterShortcut() {
            FeatureNative.UnregisterHotKey(Handle,0x4F42);hotkeyRegistered=false;
            string key=Preferences.Hotkey;if(key=="None"){hotkeyStatus="已关闭";return;}
            uint modifiers=key=="Ctrl+Shift+B"?6u:3u,code=key=="Ctrl+Alt+F8"?0x77u:0x42u;
            hotkeyRegistered=FeatureNative.RegisterHotKey(Handle,0x4F42,modifiers|0x4000,code);
            hotkeyStatus=hotkeyRegistered?key+" · 可用":key+" · 被其他应用占用，请换一组";
        }
        protected override void OnHandleDestroyed(EventArgs e){if(hotkeyRegistered){FeatureNative.UnregisterHotKey(Handle,0x4F42);hotkeyRegistered=false;}base.OnHandleDestroyed(e);}
        void ToggleVisibility(){if(Visible)Hide();else{SetThrough(false);ShowBuddy();}}
        bool IsOwnClick(){IntPtr window=Native.WindowFromPoint(new Native.Point(Cursor.Position.X,Cursor.Position.Y));uint pid;Native.GetWindowThreadProcessId(window,out pid);return pid==(uint)System.Diagnostics.Process.GetCurrentProcess().Id;}
        void FeatureTick() {
            DateTime now=DateTime.UtcNow;if(now<nextFeatureTick&&(nextFeatureTick-now).TotalSeconds<1)return;nextFeatureTick=now.AddMilliseconds(200);
            double idle=FeatureNative.IdleSeconds();companion.Advance(Preferences,idle,now,mode=="work");
            if(compact&&!dragging){Point clamped=PetPlacement.Constrain(Location,ClientSize,WorkingAreas);if(clamped!=Location){Location=clamped;SavePetPosition();}}
            if(featureDialog||reminderWindow!=null)return;
            string kind=reminders.Tick(Preferences,DateTime.Now,idle<300);
            if(kind!=null) {
                model.Data.LastOffworkDay=reminders.LastOffworkDay;dirty=true;
                ShowReminder(kind);
            }
        }
        void ShowReminder(string kind) {
            if(renderOnly||!notifications)return;
            reminderWindow=new ReminderForm(kind,()=>reminders.Snooze(kind,DateTime.Now),()=>{if(kind=="water")Mode("water");else if(kind=="rest")Mode("rest");});
            Rectangle a=Screen.FromControl(this).WorkingArea;
            reminderWindow.Location=PetPlacement.Constrain(new Point(Left+Width-reminderWindow.Width,Top+Height-reminderWindow.Height-(int)(120*Native.Dpi(Handle))),reminderWindow.Size,new[]{a});
            reminderWindow.FormClosed+=(o,e)=>{reminderWindow.Dispose();reminderWindow=null;};reminderWindow.Show();
        }
        void OpenSettings(int section) {
            if(featureDialog)return;featureDialog=true;
            try {
                using(var dialog=new SettingsForm(model.Data,StartupEntry.Enabled,hotkeyStatus,ApplySettings,ExportCsv,BackupData,RestoreData,section)) {
                    if(dialog.ShowDialog(this)==DialogResult.OK&&Equals(dialog.Tag,"reset-position"))RecoverPosition();
                }
            }finally{featureDialog=false;}
        }
        void ApplySettings(SettingsDraft draft) {
            // Only an explicit checkbox change writes the per-user startup entry.
            if(draft.Startup!=StartupEntry.Enabled)StartupEntry.Set(draft.Startup);
            model.Data.Settings=draft.Preferences;model.Data.Theme=draft.Theme;model.Data.Zoom=draft.Zoom;model.Data.PetScale=draft.PetScale;
            ApplyPalette();reminders.Reset(Preferences,DateTime.Now);RegisterShortcut();ResizeDpi(Native.Dpi(Handle));
            if(compact){RestorePetPosition();SavePetPosition();}dirty=true;
            if(!Persist())throw new IOException("设置保存失败："+saveError);
            if(reminderWindow!=null&&(!Preferences.Reminders||Preferences.DoNotDisturb))reminderWindow.Close();
            Say("设置好啦，按你的节奏来。",4);if(compact)RenderLayer();else Invalidate();
        }
        void ToggleDnd(){Preferences.DoNotDisturb=!Preferences.DoNotDisturb;reminders.Reset(Preferences,DateTime.Now);if(reminderWindow!=null)reminderWindow.Close();dirty=true;Persist();Say(Preferences.DoNotDisturb?"勿扰已开启，安静陪着。":"提醒已恢复。",4);}
        void ExportCsv() {
            using(var dialog=new SaveFileDialog{Title="导出每日点击记录",Filter="CSV 表格|*.csv",FileName="上班搭子-每日记录-"+DateTime.Now.ToString("yyyyMMdd")+".csv"}) {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try{DataTools.ExportCsv(model.Data,dialog.FileName);MessageBox.Show(this,"已导出全部每日记录，可用 Excel 打开。","导出完成");}catch(Exception e){MessageBox.Show(this,"导出失败："+e.Message,"上班搭子");}
            }
        }
        void BackupData() {
            using(var dialog=new SaveFileDialog{Title="备份点击记录、装饰和设置",Filter="JSON 存档|*.json",FileName="上班搭子-备份-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json"}) {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try{if(string.Equals(Path.GetFullPath(dialog.FileName),Path.GetFullPath(store.Path),StringComparison.OrdinalIgnoreCase))throw new IOException("请选择存档目录之外的备份位置。");new Store(dialog.FileName).Save(model.Data);MessageBox.Show(this,"备份已保存。","上班搭子");}catch(Exception e){MessageBox.Show(this,"备份失败："+e.Message,"上班搭子");}
            }
        }
        bool RestoreData() {
            using(var dialog=new OpenFileDialog{Title="选择要恢复的存档",Filter="JSON 存档|*.json"}) {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return false;
                try {
                    SaveData incoming=Store.Read(dialog.FileName);
                    if(MessageBox.Show(this,"将恢复 "+incoming.Lifetime.Sum().ToString("N0")+" 次累计点击、"+incoming.Coins+" 枚币及外观设置。\n当前存档会先另存为恢复前备份。是否继续？","恢复存档",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return false;
                    string backup=DataTools.ApplyRestore(store,model.Data,incoming,DateTime.Now);
                    // Write the validated replacement before swapping the live model.
                    model.Data=incoming;ApplyPalette();InitializeFeatures();RegisterShortcut();ResizeDpi(Native.Dpi(Handle));if(compact)RestorePetPosition();dirty=false;
                    if(reminderWindow!=null)reminderWindow.Close();Say("存档已恢复。",4);if(compact)RenderLayer();else Invalidate();
                    MessageBox.Show(this,"恢复完成。恢复前备份：\n"+backup,"上班搭子");return true;
                }catch(Exception e){MessageBox.Show(this,"恢复失败，原记录继续保留："+e.Message,"上班搭子");return false;}
            }
        }
        void DrawSettingsPage(Graphics g) {
            Txt(g,"按你的节奏来",24,98,22,ink,true);Txt(g,"位置、动作、提醒和数据，都在这里调整。",25,135,14,muted,false);
            Panel(g,24,178,326,145);Txt(g,"日常与桌面",44,197,18,ink,true);Txt(g,Preferences.PositionLocked?"位置已锁定 · 自动记住位置":"位置可拖动 · 自动记住位置",44,231,13,muted,false);Button(g,"调整桌面与动作",44,272,286,32,()=>OpenSettings(0),false);
            Panel(g,370,178,326,145);Txt(g,"工作提醒",390,197,18,ink,true);Txt(g,Preferences.DoNotDisturb?"勿扰中 · 提醒已静音":Preferences.Reminders?"已开启 · 喝水 / 休息 / 下班":"默认关闭 · 需要时再开启",390,231,13,muted,false);Button(g,"设置工作提醒",390,272,286,32,()=>OpenSettings(1),false);
            Panel(g,24,344,672,154);Txt(g,"统计与存档",44,362,18,ink,true);Txt(g,Preferences.CountOwnClicks?"当前统计搭子自身的点击":"当前不统计搭子自身的点击",44,397,13,muted,false);
            Button(g,"月度趋势",44,440,145,34,()=>{activeTab="month";Invalidate();},false);Button(g,"导出 CSV",203,440,145,34,ExportCsv,false);Button(g,"备份 / 恢复",362,440,145,34,()=>OpenSettings(2),false);Button(g,"统计选项",521,440,155,34,()=>OpenSettings(2),false);
        }
        void DrawMonth(Graphics g) {
            long[] values=DataTools.Month(model.Data,selectedMonth);long total=values.Sum();
            Txt(g,selectedMonth.ToString("yyyy 年 M 月"),24,100,22,ink,true);
            Button(g,"‹",300,95,42,34,()=>{selectedMonth=selectedMonth.AddMonths(-1);Invalidate();},false);
            Button(g,"›",352,95,42,34,()=>{if(selectedMonth<new DateTime(DateTime.Today.Year,DateTime.Today.Month,1))selectedMonth=selectedMonth.AddMonths(1);Invalidate();},false);
            Button(g,"本月",404,95,74,34,()=>{selectedMonth=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1);Invalidate();},false);
            Button(g,"导出 CSV",574,95,122,34,ExportCsv,true);
            Panel(g,24,149,672,106);Txt(g,"本月点击",44,168,14,muted,false);Txt(g,total.ToString("N0"),42,193,32,ink,true);
            Txt(g,"有记录的天数",350,168,14,muted,false);Txt(g,values.Count(v=>v>0)+" 天",350,201,20,green,true);Txt(g,"最高一天",536,168,14,muted,false);Txt(g,values.Max().ToString("N0"),536,201,20,green,true);
            Panel(g,24,272,672,226);Txt(g,"每日点击",44,290,18,ink,true);
            float x=46,y=342,w=628,h=114,step=w/values.Length;long max=Math.Max(1,values.Max());Line(g,C("#E2E8DD"),1,x,y+h,x+w,y+h);
            for(int i=0;i<values.Length;i++){float height=values[i]/(float)max*(h-10),xx=x+i*step+2;if(values[i]>0)Fill(g,green,xx,y+h-Math.Max(3,height),Math.Max(4,step-5),Math.Max(3,height),2);if(i==0||(i+1)%5==0||i==values.Length-1)Center(g,(i+1).ToString(),new RectangleF(xx-7,y+h+5,step+10,17),10,muted,false);}
            if(total==0)Center(g,"这个月还没有点击记录。",new RectangleF(44,366,630,50),16,muted,false);
        }
        void DrawEmotion(Graphics g) {
            if(companion.Mood==CompanionMood.Happy) {
                Color c=C("#D69791");foreach(float x in new[]{224f,241f}){Block(g,c,x,32,4,4);Block(g,c,x+6,32,4,4);Block(g,c,x-2,36,14,4);Block(g,c,x,40,10,4);Block(g,c,x+3,44,4,3);}
            } else if(companion.Mood==CompanionMood.Yawning){Txt(g,"…",220,34,22,green,true);}
            else if(companion.Mood==CompanionMood.Waking){Txt(g,"!",228,33,22,green,true);}
        }
        void DrawYawn(Graphics g) {
            if(companion.Mood!=CompanionMood.Yawning)return;
            float x=Skin=="midnight"?149:Skin=="cream"?143:143,y=Skin=="midnight"?139:Skin=="cream"?145:143;
            Block(g,C("#54313D"),x-2,y-3,5,6);Block(g,C("#D99B9A"),x-1,y+1,3,1);
        }
        public void SettingsPreview(string path,int section){using(var dialog=new SettingsForm(model.Data,false,"Ctrl+Alt+B · 可用",d=>{},()=>{},()=>{},()=>false,section))dialog.Preview(path);}
        public void FeaturePose(string folder) {
            Directory.CreateDirectory(folder);previewPhase=0.5;previewQuiet=true;previewBlink=false;
            foreach(CompanionMood pose in new[]{CompanionMood.Awake,CompanionMood.Yawning,CompanionMood.Sleeping,CompanionMood.Waking,CompanionMood.Happy}){companion.Mood=pose;Screenshot(Path.Combine(folder,pose.ToString()+".png"),true,true,2);}
        }
        public void CompanionShowcase(string folder) {
            Directory.CreateDirectory(folder);previewBlink=false;mode="work";
            for(int i=0;i<160;i++) {
                previewPhase=0.5+i/20.0;companion.Mood=i<20?CompanionMood.Awake:i<40?CompanionMood.Yawning:i<100?CompanionMood.Sleeping:i<125?CompanionMood.Waking:CompanionMood.Happy;
                string label=companion.Mood==CompanionMood.Awake?"安静陪着":companion.Mood==CompanionMood.Yawning?"空闲 · 打个哈欠":companion.Mood==CompanionMood.Sleeping?"空闲 · 小睡一会儿":companion.Mood==CompanionMood.Waking?"欢迎回来": "摸摸头 · 元气补满";
                using(var bitmap=new Bitmap(320,260,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) {
                    using(var g=Graphics.FromImage(bitmap)){g.Clear(C("#F5F5F0"));g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;Center(g,SkinName,new RectangleF(0,8,320,28),15,ink,true);PetArt(g,53,39,0.8f);Center(g,label,new RectangleF(0,225,320,25),14,green,true);}bitmap.Save(Path.Combine(folder,i.ToString("D3")+".png"),System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }
        string CheckFeatureWindows() {
            Rectangle a=Screen.FromControl(this).WorkingArea;Point saved=PetPlacement.Constrain(new Point(a.Left+120,a.Top+90),ClientSize,WorkingAreas);
            Location=saved;SavePetPosition();ToggleCompact();ToggleCompact();
            if(Location!=saved)throw new Exception("pet position changed after dashboard return");
            Preferences.PositionLocked=true;OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,(int)(160*scale),(int)(295*scale),0));
            if(dragging)throw new Exception("locked pet still starts dragging");OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,160,295,0));Preferences.PositionLocked=false;
            if(hotkeyRegistered){Native.SendMessage(Handle,0x312,new IntPtr(0x4F42),IntPtr.Zero);if(Visible)throw new Exception("shortcut did not hide pet");Native.SendMessage(Handle,0x312,new IntPtr(0x4F42),IntPtr.Zero);if(!Visible||through)throw new Exception("shortcut did not restore interactive pet");}
            using(var dialog=new SettingsForm(model.Data,false,hotkeyStatus,d=>{},()=>{},()=>{},()=>false,0)) {
                dialog.Show(this);Application.DoEvents();if(dialog.ClientSize.Width<600||dialog.Controls.Count<4)throw new Exception("settings form did not render");dialog.Close();
            }
            companion.Advance(Preferences,Preferences.IdleSeconds+3,DateTime.UtcNow,true);RenderLayer();if(!AutoClosed||!layerOk)throw new Exception("sleeping pet render failed");companion.Advance(Preferences,0,DateTime.UtcNow,true);RenderLayer();if(companion.Mood!=CompanionMood.Waking||!layerOk)throw new Exception("wake render failed");
            companion.Happy(DateTime.UtcNow);RenderLayer();if(!layerOk)throw new Exception("happy pet render failed");
            return "PASS: position retained after dashboard round trip; position lock prevents drag\nPASS: shortcut lifecycle ("+hotkeyStatus+") and settings dialog render\nPASS: sleeping, waking and happy states render on native layered window";
        }
    }
}
