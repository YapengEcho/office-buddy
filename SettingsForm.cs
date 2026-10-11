using System;
using System.Drawing;
using System.Windows.Forms;

namespace OfficeBuddy {
    sealed class SettingsDraft {
        public UserSettings Preferences;public bool Startup;public string Theme;public int Zoom,PetScale;
    }
    sealed class SettingsForm : Form {
        readonly UserSettings original;
        readonly CheckBox locked,own,sleep,startup,reminders,water,rest,offwork,weekdays,dnd;
        readonly NumericUpDown idle,waterEvery,restEvery;
        readonly ComboBox motion,hotkey,theme,size,zoom;
        readonly DateTimePicker start,end;
        readonly Label status;
        readonly Action<SettingsDraft> apply;
        readonly TabControl tabs;
        public SettingsForm(SaveData data,bool starts,string hotkeyStatus,Action<SettingsDraft> apply,Action export,Action backup,Func<bool> restore,int section) {
            this.apply=apply;original=data.Settings.Copy();
            Text="上班搭子 · 设置";AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(660,492);StartPosition=FormStartPosition.CenterParent;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;ShowInTaskbar=false;
            BackColor=Color.FromArgb(245,245,240);Font=new Font("Microsoft YaHei UI",14,FontStyle.Regular,GraphicsUnit.Pixel);
            Controls.Add(new Label{Text="把陪伴调成你喜欢的样子",Location=new Point(22,18),Size=new Size(600,32),Font=new Font(Font.FontFamily,22,FontStyle.Bold,GraphicsUnit.Pixel)});
            tabs=new TabControl{Location=new Point(22,64),Size=new Size(616,358)};Controls.Add(tabs);
            var daily=Page("日常与桌面");var work=Page("工作提醒");var stats=Page("统计与存档");
            startup=Check(daily,"随 Windows 启动",18,16,starts);locked=Check(daily,"锁定位置，防止误拖",304,16,original.PositionLocked);
            sleep=Check(daily,"空闲后自动睡眠",18,52,original.AutoSleep);LabelAt(daily,"空闲秒数",304,58);idle=Number(daily,428,53,30,3600,original.IdleSeconds);
            LabelAt(daily,"动作幅度",18,102);motion=Choice(daily,128,96,142,new[]{"静止（仍眨眼）","轻柔 · 50%","标准 · 100%","活泼 · 150%"},Array.IndexOf(new[]{0,50,100,150},original.MotionPercent));
            LabelAt(daily,"显隐快捷键",304,102);hotkey=Choice(daily,428,96,154,new[]{"关闭快捷键","Ctrl+Alt+B","Ctrl+Shift+B","Ctrl+Alt+F8"},Array.IndexOf(new[]{"None","Ctrl+Alt+B","Ctrl+Shift+B","Ctrl+Alt+F8"},original.Hotkey));
            LabelAt(daily,"今天的搭子",18,148);theme=Choice(daily,128,142,142,BuddyForm.ThemeNames,Array.IndexOf(BuddyForm.Themes,data.Theme));
            LabelAt(daily,"角色大小",304,148);size=Choice(daily,428,142,154,new[]{"迷你","小号 · 默认","中号"},Array.IndexOf(new[]{40,55,70},data.PetScale));
            LabelAt(daily,"文字大小",18,194);zoom=Choice(daily,128,188,142,new[]{"100%","120%","140%"},Array.IndexOf(new[]{100,120,140},data.Zoom));
            ButtonAt(daily,"找回桌宠位置",304,188,278,()=>{Tag="reset-position";status.Text="保存设置后，桌宠会回到当前屏幕右下角。";});
            // Reset uses the same apply transaction as other settings, and is handled by the owner.
            LabelAt(daily,"位置会自动记住；更换显示器时会找回到可见区域。",18,243,564);
            LabelAt(daily,"快捷键状态："+hotkeyStatus,18,278,564);
            reminders=Check(work,"开启工作提醒",18,16,original.Reminders);dnd=Check(work,"勿扰（暂时不弹出提醒）",304,16,original.DoNotDisturb);
            water=Check(work,"喝水提醒",18,58,original.Water);LabelAt(work,"每隔（分钟）",192,64);waterEvery=Number(work,314,59,5,240,original.WaterMinutes);
            rest=Check(work,"休息提醒",18,101,original.Rest);LabelAt(work,"每隔（分钟）",192,107);restEvery=Number(work,314,102,5,240,original.RestMinutes);
            offwork=Check(work,"下班提醒",18,144,original.Offwork);weekdays=Check(work,"只在周一至周五提醒",304,144,original.WeekdaysOnly);
            LabelAt(work,"工作时间",18,193);start=Time(work,110,186,original.StartMinute);LabelAt(work,"至",259,193,30);end=Time(work,303,186,original.EndMinute);
            LabelAt(work,"提醒只在工作时间内出现，下班提醒在结束时出现。",18,243,564);
            LabelAt(work,"可延后 10 分钟；电脑空闲时不打扰，恢复后不补弹。",18,278,564);
            own=Check(stats,"把搭子自身、设置和工位的点击也计入统计",18,18,original.CountOwnClicks);
            LabelAt(stats,"取消勾选后，只统计其他软件中的鼠标点击。",18,58,564);
            ButtonAt(stats,"导出全部每日数据 CSV",18,108,268,export);ButtonAt(stats,"备份当前存档",314,108,268,backup);
            ButtonAt(stats,"从备份恢复…",18,162,268,()=>{if(restore()){DialogResult=DialogResult.Abort;Close();}});
            LabelAt(stats,"恢复会先备份当前存档，再替换记录；取消不会修改数据。",18,223,564);
            LabelAt(stats,"统计、币和设置保存在本机；不收集键盘或窗口标题。",18,267,564);
            status=new Label{Location=new Point(22,436),Size=new Size(382,40),ForeColor=Color.FromArgb(111,76,52)};Controls.Add(status);
            var cancel=new Button{Text="取消",Location=new Point(424,436),Size=new Size(92,34),DialogResult=DialogResult.Cancel};Controls.Add(cancel);CancelButton=cancel;
            var save=new Button{Text="保存设置",Location=new Point(532,436),Size=new Size(106,34),BackColor=Color.FromArgb(77,113,96),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};save.FlatAppearance.BorderSize=0;Controls.Add(save);AcceptButton=save;
            save.Click+=(o,e)=>Save();tabs.SelectedIndex=Math.Max(0,Math.Min(2,section));
        }
        TabPage Page(string title){var page=new TabPage(title){BackColor=Color.White,UseVisualStyleBackColor=false};tabs.TabPages.Add(page);return page;}
        static void LabelAt(Control c,string text,int x,int y,int w=108){c.Controls.Add(new Label{Text=text,Location=new Point(x,y),Size=new Size(w,26),ForeColor=Color.FromArgb(73,84,76)});}
        static CheckBox Check(Control c,string text,int x,int y,bool value){var b=new CheckBox{Text=text,Location=new Point(x,y),Size=new Size(x==18&&text.Length>20?555:278,29),Checked=value};c.Controls.Add(b);return b;}
        static NumericUpDown Number(Control c,int x,int y,int min,int max,int value){var b=new NumericUpDown{Location=new Point(x,y),Size=new Size(92,30),Minimum=min,Maximum=max,Value=Math.Max(min,Math.Min(max,value))};c.Controls.Add(b);return b;}
        static ComboBox Choice(Control c,int x,int y,int width,string[] choices,int index){var b=new ComboBox{Location=new Point(x,y),Size=new Size(width,32),DropDownStyle=ComboBoxStyle.DropDownList};b.Items.AddRange(choices);b.SelectedIndex=Math.Max(0,index);c.Controls.Add(b);return b;}
        static DateTimePicker Time(Control c,int x,int y,int minute){var b=new DateTimePicker{Location=new Point(x,y),Size=new Size(125,30),Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm",ShowUpDown=true,Value=DateTime.Today.AddMinutes(minute)};c.Controls.Add(b);return b;}
        static void ButtonAt(Control c,string text,int x,int y,int w,Action action){var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(w,35),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(238,242,232)};b.FlatAppearance.BorderColor=Color.FromArgb(219,226,215);b.Click+=(o,e)=>action();c.Controls.Add(b);}
        void Save() {
            int a=start.Value.Hour*60+start.Value.Minute,b=end.Value.Hour*60+end.Value.Minute;
            if(a>=b){status.Text="工作结束时间需要晚于开始时间。";tabs.SelectedIndex=1;return;}
            var s=original.Copy();s.PositionLocked=locked.Checked;s.CountOwnClicks=own.Checked;s.AutoSleep=sleep.Checked;s.IdleSeconds=(int)idle.Value;s.MotionPercent=new[]{0,50,100,150}[motion.SelectedIndex];s.Hotkey=new[]{"None","Ctrl+Alt+B","Ctrl+Shift+B","Ctrl+Alt+F8"}[hotkey.SelectedIndex];
            s.Reminders=reminders.Checked;s.Water=water.Checked;s.Rest=rest.Checked;s.Offwork=offwork.Checked;s.DoNotDisturb=dnd.Checked;s.WeekdaysOnly=weekdays.Checked;s.StartMinute=a;s.EndMinute=b;s.WaterMinutes=(int)waterEvery.Value;s.RestMinutes=(int)restEvery.Value;
            try{apply(new SettingsDraft{Preferences=s,Startup=startup.Checked,Theme=BuddyForm.Themes[theme.SelectedIndex],Zoom=new[]{100,120,140}[zoom.SelectedIndex],PetScale=new[]{40,55,70}[size.SelectedIndex]});DialogResult=DialogResult.OK;Close();}catch(Exception e){status.Text=e.Message;}
        }
        public void Preview(string path){Show();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}Close();}
    }
    sealed class ReminderForm : Form {
        readonly System.Windows.Forms.Timer timeout=new System.Windows.Forms.Timer();
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
        public ReminderForm(string kind,Action snooze,Action accept) {
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;FormBorderStyle=FormBorderStyle.FixedSingle;ControlBox=false;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;ClientSize=new Size(330,148);
            BackColor=Color.FromArgb(252,252,247);Font=new Font("Microsoft YaHei UI",14,FontStyle.Regular,GraphicsUnit.Pixel);
            Controls.Add(new Label{Text=kind=="water"?"搭子提醒 · 喝口水":kind=="rest"?"搭子提醒 · 歇一会儿":"搭子提醒 · 到下班时间啦",Location=new Point(16,15),Size=new Size(302,29),Font=new Font(Font.FontFamily,16,FontStyle.Bold,GraphicsUnit.Pixel)});
            Controls.Add(new Label{Text=kind=="water"?"给自己补点水，慢慢忙。":kind=="rest"?"伸个懒腰，让眼睛离开屏幕片刻。":"今天辛苦啦，记得把时间留给自己。",Location=new Point(16,53),Size=new Size(300,28)});
            var later=new Button{Text="10 分钟后",Location=new Point(16,99),Size=new Size(143,32)};var ok=new Button{Text="知道啦",Location=new Point(174,99),Size=new Size(140,32)};Controls.Add(later);Controls.Add(ok);
            later.Click+=(o,e)=>{snooze();Close();};ok.Click+=(o,e)=>{accept();Close();};timeout.Interval=20000;timeout.Tick+=(o,e)=>Close();Shown+=(o,e)=>timeout.Start();
        }
        protected override void Dispose(bool disposing){if(disposing)timeout.Dispose();base.Dispose(disposing);}
    }
}
