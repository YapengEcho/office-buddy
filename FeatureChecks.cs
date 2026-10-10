using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace OfficeBuddy {
    static class FeatureChecks {
        static string Step(ReminderEngine engine,UserSettings s,DateTime from,DateTime until,bool active=true){string result=null;for(DateTime t=from;t<=until;t=t.AddMinutes(1)){string r=engine.Tick(s,t,active);if(r!=null)result=r;}return result;}
        public static void Run(Action<bool,string> check,string root) {
            DateTime now=new DateTime(2026,10,12,9,0,0);var s=new UserSettings();
            check(!s.Reminders&&!s.DoNotDisturb&&s.CountOwnClicks&&s.AutoSleep,"new reminders are opt-in; existing click behavior preserved");
            Rectangle[] screens={new Rectangle(-1920,0,1920,1040),new Rectangle(0,0,1920,1040)};var size=new Size(320,360);
            check(PetPlacement.Constrain(new Point(-1000,100),size,screens)==new Point(-1000,100),"position preserved on negative-coordinate display");
            check(PetPlacement.Constrain(new Point(-1000,100),size,new[]{screens[1]})==new Point(0,100),"unplugged display position recovered");
            check(PetPlacement.Constrain(new Point(1800,1000),size,screens)==new Point(1600,680),"pet clamped inside working area");
            check(PetPlacement.Constrain(new Point(10,10),new Size(900,900),new[]{new Rectangle(0,0,600,600)})==Point.Empty,"oversized canvas has a visible recovery position");
            var state=new CompanionState();state.Advance(s,181,now,true);check(state.Mood==CompanionMood.Yawning,"idle starts with a yawn");state.Advance(s,183,now.AddSeconds(2),true);check(state.Mood==CompanionMood.Sleeping,"idle transitions to sleeping");
            state.Advance(s,0,now.AddSeconds(3),true);check(state.Mood==CompanionMood.Waking,"activity wakes sleeping companion");state.Advance(s,0,now.AddSeconds(5),true);check(state.Mood==CompanionMood.Awake,"wake animation settles");state.Happy(now.AddSeconds(6));state.Advance(s,0,now.AddSeconds(7),true);check(state.Mood==CompanionMood.Happy,"touch and purchase happiness lasts briefly");
            s.AutoSleep=false;state.Advance(s,900,now.AddSeconds(9),true);check(state.Mood==CompanionMood.Awake,"automatic sleep can be disabled");
            var model=new Model(new SaveData());model.Data.Settings.CountOwnClicks=false;model.Click(0,now,true);check(model.Data.Lifetime.Sum()==0,"own app clicks excluded when configured");model.Click(1,now,false);check(model.Data.Lifetime[1]==1,"other app clicks remain counted");model.Data.Settings.CountOwnClicks=true;model.Click(0,now,true);check(model.Data.Lifetime.Sum()==2,"own app counting can be re-enabled");
            s=new UserSettings{Reminders=true,WaterMinutes=5,RestMinutes=8,WeekdaysOnly=true};var engine=new ReminderEngine(s,now);
            check(Step(engine,s,now,now.AddMinutes(5))=="water","water interval fires during working hours");check(Step(engine,s,now.AddMinutes(6),now.AddMinutes(8))=="rest","rest has independent interval");
            s.Rest=false;engine=new ReminderEngine(s,now);Step(engine,s,now,now.AddMinutes(5));engine.Snooze("water",now.AddMinutes(5));check(Step(engine,s,now.AddMinutes(6),now.AddMinutes(14))==null,"snoozed reminder does not fire early");check(engine.Tick(s,now.AddMinutes(15),true)=="water","ten-minute snooze fires once");
            s.Rest=true;s.RestMinutes=5;engine=new ReminderEngine(s,now);check(Step(engine,s,now,now.AddMinutes(5))=="rest"&&engine.Tick(s,now.AddMinutes(5),true)==null,"simultaneous reminders coalesced");
            s.DoNotDisturb=true;engine=new ReminderEngine(s,now);check(Step(engine,s,now,now.AddMinutes(10))==null,"do not disturb suppresses reminders");s.DoNotDisturb=false;check(engine.Tick(s,now.AddMinutes(11),true)==null,"leaving DND does not replay old reminders");
            engine=new ReminderEngine(s,now);check(Step(engine,s,now,now.AddMinutes(15),false)==null,"idle computer is not interrupted");check(engine.Tick(s,now.AddMinutes(16),true)==null,"return from idle starts fresh reminder intervals");
            engine=new ReminderEngine(s,now);check(engine.Tick(s,now.AddHours(3),true)==null,"resume after clock gap avoids reminder flood");
            check(new ReminderEngine(s,now).Tick(s,now.AddDays(5).AddMinutes(5),true)==null,"weekday reminders skip Saturday");check(new ReminderEngine(s,now).Tick(s,now.AddMinutes(-30),true)==null,"periodic reminders skip outside work hours");
            engine=new ReminderEngine(s,now);DateTime end=now.Date.AddHours(18);check(engine.Tick(s,end,true)=="offwork","offwork reminder fires at work end");check(engine.Tick(s,end.AddMinutes(1),true)==null,"offwork reminder fires once per day");
            var restarted=new ReminderEngine(s,end){LastOffworkDay=engine.LastOffworkDay};check(restarted.Tick(s,end.AddMinutes(2),true)==null,"restart does not repeat offwork reminder");engine.Snooze("offwork",end);check(Step(engine,s,end.AddMinutes(2),end.AddMinutes(10))=="offwork","offwork snooze works after normal work hours");
            string folder=Path.Combine(root,"test-features-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var store=new Store(Path.Combine(folder,"save.json"));
            var data=new SaveData{HasPosition=true,PetX=-900,PetY=140,Coins=4};data.Owned.Add("cup");data.Settings.PositionLocked=true;data.Settings.CountOwnClicks=false;data.Settings.DoNotDisturb=true;data.LastOffworkDay="2026-10-12";
            data.Days.Add(new Day{Date="2028-02-29",Buttons=new long[]{3,2,1,0,0}});data.Days.Add(new Day{Date="2028-03-01",Buttons=new long[]{4,0,0,0,0}});data.Lifetime=new long[]{7,2,1,0,0};store.Save(data);var copy=store.Load();
            check(copy.HasPosition&&copy.PetX==-900&&copy.Settings.PositionLocked&&!copy.Settings.CountOwnClicks&&copy.Settings.DoNotDisturb&&copy.LastOffworkDay=="2026-10-12","position and preferences persist without losing records");
            check(DataTools.Month(copy,new DateTime(2028,2,1)).Length==29&&DataTools.Month(copy,new DateTime(2028,2,1))[28]==6,"monthly statistics handle leap-day records");check(DataTools.Month(copy,new DateTime(2028,3,1)).Sum()==4,"month totals do not mix neighboring months");
            string csv=Path.Combine(folder,"daily.csv");DataTools.ExportCsv(copy,csv);byte[] bytes=File.ReadAllBytes(csv);check(bytes.Take(3).SequenceEqual(new byte[]{239,187,191})&&File.ReadAllLines(csv).Length==3&&File.ReadAllText(csv).Contains("2028-02-29,3,2,1,0,0,6,0"),"CSV exports all five buttons with UTF-8 BOM for Excel");
            var incoming=new SaveData{Coins=9};incoming.Lifetime[0]=42;string before=DataTools.ApplyRestore(store,copy,incoming,now);check(Store.Read(before).Lifetime.Sum()==10&&store.Load().Lifetime[0]==42,"restore creates independent current-data backup before replacing save");
            bool failed=false;using(var locked=new FileStream(store.Path,FileMode.Open,FileAccess.Read,FileShare.Read)){try{DataTools.ApplyRestore(store,incoming,data,now.AddSeconds(1));}catch(IOException){failed=true;}}
            check(failed&&store.Load().Lifetime[0]==42,"failed replacement leaves existing save intact");
            string invalid=Path.Combine(folder,"invalid.json");File.WriteAllText(invalid,"{\"Version\":1,\"Lifetime\":[0,0,0,0,0],\"Days\":[{\"Date\":\"=bad\",\"Buttons\":[0,0,0,0,0],\"Earned\":0}],\"Coins\":0,\"Owned\":[]}");failed=false;try{Store.Read(invalid);}catch(InvalidDataException){failed=true;}check(failed,"restore rejects malformed calendar dates");
            string legacy=Path.Combine(folder,"legacy.json");File.WriteAllText(legacy,"{\"Version\":1,\"Lifetime\":[1,0,0,0,0],\"Days\":[],\"Coins\":5,\"Owned\":[\"lamp\"]}");copy=Store.Read(legacy);check(copy.Coins==5&&copy.Equipped.Contains("lamp")&&copy.Settings.CountOwnClicks&&copy.Settings.AutoSleep&&!copy.Settings.Reminders&&!copy.HasPosition,"old saves migrate safely to new defaults");
            check(StartupEntry.Command("C:\\My App\\OfficeBuddyStudio.exe")=="\"C:\\My App\\OfficeBuddyStudio.exe\"","startup path with spaces is quoted");
        }
    }
}
