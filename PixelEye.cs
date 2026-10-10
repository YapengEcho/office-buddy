using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace OfficeBuddy {
    // All companions use the same fixed lid and moving pupil, in a 24 x 13 grid.
    static class PixelEye {
        static void Block(Graphics g,Color c,float x,float y,float w,float h){using(var b=new SolidBrush(c))g.FillRectangle(b,x,y,w,h);}
        public static void Draw(Graphics g,RectangleF bounds,bool closed,PointF gaze,Color iris,Color shade) {
            var state=g.Save();
            try {
                g.TranslateTransform(bounds.X,bounds.Y);g.ScaleTransform(bounds.Width/24,bounds.Height/13);
                g.SmoothingMode=SmoothingMode.None;Color edge=Color.FromArgb(56,35,52);
                if(closed) {
                    Block(g,edge,0,7,5,2);Block(g,edge,4,9,5,2);Block(g,edge,8,10,8,2);Block(g,edge,15,8,6,2);Block(g,edge,20,6,4,2);
                    return;
                }
                using(var shape=new GraphicsPath()) {
                    shape.AddPolygon(new[]{new PointF(0,3),new PointF(5,3),new PointF(5,0),new PointF(15,0),new PointF(15,2),new PointF(23,2),new PointF(23,5),new PointF(21,5),new PointF(21,10),new PointF(18,10),new PointF(18,13),new PointF(6,13),new PointF(6,11),new PointF(2,11),new PointF(2,6),new PointF(0,6)});
                    using(var b=new SolidBrush(edge))g.FillPath(b,shape);
                    g.SetClip(shape,CombineMode.Intersect);
                    Block(g,iris,5,3,14,7);Block(g,shade,7,9,10,2);
                    float dx=(float)Math.Round(Math.Max(-3.5,Math.Min(3.5,gaze.X)));
                    float dy=(float)Math.Round(Math.Max(-1.8,Math.Min(1.8,gaze.Y)));
                    Block(g,edge,8+dx,3+dy,8,7);Block(g,Color.FromArgb(255,246,232),13+dx,3+dy,2.5f,2.5f);
                }
            } finally {g.Restore(state);}
        }
    }
}
