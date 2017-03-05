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
    public class CTFClassManager
    {
        // database initialization
        private IDbConnection db;
        public CTFClassManager()
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
            creator.EnsureTableStructure(new SqlTable("ctfclasses",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("Name", MySqlDbType.String) { Unique = true },
                new SqlColumn("Description", MySqlDbType.Text),
                new SqlColumn("HP", MySqlDbType.Int32),
                new SqlColumn("Mana", MySqlDbType.Int32),
                new SqlColumn("Inventory", MySqlDbType.Text),
                new SqlColumn("Price", MySqlDbType.Int32),
                new SqlColumn("Hidden", MySqlDbType.Int32),
                new SqlColumn("Sell", MySqlDbType.Int32)));
        }

        public List<CTFClass> GetClasses()
        {
            var classes = new List<CTFClass>();
            try {
                using (var reader = db.QueryReader("SELECT * FROM ctfclasses")) {
                    while (reader.Read()) {
                        classes.Add(new CTFClass() {
                            ID = reader.Get<int>("ID"),
                            Name = reader.Get<string>("Name"),
                            Description = reader.Get<string>("Description"),
                            HP = reader.Get<int>("HP"),
                            Mana = reader.Get<int>("Mana"),
                            Inventory = reader.Get<string>("Inventory").Split('~').Select(NetItem.Parse).ToArray(),
                            Price = reader.Get<int>("Price"),
                            Hidden = reader.Get<int>("Hidden") != 0,
                            Sell = reader.Get<int>("Sell") != 0
                        });
                    }
                }
            } catch (Exception ex) {
                TShock.Log.Error(ex.ToString());
            }
            return classes;
        }

        public CTFClass GetClass(string name, bool caseSensitive = false)
        {
            List<CTFClass> classes = GetClasses();
            if (caseSensitive)
                return classes.FirstOrDefault(cls => cls.Name == name);
            else
                return classes.FirstOrDefault(cls => cls.Name.ToLower() == name.ToLower());
        }

        public void SaveClass(CTFClass cls)
        {
            if (cls.ID == -1) {
                try {
                    db.Query("INSERT INTO ctfclasses (Name, Description, HP, " +
                        "Mana, Inventory, Price, Hidden, Sell) " +
                        "VALUES (@0, @1, @2, @3, @4, @5, @6, @7)",
                        cls.Name, cls.Description, cls.HP, cls.Mana, string.Join("~", cls.Inventory),
                        cls.Price, cls.Hidden ? 1 : 0, cls.Sell ? 1 : 0);
                } catch (Exception ex) {
                    TShock.Log.Error(ex.ToString());
                }
            } else {
                try {
                    db.Query("UPDATE ctfclasses SET Name = @0, Description = @1, HP = @2, " +
                        "Mana = @3, Inventory = @4, Price = @5, Hidden = @6, Sell = @7 WHERE ID = @8",
                        cls.Name, cls.Description, cls.HP, cls.Mana, string.Join("~", cls.Inventory),
                        cls.Price, cls.Hidden ? 1 : 0, cls.Sell ? 1 : 0, cls.ID);
                } catch (Exception ex) {
                    TShock.Log.Error(ex.ToString());
                }
            }
        }
    }
}
