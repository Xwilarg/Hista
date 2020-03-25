using Discord.Commands;
using DiscordUtils;
using System.Threading.Tasks;

namespace Hista
{
    public class CommunicationModule : ModuleBase
    {
        [Command("Info")]
        public async Task Info()
        {
            await ReplyAsync("", false, Utils.GetBotInfo(Program.P.StartTime, "Hista", Program.P.client.CurrentUser));
        }

        [Command("Help")]
        public async Task Help()
        {
            await ReplyAsync("There is nothing I'm willing to do to help you.");
        }
    }
}
