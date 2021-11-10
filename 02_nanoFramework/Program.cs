using Amqp;
using nanoFramework.Azure.Devices.Client;
using nanoFramework.Azure.Devices.Provisioning.Client;
using nanoFramework.Azure.Devices.Shared;
using nanoFramework.Json;
using nanoFramework.Networking;
using nanoFramework.Runtime.Native;
using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using AmqpTrace = Amqp.Trace;

namespace TechDays2021
{
    public class Program
    {
        // Browse our samples repository: https://github.com/nanoframework/samples
        // Check our documentation online: https://docs.nanoframework.net/
        // Join our lively Discord community: https://discord.gg/gCyBu8T.

        // Azure DPS/IoTHub settings
        public static string RegistrationID = "nanoFramework-01";   //TempName will be replaced by the Flight number from the JSON packets.
        const string DpsAddress = "global.azure-devices-provisioning.net";
        const string IdScope = "0ne00426F38";
        const string SasKey = "266pldCRiFGxSXkt6QcCPkqfCf8FMFIvD6yqpi+6Jy0=";

        public static DeviceClient DeviceClient;
        public static Twin DeviceTwin;

        // Model Data
        private static FlightDataStore flightDataStore = new();
        private static FlightDataModel[] FlightDataModel;
        private static int counter = 0;
        private static int Warning = 0;

        public static void Main()
        {
            // Connect the ESP32 Device to the Wifi and check the connection...
            Debug.WriteLine("Waiting for network up and IP address...");

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
                //Update the RegistrationID with the Flight CallSign before we connect to DPS.
                RegistrationID = FlightDataModel[0].callSign;

                // Connect to DPS...
                ConnectWithDPS();

                //Add the events for C2D messages.
                DeviceClient.CloudToDeviceMessage += DeviceClient_CloudToDeviceMessage;
                DeviceClient.TwinUpated += DeviceClient_TwinUpated;
                DeviceClient.AddMethodCallback(MethodCallbackStart);

                // launch worker thread where the real work is done!
                new Thread(WorkerThread).Start();
            }

            Thread.Sleep(Timeout.Infinite);
        }

        private static bool ConnectWithDPS()
        {
            X509Certificate azureCA = new X509Certificate(Resources.GetBytes(Resources.BinaryResources.BaltimoreRootCA_crt));
            var provisioning = ProvisioningDeviceClient.Create(DpsAddress, IdScope, RegistrationID, SasKey, azureCA);
            var myDeviceRegistration = provisioning.Register(new CancellationTokenSource(60000).Token);

            if (myDeviceRegistration.Status != ProvisioningRegistrationStatusType.Assigned)
            {
                Debug.WriteLine($"Registration is not assigned: {myDeviceRegistration.Status}, error message: {myDeviceRegistration.ErrorMessage}");
                return false;
            }

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

            // You can then create the device
            DeviceClient = new DeviceClient(myDeviceRegistration.AssignedHub, myDeviceRegistration.DeviceId, SasKey, nanoFramework.M2Mqtt.Messages.MqttQoSLevel.AtMostOnce, azureCA);
            // Open it and continue like for the previous sections
            if (!DeviceClient.Open())
            {
                Debug.WriteLine($"can't open the device");
                DeviceClient.Close();
                return false;
            }

            DeviceTwin = DeviceClient.GetTwin(new CancellationTokenSource(15000).Token);

            Debug.WriteLine($"Twin DeviceID: {DeviceTwin.DeviceId}, #desired: {DeviceTwin.Properties.Desired.Count}, #reported: {DeviceTwin.Properties.Reported.Count}");

            TwinCollection reported = new TwinCollection();
            reported.Add("firmware", "myNano");
            reported.Add("sdk", 0.2);
            DeviceClient.UpdateReportedProperties(reported);

            return true;
        }

        string MethodCallbackStart(int rid, string payload)
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
                                FlightDataModel[counter].altitude = 50000;
                                break;
                            case (int)Emergency.VerticalSpeedDive:
                                FlightDataModel[counter].vSFPM = -6000;
                                break;
                            case (int)Emergency.IceCrystalIcingWarning:
                                FlightDataModel[counter].outSideAirTemp = 0;
                                break;
                            default:
                                break;
                        }                        
                    }

                    // Serialize the Current FlightDataModel into JSON to send as the mssage payload...
                    messagePayload = JsonConvert.SerializeObject(FlightDataModel[counter]);


                    // Send message to IoTHub...
                    DeviceClient.SendMessage(messagePayload, new CancellationTokenSource(2000).Token);

                    // data sent
                    Debug.WriteLine($"*** DATA SENT - Next packet will be sent in {FlightDataModel[counter].secondsNextReport} seconds ***");

                    // Wait before sending the next position update..
                    Thread.Sleep(FlightDataModel[counter].secondsNextReport);
                    counter++;

                    if (FlightDataModel.Length >= counter)
                    {
                        // All the current data chunk has been used grab the next...
                        FlightDataModel = flightDataStore.GetFlightData();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"-- D2C Error - {ex.Message} --");
            }
        }

        // Cloud to Device (C2D) message has been recieved and needs to be processed...
        private static void DeviceClient_CloudToDeviceMessage(object sender, CloudToDeviceMessageEventArgs e)
        {
            Debug.WriteLine($"*** C2D Message arrived: {e.Message} ***");

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
                    break;
            }
        }

        private static void DeviceClient_TwinUpated(object sender, nanoFramework.Azure.Devices.Shared.TwinUpdateEventArgs e)
        {
            if (e.Twin != null)
            {
                Debug.WriteLine($"Got twins");
                Debug.WriteLine($"  {e.Twin.ToJson()}");
            }
        }

        // Types of Emergency Warning that can be sent from the Cloud to the Device.
        enum Emergency
        {
            None,
            AltitudeError,          // Shift the Aircraft to 50,000ft...
            VerticalSpeedDive,      // Make the Aircraft dive at 6000ft per minute as if it was in an emergency decent...
            IceCrystalIcingWarning  // Make the Outside Air Temp read 0 degrees which at Cruise Altitude is an indication of Ice Crystal Icing and a real danger.
        }
    }
}
