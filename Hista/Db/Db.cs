using Discord;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Hista.Db
{
    public class Db
    {
        public Db()
        {
            R = RethinkDB.R;
            lastSpoke = new Dictionary<ulong, DateTime>();
        }

        public async Task InitAsync(string dbName = "Hista")
        {
            this.dbName = dbName;
            conn = await R.Connection().ConnectAsync();
            if (!await R.DbList().Contains(dbName).RunAsync<bool>(conn))
                await R.DbCreate(dbName).RunAsync(conn);
            if (!await R.Db(dbName).TableList().Contains("Users").RunAsync<bool>(conn))
                await R.Db(dbName).TableCreate("Users").RunAsync(conn);
            foreach (dynamic elem in await R.Db(dbName).Table("Users").RunAsync(conn))
            {
                lastSpoke.Add(ulong.Parse((string)elem.id), DateTime.ParseExact((string)elem.date, "yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture));
            }
        }

        public async Task RemoveRoles(IGuild guild, IRole role) // TODO: Manage many guilds
        {
            var now = DateTime.Now;
            foreach (var elem in lastSpoke)
            {
                if ((int)now.Subtract(elem.Value).TotalDays >= 7)
                {
                    await (await guild.GetUserAsync(elem.Key)).RemoveRoleAsync(role);
                    await R.Db(dbName).Table("Users").Filter(R.HashMap("id", elem.Key.ToString())).Delete().RunAsync(conn);
                    lastSpoke.Remove(elem.Key);
                }
            }
        }

        public async Task UpdateUser(IGuildUser user, DateTime now, IRole role)
        {
            if (lastSpoke.ContainsKey(user.Id))
            {
                if ((int)now.Subtract(lastSpoke[user.Id]).TotalDays != 0)
                {
                    await R.Db(dbName).Table("Users").Update(R.HashMap("id", user.Id.ToString())
                        .With("date", now.ToString("yyyy/MM/dd HH:mm:ss"))
                        ).RunAsync(conn);
                    lastSpoke[user.Id] = DateTime.Now;
                    await user.AddRoleAsync(role);
                }
            }
            else
            {
                await R.Db(dbName).Table("Users").Insert(R.HashMap("id", user.Id.ToString())
                    .With("date", now.ToString("yyyy/MM/dd HH:mm:ss"))
                    ).RunAsync(conn);
                lastSpoke.Add(user.Id, now);
                await user.AddRoleAsync(role);
            }
        }

        private Dictionary<ulong, DateTime> lastSpoke;

        private RethinkDB R;
        private Connection conn;
        private string dbName;
    }
}