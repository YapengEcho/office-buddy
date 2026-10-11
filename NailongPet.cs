using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace OfficeBuddy {
    struct NailongMotion {
        public float Left,Right,Laugh;
        public double LaughTime;
        public bool Moving {get{return Left>0||Right>0||Laugh>0;}}
    }
    sealed class NailongPulse {
        DateTime start=DateTime.MinValue;
        float from;
        public float Value(DateTime now) {
            double t=(now-start).TotalSeconds;
            if(t<0||t>=0.48)return 0;
            if(t<0.10)return from+(1-from)*NailongState.Ease(t/0.10);
            return 1-NailongState.Ease((t-0.10)/0.38);
        }
        public void Trigger(DateTime now){from=Value(now);start=now;}
        public bool Active(DateTime now){double t=(now-start).TotalSeconds;return t>=0&&t<0.48;}
    }
    // Input schedules are separate from rendering: counters do not depend on the animation.
    sealed class NailongState {
        DateTime lastClick=DateTime.MinValue,laughStart=DateTime.MinValue,laughUntil=DateTime.MinValue,nextLaugh=DateTime.MinValue;
        int burst;
        readonly NailongPulse left=new NailongPulse(),right=new NailongPulse();
        internal static float Ease(double t){t=Math.Max(0,Math.Min(1,t));return (float)(t*t*(3-2*t));}
        public bool Laugh(DateTime now,bool manual=false) {
            if(now<laughUntil||(!manual&&now<nextLaugh))return false;
            laughStart=now;laughUntil=now.AddSeconds(3.6);nextLaugh=now.AddSeconds(8);burst=0;return true;
        }
        public bool Click(DateTime now,int button=0) {
            if(button==0||button==2||button==3)left.Trigger(now);
            if(button==1||button==2||button==4)right.Trigger(now);
            double gap=(now-lastClick).TotalSeconds;
            burst=gap<0||gap>1.2?1:burst+1;lastClick=now;
            return burst>=5&&Laugh(now);
        }
        public bool Laughing(DateTime now){return now>=laughStart&&now<laughUntil;}
        public double LaughSeconds(DateTime now){return Math.Max(0,(now-laughStart).TotalSeconds);}
        public bool Animating(DateTime now){return Laughing(now)||left.Active(now)||right.Active(now);}
        public NailongMotion Motion(DateTime now,string mode,CompanionMood mood,float strength) {
            if(strength<=0||mode!="work"||mood==CompanionMood.Sleeping||mood==CompanionMood.Yawning)return new NailongMotion();
            if(Laughing(now)) {
                double t=LaughSeconds(now);
                return new NailongMotion{Laugh=strength*Ease(t/0.24)*Ease((3.6-t)/0.42),LaughTime=t};
            }
            return new NailongMotion{Left=left.Value(now)*strength,Right=right.Value(now)*strength};
        }
        public int Pose(DateTime now,string mode,CompanionMood mood,float strength,bool blink) {
            if(mode=="rest"||mood==CompanionMood.Sleeping)return 10;
            if(mode=="water")return 8;
            if(mood==CompanionMood.Yawning)return 9;
            if(Laughing(now)) {
                // One stable belly-holding drawing; continuous deformation supplies the motion.
                return 4;
            }
            if(left.Active(now)||right.Active(now))return blink?1:0;
            if(mood==CompanionMood.Happy||mood==CompanionMood.Waking)return strength==0?0:3;
            return blink?1:0;
        }
    }
    sealed class NailongAtlas : IDisposable {
        readonly Bitmap[] frames=new Bitmap[12];
        readonly Rectangle[] bounds=new Rectangle[12];
        const int RigSize=256,Foot=244;
        readonly int[][] rig=new int[12][];
        readonly Bitmap[] rigImages=new Bitmap[12];
        readonly int[] warped=new int[RigSize*RigSize];
        readonly float[] handY=new float[RigSize],handX=new float[RigSize];
        readonly Bitmap moving=new Bitmap(RigSize,RigSize,PixelFormat.Format32bppPArgb);
        public NailongAtlas(Bitmap atlas) {
            if(atlas==null||atlas.Width%4!=0||atlas.Height%3!=0)throw new InvalidDataException("奶龙动作图集缺失或网格不完整。");
            int w=atlas.Width/4,h=atlas.Height/3;
            for(int i=0;i<12;i++) {
                frames[i]=atlas.Clone(new Rectangle(i%4*w,i/4*h,w,h),PixelFormat.Format32bppArgb);
                bounds[i]=KeepSilhouette(frames[i]);
            }
        }
        // Only the largest connected character is interactive. Keep the source PNG unchanged.
        static Rectangle KeepSilhouette(Bitmap frame) {
            int w=frame.Width,h=frame.Height,n=w*h;var pixels=new int[n];var seen=new bool[n];var queue=new int[n];int[] best=new int[0];
            var bits=frame.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            try {
                Marshal.Copy(bits.Scan0,pixels,0,n);
                for(int start=0;start<n;start++) {
                    if(seen[start]||(uint)pixels[start]>>24<8)continue;
                    int count=1,read=0;queue[0]=start;seen[start]=true;
                    while(read<count) {
                        int k=queue[read++],x=k%w,y=k/w;
                        for(int yy=Math.Max(0,y-1);yy<=Math.Min(h-1,y+1);yy++)for(int xx=Math.Max(0,x-1);xx<=Math.Min(w-1,x+1);xx++) {
                            int p=yy*w+xx;if(!seen[p]&&(uint)pixels[p]>>24>=8){seen[p]=true;queue[count++]=p;}
                        }
                    }
                    if(count>best.Length){best=new int[count];Array.Copy(queue,best,count);}
                }
                if(best.Length<n/10)throw new InvalidDataException("奶龙动作缺少完整角色。");
                var keep=new bool[n];int minX=w,minY=h,maxX=0,maxY=0;
                foreach(int p in best){keep[p]=true;minX=Math.Min(minX,p%w);minY=Math.Min(minY,p/w);maxX=Math.Max(maxX,p%w);maxY=Math.Max(maxY,p/w);}
                for(int p=0;p<n;p++)if(!keep[p])pixels[p]=0;
                Marshal.Copy(pixels,0,bits.Scan0,n);return Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);
            }finally{frame.UnlockBits(bits);}
        }
        public Bitmap Frame(int pose){return frames[pose];}
        int[] Rig(int pose) {
            if(rig[pose]!=null)return rig[pose];
            var values=new int[RigSize*RigSize];Rectangle b=bounds[pose];float s=205f/frames[0].Height;
            var image=new Bitmap(RigSize,RigSize,PixelFormat.Format32bppPArgb);
            {
                using(var g=Graphics.FromImage(image)) {
                    g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    g.DrawImage(frames[pose],new RectangleF(RigSize/2f-(b.Left+b.Width/2f)*s,Foot-b.Bottom*s,frames[pose].Width*s,frames[pose].Height*s));
                }
                var bits=image.LockBits(new Rectangle(0,0,RigSize,RigSize),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
                try{Marshal.Copy(bits.Scan0,values,0,values.Length);}finally{image.UnlockBits(bits);}
            }
            rigImages[pose]=image;rig[pose]=values;return values;
        }
        internal static PointF WarpSource(float x,float y,NailongMotion m) {
            float sx=x,sy=y;
            if(m.Laugh>0) {
                double beat=m.LaughTime*Math.PI*2*2.25;
                float upper=NailongState.Ease((Foot-y)/180.0);
                float belly=Math.Max(0,1-Math.Abs(y-166)/72f);belly*=belly;
                // Deform head, attached arms and belly together; feet are the fixed anchor.
                sx-=m.Laugh*((float)Math.Sin(beat*0.5)*4*upper+(x-128)*(float)Math.Sin(beat+0.4)*0.025f*belly);
                sy+=m.Laugh*((float)Math.Sin(beat)*6.5f*upper+(float)Math.Sin(beat*2+0.3)*1.2f*belly);
            }else if(m.Left>0||m.Right>0) {
                float left=1-NailongState.Ease((x-55)/50.0),right=1-NailongState.Ease((201-x)/50.0);
                float arm=NailongState.Ease((y-100)/75.0)*(1-NailongState.Ease((y-214)/20.0));
                sy+=24*arm*(m.Left*left+m.Right*right);
                sx+=4*arm*(m.Left*left-m.Right*right);
            }
            return new PointF(sx,sy);
        }
        static int Blend(int a,int b,int c,int d,float fx,float fy) {
            int result=0;
            for(int shift=0;shift<=24;shift+=8) {
                float top=((a>>shift)&255)*(1-fx)+((b>>shift)&255)*fx;
                float bottom=((c>>shift)&255)*(1-fx)+((d>>shift)&255)*fx;
                result|=((int)(top*(1-fy)+bottom*fy+0.5f))<<shift;
            }
            return result;
        }
        void Warp(int pose,NailongMotion motion) {
            int[] source=Rig(pose);
            double beat=motion.LaughTime*Math.PI*2*2.25;
            float sway=(float)Math.Sin(beat*0.5)*4,breathe=(float)Math.Sin(beat)*6.5f;
            float ripple=(float)Math.Sin(beat*2+0.3)*1.2f,bulge=(float)Math.Sin(beat+0.4)*0.025f;
            for(int x=0;x<RigSize;x++) {
                float left=1-NailongState.Ease((x-55)/50.0),right=1-NailongState.Ease((201-x)/50.0);
                handY[x]=24*(motion.Left*left+motion.Right*right);handX[x]=4*(motion.Left*left-motion.Right*right);
            }
            // Row coefficients and hand weights are reused; no trigonometry per pixel.
            for(int y=0;y<RigSize;y++) {
                float upper=NailongState.Ease((Foot-y)/180.0),belly=Math.Max(0,1-Math.Abs(y-166)/72f);belly*=belly;
                float shift=motion.Laugh*sway*upper,scale=motion.Laugh*bulge*belly;
                float dy=motion.Laugh*(breathe*upper+ripple*belly);
                float arm=NailongState.Ease((y-100)/75.0)*(1-NailongState.Ease((y-214)/20.0));
                for(int x=0;x<RigSize;x++) {
                    float sx=motion.Laugh>0?x-shift-(x-128)*scale:x+arm*handX[x];
                    float sy=motion.Laugh>0?y+dy:y+arm*handY[x];
                    if(sx<0||sy<0||sx>=RigSize-1||sy>=RigSize-1){warped[y*RigSize+x]=0;continue;}
                    int ix=(int)sx,iy=(int)sy,p=iy*RigSize+ix;
                    warped[y*RigSize+x]=Blend(source[p],source[p+1],source[p+RigSize],source[p+RigSize+1],sx-ix,sy-iy);
                }
            }
            var bits=moving.LockBits(new Rectangle(0,0,RigSize,RigSize),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
            try{Marshal.Copy(warped,0,bits.Scan0,warped.Length);}finally{moving.UnlockBits(bits);}
        }
        public void Draw(Graphics g,int pose,double phase,float strength,NailongMotion motion) {
            Rectangle b=bounds[pose];var state=g.Save();
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            if(motion.Moving) {
                Warp(pose,motion);g.DrawImage(moving,134-RigSize/2f,216-Foot,RigSize,RigSize);g.Restore(state);return;
            }
            float sway=pose==11?(float)Math.Sin(phase*2)*1.2f:0;
            float lift=pose==3?(float)Math.Abs(Math.Sin(phase*7))*7:pose==2?(float)Math.Abs(Math.Sin(phase*18))*3:0;
            g.TranslateTransform(134,216-lift*strength);g.RotateTransform(sway*strength);
            float s=205f/frames[0].Height,breath=pose==10?(float)Math.Sin(phase*1.5)*0.009f:pose==0?(float)Math.Sin(phase*2)*0.008f:0;
            g.ScaleTransform(1+breath*strength,1-breath*strength);
            if(pose==0||pose==1||pose==4){Rig(pose);g.DrawImage(rigImages[pose],-RigSize/2f,-Foot,RigSize,RigSize);}
            else g.DrawImage(frames[pose],new RectangleF(-(b.Left+b.Width/2f)*s,-b.Bottom*s,frames[pose].Width*s,frames[pose].Height*s));
            g.Restore(state);
        }
        public void Dispose(){moving.Dispose();foreach(var frame in frames)if(frame!=null)frame.Dispose();foreach(var image in rigImages)if(image!=null)image.Dispose();}
    }
    public partial class BuddyForm {
        internal static readonly string[] Themes={"pixel","cream","midnight","nailong"};
        internal static readonly string[] ThemeNames={"薄荷像素","杏桃糯米兔","月光夜猫","抽象奶龙"};
        readonly NailongState nailong=new NailongState();
        static readonly Bitmap nailongSprite=LoadSprite("nailong-atlas");
        NailongAtlas nailongAtlas;
        int nailongPreviewPose=-1;
        DateTime NailongNow {get{return renderOnly?new DateTime(2026,10,11,9,0,0,DateTimeKind.Utc).AddSeconds(previewPhase):DateTime.UtcNow;}}
        public static bool NailongArtworkReady {get{return nailongSprite!=null&&nailongSprite.Width%4==0&&nailongSprite.Height%3==0&&nailongSprite.GetPixel(0,0).A==0;}}
        void LaughNow(){if(Skin!="nailong")return;companion.Happy(DateTime.UtcNow);if(nailong.Laugh(NailongNow,true))Say("哈哈哈，肚子都笑圆了。",4);if(compact)RenderLayer();else Invalidate();}
        void DrawNailong(Graphics g,double phase) {
            if(nailongAtlas==null)nailongAtlas=new NailongAtlas(nailongSprite);
            if(model.IsPlaced("cushion"))Decor(g,"cushion",91,205,1.65f);
            if(model.IsPlaced("lamp"))Decor(g,"lamp",245,163,0.7f);
            if(model.IsPlaced("clock"))Decor(g,"clock",15,182,0.55f);
            bool blink=renderOnly&&previewBlink.HasValue?previewBlink.Value:phase%4.4>4.13;
            int pose=nailongPreviewPose>=0?nailongPreviewPose:nailong.Pose(NailongNow,mode,companion.Mood,MotionStrength,blink);
            NailongMotion motion=nailongPreviewPose>=0?new NailongMotion():nailong.Motion(NailongNow,mode,companion.Mood,MotionStrength);
            if(nailongPreviewPose<0&&!motion.Moving&&pose==0&&phase%20>=12&&phase%20<16)pose=11;
            nailongAtlas.Draw(g,pose,phase,MotionStrength,motion);
            if(pose==10){Txt(g,"z",209,37,18,green,true);Txt(g,"z",224,23,13,green,true);}
            if(model.IsPlaced("plant"))Decor(g,"plant",4,185,0.68f);
            if(model.IsPlaced("cup")&&mode!="water")Decor(g,"cup",230,192,0.62f);
        }
        public void NailongPoses(string folder) {
            Directory.CreateDirectory(folder);model.Data.Theme="nailong";mode="work";previewPhase=0.5;previewQuiet=true;
            for(int i=0;i<12;i++){nailongPreviewPose=i;Screenshot(Path.Combine(folder,i.ToString("D2")+".png"),true,true,2);}
            nailongPreviewPose=-1;
        }
        static Bitmap MotionImage(NailongAtlas atlas,int pose,NailongMotion motion) {
            var image=new Bitmap(280,240,PixelFormat.Format32bppPArgb);
            using(var g=Graphics.FromImage(image))atlas.Draw(g,pose,0,1,motion);
            return image;
        }
        public static void NailongMotionFrames(string folder) {
            Directory.CreateDirectory(folder);DateTime now=new DateTime(2026,10,11);var left=new NailongState();var right=new NailongState();var both=new NailongState();var laugh=new NailongState();
            left.Click(now,0);right.Click(now,1);both.Click(now,2);laugh.Laugh(now,true);
            using(var atlas=new NailongAtlas(nailongSprite)) {
                using(var image=MotionImage(atlas,0,new NailongMotion()))image.Save(Path.Combine(folder,"neutral.png"),ImageFormat.Png);
                foreach(var item in new[]{Tuple.Create("left",left),Tuple.Create("right",right),Tuple.Create("both",both)})
                    using(var image=MotionImage(atlas,0,item.Item2.Motion(now.AddMilliseconds(100),"work",CompanionMood.Awake,1)))image.Save(Path.Combine(folder,item.Item1+".png"),ImageFormat.Png);
                for(int i=0;i<180;i++)using(var image=MotionImage(atlas,4,laugh.Motion(now.AddSeconds(i/50.0),"work",CompanionMood.Awake,1)))image.Save(Path.Combine(folder,"laugh-"+i.ToString("D3")+".png"),ImageFormat.Png);
                var watch=System.Diagnostics.Stopwatch.StartNew();
                using(var image=new Bitmap(280,240))using(var g=Graphics.FromImage(image))for(int i=0;i<100;i++){g.Clear(Color.Transparent);atlas.Draw(g,4,0,1,laugh.Motion(now.AddSeconds(0.5+i/100.0),"work",CompanionMood.Awake,1));}
                watch.Stop();File.WriteAllText(Path.Combine(folder,"timing.txt"),"Native 256px bilinear rig: "+(watch.Elapsed.TotalMilliseconds/100).ToString("F2")+" ms per draw (100 warm draws)");
            }
        }
        internal void CheckNailong(Action<bool,string> check,string root) {
            DateTime now=new DateTime(2026,10,11,9,0,0);var state=new NailongState();
            check(!state.Click(now)&&state.Motion(now.AddMilliseconds(100),"work",CompanionMood.Awake,1).Left==1,"single left click lifts left arm without laughing");
            for(int i=1;i<5;i++)state.Click(now.AddMilliseconds(i*100));
            check(state.Laughing(now.AddSeconds(1)),"five rapid clicks trigger belly laugh");
            check(state.Pose(now.AddSeconds(1),"rest",CompanionMood.Awake,1,false)==10&&state.Pose(now.AddSeconds(1),"water",CompanionMood.Awake,1,false)==8,"rest and water take priority over laughter");
            check(state.Pose(now.AddSeconds(1),"work",CompanionMood.Sleeping,1,false)==10,"automatic sleep uses actual closed-eye artwork");
            check(state.Pose(now.AddSeconds(1),"work",CompanionMood.Awake,0,false)==4&&!state.Motion(now.AddSeconds(1),"work",CompanionMood.Awake,0).Moving,"zero motion freezes laugh without losing its expression");
            var poses=Enumerable.Range(0,36).Select(i=>state.Pose(now.AddSeconds(0.4+i*0.1),"work",CompanionMood.Awake,1,false)).Distinct().ToArray();
            check(poses.Length==1&&poses[0]==4,"laughter keeps one stable silhouette instead of jumping drawings");
            var leftState=new NailongState();leftState.Click(now,0);var lm=leftState.Motion(now.AddMilliseconds(100),"work",CompanionMood.Awake,1);
            var rightState=new NailongState();rightState.Click(now,1);var rm=rightState.Motion(now.AddMilliseconds(100),"work",CompanionMood.Awake,1);
            check(lm.Left==1&&lm.Right==0&&rm.Right==1&&rm.Left==0,"left and right input drive independent opposite arms");
            PointF lp=NailongAtlas.WarpSource(64,190,lm),rp=NailongAtlas.WarpSource(192,190,rm);
            check(Math.Abs(lp.Y-rp.Y)<0.0001&&Math.Abs(lp.X+rp.X-256)<0.0001&&lp.Y>190,"arm movement has equal mirrored direction and amplitude");
            leftState.Click(now.AddMilliseconds(80),0);float beforeRepeat=leftState.Motion(now.AddMilliseconds(80),"work",CompanionMood.Awake,1).Left;
            check(beforeRepeat>0.89&&Math.Abs(leftState.Motion(now.AddMilliseconds(81),"work",CompanionMood.Awake,1).Left-beforeRepeat)<0.001,"repeated input starts from current arm position without snapping back");
            var middle=new NailongState();middle.Click(now,2);var mm=middle.Motion(now.AddMilliseconds(100),"work",CompanionMood.Awake,1);
            check(mm.Left==1&&mm.Right==1,"middle button raises both arms together");
            float maxDelta=0;for(int i=401;i<3980;i++) {
                var a=state.Motion(now.AddMilliseconds(i),"work",CompanionMood.Awake,1);var b=state.Motion(now.AddMilliseconds(i-1),"work",CompanionMood.Awake,1);
                PointF pa=NailongAtlas.WarpSource(128,65,a),pb=NailongAtlas.WarpSource(128,65,b);maxDelta=Math.Max(maxDelta,Math.Abs(pa.Y-pb.Y)+Math.Abs(pa.X-pb.X));
            }
            check(maxDelta<0.15,"laugh coordinates remain continuous at former frame-switch boundaries");
            check(state.Motion(now.AddMilliseconds(400),"work",CompanionMood.Awake,1).Laugh==0&&state.Motion(now.AddMilliseconds(3999),"work",CompanionMood.Awake,1).Laugh<0.0001,"laugh deforms with smooth entry and recovery envelopes");
            check(!state.Laugh(now.AddSeconds(5))&&state.Laugh(now.AddSeconds(9)),"continuous input respects laugh cooldown");
            check(state.Pose(now.AddSeconds(14),"work",CompanionMood.Awake,1,true)==1,"after laughter pet settles and blinks");
            var slow=new NailongState();for(int i=0;i<8;i++)slow.Click(now.AddSeconds(i*2));check(!slow.Laughing(now.AddSeconds(14)),"slow clicks do not unexpectedly trigger laughter");
            check(slow.Laugh(now.AddSeconds(15),true),"manual belly laugh works independently of click counting");
            var data=new SaveData{Theme="nailong"};var file=Path.Combine(root,"nailong-restore.json");new Store(file).Save(data);check(Store.Read(file).Theme=="nailong","new character survives restart and restore");
            check(NailongArtworkReady,"twelve-frame transparent atlas embedded");
            using(var atlas=new NailongAtlas(nailongSprite)){check(Enumerable.Range(0,12).All(p=>atlas.Frame(p).GetPixel(0,0).A==0),"all twelve poses retain transparent corners");}
            using(var atlas=new NailongAtlas(nailongSprite))using(var idle=MotionImage(atlas,0,new NailongMotion()))using(var l=MotionImage(atlas,0,lm))using(var r=MotionImage(atlas,0,rm)) {
                int changedL=0,changedR=0;bool leftQuiet=true,rightQuiet=true,feet=true;
                for(int y=0;y<240;y++)for(int x=0;x<280;x++) {
                    int original=idle.GetPixel(x,y).ToArgb();bool cl=original!=l.GetPixel(x,y).ToArgb(),cr=original!=r.GetPixel(x,y).ToArgb();
                    if(cl)changedL++;if(cr)changedR++;if(x>150&&cl)rightQuiet=false;if(x<112&&cr)leftQuiet=false;if(y>=212&&(cl||cr))feet=false;
                }
                check(changedL>100&&changedR>100&&rightQuiet&&leftQuiet,"rendered clicks change only the requested arm region");
                check(feet&&l.GetPixel(0,0).A==0&&r.GetPixel(0,0).A==0,"click deformation preserves feet and transparent corners");
            }
            long before=model.Data.Lifetime.Sum();model.Paused=true;previewPhase=20;Receive(0);
            check(model.Data.Lifetime.Sum()==before&&!nailong.Laughing(NailongNow),"paused input neither counts nor triggers laugh");
            model.Paused=false;mode="work";for(int i=0;i<5;i++){previewPhase=21+i*0.1;Receive(i);}
            check(model.Data.Lifetime.Sum()==before+5&&nailong.Laughing(NailongNow),"real input path counts all five buttons once and triggers laugh");
            mode="rest";previewPhase=22;Receive(0);
            check(model.Data.Lifetime.Sum()==before+6&&nailong.Pose(NailongNow,mode,CompanionMood.Awake,1,false)==10,"rest still counts input while suppressing laugh artwork");
            mode="work";previewPhase=27;
            check(!nailong.Laughing(NailongNow),"input path laughter expires independently of counters");
        }
    }
}
