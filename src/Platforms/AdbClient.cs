using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Windows.Forms;

// ADB discovery, device connection, screenshots, and Android input transport.
namespace ClashPekkas {
    public sealed class AdbClient {
        readonly string executable,serial;
        string zoomHelper;
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
        public void Swipe(int x1,int y1,int x2,int y2,int durationMs) {
            if(x1<0 || y1<0 || x2<0 || y2<0 || durationMs<100 || durationMs>1000)throw new Exception("Invalid Android swipe.");
            Text("shell input swipe "+x1+" "+y1+" "+x2+" "+y2+" "+durationMs);
        }
        public static int AndroidKeyCode(Keys key) {
            switch(key){case Keys.D1:return 8;case Keys.A:return 29;case Keys.Z:return 54;case Keys.Q:return 45;case Keys.W:return 51;case Keys.E:return 33;case Keys.R:return 46;default:throw new Exception("Unsupported Android selection key.");}
        }
        public void Key(Keys key) {Text("shell input keyevent "+AndroidKeyCode(key));}
        public void Back(){Text("shell input keyevent 4");}
        public void ZoomOut(string helperPath,int width,int height) {
            if(width<600 || height<300 || width>16384 || height>16384)throw new Exception("Invalid Android zoom display size.");
            if(zoomHelper==null) {
                if(!File.Exists(helperPath))throw new Exception("The bundled assets/android-zoom.dex helper is missing. Restore it before starting.");
                string hash;using(var sha=SHA256.Create())using(var stream=File.OpenRead(helperPath))hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                string remote="/data/local/tmp/clash-farmer-zoom-"+hash+".dex";
                string existing=Text("shell if [ -f "+remote+" ]; then sha256sum "+remote+"; fi").Trim();
                if(!existing.StartsWith(hash+" ",StringComparison.Ordinal)) {
                    // The shell process reads only our bundled, checksum-verified helper.
                    Run("-s "+serial+" push \""+helperPath+"\" "+remote,6000);
                    Text("shell chmod 444 "+remote);
                    if(!Text("shell sha256sum "+remote).StartsWith(hash+" ",StringComparison.Ordinal))throw new Exception("Android zoom helper checksum did not match.");
                }
                zoomHelper=remote;
            }
            string result=Text("shell CLASSPATH="+zoomHelper+" app_process / clashfarmer.input.ZoomInput "+width+" "+height,6000);
            if(result.Trim()!="ZOOM_OUT_SENT")throw new Exception("Android did not confirm the zoom-out gesture. No deployment started.");
        }
    }
}
