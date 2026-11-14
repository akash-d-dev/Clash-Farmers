using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

// Battle state machine, action scheduling, cancellation, and home recovery.
namespace ClashPekkas {
    public sealed class Runner {
        readonly IGame game; readonly Settings settings; readonly Random random;
        readonly Action<string> log; readonly int limit; readonly bool deployOnly;
        DateTime entered, next; int stable; Vec last;
        List<Vec> plan,openingSpells; PlacementPlan openingLayout; Vec supportPoint,troopCardPoint; Vec[] heroPoints;int heroIndex;static readonly Keys[] HeroKeys={Keys.Q,Keys.W,Keys.E,Keys.R}; readonly List<Vec> spellHistory=new List<Vec>();int deployed,spellsCast;bool reverseAttack,spellsAvailable;
        LootReading previousLoot;int lootFailures,lootConfirmations;
        DateTime troopsFinished,spellDue,battleDeadline;
        readonly List<Keys> pendingAbilities=new List<Keys>();int abilityIndex;
        int actionIndex;
        Settings placementSettings;
        bool battleSwipes;DateTime swipeDue;int homeSwipesRemaining;
        bool battlePending,skipHomeSwipes,recoveryCooling,recoverySnapshot,recoveryReloaded;
        bool zoomPending;int zoomSteps;string zoomScene;DateTime zoomProgress;
        const int ZoomStepCount=3;
        int recoveryBacks;string recoveryOrigin;
        public int RecoveryAttempt {get;private set;}
        static bool HomeState(string state) {return state=="Home" || state=="Return home" || state=="Search home" || state=="Home swipes";}
        static bool HomeNavigation(string state) {return HomeState(state) || state=="Find match" || state=="Army" || state=="Fast verify" || state=="Navigation recovery" || state=="Search return";}
        void CountReturnedBattle() {
            if(!battlePending)return;
            battlePending=false;Completed++;log("Completed battle "+Completed+"/"+limit+".");
        }
        void RecoveryDiagnostic(NavigationObservation observation,string reason) {
            ((IRecoveryGame)game).SaveRecovery(observation,new RecoveryRecord{Reason=reason,Origin=recoveryOrigin,State=State,Attempt=RecoveryAttempt,Completed=Completed,Total=limit,SkippedBases=SkippedBases,BackInputs=recoveryBacks,BattlePending=battlePending});
        }
        void BeginHomeRecovery(DateTime now) {
            zoomPending=true;
            recoveryOrigin=State;battleSwipes=false;homeSwipesRemaining=0;skipHomeSwipes=true;
            if(RecoveryAttempt>=3){Stop("Stopped: home recovery failed after three cycles. See data/diagnostics/recovery. Progress: "+Completed+"/"+limit+".");return;}
            RecoveryAttempt++;recoveryBacks=0;recoveryCooling=false;recoverySnapshot=true;recoveryReloaded=false;
            Go("Home recovery",now);next=now;
            log("Recovering home - attempt "+RecoveryAttempt+"/3 (from "+recoveryOrigin+").");
        }
        bool StableHome(NavigationObservation observation) {
            Vec point=observation.Home;
            if(observation.Kind!="Home" || point==null){stable=0;last=null;return false;}
            if(last!=null && Math.Abs(last.X-point.X)<.015 && Math.Abs(last.Y-point.Y)<.015)stable++;else stable=1;
            last=point;return stable>=2;
        }
        void HomeTick(DateTime now,Stopwatch clock) {
            using(NavigationObservation observation=((IRecoveryGame)game).ObserveNavigation()) {
                if(!StableHome(observation)) {next=now.AddMilliseconds(Math.Ceiling(clock.Elapsed.TotalMilliseconds)+500);return;}
                if(State=="Return home") {
                    CountReturnedBattle();battleDeadline=DateTime.MinValue;
                    if(SwipesEnabled && !skipHomeSwipes){homeSwipesRemaining=random.Next(1,3);Go("Home swipes",now);next=now.AddMilliseconds(Math.Ceiling(clock.Elapsed.TotalMilliseconds)+random.Next(1000,2001));return;}
                }
                if(State=="Home swipes") {
                    if(homeSwipesRemaining>0) {
                        SendSwipe("Home",now);homeSwipesRemaining--;stable=0;last=null;
                        // Verify the village after every gesture, including the final one.
                        entered=now.AddMilliseconds(Math.Ceiling(clock.Elapsed.TotalMilliseconds));
                        next=entered.AddMilliseconds(homeSwipesRemaining>0?random.Next(1000,2001):500);return;
                    }
                }
                if(Completed>=limit){Stop("Finished "+Completed+" battles. Stopped at home.");return;}
                ResetLoot();battleDeadline=DateTime.MinValue;OpenFromHome(observation.Home,now);
                next=next.AddMilliseconds(Math.Ceiling(clock.Elapsed.TotalMilliseconds));
            }
        }
        void RecoveryTick(DateTime now,Stopwatch clock) {
            if(recoveryCooling){RecoveryAttempt++;recoveryBacks=0;recoveryCooling=false;recoverySnapshot=true;recoveryReloaded=false;entered=now;stable=0;last=null;log("Recovering home - attempt "+RecoveryAttempt+"/3.");}
            using(NavigationObservation observation=((IRecoveryGame)game).ObserveNavigation()) {
                if(recoverySnapshot){RecoveryDiagnostic(observation,"entry");recoverySnapshot=false;}
                DateTime observedAt=now.AddMilliseconds(Math.Ceiling(clock.Elapsed.TotalMilliseconds));
                if(observation.Kind=="Scouting") {log("Recovery found scouting; resuming loot checks.");ResetLoot();Go("Scouting",observedAt);return;}
                if(observation.Kind=="Battle" || observation.Kind=="Battle exit") {log("Recovery found an active battle; no Back input sent.");Go(observation.Kind=="Battle exit"?"Exit battle":"Result",observedAt);return;}
                if(observation.Kind=="Results") {log("Recovery found results; resuming return home.");Go(battlePending?"Result":"Search return",observedAt);return;}
                if(StableHome(observation)) {
                    CountReturnedBattle();battleDeadline=DateTime.MinValue;
                    if(Completed>=limit){Stop("Finished "+Completed+" battles. Stopped at home.");return;}
                    log("Home recovered. Skipping remaining home swipes and using scanned navigation.");
                    scannedNavigation=true;ResetLoot();OpenFromHome(observation.Home,observedAt);return;
                }
                // A first clean-home observation needs confirmation, not a Back key.
                bool expired=(observedAt-entered).TotalSeconds>=30;
                if(expired || (recoveryBacks>=5 && observation.Kind!="Exit dialog" && observation.Kind!="Home")) {
                    RecoveryDiagnostic(observation,"cycle failed");
                    if(RecoveryAttempt>=3){Stop("Stopped: home recovery failed after three cycles. See data/diagnostics/recovery. Progress: "+Completed+"/"+limit+".");return;}
                    recoveryCooling=true;next=observedAt.AddSeconds(2);stable=0;last=null;return;
                }
                if(observation.Kind=="Inactivity dialog") {
                    if(observation.Reload!=null && !recoveryReloaded){game.Click(observation.Reload);recoveryReloaded=true;log("Recovery reloading the game after its recognized inactivity disconnect.");}
                    next=now.AddMilliseconds(Math.Ceiling(clock.Elapsed.TotalMilliseconds)+2000);return;
                }
                if(observation.Kind=="Loading" || (recoveryReloaded && observation.Kind=="Unknown")) {
                    // Reloading can briefly show a blank frame before the loading
                    // label appears. Back during this transition can open Exit.
                    next=now.AddMilliseconds(Math.Ceiling(clock.Elapsed.TotalMilliseconds)+1000);return;
                }
                if(observation.Kind=="Exit dialog" && observation.Cancel!=null) {
                    game.Click(observation.Cancel);log("Recovery cancelled Confirm Exit; the game stays open.");
                }else if(observation.Kind!="Home" && recoveryBacks<5) {
                    ((IRecoveryGame)game).Back();recoveryBacks++;log("Recovery Android Back "+recoveryBacks+"/5.");
                }
                next=now.AddMilliseconds(Math.Ceiling(clock.Elapsed.TotalMilliseconds)+1000);
            }
        }
        bool SwipesEnabled {get{return settings.RandomSwipes && game is ISwipeGame && !deployOnly;}}
        void SendSwipe(string stage,DateTime now) {
            double angle=random.NextDouble()*Math.PI*2,distance=120+random.NextDouble()*80;
            double x=.42+random.NextDouble()*.16,y=.40+random.NextDouble()*.16,dx=Math.Cos(angle)*distance/Vision.W,dy=Math.Sin(angle)*distance/Vision.H;
            ((ISwipeGame)game).Swipe(new Vec(x-dx/2,y-dy/2),new Vec(x+dx/2,y+dy/2),random.Next(180,321));
            log(stage+" swipe sent.");
        }
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
            log("Configured action order complete. Waiting for result or cutoff.");battleSwipes=SwipesEnabled;swipeDue=battleSwipes?now.AddMilliseconds(random.Next(2000,5001)):DateTime.MinValue;Go("Result",now);next=now.AddMilliseconds(delay);
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
        public void Start(DateTime now) { Active=true;zoomPending=true; Completed=0;SkippedBases=0;RecoveryAttempt=0;battlePending=false;skipHomeSwipes=false;battleSwipes=false;homeSwipesRemaining=0;ResetLoot();Message=null;battleDeadline=DateTime.MinValue;scannedNavigation=false; Go(deployOnly ? "Scouting" : "Home",now); }
        public void ResumeExistingBattle(DateTime now,int completed,bool searching=false) {
            if(completed<0 || completed>=limit) throw new Exception("Invalid completed battle count for recovery.");
            Active=true;zoomPending=true;Completed=completed;SkippedBases=0;RecoveryAttempt=0;battlePending=!searching;skipHomeSwipes=false;battleSwipes=false;homeSwipesRemaining=0;ResetLoot();Message=null;battleDeadline=DateTime.MinValue;scannedNavigation=false;Go(searching?"Recover search":"Recover result",now);
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
        public void Stop(string reason) { if (Active) log(reason); Active=false;battleSwipes=false;homeSwipesRemaining=0; State="Stopped"; Message=reason; }
        void Go(string state,DateTime now) {if(state=="Ending attack" || state=="Exit battle" || state=="Return home")battleSwipes=false;State=state; entered=now; next=now.AddMilliseconds(600); stable=0; last=null; log("Waiting: "+state+" ("+Completed+"/"+limit+")"); }
        bool Ready(string name,out Vec point) {
            point=game.Find(name);
            if (point==null) { stable=0; last=null; return false; }
            if (last!=null && (name=="find" || Math.Abs(point.X-last.X)<.015 && Math.Abs(point.Y-last.Y)<.015)) stable++; else stable=1;
            last=point; return stable>=2;
        }
        void OpenFromHome(Vec detected,DateTime now) {
            if(zoomPending && game is IZoomGame){BeginZoom("Home",now);return;}
            game.Click(settings.FastNavigation && !scannedNavigation ? settings.HomeBattleButton : detected);
            Go(settings.FastNavigation && !scannedNavigation ? "Fast find" : "Find match",now);
            next=now.AddMilliseconds(random.Next(900,1301));
        }
        void BeginFallback(DateTime now) {
            zoomPending=true;
            log("Fast navigation did not reach scouting. Returning to home for a scanned retry.");
            scannedNavigation=true;ResetLoot();Go("Navigation recovery",now);
        }
        void BeginZoom(string scene,DateTime now) {
            zoomScene=scene;zoomSteps=0;zoomProgress=now;Go("Zooming out",now);
            log("Preparing maximum zoom-out before deployment (3 steps). This is repeated only after recovery or a new Start.");
        }
        void ZoomTick(DateTime now,Stopwatch clock) {
            using(NavigationObservation observation=((IZoomGame)game).ObserveZoomScene()) {
                DateTime observedAt=now.AddMilliseconds(Math.Ceiling(clock.Elapsed.TotalMilliseconds));
                if(observation.Kind!=zoomScene) {
                    // Never send a pinch to an active battle or a blocking dialog.
                    if(observation.Kind=="Battle" || observation.Kind=="Battle exit"){Go(observation.Kind=="Battle"?"Result":"Exit battle",observedAt);return;}
                    if(observation.Kind=="Results"){Go(battlePending?"Result":"Search return",observedAt);return;}
                    if(observation.Kind=="Home" || observation.Kind=="Scouting"){Go(observation.Kind,observedAt);return;}
                    if((observedAt-zoomProgress).TotalSeconds>=8) {
                        if(game is IRecoveryGame)BeginHomeRecovery(observedAt);
                        else Stop("Stopped: could not verify a clear battlefield for zooming. Close dialogs and start again.");
                        return;
                    }
                    next=observedAt.AddMilliseconds(300);return;
                }
                if(zoomSteps==ZoomStepCount) {
                    zoomPending=false;ResetLoot();log("Zoom-out preparation complete. Rechecking the screen before navigation/deployment.");Go(zoomScene,observedAt);return;
                }
                game.Guard();((IZoomGame)game).ZoomOutStep();zoomSteps++;
                zoomProgress=now.AddMilliseconds(Math.Ceiling(clock.Elapsed.TotalMilliseconds));
                log("Zoom-out step "+zoomSteps+"/"+ZoomStepCount+" sent.");next=zoomProgress.AddMilliseconds(350);
            }
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
                if(State=="Zooming out"){ZoomTick(now,tickClock);return;}
                if(!deployOnly && game is IRecoveryGame) {
                    if(HomeNavigation(State) && (now-entered).TotalSeconds>=8)BeginHomeRecovery(now);
                    if(!Active)return;
                    if(State=="Home recovery"){RecoveryTick(now,tickClock);return;}
                    if(HomeState(State)){HomeTick(now,tickClock);return;}
                }
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
                    scannedNavigation=false;RecoveryAttempt=0;skipHomeSwipes=false;
                    if(zoomPending && game is IZoomGame){BeginZoom("Scouting",now);return;}
                    if(settings.FilterLoot && !AcceptLoot(point,now)) return;
                    Vec troopCard=settings.GooglePlay ? null : game.FirstTroopCard();troopCardPoint=troopCard;
                    if (!settings.GooglePlay && troopCard==null) { Stop("Stopped: first troop card is not available. Load troops in the first slot before starting."); return; }
                    spellsAvailable=settings.UseSpells && settings.Spells>0 && (settings.GooglePlay || game.SpellVisible());
                    if(settings.UseSpells && settings.Spells>0 && !spellsAvailable) log("Haste card not recognized before deployment. Spells will be skipped for this battle.");
                    bool reverse=random.Next(2)==1;reverseAttack=reverse;spellsCast=0;spellHistory.Clear();
                    placementSettings=game is IPlacementGame?((IPlacementGame)game).PlacementSettings(settings):settings;
                    PlacementPlan troopLayout=Plan.Troops(placementSettings,reverse,random);plan=troopLayout.Points;deployed=0;
                    battlePending=true;
                    SupportPlan support=Plan.Support(placementSettings,reverse,random);supportPoint=SupportPlan.Midpoint(support.Siege);heroPoints=support.Heroes.Select(SupportPlan.Midpoint).ToArray();
                    if(SupportEnabled)log("Support sides: "+String.Join(", ",support.Heroes.Select((side,i)=>HeroKeys[i]+"="+side.Name))+"; siege="+support.Siege.Name+".");
                    heroIndex=0;abilityIndex=0;actionIndex=0;battleSwipes=false;homeSwipesRemaining=0;pendingAbilities.Clear();troopsFinished=DateTime.MinValue;battleDeadline=DateTime.MinValue;
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
                    openingLayout=Plan.Opening(placementSettings,reverseAttack,random);openingSpells=openingLayout.Points;
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
                            Vec target=Plan.LaterSpell(placementSettings,reverseAttack,troopsFinished==DateTime.MinValue?0:(now-troopsFinished).TotalSeconds,random,spellHistory);
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
                    bool finished=Ready("return",out point);if(point!=null)battleSwipes=false;
                    if(finished) { game.Click(point); Go("Return home",now); }
                    if(State=="Result") {
                        if(battleSwipes && now>=swipeDue) {
                            if(game.Find("retreat")!=null && game.Find("surrenderTitle")==null)SendSwipe("Battle",now);
                            swipeDue=now.AddMilliseconds(Math.Ceiling(tickClock.Elapsed.TotalMilliseconds)+random.Next(2000,5001));
                        }
                        next=battleSwipes && swipeDue<now.AddMilliseconds(2500)?swipeDue:now.AddMilliseconds(2500);return;
                    }
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
                    CountReturnedBattle();
                    if(SwipesEnabled){homeSwipesRemaining=random.Next(1,3);Go("Home swipes",now);next=now.AddMilliseconds(random.Next(1000,2001));return;}
                    else if (Completed>=limit) Stop("Finished "+Completed+" battles. Stopped at home.");
                    else { ResetLoot();battleDeadline=DateTime.MinValue;scannedNavigation=false;OpenFromHome(point,now);return; }
                }
                else if(State=="Home swipes") {
                    if(game.Find("attack")==null){next=now.AddMilliseconds(500);return;}
                    SendSwipe("Home",now);homeSwipesRemaining--;
                    if(homeSwipesRemaining==0){
                        if(Completed>=limit)Stop("Finished "+Completed+" battles. Stopped at home.");
                        else {ResetLoot();battleDeadline=DateTime.MinValue;scannedNavigation=false;Go("Home",now);next=now.AddMilliseconds(300);}
                    }else next=now.AddMilliseconds(Math.Ceiling(tickClock.Elapsed.TotalMilliseconds)+random.Next(1000,2001));
                    return;
                }
                if (State!="Deploying" && State!="Spells" && State!="Opening spells" && State!="Select spells") next=now.AddMilliseconds(650);
            } catch (Exception ex) { Stop("Stopped: "+ex.Message); }
        }
    }
}
