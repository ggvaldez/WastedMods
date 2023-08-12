using System;
using System.Collections;
using System.Collections.Generic;
using Holoville.HOTween;
using Pathfinding;
using UnityEngine;
using UnityEngine.Rendering;

public class Dungeon : MonoBehaviour
{
	public void RegisterCullable(BaseCullable cullable)
	{
		if (this._cullables == null)
		{
			this._cullables = new List<BaseCullable>();
		}
		this._cullables.Add(cullable);
	}

	public void UnregisterCullable(BaseCullable cullable)
	{
		this._cullables.Remove(cullable);
	}

	public Vector3 GetSpawnPosition()
	{
		return this._spawnPosition;
	}

	public static void ResetRoomCounts()
	{
		if (Dungeon._roomCounts == null)
		{
			Dungeon._roomCounts = new Dictionary<Room, int>();
		}
		Dungeon._roomCounts.Clear();
	}

	public Vector3 GetGridPosition(Vector3 position, bool ignore_height = false)
	{
		if (ignore_height)
		{
			position.y = 0f;
		}
		return new Vector3(Mathf.Floor(position.x / Dungeon.gridUnit), Mathf.Floor((float)Mathf.RoundToInt(position.y)), Mathf.Floor(position.z / Dungeon.gridUnit));
	}

	public Vector3 GetSnappedPosition(Vector3 position)
	{
		return this.GetGridPosition(position, false) * Dungeon.gridUnit;
	}

	public void SetHunterInstance(BaseCharacter hunter)
	{
		this._hunterInstance = hunter;
		if (this._hunterInstance != null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(Resources.Load<GameObject>("Minimap Hunter"));
			gameObject.transform.parent = this._hunterInstance.transform;
			Util.InitializeTransform(gameObject);
		}
	}

	public BaseCharacter GetHunterInstance()
	{
		return this._hunterInstance;
	}

	public void LateUpdate()
	{
		if (this._levelType != null && this._cachedLightColor != this.lightColor)
		{
			this._cachedLightColor = this.lightColor;
			if (this.onLightsChanged != null)
			{
				this.onLightsChanged();
			}
		}
		if (Globals.GetInstance().GetPlayer() != null && this._startCutscene != null && Globals.GetInstance().GetPlayer().transform.position.y < this.GetLevelBottom())
		{
			this._startCutscene.Skip();
		}
		if (Globals.GetInstance().GetWorldPaused(false, false))
		{
			Globals.GetInstance().AddTimeScale("pause", 0f);
		}
		else
		{
			Globals.GetInstance().RemoveTimeScale("pause");
		}
		if (this.GetLevelType().usePVS)
		{
			for (int i = 0; i < this._cullables.Count; i++)
			{
				BaseCullable baseCullable = this._cullables[i];
				if (baseCullable == null)
				{
					this._cullables.RemoveAt(i);
					i--;
				}
				else if (baseCullable.NeedsUpdateFromPosition())
				{
					baseCullable.UpdateCullRoomFromPosition();
				}
			}
		}
	}

	public Room GetRoomAtPosition(Vector3 position)
	{
		Vector3 gridPosition = this.GetGridPosition(position, true);
		if (this._occupiedSpaces != null && this._occupiedSpaces.ContainsKey(gridPosition))
		{
			return this._occupiedSpaces[gridPosition];
		}
		return null;
	}

	public void CullRooms()
	{
		if (this._mainCamera == null)
		{
			return;
		}
		Room roomAtPosition = this.GetRoomAtPosition(this._mainCamera.transform.position);
		if (roomAtPosition != this.cameraContainingRoom)
		{
			this._visibleRooms.Clear();
			List<Room> list = new List<Room>();
			if (this.cameraContainingRoom != null)
			{
				this.cameraContainingRoom.visible = false;
				foreach (Room room in this.cameraContainingRoom.visibleRooms)
				{
					room.visible = false;
					list.Add(room);
				}
			}
			this.cameraContainingRoom = roomAtPosition;
			if (this.cameraContainingRoom != null)
			{
				if (!this.cameraContainingRoom.visible)
				{
					this.cameraContainingRoom.visible = true;
					list.Add(this.cameraContainingRoom);
					this._visibleRooms.Add(this.cameraContainingRoom);
				}
				foreach (Room room2 in this.cameraContainingRoom.visibleRooms)
				{
					room2.visible = true;
					list.Add(room2);
					this._visibleRooms.Add(room2);
				}
			}
			foreach (Room room3 in list)
			{
				room3.UpdateVisibility();
			}
		}
		foreach (Room room4 in this._visibleRooms)
		{
			room4.UpdateVisibility();
		}
	}

	public void FixedUpdate()
	{
		if (this._waitForFixedUpdate)
		{
			if (this._waitForFixedUpdateCallback != null)
			{
				this._waitForFixedUpdateCallback();
			}
			this._waitForFixedUpdateCallback = null;
			this._waitForFixedUpdate = false;
		}
	}

	public void Update()
	{
		if (this._waitForUpdate)
		{
			if (this._waitForUpdateCallback != null)
			{
				this._waitForUpdateCallback();
			}
			this._waitForUpdateCallback = null;
			this._waitForUpdate = false;
		}
		if (Globals.GetInstance().loadingScreen.IsActive())
		{
			return;
		}
		if (this._levelInitialized && this.GetLevelType().usePVS)
		{
			this.CullRooms();
		}
		if (this._mainCamera == null)
		{
			this._mainCamera = Camera.main;
		}
		for (int i = 0; i < Camera.allCamerasCount; i++)
		{
			Camera camera = Camera.allCameras[i];
			if (!(this._mainCamera == null))
			{
				if (camera.depth < this._mainCamera.depth)
				{
					if (camera.clearFlags == CameraClearFlags.Color)
					{
						camera.backgroundColor = RenderSettings.fogColor;
					}
				}
			}
		}
		Globals.GetInstance().musicSource.volume = this.musicBalance;
		this.hunterMusic.volume = 1f - this.musicBalance;
		if (!this._levelStarted)
		{
			return;
		}
		if (Globals.GetInstance().popup.IsActive())
		{
			return;
		}
		if (Globals.GetInstance().pauseMenu.IsActive())
		{
			return;
		}
		if ((GameInput.GetInstance().GetButtonDown("Pause") || (Input.GetKeyDown(KeyCode.Escape) && !this.ingameMenu.IsActive() && !this.characterCreator.IsActive() && !PhotoMode.IsActive())) && Globals.GetInstance().pauseMenu.CanPause())
		{
			Globals.GetInstance().pauseMenu.ShowPauseMenu();
		}
		if (this.ingameMenu.IsActive())
		{
			this.ingameMenu.Think();
			return;
		}
		if (this.conversationTopicHandler.IsActive())
		{
			Globals.GetInstance().GetPlayer().SetDesiredMovement(Vector3.zero, false, false);
			this.conversationTopicHandler.Think();
			return;
		}
		if (this.computerTopicHandler.IsActive())
		{
			Globals.GetInstance().GetPlayer().SetDesiredMovement(Vector3.zero, false, false);
			this.computerTopicHandler.Think();
			return;
		}
		if (this.characterCreator.IsActive())
		{
			Globals.GetInstance().GetPlayer().SetDesiredMovement(Vector3.zero, false, false);
			this.characterCreator.Think();
			return;
		}
		if (!Globals.GetInstance().cutscene.IsActive() && this.scriptedSequenceTopicHandler.IsActive())
		{
			Globals.GetInstance().GetPlayer().SetDesiredMovement(Vector3.zero, false, false);
			this.scriptedSequenceTopicHandler.Think();
			return;
		}
		if (Globals.GetInstance().GetWorldPaused(false, false))
		{
			return;
		}
		if (!Globals.GetInstance().GetLogicPaused(false, false))
		{
			if (!this._variantCalledOut && this.GetLevelType().variant != null)
			{
				this._variantCalledOut = true;
				Globals.GetInstance().PlayGlobalSound(this.hud.huntedSound, "important_notification");
				this.hud.AddCombatMessage(this.GetLevelType().variant.variantMessage);
			}
			float target = 1f;
			if (this._hunterInstance != null && this._hunterInstance.IsConscious())
			{
				float num = (this._hunterInstance.transform.position - this._mainCamera.transform.position).magnitude;
				num -= 20f;
				if (this.GetLevelType().hunterMusicMaxDistance > 0f)
				{
					target = Mathf.Clamp(num / this.GetLevelType().hunterMusicMaxDistance, 0f, 1f);
				}
				else
				{
					target = 0f;
				}
			}
			this.musicBalance = Mathf.MoveTowards(this.musicBalance, target, 5f * Time.deltaTime);
			if (this.GetLevelType().GetHunter() != null && this.timeUntilHunted > 0f)
			{
				this.timeUntilHunted -= Time.deltaTime;
				if (this.timeUntilHunted <= 0f)
				{
					if (!this.quickTest)
					{
						if (!Globals.GetInstance().HasKey("tutorial_hunter"))
						{
							Globals.GetInstance().ShowTutorial("tutorial_hunter");
						}
						Globals.GetInstance().PlayGlobalSound(this.hud.huntedSound, "important_notification");
						this.hud.AddCombatMessage("Something is out to get you...");
						GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(this.GetLevelType().GetHunter().gameObject);
						gameObject.transform.transform.parent = this._levelContent.transform;
						this.SetHunterInstance(gameObject.GetComponent<Enemy>());
						this._hunterInstance.transform.position = this._spawnPosition;
						this._hunterInstance.transform.rotation = this._spawnRotation;
						this._hunterInstance.SetLookDirection(this._spawnRotation, true);
						if (this._hunterInstance is Enemy)
						{
							this.GetLevelType().ProcessEnemy(this._hunterInstance as Enemy);
						}
					}
					this.timeUntilHunted = 0f;
				}
				else if (this._hunterWarningLevel <= 2 && this.timeUntilHunted <= 10f)
				{
					this._hunterWarningLevel = 3;
					Globals.GetInstance().PlayGlobalSound(this.hud.huntedWarningSound, "important_notification");
					this.hud.AddCombatMessage("Your seconds are numbered...");
				}
				else if (this._hunterWarningLevel <= 1 && this.timeUntilHunted <= 30f)
				{
					this._hunterWarningLevel = 2;
					Globals.GetInstance().PlayGlobalSound(this.hud.huntedWarningSound, "important_notification");
					this.hud.AddCombatMessage("The deadly presence draws close...");
				}
				else if (this._hunterWarningLevel == 0 && this.timeUntilHunted <= 60f)
				{
					if (!Globals.GetInstance().HasKey("tutorial_hunter_warned"))
					{
						Globals.GetInstance().ShowTutorial("tutorial_hunter_warned");
					}
					this._hunterWarningLevel = 1;
					Globals.GetInstance().PlayGlobalSound(this.hud.huntedWarningSound, "important_notification");
					this.hud.AddCombatMessage("You feel a deadly presence approaching...");
				}
			}
			if (Globals.GetInstance().GetPlayer() != null && Globals.GetInstance().GetPlayer().IsAlive() && GameInput.GetInstance().GetButtonDown("Suicide") && !Dungeon.isInControlledScene && !Globals.GetInstance().HasKey("disable_car"))
			{
				Globals.GetInstance().PlayGlobalSound(Globals.GetInstance().errorSound, "notification");
				PopupManager popup = Globals.GetInstance().popup;
				popup.BeginPopup("Suicide");
				popup.AddWidget<PopupLabel>(string.Empty).SetLabel("Are you sure you want to commit suicide?");
				popup.AddWidget<PopupSpacer>(string.Empty);
				popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetLabel("Yes").AddCallback(delegate(PopupButton button)
				{
					popup.ClosePopup();
					Globals.GetInstance().GetPlayer().ModifyHealth(-10000f, Globals.GetInstance().GetPlayer(), null, null, null, null, null, null, null);
					LimbState limbState = Globals.GetInstance().GetPlayer().GetLimbState("head");
					Globals.GetInstance().GetPlayer().ModifyLimbHealth(limbState, -100);
					Globals.GetInstance().GetPlayer().UpdateLimb(limbState);
				});
				PopupButton selected_widget = popup.AddWidget<PopupButton>(string.Empty).SetAlignment(TextAnchor.UpperRight).SetHotkey("Cancel").SetLabel("No").AddCallback(delegate(PopupButton button)
				{
					popup.ClosePopup();
				});
				popup.EndPopup();
				if (GameInput.IsJoystick())
				{
					popup.SelectItem(selected_widget);
				}
				return;
			}
		}
	}

