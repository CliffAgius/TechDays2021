using Amqp;
using nanoFramework.Networking;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using AmqpTrace = Amqp.Trace;

namespace TechDays2021
{
    public class Program
    {
        // Browse our samples repository: https://github.com/nanoframework/samples
        // Check our documentation online: https://docs.nanoframework.net/
        // Join our lively Discord community: https://discord.gg/gCyBu8T

        // Azure IoTHub settings
        const string _hubName = "TechDays2021";
        const string _deviceId = "TechDays2021-Device1";    // <- Give your device a meaningful name...
        const string _sasToken = "SharedAccessSignature sr=TechDays2021.azure-devices.net%2Fdevices%2FTechDays2021-Device1&sig=YnPUPjpCBUlPO1rqBFzSn2cOUhOEn%2FF1l0YlKtRD%2FZA%3D&se=1635172297";  // <- See blog post on how to obtain your SAS token.
        
        // AMQP Tracing.
        static bool TraceOn = false;

        // Model Data
        private static FlightDataModel[] FlightDataModel;
        private static int counter = 0;

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

            // Setup AMQP
            // Set trace level 
            AmqpTrace.TraceLevel = TraceLevel.Frame | TraceLevel.Information;
            // Enable tracing
            AmqpTrace.TraceListener = WriteTrace;
            Connection.DisableServerCertValidation = false;

            // Get the JSON Data from the SD card File...
            FlightDataStore flightDataStore = new();
            FlightDataModel = flightDataStore.GetConfig();
            
            if (FlightDataModel == null || FlightDataModel.Length == 0)
            {
                Debug.WriteLine($"-- JSON Data missing... --");
            }
            else
            {
                // launch worker thread where the real work is done!
                new Thread(WorkerThread).Start();
            }

            Thread.Sleep(Timeout.Infinite);
        }

        private static void WorkerThread()
        {
            try
            {
                // parse Azure IoT Hub Map settings to AMQP protocol settings
                string hostName = _hubName + ".azure-devices.net";
                string userName = _deviceId + "@sas." + _hubName;
                string senderAddress = "devices/" + _deviceId + "/messages/events";
                string receiverAddress = "devices/" + _deviceId + "/messages/deviceBound";

                Connection connection = new Connection(new Address(hostName, 5671, userName, _sasToken));
                Session session = new Session(connection);
                SenderLink sender = new SenderLink(session, "send-link", senderAddress);
                ReceiverLink receiver = new ReceiverLink(session, "receive-link", receiverAddress);
                receiver.Start(100, OnMessage);

                while (true)
                {
                    // Serialize the Current FlightDataModel into JSON to send as the mssage payload.
                    string messagePayload = ""; // JsonConvert.SerializeObject(FlightDataModel[counter]);

                    // compose message
                    Message message = new Message(Encoding.UTF8.GetBytes(messagePayload));
                    message.ApplicationProperties = new Amqp.Framing.ApplicationProperties();

                    // send message with the new Lat/Lon
                    sender.Send(message, null, null);

                    // data sent
                    Debug.WriteLine($"*** DATA SENT - Next packet will be sent in {FlightDataModel[counter].secondsNextReport} seconds ***");

                    // wait before sending the next position update
                    Thread.Sleep(FlightDataModel[counter].secondsNextReport);
                    counter++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"-- D2C Error - {ex.Message} --");
            }
        }

        private static void OnMessage(IReceiverLink receiver, Message message)
        {
            try
            {
                // command received 
                //Double.TryParse((string)message.ApplicationProperties["setlat"], out FlightDataModel.Latitude);
                //Double.TryParse((string)message.ApplicationProperties["setlon"], out FlightDataModel.Longitude);
                //Debug.WriteLine($"== Received new Location setting: Lat - {Latitude}, Lon - {Longitude} ==");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"-- C2D Error - {ex.Message} --");
            }
        }

        static void WriteTrace(TraceLevel level, string format, params object[] args)
        {
            if (TraceOn)
            {
                Debug.WriteLine(Fx.Format(format, args));
            }
        }
    }
}
