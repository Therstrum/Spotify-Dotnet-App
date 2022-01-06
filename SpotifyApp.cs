using System;
using System.Net;
using System.Text;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Diagnostics;

namespace SpotifyApp
{
    public enum Commands { PLAY, PAUSE };
    public class Player
    {
        static CancellationTokenSource s_cts = new CancellationTokenSource();
        static CancellationToken token = s_cts.Token;
        string clientId = "";
        string clientSecret = "";
        static HttpListener _httpListener = null;
        const string redirectUri = "http://localhost:8888/callback";
        const string AUTHORIZE = "http://accounts.spotify.com/authorize";
        const string TOKEN = "https://accounts.spotify.com/api/token";
        const string PAUSE = "https://api.spotify.com/v1/me/player/pause";
        const string RESUME = "https://api.spotify.com/v1/me/player/play";
        //const string GETDEVICES = "https://api.spotify.com/v1/me/player/devices";

        static string oAuthCode = "";
        static int requestNumber = 0;
        static readonly DateTime StartupDate = DateTime.UtcNow;
        private static readonly HttpClient client = new HttpClient();

        TokenData tokenData = new TokenData();

        // Check if Spotify App is running on PC
        public bool IsSpotifyRunning()
        {
            if (Process.GetProcessesByName("spotify").Length > 0)
            {
                return true;
            }
            return false;
        }

        public bool StartSpotifyApp()
        {
            LocalStorage.StartSpotifyApp();
            return IsSpotifyRunning();
        }

        // Setup a local server to receive web requests. Required for oauth redirecturi
        // Once complete, begin chain of Authentication workflow.
        // Check if roaming appdata contains saved access_code. If not -> oauth -> get access_code
        public void setupServer()
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(@"http://localhost:8888/");
            _httpListener.Start();
            _httpListener.BeginGetContext(GetContextCallback, null);

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

            ValidateTokenData();
        }

        // Entry point to invoke Spotify Api. This should be the sole means of invoking the API.
        // To add more api calls:
        // 1: Build the api call into a method.
        // 2: Add the command name to the Commanads enum.
        // 3: Append the switch statement here.
        // 4: Hook this method with the new enum in your program.
        public void InvokeSpotify(Commands command)
        {
            if (IsTokenExpired() && tokenData.refresh_token != null)
            {
                FetchAccessToken(tokenData.refresh_token);
            }
            else if (IsTokenExpired() && tokenData.refresh_token == null)
            {
                OAuth();
            }
            switch (command)
            {
                case Commands.PLAY:
                    ResumePlayer();
                    break;
                case Commands.PAUSE:
                    PausePlayer();
                    break;
                default:
                    break;
            }
        }

        // ConfigData is where the api client_id + secret are stored.
        // Pull it into local memory, or prompt the user to enter.
        void ValidateConfigData()
        {
            ConfigData cd = GetConfigData();
            if (cd == null)
            {
                cd = new ConfigData();
                Console.WriteLine(@"DEVELOPERS: Spotify App Client ID and secret needed
                https://developer.spotify.com/dashboard/applications");
                Console.WriteLine("Enter your Client ID: ");
                string _client_ID = Console.ReadLine();
                Console.WriteLine("Enter your Client Secret: ");
                string _client_secret = Console.ReadLine();
                cd.client_id = _client_ID;
                cd.client_Secret = _client_secret;
                SaveConfigData(cd);
            }
            clientId = cd.client_id;
            clientSecret = cd.client_Secret;
        }


        void ValidateTokenData()
        {
            // If there is no local saved access code from appdata, perform oauth
            TokenData td = GetTokenData();
            if (td == null)
            {
                OAuth();
                return;
            }
            // If the access code is expired, get a refresh token
            bool tokenExpired = td.expires_in < DateTime.Now;
            if (tokenExpired && td.refresh_token != null)
            {
                tokenData.refresh_token = td.refresh_token;
                FetchRefreshToken(td.refresh_token);
            }
            // If the access code is expired, and we don't have a refres code, perform oauth
            else if ((tokenExpired) && td.refresh_token == null)
            {
                OAuth();
            }
            tokenData = td;
        }
        // Launch a browser and perform Oauth.
        // Reference https://developer.spotify.com/documentation/general/guides/authorization/code-flow/
        void OAuth()
        {
            string scope = "user-modify-playback-state+user-read-currently-playing+user-read-playback-state";
            string url = AUTHORIZE + "?client_id=" + clientId + "^&scope=" + scope + "^&response_type=code" + "^&redirect_uri=" + redirectUri;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd", $"/c start {url}"));
        }

        string BuildBasicAuth()
        {
            var authKey = clientId + ":" + clientSecret;
            var bytes = System.Text.Encoding.ASCII.GetBytes(authKey);
            var basicAuth = System.Convert.ToBase64String(bytes);
            return basicAuth;
        }
        bool IsTokenExpired()
        {
            return tokenData.expires_in < DateTime.Now;
        }
        // Refresh Tokens are used when the access_token is expired
        // Reference https://developer.spotify.com/documentation/general/guides/authorization/code-flow/
        async void FetchRefreshToken(string refreshToken)
        {
            var values = new Dictionary<string, string> {
            {"grant_type","refresh_token"},
            {"refresh_token",refreshToken}};
            var url = TOKEN;
            var basicAuth = BuildBasicAuth();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            var refreshRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(values)
            };

            var refreshResult = await client.SendAsync(refreshRequest);
            string body = await refreshResult.Content.ReadAsStringAsync();
            var bodyJson = JsonSerializer.Deserialize<TokenResponse>(body);
            tokenData.access_token = bodyJson.access_token;
            tokenData.expires_in = DateTime.Now.AddSeconds(bodyJson.expires_in);

        }
        // Reference https://developer.spotify.com/documentation/general/guides/authorization/code-flow/
        async void FetchAccessToken(string code)
        {
            var values = new Dictionary<string, string> {
            {"grant_type","authorization_code"},
            {"redirect_uri",redirectUri},
            {"code",code}};
            var url = TOKEN;

            var authKey = clientId + ":" + clientSecret;
            var bytes = System.Text.Encoding.ASCII.GetBytes(authKey);
            var basicAuth = BuildBasicAuth();

            var newRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(values)
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

            var newResult = await client.SendAsync(newRequest);

            string body = await newResult.Content.ReadAsStringAsync();
            var bodyJson = JsonSerializer.Deserialize<TokenResponse>(body);

            tokenData.access_token = bodyJson.access_token;
            tokenData.refresh_token = bodyJson.refresh_token;
            tokenData.expires_in = DateTime.Now.AddSeconds(bodyJson.expires_in);

        }
        public TokenData GetTokenData()
        {
            return LocalStorage.GetTokenData(false) as TokenData;
        }
        public ConfigData GetConfigData()
        {
            return LocalStorage.GetTokenData(true) as ConfigData;
        }
        void SaveTokenData(TokenData td)
        {
            string tokenText = JsonSerializer.Serialize(td);
            LocalStorage.SaveTokenDataPublic(tokenText, false);
        }
        void SaveConfigData(ConfigData cd)
        {
            string tokenText = JsonSerializer.Serialize(cd);
            LocalStorage.SaveTokenDataPublic(tokenText, true);
        }
        // Reference https://developer.spotify.com/console/put-pause/
        async void PausePlayer()
        {
            var pauseRequest = new HttpRequestMessage(HttpMethod.Put, PAUSE);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenData.access_token);
            var pauseResult = await client.SendAsync(pauseRequest);


