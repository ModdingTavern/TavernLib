///////////////////////////////////////////////////
///         THIS IS A DECOMPILED SCRIPT         ///
///////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alta.Alchemy;
using Alta.Api.DataTransferModels.Models.Responses;
using Alta.Character;
using Alta.Console;
using Alta.Console.Commands;
using Alta.Global;
using Alta.Impact;
using Alta.Networking.Scripts.Player;
using Alta.Networking.Servers;
using Alta.StatSystem;
using Alta.Utilities;
using ATT.PlayerStates;
using NLog;
using UnityEngine;
using Inventory = Alta.Console.Commands.Inventory;

namespace TavernLib.ModulePorts;

// Assembly-CSharp, Version=0.0.39.2, Culture=neutral, PublicKeyToken=null
// Alta.Console.Commands.PlayerCommandModule

[PlayOnly]
[Module("player", "Commands to interact with a player.")]
public static class PlayerCommandModule
{
	[ServerOnly]
	[Module("inventory", "inventory related commands")]
	public static class InventoryModule
	{
		[ServerOnly]
		[Command("save", "Saves a players inventory")]
		private static async Task<string> Save(UserInfoAccess user)
		{
			if (!(await user.IsValid()))
			{
				CommandService.ThrowError("Invalid player");
			}
			int id = await user.GetIdentifier();
			Player player = Player.GetPlayer(id);
			if (player != null)
			{
				player.WriteSave(false);
				logger.Trace("Saving inventory for online player " + id);
				return GetData(player.Save);
			}
			IAltaFile altaFile = await ServerHandler.Current.SaveUtility.PlayerSaveUtility.Load(id);
			if (altaFile?.Content == null)
			{
				CommandService.ThrowError("No save file found");
			}
			logger.Trace("Saving inventory for offline player " + id);
			string result = GetData((PlayerSave)altaFile.Content);
			if (Player.GetPlayer(id) == null)
			{
				altaFile.QueueUnload();
			}
			return result;
			string GetData(PlayerSave save)
			{
				if (save.PrefabData == null)
				{
					CommandService.ThrowError("That save has no inventory data");
				}
				byte[] array = new byte[save.PrefabData.Length * 4];
				Buffer.BlockCopy(save.PrefabData, 0, array, 0, array.Length);
				return Convert.ToBase64String(array);
			}
		}

		[ServerOnly]
		[Command("load", "Loads a players inventory")]
		private static async Task Load(UserInfoAccess user, string save)
		{
			if (!(await user.IsValid()))
			{
				CommandService.ThrowError("Invalid player");
			}
			int id = await user.GetIdentifier();
			if (Player.GetPlayer(id) != null)
			{
				CommandService.ThrowError("Can't load inventory for online players");
			}
			IAltaFile obj = await ServerHandler.Current.SaveUtility.PlayerSaveUtility.Load(id);
			if (Player.GetPlayer(id) != null)
			{
				CommandService.ThrowError("Can't load inventory for online players");
			}
			if (obj?.Content == null)
			{
				CommandService.ThrowError("Can't load inventory for players without a save");
			}
			if (string.IsNullOrWhiteSpace(save))
			{
				CommandService.ThrowError("No inventory data was provided");
			}
			byte[] bytes = null;
			try
			{
				bytes = Convert.FromBase64String(save);
			}
			catch (FormatException)
			{
				CommandService.ThrowError("Inventory data is not valid base64");
			}
			if (bytes.Length == 0 || bytes.Length % 4 != 0)
			{
				CommandService.ThrowError("Inventory data is not a whole number of 32 bit words");
			}
			PlayerSave playerSave = obj.Content as PlayerSave;
			if (playerSave == null)
			{
				CommandService.ThrowError("That save file does not contain player save data");
			}
			logger.Trace("Loading inventory for " + id);
			uint[] array = new uint[bytes.Length / 4];
			Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
			playerSave.PrefabData = array;
			obj.QueueUnload();
		}

		[ServerOnly]
		[Command(null, "Views an online players inventory")]
		private static IEnumerable<Inventory> Inventory(PlayerList players)
		{
			logger.Trace("Getting inventories.");
			foreach (Player player in players)
			{
				if (player?.PlayerController == null)
				{
					logger.Trace("Skipping a player with no live controller.");
					continue;
				}
				yield return new Inventory(player);
			}
		}
	}

	public enum StatApplicationType
	{
		Base,
		Min,
		Max
	}

	public class StatValue
	{
		public HashedItemInfo Definition { get; set; }

