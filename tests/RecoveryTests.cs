using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
namespace ClashPekkas {
    public class RecoveryFake : FakeGame,IRecoveryGame,ISwipeGame {
        public int Menus,Backs,Cancels,Observations,HomeSwipes,BlockOnSwipe;
        public bool ExitDialog,UnreadableHome,Permanent,FailBack,FailCapture,BlockReturnOnce,FailNavigation;
        public bool InactivityDialog,InactivityStuck;public int Reloads;
        public string ForcedKind;
        public DateTime VisibleAfter=DateTime.MinValue;
        public readonly List<DateTime> BackTimes=new List<DateTime>();
        public readonly List<RecoveryRecord> Records=new List<RecoveryRecord>();
        public virtual NavigationObservation ObserveNavigation() {
            Guard();Observations++;if(FailCapture)throw new Exception("connection failed");
            var result=new NavigationObservation();
            result.Kind=ForcedKind??(Permanent?"Unknown":InactivityDialog?"Inactivity dialog":ExitDialog?"Exit dialog":Menus>0?"Menu":Screen=="home"?(UnreadableHome || Now<VisibleAfter?"Unknown":"Home"):Screen=="find" || Screen=="army"?"Menu":Screen=="scout"?"Scouting":Screen=="result"?"Results":"Battle");
            if(result.Kind=="Inactivity dialog")result.Reload=new Vec(.34,.56);
            if(result.Kind=="Home")result.Home=new Vec(.065,.91);
            if(result.Kind=="Exit dialog")result.Cancel=new Vec(.42,.62);
            if(result.Kind=="Results")result.Return=new Vec(.5,.8);
            return result;
        }
        public void Back() {
            Guard();if(FailBack)throw new Exception("connection failed");
            if(InactivityDialog)throw new Exception("Back sent to an inactivity dialog");
            if(Screen!="home" && Screen!="find" && Screen!="army")throw new Exception("Back reached an active battle");
            Backs++;BackTimes.Add(Now);
            if(Permanent)return;
            if(Menus>0){Menus--;return;}
            if(Screen=="find" || Screen=="army"){Screen="home";return;}
            ExitDialog=true;
        }
        public override void Click(Vec point) {
            Guard();
            if(InactivityDialog){if(point.X<.3 || point.X>.4 || point.Y<.5 || point.Y>.65)throw new Exception("Wrong reload point");Reloads++;InactivityDialog=InactivityStuck;return;}
            if(ExitDialog){if(point.X>=.5)throw new Exception("Exit Okay was clicked");Cancels++;ExitDialog=false;UnreadableHome=false;return;}
            if(FailNavigation && Screen=="home")return;
            string before=Screen;base.Click(point);
            if(before=="result" && BlockReturnOnce){BlockReturnOnce=false;UnreadableHome=true;}
        }
        public void Swipe(Vec from,Vec to,int durationMs) {
            Guard();if(Screen=="home"){HomeSwipes++;if(HomeSwipes==BlockOnSwipe)UnreadableHome=true;}
        }
        public void SaveRecovery(NavigationObservation observation,RecoveryRecord record){Records.Add(record);}
    }
    public sealed class ScreenshotHomeGame : RecoveryFake {
        readonly ScreenReader reader;readonly System.Drawing.Bitmap frame;
        public ScreenshotHomeGame(ScreenReader reader,System.Drawing.Bitmap frame){this.reader=reader;this.frame=frame;}
        public override NavigationObservation ObserveNavigation(){if(Screen!="home")return base.ObserveNavigation();Guard();Observations++;return reader.ObserveNavigation(frame);}
    }
    public sealed class MissingHeroFake : FakeGame {
        public override bool TrySelectSupport(Keys key){return key==Keys.W?false:base.TrySelectSupport(key);}
    }
    public static class RecoveryTests {
        static void Check(bool ok,string message){if(!ok)throw new Exception("RECOVERY TEST FAILED: "+message);}
        static Settings Options(){return new Settings{Platform="GooglePlayGames",RandomSwipes=false,DeploySiegeAndFirstHero=false,MinGold=0,MinElixir=0};}
        static void RunUntil(Runner runner,RecoveryFake game,DateTime start,int seconds=400,Func<bool> done=null) {
            for(int i=0;i<seconds*20 && runner.Active;i++){game.Now=start.AddMilliseconds(i*50);runner.Tick(game.Now);if(done!=null && done())break;}
        }
        public static string Run() {
            var now=new DateTime(2026,1,1);
            foreach(int depth in new[]{1,3,5}) {
                var game=new RecoveryFake{Menus=depth};var runner=new Runner(game,Options(),1,false,_=>{},new Random(3));runner.Start(now);RunUntil(runner,game,now);
                Check(runner.Completed==1 && game.Backs==depth,"nested menus recover and finish one battle");
                Check(game.BackTimes.First()>=now.AddSeconds(8),"recovery waits eight seconds without progress");
                Check(game.BackTimes.Zip(game.BackTimes.Skip(1),(a,b)=>(b-a).TotalSeconds).All(gap=>gap>=1),"Back inputs settle at least one second");
            }
            foreach(bool existingExit in new[]{false,true}) {
                var game=new RecoveryFake{UnreadableHome=true,ExitDialog=existingExit};var runner=new Runner(game,Options(),1,false,_=>{},new Random(4));runner.Start(now);RunUntil(runner,game,now);
                Check(runner.Completed==1 && game.Cancels==1 && game.Backs==(existingExit?0:1),"exit dialog is cancelled only when recognized");
            }
            foreach(bool stuck in new[]{false,true}) {
                var game=new RecoveryFake{InactivityDialog=true,InactivityStuck=stuck};var runner=new Runner(game,Options(),1,false,_=>{},new Random(1));runner.Start(now);RunUntil(runner,game,now);
                Check(game.Backs==0 && game.Reloads==(stuck?3:1),"recognized inactivity reloads once per cycle without Back inputs");
                Check(stuck?!runner.Active && runner.RecoveryAttempt==3:runner.Completed==1,"inactivity either recovers or stops after three bounded cycles");
            }
            var slowReload=new RecoveryFake{InactivityDialog=true,VisibleAfter=now.AddSeconds(20)};
            var reloadRunner=new Runner(slowReload,Options(),1,false,_=>{},new Random(1));reloadRunner.Start(now);RunUntil(reloadRunner,slowReload,now);
            Check(reloadRunner.Completed==1 && slowReload.Reloads==1 && slowReload.Backs==0,"blank/loading frames after reload are waited out without Back");
            var loadingForever=new RecoveryFake{ForcedKind="Loading"};var loadingRunner=new Runner(loadingForever,Options(),1,false,_=>{},new Random(1));loadingRunner.Start(now);RunUntil(loadingRunner,loadingForever,now);
            Check(!loadingRunner.Active && loadingRunner.RecoveryAttempt==3 && loadingForever.Backs==0,"persistent loading remains bounded without Back input");
            var loading=new RecoveryFake{VisibleAfter=now.AddSeconds(6)};var loadRunner=new Runner(loading,Options(),1,false,_=>{},new Random(1));loadRunner.Start(now);RunUntil(loadRunner,loading,now);
            Check(loadRunner.Completed==1 && loading.Backs==0,"short loading transition needs no Back input");
            var failed=new RecoveryFake{Permanent=true};var failRunner=new Runner(failed,Options(),1,false,_=>{},new Random(1));failRunner.Start(now);RunUntil(failRunner,failed,now);
            Check(!failRunner.Active && failRunner.RecoveryAttempt==3 && failed.Backs==15 && failed.Records.Count==6 && failRunner.Completed==0,"three bounded cycles stop with entry and failure diagnostics");
            Check(failed.BackTimes[5]-failed.BackTimes[4]>=TimeSpan.FromSeconds(3),"two-second cooldown follows the last Back settling period");
            var navigation=new RecoveryFake{FailNavigation=true};var navRunner=new Runner(navigation,Options(),1,false,_=>{},new Random(1));navRunner.Start(now);RunUntil(navRunner,navigation,now);
            Check(!navRunner.Active && navRunner.RecoveryAttempt==3,"recovering home alone does not reset the retry budget");
            foreach(bool captureFailure in new[]{true,false}) {
                var game=new RecoveryFake{UnreadableHome=true,FailCapture=captureFailure,FailBack=!captureFailure};var runner=new Runner(game,Options(),1,false,_=>{},new Random(1));runner.Start(now);RunUntil(runner,game,now);
                Check(!runner.Active && game.Backs==0 && runner.Message.Contains("connection failed"),"connection errors stop without retries");
            }
            foreach(string kind in new[]{"Scouting","Battle","Battle exit","Results"}) {
                var game=new RecoveryFake{ForcedKind=kind,Screen=kind=="Scouting"?"scout":kind=="Results"?"result":"fighting"};var runner=new Runner(game,Options(),1,false,_=>{},new Random(1));runner.Start(now);
                RunUntil(runner,game,now,12,()=>runner.State=="Scouting" || runner.State=="Result" || runner.State=="Exit battle" || runner.State=="Search return");
                Check(game.Backs==0 && runner.State!="Home recovery","recognized non-home scenes resume without Back");
            }
            var returning=new RecoveryFake{BlockReturnOnce=true};var resumed=new Runner(returning,Options(),25,false,_=>{},new Random(1));returning.Screen="result";resumed.ResumeExistingBattle(now,11);
            RunUntil(resumed,returning,now,100,()=>returning.HomeClicks==1);
            Check(resumed.Completed==12 && returning.Cancels==1 && returning.HomeClicks==1,"blocked return counts battle 12 exactly once then starts battle 13");
            var swiping=new RecoveryFake{BlockOnSwipe=1};var swipeOptions=Options();swipeOptions.RandomSwipes=true;
            var swipeRunner=new Runner(swiping,swipeOptions,25,false,_=>{},new Random(1));swiping.Screen="result";swipeRunner.ResumeExistingBattle(now,11);
            RunUntil(swipeRunner,swiping,now,100,()=>swiping.HomeClicks==1);
            Check(swipeRunner.Completed==12 && swiping.HomeSwipes==1 && swiping.Cancels==1,"home swipe obstruction recovers without recounting or repeating gestures");
            var finalSwipe=new RecoveryFake{BlockOnSwipe=1};var finalRunner=new Runner(finalSwipe,swipeOptions,12,false,_=>{},new Random(1));finalSwipe.Screen="result";finalRunner.ResumeExistingBattle(now,11);RunUntil(finalRunner,finalSwipe,now);
            Check(finalRunner.Completed==12 && !finalRunner.Active && finalSwipe.HomeClicks==0,"final battle recovers to home and finishes without starting another");
            foreach(bool afterBack in new[]{false,true}) {
                var game=new RecoveryFake{Permanent=true};var runner=new Runner(game,Options(),1,false,_=>{},new Random(1));runner.Start(now);
                RunUntil(runner,game,now,20,()=>afterBack?game.Backs>0:runner.State=="Home");
                int inputs=game.Backs+game.Cancels;runner.Stop("user stop");runner.Tick(now.AddHours(1));Check(game.Backs+game.Cancels==inputs,"Stop cancels queued recovery inputs");
            }
            string[] clockwise={"upper-left","upper-right","lower-right","lower-left"};
            foreach(string mode in new[]{"LShape","FourSides"})foreach(bool reverse in new[]{false,true}) {
                var options=Options();options.DeploymentMode=mode;
                // A custom endpoint must remain the source of support geometry.
                options.Normal[0]=new Vec(.12,.42);
                var starts=new HashSet<string>();var combinations=new HashSet<string>();
                for(int seed=0;seed<300;seed++) {
                    SupportPlan plan=Plan.Support(options,reverse,new Random(seed));starts.Add(plan.Heroes[0].Name);combinations.Add(plan.Heroes[0].Name+":"+plan.Siege.Name);
                    var allowed=Plan.Sides(options,reverse);Check(allowed.Any(side=>side.Name==plan.Siege.Name),"siege stays in allowed sides");
                    for(int i=0;i<4;i++) {
                        Check(allowed.Any(side=>side.Name==plan.Heroes[i].Name && Plan.PixelDistance(SupportPlan.Midpoint(side),SupportPlan.Midpoint(plan.Heroes[i]))<.01),"support uses unchanged custom midpoints");
                        if(options.FourSides)Check(plan.Heroes[i].Name==clockwise[(Array.IndexOf(clockwise,plan.Heroes[0].Name)+i)%4],"clockwise hero rotation");
                    }
                    if(!options.FourSides)Check(plan.Heroes[0]==plan.Heroes[1] && plan.Heroes[2]==plan.Heroes[3] && plan.Heroes[0]!=plan.Heroes[2],"L keeps two heroes on each selected side");
                }
                Check(starts.Count==(options.FourSides?4:2) && combinations.Count==(options.FourSides?16:4),"independent hero and siege choices cover all allowed combinations");
            }
            var mixed=new MissingHeroFake{HeroAvailable=true,SiegeAvailable=true,FlexibleActions=true,LongBattle=true};var mixedOptions=Options();mixedOptions.DeploySiegeAndFirstHero=true;mixedOptions.DeploymentMode="FourSides";mixedOptions.AttackDurationSeconds=90;
            var mixedLogs=new List<string>();var mixedRunner=new Runner(mixed,mixedOptions,1,false,mixedLogs.Add,new Random(4));mixedRunner.Start(now);
            for(int i=0;i<10000 && mixedRunner.Active;i++){mixed.Now=now.AddMilliseconds(i*50);mixedRunner.Tick(mixed.Now);}
            Check(mixed.HeroDeployments==3 && mixed.SelectionKeys.Count(key=>key==Keys.W)==0,"missing W skips its placement and ability only");
            string assignments=mixedLogs.Single(line=>line.StartsWith("Support sides:"));int index=0;
            foreach(string key in new[]{"Q","E","R"}) {
                string name=System.Text.RegularExpressions.Regex.Match(assignments,key+@"=([a-z-]+)").Groups[1].Value;
                DeploymentSide side=Plan.Sides(mixedOptions,false).Single(item=>item.Name==name);
                Check(Plan.PixelDistance(mixed.SupportPoints[index++],SupportPlan.Midpoint(side))<.01,"missing hero does not shift later assignments");
            }
            return "PASS: bounded home recovery, exact counters, cancellation, independent clockwise support plans, and mixed hero availability. No live input.";
        }
    }
}
