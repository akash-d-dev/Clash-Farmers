using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ClashPekkas {
    public static class AppFiles {
        public static string Root=AppDomain.CurrentDomain.BaseDirectory;
        public static string SettingsFile(string root) { return Path.Combine(root,"data","settings.json"); }
        public static string DataFile(string name) { Directory.CreateDirectory(Path.Combine(Root,"data"));return Path.Combine(Root,"data",name); }
        public static void Initialize(string root) {
            Root=root;Directory.CreateDirectory(Path.Combine(root,"data"));
            string config=SettingsFile(root),example=Path.Combine(root,"config","settings.example.json");
            if(!File.Exists(config)) {
                if(File.Exists(example)) File.Copy(example,config);
                else File.WriteAllText(config,new JavaScriptSerializer().Serialize(new Settings()));
            }
        }
    }
    public delegate string OcrReader(byte[] png);
    public delegate IntPtr PlayerPicker();
    public delegate void PlayerActivator(IntPtr window);
    public sealed class Vec {
        public double X, Y;
        public Vec() { }
        public Vec(double x, double y) { X=x; Y=y; }
    }
    public sealed class Settings {
        public int Troops=25, Battles=10, MinDelayMs=130, MaxDelayMs=250;
        public string Platform="BlueStacks";
        public string DeploymentMode="LShape";
        public string[] ActionOrder={"Heroes","Siege","Army","HeroAbilities","Spells"};
        public static string ActionLabel(string action) {
            switch(action){case "Heroes":return "Deploy heroes";case "Siege":return "Deploy siege";case "Army":return "Deploy army";case "HeroAbilities":return "Hero abilities";case "Spells":return "Cast spells";default:return action;}
        }
        public static void ValidateOrder(string[] order) {
            string[] required={"Heroes","Siege","Army","HeroAbilities","Spells"};
            if(order==null || order.Length!=5 || order.Distinct().Count()!=5 || required.Any(action=>!order.Contains(action)))throw new Exception("Action order must contain heroes, siege, army, abilities and spells once each.");
            if(Array.IndexOf(order,"HeroAbilities")<Array.IndexOf(order,"Heroes"))throw new Exception("Place Hero abilities after Deploy heroes.");
        }
        public bool FourSides { get { return DeploymentMode=="FourSides"; } }
        public bool GooglePlay { get { return Platform=="GooglePlayGames"; } }
        public string PlayerName { get { return GooglePlay ? "Google Play Developer (ADB)" : "BlueStacks"; } }
        public bool FilterLoot=true;
        public long MinGold=500000,MinElixir=500000,MinDarkElixir=0;
        public int AttackDurationSeconds=0;
        public bool UseSpells=true;
        public bool DeploySiegeAndFirstHero=true;
        public int Spells=11,MinSpellIntervalMs=2000,MaxSpellIntervalMs=5000;
        public int MinFirstSpellWaitMs=5000,MaxFirstSpellWaitMs=10000;
        public bool FastNavigation=true,EndBattleEarly=true;
        public int MinBattleSeconds=80,MaxBattleSeconds=100;
        public Vec HomeBattleButton=new Vec(.065,.91),FindMatchButton=new Vec(.18,.73),ArmyAttackButton=new Vec(.87,.90);
        public int OpeningSpellDelayMs=100;
        public double OpeningSpellInward=.35; // Legacy JSON compatibility; depth controls replace this.
        public int LShapeOpeningDepthPercent=0,LShapeLaterDepthPercent=70,FourSidesSpellDepthPercent=0;
        public Vec SpellButton=new Vec(.57,.93);
        public int TopChrome=32, RightChrome=32;
        public bool UseAdbWhenAvailable=true;
        public string AdbPath="",AdbSerial="";
        public string GoogleAdbPath="",GoogleAdbSerial="localhost:6520";
        public double ViewAspect=16.0/9.0;
        // Two separated arms: avoid the corners, HUD, and duplicate placements.
        public Vec[] Normal = OuterLines(false);
        public Vec[] Reverse = OuterLines(true);
        public Vec[] NormalSpellGuide=PreviousOuterLines(false),ReverseSpellGuide=PreviousOuterLines(true); // Retired placement guides, preserved for compatibility.
        public static Vec[] PreviousOuterLines(bool reverse) {
            // Isometric side slope in the normalized 16:9 view is about .78, not 1.
            // Keep the lines on the grass strip between the maximum building area and scenery.
            Vec[] lines={new Vec(.1378,.49),new Vec(.4264,.12),new Vec(.0946,.57),new Vec(.2974,.83)};
            return lines.Select(p=>new Vec(reverse?1-p.X:p.X,p.Y)).ToArray();
        }
        public static Vec[] OuterLines(bool reverse) {
            Vec[] lines=PreviousOuterLines(reverse);
            // Upper edges move away from buildings. Keep the working lower-left side.
            for(int i=0;i<2;i++) lines[i]=new Vec(lines[i].X+(reverse?.027:-.027),lines[i].Y-.060);
            if(reverse) {
                // The lower-right grass corridor is narrower than its mirrored shoreline.
                // Keep a 48px horizontal margin beyond the former tile-edge line
                // in the normalized 1600px-wide view, without moving toward the HUD.
                lines[2]=new Vec(.89,.51);
                lines[3]=new Vec(.6404,.83); // Stop above the troop bar.
            }
            return lines;
        }
        public Vec TroopButton=new Vec(.185,.93);
        public int GeometryVersion=4;
        public static Settings FromJson(string text) {
            var json=new JavaScriptSerializer();var data=json.Deserialize<Dictionary<string,object>>(text);var settings=json.Deserialize<Settings>(text);
            if(!data.ContainsKey("GeometryVersion")) {
                if(settings.Normal!=null && settings.Normal.Length==4 && Math.Abs(settings.Normal[0].X-.16)<.0001 && Math.Abs(settings.Normal[3].X-.275)<.0001) settings.Normal=OuterLines(false);
                if(settings.Reverse!=null && settings.Reverse.Length==4 && Math.Abs(settings.Reverse[0].X-.65)<.0001 && Math.Abs(settings.Reverse[3].X-.73)<.0001) settings.Reverse=OuterLines(true);
            }
            else if(settings.GeometryVersion<3) {
                if(!data.ContainsKey("NormalSpellGuide")) settings.NormalSpellGuide=settings.Normal.Select(p=>new Vec(p.X,p.Y)).ToArray();
                if(!data.ContainsKey("ReverseSpellGuide")) settings.ReverseSpellGuide=settings.Reverse.Select(p=>new Vec(p.X,p.Y)).ToArray();
                foreach(bool reverse in new[]{false,true}) {
                    Vec[] current=reverse?settings.Reverse:settings.Normal,previous=PreviousOuterLines(reverse),replacement=OuterLines(reverse);
                    for(int arm=0;arm<2;arm++) if(current!=null && current.Length==4 && Enumerable.Range(arm*2,2).All(i=>Math.Abs(current[i].X-previous[i].X)<.00001 && Math.Abs(current[i].Y-previous[i].Y)<.00001)) {
                        current[arm*2]=replacement[arm*2];current[arm*2+1]=replacement[arm*2+1];
                    }
                }
            }
            if(settings.GeometryVersion==3 && settings.Reverse!=null && settings.Reverse.Length==4
                && Math.Abs(settings.Reverse[2].X-.86)<.00001 && Math.Abs(settings.Reverse[2].Y-.51)<.00001
                && Math.Abs(settings.Reverse[3].X-.6104)<.00001 && Math.Abs(settings.Reverse[3].Y-.83)<.00001) {
                Vec[] replacement=OuterLines(true);
                settings.Reverse[2]=replacement[2];settings.Reverse[3]=replacement[3];
            }
            settings.GeometryVersion=4;
            settings.Validate();return settings;
        }
        public void Validate() {
            ValidateOrder(ActionOrder);
            if(Platform!="BlueStacks" && Platform!="GooglePlayGames") throw new Exception("Select BlueStacks or Google Play Games.");
            if(DeploymentMode!="LShape" && DeploymentMode!="FourSides") throw new Exception("Select L-shaped or all four sides deployment.");
            if (Troops<1 || Troops>500 || Battles<1 || Battles>30) throw new Exception("Use 1-500 troop clicks and 1-30 battles.");
            if (MinDelayMs<80 || MaxDelayMs<MinDelayMs || MaxDelayMs>2000) throw new Exception("Invalid click timing.");
            if (TopChrome<0 || TopChrome>200 || RightChrome<0 || RightChrome>200 || ViewAspect<1.2 || ViewAspect>2.5) throw new Exception("Invalid game viewport settings.");
            foreach (Vec[] shape in new[]{Normal,Reverse}) {
                if (shape==null || shape.Length!=4) throw new Exception("Each orientation needs four endpoints.");
                foreach (Vec p in shape) if (!Valid(p) || p.X<.02 || p.X>.98 || p.Y<.05 || p.Y>.835) throw new Exception("Deployment endpoint outside the battlefield.");
                if (Distance(shape[0],shape[1])<.05 || Distance(shape[2],shape[3])<.05) throw new Exception("Deployment line is too short.");
            }
            if (!Valid(TroopButton)) throw new Exception("Invalid troop button.");
            if (!Valid(SpellButton) || SpellButton.X<.05 || SpellButton.X>.95 || SpellButton.Y<.86 || SpellButton.Y>.99) throw new Exception("Invalid spell card position.");
            if (Spells<0 || Spells>100 || MinSpellIntervalMs<2000 || MaxSpellIntervalMs<MinSpellIntervalMs || MaxSpellIntervalMs>5000) throw new Exception("Use 0-100 spells and late-spell intervals between 2000 and 5000 ms.");
            if(MinGold<0 || MinElixir<0 || MinDarkElixir<0 || MinGold>99999999 || MinElixir>99999999 || MinDarkElixir>99999999) throw new Exception("Invalid loot thresholds.");
            if(OpeningSpellDelayMs<60 || OpeningSpellDelayMs>500) throw new Exception("Invalid opening spell speed.");
            foreach(int depth in new[]{LShapeOpeningDepthPercent,LShapeLaterDepthPercent,FourSidesSpellDepthPercent}) if(depth<0 || depth>100) throw new Exception("Spell depth must be 0-100 percent.");
            if(MinFirstSpellWaitMs<5000 || MaxFirstSpellWaitMs<MinFirstSpellWaitMs || MaxFirstSpellWaitMs>10000) throw new Exception("First delayed spell must wait 5000-10000 ms.");
            if(AttackDurationSeconds==0) AttackDurationSeconds=Math.Max(3,Math.Min(180,(MinBattleSeconds+MaxBattleSeconds)/2));
            if(AttackDurationSeconds<3 || AttackDurationSeconds>180) throw new Exception("Attack duration must be 3-180 seconds from the first troop placement.");
            if(!Valid(HomeBattleButton) || HomeBattleButton.X>.20 || HomeBattleButton.Y<.82 || !Valid(FindMatchButton) || FindMatchButton.X<.05 || FindMatchButton.X>.30 || FindMatchButton.Y<.65 || FindMatchButton.Y>.82 || !Valid(ArmyAttackButton) || ArmyAttackButton.X<.78 || ArmyAttackButton.Y<.82) throw new Exception("Navigation coordinates are outside their button areas.");
        }
        static bool Valid(Vec p) { return p!=null && !double.IsNaN(p.X) && !double.IsNaN(p.Y) && p.X>=0 && p.X<=1 && p.Y>=0 && p.Y<=1; }
        static double Distance(Vec a,Vec b) { return Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2)); }
    }
    public sealed class DeploymentSide {
        public string Name; public Vec A,B; public bool Reverse; public int Arm;
    }
    public sealed class PlacementPlan {
        public DeploymentSide[] Sides; public int[] Counts;
        public readonly List<Vec> Points=new List<Vec>();
        public readonly List<int> PointSides=new List<int>();
        public string Summary { get { return String.Join(", ",Sides.Select((side,i)=>side.Name+" "+Counts[i])); } }
    }
    public static class Plan {
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
    public sealed class LootReading {
        public long Gold,Elixir,DarkElixir;
        public LootReading(long gold,long elixir,long darkElixir=0) { Gold=gold;Elixir=elixir;DarkElixir=darkElixir; }
    }
    public interface IGame {
        void Guard();
        Vec Find(string button);
        Vec FirstTroopCard();
        bool SpellVisible();
        LootReading ReadLoot(bool gold=true,bool elixir=true,bool darkElixir=false);
        void Click(Vec point);
        void PressKey(Keys key);
    }
    public interface ICapturedGame : IGame,IDisposable { Bitmap Capture(); }
    public interface ISupportGame { bool TrySelectSupport(Keys key); }
    public sealed class Runner {
        readonly IGame game; readonly Settings settings; readonly Random random;
        readonly Action<string> log; readonly int limit; readonly bool deployOnly;
        DateTime entered, next; int stable; Vec last;
        List<Vec> plan,openingSpells; PlacementPlan openingLayout; Vec supportPoint,troopCardPoint; Vec[] heroPoints;int heroIndex;static readonly Keys[] HeroKeys={Keys.Q,Keys.W,Keys.E,Keys.R}; readonly List<Vec> spellHistory=new List<Vec>();int deployed,spellsCast;bool reverseAttack,spellsAvailable;
        LootReading previousLoot;int lootFailures,lootConfirmations;
        DateTime troopsFinished,spellDue,battleDeadline;
        readonly List<Keys> pendingAbilities=new List<Keys>();int abilityIndex;
        int actionIndex;
        bool SupportEnabled {get{return settings.GooglePlay && settings.DeploySiegeAndFirstHero && game is ISupportGame;}}
        void AdvanceAction(DateTime now,int delay=0) {
            if(deployOnly){Go("Select troops",now);next=now.AddMilliseconds(delay);return;}
            while(actionIndex<settings.ActionOrder.Length) {
                string action=settings.ActionOrder[actionIndex++],state=null;
                if(action=="Heroes" && SupportEnabled)state="Select hero";
                else if(action=="Siege" && SupportEnabled)state="Select siege";
                else if(action=="Army")state="Select troops";
                else if(action=="HeroAbilities" && pendingAbilities.Count>0)state="Hero ability";
                else if(action=="Spells" && spellsAvailable)state="Select spells";
                if(state==null){log("Skipping "+Settings.ActionLabel(action)+": disabled or unavailable.");continue;}
                log("Action "+actionIndex+"/5: "+Settings.ActionLabel(action)+".");Go(state,now);next=now.AddMilliseconds(delay);return;
            }
            log("Configured action order complete. Waiting for result or cutoff.");Go("Result",now);next=now.AddMilliseconds(delay);
        }
        static bool ActionState(string state) {return state=="Deploying" || state.StartsWith("Select ") || state=="Deploy hero" || state=="Deploy siege" || state=="Hero ability" || state=="Opening spells" || state=="Spells";}
        bool scannedNavigation;
        public string State { get; private set; }
        public string Message { get; private set; }
        public int Completed { get; private set; }
        public int SkippedBases { get; private set; }
        public bool Active { get; private set; }
        public Runner(IGame game,Settings settings,int limit,bool deployOnly,Action<string> log,Random random) {
            settings.Validate(); if (limit<1 || limit>30) throw new Exception("Battle limit must be 1-30.");
            this.game=game; this.settings=settings; this.limit=limit; this.deployOnly=deployOnly; this.log=log; this.random=random;
        }
        public void Start(DateTime now) { Active=true; Completed=0;SkippedBases=0;ResetLoot();Message=null;battleDeadline=DateTime.MinValue;scannedNavigation=false; Go(deployOnly ? "Scouting" : "Home",now); }
        public void ResumeExistingBattle(DateTime now,int completed,bool searching=false) {
            if(completed<0 || completed>=limit) throw new Exception("Invalid completed battle count for recovery.");
            Active=true;Completed=completed;SkippedBases=0;ResetLoot();Message=null;battleDeadline=DateTime.MinValue;scannedNavigation=false;Go(searching?"Recover search":"Recover result",now);
        }
        void ResetLoot() { previousLoot=null;lootFailures=0;lootConfirmations=0; }
        void SkipBase(Vec nextButton,DateTime now,string reason) {
            log(reason+" Clicking Next.");game.Click(nextButton);SkippedBases++;ResetLoot();Go("Scouting",now);next=now.AddMilliseconds(1500);
        }
        bool AcceptLoot(Vec nextButton,DateTime now) {
            if(settings.MinGold==0 && settings.MinElixir==0 && settings.MinDarkElixir==0) {ResetLoot();return true;}
            LootReading reading=game.ReadLoot(settings.MinGold>0,settings.MinElixir>0,settings.MinDarkElixir>0);
            if(reading==null) {
                previousLoot=null;lootConfirmations=0;
                if(++lootFailures>=3) {
                    log("Loot unreadable after three attempts. Attacking the current base without a verified loot amount.");
                    ResetLoot();return true;
                }
                else { log("Loot unreadable; checking again before deciding.");next=now.AddMilliseconds(500); }
                return false;
            }
            lootFailures=0;
            lootConfirmations=previousLoot!=null && reading.Gold==previousLoot.Gold && reading.Elixir==previousLoot.Elixir && reading.DarkElixir==previousLoot.DarkElixir ? lootConfirmations+1 : 1;
            previousLoot=reading;
            string amounts="gold "+(settings.MinGold>0?reading.Gold.ToString("N0",CultureInfo.InvariantCulture):"unchecked")+", elixir "+(settings.MinElixir>0?reading.Elixir.ToString("N0",CultureInfo.InvariantCulture):"unchecked")+", dark elixir "+(settings.MinDarkElixir>0?reading.DarkElixir.ToString("N0",CultureInfo.InvariantCulture):"unchecked");
            if(lootConfirmations<2) { log("Available loot: "+amounts+". Confirming...");next=now.AddMilliseconds(500);return false; }
            if(reading.Gold<settings.MinGold || reading.Elixir<settings.MinElixir || reading.DarkElixir<settings.MinDarkElixir) {
                SkipBase(nextButton,now,"Below loot target: "+amounts+".");return false;
            }
            log("Loot accepted: "+amounts+".");ResetLoot();return true;
        }
        public void Stop(string reason) { if (Active) log(reason); Active=false; State="Stopped"; Message=reason; }
        void Go(string state,DateTime now) { State=state; entered=now; next=now.AddMilliseconds(600); stable=0; last=null; log("Waiting: "+state+" ("+Completed+"/"+limit+")"); }
        bool Ready(string name,out Vec point) {
            point=game.Find(name);
            if (point==null) { stable=0; last=null; return false; }
            if (last!=null && Math.Abs(point.X-last.X)<.015 && Math.Abs(point.Y-last.Y)<.015) stable++; else stable=1;
            last=point; return stable>=2;
        }
        void OpenFromHome(Vec detected,DateTime now) {
            game.Click(settings.FastNavigation && !scannedNavigation ? settings.HomeBattleButton : detected);
            Go(settings.FastNavigation && !scannedNavigation ? "Fast find" : "Find match",now);
            next=now.AddMilliseconds(random.Next(900,1301));
        }
        void BeginFallback(DateTime now) {
            log("Fast navigation did not reach scouting. Returning to home for a scanned retry.");
            scannedNavigation=true;ResetLoot();Go("Navigation recovery",now);
        }
        public void Tick(DateTime now) {
            if (!Active) return;
            var tickClock=Stopwatch.StartNew();
            try {
                game.Guard();
                if(battleDeadline!=DateTime.MinValue && now>=battleDeadline && (ActionState(State) || State=="Result")) {
                    log("Battle duration reached from the first troop placement. Cancelling remaining deployment, abilities and spells.");Go("Ending attack",now);next=now;
                }
                if (now<next) return;
                double timeout=(State=="Result" || State=="Spells" || State=="Deploying" || State.StartsWith("Recover")) ? 260 : State=="Scouting" ? 120 : 45;
                if ((now-entered).TotalSeconds>timeout) { Stop("Stopped: timed out on "+State+". No further clicks."); return; }
                Vec point;
                if(State=="Recover search") {
                    if(game.Find("next")!=null) Go("Scouting",now);
                    else if(game.Find("attack")!=null) Go("Home",now);
                    else {
                        Vec end=game.Find("end");
                        if(end!=null) { game.Click(end);Go("Search home",now); }
                        else if(game.Find("return")!=null) Go("Search return",now);
                    }
                }
                else if(State=="Recover result") {
                    if(game.Find("return")!=null) Go("Result",now);
                    else if(game.Find("attack")!=null) Go("Return home",now);
                }
                else if (State=="Home" && Ready("attack",out point)) { OpenFromHome(point,now);return; }
                else if(State=="Fast find") { game.Click(settings.FindMatchButton);Go("Fast army",now);next=now.AddMilliseconds(random.Next(900,1301));return; }
                else if(State=="Fast army") { game.Click(settings.GooglePlay ? new Vec(.85,.86) : settings.ArmyAttackButton);Go("Fast verify",now);next=now.AddMilliseconds(1500);return; }
                else if(State=="Fast verify") {
                    if(Ready("next",out point)) { log("Fast navigation verified on the scouting screen.");Go("Scouting",now); }
                    else if((now-entered).TotalSeconds>=8) BeginFallback(now);
                }
                else if(State=="Navigation recovery") {
                    // Check panels before Home: the dimmed home Battle label can remain visible behind them.
                    if(game.Find("launch")!=null) { game.Click(settings.GooglePlay ? new Vec(.92,.137) : new Vec(.96,.085));next=now.AddMilliseconds(1200);return; }
                    else if(game.Find("find")!=null) { game.Click(new Vec(.68,.82));next=now.AddMilliseconds(1200);return; }
                    else if(Ready("attack",out point)) { log("Home recovered. Using scanned navigation for this attempt.");OpenFromHome(point,now);return; }
                    else if(game.Find("next")!=null || game.Find("end")!=null) {
                        Vec end=game.Find("end");if(end!=null) { game.Click(end);Go("Search home",now); }
                    }
                    else if(game.Find("return")!=null) Go("Search return",now);
                }
                else if (State=="Find match" && Ready("find",out point)) { game.Click(point); Go("Army",now); }
                else if (State=="Army" && Ready("launch",out point)) { game.Click(point); Go("Scouting",now); }
                else if (State=="Scouting" && Ready("next",out point)) {
                    scannedNavigation=false;
                    if(settings.FilterLoot && !AcceptLoot(point,now)) return;
                    Vec troopCard=settings.GooglePlay ? null : game.FirstTroopCard();troopCardPoint=troopCard;
                    if (!settings.GooglePlay && troopCard==null) { Stop("Stopped: first troop card is not available. Load troops in the first slot before starting."); return; }
                    spellsAvailable=settings.UseSpells && settings.Spells>0 && (settings.GooglePlay || game.SpellVisible());
                    if(settings.UseSpells && settings.Spells>0 && !spellsAvailable) log("Haste card not recognized before deployment. Spells will be skipped for this battle.");
                    bool reverse=random.Next(2)==1;reverseAttack=reverse;spellsCast=0;spellHistory.Clear();
                    PlacementPlan troopLayout=Plan.Troops(settings,reverse,random);plan=troopLayout.Points;deployed=0;
                    DeploymentSide firstSide=troopLayout.Sides[Array.FindIndex(troopLayout.Counts,count=>count>0)];
                    supportPoint=new Vec((firstSide.A.X+firstSide.B.X)/2,(firstSide.A.Y+firstSide.B.Y)/2);
                    heroIndex=0;abilityIndex=0;actionIndex=0;pendingAbilities.Clear();troopsFinished=DateTime.MinValue;battleDeadline=DateTime.MinValue;heroPoints=Enumerable.Range(0,4).Select(i=>{DeploymentSide side=troopLayout.Sides[settings.FourSides?i:i/2];return new Vec((side.A.X+side.B.X)/2,(side.A.Y+side.B.Y)/2);}).ToArray();
                    log("Battle "+(Completed+1)+": "+(settings.FourSides?"all four sides":reverse?"reverse L":"normal L")+"; "+troopLayout.Summary+" troop clicks on the outer strip.");
                    log("Action order: "+String.Join(" > ",settings.ActionOrder.Select(Settings.ActionLabel))+".");AdvanceAction(now);return;
                }
                else if(State=="Scouting" && (now-entered).TotalSeconds>25) {
                    Vec end=game.Find("end");
                    if(end!=null) { log("Scouting expired without deploying. Restarting the loot search; no battle counted.");game.Click(end);SkippedBases++;ResetLoot();Go("Search home",now); }
                }
                else if(State=="Search home") {
                    if(Ready("attack",out point)) { OpenFromHome(point,now);return; }
                    else if(game.Find("return")!=null) Go("Search return",now);
                }
                else if(State=="Search return" && Ready("return",out point)) { game.Click(point);Go("Search home",now); }
                else if(State=="Select siege" || State=="Select hero") {
                    if(game.Find("return")!=null) {Go("Result",now);return;}
                    bool siege=State=="Select siege";
                    Keys supportKey=siege?Keys.Z:HeroKeys[heroIndex];
                    bool available=((ISupportGame)game).TrySelectSupport(supportKey);
                    log((siege?"Siege (Z)":"Hero ("+supportKey+")")+(available?" selection key sent.":" selection skipped."));
                    if(!siege && !available)heroIndex++;
                    if(available)Go(siege?"Deploy siege":"Deploy hero",now);
                    else if(!siege && heroIndex<4)Go("Select hero",now);
                    else AdvanceAction(now);
                    next=now.AddMilliseconds(Math.Ceiling(tickClock.Elapsed.TotalMilliseconds)+(available?350:40));return;
                }
                else if(State=="Deploy siege" || State=="Deploy hero") {
                    if(game.Find("return")!=null) {Go("Result",now);return;}
                    bool siege=State=="Deploy siege";game.Click(siege?supportPoint:heroPoints[heroIndex]);
                    log((siege?"Siege":"Hero "+HeroKeys[heroIndex])+" placement attempt sent.");
                    if(!siege){pendingAbilities.Add(HeroKeys[heroIndex]);heroIndex++;}
                    if(!siege && heroIndex<4)Go("Select hero",now);else AdvanceAction(now);
                    next=now.AddMilliseconds(Math.Ceiling(tickClock.Elapsed.TotalMilliseconds)+350);return;
                }
                else if(State=="Hero ability") {
                    if(game.Find("return")!=null){Go("Result",now);return;}
                    game.PressKey(pendingAbilities[abilityIndex]);log("Hero "+pendingAbilities[abilityIndex]+" ability key sent at the configured step.");abilityIndex++;
                    if(abilityIndex<pendingAbilities.Count)Go("Hero ability",now);else AdvanceAction(now);
                    next=now.AddMilliseconds(Math.Ceiling(tickClock.Elapsed.TotalMilliseconds)+200);return;
                }
                else if(State=="Select troops") {
                    if(settings.GooglePlay) {game.PressKey(Keys.D1);log("First troop selected with Google Play Games key 1.");}else game.Click(troopCardPoint);
                    Go("Deploying",now);next=now.AddMilliseconds(Math.Ceiling(tickClock.Elapsed.TotalMilliseconds)+500);return;
                }
                else if (State=="Deploying") {
                    if(deployed>0 && deployed%20==0 && game.Find("return")!=null) {log("Battle ended before all configured troop clicks. Skipping remaining placements.");battleDeadline=DateTime.MinValue;Go("Result",now);return;}
                    game.Click(plan[deployed]);deployed++;
                    DateTime inputFinished=now.AddMilliseconds(Math.Ceiling(tickClock.Elapsed.TotalMilliseconds));
                    if(deployed==1 && settings.EndBattleEarly && !deployOnly){
                        int seconds=random.Next(Math.Max(3,settings.AttackDurationSeconds-5),Math.Min(180,settings.AttackDurationSeconds+5)+1);
                        battleDeadline=inputFinished.AddSeconds(seconds);log("Battle cutoff scheduled in "+seconds+" seconds from the first troop placement.");
                    }
                    next=inputFinished.AddMilliseconds(deployed==1?500:random.Next(settings.MinDelayMs,settings.MaxDelayMs+1));
                    if (deployed==settings.Troops) {
                        log("Sent all "+settings.Troops+" deployment clicks.");
                        troopsFinished=inputFinished;
                        if (deployOnly) Stop("Single deployment finished. Battle remains running; no navigation clicks.");
                        else {AdvanceAction(inputFinished,350);log("Waiting for the final troop click to register before the next action.");}
                        return;
                    }
                }
                else if(State=="Select spells") {
                    if(settings.GooglePlay) game.PressKey(Keys.A);else game.Click(settings.SpellButton);
                    openingLayout=Plan.Opening(settings,reverseAttack,random);openingSpells=openingLayout.Points;
                    log((settings.GooglePlay ? "First spell selected with Google Play Games key A" : "Haste selected")+": "+openingLayout.Summary+" opening casts at "+(settings.FourSides?settings.FourSidesSpellDepthPercent:settings.LShapeOpeningDepthPercent)+"% depth; "+(settings.Spells-openingSpells.Count)+" delayed casts.");
                    Go("Opening spells",now);next=now.AddMilliseconds(100);
                }
                else if(State=="Opening spells") {
                    if(spellsCast>0 && spellsCast%6==0 && game.Find("return")!=null) {log("Battle finished after "+spellsCast+" spells. Skipping remaining casts.");Go("Result",now);return;}
                    Vec opening=openingSpells[spellsCast++];game.Click(opening);spellHistory.Add(opening);
                    log("Haste spell "+spellsCast+"/"+settings.Spells+": "+openingLayout.Sides[openingLayout.PointSides[spellsCast-1]].Name+", depth "+(settings.FourSides?settings.FourSidesSpellDepthPercent:settings.LShapeOpeningDepthPercent)+"%.");
                    if(spellsCast==settings.Spells) {log("Sent all "+settings.Spells+" spell casts.");AdvanceAction(now,200);return;}
                    else if(spellsCast==openingSpells.Count) {
                        Go("Spells",now);int delay=random.Next(settings.MinFirstSpellWaitMs,settings.MaxFirstSpellWaitMs+1);spellDue=now.AddMilliseconds(delay);next=now.AddMilliseconds(500);
                        log("Next spell in "+(delay/1000.0).ToString("0.0")+" seconds.");
                    } else next=now.AddMilliseconds(settings.OpeningSpellDelayMs);
                }
                else if(State=="Spells") {
                    // No repeated screenshot/OCR work during the scheduled pause.
                    if(now<spellDue) { next=spellDue;return; }
                    if(game.Find("return")!=null) { log("Battle finished after "+spellsCast+" spells. Skipping remaining casts.");Go("Result",now); }
                    else {
                        if(now>=spellDue) {
                            Vec target=Plan.LaterSpell(settings,reverseAttack,troopsFinished==DateTime.MinValue?0:(now-troopsFinished).TotalSeconds,random,spellHistory);
                            game.Click(target);spellHistory.Add(target);spellsCast++;
                            log("Haste spell "+spellsCast+"/"+settings.Spells+": later batch, depth "+settings.LShapeLaterDepthPercent+"%.");
                            if(spellsCast==settings.Spells) { log("Sent all "+settings.Spells+" spell casts.");AdvanceAction(now,200);return; }
                            else {
                                int delay=random.Next(settings.MinSpellIntervalMs,settings.MaxSpellIntervalMs+1);
                                spellDue=now.AddMilliseconds(delay);
                                log("Next spell in "+(delay/1000.0).ToString("0.0")+" seconds.");
                            }
                        }
                        if(State=="Spells") next=now.AddMilliseconds(Math.Max(40,Math.Min(500,(spellDue-now).TotalMilliseconds)));
                    }
                }
                else if (State=="Result") {
                    if(Ready("return",out point)) { game.Click(point); Go("Return home",now); }
                    if(State=="Result") { next=now.AddMilliseconds(2500);return; }
                }
                else if(State=="Ending attack") {
                    if(game.Find("return")!=null) {battleDeadline=DateTime.MinValue;Go("Result",now);}
                    else if(Ready("retreat",out point)) {game.Click(point);Go("Exit battle",now);}
                    else if(game.Find("surrenderTitle")!=null) Go("Exit battle",now);
                }
                else if(State=="Exit battle") {
                    if(Ready("return",out point)) { game.Click(point);Go("Return home",now); }
                    else if(game.Find("surrenderTitle")!=null && (point=game.Find("confirmSurrender"))!=null) {
                        log("Confirmed the recognized battle-exit dialog.");game.Click(point);battleDeadline=DateTime.MinValue;Go("Result",now);
                    }
                }
                else if (State=="Return home" && Ready("attack",out point)) {
                    Completed++; log("Completed battle "+Completed+"/"+limit+".");
                    if (Completed>=limit) Stop("Finished "+Completed+" battles. Stopped at home.");
                    else { ResetLoot();battleDeadline=DateTime.MinValue;scannedNavigation=false;OpenFromHome(point,now);return; }
                }
                if (State!="Deploying" && State!="Spells" && State!="Opening spells" && State!="Select spells") next=now.AddMilliseconds(650);
            } catch (Exception ex) { Stop("Stopped: "+ex.Message); }
        }
    }
    public sealed class OcrLine { public string Text; public double X,Y,Width,Height; }
    public static class Vision {
        public const int W=1600,H=900;
        public static Rectangle Region(string name) {
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
            // Stylized Battle lettering: t may be f/e, and l may be i/1.
            if (name=="attack") return Regex.IsMatch(s,@"^ba[tfe][tf][li1]e$") || Regex.IsMatch(text??"",@"\battack\b",RegexOptions.IgnoreCase) || s=="attak";
            if (name=="next") return Regex.IsMatch(text??"",@"\bnext\b",RegexOptions.IgnoreCase) || NearLabel(s,"next");
            if (name=="end") return NearLabel(Clean((text??"").Replace('+','t')),"endbattle") || Regex.IsMatch(s,@"^endba[tfe][tf][li1]e$");
            if (name=="retreat") return Label("end",text) || NearLabel(s,"surrender");
            if (name=="surrenderTitle") return NearLabel(s,"surrender") || Label("end",text);
            if (name=="confirmSurrender") return s=="okay";
            if (name=="launch") return Regex.IsMatch(text??"",@"\battack\b",RegexOptions.IgnoreCase);
            if (name=="find") return NearLabel(s,"findamatch") || NearLabel(s,"findmatch") || (s.Contains("find") && s.Contains("battle"));
            return NearLabel(s,"return") || s.Contains("returnhome") || s.Contains("returntohome");
        }
        public static bool NearLabel(string value,string expected) {
            if(Math.Abs(value.Length-expected.Length)>1) return false;
            int i=0,j=0,edits=0;
            while(i<value.Length && j<expected.Length) {
                if(value[i]==expected[j]) { i++;j++;continue; }
                if(++edits>1) return false;
                if(value.Length>=expected.Length) i++;
                if(expected.Length>=value.Length) j++;
            }
            return edits+(value.Length-i)+(expected.Length-j)<=1;
        }
        public static Vec Locate(string name,IEnumerable<OcrLine> lines,Rectangle roi) {
            var all=lines.ToList();
            var candidates=all.Where(l=>Label(name,l.Text)).ToList();
            if (name=="find" && candidates.Count==0) {
                foreach(OcrLine a in all) foreach(OcrLine b in all) {
                    if(a==b || !Clean(a.Text).Contains("find") || !Clean(b.Text).Contains("match")) continue;
                    if(Math.Abs((a.X+a.Width/2)-(b.X+b.Width/2))>180 || Math.Abs(a.Y-b.Y)>130) continue;
                    double left=Math.Min(a.X,b.X),top=Math.Min(a.Y,b.Y);
                    candidates.Add(new OcrLine{Text=a.Text+" "+b.Text,X=left,Y=top,Width=Math.Max(a.X+a.Width,b.X+b.Width)-left,Height=Math.Max(a.Y+a.Height,b.Y+b.Height)-top});
                }
            }
            if (candidates.Count!=1) return null;
            OcrLine line=candidates[0];
            double x=(roi.X+line.X+line.Width/2)/W, y=(roi.Y+line.Y+line.Height/2)/H;
            if (name=="attack" && (x>.24 || y<.65)) return null;
            if (name=="next" && x<.78) return null;
            if (name=="launch" && (x<.72 || y<.82)) return null;
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
        public static void DrawPlan(Bitmap canvas,Settings settings) {
            using(Graphics g=Graphics.FromImage(canvas)) using(Font font=new Font("Segoe UI",12,FontStyle.Bold)) {
                g.SmoothingMode=SmoothingMode.AntiAlias;
                for (int flip=0;flip<(settings.FourSides?1:2);flip++) {
                    Color color=flip==0 ? Color.Cyan : Color.Magenta;
                    PlacementPlan layout=Plan.Troops(settings,flip==1,new Random(42));
                    using(Pen pen=new Pen(color,3)) using(Brush brush=new SolidBrush(color)) {
                        foreach(DeploymentSide side in layout.Sides) {
                            g.DrawLine(pen,(float)(side.A.X*W),(float)(side.A.Y*H),(float)(side.B.X*W),(float)(side.B.Y*H));
                        }
                        List<Vec> points=layout.Points;
                        for(int i=0;i<points.Count;i++) { float x=(float)(points[i].X*W),y=(float)(points[i].Y*H); g.FillEllipse(brush,x-4,y-4,8,8); g.DrawString((i+1).ToString(),font,brush,x+5,y-18); }
                        g.DrawString(layout.Summary,font,brush,240,flip==0?800:825);
                    }
                    if(settings.UseSpells && settings.Spells>0) {
                        var random=new Random(42);List<Vec> spells=Plan.OpeningSpells(settings,flip==1,random);
                        using(Pen openingPen=new Pen(Color.Yellow,3)) using(Pen laterPen=new Pen(Color.OrangeRed,3)) {
                            foreach(Vec p in spells) g.DrawEllipse(openingPen,(float)(p.X*W)-9,(float)(p.Y*H)-9,18,18);
                            if(!settings.FourSides) for(int i=spells.Count;i<settings.Spells;i++) {
                                Vec p=Plan.LaterSpell(settings,flip==1,0,random,spells);spells.Add(p);
                                g.DrawRectangle(laterPen,(float)(p.X*W)-8,(float)(p.Y*H)-8,16,16);
                            }
                        }
                    }
                }
                g.FillRectangle(Brushes.Black,200,850,1200,50);
                g.DrawString(settings.FourSides?"CYAN: troops, all four sides     PREVIEW ONLY":"CYAN: troops, normal L     MAGENTA: troops, reverse L     PREVIEW ONLY",font,Brushes.White,210,852);
                g.DrawString("YELLOW circles: opening spells     ORANGE squares: later spells     Depth: "+(settings.FourSides?settings.FourSidesSpellDepthPercent+"%":settings.LShapeOpeningDepthPercent+"% / "+settings.LShapeLaterDepthPercent+"%"),font,Brushes.White,210,875);
            }
        }
    }
    public static class Native {
        static string lastGoogleDiagnostic;
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L,T,R,B; }
        [StructLayout(LayoutKind.Sequential)] public struct Pt { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int X,Y; public uint Data,Flags,Time; public IntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] struct KeyboardInput { public ushort Key,Scan; public uint Flags,Time; public IntPtr Extra; }
        [StructLayout(LayoutKind.Explicit)] struct InputData {
            [FieldOffset(0)] public MouseInput Mouse;
            [FieldOffset(0)] public KeyboardInput Keyboard;
        }
        [StructLayout(LayoutKind.Sequential)] struct Input { public uint Type; public InputData Data; }
        public delegate bool EnumProc(IntPtr hwnd,IntPtr arg);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback,IntPtr arg);
        [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr window,EnumProc callback,IntPtr arg);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr window,StringBuilder name,int length);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd,out Rect rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd,ref Pt point);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int length);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd,int command);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd,uint command);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")] static extern uint SendInput(uint count,Input[] inputs,int size);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Pt point);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
        public static bool Escape() { return (GetAsyncKeyState(0x1B)&0x8001)!=0 || (GetAsyncKeyState(0x78)&0x8001)!=0; }
        public static IntPtr FindWindow() {
            return FindPlayer(false);
        }
        public static IntPtr FindGoogleWindow() { return FindPlayer(true); }
        static IntPtr FindPlayer(bool google) {
            var handles=new List<IntPtr>();
            EnumWindows(delegate(IntPtr h,IntPtr p) {
                if (!IsWindowVisible(h)) return true;
                Rect bounds;
                if (!GetClientRect(h,out bounds)) return true;
                var title=new StringBuilder(512); GetWindowText(h,title,title.Capacity);
                if(google) {
                    if(!title.ToString().StartsWith("Clash of Clans",StringComparison.OrdinalIgnoreCase) || bounds.R-bounds.L<600 || bounds.B-bounds.T<400 || GetWindow(h,4)!=IntPtr.Zero) return true;
                } else if (!PlayerWindows.IsMain(title.ToString(),bounds.R-bounds.L,bounds.B-bounds.T,GetWindow(h,4)!=IntPtr.Zero)) return true;
                uint id; GetWindowThreadProcessId(h,out id);
                try { using(Process process=Process.GetProcessById((int)id)) { if (process.ProcessName.Equals(google?"crosvm":"HD-Player",StringComparison.OrdinalIgnoreCase)) handles.Add(h); } } catch { }
                return true;
            },IntPtr.Zero);
            string name=google?"Google Play Games Clash of Clans":"BlueStacks App Player";
            if (handles.Count==0) throw new Exception("Open "+name+" at your main village, then click Start. Its game window must be at least 600 x 400 pixels.");
            if (handles.Count!=1) throw new Exception("More than one "+name+" window is open. Close the other game instances, then click Start.");
            return handles[0];
        }
        public static void ActivatePlayer(IntPtr window) {
            if (!IsWindow(window)) throw new Exception("The game window was closed. Open it at your main village and click Start again.");
            ShowWindow(window,3);
            SetForegroundWindow(window);
        }
        public static Rectangle Viewport(IntPtr window,Settings settings) {
            if(settings.GooglePlay) return GoogleViewport(window);
            Rect rect; Pt origin=new Pt();
            if (!GetClientRect(window,out rect) || !ClientToScreen(window,ref origin)) throw new Exception("Cannot read BlueStacks bounds.");
            // The BlueStacks bar and toolbar are inside its borderless client.
            int width=rect.R-rect.L-settings.RightChrome, height=rect.B-rect.T-settings.TopChrome;
            if (width<600 || height<400) throw new Exception("Maximize the BlueStacks window first.");
            int w=width,h=(int)Math.Round(width/settings.ViewAspect);
            if (h>height) { h=height; w=(int)Math.Round(height*settings.ViewAspect); }
            return new Rectangle(origin.X+(width-w)/2,origin.Y+settings.TopChrome+(height-h)/2,w,h);
        }
        public static Rectangle GoogleViewport(IntPtr window) {
            Rect rect;Pt origin=new Pt();
            if(!GetClientRect(window,out rect) || !ClientToScreen(window,ref origin)) throw new Exception("Cannot read Google Play Games bounds.");
            Rectangle client=new Rectangle(origin.X,origin.Y,rect.R-rect.L,rect.B-rect.T);
            // The accessible rendering pane excludes Google's custom title bar and sidebar.
            var root=System.Windows.Automation.AutomationElement.FromHandle(window);
            var panes=root.FindAll(System.Windows.Automation.TreeScope.Descendants,new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty,System.Windows.Automation.ControlType.Pane));
            Rectangle best=Rectangle.Empty;
            var diagnostic=new List<string>();diagnostic.Add("Client: "+client);
            foreach(System.Windows.Automation.AutomationElement pane in panes) {
                string name=pane.Current.Name;
                System.Windows.Rect r=pane.Current.BoundingRectangle;
                diagnostic.Add("Pane name="+name+" class="+pane.Current.ClassName+" id="+pane.Current.AutomationId+" bounds="+r);
                if(r.IsEmpty) continue;
                Rectangle candidate=Rectangle.Round(new RectangleF((float)r.X,(float)r.Y,(float)r.Width,(float)r.Height));
                if(!IsGoogleGameArea(candidate,client)) continue;
                if(best.IsEmpty || candidate.Width* candidate.Height<best.Width*best.Height) best=candidate;
            }
            EnumChildWindows(window,delegate(IntPtr child,IntPtr ignored) {
                Rectangle candidate=ClientBounds(child);var cls=new StringBuilder(256);GetClassName(child,cls,cls.Capacity);
                diagnostic.Add("Child class="+cls+" bounds="+candidate);
                if(IsGoogleGameArea(candidate,client) && (best.IsEmpty || candidate.Width*candidate.Height<best.Width*best.Height)) best=candidate;
                return true;
            },IntPtr.Zero);
            string detail=String.Join(Environment.NewLine,diagnostic);
            if(detail!=lastGoogleDiagnostic) { File.WriteAllText(AppFiles.DataFile("google-viewport.txt"),detail);lastGoogleDiagnostic=detail; }
            if(best.IsEmpty) throw new Exception("Google Play Games game area was not found. Keep the sidebar/window layout visible and open Clash of Clans; no clicks were sent.");
            return FitGameArea(Rectangle.Intersect(best,client));
        }
        public static bool IsGoogleGameArea(Rectangle candidate,Rectangle client) {
            // Google renders a few pixels into its custom frame at some maximize sizes.
            Rectangle tolerance=client;tolerance.Inflate(8,8);
            return candidate.Width>=600 && candidate.Height>=400 && tolerance.Contains(candidate) && candidate.Width<client.Width && Math.Abs((double)candidate.Width/candidate.Height-16.0/9)<.025;
        }
        public static Rectangle ClientBounds(IntPtr window) {
            Rect r;Pt p=new Pt();if(!GetClientRect(window,out r) || !ClientToScreen(window,ref p)) throw new Exception("Cannot read game window bounds.");
            return new Rectangle(p.X,p.Y,r.R-r.L,r.B-r.T);
        }
        public static Rectangle FitGameArea(Rectangle area) {
            int w=area.Width,h=(int)Math.Round(w*9.0/16);
            if(h>area.Height) { h=area.Height;w=(int)Math.Round(h*16.0/9); }
            return new Rectangle(area.X+(area.Width-w)/2,area.Y+(area.Height-h)/2,w,h);
        }
        public static void Click(IntPtr window,Rectangle view,Vec point) {
            if (Escape() || GetForegroundWindow()!=window) throw new Exception("Stop key pressed or the game lost focus.");
            Pt p=new Pt{X=view.X+(int)Math.Round(point.X*view.Width),Y=view.Y+(int)Math.Round(point.Y*view.Height)};
            if (!view.Contains(p.X,p.Y) || GetAncestor(WindowFromPoint(p),2)!=window) throw new Exception("Click blocked: another window covers the game.");
            if (!SetCursorPos(p.X,p.Y)) throw new Exception("Could not position the mouse.");
            if (Escape()) throw new Exception("Emergency stop.");
            var down=new Input{Type=0,Data=new InputData{Mouse=new MouseInput{Flags=2}}};
            var up=new Input{Type=0,Data=new InputData{Mouse=new MouseInput{Flags=4}}};
            if (SendInput(1,new[]{down},Marshal.SizeOf(typeof(Input)))!=1) throw new Exception("Mouse input failed.");
            try { System.Threading.Thread.Sleep(35); }
            finally { if(SendInput(1,new[]{up},Marshal.SizeOf(typeof(Input)))!=1) throw new Exception("Mouse release failed. Run stopped."); }
        }
        public static void PressKey(IntPtr window,Keys key) {
            if(key!=Keys.D1 && key!=Keys.A && key!=Keys.Z && key!=Keys.Q && key!=Keys.W && key!=Keys.E && key!=Keys.R) throw new Exception("Unsupported selection key.");
            if(Escape() || window==IntPtr.Zero || GetForegroundWindow()!=window) throw new Exception("Stop key pressed or the game lost focus.");
            foreach(int held in new[]{0x10,0x11,0x12,0x5B,0x5C,(int)key})
                if((GetAsyncKeyState(held)&0x8000)!=0) throw new Exception("Release held keyboard keys before running Google Play Games selection.");
            // Physical-key scan codes are accepted by game controls independently of text entry.
            ushort scan=(ushort)(key==Keys.D1?0x02:key==Keys.A?0x1E:key==Keys.Z?0x2C:key==Keys.Q?0x10:key==Keys.W?0x11:key==Keys.E?0x12:0x13);
            var down=new Input{Type=1,Data=new InputData{Keyboard=new KeyboardInput{Scan=scan,Flags=8}}};
            var up=new Input{Type=1,Data=new InputData{Keyboard=new KeyboardInput{Scan=scan,Flags=10}}};
            int size=Marshal.SizeOf(typeof(Input));
            if(SendInput(1,new[]{down},size)!=1) throw new Exception("Selection key press failed. Check the game's default key mappings.");
            try { System.Threading.Thread.Sleep(35); }
            finally { if(SendInput(1,new[]{up},size)!=1) throw new Exception("Selection key release failed. Run stopped."); }
        }
    }
    public sealed class ScreenReader {
        readonly OcrReader ocr; readonly Action<string> log; readonly string folder;
        readonly JavaScriptSerializer json=new JavaScriptSerializer();
        readonly Dictionary<string,DateTime> lastReport=new Dictionary<string,DateTime>();
        public ScreenReader(OcrReader ocr,string folder,Action<string> log) { this.ocr=ocr;this.folder=folder;this.log=log; }
        OcrLine[] ReadImage(Bitmap image) {
            using(var stream=new MemoryStream()) { image.Save(stream,ImageFormat.Png);return json.Deserialize<OcrLine[]>(ocr(stream.ToArray()))??new OcrLine[0]; }
        }
        bool LootValue(Bitmap image,Rectangle region,string label,out long value,bool developer=false) {
            value=0;
            using(Bitmap crop=image.Clone(region,PixelFormat.Format32bppArgb)) {
                var votes=new List<long>();
                var attempts=new List<string>();
                foreach(int scale in new[]{2,4,3,5,1}) {
                    using(Bitmap enlarged=Vision.Resize(crop,crop.Width*scale,crop.Height*scale)) using(Bitmap mask=Vision.WhiteDigits(enlarged,developer)) {
                        int digits=developer?Vision.LootDigitCount(mask):0;
                        OcrLine[] lines;
                        if(developer) {
                            using(Bitmap padded=new Bitmap(mask.Width+40,mask.Height+20)) {
                                using(Graphics graphics=Graphics.FromImage(padded)){graphics.Clear(Color.White);graphics.DrawImageUnscaled(mask,20,10);}
                                lines=ReadImage(padded);
                            }
                        }else lines=ReadImage(mask);
                        var values=new List<long>();
                        attempts.Add(scale+"x: "+String.Join(" | ",lines.Select(l=>l.Text)));
                        foreach(OcrLine line in lines) { long parsed;if(Vision.TryLootNumber(line.Text,out parsed)) {
                            if(developer && (digits==0 || parsed.ToString(CultureInfo.InvariantCulture).Length!=digits)) {attempts.Add("visible digit count "+digits+" disagrees with "+parsed);continue;}
                            values.Add(parsed);
                        } }
                        values=values.Distinct().ToList();
                        if(values.Count==1) {
                            votes.Add(values[0]);
                            if(votes.Count(v=>v==values[0])>=2) { value=values[0];return true; }
                        }
                    }
                }
                if(log!=null) log("Loot OCR ["+label+"] did not agree across image scales; retrying. "+String.Join("; ",attempts));
                if(!String.IsNullOrWhiteSpace(folder)) {
                    try { string directory=Path.Combine(folder,"diagnostics");Directory.CreateDirectory(directory);crop.Save(Path.Combine(directory,"last-loot-"+label+".png"),ImageFormat.Png); } catch(IOException) { }
                }
            }
            return false;
        }
        public LootReading ReadLoot(Bitmap image,bool googlePlay=false,bool readGold=true,bool readElixir=true,bool readDark=false,bool developer=false) {
            long gold=0,elixir=0,dark=0;
            Rectangle goldRegion=googlePlay ? new Rectangle(65,94,220,32) : Vision.GoldLoot;
            Rectangle elixirRegion=googlePlay ? new Rectangle(65,133,235,32) : Vision.ElixirLoot;
            Rectangle darkRegion=googlePlay?new Rectangle(65,173,235,32):new Rectangle(65,191,235,43);
            if(developer){goldRegion=new Rectangle(57,94,158,26);elixirRegion=new Rectangle(57,133,158,26);darkRegion=new Rectangle(57,173,158,26);}
            bool goldOk=!readGold || LootValue(image,goldRegion,"gold",out gold,developer),elixirOk=!readElixir || LootValue(image,elixirRegion,"elixir",out elixir,developer),darkOk=!readDark || LootValue(image,darkRegion,"dark-elixir",out dark,developer);
            return goldOk && elixirOk && darkOk ? new LootReading(gold,elixir,dark) : null;
        }
        public Vec FirstTroopCard(Bitmap image) {
            Rectangle roi=new Rectangle(0,768,850,54);
            using(Bitmap crop=image.Clone(roi,PixelFormat.Format32bppArgb)) using(Bitmap large=Vision.Resize(crop,crop.Width*3,crop.Height*3)) {
                foreach(bool threshold in new[]{true,false}) {
                    using(Bitmap input=threshold?Vision.WhiteDigits(large):new Bitmap(large)) {
                        var candidates=new List<OcrLine>();
                        foreach(OcrLine line in ReadImage(input)) foreach(Match marker in Regex.Matches(line.Text??"",@"[xX×]\s*\d+")) {
                            double unit=line.Width/Math.Max(1,line.Text.Length);
                            candidates.Add(new OcrLine{Text=marker.Value,X=line.X+marker.Index*unit,Width=marker.Length*unit});
                        }
                        candidates=candidates.OrderBy(l=>l.X).ToList();
                        if(candidates.Count==0) continue;
                        OcrLine first=candidates[0];
                        // The stack marker is right-aligned within the first troop card.
                        double x=(first.X+first.Width/2)/3;
                        if(x<40 || x>800 || Regex.IsMatch(first.Text,@"[xX×]\s*0\s*$")) return null;
                        return new Vec((x-18)/Vision.W,.93);
                    }
                }
            }
            return null;
        }
        public Vec Find(Bitmap image,string button) {
            Rectangle region=Vision.Region(button);
            using(Bitmap crop=image.Clone(region,PixelFormat.Format32bppArgb)) using(Bitmap enlarged=Vision.Resize(crop,crop.Width*2,crop.Height*2)) using(var stream=new MemoryStream()) {
                enlarged.Save(stream,ImageFormat.Png);
                string result=ocr(stream.ToArray());
                OcrLine[] lines=json.Deserialize<OcrLine[]>(result) ?? new OcrLine[0];
                foreach(OcrLine line in lines) { line.X/=2;line.Y/=2;line.Width/=2;line.Height/=2; }
                Vec found=Vision.Locate(button,lines,region);
                if((button=="next" || button=="attack" || button=="end" || button=="retreat" || button=="return" || button=="surrenderTitle" || button=="confirmSurrender") && found==null) {
                    foreach(int scale in new[]{2,3}) {
                        using(Bitmap large=Vision.Resize(crop,crop.Width*scale,crop.Height*scale)) using(Bitmap mask=Vision.WhiteDigits(large)) {
                            OcrLine[] processed=ReadImage(mask);
                            foreach(OcrLine line in processed) { line.X/=scale;line.Y/=scale;line.Width/=scale;line.Height/=scale; }
                            Vec candidate=Vision.Locate(button,processed,region);
                            if(candidate!=null) { found=candidate;lines=processed;break; }
                        }
                    }
                }
                DateTime previous;
                if(log!=null && (!lastReport.TryGetValue(button,out previous) || (DateTime.Now-previous).TotalSeconds>=5)) {
                    lastReport[button]=DateTime.Now;
                    string seen=String.Join(" | ",lines.Select(l=>l.Text));
                    string label=button=="attack" ? "home Battle" : button=="find" ? "Find a Match" : button=="launch" ? "army Attack" : button;
                    log("Screen reader ["+label+"]: "+(found!=null ? "button found" : "not found")+"; text: "+(seen.Length==0 ? "(none)" : seen));
                    if(found==null && folder!=null) {
                        try {
                            string directory=Path.Combine(folder,"diagnostics");Directory.CreateDirectory(directory);
                            crop.Save(Path.Combine(directory,"last-"+button+".png"),ImageFormat.Png);
                            File.WriteAllText(Path.Combine(directory,"last-"+button+".json"),result);
                        } catch(IOException) { }
                    }
                }
                return found;
            }
        }
    }
    public abstract class CapturedGame : ICapturedGame {
        readonly Bitmap spellReference; protected readonly ScreenReader reader;
        protected CapturedGame(OcrReader ocr,string referencePath,Action<string> log) {
            reader=new ScreenReader(ocr,Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(referencePath)),"data"),log);
            string spellPath=Path.Combine(Path.GetDirectoryName(referencePath),"spell-face.png");
            if(File.Exists(spellPath)) spellReference=new Bitmap(spellPath);
        }
        public abstract void Guard();
        public abstract Bitmap Capture();
        public abstract void Click(Vec point);
        public virtual void PressKey(Keys key) { throw new NotSupportedException("Keyboard selection is only available for Google Play Games desktop control."); }
        protected virtual bool GoogleLayout { get { return false; } }
        protected virtual bool DeveloperLayout {get{return false;}}
        public Vec Find(string button) { using(Bitmap image=Capture()) return reader.Find(image,button); }
        public LootReading ReadLoot(bool gold=true,bool elixir=true,bool darkElixir=false) { using(Bitmap image=Capture()) return reader.ReadLoot(image,GoogleLayout,gold,elixir,darkElixir,DeveloperLayout); }
        public Vec FirstTroopCard() {using(Bitmap image=Capture()) return reader.FirstTroopCard(image);}
        public bool SpellVisible() {
            if(spellReference==null) return false;
            using(Bitmap image=Capture()) using(Bitmap face=image.Clone(Vision.SpellFace,PixelFormat.Format32bppArgb)) return Vision.Similar(face,spellReference);
        }
        public void Dispose() { if(spellReference!=null) spellReference.Dispose(); }
    }
    public sealed class DesktopGame : CapturedGame,ISupportGame {
        readonly IntPtr window; readonly Rectangle viewport; readonly Settings settings;
        protected override bool GoogleLayout { get { return settings.GooglePlay; } }
        readonly Rectangle windowBounds;DateTime nextViewCheck;
        public DesktopGame(IntPtr window,Settings settings,OcrReader ocr,string referencePath,Action<string> log=null) : base(ocr,referencePath,log) {
            this.window=window;this.settings=settings;viewport=Native.Viewport(window,settings);
            windowBounds=Native.ClientBounds(window);
            if(log!=null) log(settings.PlayerName+" game area: "+viewport+". Desktop focus required.");
        }
        public override void Guard() {
            if (!Native.IsWindow(window) || Native.IsIconic(window)) throw new Exception(settings.PlayerName+" closed or minimized.");
            if (Native.Escape()) throw new Exception("Emergency stop (Esc/F9).");
            if (Native.GetForegroundWindow()!=window) throw new Exception(settings.PlayerName+" is no longer the active window. The run stopped; keep it in front while it runs.");
            if(Native.ClientBounds(window)!=windowBounds) throw new Exception(settings.PlayerName+" moved or resized. Return to your main village and click Start again.");
            if(DateTime.UtcNow>=nextViewCheck) {
                if(Native.Viewport(window,settings)!=viewport) throw new Exception("Game area changed. Return to your main village and click Start again.");
                nextViewCheck=DateTime.UtcNow.AddSeconds(2);
            }
            if (!SystemInformation.VirtualScreen.Contains(viewport)) throw new Exception("Game is partly offscreen.");
            for (int i=0;i<5;i++) {
                Native.Pt p=new Native.Pt{ X=viewport.X+(int)(viewport.Width*(i==0?.5:i<3?.2:.8)), Y=viewport.Y+(int)(viewport.Height*(i==0?.5:i%2==0?.2:.8)) };
                if (Native.GetAncestor(Native.WindowFromPoint(p),2)!=window) throw new Exception("Another window covers "+settings.PlayerName+".");
            }
        }
        public override Bitmap Capture() {
            Guard();
            using(Bitmap source=new Bitmap(viewport.Width,viewport.Height,PixelFormat.Format32bppArgb)) {
                using(Graphics g=Graphics.FromImage(source)) g.CopyFromScreen(viewport.Location,Point.Empty,viewport.Size);
                return Vision.Resize(source,Vision.W,Vision.H);
            }
        }
        public override void Click(Vec point) { Guard(); Native.Click(window,viewport,point); }
        public bool TrySelectSupport(Keys key) {
            if(!settings.GooglePlay) return false;
            // The user explicitly requests direct shortcut attempts, including
            // unavailable heroes. Do not gate this on card recognition.
            PressKey(key);return true;
        }
        public override void PressKey(Keys key) {
            if(!settings.GooglePlay) throw new NotSupportedException("BlueStacks uses troop and spell card clicks.");
            Guard();Native.PressKey(window,key);
        }
    }
    public sealed class AdbClient {
        readonly string executable,serial;
        public string Serial { get { return serial; } }
        public static readonly string Configuration=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"BlueStacks_nxt","bluestacks.conf");
        public AdbClient(string executable,string serial) {
            if(!ValidSerial(serial)) throw new Exception("ADB must use a local emulator serial or 127.0.0.1 port.");
            this.executable=executable;this.serial=serial;
        }
        public static bool ValidSerial(string value) {
            if(value==null) return false;
            Match emulator=Regex.Match(value,@"^emulator-(\d{1,5})$");
            if(emulator.Success) { int consolePort;return Int32.TryParse(emulator.Groups[1].Value,out consolePort) && consolePort>0 && consolePort<65535; }
            if(!Regex.IsMatch(value,@"^(127\.0\.0\.1|localhost):\d{1,5}$")) return false;
            int port;return Int32.TryParse(value.Substring(value.LastIndexOf(':')+1),out port) && port>0 && port<=65535;
        }
        public static bool Enabled(Settings settings) {
            if(settings.GooglePlay)return true;
            if(!settings.UseAdbWhenAvailable || !File.Exists(Configuration)) return false;
            return File.ReadLines(Configuration).Any(l=>Regex.IsMatch(l,@"^bst\.enable_adb_access=""1""\s*$"));
        }
        public static AdbClient Connect(Settings settings) {
            if(settings.GooglePlay) {
                string googlePath=String.IsNullOrWhiteSpace(settings.GoogleAdbPath)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Google","Play Games Developer Emulator","current","emulator","adb.exe"):settings.GoogleAdbPath;
                if(!File.Exists(googlePath))throw new Exception("Google Play Games Developer Emulator ADB was not found. Install/open the Developer Edition or set GoogleAdbPath in settings.json. Desktop control is not used for this mode.");
                string address=String.IsNullOrWhiteSpace(settings.GoogleAdbSerial)?"localhost:6520":settings.GoogleAdbSerial;
                if(!ValidSerial(address) || address.StartsWith("emulator-"))throw new Exception("GoogleAdbSerial must be a local loopback address, normally localhost:6520.");
                var googleClient=new AdbClient(googlePath,address);
                string devices=Encoding.UTF8.GetString(googleClient.Run("devices",8000));
                string alias=address.StartsWith("localhost:",StringComparison.Ordinal)?address.Replace("localhost:","127.0.0.1:"):address.Replace("127.0.0.1:","localhost:");
                var onlineGoogle=Regex.Matches(devices,@"(?m)^(localhost:\d+|127\.0\.0\.1:\d+)\s+device\b").Cast<Match>().Select(m=>m.Groups[1].Value).ToList();
                // Reconnecting a live localhost endpoint can briefly take it offline.
                // Reuse its online loopback alias instead of creating two transports.
                if(!onlineGoogle.Contains(address) && onlineGoogle.Contains(alias))googleClient=new AdbClient(googlePath,alias);
                else if(!onlineGoogle.Contains(address)) {
                    foreach(string stale in new[]{address,alias}) if(Regex.IsMatch(devices,@"(?m)^"+Regex.Escape(stale)+@"\s+offline\b"))googleClient.Run("disconnect "+stale,4000);
                    address=address.Replace("localhost:","127.0.0.1:");googleClient=new AdbClient(googlePath,address);googleClient.Run("connect "+address,8000);
                }
                if(googleClient.Text("get-state",4000).Trim()!="device")throw new Exception("Google Developer Emulator is not ready. Open it with Clash of Clans running.");
                return googleClient;
            }
            if(!Enabled(settings)) throw new Exception("Local ADB is disabled in BlueStacks. Enable it under Settings > Advanced to use background control.");
            string path=settings.AdbPath;
            if(String.IsNullOrWhiteSpace(path)) path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"BlueStacks_nxt","HD-Adb.exe");
            if(!File.Exists(path)) throw new Exception("BlueStacks HD-Adb.exe was not found. Set AdbPath in settings.json.");
            var probe=new AdbClient(path,"127.0.0.1:5555");
            string listed=Encoding.UTF8.GetString(probe.Run("devices",8000));
            var online=Regex.Matches(listed,@"(?m)^(emulator-\d+|127\.0\.0\.1:\d+)\s+device\b").Cast<Match>().Select(m=>m.Groups[1].Value).ToList();
            string device=settings.AdbSerial;
            if(String.IsNullOrWhiteSpace(device)) {
                var ports=new HashSet<string>();
                foreach(string line in File.ReadLines(Configuration)) {
                    Match match=Regex.Match(line,@"^bst\.instance\.[^.]+\.adb_port=""(\d+)""");
                    if(match.Success) ports.Add(match.Groups[1].Value);
                }
                if(ports.Count!=1) throw new Exception("More than one Android port is configured. Set AdbSerial to the running instance's local address in settings.json.");
                string port=ports.First();
                string registered="emulator-"+(Int32.Parse(port)-1);
                device=online.Contains(registered) ? registered : "127.0.0.1:"+port;
            }
            var client=new AdbClient(path,device);
            if(!online.Contains(device)) {
                if(device.StartsWith("emulator-",StringComparison.Ordinal)) throw new Exception("The selected local Android emulator is not online.");
                client.Run("connect "+device,8000);
            }
            if(client.Text("get-state",4000).Trim()!="device") throw new Exception("The local BlueStacks Android connection is not ready.");
            return client;
        }
        byte[] Run(string arguments,int timeout) {
            using(var process=new Process()) using(var data=new MemoryStream()) {
                process.StartInfo=new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                process.Start();
                var read=process.StandardOutput.BaseStream.CopyToAsync(data);
                var errors=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(timeout)) { try { process.Kill(); } catch(InvalidOperationException) { } throw new Exception("The local Android connection timed out. No further taps sent."); }
                if(!read.Wait(2000) || !errors.Wait(2000)) throw new Exception("Android output did not finish.");
                if(process.ExitCode!=0) throw new Exception("Android command failed: "+errors.Result.Trim());
                return data.ToArray();
            }
        }
        public string Text(string arguments,int timeout=4000) { return Encoding.UTF8.GetString(Run("-s "+serial+" "+arguments,timeout)); }
        public Bitmap Screenshot() {
            byte[] bytes=Run("-s "+serial+" exec-out screencap -p",6000);
            if(bytes.Length<8 || bytes[0]!=137 || bytes[1]!=80 || bytes[2]!=78 || bytes[3]!=71) throw new Exception("Android did not return a valid screenshot.");
            using(var stream=new MemoryStream(bytes)) using(var image=new Bitmap(stream)) return new Bitmap(image);
        }
        public void Tap(int x,int y) { Text("shell input tap "+x+" "+y); }
        public static int AndroidKeyCode(Keys key) {
            switch(key){case Keys.D1:return 8;case Keys.A:return 29;case Keys.Z:return 54;case Keys.Q:return 45;case Keys.W:return 51;case Keys.E:return 33;case Keys.R:return 46;default:throw new Exception("Unsupported Android selection key.");}
        }
        public void Key(Keys key) {Text("shell input keyevent "+AndroidKeyCode(key));}
    }
    public sealed class AndroidGame : CapturedGame,ISupportGame {
        readonly bool google;protected override bool GoogleLayout {get{return google;}}
        protected override bool DeveloperLayout {get{return google;}}
        readonly AdbClient client;readonly IntPtr window;Size resolution;DateTime checkedApp;
        public AndroidGame(IntPtr window,Settings settings,OcrReader ocr,string referencePath,Action<string> log) : base(ocr,referencePath,log) {
            this.window=window;google=settings.GooglePlay;client=AdbClient.Connect(settings);
            if(log!=null) log("Android connected: "+client.Serial+". Desktop focus is not required.");
        }
        public override void Guard() {
            if(Native.Escape()) throw new Exception("Emergency stop (F9 / Esc).");
            if(window!=IntPtr.Zero && !Native.IsWindow(window)) throw new Exception("BlueStacks was closed.");
            if((DateTime.Now-checkedApp).TotalSeconds>=3) {
                string focus=client.Text("shell dumpsys window");
                if(!Regex.IsMatch(focus,@"mCurrentFocus[^\r\n]*com\.supercell\.clashofclans")) throw new Exception("Clash of Clans is not the open app inside the Android emulator. Open the game and start again. Windows focus is not required.");
                checkedApp=DateTime.Now;
            }
        }
        public override Bitmap Capture() {
            Guard();
            using(Bitmap image=client.Screenshot()) {
                if(Math.Abs((double)image.Width/image.Height-16.0/9.0)>.02) throw new Exception("Set the Android emulator display to 16:9 before running.");
                if(resolution!=Size.Empty && resolution!=image.Size) throw new Exception("Android display resolution changed during the run.");
                resolution=image.Size;return Vision.Resize(image,Vision.W,Vision.H);
            }
        }
        public override void Click(Vec point) {
            Guard();
            if(resolution==Size.Empty) throw new Exception("Read the Android screen before sending taps.");
            if(point.X<0 || point.X>1 || point.Y<0 || point.Y>1) throw new Exception("Tap is outside the Android screen.");
            client.Tap(Math.Min(resolution.Width-1,(int)Math.Round(point.X*resolution.Width)),Math.Min(resolution.Height-1,(int)Math.Round(point.Y*resolution.Height)));
        }
        public override void PressKey(Keys key) {if(!google)throw new NotSupportedException("BlueStacks uses card taps.");Guard();client.Key(key);}
        public bool TrySelectSupport(Keys key) {if(!google)return false;PressKey(key);return true;}
    }
    public static class PlayerWindows {
        public static bool IsMain(string title,int width,int height,bool owned) {
            return !owned && width>=600 && height>=400 &&
                !String.IsNullOrWhiteSpace(title) &&
                title.IndexOf("BlueStacks",StringComparison.OrdinalIgnoreCase)>=0 &&
                title.IndexOf("App Player",StringComparison.OrdinalIgnoreCase)>=0;
        }
    }
    public sealed class ActionOrderForm : Form {
        sealed class Entry {public string Id;public override string ToString(){return Settings.ActionLabel(Id);}}
        readonly ListBox list=new ListBox();readonly Button up=new Button(),down=new Button(),save=new Button();readonly Label error=new Label();
        public string[] SelectedOrder {get{return list.Items.Cast<Entry>().Select(item=>item.Id).ToArray();}}
        public ActionOrderForm(string[] order) {
            Text="Action order";ClientSize=new Size(460,325);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);
            Controls.Add(new Label{Text="Select an action, then move it up or down.",AutoSize=true,Location=new Point(18,18)});
            list.Name="actionOrderList";list.Location=new Point(18,53);list.Size=new Size(310,145);list.ItemHeight=24;Controls.Add(list);
            foreach(string id in order)list.Items.Add(new Entry{Id=id});list.SelectedIndex=0;
            up.Name="moveActionUp";up.Text="Move up";up.Location=new Point(340,65);up.Size=new Size(100,34);up.Click+=delegate{MoveSelected(-1);};Controls.Add(up);
            down.Name="moveActionDown";down.Text="Move down";down.Location=new Point(340,110);down.Size=new Size(100,34);down.Click+=delegate{MoveSelected(1);};Controls.Add(down);
            error.Name="orderValidation";error.ForeColor=Color.DarkRed;error.Location=new Point(18,207);error.Size=new Size(420,42);Controls.Add(error);
            save.Name="saveActionOrder";save.Text="Save";save.Location=new Point(230,270);save.Size=new Size(100,34);save.Click+=delegate{Settings.ValidateOrder(SelectedOrder);DialogResult=DialogResult.OK;};Controls.Add(save);AcceptButton=save;
            var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,Location=new Point(340,270),Size=new Size(100,34)};Controls.Add(cancel);CancelButton=cancel;
            var reset=new Button{Text="Reset default",Location=new Point(18,270),Size=new Size(120,34)};reset.Click+=delegate{list.Items.Clear();foreach(string id in new Settings().ActionOrder)list.Items.Add(new Entry{Id=id});list.SelectedIndex=0;RefreshOrder();};Controls.Add(reset);
            list.SelectedIndexChanged+=delegate{RefreshOrder();};RefreshOrder();
        }
        void MoveSelected(int direction) {
            int from=list.SelectedIndex,to=from+direction;if(from<0 || to<0 || to>=list.Items.Count)return;
            object item=list.Items[from];list.Items.RemoveAt(from);list.Items.Insert(to,item);list.SelectedIndex=to;RefreshOrder();
        }
        void RefreshOrder() {
            up.Enabled=list.SelectedIndex>0;down.Enabled=list.SelectedIndex>=0 && list.SelectedIndex<list.Items.Count-1;
            try{Settings.ValidateOrder(SelectedOrder);save.Enabled=true;error.Text="Hero abilities must follow Deploy heroes. Disabled actions are skipped.";error.ForeColor=SystemColors.GrayText;}
            catch(Exception ex){save.Enabled=false;error.Text=ex.Message;error.ForeColor=Color.DarkRed;}
        }
    }
    public sealed class MainForm : Form {
        readonly string folder; readonly OcrReader ocr; Settings settings;
        readonly PlayerPicker choosePlayer; readonly PlayerActivator activatePlayer;
        readonly TextBox log=new TextBox(); readonly Label status=new Label();
        readonly Label connectionHint=new Label();
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly List<Control> actions=new List<Control>();
        readonly Button stopButton=new Button(),startButton=new Button(),detailsButton=new Button();
        readonly NumericUpDown battleCount=new NumericUpDown(); readonly Panel details=new Panel();
        readonly NumericUpDown troopCount=new NumericUpDown(),durationSeconds=new NumericUpDown(),goldMinimum=new NumericUpDown(),elixirMinimum=new NumericUpDown(),darkMinimum=new NumericUpDown();
        readonly ComboBox platformSelector=new ComboBox(),deploymentSelector=new ComboBox();
        readonly NumericUpDown spellCount=new NumericUpDown();
        readonly NumericUpDown openingDepth=new NumericUpDown(),laterDepth=new NumericUpDown(),fourSidesDepth=new NumericUpDown();
        readonly Label openingDepthLabel=new Label(),laterDepthLabel=new Label();
        readonly CheckBox supportUnits=new CheckBox();
        ICapturedGame game; Runner runner; DateTime countdown; int requested; bool positionsOnly,useBackground;
        IntPtr target; bool inTick; string titleState="Ready";
        public bool IsBusy { get { return countdown!=DateTime.MinValue || (runner!=null && runner.Active); } }
        public void ResumeExistingBattle(int total,int completed,bool searching=false) {
            Safely(delegate {
                if(IsBusy || total<1 || total>30) throw new Exception("Cannot resume this run.");
                requested=total;battleCount.Value=total;positionsOnly=false;useBackground=AdbClient.Enabled(settings);SetConnectionHint();
                target=useBackground ? IntPtr.Zero : PickPlayer();
                if(!useBackground) activatePlayer(target);
                game=useBackground ? (ICapturedGame)new AndroidGame(target,settings,ocr,Path.Combine(folder,"assets","pekka-face.png"),Write) : new DesktopGame(target,settings,ocr,Path.Combine(folder,"assets","pekka-face.png"),Write);
                runner=new Runner(game,settings,total,false,Write,new Random());runner.ResumeExistingBattle(DateTime.Now,completed,searching);SyncControls();
                Write(searching ? "Restoring the unfinished loot search without counting it as a battle." : "Restoring the existing battle, then continuing the remaining run with the updated settings.");
            });
        }
        public MainForm(string folder,OcrReader ocr) : this(folder,ocr,null,Native.ActivatePlayer) { }
        IntPtr PickPlayer() { return choosePlayer!=null ? choosePlayer() : settings.GooglePlay ? Native.FindGoogleWindow() : Native.FindWindow(); }
        public MainForm(string folder,OcrReader ocr,PlayerPicker choosePlayer,PlayerActivator activatePlayer) {
            // Use international number grouping in this app without changing Windows settings.
            var displayCulture=(CultureInfo)CultureInfo.CurrentCulture.Clone();
            displayCulture.NumberFormat.NumberGroupSizes=new[]{3};
            displayCulture.NumberFormat.NumberGroupSeparator=",";
            displayCulture.NumberFormat.NumberDecimalSeparator=".";
            System.Threading.Thread.CurrentThread.CurrentCulture=displayCulture;
            this.folder=folder;this.ocr=ocr;
            this.choosePlayer=choosePlayer;this.activatePlayer=activatePlayer;
            AppFiles.Initialize(folder);
            string config=AppFiles.SettingsFile(folder);
            var json=new JavaScriptSerializer();
            if (!File.Exists(config)) File.WriteAllText(config,json.Serialize(new Settings()));
            string savedSettings=File.ReadAllText(config);settings=Settings.FromJson(savedSettings);
            if(savedSettings!=json.Serialize(settings)) File.WriteAllText(config,json.Serialize(settings));
            Text="Clash Farmer"; ClientSize=new Size(760,860); MinimumSize=new Size(776,899);
            Font=new Font("Segoe UI",10); StartPosition=FormStartPosition.CenterScreen;
            BackColor=Color.FromArgb(246,247,249);
            var title=new Label{Text="Clash Farmer",Font=new Font("Segoe UI",20,FontStyle.Bold),AutoSize=true,Location=new Point(20,16)};
            Controls.Add(title);
            Controls.Add(new Label{Text="Open Clash of Clans at your main village, with menus and dialogs closed.",AutoSize=true,Location=new Point(22,67)});
            Controls.Add(new Label{Text="Platform",AutoSize=true,Location=new Point(22,98)});
            platformSelector.Name="platformSelector";platformSelector.DropDownStyle=ComboBoxStyle.DropDownList;
            platformSelector.Items.AddRange(new object[]{"BlueStacks","Google Play Developer (ADB)"});platformSelector.Location=new Point(95,94);platformSelector.Size=new Size(250,27);
            platformSelector.SelectedIndex=settings.GooglePlay?1:0;actions.Add(platformSelector);Controls.Add(platformSelector);
            supportUnits.Name="supportUnits";supportUnits.Text="Siege + heroes / abilities";supportUnits.AutoSize=true;supportUnits.Location=new Point(365,96);supportUnits.Checked=settings.DeploySiegeAndFirstHero;Controls.Add(supportUnits);
            var orderButton=new Button{Name="actionOrderButton",Text="Action order...",Location=new Point(575,92),Size=new Size(163,30)};
            orderButton.Click+=delegate{Safely(delegate{SaveInputs();using(var dialog=new ActionOrderForm(settings.ActionOrder)){if(dialog.ShowDialog(this)==DialogResult.OK)ApplyActionOrder(dialog.SelectedOrder);}});};actions.Add(orderButton);Controls.Add(orderButton);
            Controls.Add(new Label{Text="Deployment",AutoSize=true,Location=new Point(22,135)});
            deploymentSelector.Name="deploymentSelector";deploymentSelector.DropDownStyle=ComboBoxStyle.DropDownList;
            deploymentSelector.Items.AddRange(new object[]{"L-shaped","All four sides"});deploymentSelector.Location=new Point(115,131);deploymentSelector.Size=new Size(195,27);
            deploymentSelector.SelectedIndex=settings.FourSides?1:0;actions.Add(deploymentSelector);Controls.Add(deploymentSelector);
            AddInput(this,spellCount,"spellCount","Spells per attack",335,131,500,0,100,settings.Spells);
            platformSelector.SelectedIndexChanged+=delegate {
                if(IsBusy) return;
                Safely(delegate {
                    SaveInputs();SetConnectionHint();
                    Write("Selected "+settings.PlayerName+". "+connectionHint.Text);
                });
            };
            var flow=new Panel{BackColor=Color.White,Location=new Point(22,170),Size=new Size(716,139)};
            AddInput(flow,troopCount,"troopCount","Troops per attack",15,12,165,1,500,settings.Troops);
            AddInput(flow,durationSeconds,"durationSeconds","End battle after (s)",305,12,500,3,180,settings.AttackDurationSeconds);
            AddInput(flow,goldMinimum,"goldMinimum","Gold min",15,55,15,0,99999999,settings.MinGold,true);
            AddInput(flow,elixirMinimum,"elixirMinimum","Elixir min",245,55,245,0,99999999,settings.MinElixir,true);
            AddInput(flow,darkMinimum,"darkMinimum","Dark elixir min",475,55,475,0,99999999,settings.MinDarkElixir,true);
            goldMinimum.Increment=100000;elixirMinimum.Increment=100000;darkMinimum.Increment=10000;
            flow.Controls.Add(new Label{Text="Timer starts at first troop (±5s, 3–180s). Zero loot minimum skips that resource.",AutoSize=true,Location=new Point(15,112)});Controls.Add(flow);
            var depthPanel=new Panel{BackColor=Color.White,Location=new Point(22,315),Size=new Size(716,74)};
            openingDepthLabel.AutoSize=true;openingDepthLabel.Location=new Point(15,10);depthPanel.Controls.Add(openingDepthLabel);
            laterDepthLabel.Text="Later spell depth (%)";laterDepthLabel.AutoSize=true;laterDepthLabel.Location=new Point(365,10);depthPanel.Controls.Add(laterDepthLabel);
            var depthInputs=new[]{openingDepth,laterDepth,fourSidesDepth};
            var depthValues=new[]{settings.LShapeOpeningDepthPercent,settings.LShapeLaterDepthPercent,settings.FourSidesSpellDepthPercent};
            var depthNames=new[]{"openingDepth","laterDepth","fourSidesDepth"};
            for(int i=0;i<depthInputs.Length;i++) {
                NumericUpDown input=depthInputs[i];input.Name=depthNames[i];input.Minimum=0;input.Maximum=100;input.Increment=5;input.Value=depthValues[i];
                input.Location=new Point(i==1?565:230,6);input.Size=new Size(85,29);depthPanel.Controls.Add(input);
            }
            depthPanel.Controls.Add(new Label{Text="0% = troop line; 100% = base center. Higher depths can overlap spells.",AutoSize=true,Location=new Point(15,44)});Controls.Add(depthPanel);
            Controls.Add(new Label{Text="Battles",AutoSize=true,Location=new Point(22,301)});
            battleCount.Name="battleCount";battleCount.Minimum=1;battleCount.Maximum=30;battleCount.Value=settings.Battles;
            battleCount.Location=new Point(83,297);battleCount.Size=new Size(70,30);actions.Add(battleCount);Controls.Add(battleCount);
            startButton.Name="startButton";startButton.Location=new Point(184,287);startButton.Size=new Size(200,45);
            startButton.BackColor=Color.FromArgb(33,105,75);startButton.ForeColor=Color.White;startButton.FlatStyle=FlatStyle.Flat;startButton.FlatAppearance.BorderSize=0;
            startButton.Font=new Font("Segoe UI",11,FontStyle.Bold);startButton.Click+=delegate { Safely(delegate { Queue(false); }); };
            battleCount.ValueChanged+=delegate { StartLabel(); };StartLabel();actions.Add(startButton);Controls.Add(startButton);
            stopButton.Name="stopButton";stopButton.Text="Stop (F9 / Esc)";stopButton.Location=new Point(398,287);stopButton.Size=new Size(180,45);
            stopButton.FlatStyle=FlatStyle.Flat;stopButton.FlatAppearance.BorderSize=0;stopButton.Click+=delegate { if(IsBusy) Finish("Stopped by user.",false); };Controls.Add(stopButton);
            connectionHint.AutoSize=true;connectionHint.Location=new Point(22,349);Controls.Add(connectionHint);SetConnectionHint();
            status.Text="Ready - start from your main village.";status.Name="statusLabel";status.BackColor=Color.White;status.Padding=new Padding(12,10,12,10);
            status.Font=new Font("Segoe UI",11);status.Location=new Point(22,385);status.Size=new Size(716,67);Controls.Add(status);
            detailsButton.Name="detailsButton";detailsButton.Text="Placement & troubleshooting...";detailsButton.Location=new Point(22,464);detailsButton.Size=new Size(235,30);
            detailsButton.FlatStyle=FlatStyle.Flat;detailsButton.Click+=delegate { details.Visible=!details.Visible;LayoutDetails(); };actions.Add(detailsButton);Controls.Add(detailsButton);
            details.Location=new Point(22,501);details.Size=new Size(716,82);details.Visible=false;
            details.Controls.Add(new Label{Text="Preview troop and spell positions on a screenshot. It sends no game clicks.",AutoSize=true,Location=new Point(0,0)});
            AddDetailButton("Show placement preview",0,29,200,delegate { Queue(true); });
            AddDetailButton("View example",201,29,145,delegate { ShowImage(new Bitmap(Path.Combine(folder,"assets","reference-preview.png"))); });
            AddDetailButton("Edit settings",357,29,145,delegate { Process.Start(new ProcessStartInfo("notepad.exe",AppFiles.SettingsFile(folder)){UseShellExecute=true}); });Controls.Add(details);
            log.Name="activityLog";log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;
            log.Location=new Point(22,505);log.Size=new Size(716,293);log.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(log);
            foreach(Control control in new Control[]{battleCount,startButton,stopButton,connectionHint,status,detailsButton,details,log}) control.Top+=40;
            // The Battles caption is independent of its input.
            foreach(Control control in Controls) if(control is Label && control.Text=="Battles") control.Top+=40;
            foreach(Control control in new Control[]{battleCount,startButton,stopButton,connectionHint,status,detailsButton,details,log}) control.Top+=80;
            foreach(Control control in Controls) if(control is Label && control.Text=="Battles") control.Top+=80;
            log.Height-=80;
            Write("Ready. Start from your main village. "+connectionHint.Text);
            Write("Use the first troop card only. Deployment, troop/spell counts, duration, and loot targets are configurable above. Unreadable loot after three attempts attacks the current base.");
            foreach(var input in new[]{troopCount,spellCount,durationSeconds,goldMinimum,elixirMinimum,darkMinimum,battleCount,openingDepth,laterDepth,fourSidesDepth}) input.ValueChanged+=delegate {if(!IsBusy) Safely(SaveInputs);};
            deploymentSelector.SelectedIndexChanged+=delegate {if(!IsBusy) Safely(SaveInputs);};
            supportUnits.CheckedChanged+=delegate {if(!IsBusy) Safely(SaveInputs);};
            SyncControls();
            timer.Interval=40;timer.Tick+=Tick;timer.Start();
            FormClosing+=delegate { if(IsBusy) Finish("App closed. Run stopped.",false);ReleaseGame(); };
        }
        void StartLabel() { int count=(int)battleCount.Value;startButton.Text="Start "+count+ (count==1 ? " battle" : " battles"); }
        void AddInput(Control parent,NumericUpDown input,string name,string label,int labelX,int y,int inputX,int min,int max,long value,bool stacked=false) {
            parent.Controls.Add(new Label{Text=label,AutoSize=true,Location=new Point(labelX,y+4)});
            input.Name=name;input.Minimum=min;input.Maximum=max;input.Value=value;input.ThousandsSeparator=true;
            input.Location=new Point(inputX,stacked?y+25:y);input.Size=new Size(stacked?205:95,29);actions.Add(input);parent.Controls.Add(input);
        }
        void SaveInputs() {
            var nextSettings=Settings.FromJson(File.ReadAllText(AppFiles.SettingsFile(folder)));
            nextSettings.Platform=platformSelector.SelectedIndex==1?"GooglePlayGames":"BlueStacks";
            nextSettings.DeploymentMode=deploymentSelector.SelectedIndex==1?"FourSides":"LShape";nextSettings.Spells=(int)spellCount.Value;
            nextSettings.DeploySiegeAndFirstHero=supportUnits.Checked;
            nextSettings.LShapeOpeningDepthPercent=(int)openingDepth.Value;nextSettings.LShapeLaterDepthPercent=(int)laterDepth.Value;nextSettings.FourSidesSpellDepthPercent=(int)fourSidesDepth.Value;
            nextSettings.Troops=(int)troopCount.Value;nextSettings.Battles=(int)battleCount.Value;nextSettings.AttackDurationSeconds=(int)durationSeconds.Value;
            nextSettings.MinGold=(long)goldMinimum.Value;nextSettings.MinElixir=(long)elixirMinimum.Value;nextSettings.MinDarkElixir=(long)darkMinimum.Value;nextSettings.FilterLoot=true;nextSettings.Validate();
            File.WriteAllText(AppFiles.SettingsFile(folder),new JavaScriptSerializer().Serialize(nextSettings));settings=nextSettings;SyncDepthControls();
        }
        void ApplyActionOrder(string[] order) {
            if(IsBusy)throw new Exception("Action order cannot change during a run.");
            Settings.ValidateOrder(order);var updated=Settings.FromJson(File.ReadAllText(AppFiles.SettingsFile(folder)));updated.ActionOrder=(string[])order.Clone();updated.Validate();
            File.WriteAllText(AppFiles.SettingsFile(folder),new JavaScriptSerializer().Serialize(updated));settings=updated;
            Write("Saved action order: "+String.Join(" > ",settings.ActionOrder.Select(Settings.ActionLabel)));
        }
        void SyncDepthControls() {
            bool four=deploymentSelector.SelectedIndex==1,enabled=!IsBusy && settings.UseSpells && spellCount.Value>0;
            openingDepthLabel.Text=four?"Spell depth (%)":"Opening spell depth (%)";
            openingDepth.Visible=!four;fourSidesDepth.Visible=four;laterDepth.Visible=!four;laterDepthLabel.Visible=!four;
            openingDepth.Enabled=enabled && !four;fourSidesDepth.Enabled=enabled && four;
            laterDepth.Enabled=enabled && !four && spellCount.Value>6;
            openingDepthLabel.Enabled=enabled;laterDepthLabel.Enabled=laterDepth.Enabled;
            supportUnits.Enabled=!IsBusy && settings.GooglePlay;
        }
        void SetConnectionHint() {
            connectionHint.Text=settings.GooglePlay ? "Google Developer ADB: background control. Emulator can stay minimized; keep it running." : AdbClient.Enabled(settings) ? "Local ADB enabled: Start will connect for background control. Keep BlueStacks running." : "Desktop control: keep BlueStacks active. Local ADB enables background control.";
        }
        void LayoutDetails() {
            int y=details.Visible?713:625;int bottom=log.Bottom;
            log.SetBounds(log.Left,y,log.Width,Math.Max(80,bottom-y));
            detailsButton.Text=details.Visible ? "Hide troubleshooting" : "Placement & troubleshooting...";
        }
        void AddDetailButton(string text,int x,int y,int width,Action action) {
            var button=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,36)};
            button.Click+=delegate { Safely(action); };
            actions.Add(button);details.Controls.Add(button);
        }
        void Safely(Action action) {
            try { action(); } catch(Exception ex) { Finish(ex.Message,false); }
        }
        void Write(string text) {
            string entry=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"  "+text+Environment.NewLine;
            log.AppendText(entry);
            try { File.AppendAllText(Path.Combine(folder,"data","run.log"),entry); } catch(IOException) { }
        }
        void SyncControls() {
            bool busy=IsBusy;
            foreach(Control c in actions) c.Enabled=!busy;
            SyncDepthControls();
            stopButton.Enabled=busy;
            stopButton.BackColor=busy ? Color.FromArgb(168,50,50) : Color.FromArgb(228,230,233);
            stopButton.ForeColor=busy ? Color.White : SystemColors.GrayText;
            UpdateWindowTitle();
        }
        static string TaskbarActivity(string state) {
            if(state=="Home") return "On home";
            if(state=="Return home" || state=="Search home" || state=="Search return") return "Returning home";
            if(state=="Navigation recovery" || state=="Recover search") return "Recovering search";
            if(state=="Ending attack" || state=="Exit battle") return "Ending battle";
            if(state=="Hero ability" || state=="Deploying" || state=="Select troops" || state=="Select siege" || state=="Deploy siege" || state=="Select hero" || state=="Deploy hero" || state=="Select spells" || state=="Opening spells" || state=="Spells" || state=="Result" || state=="Recover result") return "Battle running";
            return "Searching";
        }
        static string ProgressTitle(string activity,int completed,int total) {
            return activity+" | "+Math.Max(0,total-completed)+" left ("+completed+"/"+total+") - Clash Farmer";
        }
        void UpdateWindowTitle() {
            string nextTitle;
            if(positionsOnly) nextTitle="Clash Farmer - "+(IsBusy?"Preparing preview":"Ready");
            else if(requested>0) nextTitle=ProgressTitle(countdown!=DateTime.MinValue?"Starting":runner!=null && runner.Active?TaskbarActivity(runner.State):titleState,runner==null?0:runner.Completed,requested);
            else nextTitle="Clash Farmer - "+titleState;
            // Avoid sending a window-title update every timer tick when nothing changed.
            if(Text!=nextTitle) Text=nextTitle;
        }
        void ReleaseGame() { if(game!=null) { game.Dispose();game=null; } }
        void Queue(bool preview) {
            if(IsBusy) return;
            SaveInputs();
            ReleaseGame();runner=null;useBackground=AdbClient.Enabled(settings);target=useBackground ? IntPtr.Zero : PickPlayer();SetConnectionHint();
            foreach(Form image in OwnedForms) image.Close();
            requested=(int)battleCount.Value;positionsOnly=preview;countdown=DateTime.Now.AddSeconds(3);SyncControls();
            status.Text=preview ? "Preparing a troop-position screenshot. No game clicks will be sent." : (useBackground ? "Starting through the local Android connection in 3 seconds..." : "Opening "+settings.PlayerName+". Starting from your main village in 3 seconds...");
            Write(status.Text);Write("Run: "+(settings.FourSides?"all four sides":"L-shaped")+", "+settings.Troops+" troop clicks, "+(settings.UseSpells?settings.Spells:0)+" spell casts, "+settings.AttackDurationSeconds+"s ±5s from first troop; minimum gold "+settings.MinGold+", elixir "+settings.MinElixir+", dark elixir "+settings.MinDarkElixir+".");
            Write(useBackground ? "Control: local Android connection. Desktop focus and mouse are not used." : "Control: desktop mouse. "+settings.PlayerName+" must remain active.");
            Write(settings.FourSides ? "Spell depth: "+settings.FourSidesSpellDepthPercent+"% from troop lines toward center." : "Spell depths: opening "+settings.LShapeOpeningDepthPercent+"%, later "+settings.LShapeLaterDepthPercent+"% from troop lines toward center.");
            if(settings.GooglePlay) Write("Selection uses the game's default keys: 1 for the first troop, A for the first spell. Load the intended troop and Haste in those first slots.");
            Write("Action order: "+String.Join(" > ",settings.ActionOrder.Select(Settings.ActionLabel))+". Disabled actions are skipped.");
            if(!useBackground) activatePlayer(target);
        }
        void Finish(string reason,bool alreadyLogged) {
            countdown=DateTime.MinValue;
            if(runner!=null && runner.Active) runner.Stop(reason);else if(!alreadyLogged) Write(reason);
            titleState=runner!=null && requested>0 && runner.Completed>=requested ? "Finished" : "Stopped";
            status.Text=reason;ReleaseGame();SyncControls();
        }
        void Tick(object sender,EventArgs e) {
            if(inTick) return;
            inTick=true;
            try { TickCore(); } finally { UpdateWindowTitle();inTick=false; }
        }
        void TickCore() {
            if(Native.Escape()) { if(IsBusy) Finish("Stopped with F9 / Esc.",false);return; }
            if(countdown!=DateTime.MinValue) {
                status.Text="Starting in "+Math.Max(0,(int)Math.Ceiling((countdown-DateTime.Now).TotalSeconds))+"s. "+(useBackground ? "Using the local Android connection." : "Keep "+settings.PlayerName+" active.");
                if(DateTime.Now<countdown) return;
                try {
                    game=useBackground ? (ICapturedGame)new AndroidGame(target,settings,ocr,Path.Combine(folder,"assets","pekka-face.png"),Write) : new DesktopGame(target,settings,ocr,Path.Combine(folder,"assets","pekka-face.png"),Write);game.Guard();
                    if(positionsOnly) {
                        using(Bitmap image=game.Capture()) { Vision.DrawPlan(image,settings);string path=Path.Combine(folder,"data","last-preview.png");image.Save(path,ImageFormat.Png);ShowImage(new Bitmap(image)); }
                        Finish("Troop positions shown. No game clicks sent. This step is optional.",false);return;
                    }
                    runner=new Runner(game,settings,requested,false,Write,new Random());runner.Start(DateTime.Now);
                    countdown=DateTime.MinValue;SyncControls();
                } catch(Exception ex) { Finish(ex.Message,false);return; }
            }
            if(runner!=null&&runner.Active) {
                runner.Tick(DateTime.Now);
                if(!runner.Active) Finish(runner.Message??"Run stopped.",true);
                else status.Text=(runner.State=="Scouting" && settings.FilterLoot ? "Checking enabled loot targets; unreadable after 3 attempts attacks this base..." : FriendlyState(runner.State))+"\nCompleted "+runner.Completed+" of "+requested+" battles. F9 stops the run.";
            }
        }
        static string FriendlyState(string state) {
            if(state.StartsWith("Fast ")) return "Opening the next battle using saved button positions...";
            if(state=="Navigation recovery") return "Recovering home for a scanned navigation retry...";
            if(state=="Exit battle" || state=="Ending attack") return "Ending the timed attack and confirming Okay...";
            if(state=="Home") return "Looking for Battle on your main village...";
            if(state=="Find match") return "Looking for Find a Match...";
            if(state=="Army") return "Confirming Attack on the army screen...";
            if(state=="Scouting") return "Waiting for the opponent's base...";
            if(state=="Deploying") return "Deploying the configured troop clicks from the first card...";
            if(state=="Select siege" || state=="Deploy siege" || state=="Select hero" || state=="Deploy hero") return "Deploying siege and heroes on the selected sides...";
            if(state=="Select troops") return "Selecting the first troop card...";
            if(state=="Select spells") return "Selecting Haste spells...";
            if(state=="Opening spells") return "Dropping the opening Haste batch at the selected spell depth...";
            if(state=="Spells") return "Casting the delayed spell batch at the selected depth...";
            if(state=="Hero ability") return "Sending the deployed hero's ability key...";
            if(state=="Result") return "Waiting for the result or the scheduled battle cutoff...";
            if(state=="Recover result") return "Restoring the current battle and waiting for its result...";
            if(state=="Recover search" || state=="Search home" || state=="Search return") return "Restoring the loot search; no attack counted for skipped bases...";
            return "Returning to your main village...";
        }
        void ShowImage(Bitmap bitmap) {
            var form=new Form{Text="Troop and spell positions - selected deployment mode - no game clicks",ClientSize=new Size(1120,630),StartPosition=FormStartPosition.CenterScreen};
            var picture=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,Image=bitmap};form.Controls.Add(picture);
            form.FormClosed+=delegate { bitmap.Dispose(); };form.Show(this);
        }
    }
    public static class App {
        public static void Run(string folder,OcrReader ocr,int resumeBattles=0,int completed=0,bool resumeSearch=false) {
            Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var form=new MainForm(folder,ocr);
            if(resumeBattles>0) form.Shown+=delegate { form.ResumeExistingBattle(resumeBattles,completed,resumeSearch); };
            Application.Run(form);
        }
    }
}
