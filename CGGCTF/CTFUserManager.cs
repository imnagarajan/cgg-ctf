using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.IO;
using System.Diagnostics;

using TShockAPI;
using TShockAPI.DB;

using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;

namespace CGGCTF
{
    public class CTFUserManager
    {
        // database initialization
        private IDbConnection db;
        public CTFUserManager()
        {
            switch (TShock.Config.StorageType.ToLower()) {
                case "mysql":
                    string[] host = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection() {
                        ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                        host[0],
                        host.Length == 1 ? "3306" : host[1],
                        TShock.Config.MySqlDbName,
                        TShock.Config.MySqlUsername,
                        TShock.Config.MySqlPassword)
                    };
                    break;
                case "sqlite":
                    string dbPath = Path.Combine(TShock.SavePath, "cggctf.sqlite");
                    db = new SqliteConnection(String.Format("uri=file://{0},Version=3", dbPath));
                    break;
            }

            SqlTableCreator creator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ?
                (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            creator.EnsureTableStructure(new SqlTable("ctfusers",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true },
                new SqlColumn("Coins", MySqlDbType.Int32),
                new SqlColumn("Kills", MySqlDbType.Int32),
                new SqlColumn("Deaths", MySqlDbType.Int32),
                new SqlColumn("Assists", MySqlDbType.Int32),
                new SqlColumn("Wins", MySqlDbType.Int32),
                new SqlColumn("Loses", MySqlDbType.Int32),
                new SqlColumn("Classes", MySqlDbType.Text)));
        }

        public CTFUser GetUser(int id)
        {
            try {
                using (var reader = db.QueryReader("SELECT * FROM ctfusers WHERE ID = @0", id)) {
                    if (reader.Read()) {
                        return new CTFUser() {
                            ID = reader.Get<int>("ID"),
                            Coins = reader.Get<int>("Coins"),
                            Kills = reader.Get<int>("Kills"),
                            Deaths = reader.Get<int>("Deaths"),
                            Assists = reader.Get<int>("Assists"),
                            Wins = reader.Get<int>("Wins"),
                            Loses = reader.Get<int>("Loses"),
                            Classes = ParseClasses(reader.Get<string>("Classes"))
                        };
                    } else {
                        var ret = new CTFUser();
                        ret.ID = id;
                        if (db.Query("INSERT INTO ctfusers (ID, Coins, Kills, " +
                            "Deaths, Assists, Wins, Loses, Classes) " +
                            "VALUES (@0, @1, @2, @3, @4, @5, @6, @7)",
                            ret.ID, ret.Coins, ret.Kills, ret.Deaths,
                            ret.Assists, ret.Wins, ret.Loses,
                            ClassesToString(ret.Classes)) != 0)
                            return ret;
                    }
                }
            } catch (Exception ex) {
                TShock.Log.Error(ex.ToString());
            }
            return null;
        }

        public bool SaveUser(CTFUser user)
        {
            try {
                db.Query("UPDATE ctfusers SET Coins = @0, Kills = @1, Deaths = @2, " +
                    "Assists = @3, Wins = @4, Loses = @5, Classes = @6 WHERE ID = @7",
                    user.Coins, user.Kills, user.Deaths, user.Assists,
                    user.Wins, user.Loses, ClassesToString(user.Classes), user.ID);
                return true;
            } catch (Exception ex) {
                TShock.Log.Error(ex.ToString());
            }
            return false;
        }

        List<int> ParseClasses(string classes)
        {
            var ret = new List<int>();
            if (string.IsNullOrWhiteSpace(classes))
                return ret;
            var list = classes.Split(',');
            foreach (var cls in list) {
                ret.Add(int.Parse(cls));
            }
            return ret;
        }

        string ClassesToString(List<int> classes)
        {
            return string.Join(",", classes);
        }
    }
}
