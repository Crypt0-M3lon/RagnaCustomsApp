﻿using Newtonsoft.Json;
using RagnaCustoms.Class;
using RagnaCustoms.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

namespace RagnaCustoms
{

    public partial class Main : Form
    {
        public Main()
        {
            //Icon = new Icon("Logo.ico");
            InitializeComponent();
            CreateContextMenu();
            Text = "RagnaCustoms.com";

            if (Program.Settings.AutoDownload)
            {
                checkBox1.Checked = true;
            }
            else
            {
                checkBox1.Checked = false;
            }

            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 20000;
            timer.Tick += delegate (object sender, EventArgs args)
            {
                computeScore();
                timer.Stop();
                timer.Start();
            };
            timer.Start();
        }
        void menuItem_Click(object sender, EventArgs e)

        {

            ToolStripItem menuItem = (ToolStripItem)sender;

            if (menuItem.Name == "Exit")
            {
                computeScore();
                Application.Exit();
            }
            if(menuItem.Name == "Open")
            {
                Show();
                this.WindowState = FormWindowState.Normal;
                notifyIcon.Visible = false;
            }

        }
       

        private void CreateContextMenu()

        {

            ContextMenuStrip menuStrip = new ContextMenuStrip();

            ToolStripMenuItem menuItem = new ToolStripMenuItem("Exit");
            ToolStripMenuItem menuItem2 = new ToolStripMenuItem("Open");



            menuItem.Click += new EventHandler(menuItem_Click);

            menuItem.Name = "Exit";


            menuItem2.Click += new EventHandler(menuItem_Click);

            menuItem2.Name = "Open";

            menuStrip.Items.Add(menuItem2);
            menuStrip.Items.Add(menuItem);

            notifyIcon.ContextMenuStrip = menuStrip;

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            Program.Settings.AutoDownload = checkBox1.Checked;
            Program.Settings.Save();
#if !DEBUG
            if (checkBox1.Checked)
            {
                Register.AddRegistryKeys();
            }
            else
            {
                Register.RemoveRegistryKeys();
            }
#endif
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var list = new List<Language>()
            {
                new Language() { Text = "Français", Code = "fr-FR" },
                new Language() { Text = "English", Code = "en-US" },
                new Language() { Text = "Deutsch", Code = "de-DE" }
            };

            setTexts();
            prefixe = Program.Settings.Prefix;
            prefix.Text = Program.Settings.Prefix;
            checkBox1.Checked = Program.Settings.AutoDownload;
            checkBox2.Checked = Program.Settings.AutoClose;
            twitchChannel.Text = Program.Settings.TwitchChannel;
            twitchOAuth.Text = Program.Settings.AuthTmi;
            bot_enabled.Checked = Program.Settings.TwitchBotEnabled;
            textBox2.Text = Program.Settings.ScoringApiKey;
            comboBox1.DataSource = list;
            refreshApplication();

            comboBox1.ValueMember = "Code";
            comboBox1.DisplayMember = "Text";
            comboBox1.SelectedIndex = list.IndexOf(list.FirstOrDefault(x => x.Code == Program.Settings.Culture));
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo("https://www.twitch.tv/rhokapa/about");
            Process.Start(sInfo);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var code = ((Language)comboBox1.SelectedItem).Code;
            if (code != Program.Settings.Culture)
            {
                Program.Settings.Culture = code;
                Program.Settings.Save();
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(Program.Settings.Culture);
                setTexts();
            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            Program.Settings.AutoClose = checkBox2.Checked;
            Program.Settings.Save();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            var api = $"https://ragnacustoms.com/api/search/{textBox1.Text}";
            using (var webClient = new System.Net.WebClient())
            {
                var json = webClient.DownloadString(api);
                var stuff = JsonConvert.DeserializeObject<SearchResponse>(json);

                comboBox3.DataSource = stuff.Results;
                comboBox3.ValueMember = "Id";
                comboBox3.DisplayMember = "fullTitle";
            }
        }

        private void setTexts()
        {
            versionLabel.Text = Program.Version;
            checkBox1.Text = Resources.app.strings.Enable_OneClick_download_;
            checkBox2.Text = Resources.app.strings.Enable_auto_close_after_download_;
            searchLabel.Text = Resources.app.strings.Search;
            label1.Text = Resources.app.strings.Language;
            downloadButton.Text = Resources.app.strings.Download;
            helptwitchtmi.Text = Resources.app.strings.Get_your_Twitch_Chat_OAuth_Password_on;
            label2.Text = Resources.app.strings.Twitch_Chat_OAuth_password;
            label3.Text = Resources.app.strings.Your_Twitch_channel;
            bot_enabled.Text = Resources.app.strings.Enabled__;
            refreshApps.Text = Resources.app.strings.Refresh;
        }


        public void StartDownload(string song)
        {
            Process p = new Process();
            p.StartInfo.FileName = Application.ExecutablePath;
            p.StartInfo.Arguments = $"--install ragnac://install/{song}";
            p.Start();
        }


        private void button1_Click(object sender, EventArgs e)
        {
            if (comboBox3.SelectedValue != null)
            {
                StartDownload(comboBox3.SelectedValue.ToString());
            }
            else
            {
                MessageBox.Show(Resources.app.strings.Error_1, "RagnaCustoms.com", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo("https://twitchapps.com/tmi/");
            Process.Start(sInfo);
        }

        private TwitchClient TwitchClient;
        private JoinedChannel joinedChannel;

        public Process RagnarockApp { get; set; }
        public TwitchPubSub client { get; private set; }

        private void bot_enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (bot_enabled.Checked && (String.IsNullOrEmpty(twitchOAuth.Text.ToString()) || String.IsNullOrEmpty(twitchChannel.Text)))
            {
                MessageBox.Show(Resources.app.strings.Error_2, "RagnaCustoms.com", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                bot_enabled.Checked = false;
                Program.Settings.TwitchBotEnabled = bot_enabled.Checked;
                Program.Settings.Save();
                return;
            }
            if (bot_enabled.Checked)
            {

                var credentials = new ConnectionCredentials(twitchChannel.Text, twitchOAuth.Text);
                var clientOptions = new ClientOptions
                {
                    MessagesAllowedInPeriod = 750,
                    ThrottlingPeriod = TimeSpan.FromSeconds(30)
                };

                client = new TwitchPubSub();

                client.ListenToRewards(twitchOAuth.Text);
                client.OnRewardRedeemed += OnRewardRedeemed;
                client.Connect();

                TwitchClient = new TwitchClient(new WebSocketClient(clientOptions));


                TwitchClient.Initialize(credentials, channel: twitchChannel.Text);

                TwitchClient.OnMessageReceived += OnMessageReceived;

                TwitchClient.OnConnected += OnConnected;
                TwitchClient.Connect();

            }
            Program.Settings.TwitchBotEnabled = bot_enabled.Checked;
            Program.Settings.Save();

        }

        private void OnRewardRedeemed(object sender, OnRewardRedeemedArgs e)
        {
            if (e != null)
            {

            }
        }
        public string prefixe { get; set; }
        private void OnConnected(object sender, OnConnectedArgs e)
        {
            joinedChannel = TwitchClient.GetJoinedChannel(twitchChannel.Text);
            TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.bot_connected__}");
            //TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.app.strings.WelcomeBot}");
        }
        public bool QueueIsOpen = true;

        private void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string[] command = e.ChatMessage.Message.Split(' ');

            var part1 = command[0];
            if (part1 != "!rc")
            {
                return;
            }

            if (command.Length < 2)
            {
                TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Error_on_command_}");
                return;
            }

            var part2 = command[1];

            switch (part1)
            {
                case "!rc":
                    #region specific commands
                    switch (part2)
                    {
                        case "open":
                            if (e.ChatMessage.UserType == TwitchLib.Client.Enums.UserType.Viewer)
                            {
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Sorry_only_moderator_can_do_that_}");
                                return;
                            }
                            if (!QueueIsOpen)
                            {
                                QueueIsOpen = true;
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Queue_is_now_open}");
                            }
                            else
                            {
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Queue_is_already_open}");
                            }
                            return;
                        case "close":
                            if (e.ChatMessage.UserType == TwitchLib.Client.Enums.UserType.Viewer)
                            {
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Sorry_only_moderator_can_do_that_}");
                                return;
                            }
                            if (!QueueIsOpen)
                            {
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}Queue is already closed");
                            }
                            else
                            {
                                QueueIsOpen = false;
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}Queue is now closed");
                            }
                            return;
                        case "shift":
                        case "done":
                            if (e.ChatMessage.UserType == TwitchLib.Client.Enums.UserType.Viewer)
                            {
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}Sorry only moderator can do that.");
                                return;
                            }
                            try
                            {
                                RemoveAtSongRequestInList(0);
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}Song removed");

                                var sog = songRequests.Rows[0].Cells["Song"].Value.ToString();
                                if (sog != null)
                                {
                                    TwitchClient.SendMessage(joinedChannel, $"{prefixe}Next song : {sog} ");
                                }
                                else
                                {
                                    TwitchClient.SendMessage(joinedChannel, $"{prefixe}End of the queue");
                                }
                            }
                            catch (Exception O_o)
                            {
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}No More song to remove");
                            }
                            return;
                        case "next":
                            var song = songRequests.Rows[0].Cells["Song"].Value.ToString();
                            if (song != null)
                            {
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}Next song : {song} ");
                            }
                            else
                            {
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}End of the queue");
                            }
                            return;

                        case "queue":
                            var songs = new List<string>();
                            var counter = 0;
                            var concatStr = "";
                            for (var i = 0; songRequests.Rows.Count - 1 > i; i++)
                            {
                                var songStr = songRequests.Rows[i].Cells["Song"].Value.ToString();
                                if (concatStr.Length + songStr.Length > 500)
                                {
                                    songs.Add(concatStr);
                                    concatStr = songStr;
                                    counter = songStr.Length;
                                }
                                else
                                {
                                    if (!String.IsNullOrEmpty(concatStr))
                                    {
                                        concatStr += " // ";
                                    }
                                    concatStr += songStr;
                                }
                            }
                            songs.Add(concatStr);

                            TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Next_songs_are__}");
                            Thread.Sleep(400);
                            foreach (var songMessage in songs)
                            {
                                TwitchClient.SendMessage(joinedChannel, $"{prefixe}{songMessage}");
                            }
                            return;
                        case "cancel":

                            for (var i = songRequests.Rows.Count - 2; 0 <= i; i--)
                            {
                                if (songRequests.Rows[i].Cells["Viewer"].Value.ToString() == e.ChatMessage.Username)
                                {
                                    var sng = songRequests.Rows[i].Cells["Song"].Value.ToString();
                                    try
                                    {
                                        RemoveAtSongRequestInList(i);
                                        TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Song_Cancelled__} {sng} ");
                                    }
                                    catch (Exception o_O)
                                    {
                                        TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.No_More_song_to_remove}");
                                    }
                                    return;
                                }
                            }
                            return;
                        case "dev":
                            TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.My_dev_is_Rhokapa}");
                            return;

                        case "version":
                        case "v":
                            TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.I_m_version} {Program.Version}");
                            return;

                        case "cam":
                            //try
                            //{
                            int camera;
                            if (int.TryParse(command[2], out camera))
                            {
                                if (camera >= 1 && camera <= 5)
                                {
                                    ChangeCamera(command[2]);
                                    TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Camera_changed}");
                                }
                                else
                                {
                                    TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Camera_error}");
                                }
                            }
                            //}
                            //catch (Exception o_O)
                            //{
                            //    TwitchClient.SendMessage(joinedChannel, $"Camera error");
                            //}

                            return;
                        case "how-to":
                        case "ht":
                            TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Go_to_https___ragnacustoms_com__click_on_the_twitch_button_to_copy__rc__songid__and_paste_it_here}");

                            return;
                        case "help":
                            TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Help_1_2____rc_cam__numero_cam__1_5____switch_camera____rc_dev__information_about_dev____rc_help__this_command____rc__song_id___download_the_map____rc_cancel__remove_last_song_you_request_}");
                            TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Help_2_2____rc_open__open_queue____rc_close__close_queue____rc_shift__remove_first_song_in_list____rc_queue__list_of_songs_not_played____rc_next__next_song_to_play____rc_version__to_know_current_version_}");
                            return;
                    }
                    #endregion
                    if (!QueueIsOpen)
                    {
                        TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Queue_is_closed}");
                        return;
                    }

                    var s = GetSongInfo(part2);
                    if (s != null)
                    {
                        TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.app.strings.RequestInfo} : {s.fullTitle}, {Resources.app.strings.Mapped_by} : {s.Mapper}, {Resources.twitchBot.strings.asked_by} @{e.ChatMessage.Username}");