		public float Value { get; set; }

		public float Base { get; set; }

		public static implicit operator StatValue(Stat stat)
		{
			return new StatValue
			{
				Definition = stat.Definition,
				Value = stat.Value,
				Base = stat.Base
			};
		}
	}

	public class StatDefinitionInfo
	{
		public string Name { get; set; }

		public float Min { get; set; }

		public float Max { get; set; }

		public float Value { get; set; }

		public static implicit operator StatDefinitionInfo(StatDefinitionApplication stat)
		{
			return new StatDefinitionInfo
			{
				Name = stat.Name,
				Max = stat.DefaultMaximum,
				Min = stat.DefaultMinimum
			};
		}

		public static implicit operator StatDefinitionInfo(Stat stat)
		{
			return new StatDefinitionInfo
			{
				Name = stat.Definition.Name,
				Max = stat.Maximum,
				Min = stat.Minimum,
				Value = stat.Value
			};
		}
	}

	[Module("progression", "Progression related commands")]
	public static class ProgressionCommandModule
	{
		private static NLog.Logger logger = LogManager.GetCurrentClassLogger();

		private static Dictionary<int, ProgressionSlot> available = new Dictionary<int, ProgressionSlot>();

		private static Player lastPlayer;

		[Command("list", "List the available paths")]
		[ServerOnly]
		private static void List()
		{
			int length = typeof(ProgressionPath).GetEnumValues().Length;
			logger.Debug("Available Paths:");
			for (int i = 0; i < length; i++)
			{
				logger.Debug("  - {0}", (ProgressionPath)i);
			}
		}

		[Command("pathxp", "Gain Experience on a Profession Path")]
		[ServerOnly]
		private static void GainPathXp(PlayerList players, ProgressionPath path, int xp)
		{
			foreach (Player player in players)
			{
				uint num = 0u;
				if (player.ProgressionManager.PathExperiences.TryGetValue(path, out var value))
				{
					num = value.CurrentLevel;
				}
				PlayerProgressionManager.GainExperience(player, path, xp);
				uint currentLevel = player.ProgressionManager.PathExperiences[path].CurrentLevel;
				logger.Debug("Player {0} got {1} xp on {2}, going from level {3} to level {4}", player.UserInfo.Username, xp, path, num, currentLevel);
			}
		}

		[Command("allxp", "Gain Experience on ALL Profession Paths")]
		[ServerOnly]
		private static void GainXp(PlayerList players, int xp)
		{
			int length = typeof(ProgressionPath).GetEnumValues().Length;
			foreach (Player player in players)
			{
				for (int i = 0; i < length; i++)
				{
					uint num = 0u;
					if (player.ProgressionManager.PathExperiences.TryGetValue((ProgressionPath)i, out var value))
					{
						num = value.CurrentLevel;
					}
					if (PlayerProgressionManager.GainExperience(player, (ProgressionPath)i, xp))
					{
						uint currentLevel = player.ProgressionManager.PathExperiences[(ProgressionPath)i].CurrentLevel;
						logger.Debug("Player {0} got {1} xp on {2}, going from level {3} to level {4}", player.UserInfo.Username, xp, (ProgressionPath)i, num, currentLevel);
					}
				}
			}
		}

		[Command("pathlevelup", "Gain a level on a Profession Path")]
		[ServerOnly]
		private static void GainPathLevel(PlayerList players, ProgressionPath path)
		{
			PathExperience value = null;
			foreach (Player player in players)
			{
				uint num = 0u;
				uint num2 = 0u;
				if (player.ProgressionManager.PathExperiences.TryGetValue(path, out value))
				{
					num = GlobalSettings<ProgressionSettings>.Instance.ExperiencePerLevel(value.CurrentLevel) - value.CurrentExperience;
					num2 = value.CurrentLevel;
				}
				else
				{
					num = GlobalSettings<ProgressionSettings>.Instance.ExperiencePerLevel(0u);
				}
				PlayerProgressionManager.GainExperience(player, path, num);
				uint num3 = num2 + 1;
				logger.Debug("Player {0} got {1} xp on {2}, going from level {3} to level {4}", player.UserInfo.Username, num, path, num2, num3);
			}
		}

