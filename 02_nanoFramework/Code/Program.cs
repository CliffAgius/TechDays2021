using nanoFramework.Azure.Devices.Client;
using nanoFramework.Azure.Devices.Provisioning.Client;
using nanoFramework.Azure.Devices.Shared;
using nanoFramework.Json;
using nanoFramework.Networking;
using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace TechDays2021
{
    public class Program
    {
        // *** Notes from the nanoFrameWork Team ***
        // *  Browse our samples repository: https://github.com/nanoframework/samples
        // *  Check our documentation online: https://docs.nanoframework.net/
        // *  Join our lively Discord community: https://discord.gg/gCyBu8T.
        // *****************************************

        // Azure DPS/IoTHub settings...
        public static string RegistrationID = "nanoFramework-01";   //TempName will be replaced by the Flight number from the JSON packets.
        const string DpsAddress = "global.azure-devices-provisioning.net";
        const string IdScope = "0ne00426F38";
        const string SasKey = "266pldCRiFGxSXkt6QcCPkqfCf8FMFIvD6yqpi+6Jy0=";

        // Device details...
        public static DeviceClient DeviceClient;
        public static Twin DeviceTwin;

        // Model Data...
        private static FlightDataStore flightDataStore = new();
        private static FlightDataModel[] FlightDataModel;
        private static int counter = 0;
        private static int Warning = 0;

        public static void Main()
        {
            // Connect the ESP32 Device to the Wifi and check the connection...
            Debug.WriteLine("Waiting for network up and IP address...");

            // Check if there is any stored Wifi COnfiguration...
            if (!NetworkHelper.IsConfigurationStored())
            {
                Debug.WriteLine("No configuration stored in the device");
            }
            else
            {
                // The wifi credentials are already stored on the device
                // Give 60 seconds to the wifi join to happen
                CancellationTokenSource cs = new(60000);
                var success = NetworkHelper.ReconnectWifi(setDateTime: true, token: cs.Token);
                if (!success)
                {
                    // Something went wrong, you can get details with the ConnectionError property:
                    Debug.WriteLine($"Can't connect to the network, error: {NetworkHelper.ConnectionError.Error}");
                    if (NetworkHelper.ConnectionError.Exception != null)
                    {
                        Debug.WriteLine($"ex: { NetworkHelper.ConnectionError.Exception}");
                        throw NetworkHelper.ConnectionError.Exception;
                    }
                }
                // Otherwise, you are connected and have a valid IP and date
                Debug.WriteLine($"YAY! Connected to Wifi...");
            }

            // Get the JSON Data from the SD card File...
            FlightDataModel = flightDataStore.GetFlightData();

            if (FlightDataModel == null || FlightDataModel.Length == 0)
            {
                Debug.WriteLine($"-- JSON Data missing... --");
            }
            else
            {
                // Connect to DPS...
                ConnectWithDPS();

                //Add the events for C2D messages.
                DeviceClient.CloudToDeviceMessage += DeviceClient_CloudToDeviceMessage;
                DeviceClient.TwinUpated += DeviceClient_TwinUpated;
                DeviceClient.AddMethodCallback(MethodCallbackStart);

                // launch worker thread where the real work is done!
                new Thread(WorkerThread).Start();
            }

            // Should never reach here but it's to stop the device crashing if we reach the end of the program...
            Thread.Sleep(Timeout.Infinite);
        }

        private static bool ConnectWithDPS()
        {
            // Grab the X509 Azure connection Cert from the resources folder...
            X509Certificate azureCA = new X509Certificate(Resources.GetBytes(Resources.BinaryResources.BaltimoreRootCA_crt));
            // Create the Provisioning client ready for the device - Ensure your SasKey derived Device ID and RegistartionID match otherwise this line will cause an exception...
            var provisioning = ProvisioningDeviceClient.Create(DpsAddress, IdScope, RegistrationID, SasKey, azureCA);
            // Register the device with DPS and thus the assigned IoTHub...
            var myDeviceRegistration = provisioning.Register(new CancellationTokenSource(60000).Token);

            //Check that the registration worked and we have a valid IoTHub assigned...
            if (myDeviceRegistration.Status != ProvisioningRegistrationStatusType.Assigned)
            {
                Debug.WriteLine($"Registration is not assigned: {myDeviceRegistration.Status}, error message: {myDeviceRegistration.ErrorMessage}");
                throw new OperationCanceledException();
            }

            // Write out all the IoTHub details that were assigned by DPS...
            Debug.WriteLine($"Device successfully assigned:");
            Debug.WriteLine($"  Assigned Hub: {myDeviceRegistration.AssignedHub}");
            Debug.WriteLine($"  Created time: {myDeviceRegistration.CreatedDateTimeUtc}");
            Debug.WriteLine($"  Device ID: {myDeviceRegistration.DeviceId}");
            Debug.WriteLine($"  Error code: {myDeviceRegistration.ErrorCode}");
            Debug.WriteLine($"  Error message: {myDeviceRegistration.ErrorMessage}");
            Debug.WriteLine($"  ETAG: {myDeviceRegistration.Etag}");
            Debug.WriteLine($"  Generation ID: {myDeviceRegistration.GenerationId}");
            Debug.WriteLine($"  Last update: {myDeviceRegistration.LastUpdatedDateTimeUtc}");
            Debug.WriteLine($"  Status: {myDeviceRegistration.Status}");
            Debug.WriteLine($"  Sub Status: {myDeviceRegistration.Substatus}");

            // Now we have a valid IoTHub Connection create the device on the IoTHub...
            DeviceClient = new DeviceClient(myDeviceRegistration.AssignedHub, myDeviceRegistration.DeviceId, SasKey, nanoFramework.M2Mqtt.Messages.MqttQoSLevel.AtMostOnce, azureCA);
            // Open the IoTHub connection and check it has worked...
            if (!DeviceClient.Open())
            {
                Debug.WriteLine($"can't open the device");
                DeviceClient.Close();
                return false;
            }

            // Grab the Device Twins that have been set by the DPS and IoTHub for this device...
            DeviceTwin = DeviceClient.GetTwin(new CancellationTokenSource(15000).Token);
            Debug.WriteLine($"Twin DeviceID: {DeviceTwin.DeviceId}, #desired: {DeviceTwin.Properties.Desired.Count}, #reported: {DeviceTwin.Properties.Reported.Count}");

            // Update the Device Twins with information for this Device...
            TwinCollection reported = new TwinCollection();
            reported.Add("firmware", "nanoFramework");
            reported.Add("sdk", "1.7.1-preview.1102");
            DeviceClient.UpdateReportedProperties(reported);

            return true;
        }

        public static string MethodCallbackStart(int rid, string payload)
        {
            return "Flight is closed and requesting pushback...";
        }

        private static void WorkerThread()
        {
            try
            {
                string messagePayload = "";

                while (true)
                {
                    // Check if there are any warning active and if so fake the error...
                    if (Warning != 0)
                    {
                        switch (Warning)
                        {
                            case (int)Emergency.AltitudeError:
                                FlightDataModel[counter].altitude = 40000;
                                break;
                            case (int)Emergency.VerticalSpeedDive:
                                FlightDataModel[counter].vSFPM = -6000;
                                break;
                            case (int)Emergency.IceCrystalIcingWarning:
                                FlightDataModel[counter].outsideAirTemp = 0;
                                break;
                            default:
                                break;
                        }
                    }

                    // Serialize the Current FlightDataModel into JSON to send as the mssage payload...
                    FlightDataModel[counter].directionString = ConvertDegreesToCompass(FlightDataModel[counter].direction);
                    FlightDataModel[counter].windDirectionString = ConvertDegreesToCompass(FlightDataModel[counter].windDirection);
                    messagePayload = JsonConvert.SerializeObject(FlightDataModel[counter]);

                    // Send message to IoTHub...
                    DeviceClient.SendMessage(messagePayload, new CancellationTokenSource(2000).Token);

                    // Data sent to log what was sent...
                    Debug.WriteLine($"Message Payload - {messagePayload}");
                    Debug.WriteLine($"*** DATA SENT - Next packet will be sent in {FlightDataModel[counter].secondsNextReport} seconds ***");

                    // Wait before sending the next position update..
                    Thread.Sleep(TimeSpan.FromSeconds(FlightDataModel[counter].secondsNextReport));
                    counter++;

                    // Check if we need to grab the next JSON Chunk from the SD card.
                    if (counter >= FlightDataModel.Length)
                    {
                        // All the current data chunk has been used grab the next...
                        FlightDataModel = flightDataStore.GetFlightData();
                        // Reset the Array counter...
                        counter = 0;

                        if (FlightDataModel is null)
                        {
                            FlightFinished();
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Oh Dear bad things happened...
                Debug.WriteLine($"-- D2C Error - {ex.Message} --");
            }
        }

        private static void FlightFinished()
        {
            Debug.WriteLine("The flight has finished...");
            //Reset the File Counter ready to go again...
            flightDataStore.FileCount = 1;
        }

        // Cloud to Device (C2D) message has been recieved and needs to be processed...
        private static void DeviceClient_CloudToDeviceMessage(object sender, CloudToDeviceMessageEventArgs e)
        {
            Debug.WriteLine($"*** C2D Message arrived: {e.Message} ***");

            // Switch on the message sent in so that we know what data to fake as part of the warning...
            switch (e.Message)
            {
                case "AltitudeError":
                    Warning = (int)Emergency.AltitudeError;
                    break;
                case "VerticalSpeedDive":
                    Warning = (int)Emergency.VerticalSpeedDive;
                    break;
                case "IceCrystalIcingWarning":
                    Warning = (int)Emergency.IceCrystalIcingWarning;
                    break;
                default:
                    Warning = (int)Emergency.None;
                    break;
            }
        }

        private static void DeviceClient_TwinUpated(object sender, nanoFramework.Azure.Devices.Shared.TwinUpdateEventArgs e)
        {
            // Device Twins have been updated we are not doing anything with them but you can in here, all we are doing is logging them out to the console...
            if (e.Twin != null)
            {
                Debug.WriteLine($"Got twins");
                Debug.WriteLine($"  {e.Twin.ToJson()}");
            }
        }

        private static string ConvertDegreesToCompass(double degrees)
        {
            // For the PowerBI it can't convert the numerical value into a pointer so we are showing a String value of the headings and this snippet does that conversion...
            var val = (int)Math.Floor((degrees / 22.5) + 0.5);
            var arr = new string[] { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };

            return arr[(val % 16)];
        }

        // Types of Emergency Warning that can be sent from the Cloud to the Device.
        enum Emergency
        {
            None,
            AltitudeError,          // Shift the Aircraft to an out of limits Altitude...
            VerticalSpeedDive,      // Make the Aircraft dive at 6000ft per minute as if it was in an emergency decent...
            IceCrystalIcingWarning  // Make the Outside Air Temp read 0 degrees which at Cruise Altitude is an indication of Ice Crystal Icing and a real danger.

            // Add your own for fun in here...
        }
    }
}
