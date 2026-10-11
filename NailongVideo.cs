using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace OfficeBuddy {
    public partial class BuddyForm {
        // Deterministic demo uses isolated preview data, never the user's save or input.
        public void NailongVideo(string folder) {
            Directory.CreateDirectory(folder);model.Data.Theme="nailong";mode="work";
            model.Data.PetScale=70;previewQuiet=true;previewBlink=null;
            for(int i=0;i<1000;i++) {
                double t=i/50.0;previewPhase=t;previewQuiet=true;hits.Clear();mode=t>=13&&t<16?"water":t>=16&&t<18?"rest":"work";
                if(i==200||i==250||i==300) {
                    int button=(i-200)/50;model.Click(button,DateTime.Today,false);nailong.Click(NailongNow,button);
                }
                if(i>=365&&i<370){model.Click(0,DateTime.Today,false);nailong.Click(NailongNow,0);}
                string title=t<4?"上班搭子，来了个笑声客":t<7?"左键抬左手，右键抬右手":t<12?"捧腹大笑，动作连续起来":t<16?"喝口水，缓一缓":t<18?"午休也有自己的姿势":"小小一只，陪你下班";
                string subtitle=t<4?"抽象奶龙 · 无电脑 · 透明桌宠":t<7?"左右对称独立回应 · 中键一起抬手":t<12?"头部与肚子连续起伏 · 脚底稳定":t<16?"提醒与互动沿用上班搭子的设置":t<18?"空闲睡眠 / 回来醒来":"github.com/YapengEcho/office-buddy";
                using(var b=new Bitmap(1280,720,PixelFormat.Format32bppPArgb)) {
                    using(var g=Graphics.FromImage(b)) {
                        g.Clear(C("#F6F3E9"));g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        Txt(g,"OFFICE BUDDY / 抽象搭子",64,42,20,C("#8F7351"),true);
                        Txt(g,title,64,116,42,C("#302D27"),true);Txt(g,subtitle,67,181,23,C("#746B5C"),false);
                        Line(g,C("#DCD2BA"),2,68,239,1212,239);
                        Ellipse(g,C("#EAE1CA"),670,608,390,22);
                        if(t>=3&&t<4) {
                            var saved=g.Save();g.TranslateTransform(710,254);g.ScaleTransform(1.2f,1.2f);
                            previewQuiet=false;DrawCompact(g);g.Restore(saved);
                        }else PetArt(g,606,256,1.95f);
                        Txt(g,t>=7&&t<12?"哈哈哈哈哈哈":t>=4&&t<5?"左键 · 左手回应":t>=5&&t<6?"右键 · 右手回应":t>=6&&t<7?"中键 · 双手回应":"今日演示点击",70,334,28,C("#765E3B"),true);
                        Txt(g,model.Today(DateTime.Today).Total.ToString(),68,383,80,C("#95621A"),true);
                        Txt(g,"靠近时出现操作面板",72,525,23,C("#746B5C"),false);
                        Txt(g,"移开后回到透明桌宠",72,560,23,C("#746B5C"),false);
                        Txt(g,"角色动作来自软件实际渲染 · 无声演示",64,660,18,C("#8F7351"),false);
                        Fill(g,C("#B17C26"),64,694,(float)(1152*(t+0.02)/20),4,2);
                    }
                    b.Save(Path.Combine(folder,i.ToString("D4")+".png"),ImageFormat.Png);
                    if(i==440)b.Save(Path.Combine(folder,"cover.png"),ImageFormat.Png);
                }
            }
        }
    }
}
