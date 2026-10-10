using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace OfficeBuddy {
    [DataContract] public sealed class UserSettings {
        [DataMember] public bool PositionLocked;
        [DataMember] public bool CountOwnClicks=true;
        [DataMember] public bool AutoSleep=true;
        [DataMember] public int IdleSeconds=180;
        [DataMember] public int MotionPercent=100;
        [DataMember] public string Hotkey="Ctrl+Alt+B";
        [DataMember] public bool Reminders;
        [DataMember] public bool Water=true;
        [DataMember] public bool Rest=true;
        [DataMember] public bool Offwork=true;
        [DataMember] public int WaterMinutes=60;
        [DataMember] public int RestMinutes=90;
        [DataMember] public int StartMinute=540;
        [DataMember] public int EndMinute=1080;
        [DataMember] public bool WeekdaysOnly=true;
        [DataMember] public bool DoNotDisturb;
        [OnDeserializing] void Defaults(StreamingContext context){CountOwnClicks=true;AutoSleep=true;IdleSeconds=180;MotionPercent=100;Hotkey="Ctrl+Alt+B";Water=Rest=Offwork=true;WaterMinutes=60;RestMinutes=90;StartMinute=540;EndMinute=1080;WeekdaysOnly=true;}
        public UserSettings Copy(){return (UserSettings)MemberwiseClone();}
        public void Normalize() {
            IdleSeconds=IdleSeconds<30||IdleSeconds>3600?180:IdleSeconds;
            MotionPercent=new[]{0,50,100,150}.Contains(MotionPercent)?MotionPercent:100;
            Hotkey=new[]{"None","Ctrl+Alt+B","Ctrl+Shift+B","Ctrl+Alt+F8"}.Contains(Hotkey)?Hotkey:"Ctrl+Alt+B";
            WaterMinutes=WaterMinutes<5||WaterMinutes>240?60:WaterMinutes;
            RestMinutes=RestMinutes<5||RestMinutes>240?90:RestMinutes;
            if(StartMinute<0||EndMinute>1439||StartMinute>=EndMinute){StartMinute=540;EndMinute=1080;}
        }
    }
    static class PetPlacement {
        public static Point Constrain(Point point,Size size,Rectangle[] areas) {
            if(areas.Length==0)return point;
            Point center=new Point(point.X+size.Width/2,point.Y+size.Height/2);
            Rectangle chosen=areas.OrderBy(a=>Distance(center,a)).First();
            return new Point(Math.Max(chosen.Left,Math.Min(point.X,Math.Max(chosen.Left,chosen.Right-size.Width))),
                Math.Max(chosen.Top,Math.Min(point.Y,Math.Max(chosen.Top,chosen.Bottom-size.Height))));
        }
        static double Distance(Point p,Rectangle a){double x=Math.Max(a.Left-p.X,Math.Max(0,p.X-a.Right)),y=Math.Max(a.Top-p.Y,Math.Max(0,p.Y-a.Bottom));return x*x+y*y;}
    }
    enum CompanionMood { Awake,Yawning,Sleeping,Waking,Happy }
    sealed class CompanionState {
        public CompanionMood Mood=CompanionMood.Awake;
        DateTime happyUntil=DateTime.MinValue,wakingUntil=DateTime.MinValue;
        public void Happy(DateTime now){happyUntil=now.AddSeconds(2.2);Mood=CompanionMood.Happy;}
        public void Advance(UserSettings settings,double idle,DateTime now,bool working) {
            if(!working){Mood=CompanionMood.Awake;return;}
            if(settings.AutoSleep&&idle>=settings.IdleSeconds) {Mood=idle<settings.IdleSeconds+2?CompanionMood.Yawning:CompanionMood.Sleeping;return;}
            if(Mood==CompanionMood.Sleeping||Mood==CompanionMood.Yawning){wakingUntil=now.AddSeconds(1.2);}
            Mood=now<happyUntil?CompanionMood.Happy:now<wakingUntil?CompanionMood.Waking:CompanionMood.Awake;
        }
        public bool Closed {get{return Mood==CompanionMood.Yawning||Mood==CompanionMood.Sleeping;}}
    }
    // Local wall-clock schedules, coalesced reminders, and no catch-up flood after sleep.
    sealed class ReminderEngine {
        public DateTime NextWater,NextRest;
        DateTime lastTick;readonly Dictionary<string,DateTime> snoozes=new Dictionary<string,DateTime>();
        public string LastOffworkDay="";
        public ReminderEngine(UserSettings settings,DateTime now){Reset(settings,now);}
        public void Reset(UserSettings settings,DateTime now){NextWater=now.AddMinutes(settings.WaterMinutes);NextRest=now.AddMinutes(settings.RestMinutes);lastTick=now;snoozes.Clear();}
        public void Snooze(string kind,DateTime now){snoozes[kind]=now.AddMinutes(10);}
        static bool WorkingDay(UserSettings settings,DateTime now){return !settings.WeekdaysOnly||(now.DayOfWeek!=DayOfWeek.Saturday&&now.DayOfWeek!=DayOfWeek.Sunday);}
        public string Tick(UserSettings s,DateTime now,bool active) {
            bool gap=(now-lastTick).TotalMinutes>2||(now-lastTick).TotalSeconds<0;lastTick=now;
            if(!s.Reminders||s.DoNotDisturb||!active||!WorkingDay(s,now)) {
                NextWater=now.AddMinutes(s.WaterMinutes);NextRest=now.AddMinutes(s.RestMinutes);return null;
            }
            if(gap){NextWater=now.AddMinutes(s.WaterMinutes);NextRest=now.AddMinutes(s.RestMinutes);}
            int minute=now.Hour*60+now.Minute;bool work=minute>=s.StartMinute&&minute<s.EndMinute;
            foreach(string kind in new[]{"rest","water","offwork"}) {
                DateTime due;if(snoozes.TryGetValue(kind,out due)&&now>=due) {
                    snoozes.Remove(kind);
                    bool enabled=kind=="water"?s.Water:kind=="rest"?s.Rest:s.Offwork;
                    if(enabled&&due.Date==now.Date&&(work||(kind=="offwork"&&minute>=s.EndMinute&&minute<s.EndMinute+120)))return kind;
                }
            }
            if(s.Offwork&&minute>=s.EndMinute&&minute<s.EndMinute+10&&LastOffworkDay!=now.ToString("yyyy-MM-dd")) {
                LastOffworkDay=now.ToString("yyyy-MM-dd");return "offwork";
            }
            if(!work){NextWater=now.AddMinutes(s.WaterMinutes);NextRest=now.AddMinutes(s.RestMinutes);return null;}
            if(s.Rest&&now>=NextRest&&!snoozes.ContainsKey("rest")) {
                NextRest=now.AddMinutes(s.RestMinutes);NextWater=now.AddMinutes(s.WaterMinutes);return "rest";
            }
            if(s.Water&&now>=NextWater&&!snoozes.ContainsKey("water")){NextWater=now.AddMinutes(s.WaterMinutes);return "water";}
            return null;
        }
    }
    static class DataTools {
        public static long[] Month(SaveData data,DateTime month) {
            DateTime start=new DateTime(month.Year,month.Month,1);var result=new long[DateTime.DaysInMonth(start.Year,start.Month)];
            foreach(Day day in data.Days){DateTime date;if(DateTime.TryParseExact(day.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)&&date.Year==start.Year&&date.Month==start.Month)result[date.Day-1]=day.Total;}
            return result;
        }
        public static void ExportCsv(SaveData data,string path) {
            var text=new StringBuilder("日期,左键,右键,中键,侧键1,侧键2,合计,获得搭子币\r\n");
            foreach(Day day in data.Days.OrderBy(d=>d.Date))text.Append(day.Date).Append(',').Append(string.Join(",",day.Buttons.Select(x=>x.ToString(CultureInfo.InvariantCulture)))).Append(',').Append(day.Total.ToString(CultureInfo.InvariantCulture)).Append(',').Append(day.Earned).Append("\r\n");
            File.WriteAllText(path,text.ToString(),new UTF8Encoding(true));
        }
        public static SaveData Copy(SaveData data) {
            using(var memory=new MemoryStream()){var serializer=new DataContractJsonSerializer(typeof(SaveData));serializer.WriteObject(memory,data);memory.Position=0;return (SaveData)serializer.ReadObject(memory);}
        }
        public static string ApplyRestore(Store store,SaveData current,SaveData incoming,DateTime now) {
            string backup=store.Path+".before-restore-"+now.ToString("yyyyMMddHHmmssfff")+".json";
            new Store(backup).Save(current);store.Save(incoming);return backup;
        }
    }
}
