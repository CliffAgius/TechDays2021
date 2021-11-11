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
            // Configure the SD card, this is for the ESP32CAM board other ESP Boards look at the nanoFramework Docs as this line is different depending on the board.
            // GitHub Repo for the sample - https://github.com/nanoframework/Samples/blob/main/samples/System.IO.FileSystem/MountExample/Program.cs
            mycard = new SDCard(new SDCard.SDCardMmcParameters { dataWidth = SDCard.SDDataWidth._4_bit, enableCardDetectPin = false });

            // Some devices are a little slow when booted so give it time to sort itself out...
            Thread.Sleep(3000);     // Wait until the Storage Devices are mounted (SD Card & USB). This usally takes some seconds after startup.

            try
            {
                mycard.Mount();
                Debug.WriteLine("Card Mounted");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Card failed to mount : {ex.Message}");
            }

            if (string.IsNullOrEmpty(FlightDataFilePath)) //Generally only "should" happen on initialization.
            {
                // list all removable drives this is just to show that the device has more logical drives than just the SD card.
                var removableDrives = Directory.GetLogicalDrives();
                foreach (var drive in removableDrives)
                {
                    Debug.WriteLine($"Found logical drive {drive}");
                }
                // The SD Card is always the D drive so we will force that here...
                FlightDataFilePath = "D:\\";    
            }
        }

        /// <summary>
        /// Read the new chunk of Flight Data JSON from the SD card in the correct order...
        /// We are Chunking the data into chunks of 50 position reports as the memory on the ESP32-CAM is not big enough to hold the full flight...
        /// </summary>
        /// <returns>A FlightDataModel Array...</returns>
        public FlightDataModel[] GetFlightData()
        {
            if (!string.IsNullOrEmpty(FlightDataFilePath))
            {
                Debug.WriteLine("Reading storage...");

                // Build a filename for the next JSON chunk file
                string file = $"{FlightDataFilePath}\\{FileCount++}.json";

                // Check that the file Exists as when we reach the last File the above line will continue to increment and we may try to read a file that is not on the SD Card...
                if (File.Exists(file))
                {
                    Debug.WriteLine($"Found file: {file}");
                    using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        // Using the FileStream deserialize the JSON Chunk in the file into the FlightDataModel...
                        FlightDataModel[] config = (FlightDataModel[])JsonConvert.DeserializeObject(fs, typeof(FlightDataModel[]));
                        return config;
                    }
                }
            }
            return null;
        }
    }
}
