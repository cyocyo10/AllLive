using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AllLive.UWP.Helper;
using AllLive.UWP.Models;
using AllLive.UWP.ViewModels;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

class Program
{
    static readonly List<object> Results = new List<object>();
    static readonly List<string> Writes = new List<string>();
    static SqliteConnection Db;
    static SyncVM Vm;
    static string Before;
    static string PreservedAfterFailure;
    static readonly string ResultDirectory = Environment.GetEnvironmentVariable("HARNESS_RESULTS_DIR") ?? "results";
    static string DbDirectory;
    static int Passed;
    const string FavoriteJson = "[{\"siteId\":\"douyu\",\"roomId\":\"new-f\",\"userName\":\"new\"}]";
    const string HistoryJson = "[{\"siteId\":\"douyu\",\"roomId\":\"new-h\",\"updateTime\":\"2001-01-01 12:00:00\"}]";
    static readonly DateTime ImportedTime = new DateTime(2001,1,1,12,0,0);
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static Exception Receive(string method, bool overlay, string json)
    {
        try { typeof(SyncVM).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Vm, new object[]{overlay,json}); return null; }
        catch (TargetInvocationException ex) { return ex.InnerException; }
    }
    static void Sql(string sql) { using(var c = new SqliteCommand(sql, Db)) c.ExecuteNonQuery(); }
    static List<Dictionary<string,object>> Rows(string table)
    {
        var rows = new List<Dictionary<string,object>>();
        using(var c = new SqliteCommand("SELECT * FROM " + table + " ORDER BY 1", Db))
        using(var r = c.ExecuteReader())
            while(r.Read())
            {
                var row = new Dictionary<string,object>();
                for(int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.GetValue(i);
                rows.Add(row);
            }
        return rows;
    }
    static string Snapshot() => JsonConvert.SerializeObject(new {favorites=Rows("Favorite"), history=Rows("History"), sequence=Rows("sqlite_sequence")});
    static void Reset(string id)
    {
        Db?.Dispose();
        Db = new SqliteConnection("Data Source=" + Path.Combine(DbDirectory, id + ".db") + ";Pooling=False");
        Db.Open();
        Sql(@"CREATE TABLE Favorite(id INTEGER PRIMARY KEY AUTOINCREMENT,user_name TEXT,site_name TEXT,photo TEXT,room_id TEXT);
CREATE TABLE History(id INTEGER PRIMARY KEY AUTOINCREMENT,user_name TEXT,site_name TEXT,photo TEXT,room_id TEXT,watch_time DATETIME);
CREATE INDEX idx_favorite_room_site ON Favorite(room_id,site_name);
CREATE INDEX idx_history_room_site ON History(room_id,site_name);");
        Db.CreateFunction<string,int>("observe_write", value => { Writes.Add(value); return 0; });
        foreach(var table in new[]{"Favorite","History"})
            foreach(var operation in new[]{"INSERT","UPDATE","DELETE"})
                Sql("CREATE TRIGGER trace_" + table + "_" + operation + " AFTER " + operation + " ON " + table + " BEGIN SELECT observe_write('" + table + ":" + operation + ":' || " + (operation == "DELETE" ? "OLD" : "NEW") + ".room_id); END;");
        typeof(DatabaseHelper).GetField("db", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, Db);
        Vm = new SyncVM { Dispatcher = new Windows.UI.Core.CoreDispatcher() };
        Utils.Toasts.Clear(); MessageCenter.Updates = 0;
        DatabaseHelper.AddFavorite(new FavoriteItem { RoomID="old-f", SiteName="虎牙直播", UserName="old", Photo="old-photo" });
        DatabaseHelper.AddHistory(new HistoryItem { RoomID="old-h", SiteName="虎牙直播", UserName="old", Photo="old-photo", WatchTime=new DateTime(2000,1,1) });
        Before = Snapshot(); PreservedAfterFailure = null; Writes.Clear();
    }
    static void Unchanged(bool noWrites = true)
    {
        Assert(Snapshot() == Before, "Existing rows, IDs and SQLite sequences must remain exactly unchanged");
        if(noWrites) Assert(Writes.Count == 0, "Validation must complete before the first write");
        Assert(Utils.Toasts.Count == 0 && MessageCenter.Updates == 0, "Failure must not announce success or refresh favorites");
        PreservedAfterFailure = Snapshot();
    }
    static void Case(string id, string expectation, Action action)
    {
        Reset(id);
        try
        {
            action(); Passed++;
            Results.Add(new {id, expectation, status="PASS", preservedAfterFailure=PreservedAfterFailure, favoriteRows=Rows("Favorite"), historyRows=Rows("History"), writes=Writes.ToArray()});
            Console.WriteLine("PASS " + id);
        }
        catch(Exception ex)
        {
            Results.Add(new {id, expectation, status="FAIL", error=ex.ToString(), favoriteRows=Rows("Favorite"), historyRows=Rows("History"), writes=Writes.ToArray()});
            Console.WriteLine("FAIL " + id + ": " + ex.Message); Environment.ExitCode = 1;
        }
    }
    static void ImportBatch(string method, object items)
    {
        var member = typeof(DatabaseHelper).GetMethod(method, BindingFlags.Public | BindingFlags.Static);
        if(member == null) throw new MissingMethodException("The atomic import API is absent from this source version.");
        try { member.Invoke(null, new object[]{items, true}); }
        catch(TargetInvocationException ex) { throw ex.InnerException; }
    }
    static void FailInsert(string table)
    {
        Sql("CREATE TRIGGER fail_insert BEFORE INSERT ON " + table + " WHEN NEW.room_id='fail' BEGIN SELECT RAISE(ABORT, 'injected mid-batch insert failure'); END;");
    }
    static List<HistoryItem> History() => DatabaseHelper.GetHistory().GetAwaiter().GetResult();
    static List<FavoriteItem> Favorites() => DatabaseHelper.GetFavorites().GetAwaiter().GetResult();
    static void Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Directory.CreateDirectory(ResultDirectory);
        DbDirectory = Path.Combine(Path.GetTempPath(), "alllive-sync-regression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DbDirectory);

        // These retain the original audit scenario IDs and now assert the required safe behavior.
        Case("favorite-overlay-malformed", "Malformed JSON rejects before any favorite mutation", () => {
            Assert(Receive("ReceiveFavorite", true, "not-json") is JsonReaderException, "Expected JSON error"); Unchanged();
        });
        Case("favorite-merge-malformed-control", "Malformed merge input preserves existing rows", () => {
            Assert(Receive("ReceiveFavorite", false, "not-json") is JsonReaderException, "Expected JSON error"); Unchanged();
        });
        Case("favorite-overlay-null", "JSON null rejects without deleting existing favorites", () => {
            Assert(Receive("ReceiveFavorite", true, "null") != null, "Expected validation error"); Unchanged();
        });
        Case("favorite-overlay-null-second-item", "Null second favorite rejects the entire batch before writing", () => {
            Assert(Receive("ReceiveFavorite", true, FavoriteJson.TrimEnd(']') + ",null]") != null, "Expected null-item rejection"); Unchanged();
        });
        Case("favorite-overlay-valid-control", "Valid replacement commits and announces success once", () => {
            Assert(Receive("ReceiveFavorite", true, FavoriteJson) == null, "Expected successful receive");
            Assert(Favorites().Count == 1 && Favorites().Single().RoomID == "new-f", "Expected complete replacement");
            Assert(Utils.Toasts.Count == 1 && MessageCenter.Updates == 1, "Expected completion UI calls");
        });
        Case("history-overlay-malformed", "Malformed JSON rejects before any history mutation", () => {
            Assert(Receive("ReceiveHistory", true, "not-json") is JsonReaderException, "Expected JSON error"); Unchanged();
        });
        Case("history-merge-malformed-control", "Malformed merge input preserves existing history", () => {
            Assert(Receive("ReceiveHistory", false, "not-json") is JsonReaderException, "Expected JSON error"); Unchanged();
        });
        Case("history-overlay-invalid-date", "Invalid date rejects before deleting history", () => {
            Assert(Receive("ReceiveHistory", true, HistoryJson.Replace("2001-01-01 12:00:00", "invalid-date")) is FormatException, "Expected date parse error"); Unchanged();
        });
        Case("history-overlay-late-invalid-date", "A later bad date prevents all writes in the batch", () => {
            var json = HistoryJson.TrimEnd(']') + ",{\"siteId\":\"douyu\",\"roomId\":\"bad-h\",\"updateTime\":\"invalid-date\"}]";
            Assert(Receive("ReceiveHistory", true, json) is FormatException, "Expected date parse error"); Unchanged();
        });
        Case("history-sync-valid-loses-original-time", "Regression: valid sync retains the original time", () => {
            Assert(Receive("ReceiveHistory", true, HistoryJson) == null, "Expected successful receive");
            Assert(History().Single().WatchTime == ImportedTime, "Sync must preserve original WatchTime");
            Assert(Utils.Toasts.Count == 1, "Success reported once");
        });
        Case("add-history-insert-ignores-WatchTime", "Regression: INSERT preserves supplied WatchTime", () => {
            DatabaseHelper.AddHistory(new HistoryItem {RoomID="insert", SiteName="虎牙直播", WatchTime=new DateTime(1999,2,3)});
            Assert(History().Single(r=>r.RoomID=="insert").WatchTime == new DateTime(1999,2,3), "INSERT must retain input time");
            Assert(Writes.Single() == "History:INSERT:insert", "Expected insert branch");
        });
        Case("add-history-update-ignores-WatchTime", "Regression: UPDATE preserves supplied WatchTime and metadata", () => {
            DatabaseHelper.AddHistory(new HistoryItem {RoomID="old-h", SiteName="虎牙直播", UserName="updated", Photo="updated-photo", WatchTime=new DateTime(1999,2,3)});
            var item = History().Single();
            Assert(item.WatchTime == new DateTime(1999,2,3) && item.UserName == "updated" && item.Photo == "updated-photo", "UPDATE must retain supplied fields");
            Assert(Writes.Single() == "History:UPDATE:old-h", "Expected update branch");
        });

        foreach(bool overlay in new[]{true,false})
        {
            var mode = overlay ? "overlay" : "merge";
            foreach(var payload in new[]{new {id="null", json="null"}, new {id="null-second-item", json=HistoryJson.TrimEnd(']') + ",null]"}, new {id="missing-room", json=HistoryJson.Replace("new-h", " ")}, new {id="missing-site", json=HistoryJson.Replace("douyu", " ")}})
                Case("history-" + mode + "-" + payload.id, "Invalid history payload preserves the full existing database", () => {
                    Assert(Receive("ReceiveHistory", overlay, payload.json) != null, "Expected validation error"); Unchanged();
                });
            foreach(var payload in new[]{new {id="missing-room", json=FavoriteJson.Replace("new-f", " ")}, new {id="missing-site", json=FavoriteJson.Replace("douyu", " ")}})
                Case("favorite-" + mode + "-" + payload.id, "Invalid favorite identifiers reject before mutation", () => {
                    Assert(Receive("ReceiveFavorite", overlay, payload.json) is JsonSerializationException, "Expected validation error"); Unchanged();
                });
            Case("favorite-" + mode + "-mid-insert-rollback", "Real SQLite failure on second insert rolls back deletion and first insert", () => {
                FailInsert("Favorite");
                var json = FavoriteJson.TrimEnd(']') + ",{\"siteId\":\"douyu\",\"roomId\":\"fail\"}]";
                Assert(Receive("ReceiveFavorite", overlay, json) is SqliteException, "Expected actual SQLite constraint error");
                Assert(Writes.Contains("Favorite:INSERT:new-f"), "The first insert must have executed before failure");
                if(overlay) Assert(Writes.First() == "Favorite:DELETE:old-f", "The delete must have executed inside the transaction");
                Unchanged(false);
                Sql("DROP TRIGGER fail_insert");
                Assert(Receive("ReceiveFavorite", overlay, FavoriteJson) == null, "Rolled-back connection must remain reusable");
            });
            Case("history-" + mode + "-mid-insert-rollback", "Real SQLite failure on second insert rolls back the whole history batch", () => {
                FailInsert("History");
                var json = HistoryJson.TrimEnd(']') + ",{\"siteId\":\"douyu\",\"roomId\":\"fail\",\"updateTime\":\"2002-01-01\"}]";
                Assert(Receive("ReceiveHistory", overlay, json) is SqliteException, "Expected actual SQLite constraint error");
                Assert(Writes.Contains("History:INSERT:new-h"), "The first insert must have executed before failure");
                if(overlay) Assert(Writes.First() == "History:DELETE:old-h", "The delete must have executed inside the transaction");
                Unchanged(false);
                Sql("DROP TRIGGER fail_insert");
                Assert(Receive("ReceiveHistory", overlay, HistoryJson) == null, "Rolled-back connection must remain reusable");
            });
        }
        Case("history-merge-update-before-failed-insert-rollback", "A prior update in a merge is undone when a later insert fails", () => {
            FailInsert("History");
            var json = "[{\"siteId\":\"huya\",\"roomId\":\"old-h\",\"userName\":\"changed\",\"updateTime\":\"2005-01-01\"},{\"siteId\":\"douyu\",\"roomId\":\"fail\",\"updateTime\":\"2002-01-01\"}]";
            Assert(Receive("ReceiveHistory", false, json) is SqliteException, "Expected constraint error");
            Assert(Writes.Single() == "History:UPDATE:old-h", "Earlier update must actually execute"); Unchanged(false);
        });
        Case("favorite-valid-batch-deduplicates", "Valid multi-item replacement deduplicates using the transaction", () => {
            var json = "[{\"siteId\":\"douyu\",\"roomId\":\"new-f\"},{\"siteId\":\"douyu\",\"roomId\":\"new-f\"},{\"siteId\":\"huya\",\"roomId\":\"second\"}]";
            Assert(Receive("ReceiveFavorite", true, json) == null, "Expected successful batch");
            Assert(Favorites().Count == 2 && Favorites().Any(r=>r.RoomID=="second"), "Expected complete deduplicated batch");
        });
        Case("history-valid-batch-retains-each-time", "Valid multi-item replacement persists each original viewing time", () => {
            var json = HistoryJson.TrimEnd(']') + ",{\"siteId\":\"huya\",\"roomId\":\"second\",\"updateTime\":\"2002-01-01 12:34:56\"}]";
            Assert(Receive("ReceiveHistory", true, json) == null, "Expected successful batch");
            Assert(History().Count == 2 && History().Single(r=>r.RoomID=="new-h").WatchTime == ImportedTime && History().Single(r=>r.RoomID=="second").WatchTime == new DateTime(2002,1,1,12,34,56), "Expected each original time");
        });
        Case("favorite-valid-merge-preserves-existing", "Valid merge keeps old favorite and adds new one", () => {
            Assert(Receive("ReceiveFavorite", false, FavoriteJson) == null, "Expected valid merge");
            Assert(Favorites().Count == 2 && Favorites().Any(r=>r.RoomID=="old-f"), "Expected old and new favorite");
        });
        Case("history-valid-merge-updates-existing", "Valid merge updates timestamp and metadata and keeps other rows", () => {
            var json = "[{\"siteId\":\"huya\",\"roomId\":\"old-h\",\"userName\":\"changed\",\"updateTime\":\"2005-01-01\"}]";
            Assert(Receive("ReceiveHistory", false, json) == null, "Expected valid merge");
            Assert(History().Single().WatchTime == new DateTime(2005,1,1) && History().Single().UserName == "changed", "Expected existing row updated with supplied time");
        });
        foreach(var method in new[]{"ReceiveFavorite", "ReceiveHistory"})
            foreach(var payload in new[]{new {id="csharp-null", json=(string)null}, new {id="empty-string", json=""}, new {id="object-instead-of-array", json="{}"}})
                Case(method + "-" + payload.id, "Invalid whole payload rejects without mutation", () => {
                    Assert(Receive(method, true, payload.json) != null, "Expected payload rejection"); Unchanged();
                });
        Case("empty-valid-replacement-clears-only-selected-table", "An explicit empty array remains a valid intentional replacement", () => {
            Assert(Receive("ReceiveFavorite", true, "[]") == null, "Expected empty replacement");
            Assert(Favorites().Count == 0 && History().Count == 1, "Only favorites must be cleared");
        });
        Case("empty-valid-merge-preserves-existing", "Empty merges leave old favorites and history intact", () => {
            Assert(Receive("ReceiveFavorite", false, "[]") == null && Receive("ReceiveHistory", false, "[]") == null, "Expected empty merges");
            Assert(Snapshot() == Before && Writes.Count == 0, "Empty merge must not mutate rows");
        });
        foreach(var id in new[]{"new-default", "old-h"})
            Case("add-history-default-time-" + id, "Ordinary playback without WatchTime retains current-time behavior", () => {
                var before = DateTime.Now;
                DatabaseHelper.AddHistory(new HistoryItem {RoomID=id, SiteName="虎牙直播"});
                var after = DateTime.Now;
                var stored = History().Single(r=>r.RoomID==id).WatchTime;
                // SQLite's standard DateTime serialization has millisecond precision.
                Assert(stored >= before.AddMilliseconds(-1) && stored <= after, "Default WatchTime must use the current local time");
            });
        Case("history-sync-invariant-date-under-other-culture", "Protocol timestamps parse consistently regardless of current culture", () => {
            var previous = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE"); Assert(Receive("ReceiveHistory", true, HistoryJson) == null, "Expected invariant parse"); Assert(History().Single().WatchTime == ImportedTime, "Expected unchanged original time"); }
            finally { CultureInfo.CurrentCulture = previous; }
        });
        Case("history-missing-date-rejects", "Missing history date rejects before mutation", () => {
            Assert(Receive("ReceiveHistory", true, "[{\"siteId\":\"douyu\",\"roomId\":\"new-h\"}]") is ArgumentNullException, "Expected missing date error"); Unchanged();
        });
        Case("database-import-invalid-favorite-validates-whole-batch", "Database batch API rejects later invalid items before deletion", () => {
            try { ImportBatch("ImportFavorites", new[]{new FavoriteItem {RoomID="valid", SiteName="斗鱼直播"},null}); throw new Exception("Must reject invalid batch"); }
            catch(ArgumentException) {} Unchanged();
        });
        Case("database-import-invalid-history-validates-whole-batch", "Database batch API rejects later invalid items before deletion", () => {
            try { ImportBatch("ImportHistory", new[]{new HistoryItem {RoomID="valid", SiteName="斗鱼直播"},null}); throw new Exception("Must reject invalid batch"); }
            catch(ArgumentException) {} Unchanged();
        });

        var version = typeof(SqliteConnection).Assembly.GetName().Version.ToString();
        File.WriteAllText(Path.Combine(ResultDirectory, "results.json"), JsonConvert.SerializeObject(new {baselineCommit="c6951df959b7ead0d93edc087188788b49f1e273", runtime=Environment.Version.ToString(), sqliteAssembly=version, sourceVariant=Environment.GetEnvironmentVariable("HARNESS_SOURCE_LABEL") ?? "fixed", execution="whole selected SyncVM.cs and DatabaseHelper.cs; actual Microsoft.Data.Sqlite 8.0.6 file databases; UI and SignalR doubles", total=Results.Count, passed=Passed, failed=Results.Count-Passed, results=Results}, Formatting.Indented));
        Console.WriteLine($"{Passed}/{Results.Count} passed using real Microsoft.Data.Sqlite {version}.");
        Db?.Dispose();
        typeof(DatabaseHelper).GetField("db", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        Directory.Delete(DbDirectory, true);
    }
}
