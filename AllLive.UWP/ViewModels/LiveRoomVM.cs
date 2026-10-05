using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Windows.UI.Core;
using AllLive.UWP.Helper;
using System.Windows.Input;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.ApplicationModel.Core;
using System.ComponentModel;
using System.Timers;
using System.Collections.Concurrent;

namespace AllLive.UWP.ViewModels
{
    public class LiveRoomVM : BaseViewModel
    {
        SettingVM settingVM;
        public event EventHandler<string> ChangedPlayUrl;
        public event EventHandler<LiveMessage> AddDanmaku;

        // 弹幕消息队列和批量处理
        private readonly ConcurrentQueue<KeyValuePair<int, LiveMessage>> _messageQueue = new ConcurrentQueue<KeyValuePair<int, LiveMessage>>();
        private Timer _messageProcessTimer;
        private volatile bool _isStopped;
        private int _loadGeneration;
        private int _playUrlGeneration;
        private Task _danmakuStopTask = Task.CompletedTask;
        private const int MESSAGE_BATCH_INTERVAL = 100; // 100ms 处理一批
        private const int MESSAGE_BATCH_SIZE = 10; // 每批最多处理10条

        public LiveRoomVM(SettingVM settingVM)
        {
            this.settingVM = settingVM;
            Messages = new ObservableCollection<LiveMessage>();
            SuperChatMessages = new ObservableCollection<SuperChatItem>();
            MessageCleanCount = SettingHelper.GetValue<int>(SettingHelper.LiveDanmaku.DANMU_CLEAN_COUNT, 200);
            KeepSC = SettingHelper.GetValue<bool>(SettingHelper.LiveDanmaku.KEEP_SUPER_CHAT, true);
            AddFavoriteCommand = new RelayCommand(AddFavorite);
            RemoveFavoriteCommand = new RelayCommand(RemoveFavorite);

            EnsureMessageTimer();
        }

        private void EnsureMessageTimer()
        {
            if (_messageProcessTimer != null) return;
            _messageProcessTimer = new Timer(MESSAGE_BATCH_INTERVAL);
            _messageProcessTimer.Elapsed += ProcessMessageQueue;
            _messageProcessTimer.AutoReset = true;
        }

        private bool IsCurrentLoad(int generation)
        {
            return !_isStopped && generation == System.Threading.Volatile.Read(ref _loadGeneration);
        }

        private Task StopDanmaku()
        {
            var previous = LiveDanmaku;
            LiveDanmaku = null;
            if (previous != null)
            {
                previous.NewMessage -= LiveDanmaku_NewMessage;
                previous.OnClose -= LiveDanmaku_OnClose;
                _danmakuStopTask = previous.Stop();
            }
            return _danmakuStopTask;
        }
        public ICommand AddFavoriteCommand { get; set; }
        public ICommand RemoveFavoriteCommand { get; set; }
        public int MessageCleanCount { get; set; } = 200;

        /// <summary>
        /// 保留SC
        /// </summary>
        private bool _keepSC;

        public bool KeepSC
        {
            get { return _keepSC; }
            set { _keepSC = value; DoPropertyChanged("KeepSC"); }
        }



        ILiveSite Site;
        ILiveDanmaku LiveDanmaku;

        private string _siteLogo = "ms-appx:///Assets/Placeholder/Placeholder1x1.png";

        public string SiteLogo
        {
            get { return _siteLogo; }
            set { _siteLogo = value; DoPropertyChanged("SiteLogo"); }
        }

        private string _siteName;
        public string SiteName
        {
            get { return _siteName; }
            set { _siteName = value; DoPropertyChanged("SiteName"); }
        }

        private bool _isFavorite = false;
        public bool IsFavorite
        {
            get { return _isFavorite; }
            set { _isFavorite = value; DoPropertyChanged("IsFavorite"); }
        }

        private long? FavoriteID { get; set; }

        object RoomId;
        public LiveRoomDetail detail { get; set; }

        private long _Online = 0;
        public long Online
        {
            get { return _Online; }
            set { _Online = value; DoPropertyChanged("Online"); }
        }
        private string _RoomID;

        public string RoomID
        {
            get { return _RoomID; }
            set { _RoomID = value; DoPropertyChanged("RoomID"); }
        }


        private string _photo = "ms-appx:///Assets/Placeholder/Placeholder1x1.png";
        public string Photo
        {
            get { return _photo; }
            set { _photo = value; DoPropertyChanged("Photo"); }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
            set { _name = value; DoPropertyChanged("Name"); }
        }
        private string _title;
        public string Title
        {
            get { return _title; }
            set { _title = value; DoPropertyChanged("Title"); }
        }
        private bool _living = true;

