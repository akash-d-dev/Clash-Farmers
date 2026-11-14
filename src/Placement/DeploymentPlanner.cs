using System;
using System.Collections.Generic;
using System.Linq;

// Troop, spell, hero, and siege placement plans on selected deployment sides.
namespace ClashPekkas {
    public sealed class DeploymentSide {
        public string Name; public Vec A,B; public bool Reverse; public int Arm;
    }
    public sealed class PlacementPlan {
        public DeploymentSide[] Sides; public int[] Counts;
        public readonly List<Vec> Points=new List<Vec>();
        public readonly List<int> PointSides=new List<int>();
        public string Summary { get { return String.Join(", ",Sides.Select((side,i)=>side.Name+" "+Counts[i])); } }
    }
    public sealed class SupportPlan {
        public DeploymentSide[] Heroes;
        public DeploymentSide Siege;
        public static Vec Midpoint(DeploymentSide side) { return new Vec((side.A.X+side.B.X)/2,(side.A.Y+side.B.Y)/2); }
    }
    public static class Plan {
        public static SupportPlan Support(Settings settings,bool reverse,Random random) {
            DeploymentSide[] sides=Sides(settings,reverse);
            if(settings.FourSides) {
                string[] clockwise={"upper-left","upper-right","lower-right","lower-left"};
                sides=clockwise.Select(name=>sides.Single(side=>side.Name==name)).ToArray();
            }
            int start=random.Next(sides.Length);
            return new SupportPlan {
                Heroes=Enumerable.Range(0,4).Select(i=>sides[(start+(settings.FourSides?i:i/2))%sides.Length]).ToArray(),
                Siege=sides[random.Next(sides.Length)]
            };
        }
        public static int OpeningSpellCount(Settings settings) {return settings.FourSides || settings.Spells<=6 ? settings.Spells : (settings.Spells+1)/2;}
        public static DeploymentSide[] Sides(Settings settings,bool reverse,bool spells=false) {
            var sides=new List<DeploymentSide>();
            // Upper paths first, so scouting controls disappear before lower placements.
            for(int arm=0;arm<2;arm++) foreach(bool orientation in settings.FourSides ? new[]{false,true} : new[]{reverse}) {
                Vec[] lines=orientation?settings.Reverse:settings.Normal;
                sides.Add(new DeploymentSide{Name=(arm==0?"upper-":"lower-")+(orientation?"right":"left"),A=lines[arm*2],B=lines[arm*2+1],Reverse=orientation,Arm=arm});
            }
            return sides.ToArray();
        }
        static PlacementPlan Allocate(Settings settings,bool reverse,int total,bool spells,Random random) {
            var result=new PlacementPlan{Sides=Sides(settings,reverse,spells)};
            int n=result.Sides.Length;result.Counts=Enumerable.Repeat(total/n,n).ToArray();
            int[] order=Enumerable.Range(0,n).ToArray();
            for(int i=n-1;i>0;i--) {int j=random.Next(i+1),swap=order[i];order[i]=order[j];order[j]=swap;}
            for(int i=0;i<total%n;i++) result.Counts[order[i]]++;
            return result;
        }
        public static PlacementPlan Troops(Settings settings,bool reverse,Random random) {
            settings.Validate();var result=Allocate(settings,reverse,settings.Troops,false,random);
            for(int side=0;side<result.Sides.Length;side++) for(int i=0;i<result.Counts[side];i++) {
                double t=(i+.3+random.NextDouble()*.4)/result.Counts[side];
                Vec a=result.Sides[side].A,b=result.Sides[side].B;
                result.Points.Add(new Vec(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t));result.PointSides.Add(side);
            }
            return result;
        }
        public static List<Vec> Make(Settings settings,bool reverse,Random random) {return Troops(settings,reverse,random).Points;}
        public static PlacementPlan Opening(Settings settings,bool reverse,Random random) {
            settings.Validate();int total=OpeningSpellCount(settings);
            var result=Allocate(settings,reverse,total,true,random);
            for(int side=0;side<result.Sides.Length;side++) for(int i=0;i<result.Counts[side];i++) {
                DeploymentSide edge=result.Sides[side];Vec[] lines={edge.A,edge.B};
                Vec best=null;double bestScore=-1;
                for(int attempt=0;attempt<48;attempt++) {
                    int count=result.Counts[side];
                    double t=count==1?.5:.08+i*.84/(count-1)+(random.NextDouble()-.5)*Math.Min(.06,.4/count);
                    Vec candidate=SpellPoint(lines,0,t,(settings.FourSides?settings.FourSidesSpellDepthPercent:settings.LShapeOpeningDepthPercent)/100.0);
                    double score=result.Points.Count==0?Double.MaxValue:result.Points.Min(p=>PixelDistance(p,candidate));
                    if(score>bestScore) {best=candidate;bestScore=score;}
                    if(score>=120) break;
                }
                result.Points.Add(best);result.PointSides.Add(side);
            }
            return result;
        }
        public static List<Vec> OpeningSpells(Settings settings,bool reverse,Random random) {return Opening(settings,reverse,random).Points;}
        public static Vec LaterSpell(Settings settings,bool reverse,double elapsedSeconds,Random random,IList<Vec> previous=null) {
            Vec[] lines=reverse ? settings.Reverse : settings.Normal;
            Vec best=null;double bestScore=-1;
            for(int attempt=0;attempt<64;attempt++) {
                Vec target=SpellPoint(lines,random.Next(2),.05+random.NextDouble()*.90,settings.LShapeLaterDepthPercent/100.0);
                double score=previous==null || previous.Count==0?Double.MaxValue:previous.Skip(Math.Max(0,previous.Count-5)).Min(p=>PixelDistance(p,target));
                if(score>bestScore) {best=target;bestScore=score;}
                if(score>=90) break;
            }
            return best;
        }
        public static double PixelDistance(Vec a,Vec b) {return Math.Sqrt(Math.Pow((a.X-b.X)*1600,2)+Math.Pow((a.Y-b.Y)*900,2));}
        static Vec SpellPoint(Vec[] lines,int arm,double t,double inward) {
            Vec a=lines[arm*2],b=lines[arm*2+1];
            double x=a.X+(b.X-a.X)*t,y=a.Y+(b.Y-a.Y)*t;
            x+=(.5-x)*inward;y+=(.45-y)*inward;
            // Interpolation between validated battlefield paths and the center stays
            // above the troop bar. Reject HUD points instead of changing their depth.
            if(x<.02 || x>.98 || y<.05 || y>.835 || (x<.145 && y>.74) || (x>.76 && y<.23) || (x>.84 && y>.69))
                throw new Exception("Spell path overlaps the battlefield HUD. Adjust the custom troop paths or spell depth.");
            return new Vec(x,y);
        }
    }
}