		[Command("checkallxp", "List all paths and their xp")]
		[ServerOnly]
		private static void CheckAllXP(PlayerList players)
		{
			int length = typeof(ProgressionPath).GetEnumValues().Length;
			foreach (Player player in players)
			{
				for (int i = 0; i < length; i++)
				{
					ProgressionPath progressionPath = (ProgressionPath)i;
					uint num = 0u;
					uint currentLevel = 0u;
					if (player.ProgressionManager.PathExperiences.TryGetValue(progressionPath, out var value))
					{
						num = value.CurrentExperience;
						currentLevel = value.CurrentLevel;
					}
					logger.Debug("Player {0} on {1} has {2} xp, next level at {3} xp", player.UserInfo.Username, progressionPath, num, GlobalSettings<ProgressionSettings>.Instance.ExperiencePerLevel(currentLevel));
				}
			}
		}

		[Command("clearpath", "Clear all progress in a given path")]
		private static void ClearPath(Player player, ProgressionPath path)
		{
			if (player.ProgressionManager.PathExperiences.TryGetValue(path, out var value))
			{
				value.DebugClearSlots();
			}
			player.ProgressionManager.UpdateCounts();
			logger.Debug("Player {0} has cleared progress on {1}", player.UserInfo.Username, path);
		}

		[Command("clearall", "Clear all progress")]
		private static void ClearAll(PlayerList players)
		{
			int length = typeof(ProgressionPath).GetEnumValues().Length;
			foreach (Player player in players)
			{
				for (int i = 0; i < length; i++)
				{
					if (player.ProgressionManager.PathExperiences.TryGetValue((ProgressionPath)i, out var value))
					{
						value.DebugClearSlots();
					}
				}
				player.ProgressionManager.UpdateCounts();
				logger.Debug("Player {0} has cleared progress on all paths", player.UserInfo.Username);
			}
		}

		[Command("showskills", "Show available skills for player")]
		private static void ShowAvailableSkills(Player player)
		{
			available.Clear();
			lastPlayer = player;
			int num = 0;
			logger.Trace("Available skills for {0}", player.UserInfo.Username);
			int length = typeof(ProgressionPath).GetEnumValues().Length;
			for (int i = 0; i < length; i++)
			{
				ProgressionPath progressionPath = (ProgressionPath)i;
				ProfessionSkillTree professionTree = ProfessionSkillTree.GetProfessionTree(progressionPath);
				logger.Trace("{0} skills:", progressionPath);
				if (player.ProgressionManager.PathExperiences.TryGetValue(progressionPath, out var value))
				{
					foreach (ProgressionSlot slot in professionTree.Slots)
					{
						if (value.IsSlotAvailable(slot) == SkillStatus.Allowed)
						{
							available[num] = slot;
							logger.Trace("{0} {1}", num, slot.Name);
							num++;
						}
					}
					continue;
				}
				foreach (ProgressionSlot slot2 in professionTree.Slots)
				{
					if (slot2.Cost == 0 && slot2.Dependencies.Count == 0)
					{
						available[num] = slot2;
						logger.Trace("{0} {1}", num, slot2.Name);
						num++;
					}
				}
			}
		}

		[Command("buyskill", "Buy specified skill")]
		private static void BuySkill(int slotIndex, bool isConsumingExperience = true)
		{
			if (lastPlayer != null)
			{
				if (available.TryGetValue(slotIndex, out var value))
				{
					lastPlayer.ProgressionManager.UnlockSlot(value, isConsumingExperience);
					logger.Trace("{0} skill bought for {1}", value.Name, lastPlayer.UserInfo.Username);
				}
			}
			else
			{
				logger.Trace("Please user showskills first");
			}
		}

		[Command("offlinelevels", "Give levels to players even if they are offline")]
		[Alias(new string[] { "offlvl", "lvl" })]
		private static async Task AddOfflineLevels(UserInfoAccess userInfo, ProgressionPath path, int levels)
		{
			if (OfflinePlayerProgressionHandler.instance == null)
			{
				CommandService.ThrowError("Offline progression handler is not available");
			}
			if (await userInfo.IsValid())
			{
				int playerID = await userInfo.GetIdentifier();
				OfflinePlayerProgressionHandler.instance.ModifySkills(playerID, path, levels);
			}
		}

		[Command("printofflinelevels", "Give levels to players even if they are offline")]
		[Alias(new string[] { "printofflvl", "print-lvl" })]
		private static void PrintOfflineLevels()
		{
			if (OfflinePlayerProgressionHandler.instance == null)
			{
				CommandService.ThrowError("Offline progression handler is not available");
			}
			OfflinePlayerProgressionHandler.instance.LogCurrent();
		}
	}

	private static NLog.Logger logger = LogManager.GetCurrentClassLogger();

