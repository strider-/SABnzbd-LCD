using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.ComponentModel;

namespace SABnzbd_LCD {
    class Program {
        const string API_QUEUE = "http://localhost:8080/sabnzbd/api?mode=queue&output=json&apikey=";
        const string COM_PORT = "COM3";
        const double INTERVAL = 500;

        static bool hadData = false;
        static LCD lcd;
        static string apikey;

        static void Main(string[] args) {
            string[] ports = System.IO.Ports.SerialPort.GetPortNames();

            apikey = System.Xml.Linq.XDocument.Load("ApiKey.xml").Element("apikey").Value;

            Timer timer = new Timer(INTERVAL);
            timer.Elapsed += updateLCD;
            timer.Start();

            lcd = new LCD(COM_PORT);
            lcd.StopMarquee();
            lcd.FormFeed();

            while(true)
                ;

            lcd.Dispose();
        }

        static void updateLCD(object sender, ElapsedEventArgs e) {
            Timer timer = (sender as Timer);
            timer.Stop();

            if(LCD.IsPortAvailable(COM_PORT)) {
                try {
                    string raw = new System.Net.WebClient().DownloadString(API_QUEUE + apikey);
                    dynamic json = JsonDocument.Parse(raw);

                    lcd.Wrap(false);
                    lcd.HideCursor();
                    lcd.SetContrast(60);

                    if(json.queue.slots.Count > 0) {
                        if(!hadData)
                            lcd.FormFeed();
                        hadData = true;
                        lcd.SetBacklight(60);

                        raw = new System.Net.WebClient().DownloadString(API_QUEUE);
                        json = JsonDocument.Parse(raw);
                        lcd.WriteText(0, 0, "{0,-10}{1,10}", json.queue.speed + "/s", json.queue.sizeleft);

                        if(json.queue.noofslots > 0) {
                            var slot = json.queue.slots[0];
                            string filename = slot.filename;

                            lcd.WriteText(0, 1, "{0,-2}: {1,-16}", json.queue.noofslots, json.queue.status);

                            //if(filename.Length > 20)
                                //filename = slot.filename.Substring(0, 17) + "...";
                            
                            lcd.WriteText(0, 2, "\x00fa");
                            lcd.WriteText(19, 2, "\x00fc");
                            lcd.ShowGraph(LCD.LCDGraphType.MediumCenter, 2, 1, 18, (float.Parse(slot.percentage) / 100f));
                            //lcd.WriteText(0, 4, "{0,-20}", filename);
                            lcd.Marquee(filename, 3, 2);
                        }
                    } else {
                        if(hadData)
                            lcd.FormFeed();
                        hadData = false;
                        lcd.SetBacklight(0);

                        lcd.WriteText(0, 0, "SABnzbd    v{0,-8}", json.queue.version);
                        lcd.WriteText(0, 1, "Uptime:    {0,-9}", json.queue.uptime);
                        lcd.WriteText(0, 2, "Warnings:  {0,-9}", json.queue.have_warnings);
                        lcd.WriteText(0, 3, "Avail(GB): {0,-9}", json.queue.diskspace2);

                    }
                } catch {
                    // whateva, just leave & try again next interval
                }
            }

            timer.Start();
        }
    }
}
