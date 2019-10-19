namespace IoTEdgeIcBridgeModule
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Newtonsoft.Json;
    using System.Net.Http;
    using Microsoft.Azure.Devices.Shared;

    class Program
    {
        private static string defaultUri = string.Empty;
        private static string defaultDeviceId = string.Empty;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

            // Attach callback for Twin desired properties updates
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, ioTHubModuleClient);

            // Execute callback method for Twin desired properties updates
            var twin = await ioTHubModuleClient.GetTwinAsync();
            await onDesiredPropertiesUpdate(twin.Properties.Desired, ioTHubModuleClient);

            await ioTHubModuleClient.OpenAsync();

            Console.WriteLine(@"");
            Console.WriteLine(@"     /$$$$$$      /$$$$$$  /$$    /$$ /$$$$$$$$ /$$       /$$$$$$$  /$$$$$$$$ ");
            Console.WriteLine(@"   /$$$__  $$$   /$$__  $$| $$   | $$| $$_____/| $$      | $$__  $$| $$_____/ ");
            Console.WriteLine(@"  /$$_/  \_  $$ | $$  \__/| $$   | $$| $$      | $$      | $$  \ $$| $$       ");
            Console.WriteLine(@" /$$/ /$$$$$  $$|  $$$$$$ |  $$ / $$/| $$$$$   | $$      | $$  | $$| $$$$$    ");
            Console.WriteLine(@"| $$ /$$  $$| $$ \____  $$ \  $$ $$/ | $$__/   | $$      | $$  | $$| $$__/    ");
            Console.WriteLine(@"| $$| $$\ $$| $$ /$$  \ $$  \  $$$/  | $$      | $$      | $$  | $$| $$       ");
            Console.WriteLine(@"| $$|  $$$$$$$$/|  $$$$$$/   \  $/   | $$$$$$$$| $$$$$$$$| $$$$$$$/| $$$$$$$$ ");
            Console.WriteLine(@"|  $$\________/  \______/     \_/    |________/|________/|_______/ |________/ ");
            Console.WriteLine(@" \  $$$   /$$$                                                                ");
            Console.WriteLine(@"  \_  $$$$$$_/                                                                ");
            Console.WriteLine(@"    \______/                                                                  ");
            Console.WriteLine("IoT Central Bridge module client initialized.");
            Console.WriteLine("MIT licensed by Sander van de Velde");

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);

            Console.WriteLine("Input 'input1' attached.");
            Console.WriteLine("Output 'Exception' attached.");

            // assign direct method handler 
            await ioTHubModuleClient.SetMethodHandlerAsync("inputMessage", InputMessageMethodCallBack, ioTHubModuleClient);

            Console.WriteLine("Method 'inputMessage' attached.");
        }

        public static string DeviceId { get; set; }

        public static string Uri { get; set; }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            var moduleClient = userContext as ModuleClient;
            
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            var messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);

            Console.WriteLine($"Received message:[{messageString}]");

            await SendToIoTCentralBridge(messageString, moduleClient);

            return MessageResponse.Completed;
        }

        static async Task<MethodResponse> InputMessageMethodCallBack(MethodRequest methodRequest, object userContext)
        {
            var moduleClient = userContext as ModuleClient;
            
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            var messageBytes = methodRequest.Data;
            var messageString = Encoding.UTF8.GetString(messageBytes);

            Console.WriteLine($"Received method InputMessageMethodCallBack:[{messageString}]");

            await SendToIoTCentralBridge(messageString, moduleClient);

            return new MethodResponse(200);
        }


        static async Task SendToIoTCentralBridge(string messageString, ModuleClient moduleClient)
        {           
            if (string.IsNullOrEmpty(DeviceId)
                    || (string.IsNullOrEmpty(Uri)))
            {
                await OutputErrorRouteMessage("-2", $"DeviceId and/or Uri is empty. Message '{messageString}' ignored.", moduleClient);   
            }

            if (!string.IsNullOrEmpty(messageString))
            {
                try
                {
                    var messageJson = JsonConvert.DeserializeObject(messageString);

                    BridgeRequest bridgeRequest = new BridgeRequest
                    {
                        Device = new BridgeDevice
                        {
                            DeviceId = DeviceId
                        },
                        Measurements = messageJson
                    };

                    var json = JsonConvert.SerializeObject(bridgeRequest);

                    Console.WriteLine($"Output: '{json}'");

                    var httpContent = new StringContent(json, Encoding.UTF8, "application/json"); 

                    using (var client = new HttpClient())
                    {
                        var response = await client.PostAsync(Uri, httpContent);

                        var responseContent = response.Content.ReadAsStringAsync();

                        Console.WriteLine($"IoT Central Response status: '{responseContent.Status}'; result= '{responseContent.Result}'");

                        if (!string.IsNullOrEmpty(responseContent.Result)
                                && responseContent.Result.Contains("Unable to register device"))
                        {
                            await OutputErrorRouteMessage(responseContent.Status.ToString(), responseContent.Result, moduleClient);
                        }

                    }
                }
                catch(Exception ex)
                {
                    await OutputErrorRouteMessage("-1", ex.Message, moduleClient);
                }				
            }
        }

        private static async Task OutputErrorRouteMessage(string status, string result, ModuleClient moduleClient)
        {
            var outputMessage = new OutputMessage
            {
                Status = status,
                Result = result,
            };

            var messageJson = JsonConvert.SerializeObject(outputMessage);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);

            using (var pipeMessage = new Message(messageBytes))
            {
                await moduleClient.SendEventAsync("Exception", pipeMessage);
                Console.WriteLine($"Received message '{status}','{result}' sent");
            }   
        }

        private static Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            if (desiredProperties.Count == 0)
            {
                return Task.CompletedTask;
            }

            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

                var client = userContext as ModuleClient;

                if (client == null)
                {
                    throw new InvalidOperationException($"UserContext doesn't contain expected ModuleClient");
                }

                var reportedProperties = new TwinCollection();

                if (desiredProperties.Contains("uri")) 
                {
                    if (desiredProperties["uri"] != null)
                    {
                        Uri = desiredProperties["uri"];
                    }
                    else
                    {
                        Uri = defaultUri;
                    }

                    Console.WriteLine($"Uri changed to '{Uri}'");

                    reportedProperties["uri"] = Uri;
                }

                if (desiredProperties.Contains("deviceId")) 
                {
                    if (desiredProperties["deviceId"] != null)
                    {
                        string deviceId = desiredProperties["deviceId"];

                        DeviceId = deviceId.ToLower().Trim();

                        if (DeviceId != deviceId)
                        {
                            Console.WriteLine($"DeviceId '{deviceId}' is changed into '{DeviceId}' to match IoT Central Bridge requirements.");
                        } 
                    }
                    else
                    {
                        DeviceId = defaultDeviceId;
                    }

                    Console.WriteLine($"DeviceId changed to '{DeviceId}'");

                    reportedProperties["deviceId"] = DeviceId;
                }

                if (reportedProperties.Count > 0)
                {
                    client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
                }
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }

            return Task.CompletedTask;
        }
    }

    public class OutputMessage
    {
        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }

        [JsonProperty(PropertyName = "result")]
        public string Result { get; set; }
    }

    public class BridgeRequest
    {
        [JsonProperty(PropertyName = "device")]
        public BridgeDevice Device { get; set; }

        [JsonProperty(PropertyName = "measurements")]
        public dynamic Measurements { get; set; }
    }

    public class BridgeDevice
    {
        [JsonProperty(PropertyName = "deviceId")]
        public string DeviceId { get; set; }
    }
}
