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

namespace Runterra_Rumble
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public LeagueClient lcu = new LeagueClient();

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            lcu.Connect();

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        public static LeagueClient GetLCU()
        {
            return ((App)Application.Current).lcu;
        }
    }
}
