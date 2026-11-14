using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// Background Android control with connection guards and recovery inputs.
namespace ClashPekkas {
    public sealed class AndroidGame : CapturedGame,ISupportGame,ISwipeGame,IRecoveryGame,IZoomGame {
        readonly bool google;protected override bool GoogleLayout {get{return google;}}
        protected override bool DeveloperLayout {get{return google;}}
        readonly AdbClient client;readonly IntPtr window;Size resolution;DateTime checkedApp;
        readonly string recoveryDirectory;readonly Action<string> report;
        readonly string zoomHelperPath;
        public AndroidGame(IntPtr window,Settings settings,OcrReader ocr,string referencePath,Action<string> log) : base(ocr,referencePath,log) {
            this.window=window;google=settings.GooglePlay;client=AdbClient.Connect(settings);
            recoveryDirectory=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(referencePath)),"data","diagnostics","recovery");report=log;
            zoomHelperPath=Path.Combine(Path.GetDirectoryName(referencePath),"android-zoom.dex");
            if(log!=null) log("Android connected: "+client.Serial+". Desktop focus is not required.");
        }
        public override void Guard() {
            if(Native.StopRequested()) throw new Exception("Emergency stop (F9).");
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
        public NavigationObservation ObserveNavigation(){using(Bitmap image=Capture())return reader.ObserveNavigation(image);}
        public void Back(){Guard();client.Back();}
        public void SaveRecovery(NavigationObservation observation,RecoveryRecord record) {
            try{RecoveryDiagnostics.Save(recoveryDirectory,observation,record);}
            catch(IOException ex){if(report!=null)report("Could not save recovery diagnostics: "+ex.Message);}
            catch(UnauthorizedAccessException ex){if(report!=null)report("Could not save recovery diagnostics: "+ex.Message);}
            catch(ExternalException ex){if(report!=null)report("Could not save recovery screenshot: "+ex.Message);}
        }
        public bool TrySelectSupport(Keys key) {if(!google)return false;PressKey(key);return true;}
        public void ZoomOutStep() {
            Guard();if(resolution==Size.Empty)throw new Exception("Read the Android screen before zooming.");
            client.ZoomOut(zoomHelperPath,resolution.Width,resolution.Height);
        }
        public void Swipe(Vec from,Vec to,int durationMs) {
            Guard();if(resolution==Size.Empty)throw new Exception("Read the Android screen before swiping.");
            foreach(Vec point in new[]{from,to})if(point==null || Double.IsNaN(point.X) || Double.IsNaN(point.Y) || point.X<.25 || point.X>.75 || point.Y<.25 || point.Y>.70)throw new Exception("Swipe must stay in the central battlefield away from buttons.");
            client.Swipe((int)Math.Round(from.X*(resolution.Width-1)),(int)Math.Round(from.Y*(resolution.Height-1)),(int)Math.Round(to.X*(resolution.Width-1)),(int)Math.Round(to.Y*(resolution.Height-1)),durationMs);
        }
    }
}
