using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Holoville.HOTween;
using MoonSharp.Interpreter;
using Newtonsoft.Json;
using Steamworks;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wenzil.Console;

[MoonSharpUserData]
public class Globals : MonoBehaviour
{
	[MoonSharpHidden]
	public void OnApplicationFocus(bool focus)
	{
		this._applicationFocused = focus;
		if (this._applicationFocused)
		{
			this.RemoveTimeScale("focus");
		}
		else
		{
			this.AddTimeScale("focus", 0f);
		}
	}

	[MoonSharpHidden]
	public bool ShouldCheckSight()
	{
		if (this.lookFrameTime != Time.frameCount)
		{
			this.lookCount = 0;
			this.lookFrameTime = Time.frameCount;
		}
		if (this.lookCount >= 2)
		{
			return false;
		}
		this.lookCount++;
		return true;
	}

	[MoonSharpHidden]
	public void RemoveSaveData(string identifier_name)
	{
		if (Globals.GetInstance().saveData.ContainsKey(identifier_name))
		{
			string areaName = Globals.GetInstance().saveData[identifier_name].areaName;
			if (Globals.GetInstance().GetAreaSaveData(areaName).data.Contains(Globals.GetInstance().saveData[identifier_name]))
			{
				Globals.GetInstance().GetAreaSaveData(areaName).data.Remove(Globals.GetInstance().saveData[identifier_name]);
			}
			Globals.GetInstance().saveData.Remove(identifier_name);
		}
	}

	[MoonSharpHidden]
	public List<BaseItem> GetItems()
	{
		if (this.IsOnlineMode())
		{
			if (this._vanillaItems == null)
			{
				this._vanillaItems = new List<BaseItem>((Resources.Load("Globals") as GameObject).GetComponent<Globals>().items);
			}
			return this._vanillaItems;
		}
		return this.items;
	}

	[MoonSharpHidden]
	public SeededRandom GetDungeonRandom()
	{
		if (this._dungeonLayoutRandom == null)
		{
			this._dungeonLayoutRandom = new SeededRandom();
		}
		if (this._seededRandom == null)
		{
			this._seededRandom = new SeededRandom();
		}
		if (!this.isGeneratingLevel)
		{
			return this._seededRandom;
		}
		return this._dungeonLayoutRandom;
	}

	[MoonSharpHidden]
	public ConsoleController GetConsole()
	{
		return this._console;
	}

	[MoonSharpHidden]
	public string GetLevelName()
	{
		return Application.loadedLevelName;
	}

	[MoonSharpHidden]
	public void FreezeFrame()
	{
		if (!this.critFrameHoldDisabled)
		{
			this._freezeDuration = 0.15f;
		}
	}

	[MoonSharpHidden]
	public List<AudioClip> GetFootstepSound(PhysicMaterial ground_material)
	{
		if (ground_material == null)
		{
			return this.footstepSounds[0].footstepSounds;
		}
		foreach (Globals.FootstepSoundDefinition footstepSoundDefinition in this.footstepSounds)
		{
			if (footstepSoundDefinition.physicMaterial == ground_material)
			{
				return footstepSoundDefinition.footstepSounds;
			}
		}
		return null;
	}

	[MoonSharpHidden]
	public DateTime GetServerToday()
	{
		return this._lastServerTime;
	}

	[MoonSharpHidden]
	public string GetLeaderboardName(DateTime time)
	{
		return string.Concat(new object[]
		{
			time.Month,
			"/",
			time.Day,
			"/",
			time.Year,
			" ",
			this.buildNumber
		});
	}

	[MoonSharpHidden]
	public bool IsOnlineMode()
	{
		return this._dailyOnlineMode;
	}

	[MoonSharpHidden]
	public int CalculateInfiniteSeed(DateTime time)
	{
		if (this.infiniteCoolerSeedOverride >= 0)
		{
			return this.infiniteCoolerSeedOverride;
		}
		return (time.Date.DayOfYear + 366 * (time.Date.Year + this.buildNumber)) % int.MaxValue;
	}

	[MoonSharpHidden]
	public int GetInfiniteSeed()
	{
		return this._infiniteSeed;
	}

	[MoonSharpHidden]
	public void StartCourierRun(Globals.Callback on_failure = null, Globals.Callback on_success = null, bool use_narrator = false)
	{
		base.StartCoroutine(this._StartCourierRun(on_failure, on_success, use_narrator));
	}

	[MoonSharpHidden]
	public string GetDailyLeaderboardName()
	{
		return this._dailyLeaderboardName;
	}

	[MoonSharpHidden]
	public IEnumerator GetTimeFromServer(Globals.Callback on_failure = null)
	{
		PopupManager popup = Globals.GetInstance().popup;
		popup.BeginPopup("Contacting Server");
		popup.AddWidget<PopupLabel>(string.Empty).SetLabel("Please wait...");
		popup.EndPopup();
		this._dailyOnlineMode = false;
		WWW seed_query = new WWW("http://www.adultswim.com/videos/api/v1/_server_time");
		while (!seed_query.isDone)
		{
			yield return 0;
		}
		popup.ClosePopup();
		if (string.IsNullOrEmpty(seed_query.error))
		{
			try
			{
				Globals.ServerTimeResponse serverTimeResponse = JsonConvert.DeserializeObject<Globals.ServerTimeResponse>(seed_query.text);
				long utc_timestamp = serverTimeResponse.utc_timestamp;
				this._lastServerTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
				this._lastServerTime = this._lastServerTime.AddMilliseconds((double)utc_timestamp);
				this._lastServerTime = this._lastServerTime.Date;
				this._infiniteSeed = this.CalculateInfiniteSeed(this._lastServerTime);
				this._dailyLeaderboardName = this.GetLeaderboardName(this._lastServerTime);
				this._dailyOnlineMode = true;
			}
			catch (Exception ex)
			{
				Debug.Log(ex.Message);
			}
		}
		if (!this._dailyOnlineMode)
		{
			popup.BeginPopup("Error");
			popup.AddWidget<PopupLabel>(string.Empty).SetLabel("Couldn't contact server.\n\nPlease ensure you are connected to the internet and try again later.");
			popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetHotkey("Accept").SetLabel("Okay").AddCallback(delegate(PopupButton popup_button)
			{
				popup.ClosePopup();
			});
			popup.EndPopup();
			while (popup.IsActive())
			{
				yield return 0;
			}
			if (on_failure != null)
			{
				on_failure();
			}
			yield break;
		}
		yield break;
	}

	[MoonSharpHidden]
	public IEnumerator CheckCourierRunValidity()
	{
		this._canAttemptCourierRun = false;
		yield return this.leaderboards.UpdateLastScore();
		if (!Globals.GetInstance().IsGameGenuine())
		{
			PopupManager popup = Globals.GetInstance().popup;
			popup.BeginPopup("Game Modified");
			popup.AddWidget<PopupLabel>(string.Empty).SetLabel("You can't run the Courier Run with a modified/modded version of the game!");
			popup.AddWidget<PopupSpacer>(string.Empty);
			PopupButton no_button = popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetLabel("Okay").AddCallback(delegate(PopupButton button)
			{
				popup.ClosePopup();
			});
			popup.EndPopup();
			while (popup.IsActive())
			{
				yield return 0;
			}
			yield break;
		}
		if (this.leaderboards.GetLastScore() >= 0 || (Globals.GetInstance().GetServerToday().Kind != DateTimeKind.Unspecified && Globals.GetInstance().lastCoolerRunSeed == Globals.GetInstance().CalculateInfiniteSeed(Globals.GetInstance().GetServerToday())))
		{
			this.lastCoolerRunSeed = Globals.GetInstance().CalculateInfiniteSeed(Globals.GetInstance().GetServerToday());
			string contents = JsonConvert.SerializeObject(this._gameSaveData, Formatting.Indented, this.GetSerializerSettings());
			this.SaveData(this.GetSaveFileName(), contents, "sav");
			PopupManager popup = Globals.GetInstance().popup;
			Globals.GetInstance().PlayGlobalSound(Globals.GetInstance().errorSound, "notification");
			popup.BeginPopup("Already Attempted");
			popup.AddWidget<PopupLabel>(string.Empty).SetLabel("You've already attempted the Courier Run for today. Try again tomorrow!");
			popup.AddWidget<PopupSpacer>(string.Empty);
			popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetLabel("Okay").AddCallback(delegate(PopupButton button)
			{
				popup.ClosePopup();
			});
			popup.EndPopup();
			while (popup.IsActive())
			{
				yield return 0;
			}
			yield break;
		}
		if (ModManager.GetInstance().IsModded() && !this._modWarningShown)
		{
			this._modWarningShown = true;
			PopupManager popup = Globals.GetInstance().popup;
			popup.BeginPopup("Game Mods Active");
			popup.AddWidget<PopupLabel>(string.Empty).SetLabel("Because you are running the game with active mods, you cannot submit scores to the leaderboards.");
			popup.AddWidget<PopupSpacer>(string.Empty);
			PopupButton no_button2 = popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetLabel("Okay").AddCallback(delegate(PopupButton button)
			{
				popup.ClosePopup();
			});
			popup.EndPopup();
			while (popup.IsActive())
			{
				yield return 0;
			}
		}
		this._canAttemptCourierRun = true;
		yield break;
	}

	[MoonSharpHidden]
	public bool CanAttemptCourierRun()
	{
		return this._canAttemptCourierRun;
	}

	[MoonSharpHidden]
	protected IEnumerator _StartCourierRun(Globals.Callback on_failure, Globals.Callback on_success, bool use_narrator)
	{
		if (this._dailyOnlineMode && !this.NeedServerTimeUpdate())
		{
			yield return this.GetTimeFromServer(on_failure);
			if (!this._dailyOnlineMode)
			{
				if (on_failure != null)
				{
					on_failure();
				}
				yield break;
			}
		}
		yield return this.CheckCourierRunValidity();
		if (!this.CanAttemptCourierRun())
		{
			if (on_failure != null)
			{
				on_failure();
			}
			yield break;
		}
		if (on_success != null)
		{
			on_success();
		}
		yield return this._StartInfiniteCooler(use_narrator);
		yield break;
	}

	[MoonSharpHidden]
	public bool NeedServerTimeUpdate()
	{
		if (!this._dailyOnlineMode)
		{
			return true;
		}
		if (DateTime.UtcNow.CompareTo(this._lastServerTime.AddDays(1.0)) >= 0)
		{
			Debug.Log("Should refresh time.");
			return true;
		}
		return false;
	}

	[MoonSharpHidden]
	public void StartInfiniteCooler(bool use_narrator = false)
	{
		if (this.infiniteCoolerSeedOverride >= 0)
		{
			this._infiniteSeed = this.infiniteCoolerSeedOverride;
		}
		else
		{
			this._infiniteSeed = UnityEngine.Random.Range(0, int.MaxValue);
		}
		this._dailyOnlineMode = false;
		if (!this._isInfiniteCooler)
		{
			base.StartCoroutine(this._StartInfiniteCooler(use_narrator));
		}
	}

	[MoonSharpHidden]
	public bool IsInfiniteCooler()
	{
		return this._isInfiniteCooler;
	}

	[MoonSharpHidden]
	public void EndInfiniteCooler()
	{
		if (this._isInfiniteCooler)
		{
			base.StartCoroutine(this._EndInfiniteCooler());
		}
	}

	[MoonSharpHidden]
	public IEnumerator _StartInfiniteCooler(bool use_narrator = false)
	{
		this.useNarrator = use_narrator;
		this.loadingScreen.Show();
		InfiniteLevelType.spawnFactionModifiers = null;
		yield return 0;
		this.level = 1;
		this.score = 0;
		if (this._dungeon != null)
		{
			this._dungeon.OnSave();
			this._wasInfiniteCoolerMenuInitiated = false;
			this._gameSaveData = new Globals.GameSaveData(this);
		}
		else
		{
			this._wasInfiniteCoolerMenuInitiated = true;
			this._gameSaveData = null;
			if (this.HasSaveData())
			{
				yield return base.StartCoroutine(this.LoadGame());
				if (!this.loadSuccessful)
				{
					this.loadingScreen.Hide();
					this.popup.BeginPopup("Corrupt Save Game");
					this.popup.AddWidget<PopupLabel>(string.Empty).SetLabel("Cannot start Courier Run with corrupt save data. Please start a new game.");
					this.popup.AddWidget<PopupSpacer>(string.Empty);
					this.popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetLabel("Okay").AddCallback(delegate(PopupButton button)
					{
						this.popup.ClosePopup();
					});
					this.popup.EndPopup();
					while (this.popup.IsActive())
					{
						yield return 0;
					}
					this.LoadLevel("Main Menu", delegate
					{
						this.Reset(false);
					}, true);
					yield break;
				}
			}
		}
		this.loadSuccessful = false;
		if (this._dailyOnlineMode)
		{
			Globals.GetInstance().infiniteAmmo = false;
			this.lastCoolerRunSeed = this._infiniteSeed;
		}
		List<string> transferred_keys = new List<string>();
		if (this._gameSaveData != null)
		{
			if (this._gameSaveData.lastCoolerRunSeed != this.lastCoolerRunSeed)
			{
				this._gameSaveData.lastCoolerRunScore = 0;
			}
			this._gameSaveData.lastCoolerRunSeed = this.lastCoolerRunSeed;
			foreach (string text in this.gameKeys.Keys)
			{
				if (text.StartsWith("cr_"))
				{
					transferred_keys.Add(text);
				}
			}
			string data = JsonConvert.SerializeObject(this._gameSaveData, Formatting.Indented, this.GetSerializerSettings());
			this.SaveData(this.GetSaveFileName(), data, "sav");
		}
		if (this._player != null)
		{
			UnityEngine.Object.Destroy(this._player.gameObject);
		}
		yield return 0;
		yield return 0;
		this.Reset(false);
		yield return 0;
		yield return 0;
		foreach (string key in transferred_keys)
		{
			this.AddKey(key);
		}
		this.GetDungeonRandom();
		this._dungeonLayoutRandom.SetSeed(this._infiniteSeed);
		this._seededRandom.SetSeed(this._infiniteSeed);
		this._seededRandom.Reseed();
		this._isInfiniteCooler = true;
		this.UpdateCourierSlotLimit();
		UnityEngine.Random.seed = this._dungeonLayoutRandom.Range(0, int.MaxValue);
		this.SetPlayer(Player.CreateNewPlayer());
		foreach (BaseItem baseItem in this._player.GetInventory().GetItems(true))
		{
			if (!(baseItem.GetPrefab() == this._player.GetDefaultWeaponPrefab()))
			{
				this._player.GetInventory().RemoveItem(baseItem, false);
			}
		}
		InventoryInitializer[] initializers = this.courierRunStartLoot.GetComponentsInChildren<InventoryInitializer>(true);
		this.isGeneratingLevel = true;
		foreach (InventoryInitializer inventoryInitializer in initializers)
		{
			inventoryInitializer.Apply(this._player.GetInventory());
		}
		foreach (BaseItem baseItem2 in this._player.GetInventory().GetItems(true))
		{
			if (!(baseItem2.GetPrefab() == this._player.GetDefaultWeaponPrefab()))
			{
				if (!baseItem2.HasItemTag("never_best"))
				{
					if (baseItem2 is Weapon)
					{
						this._player.Equip(baseItem2 as Weapon);
						break;
					}
				}
			}
		}
		this._player.EquipAvailableArmor();
		this.isGeneratingLevel = false;
		this.LoadLevel("Cooler Infinite", null, true);
		yield break;
	}