	[ServerOnly]
	[Command("username", "Get the username from a user identifier")]
	private static async Task<UserInfo> GetUsername(int userId)
	{
		UserInfo userInfo = await ApiAccess.ApiClient.UserClient.GetUserInfoAsync(userId);
		if (userInfo == null)
		{
			CommandService.ThrowError("Couldnt find user with id: {0}", userId);
		}
		return userInfo;
	}

	[ServerOnly]
	[Command("id", "Get the id from a username")]
	private static async Task<UserInfo> GetIdentifier(string username)
	{
		UserInfo userInfo = await ApiAccess.ApiClient.UserClient.GetUserInfoAsync(username);
		if (userInfo == null)
		{
			CommandService.ThrowError("Couldnt find user with username: {0}", username);
		}
		return userInfo;
	}

	[ServerOnly]
	[Command("count", "Responds with the number of online players")]
	private static int Count()
	{
		return Player.AllPlayers.Count;
	}

	[ServerOnly]
	[Command("kick", "Disconnects the player from the server")]
	private static void KickPlayer(PlayerList players, string reason = "No reason provided")
	{
		foreach (Player player in players)
		{
			if (player == null)
			{
				continue;
			}
			player.Kick(reason);
		}
	}

	[ServerOnly]
	[Command("list", "Lists all players")]
	private static IEnumerable<UserInfo> List()
	{
		return Player.AllPlayers.Where(player => player?.UserInfo != null).Select(player => player.UserInfo.UserInfo).ToList();
	}

	[ServerOnly]
	[Command("list-detailed", "Lists all players")]
	private static IEnumerable<UserInfoDetailed> ListDetailed()
	{
		return Player.AllPlayers.Where(player => player?.UserInfo != null).Select(player => new UserInfoDetailed(player)).ToList();
	}

	[ServerOnly]
	[Command("detailed", "Gets user info")]
	private static UserInfoDetailed ListDetailed(IPlayer player)
	{
		return new UserInfoDetailed(player);
	}

	[ServerOnly]
	[Command("message", "Sends a message to a player")]
	private static void Message(PlayerList players, string message, float duration = 10f)
	{
		PlayerCommunicationManager communicationManager = PlayerCommunicationManager.Instance;
		if (communicationManager == null)
		{
			CommandService.ThrowError("Player communication manager is not available yet");
		}
		logger.Trace("Sending message to " + players.CountOrName() + ".");
		foreach (var player in players)
		{
			if (player == null)
			{
				continue;
			}
			communicationManager.SendMessageToPlayer(player, message, duration);
		}
	}

	[ServerOnly]
	[Command("kill", "Kills a player")]
	private static void KillPlayer(PlayerList players)
	{
		logger.Trace("Killing " + players.CountOrName() + ".");
		foreach (var player in players)
		{
			var playerCharacter = player?.PlayerController as PlayerCharacter;
			if (playerCharacter == null || playerCharacter.Health == null)
			{
				logger.Trace("Skipping a player with no live character.");
				continue;
			}
			bool value = PlayerHealth.ArePlayersAvoidingDamage.Value;
			bool isIgnoringDamage = playerCharacter.IsIgnoringDamage;
			float playerDamageTakenMultiplier = ServerHandler.Current.ServerConfig.Settings.PlayerDamageTakenMultiplier;
			PlayerHealth.ArePlayersAvoidingDamage.Value = false;
			playerCharacter.IsIgnoringDamage = false;
			ServerHandler.Current.ServerConfig.Settings.PlayerDamageTakenMultiplier = 1f;
			try
			{
				playerCharacter.Health.ReceiveDamage(float.MaxValue, 0f, new DamageData(DamageSource.Command));
			}
			finally
			{
				ServerHandler.Current.ServerConfig.Settings.PlayerDamageTakenMultiplier = playerDamageTakenMultiplier;
				PlayerHealth.ArePlayersAvoidingDamage.Value = value;
				playerCharacter.IsIgnoringDamage = isIgnoringDamage;
			}
		}
	}

	[ServerOnly]
	[Command("setdamagemulti", "Sets the PlayerDamageTakenMultiplier value in Server Settings")]
	private static void SetDamageMultiplier_Command(float multiplier)
	{
		ServerHandler.Current.ServerConfig.Settings.PlayerDamageTakenMultiplier = multiplier;
		logger.Trace("PlayerDamageTakenMultiplier = " + ServerHandler.Current.ServerConfig.Settings.PlayerDamageTakenMultiplier);
	}

