using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordUtils;
using Newtonsoft.Json;
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
            client.ReactionRemoved += ReactionRemoved;
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
            if (File.Exists("Keys/roles.json"))
            {
                dynamic json = JsonConvert.DeserializeObject(File.ReadAllText("Keys/roles.json"));
                foreach (dynamic elem in json.roles)
                {
                    Match match = Regex.Match((string)elem.emote, "<:([^:]+):[0-9]{18}>>");
                    roles.Add(match.Success ? match.Groups[1].Value : (string)elem.emote, new Tuple<ulong, IRole>((ulong)elem.messageId, client.GetGuild((ulong)elem.guildId).GetRole((ulong)elem.roleId)));
                    var msg = (IUserMessage)await client.GetGuild((ulong)elem.guildId).GetTextChannel((ulong)elem.channelId).GetMessageAsync((ulong)elem.messageId);
                    if (!msg.Reactions.Any(x => x.Value.IsMe && x.Key.Name == (string)elem.emote))
                        await msg.AddReactionAsync(match.Success ? (IEmote)Emote.Parse((string)elem.emote) : new Emoji((string)elem.emote)); // TODO: Emote.Parse doesn't work ?
                }
            }
            else if (File.Exists("Keys/factions.txt"))
            {

            }
        }

        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> msg, ISocketMessageChannel chan, SocketReaction reaction)
        {
            if (roles.ContainsKey(reaction.Emote.Name))
            {
                var role = roles[reaction.Emote.Name];
                if (msg.Id == role.Item1)
                {
                    IGuildUser author = (IGuildUser)reaction.User.Value;
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
                    IGuildUser author = (IGuildUser)reaction.User.Value;
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