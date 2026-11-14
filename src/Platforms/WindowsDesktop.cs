using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// Windows window discovery, viewport/focus guards, and desktop input.
namespace ClashPekkas {
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
        public static bool StopRequested() { return StopRequested(GetAsyncKeyState); }
        public static bool StopRequested(Func<int,short> readKeyState) { return (readKeyState((int)Keys.F9)&0x8001)!=0; }
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
            if (StopRequested() || GetForegroundWindow()!=window) throw new Exception("Stop key pressed or the game lost focus.");
            Pt p=new Pt{X=view.X+(int)Math.Round(point.X*view.Width),Y=view.Y+(int)Math.Round(point.Y*view.Height)};
            if (!view.Contains(p.X,p.Y) || GetAncestor(WindowFromPoint(p),2)!=window) throw new Exception("Click blocked: another window covers the game.");
            if (!SetCursorPos(p.X,p.Y)) throw new Exception("Could not position the mouse.");
            if (StopRequested()) throw new Exception("Emergency stop (F9).");
            var down=new Input{Type=0,Data=new InputData{Mouse=new MouseInput{Flags=2}}};
            var up=new Input{Type=0,Data=new InputData{Mouse=new MouseInput{Flags=4}}};
            if (SendInput(1,new[]{down},Marshal.SizeOf(typeof(Input)))!=1) throw new Exception("Mouse input failed.");
            try { System.Threading.Thread.Sleep(35); }
            finally { if(SendInput(1,new[]{up},Marshal.SizeOf(typeof(Input)))!=1) throw new Exception("Mouse release failed. Run stopped."); }
        }
        public static void PressKey(IntPtr window,Keys key) {
            if(key!=Keys.D1 && key!=Keys.A && key!=Keys.Z && key!=Keys.Q && key!=Keys.W && key!=Keys.E && key!=Keys.R) throw new Exception("Unsupported selection key.");
            if(StopRequested() || window==IntPtr.Zero || GetForegroundWindow()!=window) throw new Exception("Stop key pressed or the game lost focus.");
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
            if (Native.StopRequested()) throw new Exception("Emergency stop (F9).");
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
    public static class PlayerWindows {
        public static bool IsMain(string title,int width,int height,bool owned) {
            return !owned && width>=600 && height>=400 &&
                !String.IsNullOrWhiteSpace(title) &&
                title.IndexOf("BlueStacks",StringComparison.OrdinalIgnoreCase)>=0 &&
                title.IndexOf("App Player",StringComparison.OrdinalIgnoreCase)>=0;
        }
    }
}
