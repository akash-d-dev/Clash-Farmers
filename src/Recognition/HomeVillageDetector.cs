using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

// Visual home-button and independent Build/Shop recognition.
namespace ClashPekkas {
    // Match the bright home HUD itself, rather than scenery that moves with the camera.
    // Sampling RGB also rejects the dimmed HUD behind modal dialogs.
    public sealed class HomeVisual {
        sealed class Template {public int Width,Height;public Color[] Pixels;}
        readonly Template[] battle,build,shop;
        static Template Load(string folder,string name) {
            string path=Path.Combine(folder,name);if(!File.Exists(path))return null;
            using(Bitmap image=new Bitmap(path)) {
                var result=new Template{Width=image.Width,Height=image.Height,Pixels=new Color[256]};
                for(int y=0;y<16;y++)for(int x=0;x<16;x++)result.Pixels[y*16+x]=image.GetPixel((int)((x+.5)*image.Width/16),(int)((y+.5)*image.Height/16));
                return result;
            }
        }
        static Template[] LoadVariants(string folder,string name){return new[]{Load(folder,"home-"+name+".png"),Load(folder,"developer-home-"+name+".png")}.Where(item=>item!=null).ToArray();}
        public HomeVisual(string folder){battle=LoadVariants(folder,"battle");build=LoadVariants(folder,"build");shop=LoadVariants(folder,"shop");}
        static Vec MatchAny(byte[] pixels,int stride,Template[] templates,Rectangle region) {
            foreach(Template template in templates){Vec point=Match(pixels,stride,template,region);if(point!=null)return point;}return null;
        }
        static Vec Match(byte[] pixels,int stride,Template template,Rectangle region) {
            if(template==null)return null;
            // Allow minor antialiasing/size differences between valid home HUDs.
            // The independent bright Build/Shop match remains mandatory.
            double best=26;Vec found=null;
            foreach(double scale in new[]{.70,.80,.90,.94,.96,.98,1.0,1.02,1.04,1.06,1.08,1.10,1.20}) {
                int w=(int)Math.Round(template.Width*scale),h=(int)Math.Round(template.Height*scale);
                for(int top=region.Top;top+h<=region.Bottom;top+=3)for(int left=region.Left;left+w<=region.Right;left+=3) {
                    double error=0;bool rejected=false;
                    for(int y=0;y<16 && !rejected;y++)for(int x=0;x<16;x++) {
                        int offset=(top+(int)((y+.5)*h/16))*stride+(left+(int)((x+.5)*w/16))*4;Color expected=template.Pixels[y*16+x];
                        error+=Math.Abs(pixels[offset]-expected.B)+Math.Abs(pixels[offset+1]-expected.G)+Math.Abs(pixels[offset+2]-expected.R);
                        if(error>=best*256*3){rejected=true;break;}
                    }
                    if(!rejected && error/(256*3)<best){best=error/(256*3);found=new Vec((left+w/2.0)/Vision.W,(top+h/2.0)/Vision.H);}
                }
            }
            return found;
        }
        public Vec FindHome(Bitmap image,Vec recognizedBattle=null) {
            BitmapData data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try {
                byte[] pixels=new byte[data.Stride*data.Height];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
                Vec point=recognizedBattle??MatchAny(pixels,data.Stride,battle,new Rectangle(0,630,240,270));if(point==null)return null;
                // Both supported home HUD sizes contain this point. Use one stable
                // target when recognition alternates between text and icon matching.
                return MatchAny(pixels,data.Stride,build,new Rectangle(1360,630,240,270))!=null || MatchAny(pixels,data.Stride,shop,new Rectangle(1400,330,200,230))!=null?new Vec(.065,.91):null;
            }finally{image.UnlockBits(data);}
        }
    }
}
