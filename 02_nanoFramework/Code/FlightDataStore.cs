using System.IO;
using System.Diagnostics;
using System.Threading;
using nanoFramework.System.IO.FileSystem;
using System;
using nanoFramework.Json;

namespace TechDays2021
{
    public class FlightDataStore
    {
        //private StorageFolder flightDataFolder { get; set; }
        private string FlightDataFilePath { get; set; }
        static SDCard mycard;
        public int FileCount { get; set; } = 1;

        public FlightDataStore(string path = "")
        {
            //Store the path value.
            FlightDataFilePath = path;
            InitSDCard();
        }

        public void InitSDCard()
        {
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

            if (string.IsNullOrEmpty(FlightDataFilePath)) //Generally only "should" happen on initialization.
            {
                // list all removable drives
                var removableDrives = Directory.GetLogicalDrives();
                foreach (var drive in removableDrives)
                {
                    Debug.WriteLine($"Found logical drive {drive}");
                }
                FlightDataFilePath = "D:\\"; 
            }
        }

        public FlightDataModel[] GetFlightData()
        {
            if (!string.IsNullOrEmpty(FlightDataFilePath))
            {
                Debug.WriteLine("Reading storage...");

                string file = $"{FlightDataFilePath}\\{FileCount++}.json";

                if (File.Exists(file))
                {
                    Debug.WriteLine($"Found file: {file}");
                    using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        FlightDataModel[] config = (FlightDataModel[])JsonConvert.DeserializeObject(fs, typeof(FlightDataModel[]));
                        return config;
                    }
                }
            }
            return null;
        }
    }
}
