using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Windows.Storage;
using System.IO;
using AllLive.UWP.Models;

namespace AllLive.UWP.Helper
{

    public static class DatabaseHelper
    {
        static SqliteConnection db;
        public async static Task InitializeDatabase()
        {
            await ApplicationData.Current.LocalFolder.CreateFileAsync("alllive.db", CreationCollisionOption.OpenIfExists);
            string dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "alllive.db");
            // 添加 UTF-8 编码支持
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
            db = new SqliteConnection(connectionString);
            db.Open();
            
            // 确保数据库使用 UTF-8 编码
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "PRAGMA encoding = 'UTF-8';";
                cmd.ExecuteNonQuery();
            }
            
            string tableCommand = @"CREATE TABLE IF NOT EXISTS Favorite (
id INTEGER PRIMARY KEY AUTOINCREMENT, 
user_name TEXT,
site_name TEXT,
photo TEXT,
room_id TEXT);

CREATE TABLE IF NOT EXISTS History (
id INTEGER PRIMARY KEY AUTOINCREMENT, 
user_name TEXT,
site_name TEXT,
photo TEXT,
room_id TEXT,
watch_time DATETIME);
";
            SqliteCommand createTable = new SqliteCommand(tableCommand, db);
            createTable.ExecuteReader();

            // 先尝试修复乱码数据，修复不了的再删除
            FixCorruptedData();
            await DetectAndCleanCorruptedData();
        }

        /// <summary>
        /// 检测并清理乱码数据
        /// </summary>
        private async static Task DetectAndCleanCorruptedData()
        {
            try
            {
                bool hasCorruptedData = false;
                
                // 检查收藏表
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Favorite WHERE site_name NOT IN ('哔哩哔哩直播', '斗鱼直播', '虎牙直播', '抖音直播')";
                    var count = Convert.ToInt32(cmd.ExecuteScalar());
                    if (count > 0)
                    {
                        hasCorruptedData = true;
                    }
                }
                
                // 检查历史表
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM History WHERE site_name NOT IN ('哔哩哔哩直播', '斗鱼直播', '虎牙直播', '抖音直播')";
                    var count = Convert.ToInt32(cmd.ExecuteScalar());
                    if (count > 0)
                    {
                        hasCorruptedData = true;
                    }
                }
                
                if (hasCorruptedData)
                {
                    LogHelper.Log("检测到数据库中存在乱码数据，正在清理...", LogType.INFO);
                    
                    // 清理乱码数据
                    using (var cmd = db.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM Favorite WHERE site_name NOT IN ('哔哩哔哩直播', '斗鱼直播', '虎牙直播', '抖音直播')";
                        cmd.ExecuteNonQuery();
                    }
                    
                    using (var cmd = db.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM History WHERE site_name NOT IN ('哔哩哔哩直播', '斗鱼直播', '虎牙直播', '抖音直播')";
                        cmd.ExecuteNonQuery();
                    }
                    
                    LogHelper.Log("乱码数据已清理完成", LogType.INFO);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log("清理乱码数据时出错", LogType.ERROR, ex);
            }
        }

        /// <summary>
        /// 尝试修复乱码数据（将乱码映射到正确的站点名称）
        /// </summary>
        public static void FixCorruptedData()
        {
            try
            {
                LogHelper.Log("开始修复乱码数据...", LogType.INFO);
                
                // 修复收藏表
                // 虎牙直播的乱码特征：»¢ÑÀÖ±²¥
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Favorite SET site_name = '虎牙直播' WHERE site_name LIKE '%»¢%' OR site_name LIKE '%ÑÀ%'";
                    var count = cmd.ExecuteNonQuery();
                    if (count > 0) LogHelper.Log($"修复收藏表中 {count} 条虎牙直播数据", LogType.INFO);
                }
                
                // 斗鱼直播的乱码特征：¶·ÓãÖ±²¥
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Favorite SET site_name = '斗鱼直播' WHERE site_name LIKE '%¶·%' OR site_name LIKE '%Óã%'";
                    var count = cmd.ExecuteNonQuery();
                    if (count > 0) LogHelper.Log($"修复收藏表中 {count} 条斗鱼直播数据", LogType.INFO);
                }
                
                // 哔哩哔哩直播的乱码特征：±¹À¹±¹Ö±²¥
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Favorite SET site_name = '哔哩哔哩直播' WHERE site_name LIKE '%±%' OR site_name LIKE '%¹%'";
                    var count = cmd.ExecuteNonQuery();
                    if (count > 0) LogHelper.Log($"修复收藏表中 {count} 条哔哩哔哩直播数据", LogType.INFO);
                }
                
                // 抖音直播的乱码特征：¶¶ÒôÖ±²¥
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Favorite SET site_name = '抖音直播' WHERE site_name LIKE '%¶¶%' OR site_name LIKE '%Òô%'";
                    var count = cmd.ExecuteNonQuery();
                    if (count > 0) LogHelper.Log($"修复收藏表中 {count} 条抖音直播数据", LogType.INFO);
                }
                
                // 修复历史表
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "UPDATE History SET site_name = '虎牙直播' WHERE site_name LIKE '%»¢%' OR site_name LIKE '%ÑÀ%'";
                    var count = cmd.ExecuteNonQuery();
                    if (count > 0) LogHelper.Log($"修复历史表中 {count} 条虎牙直播数据", LogType.INFO);
                }
                
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "UPDATE History SET site_name = '斗鱼直播' WHERE site_name LIKE '%¶·%' OR site_name LIKE '%Óã%'";
                    var count = cmd.ExecuteNonQuery();
                    if (count > 0) LogHelper.Log($"修复历史表中 {count} 条斗鱼直播数据", LogType.INFO);
                }
                
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "UPDATE History SET site_name = '哔哩哔哩直播' WHERE site_name LIKE '%±%' OR site_name LIKE '%¹%'";
                    var count = cmd.ExecuteNonQuery();
                    if (count > 0) LogHelper.Log($"修复历史表中 {count} 条哔哩哔哩直播数据", LogType.INFO);
                }
                
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "UPDATE History SET site_name = '抖音直播' WHERE site_name LIKE '%¶¶%' OR site_name LIKE '%Òô%'";
                    var count = cmd.ExecuteNonQuery();
                    if (count > 0) LogHelper.Log($"修复历史表中 {count} 条抖音直播数据", LogType.INFO);
                }
                
                LogHelper.Log("乱码数据修复完成", LogType.INFO);
            }
            catch (Exception ex)
            {
                LogHelper.Log("修复乱码数据时出错", LogType.ERROR, ex);
            }
        }


        public static void AddFavorite(FavoriteItem item)
        {
            // 空值检查
            if (string.IsNullOrEmpty(item.RoomID) || string.IsNullOrEmpty(item.SiteName))
            {
                return;
            }

            if (CheckFavorite(item.RoomID, item.SiteName)!=null) { return; }
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "INSERT INTO Favorite VALUES (NULL,@user_name,@site_name, @photo, @room_id);";
            command.Parameters.AddWithValue("@user_name", item.UserName ?? "");
            command.Parameters.AddWithValue("@site_name", item.SiteName);
            command.Parameters.AddWithValue("@photo", item.Photo ?? "");
            command.Parameters.AddWithValue("@room_id", item.RoomID);
            command.ExecuteReader();
        }
        public static long? CheckFavorite(string roomId, string siteName)
        {
            // 空值检查
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(siteName))
            {
                return null;
            }

            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "SELECT * FROM Favorite WHERE room_id=@room_id and site_name=@site_name";
            command.Parameters.AddWithValue("@site_name", siteName);
            command.Parameters.AddWithValue("@room_id", roomId);
            var result = command.ExecuteScalar();
            if (result==null)
            {
                return null;
            }
            return (long)result;
        }
        public static void DeleteFavorite(long id)
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "DELETE FROM Favorite WHERE id=@id";
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();

        }

        public static void DeleteFavorite()
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "DELETE FROM Favorite";
            command.ExecuteNonQuery();

        }

        public async static Task<List<FavoriteItem>> GetFavorites()
        {
            List<FavoriteItem> favoriteItems = new List<FavoriteItem>();
            SqliteCommand command = new SqliteCommand("SELECT * FROM Favorite", db);
            var reader =await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                favoriteItems.Add(new FavoriteItem()
                {
                    ID= reader.GetInt32(0),
                    RoomID = reader.GetString(4),
                    Photo = reader.GetString(3),
                    SiteName = reader.GetString(2),
                    UserName = reader.GetString(1)
                });
            }
            return favoriteItems;
        }


        public static void AddHistory(HistoryItem item)
        {
            // 空值检查，防止 SQLite 参数绑定失败
            if (string.IsNullOrEmpty(item.RoomID) || string.IsNullOrEmpty(item.SiteName))
            {
                return;
            }

            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            var hisId = CheckHistory(item.RoomID, item.SiteName);
            if (hisId != null)
            {  
                //更新时间
                command.CommandText = "UPDATE History SET watch_time=@time WHERE room_id=@room_id and site_name=@site_name";
                command.Parameters.AddWithValue("@site_name", item.SiteName);
                command.Parameters.AddWithValue("@room_id", item.RoomID);
                command.Parameters.AddWithValue("@time", DateTime.Now);
                command.ExecuteReader();
              
                return;
            }
          
            command.CommandText = "INSERT INTO History VALUES (NULL,@user_name,@site_name, @photo, @room_id,@time);";
            command.Parameters.AddWithValue("@user_name", item.UserName ?? "");
            command.Parameters.AddWithValue("@site_name", item.SiteName);
            command.Parameters.AddWithValue("@photo", item.Photo ?? "");
            command.Parameters.AddWithValue("@room_id", item.RoomID);
            command.Parameters.AddWithValue("@time", DateTime.Now);
            command.ExecuteReader();
        }
        public static long? CheckHistory(string roomId, string siteName)
        {
            // 空值检查
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(siteName))
            {
                return null;
            }

            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "SELECT * FROM History WHERE room_id=@room_id and site_name=@site_name";
            command.Parameters.AddWithValue("@site_name", siteName);
            command.Parameters.AddWithValue("@room_id", roomId);
            var result = command.ExecuteScalar();
            if (result == null)
            {
                return null;
            }
            return (long)result;
        }
        public static void DeleteHistory(long id)
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "DELETE FROM History WHERE id=@id";
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
          
        }
        public static void DeleteHistory()
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "DELETE FROM History";
            command.ExecuteNonQuery();

        }
        public async static Task<List<HistoryItem>> GetHistory()
        {
            List<HistoryItem> favoriteItems = new List<HistoryItem>();
            SqliteCommand command = new SqliteCommand("SELECT * FROM History ORDER BY watch_time DESC", db);
            var reader =await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                favoriteItems.Add(new HistoryItem()
                {
                    ID= reader.GetInt32(0),
                    RoomID = reader.GetString(4),
                    Photo = reader.GetString(3),
                    SiteName = reader.GetString(2),
                    UserName = reader.GetString(1),
                    WatchTime= reader.GetDateTime(5)
                });
            }
            return favoriteItems;
        }

    }


}