        public bool Living
        {
            get { return _living; }
            set { _living = value; DoPropertyChanged("Living"); }
        }


        private List<LivePlayQuality> qualities;
        public List<LivePlayQuality> Qualities
        {
            get { return qualities; }
            set { qualities = value; DoPropertyChanged("Qualities"); }
        }

        private LivePlayQuality currentQuality;
        public LivePlayQuality CurrentQuality
        {
            get { return currentQuality; }
            set
            {
                if (value == null || value == currentQuality)
                {
                    return;
                }
                currentQuality = value;
                DoPropertyChanged("CurrentQuality");
                LoadPlayUrl();
            }

        }

        private List<PlayurlLine> lines;
        public List<PlayurlLine> Lines
        {
            get { return lines; }
            set { lines = value; DoPropertyChanged("Lines"); }
        }


        private PlayurlLine currentLine;
        public PlayurlLine CurrentLine
        {
            get { return currentLine; }
            set
            {
                if (value == null || value == currentLine)
                {
                    return;
                }
                currentLine = value;
                DoPropertyChanged("CurrentLine");
                ChangedPlayUrl?.Invoke(this, value.Url);
            }

        }

        public ObservableCollection<LiveMessage> Messages { get; set; }
        public ObservableCollection<SuperChatItem> SuperChatMessages { get; set; }

        public List<SettingsItem<double>> DanmakuOpacityItems { get; } = new List<SettingsItem<double>>()
        {
            new SettingsItem<double>(){ Name="100%",Value=1},
            new SettingsItem<double>(){ Name="90%",Value=0.9},
            new SettingsItem<double>(){ Name="80%",Value=0.8},
            new SettingsItem<double>(){ Name="70%",Value=0.7},
            new SettingsItem<double>(){ Name="60%",Value=0.6},
            new SettingsItem<double>(){ Name="50%",Value=0.5},
            new SettingsItem<double>(){ Name="40%",Value=0.4},
            new SettingsItem<double>(){ Name="30%",Value=0.3},
            new SettingsItem<double>(){ Name="20%",Value=0.2},
            new SettingsItem<double>(){ Name="10%",Value=0.1},
        };
        public List<SettingsItem<double>> DanmakuDiaplayAreaItems { get; } = new List<SettingsItem<double>>()
        {
            new SettingsItem<double>(){ Name="100%",Value=1},
            new SettingsItem<double>(){ Name="75%",Value=0.75},
            new SettingsItem<double>(){ Name="50%",Value=0.5},
            new SettingsItem<double>(){ Name="25%",Value=0.25},
        };
        public List<SettingsItem<int>> DanmakuSpeedItems { get; } = new List<SettingsItem<int>>()
        {
            new SettingsItem<int>(){ Name="极快",Value=2},
            new SettingsItem<int>(){ Name="很快",Value=4},
            new SettingsItem<int>(){ Name="较快",Value=6},
            new SettingsItem<int>(){ Name="快",Value=8},
            new SettingsItem<int>(){ Name="正常",Value=10},
            new SettingsItem<int>(){ Name="慢",Value=12},
            new SettingsItem<int>(){ Name="较慢",Value=14},
            new SettingsItem<int>(){ Name="很慢",Value=16},
            new SettingsItem<int>(){ Name="极慢",Value=18},
        };
        public List<SettingsItem<double>> DnamakuFontZoomItems { get; } = new List<SettingsItem<double>>()
        {
            new SettingsItem<double>(){ Name="极小",Value=0.2},
            new SettingsItem<double>(){ Name="很小",Value=0.6},
            new SettingsItem<double>(){ Name="较小",Value=0.8},
            new SettingsItem<double>(){ Name="小",Value=0.9},
            new SettingsItem<double>(){ Name="正常",Value=1.0},
            new SettingsItem<double>(){ Name="大",Value=1.1},
            new SettingsItem<double>(){ Name="较大",Value=1.2},
            new SettingsItem<double>(){ Name="很大",Value=1.4},
            new SettingsItem<double>(){ Name="极大",Value=1.8},
            new SettingsItem<double>(){ Name="特大",Value=2.0},
        };


