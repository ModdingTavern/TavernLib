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
using TavernLib.Backend.Api;
using TavernLib.Services;
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
				await altaFile.QueueUnloadAsync();
			}
			return result;
			string GetData(PlayerSave save)
			{
				byte[] array = new byte[save.PrefabData.Length * 4];
				Buffer.BlockCopy(save.PrefabData, 0, array, 0, array.Length);
				return Encoding.UTF8.GetString(array);
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
			logger.Trace("Loading inventory for " + id);
			byte[] bytes = Encoding.UTF8.GetBytes(save);
			uint[] array = new uint[bytes.Length / 4];
			Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
			(obj.Content as PlayerSave).PrefabData = array;
			await obj.QueueUnloadAsync();
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
					yield return null;
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
			OfflinePlayerProgressionHandler.instance.LogCurrent();
		}
	}

	private static NLog.Logger logger = LogManager.GetCurrentClassLogger();

	[ServerOnly]
	[Command("username", "Get the username from a user identifier")]
	private static UserInfo GetUsername(int userId)
	{
		var foundMatch = TavernServices.GetService<TavernManager>().UserConfig.LastRead.Users.Where(kvp => kvp.Value.UserId == (ulong)userId).ToList();
		
		if (!foundMatch.Any()) CommandService.ThrowError("Couldn't find user with id: {0}", userId);

		var firstPlayer = foundMatch[0];
		return new UserInfo((int)firstPlayer.Value.UserId, firstPlayer.Key);
	}

	[ServerOnly]
	[Command("id", "Get the id from a username")]
	private static UserInfo GetIdentifier(string username)
	{
		if (TavernServices.GetService<TavernManager>().UserConfig.LastRead.Users.TryGetValue(username.ToLower(), out var user))
			return new UserInfo((int)user.UserId, username);
		
		
		CommandService.ThrowError("Couldn't find user with username: {0}", username);
		return null; // Stupid compiler problem, it doesn't know the function above throws :(
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
			player.Kick(reason);
		}
	}

	[ServerOnly]
	[Command("list", "Lists all players")]
	private static IEnumerable<UserInfo> List()
	{
		return Player.AllPlayers.Select(player => player.UserInfo.UserInfo);
	}

	[ServerOnly]
	[Command("list-detailed", "Lists all players")]
	private static IEnumerable<UserInfoDetailed> ListDetailed()
	{
		return Player.AllPlayers.Select(player => new UserInfoDetailed(player));
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
		logger.Trace("Sending message to " + players.CountOrName() + ".");
		foreach (var player in players)
		{
			PlayerCommunicationManager.Instance.SendMessageToPlayer(player, message, duration);
		}
	}

	[ServerOnly]
	[Command("kill", "Kills a player")]
	private static void KillPlayer(PlayerList players)
	{
		logger.Trace("Killing " + players.CountOrName() + ".");
		foreach (var player in players)
		{
			var playerCharacter = player.PlayerController as PlayerCharacter;
			bool value = PlayerHealth.ArePlayersAvoidingDamage.Value;
			bool isIgnoringDamage = playerCharacter != null && playerCharacter.IsIgnoringDamage;
			PlayerHealth.ArePlayersAvoidingDamage.Value = false;
			if (playerCharacter != null)
			{
				playerCharacter.IsIgnoringDamage = false;
				float playerDamageTakenMultiplier = ServerHandler.Current.ServerConfig.Settings.PlayerDamageTakenMultiplier;
				ServerHandler.Current.ServerConfig.Settings.PlayerDamageTakenMultiplier = 1f;
				playerCharacter.Health.ReceiveDamage(float.MaxValue, 0f, new DamageData(DamageSource.Command));
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
			PlayerCharacter playerCharacter = player.PlayerController as PlayerCharacter;
			if (player == null)
			{
				break;
			}
			bool value = PlayerHealth.ArePlayersAvoidingDamage.Value;
			bool isIgnoringDamage = playerCharacter.IsIgnoringDamage;
			PlayerHealth.ArePlayersAvoidingDamage.Value = false;
			playerCharacter.IsIgnoringDamage = false;
			playerCharacter.Health.CrippleStat.Base -= 10000f;
			PlayerHealth.ArePlayersAvoidingDamage.Value = value;
			playerCharacter.IsIgnoringDamage = isIgnoringDamage;
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
	private static void GodMode(PlayerList players, bool isOn)
	{
		foreach (Player player in players)
		{
			if (player.PlayerController != null)
			{
				player.PlayerController.IsIgnoringDamage = isOn;
			}
		}
	}

	[Command("teleport", "Teleports the player to a destination.")]
	[Alias(new string[] { "tp" })]
	public static void Teleport(PlayerList players, SpawnAreaIdentifier target = SpawnAreaIdentifier.RespawnPoint)
	{
		foreach (Player player in players)
		{
			PlayerEffectController component = player.PlayerController.GetComponent<PlayerEffectController>();
			Vector3 position = ((target != SpawnAreaIdentifier.Home) ? SpawnArea.InstanceMap[target].GetRandomSpawnPosition() : player.GetSpawnPosition(true));
			if (component != null)
			{
				component.Teleport(position, 1f);
			}
			else
			{
				player.PlayerController.LocomotionController.MoveTo(position, LocomotionFunction.Reposition);
			}
			logger.Debug("Teleported {0} to {1}", player.UserInfo.Username, target);
		}
	}

	[Command("unlock-cancel", "Testing command while working on unlocks")]
	private static void UnlockCancel(Player player)
	{
		player.UnlockManager.CancelUnlock();
	}

	[Command("set-unlock", "Set Unlock")]
	[NonSimpleServer(new string[] { "debug_features" })]
	private static void CheckStat(PlayerList players, PlayerUnlock unlock, bool isUnlocked = true)
	{
		foreach (Player player in players)
		{
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
		return player.UnlockManager.Check(unlock);
	}

	[Command("get-home", "Gets a players respawn point")]
	private static Vector3 GetHome(Player player)
	{
		return player.Save.Data.home;
	}

	[Command("set-home", "Sets a players respawn point")]
	private static void SetHome(PlayerList players, Vector3 home = default(Vector3))
	{
		foreach (Player player in players)
		{
			player.Save.Data.home = home;
		}
	}

	[Command("set-stat", "Set stat value")]
	[Alias(new string[] { "setstat" })]
	private static void SetStat(PlayerList players, StatDefinition statDefinition, float value, StatApplicationType applicationType = StatApplicationType.Base)
	{
		foreach (Player player in players)
		{
			Stat statOnPlayer = statDefinition.GetStatOnPlayer(player);
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
			Stat statOnPlayer = statDefinition.GetStatOnPlayer(player);
			StatModifier modifier = new StatModifier(statOnPlayer, valueModifier, isMultiplier);
			player.PlayerController.Health.StatManager.ApplyTimedModifier(modifier, duration);
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
