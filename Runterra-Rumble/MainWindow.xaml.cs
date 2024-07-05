using Newtonsoft.Json;
using soulspine.LCU;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Runterra_Rumble
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly LeagueClient lcu = App.GetLCU(); // this is so retarded that i have to do this AAAHAHAHHHAHAHAH
        private readonly MODE mode = MODE.Player;
        public MainWindow()
        {
            lcu.OnDisconnected += OnLcuDisconnected;
            lcu.OnConnected += OnLcuConnected;
            lcu.Subscribe("lol-summoner/v1/current-summoner", OnLocalSummonerInfoChanged);
            InitializeComponent();
        }

        private enum MODE { Admin, Player };


        private void UserUpdate(int? iconId = null, string displayName = null )
        {
            Dispatcher.Invoke(() => // needed to access lcu element
            {
                if (displayName != null) UserName.Text = displayName;
                if (iconId != null) UserIcon.Source = new BitmapImage(new Uri($"img\\profileicon\\{iconId.ToString()}.png", UriKind.Relative));
            });
        }

        private void OnLocalSummonerInfoChanged(OnWebsocketEventArgs e)
        {
            UserUpdate(lcu.localSummoner.profileIconId, $"{lcu.localSummoner.gameName}");
        }

        private void OnLcuConnected()
        {
            UserUpdate(lcu.localSummoner.profileIconId, $"{lcu.localSummoner.gameName}");
        }

        private void OnLcuDisconnected()
        {
            Trace.WriteLine("Disconnected");
            UserUpdate(29, "Not connected");
        }
    }
}
