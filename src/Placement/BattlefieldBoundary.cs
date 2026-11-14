using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

// Conservative per-opponent fitting of the outer deployment boundary.
namespace ClashPekkas {
    public static class BattlefieldRim {
        static bool SameArm(Vec[] current,Vec[] preset,int arm){return current!=null && current.Length==4 && Enumerable.Range(arm*2,2).All(i=>Plan.PixelDistance(current[i],preset[i])<.01);}
        public static bool TryDetect(Bitmap image,out double[] edges) {
            edges=null;if(image.Width!=Vision.W || image.Height!=Vision.H)return false;
            var bins=new Dictionary<int,HashSet<int>>[4];for(int side=0;side<4;side++)bins[side]=new Dictionary<int,HashSet<int>>();
            BitmapData data=image.LockBits(new Rectangle(0,0,Vision.W,Vision.H),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try {
                byte[] pixels=new byte[data.Stride*Vision.H];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
                for(int y=50;y<745;y++)for(int x=80;x<1520;x++) {
                    int offset=y*data.Stride+x*4,b=pixels[offset],g=pixels[offset+1],r=pixels[offset+2];
                    if(r<55 || g<20 || b>130 || r<g*.9)continue;
                    for(int side=0;side<4;side++) {
                        bool right=side%2==1,lower=side>=2;if(right?x<820:x>780)continue;if(lower?y<400:y>510)continue;
                        int ny=side==0 || side==3?3:-3,first=(y+ny)*data.Stride+(x+2)*4,second=(y-ny)*data.Stride+(x-2)*4;
                        double contrast=2*r-g-b-(2*pixels[first+2]-pixels[first+1]-pixels[first]+2*pixels[second+2]-pixels[second+1]-pixels[second])/2.0;
                        if(contrast<20)continue; // Thin red boundary, rather than a broad red roof or decoration.
                        double c=x/(double)Vision.W+(side==0 || side==3?1:-1)*.78*y/Vision.H;
                        double min=side==0?.4:side==1?.35:side==2?-.4:1.20,max=side==0?.65:side==1?.62:side==2?-.18:1.42;
                        if(c<min || c>max)continue;
                        int bin=(int)Math.Round(c*400);
                        for(int delta=-1;delta<=1;delta++) {
                            int key=bin+delta;HashSet<int> rows;
                            if(!bins[side].TryGetValue(key,out rows)){rows=new HashSet<int>();bins[side][key]=rows;}
                            rows.Add(y/8);
                        }
                    }
                }
            }finally{image.UnlockBits(data);}
            var fitted=new double[4];
            for(int side=0;side<4;side++) {
                var candidates=bins[side].Where(pair=>pair.Value.Count>=22 && (pair.Value.Max()-pair.Value.Min())*8>=280);
                var ordered=side==0 || side==2?candidates.OrderBy(pair=>pair.Key):candidates.OrderByDescending(pair=>pair.Key);
                if(!ordered.Any())return false;fitted[side]=ordered.First().Key/400.0;
            }
            double northX=(fitted[0]+fitted[1])/2,southX=(fitted[2]+fitted[3])/2;
            double leftX=(fitted[0]+fitted[2])/2,rightX=(fitted[1]+fitted[3])/2;
            double leftY=(fitted[0]-fitted[2])/1.56,rightY=(fitted[3]-fitted[1])/1.56;
            // Accept only a complete, centered, maximum-size diamond. Ambiguous
            // scenery and compact building outlines must use the calibrated preset.
            if(Math.Abs(northX-.5)>.015 || Math.Abs(southX-.5)>.015 || Math.Abs(northX-southX)>.012 || Math.Abs(leftY-rightY)>.012
                || leftX<.075 || leftX>.16 || rightX<.84 || rightX>.925 || (leftY+rightY)/2<.43 || (leftY+rightY)/2>.59)return false;
            edges=fitted;return true;
        }
        public static Settings Adapt(Settings settings,Bitmap image,Action<string> log=null) {
            bool[] eligible={SameArm(settings.Normal,Settings.OuterLines(false),0),SameArm(settings.Reverse,Settings.OuterLines(true),0),SameArm(settings.Normal,Settings.OuterLines(false),1),SameArm(settings.Reverse,Settings.OuterLines(true),1)};
            double[] edge;
            if(!eligible.Any(value=>value) || !TryDetect(image,out edge)) {if(log!=null)log("Deployment boundary: calibrated outer-grass preset; no reliable full boundary fit.");return settings;}
            // Twenty screen pixels perpendicular to the detected restricted outline.
            double padding=20*Math.Sqrt(1+Math.Pow(.78*Vision.W/Vision.H,2))/Vision.W;
            double upperLeft=edge[0]-padding,upperRight=edge[1]+padding,lowerLeft=edge[2]-padding,lowerRight=edge[3]+padding;
            double middleY=((edge[0]-edge[2])+(edge[3]-edge[1]))/3.12;
            double top=Math.Max(.06,Math.Max((upperLeft-.40)/.78,(.60-upperRight)/.78)),upperEnd=Math.Min(.49,middleY-.06);
            double lowerStartLeft=Math.Max(.57,middleY+.10),lowerStartRight=Math.Max(.53,middleY+.06);
            if(top>=upperEnd-.08 || lowerStartLeft>=.78 || lowerStartRight>=.78)return settings;
            Vec[][] arms={new[]{new Vec(upperLeft-.78*upperEnd,upperEnd),new Vec(upperLeft-.78*top,top)},new[]{new Vec(upperRight+.78*upperEnd,upperEnd),new Vec(upperRight+.78*top,top)},
                new[]{new Vec(lowerLeft+.78*lowerStartLeft,lowerStartLeft),new Vec(lowerLeft+.78*.83,.83)},new[]{new Vec(lowerRight-.78*lowerStartRight,lowerStartRight),new Vec(lowerRight-.78*.83,.83)}};
            var serializer=new JavaScriptSerializer();Settings adapted=Settings.FromJson(serializer.Serialize(settings));
            for(int side=0;side<4;side++)if(eligible[side]) {
                Vec[] shape=side%2==0?adapted.Normal:adapted.Reverse;int arm=side/2;shape[arm*2]=arms[side][0];shape[arm*2+1]=arms[side][1];
            }
            try{adapted.Validate();}catch(Exception){return settings;}
            if(log!=null)log("Deployment boundary: fitted visible outer outline; 20px outward margin. Custom paths preserved.");
            return adapted;
        }
    }
}