        public async void LoadData(ILiveSite site, object roomId)
        {
            var generation = System.Threading.Interlocked.Increment(ref _loadGeneration);
            System.Threading.Interlocked.Increment(ref _playUrlGeneration);
            _isStopped = false;
            try
            {
                LogHelper.Log("[LiveRoomVM.LoadData] 开始加载房间数据", LogType.DEBUG);
                Loading = true;
                var previousStop = StopDanmaku();
                while (_messageQueue.TryDequeue(out _)) { }
                await previousStop;
                if (!IsCurrentLoad(generation)) return;
                Site = site;
                // Settings are loaded before the room on initial navigation. Rebind the
                // SC timer to this load, and recreate it after Stop/reload as needed.
                SetSCTimer();
                currentQuality = null;

                RoomId = roomId;
                var result = await site.GetRoomDetail(roomId);
                if (!IsCurrentLoad(generation)) return;
                detail = result;
                RoomID = result.RoomID;

                Online = result.Online;
                Title = result.Title;
                SetWindowTitle();

                Name = result.UserName;
                MessageCenter.ChangeTitle(Title + " - " + Name, Site);
                if (!string.IsNullOrEmpty(result.UserAvatar))
                {
                    Photo = result.UserAvatar;
                }
                Living = result.Status;
                //加载SC
                LoadSuperChat();
                //检查收藏情况
                FavoriteID = DatabaseHelper.CheckFavorite(RoomID, Site.Name);
                IsFavorite = FavoriteID != null;

                LiveDanmaku = Site.GetDanmaku();
                Messages.Add(new LiveMessage()
                {
                    Type = LiveMessageType.Chat,
                    UserName = "系统",
                    Message = "开始接收弹幕"
                });

                LiveDanmaku.NewMessage += LiveDanmaku_NewMessage;
                LiveDanmaku.OnClose += LiveDanmaku_OnClose;
                EnsureMessageTimer();
                if (!_messageProcessTimer.Enabled)
                {
                    _messageProcessTimer.Start();
                }
                await LiveDanmaku.Start(result.DanmakuData);
                if (!IsCurrentLoad(generation)) return;
                if (result.Status)
                {
                    var qualities = await site.GetPlayQuality(result);
                    if (!IsCurrentLoad(generation)) return;
                    if (Site.Name == "虎牙直播")
                    {
                        //HDR无法播放
                        qualities = qualities.Where(x => !x.Quality.Contains("HDR")).ToList();
                    }
                    Qualities = qualities;
                    if (Qualities != null && Qualities.Count > 0)
                    {
                        CurrentQuality = Qualities[0];
                    }
                    // var u = await Site.GetPlayUrls(result, q[0]);
                    //ChangedPlayUrl?.Invoke(this, u[0]);
                }
                DatabaseHelper.AddHistory(new Models.HistoryItem()
                {
                    Photo = Photo,
                    RoomID = RoomID,
                    SiteName = Site.Name,
                    UserName = Name
                });

            }
            catch (Exception ex)
            {
                if (IsCurrentLoad(generation)) HandleError(ex);
            }
            finally
            {
                if (IsCurrentLoad(generation)) Loading = false;
            }
        }

