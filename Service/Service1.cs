using System.IO;
using System.ServiceProcess;
using System.Timers;
using SABnzbd_LCD;

namespace Service {
    public partial class Service1 : ServiceBase {
        SABnzbdLCD slcd;

        public Service1() {
            InitializeComponent();
            slcd = new SABnzbdLCD("COM3", 500);
        }

        protected override void OnStart(string[] args) {
            slcd.Start();
        }

        protected override void OnStop() {
            slcd.Stop();
        }
    }
}
