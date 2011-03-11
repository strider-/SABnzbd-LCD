using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace SABnzbd_LCD {
    public class SABnzbdLCD {
        const string API_QUEUE = "http://localhost:8080/sabnzbd/api?mode=queue&output=json&apikey=";

        Timer timer;
        bool hadData = false;
        LCD lcd;
        string apikey = string.Empty,
               port;
        double interval;

        public SABnzbdLCD(string Port, double Interval) {
            port = Port;
            interval = Interval;
            FileInfo keyXml = new FileInfo(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "ApiKey.xml"));
            if(keyXml.Exists)
                apikey = System.Xml.Linq.XDocument.Load(keyXml.FullName).Element("apikey").Value;
        }

        public void Start() {
            timer = new Timer(interval);
            timer.Elapsed += updateLCD;

            lcd = new LCD(port);
            lcd.StopMarquee();
            lcd.FormFeed();

            timer.Start();
        }

        public void Stop() {
            timer.Stop();
            lcd.StopMarquee();
            lcd.FormFeed();
            lcd.Dispose();
        }

        void updateLCD(object sender, ElapsedEventArgs e) {
            Timer timer = (sender as Timer);
            timer.Stop();

            if(LCD.IsPortAvailable(port)) {
                try {
                    string raw = new System.Net.WebClient().DownloadString(API_QUEUE + apikey);
                    dynamic json = JsonDocument.Parse(raw);

                    lcd.Wrap(false);
                    lcd.HideCursor();
                    lcd.SetContrast(60);

                    if(json.status != null && json.status == false) {
                        lcd.WriteText(0, 0, json.error);
                        lcd.WriteText(0, 2, "Please correct &");
                        lcd.WriteText(0, 3, "restart.");
                    } else {
                        if(json.queue.slots.Count > 0) {
                            if(!hadData)
                                lcd.FormFeed();
                            hadData = true;
                            lcd.SetBacklight(60);

                            lcd.WriteText(0, 0, "{0,-10}{1,10}", json.queue.speed + "/s", json.queue.sizeleft);

                            if(json.queue.slots.Count > 0) {
                                var slot = json.queue.slots[0];
                                string filename = slot.filename;

                                lcd.WriteText(0, 1, "{0,-2}: {1,-16}", json.queue.noofslots, json.queue.status);

                                lcd.WriteText(0, 2, "\x00fa");
                                lcd.WriteText(19, 2, "\x00fc");
                                lcd.ShowGraph(LCD.LCDGraphType.MediumCenter, 2, 1, 18, (float.Parse(slot.percentage) / 100f));
                                lcd.Marquee(filename, 3, 2);
                            }
                        } else {
                            if(hadData)
                                lcd.FormFeed();
                            hadData = false;
                            lcd.SetBacklight(0);

                            lcd.StopMarquee();
                            lcd.WriteText(0, 0, "SABnzbd    v{0,-8}", json.queue.version);
                            lcd.WriteText(0, 1, "Uptime:    {0,-9}", json.queue.uptime);
                            lcd.WriteText(0, 2, "Warnings:  {0,-9}", json.queue.have_warnings);
                            lcd.WriteText(0, 3, "Avail(GB): {0,-9}", json.queue.diskspace2);

                        }
                    }
                } catch {
                    // whateva, just leave & try again next interval.
                    // maybe log these exceptions, but chances are the LCD is no longer available (unplugged?)
                }
            }

            timer.Start();
        }
    }
}
