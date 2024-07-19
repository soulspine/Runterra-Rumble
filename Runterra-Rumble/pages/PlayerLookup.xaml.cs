using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using soulspine.LCU;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using Runterra_Rumble.objects;
using System.IO;
using System.Threading;

namespace Runterra_Rumble.pages
{
    /// <summary>
    /// Interaction logic for PlayerLookup.xaml
    /// </summary>
    public partial class PlayerLookup : Page
    {
        private readonly LeagueClient lcu = App.GetLCU();

        private bool SearchButton_IsEnabled = false;

        private bool Input_HasDefaultText = false;
        private bool Input_IsInDefaultPosition = true;
        private bool Input_ShowingError = false;

        private const double Input_AnimationDuration = 0.5;
        private const int Input_GapBetweenBoxAndButton = 30;

        private bool InputName_IsValid = false;
        private const int InputName_MinLength = 3;
        private const int InputName_MaxLength = 16;

        private bool InputTagline_IsValid = false;
        private const int InputTagline_MinLength = 3;
        private const int InputTagline_MaxLength = 5;

        public PlayerLookup()
        {
            InitializeComponent();

            InputBorder.Visibility = Visibility.Visible;
            SearchButton.Visibility = Visibility.Visible;
            SummonerErrorMessageLabel.Visibility = Visibility.Visible;

            InputName.Text = lcu.localSummoner.gameName;
            InputTagline.Text = lcu.localSummoner.tagLine;

            Input_HasDefaultText = true;
            InputCanvas_SizeChanged(null, null);
        }

        private void SearchButton_Enable()
        {
            if (SearchButton_IsEnabled) return;
            
            SearchButton.Cursor = Cursors.Hand;
            SearchButton_IsEnabled = true;

            SearchButton.BeginAnimation(OpacityProperty, new DoubleAnimation()
            {
                To = 1,
                Duration = TimeSpan.FromSeconds(Input_AnimationDuration)
            });

            SearchButton.Visibility = Visibility.Visible;
        }

        private void SearchButton_Disable()
        {
            if (!SearchButton_IsEnabled) return;

            SearchButton.Cursor = Cursors.No;
            SearchButton_IsEnabled = false;

            SearchButton.BeginAnimation(OpacityProperty, new DoubleAnimation()
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(Input_AnimationDuration)
            });

            Task.Delay((int)(1000*Input_AnimationDuration)).ContinueWith((_) => Dispatcher.Invoke(() =>
            {
                if (!SearchButton_IsEnabled) SearchButton.Visibility = Visibility.Hidden;
            }));
        }

        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        { 
            var textBox = (TextBox)sender;
            int minLength, maxLength;

            if (textBox.Text.Contains("#") && textBox.Text.Length != 0)
            {
                string fullText = textBox.Text;
                InputName.Text = fullText.Substring(0, fullText.IndexOf("#"));
                InputTagline.Text = fullText.Substring(fullText.IndexOf("#") + 1);

                SearchButton.Focus();
                return;
            }

            if (textBox == InputName)
            {
                minLength = InputName_MinLength;
                maxLength = InputName_MaxLength;
            }
            else
            {
                minLength = InputTagline_MinLength;
                maxLength = InputTagline_MaxLength;
            }

            if ((textBox.Text.Length >= minLength) && (textBox.Text.Length <= maxLength))
            {
                if (textBox == InputName) InputName_IsValid = true;
                else InputTagline_IsValid = true;
            }
            else
            {
                if (textBox == InputName) InputName_IsValid = false;
                else InputTagline_IsValid = false;
            }

            if (textBox.Text.Length > maxLength)
            {
                textBox.Text = textBox.Text.Substring(0, maxLength);
                textBox.CaretIndex = textBox.Text.Length;
            }

            if (InputName_IsValid && InputTagline_IsValid) SearchButton_Enable();
            else SearchButton_Disable();
        }

        private void Input_GotFocus(object sender, RoutedEventArgs e)
        {
            HideSummonerError();

            Dispatcher.Invoke(() =>
            {

                if (Input_HasDefaultText)
                {
                    InputName.Text = "";
                    InputTagline.Text = "";
                    InputName.Opacity = 1;
                    InputTagline.Opacity = 1;

                    Input_HasDefaultText = false;
                }
            });
            
        }

