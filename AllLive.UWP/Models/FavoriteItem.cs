using AllLive.Core.Models;
using AllLive.UWP.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllLive.UWP.Models
{
    public class FavoriteItem: BaseNotifyPropertyChanged
    {
        public int ID { get; set; }
        public string RoomID { get; set; }
        public string UserName { get; set; }
        public string Photo { get; set; }
        public string SiteName { get; set; }


        private LiveStatusType _LiveStatus = LiveStatusType.Offline;
        public LiveStatusType LiveStatus
        {
            get { return _LiveStatus; }
            set 
            { 
                _LiveStatus = value; 
                DoPropertyChanged("LiveStatus");
                DoPropertyChanged("IsLive");
                DoPropertyChanged("IsReplay");
                DoPropertyChanged("IsLiveOrReplay");
            }
        }

        /// <summary>
        /// 是否正在直播（直播或回放）
        /// </summary>
        public bool IsLiveOrReplay => LiveStatus == LiveStatusType.Live || LiveStatus == LiveStatusType.Replay;

        /// <summary>
        /// 是否正在直播
        /// </summary>
        public bool IsLive => LiveStatus == LiveStatusType.Live;

        /// <summary>
        /// 是否回放中
        /// </summary>
        public bool IsReplay => LiveStatus == LiveStatusType.Replay;
    }
}
