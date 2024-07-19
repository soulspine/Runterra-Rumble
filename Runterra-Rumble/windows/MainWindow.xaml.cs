using Microsoft.SqlServer.Server;
using Newtonsoft.Json;
using Runterra_Rumble.pages;
using soulspine.LCU;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
        public readonly LeagueClient lcu = App.GetLCU(); // this is so retarded that i have to do this AAAHAHAHHHAHAHAH
        private MODE mode = MODE.Player;
        private object currentPage = null;

        //Mode button
        private bool ModeButton_IsEnabled = true;
        private static readonly int ModeButton_LowerTextSize = 15;
        private static readonly int ModeButton_HigherTextSize = 25;
        private static readonly double ModeButton_AnimationDuration = 0.5;
        private static readonly QuadraticEase ModeButton_EasingFunction = new QuadraticEase();
        private static readonly DoubleAnimation ModeButton_ShrinkAnimation = new DoubleAnimation()
        {
            To = ModeButton_LowerTextSize,
            Duration = new Duration(TimeSpan.FromSeconds(ModeButton_AnimationDuration)),
            EasingFunction = ModeButton_EasingFunction,

        };
        private static readonly DoubleAnimation ModeButton_GrowAnimation = new DoubleAnimation()
        {
            To = ModeButton_HigherTextSize,
            Duration = new Duration(TimeSpan.FromSeconds(ModeButton_AnimationDuration)),
            EasingFunction = ModeButton_EasingFunction,
        };
        private static readonly DoubleAnimation ModeButton_OpacityDownAnimation = new DoubleAnimation()
        {
            To = 0.3,
            Duration = new Duration(TimeSpan.FromSeconds(ModeButton_AnimationDuration)),
            EasingFunction = ModeButton_EasingFunction,
        };
        private static readonly DoubleAnimation ModeButton_OpacityUpAnimation = new DoubleAnimation()
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromSeconds(ModeButton_AnimationDuration)),
            EasingFunction = ModeButton_EasingFunction,
        };

        //User Area
        private static readonly double UserArea_AnimationDuration = 0.5;

        //Join / Create button
        private bool JoinCreateButton_IsEnabled = true;
        private static readonly double JoinCreateButton_AnimationDuration = 0.5;

        //easter egg sounds
        MediaPlayer ee_MusicPlayer = new MediaPlayer();

        //mode button arrow spinoff easter egg
        private bool ee_ModeButtonSpinoff_Active = false;
        private const double ee_ModeButtonSpinoff_MaxClickInterval = 0.2; //seconds
        private const int ee_ModeButtonSpinoff_ClicksNeededToTrigger = 5;
        private int ee_ModeButtonSpinoff_ConcurrentClickCount = 0;
        private DateTime ee_ModeButtonSpinoff_LastClickTime = DateTime.Now;

        public MainWindow()
        {
            lcu.OnDisconnected += OnLcuDisconnected;
            lcu.OnConnected += OnLcuConnected;

            lcu.Subscribe("lol-summoner/v1/current-summoner", OnLocalSummonerInfoChanged);
            lcu.Subscribe("lol-gameflow/v1/gameflow-phase", OnGameflowPhaseChanged);

            ee_MusicPlayer.Open(new Uri("C:\\code\\Runterra-Rumble\\Runterra-Rumble\\sfx\\easteregg.mp3"));
            ee_MusicPlayer.Volume = 0.2;

            InitializeComponent();

            ModeButton_Disable();
            JoinCreateButton_Disable();
        }

        private enum MODE { Organizer, Player };


        private void UserUpdate(int? iconId = null, string displayName = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (displayName != null)
                {
                    UserName.BeginAnimation(TextBlock.TextProperty, CustomAnimation.DeleteAndTyping(UserName.Text, displayName, UserArea_AnimationDuration));
                }
                if (iconId != null) UserIcon.Source = new BitmapImage(new Uri($"pack://application:,,,/img/profileicon/{iconId.ToString()}.png"));
            });
        }

        private void OnLocalSummonerInfoChanged(OnWebsocketEventArgs e)
        {
            if (lcu.localSummoner == null) return; // this is here to prevent crashing when logging out of league, because websocket sends an empty summoner event before disconnect
            UserUpdate(lcu.localSummoner.profileIconId, null);
        }

        private void OnLcuConnected()
        {
            UserUpdate(lcu.localSummoner.profileIconId, $"{lcu.localSummoner.gameName}");
            TitleBar_GlowEnable(0.5);

            HideNoLcuCard(0.5);

            if (lcu.currentGameflowPhase != "None")
            {
                ModeButton_Disable();
                JoinCreateButton_Disable();
            }
            else
            {
                ModeButton_Enable();
                JoinCreateButton_Enable();
            }
        }

        private void OnLcuDisconnected()
        {
            UserUpdate(29, "Not connected");
            TitleBar_GlowDisable(0.5);

            ShowNoLcuCard(0.5);

            ModeButton_Disable();
            JoinCreateButton_Disable();
        }

        private void OnGameflowPhaseChanged(OnWebsocketEventArgs e)
        {
            if (e.Data == "None")
            {
                ModeButton_Enable();
                JoinCreateButton_Enable();
            }
            else
            {
                ModeButton_Disable();
                JoinCreateButton_Disable();
            }
        }

        private void ModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ModeButton_IsEnabled) return;

            var button = (Button)sender;
            button.IsEnabled = false;

            // easter egg
            if ((DateTime.Now - ee_ModeButtonSpinoff_LastClickTime).TotalSeconds < ee_ModeButtonSpinoff_MaxClickInterval) ee_ModeButtonSpinoff_ConcurrentClickCount++;
            else ee_ModeButtonSpinoff_ConcurrentClickCount = 1;

            ee_ModeButtonSpinoff_LastClickTime = DateTime.Now;

            if (ee_ModeButtonSpinoff_ConcurrentClickCount == ee_ModeButtonSpinoff_ClicksNeededToTrigger)
            {
                ee_ModeButtonSpinoff_Active = !ee_ModeButtonSpinoff_Active;
                ee_MusicPlayer.Position = TimeSpan.Zero;
                ee_MusicPlayer.Play();
            }
            // end easter egg

            if (mode == MODE.Player)
            {
                ModeButtonPlayerText.BeginAnimation(FontSizeProperty, ModeButton_ShrinkAnimation);
                ModeButtonPlayerText.BeginAnimation(OpacityProperty, ModeButton_OpacityDownAnimation);

                ModeButtonOrganizerText.BeginAnimation(FontSizeProperty, ModeButton_GrowAnimation);
                ModeButtonOrganizerText.BeginAnimation(OpacityProperty, ModeButton_OpacityUpAnimation);
            }
            else
            {
                ModeButtonPlayerText.BeginAnimation(FontSizeProperty, ModeButton_GrowAnimation);
                ModeButtonPlayerText.BeginAnimation(OpacityProperty, ModeButton_OpacityUpAnimation);

                ModeButtonOrganizerText.BeginAnimation(FontSizeProperty, ModeButton_ShrinkAnimation);
                ModeButtonOrganizerText.BeginAnimation(OpacityProperty, ModeButton_OpacityDownAnimation);
            }

            double from, to;

            if (ee_ModeButtonSpinoff_Active)
            {
                var r = new Random();
                from = ModeButtonIconRotateTransform.Angle;
                to = from + r.NextDouble() * 360 * 3;
            }
            else
            {
                from = mode == MODE.Player ? 0 : 180;
                to = mode == MODE.Player ? 180 : 0;
            }

            var rotateAnimation = new DoubleAnimation()
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromSeconds(ModeButton_AnimationDuration)),
                EasingFunction = ModeButton_EasingFunction,
            };

            ModeButtonIconRotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);

            mode = mode == MODE.Player ? MODE.Organizer : MODE.Player;
            Trace.WriteLine($"Mode changed to {mode}");

            int lettersToDelete = 0;

            foreach (char letter in JoinCreateButtonText.Text)
            {
                if (letter == ' ') break;
                lettersToDelete++;
            }

            string desiredPrefix = mode == MODE.Player ? "Join" : "Host";

            JoinCreateButtonText.BeginAnimation(TextBlock.TextProperty, CustomAnimation.DeleteAndTyping(JoinCreateButtonText.Text, desiredPrefix, JoinCreateButton_AnimationDuration));

            button.IsEnabled = true;
        }

        private void ModeButton_Disable()
        {
            if (!ModeButton_IsEnabled) return;

            ModeButton_IsEnabled = false;

            Dispatcher.Invoke(() =>
            {
                ModeButton.Cursor = Cursors.No;
                ModeButton.BeginAnimation(OpacityProperty, ModeButton_OpacityDownAnimation);
            });
        }

        private void ModeButton_Enable()
        {
            if (ModeButton_IsEnabled) return;

            ModeButton_IsEnabled = true;

            Dispatcher.Invoke(() =>
            {
                ModeButton.Cursor = Cursors.Hand;
                ModeButton.BeginAnimation(OpacityProperty, ModeButton_OpacityUpAnimation);
            });
        }

        private void JoinCreateButton_Disable()
        {
            if (!JoinCreateButton_IsEnabled) return;

            JoinCreateButton_IsEnabled = false;

            Dispatcher.Invoke(() =>
            {
                JoinCreateButton.Cursor = Cursors.No;
                JoinCreateButton.BeginAnimation(OpacityProperty, ModeButton_OpacityDownAnimation); // reusing the same animation
            });
        }

        private void JoinCreateButton_Enable()
        {
            if (JoinCreateButton_IsEnabled) return;

            JoinCreateButton_IsEnabled = true;

            Dispatcher.Invoke(() =>
            {
                JoinCreateButton.Cursor = Cursors.Hand;
                JoinCreateButton.BeginAnimation(OpacityProperty, ModeButton_OpacityUpAnimation); // reusing the same animation
            });
        }

        private void JoinCreateButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TitleBar_GlowEnable(double duration)
        {
            Dispatcher.Invoke(() =>
            {
                TitleBarBackgroundGlow.BeginAnimation(OpacityProperty, new DoubleAnimation()
                {
                    To = 1,
                    Duration = new Duration(TimeSpan.FromSeconds(duration)),
                });
            });
        }

        private void TitleBar_GlowDisable(double duration)
        {
            Dispatcher.Invoke(() =>
            {
                TitleBarBackgroundGlow.BeginAnimation(OpacityProperty, new DoubleAnimation()
                {
                    To = 0,
                    Duration = new Duration(TimeSpan.FromSeconds(duration)),
                });
            });
        }

        private void ShowNoLcuCard(double duration)
        {
            Dispatcher.Invoke(() =>
            {
                NoLcuFrame.BeginAnimation(OpacityProperty, new DoubleAnimation(NoLcuFrame.Opacity, 1, new Duration(TimeSpan.FromSeconds(duration))));
                MainFrame.BeginAnimation(OpacityProperty, new DoubleAnimation(MainFrame.Opacity, 0, new Duration(TimeSpan.FromSeconds(duration))));
                Panel.SetZIndex(NoLcuFrame, 1);
            });
        }

        private void HideNoLcuCard(double duration)
        {
            Dispatcher.Invoke(() =>
            {
                NoLcuFrame.BeginAnimation(OpacityProperty, new DoubleAnimation(NoLcuFrame.Opacity, 0, new Duration(TimeSpan.FromSeconds(duration))));
                MainFrame.BeginAnimation(OpacityProperty, new DoubleAnimation(MainFrame.Opacity, 1, new Duration(TimeSpan.FromSeconds(duration))));
                Panel.SetZIndex(NoLcuFrame, -1);
            });
        }

        private void ChangeMainFrameSource(MainFrameSource source)
        {
            object newElement;

            switch (source)
            {
                case MainFrameSource.PlayerLookup:
                    newElement = new PlayerLookup();
                    break;
                default:
                    throw new NotImplementedException();
            }

            Dispatcher.Invoke(() => MainFrame.Content = newElement);
        }

        private enum MainFrameSource { PlayerLookup };

        private void TestPlayerLookup_Click(object sender, RoutedEventArgs e)
        {
            ChangeMainFrameSource(MainFrameSource.PlayerLookup);
        }
    }
}
