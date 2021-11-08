using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading;
using nanoFramework.System.IO.FileSystem;
using System;
using nanoFramework.Json;

namespace TechDays2021
{
    public class FlightDataStore
    {
        //private StorageFolder flightDataFolder { get; set; }
        private string flightDataFilePath { get; set; }
        static SDCard mycard;


        public FlightDataStore(string path = "")
        {
            //Store the path value.
            flightDataFilePath = path;
        }

        public FlightDataModel[] GetConfig()
        {
            //D:\\FlightData.json

            mycard = new SDCard(new SDCard.SDCardMmcParameters { dataWidth = SDCard.SDDataWidth._4_bit, enableCardDetectPin = false });

            Thread.Sleep(3000);     // Wait until the Storage Devices are mounted (SD Card & USB). This usally takes some seconds after startup.

            try
            {
                mycard.Mount();
                Debug.WriteLine("Card Mounted");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Card failed to mount : {ex.Message}");
                Debug.WriteLine($"IsMounted {mycard.IsMounted}");
            }

            if (string.IsNullOrEmpty(flightDataFilePath)) //Generally only "should" happen on initialization.
            {
                // list all removable drives
                var removableDrives = Directory.GetLogicalDrives();
                foreach (var drive in removableDrives)
                {
                    Debug.WriteLine($"Found logical drive {drive}");
                    //path = drive; 
                }
                //TODO: techincally we should handle more than one drive... but for the moment, we are just going to handle the first one!
                flightDataFilePath = "D:\\"; //removableDrives[0];
            }

            if (!string.IsNullOrEmpty(flightDataFilePath))
            {
                Debug.WriteLine("Reading storage...");

                // get files on the root of the 1st removable device
                var filesInDevice = Directory.GetFiles(flightDataFilePath);

                //TODO: in certain cases it would be helpful to support File.ReadAllText -- https://zetcode.com/csharp/file/ (Helper lib??)
                foreach (var file in filesInDevice)
                {
                    Debug.WriteLine($"Found file: {file}");
                    //TODO: we should really check if certs are in the mcu flash before retreiving them from the filesystem (SD).
                    if (file.Contains("2.json"))
                    {
                        using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                        {
                            //var buffer = new byte[fs.Length];
                            //fs.Read(buffer, 0, (int)fs.Length);
                            //string json = Encoding.UTF8.GetString(buffer, 0, buffer.Length);

                            FlightDataModel[] config = (FlightDataModel[])JsonConvert.DeserializeObject(fs, typeof(FlightDataModel[]));
                            return config;

                        }
                        //Should load into secure storage (somewhere) and delete file on removable device?
                    }
                }
            }
            return null;
        }
    }
}
