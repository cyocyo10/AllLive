using AllLive.UWP.Helper;
using AllLive.UWP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Windows.Storage.Pickers;
using Windows.Storage;
using Newtonsoft.Json;
using Windows.UI.Popups;

namespace AllLive.UWP.ViewModels
{
    public class FavoriteVM : BaseViewModel
    {
        public FavoriteVM()
        {
            Items = new ObservableCollection<FavoriteItem>();
            InputCommand = new RelayCommand(Input);
            OutputCommand = new RelayCommand(Output);
            TipCommand = new RelayCommand(Tip);
        }

        public ICommand InputCommand { get; set; }
        public ICommand OutputCommand { get; set; }
        public ICommand TipCommand { get; set; }


        private ObservableCollection<FavoriteItem> _items;
        public ObservableCollection<FavoriteItem> Items
        {
            get { return _items; }
            set { _items = value; DoPropertyChanged("Items"); }
        }


        private bool _loadingLiveStatus;

        public bool LoaddingLiveStatus
        {
            get { return _loadingLiveStatus; }
            set { _loadingLiveStatus = value; DoPropertyChanged("LoaddingLiveStatus"); }
        }



        public async void LoadData()
        {
            try
            {
                LogHelper.Log("[FavoriteVM.LoadData] 开始加载收藏数据", LogType.DEBUG);
                Loading = true;
                var list = await DatabaseHelper.GetFavorites();
                Items = new ObservableCollection<FavoriteItem>(list);
                IsEmpty = Items.Count == 0;
                LogHelper.Log($"[FavoriteVM.LoadData] 加载完成, 共{Items.Count}条", LogType.DEBUG);
                if (!IsEmpty)
                {
                    LoadLiveStatus();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log("[FavoriteVM.LoadData] 加载收藏数据失败", LogType.ERROR, ex);
                HandleError(ex);
            }
            finally
            {
                Loading = false;
            }
        }

        public async void LoadLiveStatus()
        {
            try
            {
                LoaddingLiveStatus = true;
                LogHelper.Log("[FavoriteVM.LoadLiveStatus] 开始批量加载直播状态", LogType.DEBUG);
                var tasks = Items.Select(item => LoadLiveStatusAsync(item)).ToArray();
                var results = await Task.WhenAll(tasks);
                LogHelper.Log("[FavoriteVM.LoadLiveStatus] 批量加载直播状态完成", LogType.DEBUG);
                // GetLiveStatus 的 continuation 在线程池线程上执行,
                // LiveStatus/LoaddingLiveStatus 的赋值必须回到 UI 线程,否则会触发非 UI 线程的 PropertyChanged
                await Windows.ApplicationModel.Core.CoreApplication.MainView.Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    foreach (var result in results)
                    {
                        if (result.Item1 != null)
                        {
                            result.Item1.LiveStatus = result.Item2;
                        }
                    }
                    Items = new ObservableCollection<FavoriteItem>(
                        Items.OrderByDescending(x => x.LiveStatus == AllLive.Core.Models.LiveStatusType.Live)
                             .ThenByDescending(x => x.LiveStatus == AllLive.Core.Models.LiveStatusType.Replay));
                    LoaddingLiveStatus = false;
                });
            }
            catch (Exception ex)
            {
                LogHelper.Log("[FavoriteVM.LoadLiveStatus] 批量加载直播状态失败", LogType.ERROR, ex);
                await Windows.ApplicationModel.Core.CoreApplication.MainView.Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    LoaddingLiveStatus = false;
                });
            }
        }

        private async Task<Tuple<FavoriteItem, AllLive.Core.Models.LiveStatusType>> LoadLiveStatusAsync(FavoriteItem item)
        {
            try
            {
                var site = MainVM.Sites.FirstOrDefault(x => x.Name == item.SiteName);
                if (site != null)
                {
                    var status = await site.LiveSite.GetLiveStatus(item.RoomID);
                    return Tuple.Create(item, status);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log($"[FavoriteVM.LoadLiveStatusAsync] {item.SiteName}-{item.RoomID} 失败", LogType.ERROR, ex);
            }
            return Tuple.Create(item, AllLive.Core.Models.LiveStatusType.Offline);
        }


        public override void Refresh()
        {
            base.Refresh();
            Items.Clear();
            LoadData();
        }

        public void RemoveItem(FavoriteItem item)
        {
            try
            {
                DatabaseHelper.DeleteFavorite(item.ID);
                Items.Remove(item);
                IsEmpty = Items.Count == 0;
            }
            catch (Exception ex)
            {
                LogHelper.Log("[FavoriteVM.RemoveItem] 删除收藏失败", LogType.ERROR, ex);
                HandleError(ex);
            }

        }

        public async void Input()
        {
          
            // 打开文件选择器
            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.ViewMode = PickerViewMode.List;
            picker.CommitButtonText = "导入";

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    var json = await FileIO.ReadTextAsync(file);
                    var items = Newtonsoft.Json.JsonConvert.DeserializeObject<List<FavoriteJsonItem>>(json);
                    foreach (var item in items)
                    {

                        DatabaseHelper.AddFavorite(new FavoriteItem()
                        {
                            SiteName = item.SiteName,
                            RoomID = item.RoomId,
                            UserName = item.UserName,
                            Photo = item.Face,
                        });
                    }
                    Utils.ShowMessageToast("导入成功");
                    Refresh();
                }
                catch (Exception ex)
                {
                    LogHelper.Log("[FavoriteVM.Input] 导入收藏失败", LogType.ERROR, ex);
                    HandleError(ex);
                    Utils.ShowMessageToast("导入失败");
                }
            }
        }

        public async void Output()
        {
            // 打开文件选择器
            FileSavePicker picker = new FileSavePicker();
            picker.FileTypeChoices.Add("Json", new List<string>() { ".json" });
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.SuggestedFileName = "favorite.json";

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    var items = new List<FavoriteJsonItem>();
                    foreach (var item in Items)
                    {
                        var siteId = "";
                        switch(item.SiteName)
                        {
                            case "哔哩哔哩直播":
                                siteId = "bilibili";
                                break;
                            case "斗鱼直播":
                                siteId = "douyu";
                                break;
                            case "虎牙直播":
                                siteId = "huya";
                                break;
                            case "抖音直播":
                                siteId = "douyin";
                                break;
                        }

                        items.Add(new FavoriteJsonItem()
                        {
                            SiteId = siteId,
                            Id = $"{siteId}_{item.RoomID}",
                            RoomId = item.RoomID,
                            UserName = item.UserName,
                            Face = item.Photo,
                            AddTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.M")
                        });
                    }
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(items, Newtonsoft.Json.Formatting.Indented);
                    await FileIO.WriteTextAsync(file, json);
                    Utils.ShowMessageToast("导出成功");
                }
                catch (Exception ex)
                {
                    LogHelper.Log("[FavoriteVM.Output] 导出收藏失败", LogType.ERROR, ex);
                    HandleError(ex);
                    Utils.ShowMessageToast("导出失败");
                }
            }


        }

        public void Tip()
        {
            MessageDialog dialog = new MessageDialog(@"该程序兼容Simple Live，您可以导入Simple Live的关注数据，导出的数据也可以在Simple Live中导入。", "导入导出说明");
           _= dialog.ShowAsync();
        }
    }

    public class FavoriteJsonItem
    {
        [JsonProperty("siteId")]
        public string SiteId;

        [JsonProperty("id")]
        public string Id;

        [JsonProperty("roomId")]
        public string RoomId;

        [JsonProperty("userName")]
        public string UserName;

        [JsonProperty("face")]
        public string Face;

        [JsonProperty("addTime")]
        public string AddTime;

        [JsonIgnore]
        public string SiteName
        {
            get
            {
                switch (SiteId)
                {
                    case "bilibili":
                        return "哔哩哔哩直播";
                    case "douyu":
                        return "斗鱼直播";
                    case "huya":
                        return "虎牙直播";
                    case "douyin":
                        return "抖音直播";
                    default:
                        return "未知";
                }
            }
        }
        
    }
}
