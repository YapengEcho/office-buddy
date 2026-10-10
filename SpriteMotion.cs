using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace OfficeBuddy {
    // Composite original eye/paw pixels over the generated body. Local inverse
    // maps keep sleeves connected as each independently controlled paw moves.
    sealed class SpriteMotion : IDisposable {
        sealed class Part : IDisposable {
            public Bitmap Ink;public Rectangle Bounds;
            public void Dispose(){Ink.Dispose();}
        }
        readonly int size;
        readonly Bitmap neutral,frame;
        readonly int[] pixels,pose;
        readonly RectangleF leftEye,rightEye,leftHand,rightHand;
        readonly float handSplit;
        readonly Part[] parts;

        public SpriteMotion(Bitmap source,Bitmap cleanBody,int size,Rectangle leftEye,Rectangle rightEye,Rectangle leftHand,Rectangle rightHand) {
            this.size=size;
            neutral=new Bitmap(size,size,PixelFormat.Format32bppArgb);
            frame=new Bitmap(size,size,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(neutral)) {
                g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode=PixelOffsetMode.Half;g.SmoothingMode=SmoothingMode.None;
                g.DrawImage(cleanBody,new Rectangle(0,0,size,size));
            }
            pixels=new int[size*size];pose=new int[pixels.Length];
            var data=neutral.LockBits(new Rectangle(0,0,size,size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{Marshal.Copy(data.Scan0,pixels,0,pixels.Length);}finally{neutral.UnlockBits(data);}
            // Almost invisible exported dust must not intercept desktop clicks.
            for(int i=0;i<pixels.Length;i++)if((uint)pixels[i]>>24<8)pixels[i]=0;
            Write(neutral,pixels);
            this.leftEye=Scale(leftEye,source.Size);this.rightEye=Scale(rightEye,source.Size);
            this.leftHand=Scale(leftHand,source.Size);this.rightHand=Scale(rightHand,source.Size);
            handSplit=(this.leftHand.Right+this.rightHand.Left)/2;
            using(var original=new Bitmap(size,size,PixelFormat.Format32bppArgb)) {
                using(var g=Graphics.FromImage(original)){g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(source,new Rectangle(0,0,size,size));}
                var colors=new int[size*size];var originalData=original.LockBits(new Rectangle(0,0,size,size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                try{Marshal.Copy(originalData.Scan0,colors,0,colors.Length);}finally{original.UnlockBits(originalData);}
                parts=new[]{Extract(colors,this.leftEye,true),Extract(colors,this.rightEye,true),Extract(colors,this.leftHand,false),Extract(colors,this.rightHand,false)};
            }
            this.leftHand=parts[2].Bounds;this.rightHand=parts[3].Bounds;
            handSplit=(this.leftHand.Right+this.rightHand.Left)/2;
        }

        Part Extract(int[] original,RectangleF area,bool eye) {
            Rectangle bounds=Rectangle.Intersect(new Rectangle(0,0,size,size),Rectangle.FromLTRB((int)Math.Floor(area.Left),(int)Math.Floor(area.Top),(int)Math.Ceiling(area.Right),(int)Math.Ceiling(area.Bottom)));
            int w=bounds.Width,h=bounds.Height;bool[] mask=new bool[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                uint color=(uint)original[(y+bounds.Y)*size+x+bounds.X];int r=(int)(color>>16)&255,g=(int)(color>>8)&255,b=(int)color&255;
                mask[y*w+x]=(color>>24)>=8&&(eye?r<190&&g<175&&b<151:r>210&&g>190&&b>155);
            }
            if(eye) {
                // Preserve highlights enclosed by the dark eye silhouette.
                for(int y=0;y<h;y++){int first=w,last=-1;for(int x=0;x<w;x++)if(mask[y*w+x]){first=Math.Min(first,x);last=x;}for(int x=first;x<=last;x++)mask[y*w+x]=true;}
            } else {
                var pale=(bool[])mask.Clone();
                for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                    bool near=false;for(int yy=Math.Max(0,y-1);yy<=Math.Min(h-1,y+1)&&!near;yy++)for(int xx=Math.Max(0,x-1);xx<=Math.Min(w-1,x+1);xx++)if(pale[yy*w+xx]){near=true;break;}
                    mask[y*w+x]=near;
                }
            }
            int minX=w,minY=h,maxX=-1,maxY=-1;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(mask[y*w+x]){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
            if(maxX<0)throw new ArgumentException("Animated eye or paw has no visible pixels");
            int width=maxX-minX+1,height=maxY-minY+1;
            var bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb);var fragment=new int[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(mask[(y+minY)*w+x+minX])fragment[y*width+x]=original[(y+bounds.Y+minY)*size+x+bounds.X+minX];
            var data=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try{Marshal.Copy(fragment,0,data.Scan0,fragment.Length);}finally{bitmap.UnlockBits(data);}
            return new Part{Ink=bitmap,Bounds=new Rectangle(bounds.X+minX,bounds.Y+minY,width,height)};
        }

        RectangleF Scale(Rectangle rect,Size source) {
            return new RectangleF(rect.X*size/(float)source.Width,rect.Y*size/(float)source.Height,rect.Width*size/(float)source.Width,rect.Height*size/(float)source.Height);
        }
        void Write(Bitmap bitmap,int[] buffer) {
            var data=bitmap.LockBits(new Rectangle(0,0,size,size),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try{Marshal.Copy(buffer,0,data.Scan0,buffer.Length);}finally{bitmap.UnlockBits(data);}
        }
        static float Clamp(float value,float min,float max){return Math.Max(min,Math.Min(max,value));}
        static float Influence(float value,float start,float end,float pad) {
            if(value<start)return Clamp((value-start+pad)/pad,0,1);
            if(value>end)return Clamp((end+pad-value)/pad,0,1);
            return 1;
        }
        static float InverseY(float y,float outerTop,float outerBottom,RectangleF core,float targetTop,float targetBottom) {
            if(y<targetTop)return outerTop+(y-outerTop)*(core.Top-outerTop)/(targetTop-outerTop);
            if(y>targetBottom)return core.Bottom+(y-targetBottom)*(outerBottom-core.Bottom)/(outerBottom-targetBottom);
            return core.Top+(y-targetTop)*core.Height/(targetBottom-targetTop);
        }

        void Warp(RectangleF core,float horizontal,float vertical,float squash,bool hand,bool right) {
            if(horizontal==0&&vertical==0&&squash==1)return;
            float padX=hand?7:5,padTop=hand?26:9,padBottom=hand?13:8;
            float outerTop=core.Top-padTop,outerBottom=core.Bottom+padBottom;
            int x0=Math.Max(0,(int)Math.Floor(core.Left-padX)),x1=Math.Min(size,(int)Math.Ceiling(core.Right+padX));
            if(hand){if(right)x0=Math.Max(x0,(int)Math.Floor(handSplit));else x1=Math.Min(x1,(int)Math.Floor(handSplit));}
            int y0=Math.Max(0,(int)Math.Floor(outerTop)),y1=Math.Min(size,(int)Math.Ceiling(outerBottom));
            if(hand)y1=Math.Min(y1,(int)Math.Ceiling(core.Top));
            for(int x=x0;x<x1;x++) {
                float weight=Influence(x+0.5f,core.Left,core.Right,padX);
                float height=core.Height*(1+(squash-1)*weight);
                float top=core.Top+(core.Height-height)*0.62f+vertical*weight;
                top=Clamp(top,outerTop+0.5f,outerBottom-height-0.5f);
                float bottom=top+height;
                for(int y=y0;y<y1;y++) {
                    // Only stretch the sleeve above the original wrist; keyboard pixels stay fixed.
                    float sy=hand?(y+0.5f<top?outerTop+(y+0.5f-outerTop)*(core.Top-outerTop)/(top-outerTop):core.Top-0.5f):InverseY(y+0.5f,outerTop,outerBottom,core,top,bottom);
                    float wy=Influence(sy,core.Top,core.Bottom,Math.Max(padTop,padBottom));
                    float sx=x+0.5f-horizontal*weight*wy;
                    int ix=(int)Clamp((float)Math.Floor(sx),0,size-1),iy=(int)Clamp((float)Math.Floor(sy),0,size-1);
                    pose[y*size+x]=pixels[iy*size+ix];
                }
            }
        }

        public Bitmap Render(bool closed,PointF gaze,float leftLift,float rightLift) {
            Array.Copy(pixels,pose,pixels.Length);
            Warp(leftHand,0,-leftLift,1,true,false);
            Warp(rightHand,0,-rightLift,1,true,true);
            Write(frame,pose);
            using(var g=Graphics.FromImage(frame)) {
                g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.SmoothingMode=SmoothingMode.None;
                // Keep lids fixed; only the pupil follows the pointer, like the mint cat.
                Color iris=Color.FromArgb(101,66,81),shade=Color.FromArgb(83,52,72);
                PixelEye.Draw(g,parts[0].Bounds,closed,gaze,iris,shade);
                PixelEye.Draw(g,parts[1].Bounds,closed,gaze,iris,shade);
                DrawPart(g,parts[2],0,-leftLift);DrawPart(g,parts[3],0,-rightLift);
            }
            return frame;
        }
        static void DrawPart(Graphics g,Part part,float dx,float dy) {
            g.DrawImage(part.Ink,new RectangleF(part.Bounds.X+dx,part.Bounds.Y+dy,part.Bounds.Width,part.Bounds.Height));
        }
        public Rectangle PartBounds(int index){return parts[index].Bounds;}
        public void Dispose(){neutral.Dispose();frame.Dispose();foreach(var part in parts)part.Dispose();}
    }
}
