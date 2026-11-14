using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

// Visual overlay for troop paths, opening spells, and later spells.
namespace ClashPekkas {
    public static class PlacementPreview {
        public static void Draw(Bitmap canvas,Settings settings) {
            settings=BattlefieldRim.Adapt(settings,canvas);
            using(Graphics g=Graphics.FromImage(canvas)) using(Font font=new Font("Segoe UI",12,FontStyle.Bold)) {
                g.SmoothingMode=SmoothingMode.AntiAlias;
                for (int flip=0;flip<(settings.FourSides?1:2);flip++) {
                    Color color=flip==0 ? Color.Cyan : Color.Magenta;
                    PlacementPlan layout=Plan.Troops(settings,flip==1,new Random(42));
                    using(Pen pen=new Pen(color,3)) using(Brush brush=new SolidBrush(color)) {
                        foreach(DeploymentSide side in layout.Sides) {
                            g.DrawLine(pen,(float)(side.A.X*Vision.W),(float)(side.A.Y*Vision.H),(float)(side.B.X*Vision.W),(float)(side.B.Y*Vision.H));
                        }
                        List<Vec> points=layout.Points;
                        for(int i=0;i<points.Count;i++) { float x=(float)(points[i].X*Vision.W),y=(float)(points[i].Y*Vision.H); g.FillEllipse(brush,x-4,y-4,8,8); g.DrawString((i+1).ToString(),font,brush,x+5,y-18); }
                        g.DrawString(layout.Summary,font,brush,240,flip==0?800:825);
                    }
                    if(settings.UseSpells && settings.Spells>0) {
                        var random=new Random(42);List<Vec> spells=Plan.OpeningSpells(settings,flip==1,random);
                        using(Pen openingPen=new Pen(Color.Yellow,3)) using(Pen laterPen=new Pen(Color.OrangeRed,3)) {
                            foreach(Vec p in spells) g.DrawEllipse(openingPen,(float)(p.X*Vision.W)-9,(float)(p.Y*Vision.H)-9,18,18);
                            if(!settings.FourSides) for(int i=spells.Count;i<settings.Spells;i++) {
                                Vec p=Plan.LaterSpell(settings,flip==1,0,random,spells);spells.Add(p);
                                g.DrawRectangle(laterPen,(float)(p.X*Vision.W)-8,(float)(p.Y*Vision.H)-8,16,16);
                            }
                        }
                    }
                }
                g.FillRectangle(Brushes.Black,200,850,1200,50);
                g.DrawString(settings.FourSides?"CYAN: troops, all four sides     PREVIEW ONLY":"CYAN: troops, normal L     MAGENTA: troops, reverse L     PREVIEW ONLY",font,Brushes.White,210,852);
                g.DrawString("YELLOW circles: opening spells     ORANGE squares: later spells     Depth: "+(settings.FourSides?settings.FourSidesSpellDepthPercent+"%":settings.LShapeOpeningDepthPercent+"% / "+settings.LShapeLaterDepthPercent+"%"),font,Brushes.White,210,875);
            }
        }
    }
}
