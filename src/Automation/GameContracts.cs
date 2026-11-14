using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// Shared values and platform capabilities consumed by the runner.
namespace ClashPekkas {
    public sealed class Vec {
        public double X, Y;
        public Vec() { }
        public Vec(double x, double y) { X=x; Y=y; }
    }
    public sealed class LootReading {
        public long Gold,Elixir,DarkElixir;
        public LootReading(long gold,long elixir,long darkElixir=0) { Gold=gold;Elixir=elixir;DarkElixir=darkElixir; }
    }
    public interface IGame {
        void Guard();
        Vec Find(string button);
        Vec FirstTroopCard();
        bool SpellVisible();
        LootReading ReadLoot(bool gold=true,bool elixir=true,bool darkElixir=false);
        void Click(Vec point);
        void PressKey(Keys key);
    }
    public interface ICapturedGame : IGame,IDisposable { Bitmap Capture(); }
    public interface ISupportGame { bool TrySelectSupport(Keys key); }
    public interface ISwipeGame {void Swipe(Vec from,Vec to,int durationMs);}
    public interface IPlacementGame {Settings PlacementSettings(Settings settings);}
    public interface IZoomGame {NavigationObservation ObserveZoomScene();void ZoomOutStep();}
    public interface IRecoveryGame {
        NavigationObservation ObserveNavigation();
        void Back();
        void SaveRecovery(NavigationObservation observation,RecoveryRecord record);
    }
    public sealed class NavigationObservation : IDisposable {
        public string Kind="Unknown";
        public Vec Home,Cancel,Return,Reload;
        public OcrLine[] Ocr=new OcrLine[0];
        public readonly Dictionary<string,OcrLine[]> Crops=new Dictionary<string,OcrLine[]>();
        public Bitmap Frame;
        public void Dispose(){if(Frame!=null){Frame.Dispose();Frame=null;}}
    }
    public sealed class RecoveryRecord {
        public string Reason,Origin,State;
        public int Attempt,Completed,Total,SkippedBases,BackInputs;
        public bool BattlePending;
    }
}