	[MoonSharpHidden]
	public void UpdateCourierSlotLimit()
	{
		if (this._isInfiniteCooler)
		{
			this.courierBox.slotLimit = 1;
		}
		else
		{
			this.courierBox.slotLimit = (Resources.Load("Globals") as GameObject).GetComponent<Globals>().courierBox.slotLimit;
		}
		if (this._player != null)
		{
			this.courierBox.slotLimit += (int)this._player.GetSymbolValue(CharacterAttributeManager.GetInstance().GetAttributeIndex("COURIER_SLOTS"), null, ParamEvalMode.Normal, null);
		}
	}

	[MoonSharpHidden]
	public bool IsGameGenuine()
	{
		return !Application.genuineCheckAvailable || Application.genuine;
	}

	[MoonSharpHidden]
	public IEnumerator _EndInfiniteCooler()
	{
		Globals.GetInstance().PlayGlobalSound(Globals.GetInstance().noticeSound, "notification");
		PopupManager popup = Globals.GetInstance().popup;
		popup.BeginPopup("Final Score");
		popup.AddWidget<PopupLabel>(string.Empty).SetLabel("Your final score on this Courier Run was:\n" + Globals.GetInstance().score);
		popup.AddWidget<PopupSpacer>(string.Empty);
		popup.AddWidget<PopupButton>(string.Empty).SetHotkey("Accept").SetLabel("Okay").AddCallback(delegate(PopupButton button)
		{
			popup.ClosePopup();
		});
		popup.EndPopup();
		while (popup.IsActive())
		{
			yield return 0;
		}
		if (!ModManager.GetInstance().IsModded())
		{
			if (Globals.GetInstance().IsGameGenuine())
			{
				if (this._dailyOnlineMode)
				{
					Globals.GetInstance().leaderboards.UploadScore(this._dailyLeaderboardName, Globals.GetInstance().score, Globals.GetInstance().level);
				}
			}
		}
		while (Globals.GetInstance().leaderboards.IsActive())
		{
			yield return 0;
		}
		this.loadingScreen.Show();
		yield return 0;
		UnityEngine.Object.Destroy(this._player.gameObject);
		this._player = null;
		yield return 0;
		yield return 0;
		this.loadSuccessful = false;
		this.isDeserializing = true;
		this._isInfiniteCooler = false;
		int old_score = this.score;
		if (this._gameSaveData != null)
		{
			if (this._gameSaveData.lastCoolerRunSeed == this.lastCoolerRunSeed)
			{
				this._gameSaveData.lastCoolerRunScore = Mathf.Max(this.score, this._gameSaveData.lastCoolerRunScore);
			}
			List<BaseItem.ItemSaveData> list = new List<BaseItem.ItemSaveData>();
			if (this._dailyOnlineMode || this._canStashInPractice)
			{
				foreach (BaseItem baseItem in this.courierBox.GetItems(false))
				{
					list.Add(baseItem.SerializeData());
				}
				this._gameSaveData.courierRun.items.AddRange(list);
			}
			foreach (string text in this.gameKeys.Keys)
			{
				if (text.StartsWith("cr_") && !this._gameSaveData.gameKeys.Contains(text))
				{
					this._gameSaveData.gameKeys.Add(text);
				}
			}
			string data = JsonConvert.SerializeObject(this._gameSaveData, Formatting.Indented, this.GetSerializerSettings());
			this.SaveData(this.GetSaveFileName(), data, "sav");
		}
		yield return 0;
		yield return 0;
		this.Reset(false);
		yield return 0;
		yield return 0;
		if (this._gameSaveData != null)
		{
			this._gameSaveData.DeserializeData(this);
		}
		this.isDeserializing = false;
		this.loadSuccessful = true;
		if (this._wasInfiniteCoolerMenuInitiated)
		{
			Globals.GetInstance().LoadLevel("Main Menu", delegate
			{
				this.Reset(false);
			}, true);
		}
		else
		{
			this.FinalizeLoad();
		}
		yield return 0;
		yield break;
	}

	[MoonSharpHidden]
	public Globals.FootstepSoundDefinition GetMaterialDefinition(PhysicMaterial ground_material)
	{
		if (ground_material == null)
		{
			return this.footstepSounds[0];
		}
		foreach (Globals.FootstepSoundDefinition footstepSoundDefinition in this.footstepSounds)
		{
			if (footstepSoundDefinition.physicMaterial == ground_material)
			{
				return footstepSoundDefinition;
			}
		}
		return null;
	}

	[MoonSharpHidden]
	public GameObject GetHitImpact(PhysicMaterial ground_material)
	{
		if (ground_material == null)
		{
			return this.footstepSounds[0].hitImpact;
		}
		foreach (Globals.FootstepSoundDefinition footstepSoundDefinition in this.footstepSounds)
		{
			if (footstepSoundDefinition.physicMaterial == ground_material)
			{
				return footstepSoundDefinition.hitImpact;
			}
		}
		return null;
	}

	[MoonSharpHidden]
	public BaseCharacter GetCharacter(BaseCharacter character)
	{
		if (character == null)
		{
			return null;
		}
		if (character.gameObject == Resources.Load("Player") as GameObject)
		{
			return this.GetPlayer();
		}
		return character;
	}

	[MoonSharpHidden]
	public GameObject GetGameObject(GameObject target)
	{
		if (target == null)
		{
			return null;
		}
		if (target == Resources.Load("Main Camera") as GameObject)
		{
			return this._mainCamera.gameObject;
		}
		if (target == Resources.Load("Player") as GameObject)
		{
			return this.GetPlayer().gameObject;
		}
		if (target.GetComponent<Saveable>() != null)
		{
			Saveable saveable = target.GetComponent<Saveable>();
			if (!string.IsNullOrEmpty(saveable.identifierName))
			{
				saveable = Globals.GetInstance().GetDungeon().GetSaveableByIdentifier(saveable.identifierName);
				if (saveable != null)
				{
					return saveable.gameObject;
				}
				GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(target.gameObject);
				saveable = gameObject.GetComponent<Saveable>();
				if (Globals.GetInstance().saveData.ContainsKey(saveable.identifierName))
				{
					saveable.data = Globals.GetInstance().saveData[saveable.identifierName];
					saveable.data.DeserializeData(saveable);
				}
				saveable.StartGhosting();
				saveable.Initialize(this.GetDungeon());
				saveable.SetArea("limbo");
				return saveable.gameObject;
			}
		}
		return target;
	}

	[MoonSharpHidden]
	public MainCamera GetMainCamera()
	{
		if (this._mainCamera == null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(Resources.Load("Main Camera")) as GameObject;
			gameObject.name = "Main Camera";
			this._mainCamera = gameObject.GetComponent<MainCamera>();
		}
		return this._mainCamera;
	}

	[MoonSharpHidden]
	public string FormatText(string text)
	{
		text = text.Replace("[CHARACTER]", this.GetPlayer().characterName);
		return text;
	}

	[MoonSharpHidden]
	public int CompareItemCategories(BaseItem.ItemCategory a, BaseItem.ItemCategory b)
	{
		return this.itemCategorySortOrder.IndexOf(a).CompareTo(this.itemCategorySortOrder.IndexOf(b));
	}

	[MoonSharpHidden]
	public string GetLoadScreenHint()
	{
		if (this.loadScreenHintIndices.Count == 0)
		{
			for (int i = 0; i < this.loadScreenHints.Count; i++)
			{
				this.loadScreenHintIndices.Add(i);
			}
			Util.FastShuffle<int>(this.loadScreenHintIndices);
		}
		int index = this.loadScreenHintIndices[0];
		this.loadScreenHintIndices.RemoveAt(0);
		return this.loadScreenHints[index];
	}

	[MoonSharpHidden]
	public BaseItem.ItemCategory GetNextItemCategory(BaseItem.ItemCategory current_category)
	{
		if (current_category == BaseItem.ItemCategory.Auto)
		{
			return this.itemCategorySortOrder[0];
		}
		int num = this.itemCategorySortOrder.IndexOf(current_category);
		num++;
		if (num >= this.itemCategorySortOrder.Count)
		{
			return BaseItem.ItemCategory.Auto;
		}
		return this.itemCategorySortOrder[num];
	}

	[MoonSharpHidden]
	public BaseItem.ItemCategory GetPreviousItemCategory(BaseItem.ItemCategory current_category)
	{
		if (current_category == BaseItem.ItemCategory.Auto)
		{
			return this.itemCategorySortOrder[this.itemCategorySortOrder.Count - 1];
		}
		int num = this.itemCategorySortOrder.IndexOf(current_category);
		num--;
		if (num <= 0)
		{
			return BaseItem.ItemCategory.Auto;
		}
		return this.itemCategorySortOrder[num];
	}

	[MoonSharpHidden]
	public string GetKillString(IKillSource victim, IKillSource attacker, WeaponClass weapon_class)
	{
		bool victim_first = false;
		string text = this.defaultKillString;
		if (weapon_class != null)
		{
			text = Util.GetRandom<string>(weapon_class.killStrings, null);
		}
		if (text.IndexOf("[victim") < text.IndexOf("[attacker"))
		{
			victim_first = true;
		}
		text = text.Replace("[victim]", victim.GetKillName(null, false, victim_first));
		text = text.Replace("[victim's]", victim.GetKillName(null, true, victim_first));
		if (attacker != null)
		{
			text = text.Replace("[attacker]", attacker.GetKillName(victim, false, victim_first));
		}
		else
		{
			text = text.Replace("[attacker]", "something");
		}
		if (attacker != null)
		{
			text = text.Replace("[attacker's]", attacker.GetKillName(victim, true, victim_first));
		}
		else
		{
			text = text.Replace("[attacker's]", "something's");
		}
		if (text.Length > 1)
		{
			text = text.ToUpper()[0] + text.Substring(1);
		}
		return text;
	}

	[MoonSharpHidden]
	public CharacterAttribute GetGlobalAttribute(int attribute_index)
	{
		foreach (CharacterAttribute characterAttribute in this.globalAttributes)
		{
			if (characterAttribute.GetAttributeIndex() == attribute_index)
			{
				return characterAttribute;
			}
		}
		return null;
	}

	[MoonSharpHidden]
	public AttributeCap GetAttributeCap(int attribute_index)
	{
		foreach (AttributeCap attributeCap in this.attributeCaps)
		{
			if (attributeCap.GetAttributeIndex() == attribute_index)
			{
				return attributeCap;
			}
		}
		return null;
	}

	[MoonSharpHidden]
	public bool HasKey(string keys)
	{
		if (keys == string.Empty)
		{
			return true;
		}
		foreach (string text in keys.Split(new char[]
		{
			','
		}))
		{
			string text2 = text.Trim();
			bool flag = true;
			if (text2.Length > 0 && text2[0] == '!')
			{
				flag = false;
				text2 = text2.Remove(0, 1);
			}
			if (this.gameKeys.ContainsKey(text2) != flag)
			{
				return false;
			}
		}
		return true;
	}

	[MoonSharpHidden]
	public void SetAchievement(string achievement_name)
	{
		SteamUserStats.SetAchievement(achievement_name);
		SteamUserStats.StoreStats();
	}

	[MoonSharpHidden]
	public void AddKey(string key)
	{
		if (this.HasKey(key))
		{
			return;
		}
		if (key == "started_game")
		{
			this.SetAchievement("WASTED_0");
		}
		if (key == "killed_starr")
		{
			this.SetAchievement("BOSS_0");
		}
		if (key == "killed_tank_boss")
		{
			this.SetAchievement("BOSS_1");
		}
		if (key == "killed_master_y")
		{
			this.SetAchievement("MASTER_Y");
		}
		if (key == "queen_bee_suggested_recruitment")
		{
			this.SetAchievement("BEEZ_0");
		}
		if (key == "queen_bee_suggested_dating")
		{
			this.SetAchievement("BEEZ_1");
		}
		if (key == "queen_bee_suggested_booze")
		{
			this.SetAchievement("BEEZ_2");
		}
		if (key == "end_sided_with_sid")
		{
			this.SetAchievement("END_0");
		}
		if (key == "end_sided_with_sob")
		{
			this.SetAchievement("END_1");
		}
		if (key == "end_sided_with_nobody")
		{
			this.SetAchievement("END_2");
		}
		if (key == "completed_game")
		{
			this.SetAchievement("FINISHED_GAME");
		}
		if (key == "unlocked_computer")
		{
			this.SetAchievement("COMPUTER");
		}
		if (key == "dick_completed_quest")
		{
			this.SetAchievement("DICK");
		}
		this.gameKeys[key] = 1;
		if (this.onKeysChanged != null)
		{
			this.onKeysChanged();
		}
	}

	[MoonSharpHidden]
	public void RemoveKey(string key)
	{
		if (!this.HasKey(key))
		{
			return;
		}
		this.gameKeys.Remove(key);
		if (this.onKeysChanged != null)
		{
			this.onKeysChanged();
		}
	}