        Timer scTimer;
        public void SetSCTimer()
        {
            var generation = _loadGeneration;
            KeepSC = SettingHelper.GetValue<bool>(SettingHelper.LiveDanmaku.KEEP_SUPER_CHAT, true);
            if (KeepSC)
            {
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     if (!IsCurrentLoad(generation)) return;
                     foreach (var item in SuperChatMessages)
                     {
                         item.ShowCountdown = false;
                     }
                 });
                scTimer?.Stop();
                scTimer?.Dispose();
                scTimer = null;
            }
            else
            {
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (!IsCurrentLoad(generation)) return;
                    foreach (var item in SuperChatMessages)
                    {
                        item.ShowCountdown = true;
                        item.CountdownTime = Convert.ToInt32(item.EndTime.Subtract(DateTime.Now).TotalSeconds);
                    }
                });
                scTimer?.Stop();
                scTimer?.Dispose();
                var countdownTimer = new Timer(1000);
                scTimer = countdownTimer;
                countdownTimer.Elapsed += (s, e) =>
                {
                    _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (!IsCurrentLoad(generation) || !ReferenceEquals(countdownTimer, scTimer)) return;
                        for (var i = SuperChatMessages.Count - 1; i >= 0; i--)
                        {
                            var item = SuperChatMessages[i];
                            item.CountdownTime--;
                            if (item.CountdownTime <= 0)
                            {
                                SuperChatMessages.RemoveAt(i);
                            }
                        }
                    });

                };
                scTimer.Start();
            }
        }

        private void SetWindowTitle()
        {
            if (SettingHelper.GetValue(SettingHelper.NEW_WINDOW_LIVEROOM, false))
            {
                Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().Title = $"{Title} - {SiteName}";
            }
        }

        private void AddFavorite()
        {
            if (Site == null || RoomID == null || RoomID == "0" || RoomID == "") return;
            try
            {
                DatabaseHelper.AddFavorite(new Models.FavoriteItem()
                {
                    Photo = Photo,
                    RoomID = RoomID,
                    SiteName = Site.Name,
                    UserName = Name
                });
                FavoriteID = DatabaseHelper.CheckFavorite(RoomID, Site.Name);
                IsFavorite = true;
                MessageCenter.UpdateFavorite();
            }
            catch (Exception ex)
            {
                HandleError(ex, "关注失败");
            }
        }
        private void RemoveFavorite()
        {
            if (FavoriteID == null)
            {
                return;
            }
            try
            {
                DatabaseHelper.DeleteFavorite(FavoriteID.Value);
                FavoriteID = null;
                IsFavorite = false;
                MessageCenter.UpdateFavorite();
            }
            catch (Exception ex)
            {
                HandleError(ex, "取消关注失败");
            }
        }

        public async void LoadPlayUrl()
        {
            var generation = _loadGeneration;
            var request = System.Threading.Interlocked.Increment(ref _playUrlGeneration);
            var site = Site;
            var room = detail;
            var quality = CurrentQuality;
            if (!IsCurrentLoad(generation) || site == null || room == null || quality == null) return;
            try
            {
                // 断流重连时尽量保持原线路索引（对齐 pure_live changeLine / refresh）
                int preferIndex = 0;
                if (Lines != null && CurrentLine != null)
                {
                    var idx = Lines.IndexOf(CurrentLine);
                    if (idx >= 0) preferIndex = idx;
                }

                var data = await site.GetPlayUrls(room, quality);
                if (!IsCurrentLoad(generation) || request != _playUrlGeneration) return;
                if (data == null || data.Count == 0)
                {
                    Utils.ShowMessageToast("加载播放地址失败");
                    return;
                }
                List<PlayurlLine> ls = new List<PlayurlLine>();
                for (int i = 0; i < data.Count; i++)
                {
                    ls.Add(new PlayurlLine()
                    {
                        Name = $"线路{i + 1}",
                        Url = data[i]
                    });
                }

                Lines = ls;
                if (preferIndex >= ls.Count) preferIndex = 0;
                // 强制触发播放：先清空再赋值，避免引用相等导致不刷新
                currentLine = null;
                CurrentLine = Lines[preferIndex];
            }
            catch (Exception ex)
            {
                if (!IsCurrentLoad(generation) || request != _playUrlGeneration) return;
                LogHelper.Log("[LiveRoomVM.LoadPlayUrl] 加载播放地址失败", LogType.ERROR, ex);
                Utils.ShowMessageToast("加载播放地址失败");
            }




        }

        public async void LoadSuperChat()
        {
            var generation = _loadGeneration;
            var site = Site;
            var roomId = RoomID;
            if (!IsCurrentLoad(generation) || site == null) return;
            try
            {
                var data = await site.GetSuperChatMessages(roomId);
                if (!IsCurrentLoad(generation)) return;
                if (data.Count > 0)
                {
                    foreach (var item in data)
                    {
                        SuperChatMessages.Insert(0, new SuperChatItem(item, KeepSC ? false : true));
                    }
                }
            }
            catch (Exception ex)
            {
                if (!IsCurrentLoad(generation)) return;
                LogHelper.Log("加载SC失败", LogType.ERROR, ex);
                Utils.ShowMessageToast("加载SC失败");
            }

        }
        private async void LiveDanmaku_OnClose(object sender, string e)
        {
            var generation = _loadGeneration;
            if (!IsCurrentLoad(generation) || !ReferenceEquals(sender, LiveDanmaku)) return;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!IsCurrentLoad(generation) || !ReferenceEquals(sender, LiveDanmaku)) return;
                Messages.Add(new LiveMessage()
                {
                    Type = LiveMessageType.Chat,
                    UserName = "系统",
                    Message = "连接已经关闭"
                });
            });
        }
        public CoreDispatcher Dispatcher { get; set; }

        private void ProcessMessageQueue(object sender, ElapsedEventArgs e)
        {
            var generation = _loadGeneration;
            if (!IsCurrentLoad(generation) || !ReferenceEquals(sender, _messageProcessTimer) || _messageQueue.IsEmpty) return;

            var messagesToProcess = new List<LiveMessage>();
            for (int i = 0; i < MESSAGE_BATCH_SIZE && _messageQueue.TryDequeue(out var msg); i++)
            {
                // A callback can finish filtering after Stop cleared the queue.
                // Stamp each item at reception so such late enqueues stay stale.
                if (msg.Key == generation) messagesToProcess.Add(msg.Value);
            }

            if (messagesToProcess.Count == 0) return;

            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!IsCurrentLoad(generation)) return;
                foreach (var msg in messagesToProcess)
                {
                    // 清理旧消息
                    while (Messages.Count >= MessageCleanCount)
                    {
                        Messages.RemoveAt(0);
                    }

                    if (!Utils.IsXbox)
                    {
                        Messages.Add(msg);
                    }

                    AddDanmaku?.Invoke(this, msg);
                }
            });
        }

        private void LiveDanmaku_NewMessage(object sender, LiveMessage e)
        {
            var generation = _loadGeneration;
            if (!IsCurrentLoad(generation) || !ReferenceEquals(sender, LiveDanmaku)) return;
            // Online 和 SuperChat 立即处理
            if (e.Type == LiveMessageType.Online)
            {
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (!IsCurrentLoad(generation) || !ReferenceEquals(sender, LiveDanmaku)) return;
                    Online = Convert.ToInt64(e.Data);
                });
                return;
            }

            if (e.Type == LiveMessageType.SuperChat)
            {
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (!IsCurrentLoad(generation) || !ReferenceEquals(sender, LiveDanmaku)) return;
                    SuperChatMessages.Insert(0, new SuperChatItem(e.Data as LiveSuperChatMessage, KeepSC ? false : true));
                });
                return;
            }

            if (e.Type == LiveMessageType.Chat)
            {
                // 屏蔽词过滤在入队前处理
                if (settingVM.ShieldWords != null && settingVM.ShieldWords.Count > 0)
                {
                    if (settingVM.ShieldWords.FirstOrDefault(x => e.Message.Contains(x)) != null) return;
                }

                // 普通弹幕加入队列批量处理
                _messageQueue.Enqueue(new KeyValuePair<int, LiveMessage>(generation, e));
                return;
            }
        }

        public async void Stop()
        {
            _isStopped = true;
            System.Threading.Interlocked.Increment(ref _loadGeneration);
            System.Threading.Interlocked.Increment(ref _playUrlGeneration);
            Loading = false;
            if (_messageProcessTimer != null)
            {
                _messageProcessTimer.Elapsed -= ProcessMessageQueue;
                _messageProcessTimer.Stop();
                _messageProcessTimer.Dispose();
                _messageProcessTimer = null;
            }
            scTimer?.Stop();
            scTimer?.Dispose();
            scTimer = null;
            while (_messageQueue.TryDequeue(out _)) { }
            Messages.Clear();
            try
            {
                await StopDanmaku();
            }
            catch (Exception ex)
            {
                LogHelper.Log("停止弹幕失败", LogType.ERROR, ex);
            }
        }

    }

    public class PlayurlLine
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }
    public class SettingsItem<T>
    {

        public string Name { get; set; }
        public T Value { get; set; }
    }

    public class SuperChatItem : LiveSuperChatMessage, INotifyPropertyChanged
    {
        public SuperChatItem(LiveSuperChatMessage message, bool showCountdown)
        {
            UserName = message.UserName;
            Face = message.Face;
            Message = message.Message;
            Price = message.Price;
            StartTime = message.StartTime;
            EndTime = message.EndTime;
            BackgroundColor = message.BackgroundColor;
            BackgroundBottomColor = message.BackgroundBottomColor;
            CountdownTime = Convert.ToInt32(EndTime.Subtract(DateTime.Now).TotalSeconds);
            ShowCountdown = showCountdown;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void DoPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private int _countdownTime = 0;
        public int CountdownTime
        {
            get { return _countdownTime; }
            set { _countdownTime = value; DoPropertyChanged("CountdownTime"); }
        }

        public string StartTimeStr
        {
            get
            {
                return StartTime.ToString("HH:mm:ss");
            }
        }

        private bool showCountdown = false;

        public bool ShowCountdown
        {
            get { return showCountdown; }
            set { showCountdown = value; DoPropertyChanged("ShowCountdown"); }
        }


    }
}
