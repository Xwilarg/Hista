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
    }
}