	[ServerOnly]
	[Command("getdamagemulti", "Gets the PlayerDamageTakenMultiplier value in Server Settings")]
	private static void GetDamageMultiplier_Command()
	{
		logger.Trace("PlayerDamageTakenMultiplier = " + ServerHandler.Current.ServerConfig.Settings.PlayerDamageTakenMultiplier);
	}

	[ServerOnly]
	[Command("cripple", "Cripples a player")]
	private static void CripplePlayer(PlayerList players)
	{
		logger.Trace("Crippling " + players.CountOrName() + ".");
		foreach (Player player in players)
		{
			PlayerCharacter playerCharacter = player?.PlayerController as PlayerCharacter;
			if (playerCharacter == null)
			{
				logger.Trace("Skipping a player with no live character.");
				continue;
			}
			PlayerHealth health = playerCharacter.Health;
			if (health == null || health.CrippleStat == null)
			{
				logger.Trace("Skipping a player with no cripple stat.");
				continue;
			}
			bool value = PlayerHealth.ArePlayersAvoidingDamage.Value;
			bool isIgnoringDamage = playerCharacter.IsIgnoringDamage;
			PlayerHealth.ArePlayersAvoidingDamage.Value = false;
			playerCharacter.IsIgnoringDamage = false;
			try
			{
				health.CrippleStat.Base -= 10000f;
			}
			finally
			{
				PlayerHealth.ArePlayersAvoidingDamage.Value = value;
				playerCharacter.IsIgnoringDamage = isIgnoringDamage;
			}
		}
	}

	[ServerOnly]
	[Command("teleport", "Teleports a player to another")]
	[Alias(new string[] { "tp" })]
	private static void Teleport(PlayerList players, Player targetPlayer)
	{
		if (targetPlayer.PlayerController == null)
		{
			logger.Trace("Target player has no controller");
			return;
		}
		logger.Trace("Teleporting " + players.CountOrName() + " to " + targetPlayer.UserInfo.Username + ".");
		Vector3 forward = targetPlayer.PlayerController.Head.forward;
		forward.y = 0f;
		forward.Normalize();
		Vector3 position = targetPlayer.PlayerController.Head.position;
		position.y = targetPlayer.PlayerController.transform.position.y;
		position += forward * 2f;
		foreach (Player player in players)
		{
			player.PlayerController?.ForceTeleportTo(position);
		}
	}

	[Command("god-mode", "Makes a player invulnerable")]
	[Alias(new string[] { "godmodeon" })]
	private static void GodMode(PlayerList players, bool isOn = true)
	{
		foreach (Player player in players)
		{
			if (player?.PlayerController != null)
			{
				player.PlayerController.IsIgnoringDamage = isOn;
			}
		}
	}

	[Command("god-mode-toggle", "Flips a players invulnerability on or off")]
	[Alias(new string[] { "godmodetoggle" })]
	private static void GodModeToggle(PlayerList players)
	{
		foreach (Player player in players)
		{
			if (player?.PlayerController != null)
			{
				player.PlayerController.IsIgnoringDamage = !player.PlayerController.IsIgnoringDamage;
			}
		}
	}

	[Command("teleport", "Teleports the player to a destination.")]
	[Alias(new string[] { "tp" })]
	public static void Teleport(PlayerList players, SpawnAreaIdentifier target = SpawnAreaIdentifier.RespawnPoint)
	{
		SpawnArea spawnArea = null;
		if (target != SpawnAreaIdentifier.Home && !SpawnArea.InstanceMap.TryGetValue(target, out spawnArea))
		{
			CommandService.ThrowError("No spawn area is loaded for {0}", target);
		}
		foreach (Player player in players)
		{
			PlayerController controller = player?.PlayerController;
			if (controller == null)
			{
				logger.Trace("Skipping a player with no live controller.");
				continue;
			}
			PlayerEffectController component = controller.GetComponent<PlayerEffectController>();
			Vector3 position = ((target != SpawnAreaIdentifier.Home) ? spawnArea.GetRandomSpawnPosition() : player.GetSpawnPosition(true));
			if (component != null)
			{
				component.Teleport(position, 1f);
			}
			else if (controller.LocomotionController != null)
			{
				controller.LocomotionController.MoveTo(position, LocomotionFunction.Reposition);
			}
			else
			{
				logger.Trace("Skipping a player with no locomotion controller.");
				continue;
			}
			logger.Debug("Teleported {0} to {1}", player.UserInfo?.Username, target);
		}
	}

