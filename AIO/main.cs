/* 
* Credit goes to Ancientgods for original AIO plugin.
*/

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace AIO
{
	[ApiVersion(2, 0)]
	public class AIO : TerrariaPlugin
	{
		#region items
		List<Backup> Backups = new List<Backup>();

		private List<Report> HouseLoc = new List<Report>();
		private List<Report> GriefLoc = new List<Report>();

		bool usinginfchests = false;


		Random rnd = new Random();

		List<string> spies = new List<string>();
		List<string> frozenplayer = new List<string>();
		public List<string> staffchatplayers = new List<string>();

		Color staffchatcolor = new Color(200, 50, 150);
		DateTime LastCheck = DateTime.UtcNow;
		#endregion

		#region plugin info
		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}
		public override string Author
		{
			get { return "Zaicon"; }
		}
		public override string Name
		{
			get { return "AIO"; }
		}

		public override string Description
		{
			get { return "All-In-One Plugin"; }
		}
		#endregion


		#region initialize
		public override void Initialize()
		{
			ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
			PlayerHooks.PlayerPostLogin += OnLogin;
			PlayerHooks.PlayerLogout += OnLogout;


			#region staffchatcommands
			Commands.ChatCommands.Add(new Command(Staffchat, "s"));
			Commands.ChatCommands.Add(new Command("aio.staffchat.admin", SKickCommand, "skick"));
			Commands.ChatCommands.Add(new Command("aio.staffchat.admin", SInviteCommand, "sinvite"));
			Commands.ChatCommands.Add(new Command("aio.staffchat.admin", SClearCommand, "sclear"));
			Commands.ChatCommands.Add(new Command("tshock.world.modify", SListCommand, "slist"));
			#endregion
			#region report grief/building
			Commands.ChatCommands.Add(new Command("tshock.world.modify", SReportGriefCommand, "reportgrief", "rg") { AllowServer = false });
			Commands.ChatCommands.Add(new Command("aio.checkgrief", SCheckGriefCommand, "checkgrief", "cg") { AllowServer = false });
			Commands.ChatCommands.Add(new Command("aio.listgrief", SListGriefCommand, "listgrief", "lg"));
			Commands.ChatCommands.Add(new Command("aio.checkbuilding", SCheckBuildingCommand, "checkbuilding", "cb") { AllowServer = false });
			Commands.ChatCommands.Add(new Command("aio.listbuilding", SListBuildingCommand, "listbuilding", "lb"));
			Commands.ChatCommands.Add(new Command("tshock.world.modify", SReportBuildingCommand, "building") { AllowServer = false });
			#endregion
			#region other commands
			Commands.ChatCommands.Add(new Command(SListStaffCommand, "staff"));
			Commands.ChatCommands.Add(new Command("aio.freeze", SFreezeCommand, "freeze"));
			Commands.ChatCommands.Add(new Command("aio.read", SReadCommand, "read"));
			Commands.ChatCommands.Add(new Command("aio.copy", SCopyCommand, "copy") { AllowServer = false });
			Commands.ChatCommands.Add(new Command("aio.worldgen", SGenCommand, "gen") { AllowServer = false });
			#endregion
		}
		#endregion

		#region dispose
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
				PlayerHooks.PlayerPostLogin -= OnLogin;
				PlayerHooks.PlayerLogout -= OnLogout;
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

		#region login/logout
		public void OnLogin(PlayerPostLoginEventArgs args)
		{
			if (args.Player != null && args.Player.User != null)
			{
				if (args.Player.Group.HasPermission("aio.checkgrief"))
				{
					File.AppendAllText(Utils.GetPath(), $"{DateTime.Now.ToString("g")} :: {args.Player.User.Name} has logged in.\r\n");
				}
			}
		}

		public void OnLogout(PlayerLogoutEventArgs args)
		{
			if (args.Player != null && args.Player.User != null)
			{
				if (args.Player.Group.HasPermission("aio.checkgrief"))
				{
					File.AppendAllText(Utils.GetPath(), $"{DateTime.Now.ToString("g")} :: {args.Player.User.Name} has logged out.\r\n");
				}
			}
		}
		#endregion

		#region onupdate
		public void OnUpdate(EventArgs args)
		{
			WorldGen.spawnMeteor = false;
			if ((DateTime.UtcNow - LastCheck).TotalSeconds >= 3)
			{
				LastCheck = DateTime.UtcNow;
				foreach (TSPlayer ts in TShock.Players.Where(e => e != null && frozenplayer.Contains(e.IP)))
				{
					ts.Disable("Frozen", DisableFlags.None);
				}
			}
		}
		#endregion

		#region commands
		#region staffchat
		private void Staffchat(CommandArgs args)
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
								ts.SendMessage($"[Staffchat] {args.Player.User.Name}: {message}", staffchatcolor);
							}
						}
					}
				}
				else
					args.Player.SendMessage($"{args.Silent.Specifier()}s \"[Message]\" is the right format.", staffchatcolor);
			}
			else
				args.Player.SendErrorMessage("You have not been invited to the staffchat!");
		}

		private void SInviteCommand(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage($"Invalid syntax! Syntax: {args.Silent.Specifier()}sinvite <player>");
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

			if (!staffchatplayers.Contains(plr.IP) && !plr.Group.HasPermission("aio.staffchat.chat"))
			{
				staffchatplayers.Add(plr.IP);
				plr.SendInfoMessage($"You have been invited to the staffchat! Type {TShock.Config.CommandSpecifier}s <message> to chat.");
				foreach (TSPlayer ts in TShock.Players.Where(e => e != null && e.HasPermission("aio.staffchat.chat")))
				{
					if (!args.Silent)
						ts.SendInfoMessage($"{plr.Name} has been invited to the staffchat.");
				}
			}
			else
				args.Player.SendErrorMessage("Player is already in the staffchat.");

		}
		private void SKickCommand(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage($"Invalid syntax! Syntax: {args.Silent.Specifier()}skick <player name>");
				return;
			}
			List<TSPlayer> foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
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

			if (staffchatplayers.Contains(plr.IP) && !plr.HasPermission("aio.staffchat.chat"))
			{
				staffchatplayers.Remove(plr.IP);
				plr.SendInfoMessage("You have been removed from the staffchat.");
				foreach (TSPlayer ts in TShock.Players.Where(e => e != null && e.HasPermission("aio.staffchat.chat")))
				{
					if (!args.Silent)
						ts.SendInfoMessage($"{plr.Name} has been removed from the staffchat.");
				}
			}
			else if (plr.HasPermission("aio.staffchat.chat"))
				args.Player.SendErrorMessage("You can't kick a staff member from staffchat!");
			else
				args.Player.SendErrorMessage("This player hasn't been invited to staffchat!");

		}
		private void SClearCommand(CommandArgs args)
		{
			if (!args.Silent)
			{
				foreach (TSPlayer ts in TShock.Players.Where(e => e != null && staffchatplayers.Contains(e.IP)))
					ts.SendInfoMessage("You have been removed from the staffchat.");
			}
			staffchatplayers.Clear();
			if (!args.Silent)
			{
				foreach (TSPlayer ts in TShock.Players.Where(e => e != null && e.HasPermission("aio.staffchat.chat")))
					ts.SendInfoMessage("Staffchat invites have been cleared!");
			}
		}
		private void SListCommand(CommandArgs args)
		{
			List<string> staffchatlist = new List<string>();
			foreach (TSPlayer ts in TShock.Players.Where(e => e != null && staffchatplayers.Contains(e.IP)))
				staffchatlist.Add(ts.Name);
			args.Player.SendInfoMessage($"Players in staffchat: {string.Join(", ", staffchatlist)}");
		}
		#endregion

		#region list of online staff
		public void SListStaffCommand(CommandArgs args)
		{
			List<TSPlayer> Staff = TShock.Players.Where(e => e != null && e.HasPermission("aio.staffchat.chat")).ToList();
			if (Staff.Count == 0)
			{
				args.Player.SendErrorMessage("No staff members currently online.");
				return;
			}
			args.Player.SendErrorMessage("[Currently online staff members]");
			foreach (TSPlayer who in Staff.Where(e => e != null))
			{
				Color groupcolor = new Color(who.Group.R, who.Group.G, who.Group.B);
				args.Player.SendMessage($"{who.Group.Prefix}{who.Name}", groupcolor);
			}
		}
		#endregion

		#region reportgrief
		public void SReportGriefCommand(CommandArgs args)
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
			File.AppendAllText(Utils.GetPath(), $"{DateTime.Now.ToString("g")} :: {args.Player.Name} reported a grief at POS ({args.Player.TileX}, {args.Player.TileY}).\r\n");

			foreach (TSPlayer ts in TShock.Players.Where(e => e != null && e.HasPermission("aio.checkgrief")))
				ts.SendInfoMessage($"{args.Player.Name} has sent in a grief report at: {args.Player.TileX}, {args.Player.TileY}");
		}
		public void SListGriefCommand(CommandArgs args)
		{
			if (GriefLoc.Count == 0)
			{
				args.Player.SendErrorMessage("There are no reported griefs.");
				return;
			}
			args.Player.SendInfoMessage($"There are {GriefLoc.Count} reported griefs.");
			if (args.Parameters.Count < 1 || args.Parameters[0].ToLower() != "all")
				return;
			for (int i = 0; i < GriefLoc.Count; i++)
			{
				Report Re = GriefLoc[i];
				args.Player.SendInfoMessage(string.Format("[{0}] {1} reported a grief at POS ({2},{3}) at {4}.", (i + 1).ToString(), Re.Name, Re.X, Re.Y, Re.Date));
			}

		}
		public void SCheckGriefCommand(CommandArgs args)
		{
			if (GriefLoc.Count == 0)
			{
				args.Player.SendErrorMessage("There are no reported griefs.");
				return;
			}
			for (int i = 0; i < GriefLoc.Count; i++)
			{
				Report Re = GriefLoc[i];
				if (Re != null)
				{
					File.AppendAllText(Utils.GetPath(), $"{DateTime.Now.ToString("g")} :: {args.Player.Name} checked the grief at POS ({Re.X}, {Re.Y}).\r\n");
					args.Player.Teleport(Re.X * 16, Re.Y * 16);
					args.Player.SendInfoMessage($"Reported by: {Re.Name} at {Re.Date}");
					GriefLoc.Remove(Re);
					i = GriefLoc.Count;
				}
			}
		}
		#endregion

		#region reportbuilding
		public void SCheckBuildingCommand(CommandArgs args)
		{
			if (HouseLoc.Count == 0)
			{
				args.Player.SendErrorMessage("There are no reported buildings.");
				return;
			}
			for (int i = 0; i < HouseLoc.Count; i++)
			{
				Report Re = HouseLoc[i];
				if (Re != null)
				{
					File.AppendAllText(Utils.GetPath(), $"{DateTime.Now.ToString("g")} :: {args.Player.Name} checked building at POS ({Re.X}, {Re.Y}).\r\n");
					args.Player.Teleport(Re.X * 16, Re.Y * 16);
					args.Player.SendInfoMessage($"Reported by: {Re.Name} at {Re.Date}");
					HouseLoc.Remove(Re);
					i = HouseLoc.Count;
				}
			}
		}
		public void SListBuildingCommand(CommandArgs args)
		{
			if (HouseLoc.Count == 0)
			{
				args.Player.SendErrorMessage("There are no reported buildings.");
				return;
			}
			args.Player.SendInfoMessage($"There are {HouseLoc.Count} reported buildings.");
			if (args.Parameters.Count < 1 || args.Parameters[0].ToLower() != "all")
				return;
			for (int i = 0; i < HouseLoc.Count; i++)
			{
				Report Re = HouseLoc[i];
				args.Player.SendInfoMessage("[{0}] {1} reported a building at POS ({2},{3}) at {4}", (i + 1).ToString(), Re.Name, Re.X, Re.Y, Re.Date);
			}

		}
		public void SReportBuildingCommand(CommandArgs args)
		{
			int x = args.Player.TileX;
			int y = args.Player.TileY;
			if (HouseLoc.Exists(e => e.X > (x - 50) && e.Y > (y - 50) && e.X < (x + 50) && e.Y < (y + 50)))
			{
				args.Player.SendErrorMessage("This location has already been reported! Please wait until someone is available to protect it for you.");
				return;
			}
			if (args.Player.CurrentRegion == null || !args.Player.CurrentRegion.DisableBuild)
			{
				HouseLoc.Add(new Report(args.Player.TileX, args.Player.TileY, args.Player.User.Name, DateTime.UtcNow));
				args.Player.SendSuccessMessage($"Your house has been reported at {x}, {y}.");
				File.AppendAllText(Utils.GetPath(), $"{DateTime.Now.ToString("g")} :: {args.Player.Name} reported a building at POS ({x}, {y}).\r\n");
				foreach (TSPlayer ts in TShock.Players.Where(e => e != null && e.HasPermission("aio.checkbuilding")))
					ts.SendInfoMessage($"{args.Player.Name} has reported a house at: {x}, {y}");
			}
			else
			{
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
		public void SFreezeCommand(CommandArgs args)
		{
			if (args.Player != null)
			{
				if (args.Parameters.Count != 1)
				{
					args.Player.SendErrorMessage($"Invalid syntax! Proper syntax: {args.Silent.Specifier()}freeze [player]");
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
						TSPlayer.All.SendInfoMessage($"{args.Player.Name} froze {plr.Name}!");
					return;
				}
				else
				{
					frozenplayer.Remove(plr.IP);
					if (!args.Silent)
						TSPlayer.All.SendInfoMessage($"{args.Player.Name} unfroze {plr.Name}!");
					return;
				}
			}
		}
		#endregion freeze

		#region read items/buffs/dye slots/armor
		void SReadCommand(CommandArgs args)
		{
			int amount = 0;
			if (args.Player != null)
			{
				if (args.Parameters.Count != 2)
				{
					args.Player.SendErrorMessage($"Invalid syntax! Proper syntax: {args.Silent.Specifier()}read <inventory/buff/armor/dye> <player>");
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

						foreach (Item Item in plr.TPlayer.inventory.Where(e => e.active && e.netID > 0))
							items.Add(Item.name); amount++;

						if (amount <= 0)
						{
							args.Player.SendInfoMessage("Player currently has no items in inventory.");
							return;
						}

						args.Player.SendInfoMessage("Inventory: " + string.Join(", ", items));
						return;
					case "buff":
					case "buffs":
						List<string> buffs = new List<string>();

						foreach (int BuffId in plr.TPlayer.buffType.Where(e => e > 0))
							buffs.Add(TShock.Utils.GetBuffName(BuffId));

						if (plr.TPlayer.CountBuffs() <= 0)
						{
							args.Player.SendInfoMessage("Player currently has no buffs.");
							return;
						}

						args.Player.SendInfoMessage("Buffs: " + string.Join(", ", buffs));
						return;
					case "armor":
						List<string> armor = new List<string>();

						foreach (Item InvItem in plr.TPlayer.armor.Where(e => e.active && e.netID > 0))
							armor.Add(InvItem.name);

						if (armor.Count == 0)
						{
							args.Player.SendInfoMessage("Player currently isn't wearing any armor.");
							return;
						}

						args.Player.SendInfoMessage("Armor: " + string.Join(", ", armor));
						return; ;
					case "dye":
					case "dyes":
						List<string> dye = new List<string>();

						foreach (Item DyeItem in plr.TPlayer.dye.Where(e => e.active && e.netID > 0))
							dye.Add(DyeItem.name);

						if (dye.Count == 0)
						{
							args.Player.SendInfoMessage("Player currently isn't wearing any dye.");
							return;
						}
						args.Player.SendInfoMessage("Dyes: " + string.Join(", ", dye));
						return;
					default:
						args.Player.SendErrorMessage($"Invalid syntax! Proper syntax: {args.Silent.Specifier()}read <inventory/buff/armor/dye> <player>");
						return;
				}
			}
		}
		#endregion

		#region copy items/buffs/dye slots/armor
		void SCopyCommand(CommandArgs args)
		{
			if (args.Player != null)
			{
				if (args.Parameters.Count != 2)
				{
					args.Player.SendErrorMessage($"Invalid syntax! Proper syntax: {args.Silent.Specifier()}copy <inventory/buff/armor/dye> <player>");
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
						foreach (Item Item in plr.TPlayer.inventory.Where(e => e.active && e.netID > 0))
							args.Player.GiveItemCheck(Item.type, Item.name, Item.width, Item.height, Item.stack, Item.prefix);
						return;
					case "buff":
					case "buffs":
						foreach (int Buff in plr.TPlayer.buffType.Where(e => e > 0))
							args.Player.SetBuff(Buff, 32400, false);
						return;
					case "armor":
						foreach (Item Item in plr.TPlayer.armor.Where(e => e.active && e.netID > 0))
							args.Player.GiveItemCheck(Item.type, Item.name, Item.width, Item.height, 1, Item.prefix);
						return;
					case "dye":
					case "dyes":
						foreach (Item Item in plr.TPlayer.dye.Where(e => e.active && e.netID > 0))
							args.Player.GiveItemCheck(Item.type, Item.name, Item.width, Item.height, 1, Item.prefix);
						return;
					default:
						args.Player.SendErrorMessage($"Invalid syntax! Proper syntax: {args.Silent.Specifier()}copy <inventory/buff/armor/dye> <player>");
						return;
				}
			}
		}
		#endregion

		#region worldgen
		private void SGenCommand(CommandArgs args)
		{
			int Currchests = 0;
			if (usinginfchests)
			{
				for (int i = 0; i < 1000; i++)
					if (Main.chest[i] != null)
						Currchests++;
			}
			if (args.Parameters.Count != 1)
			{
				args.Player.SendInfoMessage($"{args.Silent.Specifier()}gen <shroompatch/islandhouse/island/dungeon/minehouse/hive/");
				args.Player.SendInfoMessage("cloudisland/temple/hellfort/hellhouse/mountain/pyramid/crimson/");
				args.Player.SendInfoMessage("trees/cloudlake/livingtree/softice/mayantrap");
				args.Player.SendInfoMessage("[WARNING] islands will spawn 50 tiles above you! [WARNING]");
			}
			else
			{
				switch (args.Parameters[0])
				{
					case "trees":
						WorldGen.AddTrees();
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "cloudlake":
						WorldGen.CloudLake(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "livingtree":
						bool gen = WorldGen.GrowLivingTree(args.Player.TileX, args.Player.TileY);
						if (!gen)
							args.Player.SendErrorMessage("Could not generate a living tree.");
						else
						{
							Notify(args.Player, args.Parameters[0]);
							Informplayers();
						}
						break;
					case "softice":
						WorldGen.MakeWateryIceThing(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "mayantrap":
						if (!WorldGen.mayanTrap(args.Player.TileX, args.Player.TileY))
							args.Player.SendErrorMessage("Could not generate a mayantrap.");
						else
						{
							Notify(args.Player, args.Parameters[0]);
							Informplayers();
						}
						break;
					case "crimson":
						WorldGen.CrimStart(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "shroompatch":
						WorldGen.ShroomPatch(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "islandhouse":
						WorldGen.IslandHouse(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "island":
						WorldGen.FloatingIsland(args.Player.TileX, args.Player.TileY - 50);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "dungeon":
						WorldGen.MakeDungeon(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "minehouse":
						WorldGen.MineHouse(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "hive":
						WorldGen.Hive(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "temple":
						WorldGen.makeTemple(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "cloudisland":
						WorldGen.CloudIsland(args.Player.TileX, args.Player.TileY - 50);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "hellfort":
						WorldGen.HellFort(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "hellhouse":
						WorldGen.HellHouse(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "mountain":
						WorldGen.Mountinater(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					case "pyramid":
						WorldGen.Pyramid(args.Player.TileX, args.Player.TileY);
						Notify(args.Player, args.Parameters[0]);
						Informplayers();
						break;
					default:
						args.Player.SendInfoMessage($"{args.Silent.Specifier()}gen <shroompatch/islandhouse/island/dungeon/minehouse/hive/");
						args.Player.SendInfoMessage("cloudisland/temple/hellfort/hellhouse/mountain/pyramid/crimson>");
						args.Player.SendInfoMessage("[WARNING] islands will spawn 50 tiles above you! [WARNING]");
						break;

				}
			}
			void Notify(TSPlayer ts, string spawned)
			{
				ts.SendSuccessMessage("You succesfully generated a " + spawned);
			}
			void Informplayers(bool hard = false)
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
									Netplay.Clients[i].TileSections[j, k] = false;
								}
							}
						}
					}
				}
			}
		}
		//ends here
		#endregion
		#endregion commands
	}
}
