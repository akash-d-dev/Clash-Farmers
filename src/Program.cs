using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

[assembly: System.Reflection.AssemblyTitle("Clash Farmer")]
[assembly: System.Reflection.AssemblyProduct("Clash Farmer")]

namespace ClashPekkas {
    public static class Program {
        [STAThread]
        public static void Main(string[] args) {
            string folder=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".."));
            try {
                AppFiles.Initialize(folder);
                using(Runspace session=RunspaceFactory.CreateRunspace()) {
                    session.ApartmentState=ApartmentState.STA;session.ThreadOptions=PSThreadOptions.UseCurrentThread;session.Open();
                    using(PowerShell shell=PowerShell.Create()) {
                        shell.Runspace=session;
                        shell.AddScript("$ErrorActionPreference='Stop'; . '"+Path.Combine(folder,"scripts","Ocr.ps1").Replace("'","''")+"'");shell.Invoke();
                        Check(shell);
                        OcrReader reader=delegate(byte[] png) {
                            shell.Commands.Clear();shell.Streams.Error.Clear();
                            shell.AddCommand("Read-GameText").AddParameter("Png",png);
                            var result=shell.Invoke();Check(shell);
                            return String.Join("",result.Select(r=>r.ToString()));
                        };
                        int resume=0,completed=0;bool searching=false;
                        if(args.Length>0) {
                            if(args.Length<3 || args[0]!="--resume" || !Int32.TryParse(args[1],out resume) || !Int32.TryParse(args[2],out completed) || resume<1 || resume>30 || completed<0 || completed>=resume) throw new Exception("Invalid recovery arguments.");
                            searching=args.Length>3 && args[3]=="search";
                        }
                        App.Run(folder,reader,resume,completed,searching);
                    }
                }
            } catch(Exception ex) {
                File.WriteAllText(AppFiles.DataFile("last-error.txt"),ex.ToString());
                MessageBox.Show(ex.Message,"Clash Farmer - startup error");
            }
        }
        static void Check(PowerShell shell) {
            if(shell.HadErrors) throw new Exception(String.Join(Environment.NewLine,shell.Streams.Error.Select(e=>e.ToString())));
        }
    }
}