            string body = await pauseResult.Content.ReadAsStringAsync();
        }
        // Reference https://developer.spotify.com/console/put-play/
        // If called with no request body, will resume playback.
        // Optional body to play specific song, offset, volume, etc.
        async void ResumePlayer()
        {
            var resumeRequest = new HttpRequestMessage(HttpMethod.Put, RESUME);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenData.access_token);
            var resumeResult = await client.SendAsync(resumeRequest);

            string body = await resumeResult.Content.ReadAsStringAsync();
        }
        //Callback for local server that serves as oauth redirecturi
        void GetContextCallback(IAsyncResult ar)
        {

            int req = ++requestNumber;
            var responseString = "";
            var context = _httpListener.EndGetContext(ar);
            _httpListener.BeginGetContext(GetContextCallback, null);
            var NowTime = DateTime.UtcNow;

            //If we get a response from oauth with an access code, invoke the access_token call.
            string[] responseParsed = context.Request.RawUrl.Split("code=");
            if (responseParsed.Length > 1)
            {
                oAuthCode = responseParsed[1];
                responseString = "Authenticated!";
                FetchAccessToken(oAuthCode);
            }
            else
            {
                responseString = string.Format("<html><body>Your request, \"{0}\", was received at {1}.<br/>It is request #{2:N0} since {3}.",
                context.Request.RawUrl, NowTime.ToString("R"), req, StartupDate.ToString("R"));
            }
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            var response = context.Response;
            response.ContentType = "text/html";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = 200;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();

        }
        /* Not yet implemented Future development to prompt user to choose device if multiple are active.
        async void GetDevices() 
        {
            var deviceRequest = new HttpRequestMessage(HttpMethod.Get, GETDEVICES);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenData.access_token);

            var deviceResult = await client.SendAsync(deviceRequest);

            string body = await deviceResult.Content.ReadAsStringAsync();
            Console.WriteLine(body);

            deviceList = JsonSerializer.Deserialize<SpotifyDeviceList>(body);

        }
        //Not yet implemented
        public class SpotifyDeviceList
        {
            public List<SpotifyDevice> devices { get; set; }
        }
        
        public class SpotifyDevice
        {
            string id { get; set; }
            bool is_active { get; set; }
            bool is_private_session { get; set; }
            bool is_restricted { get; set; }
            string name { get; set; }
            string type { get; set; }
            int volume_percent { get; set; }
        }*/

        // Failsafe in case StartSpotifyApp() can't do it's job.
        public async void StartPlayer(Player spotify)
        {
            if (!IsSpotifyRunning())
            {
                bool success = StartSpotifyApp();
                if (!success)
                {
                    PromptStartSpotify();
                }
            }

            ValidateConfigData();
            await Task.Run(() =>
            {
                spotify.setupServer();
            }, token);
        }

        void PromptStartSpotify()
        {
            Console.WriteLine("Unable to launch Spotify. Please launch manually and press enter...");
            Console.ReadLine();
        }
        public void StopPlayer()
        {
            SaveTokenData(tokenData);
            s_cts.Cancel();
        }
    }
    // Response object from authentication workflows.
    public class TokenResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
        public string scope { get; set; }
    }
}