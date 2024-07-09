﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using System.Security.Authentication;
using System.Net.Http;
using System.Threading;
using System.Collections.Concurrent;

namespace soulspine.LCU //https://github.com/soulspine/LCU
{
    public class LeagueClient
    {
        // process 
        private int? lcuPort = null;
        private string lcuToken = null;
        private string rawLcuToken = null;
        private bool lcuProcessRunning;

        // config
        public bool autoReconnect { get; set; }

        // http and websocket
        private HttpClient client = new HttpClient(new HttpClientHandler()
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) =>
            {
                return true;
            }
        });
        private WebSocket socketConnection = null;
        ConcurrentDictionary<string, List<Action<OnWebsocketEventArgs>>> subscriptions = new ConcurrentDictionary<string, List<Action<OnWebsocketEventArgs>>>();

        //events
        public event Action OnConnected = null;
        public event Action OnDisconnected = null;

        public event Action OnLobbyEnter = null;
        public event Action OnLobbyLeave = null;
        public event Action OnChampSelectEnter = null;
        public event Action OnChampSelectLeave = null;
        public event Action OnGameEnter = null;
        public event Action OnGameLeave = null;

        // status
        private bool tryingToConnect = false;

        public bool isConnected { get; private set; } = false;
        public bool isInLobby { get; private set; } = false;
        public bool isInChampSelect { get; private set; } = false;
        public bool isInGame { get; private set; } = false;

        public string currentGameflowPhase { get; private set; }
        public Summoner localSummoner { get; private set; }
        public string localSummonerRegion { get; private set; }


        public LeagueClient(bool autoReconnect = true)
        {
            this.autoReconnect = autoReconnect;
        }

        //handles are the only things allowed to use .Result instead of await
        //just because I SAID SO

        public void Connect()
        {
            if (isConnected)
            {
                throw new InvalidOperationException("Tried to connect to LCU, but it is already connected.");
            }
            else if (tryingToConnect)
            {
                throw new InvalidOperationException("Tried invoking Connect() when there already was an ongoing attempt to connect. Perhaps try changing autoReconnect option when initializing a LeagueClient object.");
            }
            tryingToConnect = true;
            Task.Run(() => handleConnection());
        }

        /// <summary>
        /// Disconnects from the League Client.
        /// </summary>
        /// <exception cref="InvalidOperationException if not connected."></exception>
        public void Disconnect()
        {
            if (!isConnected)
            {
                throw new InvalidOperationException("Tried to disconnect from LCU, but it is not connected.");
            }
            handleDisconnection(true);
        }

        private void handleConnection()
        {
            RESTART:

            while (!(lcuProcessRunning = isProcessRunning("LeagueClientUx")))
            {
                Thread.Sleep(2000);
                lcuProcessRunning = isProcessRunning("LeagueClientUx");
            }

            List<(string, string)> leagueArgs = getProcessCmdArgs("LeagueClientUx");

            lcuPort = null;
            lcuToken = null;
            rawLcuToken = null;

            foreach ((string arg, string value) in leagueArgs)
            {
                if (arg == "app-port")
                {
                    lcuPort = int.Parse(value);
                }
                else if (arg == "remoting-auth-token")
                {
                    rawLcuToken = value;
                    lcuToken = base64Encode("riot:" + value);
                }

                if (lcuPort != null && lcuToken != null) break;
            }

            if (lcuPort == null || lcuToken == null)
            {
                lcuProcessRunning = false;
                goto RESTART;
            }

            bool? apiConnected = lcuApiReadyCheck().Result;

            while (true)
            {
                if (apiConnected == null) goto RESTART;
                else if (apiConnected == false)
                {
                    Thread.Sleep(2000);
                    apiConnected = lcuApiReadyCheck().Result;
                }
                else break; //ready
            }

            socketConnection = new WebSocket($"wss://127.0.0.1:{lcuPort}/", "wamp");
            socketConnection.SetCredentials("riot", rawLcuToken, true);
            socketConnection.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
            socketConnection.SslConfiguration.ServerCertificateValidationCallback = (a, b, c, d) => true;
            socketConnection.OnMessage += handleWebsocketMessage;
            socketConnection.OnClose += handleWebsocketDisconnection;
            socketConnection.Connect();

            _websocketSubscriptionSend("OnJsonApiEvent", 5, isKey: true); //subscribing to all events because then you can assign methods to endpoint and not event - tldr its simpler and safer

            isInLobby = false;
            isInChampSelect = false;
            isInGame = false;

            HttpResponseMessage gameflowResponse = request(requestMethod.GET, "/lol-gameflow/v1/gameflow-phase", ignoreReadyCheck:true).Result;

            if (gameflowResponse == null || gameflowResponse.StatusCode != HttpStatusCode.OK)
            {
                gameflowEventProc("None");
            }
            else
            {
                gameflowEventProc(gameflowResponse.Content.ReadAsStringAsync().Result.Replace("\"", ""));
            }


            localSummoner = getLocalSummonerFromLCU();
            localSummonerRegion = getSummonerRegionFromLCU().Result;

            OnConnected?.Invoke();
            isConnected = true;

            tryingToConnect = false;
        }

        private void handleDisconnection(bool byExit = false)
        {
            if (socketConnection.IsAlive) socketConnection.Close();

            if (byExit) return;
            else

                isConnected = false;

            socketConnection = null;

            isInChampSelect = false;
            isInLobby = false;
            isInGame = false;

            localSummoner = null;
            localSummonerRegion = null;
            currentGameflowPhase = null;

            lcuToken = null;
            lcuPort = null;

            OnDisconnected?.Invoke();

            if (autoReconnect) Task.Run(() => handleConnection());
        }

        private string _websocketEventFromEndpoint(string endpoint)
        {
            if (!endpoint.StartsWith("/")) endpoint = "/" + endpoint;
            return "OnJsonApiEvent" + endpoint.Replace("/", "_");
        }

            
        private void handleWebsocketMessage(object sender, MessageEventArgs e)
        {
            var arr = JsonConvert.DeserializeObject<JArray>(e.Data);

            if (arr.Count != 3) return;
            else if (Convert.ToInt16(arr[0]) != 8) return;

            string eventKey = arr[1].ToString();
            dynamic data = arr[2];

            if (eventKey != "OnJsonApiEvent")
            {
                return;
            }

            OnWebsocketEventArgs args = new OnWebsocketEventArgs()
            {
                Endpoint = data["uri"].ToString(),
                Type = data["eventType"].ToString(),
                Data = data["data"]
            };

            // special case for exiting the client
            if (args.Endpoint == "/process-control/v1/process")
            {
                string status = args.Data["status"].ToString();
                if (status == "Stopping")
                {
                    handleDisconnection(true);
                    return;
                }
            }
            // gameflow updates
            else if (args.Endpoint == "/lol-gameflow/v1/gameflow-phase")
            {
                gameflowEventProc(args.Data.ToString());
            }
            // current summoner info changed
            else if (args.Endpoint == "/lol-summoner/v1/current-summoner")
            {
                localSummoner = JsonConvert.DeserializeObject<Summoner>(args.Data.ToString());
            }

            if (!subscriptions.ContainsKey(args.Endpoint)) return;

            foreach (Action<OnWebsocketEventArgs> action in subscriptions[args.Endpoint]) action(args);
        }

        private void gameflowEventProc(string phase)
        {
            currentGameflowPhase = phase;

            switch (phase)
            {
                case "None":
                    if (isInChampSelect) OnChampSelectLeave?.Invoke();
                    if (isInLobby) OnLobbyLeave?.Invoke();
                    if (isInGame) OnGameLeave?.Invoke();

                    isInLobby = false;
                    isInChampSelect = false;
                    isInGame = false;

                    break;

                case "Lobby":
                    if (isInChampSelect) OnChampSelectLeave?.Invoke();
                    if (isInGame) OnGameLeave?.Invoke();

                    OnLobbyEnter?.Invoke();

                    isInLobby = true;
                    isInChampSelect = false;
                    isInGame = false;
                    break;

                case "ChampSelect":
                    if (isInLobby) OnLobbyLeave?.Invoke();
                    if (isInGame) OnGameLeave?.Invoke();

                    OnChampSelectEnter?.Invoke();

                    isInLobby = false;
                    isInChampSelect = true;
                    isInGame = false;
                    break;

                case "InProgress":
                    if (isInLobby) OnLobbyLeave?.Invoke();
                    if (isInChampSelect) OnChampSelectLeave?.Invoke();

                    OnGameEnter?.Invoke();

                    isInLobby = false;
                    isInChampSelect = false;
                    isInGame = true;
                    break;
            }
        }

        private void handleWebsocketDisconnection(object sender, CloseEventArgs e)
        {
            handleDisconnection();
        }

        private bool _websocketSubscriptionSend(string endpoint, int opcode, bool isKey = false)
        {
            if (socketConnection == null)
            {
                switch (opcode)
                {
                    case 5:
                        throw new InvalidOperationException($"Tried to subscribe to {endpoint}, but LCU is not connected.");

                    case 6:
                        throw new InvalidOperationException($"Tried to unsubscribe from {endpoint}, but LCU is not connected.");

                    default:
                        throw new InvalidOperationException($"Tried to send a message to {endpoint}, but LCU is not connected.");
                }
            }

            if (!isKey) socketConnection.Send($"[{opcode}, \"{_websocketEventFromEndpoint(endpoint)}\"]");
            else socketConnection.Send($"[{opcode}, \"{endpoint}\"]");
            return true;
        }

        /// <summary>
        /// Binds an <paramref name="action"/> to specified <paramref name="endpoint"/>. It will get invoked when an event is received from the League Client.
        /// </summary>
        /// <param name="endpoint">gg</param>
        /// <param name="action"></param>
        public void Subscribe(string endpoint, Action<OnWebsocketEventArgs> action)
        {
            if (action == null)
            {
                throw new InvalidOperationException("Tried to subscribe to an endpoint without specifying an action.");
            }

            if (!endpoint.StartsWith("/")) endpoint = "/" + endpoint;

            if (!subscriptions.ContainsKey(endpoint))
            {
                subscriptions.TryAdd(endpoint, new List<Action<OnWebsocketEventArgs>>() { action });
            }
            else
            {
                if (subscriptions[endpoint].Contains(action))
                {
                    throw new InvalidOperationException($"Tried to subscribe {action.ToString()} to {endpoint}, but this action was already bound.");
                }
                subscriptions[endpoint].Add(action);
            }
        }

        /// <summary>
        /// Unbinds an <paramref name="action"/> from specified <paramref name="endpoint"/>. If no action is specified, all actions bound to this endpoint will be removed.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="action"></param>
        public void Unsubscribe(string endpoint = null, Action<OnWebsocketEventArgs> action = null)
        {
            if (endpoint == null)
            {
                subscriptions.Clear();
                return;
            }

            if (!endpoint.StartsWith("/")) endpoint = "/" + endpoint;

            if (!subscriptions.ContainsKey(endpoint))
            {
                throw new InvalidOperationException($"Tried to unsubscribe from {endpoint}, but there are no actions bound to it.");
            }

            if (action == null)
            {
                subscriptions.TryRemove(endpoint, out _);
            }
            else
            {
                if (!subscriptions[endpoint].Remove(action))
                {
                    throw new InvalidOperationException($"Tried to unsubscribe {action.ToString()} from {endpoint}, but this action was not bound.");
                }
                else if (subscriptions[endpoint].Count == 0)
                {
                    subscriptions.TryRemove(endpoint, out _);
                }
            }
        }

        private void websocketClearSubscriptions()
        {
            subscriptions.Clear();
        }

        private bool isProcessRunning(string processName)
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }

        private List<(string, string)> getProcessCmdArgs(string processName)
        {
            if (!isProcessRunning(processName)) return new List<(string, string)>();

            if (!processName.EndsWith(".exe")) processName += ".exe";

            string command = $"wmic process where \"caption='{processName}'\" get CommandLine";
            Process p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = "/c " + command;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.UseShellExecute = false;
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            List<(string, string)> outputList = new List<(string, string)>();

            foreach (string line in output.Split(' '))
            {
                string rawArg = line.Replace("\"", "").Replace("--", "");

                if (rawArg.Contains("="))
                {
                    string arg = rawArg.Split(Convert.ToChar("="))[0];
                    string value = rawArg.Split(Convert.ToChar("="))[1];
                    outputList.Add((arg, value));
                }
                else
                {
                    outputList.Add((rawArg, ""));
                }
            }

            return outputList;
        }

        private static string base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// Sends a request to the League Client API. If <paramref name="ignoreReadyCheck"/> is set to true, it will not throw an exception if the client is not connected.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="endpoint"></param>
        /// <param name="data"></param>
        /// <param name="ignoreReadyCheck"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="HttpRequestException"></exception>
        public async Task<HttpResponseMessage> request(requestMethod method, string endpoint, dynamic data = null, bool ignoreReadyCheck = false)
        {
            if (!ignoreReadyCheck && !isConnected)
            {
                throw new InvalidOperationException($"Tried to request {endpoint}, but LCU is not connected yet.");
            }

            if (endpoint.StartsWith("/")) endpoint = endpoint.Substring(1);

            HttpRequestMessage request = new HttpRequestMessage
            {
                Method = new HttpMethod(method.ToString()),
                RequestUri = new Uri($"https://127.0.0.1:{lcuPort}/{endpoint}"),
                Headers =
                    {
                        { HttpRequestHeader.Authorization.ToString(), $"Basic {lcuToken}" },
                        { HttpRequestHeader.Accept.ToString(), "application/json" }
                    },
            };

            if (data != null)
            {
                request.Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
            }

            try
            {
                return await client.SendAsync(request);
            }
            catch (Exception e)
            {
                if (!ignoreReadyCheck) throw new HttpRequestException($"Failed to send request to {endpoint}. - {e.Message}");
                return null;
            }
        }

        private async Task<bool?> lcuApiReadyCheck()
        {
            HttpResponseMessage response;
            try
            {
                response = await request(requestMethod.GET, "/lol-gameflow/v1/availability", ignoreReadyCheck: true);
            }
            catch { return null; }

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }
            else
            {
                JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
                if (json == null) return null;
                else return json["isAvailable"].ToObject<bool>();
            }
        }

        /// <summary>
        /// Gets summoner data for a list of tuples containing name and tagline.
        /// </summary>
        /// <param name="tupList"></param>
        /// <returns></returns>
        /// <exception cref="HttpRequestException"></exception>
        public List<Summoner> GetSummoners(List<Tuple<string, string>> tupList)
        {
            List<string> names = new List<string>();

            foreach ((string name, string tagline) in tupList)
            {
                names.Add($"{name}#{tagline}");
            }

            HttpResponseMessage response = request(requestMethod.POST, "/lol-summoner/v2/summoners/names", names).Result;

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                string v = "";

                foreach (string n in names) v += $" {n}";

                throw new HttpRequestException($"Failed to get summoner data for: [{v.Substring(1)}]");
            }
            else
            {
                return JsonConvert.DeserializeObject<List<Summoner>>(response.Content.ReadAsStringAsync().Result);
            }


        }

        /// <summary>
        /// Gets info about a single summoner by <paramref name="name"/> and <paramref name="tagline"/>. If you want to get info about multiple summoners, use GetSummoners.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tagline"></param>
        /// <returns></returns>
        public Summoner GetSummoner(string name = null, string tagline = null)
        {
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(tagline)) return getLocalSummonerFromLCU();
            List<Tuple<string, string>> list = new List<Tuple<string, string>> { new Tuple<string, string>(name, tagline) };
            List<Summoner> outList = GetSummoners(list);
            if (outList == null || outList.Count == 0) return null;
            else return outList[0];
        }

        private Summoner getLocalSummonerFromLCU()
        {
            HttpResponseMessage response = request(requestMethod.GET, "/lol-summoner/v1/current-summoner", ignoreReadyCheck: true).Result;

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException("Failed to get local summoner data.");
            }
            else
            {
                return JsonConvert.DeserializeObject<Summoner>(response.Content.ReadAsStringAsync().Result);
            }
        }

        private async Task<string> getSummonerRegionFromLCU()
        {
            HttpResponseMessage response = await request(requestMethod.GET, $"/riotclient/region-locale", ignoreReadyCheck: true);

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException("Failed to get region locale.");
            }
            else
            {
                string region = JObject.Parse(await response.Content.ReadAsStringAsync())["region"].ToString();

                return region;
            }
        }
    }

    public enum requestMethod
    {
        GET, POST, PATCH, DELETE, PUT
    }

    public class OnWebsocketEventArgs : EventArgs
    {   // URI    
        public string Endpoint { get; set; }

        // Update create delete
        public string Type { get; set; }

        // data :D
        public dynamic Data { get; set; }
    }

    public class RerollPoints
    {
        public int currentPoints { get; set; }
        public int maxRolls { get; set; }
        public int numberOfRolls { get; set; }
        public int pointsCostToRoll { get; set; }
        public int pointsToReroll { get; set; }
    }

    public class Summoner
    {
        public Int64 accountId { get; set; }
        public string displayName { get; set; }
        public string gameName { get; set; }
        public string internalName { get; set; }
        public bool nameChangeFlag { get; set; }
        public int percentCompleteForNextLevel { get; set; }
        public string privacy { get; set; }
        public int profileIconId { get; set; }
        public string puuid { get; set; }
        public RerollPoints rerollPoints { get; set; }
        public Int64 summonerId { get; set; }
        public int summonerLevel { get; set; }
        public string tagLine { get; set; }
        public bool unnamed { get; set; }
        public Int64 xpSinceLastLevel { get; set; }
        public Int64 xpUntilNextLevel { get; set; }
    }
}