using System.Drawing;
using System.Drawing.Drawing2D;

namespace OfficeBuddy {
    // Small original pixel sprites: the shop and the desktop share this renderer.
    static class PixelDecor {
        static readonly string[] Cup={
            ".......s....s...........","......s....s............",".......s....s...........","........................",
            "....oooooooooooo........","...ohhhhhhhhhhhho.......","..ohhhhhccccchhhhho.....","..ohhccccccccccchho.....",
            "..oaaaaaaaaaaaaaaoooo...","..oalllllllllllaaoaaao..","..oalllllllllllaao..ao..","..oalllllllllllaao..ao..",
            "..oalllllllllllaao..ao..","..oalllllllllllaao..ao..","..oalllllllllllaao..ao..","..oaaaaaaaaaaaaaaoaaao..",
            "..oaaaaaaaaaaaaaaoooo...","..oaaaaaaaaaaaaaao......","...oaaaaaaaaaaaao.......","....oooooooooooo........",
            "........................",".....sssssssssss........"};
        static readonly string[] Plant={
            "..............oooo......",".............oggggo.....",".....oooo...ogllgggo....","....oggggo..oglllgggo...",
            "...ogllgggo.oglllgggo...","...oglllgggo.ogggggo....","....oggggggo..oggggo....",".....oooooo....oo.......",
            "..........ot..ot........","...........ottto........","........oooooto.........",".......ogggggot.........",
            "......oglllgggot........","......ogllllgggot.......",".......oggggggot........","........oooooot.........",
            ".....oooooooooooooo.....",".....ohhhhhhhhhhhho.....","......oaaaaaaaaaao......","......oalllllllaao......",
            "......oalllllllaao......",".......oaaaaaaaao.......",".......oaaaaaaaao.......","........oooooooo........",
            ".........ssssss........."};
        static readonly string[] Lamp={
            "........oooooooo........",".......ollllllllo.......","......ollllllllllo......",".....ollllllllllllo.....",
            "....ollllllllllllllo....","...ollllllllllllllllo...","...ollllllllllllllllo...","...ollllllllllllllllo...",
            "...oooooooooooooooooo...","....ohhhhhhhhhhhhhho....",".....oooooooooooooo.....","...........oto..........",
            "...........oto..........","...........oto..........","...........oto..........","...........oto..........",
            "...........oto..........","...........oto..........","...........oto..........","...........oto..........",
            ".......oooootooooo......","......olllllllllllo.....",".....ooooooooooooooo....",".......sssssssssss......"};
        static readonly string[] Clock={
            "......oooo....oooo......",".....oppppo..oppppo.....","......oooo....oooo......",".......oooooooooo.......",
            ".....ooggggggggggoo.....","....ogghhhhhhhhhhggo....","...oghhhhhhhhhhhhhhgo...","..oghhhhhhhohhhhhhhhgo..",
            "..oghhhhhhhohhhhhhhhgo..","..oghhhhhhhohhhhhhhhgo..","..oghhhhhhhohhhhhhhhgo..","..oghhhhhhhohhhhhhhhgo..",
            "..oghhhhhhhhohhhhhhhgo..","..oghhhhhhhhhohhhhhhgo..","..oghhhhhhhhhhhohhhhgo..","..oghhhhhhhhhhhhhhhhgo..",
            "...oghhhhhhhhhhhhhhgo...","....ogghhhhhhhhhhggo....",".....ooggggggggggoo.....",".......oooooooooo.......",
            "......oo........oo......",".....opo........opo.....","....ooo..........ooo....",".......ssssssssss......."};
        static readonly string[] Cushion={
            "........................","........................","........................","........................",
            ".....oooooooooooooo.....","...ooppppppppppppppoo...","..oppppppppppppppppppo..","..opphhhhhhhhhhhhhhppo..",
            "..opphhhhhhhhhhhhhhppo..","..opphhhhhhhhhhhhhhppo..","..opphhhhhhhhhhhhhhppo..","..opphhhhhhpphhhhhhppo..",
            "..opphhhhhpoophhhhhppo..","..opphhhhhpoophhhhhppo..","..opphhhhhhpphhhhhhppo..","..opphhhhhhhhhhhhhhppo..",
            "..opphhhhhhhhhhhhhhppo..","..opphhhhhhhhhhhhhhppo..","..oppppppppppppppppppo..","...ooppppppppppppppoo...",
            ".....oooooooooooooo.....","......ssssssssssss......"};
        public static void Draw(Graphics g,string id,float x,float y,float scale,bool night) {
            string[] rows=id=="cup"?Cup:id=="plant"?Plant:id=="lamp"?Lamp:id=="clock"?Clock:Cushion;
            var state=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale*2,scale*2);g.SmoothingMode=SmoothingMode.None;
            try {
                for(int yy=0;yy<rows.Length;yy++)for(int xx=0;xx<rows[yy].Length;xx++) {
                    char c=rows[yy][xx];if(c=='.')continue;
                    Color color=c=='o'?Color.FromArgb(85,60,74):c=='a'?Color.FromArgb(211,156,128):c=='c'?Color.FromArgb(111,79,70):
                        c=='g'?Color.FromArgb(128,157,121):c=='t'?Color.FromArgb(99,127,92):c=='l'?(id=="lamp"?Color.FromArgb(239,203,137):id=="cup"?Color.FromArgb(235,192,152):Color.FromArgb(173,185,146)):
                        c=='h'?Color.FromArgb(255,235,204):c=='p'?(night?Color.FromArgb(176,157,196):Color.FromArgb(226,176,158)):Color.FromArgb(70,99,90,82);
                    using(var b=new SolidBrush(color))g.FillRectangle(b,xx,yy,1,1);
                }
            }finally{g.Restore(state);}
        }
    }
}