#if !DEBUG  
                        StartDownload(s.Id.ToString());
#endif
                        AddSongRequestToList(s, e.ChatMessage.Username);
                        TwitchClient.SendMessage(joinedChannel, $"{prefixe}{Resources.twitchBot.strings.Ready__}");
                    }
                    else
                    {
                        TwitchClient.SendMessage(joinedChannel, $"{prefixe}@{e.ChatMessage.Username} {Resources.app.strings.Map_not_found}");
                    }
                    break;
            }
        }

        private void AddSongRequestToList(Song song, string viewer)
        {
            songRequests.Invoke(new MethodInvoker(delegate
             {
                 songRequests.Rows.Add(song.Name, song.Author, viewer, song.Id);
             }
             ));
        }
        private void RemoveAtSongRequestInList(int i)
        {
            songRequests.Invoke(new MethodInvoker(delegate
            {
                songRequests.Rows.RemoveAt(i);
            }
             ));
        }


        private Song GetSongInfo(string songId)
        {
            try
            {
                using (var webClient = new System.Net.WebClient())
                {
                    var json = webClient.DownloadString("https://ragnacustoms.com/api/song/" + songId);
                    Song stuff = JsonConvert.DeserializeObject<Song>(json);
                    //debug_console.Items.Add($"Début de la récuperation de {stuff.title}");
                    return stuff;
                }
            }
            catch (WebException o_O)
            {
                return null;
            }
        }


        private void twitchOAuth_TextChanged(object sender, EventArgs e)
        {
            bot_enabled.Checked = false;
            Program.Settings.AuthTmi = twitchOAuth.Text;
            Program.Settings.Save();

        }

        private void twitchChannel_TextChanged(object sender, EventArgs e)
        {
            bot_enabled.Checked = false;
            Program.Settings.TwitchChannel = twitchChannel.Text;
            Program.Settings.Save();
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        public void refreshApplication()
        {
            RagnarockApp = Process.GetProcesses().FirstOrDefault(x => x.ProcessName.Contains("Ragnarock-Win64"));
            if (RagnarockApp == null)
            {
                ragnarockApp.Text = Resources.app.strings.Ragnarock_application_not_found_please_refresh;
            }
            else
            {
                ragnarockApp.Text = Resources.app.strings.Ragnarock_application_found;
            }
        }

        private void refreshApps_Click(object sender, EventArgs e)
        {
            refreshApplication();
        }

        private void ChangeCamera(string num)
        {
            if (RagnarockApp != null)
            {
                ProcessHelper.SetFocusToExternalApp(RagnarockApp.ProcessName);

                FieldInfo info = typeof(SendKeys).GetField("keywords", BindingFlags.Static | BindingFlags.NonPublic);
                Thread.Sleep(1000);
                Array oldKeys = (Array)info.GetValue(null);
                Type elementType = oldKeys.GetType().GetElementType();
                Array newKeys = Array.CreateInstance(elementType, oldKeys.Length + 10);
                Array.Copy(oldKeys, newKeys, oldKeys.Length);
                for (int i = 0; i < 10; i++)
                {
                    var newItem = Activator.CreateInstance(elementType, "NUM" + i, (int)Keys.NumPad0 + i);
                    newKeys.SetValue(newItem, oldKeys.Length + i);
                }
                info.SetValue(null, newKeys);

                SendKeys.SendWait("{NUM" + num + "}");

            }
        }

        private void songRequests_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void tabPage2_Click(object sender, EventArgs e)
        {

        }

        private void doneButton_Click(object sender, EventArgs e)
        {

        }

        #region score
        public void computeScore()
        {
            var log = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/Ragnarock/Saved/Logs/Ragnarock.log";
            if (File.Exists(log + ".cpy"))
            {
                File.Delete(log + ".cpy");
            }
            File.Copy(log, log + ".cpy");

            IEnumerable<string> lines = File.ReadAllLines(log + ".cpy").ToList();
            File.Delete(log + ".cpy");
            var songs = new Dictionary<string, float>();
            var folders = new Dictionary<string, string>();
            var current = "";
            var current_level = "";
            for (var i = 0; lines.Count() > i; i++)
            {
                var line = lines.ElementAt(i);              
                if (line.Contains("LogTemp: Loading song"))
                {
                    current = line.PregReplace("^(.*)LogTemp: Loading song (.*)/([a-zA-Z0-9]{0,}.ogg)(.*)$", "$2");
                    current_level += "_" + current;
                    continue;
                }
                if (line.Contains("LogTemp: Warning: Song level"))
                {
                    current_level = line.PregReplace("^(.*) ([0-9]{1,2})$", "$2");
                    continue;
                }
                if (line.Contains("raw distance"))
                {
                    var content = line.PregReplace("^(.*)raw distance = (.*) and (.*)$", "$2").Replace('.', ',');
                    var score = float.Parse(content, CultureInfo.InvariantCulture.NumberFormat);
                    if (songs.ContainsKey(current_level))
                    {
                        if (songs[current_level] < score)
                        {
                            songs[current_level] = score;
                        }
                    }
                    else
                    {
                        songs.Add(current_level, score);
                        folders.Add(current_level, current);
                    }
                    continue;
                }
            }
            SendScore(songs, folders);
        }

        private void SendScore(Dictionary<string, float> songs, Dictionary<string, string> folders)
        {
            if (string.IsNullOrEmpty(textBox2.Text)) return;

            var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://ragnacustoms.com/api/score");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            var scores = new Score();
            scores.ApiKey = textBox2.Text;
            foreach (var song in songs)
            {
                var info = folders[song.Key] + "/Info.dat";
                if (!File.Exists(info))
                {
                    info = folders[song.Key] + "/info.dat";
                }
                var level = song.Key.Split('_').First();
                var hash = CalculateMD5(info);
                scores.Scores.Add(new SubScore() { HashInfo = hash, Score = (song.Value / 100).ToString(), Level = level });
            }
            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = new JavaScriptSerializer().Serialize(scores);
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
            }

        }

        #endregion

        private void apiKey_Click(object sender, EventArgs e)
        {
            computeScore();
        }

        static string CalculateMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                try
                {
                    using (var stream = File.OpenRead(filename))
                    {
                        var hash = md5.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
                catch (Exception e)
                {
                    return "";
                }
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            Program.Settings.ScoringApiKey = textBox2.Text;
            Program.Settings.Save();
        }

        private void prefix_TextChanged(object sender, EventArgs e)
        {
            prefixe = prefix.Text;
            Program.Settings.Prefix = prefix.Text;
            Program.Settings.Save();
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            this.WindowState = FormWindowState.Normal;
            notifyIcon.Visible = false;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {

            if (this.WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon.Visible = true;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            computeScore();
        }
    }
}
