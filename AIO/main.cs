using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;


namespace AIO
{
    [ApiVersion(1, 17)]
    public class AIO : TerrariaPlugin
    {
        #region items
        List<Backup> Backups = new List<Backup>();
        bool lastchest = false;
        int bckup = 0;
        bool canrollback = false;

        private List<Report> HouseLoc = new List<Report>();
        private List<Report> GriefLoc = new List<Report>();
        List<string> cgrief = new List<string>();
        List<string> cbuilding = new List<string>();

        IDbConnection Database;
        bool usinginfchests = false;
        string filepath = Path.Combine("tshock", "logs");

        Random rnd = new Random();

        List<string> spies = new List<string>();
        List<string> frozenplayer = new List<string>();
        public List<string> staffchatplayers = new List<string>();

        Color staffchatcolor = new Color(200, 50, 150);
        DateTime LastCheck = DateTime.UtcNow;

        short[] torchframey = new short[] { 0, 22, 44, 66, 88, 110, 132, 154, 176, 198, 220, 242 };
        short[] platformframey = new short[] { 0, 18, 36, 54, 72, 90, 108, 144, 234, /* halloween ->*/ 228 };
        int[] tiles = new int[] { 38, 39, 41, 43, 44, 45, 47, 54, 118, 119, 121, 122, 140, 145, 146, 148, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 175, 176, 177, 189, 190, 191, 193, 194, 195, 196, 197, 198, 202, 206, 208, 225, 226, 229, 230, 248, 249, 250, /* halloween ->*/ 251, 252, 253 };
        int[] walls = new int[] { 4, 5, 6, 7, 9, 10, 11, 12, 19, 21, 22, 23, 24, 25, 26, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 41, 42, 43, 45, 46, 47, 72, 73, 74, 75, 76, 78, 82, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 109, 110, /* halloween ->*/ 113, 114, 115 };
        int[] chests = new int[] { 1, 3, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 };
        #endregion

        #region plugin info
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }
        public override string Author
        {
            get { return "Ancientgods"; }
        }
        public override string Name
        {
            get { return "AIO"; }
        }

        public override string Description
        {
            get { return "all-in-one plugin, now compatible with infinite chests!"; }
        }
        #endregion


        #region initialize
        public override void Initialize()
        {
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);


            #region staffchatcommands
            Commands.ChatCommands.Add(new Command(staffchat, "s"));
            Commands.ChatCommands.Add(new Command("aio.staffchat.admin", staffchatkick, "skick"));
            Commands.ChatCommands.Add(new Command("aio.staffchat.admin", staffchatinvite, "sinvite"));
            Commands.ChatCommands.Add(new Command("aio.staffchat.admin", staffchatclear, "sclear"));
            Commands.ChatCommands.Add(new Command("tshock.world.modify", staffchatlist, "slist"));
            #endregion
            #region report grief/building
            Commands.ChatCommands.Add(new Command("tshock.world.modify", reportgrief, "reportgrief", "rg") { AllowServer = false });
            Commands.ChatCommands.Add(new Command("aio.checkgrief", checkgrief, "checkgrief", "cg") { AllowServer = false });
            Commands.ChatCommands.Add(new Command("aio.listgrief", listgrief, "listgrief", "lg"));
            Commands.ChatCommands.Add(new Command("aio.checkbuilding", checkbuilding, "checkbuilding", "cb") { AllowServer = false });
            Commands.ChatCommands.Add(new Command("aio.listbuilding", listbuilding, "listbuilding", "lb"));
            Commands.ChatCommands.Add(new Command("tshock.world.modify", building, "building") { AllowServer = false });
            #endregion
            #region other commands
            Commands.ChatCommands.Add(new Command(staff, "staff"));
            Commands.ChatCommands.Add(new Command("aio.freeze", freeze, "freeze"));
            Commands.ChatCommands.Add(new Command("aio.read", GetItemOrBuff, "read"));
            Commands.ChatCommands.Add(new Command("aio.copy", copyitems, "copy") { AllowServer = false });
            Commands.ChatCommands.Add(new Command("aio.worldgen", world_gen, "gen") { AllowServer = false });
            Commands.ChatCommands.Add(new Command("aio.spywhisper", SPY, "spywhisper"));
            Commands.ChatCommands.Add(new Command("aio.chestroom", chestroom, "chestroom", "cr"));
            Commands.ChatCommands.Add(new Command("aio.chestroom", undochestroom, "undochestroom", "undocr"));
            #endregion

