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

        // Set-up Wifi Credentials so we can connect to the web.
        private static string Ssid = "<ENTER YOUR WIFI SSID";
        private static string WifiPassword = "<ENTER YOUR WIFI PASSOWRD>";

        // Azure IoTHub settings
        const string _hubName = "<ENTER YOUR IOTHub NAME>";
        const string _deviceId = "TechDays2021-Device1";    // <- Give your device a meaningful name...
        const string _sasToken = "<ENTER YOUR SAS TOKEN>";  // <- See blog post on how to obtain your SAS token.
        
        // AMQP Tracing.
        static bool TraceOn = false;

        public static void Main()
        {
            // Connect the ESP32 Device to the Wifi and check the connection...

            Debug.WriteLine("Waiting for network up and IP address...");
            bool success = false;
            CancellationTokenSource cs = new(60000);

            success = NetworkHelper.ConnectWifiDhcp(Ssid, WifiPassword, setDateTime: true, token: cs.Token);

            if (!success)
            {
                Debug.WriteLine($"Can't get a proper IP address and DateTime, error: {NetworkHelper.ConnectionError.Error}.");
                if (NetworkHelper.ConnectionError.Exception != null)
                {
                    Debug.WriteLine($"Exception: {NetworkHelper.ConnectionError.Exception}");
                }
                return;
            }
            else
            {
                Debug.WriteLine($"YAY! Connected to Wifi - {Ssid}");
            }

            // Setup AMQP
            // Set trace level 
            AmqpTrace.TraceLevel = TraceLevel.Frame | TraceLevel.Information;
            // Enable tracing
            AmqpTrace.TraceListener = WriteTrace;
            Connection.DisableServerCertValidation = false;

            // launch worker thread where the real work is done!
            new Thread(WorkerThread).Start();

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

                    string messagePayload = $"{{\"Latitude\":{Latitude},\"Longitude\":{Longitude}}}";

                    // compose message
                    Message message = new Message(Encoding.UTF8.GetBytes(messagePayload));
                    message.ApplicationProperties = new Amqp.Framing.ApplicationProperties();

                    // send message with the new Lat/Lon
                    sender.Send(message, null, null);

                    // data sent
                    Debug.WriteLine($"*** DATA SENT - Lat - {Latitude}, Lon - {Longitude} ***");

                    // update the location data
                   

                    // wait before sending the next position update
                    Thread.Sleep(5000);
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
                Double.TryParse((string)message.ApplicationProperties["setlat"], out Latitude);
                Double.TryParse((string)message.ApplicationProperties["setlon"], out Longitude);
                Debug.WriteLine($"== Received new Location setting: Lat - {Latitude}, Lon - {Longitude} ==");
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
