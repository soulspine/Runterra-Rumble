using Newtonsoft.Json;
using soulspine.LCU;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private MODE mode = MODE.Player;
        public MainWindow()
        {
            lcu.OnDisconnected += OnLcuDisconnected;
            lcu.OnConnected += OnLcuConnected;
            lcu.Subscribe("lol-summoner/v1/current-summoner", OnLocalSummonerInfoChanged);
            InitializeComponent();
        }

        private enum MODE { Organizer, Player };


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

        private void ModeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.IsEnabled = false;

            const int lowerTextSize = 15;
            const int higherTextSize = 25;
            const double animationDuration = 0.3;
            var easingFunction = new QuadraticEase();

            var shrinkAnimation = new DoubleAnimation()
            {
                To = lowerTextSize,
                Duration = new Duration(TimeSpan.FromSeconds(animationDuration)),
                EasingFunction = easingFunction,

            };
            
            var growAnimation = new DoubleAnimation()
            {
                To = higherTextSize,
                Duration = new Duration(TimeSpan.FromSeconds(animationDuration)),
                EasingFunction = easingFunction,
            };

            var opacityDownAnimation = new DoubleAnimation()
            {
                To = 0.5,
                Duration = new Duration(TimeSpan.FromSeconds(animationDuration)),
                EasingFunction = easingFunction,
            };

            var opacityUpAnimation = new DoubleAnimation()
            {
                To = 1,
                Duration = new Duration(TimeSpan.FromSeconds(animationDuration)),
                EasingFunction = easingFunction,
            };

            if (mode == MODE.Player)
            {
                ModeButtonPlayerText.BeginAnimation(FontSizeProperty, shrinkAnimation);
                ModeButtonPlayerText.BeginAnimation(OpacityProperty, opacityDownAnimation);

                ModeButtonOrganizerText.BeginAnimation(FontSizeProperty, growAnimation);
                ModeButtonOrganizerText.BeginAnimation(OpacityProperty, opacityUpAnimation);
            }
            else
            {
                ModeButtonPlayerText.BeginAnimation(FontSizeProperty, growAnimation);
                ModeButtonPlayerText.BeginAnimation(OpacityProperty, opacityUpAnimation);

                ModeButtonOrganizerText.BeginAnimation(FontSizeProperty, shrinkAnimation);
                ModeButtonOrganizerText.BeginAnimation(OpacityProperty, opacityDownAnimation);
            }


            var rotateAnimation = new DoubleAnimation()
            {
                From = mode == MODE.Player ? 0 : 180,
                To = mode == MODE.Player ? 180 : 0,
                Duration = new Duration(TimeSpan.FromSeconds(animationDuration)),
                EasingFunction = easingFunction,
            };

            ModeButtonIconRotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);

            mode = mode == MODE.Player ? MODE.Organizer : MODE.Player;
            Trace.WriteLine($"Mode changed to {mode}");

            button.IsEnabled = true;
        }
    }
}
