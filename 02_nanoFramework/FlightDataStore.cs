using nanoFramework.Json;
using Windows.Storage;

namespace TechDays2021
{
    public class FlightDataStore
    {
        private StorageFolder flightDataFolder { get; set; }
        private string flightDataFilePath { get; set; }


        public FlightDataStore(string path = "I:\\FlightData.json")
        {
            flightDataFilePath = path;
        }

        public bool ClearConfig()
        {
            return WriteConfig(new FlightDataModel());
        }

        public FlightDataModel[] GetConfig()
        {
            var InternalDevices = Windows.Storage.KnownFolders.InternalDevices;
            var flashDevices = InternalDevices.GetFolders();
            var configFolder = flashDevices[0];

            var configFile = StorageFile.GetFileFromPath(flightDataFilePath);

            string json = FileIO.ReadText(configFile);
            FlightDataModel[] config = (FlightDataModel[])JsonConvert.DeserializeObject(json, typeof(FlightDataModel));
            return config;
        }
        public bool WriteConfig(FlightDataModel config)
        {
            try
            {
                var configJson = JsonConvert.SerializeObject(config);
                StorageFile configFile = flightDataFolder.CreateFile("FlightData.json", CreationCollisionOption.ReplaceExisting);
                FileIO.WriteText(configFile, configJson);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