	[Command("unlock-cancel", "Testing command while working on unlocks")]
	private static void UnlockCancel(Player player)
	{
		if (player?.UnlockManager == null)
		{
			CommandService.ThrowError("That player has no unlock manager");
		}
		player.UnlockManager.CancelUnlock();
	}

	[Command("set-unlock", "Set Unlock")]
	[NonSimpleServer(new string[] { "debug_features" })]
	private static void CheckStat(PlayerList players, PlayerUnlock unlock, bool isUnlocked = true)
	{
		foreach (Player player in players)
		{
			if (player?.UnlockManager == null) continue;
			player.UnlockManager.SetPermanentUnlock(unlock, isUnlocked);
			if (player.UnlockManager.CurrentUnlock == unlock)
			{
				player.UnlockManager.CancelUnlock();
			}
		}
	}

	[Command("unlock-check", "Check Unlock")]
	private static bool CheckStat(Player player, PlayerUnlock unlock)
	{
		return player?.UnlockManager != null && player.UnlockManager.Check(unlock);
	}

	[Command("get-home", "Gets a players respawn point")]
	private static Vector3 GetHome(Player player)
	{
		if (player?.Save?.Data == null)
		{
			CommandService.ThrowError("The player has no save loaded yet.");
		}
		return player.Save.Data.home;
	}

	[Command("set-home", "Sets a players respawn point")]
	private static void SetHome(PlayerList players, Vector3 home = default(Vector3))
	{
		foreach (Player player in players)
		{
			if (player?.Save?.Data == null)
			{
				logger.Trace("Skipping a player with no save loaded");
				continue;
			}
			player.Save.Data.home = home;
		}
	}

	[Command("set-stat", "Set stat value")]
	[Alias(new string[] { "setstat" })]
	private static void SetStat(PlayerList players, StatDefinition statDefinition, float value, StatApplicationType applicationType = StatApplicationType.Base)
	{
		foreach (Player player in players)
		{
			Stat statOnPlayer = (player != null) ? statDefinition.GetStatOnPlayer(player) : null;
			if (statOnPlayer == null)
			{
				logger.Trace("Skipping a player with no '" + statDefinition.Name + "' stat.");
				continue;
			}
			switch (applicationType)
			{
			case StatApplicationType.Base:
				statOnPlayer.Base = value;
				break;
			case StatApplicationType.Min:
				statOnPlayer.Minimum = value;
				break;
			case StatApplicationType.Max:
				statOnPlayer.Maximum = value;
				break;
			}
			logger.Trace(string.Concat(player, " ", statDefinition.Name, " ", applicationType.ToString(), " value is ", value));
		}
	}

	[Command("modify-stat", "Apply a timed modifier to a stat")]
	[Alias(new string[] { "modifystat" })]
	private static void ModifyStat(PlayerList players, StatDefinition statDefinition, float valueModifier, float duration, bool isMultiplier = false)
	{
		foreach (Player player in players)
		{
			PlayerController controller = player?.PlayerController;
			Stat statOnPlayer = (controller != null) ? statDefinition.GetStatOnPlayer(player) : null;
			if (statOnPlayer == null)
			{
				logger.Trace("Skipping a player with no '" + statDefinition.Name + "' stat.");
				continue;
			}
			StatModifier modifier = new StatModifier(statOnPlayer, valueModifier, isMultiplier);
			controller.Health.StatManager.ApplyTimedModifier(modifier, duration);
			logger.Trace(string.Concat(player, " ", statOnPlayer.Definition.Name, " is now modified, current value is ", statOnPlayer.Value));
		}
	}

	[Command("check-stat", "Check Stat Value")]
	[Alias(new string[] { "checkstat" })]
	private static StatValue CheckStat(Player player, Stat stat)
	{
		return stat;
	}

	[Command("list-stats", "List all available stats for players")]
	[Alias(new string[] { "list-stat" })]
	private static IEnumerable<StatDefinitionInfo> ListStats(Player player)
	{
		if (player?.PlayerController == null)
		{
			return GlobalSettings<PlayerControllerPrefabs>.Instance.GetController(PlayerMode.OpenVR).GetComponentInChildren<StatManager>().DefinitionApplications.Select((Func<StatDefinitionApplication, StatDefinitionInfo>)((StatDefinitionApplication stat) => stat));
		}
		return player.PlayerController.Health.StatManager.Stats.Select((Func<KeyValuePair<StatDefinition, Stat>, StatDefinitionInfo>)((KeyValuePair<StatDefinition, Stat> statPair) => statPair.Value));
	}
}
