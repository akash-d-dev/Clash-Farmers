using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Editor for the deployment action sequence.
namespace ClashPekkas {
    public sealed class ActionOrderForm : Form {
        sealed class Entry {public string Id;public override string ToString(){return Settings.ActionLabel(Id);}}
        readonly ListBox list=new ListBox();readonly Button up=new Button(),down=new Button(),save=new Button();readonly Label error=new Label();
        public string[] SelectedOrder {get{return list.Items.Cast<Entry>().Select(item=>item.Id).ToArray();}}
        public ActionOrderForm(string[] order) {
            Text="Action order";ClientSize=new Size(460,325);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);
            Controls.Add(new Label{Text="Select an action, then move it up or down.",AutoSize=true,Location=new Point(18,18)});
            list.Name="actionOrderList";list.Location=new Point(18,53);list.Size=new Size(310,145);list.ItemHeight=24;Controls.Add(list);
            foreach(string id in order)list.Items.Add(new Entry{Id=id});list.SelectedIndex=0;
            up.Name="moveActionUp";up.Text="Move up";up.Location=new Point(340,65);up.Size=new Size(100,34);up.Click+=delegate{MoveSelected(-1);};Controls.Add(up);
            down.Name="moveActionDown";down.Text="Move down";down.Location=new Point(340,110);down.Size=new Size(100,34);down.Click+=delegate{MoveSelected(1);};Controls.Add(down);
            error.Name="orderValidation";error.ForeColor=Color.DarkRed;error.Location=new Point(18,207);error.Size=new Size(420,42);Controls.Add(error);
            save.Name="saveActionOrder";save.Text="Save";save.Location=new Point(230,270);save.Size=new Size(100,34);save.Click+=delegate{Settings.ValidateOrder(SelectedOrder);DialogResult=DialogResult.OK;};Controls.Add(save);AcceptButton=save;
            var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,Location=new Point(340,270),Size=new Size(100,34)};Controls.Add(cancel);CancelButton=cancel;
            var reset=new Button{Text="Reset default",Location=new Point(18,270),Size=new Size(120,34)};reset.Click+=delegate{list.Items.Clear();foreach(string id in new Settings().ActionOrder)list.Items.Add(new Entry{Id=id});list.SelectedIndex=0;RefreshOrder();};Controls.Add(reset);
            list.SelectedIndexChanged+=delegate{RefreshOrder();};RefreshOrder();
        }
        void MoveSelected(int direction) {
            int from=list.SelectedIndex,to=from+direction;if(from<0 || to<0 || to>=list.Items.Count)return;
            object item=list.Items[from];list.Items.RemoveAt(from);list.Items.Insert(to,item);list.SelectedIndex=to;RefreshOrder();
        }
        void RefreshOrder() {
            up.Enabled=list.SelectedIndex>0;down.Enabled=list.SelectedIndex>=0 && list.SelectedIndex<list.Items.Count-1;
            try{Settings.ValidateOrder(SelectedOrder);save.Enabled=true;error.Text="Hero abilities must follow Deploy heroes. Disabled actions are skipped.";error.ForeColor=SystemColors.GrayText;}
            catch(Exception ex){save.Enabled=false;error.Text=ex.Message;error.ForeColor=Color.DarkRed;}
        }
    }
}