	public IEnumerator UpdateGraph()
	{
		AstarPath astar = UnityEngine.Object.FindObjectOfType<AstarPath>();
		if (astar == null)
		{
			UnityEngine.Object.Instantiate<GameObject>(Resources.Load<GameObject>("Astar"));
			astar = AstarPath.active;
		}
		yield return 0;
		if (this.GetLevelType().dynamicLevelBounds)
		{
			this.sceneBounds = default(Bounds);
			foreach (Collider collider in UnityEngine.Object.FindObjectsOfType<Collider>())
			{
				if (collider.gameObject.layer != LayerMask.NameToLayer("Terrain"))
				{
					this.sceneBounds.Encapsulate(collider.bounds);
				}
			}
			if (astar.graphs[0] is RecastGraph)
			{
				RecastGraph recastGraph = astar.graphs[0] as RecastGraph;
				recastGraph.forcedBoundsCenter = this.sceneBounds.center;
				recastGraph.forcedBoundsSize = this.sceneBounds.size;
			}
		}
		else
		{
			this.sceneBounds = this.GetLevelType().levelBounds;
			if (astar.graphs[0] is RecastGraph)
			{
				RecastGraph recastGraph2 = astar.graphs[0] as RecastGraph;
				recastGraph2.forcedBoundsCenter = this.GetLevelType().levelBounds.center;
				recastGraph2.forcedBoundsSize = this.GetLevelType().levelBounds.size;
			}
		}
		if (!this.GetLevelType().prebakedNavmesh)
		{
			RecastGraph recast = astar.graphs[0] as RecastGraph;
			if (recast != null)
			{
				recast.walkableHeight = 1.5f;
				recast.characterRadius = 0.55f;
				recast.rasterizeMeshes = false;
			}
			astar.threadCount = ThreadCount.AutomaticLowLoad;
			yield return 0;
			astar.Scan(null);
			if (AstarPath.active.isScanning)
			{
				yield return 0;
			}
		}
		this._levelBottomY = (astar.graphs[0] as RecastGraph).forcedBounds.min.y;
		this._levelBottomY -= 25f;
		yield break;
	}

	public float GetLevelBottom()
	{
		return this._levelBottomY;
	}

	public void Start()
	{
		Globals globals = this._globals;
		globals.onPreSave = (Globals.Callback)Delegate.Combine(globals.onPreSave, new Globals.Callback(this.OnSave));
		Globals globals2 = this._globals;
		globals2.onLoadNewLevel = (Globals.Callback)Delegate.Combine(globals2.onLoadNewLevel, new Globals.Callback(this.OnSave));
		base.StartCoroutine(this.InitializeLevel());
	}

	public IEnumerator InitializeLevel()
	{
		this.hunterMusic.ignoreListenerPause = true;
		if (Dungeon._roomCounts == null)
		{
			Dungeon._roomCounts = new Dictionary<Room, int>();
		}
		Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character Containers"), LayerMask.NameToLayer("Trigger"), true);
		if (Application.loadedLevelName != "Ending")
		{
			Globals.GetInstance().loadingScreen.Show();
		}
		this.levelMusic = this.GetLevelType().GetMusic();
		this.GetLevelType();
		this._levelStarted = false;
		this._levelInitialized = false;
		yield return Application.LoadLevelAdditiveAsync("HUD Scene");
		yield return 0;
		GameObject hud_root = GameObject.FindGameObjectWithTag("HUD Root");
		this.hud = hud_root.GetComponentsInChildren<HUD>(true)[0];
		this.conversationTopicHandler = hud_root.GetComponentsInChildren<ConversationTopicHandler>(true)[0];
		this.computerTopicHandler = hud_root.GetComponentsInChildren<ComputerTopicHandler>(true)[0];
		this.characterCreator = hud_root.GetComponentsInChildren<CharacterCreator>(true)[0];
		this.ingameMenu = hud_root.GetComponentsInChildren<IngameMenu>(true)[0];
		this.hud.gameObject.SetActive(true);
		this.ingameMenu.gameObject.SetActive(true);
		if (this._levelContent != null)
		{
			UnityEngine.Object.Destroy(this._levelContent.gameObject);
			this._levelContent = null;
		}
		this._levelContent = new GameObject("Level Container");
		this._levelContent.transform.parent = base.transform;
		Util.InitializeTransform(this._levelContent);
		yield return 0;
		this.InitializePlayer();
		if (Globals.GetInstance().GetPlayer() != null)
		{
			Globals.GetInstance().GetPlayer().gameObject.SetActive(false);
			Globals.GetInstance().GetPlayer().PruneCompanions();
			if (Globals.GetInstance().GetPlayer().companions != null)
			{
				foreach (BaseCharacter baseCharacter in Globals.GetInstance().GetPlayer().companions)
				{
					baseCharacter.gameObject.SetActive(false);
				}
			}
		}
		if (this.GetLevelType().levelSubname == HUD.areaSubname && this.GetLevelType().GetAreaName(this.GetDungeonLevel()) != HUD.areaName)
		{
			Dungeon.ResetRoomCounts();
		}
		this._roomPool = new List<Room>(this.GetLevelType().GetRoomPool());
		foreach (Quest quest in QuestManager.GetInstance().GetInProgressQuests())
		{
			if (!QuestManager.GetInstance().GetQuestState(quest).GetCurrentStep().isFloorReplacement)
			{
				this._roomPool.AddRange(QuestManager.GetInstance().GetQuestState(quest).GetCurrentStep().questRooms);
			}
		}
		this._openExits = new List<RoomExit>();
		this._rooms = new List<Room>();
		this._spawnedBooze = new Dictionary<Room, List<BaseBuff>>();
		this._occupiedSpaces = new Dictionary<Vector3, Room>();
		this._roomEntrances = new Dictionary<Vector3, RoomExit>();
		this._occupiedSpaces.Clear();
		this._disableHunter = false;
		Globals.GetInstance().isGeneratingLevel = true;
		if (this.dungeonSeed < 0)
		{
			if (Globals.GetInstance().loadSuccessful)
			{
				Globals.GetInstance().GetDungeonRandom().SetSeed(Globals.GetInstance().loadedSeed);
				this.dungeonSeed = Globals.GetInstance().GetDungeonRandom().seed;
			}
			else
			{
				Globals.GetInstance().GetDungeonRandom().Reseed();
				this.dungeonSeed = Globals.GetInstance().GetDungeonRandom().seed;
			}
		}
		else
		{
			Globals.GetInstance().GetDungeonRandom().SetSeed(this.dungeonSeed);
		}
		this._levelType.Initialize();
		if (this.GetLevelType().GetRoomPool().Count > 0)
		{
			yield return base.StartCoroutine(this.GenerateLayout());
		}
		else
		{
			this._rooms = new List<Room>(UnityEngine.Object.FindObjectsOfType<Room>());
			foreach (Room room in this._rooms)
			{
				room.transform.parent = this._levelContent.transform;
			}
		}
		if (Globals.GetInstance().gameMode != 0)
		{
			this._disableHunter = true;
		}
		if (this._disableHunter)
		{
			this.timeUntilHunted = 0f;
		}
		else
		{
			if (this.GetLevelType() is InfiniteLevelType)
			{
				this.timeUntilHunted = (float)(60 + this._layoutComplexity * 25);
			}
			else
			{
				this.timeUntilHunted = Globals.GetInstance().timeUntilHuntedExpression.Evaluate(null, null, ParamEvalMode.Normal, (float)this._layoutComplexity, null, null);
			}
			if (this.GetLevelType() is InfiniteLevelType)
			{
				this.timeUntilHunted = Mathf.Lerp(this.timeUntilHunted, 0f, (float)this.GetDungeonLevel() / 20f);
				if (this.GetLevelType().GetDungeonTags().Contains("fast_hunt"))
				{
					this.timeUntilHunted = Mathf.Min(this.timeUntilHunted, 15f);
				}
				if (this.timeUntilHunted < 15f)
				{
					this.timeUntilHunted = 15f;
				}
			}
		}
		yield return 0;
		yield return base.StartCoroutine(this.UpdateGraph());
		yield return 0;
		Globals.GetInstance().GetDungeonRandom().Reseed();
		int old_seed = Globals.GetInstance().GetDungeonRandom().seed;
		this.SpawnClutter();
		yield return 0;
		yield return 0;
		Globals.GetInstance().GetDungeonRandom().SetSeed(old_seed);
		Globals.GetInstance().GetDungeonRandom().Reseed();
		if (this.GetLevelType().usePVS)
		{
			yield return base.StartCoroutine(this.CalculatePVS());
			foreach (Room room2 in this._rooms)
			{
				if (this.GetLevelType().roomParticlePrefab != null)
				{
					GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(this.GetLevelType().roomParticlePrefab);
					gameObject.transform.parent = room2.transform;
					Util.InitializeTransform(gameObject);
				}
				room2.visible = false;
				room2.StoreLevelRenderers();
			}
		}
		yield return 0;
		List<GameObject> batched_objects = new List<GameObject>();
		foreach (GameObject gameObject2 in GameObject.FindGameObjectsWithTag("Static Batched"))
		{
			if (Util.ShouldBeBatched(gameObject2, false))
			{
				Util.GetDescendants(gameObject2, batched_objects);
			}
		}
		StaticBatchingUtility.Combine(batched_objects.ToArray(), null);
		yield return 0;
		GameObject minimap_gameobject = UnityEngine.Object.Instantiate(Resources.Load("Minimap")) as GameObject;
		Minimap minimap = minimap_gameobject.GetComponent<Minimap>();
		minimap.Reset();
		if (this.GetLevelType().minimapOverride == null)
		{
			minimap.GenerateMinimap();
		}
		this.SpawnObstacles();
		Color cooler_color = this.GetLevelType().GetCoolerTint();
		foreach (GameObject gameObject3 in GameObject.FindGameObjectsWithTag("Cooler Tinted"))
		{
			Light component = gameObject3.GetComponent<Light>();
			if (component != null)
			{
				component.color = cooler_color;
			}
			foreach (MeshRenderer meshRenderer in gameObject3.GetComponentsInChildren<MeshRenderer>())
			{
				foreach (Material material in meshRenderer.sharedMaterials)
				{
					if (material.IsKeywordEnabled("_EMISSION"))
					{
						HSBColor hsbcolor = HSBColor.FromColor(material.GetColor("_EmissionColor"));
						HSBColor hsbcolor2 = new HSBColor(cooler_color);
						hsbcolor2.b = hsbcolor.b;
						material.SetColor("_EmissionColor", hsbcolor2.ToColor());
					}
				}
			}
		}
		Globals.GetInstance().isDeserializing = true;
		this._RegisterSaveables();
		Globals.GetInstance().isDeserializing = false;
		foreach (Door door in this._levelContent.GetComponentsInChildren<Door>(true))
		{
			door.UpdateTag();
		}
		if (this.GetLevelType().usePVS)
		{
			foreach (Room room3 in this._rooms)
			{
				room3.visible = false;
				room3.UpdateVisibility();
			}
		}
		this.cameraContainingRoom = null;
		MonsterSpawnPoint[] monster_spawn_points = UnityEngine.Object.FindObjectsOfType<MonsterSpawnPoint>();
		for (int n = 0; n < monster_spawn_points.Length; n++)
		{
			UnityEngine.Object.Destroy(monster_spawn_points[n].gameObject);
		}
		ObstacleSpawnHint[] spawn_points = UnityEngine.Object.FindObjectsOfType<ObstacleSpawnHint>();
		for (int num = 0; num < spawn_points.Length; num++)
		{
			UnityEngine.Object.Destroy(spawn_points[num].gameObject);
		}
		this._openExits.Clear();
		this.hunterMusic.Play();
		Globals.GetInstance().isGeneratingLevel = false;
		yield return 0;
		if (this._rooms.Count > 0)
		{
			if (this.GetLevelType().usePVS)
			{
				this._rooms[0].Activate();
			}
			else
			{
				foreach (Room room4 in this._rooms)
				{
					room4.Activate();
				}
			}
		}
		Globals.GetInstance().GetPlayer().gameObject.SetActive(true);
		this.PlacePlayer();
		yield return 0;
		Globals.GetInstance().GetPlayer().gameObject.SetActive(true);
		if (this.ambience != null)
		{
			this.ambience.spatialBlend = 0f;
			this.ambience.loop = true;
			this.ambience.Play();
		}
		yield return base.StartCoroutine(this.WaitForUpdate(delegate
		{
			this.OnLoad();
		}));
		this.SetMood(this.GetLevelType());
		yield return 0;
		yield return base.StartCoroutine(this.WaitForUpdate(delegate
		{
			if (!Globals.GetInstance().loadSuccessful)
			{
				Cutscene startCutscene = this.GetLevelType().GetStartCutscene();
				if (startCutscene != null)
				{
					this._startCutscene = Globals.GetInstance().cutscene.LoadCutscene(startCutscene);
					this._startCutscene.transform.position = Globals.GetInstance().GetPlayer().transform.position;
					this._startCutscene.transform.rotation = Globals.GetInstance().GetPlayer().GetLookDirection();
					Cutscene startCutscene2 = this._startCutscene;
					startCutscene2.onComplete = (Cutscene.Callback)Delegate.Combine(startCutscene2.onComplete, new Cutscene.Callback(delegate()
					{
						foreach (BaseCharacter baseCharacter4 in Globals.GetInstance().GetPlayer().companions)
						{
							baseCharacter4.gameObject.SetActive(true);
						}
					}));
					Globals.GetInstance().cutscene.Play();
				}
				else
				{
					foreach (BaseCharacter baseCharacter2 in Globals.GetInstance().GetPlayer().companions)
					{
						baseCharacter2.gameObject.SetActive(true);
					}
				}
			}
			foreach (BaseCharacter baseCharacter3 in Globals.GetInstance().GetPlayer().companions)
			{
				baseCharacter3.ResetDungeon();
			}
		}));
		this._characterInteractPoints = new List<CharacterInteractPoint>(UnityEngine.Object.FindObjectsOfType<CharacterInteractPoint>());
		yield return base.StartCoroutine(this.WaitForUpdate(delegate
		{
			Globals.GetInstance().FlushBodyStates();
			this._levelInitialized = true;
			if (this.onLevelInitialize != null)
			{
				this.onLevelInitialize();
			}
			this.onLevelInitialize = null;
		}));
		Globals.GetInstance().GetPlayer().exitIdentifier = string.Empty;
		yield return 0;
		Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character Containers"), LayerMask.NameToLayer("Trigger"), false);
		Globals.GetInstance().loadingScreen.Hide();
		Globals.GetInstance().loadSuccessful = false;
		if (this._levelType.visitedFlag != string.Empty)
		{
			Globals.GetInstance().AddKey(this._levelType.visitedFlag);
		}
		yield return 0;
		Globals.GetInstance().GetPlayer().SnapToFloor();
		while (this.scriptedSequenceTopicHandler.IsActive() || Globals.GetInstance().cutscene.IsActive())
		{
			yield return 0;
		}
		while (this.levelLoadBlockers > 0)
		{
			yield return 0;
		}
		if (Globals.GetInstance().IsInfiniteCooler())
		{
			QuestManager.GetInstance().StartQuest(Globals.GetInstance().courierRunQuest);
		}
		this._levelStarted = true;
		if (this.onLevelStart != null)
		{
			this.onLevelStart();
		}
		this.onLevelStart = null;
		if (Globals.GetInstance().onDungeonLevelStarted != null)
		{
			Globals.GetInstance().onDungeonLevelStarted();
		}
		while (this.scriptedSequenceTopicHandler.IsActive() || Globals.GetInstance().cutscene.IsActive())
		{
			yield return 0;
		}
		this.hud.ShowAreaName(this.GetLevelType().GetAreaName(this.GetDungeonLevel()), this.GetLevelType().levelSubname);
		while (this.hud.areaNameAnimator.gameObject.activeInHierarchy)
		{
			yield return 0;
		}
		this._shouldShowJournals = true;
		this.ShowJournalUpdateEntries();
		yield break;
	}

