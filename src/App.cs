using System;
using System.Windows.Forms;

// Shared application bootstrap and host callbacks.
namespace ClashPekkas {
    public delegate string OcrReader(byte[] png);
    public delegate IntPtr PlayerPicker();
    public delegate void PlayerActivator(IntPtr window);
    public static class App {
        public static void Run(string folder,OcrReader ocr,int resumeBattles=0,int completed=0,bool resumeSearch=false) {
            Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var form=new MainForm(folder,ocr);
            if(resumeBattles>0) form.Shown+=delegate { form.ResumeExistingBattle(resumeBattles,completed,resumeSearch); };
            Application.Run(form);
        }
    }
}
