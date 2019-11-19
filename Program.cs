using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.IO;
using System.Windows.Forms;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace CavernBotV2ConsoleApp1
{
    class Program
    {
        public static string timeNow = "[" + DateTime.Now.ToString() + "] ";

        public DiscordSocketClient _client = new DiscordSocketClient();
        public static StreamReader keyFile;
        public static string key;

        public static DirectoryInfo cavernBotParentDirectory;

        public static Dictionary<ulong, Guild> GuildDictionary = new Dictionary<ulong, Guild>();

        private CommandService _ownerCommands = new CommandService();
        private CommandService _adminCommands = new CommandService();
        private CommandService _userCommands = new CommandService();

        private IServiceProvider _services;

        static void Main(string[] args)
        {
            if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\DiscordBots\\CavernBotV2"))
            {
                Console.WriteLine(timeNow + "Cavern Bot V2 directory exists");

                cavernBotParentDirectory = Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\DiscordBots\\CavernBotV2");

                try
                {
                    keyFile = File.OpenText(cavernBotParentDirectory.FullName + "\\KeyFile.txt");
                }
                catch (FileNotFoundException)
                {
                    MessageBox.Show("KeyFile Cannot be found.");
                    Application.Exit();
                }

                if (keyFile != null)
                {
                    key = keyFile.ReadToEnd();
                }
            }
            else
            {
                Console.WriteLine(timeNow + "Cavern Bot V2 directory missing");

                cavernBotParentDirectory = Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\DiscordBots\\CavernBotV2");

                Console.WriteLine(timeNow + "Cavern Bot V2 directory created at " + cavernBotParentDirectory.FullName);
                MessageBox.Show("Cavern Bot V2 directory created at " + cavernBotParentDirectory.FullName + " please create a text file called KeyFile containing the bot's token in the created directory.");
                Application.Exit();
            }

            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {

            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_ownerCommands)
                .AddSingleton(_adminCommands)
                .AddSingleton(_userCommands)
                .BuildServiceProvider();

            await InstallCommandsAsync();

            _client.Log += Log;
            _ownerCommands.Log += Log;

            Console.WriteLine(timeNow + "Logging in using key: {0}", key);
            await _client.LoginAsync(TokenType.Bot, key);
            await _client.StartAsync();

            _client.Ready += BuildGuildDictionary;

            //_client.JoinedGuild += AddGuild;

            await Task.Delay(-1);
        }

        public Task Log(LogMessage msg)
        {
            Console.WriteLine(timeNow + msg.Message);
            return Task.CompletedTask;
        }

        public async Task InstallCommandsAsync()
        {
            _client.MessageReceived += HandleCommandAsync;

            await _ownerCommands.AddModuleAsync<OwnerCommands>();

            await _adminCommands.AddModuleAsync<AdminCommands>();

            await _userCommands.AddModuleAsync<UserCommands>();

            Console.WriteLine(timeNow + "Commands installed");
        }

        public Task BuildGuildDictionary()
        {
            foreach (SocketGuild guild in _client.Guilds)
            {
                string currentXmlString = cavernBotParentDirectory.FullName + "\\Guilds\\" + guild.Id + ".xml";
                XmlDocument currentXml = new XmlDocument();

                try
                {
                    currentXml.Load(currentXmlString);
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine(timeNow + "XML file for Guild: " + guild.Name + " - " + guild.Id);
                    CreateGuildXML(guild, currentXml, out currentXml).Wait();
                    Console.WriteLine(timeNow + "Gulid XML document created");
                }

                Guild currentGuild = new Guild
                {
                    guildSocket = guild,

                    xmlFileString = currentXmlString,
                    xml = currentXml,
                    guildSettingsNode = GuildNodeGetter(currentXml.LastChild, "GuildSettings"),
                    commandSettingsNode = GuildNodeGetter(currentXml.LastChild, "Commands"),
                    adminsNode = GuildNodeGetter(currentXml.LastChild, "Admins"),
                    modsNode = GuildNodeGetter(currentXml.LastChild, "Mods"),
                    warnedUsersNode = GuildNodeGetter(currentXml.LastChild, "WarnedUsers"),
                    watchedUsersNode = GuildNodeGetter(currentXml.LastChild, "WatchedUsers")
                };

                currentGuild.FillWarnedUsersDict().Wait();
                currentGuild.FillWatchUsersList().Wait();

                GuildDictionary.Add(guild.Id, currentGuild);
                Console.WriteLine(timeNow + currentGuild.xml.LastChild.Name);
                Console.WriteLine(timeNow + "Guild: " + guild.Name + " - " + guild.Id + " added to guild dictionary");
            }

            Console.WriteLine(timeNow + "Guild dictionary built");
            return Task.CompletedTask;
        }

        public Task CreateGuildXML(SocketGuild guild, XmlDocument xmlIn, out XmlDocument xmlOut)
        {
            xmlIn.LoadXml("<?xml version=\"1.0\" encoding=\"UTF-8\"?> \n" +
                "<Guild> \n" +
                "    <GuildSettings> \n" +
                "        <Commands> \n" +
                "        </Commands> \n" +
                "    </GuildSettings> \n" +
                "    <Admins> \n" +
                "    </Admins> \n" +
                "    <Mods> \n" +
                "    </Mods> \n" +
                "    <WarnedUsers> \n" +
                "    </WarnedUsers> \n" +
                "    <WatchedUsers> \n" +
                "    </WatchedUsers> \n" +
                "</Guild>");
            xmlIn.Save(cavernBotParentDirectory.FullName + "\\Guilds\\" + guild.Id + ".xml");
            xmlOut = xmlIn;
            return Task.CompletedTask;
        }

        public XmlNode GuildNodeGetter(XmlNode parentNode, string nodeName)
        {
            if (parentNode.HasChildNodes)
            {
                foreach (XmlNode node in parentNode.ChildNodes)
                {
                    if (node.Name == nodeName)
                    {
                        Console.Write(timeNow + nodeName + " has been found");
                        return node;
                    }

                    if (node.HasChildNodes)
                    {
                        XmlNode testerNode;

                        EnumerateAllNodes(node, nodeName, out testerNode).Wait();

                        if (testerNode != null)
                        {
                            Console.WriteLine(timeNow + nodeName + " has been found");
                            return testerNode;
                        }
                    }
                }

                Console.WriteLine(timeNow + "Node: " + nodeName + " cannot be found");
                return null;
            }
            else
            {
                return null;
            }
        }

        private Task EnumerateAllNodes(XmlNode parentNode, string nodeName, out XmlNode nodeOut)
        {
            foreach (XmlNode node in parentNode.ChildNodes)
            {
                if (node.Name == nodeName)
                {
                    nodeOut = node;
                    return Task.CompletedTask;
                }

                if (node.HasChildNodes)
                {
                    XmlNode testerNode;

                    EnumerateAllNodes(node, nodeName, out testerNode).Wait();

                    if (testerNode != null)
                    {
                        nodeOut = testerNode;
                        return Task.CompletedTask;
                    }
                }
            }

            nodeOut = null;
            return Task.CompletedTask;
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            //checks if message was from user or system
            var message = messageParam as SocketUserMessage;
            if (message == null) return;

            //sets the possition of the command identifier
            int argPos = 0;

            //creates command context
            var context = new SocketCommandContext(_client, message);

            //checks if message has command prefix
            if (message.HasCharPrefix('>', ref argPos))
            {
                var result = await _ownerCommands.ExecuteAsync(context, argPos, _services);
                if (!result.IsSuccess)
                    await context.Channel.SendMessageAsync(result.ErrorReason);
            }
            if (message.HasCharPrefix(';', ref argPos))
            {
                var result = await _adminCommands.ExecuteAsync(context, argPos, _services);
                if (!result.IsSuccess)
                    await context.Channel.SendMessageAsync(result.ErrorReason);
            }
            if (message.HasCharPrefix('!', ref argPos))
            {
                var result = await _userCommands.ExecuteAsync(context, argPos, _services);
                if (!result.IsSuccess)
                    await context.Channel.SendMessageAsync(result.ErrorReason);
            }
        }
    }

    [RequireOwner]
    public class OwnerCommands : ModuleBase<SocketCommandContext>
    {
        [Group("debug")]
        public class DebugCommands : ModuleBase<SocketCommandContext>
        {
            [Command("say")]
            [Summary("echos a message")]
            public async Task SayAsync([Remainder] string echo)
            {
                await ReplyAsync(echo);
            }

            [Command("warntest")]
            [Summary("tests the sending of a warning dm to the user.")]
            public async Task WarnTest(SocketUser warnedUser, int warningInt, [Remainder] string reason)
            {
                string finalWarning()
                {
                    if (warningInt == Program.GuildDictionary[Context.Guild.Id].warnLimt)
                    {
                        return "This is your final warning";
                    }
                    else
                    {
                        return "Number of warnings: " + warningInt;
                    }
                }

                IDMChannel warnedUserDm = await warnedUser.GetOrCreateDMChannelAsync();
                await warnedUserDm.SendMessageAsync("You have been warned for " + reason + "\n" + finalWarning());
            }
        }

    }

    [RequireGuildOwner]
    public class GuildOwnerCommands : ModuleBase<SocketCommandContext>
    {

    }

    [RequireAdminPrivileges]
    public class AdminCommands : ModuleBase<SocketCommandContext>
    {
        [Command("warn")]
        [Summary("Adds the user to the warn list, bans them if they reach the set warn limit if the limit has been set.")]
        public async Task WarnUser(SocketUser warnedUser, [Remainder] string reason)
        {
            if (Program.GuildDictionary[Context.Guild.Id].WarnUser(warnedUser))
            {
                await Context.Guild.AddBanAsync(warnedUser,0,"You have been banned for reaching the warn limit.");
            }
            else
            {
                IDMChannel warnedUserDm = await warnedUser.GetOrCreateDMChannelAsync();

                await warnedUserDm.SendMessageAsync("You have been warned by " + Context.User.Username + "#" + Context.User.Discriminator + " for " + reason + "\n" + Program.GuildDictionary[Context.Guild.Id].FinalWarning(warnedUser));
            }

            Program.GuildDictionary[Context.Guild.Id].xml.Save(Program.GuildDictionary[Context.Guild.Id].xmlFileString);
        }

        [Command("removewarning")]
        [Summary("removes warnings from users")]
        public async Task RemoveWarnings(SocketUser user, [Remainder] string warningsToRemove = "1")
        {
            if (warningsToRemove.ToLower() == "all")
            {
                if (Program.GuildDictionary[Context.Guild.Id].RemoveWarnings(user, 1, true))
                {
                    await ReplyAsync(user.Username + "'s warnings have been removed.");
                }
                else
                {
                    await ReplyAsync("Cannot find user. Have they been warned?");
                }
            }
            else
            {
                int warnings;

                if (int.TryParse(warningsToRemove, out warnings))
                {
                    if (Program.GuildDictionary[Context.Guild.Id].RemoveWarnings(user, warnings))
                    {
                        await ReplyAsync("You have removed " + warnings + " warnings from " + user.Username);
                    }
                    else
                    {
                        await ReplyAsync("Cannot find user. Have they been warned?");
                    }
                }
                else
                {
                    await ReplyAsync("The string you have passed is niether a number nor \"all\"");
                }
            }

            Program.GuildDictionary[Context.Guild.Id].xml.Save(Program.GuildDictionary[Context.Guild.Id].xmlFileString);
        }

        [Command("checkwarnings")]
        [Summary("allows admins to check how many warnings another user has")]
        public async Task CheckWarnings(SocketUser user = null)
        {
            user = user ?? Context.User;
            Guild guild = Program.GuildDictionary[Context.Guild.Id];

            if (user != Context.User)
            {
                if (guild.warnedUsers.ContainsKey(user.Id))
                {
                    await ReplyAsync(user.Username + " has been warned " + guild.warnedUsers[user.Id] + " times.");
                }
                else
                {
                    await ReplyAsync(user.Username + " has not been warned.");
                }
            }
            else
            {
                if (guild.warnedUsers.ContainsKey(user.Id))
                {
                    await ReplyAsync("You have been warned " + guild.warnedUsers[user.Id] + " times.");
                }
                else
                {
                    await ReplyAsync("You have not been warned");
                }
            }
        }

        [Command("watch")]
        [Summary("adds user to the watch list")]
        public async Task WatchUser(SocketUser watchUser)
        {
            if (Program.GuildDictionary[Context.Guild.Id].WatchUser(watchUser))
            {
                IDMChannel userChannel = await Context.User.GetOrCreateDMChannelAsync();

                await userChannel.SendMessageAsync("You have placed " + watchUser.Username + "#" + watchUser.Discriminator + " on the watchlist for " + Context.Guild.Name);
            }
            else
            {
                IDMChannel userChannel = await Context.User.GetOrCreateDMChannelAsync();

                await userChannel.SendMessageAsync("That user is already on the watchlist.");
            }

            Program.GuildDictionary[Context.Guild.Id].xml.Save(Program.GuildDictionary[Context.Guild.Id].xmlFileString);
        }

        [Command("unwatch")]
        [Summary("removes user from the watch list")]
        public async Task UnwatchUser(SocketUser watchUser)
        {
            if (Program.GuildDictionary[Context.Guild.Id].UnwatchUser(watchUser))
            {
                IDMChannel userChannel = await Context.User.GetOrCreateDMChannelAsync();

                await userChannel.SendMessageAsync("You have removed " + watchUser.Username + "#" + watchUser.Discriminator + " from the watch list for " + Context.Guild.Name);
            }
        }

        [Command("watchlist")]
        [Summary("DM's the watch list to the user")]
        public async Task DmWatchlist()
        {
            IDMChannel userChannel = await Context.User.GetOrCreateDMChannelAsync();
            Guild currentGuild = Program.GuildDictionary[Context.Guild.Id];
            string dmString = "```" +
                "-----Watch List-----" +
                "\n";

            foreach (ulong watchUserId in currentGuild.watchedUsers)
            {
                SocketUser currentUser = Context.Client.GetUser(watchUserId);

                dmString += currentUser.Username + "#" + currentUser.Discriminator + "\n";
            }

            dmString += "--------------------" +
                "\n" +
                "Server: " + Context.Guild.Name +
                "```";

            await userChannel.SendMessageAsync(dmString);
        }
        
    }

    public class UserCommands : ModuleBase<SocketCommandContext>
    {
        [Command("checkwarnings")]
        [Summary("checks if the user has been warned and returns the number of warnings if they have")]
        public async Task CheckWarnings()
        {
            Guild guild = Program.GuildDictionary[Context.Guild.Id];

            if (guild.warnedUsers.ContainsKey(Context.User.Id))
            {
                await ReplyAsync("You have been warned " + guild.warnedUsers[Context.User.Id] + " times.");
            }
            else
            {
                await ReplyAsync("You have no warnings");
            }
        }

    }





    public class RequireModPrivileges : PreconditionAttribute
    {
        public async override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            Guild guild = Program.GuildDictionary[context.Guild.Id];
            var guildOwnerId = context.Guild.OwnerId;
            var userId = context.User.Id;

            bool userFound;

            if (userId == guildOwnerId | guild.mods.Contains(userId) | guild.admins.Contains(userId) | guild.superAdmins.Contains(userId))
            {
                return PreconditionResult.FromSuccess();
            }

            await guild.CheckGuildRoles(context, 1, out userFound);

            if (userFound)
            {
                return PreconditionResult.FromSuccess();
            }
            else
            {
                return PreconditionResult.FromError("You need to be a mod to user this command");
            }
        }
    }

    public class RequireAdminPrivileges : PreconditionAttribute
    {
        public async override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            Guild guild = Program.GuildDictionary[context.Guild.Id];
            var userId = context.User.Id;
            var guildOwnerId = context.Guild.OwnerId;

            bool userFound;

            if (userId == guildOwnerId | guild.admins.Contains(userId) | guild.superAdmins.Contains(userId))
            {
                return PreconditionResult.FromSuccess();
            }

            await guild.CheckGuildRoles(context, 2, out userFound);

            if (userFound)
            {
                return PreconditionResult.FromSuccess();
            }
            else
            {
                return PreconditionResult.FromError("You must be an admin to use this command.");
            }
        }
    }

    public class RequireSuperAdminPrivilages : PreconditionAttribute
    {
        public async override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            Guild guild = Program.GuildDictionary[context.Guild.Id];
            var guildOwnerId = context.Guild.OwnerId;
            var userId = context.User.Id;

            if (guildOwnerId == userId | guild.superAdmins.Contains(userId))
            {
                return PreconditionResult.FromSuccess();
            }
            else if (guild.superAdminRole != null)
            {
                if (guild.superAdminRole.Members.Contains(context.User))
                {
                    return PreconditionResult.FromSuccess();
                }
                else
                {
                    return PreconditionResult.FromError("You need to be a super admin to use this command");
                }
            }
            else
            {
                return PreconditionResult.FromError("You need to be a super admin to use this command");
            }
        }
    }

    public class RequireGuildOwner : PreconditionAttribute
    {
        public async override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var guildOwnerId = context.Guild.OwnerId;
            var userId = context.User.Id;

            if (userId == guildOwnerId)
            {
                return PreconditionResult.FromSuccess();
            }
            else
            {
                return PreconditionResult.FromError("You need to be the owner of the server to use this command");
            }
        }
    }





    class Guild
    {
        public SocketGuild guildSocket;

        public List<ulong> superAdmins = new List<ulong>();
        public List<ulong> admins = new List<ulong>();
        public List<ulong> mods = new List<ulong>();
        public Dictionary<ulong, int> warnedUsers = new Dictionary<ulong, int>();
        public List<ulong> watchedUsers = new List<ulong>();

        public SocketRole superAdminRole = null;
        public SocketRole adminRole = null;
        public SocketRole modRole = null;

        public int warnLimt;

        public string xmlFileString;
        public XmlDocument xml;
        public XmlNode guildSettingsNode;
        public XmlNode commandSettingsNode;
        public XmlNode adminsNode;
        public XmlNode modsNode;
        public XmlNode warnedUsersNode;
        public XmlNode watchedUsersNode;

        public Task CheckGuildRoles(ICommandContext context, int atLeast, out bool found)
        {
            if (modRole != null | adminRole != null | superAdminRole != null)
            {
                if (modRole.Members.Contains(context.User))
                {
                    if (atLeast >= 1)
                    {
                        found = true;
                        return Task.CompletedTask;
                    }
                }

                if (adminRole.Members.Contains(context.User))
                {
                    if (atLeast >=2)
                    {
                        found = true;
                        return Task.CompletedTask;
                    }
                }

                if (superAdminRole.Members.Contains(context.User))
                {
                    if (atLeast >= 3)
                    {
                        found = true;
                        return Task.CompletedTask;
                    }
                }
            }
            else
            {
                found = false;
                return Task.CompletedTask;
            }

            found = false;
            return Task.CompletedTask;
        }

        public bool WarnUser(SocketUser user)
        {
            if (warnedUsers.ContainsKey(user.Id))
            {
                warnedUsers[user.Id] += 1;

                if (warnedUsers[user.Id] > warnLimt && warnLimt != 0)
                {
                    XmlNode warnedUser;

                    FindUserNode(user.Id.ToString(), warnedUsersNode, out warnedUser).Wait();

                    if (warnedUser != null)
                    {
                        warnedUsersNode.RemoveChild(warnedUser);
                    }

                    return true;
                }
                else
                {
                    XmlNode warnedUser;
                    int currentWarnings;

                    FindUserNode(user.Id.ToString(), warnedUsersNode, out warnedUser).Wait();

                    if (warnedUser != null)
                    {
                        int.TryParse(warnedUser.InnerText, out currentWarnings);
                        warnedUser.InnerText = (currentWarnings + 1).ToString();
                    }

                    return false;
                }
            }
            else
            {
                warnedUsers.Add(user.Id, 1);

                XmlElement warnedUserElement = xml.CreateElement("User");

                XmlAttribute attr = xml.CreateAttribute("ID");
                attr.Value = user.Id.ToString();
                warnedUserElement.Attributes.Append(attr);

                warnedUserElement.InnerText = "1";

                warnedUsersNode.AppendChild(warnedUserElement);

                return false;
            }
        }

        public bool RemoveWarnings(SocketUser user, int warningsToRemove = 1, bool removeAll = false)
        {
            if (warnedUsers.ContainsKey(user.Id))
            {
                int currentWarnings = warnedUsers[user.Id];

                if (currentWarnings == 1 || (currentWarnings - warningsToRemove) <= 0 || removeAll == true)
                {
                    XmlNode userNode;

                    FindUserNode(user.Id.ToString(), warnedUsersNode, out userNode).Wait();

                    warnedUsers.Remove(user.Id);
                    warnedUsersNode.RemoveChild(userNode);

                    return true;
                }
                else
                {
                    XmlNode userNode;
                    int warnings;

                    FindUserNode(user.Id.ToString(), warnedUsersNode, out userNode);
                    int.TryParse(userNode.InnerText, out warnings);

                    warnedUsers[user.Id] -= warningsToRemove;
                    warnings -= warningsToRemove;
                    userNode.InnerText = warnings.ToString();

                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        public bool WatchUser(SocketUser user)
        {
            if (watchedUsers.Contains(user.Id))
            {
                return false;
            }
            else
            {
                watchedUsers.Add(user.Id);

                XmlElement watchedUserElement = xml.CreateElement("User");

                XmlAttribute attr = xml.CreateAttribute("ID");
                attr.Value = user.Id.ToString();
                watchedUserElement.Attributes.Append(attr);

                watchedUsersNode.AppendChild(watchedUserElement);

                return true;
            }
        }

        public bool UnwatchUser(SocketUser user)
        {
            if (!watchedUsers.Contains(user.Id))
            {
                return false;
            }
            else
            {
                XmlNode userNode;

                FindUserNode(user.Id.ToString(), watchedUsersNode, out userNode);

                watchedUsers.Remove(user.Id);
                watchedUsersNode.RemoveChild(userNode);

                return true;
            }
        }

        public string FinalWarning(SocketUser user)
        {
            if (warnedUsers[user.Id] == warnLimt)
            {
                return "This is your final warning";
            }
            else
            {
                return "Number of warnings: " + warnedUsers[user.Id];
            }
        }


        private Task FindUserNode(string userId, XmlNode parentNode, out XmlNode outNode)
        {
            if (parentNode.HasChildNodes)
            {
                foreach (XmlNode node in parentNode.ChildNodes)
                {
                    if (node.Attributes[0].Value == userId)
                    {
                        outNode = node;
                        return Task.CompletedTask;
                    }
                }
            }

            outNode = null;
            return Task.CompletedTask;
        }

        public Task FillWarnedUsersDict()
        {
            if (warnedUsersNode != null)
            {
                if (warnedUsersNode.HasChildNodes)
                {
                    foreach (XmlNode node in warnedUsersNode.ChildNodes)
                    {
                        ulong userId;
                        int userWarnings;

                        ulong.TryParse(node.Attributes[0].Value, out userId);
                        int.TryParse(node.InnerText, out userWarnings);

                        warnedUsers.Add(userId, userWarnings);
                    }

                    return Task.CompletedTask;
                }
                else
                {
                    Console.Write(Program.timeNow + "Warned users node for " + guildSocket.Name + " does not have children");
                    return Task.CompletedTask;
                }
            }
            else
            {
                Console.WriteLine(Program.timeNow + "Warned users node for " + guildSocket.Name + " does not exist");
                return Task.CompletedTask;
            }
        }

        public Task FillWatchUsersList()
        {
            if (watchedUsersNode != null)
            {
                if (watchedUsersNode.HasChildNodes)
                {
                    foreach (XmlNode node in watchedUsersNode.ChildNodes)
                    {
                        ulong userId;

                        ulong.TryParse(node.Attributes[0].Value, out userId);

                        watchedUsers.Add(userId);
                    }

                    return Task.CompletedTask;
                }
                else
                {
                    Console.WriteLine(Program.timeNow + "Watched user node for " + guildSocket.Name + " does not have children");
                    return Task.CompletedTask;
                }
            }
            else
            {
                Console.WriteLine(Program.timeNow + "Watched users node fore " + guildSocket.Name + " does not exist");
                return Task.CompletedTask;
            }
        }
    }
}
