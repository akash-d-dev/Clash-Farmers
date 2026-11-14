using System;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Local data paths, first-run settings, and retained recovery diagnostics.
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
    public static class RecoveryDiagnostics {
        public static void Save(string directory,NavigationObservation observation,RecoveryRecord record) {
            Directory.CreateDirectory(directory);
            string prefix=Path.Combine(directory,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
            if(observation.Frame!=null)observation.Frame.Save(prefix+".png",ImageFormat.Png);
            File.WriteAllText(prefix+".json",new JavaScriptSerializer().Serialize(new{Context=record,Observation=observation.Kind,Ocr=observation.Ocr,Crops=observation.Crops}));
            foreach(string path in Directory.GetFiles(directory,"*.json").Where(path=>Regex.IsMatch(Path.GetFileName(path),@"^\d{8}-\d{6}-\d{3}-[a-f0-9]{8}\.json$")).OrderByDescending(path=>Path.GetFileName(path),StringComparer.Ordinal).Skip(20)) {
                // Only prune this method's own timestamped records, within its dedicated directory.
                if(!Regex.IsMatch(Path.GetFileName(path),@"^\d{8}-\d{6}-\d{3}-[a-f0-9]{8}\.json$"))continue;
                File.Delete(path);string png=Path.ChangeExtension(path,".png");if(File.Exists(png))File.Delete(png);
            }
        }
    }
}
