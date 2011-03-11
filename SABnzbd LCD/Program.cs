using System.IO;
using System.Timers;
using System;

namespace SABnzbd_LCD {
    class Program {
        static SABnzbdLCD slcd;

        static void Main(string[] args) {
            slcd = new SABnzbdLCD("COM3", 500);
            slcd.Start();
            
            while(Console.ReadKey(true).Key != ConsoleKey.Escape)
                ;
            
            slcd.Stop();
        }
    }
}