	[MoonSharpHidden]
	public void Start()
	{
		if (this != Globals.GetInstance())
		{
			base.gameObject.SetActive(false);
			UnityEngine.Object.DestroyImmediate(base.gameObject);
			return;
		}
	}

	[MoonSharpHidden]
	public static Globals GetInstance()
	{
		if (Globals.isQuitting && !Globals.overrideQuitInstanceLock)
		{
			Debug.LogError("Trying to instantiate Globals while quitting!");
			return null;
		}
		if (Globals._instance == null)
		{
			Globals._instance = UnityEngine.Object.FindObjectOfType<Globals>();
			if (Globals._instance == null)
			{
				Globals._instance = (UnityEngine.Object.Instantiate(Resources.Load("Globals")) as GameObject).GetComponent<Globals>();
			}
			if (Globals._instance != null)
			{
				Globals._instance.Initialize();
			}
		}
		return Globals._instance;
	}

	[MoonSharpHidden]
	public void OnLevelWasLoaded(int level)
	{
		RenderSettings.flareFadeSpeed = 30f;
		this._dungeonSearched = false;
		if (Camera.main != null)
		{
			RadialBlur component = Camera.main.GetComponent<RadialBlur>();
			if (component != null)
			{
				component.blurStrength = 0f;
				HOTween.Kill(component);
			}
		}
		HOTween.Kill(this.fadePanel);
		if (level != SceneManager.GetSceneByName("Ending").buildIndex)
		{
			this.fadePanel.color = Color.clear;
		}
		foreach (PermanenceHandler permanenceHandler in Util.FindAllObjectsOfType<PermanenceHandler>())
		{
			if (!permanenceHandler.permanent)
			{
				UnityEngine.Object.Destroy(permanenceHandler.gameObject);
			}
		}
		this.GetDungeon();
		if (this._dungeon == null)
		{
			if (this.loadingScreen.IsActive())
			{
				this.loadingScreen.Hide();
			}
			if (this._timeScaleEntries != null)
			{
				this._timeScaleEntries.Clear();
				this.UpdateTimeScale();
			}
		}
		foreach (ActivateOnEnable activateOnEnable in Util.FindAllObjectsOfType<ActivateOnEnable>())
		{
			activateOnEnable.gameObject.SetActive(true);
		}
	}

	[MoonSharpHidden]
	public void UpdateConfig()
	{
		Application.targetFrameRate = 120;
		int num = 0;
		int vSyncCount = 0;
		int antiAliasing = 0;
		int width = 1024;
		int height = 768;
		int qualityLevel = 3;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 1;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int pixelLightCount = 0;
		int num9 = 0;
		int anisotropicFiltering = 0;
		this.sensitivity = 4f;
		this.fov = 50f;
		float num10 = 1f;
		float num11 = 1f;
		int num12 = 0;
		int num13 = 0;
		int num14 = 0;
		ConfigManager.GetInstance().GetInt("width", "display", ref width);
		ConfigManager.GetInstance().GetInt("height", "display", ref height);
		ConfigManager.GetInstance().GetInt("shadow_quality", "display", ref qualityLevel);
		ConfigManager.GetInstance().GetInt("fullscreen", "display", ref num);
		ConfigManager.GetInstance().GetInt("vsync", "display", ref vSyncCount);
		ConfigManager.GetInstance().GetInt("pixel_light_count", "display", ref pixelLightCount);
		ConfigManager.GetInstance().GetInt("antialiasing", "display", ref antiAliasing);
		ConfigManager.GetInstance().GetInt("anisotropic_filtering", "display", ref anisotropicFiltering);
		ConfigManager.GetInstance().GetFloat("sensitivity", "input", ref this.sensitivity);
		ConfigManager.GetInstance().GetInt("invert_y", "input", ref num2);
		ConfigManager.GetInstance().GetInt("disable_gamepad", "input", ref num12);
		ConfigManager.GetInstance().GetInt("gamepad_camera_toggle_disabled", "input", ref num13);
		ConfigManager.GetInstance().GetInt("aim_assist", "gameplay", ref num3);
		ConfigManager.GetInstance().GetInt("captions", "gameplay", ref num8);
		ConfigManager.GetInstance().GetInt("view_bob", "gameplay", ref num4);
		ConfigManager.GetInstance().GetInt("iron_sights", "gameplay", ref num5);
		ConfigManager.GetInstance().GetInt("hide_crosshair_mode", "gameplay", ref num6);
		ConfigManager.GetInstance().GetInt("hide_hunter_timer", "gameplay", ref num7);
		ConfigManager.GetInstance().GetInt("crit_frame_hold_disabled", "gameplay", ref num14);
		ConfigManager.GetInstance().GetFloat("fov", "display", ref this.fov);
		ConfigManager.GetInstance().GetInt("active", "debug", ref num9);
		ConfigManager.GetInstance().GetFloat("sound_volume", "audio", ref num10);
		ConfigManager.GetInstance().GetFloat("music_volume", "audio", ref num11);
		this.critFrameHoldDisabled = (num14 == 1);
		this.showCaptions = (num8 == 1);
		this.hideHunterTimer = (num7 == 1);
		this.fullscreenMode = num;
		Screen.SetResolution(width, height, this.fullscreenMode != 0);
		QualitySettings.vSyncCount = vSyncCount;
		QualitySettings.antiAliasing = antiAliasing;
		QualitySettings.SetQualityLevel(qualityLevel);
		QualitySettings.pixelLightCount = pixelLightCount;
		QualitySettings.anisotropicFiltering = (AnisotropicFiltering)anisotropicFiltering;
		QualitySettings.maxQueuedFrames = 0;
		this.isDebug = (num9 == 1);
		this._console.gameObject.SetActive(this.isDebug);
		this.soundVolume = num10;
		this.musicVolume = num11;
		this.invertY = (num2 == 1);
		this.disableGamepad = (num12 == 1);
		this.gamepadCameraToggleDisabled = (num13 == 1);
		this.viewBob = (num4 == 1);
		this.aimAssist = (num3 == 1);
		this.ironSights = (num5 == 1);
		this.hideCrosshairMode = num6;
		float num15 = 0f;
		ConfigManager.GetInstance().GetFloat("brightness", "display", ref num15);
		this.globalCamera.GetComponent<ColorCorrectionCurves>().brightness = num15 * 0.25f;
	}

	[MoonSharpHidden]
	public List<BaseBuff> GetGlobalBuffs()
	{
		return this._globalBuffs;
	}

	[MoonSharpHidden]
	public void AddGlobalBuff(BaseBuff buff)
	{
		if (!this._globalBuffs.Contains(buff))
		{
			this._globalBuffs.Add(buff);
			if (this.onGlobalBuffsChanged != null)
			{
				this.onGlobalBuffsChanged();
			}
		}
	}

	[MoonSharpHidden]
	public void RemoveGlobalBuff(BaseBuff buff)
	{
		if (this._globalBuffs.Contains(buff))
		{
			this._globalBuffs.Remove(buff);
			if (this.onGlobalBuffsChanged != null)
			{
				this.onGlobalBuffsChanged();
			}
		}
	}

	[MoonSharpHidden]
	public void ConsoleLog(string text)
	{
		this._console.ui.AddNewOutputLine(text);
		Debug.Log(text);
	}

	[MoonSharpHidden]
	public void Initialize()
	{
		this._console = base.GetComponentInChildren<ConsoleController>();
		this.ConsoleLog(string.Concat(new object[]
		{
			"WASTED Build ",
			this.buildNumber,
			" ",
			this.buildTime
		}));
		this.ConsoleLog("Running on: " + SystemInfo.graphicsDeviceName);
		this._saveIconGroup = this.saveIcon.GetComponent<CanvasGroup>();
		this._itemLossCallbacks = new Dictionary<BaseItem, List<Func<BaseItem, bool>>>();
		Globals._gameDataPath = string.Empty;
		this._globalBuffs = new List<BaseBuff>();
		this._weatherDurations = new List<double>(1);
		base.gameObject.SetActive(true);
		HOTween.Init(true, true, true);
		this.gameKeys = new Dictionary<string, int>();
		HUD.areaName = string.Empty;
		HUD.areaSubname = string.Empty;
		this.pauseMenu.Initialize();
		this.loadScreenHintIndices.Clear();
		this.popup.Initialize();
		if (Globals._firstInitialization && !SteamAPI.Init())
		{
			Debug.Log("Steam API didn't initialize.");
			Application.Quit();
			return;
		}
		ConfigManager.GetInstance().AddConfigUpdatedCallback(new ConfigManager.Callback(this.UpdateConfig));
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		this._globalSoundSources = new Dictionary<string, AudioSource>();
		Debug.Log("Initializing Globals.");
		TextAsset textAsset = Resources.Load("Text Assets/first_names") as TextAsset;
		this.firstNames = new List<string>(textAsset.text.Split(new char[]
		{
			'\n'
		}));
		textAsset = (Resources.Load("Text Assets/loading_text") as TextAsset);
		this.loadScreenHints = new List<string>(textAsset.text.Split(new char[]
		{
			'\n'
		}));
		textAsset = (Resources.Load("Text Assets/first_names_female") as TextAsset);
		this.firstNamesFemale = new List<string>(textAsset.text.Split(new char[]
		{
			'\n'
		}));
		textAsset = (Resources.Load("Text Assets/last_names") as TextAsset);
		this.lastNames = new List<string>(textAsset.text.Split(new char[]
		{
			'\n'
		}));
		this._listenerContainer = new GameObject("Audio Listener Container");
		this._listenerContainer.transform.parent = base.transform;
		this._listenerContainer.AddComponent<AudioListener>();
		this.OnLevelWasLoaded(Application.loadedLevel);
		ModManager.GetInstance();
		this._timeScaleEntries = new List<Globals.TimeScaleEntry>();
		this.UpdateTimeScale();
		CustomizationManager.GetInstance().Initialize();
		QuestManager.GetInstance().Initialize();
		this.globalContainer.Initialize();
		this.lostItems.Initialize();
		this.courierBox.Initialize();
		this.radio.Initialize();
		this.Reset(false);
		if (this.giveAllItems)
		{
			this.GiveAllItems();
		}
		this.buildInfoDisplay.gameObject.SetActive(false);
		Globals.globalsInitialized = true;
		Globals._firstInitialization = false;
	}

	[MoonSharpHidden]
	public void AddItemCountChangeCallback(BaseItem item, Globals.Callback callback)
	{
		if (!this._itemCountChangeCallbacks.ContainsKey(item.GetPrefab()))
		{
			this._itemCountChangeCallbacks[item.GetPrefab()] = null;
		}
		Dictionary<BaseItem, Globals.Callback> itemCountChangeCallbacks;
		BaseItem prefab;
		(itemCountChangeCallbacks = this._itemCountChangeCallbacks)[prefab = item.GetPrefab()] = (Globals.Callback)Delegate.Combine(itemCountChangeCallbacks[prefab], callback);
	}

	[MoonSharpHidden]
	public void RemoveItemCountChangeCallback(BaseItem item, Globals.Callback callback)
	{
		if (!this._itemCountChangeCallbacks.ContainsKey(item.GetPrefab()))
		{
			return;
		}
		Dictionary<BaseItem, Globals.Callback> itemCountChangeCallbacks;
		BaseItem prefab;
		(itemCountChangeCallbacks = this._itemCountChangeCallbacks)[prefab = item.GetPrefab()] = (Globals.Callback)Delegate.Remove(itemCountChangeCallbacks[prefab], callback);
	}

	[MoonSharpHidden]
	public void OnItemCountChanged(BaseItem item)
	{
		if (this._itemCountChangeCallbacks.ContainsKey(item.GetPrefab()) && this._itemCountChangeCallbacks[item.GetPrefab()] != null)
		{
			this._itemCountChangeCallbacks[item.GetPrefab()]();
		}
	}

	[MoonSharpHidden]
	public void GiveAllItems()
	{
		foreach (BaseItem baseItem in this.GetItems())
		{
			if (baseItem.pooled)
			{
				for (int i = 0; i < 100; i++)
				{
					this.globalContainer.AddItem(baseItem, false);
				}
			}
			else
			{
				this.globalContainer.AddItem(baseItem, false);
			}
		}
	}

	[MoonSharpHidden]
	public void Update()
	{
		SteamAPI.RunCallbacks();
		if (this.skipCutsceneText.alpha > 0f)
		{
			if (!this.cutscene.IsActive())
			{
				this.skipCutsceneText.alpha = 0f;
			}
			else if (!this.GetWorldPaused(false, false))
			{
				this.skipCutsceneText.alpha -= RealTime.deltaTime;
			}
			if (this.skipCutsceneText.alpha < 0f)
			{
				this.skipCutsceneText.alpha = 0f;
			}
		}
		if (this.cutscene != null && this.captions != null)
		{
			if (this.showCaptions || this.cutscene.IsActive())
			{
				if (!this.captions.gameObject.activeSelf)
				{
					this.captions.gameObject.SetActive(true);
				}
			}
			else if (this.captions.gameObject.activeSelf)
			{
				this.captions.gameObject.SetActive(false);
			}
		}
		if (this.isDebug != this._isDebug)
		{
			this._isDebug = this.isDebug;
			if (this._isDebug)
			{
				this.AddKey("debug_mode");
			}
			else
			{
				this.RemoveKey("debug_mode");
			}
		}
		if (this._dungeon == null)
		{
			this.musicSource.mute = true;
		}
		else
		{
			this.musicSource.mute = false;
		}
		Shader.SetGlobalFloat("_RealTime", RealTime.time);
		if (!this._applicationFocused)
		{
			AudioListener.volume = 0f;
		}
		else
		{
			AudioListener.volume = this.globalVolume;
		}
		this.playTime += (double)RealTime.deltaTime;
		if (!this.GetLogicPaused(false, false))
		{
			this._AdvanceTime((double)(Time.deltaTime * this.worldTimeMultiplier));
		}
		if (Input.GetKeyDown(KeyCode.F5))
		{
			this.buildInfoDisplay.gameObject.SetActive(!this.buildInfoDisplay.gameObject.activeSelf);
		}
	}

	[MoonSharpHidden]
	public void AdvanceTime(double duration)
	{
		this._AdvanceTime(duration);
	}