	public void InitializePlayer()
	{
		Player player = null;
		if (Player.players.Count > 0)
		{
			player = Player.players[0];
			if (!player.IsAlive())
			{
				UnityEngine.Object.Destroy(player.gameObject);
				player = null;
			}
		}
		if (player == null)
		{
			player = Player.CreateNewPlayer();
			if (Globals.GetInstance().old_buffs != null && Globals.GetInstance().keepHangoversOnDeath)
			{
				foreach (KeyValuePair<BaseBuff, int> keyValuePair in Globals.GetInstance().old_buffs)
				{
					for (int i = 0; i < keyValuePair.Value; i++)
					{
						if (keyValuePair.Key.buffType == BaseBuff.BuffType.Hangover)
						{
							player.AddBuff(keyValuePair.Key);
						}
					}
				}
			}
		}
		this._registeredPlayer = player;
		Globals.GetInstance().SetPlayer(this._registeredPlayer);
		player.ResetDungeon();
		Globals.GetInstance().GetMainCamera().transform.parent = player.transform;
		Globals.GetInstance().GetPlayer().ResetThirdPerson();
		Player registeredPlayer = this._registeredPlayer;
		registeredPlayer.onDeath = (BaseCharacter.Callback)Delegate.Combine(registeredPlayer.onDeath, new BaseCharacter.Callback(this.OnPlayerDeath));
		this.hud.SetPlayer(this._registeredPlayer);
	}

