using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Web.Script.Serialization;

// Settings controls, run lifecycle, status, logs, and troubleshooting UI.
namespace ClashPekkas {
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
        readonly CheckBox randomSwipes=new CheckBox();
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
            stopButton.Name="stopButton";stopButton.Text="Stop (F9)";stopButton.Location=new Point(398,287);stopButton.Size=new Size(180,45);
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
            randomSwipes.Name="randomSwipes";randomSwipes.Text="Random swipes (ADB)";randomSwipes.AutoSize=true;randomSwipes.Location=new Point(280,588);randomSwipes.Checked=settings.RandomSwipes;Controls.Add(randomSwipes);
            Write("Ready. Start from your main village. "+connectionHint.Text);
            Write("Use the first troop card only. Deployment, troop/spell counts, duration, and loot targets are configurable above. Unreadable loot after three attempts attacks the current base.");
            foreach(var input in new[]{troopCount,spellCount,durationSeconds,goldMinimum,elixirMinimum,darkMinimum,battleCount,openingDepth,laterDepth,fourSidesDepth}) input.ValueChanged+=delegate {if(!IsBusy) Safely(SaveInputs);};
            deploymentSelector.SelectedIndexChanged+=delegate {if(!IsBusy) Safely(SaveInputs);};
            supportUnits.CheckedChanged+=delegate {if(!IsBusy) Safely(SaveInputs);};
            randomSwipes.CheckedChanged+=delegate {if(!IsBusy) Safely(SaveInputs);};
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
            nextSettings.RandomSwipes=randomSwipes.Checked;
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
            randomSwipes.Enabled=!IsBusy && AdbClient.Enabled(settings);
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
            if(state=="Home recovery")return "Recovering home";
            if(state=="Home" || state=="Home swipes") return "On home";
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
            else if(requested>0) nextTitle=ProgressTitle(countdown!=DateTime.MinValue?"Starting":runner!=null && runner.Active?(runner.State=="Home recovery"?"Recovering home - attempt "+runner.RecoveryAttempt+"/3":TaskbarActivity(runner.State)):titleState,runner==null?0:runner.Completed,requested);
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
            if(settings.RandomSwipes && useBackground)Write("Random swipes enabled: 2-5 seconds after deployment finishes, then 1-2 home swipes 1-2 seconds apart.");
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
            if(Native.StopRequested()) { if(IsBusy) Finish("Stopped with F9.",false);return; }
            if(countdown!=DateTime.MinValue) {
                status.Text="Starting in "+Math.Max(0,(int)Math.Ceiling((countdown-DateTime.Now).TotalSeconds))+"s. "+(useBackground ? "Using the local Android connection." : "Keep "+settings.PlayerName+" active.");
                if(DateTime.Now<countdown) return;
                try {
                    game=useBackground ? (ICapturedGame)new AndroidGame(target,settings,ocr,Path.Combine(folder,"assets","pekka-face.png"),Write) : new DesktopGame(target,settings,ocr,Path.Combine(folder,"assets","pekka-face.png"),Write);game.Guard();
                    if(positionsOnly) {
                        using(Bitmap image=game.Capture()) { PlacementPreview.Draw(image,settings);string path=Path.Combine(folder,"data","last-preview.png");image.Save(path,ImageFormat.Png);ShowImage(new Bitmap(image)); }
                        Finish("Troop positions shown. No game clicks sent. This step is optional.",false);return;
                    }
                    runner=new Runner(game,settings,requested,false,Write,new Random());runner.Start(DateTime.Now);
                    countdown=DateTime.MinValue;SyncControls();
                } catch(Exception ex) { Finish(ex.Message,false);return; }
            }
            if(runner!=null&&runner.Active) {
                runner.Tick(DateTime.Now);
                if(!runner.Active) Finish(runner.Message??"Run stopped.",true);
                else status.Text=(runner.State=="Home recovery"?"Recovering home - attempt "+runner.RecoveryAttempt+"/3":runner.State=="Scouting" && settings.FilterLoot ? "Checking enabled loot targets; unreadable after 3 attempts attacks this base..." : FriendlyState(runner.State))+"\nCompleted "+runner.Completed+" of "+requested+" battles. F9 stops the run.";
            }
        }
        static string FriendlyState(string state) {
            if(state.StartsWith("Fast ")) return "Opening the next battle using saved button positions...";
            if(state=="Navigation recovery") return "Recovering home for a scanned navigation retry...";
            if(state=="Exit battle" || state=="Ending attack") return "Ending the timed attack and confirming Okay...";
            if(state=="Home") return "Looking for Battle on your main village...";
            if(state=="Home swipes") return "Sending one or two home swipes before continuing...";
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
}