        private void InputCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Input_IsInDefaultPosition)
            {
                Canvas.SetTop(InputBorder, (InputCanvas.ActualHeight - InputBorder.ActualHeight - Input_GapBetweenBoxAndButton) / 2);
                Canvas.SetTop(SummonerErrorMessageLabel, (InputCanvas.ActualHeight - SummonerErrorMessageLabel.ActualHeight) / 2);
                Canvas.SetTop(SearchButton, (InputCanvas.ActualHeight - SearchButton.ActualHeight + InputBorder.ActualHeight + Input_GapBetweenBoxAndButton) / 2);
            }
            
            Canvas.SetLeft(InputBorder, (InputCanvas.ActualWidth - InputBorder.ActualWidth) / 2);
            Canvas.SetLeft(SummonerErrorMessageLabel, (InputCanvas.ActualWidth - SummonerErrorMessageLabel.ActualWidth) / 2);
            Canvas.SetLeft(SearchButton, (InputCanvas.ActualWidth - SearchButton.ActualWidth) / 2);
        }

        private void Input_MoveSearchBox() 
        {
            Dispatcher.Invoke(() =>
            {
                DoubleAnimation anim = new DoubleAnimation();
                anim.Duration = new Duration(TimeSpan.FromSeconds(Input_AnimationDuration));
                anim.EasingFunction = new CubicEase();

                if (Input_IsInDefaultPosition)
                {
                    Input_IsInDefaultPosition = false;

                    anim.To = 20;

                    InputBorder.BeginAnimation(Canvas.TopProperty, anim);
                    SummonerErrorMessageLabel.BeginAnimation(Canvas.TopProperty, anim);

                    anim.To += Input_GapBetweenBoxAndButton;

                    SearchButton.BeginAnimation(Canvas.TopProperty, anim);
                }
                else
                {
                    Input_IsInDefaultPosition = true;

                    anim.To = (InputCanvas.ActualHeight - InputBorder.ActualHeight - Input_GapBetweenBoxAndButton) / 2;

                    InputBorder.BeginAnimation(Canvas.TopProperty, anim);
                    SummonerErrorMessageLabel.BeginAnimation(Canvas.TopProperty, anim);

                    anim.To = (InputCanvas.ActualHeight - SearchButton.ActualHeight + InputBorder.ActualHeight + Input_GapBetweenBoxAndButton) / 2;

                    SearchButton.BeginAnimation(Canvas.TopProperty, anim);
                }
            });
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SearchButton_IsEnabled) return;

            SearchButton.IsEnabled = false;

            string name = InputName.Text;
            string tagline = InputTagline.Text;

            // this is weird but if you dont use task.run it will freeze the ui, i think its because the lcu api is blocking the ui thread
            // same thing happens if you put lcu call in dispatcher.invoke
            Task.Run(() =>
            {
                var summoner = lcu.GetSummoner(name, tagline);

                if (summoner == null)
                {
                    if (!Input_IsInDefaultPosition) Input_MoveSearchBox(); //search box is up, move it down
                    HideProfile();
                    ShowSummonerError();
                }
                else
                {
                    if (Input_IsInDefaultPosition) //search box is down, move it up
                    {
                        if (Input_HasDefaultText) Input_GotFocus(null, null);
                        Input_MoveSearchBox();
                    }
                    else //search box is up, do nothing with it
                    {
                        HideProfile();
                        Thread.Sleep(500);
                    }
                    LoadProfile(summoner);
                }
                
            });

            SearchButton.IsEnabled = true;
        }

        private void LoadProfile(Summoner summoner)
        {
            var rankedResponse = lcu.request(requestMethod.GET, $"/lol-ranked/v1/ranked-stats/{summoner.puuid}").Result;

            if (rankedResponse == null || rankedResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                MessageBox.Show("Failed to load profile.");
                return;
            }

            JObject rankedData = JObject.Parse(rankedResponse.Content.ReadAsStringAsync().Result);

            JObject solo = rankedData["queueMap"]["RANKED_SOLO_5x5"].ToObject<JObject>();
            string soloTier = solo["tier"].ToString();
            if (soloTier == "") soloTier = "UNRANKED";

            JObject flex = rankedData["queueMap"]["RANKED_FLEX_SR"].ToObject<JObject>();
            string flexTier = flex["tier"].ToString();
            if (flexTier == "") flexTier = "UNRANKED";

            Dispatcher.Invoke(() =>
            {
                LeftGrid.BeginAnimation(OpacityProperty, new DoubleAnimation()
                {
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.5)
                });

                UserIcon.Source = new BitmapImage(new Uri($"pack://application:,,,/img/profileicon/{summoner.profileIconId.ToString()}.png"));

                ProfileRankedSoloIcon.Source = new BitmapImage(new Uri($"pack://application:,,,/img/tier/{soloTier.ToLower()}.png"));
                ProfileRankedFlexIcon.Source = new BitmapImage(new Uri($"pack://application:,,,/img/tier/{flexTier.ToLower()}.png"));

                ProfileRankedSoloText.Content = TierMessage(solo);
                ProfileRankedFlexText.Content = TierMessage(flex);

                ProfileRankedSoloWins.Content = $"Wins: {solo["wins"]}";
                ProfileRankedFlexWins.Content = $"Wins: {flex["wins"]}";

                InputName.Text = summoner.gameName;
                InputTagline.Text = summoner.tagLine;

            });


            /* This whole block was supposed to retrieve winrate counting games in match history
             * Turns out LCU API is rate limiting us so its not possible
             * It takes roughly 0.1s to analyze one game and it cannot be sped up, because of time needed to wait for LCU to retrieve match info
             * Trying to do it sequentially or breaking it down into multiple threads has little to no effects
             * It's not a huge problem when displaying match info in the middle, because it's rendering 20 games at a time
             * Needing to go through possibly hundreds of games to reach start of split to count the winrate is just not worth it.
             * I could also do it the way big sites do it - only get stats after manually updating it and store them locally to get a quick retrieve when possible
             * I have one more idea - I could use external API and do it simultaneously with local API
             * This theoretically could half the wait time, potentially even more
             * Downside to this approach is that it would require users to update their API key daily
             * Thats because Riot API keys expire at some time every day unless you have a production key
             * That's why default profile lookup displays only number of wins.
             
            var splitsResponse = lcu.request(requestMethod.GET, "/lol-ranked/v1/splits-config").Result;

            JObject splitsData = JObject.Parse(splitsResponse.Content.ReadAsStringAsync().Result);

            Int64 splitStartMillis = splitsData["currentSplit"]["startTimeMillis"].ToObject<Int64>();
            Int64 splitEndMillis = splitsData["currentSplit"]["endTimeMillis"].ToObject<Int64>();

            DateTime splitStartDate = DateTimeOffset.FromUnixTimeMilliseconds(splitStartMillis).DateTime;
            DateTime splitEndDate = DateTimeOffset.FromUnixTimeMilliseconds(splitEndMillis).DateTime;

            DateTime timeStart = DateTime.Now;

            int matchCheckRange = 5;

            int matchCounter = 0;

            ConcurrentDictionary<string, int> soloWinrate = new ConcurrentDictionary<string, int>();
            ConcurrentDictionary<string, int> flexWinrate = new ConcurrentDictionary<string, int>();

            DateTime beforeProcessingMH = DateTime.Now;

            bool stopper = false;

            int completionCounter = 0;
            int completionsRequired = 0;

            while (!stopper)
            {
                new Thread(() => WinrateAnalyzerThread(summoner.puuid, matchCounter, matchCounter + matchCheckRange - 1, splitStartDate, splitEndDate, ref soloWinrate, ref flexWinrate, ref stopper, ref completionCounter)).Start();
                matchCounter += matchCheckRange;
                completionsRequired++;
                Thread.Sleep(matchCheckRange*90);
            }

            Trace.WriteLine($"Waiting for completion");

            while (completionCounter != completionsRequired) { }

            DateTime afterProcessingMH = DateTime.Now;
            Trace.WriteLine($"Time taken to process match history: {afterProcessingMH - beforeProcessingMH}");

            Dispatcher.Invoke(() =>
            {
                ProfileRankedSoloWins.Content = WinrateMessage(soloWinrate);
                ProfileRankedFlexWins.Content = WinrateMessage(flexWinrate);
            });

            DateTime timeEnd = DateTime.Now;

            Trace.WriteLine($"Time taken ({matchCheckRange} range): {timeEnd - timeStart}");
            */
        }

        private void HideProfile()
        {
            Dispatcher.Invoke(() =>
            {
                LeftGrid.BeginAnimation(OpacityProperty, new DoubleAnimation()
                {
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.5)
                });
            });
            
        }

        private void ShowSummonerError()
        {
            if (Input_ShowingError) return;

            Dispatcher.Invoke(() =>
            {
                SummonerErrorMessageLabel.Visibility = Visibility.Visible;

                var opacityAnim = new DoubleAnimation()
                {
                    To = 1,
                    Duration = TimeSpan.FromSeconds(Input_AnimationDuration)
                };

                SummonerErrorMessageLabel.BeginAnimation(OpacityProperty, opacityAnim);
            });

            Input_ShowingError = true;
        }

        private void HideSummonerError()
        {
            if (!Input_ShowingError) return;

            Dispatcher.Invoke(() =>
            {
                var opacityAnim = new DoubleAnimation()
                {
                    To = 0,
                    Duration = TimeSpan.FromSeconds(Input_AnimationDuration)
                };

                SummonerErrorMessageLabel.BeginAnimation(OpacityProperty, opacityAnim);

                Task.Delay((int)(1000 * Input_AnimationDuration)).ContinueWith((_) => Dispatcher.Invoke(() =>
                {
                    SummonerErrorMessageLabel.Visibility = Visibility.Hidden;
                }));
            });

            Input_ShowingError = false;
        }

        private string FirstCapitalLetter(string str)
        {
            return str.Substring(0, 1).ToUpper() + str.Substring(1).ToLower();
        }

        private string WinrateMessage(ConcurrentDictionary<string, int> winrateObj)
        {
            int wins = winrateObj["wins"];
            int losses = winrateObj["losses"];

            if (wins == 0 && losses == 0) return "0W / 0L (0%)";

            return $"{wins}W / {losses}L ({wins*100/(wins+losses)}%)";
        }

        private string TierMessage(JObject RankedQueueInfo)
        {
            string tier = RankedQueueInfo["tier"].ToString();
            
            if (tier == "") return "Unranked";

            string div = RankedQueueInfo["division"].ToString();
            if (div == "NA") div = "";
            else div = " " + div;

            return $"{FirstCapitalLetter(tier)}{div} {RankedQueueInfo["leaguePoints"].ToString()}LP";
        }

        /*
        private void WinrateAnalyzerThread(string puuid, int begIndex, int endIndex, DateTime splitStartDate, DateTime splitEndDate, ref ConcurrentDictionary<string, int> soloWinrate, ref ConcurrentDictionary<string, int> flexWinrate, ref bool stopper, ref int completionCount)
        {
            var matches = GetMatchHistory(puuid, begIndex, endIndex);

            DateTime matchDate = matches.Last().Value<DateTime>("gameCreationDate");

            if (matchDate < splitStartDate)
            {
                stopper = true;
                goto end;
            }

            foreach (var match in matches)
            {
                if (match.Value<int>("gameDuration") <= 180) continue; //remake

                QueueID queueId = (QueueID)match.Value<int>("queueId");

                if (queueId != QueueID.RankedSolo && queueId != QueueID.RankedFlex) continue; // not ranked

                matchDate = match.Value<DateTime>("gameCreationDate");

                if (matchDate > splitEndDate) continue; // match past ranked split

                if (matchDate < splitStartDate) break;

                if (queueId == QueueID.RankedSolo) MatchWinRateHandler(match, ref soloWinrate);
                else MatchWinRateHandler(match, ref flexWinrate);
            };

            end:

            completionCount++;
        }
        */

        private List<JObject> GetMatchHistory(string puuid, int begIndex, int endIndex)
        {
            var mhResponse = lcu.request(requestMethod.GET, $"/lol-match-history/v1/products/lol/{puuid}/matches?begIndex={begIndex}&endIndex={endIndex}").Result;

            var mhData = JObject.Parse(mhResponse.Content.ReadAsStringAsync().Result);

            List<JObject> matchHistory = mhData["games"]["games"].ToObject<List<JObject>>();

            for (int i = 0; i < matchHistory.Count; i++)
            {
                var match = matchHistory[i];

                var matchResponse = lcu.request(requestMethod.GET, $"/lol-match-history/v1/games/{match["gameId"]}").Result;

                var matchData = JObject.Parse(matchResponse.Content.ReadAsStringAsync().Result);

                matchHistory[i] = matchData;
            }

            return matchHistory;
        }
    }
}
