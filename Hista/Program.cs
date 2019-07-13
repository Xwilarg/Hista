using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hista
{
    class Program
    {
        public static async Task Main()
            => await new Program().MainAsync();

        public readonly DiscordSocketClient client;
        private readonly CommandService commands = new CommandService();

        public DateTime StartTime { private set; get; }
        public static Program P { private set; get; }
        private Dictionary<string, Tuple<ulong, IRole>> roles;

        private Program()
        {
            P = this;
            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
            });
            client.Log += Utils.Log;
            commands.Log += Utils.LogError;
        }

        private async Task MainAsync()
        {
            roles = new Dictionary<string, Tuple<ulong, IRole>>();

            client.MessageReceived += HandleCommandAsync;
            client.ReactionAdded += ReactionAdded;
            client.ReactionAdded += ReactionRemoved;
            client.Ready += Ready;

            await commands.AddModuleAsync<CommunicationModule>(null);

            if (!File.Exists("Keys/token.txt"))
                throw new FileNotFoundException("Missing token.txt in Keys/");
            await client.LoginAsync(TokenType.Bot, File.ReadAllText("Keys/token.txt"));
            StartTime = DateTime.Now;
            await client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task Ready()
        {
            if (File.Exists("Keys/roles.txt"))
            {
                string[] lines = File.ReadAllLines("Keys/roles.txt");
                foreach (string line in lines)
                {
                    if (line.StartsWith("//"))
                        continue;
                    // Emote name, Guild ID, Channel ID, Message ID, Role ID
                    string[] parts = line.Split(' ');
                    Match match = Regex.Match(parts[0], "<:([^:]+):[0-9]{18}>>");
                    roles.Add(match.Success ? match.Groups[1].Value : parts[0], new Tuple<ulong, IRole>(ulong.Parse(parts[3]), client.GetGuild(ulong.Parse(parts[1])).GetRole(ulong.Parse(parts[4]))));
                    await ((IUserMessage)await client.GetGuild(ulong.Parse(parts[1])).GetTextChannel(ulong.Parse(parts[2])).GetMessageAsync(ulong.Parse(parts[3])))
                        .AddReactionAsync(match.Success ? (IEmote)Emote.Parse(parts[0]) : new Emoji(parts[0]));
                }
            }
        }

        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> msg, ISocketMessageChannel chan, SocketReaction reaction)
        {
            if (roles.ContainsKey(reaction.Emote.Name))
            {
                var role = roles[reaction.Emote.Name];
                if (msg.Id == role.Item1)
                {
                    IGuildUser author = (IGuildUser)(await msg.GetOrDownloadAsync()).Author;
                    if (!author.RoleIds.Contains(role.Item2.Id))
                        await author.AddRoleAsync(role.Item2);
                }
            }
        }

        private async Task ReactionRemoved(Cacheable<IUserMessage, ulong> msg, ISocketMessageChannel chan, SocketReaction reaction)
        {
            if (roles.ContainsKey(reaction.Emote.Name))
            {
                var role = roles[reaction.Emote.Name];
                if (msg.Id == role.Item1)
                {
                    IGuildUser author = (IGuildUser)(await msg.GetOrDownloadAsync()).Author;
                    if (author.RoleIds.Contains(role.Item2.Id))
                        await author.RemoveRoleAsync(role.Item2);
                }
            }
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            SocketUserMessage msg = arg as SocketUserMessage;
            if (msg == null || arg.Author.IsBot) return;
            int pos = 0;
            if (msg.HasMentionPrefix(client.CurrentUser, ref pos) || msg.HasStringPrefix("h.", ref pos))
            {
                SocketCommandContext context = new SocketCommandContext(client, msg);
                await commands.ExecuteAsync(context, pos, null);
            }
        }
    }
}