	[MoonSharpHidden]
	protected void _AdvanceTime(double duration)
	{
		this.worldTime += duration;
		float num = (float)duration;
		this._nextWeatherEvent -= (float)duration;
		while (this._nextWeatherEvent <= 0f)
		{
			num += this._nextWeatherEvent;
			SeededRandom seededRandom = new SeededRandom(this._weatherSeed);
			this._weatherTime = 0f;
			this._weatherDuration = UnityEngine.Random.Range(43200f, 129600f);
			this._nextWeatherEvent += this._weatherDuration + UnityEngine.Random.Range(43200f, 259200f);
			this._currentWeather = (Globals.WeatherType)seededRandom.Range(0, 1);
			seededRandom.Reseed();
			this._weatherSeed = seededRandom.seed;
			if (num >= this._weatherDuration)
			{
				this.RecordWeather((double)this._weatherDuration);
				num -= this._weatherDuration;
			}
		}
		if (this._weatherDuration >= 0f && num > 0f)
		{
			float remainingWeatherTime = this.GetRemainingWeatherTime();
			this._weatherTime += Mathf.Min(num, remainingWeatherTime);
			this.RecordWeather((double)num);
			if (this._weatherTime >= this._weatherDuration)
			{
				this._weatherTime = 0f;
				this._weatherDuration = 0f;
				this._currentWeather = Globals.WeatherType.None;
			}
		}
		for (int i = 0; i < this.factions.Count; i++)
		{
			float num2 = this.GetFactionAggro(this._player.GetFaction(), this.factions[i]);
			if (num2 > 0f)
			{
				num2 -= (float)duration / 86400f;
				if (num2 <= 0f)
				{
					num2 = 0f;
					this.PlayGlobalSound(this._dungeon.hud.huntedSound, "important_notification");
					this._dungeon.hud.AddCombatMessage(this.factions[i].factionName + " have forgotten about your crimes.");
				}
				this.SetFactionAggro(this._player.GetFaction(), this.factions[i], num2);
			}
		}
	}

	[MoonSharpHidden]
	public float GetRemainingWeatherTime()
	{
		return Mathf.Max(this._weatherDuration - this._weatherTime, 0f);
	}

	[MoonSharpHidden]
	public float GetWeatherTime()
	{
		return this._weatherTime;
	}

	[MoonSharpHidden]
	public double GetWeatherDuration(Globals.WeatherType weather)
	{
		if (this._currentWeather >= (Globals.WeatherType)this._weatherDurations.Count)
		{
			return 0.0;
		}
		return this._weatherDurations[(int)weather];
	}

	[MoonSharpHidden]
	public void RecordWeather(double duration)
	{
		if (this._currentWeather == Globals.WeatherType.None)
		{
			return;
		}
		while (this._weatherDurations.Count < (int)(this._currentWeather + 1))
		{
			this._weatherDurations.Add(0.0);
		}
		List<double> weatherDurations;
		int currentWeather;
		(weatherDurations = this._weatherDurations)[currentWeather = (int)this._currentWeather] = weatherDurations[currentWeather] + duration;
	}

	[MoonSharpHidden]
	public double GetTimeUntilTimeOfDay(double time_of_day)
	{
		if (time_of_day >= this.GetTimeOfDay())
		{
			return time_of_day - this.GetTimeOfDay();
		}
		return 86400.0 + time_of_day - this.GetTimeOfDay();
	}

	[MoonSharpHidden]
	public Player GetPlayer()
	{
		return this._player;
	}

	[MoonSharpHidden]
	public void GoHome()
	{
		if (this._dungeon != null && this.GetPlayer() != null && this.GetPlayer().IsConscious())
		{
			this.GetPlayer().RemoveAllBuzzes();
			this.GetPlayer().currentHP = this.GetPlayer().GetMaxHP();
		}
		this.level = 1;
		this.LoadLevel("Home Interior", null, true);
	}

	[MoonSharpHidden]
	public float musicFade
	{
		get
		{
			float num = 0f;
			this.musicMixer.GetFloat("Fader", out num);
			num = (num + 80f) / 80f;
			return num;
		}
		set
		{
			this.musicMixer.SetFloat("Fader", Mathf.Lerp(-80f, 0f, value));
		}
	}

	[MoonSharpHidden]
	public float soundFade
	{
		get
		{
			float num = 0f;
			this.worldSoundMixer.GetFloat("WorldVolume", out num);
			num = (num + 80f) / 80f;
			return num;
		}
		set
		{
			this.worldSoundMixer.SetFloat("WorldVolume", Mathf.Lerp(-80f, 0f, value));
		}
	}

	[MoonSharpHidden]
	public void LoadLevel(string level, Globals.Callback callback = null, bool show_loading_screen = true)
	{
		base.StartCoroutine(this._LoadLevel(level, callback, show_loading_screen));
	}

	[MoonSharpHidden]
	protected IEnumerator _LoadLevel(string level, Globals.Callback callback, bool show_loading_screen)
	{
		this.captions.ShowCaptions(string.Empty, 0f);
		this.itemLossIdentifier++;
		if (this.onLoadNewLevel != null)
		{
			this.onLoadNewLevel();
		}
		if (show_loading_screen)
		{
			this.loadingScreen.Show();
		}
		this.GetMainCamera().transform.parent = null;
		this.GetMainCamera().CheckParentChange();
		UnityEngine.Object.DontDestroyOnLoad(this.GetMainCamera());
		List<BaseItem> lost_items = this.GetTemporarilyLostItems();
		if (this.GetDungeon() != null && this.GetPlayer() != null)
		{
			for (int i = 0; i < lost_items.Count; i++)
			{
				BaseItem baseItem = lost_items[i];
				bool flag = false;
				if (this._itemLossCallbacks.ContainsKey(baseItem.GetPrefab()))
				{
					List<Func<BaseItem, bool>> list = new List<Func<BaseItem, bool>>(this._itemLossCallbacks[baseItem.GetPrefab()]);
					foreach (Func<BaseItem, bool> func in list)
					{
						if (func(baseItem))
						{
							flag = true;
						}
					}
				}
				if (flag)
				{
					lost_items.RemoveAt(i);
					i--;
				}
				else
				{
					if (baseItem is BaseEquipment && (baseItem as BaseEquipment).HasMod(Globals.GetInstance().importantMod))
					{
						(baseItem as BaseEquipment).RemoveMod(Globals.GetInstance().importantMod);
					}
					this.lostItems.AddItem(baseItem, false);
				}
			}
			Globals.GetInstance().GetMainCamera().radialBlur.blurStrength = 0f;
			this.GetPlayer().ForceUncrouch();
		}
		this.AddTimeScale("loading", 0f);
		this._dungeonSearched = false;
		this._dungeon = null;
		if (this._bodyStates != null)
		{
			foreach (Rigidbody rigidbody in this._bodyStates.Keys)
			{
				if (!(rigidbody == null))
				{
					this._bodyStates[rigidbody].Restore(rigidbody);
				}
			}
			this._bodyStates.Clear();
			this._bodyStates = null;
		}
		this._dungeonSearched = false;
		yield return new WaitForEndOfFrame();
		AsyncOperation async_load = Application.LoadLevelAsync(level);
		yield return async_load;
		if (callback != null)
		{
			callback();
		}
		this.RemoveTimeScale("loading");
		Dungeon dungeon = UnityEngine.Object.FindObjectOfType<Dungeon>();
		if (dungeon != null)
		{
			Dungeon dungeon4 = dungeon;
			dungeon4.onLevelStart = (Dungeon.Callback)Delegate.Combine(dungeon4.onLevelStart, new Dungeon.Callback(delegate()
			{
				foreach (BaseItem baseItem2 in lost_items)
				{
					dungeon.hud.AddCombatMessage(baseItem2.GetItemName() + " was collected by the Road Couriers.");
				}
			}));
			Dungeon dungeon2 = dungeon;
			dungeon2.onLevelInitialize = (Dungeon.Callback)Delegate.Combine(dungeon2.onLevelInitialize, new Dungeon.Callback(delegate()
			{
				Dungeon dungeon3 = dungeon;
				dungeon3.onLevelStart = (Dungeon.Callback)Delegate.Combine(dungeon3.onLevelStart, new Dungeon.Callback(delegate()
				{
					if (!this.GetPlayer().HasKey("started_courier_run"))
					{
						foreach (BaseItem baseItem2 in this.courierRunBox.GetItems(true))
						{
							int pooledCount = baseItem2.pooledCount;
							for (int j = 0; j < pooledCount; j++)
							{
								this.globalContainer.AddItem(baseItem2, false);
							}
							dungeon.hud.AddCombatMessage(baseItem2.GetItemName() + " from your recent Courier Run was added to your Stash.");
						}
					}
				}));
			}));
		}
		yield break;
	}

	[MoonSharpHidden]
	public void SetDungeon(Dungeon dungeon)
	{
		this._dungeon = dungeon;
		this._dungeonSearched = true;
	}

	[MoonSharpHidden]
	public Dungeon GetDungeon()
	{
		if (!this._dungeonSearched)
		{
			if (this._dungeon == null)
			{
				this._dungeon = UnityEngine.Object.FindObjectOfType<Dungeon>();
			}
			this._dungeonSearched = true;
		}
		return this._dungeon;
	}

	[MoonSharpHidden]
	public bool HasTimeScale(string scale_tag)
	{
		foreach (Globals.TimeScaleEntry timeScaleEntry in this._timeScaleEntries)
		{
			if (timeScaleEntry.name == scale_tag)
			{
				return true;
			}
		}
		return false;
	}

	[MoonSharpHidden]
	public void AddTimeScale(string scale_tag, float scale)
	{
		foreach (Globals.TimeScaleEntry timeScaleEntry in this._timeScaleEntries)
		{
			if (timeScaleEntry.name == scale_tag)
			{
				timeScaleEntry.amount = scale;
				this.UpdateTimeScale();
				return;
			}
		}
		Globals.TimeScaleEntry timeScaleEntry2 = new Globals.TimeScaleEntry();
		timeScaleEntry2.name = scale_tag;
		timeScaleEntry2.amount = scale;
		this._timeScaleEntries.Add(timeScaleEntry2);
		this.UpdateTimeScale();
	}

	[MoonSharpHidden]
	public void RemoveTimeScale(string scale_tag)
	{
		foreach (Globals.TimeScaleEntry timeScaleEntry in this._timeScaleEntries)
		{
			if (timeScaleEntry.name == scale_tag)
			{
				this._timeScaleEntries.Remove(timeScaleEntry);
				this.UpdateTimeScale();
				break;
			}
		}
	}

	[MoonSharpHidden]
	public void FlushBodyStates()
	{
		if (this._bodyStates != null)
		{
			this._bodyStates.Clear();
		}
	}

	[MoonSharpHidden]
	public void UpdateTimeScale()
	{
		float num = 1f;
		foreach (Globals.TimeScaleEntry timeScaleEntry in this._timeScaleEntries)
		{
			num *= timeScaleEntry.amount;
		}
		this._desiredTimeScale = num;
		if (Time.timeScale != this._desiredTimeScale)
		{
			Time.timeScale = this._desiredTimeScale;
			Time.fixedDeltaTime = 0.01f * this._desiredTimeScale;
			if (Time.timeScale < 0.1f)
			{
				this._bodyStates = new Dictionary<Rigidbody, Globals.RigidbodyState>();
				if (this._dungeon != null && !this._dungeon.IsLevelStarted())
				{
					return;
				}
				foreach (Rigidbody rigidbody in UnityEngine.Object.FindObjectsOfType<Rigidbody>())
				{
					Globals.RigidbodyState rigidbodyState = new Globals.RigidbodyState();
					rigidbodyState.Store(rigidbody);
					this._bodyStates[rigidbody] = rigidbodyState;
				}
			}
			else if (this._bodyStates != null)
			{
				foreach (Rigidbody rigidbody2 in this._bodyStates.Keys)
				{
					if (!(rigidbody2 == null))
					{
						this._bodyStates[rigidbody2].Restore(rigidbody2);
					}
				}
				this._bodyStates.Clear();
				this._bodyStates = null;
			}
		}
	}

	[MoonSharpHidden]
	public bool CanSave()
	{
		return !this.IsInfiniteCooler() && !(this._dungeon == null) && !(this.GetPlayer() == null) && this.GetPlayer().IsConscious() && !this.GetLogicPaused(true, false) && this._dungeon.IsSaveableArea();
	}

	[MoonSharpHidden]
	public void SaveGame()
	{
		if (!this.CanSave())
		{
			return;
		}
		this.itemLossIdentifier++;
		if (this.onPreSave != null)
		{
			this.onPreSave();
		}
		Globals.GameSaveData value = new Globals.GameSaveData(this);
		string data = JsonConvert.SerializeObject(value, Formatting.Indented, this.GetSerializerSettings());
		this.SaveData(this.GetSaveFileName(), data, "sav");
		this.saveIconVisibleTime = 3f;
		if (this.onSave != null)
		{
			this.onSave();
		}
	}

	public void AddItemLossCallback(BaseItem item, Func<BaseItem, bool> callback)
	{
		if (!this._itemLossCallbacks.ContainsKey(item.GetPrefab()))
		{
			this._itemLossCallbacks[item.GetPrefab()] = new List<Func<BaseItem, bool>>();
		}
		this._itemLossCallbacks[item.GetPrefab()].Add(callback);
	}

	public void RemoveItemLossCallback(BaseItem item, Func<BaseItem, bool> callback)
	{
		if (this._itemLossCallbacks.ContainsKey(item.GetPrefab()))
		{
			this._itemLossCallbacks[item.GetPrefab()].Remove(callback);
			if (this._itemLossCallbacks[item.GetPrefab()].Count == 0)
			{
				this._itemLossCallbacks.Remove(item.GetPrefab());
			}
		}
	}

	[MoonSharpHidden]
	public List<BaseItem> GetTemporarilyLostItems()
	{
		List<BaseItem> list = new List<BaseItem>();
		if (this.GetDungeon() == null)
		{
			return list;
		}
		foreach (BaseItem baseItem in UnityEngine.Object.FindObjectsOfType<BaseItem>())
		{
			if (baseItem.ShouldTrackIfLost())
			{
				if (baseItem.itemLossIdentifier != this.itemLossIdentifier)
				{
					list.Add(baseItem);
				}
			}
		}
		return list;
	}

