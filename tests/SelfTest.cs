using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace ClashPekkas {
    public class FakeGame : IGame,ISupportGame {
        public bool SiegeAvailable,HeroAvailable;public int SiegeDeployments,HeroDeployments;
        public bool FlexibleActions;
        public readonly List<Vec> SupportPoints=new List<Vec>();
        public virtual bool TrySelectSupport(Keys key) {
            Guard();if(!(key==Keys.Z?SiegeAvailable:HeroAvailable))return false;
            if(Screen!="scout" && !(FlexibleActions && (Screen=="battle" || Screen=="fighting")))throw new Exception("Support selected on wrong screen");
            SelectionKeys.Add(key);Screen=key==Keys.Z?"supportSiege":"supportHero";return true;
        }
        public string Screen="home"; public int Deployments,Selections,SpellSelections,SpellCasts,HomeClicks,FindClicks,ArmyClicks,ReturnClicks;
        public bool Occluded, Missing, WrongTroop,MissingLaunch,WrongSpell,SpellsRequired=true;
        public int TroopCardReads,SpellCardChecks;public bool RejectKey,KeySelection;
        public readonly List<Keys> SelectionKeys=new List<Keys>();
        public DateTime Now;public readonly List<DateTime> SpellTimes=new List<DateTime>();public readonly List<Vec> SpellPoints=new List<Vec>();
        public int NextClicks,LootReads,ReturnReadsDuringOpening,ExpiredSearches;
        public bool UnreadableFirst,ScoutWithoutNext;
        public bool LongBattle,NoStar,DropArmyOnce,DropFindOnce,UnknownDialog,StrictNavigation;
        public int TroopsPerBattle=25,SpellsPerBattle=11;
        public int SelectionDelayMs,TroopClickDelayMs;
        public DateTime TroopSelectedAt,SpellSelectedAt;
        public readonly List<DateTime> TroopStarts=new List<DateTime>();
        public int EndClicks,ConfirmClicks,PanelCloses;public DateTime TroopsDone,EndedAt;
        public readonly Dictionary<string,int> Reads=new Dictionary<string,int>();
        public List<LootReading> Opponents=new List<LootReading>{new LootReading(1000000,1000000)};
        public readonly Queue<LootReading> TransientLoot=new Queue<LootReading>();
        public virtual void Guard() { if(Occluded) throw new Exception("focus lost"); }
        public virtual Vec Find(string button) {
            if(!Reads.ContainsKey(button)) Reads[button]=0;Reads[button]++;
            if(Missing) return null;
            if(button=="attack" && Screen=="home") return new Vec(.1,.9);
            if(button=="find" && Screen=="find") return new Vec(.18,.73);
            if(button=="launch" && Screen=="army" && !MissingLaunch) return new Vec(.87,.9);
            if(button=="next" && Screen=="scout" && !ScoutWithoutNext) return new Vec(.9,.7);
            if(button=="end" && Screen=="scout" && ScoutWithoutNext) return new Vec(.1,.78);
            if(button=="retreat" && (Screen=="deploy" || Screen=="fighting" || Screen=="spells" || Screen=="battle")) return new Vec(.1,.78);
            if(button=="surrenderTitle" && Screen=="confirm" && !UnknownDialog) return new Vec(.5,.3);
            if(button=="confirmSurrender" && Screen=="confirm") return new Vec(.6,.63);
            if(button=="return") { if(Screen=="spells" && SpellCasts<6) ReturnReadsDuringOpening++;if(Screen=="result") return new Vec(.5,.8); }
            return null;
        }
        public Vec FirstTroopCard() { TroopCardReads++;return WrongTroop?null:new Vec(.185,.93); }
        public bool SpellVisible() { SpellCardChecks++;return !WrongSpell; }
        public void PressKey(Keys key) {
            Guard();if(RejectKey) throw new Exception("selection input failed");
            if((Screen=="battle" || Screen=="fighting") && HeroAvailable && new[]{Keys.Q,Keys.W,Keys.E,Keys.R}.Contains(key)){if(!FlexibleActions && (Deployments%TroopsPerBattle!=0 || Now<TroopsDone.AddMilliseconds(350)))throw new Exception("Ability before army finished settling");SelectionKeys.Add(key);return;}
            if(key==Keys.D1 && (Screen=="scout" || FlexibleActions && (Screen=="battle" || Screen=="fighting"))) {if(SelectionDelayMs>0)System.Threading.Thread.Sleep(SelectionDelayMs);TroopSelectedAt=Now.AddMilliseconds(SelectionDelayMs);KeySelection=true;Selections++;Screen="deploy";}
            else if(key==Keys.A && (Screen=="battle" || FlexibleActions && (Screen=="scout" || Screen=="fighting"))) {SpellSelectedAt=Now;SpellSelections++;Screen="spells";}
            else throw new Exception("Unexpected selection key or screen");
            SelectionKeys.Add(key);
        }
        public LootReading ReadLoot(bool gold=true,bool elixir=true,bool darkElixir=false) { LootReads++;if(TransientLoot.Count>0) return TransientLoot.Dequeue();return UnreadableFirst && NextClicks==0 ? null : Opponents[Math.Min(NextClicks,Opponents.Count-1)]; }
        public virtual void Click(Vec p) {
            Guard();
            if(Screen=="supportSiege" || Screen=="supportHero") {if(Screen=="supportSiege")SiegeDeployments++;else HeroDeployments++;SupportPoints.Add(p);Screen=FlexibleActions?"battle":"scout";}
            else if(Screen=="home") { if(StrictNavigation && (p.X>.2 || p.Y<.82)) throw new Exception("Wrong home coordinate");HomeClicks++;Screen="find"; }
            else if((Screen=="army" && p.X>.94 && p.Y<.15) || (Screen=="find" && p.X>.65 && p.X<.75 && p.Y>.8 && p.Y<.85)) { PanelCloses++;Screen="home"; }
            else if(Screen=="find") { if(DropFindOnce) { DropFindOnce=false;return; } if(p.X>.78 && p.Y>.82) return; if(StrictNavigation && (p.X>.3 || p.Y<.65 || p.Y>.82)) throw new Exception("Wrong match coordinate");FindClicks++;Screen="army"; }
            else if(Screen=="army") { if(DropArmyOnce) { DropArmyOnce=false;return; } if(StrictNavigation && (p.X<.78 || p.Y<.82)) throw new Exception("Wrong army coordinate");ArmyClicks++;Screen="scout"; }
            else if(Screen=="scout") { if(ScoutWithoutNext && p.X<.2 && p.Y<.85) { ExpiredSearches++;ScoutWithoutNext=false;Screen="home"; } else if(p.X>.8 && p.Y<.85) { NextClicks++; } else { Selections++;Screen="deploy"; } }
            else if(Screen=="deploy" && p.X<.2 && p.Y>.74) {EndClicks++;EndedAt=Now;Screen="confirm";}
            else if(Screen=="deploy") { TroopStarts.Add(Now);if(TroopClickDelayMs>0)System.Threading.Thread.Sleep(TroopClickDelayMs);Deployments++;if(Deployments%TroopsPerBattle==0) { TroopsDone=Now.AddMilliseconds(TroopClickDelayMs);Screen=FlexibleActions?"battle":SpellsRequired && (!WrongSpell || KeySelection)?"battle":LongBattle?"fighting":"result"; } }
            else if((Screen=="battle" || Screen=="spells") && p.X<.2 && p.Y>.74) {EndClicks++;EndedAt=Now;Screen="confirm";}
            else if(Screen=="battle") { if(p.Y<.85) throw new Exception("Expected spell card selection");SpellSelections++;Screen="spells"; }
            else if(Screen=="spells") { if(p.Y>=.835) throw new Exception("Spell cast on HUD");SpellCasts++;SpellTimes.Add(Now);SpellPoints.Add(p);if(SpellCasts%SpellsPerBattle==0) Screen=FlexibleActions?"battle":LongBattle?"fighting":"result"; }
            else if(Screen=="fighting") { EndClicks++;EndedAt=Now;Screen="confirm"; }
            else if(Screen=="confirm") { ConfirmClicks++;Screen="result"; }
            else if(Screen=="result") { ReturnClicks++;Screen="home"; }
            else throw new Exception("Unexpected click");
        }
    }
    public sealed class SwipeFakeGame : FakeGame,ISwipeGame {
        public readonly List<DateTime> BattleSwipeTimes=new List<DateTime>(),HomeSwipeTimes=new List<DateTime>();
        public readonly List<int> HomeSwipeBattles=new List<int>();
        public readonly List<int> BattleSwipeBattles=new List<int>();
        public readonly List<Vec> SwipeStarts=new List<Vec>(),SwipeEnds=new List<Vec>();
        public readonly List<int> SwipeDurations=new List<int>();
        public int FinishAfterSwipes;
        public void Swipe(Vec from,Vec to,int durationMs) {
            Guard();SwipeStarts.Add(from);SwipeEnds.Add(to);SwipeDurations.Add(durationMs);
            if(Screen=="home"){HomeSwipeTimes.Add(Now);HomeSwipeBattles.Add(ReturnClicks);return;}
            if(Screen!="fighting" && Screen!="battle" && Screen!="spells")throw new Exception("Swipe outside home or live battle");
            BattleSwipeTimes.Add(Now);BattleSwipeBattles.Add(ReturnClicks+1);if(FinishAfterSwipes>0 && BattleSwipeTimes.Count>=FinishAfterSwipes)Screen="result";
        }
    }
    public static class Tests {
        static void Assert(bool ok,string name) { if(!ok) throw new Exception("TEST FAILED: "+name); }
        static IEnumerable<string[]> Orders(string[] remaining) {
            if(remaining.Length==0){yield return new string[0];yield break;}
            for(int i=0;i<remaining.Length;i++)foreach(string[] tail in Orders(remaining.Where((value,index)=>index!=i).ToArray()))yield return new[]{remaining[i]}.Concat(tail).ToArray();
        }
        static bool OnDepth(Vec point,DeploymentSide side,int depth) {
            double d=depth/100.0;
            if(depth==100)return Math.Abs(point.X-.5)<1e-10 && Math.Abs(point.Y-.45)<1e-10;
            var p=new Vec((point.X-.5*d)/(1-d),(point.Y-.45*d)/(1-d));
            Vec a=side.A,b=side.B;
            return Math.Abs((p.X-a.X)*(b.Y-a.Y)-(p.Y-a.Y)*(b.X-a.X))<1e-10 && p.X>=Math.Min(a.X,b.X)-1e-10 && p.X<=Math.Max(a.X,b.X)+1e-10 && p.Y>=Math.Min(a.Y,b.Y)-1e-10 && p.Y<=Math.Max(a.Y,b.Y)+1e-10;
        }
        public static string Run() {
            Settings settings=new Settings{FastNavigation=false};var logs=new List<string>();DateTime now=new DateTime(2026,1,1);
            var swipeGame=new SwipeFakeGame{LongBattle=true};var completeTimes=new Dictionary<int,DateTime>();var homeReadyTimes=new Dictionary<int,DateTime>();
            Action<string> swipeLog=line=>{if(line.StartsWith("Configured action order complete"))completeTimes[swipeGame.ReturnClicks+1]=swipeGame.Now;if(line.StartsWith("Waiting: Home swipes"))homeReadyTimes[swipeGame.ReturnClicks]=swipeGame.Now;};
            var swipeRunner=new Runner(swipeGame,new Settings{AttackDurationSeconds=60},2,false,swipeLog,new Random(4));swipeRunner.Start(now);
            for(int t=0;t<12000 && swipeRunner.Active;t++){swipeGame.Now=now.AddMilliseconds(t*50);swipeRunner.Tick(swipeGame.Now);}
            Assert(swipeRunner.Completed==2 && swipeGame.Deployments==50 && swipeGame.SpellCasts==22 && swipeGame.HomeClicks==2,"random swipes preserve battle and unit counts");
            Assert(swipeGame.BattleSwipeTimes.Count>0,"swipes occur while a completed deployment waits for the battle cutoff");
            for(int battle=1;battle<=2;battle++) {
                var battleTimes=swipeGame.BattleSwipeTimes.Where((time,index)=>swipeGame.BattleSwipeBattles[index]==battle).ToArray();
                for(int i=0;i<battleTimes.Length;i++){double gap=(battleTimes[i]-(i==0?completeTimes[battle]:battleTimes[i-1])).TotalSeconds;Assert(gap>=2 && gap<=5.1,"battle swipe interval is 2-5 seconds after the full sequence");}
                var homeTimes=swipeGame.HomeSwipeTimes.Where((time,index)=>swipeGame.HomeSwipeBattles[index]==battle).ToArray();Assert(homeTimes.Length>=1 && homeTimes.Length<=2,"one or two home swipes per completed battle including the final battle");
                for(int i=0;i<homeTimes.Length;i++){double gap=(homeTimes[i]-(i==0?homeReadyTimes[battle]:homeTimes[i-1])).TotalSeconds;Assert(gap>=1 && gap<=2.1,"home swipes are 1-2 seconds apart before navigation");}
            }
            for(int i=0;i<swipeGame.SwipeStarts.Count;i++) {
                foreach(Vec p in new[]{swipeGame.SwipeStarts[i],swipeGame.SwipeEnds[i]})Assert(p.X>=.25 && p.X<=.75 && p.Y>=.25 && p.Y<=.70,"swipe path stays away from HUD buttons");
                double distance=Plan.PixelDistance(swipeGame.SwipeStarts[i],swipeGame.SwipeEnds[i]);Assert(distance>=120 && distance<=200 && swipeGame.SwipeDurations[i]>=180 && swipeGame.SwipeDurations[i]<=320,"swipes use bounded lengths and durations");
            }
            foreach(string stopping in new[]{"battle","home","disabled","natural"}) {
                var testGame=new SwipeFakeGame{LongBattle=true,FinishAfterSwipes=stopping=="natural"?2:0};
                var testRunner=new Runner(testGame,new Settings{RandomSwipes=stopping!="disabled",AttackDurationSeconds=60},1,false,line=>{},new Random(2));testRunner.Start(now);
                for(int t=0;t<10000 && testRunner.Active;t++){testGame.Now=now.AddMilliseconds(t*50);testRunner.Tick(testGame.Now);if(stopping=="battle" && testRunner.State=="Result" || stopping=="home" && testRunner.State=="Home swipes")break;}
                if(stopping=="battle" || stopping=="home"){int count=testGame.SwipeStarts.Count;testRunner.Stop("Stop during swipe wait");testRunner.Tick(now.AddHours(1));Assert(testGame.SwipeStarts.Count==count,"Stop cancels all pending battle/home swipes");}
                if(stopping=="disabled")Assert(testRunner.Completed==1 && testGame.SwipeStarts.Count==0,"random swipes can be disabled");
                if(stopping=="natural")Assert(testRunner.Completed==1 && testGame.BattleSwipeTimes.Count==2 && testGame.EndClicks==0,"natural results stop battle swipes before returning home");
            }
            var game=new FakeGame();var runner=new Runner(game,settings,10,false,logs.Add,new Random(3));runner.Start(now);
            for(int t=0;t<5000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(!runner.Active && runner.Completed==10,"ten battles finish");
            foreach(string mode in new[]{"LShape","FourSides"}) foreach(int troopTotal in new[]{1,2,3,9,20,25,30,500}) {
                var variableGame=new FakeGame{TroopsPerBattle=troopTotal};var variableSettings=new Settings{Troops=troopTotal,DeploymentMode=mode,EndBattleEarly=false};
                var variableRunner=new Runner(variableGame,variableSettings,1,false,logs.Add,new Random(troopTotal));variableRunner.Start(now);
                for(int t=0;t<10000 && variableRunner.Active;t++){variableGame.Now=now.AddMilliseconds(t*100);variableRunner.Tick(variableGame.Now);}
                Assert(variableRunner.Completed==1 && variableGame.Deployments==troopTotal && variableGame.Selections==1,"configured troop count uses only one card and exact click attempts");
                foreach(bool mirror in new[]{false,true}) for(int seed=0;seed<25;seed++) {
                    var layout=Plan.Troops(variableSettings,mirror,new Random(seed));
                    Assert(layout.Points.Count==troopTotal && layout.Counts.Max()-layout.Counts.Min()<=1,"balanced side counts with exact total");
                    Assert(layout.Sides.Length==(mode=="FourSides"?4:2),"selected side count");
                    for(int arm=0;arm<layout.Sides.Length;arm++) {
                        var side=layout.Points.Where((p,i)=>layout.PointSides[i]==arm).ToList();Vec a=layout.Sides[arm].A,b=layout.Sides[arm].B;
                        foreach(Vec point in side) {
                            double cross=(point.X-a.X)*(b.Y-a.Y)-(point.Y-a.Y)*(b.X-a.X);
                            Assert(Math.Abs(cross)<1e-10 && point.Y>=Math.Min(a.Y,b.Y) && point.Y<=Math.Max(a.Y,b.Y),"all troop points stay on saved grass segments");
                        }
                        double length=Plan.PixelDistance(a,b);
                        for(int i=1;i<side.Count;i++) Assert(Plan.PixelDistance(side[i-1],side[i])>=length*.59/side.Count,"jitter preserves a minimum gap between troop placements");
                    }
                }
            }
            foreach(int troopTotal in new[]{9,30}) {
                var latencyGame=new FakeGame{TroopsPerBattle=troopTotal,SelectionDelayMs=400,TroopClickDelayMs=35};
                var latencyRunner=new Runner(latencyGame,new Settings{Platform="GooglePlayGames",Troops=troopTotal},1,false,logs.Add,new Random(8));latencyRunner.Start(now);
                for(int t=0;t<20000 && latencyRunner.Active;t++){latencyGame.Now=now.AddMilliseconds(t*40);latencyRunner.Tick(latencyGame.Now);}
                Assert(latencyRunner.Completed==1 && latencyGame.Deployments==troopTotal,"latency does not change configured troop count");
                Assert((latencyGame.TroopStarts[0]-latencyGame.TroopSelectedAt).TotalMilliseconds>=500,"selection settles after input returns");
                Assert((latencyGame.TroopStarts[1]-latencyGame.TroopStarts[0]).TotalMilliseconds>=535,"first placement settles through scouting transition");
                Assert((latencyGame.SpellSelectedAt-latencyGame.TroopsDone).TotalMilliseconds>=350,"final mouse release settles before spell selection");
            }
            Assert(Settings.FromJson("{}").DeploymentMode=="LShape" && Settings.FromJson("{}").Spells==11,"legacy settings default to L and eleven spells");
            int validOrders=0;
            foreach(string[] order in Orders(new Settings().ActionOrder)) {
                bool valid=Array.IndexOf(order,"Heroes")<Array.IndexOf(order,"HeroAbilities"),accepted=true;
                try{Settings.ValidateOrder(order);}catch{accepted=false;}Assert(valid==accepted,"only hero-before-ability permutations are valid");if(!valid)continue;validOrders++;
                foreach(string mode in new[]{"LShape","FourSides"}) {
                    var orderedGame=new FakeGame{FlexibleActions=true,HeroAvailable=true,SiegeAvailable=true,TroopsPerBattle=9,SpellsPerBattle=2};
                    var orderedSettings=new Settings{Platform="GooglePlayGames",ActionOrder=order,DeploymentMode=mode,Troops=9,Spells=2,AttackDurationSeconds=60};
                    var orderedRunner=new Runner(orderedGame,orderedSettings,2,false,logs.Add,new Random(8));orderedRunner.Start(now);
                    for(int t=0;t<10000 && orderedRunner.Active;t++){orderedGame.Now=now.AddMilliseconds(t*100);orderedRunner.Tick(orderedGame.Now);}
                    var expected=new List<Keys>();for(int battle=0;battle<2;battle++)foreach(string action in order){if(action=="Heroes" || action=="HeroAbilities")expected.AddRange(new[]{Keys.Q,Keys.W,Keys.E,Keys.R});else expected.Add(action=="Siege"?Keys.Z:action=="Army"?Keys.D1:Keys.A);}
                    Assert(orderedRunner.Completed==2 && orderedGame.Deployments==18 && orderedGame.SpellCasts==4 && orderedGame.HeroDeployments==8 && orderedGame.SiegeDeployments==2,"configured order preserves exact counts across battles");
                    Assert(orderedGame.SelectionKeys.SequenceEqual(expected),"all valid permutations follow the requested key order in both modes");
                    Assert((orderedGame.EndedAt-orderedGame.TroopStarts[9]).TotalSeconds>=55 && (orderedGame.EndedAt-orderedGame.TroopStarts[9]).TotalSeconds<=66,"custom order still times each battle from its first army troop");
                }
            }
            Assert(validOrders==60,"all sixty valid action orders tested");
            var spellsFirstGame=new FakeGame{FlexibleActions=true,HeroAvailable=true,SiegeAvailable=true};
            var spellsFirstSettings=new Settings{Platform="GooglePlayGames",ActionOrder=new[]{"Spells","Army","Heroes","HeroAbilities","Siege"}};
            var spellsFirstRunner=new Runner(spellsFirstGame,spellsFirstSettings,1,false,logs.Add,new Random(1));spellsFirstRunner.Start(now);
            for(int t=0;t<10000 && spellsFirstRunner.Active;t++){spellsFirstGame.Now=now.AddMilliseconds(t*100);spellsFirstRunner.Tick(spellsFirstGame.Now);}
            Assert(spellsFirstRunner.Completed==1 && spellsFirstGame.SpellCasts==11 && spellsFirstGame.Deployments==25 && spellsFirstGame.SelectionKeys[0]==Keys.A,"entire opening and delayed spell batch completes before the next configured action");
            var reorderedCutoffGame=new FakeGame{FlexibleActions=true,HeroAvailable=true,SiegeAvailable=true,TroopsPerBattle=1};
            var reorderedCutoffRunner=new Runner(reorderedCutoffGame,new Settings{Platform="GooglePlayGames",Troops=1,ActionOrder=new[]{"Army","Heroes","Siege","HeroAbilities","Spells"}},1,false,logs.Add,new Random(1));reorderedCutoffRunner.Start(now);
            for(int t=0;t<10000 && reorderedCutoffRunner.Active && reorderedCutoffRunner.State!="Deploy hero";t++){reorderedCutoffGame.Now=now.AddMilliseconds(t*100);reorderedCutoffRunner.Tick(reorderedCutoffGame.Now);}
            reorderedCutoffGame.Now=reorderedCutoffGame.TroopStarts[0].AddMinutes(2);reorderedCutoffRunner.Tick(reorderedCutoffGame.Now);
            Assert(reorderedCutoffRunner.State=="Ending attack" && reorderedCutoffGame.SupportPoints.Count==0 && reorderedCutoffGame.SpellCasts==0,"first-troop deadline cancels support stages that follow army in a custom order");
            foreach(string[] invalidOrder in new[]{new string[0],new[]{"Army","Army","Spells","Heroes","HeroAbilities"},new[]{"Heroes","Siege","Army","Unknown","Spells"}}){bool failed=false;try{Settings.ValidateOrder(invalidOrder);}catch{failed=true;}Assert(failed,"missing, duplicated and unknown actions rejected");}
            Assert(Settings.FromJson("{}").ActionOrder.SequenceEqual(new[]{"Heroes","Siege","Army","HeroAbilities","Spells"}),"old settings retain default order");
            foreach(string mode in new[]{"LShape","FourSides"}) foreach(bool siege in new[]{false,true}) foreach(bool hero in new[]{false,true}) {
                var supportGame=new FakeGame{SiegeAvailable=siege,HeroAvailable=hero};
                var supportSettings=new Settings{Platform="GooglePlayGames",DeploymentMode=mode};
                var supportRunner=new Runner(supportGame,supportSettings,2,false,logs.Add,new Random(5));supportRunner.Start(now);
                for(int t=0;t<20000 && supportRunner.Active;t++){supportGame.Now=now.AddMilliseconds(t*50);supportRunner.Tick(supportGame.Now);}
                Assert(supportRunner.Completed==2 && supportGame.Deployments==50 && supportGame.SpellCasts==22,"support preserves troop, spell and battle counts");
                Assert(supportGame.SiegeDeployments==(siege?2:0) && supportGame.HeroDeployments==(hero?8:0),"siege and each of four heroes deploy once per battle");
                var sequence=new List<Keys>();for(int b=0;b<2;b++){if(hero)sequence.AddRange(new[]{Keys.Q,Keys.W,Keys.E,Keys.R});if(siege)sequence.Add(Keys.Z);sequence.Add(Keys.D1);if(hero)sequence.AddRange(new[]{Keys.Q,Keys.W,Keys.E,Keys.R});sequence.Add(Keys.A);}
                Assert(supportGame.SelectionKeys.SequenceEqual(sequence),"heroes then siege then settled army then abilities then spells");
                if(hero) {
                    var sides=Plan.Sides(supportSettings,false);
                    int offset=0;
                    for(int b=0;b<2;b++)for(int i=0;i<4;i++) {
                        Vec point=supportGame.SupportPoints[b*(siege?5:4)+offset+i];
                        if(mode=="FourSides") {
                            string[] clockwise={"upper-left","upper-right","lower-right","lower-left"};
                            Vec first=supportGame.SupportPoints[b*(siege?5:4)+offset];
                            string firstName=sides.Single(side=>Plan.PixelDistance(first,SupportPlan.Midpoint(side))<.01).Name;
                            DeploymentSide expected=sides.Single(side=>side.Name==clockwise[(Array.IndexOf(clockwise,firstName)+i)%4]);
                            Assert(Plan.PixelDistance(point,SupportPlan.Midpoint(expected))<.01,"heroes rotate clockwise from their random starting side");
                        }
                        if(mode=="LShape" && i%2==1) {Vec previous=supportGame.SupportPoints[b*(siege?5:4)+offset+i-1];Assert(point.X==previous.X && point.Y==previous.Y,"two heroes share each L side");}
                    }
                }
                foreach(Vec point in supportGame.SupportPoints) Assert(Plan.Sides(supportSettings,false).Concat(Plan.Sides(supportSettings,true)).Any(side=>Math.Abs(point.X-(side.A.X+side.B.X)/2)<1e-10 && Math.Abs(point.Y-(side.A.Y+side.B.Y)/2)<1e-10),"support stays at a troop-side midpoint");
            }
            foreach(string platform in new[]{"GooglePlayGames","BlueStacks"}) {
                var disabledGame=new FakeGame{SiegeAvailable=true,HeroAvailable=true};
                var disabledRunner=new Runner(disabledGame,new Settings{Platform=platform,DeploySiegeAndFirstHero=false},1,false,logs.Add,new Random(3));disabledRunner.Start(now);
                for(int t=0;t<10000 && disabledRunner.Active;t++){disabledGame.Now=now.AddMilliseconds(t*50);disabledRunner.Tick(disabledGame.Now);}
                Assert(disabledRunner.Completed==1 && disabledGame.SupportPoints.Count==0,"support toggle off preserves original flow");
            }
            foreach(bool afterSelection in new[]{false,true}) {
                var stoppedGame=new FakeGame{SiegeAvailable=true,HeroAvailable=true};var stoppedRunner=new Runner(stoppedGame,new Settings{Platform="GooglePlayGames"},1,false,logs.Add,new Random(3));stoppedRunner.Start(now);
                for(int t=0;t<10000 && stoppedRunner.Active;t++){stoppedGame.Now=now.AddMilliseconds(t*50);stoppedRunner.Tick(stoppedGame.Now);if(stoppedRunner.State==(afterSelection?"Deploy hero":"Deploy siege"))break;}
                int clicks=stoppedGame.SupportPoints.Count;stoppedRunner.Stop("Stop before support placement");stoppedRunner.Tick(now.AddHours(1));Assert(stoppedGame.SupportPoints.Count==clicks,"Stop cancels queued support placement");
            }
            foreach(string ending in new[]{"stop","natural","cutoff"}) {
                var abilityGame=new FakeGame{HeroAvailable=true,SiegeAvailable=true};
                var abilityRunner=new Runner(abilityGame,new Settings{Platform="GooglePlayGames",AttackDurationSeconds=60},1,false,logs.Add,new Random(6));abilityRunner.Start(now);
                int tick=0;while(abilityRunner.Active && abilityRunner.State!="Hero ability" && tick<10000){abilityGame.Now=now.AddMilliseconds(tick++*50);abilityRunner.Tick(abilityGame.Now);}
                Assert(abilityRunner.State=="Hero ability" && abilityGame.Deployments==25,"abilities wait until full troop deployment");
                int selected=abilityGame.SelectionKeys.Count;
                if(ending=="stop")abilityRunner.Stop("Stop before delayed abilities");
                if(ending=="natural")abilityGame.Screen="result";
                DateTime endTime=ending=="cutoff"?abilityGame.TroopStarts[0].AddMinutes(2):abilityGame.Now.AddSeconds(1);
                abilityGame.Now=endTime;abilityRunner.Tick(endTime);
                Assert(abilityGame.SelectionKeys.Count==selected,"Stop, natural completion and cutoff prevent pending abilities");
            }
            var shortGame=new FakeGame{HeroAvailable=true,SiegeAvailable=true,TroopsPerBattle=500};
            var shortRunner=new Runner(shortGame,new Settings{Platform="GooglePlayGames",Troops=500,AttackDurationSeconds=3},1,false,logs.Add,new Random(7));shortRunner.Start(now);
            for(int t=0;t<20000 && shortRunner.Active;t++){shortGame.Now=now.AddMilliseconds(t*50);shortRunner.Tick(shortGame.Now);}
            Assert(shortRunner.Completed==1 && shortGame.Deployments>0 && shortGame.Deployments<500 && shortGame.SpellCasts==0,"short first-troop deadline cancels unfinished army and spells");
            Assert(shortGame.SelectionKeys.Count(k=>k==Keys.Q)==1 && (shortGame.EndedAt-shortGame.TroopStarts[0]).TotalSeconds<=8.1,"cutoff does not activate queued hero abilities or count support deployment time");
            var noSpellSupport=new FakeGame{HeroAvailable=true,SiegeAvailable=true,SpellsRequired=false,LongBattle=true};
            var noSpellRunner=new Runner(noSpellSupport,new Settings{Platform="GooglePlayGames",Spells=0},1,false,logs.Add,new Random(4));noSpellRunner.Start(now);
            for(int t=0;t<10000 && noSpellRunner.Active;t++){noSpellSupport.Now=now.AddMilliseconds(t*50);noSpellRunner.Tick(noSpellSupport.Now);}
            Assert(noSpellRunner.Completed==1 && noSpellSupport.SelectionKeys.Count(k=>k==Keys.Q)==2 && !noSpellSupport.SelectionKeys.Contains(Keys.A),"abilities still fire after army when spells are disabled");
            foreach(string invalid in new[]{"{\"DeploymentMode\":\"unknown\"}","{\"Spells\":-1}","{\"Spells\":101}"}) {
                bool invalidRejected=false;try {Settings.FromJson(invalid);}catch {invalidRejected=true;}Assert(invalidRejected,"invalid new settings rejected");
            }
            foreach(bool naturalEnd in new[]{false,true}) {
                var burstGame=new FakeGame{SpellsPerBattle=100};
                var burstRunner=new Runner(burstGame,new Settings{DeploymentMode="FourSides",Spells=100},1,false,logs.Add,new Random(7));burstRunner.Start(now);
                int tick=0;
                while(burstRunner.Active && burstGame.SpellCasts<6 && tick<10000) {burstGame.Now=now.AddMilliseconds(tick++*50);burstRunner.Tick(burstGame.Now);}
                Assert(burstGame.SpellCasts==6,"four-side burst begins");
                if(naturalEnd) burstGame.Screen="result";else burstRunner.Stop("Stop during four-side burst");
                for(int t=tick;t<tick+200 && (naturalEnd?burstRunner.Active:true);t++) {burstGame.Now=now.AddMilliseconds(t*50);burstRunner.Tick(burstGame.Now);}
                Assert(burstGame.SpellCasts==6 && (naturalEnd?burstRunner.Completed==1:burstGame.ReturnClicks==0),"natural completion and Stop cancel remaining four-side casts");
            }
            foreach(string mode in new[]{"LShape","FourSides"}) foreach(int total in new[]{0,1,2,3,4,5,6,7,11,12,15,100}) {
                var configurable=new Settings{DeploymentMode=mode,Spells=total,Platform="GooglePlayGames",EndBattleEarly=false};
                var layout=Plan.Opening(configurable,false,new Random(4));
                int opening=mode=="FourSides" || total<=6?total:(total+1)/2;
                Assert(layout.Points.Count==opening && layout.Counts.Max()-layout.Counts.Min()<=1,"balanced variable opening spell groups");
                Assert(layout.Points.All(p=>p.Y>=.05 && p.Y<=.835),"variable spells avoid troop bar");
                var spellGame=new FakeGame{SpellsPerBattle=Math.Max(1,total),SpellsRequired=total>0};
                var spellRunner=new Runner(spellGame,configurable,1,false,logs.Add,new Random(4));spellRunner.Start(now);
                for(int t=0;t<20000 && spellRunner.Active;t++) {spellGame.Now=now.AddMilliseconds(t*50);spellRunner.Tick(spellGame.Now);}
                Assert(spellRunner.Completed==1 && spellGame.SpellCasts==total,"exact configured spell casts and completed battle");
                Assert(spellGame.SelectionKeys.Count(k=>k==Keys.A)==(total==0?0:1),"zero spells skip selection");
                for(int i=1;i<total;i++) {
                    double ms=(spellGame.SpellTimes[i]-spellGame.SpellTimes[i-1]).TotalMilliseconds;
                    Assert(i<opening?ms<=150:i==opening?ms>=5000 && ms<=10050:ms>=2000 && ms<=5050,"mode-specific burst and delayed timing");
                }
                int sent=spellGame.SpellCasts;spellRunner.Tick(now.AddHours(1));Assert(spellGame.SpellCasts==sent,"finished variable run sends no extra spells");
            }
            foreach(int duration in new[]{3,60,180}) {
                var timedGame=new FakeGame{LongBattle=true};var timedSettings=new Settings{AttackDurationSeconds=duration};
                var timedRunner=new Runner(timedGame,timedSettings,1,false,logs.Add,new Random(2));timedRunner.Start(now);
                for(int t=0;t<5000 && timedRunner.Active;t++){timedGame.Now=now.AddMilliseconds(t*100);timedRunner.Tick(timedGame.Now);}
                double elapsed=(timedGame.EndedAt-timedGame.TroopStarts[0]).TotalSeconds;
                Assert(timedRunner.Completed==1 && timedGame.EndClicks==1 && timedGame.ConfirmClicks==1,"configured duration ends the battle through its confirmation");
                Assert(elapsed>=Math.Max(3,duration-5) && elapsed<=Math.Min(180,duration+5)+1,"duration jitter is bounded and does not wait for the final spell");
                if(duration==3)Assert(timedGame.SpellCasts<11,"short timer cancels pending late spells");
            }
            var noLootGame=new FakeGame{UnreadableFirst=true};var noLootSettings=new Settings{MinGold=0,MinElixir=0,MinDarkElixir=0};
            var noLootRunner=new Runner(noLootGame,noLootSettings,1,false,logs.Add,new Random(2));noLootRunner.Start(now);
            for(int t=0;t<5000 && noLootRunner.Active;t++)noLootRunner.Tick(now.AddMilliseconds(t*100));
            Assert(noLootRunner.Completed==1 && noLootGame.LootReads==0 && noLootGame.NextClicks==0,"zero thresholds bypass loot OCR and accept the first base");
            var darkGame=new FakeGame{Opponents=new List<LootReading>{new LootReading(900000,900000,999),new LootReading(900000,900000,1000)}};
            var darkRunner=new Runner(darkGame,new Settings{MinDarkElixir=1000},1,false,logs.Add,new Random(2));darkRunner.Start(now);
            for(int t=0;t<5000 && darkRunner.Active;t++)darkRunner.Tick(now.AddMilliseconds(t*100));
            Assert(darkRunner.Completed==1 && darkGame.NextClicks==1,"dark elixir threshold is enforced alongside gold and elixir");
            var migrated=Settings.FromJson("{\"Troops\":10,\"MinBattleSeconds\":60,\"MaxBattleSeconds\":80}");
            Assert(migrated.Troops==10 && migrated.AttackDurationSeconds==70,"legacy duration settings migrate to their midpoint");
            Assert(game.Deployments==250 && game.Selections==10,"exactly 25 deployment clicks per battle");
            Assert(game.SpellSelections==10 && game.SpellCasts==110,"exactly 11 spell casts per battle");
            Assert(game.HomeClicks==10 && game.FindClicks==10 && game.ArmyClicks==10 && game.ReturnClicks==10,"home, matchmaking, army confirmation, and return once per battle");
            Assert(game.SelectionKeys.Count==0 && game.TroopCardReads==10 && game.SpellCardChecks==10,"BlueStacks keeps card recognition and mouse selection");
            for(int t=0;t<100;t++) runner.Tick(now.AddHours(1));
            Assert(game.Deployments==250 && game.SpellCasts==110 && game.HomeClicks==10,"hard stop prevents eleventh battle or further spell casts");
            game=new FakeGame();runner=new Runner(game,new Settings{Battles=30,Platform="GooglePlayGames"},30,false,logs.Add,new Random(3));runner.Start(now);
            for(int t=0;t<20000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(!runner.Active && runner.Completed==30 && game.Deployments==750 && game.SpellCasts==330 && game.HomeClicks==30,"thirty Google battles finish with exactly 750 troops and 330 spells");
            Assert(game.TroopCardReads==0 && game.SpellCardChecks==0 && game.SelectionKeys.SequenceEqual(Enumerable.Range(0,60).Select(i=>i%2==0?Keys.D1:Keys.A)),"Google uses exactly 1 then A per battle without troop OCR or spell matching");
            runner.Tick(now.AddHours(10));Assert(game.HomeClicks==30 && game.Deployments==750,"thirty-battle run cannot start battle thirty-one");
            bool limitRejected=false;try {new Runner(new FakeGame(),new Settings(),31,false,logs.Add,new Random());}catch{limitRejected=true;}
            Assert(limitRejected,"more than thirty battles rejected");
            game=new FakeGame{StrictNavigation=true};runner=new Runner(game,new Settings(),10,false,logs.Add,new Random(3));runner.Start(now);
            for(int t=0;t<10000 && runner.Active;t++) { game.Now=now.AddMilliseconds(t*100);runner.Tick(game.Now); }
            Assert(runner.Completed==10 && game.Deployments==250 && game.SpellCasts==110,"default fast navigation preserves ten-battle cap and deployment counts");
            Assert(!game.Reads.ContainsKey("find") && !game.Reads.ContainsKey("launch"),"successful fast route does not OCR intermediate buttons");
            game=new FakeGame{StrictNavigation=true,DropArmyOnce=true};runner=new Runner(game,new Settings(),2,false,logs.Add,new Random(3));runner.Start(now);
            for(int t=0;t<10000 && runner.Active;t++) { game.Now=now.AddMilliseconds(t*100);runner.Tick(game.Now); }
            Assert(runner.Completed==2 && game.Deployments==50 && game.PanelCloses==1 && game.ArmyClicks==2,"failed fast click returns home and scanned retry does not count an extra battle");
            Assert(logs.Any(s=>s.Contains("scanned retry")) && logs.Any(s=>s.Contains("Fast navigation verified")),"fallback recovers then later battle uses fast navigation again");
            game=new FakeGame{StrictNavigation=true,DropFindOnce=true};runner=new Runner(game,new Settings(),1,false,logs.Add,new Random(3));runner.Start(now);
            for(int t=0;t<10000 && runner.Active;t++) { game.Now=now.AddMilliseconds(t*100);runner.Tick(game.Now); }
            Assert(runner.Completed==1 && game.PanelCloses==1 && game.Deployments==25,"missed Find a Match dismisses the recognized left sheet then retries from home");
            foreach(bool noStar in new[]{false,true}) {
                game=new FakeGame{LongBattle=true,NoStar=noStar};runner=new Runner(game,new Settings(),1,false,logs.Add,new Random(3));runner.Start(now);
                for(int t=0;t<3000 && runner.Active;t++) { game.Now=now.AddMilliseconds(t*100);runner.Tick(game.Now); }
                Assert(runner.Completed==1 && game.EndClicks==1 && game.ConfirmClicks==1,"timed exit handles both End Battle and Surrender confirmations");
                double elapsed=(game.EndedAt-game.TroopStarts[0]).TotalSeconds;
                Assert(elapsed>=85 && elapsed<=98,"cutoff is measured from the first troop deployment with bounded polling overhead");
            }
            game=new FakeGame{LongBattle=true,NoStar=true,UnknownDialog=true};runner=new Runner(game,new Settings(),1,false,logs.Add,new Random(3));runner.Start(now);
            for(int t=0;t<3000 && runner.Active;t++) { game.Now=now.AddMilliseconds(t*100);runner.Tick(game.Now); }
            Assert(!runner.Active && runner.Completed==0 && game.ConfirmClicks==0,"unknown confirmation is never accepted");
            game=new FakeGame{LongBattle=true,SpellsRequired=false};runner=new Runner(game,new Settings{UseSpells=false},1,false,logs.Add,new Random(3));runner.Start(now);
            for(int t=0;t<3000 && runner.Active;t++) { game.Now=now.AddMilliseconds(t*100);runner.Tick(game.Now); }
            Assert(runner.Completed==1 && game.EndClicks==1,"cutoff also applies without spells");
            game=new FakeGame{LongBattle=true};runner=new Runner(game,new Settings(),1,false,logs.Add,new Random(3));runner.Start(now);
            for(int t=0;t<2000 && game.SpellCasts<11;t++) { game.Now=now.AddMilliseconds(t*100);runner.Tick(game.Now); }
            runner.Stop("Stop before cutoff");runner.Tick(now.AddMinutes(5));
            Assert(game.EndClicks==0 && game.ReturnClicks==0,"stop cancels the future battle cutoff");
            game=new FakeGame{Screen="result"};runner=new Runner(game,settings,5,false,logs.Add,new Random(2));runner.ResumeExistingBattle(now,0);
            for(int t=0;t<5000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(runner.Completed==5 && game.Deployments==100 && game.ReturnClicks==5 && game.HomeClicks==4,"recovery completes existing battle then exactly four remaining battles");
            game=new FakeGame{Screen="scout",ScoutWithoutNext=true};runner=new Runner(game,settings,5,false,logs.Add,new Random(2));runner.ResumeExistingBattle(now,1,true);
            for(int t=0;t<5000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(runner.Completed==5 && game.Deployments==100 && game.ReturnClicks==4 && game.ExpiredSearches==1,"unfinished search recovery keeps previous count and completes four actual attacks");
            game=new FakeGame{ScoutWithoutNext=true};runner=new Runner(game,settings,1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<5000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(runner.Completed==1 && game.ExpiredSearches==1 && game.Deployments==25,"expired scout is left without counting it as a battle");
            game=new FakeGame();runner=new Runner(game,settings,1,false,logs.Add,new Random(32));runner.Start(now);
            for(int t=0;t<5000 && runner.Active;t++) { game.Now=now.AddMilliseconds(t*50);runner.Tick(game.Now); }
            Assert(game.SpellTimes.Count==11 && runner.Completed==1,"complete timed spell schedule");
            Assert((game.SpellTimes[5]-game.SpellTimes[0]).TotalMilliseconds<=600 && game.ReturnReadsDuringOpening==0,"opening six are a fast burst without OCR between casts");
            for(int i=6;i<11;i++) {
                double interval=(game.SpellTimes[i]-game.SpellTimes[i-1]).TotalMilliseconds;
                Assert(i==6 ? interval>=5000 && interval<=10050 : interval>=2000 && interval<=5050,"first delayed spell waits 5-10 seconds, remaining four wait 2-5 seconds");
            }
            game=new FakeGame();runner=new Runner(game,settings,1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<5000 && game.SpellCasts<6;t++) runner.Tick(now.AddMilliseconds(t*50));
            Assert(game.SpellCasts==6,"six opening casts happen before delayed casts");
            runner.Stop("Stopped during spell wait.");
            for(int t=0;t<100;t++) runner.Tick(now.AddMinutes(4).AddMilliseconds(t*300));
            Assert(game.SpellCasts==6 && game.ReturnClicks==0,"stop cancels delayed spells without navigation");
            game=new FakeGame();runner=new Runner(game,settings,1,false,logs.Add,new Random(2));runner.Start(now);
            int earlyTick=0;
            while(runner.Active && game.SpellCasts<6 && earlyTick<5000) runner.Tick(now.AddMilliseconds(earlyTick++*50));
            game.Screen="result";
            for(int t=earlyTick;t<earlyTick+400 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*50));
            Assert(runner.Completed==1 && game.SpellCasts==6,"early result skips unused spells and returns home");
            game=new FakeGame{WrongSpell=true};runner=new Runner(game,settings,1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<1000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(runner.Completed==1 && game.SpellSelections==0 && game.SpellCasts==0,"unrecognized spell card is skipped");
            game=new FakeGame{SpellsRequired=false};runner=new Runner(game,new Settings{UseSpells=false},1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<1000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(runner.Completed==1 && game.SpellCasts==0,"spells can be disabled without changing the battle loop");
            game=new FakeGame{Opponents=new List<LootReading>{new LootReading(499999,700000),new LootReading(700000,499999),new LootReading(500000,500000)}};
            runner=new Runner(game,settings,1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<5000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(runner.Completed==1 && runner.SkippedBases==2 && game.NextClicks==2 && game.Selections==1 && game.HomeClicks==1,"both loot thresholds required, exact boundary accepted, skips do not count as battles");
            game=new FakeGame{UnreadableFirst=true};runner=new Runner(game,settings,1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<5000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(runner.Completed==1 && game.NextClicks==0 && game.LootReads==3 && game.Deployments==25,"unreadable loot is retried three times then current base is attacked without Next");
            game=new FakeGame{UnreadableFirst=true};runner=new Runner(game,new Settings{Platform="GooglePlayGames"},1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<1000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*100));
            Assert(!runner.Active && runner.Completed==1 && game.LootReads==3 && game.NextClicks==0 && game.Deployments==25,"Google unreadable loot attacks the current base after three attempts and completes the battle");
            game=new FakeGame{UnreadableFirst=true,WrongTroop=true,WrongSpell=true};runner=new Runner(game,new Settings{Platform="GooglePlayGames"},1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<1000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*100));
            Assert(game.LootReads==3 && runner.Completed==1 && game.Deployments==25 && game.SpellCasts==11 && game.NextClicks==0 && game.TroopCardReads==0 && game.SpellCardChecks==0,"Google key selection works when card recognition would fail");
            game=new FakeGame{RejectKey=true};runner=new Runner(game,new Settings{Platform="GooglePlayGames"},1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<1000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*100));
            Assert(!runner.Active && game.Deployments==0 && game.SelectionKeys.Count==0,"failed selection key stops before any troop placement");
            game=new FakeGame{SpellsRequired=false};runner=new Runner(game,new Settings{Platform="GooglePlayGames",UseSpells=false},1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<1000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*100));
            Assert(runner.Completed==1 && game.SelectionKeys.SequenceEqual(new[]{Keys.D1}) && game.SpellCasts==0,"Google sends no spell key when spells are disabled");
            game=new FakeGame{Screen="scout"};runner=new Runner(game,new Settings{Platform="GooglePlayGames"},1,true,logs.Add,new Random(2));runner.Start(now);
            runner.Stop("Stopped before selection");runner.Tick(now.AddHours(1));
            Assert(game.SelectionKeys.Count==0 && game.Deployments==0,"Stop cancels pending keyboard selection");
            var inputType=typeof(Native).GetNestedType("Input",System.Reflection.BindingFlags.NonPublic);
            Assert(System.Runtime.InteropServices.Marshal.SizeOf(inputType)==(IntPtr.Size==8?40:28),"mouse and keyboard INPUT union has the required Windows ABI size");
            game=new FakeGame{UnreadableFirst=true};runner=new Runner(game,new Settings{Platform="GooglePlayGames"},1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<1000 && game.LootReads<2;t++) runner.Tick(now.AddMilliseconds(t*100));
            runner.Stop("Stopped during loot retries");runner.Tick(now.AddHours(1));
            Assert(game.LootReads==2 && game.Deployments==0 && game.NextClicks==0,"Stop cancels the attack fallback while loot retries are pending");
            game=new FakeGame{Opponents=new List<LootReading>{new LootReading(1966173,2046173)}};runner=new Runner(game,new Settings{Platform="GooglePlayGames"},1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<1000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*100));
            Assert(runner.Completed==1 && game.NextClicks==0 && game.Deployments==25,"qualifying Google base is accepted without a Next click");
            game=new FakeGame();game.TransientLoot.Enqueue(new LootReading(700000,700000));game.TransientLoot.Enqueue(new LootReading(400000,700000));game.TransientLoot.Enqueue(new LootReading(400000,700000));
            runner=new Runner(game,settings,1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<5000 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(runner.Completed==1 && game.NextClicks==1,"single transient high loot reading cannot trigger deployment");
            game=new FakeGame{Opponents=new List<LootReading>{new LootReading(1,1)}};runner=new Runner(game,settings,1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<200;t++) runner.Tick(now.AddMilliseconds(t*300));
            int skippedBeforeStop=game.NextClicks;runner.Stop("Stopped searching.");runner.Tick(now.AddHours(1));
            Assert(skippedBeforeStop>0 && game.NextClicks==skippedBeforeStop && game.Deployments==0,"indefinite loot search remains cancellable");
            game=new FakeGame{MissingLaunch=true};runner=new Runner(game,settings,1,false,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<500 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(!runner.Active && game.FindClicks==1 && game.ArmyClicks==0 && game.Selections==0 && game.Deployments==0,"missing army Attack times out without selecting or deploying troops");
            game=new FakeGame{Screen="scout"};runner=new Runner(game,settings,1,true,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<500 && runner.Active;t++) runner.Tick(now.AddMilliseconds(t*300));
            Assert(!runner.Active && game.Deployments==25 && game.HomeClicks==0 && game.ReturnClicks==0,"deployment only never navigates");
            game=new FakeGame{Missing=true};runner=new Runner(game,settings,10,false,logs.Add,new Random(2));runner.Start(now);
            runner.Tick(now.AddSeconds(46));Assert(!runner.Active && game.HomeClicks==0,"missing button times out without clicks");
            game=new FakeGame{Occluded=true};runner=new Runner(game,settings,10,false,logs.Add,new Random(2));runner.Start(now);
            runner.Tick(now.AddSeconds(1));Assert(!runner.Active && game.HomeClicks==0,"focus failure stops before clicking");
            game=new FakeGame{Screen="scout",WrongTroop=true};runner=new Runner(game,settings,1,true,logs.Add,new Random(2));runner.Start(now);
            runner.Tick(now.AddSeconds(1));runner.Tick(now.AddSeconds(2));runner.Tick(now.AddSeconds(3));Assert(!runner.Active && game.Selections==0,"wrong troop stops");
            game=new FakeGame();runner=new Runner(game,settings,10,false,logs.Add,new Random(2));runner.Start(now);
            runner.Tick(now.AddSeconds(1));Assert(game.HomeClicks==0,"button requires two observations");
            game.Missing=true;runner.Tick(now.AddSeconds(2));game.Missing=false;runner.Tick(now.AddSeconds(3));Assert(game.HomeClicks==0,"missing frame resets stability");
            runner.Tick(now.AddSeconds(4));Assert(game.HomeClicks==1,"stable button clicked once");
            game=new FakeGame{Screen="scout"};runner=new Runner(game,settings,1,true,logs.Add,new Random(2));runner.Start(now);
            for(int t=0;t<500 && game.Deployments<4;t++) runner.Tick(now.AddMilliseconds(t*300));
            int beforeStop=game.Deployments;runner.Stop("Stopped by user.");
            for(int t=0;t<100;t++) runner.Tick(now.AddMinutes(2).AddMilliseconds(t*300));
            Assert(beforeStop==4 && game.Deployments==beforeStop && runner.Message=="Stopped by user.","manual stop prevents subsequent placements and preserves the reason");
            Assert(PlayerWindows.IsMain("BlueStacks App Player",2560,1392,false),"main BlueStacks window selected");
            Assert(!PlayerWindows.IsMain("BlueStacks App Player",240,130,false),"small helper window ignored");
            Assert(!PlayerWindows.IsMain("BlueStacks App Player",1200,800,true),"owned popup ignored");
            Assert(!PlayerWindows.IsMain("BlueStacks Settings",1200,800,false),"settings window ignored");
            var googleClient=new Rectangle(42,0,2471,1388);var googlePane=new Rectangle(106,36,2411,1356);
            Assert(Native.IsGoogleGameArea(googlePane,googleClient),"actual Google maximized pane tolerates its four-pixel frame overlap");
            Rectangle googleView=Native.FitGameArea(Rectangle.Intersect(googlePane,googleClient));
            Assert(googleClient.Contains(googleView) && googleView.X>=106 && googleView.Y>=36 && Math.Abs((double)googleView.Width/googleView.Height-16.0/9)<.002,"clipped Google game area excludes title, sidebar and desktop gaps");
            Assert(!Native.IsGoogleGameArea(new Rectangle(106,36,2425,1356),googleClient) && !Native.IsGoogleGameArea(googleClient,googleClient),"large offscreen overflow and whole-window candidates are rejected");
            Assert(AdbClient.Enabled(new Settings{Platform="GooglePlayGames",UseAdbWhenAvailable=false}),"Google Developer always uses ADB without desktop fallback");
            Assert(AdbClient.ValidSerial("localhost:6520") && !AdbClient.ValidSerial("localhost.example.com:6520"),"Google loopback alias accepted without remote hosts");
            var oldGeometry=new Settings{GeometryVersion=2,Normal=Settings.PreviousOuterLines(false),Reverse=Settings.PreviousOuterLines(true),AttackDurationSeconds=30};
            var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
            var previousConfig=serializer.Deserialize<Dictionary<string,object>>(serializer.Serialize(oldGeometry));
            previousConfig.Remove("NormalSpellGuide");previousConfig.Remove("ReverseSpellGuide");
            var adjusted=Settings.FromJson(serializer.Serialize(previousConfig));
            Assert(adjusted.GeometryVersion==4 && adjusted.AttackDurationSeconds==30,"geometry migration preserves the selected duration");
            for(int i=2;i<4;i++)Assert(adjusted.Normal[i].X==oldGeometry.Normal[i].X && adjusted.Normal[i].Y==oldGeometry.Normal[i].Y,"working lower-left green line is unchanged");
            for(int i=0;i<2;i++) {
                Assert(adjusted.Normal[i].X<oldGeometry.Normal[i].X && adjusted.Normal[i].Y<oldGeometry.Normal[i].Y,"upper-left moves outward and upward");
                Assert(adjusted.Reverse[i].X>oldGeometry.Reverse[i].X && adjusted.Reverse[i].Y<oldGeometry.Reverse[i].Y,"upper-right moves outward and upward");
            }
            foreach(Vec p in Plan.Make(adjusted,true,new Random(11)).Where(p=>p.Y>.5))Assert(p.X+.78*p.Y>1.28 && p.X+.78*p.Y<1.30 && p.Y<=.83,"lower-right points stay beyond the tile edge and above the troop bar");
            var tileEdge=new Settings{GeometryVersion=3,Battles=2,AttackDurationSeconds=30};
            tileEdge.Reverse[2]=new Vec(.86,.51);tileEdge.Reverse[3]=new Vec(.6104,.83);
            var grass=Settings.FromJson(serializer.Serialize(tileEdge));
            Assert(grass.GeometryVersion==4 && grass.Battles==2 && grass.AttackDurationSeconds==30,"version 3 migration keeps run preferences");
            for(int i=0;i<4;i++)Assert(grass.Normal[i].X==tileEdge.Normal[i].X && grass.Normal[i].Y==tileEdge.Normal[i].Y,"all normal L endpoints stay unchanged");
            for(int i=0;i<4;i++)Assert(Math.Abs((grass.Reverse[i].X-tileEdge.Reverse[i].X)-(i<2?0:.03))<1e-10 && grass.Reverse[i].Y==tileEdge.Reverse[i].Y,"only lower-right endpoints move outward by 48 normalized pixels");
            tileEdge.Reverse[2].X=.87;
            var customGrass=Settings.FromJson(serializer.Serialize(tileEdge));
            Assert(customGrass.Reverse[2].X==.87 && customGrass.Reverse[3].X==.6104,"custom lower-right paths are preserved");
            var legacyDepth=Settings.FromJson("{\"OpeningSpellInward\":0.6,\"Battles\":7,\"Spells\":15}");
            Assert(legacyDepth.LShapeOpeningDepthPercent==0 && legacyDepth.LShapeLaterDepthPercent==70 && legacyDepth.FourSidesSpellDepthPercent==0 && legacyDepth.Battles==7 && legacyDepth.Spells==15,"legacy depth defaults preserve run settings");
            foreach(string mode in new[]{"LShape","FourSides"}) foreach(bool orientation in new[]{false,true}) foreach(int depth in new[]{0,10,70,100}) {
                var depthSettings=new Settings{DeploymentMode=mode,LShapeOpeningDepthPercent=depth,LShapeLaterDepthPercent=depth,FourSidesSpellDepthPercent=depth};
                depthSettings.Normal[0].X-=.01; // Custom paths must be the source, too.
                for(int seed=0;seed<30;seed++) {
                    var opening=Plan.Opening(depthSettings,orientation,new Random(seed));
                    for(int i=0;i<opening.Points.Count;i++) Assert(OnDepth(opening.Points[i],opening.Sides[opening.PointSides[i]],depth),"opening spells use exact selected depth on actual paths");
                    if(mode=="LShape") {
                        var later=Plan.LaterSpell(depthSettings,orientation,20,new Random(seed));
                        Assert(Plan.Sides(depthSettings,orientation).Any(side=>OnDepth(later,side,depth)),"later spells use exact depth without radial jitter");
                    }
                }
                var before=Plan.OpeningSpells(depthSettings,orientation,new Random(6));
                depthSettings.NormalSpellGuide=null;depthSettings.ReverseSpellGuide=null;depthSettings.OpeningSpellInward=-1;
                var after=Plan.OpeningSpells(depthSettings,orientation,new Random(6));
                Assert(before.Zip(after,(a,b)=>a.X==b.X && a.Y==b.Y).All(v=>v),"retired spell settings no longer affect placement");
            }
            foreach(string field in new[]{"LShapeOpeningDepthPercent","LShapeLaterDepthPercent","FourSidesSpellDepthPercent"}) foreach(int invalidDepth in new[]{-1,101}) {
                bool failed=false;try{Settings.FromJson("{\""+field+"\":"+invalidDepth+"}");}catch{failed=true;}Assert(failed,"out of range depth rejected");
            }
            var rng=new Random(6);
            for(int k=0;k<1000;k++) foreach(bool reverse in new[]{false,true}) {
                List<Vec> points=Plan.Make(settings,reverse,rng);Vec[] lines=reverse?settings.Reverse:settings.Normal;
                Assert(points.Count==25,"geometry count");
                for(int i=0;i<points.Count;i++) {
                    int arm=points[i].Y<.5?0:1;Vec a=lines[arm*2],b=lines[arm*2+1],p=points[i];
                    double cross=(p.X-a.X)*(b.Y-a.Y)-(p.Y-a.Y)*(b.X-a.X);
                    Assert(Math.Abs(cross)<1e-10 && p.X>=Math.Min(a.X,b.X) && p.X<=Math.Max(a.X,b.X) && p.Y>=Math.Min(a.Y,b.Y) && p.Y<=Math.Max(a.Y,b.Y),"random point stays on its segment");
                }
                List<Vec> opening=Plan.OpeningSpells(settings,reverse,rng);
                Assert(opening.Count==6,"three opening spells per arm");
                Assert(opening.Take(3).Average(p=>p.Y)<opening.Skip(3).Average(p=>p.Y),"first three and second three cover separate entry flanks");
                for(int arm=0;arm<2;arm++) {
                    DeploymentSide side=Plan.Sides(settings,reverse)[arm];
                    foreach(Vec p in opening.Skip(arm*3).Take(3)) Assert(OnDepth(p,side,0),"default opening spells lie on troop paths");
                }
                foreach(Vec spell in opening) Assert(spell.Y>=.05 && spell.Y<=.835,"opening spells stay above troop bar");
                foreach(double seconds in new[]{0.0,15.0,40.0,90.0,180.0}) {
                    Vec spell=Plan.LaterSpell(settings,reverse,seconds,rng);
                    Assert(spell.Y>=.14 && spell.Y<=.74 && (reverse ? spell.X>=.52 && spell.X<=.92 : spell.X>=.08 && spell.X<=.48),"later spell target stays in the attacked half");
                }
            }
            Assert(!Vision.Label("attack","End Battle") && !Vision.Label("find","Next") && !Vision.Label("return","Okay"),"unrelated buttons rejected");
            Assert(Vision.Label("attack","Battle") && !Vision.Label("attack","Ranked Battle") && !Vision.Label("attack","Battle ends in"),"current home Battle label accepted without accepting in-battle labels");
            Assert(Vision.Label("attack","Baffle"),"observed Google Battle OCR with both stylized t letters read as f is accepted inside the home-button region");
            Assert(Vision.Label("attack","'Baetie") && Vision.Label("attack","BattiE") && !Vision.Label("attack","Battle ends in") && !Vision.Label("attack","Ranked Battle"),"common home-button font confusions accepted without matching battle screen labels");
            Assert(Vision.Label("return","R TURN -") && !Vision.Label("return","Okay"),"observed Google Return OCR with missing e is accepted in the results-only button crop");
            Assert(Vision.Label("find","FiNd a Matcx") && !Vision.Label("find","Join Tournament"),"observed Find a Match OCR error accepted without selecting ranked button");
            Assert(Vision.Label("find","FiNd a Mate") && Vision.Label("find","Find a Macth") && Vision.Label("find","FiNd a Mate 1500 0"),"80-percent button matching accepts observed missing/misread letters and ignores price");
            Assert(Vision.TextSimilarity("findamate","findamatch")>=.8 && !Vision.Label("find","Find a Player") && !Vision.Label("find","Edit Army") && !Vision.Label("find","Next"),"fuzzy labels remain bounded to the full expected phrase");
            Assert(Vision.Locate("find",new[]{new OcrLine{Text="FiNd a Mate",X=100,Y=20,Width=150,Height=30}},Vision.Region("find"))!=null,"fuzzy match maps to the known Find a Match button region");
            Assert(Vision.Locate("find",new[]{new OcrLine{Text="Find a",X=100,Y=20,Width=100,Height=25},new OcrLine{Text="Mate",X=110,Y=50,Width=80,Height=25}},Vision.Region("find"))!=null,"split label lines use the same tolerant comparison");
            Assert(Vision.Label("launch","Attack!") && !Vision.Label("launch","Boost Army") && !Vision.Label("launch","Battle"),"army confirmation recognizes only Attack");
            Assert(AdbClient.ValidSerial("127.0.0.1:5555") && !AdbClient.ValidSerial("192.168.1.20:5555") && !AdbClient.ValidSerial("127.0.0.1:99999") && !AdbClient.ValidSerial("127.0.0.1:5555 shell"),"ADB only accepts a valid loopback endpoint");
            Assert(AdbClient.ValidSerial("emulator-5554") && !AdbClient.ValidSerial("emulator-5554 shell") && !AdbClient.ValidSerial("emulator-99999"),"registered local emulator serial accepted without shell arguments");
            Assert(Vision.Label("attack","Attack!") && Vision.Label("find","Find a Match") && Vision.Label("return","Return Home"),"expected button labels");
            Rectangle roi=Vision.Region("next");
            Assert(Vision.Locate("next",new[]{new OcrLine{Text="Next",X=30,Y=50,Width=100,Height=40}},roi)!=null,"OCR coordinates converted");
            Assert(Vision.Locate("next",new[]{new OcrLine{Text="Next"},new OcrLine{Text="Next"}},roi)==null,"ambiguous labels rejected");
            long number;
            Assert(Vision.TryLootNumber("500 000",out number) && number==500000 && Vision.TryLootNumber("1,437,714",out number) && number==1437714 && Vision.TryLootNumber("5,00,000",out number) && number==500000,"loot digit grouping parsed");
            Assert(!Vision.TryLootNumber("500K",out number) && !Vision.TryLootNumber("1 4370714",out number) && !Vision.TryLootNumber("Gold 500000",out number),"ambiguous loot strings rejected");
            using(Bitmap a=new Bitmap(40,40)) using(Bitmap b=new Bitmap(40,40)) {
                using(Graphics g=Graphics.FromImage(a)) g.Clear(Color.Blue);
                using(Graphics g=Graphics.FromImage(b)) g.Clear(Color.Red);
                Assert(Vision.Similar(a,a) && !Vision.Similar(a,b),"troop appearance comparison");
            }
            settings.MinDelayMs=0;bool rejected=false;try { settings.Validate(); } catch { rejected=true; }Assert(rejected,"bad configuration rejected");
            return "PASS: both 500,000 loot thresholds, stable reads, unreadable loot retries, Next filtering and cancellation, 30 battles, 750 troops, 330 spells, fast six-cast burst without OCR, inward placement, 5-10 / 2-5 second late casts, fast navigation and scanned fallback, timed exit and guarded surrender, early results, stop behavior, and recognition checks. No live input sent.";
        }
    }
}



