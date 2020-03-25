using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hista.Db
{
    public class Db
    {
        public Db()
        {
            R = RethinkDB.R;
            lastSpoke = new Dictionary<string, DateTime>();
        }

        public async Task InitAsync(string dbName = "Hista")
        {
            this.dbName = dbName;
            conn = await R.Connection().ConnectAsync();
            if (!await R.DbList().Contains(dbName).RunAsync<bool>(conn))
                await R.DbCreate(dbName).RunAsync(conn);
            if (!await R.Db(dbName).TableList().Contains("Users").RunAsync<bool>(conn))
                await R.Db(dbName).TableCreate("Users").RunAsync(conn);
        }

        private Dictionary<string, DateTime> lastSpoke;

        private RethinkDB R;
        private Connection conn;
        private string dbName;
    }
}