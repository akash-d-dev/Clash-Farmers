using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

// Canonical image geometry, OCR matching rules, crops, and pixel helpers.
namespace ClashPekkas {
    public sealed class OcrLine { public string Text; public double X,Y,Width,Height; }
    public static class Vision {
        public const int W=1600,H=900;
        public const double NavigationMatchThreshold=.60;
        public static readonly Rectangle FindMatchCostRegion=new Rectangle(90,635,355,105);
        public static Rectangle Region(string name) {
            if(name=="inactivityTitle")return new Rectangle(420,330,760,150);
            if(name=="reload")return new Rectangle(420,440,760,180);
            if(name=="exitTitle")return new Rectangle(420,210,760,180);
            if(name=="exitCancel")return new Rectangle(470,480,340,200);
            if (name=="attack") return new Rectangle(0,590,400,310);
            if (name=="find") return new Rectangle(65,600,420,140);
            if (name=="launch") return new Rectangle(1160,750,440,140);
            if (name=="next") return new Rectangle(1370,620,220,180);
            if (name=="end" || name=="retreat") return new Rectangle(0,650,230,155);
            if (name=="surrenderTitle") return new Rectangle(600,230,400,110);
            if (name=="confirmSurrender") return new Rectangle(810,540,300,100);
            return new Rectangle(650,720,300,120);
        }
        public static string Clean(string value) { return Regex.Replace((value??"").ToLowerInvariant(),"[^a-z0-9]",""); }
        public static bool Label(string name,string text) {
            string s=Clean(text);
            if(name=="inactivityTitle")return ButtonMatches(s,"anyonethere");
            if(name=="reload")return ButtonMatches(s,"reloadgame");
            if(name=="exitTitle")return ButtonMatches(s,"confirmexit");
            if(name=="exitCancel")return ButtonMatches(s,"cancel");
            // Stylized Battle lettering: t may be f/e, and l may be i/1.
            if (name=="attack") return !s.StartsWith("end") && !s.StartsWith("ranked") && !s.Contains("endsin") && (Regex.IsMatch(s,@"^ba[tfe][tf][li1]e$") || ButtonMatches(s,"battle",NavigationMatchThreshold) || ButtonMatches(s,"attack",NavigationMatchThreshold));
            if (name=="next") return ButtonMatches(s,"next",NavigationMatchThreshold);
            if (name=="end") return Regex.Replace(s,"[^a-z]","").Length>=7 && (ButtonMatches(Clean((text??"").Replace('+','t')),"endbattle",NavigationMatchThreshold) || Regex.IsMatch(s,@"^endba[tfe][tf][li1]e$"));
            if (name=="retreat") return Label("end",text) || ButtonMatches(s,"surrender",NavigationMatchThreshold);
            if (name=="surrenderTitle") return ButtonMatches(s,"surrender") || ButtonMatches(Clean((text??"").Replace('+','t')),"endbattle");
            if (name=="confirmSurrender") return s=="okay";
            if (name=="launch") return ButtonMatches(s,"attack",NavigationMatchThreshold);
            if (name=="find") return ButtonMatches(s,"findamatch",NavigationMatchThreshold) || ButtonMatches(s,"findmatch",NavigationMatchThreshold) || s.StartsWith("find") && ButtonMatches(s,"findbattle");
            return ButtonMatches(s,"return",NavigationMatchThreshold) || ButtonMatches(s,"returnhome",NavigationMatchThreshold) || ButtonMatches(s,"returntohome",NavigationMatchThreshold);
        }
        static bool ButtonMatches(string value,string expected,double threshold=.80) {
            // Compare the complete label in its button-specific ROI, ignoring price digits.
            string letters=Regex.Replace(value,"[^a-z]","");
            return NearLabel(value,expected) || TextSimilarity(letters,expected)>=threshold;
        }
        public static bool TryFindMatchCost(string text,out int cost) {
            cost=0;
            string normalized=(text??"").Trim().Replace('O','0').Replace('o','0').Replace('I','1').Replace('l','1').Replace('|','1');
            // A trailing OCR zero/@ may be the coin icon. It must be separated;
            // never truncate a genuine five-digit amount into an allowed price.
            Match match=Regex.Match(normalized,@"^(?<cost>(?:\d{1,3}[,\s]\d{3}|\d{3,4}))(?:\s+[0©@])?$");
            return match.Success && Int32.TryParse(Regex.Replace(match.Groups["cost"].Value,@"\D",""),out cost) && cost>=100 && cost<=5000;
        }
        public static double TextSimilarity(string value,string expected) {
            value=Clean(value);expected=Clean(expected);int length=Math.Max(value.Length,expected.Length);
            return length==0?0:1.0-EditDistance(value,expected)/(double)length;
        }
        static int EditDistance(string value,string expected) {
            int[] previous=Enumerable.Range(0,expected.Length+1).ToArray(),current=new int[expected.Length+1];
            for(int i=1;i<=value.Length;i++) {
                current[0]=i;for(int j=1;j<=expected.Length;j++)current[j]=Math.Min(Math.Min(current[j-1]+1,previous[j]+1),previous[j-1]+(value[i-1]==expected[j-1]?0:1));
                int[] swap=previous;previous=current;current=swap;
            }
            return previous[expected.Length];
        }
        public static bool NearLabel(string value,string expected) {
            return Math.Abs(value.Length-expected.Length)<=1 && EditDistance(value,expected)<=1;
        }
        public static Vec Locate(string name,IEnumerable<OcrLine> lines,Rectangle roi) {
            var all=lines.ToList();
            var candidates=all.Where(l=>Label(name,l.Text)).ToList();
            if (name=="find" && candidates.Count==0) {
                foreach(OcrLine a in all) foreach(OcrLine b in all) {
                    if(a==b || !Clean(a.Text).StartsWith("find") || !Label("find",a.Text+" "+b.Text)) continue;
                    if(Math.Abs((a.X+a.Width/2)-(b.X+b.Width/2))>180 || Math.Abs(a.Y-b.Y)>130) continue;
                    double left=Math.Min(a.X,b.X),top=Math.Min(a.Y,b.Y);
                    candidates.Add(new OcrLine{Text=a.Text+" "+b.Text,X=left,Y=top,Width=Math.Max(a.X+a.Width,b.X+b.Width)-left,Height=Math.Max(a.Y+a.Height,b.Y+b.Height)-top});
                }
            }
            if(name=="find" && candidates.Count==0) {
                var prices=all.Where(priceLine=> {
                    int cost;return TryFindMatchCost(priceLine.Text,out cost) && FindMatchCostRegion.Contains((int)(roi.X+priceLine.X+priceLine.Width/2),(int)(roi.Y+priceLine.Y+priceLine.Height/2));
                }).ToList();
                if(prices.Count==1)candidates.Add(prices[0]);
            }
            if (candidates.Count!=1) return null;
            OcrLine line=candidates[0];
            double x=(roi.X+line.X+line.Width/2)/W, y=(roi.Y+line.Y+line.Height/2)/H;
            if (name=="attack" && (x>.24 || y<.65)) return null;
            if (name=="next" && x<.78) return null;
            if (name=="launch" && (x<.72 || y<.82)) return null;
            if(name=="find" && (x<.04 || x>.31 || y<.65 || y>.825))return null;
            if ((name=="end" || name=="retreat") && (x>.20 || y<.65)) return null;
            if (name=="return" && (x<.25 || x>.75 || y<.60)) return null;
            return new Vec(x,y);
        }
        public static Bitmap Resize(Bitmap source,int width,int height) {
            Bitmap result=new Bitmap(width,height,PixelFormat.Format32bppArgb);
            using (Graphics g=Graphics.FromImage(result)) { g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.DrawImage(source,0,0,width,height); }
            return result;
        }
        public static bool Similar(Bitmap first,Bitmap second) {
            using (Bitmap a=Resize(first,40,40)) using(Bitmap b=Resize(second,40,40)) {
                double error=0;
                for (int y=0;y<40;y+=2) for (int x=0;x<40;x+=2) {
                    Color p=a.GetPixel(x,y),q=b.GetPixel(x,y);
                    error+=Math.Abs(p.R-q.R)+Math.Abs(p.G-q.G)+Math.Abs(p.B-q.B);
                }
                return error/(400*3)<32;
            }
        }
        public static readonly Rectangle TroopFace=new Rectangle(258,803,66,48);
        public static readonly Rectangle SpellFace=new Rectangle(890,812,54,46);
        public static readonly Rectangle GoldLoot=new Rectangle(65,106,220,42),ElixirLoot=new Rectangle(65,148,235,45);
        public static bool TryLootNumber(string text,out long value) {
            value=0;if(text==null) return false;
            string normalized=text.Trim().Replace('O','0').Replace('o','0').Replace('I','1').Replace('l','1').Replace('|','1');
            if(!Regex.IsMatch(normalized,@"^(?:\d{1,8}|\d{1,3}(?:[\s,.'\u2019]+\d{3})+|\d{1,2}(?:[\s,]+\d{2})+[\s,]+\d{3})$")) return false;
            string digits=Regex.Replace(normalized,@"\D","");
            return digits.Length<=8 && Int64.TryParse(digits,out value) && value<=99999999;
        }
        public static Bitmap WhiteDigits(Bitmap source,bool developer=false) {
            Bitmap result=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(result)) g.DrawImageUnscaled(source,0,0);
            BitmapData locked=result.LockBits(new Rectangle(0,0,result.Width,result.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            try {
                byte[] pixels=new byte[Math.Abs(locked.Stride)*result.Height];Marshal.Copy(locked.Scan0,pixels,0,pixels.Length);
                for(int offset=0;offset<pixels.Length;offset+=4) {
                    int b=pixels[offset],g=pixels[offset+1],r=pixels[offset+2];
                    bool ink=developer ? Math.Min(r,Math.Min(g,b))>=190 && Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))<85 : r>175 && g>175 && b>160 && Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))<65;
                    byte shade=ink ? (byte)0 : (byte)255;pixels[offset]=shade;pixels[offset+1]=shade;pixels[offset+2]=shade;pixels[offset+3]=255;
                }
                Marshal.Copy(pixels,0,locked.Scan0,pixels.Length);
            } finally { result.UnlockBits(locked); }
            return result;
        }
        public static int LootDigitCount(Bitmap mask) {
            int w=mask.Width,h=mask.Height;var ink=new bool[w*h];var seen=new bool[w*h];var columns=new bool[w];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)ink[y*w+x]=mask.GetPixel(x,y).R<128;
            var queue=new Queue<int>();var neighbours=new[]{-1,1,-w,w};
            for(int start=0;start<ink.Length;start++) {
                if(!ink[start] || seen[start])continue;
                int left=w,right=0,top=h,bottom=0;seen[start]=true;queue.Enqueue(start);
                while(queue.Count>0) {
                    int i=queue.Dequeue(),x=i%w,y=i/w;left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                    foreach(int delta in neighbours){int next=i+delta;if(next<0 || next>=ink.Length || (delta==-1 && x==0) || (delta==1 && x==w-1) || seen[next] || !ink[next])continue;seen[next]=true;queue.Enqueue(next);}
                }
                // Ignore small background flecks and the resource icon touching the crop edge.
                if(left==0 || right==w-1 || bottom-top+1<h*.4 || right-left+1<h*.06 || right-left+1>h*1.5)continue;
                for(int x=left;x<=right;x++)columns[x]=true;
            }
            int count=0;for(int x=0;x<w;x++)if(columns[x] && (x==0 || !columns[x-1]))count++;
            return count;
        }
    }
}
