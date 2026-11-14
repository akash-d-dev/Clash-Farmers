using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

// Shared screenshot-backed recognition and placement adapter.
namespace ClashPekkas {
    public abstract class CapturedGame : ICapturedGame,IPlacementGame {
        readonly Bitmap spellReference; protected readonly ScreenReader reader;
        readonly Action<string> placementLog;
        protected CapturedGame(OcrReader ocr,string referencePath,Action<string> log) {
            placementLog=log;
            reader=new ScreenReader(ocr,Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(referencePath)),"data"),log);
            string spellPath=Path.Combine(Path.GetDirectoryName(referencePath),"spell-face.png");
            if(File.Exists(spellPath)) spellReference=new Bitmap(spellPath);
        }
        public abstract void Guard();
        public abstract Bitmap Capture();
        public abstract void Click(Vec point);
        public virtual void PressKey(Keys key) { throw new NotSupportedException("Keyboard selection is only available for Google Play Games desktop control."); }
        protected virtual bool GoogleLayout { get { return false; } }
        protected virtual bool DeveloperLayout {get{return false;}}
        public Vec Find(string button) { using(Bitmap image=Capture()) return reader.Find(image,button); }
        public NavigationObservation ObserveZoomScene(){using(Bitmap image=Capture())return reader.ObserveNavigation(image);}
        public Settings PlacementSettings(Settings settings){using(Bitmap image=Capture())return BattlefieldRim.Adapt(settings,image,placementLog);}
        public LootReading ReadLoot(bool gold=true,bool elixir=true,bool darkElixir=false) { using(Bitmap image=Capture()) return reader.ReadLoot(image,GoogleLayout,gold,elixir,darkElixir,DeveloperLayout); }
        public Vec FirstTroopCard() {using(Bitmap image=Capture()) return reader.FirstTroopCard(image);}
        public bool SpellVisible() {
            if(spellReference==null) return false;
            using(Bitmap image=Capture()) using(Bitmap face=image.Clone(Vision.SpellFace,PixelFormat.Format32bppArgb)) return Vision.Similar(face,spellReference);
        }
        public void Dispose() { if(spellReference!=null) spellReference.Dispose(); }
    }
}
