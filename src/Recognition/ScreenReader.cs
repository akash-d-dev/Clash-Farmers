using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Screenshot OCR, navigation classification, and available-loot readings.
namespace ClashPekkas {
    public sealed class ScreenReader {
        readonly OcrReader ocr; readonly Action<string> log; readonly string folder;
        readonly HomeVisual homeVisual;
        readonly JavaScriptSerializer json=new JavaScriptSerializer();
        readonly Dictionary<string,DateTime> lastReport=new Dictionary<string,DateTime>();
        public ScreenReader(OcrReader ocr,string folder,Action<string> log,string assetFolder=null) {
            this.ocr=ocr;this.folder=folder;this.log=log;
            homeVisual=new HomeVisual(String.IsNullOrWhiteSpace(assetFolder)?Path.Combine(String.IsNullOrWhiteSpace(folder)?AppFiles.Root:Path.GetDirectoryName(folder),"assets"):assetFolder);
        }
        static Vec InRegion(OcrLine[] lines,string name) {
            Rectangle roi=Vision.Region(name);
            OcrLine[] local=lines.Where(line=>roi.Contains((int)(line.X+line.Width/2),(int)(line.Y+line.Height/2)) && !(name=="find" && Vision.Label("retreat",line.Text)))
                .Select(line=>new OcrLine{Text=line.Text,X=line.X-roi.X,Y=line.Y-roi.Y,Width=line.Width,Height=line.Height}).ToArray();
            return Vision.Locate(name,local,roi);
        }
        public NavigationObservation ObserveNavigation(Bitmap image) {
            // All classifications and fallback crops below use this exact captured frame.
            var result=new NavigationObservation{Frame=new Bitmap(image)};
            Func<string,Vec> crop=name=>Find(image,name,result.Crops);
            try {
                result.Ocr=ReadImage(image);
                if(InRegion(result.Ocr,"inactivityTitle")!=null){result.Kind="Inactivity dialog";result.Reload=InRegion(result.Ocr,"reload")??crop("reload");return result;}
                if(result.Ocr.Any(line=>line.Y>650 && line.X>450 && line.X<1150 && Vision.TextSimilarity(line.Text,"Loading")>=.8)){result.Kind="Loading";return result;}
                if(InRegion(result.Ocr,"exitTitle")!=null) {
                    result.Kind="Exit dialog";result.Cancel=InRegion(result.Ocr,"exitCancel")??crop("exitCancel");return result;
                }
                if(InRegion(result.Ocr,"surrenderTitle")!=null){result.Kind="Battle exit";return result;}
                if(InRegion(result.Ocr,"launch")!=null || InRegion(result.Ocr,"find")!=null){result.Kind="Menu";return result;}
                bool selection=result.Ocr.Any(line=>line.Y>680 && line.X>230 && line.X<1300 && Regex.IsMatch(Vision.Clean(line.Text),@"^(upgrade|info|move|remove|train|research)"));
                if(selection){result.Kind="Menu";return result;}
                Vec home=homeVisual.FindHome(image);
                if(home!=null){
                    result.Kind="Home";result.Home=home; // Stable button center even when text OCR alternates between success and failure.
                    DateTime previous;
                    if(InRegion(result.Ocr,"attack")==null && log!=null && (!lastReport.TryGetValue("visualHome",out previous) || (DateTime.Now-previous).TotalSeconds>=5)) {
                        lastReport["visualHome"]=DateTime.Now;log("Home recognized visually: Battle and Build/Shop indicators. Text OCR was not required.");
                    }
                    return result;
                }
                // Text is an independent recognition path, not gated on a Battle
                // template match. The bright independent HUD anchor rejects dimmed
                // home controls behind dialogs. All checks still use this one frame.
                Vec battleText=InRegion(result.Ocr,"attack")??crop("attack");
                if(battleText!=null && (home=homeVisual.FindHome(image,battleText))!=null) {
                    result.Kind="Home";result.Home=home;
                    if(log!=null)log("Home recognized by Battle text and a bright Build/Shop indicator.");
                    return result;
                }
                // Full-frame OCR can lose small labels; retry dialog crops before
                // classifying controls visible behind a modal.
                if(crop("inactivityTitle")!=null){result.Kind="Inactivity dialog";result.Reload=crop("reload");return result;}
                if(crop("exitTitle")!=null){result.Kind="Exit dialog";result.Cancel=crop("exitCancel");return result;}
                if(crop("surrenderTitle")!=null){result.Kind="Battle exit";return result;}
                if(InRegion(result.Ocr,"next")!=null || crop("next")!=null){result.Kind="Scouting";return result;}
                result.Return=InRegion(result.Ocr,"return")??crop("return");
                if(result.Return!=null){result.Kind="Results";return result;}
                if(InRegion(result.Ocr,"retreat")!=null || result.Ocr.Any(line=>line.Y<180 && line.X>500 && line.X<1100 && Vision.TextSimilarity(line.Text,"Battle ends in")>=.8) || crop("retreat")!=null) {result.Kind="Battle";return result;}
                if(crop("find")!=null || crop("launch")!=null)result.Kind="Menu";
                return result;
            }catch {result.Dispose();throw;}
        }
        OcrLine[] ReadImage(Bitmap image) {
            using(var stream=new MemoryStream()) { image.Save(stream,ImageFormat.Png);return json.Deserialize<OcrLine[]>(ocr(stream.ToArray()))??new OcrLine[0]; }
        }
        bool LootValue(Bitmap image,Rectangle region,string label,out long value,bool developer=false) {
            value=0;
            using(Bitmap crop=image.Clone(region,PixelFormat.Format32bppArgb)) {
                var votes=new List<long>();
                var attempts=new List<string>();
                foreach(int scale in new[]{2,4,3,5,1}) {
                    using(Bitmap enlarged=Vision.Resize(crop,crop.Width*scale,crop.Height*scale)) using(Bitmap mask=Vision.WhiteDigits(enlarged,developer)) {
                        int digits=developer?Vision.LootDigitCount(mask):0;
                        OcrLine[] lines;
                        if(developer) {
                            using(Bitmap padded=new Bitmap(mask.Width+40,mask.Height+20)) {
                                using(Graphics graphics=Graphics.FromImage(padded)){graphics.Clear(Color.White);graphics.DrawImageUnscaled(mask,20,10);}
                                lines=ReadImage(padded);
                            }
                        }else lines=ReadImage(mask);
                        var values=new List<long>();
                        attempts.Add(scale+"x: "+String.Join(" | ",lines.Select(l=>l.Text)));
                        foreach(OcrLine line in lines) { long parsed;if(Vision.TryLootNumber(line.Text,out parsed)) {
                            if(developer && (digits==0 || parsed.ToString(CultureInfo.InvariantCulture).Length!=digits)) {attempts.Add("visible digit count "+digits+" disagrees with "+parsed);continue;}
                            values.Add(parsed);
                        } }
                        values=values.Distinct().ToList();
                        if(values.Count==1) {
                            votes.Add(values[0]);
                            if(votes.Count(v=>v==values[0])>=2) { value=values[0];return true; }
                        }
                    }
                }
                if(log!=null) log("Loot OCR ["+label+"] did not agree across image scales; retrying. "+String.Join("; ",attempts));
                if(!String.IsNullOrWhiteSpace(folder)) {
                    try { string directory=Path.Combine(folder,"diagnostics");Directory.CreateDirectory(directory);crop.Save(Path.Combine(directory,"last-loot-"+label+".png"),ImageFormat.Png); } catch(IOException) { }
                }
            }
            return false;
        }
        public LootReading ReadLoot(Bitmap image,bool googlePlay=false,bool readGold=true,bool readElixir=true,bool readDark=false,bool developer=false) {
            long gold=0,elixir=0,dark=0;
            Rectangle goldRegion=googlePlay ? new Rectangle(65,94,220,32) : Vision.GoldLoot;
            Rectangle elixirRegion=googlePlay ? new Rectangle(65,133,235,32) : Vision.ElixirLoot;
            Rectangle darkRegion=googlePlay?new Rectangle(65,173,235,32):new Rectangle(65,191,235,43);
            if(developer){goldRegion=new Rectangle(57,94,158,26);elixirRegion=new Rectangle(57,133,158,26);darkRegion=new Rectangle(57,173,158,26);}
            bool goldOk=!readGold || LootValue(image,goldRegion,"gold",out gold,developer),elixirOk=!readElixir || LootValue(image,elixirRegion,"elixir",out elixir,developer),darkOk=!readDark || LootValue(image,darkRegion,"dark-elixir",out dark,developer);
            return goldOk && elixirOk && darkOk ? new LootReading(gold,elixir,dark) : null;
        }
        public Vec FirstTroopCard(Bitmap image) {
            Rectangle roi=new Rectangle(0,768,850,54);
            using(Bitmap crop=image.Clone(roi,PixelFormat.Format32bppArgb)) using(Bitmap large=Vision.Resize(crop,crop.Width*3,crop.Height*3)) {
                foreach(bool threshold in new[]{true,false}) {
                    using(Bitmap input=threshold?Vision.WhiteDigits(large):new Bitmap(large)) {
                        var candidates=new List<OcrLine>();
                        foreach(OcrLine line in ReadImage(input)) foreach(Match marker in Regex.Matches(line.Text??"",@"[xX×]\s*\d+")) {
                            double unit=line.Width/Math.Max(1,line.Text.Length);
                            candidates.Add(new OcrLine{Text=marker.Value,X=line.X+marker.Index*unit,Width=marker.Length*unit});
                        }
                        candidates=candidates.OrderBy(l=>l.X).ToList();
                        if(candidates.Count==0) continue;
                        OcrLine first=candidates[0];
                        // The stack marker is right-aligned within the first troop card.
                        double x=(first.X+first.Width/2)/3;
                        if(x<40 || x>800 || Regex.IsMatch(first.Text,@"[xX×]\s*0\s*$")) return null;
                        return new Vec((x-18)/Vision.W,.93);
                    }
                }
            }
            return null;
        }
        public Vec Find(Bitmap image,string button,Dictionary<string,OcrLine[]> observations=null) {
            Rectangle region=Vision.Region(button);
            using(Bitmap crop=image.Clone(region,PixelFormat.Format32bppArgb)) using(Bitmap enlarged=Vision.Resize(crop,crop.Width*2,crop.Height*2)) using(var stream=new MemoryStream()) {
                enlarged.Save(stream,ImageFormat.Png);
                string result=ocr(stream.ToArray());
                OcrLine[] lines=json.Deserialize<OcrLine[]>(result) ?? new OcrLine[0];
                foreach(OcrLine line in lines) { line.X/=2;line.Y/=2;line.Width/=2;line.Height/=2; }
                Vec found=Vision.Locate(button,lines,region);
                if((button=="next" || button=="attack" || button=="find" || button=="end" || button=="retreat" || button=="return" || button=="surrenderTitle" || button=="confirmSurrender" || button=="exitTitle" || button=="exitCancel" || button=="inactivityTitle" || button=="reload") && found==null) {
                    foreach(int scale in new[]{2,3}) {
                        using(Bitmap large=Vision.Resize(crop,crop.Width*scale,crop.Height*scale)) using(Bitmap mask=Vision.WhiteDigits(large)) {
                            OcrLine[] processed=ReadImage(mask);
                            foreach(OcrLine line in processed) { line.X/=scale;line.Y/=scale;line.Width/=scale;line.Height/=scale; }
                            Vec candidate=Vision.Locate(button,processed,region);
                            if(candidate!=null) { found=candidate;lines=processed;break; }
                        }
                    }
                }
                if(button=="find" && found==null) {
                    Rectangle costRegion=Vision.FindMatchCostRegion;
                    using(Bitmap costCrop=image.Clone(costRegion,PixelFormat.Format32bppArgb))foreach(int scale in new[]{2,3}) {
                        using(Bitmap large=Vision.Resize(costCrop,costCrop.Width*scale,costCrop.Height*scale)) {
                            OcrLine[] costLines=ReadImage(large);
                            foreach(OcrLine costLine in costLines){costLine.X/=scale;costLine.Y/=scale;costLine.Width/=scale;costLine.Height/=scale;}
                            if(observations!=null)observations["findCost"]=costLines;
                            Vec candidate=Vision.Locate("find",costLines,costRegion);
                            if(candidate!=null){found=candidate;lines=costLines;break;}
                        }
                    }
                }
                if(observations!=null)observations[button]=lines;
                DateTime previous;
                if(log!=null && (!lastReport.TryGetValue(button,out previous) || (DateTime.Now-previous).TotalSeconds>=5)) {
                    lastReport[button]=DateTime.Now;
                    string seen=String.Join(" | ",lines.Select(l=>l.Text));
                    string label=button=="attack" ? "home Battle" : button=="find" ? "Find a Match" : button=="launch" ? "army Attack" : button;
                    string evidence="";
                    if(button=="find" && found!=null) {
                        bool price=lines.Any(item=>{int value;return Vision.TryFindMatchCost(item.Text,out value);});
                        evidence=price && !lines.Any(item=>Vision.Label("find",item.Text))?" (coin cost 100-5,000)":" (text match at least 60%)";
                    }
                    log("Screen reader ["+label+"]: "+(found!=null ? "button found"+evidence : "not found")+"; text: "+(seen.Length==0 ? "(none)" : seen));
                    if(found==null && folder!=null) {
                        try {
                            string directory=Path.Combine(folder,"diagnostics");Directory.CreateDirectory(directory);
                            crop.Save(Path.Combine(directory,"last-"+button+".png"),ImageFormat.Png);
                            File.WriteAllText(Path.Combine(directory,"last-"+button+".json"),result);
                        } catch(IOException) { }
                    }
                }
                return found;
            }
        }
    }
}
