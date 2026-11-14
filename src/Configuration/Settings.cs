using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

// Persisted preferences, defaults, validation, and backward-compatible migrations.
namespace ClashPekkas {
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
        public bool RandomSwipes=true;
        public bool ShowLogs=false;
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
        public static Vec[] Version4OuterLines(bool reverse) {
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
        public static Vec[] OuterLines(bool reverse) {
            // Follow the unbuildable grass rim, not walls or the first building
            // tile. All four paths retain the isometric slope x +/- .78*y.
            // The lower-left rim stays inside the shoreline/scenery boundary.
            return reverse ? new[]{new Vec(.9054,.43),new Vec(.6168,.06),new Vec(.8916,.53),new Vec(.6576,.83)}
                : new[]{new Vec(.0946,.43),new Vec(.3832,.06),new Vec(.1596,.57),new Vec(.3624,.83)};
        }
        public Vec TroopButton=new Vec(.185,.93);
        public int GeometryVersion=5;
        public static Settings FromJson(string text) {
            var json=new JavaScriptSerializer();var data=json.Deserialize<Dictionary<string,object>>(text);var settings=json.Deserialize<Settings>(text);
            bool migrateOuterRim=!data.ContainsKey("GeometryVersion") || settings.GeometryVersion<5;
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
            if(migrateOuterRim)foreach(bool reverse in new[]{false,true}) {
                Vec[] current=reverse?settings.Reverse:settings.Normal,previous=Version4OuterLines(reverse),replacement=OuterLines(reverse);
                // Migrate each known preset side independently; custom sides remain intact.
                for(int arm=0;arm<2;arm++)if(current!=null && current.Length==4 && Enumerable.Range(arm*2,2).All(i=>Math.Abs(current[i].X-previous[i].X)<.00001 && Math.Abs(current[i].Y-previous[i].Y)<.00001)) {
                    current[arm*2]=replacement[arm*2];current[arm*2+1]=replacement[arm*2+1];
                }
            }
            settings.GeometryVersion=5;
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
}