            if (File.Exists(Path.Combine(Environment.CurrentDirectory, "ServerPlugins\\InfiniteChests.dll")))
            {
                switch (TShock.Config.StorageType.ToLower())
                {
                    case "mysql":
                        string[] host = TShock.Config.MySqlHost.Split(':');
                        Database = new MySqlConnection()
                        {
                            ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                                    host[0],
                                    host.Length == 1 ? "3306" : host[1],
                                    TShock.Config.MySqlDbName,
                                    TShock.Config.MySqlUsername,
                                    TShock.Config.MySqlPassword)
                        };
                        break;
                    case "sqlite":
                        string sql = Path.Combine(TShock.SavePath, "chests.sqlite");
                        Database = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                        break;
                }
                usinginfchests = true;
            }
            
        }
        #endregion

        #region dispose
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
            }
            base.Dispose(disposing);
        }
        #endregion

        #region main game
        public AIO(Main game)
            : base(game)
        {
            Order = 9999;
        }
        #endregion

        #region onchat
        public void OnChat(ServerChatEventArgs args)
        {
            List<string> whisperalts = new List<string> { "w", "whisper", "t", "tell", "r", "reply" };
            if (args.Text.StartsWith(TShock.Config.CommandSpecifier) || args.Text.StartsWith(TShock.Config.CommandSilentSpecifier))
            {
                foreach (string alt in whisperalts)
                {
                    if (args.Text.StartsWith("{0}{1} ".SFormat(args.Text[0].ToString(), alt)))
                    {
                        foreach (TSPlayer ts in TShock.Players)
                        {
                            if (ts != null)
                            {
                                if (spies.Contains(ts.IP))
                                {
                                    ts.SendMessage(TShock.Players[args.Who].Name + ": " + args.Text, staffchatcolor);
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region onupdate
        public void OnUpdate(EventArgs e)
        {
            WorldGen.spawnMeteor = false;
            if ((DateTime.UtcNow - LastCheck).TotalSeconds >= 3)
            {
                LastCheck = DateTime.UtcNow;
                foreach (TSPlayer ts in TShock.Players)
                {
                    if (ts != null)
                    {
                        if (frozenplayer.Contains(ts.IP))
                        {
                            ts.SetBuff(47, 240, true);
                            ts.SetBuff(80, 240, true);
                            ts.SetBuff(23, 240, true);
                        }
                    }
                }
            }
        }
        #endregion

        #region commands
        #region staffchat
        private void staffchat(CommandArgs args)
        {
            if (args.Player.Group.HasPermission("aio.staffchat.chat") || staffchatplayers.Contains(args.Player.IP))
            {
                if (args.Parameters.Count >= 1)
                {
                    foreach (TSPlayer ts in TShock.Players)
                    {
                        if (ts != null)
                        {
                            if (ts.Group.HasPermission("aio.staffchat.chat") || staffchatplayers.Contains(ts.IP))
                            {
                                string message = string.Join(" ", args.Parameters);
                                ts.SendMessage("[Staffchat] " + args.Player.User.Name + ": " + message, staffchatcolor);
                            }
                        }
                    }
                }
                else             
                    args.Player.SendMessage("{0}s \"[Message]\" is the right format.".SFormat((args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier)), staffchatcolor);               
            }
            else
                args.Player.SendErrorMessage("You have not been invited to the staffchat!");
        }

        private void staffchatinvite(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Syntax: {0}sinvite <player>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                return;
            }
            var foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
            if (foundplr.Count == 0)
            {
                args.Player.SendErrorMessage("Invalid player!");
            }
            else if (foundplr.Count > 1)
            {
                TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
            }
            var plr = foundplr[0];
            {
                if (!staffchatplayers.Contains(plr.IP) && !plr.Group.HasPermission("aio.staffchat.chat"))
                {
                    staffchatplayers.Add(plr.IP);
                    plr.SendInfoMessage("You have been invited to the staffchat, type {0}s [message] to talk.", (TShock.Config.CommandSpecifier));
                    foreach (TSPlayer ts in TShock.Players)
                    {
                        if (ts != null)
                        {
                            if (ts.Group.HasPermission("aio.staffchat.chat"))
                            {
                                ts.SendErrorMessage(plr.Name + " has been invited to the staffchat.");
                            }
                        }
                    }
                }
                else
                    args.Player.SendErrorMessage("Player is already in the staffchat.");
            }
        }
        private void staffchatkick(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Syntax: {0}skick <player>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                return;
            }
            List<TSPlayer> foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
            if (foundplr.Count == 0)
            {
                args.Player.SendErrorMessage("Invalid player!");
            }
            else if (foundplr.Count > 1)
            {
                TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
            }
            var plr = foundplr[0];
            {
                if (staffchatplayers.Contains(plr.IP) && !plr.Group.HasPermission("aio.staffchat.chat"))
                {
                    staffchatplayers.Remove(plr.IP);
                    plr.SendInfoMessage("You have been removed from the staffchat.");
                    foreach (TSPlayer ts in TShock.Players)
                    {
                        if (ts != null)
                        {
                            if (ts.Group.HasPermission("aio.staffchat.chat"))
                            {
                                if (!args.Silent)
                                    ts.SendSuccessMessage(plr.Name + " has been removed from the staffchat.");
                            }
                        }
                    }
                }
                else if (plr.Group.HasPermission("aio.staffchat.chat"))
                    args.Player.SendErrorMessage("You can't kick a staff member from staffchat!");
                else
                    args.Player.SendErrorMessage("This player hasn't been invited to staffchat!");
            }
        }
        private void staffchatclear(CommandArgs args)
        {
            foreach (TSPlayer ts in TShock.Players)
            {
                if (ts != null)
                {
                    if (staffchatplayers.Contains(ts.IP))
                    {
                        if (!args.Silent)
                            ts.SendInfoMessage("You have been removed from the staffchat.");
                    }
                }
            }
            staffchatplayers.Clear();
            foreach (TSPlayer ts in TShock.Players)
            {
                if (ts != null)
                {
                    if (ts.Group.HasPermission("aio.staffchat.chat"))
                    {
                        if (!args.Silent)
                            ts.SendInfoMessage("Staffchat invites have been cleared!");
                    }
                }
            }
        }
        private void staffchatlist(CommandArgs args)
        {
            List<string> staffchatlist = new List<string>();
            //string staffchatlist = "";
            foreach (TSPlayer ts in TShock.Players)
            {
                if (ts != null)
                {
                    if (staffchatplayers.Contains(ts.IP))
                    {
                        staffchatlist.Add(ts.Name);
                    }
                }
            }
            args.Player.SendInfoMessage("Players in staffchat: {0}", string.Join(", ", staffchatlist));
        }
        #endregion

        #region list of online staff
        public void staff(CommandArgs args)
        {
            List<TSPlayer> Staff = new List<TSPlayer>(TShock.Players).FindAll(t => t != null && t.Group.HasPermission("aio.staffchat.chat"));
            if (Staff.Count == 0)
            {
                args.Player.SendErrorMessage("No staff members currently online.");
                return;
            }
            args.Player.SendMessage("[Currently online staff members]", Color.Red);
            foreach (TSPlayer who in Staff)
            {
                if (who != null)
                {
                    {
                        Color groupcolor = new Color(who.Group.R, who.Group.G, who.Group.B);
                        args.Player.SendMessage(string.Format("{0}{1}", who.Group.Prefix, who.Name), groupcolor);
                    }
                }
            }
        }
        #endregion

        #region reportgrief
        public void reportgrief(CommandArgs args)
        {
            int x = args.Player.TileX;
            int y = args.Player.TileY;
            foreach (Report loc in GriefLoc)
            {
                int lx = loc.X;
                int ly = loc.Y;
                if (lx > x - 50 && ly > y - 50 && lx < x + 50 && ly < y + 50)
                {
                    args.Player.SendErrorMessage("This location has already been reported!");
                    return;
                }
            }
            GriefLoc.Add(new Report(args.Player.TileX, args.Player.TileY, args.Player.User.Name, DateTime.UtcNow));
            args.Player.SendSuccessMessage("Your grief has been reported!");
            Console.WriteLine(string.Format("{0} has sent in a grief report at: {1}, {2}", args.Player.User.Name, args.Player.TileX, args.Player.TileY));
            File.AppendAllText(Path.Combine(filepath, DateTime.Now.ToString("yyyy-MM-dd") + ".log"), "{3} :: {0} reported a grief at POS ({1}, {2}).\n".SFormat(args.Player.User.Name, args.Player.TileX, args.Player.TileY, DateTime.Now.ToString("g")));
            foreach (TSPlayer ts in TShock.Players)
            {
                if (ts != null)
                {
                    if (ts.Group.HasPermission("aio.checkgrief"))
                    { ts.SendInfoMessage("{0} has sent in a grief report at: {1}, {2}", args.Player.User.Name, args.Player.TileX, args.Player.TileY); }
                }
            }
        }
        public void listgrief(CommandArgs args)
        {
            if (GriefLoc.Count == 0)
            {
                args.Player.SendErrorMessage("There currently isn't any reported grief");
                return;
            }
            for (int i = 0; i < GriefLoc.Count; i++)
            {
                Report Re = GriefLoc[i];
                args.Player.SendInfoMessage(string.Format("[{0}] {1} reported a grief at POS ({2},{3}) at {4}", (i + 1).ToString(), Re.Name, Re.X, Re.Y, Re.Date));
            }

        }
        public void checkgrief(CommandArgs args)
        {
            if (GriefLoc.Count == 0)
            {
                args.Player.SendErrorMessage("There currently isn't any reported grief");
                return;
            }
            for (int i = 0; i < GriefLoc.Count; i++)
            {
                Report Re = GriefLoc[i];
                if (Re != null)
                {
                    cgrief.Add("{0} checked the grief at POS ({1}, {2}).".SFormat(args.Player.User.Name, Re.X, Re.Y));
                    File.AppendAllText(Path.Combine(filepath, DateTime.Now.ToString("yyyy-MM-dd") + ".log"), "{3} :: {0} checked the grief at POS ({1}, {2}).\n".SFormat(args.Player.User.Name, Re.X, Re.Y, DateTime.Now.ToString("g")));
                    args.Player.Teleport(Re.X * 16, Re.Y * 16);
                    args.Player.SendInfoMessage("Reported by: {0} at {1}", Re.Name, Re.Date);
                    GriefLoc.Remove(Re);
                    i = GriefLoc.Count;
                }
            }
        }
        #endregion

        #region reportbuilding
        public void checkbuilding(CommandArgs args)
        {
            if (HouseLoc.Count == 0)
            {
                args.Player.SendErrorMessage("There currently isn't any reported building");
                return;
            }
            for (int i = 0; i < HouseLoc.Count; i++)
            {
                Report Re = HouseLoc[i];
                if (Re != null)
                {
                    cbuilding.Add("{0} checked building at POS ({1}, {2}).".SFormat(args.Player.User.Name, Re.X, Re.Y));
                    File.AppendAllText(Path.Combine(filepath, DateTime.Now.ToString("yyyy-MM-dd") + ".log"), "{3} :: {0} checked building at POS ({1}, {2}).\n".SFormat(args.Player.User.Name, Re.X, Re.Y, DateTime.Now.ToString("g")));
                    args.Player.Teleport(Re.X * 16, Re.Y * 16);
                    args.Player.SendInfoMessage("Reported by: {0} at {1}", Re.Name, Re.Date);
                    HouseLoc.Remove(Re);
                    i = HouseLoc.Count;
                }
            }
        }
        public void listbuilding(CommandArgs args)
        {
            if (HouseLoc.Count == 0)
            {
                args.Player.SendErrorMessage("There currently aren't any reported buildings");
                return;
            }
            for (int i = 0; i < HouseLoc.Count; i++)
            {
                Report Re = HouseLoc[i];
                args.Player.SendInfoMessage("[{0}] {1} reported a building at POS ({2},{3}) at {4}", (i + 1).ToString(), Re.Name, Re.X, Re.Y, Re.Date);
            }

        }
        public void building(CommandArgs args)
        {
            int x = args.Player.TileX;
            int y = args.Player.TileY;
            foreach (Report loc in HouseLoc)
            {
                int lx = loc.X;
                int ly = loc.Y;
                if (lx > x - 50 && ly > y - 50 && lx < x + 50 && ly < y + 50)
                {
                    args.Player.SendErrorMessage("This location has already been reported! Please wait until someone is available to protect it for you.");
                    return;
                }
            }
            if (args.Player.CurrentRegion == null)
            {
                HouseLoc.Add(new Report(args.Player.TileX, args.Player.TileY, args.Player.User.Name, DateTime.UtcNow));
                args.Player.SendSuccessMessage("Your House has been reported at {0}, {1}.", args.Player.TileX, args.Player.TileY);
                Console.WriteLine(string.Format("{0} has reported a house at: {1}, {2}", args.Player.User.Name, args.Player.TileX, args.Player.TileY));
                File.AppendAllText(Path.Combine(filepath, DateTime.Now.ToString("yyyy-MM-dd") + ".log"), "{3} :: {0} reported a building at POS ({1}, {2}).\n".SFormat(args.Player.User.Name, args.Player.TileX, args.Player.TileY, DateTime.Now.ToString("g")));
                foreach (TSPlayer ts in TShock.Players)
                {
                    if (ts != null)
                    {
                        if (ts.Group.HasPermission("aio.checkbuilding"))
                        { ts.SendInfoMessage("{0} has reported a house at: {1}, {2}", args.Player.User.Name, args.Player.TileX, args.Player.TileY); }
                    }
                }
            }
            else {
                if (args.Player.CurrentRegion.Owner == args.Player.User.Name)
                {
                    args.Player.SendSuccessMessage("This building has been protected for you!");
                }
                else if (args.Player.CurrentRegion.AllowedIDs.Contains(args.Player.User.ID))
                {
                    args.Player.SendSuccessMessage("This building has been protected and you have been allowed to build in it!");
                }
                else
                {
                    args.Player.SendSuccessMessage("This building has been protected for its owner!");
                }
            }
        }
        #endregion
        
        #region freeze
        public void freeze(CommandArgs args)
        {
            if (args.Player != null)
            {
                if (args.Parameters.Count != 1)
                {
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}freeze [player]", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    return;
                }
                var foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
                if (foundplr.Count == 0)
                {
                    args.Player.SendErrorMessage("Invalid player!");
                    return;
                }
                else if (foundplr.Count > 1)
                {
                    TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
                    return;
                }
                var plr = foundplr[0];
                if (!frozenplayer.Contains(plr.IP))
                {
                    frozenplayer.Add(plr.IP);
                    if (!args.Silent)
                        TSPlayer.All.SendInfoMessage("{0} froze {1}", args.Player.User.Name, plr.Name);
                    return;
                }
                else
                {
                    frozenplayer.Remove(plr.IP);
                    if (!args.Silent)
                        TSPlayer.All.SendInfoMessage("{0} unfroze {1}", args.Player.User.Name, plr.Name);
                    return;
                }
            }
        }
        #endregion freeze

        #region read items/buffs/dye slots/armor
        void GetItemOrBuff(CommandArgs args)
        {
            int amount = 0;
            if (args.Player != null)
            {
                if (args.Parameters.Count != 2)
                {
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}read <inventory/buff/armor/dye> <player>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    return;
                }
                var foundplr = TShock.Utils.FindPlayer(args.Parameters[1]);
                if (foundplr.Count == 0)
                {
                    args.Player.SendErrorMessage("Invalid player!");
                    return;
                }
                else if (foundplr.Count > 1)
                {
                    TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
                    return;
                }
                var plr = foundplr[0];
                string cmd = args.Parameters[0].ToString().ToLower();
                switch (cmd)
                {
                    case "item":
                    case "items":
                    case "inventory":
                        List<string> items = new List<string>();
                        foreach (Item Item in plr.TPlayer.inventory)
                        {
                            if (Item.active && Item.netID > 0) { items.Add(Item.name); amount++; }
                        }
                        if (amount <= 0) { args.Player.SendInfoMessage("Player currently has no items in inventory."); return; }
                        args.Player.SendInfoMessage(string.Join(", ", items));
                        return;
                    case "buff":
                    case "buffs":
                        List<string> buffs = new List<string>();
                        foreach (int BuffId in plr.TPlayer.buffType)
                        {
                            if (BuffId > 0) { buffs.Add(TShock.Utils.GetBuffName(BuffId)); }
                        }
                        if (plr.TPlayer.countBuffs() <= 0) { args.Player.SendInfoMessage("Player currently has no buffs."); return; }
                        args.Player.SendInfoMessage(string.Join(", ", buffs));
                        return;
                    case "armor":
                        List<string> armor = new List<string>();
                        foreach (Item InvItem in plr.TPlayer.armor)
                        {
                            if (InvItem.active && InvItem.netID > 0) { armor.Add(InvItem.name); amount++; }
                        }
                        if (amount <= 0) { args.Player.SendInfoMessage("Player currently isn't wearing any armor."); return; }
                        args.Player.SendInfoMessage(string.Join(", ", armor));
                        return; ;
                    case "dye":
                    case "dyes":
                        List<string> dye = new List<string>();
                        foreach (Item DyeItem in plr.TPlayer.dye)
                        {
                            if (DyeItem.active && DyeItem.netID > 0) { dye.Add(DyeItem.name); amount++; }
                        }
                        if (amount <= 0) { args.Player.SendInfoMessage("Player currently isn't wearing any dye."); return; }
                        args.Player.SendInfoMessage(string.Join(", ", dye));
                        return;
                    default:
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}read <inventory/buff/armor/dye> <player>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                        return;
                }
            }
        }
        #endregion

        #region copy items/buffs/dye slots/armor
        void copyitems(CommandArgs args)
        {
            if (args.Player != null)
            {
                if (args.Parameters.Count != 2)
                {
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}copy <inventory/buff/armor/dye> <player>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    return;
                }
                var foundplr = TShock.Utils.FindPlayer(args.Parameters[1]);
                if (foundplr.Count == 0)
                {
                    args.Player.SendErrorMessage("Invalid player!");
                    return;
                }
                else if (foundplr.Count > 1)
                {
                    TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
                    return;
                }
                var plr = foundplr[0];
                string cmd = args.Parameters[0].ToString().ToLower();
                switch (cmd)
                {
                    case "inv":
                    case "item":
                    case "items":
                    case "inventory":
                        foreach (Item Item in plr.TPlayer.inventory)
                        {
                            if (Item.active && Item.netID > 0) { args.Player.GiveItemCheck(Item.type, Item.name, Item.width, Item.height, Item.stack, Item.prefix); }
                        }
                        return;
                    case "buff":
                    case "buffs":
                        foreach (int Buff in plr.TPlayer.buffType)
                        {
                            if (Buff > 0) { args.Player.SetBuff(Buff, 32400, false); }
                        }
                        return;
                    case "armor":
                        foreach (Item Item in plr.TPlayer.armor)
                        {
                            if (Item.active && Item.netID > 0) { args.Player.GiveItemCheck(Item.type, Item.name, Item.width, Item.height, 1, Item.prefix); }
                        }
                        return; ;
                    case "dye":
                    case "dyes":
                        foreach (Item Item in plr.TPlayer.dye)
                        {
                            if (Item.active && Item.netID > 0) { args.Player.GiveItemCheck(Item.type, Item.name, Item.width, Item.height, 1, Item.prefix); }
                        }
                        return;
                    default:
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}copy <inventory/buff/armor/dye> <player>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                        return;
                }
            }
        }
        #endregion    

        #region worldgen
        private void world_gen(CommandArgs args)
        {
            int Currchests = 0;
            int Currchests2 = 0;
            if (usinginfchests)
            {
                for (int i = 0; i < 1000; i++)
                    if (Main.chest[i] != null)
                        Currchests++;
            }
            if (args.Parameters.Count != 1)
            {
                args.Player.SendInfoMessage("{0}gen <shroompatch/islandhouse/island/dungeon/minehouse/hive/", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                args.Player.SendInfoMessage("cloudisland/temple/hellfort/hellhouse/mountain/pyramid/crimson>");
                args.Player.SendInfoMessage("[WARNING] islands will spawn 50 tiles above you! [WARNING]");
            }
            else
            {
                switch (args.Parameters[0])
                {
                    case "crimson":
                        WorldGen.CrimStart(args.Player.TileX, args.Player.TileY);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "shroompatch":
                        WorldGen.ShroomPatch(args.Player.TileX, args.Player.TileY);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "islandhouse":
                        WorldGen.IslandHouse(args.Player.TileX, args.Player.TileY);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "island":
                        WorldGen.FloatingIsland(args.Player.TileX, args.Player.TileY - 50);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "dungeon":
                        WorldGen.MakeDungeon(args.Player.TileX, args.Player.TileY);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "minehouse":
                        WorldGen.MineHouse(args.Player.TileX, args.Player.TileY);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "hive":
                        WorldGen.Hive(args.Player.TileX, args.Player.TileY);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "temple":
                        WorldGen.makeTemple(args.Player.TileX, args.Player.TileY);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "cloudisland":
                        WorldGen.CloudIsland(args.Player.TileX, args.Player.TileY - 50);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "hellfort":
                        WorldGen.HellFort(args.Player.TileX, args.Player.TileY);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "hellhouse":
                        WorldGen.HellHouse(args.Player.TileX, args.Player.TileY);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "mountain":
                        WorldGen.Mountinater(args.Player.TileX, args.Player.TileY);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    case "pyramid":
                        WorldGen.Pyramid(args.Player.TileX, args.Player.TileY);
                        notify(args.Player, args.Parameters[0]);
                        informplayers();
                        break;
                    default:
                        args.Player.SendInfoMessage("{0}gen <shroompatch/islandhouse/island/dungeon/minehouse/hive/", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                        args.Player.SendInfoMessage("cloudisland/temple/hellfort/hellhouse/mountain/pyramid/crimson>");
                        args.Player.SendInfoMessage("[WARNING] islands will spawn 50 tiles above you! [WARNING]");
                        break;

                }
            }
            if (usinginfchests)
            {
                StringBuilder items = new StringBuilder();
                for (int i = 0; i < 1000; i++)
                    if (Main.chest[i] != null)
                        Currchests2++;

                int difference = Currchests2 - Currchests;
                for (int i = Currchests; i < Currchests + difference; i++)
                {
                    if (Main.chest[i] != null)
                    {
                        for (int it = 0; it < 40; it++)
                        {
                            items.Append(Main.chest[i].item[it].netID + "," + Main.chest[i].item[it].stack + "," + Main.chest[i].item[it].prefix);
                            if (it != 39)
                            {
                                items.Append(",");
                            }
                        }
                    }
                    Database.Query("INSERT INTO Chests (X, Y, Name, Account, Items, Flags, WorldID) VALUES (@0, @1, @2, '', @3, @4, @5)", Main.chest[i].x, Main.chest[i].y, "AIO_Chestroom", items.ToString(), "0", Main.worldID);
                    Main.chest[i] = null;
                }
            }
        }
        void notify(TSPlayer ts, string spawned)
        {
            ts.SendSuccessMessage("You succesfully generated a " + spawned);
        }

        public static void informplayers(bool hard = false)
        {
            foreach (TSPlayer ts in TShock.Players)
            {
                if ((ts != null) && (ts.Active))
                {
                    for (int i = 0; i < 255; i++)
                    {
                        for (int j = 0; j < Main.maxSectionsX; j++)
                        {
                            for (int k = 0; k < Main.maxSectionsY; k++)
                            {
                                Netplay.serverSock[i].tileSection[j, k] = false;
                            }
                        }
                    }
                }
            }
        }
        //ends here
        #endregion

        #region fillchest
        private void fillchest(CommandArgs args)
        {
            if (!usinginfchests) { args.Player.SendInfoMessage("Sorry but you can't use {0}fillchest with infinitechests plugin!", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier)); return; }
            lastchest = false;
            StringBuilder items = new StringBuilder();
            bool found = false;
            if (args.Parameters.Count != 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! use {0}fillchest <number>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                return;
            }
            else
            {
                for (int z = 0; z < 1000; z++)
                {
                    if (Main.chest[z] != null)
                    {
                        if (Main.chest[z].x == args.Player.TileX && Main.chest[z].y == args.Player.TileY + 1)
                        {
                            int chestitem = Convert.ToInt32(args.Parameters[0]);
                            if (chestitem > 2748 || chestitem < -48) { args.Player.SendErrorMessage("Id must be between -48 and 2748"); return; }
                            for (int it = 0; it < 40; it++)
                            {
                                if (chestitem == 0) { chestitem++; }

                                Item itm = TShock.Utils.GetItemById(chestitem);
                                itm.stack = TShock.Utils.GetItemById(chestitem).maxStack;   
                                Main.chest[z].item[it] = itm;
                                chestitem++;
                                if (chestitem > 2748) { break; }
                            }
                            found = true;
                            args.Player.SendSuccessMessage("The chest under you has been filled with items {0} through {1}!", chestitem - 40, chestitem);
                            return;
                        }
                    }
                }
            }
            if (!found)
            {
                args.Player.SendErrorMessage("No chest found under you!");
            }
            return;
        }
        #endregion

        #region killchest
        private void killchest(CommandArgs args)
        {
            if (usinginfchests) { args.Player.SendErrorMessage("Sorry but you can't use {0}killchest with infinitechests plugin!", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier)); return; }
            bool found = false;
            for (int x = 0; x < 1000; x++)
            {
                if (Main.chest[x] != null)
                {
                    if (Main.chest[x].x == args.Player.TileX && Main.chest[x].y == args.Player.TileY + 1)
                    {
                        found = true;
                        Item emptyitem = TShock.Utils.GetItemById(0);
                        for (int i = 0; i < 40; i++)
                            Main.chest[x].item[i] = emptyitem;

                        WorldGen.KillTile(Main.chest[x].x, Main.chest[x].y, false, false, true);
                        Main.chest[x] = null;
                        args.Player.SendSuccessMessage("The chest under you has been killed!");
                        informplayers();
                        return;
                    }
                }
            }
            if (!found)
            {
                args.Player.SendErrorMessage("No chest found under you!");
            }
            return;
        }
        #endregion

        #region spywhisper
        private void SPY(CommandArgs args)
        {
            if (spies.Contains(args.Player.IP))
            {
                spies.Remove(args.Player.IP);
                args.Player.SendSuccessMessage("You have stopped spying on whispers");
                return;
            }
            spies.Add(args.Player.IP);
            args.Player.SendSuccessMessage("You are now spying on whispers");
        }
        #endregion

        #region chestroom
        private void chestroom(CommandArgs args)
        {
                int chestitem = -48;
                lastchest = false;
                int x = args.Player.TileX + 1;
                int y = args.Player.TileY;

                #region choose direction
                if (args.Parameters.Count < 1)
                {
                    args.Player.SendErrorMessage("Use {0}chestroom <tl/tr/bl/br/tc/bc>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    args.Player.SendInfoMessage("t = top, l = left, r = right, b = bottom, c = center");
                    args.Player.SendErrorMessage("This is where you'll stand when the chestroom spawns");
                    return;
                }
                switch (args.Parameters[0])
                {
                    case "tl":
                        break;
                    case "tr":
                        x -= 32;
                        break;
                    case "bl":
                        y -= 35;
                        break;
                    case "br":
                        y -= 35;
                        x -= 32;
                        break;
                    case "tc":
                        x -= 16;
                        break;
                    case "bc":
                        x -= 16;
                        y -= 35;
                        break;
                    default:
                        args.Player.SendErrorMessage("Use {0}chestroom <tl/tr/bl/br/tc/bc>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                        args.Player.SendErrorMessage("t = top, l = left, r = right, b = bottom, c = center");
                        args.Player.SendErrorMessage("This is where you'll stand when the chestroom spawns");
                        return;
                }
                if (!args.Silent)
                    TSPlayer.All.SendErrorMessage("Placing chestroom...");
                #endregion choose direction

                #region choose tiles/background/chests/torches/platforms
                int tileid = tiles[rnd.Next(0, tiles.Length)];
                int chestid = chests[rnd.Next(0, chests.Length)];
                int bgwall = walls[rnd.Next(0, walls.Length)];
                short framey = platformframey[rnd.Next(0, platformframey.Length)];
                short torchframe = torchframey[rnd.Next(0, torchframey.Length)];
                #endregion choose tiles/background/chests

                #region count current chests
                int count = 0;
                for (int ch = 0; ch < 1000; ch++)
                {
                    if (Main.chest[ch] != null)
                    {
                        count++;
                    }
                }
                if (count > 930) { args.Player.SendErrorMessage("Making this chestroom would make you pass the chest limit, chestroom cancelled."); return; }
                bckup = count;
                #endregion count current chests

                #region delete tiles
                Backups.Clear();
                for (int z = -5; z < 36; z++)
                {
                    for (int i = -3; i < 35; i++)
                    {
                        if (usinginfchests)
                        {
                            Database.Query("DELETE FROM Chests WHERE X = @0 AND Y = @1 AND WorldID = @2", x + i, z + 3, Main.worldID);
                        }
                        Backups.Add(new Backup(x + i, y + z + 3));
                        Main.tile[x + i, y + z + 3] = new Tile();
                    }
                }
                #endregion delete tiles

                #region place frame + walls
                //background wall
                for (int z = -4; z < 35; z++)
                {
                    for (int i = -2; i < 34; i++)
                    {
                        Main.tile[x + i, y + z + 3].wall = (byte)bgwall;
                    }
                }

                for (int frame = -2; frame < 39; frame++)
                {
                    //left border
                    Main.tile[x - 3, y + frame].active(true);
                    Main.tile[x - 3, y + frame].type = (byte)tileid;
                    //right border
                    Main.tile[x + 34, y + frame].active(true);
                    Main.tile[x + 34, y + frame].type = (byte)tileid;
                }
                #endregion place frame + walls

                #region place tiles for under the chests
                for (int z = -2; z < 43; z += 5)
                {
                    for (int i = -2; i < 34; i++)
                    {
                        Main.tile[x + i, y + z].active(true);
                        Main.tile[x + i, y + z].type = (byte)tileid;
                    }
                }
                for (int z = +3; z < 38; z += 5)
                {
                    for (int i = +1; i < 32; i += 4)
                    {
                        Main.tile[x + i, y + z].active(true);
                        Main.tile[x + i, y + z].type = (byte)19;
                        Main.tile[x + i, y + z].frameY = framey;

                        Main.tile[x + i + 1, y + z].active(true);
                        Main.tile[x + i + 1, y + z].type = (byte)19;
                        Main.tile[x + i + 1, y + z].frameY = framey;
                    }
                }
                #endregion place tiles for under the chests

                #region place chests on the tiles
                int chestsplaced = 0;
                for (int z = 0; z < 40; z += 5)
                {
                    for (int i = 0; i < 36; i += 4)
                    {
                        if (chestsplaced >= 70) { break; }
                        WorldGen.AddBuriedChest(x + i, y + z, 1, false, chestid);
                        chestsplaced++;
                    }
                }

                #endregion place chests on the tiles

                informplayers();

                #region paint chests
                for (int z = 0; z < 40; z += 5)
                {
                    for (int i = 0; i < 36; i += 4)
                    {
                        int paint = rnd.Next(0, 27);
                        Main.tile[x + i - 1, y + z + 1].color((byte)paint);
                        Main.tile[x + i - 1, y + z + 2].color((byte)paint);
                        Main.tile[x + i, y + z + 1].color((byte)paint);
                        Main.tile[x + i, y + z + 2].color((byte)paint);
                    }
                }
                #endregion paint chests

                #region add torches

                for (int frame = -1; frame < 35; frame += 5)
                {
                    //left border
                    Main.tile[x - 2, y + frame].active(true);
                    Main.tile[x - 2, y + frame].type = 4;
                    Main.tile[x - 2, y + frame].frameY = torchframe;
                    //right border
                    Main.tile[x + 33, y + frame].active(true);
                    Main.tile[x + 33, y + frame].type = 4;
                    Main.tile[x + 33, y + frame].frameY = torchframe;
                }

                for (int frame = 2; frame < 40; frame += 5)
                {
                    //middle left
                    Main.tile[x + 10, y + frame].active(true);
                    Main.tile[x + 10, y + frame].type = 4;
                    Main.tile[x + 10, y + frame].frameY = torchframe;
                    //middle right
                    Main.tile[x + 21, y + frame].active(true);
                    Main.tile[x + 21, y + frame].type = 4;
                    Main.tile[x + 21, y + frame].frameY = torchframe;
                }
                #endregion add torches

                #region fill the chests with LOOT
                StringBuilder items = new StringBuilder();
                for (int id = count; id < count + 70; id++)
                {
                    if (Main.chest[id] != null)
                    {
                        for (int it = 0; it < 40; it++)
                        {
                            if (chestitem == 0 && !lastchest) { chestitem++; }
                            if (chestitem > 2748) { chestitem = 0; lastchest = true; }
                            Item item = TShock.Utils.GetItemById(chestitem);
                            item.stack = TShock.Utils.GetItemById(chestitem).maxStack;
                            Main.chest[id].item[it] = item;
                            if (usinginfchests)
                            {
                                items.Append(Main.chest[id].item[it].netID + "," + Main.chest[id].item[it].stack + "," + Main.chest[id].item[it].prefix);
                                if (it != 39)
                                {
                                    items.Append(",");
                                }
                            }
                            if (!lastchest) { chestitem++; }
                        }
                        if (usinginfchests)
                        {
                            Database.Query("INSERT INTO Chests (X, Y, Name, Account, Items, Flags, WorldID) VALUES (@0, @1, @2, '', @3, @4, @5)", Main.chest[id].x, Main.chest[id].y, "AIO_Chestroom", items.ToString(), "12", Main.worldID);
                            Main.chest[id] = null;
                            items.Clear();
                        }
                    }
                }
                #endregion fill the chests with LOOT
                canrollback = true;
                args.Player.SendSuccessMessage("Chestroom succesfully created!");
                informplayers();

        }
        #endregion chestroom

        #region undo chestroom
        private void undochestroom(CommandArgs args)
        {
            if (!canrollback)
            {
                args.Player.SendErrorMessage("No previous chestroom to rollback.");
                return;
            }
            canrollback = false;
            if (!args.Silent)
                TSPlayer.All.SendErrorMessage("Rolling back chestroom...");

            Item itm = TShock.Utils.GetItemById(0);
            if (usinginfchests)
            {
                Database.Query("DELETE FROM Chests WHERE Name = @0 AND WorldID = @1", "AIO_Chestroom", Main.worldID);
            }
            else
            {
                for (int id = bckup; id < bckup + 70; id++)
                {
                    if (Main.chest[id] != null)
                    {
                        for (int it = 0; it < 40; it++)
                        {
                            Main.chest[id].item[it] = itm;
                        }
                        Main.chest[id] = null;
                    }
                }
            }

            foreach (Backup bt in Backups)
            {
                Main.tile[bt.X, bt.Y] = bt.Tile;
            }
            informplayers();
            Backups.Clear();
            args.Player.SendSuccessMessage("Chestroom succesfully wiped!");
        }      
        #endregion undo chestroom

        #endregion commands
    }
}