	[MoonSharpHidden]
	public double GetWorldTime()
	{
		return this.worldTime;
	}

	[MoonSharpHidden]
	public float GetDesiredTimeScale()
	{
		return this._desiredTimeScale;
	}

	[MoonSharpHidden]
	public double GetTimeOfDay()
	{
		return this.worldTime % 86400.0;
	}

	[MoonSharpHidden]
	public void SetPlayer(Player player)
	{
		if (this._player != player)
		{
			if (this._player != null && this.onPlayerUnset != null)
			{
				this.onPlayerUnset(this._player);
			}
			this._player = player;
			if (this._player != null && this.onPlayerSet != null)
			{
				this.onPlayerSet(this._player);
			}
			foreach (Quest quest in QuestManager.GetInstance().GetInProgressQuests())
			{
				QuestManager.QuestState questState = QuestManager.GetInstance().GetQuestState(quest);
				foreach (QuestManager.QuestConditionState questConditionState in questState.GetConditionStates())
				{
					if (questConditionState.GetQuestCondition().IsPlayerSpecificCondition())
					{
						questConditionState.UpdateQuest();
					}
				}
			}
		}
	}

	[MoonSharpHidden]
	public string GetSaveFileName()
	{
		return "save";
	}

	[MoonSharpHidden]
	public void LearnRecipe(CraftingRecipe recipe)
	{
		HUD hud = this.GetDungeon().hud;
		if (this.learnedRecipes.Contains(recipe))
		{
			this.GetDungeon().hud.AddCombatMessage("You already know how to make " + recipe.GetName());
		}
		else
		{
			this.ShowTutorial("tutorial_recipe_unlocked");
			this.PlayGlobalSound(hud.lootSound, "notification");
			this.GetDungeon().hud.AddCombatMessage("New recipe unlocked: " + recipe.GetName());
			this.learnedRecipes.Add(recipe);
		}
	}

	[MoonSharpHidden]
	public static Faction GetFactionByName(string name)
	{
		GameObject gameObject = Resources.Load("Factions/" + name) as GameObject;
		if (gameObject == null)
		{
			return null;
		}
		return gameObject.GetComponent<Faction>();
	}

	[MoonSharpHidden]
	public bool DataExists(string file, string extension = "sav")
	{
		if (!this.HasFileAccess())
		{
			Debug.Log("Check player prefs.");
			return PlayerPrefs.HasKey(file);
		}
		Debug.Log("Checking files.");
		return Globals.Load(file + "." + extension) != null;
	}

	[MoonSharpHidden]
	public string LoadData(string file, string extension = "sav")
	{
		string result = string.Empty;
		if (!this.HasFileAccess())
		{
			result = PlayerPrefs.GetString(file);
		}
		else
		{
			byte[] array = Globals.Load(file + "." + extension);
			if (array == null)
			{
				return string.Empty;
			}
			result = Encoding.UTF8.GetString(array);
		}
		return result;
	}

	[MoonSharpHidden]
	public void SaveData(string file, string data, string extension = "sav")
	{
		if (!this.HasFileAccess())
		{
			PlayerPrefs.SetString(file, data);
			PlayerPrefs.Save();
		}
		else
		{
			Globals.Save(file + "." + extension, Encoding.UTF8.GetBytes(data));
		}
	}

	[MoonSharpHidden]
	public static byte[] Load(string fileName)
	{
		string path = Path.Combine(Globals._gameDataPath, fileName);
		if (File.Exists(path))
		{
			return File.ReadAllBytes(path);
		}
		return null;
	}

