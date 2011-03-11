using System.IO;
using System.Timers;

namespace SABnzbd_LCD {
    class Program {
        static SABnzbdLCD slcd;

        static void Main(string[] args) {
            slcd = new SABnzbdLCD("COM3", 500);
            slcd.Start();

            while(true)
                ;
            
            slcd.Stop();
        }
    }
}