	public void SetMood(LevelType level_type)
	{
		if (!level_type.useSceneEnvironmentalLighting)
		{
			if (level_type.skyColor == level_type.equatorColor && level_type.equatorColor == level_type.groundColor)
			{
				RenderSettings.ambientMode = AmbientMode.Flat;
			}
			else
			{
				RenderSettings.ambientMode = AmbientMode.Trilight;
			}
			RenderSettings.ambientSkyColor = level_type.skyColor * level_type.ambientIntensity;
			RenderSettings.ambientEquatorColor = level_type.equatorColor * level_type.ambientIntensity;
			RenderSettings.ambientGroundColor = level_type.groundColor * level_type.ambientIntensity;
			RenderSettings.fogColor = level_type.GetFogColor();
			RenderSettings.fogDensity = level_type.GetFogDensity();
		}
		this.lightColor = level_type.lightColor;
		Globals.GetInstance().GetMainCamera().GetComponent<MainCamera>().SetBlack(level_type.GetBlackColor());
		SkyLight skyLight = UnityEngine.Object.FindObjectOfType<SkyLight>();
		if (skyLight != null)
		{
			if (skyLight.skySphereMaterial != null)
			{
				Globals.GetInstance().GetMainCamera().camera.clearFlags = CameraClearFlags.Depth;
			}
			else
			{
				Globals.GetInstance().GetMainCamera().camera.clearFlags = CameraClearFlags.Skybox;
			}
		}
		if (level_type.GetDungeonTags().Contains("lights_out"))
		{
			RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, Color.black, 0.75f);
			RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, Color.black, 0.75f);
			RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, Color.black, 0.75f);
			RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, Color.black, 0.5f);
			GameObject x = GameObject.Find("Directional light");
			if (x != null)
			{
			}
		}
	}

	public List<CharacterInteractPoint> GetCharacterInteractPoints()
	{
		return this._characterInteractPoints;
	}

	public IEnumerator WaitForUpdate(Dungeon.Callback callback)
	{
		this._waitForUpdate = true;
		this._waitForUpdateCallback = (Dungeon.Callback)Delegate.Combine(this._waitForUpdateCallback, callback);
		while (this._waitForUpdate)
		{
			yield return new WaitForEndOfFrame();
		}
		yield break;
	}

	public IEnumerator WaitForFixedUpdate(Dungeon.Callback callback)
	{
		this._waitForFixedUpdate = true;
		this._waitForFixedUpdateCallback = (Dungeon.Callback)Delegate.Combine(this._waitForFixedUpdateCallback, callback);
		while (this._waitForFixedUpdate)
		{
			yield return new WaitForEndOfFrame();
		}
		yield break;
	}

	public IEnumerator CalculatePVS()
	{
		Door[] doors = UnityEngine.Object.FindObjectsOfType<Door>();
		foreach (Door door in doors)
		{
			if (door.IsOpenable())
			{
				door.gameObject.SetActive(false);
			}
		}
		Debug.Log("Calculating PVS.");
		for (int i = 0; i < this._rooms.Count; i++)
		{
			Room room_a = this._rooms[i];
			for (int k = i; k < this._rooms.Count; k++)
			{
				if (k != i)
				{
					Room room = this._rooms[k];
					bool flag = false;
					foreach (RoomExit roomExit in room_a.exits)
					{
						if (!(roomExit.destination == null))
						{
							foreach (RoomExit roomExit2 in room.exits)
							{
								if (!(roomExit2.destination == null))
								{
									if (this.CheckVisibility(roomExit, roomExit2))
									{
										flag = true;
										break;
									}
								}
							}
							if (flag)
							{
								break;
							}
						}
					}
					if (flag)
					{
						room_a.visibleRooms.Add(room);
						room.visibleRooms.Add(room_a);
					}
				}
			}
			yield return 0;
		}
		Debug.Log("Processed " + this._rooms.Count + " rooms");
		foreach (Door door2 in doors)
		{
			door2.gameObject.SetActive(true);
		}
		yield break;
	}

	public bool CheckVisibility(RoomExit a, RoomExit b)
	{
		if (a.destination == null)
		{
			return false;
		}
		if (b.destination == null)
		{
			return false;
		}
		Vector3 vector = b.transform.position - a.transform.position;
		if (vector.magnitude > 60f)
		{
			return false;
		}
		vector.Normalize();
		if (Vector3.Dot(vector, a.transform.forward) <= 0f)
		{
			return false;
		}
		if (Vector3.Dot(-vector, b.transform.forward) <= 0f)
		{
			return false;
		}
		if (this.GetRoomAtPosition(a.GetExitPosition()) == null)
		{
			return false;
		}
		if (this.GetRoomAtPosition(b.GetExitPosition()) == null)
		{
			return false;
		}
		for (int i = -2; i <= 2; i++)
		{
			for (int j = -2; j <= 2; j++)
			{
				Vector3 vector2 = (a.transform.position + a.GetExitPosition()) / 2f + (float)i * (a.transform.right * (Dungeon.gridUnit / 5f)) + -a.transform.up * Dungeon.gridUnit * 0.5f + (a.transform.up * 2f + (float)j * (a.transform.up * 0.75f));
				for (int k = -2; k <= 2; k++)
				{
					for (int l = -2; l <= 2; l++)
					{
						Vector3 vector3 = (b.transform.position + b.GetExitPosition()) / 2f + (float)k * (b.transform.right * (Dungeon.gridUnit / 5f)) + -b.transform.up * Dungeon.gridUnit * 0.5f + (b.transform.up * 2f + (float)l * (b.transform.up * 0.75f));
						if (!Physics.Linecast(vector2, vector3, Globals.GetInstance().occlusionLayerMask) && !Physics.Linecast(vector3, vector2, Globals.GetInstance().occlusionLayerMask))
						{
							return true;
						}
					}
				}
			}
		}
		return false;
	}

	public bool BlocksSight(int x, int z)
	{
		return !this._occupiedSpaces.ContainsKey(new Vector3((float)x, 0f, (float)z));
	}

	public bool CheckLineOfSight(int x1, int y1, int x2, int y2)
	{
		int num = x1;
		int num2 = y1;
		int num3 = y2 - y1;
		int num4 = x2 - x1;
		int num5;
		if (num4 < 0)
		{
			num5 = -1;
			num4 = -num4;
		}
		else
		{
			num5 = 1;
		}
		int num6;
		if (num3 < 0)
		{
			num6 = -1;
			num3 = -num3;
		}
		else
		{
			num6 = 1;
		}
		int num7 = 2 * num4;
		int num8 = 2 * num3;
		if (num8 >= num7)
		{
			int num10;
			int num9 = num10 = num3;
			for (int i = 0; i < num3; i++)
			{
				num2 += num6;
				num9 += num7;
				if (num9 > num8)
				{
					num += num5;
					num9 -= num8;
					if (num9 + num10 < num8)
					{
						if (this.BlocksSight(num - num5, num2))
						{
							return false;
						}
					}
					else if (num9 + num10 > num8)
					{
						if (this.BlocksSight(num, num2 - num6))
						{
							return false;
						}
					}
				}
				if (this.BlocksSight(num, num2))
				{
					return false;
				}
				num10 = num9;
			}
		}
		else
		{
			int num10;
			int num9 = num10 = num4;
			for (int i = 0; i < num4; i++)
			{
				num += num5;
				num9 += num8;
				if (num9 > num7)
				{
					num2 += num6;
					num9 -= num7;
					if (num9 + num10 < num7)
					{
						if (this.BlocksSight(num, num2 - num6))
						{
							return false;
						}
					}
					else if (num9 + num10 > num7)
					{
						if (this.BlocksSight(num - num5, num2 - num6))
						{
							return false;
						}
					}
				}
				if (this.BlocksSight(num, num2))
				{
					return false;
				}
				num10 = num9;
			}
		}
		return true;
	}

	public void SpawnClutter()
	{
		int count = this._rooms.Count * 30;
		if (this.GetLevelType().clutterPrefabs.Count > 0)
		{
			WeightedTable<SpawnableItem> weightedTable = new WeightedTable<SpawnableItem>();
			foreach (GameObject gameObject in this.GetLevelType().clutterPrefabs)
			{
				if (!(gameObject == null))
				{
					SpawnableItem component = gameObject.GetComponent<SpawnableItem>();
					if (component != null)
					{
						weightedTable.AddItem(component);
					}
				}
			}
			List<GraphNode> nodes = new List<GraphNode>();
			AstarPath.active.astarData.recastGraph.GetNodes(delegate(GraphNode node)
			{
				nodes.Add(node);
				return true;
			});
			List<Vector3> list = new List<Vector3>();
			if (nodes.Count > 0)
			{
				list = PathUtilities.GetPointsOnNodes(nodes, count, 1f, Globals.GetInstance().GetDungeonRandom().Range(0, int.MaxValue));
			}
			int layerMask = 1 << LayerMask.NameToLayer("Floor");
			foreach (Vector3 a in list)
			{
				RaycastHit raycastHit;
				if (Physics.Linecast(a + Vector3.up, a + Vector3.down, out raycastHit, layerMask) && raycastHit.collider.gameObject.layer == LayerMask.NameToLayer("Floor") && Vector3.Dot(raycastHit.normal, Vector3.up) >= 0f)
				{
					SpawnableItem random = weightedTable.GetRandom(Globals.GetInstance().GetDungeonRandom());
					if (random != null)
					{
						GameObject gameObject2 = random.gameObject;
						GameObject gameObject3 = UnityEngine.Object.Instantiate<GameObject>(gameObject2);
						gameObject3.transform.position = raycastHit.point + raycastHit.normal * 0.001f * (float)Globals.GetInstance().GetDungeonRandom().Range(1, 10);
						gameObject3.transform.rotation = Quaternion.LookRotation(Vector3.forward, raycastHit.normal);
						gameObject3.transform.Rotate(new Vector3(0f, Globals.GetInstance().GetDungeonRandom().Range(0f, 360f), 0f));
						gameObject3.transform.parent = Util.FindInParent<Room>(raycastHit.transform).transform;
					}
				}
			}
		}
		int num = this._rooms.Count * this.GetLevelType().decalsPerRoom;
		List<WallDecalPlaceholder> list2 = new List<WallDecalPlaceholder>(this._levelContent.GetComponentsInChildren<WallDecalPlaceholder>());
		if (this.GetLevelType().wallDecalPrefabs.Count > 0)
		{
			for (int i = 0; i < num; i++)
			{
				if (list2.Count == 0)
				{
					break;
				}
				WallDecalPlaceholder random2 = Util.GetRandom<WallDecalPlaceholder>(list2, Globals.GetInstance().GetDungeonRandom());
				list2.Remove(random2);
				random2.CreateDecal();
				UnityEngine.Object.Destroy(random2.gameObject);
			}
		}
		while (list2.Count > 0)
		{
			UnityEngine.Object.Destroy(list2[0].gameObject);
			list2.RemoveAt(0);
		}
	}

	public void SpawnObstacles()
	{
		if (this._rooms.Count > 0)
		{
			Room room = this._rooms[0];
			foreach (MonsterSpawnPoint monsterSpawnPoint in room.GetComponentsInChildren<MonsterSpawnPoint>(true))
			{
				monsterSpawnPoint.transform.parent = null;
				monsterSpawnPoint.gameObject.SetActive(false);
				UnityEngine.Object.Destroy(monsterSpawnPoint.gameObject);
			}
			foreach (ObstacleSpawnHint obstacleSpawnHint in room.GetComponentsInChildren<ObstacleSpawnHint>(true))
			{
				obstacleSpawnHint.transform.parent = null;
				obstacleSpawnHint.gameObject.SetActive(false);
				UnityEngine.Object.Destroy(obstacleSpawnHint.gameObject);
			}
		}
		List<ObstacleSpawnHint> list = new List<ObstacleSpawnHint>(this._levelContent.GetComponentsInChildren<ObstacleSpawnHint>(true));
		List<MonsterSpawnPoint> list2 = new List<MonsterSpawnPoint>(this._levelContent.GetComponentsInChildren<MonsterSpawnPoint>(true));
		list.Sort(new Comparison<ObstacleSpawnHint>(this.SortByIndex<ObstacleSpawnHint>));
		list2.Sort(new Comparison<MonsterSpawnPoint>(this.SortByIndex<MonsterSpawnPoint>));
		int num = Globals.GetInstance().GetDungeonRandom().Range(this._roomCount * 4, this._roomCount * 8);
		int num2 = Globals.GetInstance().GetDungeonRandom().Range(this._roomCount, this._roomCount * 3);
		for (int k = 0; k < num; k++)
		{
			if (list.Count <= 0)
			{
				break;
			}
			ObstacleSpawnHint obstacleSpawnHint2 = list[Globals.GetInstance().GetDungeonRandom().Range(0, list.Count)];
			this.currentRoom = Util.FindInParent<Room>(obstacleSpawnHint2.transform);
			obstacleSpawnHint2.Spawn();
			if (obstacleSpawnHint2.occupied)
			{
				list.Remove(obstacleSpawnHint2);
				obstacleSpawnHint2.transform.parent = null;
				UnityEngine.Object.Destroy(obstacleSpawnHint2.gameObject);
			}
			else
			{
				k--;
			}
			this.currentRoom = null;
		}
		for (int l = 0; l < num2; l++)
		{
			if (list2.Count <= 0)
			{
				break;
			}
			MonsterSpawnPoint monsterSpawnPoint2 = list2[Globals.GetInstance().GetDungeonRandom().Range(0, list2.Count)];
			this.currentRoom = Util.FindInParent<Room>(monsterSpawnPoint2.transform);
			monsterSpawnPoint2.Spawn();
			if (monsterSpawnPoint2.occupied)
			{
				this.spawnedEnemies = true;
				list2.Remove(monsterSpawnPoint2);
				monsterSpawnPoint2.transform.parent = null;
				UnityEngine.Object.Destroy(monsterSpawnPoint2.gameObject);
			}
			else
			{
				l--;
			}
			this.currentRoom = null;
		}
	}

	public void ShowJournalUpdateEntries()
	{
		if (Globals.GetInstance().GetDungeon() != null && Globals.GetInstance().GetDungeon().IsLevelStarted() && this._shouldShowJournals && QuestManager.GetInstance().journalUpdateEntries.Count > 0)
		{
			JournalUpdateEntry journalUpdateEntry = QuestManager.GetInstance().journalUpdateEntries[0];
			if (journalUpdateEntry.entryType == JournalUpdateEntry.JournalUpdateEntryType.QuestAdded || journalUpdateEntry.entryType == JournalUpdateEntry.JournalUpdateEntryType.QuestCompleted || journalUpdateEntry.entryType == JournalUpdateEntry.JournalUpdateEntryType.QuestFailed)
			{
				this.hud.questTracker.Stop();
				if (journalUpdateEntry.entryType == JournalUpdateEntry.JournalUpdateEntryType.QuestAdded)
				{
					this.hud.journalUpdate.Show("Quest Started", journalUpdateEntry.questState.GetQuest().questName, journalUpdateEntry.entryType);
					HUDAnimation animation = this.hud.journalUpdate.GetAnimation();
					animation.onAnimationEnd = (HUDAnimation.Callback)Delegate.Combine(animation.onAnimationEnd, new HUDAnimation.Callback(this.PopJournalUpdateEntry));
				}
				else if (journalUpdateEntry.entryType == JournalUpdateEntry.JournalUpdateEntryType.QuestCompleted)
				{
					this.hud.journalUpdate.Show("Quest Complete", journalUpdateEntry.questState.GetQuest().questName, journalUpdateEntry.entryType);
					HUDAnimation animation2 = this.hud.journalUpdate.GetAnimation();
					animation2.onAnimationEnd = (HUDAnimation.Callback)Delegate.Combine(animation2.onAnimationEnd, new HUDAnimation.Callback(this.PopJournalUpdateEntry));
				}
				else
				{
					this.hud.journalUpdate.Show("Quest Failed", journalUpdateEntry.questState.GetQuest().questName, journalUpdateEntry.entryType);
					HUDAnimation animation3 = this.hud.journalUpdate.GetAnimation();
					animation3.onAnimationEnd = (HUDAnimation.Callback)Delegate.Combine(animation3.onAnimationEnd, new HUDAnimation.Callback(this.PopJournalUpdateEntry));
				}
			}
			else if (journalUpdateEntry.entryType == JournalUpdateEntry.JournalUpdateEntryType.QuestUpdate)
			{
				this.hud.journalUpdate.Stop();
				this.hud.questTracker.Show(journalUpdateEntry.questState, journalUpdateEntry.entryType, new Globals.Callback(this.PopJournalUpdateEntry));
			}
			else if (journalUpdateEntry.entryType == JournalUpdateEntry.JournalUpdateEntryType.QuestStepComplete)
			{
				this.hud.journalUpdate.Stop();
				this.hud.questTracker.Show(journalUpdateEntry.questState, journalUpdateEntry.entryType, new Globals.Callback(this.PopJournalUpdateEntry));
			}
		}
	}

	public int SortByIndex<T>(T a, T b) where T : MonoBehaviour
	{
		return a.GetInstanceID().CompareTo(b.GetInstanceID());
	}

	public void PopJournalUpdateEntry()
	{
		if (QuestManager.GetInstance().journalUpdateEntries.Count > 0)
		{
			QuestManager.GetInstance().journalUpdateEntries.RemoveAt(0);
			this.ShowJournalUpdateEntries();
		}
	}

	public void PlacePlayer()
	{
		bool flag = false;
		if (Globals.GetInstance().GetPlayer().exitIdentifier != string.Empty)
		{
			foreach (AreaDoor areaDoor in UnityEngine.Object.FindObjectsOfType<AreaDoor>())
			{
				if (areaDoor.identifier == Globals.GetInstance().GetPlayer().exitIdentifier)
				{
					this._spawnPosition = areaDoor.GetSpawnPosition();
					this._spawnRotation = areaDoor.transform.rotation;
					flag = true;
					break;
				}
			}
		}
		if (!flag)
		{
			SpawnPoint[] array2 = UnityEngine.Object.FindObjectsOfType<SpawnPoint>();
			SpawnPoint spawnPoint = null;
			if (array2.Length > 0)
			{
				spawnPoint = array2[Globals.GetInstance().GetDungeonRandom().Range(0, array2.Length)];
			}
			if (spawnPoint != null)
			{
				this._spawnPosition = spawnPoint.transform.position;
				this._spawnRotation = spawnPoint.transform.rotation;
			}
			else if (this._entrance != null)
			{
				this._spawnPosition = this._entrance.transform.position;
				this._spawnRotation = this._entrance.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
			}
			else
			{
				Globals.GetInstance().GetPlayer().ReorientFromTransform();
			}
			Globals.GetInstance().GetPlayer().lastPosition = this._spawnPosition;
		}
		if (!Globals.GetInstance().loadSuccessful)
		{
			Globals.GetInstance().GetPlayer().transform.position = this._spawnPosition;
			Globals.GetInstance().GetPlayer().SetLookDirection(this._spawnRotation, true);
		}
		Globals.GetInstance().GetPlayer().SnapToFloor();
		Globals.GetInstance().GetPlayer().gameObject.SetActive(false);
	}

	public void OnDestroy()
	{
		if (this._registeredPlayer != null)
		{
			Player registeredPlayer = this._registeredPlayer;
			registeredPlayer.onDeath = (BaseCharacter.Callback)Delegate.Remove(registeredPlayer.onDeath, new BaseCharacter.Callback(this.OnPlayerDeath));
			this.hud.SetPlayer(null);
		}
		if (this._globals != null)
		{
			Globals globals = this._globals;
			globals.onPreSave = (Globals.Callback)Delegate.Remove(globals.onPreSave, new Globals.Callback(this.OnSave));
			Globals globals2 = this._globals;
			globals2.onLoadNewLevel = (Globals.Callback)Delegate.Remove(globals2.onLoadNewLevel, new Globals.Callback(this.OnSave));
		}
	}

	public IEnumerator YieldOnLongExecution()
	{
		if (this._lastTime - Time.realtimeSinceStartup > 0.01f)
		{
			yield return 0;
			this._lastTime = Time.realtimeSinceStartup;
		}
		yield break;
	}

	public virtual IEnumerator GenerateLayout()
	{
		Globals.GetInstance().GetDungeonRandom().Reseed();
		List<Room> floor_rooms = new List<Room>();
		Room entry_room = null;
		foreach (Quest quest in QuestManager.GetInstance().GetInProgressQuests())
		{
			if (QuestManager.GetInstance().GetQuestState(quest).GetCurrentStep().isFloorReplacement)
			{
				bool flag = true;
				foreach (string text in QuestManager.GetInstance().GetQuestState(quest).GetCurrentStep().floorReplacementTags.Split(new char[]
				{
					','
				}))
				{
					if (!this.GetLevelType().GetDungeonTags().Contains(text.Trim().ToLower()))
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					floor_rooms.AddRange(QuestManager.GetInstance().GetQuestState(quest).GetCurrentStep().questRooms);
					break;
				}
			}
		}
		if (floor_rooms.Count > 0)
		{
			RoomExit roomExit = null;
			List<Room> list = new List<Room>();
			foreach (Room item in floor_rooms)
			{
				list.Clear();
				list.Add(item);
				Room room = this.PlaceRoom(roomExit, list, true);
				room.isMainPath = true;
				if (room == null)
				{
					break;
				}
				if (entry_room == null)
				{
					entry_room = room;
				}
				if (this._entrance == null)
				{
					foreach (RoomExit roomExit2 in room.GetComponentsInChildren<RoomExit>(true))
					{
						if (roomExit2.overrideRoomEntrance)
						{
							this._entrance = roomExit2;
							break;
						}
					}
				}
				roomExit = null;
				foreach (RoomExit roomExit3 in room.GetComponentsInChildren<RoomExit>(true))
				{
					if (this._entrance == null)
					{
						this._entrance = roomExit3;
					}
					if (!(roomExit3.destination != null))
					{
						if (roomExit3 != this._entrance)
						{
							roomExit = roomExit3;
							break;
						}
					}
				}
				if (roomExit == null)
				{
					break;
				}
			}
			this._disableHunter = true;
		}
		else
		{
			if (this.GetLevelType().GetFloorType(this.GetDungeonLevel()) == null)
			{
				yield break;
			}
			this._layoutComplexity = this.GetLevelType().GetFloorComplexity();
			Debug.Log("Dungeon layout seed is: " + Globals.GetInstance().GetDungeonRandom().seed);
			Room room_override = null;
			room_override = this.GetLevelType().GetEntranceRoom();
			if (room_override != null)
			{
				List<Room> room_pool = new List<Room>();
				room_pool.Add(room_override);
				int old_seed = 0;
				if (Globals.GetInstance().IsInfiniteCooler())
				{
					Globals.GetInstance().GetDungeonRandom().Reseed();
					old_seed = Globals.GetInstance().GetDungeonRandom().seed;
					Globals.GetInstance().GetDungeonRandom().Reseed();
				}
				entry_room = this.PlaceRoom(null, room_pool, true);
				if (Globals.GetInstance().IsInfiniteCooler())
				{
					yield return 0;
					yield return 0;
					Globals.GetInstance().GetDungeonRandom().SetSeed(old_seed);
				}
			}
			else
			{
				entry_room = this.PlaceRoom(null, "!dead_end,!entrance,!exit,!stair,hallway,!cap", false);
			}
			SpawnPoint[] spawn_points = UnityEngine.Object.FindObjectsOfType<SpawnPoint>();
			room_override = null;
			if (spawn_points.Length <= 0)
			{
				int num = Globals.GetInstance().GetDungeonRandom().Range(0, this._openExits.Count);
				for (int m = 0; m < this._openExits.Count; m++)
				{
					if (this._openExits[num].mainPath)
					{
						this._entrance = this._openExits[num];
						break;
					}
					num += 1 % this._openExits.Count;
				}
				if (this._entrance == null)
				{
					Debug.LogError("No entrance.");
				}
				this.ConnectExits(this._entrance, null);
				this.MarkEntrance(this._entrance);
				this._openExits.Remove(this._entrance);
			}
			entry_room.isMainPath = true;
			Dictionary<Room, int> retry_attempts = new Dictionary<Room, int>();
			if (!this.GetLevelType().GetFloorType(this.GetDungeonLevel()).GetEntranceRoomOnly())
			{
				Debug.Log("Generating main path.");
				List<string> required_tags;
				if (entry_room.GetComponentsInChildren<RoomExit>(true).Length == 0)
				{
					Debug.LogError("No exit from the first room...");
				}
				else
				{
					this._roomCount = 0;
					while (this._roomCount < this._layoutComplexity)
					{
						if (this._rooms.Count == 0)
						{
							Debug.Log("No rooms can follow the first room, re-seeding and re-generating.");
							Globals.GetInstance().GetDungeonRandom().Reseed();
							for (int n = this._rooms.Count - 1; n >= 0; n--)
							{
								this.RemovePlacedRoom(this._rooms[n]);
							}
							if (this._saveables != null)
							{
								this._saveables.Clear();
							}
							if (this._registrationQueue != null)
							{
								this._registrationQueue.Clear();
							}
							yield return base.StartCoroutine(this.GenerateLayout());
							yield break;
						}
						yield return base.StartCoroutine(this.YieldOnLongExecution());
						Room last_room = this._rooms[this._rooms.Count - 1];
						if (retry_attempts.ContainsKey(last_room))
						{
							Dictionary<Room, int> dictionary;
							Room key;
							(dictionary = retry_attempts)[key = last_room] = dictionary[key] + 1;
						}
						else
						{
							retry_attempts[last_room] = 0;
						}
						List<RoomExit> exits = new List<RoomExit>(last_room.GetComponentsInChildren<RoomExit>(true));
						bool room_placed = false;
						if (retry_attempts[last_room] < 2)
						{
							int exit_index = 0;
							for (exit_index = 0; exit_index < exits.Count; exit_index++)
							{
								if (!exits[exit_index].mainPath || !this._openExits.Contains(exits[exit_index]))
								{
									exits.RemoveAt(exit_index);
									exit_index--;
								}
							}
							if (exits.Count > 0)
							{
								exit_index = Globals.GetInstance().GetDungeonRandom().Range(0, exits.Count);
								for (int a = 0; a < exits.Count; a++)
								{
									yield return base.StartCoroutine(this.YieldOnLongExecution());
									exit_index = (exit_index + 1) % exits.Count;
									RoomExit source_exit = exits[exit_index];
									required_tags = new List<string>();
									required_tags.Add("!entrance");
									required_tags.Add("!exit");
									required_tags.Add("!cap");
									if (source_exit.isInteriorExit)
									{
										required_tags.Add("!hallway");
										required_tags.Add("dead_end");
									}
									else
									{
										required_tags.Add("!dead_end");
										if (Globals.GetInstance().GetDungeonRandom().Range(0f, 1f) <= 0.8f)
										{
											if (source_exit.room.HasTag("hallway"))
											{
												required_tags.Add("!hallway");
											}
											else
											{
												required_tags.Add("hallway");
											}
										}
									}
									Room placed_room = this.PlaceRoom(source_exit, required_tags, true);
									if (placed_room != null)
									{
										if (!placed_room.HasTag("hallway"))
										{
											this._roomCount++;
										}
										room_placed = true;
										placed_room.isMainPath = true;
										break;
									}
								}
							}
						}
						if (!room_placed)
						{
							if (!last_room.HasTag("hallway"))
							{
								this._roomCount--;
							}
							if (retry_attempts.ContainsKey(last_room))
							{
								retry_attempts.Remove(last_room);
							}
							this.RemovePlacedRoom(last_room);
						}
					}
				}
				Debug.Log("Placing the exit.");
				required_tags = new List<string>();
				required_tags.Add("exit");
				room_override = this.GetLevelType().GetExitRoom();
				bool room_placed2 = false;
				while (this._rooms.Count >= 1 && !room_placed2)
				{
					yield return base.StartCoroutine(this.YieldOnLongExecution());
					Room last_room2 = this._rooms[this._rooms.Count - 1];
					List<RoomExit> exits2 = new List<RoomExit>(last_room2.GetComponentsInChildren<RoomExit>(true));
					room_placed2 = false;
					int exit_index2 = 0;
					for (exit_index2 = 0; exit_index2 < exits2.Count; exit_index2++)
					{
						if (!exits2[exit_index2].mainPath || !this._openExits.Contains(exits2[exit_index2]))
						{
							exits2.RemoveAt(exit_index2);
							exit_index2--;
						}
					}
					if (exits2.Count == 0)
					{
						this.RemovePlacedRoom(last_room2);
					}
					else
					{
						int random_index = Globals.GetInstance().GetDungeonRandom().Range(0, exits2.Count);
						for (int num2 = 0; num2 < exits2.Count; num2++)
						{
							Room room2;
							if (room_override != null)
							{
								List<Room> list2 = new List<Room>();
								list2.Add(room_override);
								room2 = this.PlaceRoom(exits2[random_index], list2, true);
							}
							else
							{
								room2 = this.PlaceRoom(exits2[random_index], required_tags, true);
							}
							if (room2 != null)
							{
								room_placed2 = true;
								room2.isMainPath = true;
								break;
							}
							random_index = (random_index + 1) % exits2.Count;
						}
						if (room_placed2)
						{
							break;
						}
						Debug.Log("Couldn't place room... (Layout)");
						this.RemovePlacedRoom(last_room2);
					}
				}
				room_override = null;
				Debug.Log("Creating extra rooms.");
				for (int i = 0; i < this._layoutComplexity * 2; i++)
				{
					yield return base.StartCoroutine(this.YieldOnLongExecution());
					RoomExit source_exit2 = Globals.GetInstance().GetDungeonRandom().GetRandom<RoomExit>(this._openExits);
					if (this._openExits.Count == 0)
					{
						break;
					}
					required_tags = new List<string>();
					required_tags.Add("!dead_end");
					required_tags.Add("!entrance");
					required_tags.Add("!exit");
					required_tags.Add("!cap");
					if (Globals.GetInstance().GetDungeonRandom().Range(0f, 1f) <= 0.8f)
					{
						if (source_exit2.room.HasTag("hallway"))
						{
							required_tags.Add("!hallway");
						}
						else
						{
							required_tags.Add("hallway");
						}
					}
					Room placed_room2 = this.PlaceRoom(source_exit2, required_tags, false);
					if (placed_room2 != null && !placed_room2.HasTag("hallway"))
					{
						this._roomCount++;
					}
				}
				List<RoomExit> open_exits = new List<RoomExit>(this._openExits);
				foreach (RoomExit roomExit4 in open_exits)
				{
					if (!(this.PlaceRoom(roomExit4, "dead_end", false) != null))
					{
						if (!roomExit4.room.HasTag("!hallway") || !(this.PlaceRoom(roomExit4, "cap", false) != null))
						{
							this.ConnectExits(roomExit4, null);
						}
					}
				}
			}
			this.TrimDeadEnds(entry_room);
		}
		foreach (Room room3 in this._rooms)
		{
			room3.processed = false;
		}
		if (this._entrance != null)
		{
			this.ProcessExit(this._entrance).doorName = "Entrance";
		}
		this.ProcessRoom(entry_room);
		yield break;
	}

	public void TrimDeadEnds(Room room)
	{
		if (room == null)
		{
			return;
		}
		room.processed = true;
		int num = 0;
		foreach (RoomExit roomExit in room.GetComponentsInChildren<RoomExit>())
		{
			if (roomExit.destination != null)
			{
				if (!roomExit.destination.room.processed)
				{
					this.TrimDeadEnds(roomExit.destination.room);
				}
				num++;
			}
		}
		if ((this._entrance == null || room != this._entrance.room) && room.HasTag("hallway") && num <= 1)
		{
			this.TrimDeadEnd(room);
		}
	}

	public void TrimDeadEnd(Room room)
	{
		if (room.isMainPath)
		{
			return;
		}
		if (!room.HasTag("hallway"))
		{
			return;
		}
		int num = 0;
		Room room2 = null;
		foreach (RoomExit roomExit in room.GetComponentsInChildren<RoomExit>())
		{
			if (roomExit.destination != null)
			{
				room2 = roomExit.destination.room;
				num++;
			}
		}
		if (num <= 1)
		{
			this.RemovePlacedRoom(room);
			if (room2 != null)
			{
				this.TrimDeadEnd(room2);
			}
		}
	}

	public void ProcessRoom(Room room)
	{
		room.processed = true;
		if (room.difficultyBonus > 0f)
		{
			List<ObstacleSpawnHint> list = new List<ObstacleSpawnHint>(room.GetComponentsInChildren<ObstacleSpawnHint>());
			int num = Mathf.FloorToInt(room.difficultyBonus);
			int num2 = Globals.GetInstance().GetDungeonRandom().Range(Mathf.Max(1, num / 2), num + 1);
			for (int i = 0; i < list.Count; i++)
			{
				ObstacleSpawnHint obstacleSpawnHint = list[i];
				if (obstacleSpawnHint.guaranteed)
				{
					obstacleSpawnHint.Spawn();
					list.RemoveAt(i);
					i--;
				}
			}
			for (int j = 0; j < num2; j++)
			{
				if (list.Count > 0)
				{
					ObstacleSpawnHint obstacleSpawnHint2 = list[Globals.GetInstance().GetDungeonRandom().Range(0, list.Count)];
					this.currentRoom = Util.FindInParent<Room>(obstacleSpawnHint2.transform);
					obstacleSpawnHint2.Spawn();
					if (obstacleSpawnHint2.occupied)
					{
						list.Remove(obstacleSpawnHint2);
						obstacleSpawnHint2.transform.parent = null;
						UnityEngine.Object.Destroy(obstacleSpawnHint2.gameObject);
					}
					this.currentRoom = null;
				}
			}
			num2 = Globals.GetInstance().GetDungeonRandom().Range(1, Mathf.Max(num, 1));
			List<MonsterSpawnPoint> list2 = new List<MonsterSpawnPoint>(room.GetComponentsInChildren<MonsterSpawnPoint>());
			if (this.GetLevelType().GetMonsterPool().Count > 0)
			{
				for (int k = 0; k < num2; k++)
				{
					if (list2.Count > 0)
					{
						MonsterSpawnPoint monsterSpawnPoint = list2[Globals.GetInstance().GetDungeonRandom().Range(0, list2.Count)];
						if (!monsterSpawnPoint.occupied)
						{
							this.currentRoom = Util.FindInParent<Room>(monsterSpawnPoint.transform);
							monsterSpawnPoint.Spawn();
							if (monsterSpawnPoint.occupied)
							{
								list2.Remove(monsterSpawnPoint);
								monsterSpawnPoint.transform.parent = null;
								UnityEngine.Object.Destroy(monsterSpawnPoint.gameObject);
							}
							else
							{
								k--;
							}
							this.currentRoom = null;
						}
					}
				}
			}
		}
		foreach (RoomExit roomExit in room.GetComponentsInChildren<RoomExit>())
		{
			if (roomExit != this._entrance && !roomExit.processed)
			{
				this.ProcessExit(roomExit);
				if (roomExit.destination != null && !roomExit.destination.room.processed)
				{
					this.ProcessRoom(roomExit.destination.room);
				}
			}
		}
	}

	public void Awake()
	{
		foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Destroy On Start"))
		{
			UnityEngine.Object.Destroy(obj);
		}
		this._visibleRooms = new List<Room>();
		this._loaded = false;
		this.loadedSaveData = new List<SaveableData>();
		Globals.GetInstance().SetDungeon(this);
		Globals.GetInstance().RemoveTimeScale("player_death");
		this.InitializeSaveableArea();
	}

	public virtual bool SpotOccupied(Vector3 position)
	{
		return this._occupiedSpaces.ContainsKey(this.GetGridPosition(position, true));
	}

	public void MarkSpot(Vector3 position, Room room)
	{
		this._occupiedSpaces[this.GetGridPosition(position, true)] = room;
	}

	public Room PlaceRoom(RoomExit source_exit, string required_context, bool main_path = false)
	{
		return this.PlaceRoom(source_exit, new List<string>(required_context.Split(new char[]
		{
			','
		}))
		{
			required_context
		}, main_path);
	}

	public void MarkEntrance(RoomExit exit)
	{
		this._roomEntrances[this.GetGridPosition(exit.GetExitPosition(), true)] = exit;
	}

	public void UnmarkEntrance(RoomExit exit)
	{
		this._roomEntrances.Remove(this.GetGridPosition(exit.GetExitPosition(), true));
	}

	public RoomExit GetSpotEntrance(Vector3 position)
	{
		Vector3 gridPosition = this.GetGridPosition(position, true);
		if (this._roomEntrances.ContainsKey(gridPosition))
		{
			return this._roomEntrances[gridPosition];
		}
		return null;
	}

	public Room PlaceRoom(RoomExit source_exit, List<Room> allowed_rooms, bool main_path = false)
	{
		allowed_rooms = new List<Room>(allowed_rooms);
		List<string> dungeonTags = this.GetLevelType().GetDungeonTags();
		for (int i = 0; i < allowed_rooms.Count; i++)
		{
			Room room = allowed_rooms[i];
			bool flag = false;
			if (room.requiredDungeonTags != string.Empty)
			{
				foreach (string text in room.requiredDungeonTags.Split(new char[]
				{
					','
				}))
				{
					if (text.Length != 0)
					{
						string text2 = text;
						bool flag2 = true;
						if (text2[0] == '!')
						{
							text2 = text2.Remove(0, 1);
							flag2 = false;
						}
						if (flag2 != dungeonTags.Contains(text.ToLower().Trim()))
						{
							allowed_rooms.RemoveAt(i);
							i--;
							flag = true;
							break;
						}
					}
				}
			}
			if (!flag)
			{
				if (room.requiredFlags != string.Empty)
				{
					foreach (string text3 in room.requiredFlags.Split(new char[]
					{
						','
					}))
					{
						if (!Globals.GetInstance().HasKey(text3.ToLower().Trim()))
						{
							allowed_rooms.RemoveAt(i);
							i--;
							flag = true;
							break;
						}
					}
				}
				if (!flag)
				{
					if (room.GetPrefab().maxInstanceCount > 0 && Dungeon._roomCounts.ContainsKey(room.GetPrefab()) && Dungeon._roomCounts.Count >= room.GetPrefab().maxInstanceCount)
					{
						allowed_rooms.RemoveAt(i);
						i--;
					}
				}
			}
		}
		int num = 0;
		if (source_exit != null)
		{
			num = source_exit.room.depth;
		}
		WeightedTable<Room> weightedTable = new WeightedTable<Room>();
		foreach (Room item in allowed_rooms)
		{
			weightedTable.AddItem(item);
		}
		Room random = weightedTable.GetRandom(Globals.GetInstance().GetDungeonRandom());
		while (weightedTable.Count > 0)
		{
			GameObject gameObject = random.gameObject;
			Vector3 vector = Vector3.zero;
			Vector3 eulerAngles = gameObject.transform.rotation.eulerAngles;
			Room component = gameObject.GetComponent<Room>();
			List<RoomExit> list = new List<RoomExit>(component.GetComponentsInChildren<RoomExit>(true));
			List<RoomExit> list2 = new List<RoomExit>(list);
			RoomExit roomExit = null;
			foreach (RoomExit roomExit2 in list2)
			{
				if (roomExit2.overrideRoomEntrance)
				{
					roomExit = roomExit2;
					break;
				}
			}
			for (int l = 0; l < list2.Count; l++)
			{
				RoomExit roomExit3 = list2[l];
				roomExit3.room = component;
				if (roomExit != null && roomExit3 != roomExit)
				{
					list2.RemoveAt(l);
					l--;
				}
				else if (source_exit != null && !roomExit3.CanConnect(source_exit))
				{
					list2.RemoveAt(l);
					l--;
				}
				else if (roomExit3.GetComponentInParent<LayoutRandomizer>() != null)
				{
					list2.RemoveAt(l);
					l--;
				}
			}
			roomExit = null;
			RoomExit roomExit4 = null;
			int num2 = Globals.GetInstance().GetDungeonRandom().Range(0, list2.Count);
			bool flag3 = false;
			Dictionary<RoomExit, RoomExit> dictionary = new Dictionary<RoomExit, RoomExit>();
			if (source_exit == null)
			{
				flag3 = true;
			}
			else if (list2.Count == 0)
			{
				flag3 = false;
			}
			else
			{
				for (int m = 0; m < list2.Count; m++)
				{
					roomExit4 = list2[num2];
					if (!roomExit4.isInteriorExit)
					{
						if (!roomExit4.notARoomEntrance)
						{
							if (!main_path || roomExit4.mainPath)
							{
								eulerAngles = new Vector3(0f, source_exit.transform.rotation.eulerAngles.y - (roomExit4.transform.rotation.eulerAngles.y + 180f), 0f);
								eulerAngles.x = Mathf.Round(eulerAngles.x / 90f) * 90f;
								eulerAngles.y = Mathf.Round(eulerAngles.y / 90f) * 90f;
								eulerAngles.z = Mathf.Round(eulerAngles.z / 90f) * 90f;
								vector = source_exit.GetExitPosition() - Quaternion.Euler(eulerAngles) * component.transform.InverseTransformPoint(roomExit4.transform.position);
								List<Vector3> occupiedSpaces = component.GetOccupiedSpaces(vector, Quaternion.Euler(eulerAngles));
								flag3 = true;
								foreach (Vector3 position in occupiedSpaces)
								{
									if (this.SpotOccupied(position))
									{
										flag3 = false;
										break;
									}
								}
								if (flag3)
								{
									foreach (RoomExit roomExit5 in list)
									{
										if (!(roomExit5 == roomExit4))
										{
											Vector3 position2 = vector + Quaternion.Euler(eulerAngles) * component.transform.InverseTransformPoint(roomExit5.transform.position);
											Vector3 position3 = vector + Quaternion.Euler(eulerAngles) * component.transform.InverseTransformPoint(roomExit5.GetExitPosition());
											if (this.SpotOccupied(position3))
											{
												RoomExit roomExit6 = null;
												foreach (RoomExit roomExit7 in this._openExits)
												{
													if (this.GetGridPosition(roomExit7.GetExitPosition(), false) == this.GetGridPosition(position2, false) && this.GetGridPosition(position3, false) == this.GetGridPosition(roomExit7.transform.position, false))
													{
														roomExit6 = roomExit7;
														break;
													}
												}
												if (roomExit6 != null)
												{
													dictionary[roomExit5] = roomExit6;
												}
												else
												{
													flag3 = false;
													dictionary.Clear();
												}
											}
											if (!flag3)
											{
												break;
											}
										}
									}
									if (flag3)
									{
										foreach (Vector3 position4 in occupiedSpaces)
										{
											RoomExit spotEntrance = this.GetSpotEntrance(position4);
											if (!(spotEntrance == null))
											{
												bool flag4 = false;
												foreach (RoomExit roomExit8 in list)
												{
													Vector3 position5 = vector + Quaternion.Euler(eulerAngles) * component.transform.InverseTransformPoint(roomExit8.transform.position);
													Vector3 position6 = vector + Quaternion.Euler(eulerAngles) * component.transform.InverseTransformPoint(roomExit8.GetExitPosition());
													if (this.GetGridPosition(spotEntrance.GetExitPosition(), false) == this.GetGridPosition(position5, false) && this.GetGridPosition(position6, false) == this.GetGridPosition(spotEntrance.transform.position, false))
													{
														if (roomExit8.CanConnect(spotEntrance))
														{
															flag4 = true;
														}
														break;
													}
												}
												if (!flag4)
												{
													flag3 = false;
													break;
												}
											}
										}
										if (flag3)
										{
											break;
										}
										num2 = (num2 + 1) % list2.Count;
										dictionary.Clear();
									}
								}
							}
						}
					}
				}
			}
			if (flag3)
			{
				gameObject = UnityEngine.Object.Instantiate<GameObject>(random.gameObject);
				gameObject.transform.parent = this._levelContent.transform;
				gameObject.transform.position = vector;
				gameObject.transform.rotation = Quaternion.Euler(eulerAngles);
				component = gameObject.GetComponent<Room>();
				component.prefab = random;
				foreach (LayoutRandomizer layoutRandomizer in gameObject.GetComponentsInChildren<LayoutRandomizer>(true))
				{
					layoutRandomizer.OnRandomizeLayout();
				}
				gameObject.transform.parent = this._levelContent.transform;
				component.prefab = random;
				component.exits = new List<RoomExit>(component.GetComponentsInChildren<RoomExit>());
				list = new List<RoomExit>(component.exits);
				foreach (RoomExit roomExit9 in dictionary.Keys)
				{
					RoomExit roomExit10 = dictionary[roomExit9];
					RoomExit component2 = Util.FindAnalogByIndex(roomExit9.transform, random.transform, component.transform).GetComponent<RoomExit>();
					this.ConnectExits(component2, roomExit10);
					list.Remove(component2);
					this._openExits.Remove(roomExit10);
				}
				if (roomExit4 != null)
				{
					RoomExit component3 = Util.FindAnalogByIndex(roomExit4.transform, random.transform, component.transform).GetComponent<RoomExit>();
					this.ConnectExits(component3, source_exit);
					this._openExits.Remove(source_exit);
					list.Remove(component3);
				}
				foreach (Vector3 position7 in component.GetOccupiedSpaces())
				{
					this.MarkSpot(position7, component);
				}
				foreach (RoomExit exit in list)
				{
					this.MarkEntrance(exit);
				}
				foreach (LayoutRandomizer layoutRandomizer2 in gameObject.GetComponentsInChildren<LayoutRandomizer>(true))
				{
					layoutRandomizer2.Finalize();
				}
				this._openExits.AddRange(list);
				component.gameObject.name = string.Concat(new object[]
				{
					"Room ",
					this._rooms.Count + 1,
					" (",
					random.name,
					")"
				});
				component.depth = num + 1;
				this._rooms.Add(component);
				if (!Dungeon._roomCounts.ContainsKey(component.GetPrefab()))
				{
					Dungeon._roomCounts[component.GetPrefab()] = 0;
				}
				Dungeon._roomCounts[component.GetPrefab()] = Dungeon._roomCounts[component.GetPrefab()] + 1;
				component.SetupSectors();
				return component;
			}
			weightedTable.RemoveItem(random);
			random = weightedTable.GetRandom(Globals.GetInstance().GetDungeonRandom());
		}
		return null;
	}

	public Room PlaceRoom(RoomExit source_exit, List<string> required_contexts, bool main_path = false)
	{
		List<Room> list = new List<Room>();
		List<Room> list2 = new List<Room>();
		list2.AddRange(this._roomPool);
		foreach (Room room in list2)
		{
			if (room.HasTag(required_contexts))
			{
				list.Add(room);
			}
		}
		if (list.Count == 0)
		{
			string text = string.Empty;
			foreach (string str in required_contexts)
			{
				text = text + str + " ";
			}
			Debug.Log("Warning: No rooms fit the criteria for tags: " + text);
		}
		return this.PlaceRoom(source_exit, list, main_path);
	}

	public void RemovePlacedRoom(Room room)
	{
		foreach (Vector3 position in room.GetOccupiedSpaces())
		{
			this._occupiedSpaces.Remove(this.GetGridPosition(position, true));
		}
		foreach (RoomExit roomExit in room.gameObject.GetComponentsInChildren<RoomExit>())
		{
			this.UnmarkEntrance(roomExit);
			if (roomExit.destination != null)
			{
				roomExit.destination.destination = null;
				if (!this._openExits.Contains(roomExit.destination))
				{
					this._openExits.Add(roomExit.destination);
				}
				else
				{
					Debug.LogError("Error: Tried to readd open exit to open exits.");
				}
			}
			if (this._openExits.Contains(roomExit))
			{
				this._openExits.Remove(roomExit);
			}
		}
		Dungeon._roomCounts[room.GetPrefab()] = Dungeon._roomCounts[room.GetPrefab()] - 1;
		this._rooms.Remove(room);
		room.gameObject.SetActive(false);
		UnityEngine.Object.Destroy(room.gameObject);
	}

	public void ConnectExits(RoomExit source_exit, RoomExit destination_exit)
	{
		if (source_exit != null && destination_exit != null && (source_exit.transform.position - destination_exit.GetExitPosition()).magnitude > 0.1f)
		{
			Debug.Log("Hole detected");
		}
		if (source_exit != null)
		{
			source_exit.destination = destination_exit;
		}
		if (destination_exit != null)
		{
			destination_exit.destination = source_exit;
		}
	}

	public virtual Door ProcessExit(RoomExit source_exit)
	{
		RoomExit destination = source_exit.destination;
		source_exit.processed = true;
		if (destination != null)
		{
			if (!source_exit.room.adjacentRooms.Contains(destination.room))
			{
				source_exit.room.adjacentRooms.Add(destination.room);
			}
			if (!destination.room.adjacentRooms.Contains(source_exit.room))
			{
				destination.room.adjacentRooms.Add(source_exit.room);
			}
			destination.processed = true;
			Room room = source_exit.room;
			Room room2 = destination.room;
			if (destination.room.depth < source_exit.room.depth)
			{
				room = destination.room;
				room2 = source_exit.room;
			}
			room2.lockTotal = room.lockTotal;
			room2.difficultyBonus = room.difficultyBonus;
			if ((source_exit.room.HasTag("hallway") && destination.room.HasTag("hallway")) || source_exit.room.HasTag("cap") || destination.room.HasTag("cap"))
			{
				if (source_exit.deadEndWall != null)
				{
					source_exit.deadEndWall.SetActive(false);
				}
				if (destination.deadEndWall != null)
				{
					destination.deadEndWall.SetActive(false);
				}
				if (source_exit.doorDecoration != null)
				{
					source_exit.doorDecoration.SetActive(true);
				}
				if (destination.doorDecoration != null)
				{
					destination.doorDecoration.SetActive(true);
				}
				GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(Resources.Load<GameObject>("Portal"));
				gameObject.transform.position = (source_exit.GetExitPosition() + source_exit.transform.position) / 2f;
				gameObject.transform.rotation = destination.transform.rotation;
				SECTR_Portal component = gameObject.GetComponent<SECTR_Portal>();
				component.BackSector = source_exit.room.GetComponent<SECTR_Sector>();
				component.FrontSector = destination.room.GetComponent<SECTR_Sector>();
				this.CombineRooms(source_exit.room, destination.room);
				return null;
			}
			if (Globals.GetInstance().GetDungeonRandom().Range(0f, 1f) < Mathf.Min(source_exit.roomCombineChance, destination.roomCombineChance))
			{
				if (source_exit.deadEndWall != null)
				{
					source_exit.deadEndWall.SetActive(false);
				}
				if (destination.deadEndWall != null)
				{
					destination.deadEndWall.SetActive(false);
				}
				if (source_exit.doorDecoration != null)
				{
					source_exit.doorDecoration.SetActive(true);
				}
				if (destination.doorDecoration != null)
				{
					destination.doorDecoration.SetActive(true);
				}
				GameObject gameObject2 = UnityEngine.Object.Instantiate<GameObject>(Resources.Load<GameObject>("Portal"));
				gameObject2.transform.position = (source_exit.GetExitPosition() + source_exit.transform.position) / 2f;
				gameObject2.transform.rotation = destination.transform.rotation;
				SECTR_Portal component2 = gameObject2.GetComponent<SECTR_Portal>();
				component2.BackSector = source_exit.room.GetComponent<SECTR_Sector>();
				component2.FrontSector = destination.room.GetComponent<SECTR_Sector>();
				this.CombineRooms(source_exit.room, destination.room);
				return null;
			}
		}
		if (source_exit != this._entrance)
		{
			if (destination == null && source_exit.deadEndWall != null)
			{
				source_exit.deadEndWall.gameObject.SetActive(true);
				if (source_exit.doorDecoration != null)
				{
					source_exit.doorDecoration.gameObject.SetActive(false);
				}
				return null;
			}
			if (source_exit.deadEndWall != null)
			{
				source_exit.deadEndWall.gameObject.SetActive(false);
			}
			if (source_exit.doorDecoration != null)
			{
				source_exit.doorDecoration.gameObject.SetActive(true);
			}
			if (destination != null)
			{
				if (destination.deadEndWall != null)
				{
					destination.deadEndWall.gameObject.SetActive(false);
				}
				if (destination.doorDecoration != null)
				{
					destination.doorDecoration.gameObject.SetActive(true);
				}
			}
		}
		GameObject gameObject3 = UnityEngine.Object.Instantiate<GameObject>(this.GetLevelType().doorPrefab, Vector3.zero, Quaternion.identity);
		gameObject3.transform.parent = this._levelContent.transform;
		gameObject3.transform.position = (source_exit.GetExitPosition() + source_exit.transform.position) / 2f;
		gameObject3.transform.rotation = source_exit.transform.rotation;
		Door componentInChildren = gameObject3.GetComponentInChildren<Door>();
		source_exit.door = componentInChildren;
		if (source_exit.destination != null)
		{
			source_exit.door = componentInChildren;
		}
		componentInChildren.Initialize(source_exit, destination);
		if (destination != null)
		{
			GameObject gameObject4 = UnityEngine.Object.Instantiate<GameObject>(Resources.Load<GameObject>("Portal Door"));
			gameObject4.transform.position = (source_exit.GetExitPosition() + source_exit.transform.position) / 2f;
			gameObject4.transform.rotation = destination.transform.rotation;
			SECTR_Portal component3 = gameObject4.GetComponent<SECTR_Portal>();
			component3.BackSector = source_exit.room.GetComponent<SECTR_Sector>();
			component3.FrontSector = destination.room.GetComponent<SECTR_Sector>();
			if (!source_exit.room.isMainPath || !destination.room.isMainPath)
			{
				int num = Mathf.Max(source_exit.room.depth, destination.room.depth);
				int num2 = Mathf.Min(source_exit.room.depth, destination.room.depth);
				int num3 = num - num2;
				Room room3 = source_exit.room;
				Room room4 = destination.room;
				if (destination.room.depth < source_exit.room.depth)
				{
					room3 = destination.room;
					room4 = source_exit.room;
				}
				if (room4.HasTag("!hallway"))
				{
					room4.difficultyBonus += Globals.GetInstance().offPathDifficultyBonus;
				}
				float num4 = Mathf.Lerp(0.75f, 0.25f, Mathf.Min((float)num2 / 10f, 1f));
				if (room3.lockTotal > 0)
				{
					num4 /= (float)room3.lockTotal;
				}
				if (Globals.GetInstance().GetDungeonRandom().Range(0f, 1f) <= num4)
				{
					int num5 = Globals.GetInstance().GetDungeonRandom().Range(5, 10 * Mathf.CeilToInt((float)num / 5f));
					if (num3 > 1)
					{
						num5 += Globals.GetInstance().GetDungeonRandom().Range(5, 16);
					}
					else
					{
						room4.difficultyBonus += (float)room3.lockTotal + (float)num5 / 5f;
					}
					componentInChildren.lockLevel = num5;
					room4.lockTotal++;
					room3.lockTotal++;
					room4.lockTotal++;
				}
			}
		}
		return componentInChildren;
	}

	public void CombineRooms(Room a, Room b)
	{
		a.connectedRooms.Add(b);
		b.connectedRooms.Add(a);
	}

	public LevelType GetLevelType()
	{
		if (this._levelType == null)
		{
			this._levelType = UnityEngine.Object.FindObjectOfType<LevelType>();
		}
		return this._levelType;
	}

	public void OnPlayerDeath()
	{
		Globals.GetInstance().deaths++;
		Globals.GetInstance().lastCharacterName = Globals.GetInstance().GetPlayer().characterName;
		Globals.GetInstance().SetAchievement("WASTED_1");
		base.StartCoroutine(this.GameOverSequence());
	}

	public IEnumerator GameOverSequence()
	{
		Globals.GetInstance().GetMainCamera().combatTextCamera.gameObject.SetActive(false);
		Globals.GetInstance().GetMainCamera().transform.parent = null;
		HOTween.Kill(Globals.GetInstance().GetMainCamera().radialBlur);
		Globals.GetInstance().GetMainCamera().radialBlur.blurStrength = 5f;
		HOTween.To(Globals.GetInstance().GetMainCamera().radialBlur, 0.25f, new TweenParms().Prop("blurStrength", 0f).Ease(EaseType.Linear));
		CameraOrbiter orbiter = Globals.GetInstance().GetMainCamera().gameObject.AddComponent<CameraOrbiter>();
		orbiter.target = Globals.GetInstance().GetPlayer().characterModel.head;
		yield return new WaitForSeconds(0.25f);
		float start_time = Time.realtimeSinceStartup;
		Globals.GetInstance().AddTimeScale("player_death", 0.25f);
		while (start_time + 0.25f > Time.realtimeSinceStartup)
		{
			yield return 0;
		}
		while (start_time + 1f > Time.realtimeSinceStartup)
		{
			yield return 0;
		}
		Globals.GetInstance().AddTimeScale("player_death_freeze", 0f);
		this.hud.deathText.text = Globals.GetInstance().GetPlayer().killText;
		Globals.GetInstance().old_buffs = Globals.GetInstance().GetPlayer().GetBuffs();
		this.hud.deathTextContainer.SetActive(true);
		Globals.GetInstance().PlayGlobalSound(this.hud.lootSound, "important_notification");
		while (start_time + 5f > Time.realtimeSinceStartup)
		{
			yield return 0;
		}
		this.hud.deathTextContainer.SetActive(false);
		Globals.GetInstance().RemoveTimeScale("player_death_freeze");
		while (start_time + 8f > Time.realtimeSinceStartup)
		{
			yield return 0;
		}
		if (Globals.GetInstance().useNarrator)
		{
			Narrator narrator = UnityEngine.Object.FindObjectOfType<Narrator>();
			if (narrator != null)
			{
				while (narrator.GetComponent<AudioSource>().isPlaying)
				{
					yield return 0;
				}
			}
		}
		Globals.GetInstance().RemoveTimeScale("player_death");
		UnityEngine.Object.Destroy(orbiter);
		Globals.GetInstance().GetMainCamera().combatTextCamera.gameObject.SetActive(true);
		if (Globals.GetInstance().IsInfiniteCooler())
		{
			Globals.GetInstance().EndInfiniteCooler();
			yield break;
		}
		Globals.GetInstance().LoadLevel("Home", null, true);
		yield break;
	}

	public BaseBuff GetLevelBuzz(BaseCharacter character, float random_value = -1f)
	{
		List<BaseBuff> list = new List<BaseBuff>();
		float num = 0f;
		foreach (BaseBuff baseBuff in Globals.GetInstance().gameBuffs)
		{
			if (baseBuff.buffType == BaseBuff.BuffType.Buzz)
			{
				float num2 = 1f;
				if (!Globals.GetInstance().IsInfiniteCooler() && character.GetBuffLevel(baseBuff) >= baseBuff.maxStacks && baseBuff.maxStacks > 0)
				{
					num2 = 0f;
				}
				if (num2 != 0f)
				{
					num += num2;
					list.Add(baseBuff);
				}
			}
		}
		if (this.currentRoom != null && this._spawnedBooze.ContainsKey(this.currentRoom) && this._spawnedBooze[this.currentRoom].Count < list.Count)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (this._spawnedBooze[this.currentRoom].Contains(list[i]))
				{
					num -= 1f;
					list.RemoveAt(i);
					i--;
				}
			}
		}
		float num3;
		if (random_value >= 0f)
		{
			num3 = Mathf.Lerp(0f, num, random_value);
		}
		else
		{
			num3 = UnityEngine.Random.Range(0f, num);
		}
		BaseBuff result = null;
		foreach (BaseBuff baseBuff2 in list)
		{
			float num4 = 1f;
			num3 -= num4;
			if (num3 <= 0f)
			{
				result = baseBuff2;
				break;
			}
		}
		return result;
	}

	public BaseBuff GetLevelBooze()
	{
		List<BaseBuff> list = new List<BaseBuff>();
		float num = 0f;
		foreach (BaseBuff baseBuff in Globals.GetInstance().gameBuffs)
		{
			if (baseBuff.buffType == BaseBuff.BuffType.Hangover)
			{
				float weightForLevel = baseBuff.GetWeightForLevel(this.GetCurrentDifficultyLevel(true, 1f));
				if (weightForLevel != 0f)
				{
					num += weightForLevel;
					list.Add(baseBuff);
				}
			}
		}
		if (this.currentRoom != null && this._spawnedBooze.ContainsKey(this.currentRoom) && this._spawnedBooze[this.currentRoom].Count < list.Count)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (this._spawnedBooze[this.currentRoom].Contains(list[i]))
				{
					num -= list[i].GetWeightForLevel(this.GetCurrentDifficultyLevel(true, 1f));
					list.RemoveAt(i);
					i--;
				}
			}
		}
		float num2 = Globals.GetInstance().GetDungeonRandom().Range(0f, num);
		BaseBuff baseBuff2 = null;
		foreach (BaseBuff baseBuff3 in list)
		{
			float weightForLevel2 = baseBuff3.GetWeightForLevel(this.GetCurrentDifficultyLevel(true, 1f));
			num2 -= weightForLevel2;
			if (num2 <= 0f)
			{
				baseBuff2 = baseBuff3;
				break;
			}
		}
		if (baseBuff2 != null && !this._spawnedBooze.ContainsKey(this.currentRoom))
		{
			this._spawnedBooze[this.currentRoom] = new List<BaseBuff>();
			this._spawnedBooze[this.currentRoom].Add(baseBuff2);
		}
		return baseBuff2;
	}

	public string GetLevelName(int level)
	{
		return this.GetLevelType().GetLevelName(level);
	}

	public string GetLevelName()
	{
		return this.GetLevelName(this.GetDungeonLevel());
	}

	public List<BaseItem> GetDungeonItems()
	{
		if (this._dungeonItems == null)
		{
			this._dungeonItems = new List<BaseItem>();
			foreach (BaseItem baseItem in Globals.GetInstance().GetItems())
			{
				if (baseItem.GetWeight(false) > 0f)
				{
					this._dungeonItems.Add(baseItem);
				}
			}
		}
		return this._dungeonItems;
	}

	public BaseItem GetRandomItem(string required_tags)
	{
		List<BaseItem> list;
		if (BaseItem.ignoreItemKeyRequirements)
		{
			list = new List<BaseItem>(Globals.GetInstance().GetItems());
		}
		else
		{
			list = new List<BaseItem>(this.GetDungeonItems());
		}
		list = Util.FilterTaggedList<BaseItem>(list, required_tags);
		WeightedTable<BaseItem> weightedTable = new WeightedTable<BaseItem>();
		foreach (BaseItem item in list)
		{
			weightedTable.AddItem(item);
		}
		return weightedTable.GetRandom(Globals.GetInstance().GetDungeonRandom());
	}

	public float GetCurrentRoomDifficulty()
	{
		if (this.currentRoom == null)
		{
			return 0f;
		}
		return this.currentRoom.GetDifficultyBonus();
	}

	public float GetCurrentDifficultyLevel(bool buff_level = false, float bonus_multiplier = 1f)
	{
		float num = (float)this.GetDungeonLevel();
		if (buff_level)
		{
			num *= this.GetLevelType().buffLevelMultiplier;
		}
		if (this.currentRoom != null)
		{
			num += this.currentRoom.difficultyBonus * bonus_multiplier;
		}
		return num;
	}

	public bool IsLevelStarted()
	{
		return this._levelStarted;
	}

	public bool IsLevelInitialized()
	{
		return this._levelInitialized;
	}

	public bool DoesAINeedsThrottle()
	{
		return this.activeAI > 6;
	}

	public int GetDungeonLevel()
	{
		if (this._dungeonLevel < 0)
		{
			this._dungeonLevel = Globals.GetInstance().level;
		}
		return this._dungeonLevel;
	}

	public string GetSpace()
	{
		return this._currentSpace;
	}

	public void SetSpace(string new_space)
	{
		if (new_space != this._currentSpace)
		{
			this._currentSpace = new_space;
			if (this.onSpaceChanged != null)
			{
				this.onSpaceChanged();
			}
		}
	}

	public void AddLevelStartCallback(Dungeon.Callback callback)
	{
		if (this.IsLevelStarted())
		{
			callback();
		}
		else
		{
			this.onLevelStart = (Dungeon.Callback)Delegate.Combine(this.onLevelStart, callback);
		}
	}

	public void AddLevelInitializeCallback(Dungeon.Callback callback)
	{
		if (this.IsLevelInitialized())
		{
			callback();
		}
		else
		{
			this.onLevelInitialize = (Dungeon.Callback)Delegate.Combine(this.onLevelInitialize, callback);
		}
	}

	public List<AINode> GetAINodes()
	{
		if (this._aiNodes == null)
		{
			this._aiNodes = new List<AINode>(UnityEngine.Object.FindObjectsOfType<AINode>());
		}
		return this._aiNodes;
	}

	public AudioClip GetMusic()
	{
		return this.levelMusic;
	}

	public void SetLevelMusic(AudioClip clip)
	{
		this.levelMusic = clip;
		LevelMusicSource levelMusicSource = UnityEngine.Object.FindObjectOfType<LevelMusicSource>();
		if (levelMusicSource != null)
		{
			levelMusicSource.UpdateMusic();
		}
	}

	public List<Saveable> GetSaveables()
	{
		if (this._saveables == null)
		{
			this._saveables = new List<Saveable>();
		}
		return this._saveables;
	}

	public void AddToSaveables(Saveable saveable)
	{
		if (this._saveables == null)
		{
			this._saveables = new List<Saveable>();
		}
		if (!this.IsSaveableValid(saveable))
		{
			return;
		}
		if (!this._saveables.Contains(saveable))
		{
			this._saveables.Add(saveable);
		}
	}

	public void RemoveFromSaveables(Saveable saveable)
	{
		if (this._saveables == null)
		{
			this._saveables = new List<Saveable>();
		}
		this._saveables.Remove(saveable);
	}

	public bool IsSaveableValid(Saveable saveable)
	{
		return true;
	}

	public void RegisterSaveable(Saveable saveable)
	{
		if (this._saveableAreaInitialized)
		{
			saveable.Initialize(this);
		}
		else
		{
			if (this._registrationQueue == null)
			{
				this._registrationQueue = new List<Saveable>();
			}
			this._registrationQueue.Add(saveable);
		}
	}

	public void InitializeSaveableArea()
	{
		this._saveableAreaName = this.GetLevelType().GetSaveableAreaName(this.GetDungeonLevel());
		if (this._saveableAreaName == string.Empty)
		{
			this._isSaveableArea = false;
			this._saveableAreaName = "limbo";
		}
		else
		{
			this._isSaveableArea = true;
		}
		this._globals = Globals.GetInstance();
		this._areaSaveData = this._globals.GetAreaSaveData(this.GetSaveableAreaName());
	}

	protected void _RegisterSaveables()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		if (this._isSaveableArea)
		{
			foreach (Saveable saveable in Util.FindAllObjectsOfType<Saveable>())
			{
				if (saveable.identifierName == string.Empty)
				{
					if (!dictionary.ContainsKey(saveable.gameObject.name))
					{
						dictionary[saveable.gameObject.name] = 0;
					}
					if (!saveable.IsInitialized())
					{
						saveable.identifierName = string.Concat(new object[]
						{
							this.GetSaveableAreaName(),
							"-",
							saveable.gameObject.name,
							"-",
							dictionary[saveable.gameObject.name]
						});
						Dictionary<string, int> dictionary2;
						string name;
						(dictionary2 = dictionary)[name = saveable.gameObject.name] = dictionary2[name] + 1;
					}
				}
			}
		}
		if (this._registrationQueue != null)
		{
			foreach (Saveable saveable2 in this._registrationQueue)
			{
				if (!(saveable2 == null))
				{
					saveable2.Initialize(this);
				}
			}
			this._registrationQueue.Clear();
		}
		this._saveableAreaInitialized = true;
	}

	public void OnApplicationQuit()
	{
		Globals.overrideQuitInstanceLock = true;
		if (Globals.GetInstance().CanSave())
		{
			Globals.GetInstance().SaveGame();
		}
		Globals.overrideQuitInstanceLock = false;
	}

	public void OnSave()
	{
		if (this._saveables == null)
		{
			return;
		}
		foreach (Saveable saveable in this._saveables)
		{
			if (this._isSaveableArea || saveable.identifierName != string.Empty)
			{
				saveable.GetSaveData();
				saveable.SerializeData();
			}
		}
	}

	public void OnLoad()
	{
		this._globals.isDeserializing = true;
		if (this._isSaveableArea && this._globals.areaSaveData.ContainsKey(this.GetSaveableAreaName()))
		{
			List<SaveableData> list = new List<SaveableData>(this._areaSaveData.data);
			foreach (SaveableData saveableData in list)
			{
				if (!saveableData.destroyed)
				{
					saveableData.Deserialize(this);
				}
			}
		}
		if (this._saveables != null)
		{
			for (int i = 0; i < this._saveables.Count; i++)
			{
				Saveable saveable = this._saveables[i];
				if (saveable.bringSaveableToCurrentArea)
				{
					saveable.GetSaveData().Deserialize(this);
				}
			}
		}
		this._globals.isDeserializing = false;
		this._loaded = true;
	}

	public Saveable GetSaveableByIdentifier(string identifier)
	{
		if (this._saveables != null)
		{
			foreach (Saveable saveable in this._saveables)
			{
				if (saveable.identifierName == identifier)
				{
					return saveable;
				}
			}
		}
		if (Globals.GetInstance().saveData.ContainsKey(identifier))
		{
			SaveableData saveableData = Globals.GetInstance().saveData[identifier];
			Saveable saveable2 = saveableData.Deserialize(this);
			if (saveable2 != null)
			{
				saveable2.StartGhosting();
				return saveable2;
			}
		}
		return null;
	}

	public string GetSaveableAreaName()
	{
		return this._saveableAreaName;
	}

	public bool IsSaveableArea()
	{
		return this._isSaveableArea;
	}

	public Dungeon.Callback onLevelStart;

	public Dungeon.Callback onLevelInitialize;

	public Dungeon.Callback onLightsChanged;

	public Color lightColor;

	protected Color _cachedLightColor;

	public static float gridUnit = 6f;

	protected List<RoomExit> _openExits;

	public HUD hud;

	public ScriptedSequenceTopicHandler scriptedSequenceTopicHandler;

	public ConversationTopicHandler conversationTopicHandler;

	public ComputerTopicHandler computerTopicHandler;

	public CharacterCreator characterCreator;

	protected Dictionary<Vector3, Room> _occupiedSpaces;

	protected Dictionary<Vector3, RoomExit> _roomEntrances;

	protected List<Room> _rooms;

	protected BaseCharacter _hunterInstance;

	protected int _dungeonLevel = -1;

	public Bounds sceneBounds;

	public float timeUntilHunted = 3f;

	protected RoomExit _entrance;

	public AudioSource hunterMusic;

	public float musicBalance = 1f;

	public bool quickTest;

	public Player playerPrefab;

	public IngameMenu ingameMenu;

	protected LevelType _levelType;

	protected bool _levelStarted;

	protected bool _levelInitialized;

	public Room currentRoom;

	protected int _layoutComplexity;

	protected GameObject _levelContent;

	public Room cameraContainingRoom;

	public AudioSource ambience;

	public VibrationTemplate explosionVibration;

	protected Dictionary<Room, List<BaseBuff>> _spawnedBooze;

	public int activeAI;

	protected string _currentSpace = string.Empty;

	public Dungeon.Callback onSpaceChanged;

	public int state;

	protected static Dictionary<Room, int> _roomCounts;

	protected List<Room> _roomPool;

	protected Player _registeredPlayer;

	protected float _levelBottomY;

	protected List<BaseItem> _dungeonItems;

	protected bool _waitForFixedUpdate;

	protected Dungeon.Callback _waitForFixedUpdateCallback;

	protected bool _waitForUpdate;

	protected Dungeon.Callback _waitForUpdateCallback;

	protected Vector3 _spawnPosition;

	protected Quaternion _spawnRotation;

	protected int _roomCount;

	protected List<AINode> _aiNodes;

	public AudioClip levelMusic;

	protected bool _shouldShowJournals;

	protected bool _allowScriptedThink;

	public static bool isInControlledScene;

	protected int _hunterWarningLevel;

	protected bool _saveableAreaInitialized;

	protected List<Saveable> _saveables;

	protected AreaSaveData _areaSaveData;

	protected List<Saveable> _registrationQueue;

	protected Globals _globals;

	protected bool _isSaveableArea;

	protected string _saveableAreaName = string.Empty;

	public int levelLoadBlockers;

	protected bool _loaded;

	public List<SaveableData> loadedSaveData;

	protected bool _disableHunter;

	protected List<CharacterInteractPoint> _characterInteractPoints;

	public List<BaseCullable> _cullables;

	public int dungeonSeed = -1;

	protected bool _variantCalledOut;

	public int alerts;

	public bool spawnedEnemies;

	protected Cutscene _startCutscene;

	protected Camera _mainCamera;

	public List<Room> _visibleRooms;

	protected float _lastTime;

	public delegate void Callback();
}
