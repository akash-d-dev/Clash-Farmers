using System;
using System.Collections.Generic;

namespace ClashPekkas {
    public sealed class ZoomFake : RecoveryFake,IZoomGame,IPlacementGame {
        public int Zooms,PlanningCalls;public bool FailZoom;
        public readonly List<int> HomeSwipeCountsAtZoom=new List<int>();
        public NavigationObservation ObserveZoomScene(){return ObserveNavigation();}
        public void ZoomOutStep() {
            Guard();if(Screen!="home" && Screen!="scout" || ExitDialog || Menus>0)throw new Exception("Zoom reached an unsafe screen");
            if(FailZoom)throw new Exception("zoom injection failed");
            Zooms++;HomeSwipeCountsAtZoom.Add(HomeSwipes);
        }
        public Settings PlacementSettings(Settings settings) {
            if(Zooms<3)throw new Exception("Placement planned before zoom preparation");PlanningCalls++;return settings;
        }
    }
    public sealed class DesktopZoomFake : FakeGame,IZoomGame {
        public int Zooms;
        public NavigationObservation ObserveZoomScene(){return new NavigationObservation{Kind=Screen=="home"?"Home":Screen=="scout"?"Scouting":"Battle"};}
        public void ZoomOutStep(){Guard();if(Screen!="home" && Screen!="scout")throw new Exception("Unsafe desktop zoom");Zooms++;}
    }
    public static class ZoomTests {
        static void Check(bool ok,string message){if(!ok)throw new Exception("ZOOM TEST FAILED: "+message);}
        static Settings Options(){return new Settings{Platform="GooglePlayGames",Troops=2,Spells=0,UseSpells=false,RandomSwipes=false,DeploySiegeAndFirstHero=false,MinGold=0,MinElixir=0};}
        static DateTime RunUntil(Runner runner,FakeGame game,DateTime start,Func<bool> done=null) {
            for(int i=0;i<16000 && runner.Active;i++) {
                game.Now=start.AddMilliseconds(i*50);runner.Tick(game.Now);
                if(done!=null && done())break;
            }
            return game.Now.AddMilliseconds(50);
        }
        public static string Run() {
            var now=new DateTime(2026,1,1);var logs=new List<string>();
            var game=new ZoomFake{TroopsPerBattle=2,SpellsRequired=false};
            var runner=new Runner(game,Options(),3,false,logs.Add,new Random(4));runner.Start(now);RunUntil(runner,game,now);
            Check(runner.Completed==3 && game.Zooms==12 && game.PlanningCalls==3 && game.HomeSwipes==0,"zoom at Start and after all three returns, including final completion, with swipes disabled");
            runner.Start(game.Now.AddSeconds(1));RunUntil(runner,game,game.Now.AddSeconds(1));
            Check(runner.Completed==3 && game.Zooms==24,"a new Start prepares zoom again");

            var desktop=new DesktopZoomFake{TroopsPerBattle=2,SpellsRequired=false};runner=new Runner(desktop,Options(),2,false,logs.Add,new Random(4));runner.Start(now);RunUntil(runner,desktop,now);
            Check(runner.Completed==2 && desktop.HomeClicks==2 && desktop.Zooms==9 && !runner.Active,"desktop final home zoom stops without opening an extra battle");

            var swiping=Options();swiping.RandomSwipes=true;
            game=new ZoomFake{TroopsPerBattle=2,SpellsRequired=false};runner=new Runner(game,swiping,2,false,logs.Add,new Random(4));runner.Start(now);RunUntil(runner,game,now);
            Check(runner.Completed==2 && game.Zooms==9 && game.HomeSwipeCountsAtZoom[3]>0 && game.HomeSwipeCountsAtZoom[6]>game.HomeSwipeCountsAtZoom[3] && game.HomeSwipeCountsAtZoom[8]==game.HomeSwipes,"each home zoom follows that visit's swipes, including the final battle");

            var filtered=Options();filtered.MinGold=500000;filtered.MinElixir=500000;
            game=new ZoomFake{TroopsPerBattle=2,SpellsRequired=false,Opponents=new List<LootReading>{new LootReading(100,100),new LootReading(1000000,1000000)}};
            runner=new Runner(game,filtered,1,false,logs.Add,new Random(4));runner.Start(now);RunUntil(runner,game,now);
            Check(runner.Completed==1 && game.NextClicks==1 && game.Zooms==6,"skipping a below-target opponent does not repeat zoom");

            game=new ZoomFake{TroopsPerBattle=2,SpellsRequired=false};runner=new Runner(game,Options(),2,false,logs.Add,new Random(4));runner.Start(now);
            var next=RunUntil(runner,game,now,()=>runner.Completed==1 && runner.State=="Fast find");
            game.DropFindOnce=true;RunUntil(runner,game,next);
            Check(runner.Completed==2 && game.Zooms==12,"navigation recovery reapplies zoom once, without duplicating a completed battle");

            game=new ZoomFake{TroopsPerBattle=2,SpellsRequired=false,UnreadableHome=true};runner=new Runner(game,Options(),1,false,logs.Add,new Random(4));runner.Start(now);RunUntil(runner,game,now);
            Check(runner.Completed==1 && game.Zooms==6 && game.Cancels==1,"recovery cancels Exit before zooming verified home");

            game=new ZoomFake{TroopsPerBattle=2,SpellsRequired=false};runner=new Runner(game,Options(),1,false,logs.Add,new Random(4));runner.Start(now);
            next=RunUntil(runner,game,now,()=>game.Zooms==1);runner.Stop("test stop");RunUntil(runner,game,next);
            Check(game.Zooms==1 && game.HomeClicks==0 && game.Selections==0,"Stop cancels remaining zoom steps and navigation");

            game=new ZoomFake{TroopsPerBattle=2,SpellsRequired=false};runner=new Runner(game,Options(),1,false,logs.Add,new Random(4));runner.Start(now);
            next=RunUntil(runner,game,now,()=>game.Zooms==1);game.ExitDialog=true;RunUntil(runner,game,next);
            Check(runner.Completed==1 && game.Zooms==7 && game.Cancels==1,"a dialog interrupting zoom recovers, then restarts the three-step preparation");

            game=new ZoomFake{Screen="scout",TroopsPerBattle=2,SpellsRequired=false};runner=new Runner(game,Options(),1,true,logs.Add,new Random(4));runner.Start(now);RunUntil(runner,game,now);
            Check(game.Zooms==3 && game.Deployments==2,"deploy-only entry zooms the verified scouting screen before selection");

            game=new ZoomFake{TroopsPerBattle=2,SpellsRequired=false,FailZoom=true};runner=new Runner(game,Options(),1,false,logs.Add,new Random(4));runner.Start(now);RunUntil(runner,game,now);
            Check(!runner.Active && game.HomeClicks==0 && game.Selections==0 && runner.Message.Contains("zoom injection failed"),"input failure stops explicitly before deploying at an unknown zoom");

            game=new ZoomFake{Screen="fighting",TroopsPerBattle=2,SpellsRequired=false,ForcedKind="Battle"};runner=new Runner(game,Options(),1,false,logs.Add,new Random(4));runner.Start(now);
            RunUntil(runner,game,now,()=>runner.State=="Result");Check(game.Zooms==0 && game.Backs==0,"recovery recognizing an active battle never zooms it");
            return "PASS: zoom at Start and after every battle, swipe ordering/on-off, recovery reset, final completion, verified scenes, exact counts, cancellation, and input failures. No live input.";
        }
    }
}
