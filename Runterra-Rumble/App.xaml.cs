using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using soulspine.LCU;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace Runterra_Rumble
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public LeagueClient lcu = new LeagueClient();
        public string gameVersion { get; private set; }
        private readonly HttpClient httpClient = new HttpClient();

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            lcu.Connect();
            lcu.OnConnected += this.CheckGameVersion;

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private void CheckGameVersion()
        {
            HttpResponseMessage response = httpClient.GetAsync("https://ddragon.leagueoflegends.com/api/versions.json").Result;
            JArray json = JArray.Parse(response.Content.ReadAsStringAsync().Result);
            gameVersion = json[0].ToString();
            Trace.WriteLine("Game version: " + gameVersion);
        }

        public static LeagueClient GetLCU()
        {
            return ((App)Application.Current).lcu;
        }

        public static string GetGameVersion()
        {
            return ((App)Application.Current).gameVersion;
        }

        public static string GetIconPath(int iconId)
        {
            return $"https://ddragon.leagueoflegends.com/cdn/{GetGameVersion()}/img/profileicon/{iconId.ToString()}.png";

        }
    }
}
