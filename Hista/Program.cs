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
        private Dictionary<string, Tuple<ulong, IRole>> factions;
        private Dictionary<ulong, IRole> defaultFactions;
        private Dictionary<ulong, IRole> speakingRole;
        private List<ulong> factionBlacklist;

        private Db.Db botDb;

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
            botDb = new Db.Db();
            await botDb.InitAsync();

            roles = new Dictionary<string, Tuple<ulong, IRole>>();
            factions = new Dictionary<string, Tuple<ulong, IRole>>();
            defaultFactions = new Dictionary<ulong, IRole>();
            factionBlacklist = new List<ulong>();
            speakingRole = new Dictionary<ulong, IRole>();

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
                    await AddMissingReaction(msg, match.Success ? (IEmote)Emote.Parse((string)elem.emote) : new Emoji((string)elem.emote));
                }
            }
            if (File.Exists("Keys/factions.json"))
            {
                dynamic json = JsonConvert.DeserializeObject(File.ReadAllText("Keys/factions.json"));
                foreach (ulong elem in json.blacklist)
                {
                    factionBlacklist.Add(elem);
                }
                foreach (dynamic elem in json.roles)
                {
                    Match match = Regex.Match((string)elem.emote, "<:([^:]+):[0-9]{18}>>");
                    factions.Add(match.Success ? match.Groups[1].Value : (string)elem.emote, new Tuple<ulong, IRole>((ulong)elem.messageId, client.GetGuild((ulong)elem.guildId).GetRole((ulong)elem.roleId)));
                    var msg = (IUserMessage)await client.GetGuild((ulong)elem.guildId).GetTextChannel((ulong)elem.channelId).GetMessageAsync((ulong)elem.messageId);
                    await AddMissingReaction(msg, match.Success ? (IEmote)Emote.Parse((string)elem.emote) : new Emoji((string)elem.emote));
                }
                foreach (dynamic elem in json.defaultRole)
                {
                    IGuild guild = client.GetGuild((ulong)elem.guildId);
                    IRole role = guild.GetRole((ulong)elem.roleId);
                    defaultFactions.Add((ulong)elem.guildId, role);
                    foreach (IGuildUser user in await guild.GetUsersAsync())
                        if (!user.RoleIds.Contains(role.Id) && !user.RoleIds.Any(x => factions.Any(y => x == y.Value.Item2.Id)))
                            await user.AddRoleAsync(role);
                }
            }
            if (File.Exists("Keys/active.json"))
            {
                dynamic json = JsonConvert.DeserializeObject(File.ReadAllText("Keys/active.json"));
                foreach (dynamic elem in json.roles)
                {
                    IGuild guild = client.GetGuild((ulong)elem.guildId);
                    speakingRole.Add(guild.Id, guild.GetRole((ulong)elem.roleId));
                }
            }
            _ = Task.Run(async () =>
            {
                foreach (var elem in speakingRole)
                {
                    await botDb.RemoveRoles(client.GetGuild(elem.Key), elem.Value);
                }
                await Task.Delay(3600000); // 1 hour
            });
        }

        private async Task AddMissingReaction(IUserMessage msg, IEmote emote)
        {
            if (!msg.Reactions.Any(x => x.Value.IsMe && x.Key.Name == emote.Name))
                await msg.AddReactionAsync(emote); // TODO: Emote.Parse doesn't work ?
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
            if (factions.ContainsKey(reaction.Emote.Name))
            {
                var role = factions[reaction.Emote.Name];
                if (msg.Id == role.Item1)
                {
                    if (factionBlacklist.Contains(reaction.UserId)) // We ignore blacklisted users
                    {
                        await (await msg.GetOrDownloadAsync()).RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                        return;
                    }
                    IGuildUser author = (IGuildUser)reaction.User.Value;
                    foreach (var elem in factions) // If the user was already in another faction
                    {
                        if (elem.Key == reaction.Emote.Name)
                            continue;
                        var elemMsg = (IUserMessage)await chan.GetMessageAsync(elem.Value.Item1);
                        KeyValuePair<IEmote, ReactionMetadata>? react = elemMsg.Reactions.FirstOrDefault(x => x.Value.IsMe && x.Key.Name == elem.Key);
                        if (react != null)
                        {
                            await author.RemoveRoleAsync(elem.Value.Item2);
                            await elemMsg.RemoveReactionAsync(react.Value.Key, reaction.User.Value);
                            break;
                        }
                    }
                    if (!author.RoleIds.Contains(role.Item2.Id))
                        await author.AddRoleAsync(role.Item2);
                    if (defaultFactions.ContainsKey(author.GuildId))
                    {
                        ulong id = defaultFactions[author.GuildId].Id;
                        if (author.RoleIds.Any(x => x == id))
                            await author.RemoveRoleAsync(defaultFactions[author.GuildId]);
                    }
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
            if (factions.ContainsKey(reaction.Emote.Name))
            {
                var role = factions[reaction.Emote.Name];
                if (msg.Id == role.Item1)
                {
                    IGuildUser author = (IGuildUser)reaction.User.Value;
                    if (author.RoleIds.Contains(role.Item2.Id))
                        await author.RemoveRoleAsync(role.Item2);
                    if (defaultFactions.ContainsKey(author.GuildId))
                        await author.AddRoleAsync(defaultFactions[author.GuildId]);
                }
            }
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            SocketUserMessage msg = arg as SocketUserMessage;
            if (msg == null) return;
            if (!arg.Author.IsBot)
            {
                int pos = 0;
                if (msg.HasMentionPrefix(client.CurrentUser, ref pos) || msg.HasStringPrefix("h.", ref pos))
                {
                    SocketCommandContext context = new SocketCommandContext(client, msg);
                    await commands.ExecuteAsync(context, pos, null);
                }
            }
            ITextChannel chan = msg.Channel as ITextChannel;
            if (chan != null && speakingRole.ContainsKey(chan.GuildId))
                await botDb.UpdateUser((IGuildUser)msg.Author, DateTime.Now, speakingRole[chan.GuildId]);
        }
    }
}