	[MoonSharpHidden]
	public static bool Save(string fileName, byte[] bytes)
	{
		string path = Path.Combine(Globals._gameDataPath, fileName);
		if (bytes == null)
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			return true;
		}
		FileStream fileStream = null;
		try
		{
			fileStream = File.Create(path);
		}
		catch (Exception ex)
		{
			Debug.LogError(ex.Message);
			return false;
		}
		fileStream.Write(bytes, 0, bytes.Length);
		fileStream.Close();
		return true;
	}

	[MoonSharpHidden]
	public bool HasFileAccess()
	{
		return Application.platform != RuntimePlatform.WebGLPlayer;
	}

	[MoonSharpHidden]
	public bool HasSaveData()
	{
		return this.DataExists(this.GetSaveFileName(), "sav");
	}

	[MoonSharpHidden]
	public IEnumerator LoadGame()
	{
		this.loadSuccessful = false;
		if (!this.DataExists(this.GetSaveFileName(), "sav"))
		{
			if (this._dungeon != null)
			{
				this._dungeon.hud.AddCombatMessage("No previous game exists!");
			}
			foreach (Player player in Player.players)
			{
				UnityEngine.Object.Destroy(player.gameObject);
			}
			this._gameSaveData = null;
			yield break;
		}
		this.Reset(false);
		string save_data = this.LoadData(this.GetSaveFileName(), "sav");
		this._gameSaveData = null;
		try
		{
			this._gameSaveData = JsonConvert.DeserializeObject<Globals.GameSaveData>(save_data, this.GetSerializerSettings());
		}
		catch (Exception ex)
		{
			Debug.Log("Load Error 1: " + ex.Message);
			this._gameSaveData = null;
		}
		yield return 0;
		if (this._gameSaveData == null)
		{
			this.PlayGlobalSound(this.errorSound, "notification");
			this.popup.BeginPopup("Corrupt Save Game");
			this.popup.AddWidget<PopupLabel>(string.Empty).SetLabel("Game data has been lost due to file corruption. Please start a new game.");
			this.popup.AddWidget<PopupSpacer>(string.Empty);
			this.popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetLabel("Okay").AddCallback(delegate(PopupButton button)
			{
				this.popup.ClosePopup();
			});
			this.popup.EndPopup();
			foreach (Player player2 in Player.players)
			{
				UnityEngine.Object.Destroy(player2.gameObject);
			}
			this._gameSaveData = null;
			yield break;
		}
		if (this._gameSaveData.saveFormatVersion == -1)
		{
			this.PlayGlobalSound(this.errorSound, "notification");
			this.popup.BeginPopup("Invalid Save Version");
			this.popup.AddWidget<PopupLabel>(string.Empty).SetLabel("Your save game data is incompatible with this version of the game. Please start a new game");
			this.popup.AddWidget<PopupSpacer>(string.Empty);
			this.popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetLabel("Okay").AddCallback(delegate(PopupButton button)
			{
				this.popup.ClosePopup();
			});
			this.popup.EndPopup();
			foreach (Player player3 in Player.players)
			{
				UnityEngine.Object.Destroy(player3.gameObject);
			}
			this._gameSaveData = null;
			yield break;
		}
		if (this._gameSaveData.saveFormatVersion > Globals.SAVE_FORMAT_VERSION)
		{
			this.PlayGlobalSound(this.errorSound, "notification");
			this.popup.BeginPopup("Newer Save Game");
			this.popup.AddWidget<PopupLabel>(string.Empty).SetLabel("The save data is from a newer version of the game. Please update the game and try again.");
			this.popup.AddWidget<PopupSpacer>(string.Empty);
			this.popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetLabel("Okay").AddCallback(delegate(PopupButton button)
			{
				this.popup.ClosePopup();
			});
			this.popup.EndPopup();
			foreach (Player player4 in Player.players)
			{
				UnityEngine.Object.Destroy(player4.gameObject);
			}
			this._gameSaveData = null;
			yield break;
		}
		yield return 0;
		try
		{
			this.isDeserializing = true;
			this._gameSaveData.DeserializeData(this);
		}
		catch (Exception ex2)
		{
			Debug.Log("Load Error 2: " + ex2.Message);
			this.isDeserializing = false;
			this.PlayGlobalSound(this.errorSound, "notification");
			this.popup.BeginPopup("Corrupt Save Game");
			this.popup.AddWidget<PopupLabel>(string.Empty).SetLabel("Game data has been lost due to file corruption. Please start a new game.");
			this.popup.AddWidget<PopupSpacer>(string.Empty);
			this.popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetLabel("Okay").AddCallback(delegate(PopupButton button)
			{
				this.popup.ClosePopup();
			});
			this.popup.EndPopup();
			foreach (Player player5 in Player.players)
			{
				UnityEngine.Object.Destroy(player5.gameObject);
			}
			this._gameSaveData = null;
			yield break;
		}
		this.isDeserializing = false;
		this._isDebug = false;
		this.loadSuccessful = true;
		yield break;
	}

	[MoonSharpHidden]
	public Globals.GameSaveData GetGameSaveDataObject()
	{
		return this._gameSaveData;
	}

	[MoonSharpHidden]
	public void FinalizeLoad()
	{
		this.level = this._gameSaveData.level;
		if (string.IsNullOrEmpty(this._gameSaveData.levelName))
		{
			this.GoHome();
		}
		else
		{
			this.LoadLevel(this._gameSaveData.levelName, new Globals.Callback(this.ConditionAddHack), true);
		}
		this._gameSaveData = null;
	}

	[MoonSharpHidden]
	public void ConditionAddHack()
	{
		bool flag = this.isDeserializing;
		this.isDeserializing = true;
		if (this._player != null)
		{
			foreach (Condition condition in this._player.GetConditions())
			{
				if (condition != null && condition.onAdded != null)
				{
					condition.onAdded(condition);
				}
			}
		}
		this.isDeserializing = flag;
	}

	[MoonSharpHidden]
	public void PushCursorUnlock()
	{
		this.cursorUnlocks++;
		this._refreshCursorUnlock = true;
	}

	[MoonSharpHidden]
	public void PopCursorUnlock()
	{
		this.cursorUnlocks--;
		this._refreshCursorUnlock = true;
	}

	[MoonSharpHidden]
	public void UpdateCursorUnlock()
	{
		if (GameInput.IsJoystick())
		{
			Cursor.visible = false;
			Cursor.lockState = CursorLockMode.Locked;
			return;
		}
		if (this.cursorUnlocks > 0)
		{
			Cursor.visible = true;
			if (Application.isEditor)
			{
				Cursor.lockState = CursorLockMode.None;
			}
			else
			{
				Cursor.lockState = CursorLockMode.Confined;
			}
		}
		else
		{
			Cursor.visible = false;
			Cursor.lockState = CursorLockMode.Locked;
		}
	}

	[MoonSharpHidden]
	public void ResetCursorUnlock()
	{
		this.cursorUnlocks = 0;
		this.UpdateCursorUnlock();
	}

	[MoonSharpHidden]
	public void ShowPopup(TextAsset text_asset, Globals.Callback callback = null)
	{
		if (text_asset == null)
		{
			return;
		}
		string text = text_asset.text;
		string popup_title = text.Substring(0, text.IndexOf("\n"));
		text = text.Substring(text.IndexOf("\n") + 1);
		Globals.GetInstance().PlayGlobalSound(Globals.GetInstance().noticeSound, "tutorial");
		PopupManager popup = Globals.GetInstance().popup;
		popup.BeginPopup(popup_title);
		popup.AddWidget<PopupLabel>(string.Empty).SetLabel(text);
		popup.AddWidget<PopupSpacer>(string.Empty);
		popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetHotkey("Accept").SetLabel("Okay").AddCallback(delegate(PopupButton button)
		{
			popup.ClosePopup();
			if (callback != null)
			{
				callback();
			}
		});
		popup.EndPopup();
	}

	[MoonSharpHidden]
	public PopupManager ShowTutorial(string tutorial_key)
	{
		if (Globals.GetInstance().IsInfiniteCooler())
		{
			tutorial_key = "cr_" + tutorial_key;
		}
		if (Globals.GetInstance().HasKey(tutorial_key))
		{
			return null;
		}
		if (this.ignoreTutorials)
		{
			return null;
		}
		TextAsset textAsset = Resources.Load("Tutorials/" + tutorial_key) as TextAsset;
		if (textAsset == null)
		{
			return null;
		}
		Globals.GetInstance().AddKey(tutorial_key);
		string text = textAsset.text;
		this._tutorialTitle = text.Substring(0, text.IndexOf("\n"));
		text = text.Substring(text.IndexOf("\n") + 1);
		this._tutorialText = new List<string>(text.Split(new char[]
		{
			'|'
		}));
		Globals.GetInstance().PlayGlobalSound(Globals.GetInstance().noticeSound, "tutorial");
		PopupManager result = Globals.GetInstance().popup;
		this.PopTutorialText();
		base.StartCoroutine(this._ForceTutorialDelay());
		return result;
	}

	[MoonSharpHidden]
	protected IEnumerator _ForceTutorialDelay()
	{
		PopupManager popup = Globals.GetInstance().popup;
		PopupButton old_button = popup.GetWidgetByName("confirm_button") as PopupButton;
		if (popup.IsActive() && old_button != null)
		{
			popup.RemoveWidget(old_button);
			PopupLabel label = popup.AddWidget<PopupLabel>(string.Empty).SetLabel("<i>Please wait...</i>").SetAlignment(TextAnchor.UpperRight);
			popup.EndPopup();
			float time_left = 1f;
			while (time_left > 0f)
			{
				time_left -= RealTime.deltaTime;
				yield return 0;
			}
			popup.RemoveWidget(label);
			popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetHotkey("Accept").SetLabel("Okay").AddCallback(delegate(PopupButton button)
			{
				this.PopTutorialText();
			}).SetName("confirm_button");
			popup.EndPopup();
		}
		yield break;
	}

	[MoonSharpHidden]
	public void PopTutorialText()
	{
		PopupManager popupManager = Globals.GetInstance().popup;
		if (this._tutorialText.Count > 0)
		{
			string text = this._tutorialText[0];
			this._tutorialText.RemoveAt(0);
			text = text.Trim();
			popupManager.BeginPopup(this._tutorialTitle);
			popupManager.AddWidget<PopupLabel>(string.Empty).SetLabel(text);
			popupManager.AddWidget<PopupSpacer>(string.Empty);
			popupManager.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetHotkey("Accept").SetLabel("Okay").AddCallback(delegate(PopupButton button)
			{
				this.PopTutorialText();
			}).SetName("confirm_button");
			popupManager.EndPopup();
		}
		else
		{
			popupManager.ClosePopup();
		}
	}

	[MoonSharpHidden]
	public void QuitGame()
	{
		if (this.pauseMenu.IsActive())
		{
			this.pauseMenu.HidePauseMenu();
		}
		if (this.popup.IsActive())
		{
			this.popup.ClosePopup();
		}
		if (this.CanSave())
		{
			Globals.GetInstance().PlayGlobalSound(Globals.GetInstance().errorSound, "notification");
			this.popup.BeginPopup("Quit?");
			this.popup.AddWidget<PopupLabel>(string.Empty).SetLabel("Are you sure you want to quit the game?\n\nYour game will automatically be saved.");
			this.popup.AddWidget<PopupSpacer>(string.Empty);
			this.popup.AddWidget<PopupButton>(string.Empty).SetLabel("Yes").SetHotkey("Accept").AddCallback(delegate(PopupButton button)
			{
				this.popup.ClosePopup();
				this.ConfirmQuit();
			});
			PopupWidget popupWidget = this.popup.AddWidget<PopupButton>(string.Empty).SetLabel("No").SetHotkey("Cancel").AddCallback(delegate(PopupButton button)
			{
				this.popup.ClosePopup();
			});
			this.popup.EndPopup();
			if (GameInput.IsJoystick())
			{
				this.popup.SelectItem(popupWidget as SelectablePopupWidget);
			}
		}
		else
		{
			Globals.GetInstance().PlayGlobalSound(Globals.GetInstance().errorSound, "notification");
			this.popup.BeginPopup("Quit?");
			this.popup.AddWidget<PopupLabel>(string.Empty).SetLabel("Are you sure you want to quit the game?\n\nAny unsaved progress will be lost!");
			this.popup.AddWidget<PopupSpacer>(string.Empty);
			this.popup.AddWidget<PopupButton>(string.Empty).SetLabel("Yes").SetHotkey("Accept").AddCallback(delegate(PopupButton button)
			{
				this.popup.ClosePopup();
				this.ConfirmQuit();
			});
			PopupWidget popupWidget2 = this.popup.AddWidget<PopupButton>(string.Empty).SetLabel("No").SetHotkey("Cancel").AddCallback(delegate(PopupButton button)
			{
				this.popup.ClosePopup();
			});
			this.popup.EndPopup();
			if (GameInput.IsJoystick())
			{
				this.popup.SelectItem(popupWidget2 as SelectablePopupWidget);
			}
		}
	}

	[MoonSharpHidden]
	public void ConfirmQuit()
	{
		if (this.CanSave())
		{
			this.SaveGame();
		}
		if (this.cutscene.IsActive())
		{
			this.cutscene.Stop();
		}
		Globals.GetInstance().LoadLevel("Main Menu", delegate
		{
			this.Reset(false);
		}, true);
	}

	[MoonSharpHidden]
	public float GetFOV()
	{
		return this.fov;
	}

	[MoonSharpHidden]
	public AudioSource PlayGlobalSound(AudioClip clip, string channel = "")
	{
		AudioSource audioSource;
		if (!this._globalSoundSources.ContainsKey(channel))
		{
			audioSource = UnityEngine.Object.Instantiate<AudioSource>(this.globalSoundSourceTemplate);
			audioSource.transform.parent = this.globalSoundSourceTemplate.transform.parent;
			audioSource.gameObject.name = this.globalSoundSourceTemplate.gameObject.name + " (" + channel + ")";
			this._globalSoundSources[channel] = audioSource;
			audioSource.outputAudioMixerGroup = this.masterMixer.FindMatchingGroups("Sound Volume")[0];
		}
		else
		{
			audioSource = this._globalSoundSources[channel];
		}
		if (clip == null)
		{
			audioSource.Stop();
			return audioSource;
		}
		audioSource.ignoreListenerPause = true;
		audioSource.spatialBlend = 0f;
		audioSource.dopplerLevel = 0f;
		audioSource.clip = clip;
		audioSource.Play();
		return audioSource;
	}

	[MoonSharpHidden]
	public void OnEnable()
	{
		GameInput.RegisterInputChangeCallback(new GameInput.Callback(this.UpdateCursorUnlock));
	}

	[MoonSharpHidden]
	public void GrantStartingRecipes()
	{
		Debug.Log("Granting starting recipes.");
		foreach (CraftingRecipe item in this.startingRecipes)
		{
			if (!this.learnedRecipes.Contains(item))
			{
				this.learnedRecipes.Add(item);
			}
		}
	}

	[MoonSharpHidden]
	public void OnDisable()
	{
		GameInput.UnregisterInputChangeCallback(new GameInput.Callback(this.UpdateCursorUnlock));
		Time.timeScale = 1f;
		Time.fixedDeltaTime = 0.01f;
	}

	[MoonSharpHidden]
	public void OnDestroy()
	{
		if (!Globals.isQuitting)
		{
			ConfigManager.GetInstance().RemoveConfigUpdatedCallback(new ConfigManager.Callback(this.UpdateConfig));
		}
	}

	[MoonSharpHidden]
	public string GetGameDataPath()
	{
		return Globals._gameDataPath;
	}

	[MoonSharpHidden]
	public void LateUpdate()
	{
		if (this.saveIconVisibleTime > 0f)
		{
			this.saveIconVisibleTime -= RealTime.deltaTime;
		}
		if (this.saveIconVisibleTime > 0f && !this.saveIcon.activeSelf)
		{
			this.saveIcon.SetActive(true);
		}
		else if (this.saveIconVisibleTime <= 0f && this.saveIcon.activeSelf)
		{
			this.saveIcon.SetActive(false);
		}
		this._saveIconGroup.alpha = Mathf.Clamp01(this.saveIconVisibleTime);
		(this.saveIcon.transform as RectTransform).anchoredPosition = new Vector2(Mathf.Sin(RealTime.time * 6f) * 8f, Mathf.Cos(RealTime.time * 3f) * 8f);
		if (this._refreshCursorUnlock)
		{
			this._refreshCursorUnlock = false;
			this.UpdateCursorUnlock();
		}
		if (this.listenerTarget != null)
		{
			this._listenerContainer.transform.rotation = this.listenerTarget.transform.rotation;
			this._listenerContainer.transform.position = this.listenerTarget.transform.position;
		}
		else if (this.GetMainCamera() != null)
		{
			this._listenerContainer.transform.rotation = this._mainCamera.transform.rotation;
			this._listenerContainer.transform.position = this._mainCamera.transform.position;
		}
		if (!this._applicationFocused)
		{
			AudioListener.pause = true;
		}
		else if (this.GetDungeon() != null && this.GetWorldPaused(false, false))
		{
			AudioListener.pause = true;
		}
		else
		{
			AudioListener.pause = false;
		}
		this.masterMixer.SetFloat("Sound Volume", Mathf.Lerp(-80f, 0f, Mathf.Sqrt(this.soundVolume)));
		float target = 1f;
		if (this.GetLogicPaused(false, true))
		{
			target = 0.75f;
		}
		if (Application.loadedLevelName == "Intro")
		{
			target = 0.75f;
		}
		this.currentMusicVolume = Mathf.MoveTowards(this.currentMusicVolume, target, RealTime.deltaTime / 0.25f);
		this.masterMixer.SetFloat("Music Volume", Mathf.Lerp(-80f, 0f, Mathf.Sqrt(this.musicVolume * this.currentMusicVolume)));
		this.worldSoundMixer.SetFloat("Radio Volume", Mathf.Lerp(-80f, -5f, Mathf.Sqrt(this.currentMusicVolume)));
		if ((float)Screen.width != this._screenResolution.x || (float)Screen.height != this._screenResolution.y)
		{
			this._screenResolution.x = (float)Screen.width;
			this._screenResolution.y = (float)Screen.height;
			if (this.onResolutionChanged != null)
			{
				this.onResolutionChanged();
			}
		}
		if (this._freezeDuration > 0f)
		{
			if (!this.HasTimeScale("freeze_frame"))
			{
				this.AddTimeScale("freeze_frame", 0f);
			}
			this._freezeDuration -= RealTime.deltaTime;
			if (this._freezeDuration <= 0f)
			{
				this.RemoveTimeScale("freeze_frame");
				this._freezeDuration = 0f;
			}
		}
	}

	[MoonSharpHidden]
	public float GetFactionAggro(Faction faction_a, Faction faction_b)
	{
		if (faction_a == faction_b)
		{
			return 0f;
		}
		float result = 0f;
		if (faction_a.factionIndex < faction_b.factionIndex)
		{
			if (faction_a.factionAggro == null)
			{
				return 0f;
			}
			if (!faction_a.factionAggro.TryGetValue(faction_b, out result))
			{
				return 0f;
			}
		}
		else
		{
			if (faction_b.factionAggro == null)
			{
				return 0f;
			}
			if (!faction_b.factionAggro.TryGetValue(faction_a, out result))
			{
				return 0f;
			}
		}
		return result;
	}

	[MoonSharpHidden]
	public void ModifyFactionAggro(Faction faction_a, Faction faction_b, float amount, bool modify_friends = true)
	{
		if (faction_a == faction_b)
		{
			return;
		}
		float num = 0f;
		if (faction_a.factionIndex < faction_b.factionIndex)
		{
			if (faction_a.factionAggro == null)
			{
				faction_a.factionAggro = new Dictionary<Faction, float>();
			}
			if (!faction_a.factionAggro.TryGetValue(faction_b, out num))
			{
				num = 0f;
			}
			faction_a.factionAggro[faction_b] = Mathf.Clamp(num + amount, 0f, 1f);
		}
		else
		{
			if (faction_b.factionAggro == null)
			{
				faction_b.factionAggro = new Dictionary<Faction, float>();
			}
			if (!faction_b.factionAggro.TryGetValue(faction_a, out num))
			{
				num = 0f;
			}
			faction_b.factionAggro[faction_a] = Mathf.Clamp(num + amount, 0f, 1f);
		}
		if (modify_friends)
		{
			foreach (Faction faction_a2 in faction_a.friends)
			{
				this.ModifyFactionAggro(faction_a2, faction_b, amount, false);
			}
		}
	}

	[MoonSharpHidden]
	public void StartTopic(GameObject target, Topic topic, BaseCharacter initiator)
	{
		if (target != null && target.GetComponent<TopicActor>() != null)
		{
			target.GetComponent<TopicActor>().StartTopic(topic, initiator);
		}
		else
		{
			this.GetDungeon().scriptedSequenceTopicHandler.SetTopic(topic, null, initiator, 0);
		}
	}

	[MoonSharpHidden]
	public void SetFactionAggro(Faction faction_a, Faction faction_b, float value)
	{
		if (faction_a.factionIndex < faction_b.factionIndex)
		{
			if (faction_a.factionAggro == null)
			{
				faction_a.factionAggro = new Dictionary<Faction, float>();
			}
			faction_a.factionAggro[faction_b] = value;
		}
		else
		{
			if (faction_b.factionAggro == null)
			{
				faction_b.factionAggro = new Dictionary<Faction, float>();
			}
			faction_b.factionAggro[faction_a] = value;
		}
	}

	[MoonSharpHidden]
	public AudioListener GetAudioListener()
	{
		return this._listenerContainer.GetComponent<AudioListener>();
	}

	[MoonSharpHidden]
	public void OnApplicationQuit()
	{
		Globals.isQuitting = true;
		SteamAPI.Shutdown();
	}

	[MoonSharpHidden]
	public bool GetWorldPaused(bool ignore_pause = false, bool ignore_menu = false)
	{
		return !this._applicationFocused || this._console.ui.isConsoleOpen || PhotoMode.IsActive() || this._dungeon == null || this.modList.IsActive() || this.popup.IsActive() || this.leaderboards.IsActive() || (this.GetDungeon() != null && !this.GetDungeon().IsLevelInitialized()) || (this.GetDungeon() != null && this.GetDungeon().ingameMenu.IsActive() && !ignore_menu) || (this.GetDungeon().computerTopicHandler != null && this.GetDungeon().computerTopicHandler.IsActive()) || (!ignore_pause && this.pauseMenu.IsActive()) || this.loadingScreen.IsActive() || (this.GetDungeon().characterCreator != null && this.GetDungeon().characterCreator.IsActive());
	}

	[MoonSharpHidden]
	public bool GetLogicPaused(bool ignore_pause = false, bool ignore_cutscene = false)
	{
		return this.GetWorldPaused(ignore_pause, false) || !this.GetDungeon().IsLevelStarted() || (this.cutscene.IsActive() && !ignore_cutscene) || (this.GetDungeon().conversationTopicHandler != null && this.GetDungeon().conversationTopicHandler.IsActive()) || (this.GetDungeon().scriptedSequenceTopicHandler != null && this.GetDungeon().scriptedSequenceTopicHandler.IsActive());
	}

	[MoonSharpHidden]
	public JsonSerializerSettings GetSerializerSettings()
	{
		if (this._serializerSettings == null)
		{
			this._serializerSettings = new JsonSerializerSettings
			{
				TypeNameHandling = TypeNameHandling.Auto
			};
		}
		return this._serializerSettings;
	}

	[MoonSharpHidden]
	public void Reset(bool new_game_plus = false)
	{
		this.isResetting = true;
		InfiniteLevelType.spawnFactionModifiers = null;
		UnityEngine.Random.seed = ((int)DateTime.Now.Ticks & 65535);
		this._dungeonLayoutRandom = new SeededRandom();
		this._seededRandom = new SeededRandom();
		this.isGeneratingLevel = false;
		this.score = 0;
		this.level = 1;
		if (!new_game_plus)
		{
			this.gameMode = 0;
			this.deaths = 0;
			this.kills = 0;
		}
		this._isInfiniteCooler = false;
		Dungeon.ResetRoomCounts();
		Dungeon.isInControlledScene = false;
		this.listenerTarget = null;
		if (this._mainCamera != null)
		{
			this._mainCamera.transform.parent = null;
		}
		this._globalBuffs.Clear();
		this.UpdateCourierSlotLimit();
		if (!new_game_plus)
		{
			this.globalContainer.Clear();
		}
		this.courierBox.Clear();
		this.lostItems.Clear();
		if (!new_game_plus)
		{
			this.globalContainer.ResetContents();
		}
		this.courierBox.ResetContents();
		this.lostItems.ResetContents();
		this._itemCountChangeCallbacks = new Dictionary<BaseItem, Globals.Callback>();
		this.ResetFactionAggro();
		this.areaSaveData = new Dictionary<string, AreaSaveData>();
		this.saveData = new Dictionary<string, SaveableData>();
		QuestManager.GetInstance().Reset();
		this.gameKeys = new Dictionary<string, int>();
		if (!new_game_plus)
		{
			CustomizationManager.GetInstance().unlockedCustomizations = new Dictionary<CharacterCustomization, bool>();
		}
		if (this.popup.IsActive())
		{
			this.popup.ClosePopup();
		}
		if (this.pauseMenu.IsActive())
		{
			this.pauseMenu.HidePauseMenu();
		}
		if (!new_game_plus)
		{
			if (Player._players != null)
			{
				List<Player> list = new List<Player>(Player._players);
				foreach (Player player in list)
				{
					UnityEngine.Object.Destroy(Player._players[0].gameObject);
				}
				Player._players.Clear();
			}
			this.learnedRecipes = new List<CraftingRecipe>();
			this.GrantStartingRecipes();
			this._player = null;
		}
		this._dungeon = null;
		this._dungeonSearched = false;
		Globals component = (Resources.Load("Globals") as GameObject).GetComponent<Globals>();
		this.lastCharacterName = component.lastCharacterName;
		if (!new_game_plus)
		{
			this.playTime = component.playTime;
		}
		this.worldTime = component.worldTime;
		this._isDebug = false;
		this.loadSuccessful = false;
		HUD.areaName = string.Empty;
		HUD.areaSubname = string.Empty;
		this._nextWeatherEvent = 172800f;
		this._weatherDuration = 0f;
		this._weatherTime = 0f;
		this._weatherSeed = 0;
		this._currentWeather = Globals.WeatherType.None;
		this._weatherDurations = new List<double>(1);
		this.isResetting = false;
	}

	[MoonSharpHidden]
	public Globals.WeatherType GetCurrentWeatherType()
	{
		return this._currentWeather;
	}

	[MoonSharpHidden]
	public void ResetFactionAggro()
	{
		for (int i = 0; i < this.factions.Count; i++)
		{
			Faction faction = this.factions[i];
			faction.factionAggro = new Dictionary<Faction, float>();
			faction.factionIndex = i;
		}
	}

	[MoonSharpHidden]
	public string GetWorldDateString()
	{
		int num = (int)Math.Floor(Globals.GetInstance().GetWorldTime() % 604800.0 / 86400.0);
		double timeOfDay = Globals.GetInstance().GetTimeOfDay();
		int num2 = (int)Math.Floor(timeOfDay / 3600.0);
		int num3 = (int)Math.Floor(timeOfDay % 3600.0 / 60.0);
		if (this._worldTimeDay != num || this._worldTimeHour != num2 || this._worldTimeMinute != num3)
		{
			string text = string.Empty;
			this._worldTimeDay = num;
			this._worldTimeHour = num2;
			this._worldTimeMinute = num3;
			switch (num)
			{
			case 0:
				text = "Mon";
				break;
			case 1:
				text = "Tue";
				break;
			case 2:
				text = "Wed";
				break;
			case 3:
				text = "Thu";
				break;
			case 4:
				text = "Fri";
				break;
			case 5:
				text = "Sat";
				break;
			case 6:
				text = "Sun";
				break;
			}
			this._worldTimeString = string.Concat(new string[]
			{
				text,
				" ",
				((num2 % 12 != 0) ? (num2 % 12) : 12).ToString().PadLeft(2, '0'),
				":",
				num3.ToString().PadLeft(2, '0'),
				" ",
				(num2 < 12) ? "AM" : "PM"
			});
		}
		return this._worldTimeString;
	}

	[MoonSharpHidden]
	public Sprite GetItemIcon(BaseItem item)
	{
		foreach (ItemIcon itemIcon in this.itemIcons)
		{
			if (itemIcon.category == item.GetItemCategory())
			{
				return itemIcon.sprite;
			}
		}
		return this.itemIcons[0].sprite;
	}

	[MoonSharpHidden]
	public Container GetSpecialContainer(Globals.SpecialContainers container_type)
	{
		if (container_type == Globals.SpecialContainers.Courier)
		{
			return this.courierBox;
		}
		if (container_type == Globals.SpecialContainers.Global)
		{
			return this.globalContainer;
		}
		if (container_type == Globals.SpecialContainers.LostAndFound)
		{
			return this.lostItems;
		}
		return null;
	}

	[MoonSharpHidden]
	public AreaSaveData GetAreaSaveData(string area_name)
	{
		AreaSaveData areaSaveData;
		if (!this.areaSaveData.ContainsKey(area_name))
		{
			areaSaveData = new AreaSaveData();
			this.areaSaveData[area_name] = areaSaveData;
		}
		else
		{
			areaSaveData = this.areaSaveData[area_name];
		}
		return areaSaveData;
	}

	[MoonSharpHidden]
	public static int SAVE_FORMAT_VERSION = 2;

	[MoonSharpHidden]
	public static bool overrideQuitInstanceLock;

	[MoonSharpHidden]
	public bool giveAllItems;

	[MoonSharpHidden]
	public bool infiniteAmmo;

	[MoonSharpHidden]
	public bool ignoreTutorials;

	[MoonSharpHidden]
	public bool noMonsters;

	[MoonSharpHidden]
	public bool noHunter;

	[MoonSharpHidden]
	public bool isDebug;

	[MoonSharpHidden]
	public bool showCaptions = true;

	[MoonSharpHidden]
	public bool critFrameHoldDisabled;

	[MoonSharpHidden]
	protected bool _isDebug;

	[MoonSharpHidden]
	public bool gamepadCameraToggleDisabled;

	[MoonSharpHidden]
	public bool hideHunterTimer;

	[MoonSharpHidden]
	public GameObject saveIcon;

	[MoonSharpHidden]
	protected CanvasGroup _saveIconGroup;

	[MoonSharpHidden]
	public float saveIconVisibleTime;

	[MoonSharpHidden]
	public int fullscreenMode = 1;

	[MoonSharpHidden]
	public Dictionary<string, SaveableData> saveData;

	[MoonSharpHidden]
	public Dictionary<string, AreaSaveData> areaSaveData;

	[MoonSharpHidden]
	public bool skipHomeCutscenes;

	[MoonSharpHidden]
	public int buildNumber;

	[MoonSharpHidden]
	public string buildTime = string.Empty;

	[MoonSharpHidden]
	public Shader weaponShader;

	[MoonSharpHidden]
	public Image fadePanel;

	[MoonSharpHidden]
	protected static Globals _instance;

	[MoonSharpHidden]
	public AudioSource musicSource;

	[MoonSharpHidden]
	public AudioSource globalSoundSourceTemplate;

	[MoonSharpHidden]
	protected Dictionary<string, AudioSource> _globalSoundSources;

	[MoonSharpHidden]
	public float globalVolume = 1f;

	[MoonSharpHidden]
	public CutsceneManager cutscene;

	[MoonSharpHidden]
	protected Dungeon _dungeon;

	[MoonSharpHidden]
	public int level = 1;

	[MoonSharpHidden]
	public int deaths;

	[MoonSharpHidden]
	public int kills;

	[MoonSharpHidden]
	public Container globalContainer;

	[MoonSharpHidden]
	public Container courierBox;

	[MoonSharpHidden]
	public Container courierRunBox;

	[MoonSharpHidden]
	protected List<Globals.TimeScaleEntry> _timeScaleEntries;

	public List<BaseBuff> gameBuffs;

	public List<BaseItem> items;

	[MoonSharpHidden]
	protected List<BaseItem> _vanillaItems;

	public List<Faction> factions;

	public List<CraftingRecipe> recipes;

	[MoonSharpHidden]
	public BuildInfoDisplay buildInfoDisplay;

	[MoonSharpHidden]
	public Texture rampTexture;

	public string defaultKillString;

	public Color enemyColor = Color.red;

	public Color friendlyColor = Color.grey;

	public Color neutralColor = Color.grey;

	[MoonSharpHidden]
	public List<GameResolution> gameResolutions;

	[MoonSharpHidden]
	public Globals.Callback onPreSave;

	[MoonSharpHidden]
	public Globals.Callback onSave;

	[MoonSharpHidden]
	public Globals.Callback onLoadNewLevel;

	[MoonSharpHidden]
	public Globals.Callback onGlobalBuffsChanged;

	[MoonSharpHidden]
	public Globals.Callback onKeysChanged;

	[MoonSharpHidden]
	public Dictionary<BaseItem, List<Func<BaseItem, bool>>> _itemLossCallbacks;

	[MoonSharpHidden]
	public Globals.Callback onDungeonLevelStarted;

	[MoonSharpHidden]
	protected Dictionary<BaseItem, Globals.Callback> _itemCountChangeCallbacks;

	[MoonSharpHidden]
	protected Player _player;

	[MoonSharpHidden]
	public static bool globalsInitialized;

	[MoonSharpHidden]
	private static bool _firstInitialization = true;

	public List<string> loadScreenHints;

	public List<int> loadScreenHintIndices;

	[MoonSharpHidden]
	protected bool _dungeonSearched;

	[MoonSharpHidden]
	public bool loadSuccessful;

	[MoonSharpHidden]
	public Container lostItems;

	[MoonSharpHidden]
	public int itemLossIdentifier;

	[MoonSharpHidden]
	public int gameMode;

	public List<CraftingRecipe> startingRecipes;

	public List<CraftingRecipe> learnedRecipes;

	[MoonSharpHidden]
	protected float _desiredTimeScale;

	[MoonSharpHidden]
	public const int IgnoreRaycastLayer = 2;

	[MoonSharpHidden]
	public int cursorUnlocks;

	[MoonSharpHidden]
	protected bool _refreshCursorUnlock;

	[MoonSharpHidden]
	protected Dictionary<Rigidbody, Globals.RigidbodyState> _bodyStates;

	public List<string> firstNames;

	public List<string> firstNamesFemale;

	public List<string> lastNames;

	[MoonSharpHidden]
	public LoadingScreen loadingScreen;

	public BaseItem lockpickItem;

	[MoonSharpHidden]
	public Dictionary<string, int> gameKeys;

	[MoonSharpHidden]
	public Camera globalCamera;

	public Radio radio;

	[MoonSharpHidden]
	public float sensitivity = 4f;

	[MoonSharpHidden]
	public float fov = 50f;

	[MoonSharpHidden]
	public bool invertY;

	[MoonSharpHidden]
	public bool disableGamepad;

	[MoonSharpHidden]
	public bool viewBob = true;

	[MoonSharpHidden]
	public bool ironSights = true;

	[MoonSharpHidden]
	public int hideCrosshairMode;

	[MoonSharpHidden]
	public bool aimAssist = true;

	[MoonSharpHidden]
	public GameObject listenerTarget;

	[MoonSharpHidden]
	protected GameObject _listenerContainer;

	[MoonSharpHidden]
	public float joystickSensitivityMultiplier = 60f;

	[MoonSharpHidden]
	public static bool isQuitting;

	[MoonSharpHidden]
	public EquipmentModification importantMod;

	public BaseItem currency;

	public List<AttributeCap> attributeCaps;

	[MoonSharpHidden]
	public LayerMask lightCullMask;

	[MoonSharpHidden]
	public LayerMask bulletLayerMask;

	[MoonSharpHidden]
	public LayerMask sightLayerMask;

	[MoonSharpHidden]
	public LayerMask orbitLayerMask;

	[MoonSharpHidden]
	public LayerMask interactLayerMask;

	[MoonSharpHidden]
	public LayerMask occlusionLayerMask;

	[MoonSharpHidden]
	public LayerMask soundLayerMask;

	[MoonSharpHidden]
	public LayerMask entitiesLayerMask;

	[MoonSharpHidden]
	public MusicToggle musicToggle;

	[MoonSharpHidden]
	public ExpressionPrefab moveSpeedExpression;

	[MoonSharpHidden]
	public ExpressionPrefab footstepRadiusExpression;

	[MoonSharpHidden]
	public ExpressionPrefab appliedForceMultiplierExpression;

	[MoonSharpHidden]
	public ExpressionPrefab fireRateExpression;

	[MoonSharpHidden]
	public ExpressionPrefab burstCountExpression;

	[MoonSharpHidden]
	public ExpressionPrefab shotRateExpression;

	[MoonSharpHidden]
	public ExpressionPrefab damageReducedExpression;

	[MoonSharpHidden]
	public ExpressionPrefab damageReceivedExpression;

	[MoonSharpHidden]
	public ExpressionPrefab damageDealtExpression;

	[MoonSharpHidden]
	public ExpressionPrefab healingReceivedExpression;

	[MoonSharpHidden]
	public ExpressionPrefab criticalDamageExpression;

	[MoonSharpHidden]
	public ExpressionPrefab weaponSpreadExpression;

	[MoonSharpHidden]
	public ExpressionField shotCountExpression;

	[MoonSharpHidden]
	public ExpressionField timeUntilHuntedExpression;

	[MoonSharpHidden]
	public double playTime;

	[MoonSharpHidden]
	public double worldTime;

	[MoonSharpHidden]
	public float worldTimeMultiplier = 60f;

	[MoonSharpHidden]
	public List<CharacterCustomization> customizationOverrides;

	public List<CharacterAttribute> globalAttributes;

	[MoonSharpHidden]
	public AudioMixer worldSoundMixer;

	[MoonSharpHidden]
	public AudioMixer masterMixer;

	[MoonSharpHidden]
	public AudioMixer musicMixer;

	[MoonSharpHidden]
	protected JsonSerializerSettings _serializerSettings;

	public AudioClip moveCursorSound;

	public AudioClip errorSound;

	public AudioClip selectSound;

	public AudioClip noticeSound;

	public AudioClip openPopupSound;

	public AudioClip closePopupSound;

	public AudioClip moveSliderSound;

	[MoonSharpHidden]
	public PopupManager popup;

	public float lootDifficultyBonus = 1.5f;

	public float enemyDifficultyBonus = 1f;

	public float offPathDifficultyBonus = 0.25f;

	public List<BaseItem.ItemCategory> itemCategorySortOrder;

	[MoonSharpHidden]
	public string lastCharacterName = "Nobody";

	[MoonSharpHidden]
	public bool desiredThirdPerson;

	[MoonSharpHidden]
	public Globals.PlayerCallback onPlayerSet;

	[MoonSharpHidden]
	public Globals.PlayerCallback onPlayerUnset;

	public Color equippedColor;

	public Color unequippedColor;

	[MoonSharpHidden]
	public List<ItemIcon> itemIcons;

	[MoonSharpHidden]
	protected MainCamera _mainCamera;

	[MoonSharpHidden]
	public bool isDeserializing;

	[MoonSharpHidden]
	protected float _freezeDuration;

	[MoonSharpHidden]
	public PauseMenu pauseMenu;

	[MoonSharpHidden]
	public CaptionsDisplay captions;

	[MoonSharpHidden]
	public bool hideAllCaptions;

	[MoonSharpHidden]
	public CanvasGroup skipCutsceneText;

	[MoonSharpHidden]
	protected Globals.GameSaveData _gameSaveData;

	[MoonSharpHidden]
	public int loadedSeed;

	[MoonSharpHidden]
	protected ConsoleController _console;

	[MoonSharpHidden]
	public Leaderboards leaderboards;

	[MoonSharpHidden]
	public ModList modList;

	[MoonSharpHidden]
	protected Vector2 _screenResolution;

	[MoonSharpHidden]
	public Globals.Callback onResolutionChanged;

	[MoonSharpHidden]
	protected SeededRandom _dungeonLayoutRandom;

	[MoonSharpHidden]
	protected SeededRandom _seededRandom;

	[MoonSharpHidden]
	public int infiniteCoolerSeedOverride = -1;

	[MoonSharpHidden]
	public bool isGeneratingLevel;

	[MoonSharpHidden]
	public int lastCoolerRunSeed;

	[MoonSharpHidden]
	public int score;

	[MoonSharpHidden]
	public GameObject courierRunStartLoot;

	[MoonSharpHidden]
	public Quest courierRunQuest;

	protected bool _canAttemptCourierRun;

	[MoonSharpHidden]
	public bool useNarrator;

	[MoonSharpHidden]
	public bool isResetting;

	protected float _nextWeatherEvent = 172800f;

	protected float _weatherDuration;

	protected float _weatherTime;

	protected int _weatherSeed;

	protected Globals.WeatherType _currentWeather = Globals.WeatherType.None;

	protected List<double> _weatherDurations;

	protected List<BaseBuff> _globalBuffs;

	protected static string _gameDataPath = string.Empty;

	public List<Condition> modConditions;

	public List<EquipmentModification> modMods;

	[MoonSharpHidden]
	protected int lookFrameTime = -1;

	[MoonSharpHidden]
	protected int lookCount;

	[MoonSharpHidden]
	protected bool _applicationFocused = true;

	public List<Globals.FootstepSoundDefinition> footstepSounds;

	protected bool _isInfiniteCooler;

	protected bool _wasInfiniteCoolerMenuInitiated;

	protected Globals.GameSaveData _preInfiniteCoolerSave;

	protected int _infiniteSeed;

	[MoonSharpHidden]
	public bool _dailyOnlineMode;

	protected string _dailyLeaderboardName = string.Empty;

	protected DateTime _lastServerTime = default(DateTime);

	protected bool _modWarningShown;

	[MoonSharpHidden]
	public float musicVolume;

	[MoonSharpHidden]
	public float soundVolume;

	protected string _tutorialTitle;

	protected List<string> _tutorialText;

	[MoonSharpHidden]
	private float currentMusicVolume = 1f;

	protected int _worldTimeDay = -1;

	protected int _worldTimeHour = -1;

	protected int _worldTimeMinute = -1;

	protected string _worldTimeString = string.Empty;

	public bool _canStashInPractice = false;

	public Dictionary<BaseBuff, int> old_buffs = null;

	public bool keepHangoversOnDeath = false;

	public delegate void Callback();

	public delegate void PlayerCallback(Player player);

	public enum WeatherType
	{
		None = -1,
		Rain,
		MAX
	}

	public enum SpecialContainers
	{
		None,
		Global,
		Courier,
		LostAndFound
	}

	[Serializable]
	public class FootstepSoundDefinition
	{
		public PhysicMaterial physicMaterial;

		public List<AudioClip> footstepSounds;

		public GameObject hitImpact;

		public bool noWeaponHitImpact;

		public bool noDecals;

		public GameObject decalOverride;
	}

	public class ServerTimeResponse
	{
		public long timestamp;

		public string timestamp_formatted;

		public long utc_timestamp;

		public string utc_timestamp_formatted;

		public bool is_dst;
	}

	public class FactionAggroState
	{
		public FactionAggroState(string a, string b, float val)
		{
			this.factionA = a;
			this.factionB = b;
			this.value = val;
		}

		public string factionA = string.Empty;

		public string factionB = string.Empty;

		public float value;
	}

	public class RigidbodyState
	{
		public void Store(Rigidbody body)
		{
			if (body == null)
			{
				return;
			}
			this.isKinematic = body.isKinematic;
			this.velocity = body.velocity;
			this.angularVelocity = body.angularVelocity;
			this.useGravity = body.useGravity;
			this.isSleeping = body.IsSleeping();
			this.rotation = body.transform.localRotation;
			this.position = body.transform.localPosition;
			if (!body.isKinematic)
			{
				body.velocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
			}
			body.isKinematic = true;
			body.Sleep();
		}

		public void Restore(Rigidbody body)
		{
			if (body == null)
			{
				return;
			}
			body.isKinematic = this.isKinematic;
			body.useGravity = this.useGravity;
			if (!body.isKinematic)
			{
				body.velocity = this.velocity;
				body.angularVelocity = this.angularVelocity;
			}
			body.transform.localRotation = this.rotation;
			body.transform.localPosition = this.position;
			if (this.isSleeping)
			{
				body.Sleep();
			}
			else
			{
				body.WakeUp();
			}
		}

		public bool isSleeping;

		public bool isKinematic;

		public Vector3 velocity;

		public Vector3 angularVelocity;

		public Vector3 position;

		public Quaternion rotation;

		public bool useGravity;
	}

	public class TimeScaleEntry
	{
		public string name;

		public float amount = 1f;
	}

	public class GameSaveData
	{
		public GameSaveData()
		{
			this.saveFormatVersion = -1;
		}

		public GameSaveData(Globals globals)
		{
			this.saveFormatVersion = Globals.SAVE_FORMAT_VERSION;
			Player player = globals.GetPlayer();
			this.courier = globals.courierBox.SerializeData();
			this.stash = globals.globalContainer.SerializeData();
			this.courierRun = globals.courierRunBox.SerializeData();
			this.gameKeys = new List<string>(globals.gameKeys.Keys);
			this.quests = QuestManager.GetInstance().SerializeData();
			this.player = player.SerializeData();
			this.levelName = Globals.GetInstance().GetLevelName();
			this.level = globals.level;
			this.areas = globals.areaSaveData;
			if (globals.GetDungeon() != null)
			{
				this.dungeonSeed = globals._dungeonLayoutRandom.seed;
			}
			this.globalBuffs = new List<string>();
			foreach (BaseBuff baseBuff in globals._globalBuffs)
			{
				this.globalBuffs.Add(baseBuff.name);
			}
			this.playTime = globals.playTime;
			this.gameMode = globals.gameMode;
			this.worldTime = globals.worldTime;
			this.lastCoolerRunSeed = globals.lastCoolerRunSeed;
			this.lastCoolerRunScore = globals.score;
			this.desiredThirdPerson = globals.desiredThirdPerson;
			this.factionAggro = new List<Globals.FactionAggroState>();
			this.deaths = globals.deaths;
			this.kills = globals.kills;
			foreach (Faction faction in globals.factions)
			{
				if (faction.factionAggro != null)
				{
					foreach (Faction faction2 in faction.factionAggro.Keys)
					{
						this.factionAggro.Add(new Globals.FactionAggroState(faction.gameObject.name, faction2.gameObject.name, faction.factionAggro[faction2]));
					}
				}
			}
			this.unlockedCustomizations = new List<string>();
			foreach (CharacterCustomization characterCustomization in CustomizationManager.GetInstance().GetUnlockedCustomizations())
			{
				this.unlockedCustomizations.Add(characterCustomization.GetSerializedName());
			}
			this.learnedRecipes = new List<string>();
			foreach (CraftingRecipe craftingRecipe in globals.learnedRecipes)
			{
				this.learnedRecipes.Add(craftingRecipe.gameObject.name);
			}
			List<BaseItem> list = new List<BaseItem>(globals.lostItems.GetActualItemsList());
			list.AddRange(globals.GetTemporarilyLostItems());
			this.lostItems = new List<BaseItem.ItemSaveData>();
			foreach (BaseItem baseItem in list)
			{
				this.lostItems.Add(baseItem.SerializeData());
			}
			this.weatherTime = globals._weatherTime;
			this.weatherDuration = globals._weatherDuration;
			this.nextWeatherEvent = globals._nextWeatherEvent;
			this.weatherSeed = globals._weatherSeed;
			this.currentWeather = globals._currentWeather;
			this.weatherDurations = new List<double>(globals._weatherDurations);
		}

		public void DeserializeData(Globals globals)
		{
			Player player;
			if (globals._dungeon != null)
			{
				player = globals.GetPlayer();
				if (player != null)
				{
					UnityEngine.Object.Destroy(player.gameObject);
					globals.SetPlayer(null);
				}
			}
			globals.areaSaveData = this.areas;
			globals.saveData = new Dictionary<string, SaveableData>();
			foreach (string key in globals.areaSaveData.Keys)
			{
				foreach (SaveableData saveableData in globals.areaSaveData[key].data)
				{
					if (saveableData.identifierName != string.Empty)
					{
						globals.saveData[saveableData.identifierName] = saveableData;
					}
				}
			}
			globals._globalBuffs = new List<BaseBuff>();
			globals.deaths = this.deaths;
			globals.kills = this.kills;
			if (this.globalBuffs != null)
			{
				foreach (string buff_name in this.globalBuffs)
				{
					BaseBuff buff = BaseBuff.GetBuff(buff_name);
					if (buff != null)
					{
						globals._globalBuffs.Add(buff);
					}
				}
			}
			globals.desiredThirdPerson = this.desiredThirdPerson;
			player = Player.CreateNewPlayer();
			globals.worldTime = this.worldTime;
			globals.playTime = this.playTime;
			globals.gameMode = this.gameMode;
			this.courier.DeserializeData(globals.courierBox);
			this.stash.DeserializeData(globals.globalContainer);
			if (this.courierRun != null)
			{
				this.courierRun.DeserializeData(globals.courierRunBox);
			}
			else
			{
				globals.courierRunBox.Clear();
			}
			this.player.DeserializeData(player);
			player.gameObject.SetActive(false);
			globals.SetPlayer(player);
			globals.gameKeys.Clear();
			globals.score = this.lastCoolerRunScore;
			globals.lastCoolerRunSeed = this.lastCoolerRunSeed;
			foreach (string key2 in this.gameKeys)
			{
				globals.AddKey(key2);
			}
			globals.loadedSeed = this.dungeonSeed;
			globals.playTime = this.playTime;
			globals.worldTime = this.worldTime;
			if (this.factionAggro != null)
			{
				foreach (Globals.FactionAggroState factionAggroState in this.factionAggro)
				{
					if (Globals.GetFactionByName(factionAggroState.factionA) != null && Globals.GetFactionByName(factionAggroState.factionB) != null)
					{
						globals.SetFactionAggro(Globals.GetFactionByName(factionAggroState.factionA), Globals.GetFactionByName(factionAggroState.factionB), factionAggroState.value);
					}
				}
			}
			if (this.unlockedCustomizations != null)
			{
				foreach (string customization_name in this.unlockedCustomizations)
				{
					CharacterCustomization customizationByName = CustomizationManager.GetInstance().GetCustomizationByName(customization_name);
					if (customizationByName != null)
					{
						CustomizationManager.GetInstance().Unlock(customizationByName);
					}
				}
			}
			globals.learnedRecipes = new List<CraftingRecipe>();
			globals.GrantStartingRecipes();
			if (this.learnedRecipes != null)
			{
				foreach (string b in this.learnedRecipes)
				{
					foreach (CraftingRecipe craftingRecipe in globals.recipes)
					{
						if (craftingRecipe.name == b && !globals.learnedRecipes.Contains(craftingRecipe))
						{
							globals.learnedRecipes.Add(craftingRecipe);
						}
					}
				}
			}
			if (this.quests != null)
			{
				QuestManager.GetInstance().DeserializeData(this.quests);
			}
			if (this.lostItems != null)
			{
				globals.lostItems.Clear();
				foreach (BaseItem.ItemSaveData item_data in this.lostItems)
				{
					globals.lostItems.AddItem(BaseItem.DeserializeToInstance(item_data), false);
				}
			}
			globals._weatherTime = this.weatherTime;
			globals._weatherDuration = this.weatherDuration;
			globals._nextWeatherEvent = this.nextWeatherEvent;
			globals._weatherSeed = this.weatherSeed;
			globals._currentWeather = this.currentWeather;
			globals._weatherDurations = new List<double>(this.weatherDurations);
			globals.UpdateCourierSlotLimit();
		}

		public int saveFormatVersion = -1;

		public Container.InventorySaveData courier;

		public Container.InventorySaveData stash;

		public Container.InventorySaveData courierRun;

		public BaseCharacter.CharacterSaveData player;

		public List<string> gameKeys;

		public QuestManager.QuestSaveData quests;

		public double worldTime;

		public double playTime;

		public int gameMode;

		public string levelName = string.Empty;

		public int level;

		public int dungeonSeed;

		public int lastCoolerRunSeed;

		public int lastCoolerRunScore;

		public float nextWeatherEvent;

		public float weatherDuration;

		public float weatherTime;

		public int weatherSeed = -1;

		public Globals.WeatherType currentWeather;

		public int deaths;

		public int kills;

		public bool desiredThirdPerson;

		public Dictionary<string, AreaSaveData> areas;

		public List<Globals.FactionAggroState> factionAggro;

		public List<string> unlockedCustomizations;

		public List<string> learnedRecipes;

		public List<BaseItem.ItemSaveData> lostItems;

		public List<double> weatherDurations;

		public List<string> globalBuffs;
	}
}
