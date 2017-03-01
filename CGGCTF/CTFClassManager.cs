using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.Diagnostics;

using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

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

        public List<CTFClass> getClasses()
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

        public CTFClass getClass(string name)
        {
            List<CTFClass> classes = getClasses();
            return classes.FirstOrDefault(cls => cls.Name.ToLower() == name);
        }
    }
}
