using AllLive.UWP.Helper;
using AllLive.UWP.Models;
using AllLive.UWP.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace AllLive.UWP.Views
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class FavoritePage : Page
    {
        readonly FavoriteVM favoriteVM;
        private bool isEventSubscribed = false;
        
        public FavoritePage()
        {
            favoriteVM = new FavoriteVM();
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
            this.InitializeComponent();
        }

        private void MessageCenter_UpdateFavoriteEvent(object sender, EventArgs e)
        {
            favoriteVM.Refresh();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 订阅事件（只订阅一次）
            if (!isEventSubscribed)
            {
                MessageCenter.UpdateFavoriteEvent += MessageCenter_UpdateFavoriteEvent;
                isEventSubscribed = true;
            }

            if(favoriteVM.Items.Count==0)
            {
                favoriteVM.LoadData();
            }

        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            
            // 如果是后退导航（页面被销毁），取消事件订阅
            if (e.NavigationMode == NavigationMode.Back)
            {
                if (isEventSubscribed)
                {
                    MessageCenter.UpdateFavoriteEvent -= MessageCenter_UpdateFavoriteEvent;
                    isEventSubscribed = false;
                }
                // 禁用缓存，让页面被销毁
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
            }
        }

        private void ls_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as FavoriteItem;
            if (item == null)
            {
                return;
            }

            // 调试信息：记录站点名称
            LogHelper.Log($"[FavoritePage] 点击收藏 - SiteName: '{item.SiteName}', RoomID: '{item.RoomID}'", LogType.DEBUG);

            var site = MainVM.Sites.FirstOrDefault(x => x.Name == item.SiteName);
            if (site == null)
            {
                // 站点不存在，可能是收藏数据中的站点已被移除
                LogHelper.Log($"[FavoritePage] 无法找到站点 - SiteName: '{item.SiteName}'", LogType.ERROR);
                LogHelper.Log($"[FavoritePage] 可用站点列表: {string.Join(", ", MainVM.Sites.Select(s => $"'{s.Name}'"))}", LogType.DEBUG);
                
                // 显示详细的调试信息
                var availableSites = string.Join(", ", MainVM.Sites.Select(s => s.Name));
                Utils.ShowMessageToast($"无法找到站点\n数据库中: '{item.SiteName}'\n可用站点: {availableSites}", 5000);
                return;
            }

            MessageCenter.OpenLiveRoom(site.LiveSite, new Core.Models.LiveRoomItem()
            {
                RoomID = item.RoomID
            });
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuFlyoutItem)?.DataContext as FavoriteItem;
            if (item == null)
            {
                return;
            }

            favoriteVM.RemoveItem(item);
        }
    }
}
