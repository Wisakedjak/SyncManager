using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using ZTDEnums;
using ZTDModels;
using Random = UnityEngine.Random;

public class SyncManager : MonoBehaviour
{
    // Create a field for the save file.
    private static string _saveFile;
    private const string ApiUrl = ""; //Enter Api Url
    //private const string ApiUrl = "http://192.168.1.63:8000";

    public static List<ShopItem> InTDPowerUps = new List<ShopItem>();
    private static readonly TableEnum[] levelGroupTableEnums = new[]
    {
        TableEnum.Chapter,
        TableEnum.Level,
        TableEnum.Wave,
        TableEnum.WavePart,
        TableEnum.LevelChestChance,
        TableEnum.LevelGift
    };

    private static readonly TableEnum[] towerGroupTableEnums = new[]
    {
        TableEnum.TowerLevel,
        TableEnum.Tower
    };

    private static readonly TableEnum[] enemyGroupTableEnums = new[]
    {
        TableEnum.Enemy,
        TableEnum.EnemyLevel
    };

    private static readonly TableEnum[] chestGroupTableEnums = new[]
    {
        TableEnum.Chest,
        TableEnum.ChestType
    };

    private static readonly TableEnum[] itemGroupTableEnums = new[]
    {
        TableEnum.Item
    };

    private static readonly TableEnum[] researchGroupTableEnums = new[]
    {
        TableEnum.ResearchNode,
        TableEnum.ResearchNodeLevel,
        TableEnum.ResearchNodeLevelCondition
    };

    private static readonly TableEnum[] dialogGroupTableEnums = new[]
    {
        TableEnum.Dialog
    };

    public static SyncManager Instance;
    private static List<TableEnum> nonSyncTableEnums = new List<TableEnum>();
    public bool isOnline = false;
    [HideInInspector] public UnityEvent<bool> connectionChanged = new UnityEvent<bool>();

    #region PRIVATE EVENTS

    private readonly UnityEvent<TDResponse<AuthenticateResponse>> _loginEvent =
        new UnityEvent<TDResponse<AuthenticateResponse>>();

    private readonly UnityEvent<TDResponse<ChapterInfoDTO>> _chapterInfoEvent =
        new UnityEvent<TDResponse<ChapterInfoDTO>>();

    private readonly UnityEvent<TDResponse<List<TableChangesDTO>>> _syncStatusEvent =
        new UnityEvent<TDResponse<List<TableChangesDTO>>>();

    private readonly UnityEvent<TDResponse<List<LevelDTO>>> _getLevelsEvent =
        new UnityEvent<TDResponse<List<LevelDTO>>>();

    private readonly UnityEvent<TDResponse<List<TowerDTO>>> _getTowersEvent =
        new UnityEvent<TDResponse<List<TowerDTO>>>();

    private readonly UnityEvent<TDResponse<List<EnemyDTO>>> _getEnemyListEvent =
        new UnityEvent<TDResponse<List<EnemyDTO>>>();

    private readonly UnityEvent<TDResponse> _addProgressListEvent =
        new UnityEvent<TDResponse>();

    private readonly UnityEvent<TDResponse<List<ItemDTO>>> _getItemsEvent =
       new UnityEvent<TDResponse<List<ItemDTO>>>();

    private readonly UnityEvent<TDResponse<List<ChestTypeDTO>>> _getChestTypesEvent =
       new UnityEvent<TDResponse<List<ChestTypeDTO>>>();

    private readonly UnityEvent<TDResponse<List<PlayerChestDTO>>> _getPlayerChestsEvent =
       new UnityEvent<TDResponse<List<PlayerChestDTO>>>();

    private readonly UnityEvent<TDResponse<List<PlayerChestDTO>>> _setPlayerChestsEvent =
       new UnityEvent<TDResponse<List<PlayerChestDTO>>>();

    private readonly UnityEvent<TDResponse<PlayerVariableDTO>> _getPlayerVariableEvent =
       new UnityEvent<TDResponse<PlayerVariableDTO>>();

    private readonly UnityEvent<TDResponse<PlayerVariableDTO>> _setPlayerVariableEvent =
       new UnityEvent<TDResponse<PlayerVariableDTO>>();

    private readonly UnityEvent<TDResponse<List<PlayerItemDTO>>> _getPlayerItemEvent =
       new UnityEvent<TDResponse<List<PlayerItemDTO>>>();

    private readonly UnityEvent<TDResponse<List<PlayerItemDTO>>> _setPlayerItemEvent =
       new UnityEvent<TDResponse<List<PlayerItemDTO>>>();

    private readonly UnityEvent<TDResponse<List<ResearchNodeDTO>>> _getResearchNodeEvent =
       new UnityEvent<TDResponse<List<ResearchNodeDTO>>>();

    private readonly UnityEvent<TDResponse<List<DialogSceneDTO>>> _getDialogScenesEvent =
       new UnityEvent<TDResponse<List<DialogSceneDTO>>>();

    private readonly UnityEvent<TDResponse<List<PlayerResearchNodeLevelDTO>>> _getPlayerResearchNodeLevelsEvent =
       new UnityEvent<TDResponse<List<PlayerResearchNodeLevelDTO>>>();

    private readonly UnityEvent<TDResponse<List<PlayerResearchNodeLevelDTO>>> _setPlayerResearchNodeLevelsEvent =
        new UnityEvent<TDResponse<List<PlayerResearchNodeLevelDTO>>>();
    #endregion


    private LocalGameData _save = new LocalGameData();
    private int _playerChestFakeId = -1;

    public static List<DateTime> RewardedGemHistory
    {
        get => Instance._save.RewardedGemHistory;
        set => Instance._save.RewardedGemHistory = value;
    }

    public string saveJson = "";
    public static int Difficulty = 1;

    private static int _currentLevel = 1;

    public static void TutorialDone()
    {
        Instance._save.TutorialIsDone = true;
        WriteFile();
    }

    public static bool IsTutorialDone()
    {
        return Instance._save.TutorialIsDone;
    }
    public static void SetLevel(int level)
    {
        _currentLevel = level;
    }
    public static List<WaveDTO> GetAllWaves()
    {
        return Instance._save.Levels.Where(l => l.Id != 9999).OrderBy(L => L.Difficulty).ThenBy(l => l.OrderId).
            SelectMany(l => l.Waves.OrderBy(k => k.OrderId)).ToList();
    }

    public static List<TDTowerInfoOnTowerBaseModel> GetTowerInfoOnTowerBases()
    {
        var temp = Instance._save.TdTowerInfoOnTowerBases;
        // Instance._save.TdTowerInfoOnTowerBases.Clear();
        // WriteFile();
        return temp;
    }
    public static void SetTowerInfoOnTowerBases(List<TDTowerInfoOnTowerBaseModel> _td)
    {
        Instance._save.TdTowerInfoOnTowerBases = _td;
        WriteFile();
    }
    public static EndlessInfoDTO GetEndlessInfo()
    {
        var temp = Instance._save.EndlessInfoDto;
        // Instance._save.EndlessInfoDto = null;
        // WriteFile();
        return temp;
    }
    public static void SetEndlessInfo(EndlessInfoDTO _endless)
    {
        Instance._save.EndlessInfoDto = _endless;
        WriteFile();
    }


    public static void AchievementPlus(AchievementsEnum en, int count = 1)
    {

        try
        {
            Instance._save.AchievementsValues[(int)en] += count;

        }
        catch (Exception e)
        {
            Instance._save.AchievementsValues.Add((int)en, count);

        }
        WriteFile();
    }

    public static Dictionary<int, int> GetAchievemts()
    {
        return Instance._save.AchievementsValues;
    }

    public static int GetUserStarCount(int chapterId)
    {
        return Instance._save.Levels.Where(l => l.ChapterId == chapterId).Select(l => l.UserStar).Sum();
    }
    public static int GetUserStarCount()
    {
        return Instance._save.Levels.Select(l => l.UserStar).Sum();
    }

    public static void SetLevelByDifficulty(int difficulty)
    {
        Difficulty = difficulty;
        var cc = Instance._save.Levels.First(l => l.Id == _currentLevel);
        _currentLevel = Instance._save.Levels.First(l => l.OrderId == cc.OrderId && l.Difficulty == Difficulty).Id;
    }
    public static int GetScene()
    {
        return Instance._save.Levels.FirstOrDefault(l => l.Id == _currentLevel)?.SceneId ?? 1;
    }

    public static int GetGemCount()
    {
        return Instance._save.PlayerVariable?.GemCount ?? 0;
    }

    public static void EarnGem(int value)
    {
        if (Instance._save.PlayerVariable == null)
        {
            Instance._save.PlayerVariable = new PlayerVariableDTO()
            {
                GemCount = value,
                UserId = -3,
                ResearchPoint = 0
            };
            WriteFile();
            return;
        }

        Instance._save.PlayerVariable.GemCount += value;
        WriteFile();

    }

    public static int GetShopItemCount(ShopItemTypeEnum shopItemTypeEnum)
    {

        return Instance._save.PlayerShopItems.FirstOrDefault(l => l.Id == (int)shopItemTypeEnum)?.Count ?? 0;
    }

    public static void ShopItemCountChange(ShopItemTypeEnum shopItemTypeEnum, int value, int gemPrice = 0)
    {
        if (GetGemCount() < gemPrice * value)
        {
            return;
        }

        Instance._save.PlayerVariable.GemCount -= (gemPrice * value);
        AnalyticsInitializer.SendGemSpend(gemPrice * value, "Shop", Enum.GetName(typeof(ShopItemTypeEnum), shopItemTypeEnum));
        if (Instance._save.PlayerShopItems.FirstOrDefault(l => l.Id == (int)shopItemTypeEnum) != null)
        {
            Instance._save.PlayerShopItems.FirstOrDefault(l => l.Id == (int)shopItemTypeEnum)!.Count += value;
        }
        else if (value > 0)
        {
            Instance._save.PlayerShopItems.Add(new ShopItem()
            {
                Count = value,
                Id = (int)shopItemTypeEnum
            });
        }

        WriteFile();

    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

        }
        else if (Instance != null)
        {
            Destroy(this);
        }
        _saveFile = Application.persistentDataPath + "/DbSave.json";

        #region ADD LISTENERS
        _loginEvent.AddListener(OnLoginDone);
        connectionChanged.AddListener(OnConnectionChanged);
        _syncStatusEvent.AddListener(OnGetSyncStatus);
        _chapterInfoEvent.AddListener(OnGetChapterInfo);
        _getLevelsEvent.AddListener(OnGetLevels);
        _getTowersEvent.AddListener(OnGetTowers);
        _getEnemyListEvent.AddListener(OnGetEnemyList);
        _addProgressListEvent.AddListener(OnAddProgressList);
        _getItemsEvent.AddListener(OnGetItems);
        _getChestTypesEvent.AddListener(OnGetChestTypes);
        _getPlayerChestsEvent.AddListener(OnGetPlayerChests);
        _setPlayerChestsEvent.AddListener(OnSetPlayerChests);
        _getPlayerVariableEvent.AddListener(OnGetPlayerVariable);
        _setPlayerVariableEvent.AddListener(OnSetPlayerVariable);
        _getPlayerItemEvent.AddListener(OnGetPlayerItems);
        _setPlayerItemEvent.AddListener(OnSetPlayerItems);
        _getResearchNodeEvent.AddListener(OnGetResearchNodes);
        _getPlayerResearchNodeLevelsEvent.AddListener(OnGetPlayerResearchNodeLevels);
        _setPlayerResearchNodeLevelsEvent.AddListener(OnSetPlayerResearchNodeLevels);
        _getDialogScenesEvent.AddListener(OnGetDialogScenes);
        #endregion

    }

    void Start()
    {
        ReadFile();
        SetFakePlayerChestId();
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            connectionChanged.Invoke(false);

            if (_save.TestVariable == "None")
            {
                //todo: en az bir defa internete baglanmalisin!!!!!
                print("en az bir defa internete baglanmalisin!!!!!");
            }
        }
        else
        {
            _save.TestVariable = DateTimeOffset.UtcNow.ToString();
            connectionChanged.Invoke(true);
        }

        StartCoroutine(TestFunction());
        StartCoroutine(_isTutorialDoneCoroutine());
    }


    public void Login()
    {
        StartCoroutine(LoginWithDeviceID(_loginEvent));
    }

    IEnumerator _isTutorialDoneCoroutine()
    {
        yield return new WaitForSeconds(3f);
        if (IsTutorialDone())
        {
            SceneManager.LoadScene("GamePlay");
        }
        else
        {
            SceneManager.LoadScene("TutorialTD");
        }

    }

    public IEnumerator TestFunction()
    {
        yield return new WaitForSeconds(3);
        saveJson = JsonConvert.SerializeObject(_save);
        StartCoroutine(TestFunction());
    }


    private static void ReadFile()
    {
        // Does the file exist?
        if (File.Exists(_saveFile) && Instance._save != null)
        {
            // Read the entire file and save its contents.
            string fileContents = File.ReadAllText(_saveFile);

            // Deserialize the JSON data 
            //  into a pattern matching the GameData class.
            Instance._save = JsonConvert.DeserializeObject<LocalGameData>(fileContents) ?? new LocalGameData();
        }
        else
        {
            Instance._save = new LocalGameData();
            WriteFile();
        }
    }

    private void SetFakePlayerChestId()
    {
        if (Instance._save.PlayerChests == null || Instance._save.PlayerChests.Count == 0)
        {
            return;
        }
        Instance._playerChestFakeId = Instance._save.PlayerChests.Min(l => l.Id) - 1;
    }

    private static void WriteFile()
    {
        string jsonString = JsonConvert.SerializeObject(Instance._save ?? new LocalGameData());

        File.WriteAllText(_saveFile, jsonString);

    }

    public static void ManualSave()
    {
        WriteFile();
    }
    public static ChapterInfoDTO GetChapterInfo()
    {
        var r = Instance._save.ChapterInfo;
        r.Levels = r.Levels.Where(l => l.OrderId != 9999 && l.Difficulty == 1).OrderBy(l => l.OrderId).ToList();
        return r;
    }

    public static LevelDTO GetLevelById(int levelId)
    {
        var cc = Instance._save.Levels.First(l => l.Id == levelId);
        return Instance._save.Levels.First(l => l.OrderId == cc.OrderId && l.Difficulty == Difficulty);
    }

    public static LevelDTO GetTutorialLevel()
    {
        return Instance._save.Levels.First(l => l.Id == 9999);
    }

    public static LevelDTO GetNextLevel()
    {
        var cc = Instance._save.Levels.First(l => l.Id == _currentLevel);
        return Instance._save.Levels.First(l => l.OrderId == cc.OrderId && l.Difficulty == Difficulty);
    }

    public static List<LevelDTO> GetLevelsByOrderId(int orderId)
    {
        return Instance._save.Levels.Where(l => l.OrderId == orderId).OrderBy(l => l.Difficulty).ToList();
    }

    public static List<TowerDTO> GetTowers()
    {
        return Instance._save.Towers;
    }

    public static EnemyDetailDTO? GetEnemyLevelById(int enemyLevelId)
    {
        return Instance._save.EnemyList
            .SelectMany(l => l.EnemyDetails)
            .FirstOrDefault(l => l.EnemyLevelId == enemyLevelId);
    }

    public static int? GetEnemyIdByEnemyLevelId(int enemyLevelId)
    {
        return SyncManager.GetEnemyList()
            .FirstOrDefault(l => l.EnemyDetails.Any(k => k.EnemyLevelId == enemyLevelId))?.Id;
    }
    public static List<EnemyDTO> GetEnemyList()
    {
        return Instance._save.EnemyList;
    }
    public static List<ResearchNodeDTO> GetResearchList()
    {
        return Instance._save.ResearchNodes;
    }

    public static List<PlayerResearchNodeLevelDTO> GetPlayerResearchNodeLevels()
    {
        return Instance._save.PlayerResearchNodes;
    }

    public static PlayerResearchNodeLevelDTO GetPlayerResearchNodeLevelByResearchId(int researchId)
    {
        var xx = Instance._save.ResearchNodes.FirstOrDefault(l => l.Id == researchId)?.ResearchNodeLevels.Select(l => l.Id).ToList();
        return Instance._save.PlayerResearchNodes.FirstOrDefault(l => xx != null && xx.Contains(l.ResearchNodeLevelId));
    }


    public static List<DialogDTO> GetDialogByCodeName(string codeName)
    {
        var langInt = PlayerPrefs.GetInt("LocalKey", 0);

        var langCode = langInt == 0 ? "EN" : "TR";
        var defaultVal = new List<DialogDTO>()
        {
            new DialogDTO()
            {
                AnimId = "idle", HeroId = 1, Texts = new List<string>() { "deneme", "test", "123" },
                TypeName = "tutorial"
            }
        };
        if (Instance._save.DialogScenes == null)
        {
            return defaultVal;
        }

        List<DialogDTO> allDialogs = Instance._save.DialogScenes.FirstOrDefault(l => l.Code == codeName)?.Dialogs ?? defaultVal;


        List<DialogDTO> allDialogsCpy = new List<DialogDTO>();

        foreach (var d in allDialogs)
        {
            allDialogsCpy.Add(new DialogDTO()
            {
                AnimId = d.AnimId,
                TypeName = d.TypeName,
                HeroId = d.HeroId,
                Texts = d.Texts
            });
        }

        var cx = allDialogsCpy.Select(l =>
        {
            l.Texts = l.Texts.Where(s => s.Split("::")[0] == langCode).Select(l => l.Split("::")[1]).ToList();
            return l;
        }).ToList();



        return cx;
    }

    public static List<ItemDTO> GetItems()
    {
        return Instance._save.Items;
    }
    /// <summary>
	/// Return Chest type, name, id, hero chest or tower chest info
    /// </summary>
    /// <param name="chestId"></param>
    /// <returns></returns>
    public static ChestTypeDTO? GetChestTypeByChestId(int chestId)
    {
        return Instance._save.ChestTypes.FirstOrDefault(l => l.Chests.Any(k => k.Id == chestId));
    }
    /// <summary>
	/// Return chests that player owns
    /// </summary>
    /// <returns></returns>
    public static List<PlayerChestDTO> GetPlayerChests()
    {
        return Instance._save.PlayerChests?.Where(l => l.SlotPlace > 0).OrderBy(l => l.SlotPlace)?.ToList() ?? new List<PlayerChestDTO>();
    }
    /// <summary>
	/// Return if player get chest or not at the end of level
    /// </summary>
    /// <param name="levelId"></param>
    /// <returns></returns>
    public static ChestDTO? GetChestOnEndLevel(int levelId, bool isTest = false)
    {
        var level = Instance._save.Levels.Where(l => l.Id == levelId).FirstOrDefault();
        var chestChances = level.LevelChestChances.OrderByDescending(l => l.ChanceMultiplier).ToList();

        for (int i = 0; i < chestChances.Count; i++)
        {
            if (chestChances[i].ChanceMultiplier >= Random.Range(0, 100))
            {
                return Instance._save.ChestTypes.SelectMany(l => l.Chests).Where(l => l.Id == chestChances[i].ChestId).FirstOrDefault();
            }
        }
        if (isTest)
        {
            return Instance._save.ChestTypes[0].Chests[0];
        }

        return null;


    }

    /// <summary>
	/// For claim chest.
    /// </summary>
    /// <param name="chestId"></param>
    public static void ClaimChest(int chestId)
    {
        if (GetPlayerChests().Count > 1)
        {
            return;
        }
        var chest = Instance._save.ChestTypes.SelectMany(l => l.Chests).Where(l => l.Id == chestId).FirstOrDefault();
        print("CHEST : " + chest.Id);
        var now = DateTimeOffset.UtcNow;
        //var x = new PlayerChestDTO()
        //{
        //    Id = Instance._playerChestFakeId--,
        //    UserId = 0,
        //    ChestId = chestId,
        //    OpenStartDate = now.ToString(),
        //    OpenFinishDate = (now + chest.OpenDuration.ToTimeSpan()).ToString(),
        //    UsedGem = 0,
        //    GainedItems = null,
        //    SlotPlace = GetPlayerChests().FirstOrDefault()?.SlotPlace == null ? 1 : 2

        //};

        var x = new PlayerChestDTO()
        {
            Id = Instance._playerChestFakeId--,
            UserId = 0,
            ChestId = chestId,
            OpenStartDate = null,
            OpenFinishDate = null,
            UsedGem = 0,
            GainedItems = null,
            SlotPlace = GetPlayerChests().FirstOrDefault(l => l.SlotPlace == 1)?.SlotPlace == null ? 1 : 2

        };

        if (Instance._save.PlayerChests == null)
        {
            Instance._save.PlayerChests = new List<PlayerChestDTO>();
        }
        Instance._save.PlayerChests.Add(x);
        WriteFile();
        Instance.TrySendPlayerChests();

    }

    public static PlayerChestDTO StartCountdownForChest(int playerChestId)
    {
        var playerChest = Instance._save.PlayerChests.FirstOrDefault(l => l.Id == playerChestId);
        if (playerChest == null)
        {
            return null;
        }
        var chestType = GetChestTypeByChestId(playerChest.ChestId);
        if (chestType == null)
        {
            return null;
        }

        var chest = chestType.Chests.FirstOrDefault(l => l.Id == playerChest.ChestId);
        if (chest == null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        playerChest.OpenStartDate = now.ToString();
        playerChest.OpenFinishDate = (now + chest.OpenDuration.ToTimeSpan()).ToString();
        WriteFile();
        return playerChest;
    }
    /// <summary>
	/// Get rewards. If use gem for open chest, Openwithgem return true
    /// </summary>
    /// <param name="playerChestId"></param>
    /// <param name="openWithGem"></param>
    /// <returns></returns>
    public static List<ChestReward> GetChestRewards(int playerChestId, bool openWithGem = false)
    {


        var playerChest = Instance._save.PlayerChests.FirstOrDefault(l => l.Id == playerChestId);
        if (playerChest == null)
        {
            return null;
        }
        var chestType = GetChestTypeByChestId(playerChest.ChestId);
        if (chestType == null)
        {
            return null;
        }

        var chest = chestType.Chests.FirstOrDefault(l => l.Id == playerChest.ChestId);
        if (chest == null)
        {
            return null;
        }

        if (openWithGem && Instance._save.PlayerVariable != null)
        {
            var gemCount = playerChest.OpenFinishDate == null ? chest.InstantOpenGemCount : (int)(playerChest.OpenFinishDate.ToDateTimeUtc() - DateTime.UtcNow).Value.TotalSeconds * chest.InstantOpenGemCount / (int)chest.OpenDuration.ToTimeSpan().Value.TotalSeconds;
            if (Instance._save.PlayerVariable.GemCount < gemCount)
            {
                return null;
            }

            Instance._save.PlayerVariable.GemCount -= gemCount;
            playerChest.UsedGem = gemCount;


        }

        var mainItem = Instance._save.Items.Where(l => l.ItemTypeId == chestType.MainItemType && l.Value1 == chest.Rarity).FirstOrDefault();
        if (mainItem == null)
        {
            return null;
        }
        var gainedItems = new List<ChestReward>() { new ChestReward() { ItemId = mainItem.Id, Count = Random.Range(chest.MainItemMinCount, chest.MainItemMaxCount) } };

        playerChest.GainedItems = JsonConvert.SerializeObject(gainedItems);
        playerChest.SlotPlace = playerChest.SlotPlace * -1;
        ClaimChestRewards(gainedItems);
        WriteFile();
        return gainedItems;
    }
    /// <summary>
	/// Required gem for chest opening
    /// </summary>
    /// <param name="playerChest"></param>
    /// <returns></returns>
    public static int GetRequiredGemCountForChestOpen(PlayerChestDTO playerChest)
    {
        var chestType = GetChestTypeByChestId(playerChest.ChestId);
        if (chestType == null)
        {
            return 1000;
        }

        var chest = chestType.Chests.FirstOrDefault(l => l.Id == playerChest.ChestId);
        if (chest == null)
        {
            return 1000;
        }
        if (playerChest.OpenFinishDate == null)
        {
            var gemCount = chest.InstantOpenGemCount;
            return gemCount;
        }
        else
        {
            var gemCount = playerChest.OpenFinishDate == null ? chest.InstantOpenGemCount : (int)(playerChest.OpenFinishDate.ToDateTimeUtc() - DateTime.UtcNow).Value.TotalSeconds * chest.InstantOpenGemCount / (int)chest.OpenDuration.ToTimeSpan().Value.TotalSeconds;
            return gemCount;
        }


    }

    private static void ClaimChestRewards(List<ChestReward> chestRewards)
    {
        if (Instance._save.PlayerItems == null)
        {
            Instance._save.PlayerItems = new List<PlayerItemDTO>();
        }
        for (int i = 0; i < chestRewards.Count; i++)
        {

            var pI = Instance._save.PlayerItems.FirstOrDefault(l => l.ItemId == chestRewards[i].ItemId);
            if (pI != null)
            {
                pI.Count += chestRewards[i].Count;
            }
            else
            {
                Instance._save.PlayerItems.Add(new PlayerItemDTO() { Id = 0, Count = chestRewards[i].Count, ItemId = chestRewards[i].ItemId, UserId = 0 });
            }
        }
    }

    public static void AddProgress(ProgressDTO progressDto)
    {
        try
        {
            if (Instance._save.ChapterInfo.Levels.FirstOrDefault(l => l.Id == progressDto.LevelId)?.UserStar < progressDto.StarCount)
            {
                Instance._save.ChapterInfo.Levels.First(l => l.Id == progressDto.LevelId).UserStar = progressDto.StarCount;
            }

            if (Instance._save.Levels.First(l => l.Id == progressDto.LevelId).UserStar < progressDto.StarCount)
            {
                Instance._save.Levels.First(l => l.Id == progressDto.LevelId).UserStar = progressDto.StarCount;
            }
            WriteFile();
            Instance.TrySendProgress(progressDto);
        }

        catch (Exception e)
        {

        }

    }

    private void TrySendProgress(ProgressDTO progressDto)
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            if (_save.ProgressList == null)
            {
                _save.ProgressList = new List<ProgressDTO>();
            }
            _save.ProgressList.Add(progressDto);
            SendAllProgress();
        }
    }


    private void TrySendPlayerChests()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            SendAllPlayerChests();
        }
    }

    private void SendAllProgress(bool startSync = false)
    {
        if (_save.ProgressList != null && _save.ProgressList.Count != 0)
        {
            StartCoroutine(AddProgressList(_save.ProgressList, startSync, _addProgressListEvent));
        }
        else
        {
            StartCoroutine(GetSyncStatus(_syncStatusEvent));
        }

    }


    private void SendAllPlayerChests(bool startSync = false)
    {
        if (_save.PlayerChests != null && _save.PlayerChests.Count != 0)
        {
            StartCoroutine(SetPlayerChests(_save.PlayerChests, _setPlayerChestsEvent));
        }

    }


    private void CheckDownloadDone()
    {
        //if (nonSyncTableEnums.Count==0)
        //{
        WriteFile();
        //}
    }


    #region EVENT LISTENERS


    private void OnConnectionChanged(bool isOnlinePrm)
    {
        this.isOnline = isOnlinePrm;
        if (isOnlinePrm)
        {
            Login();
        }
    }

    public static int GetAchievementRewardLevel(int en)
    {
        try
        {
            return Instance._save.AchievementsRewardLevel[en];
        }
        catch (Exception e)
        {

            return 1;
        }

    }



    public static void SetAchievementRewardLevel(int en, int rewardLevel)
    {

        var succeed = false;

        succeed = Instance._save.AchievementsRewardLevel.TryAdd(en, rewardLevel);

        if (!succeed)
        {
            Instance._save.AchievementsRewardLevel[en] = rewardLevel;
        }
        WriteFile();

    }
    private void OnGetSyncStatus(TDResponse<List<TableChangesDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            if (_save.AchievementsValues.Count != 11)
            {
                _save.AchievementsValues.TryAdd((int)AchievementsEnum.EndlessWave, 0);
                _save.AchievementsValues.TryAdd((int)AchievementsEnum.GainCoin, 0);
                _save.AchievementsValues.TryAdd((int)AchievementsEnum.GainStar, 0);
                _save.AchievementsValues.TryAdd((int)AchievementsEnum.SellTower, 0);
                _save.AchievementsValues.TryAdd((int)AchievementsEnum.WatchAd, 0);
                _save.AchievementsValues.TryAdd((int)AchievementsEnum.TowerUpgrade, 0);
                _save.AchievementsValues.TryAdd((int)AchievementsEnum.ArmorApeKill, 0);
                _save.AchievementsValues.TryAdd((int)AchievementsEnum.FastApeKill, 0);
                _save.AchievementsValues.TryAdd((int)AchievementsEnum.MiniBossKill, 0);
                _save.AchievementsValues.TryAdd((int)AchievementsEnum.NormalApeKill, 0);
                _save.AchievementsValues.TryAdd((int)AchievementsEnum.UseHeroSkill, 0);
            }
            nonSyncTableEnums = response.Data?.Select(l => (TableEnum)l.TableEnum).ToList() ?? new List<TableEnum>();
            if (_save.TestVariable == null)
            {

                nonSyncTableEnums = Enum.GetValues(typeof(TableEnum)).Cast<TableEnum>().ToList();

                StartCoroutine(GetLevels(null, _getLevelsEvent));
                StartCoroutine(GetChapterInfo(_chapterInfoEvent));
                StartCoroutine(GetTowers(_getTowersEvent));
                StartCoroutine(GetEnemyList(_getEnemyListEvent));
                StartCoroutine(GetChestTypes(_getChestTypesEvent));
                StartCoroutine(GetItems(_getItemsEvent));
                StartCoroutine(GetResearchNodes(_getResearchNodeEvent));
                StartCoroutine(GetPlayerChests(_getPlayerChestsEvent));
                StartCoroutine(GetPlayerVariable(_getPlayerVariableEvent));
                StartCoroutine(GetPlayerItems(_getPlayerItemEvent));
                StartCoroutine(GetPlayerResearchNodeLevels(_getPlayerResearchNodeLevelsEvent));
                StartCoroutine(GetDialogScenes(_getDialogScenesEvent));


                return;
            }

            if ((_save.Levels == null) || (response.Data != null && response.Data.Any(l => levelGroupTableEnums.Contains((TableEnum)l.TableEnum))))
            {
                nonSyncTableEnums.AddRange(levelGroupTableEnums);
                StartCoroutine(GetLevels(null, _getLevelsEvent));
                StartCoroutine(GetChapterInfo(_chapterInfoEvent));
            }

            if ((_save.Towers == null) || (response.Data != null && response.Data.Any(l => towerGroupTableEnums.Contains((TableEnum)l.TableEnum))))
            {
                nonSyncTableEnums.AddRange(towerGroupTableEnums);
                StartCoroutine(GetTowers(_getTowersEvent));
            }

            if ((_save.EnemyList == null) || (response.Data != null && response.Data.Any(l => enemyGroupTableEnums.Contains((TableEnum)l.TableEnum))))
            {
                nonSyncTableEnums.AddRange(enemyGroupTableEnums);
                StartCoroutine(GetEnemyList(_getEnemyListEvent));
            }

            if ((_save.ChestTypes == null) || (response.Data != null && response.Data.Any(l => chestGroupTableEnums.Contains((TableEnum)l.TableEnum))))
            {
                nonSyncTableEnums.AddRange(chestGroupTableEnums);
                StartCoroutine(GetChestTypes(_getChestTypesEvent));
            }

            if ((_save.Items == null) || (response.Data != null && response.Data.Any(l => itemGroupTableEnums.Contains((TableEnum)l.TableEnum))))
            {
                nonSyncTableEnums.AddRange(itemGroupTableEnums);
                StartCoroutine(GetItems(_getItemsEvent));
            }

            if ((_save.ResearchNodes == null) || (response.Data != null && response.Data.Any(l => researchGroupTableEnums.Contains((TableEnum)l.TableEnum))))
            {
                nonSyncTableEnums.AddRange(researchGroupTableEnums);
                StartCoroutine(GetResearchNodes(_getResearchNodeEvent));
            }

            if ((_save.DialogScenes == null) || (response.Data != null && response.Data.Any(l => dialogGroupTableEnums.Contains((TableEnum)l.TableEnum))))
            {
                nonSyncTableEnums.AddRange(dialogGroupTableEnums);
                StartCoroutine(GetDialogScenes(_getDialogScenesEvent));
            }

            if (_save.PlayerChests == null)
            {
                nonSyncTableEnums.Add(TableEnum.PlayerChests);
                StartCoroutine(GetPlayerChests(_getPlayerChestsEvent));
            }

            if (_save.PlayerVariable == null)
            {
                nonSyncTableEnums.Add(TableEnum.PlayerVariable);
                StartCoroutine(GetPlayerVariable(_getPlayerVariableEvent));
            }

            if (_save.PlayerItems == null)
            {
                nonSyncTableEnums.Add(TableEnum.PlayerItems);
                StartCoroutine(GetPlayerItems(_getPlayerItemEvent));
            }

            if (_save.PlayerResearchNodes == null)
            {
                nonSyncTableEnums.Add(TableEnum.PlayerResearchNodes);
                StartCoroutine(GetPlayerResearchNodeLevels(_getPlayerResearchNodeLevelsEvent));
            }
        }
    }

    private void OnLoginDone(TDResponse<AuthenticateResponse> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            SendAllProgress(true);
        }
    }

    private void OnGetChapterInfo(TDResponse<ChapterInfoDTO> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.ChapterInfo = response.Data;
            nonSyncTableEnums.Remove(TableEnum.Chapter);
            CheckDownloadDone();
        }
    }

    private void OnGetLevels(TDResponse<List<LevelDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.Levels = response.Data;
            var ens = levelGroupTableEnums.Where(l => l != TableEnum.Chapter).ToList();
            foreach (var tableEnum in ens)
            {
                nonSyncTableEnums.Remove(tableEnum);
            }
            CheckDownloadDone();

        }
    }

    private void OnGetTowers(TDResponse<List<TowerDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.Towers = response.Data;
            foreach (var tableEnum in towerGroupTableEnums)
            {
                nonSyncTableEnums.Remove(tableEnum);
            }
            CheckDownloadDone();

        }
    }

    private void OnGetEnemyList(TDResponse<List<EnemyDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.EnemyList = response.Data;
            foreach (var tableEnum in enemyGroupTableEnums)
            {
                nonSyncTableEnums.Remove(tableEnum);
            }
            CheckDownloadDone();

        }
    }

    private void OnGetItems(TDResponse<List<ItemDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.Items = response.Data;
            foreach (var tableEnum in itemGroupTableEnums)
            {
                nonSyncTableEnums.Remove(tableEnum);
            }
            CheckDownloadDone();

        }
    }
    private void OnGetDialogScenes(TDResponse<List<DialogSceneDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.DialogScenes = response.Data;
            foreach (var tableEnum in dialogGroupTableEnums)
            {
                nonSyncTableEnums.Remove(tableEnum);
            }
            CheckDownloadDone();
        }
    }

    private void OnGetChestTypes(TDResponse<List<ChestTypeDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.ChestTypes = response.Data;
            foreach (var tableEnum in chestGroupTableEnums)
            {
                nonSyncTableEnums.Remove(tableEnum);
            }
            CheckDownloadDone();
        }
    }

    private void OnGetResearchNodes(TDResponse<List<ResearchNodeDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.ResearchNodes = response.Data;
            foreach (var tableEnum in researchGroupTableEnums)
            {
                nonSyncTableEnums.Remove(tableEnum);
            }
            CheckDownloadDone();

        }
    }

    private void OnGetPlayerResearchNodeLevels(TDResponse<List<PlayerResearchNodeLevelDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.PlayerResearchNodes = response.Data ?? new List<PlayerResearchNodeLevelDTO>();
            nonSyncTableEnums.Remove(TableEnum.PlayerResearchNodes);
            CheckDownloadDone();

        }
    }

    private void OnSetPlayerResearchNodeLevels(TDResponse<List<PlayerResearchNodeLevelDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.PlayerResearchNodes = response.Data;
            CheckDownloadDone();

        }
    }

    private void OnGetPlayerChests(TDResponse<List<PlayerChestDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.PlayerChests = response.Data;
            nonSyncTableEnums.Remove(TableEnum.PlayerChests);
            CheckDownloadDone();

        }
    }

    private void OnSetPlayerChests(TDResponse<List<PlayerChestDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.PlayerChests = response.Data;
            CheckDownloadDone();

        }
    }

    private void OnGetPlayerVariable(TDResponse<PlayerVariableDTO> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.PlayerVariable = response.Data ?? new PlayerVariableDTO();
            nonSyncTableEnums.Remove(TableEnum.PlayerVariable);
            CheckDownloadDone();

        }
    }

    private void OnSetPlayerVariable(TDResponse<PlayerVariableDTO> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.PlayerVariable = response.Data;
            CheckDownloadDone();

        }
    }

    private void OnGetPlayerItems(TDResponse<List<PlayerItemDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.PlayerItems = response.Data ?? new List<PlayerItemDTO>();
            nonSyncTableEnums.Remove(TableEnum.PlayerItems);
            CheckDownloadDone();

        }
    }

    private void OnSetPlayerItems(TDResponse<List<PlayerItemDTO>> response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.PlayerItems = response.Data;
            CheckDownloadDone();

        }
    }

    private void OnAddProgressList(TDResponse response)
    {
        if (response.HasError)
        {
            //todo: error handling
        }
        else
        {
            _save.ProgressList.Clear();
            WriteFile();
        }

        if (response.GenericValue == 1)
        {
            StartCoroutine(GetSyncStatus(_syncStatusEvent));
        }
    }

    #endregion


    #region API FUNCTIONS

    private static IEnumerator LoginWithDeviceID(
        UnityEvent<TDResponse<AuthenticateResponse>> loginEndEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/User/LoginWithDeviceId"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            var response =
                JsonConvert.DeserializeObject<TDResponse<AuthenticateResponse>>(www.downloadHandler.text);
            if (response.Data != null)
            {
                PlayerPrefs.SetString("token", response.Data.Token);
                response.Data.LoginKind = 1;
            }

            loginEndEvent.Invoke(response);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetSyncStatus(
        UnityEvent<TDResponse<List<TableChangesDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetPlayerSyncStatus"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<TableChangesDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetChapterInfo(
        UnityEvent<TDResponse<ChapterInfoDTO>> responseEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetChapterInfo"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<ChapterInfoDTO>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetTowers(
        UnityEvent<TDResponse<List<TowerDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetTowers"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<TowerDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetEnemyList(
        UnityEvent<TDResponse<List<EnemyDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetEnemyList"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<EnemyDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetLevels(List<int> req,
        UnityEvent<TDResponse<List<LevelDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest<List<int>>()
        {
            Data = req,
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetLevels"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<LevelDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetItems(
        UnityEvent<TDResponse<List<ItemDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetItems"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<ItemDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetChestTypes(
        UnityEvent<TDResponse<List<ChestTypeDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetChestTypes"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<ChestTypeDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetPlayerChests(
        UnityEvent<TDResponse<List<PlayerChestDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetPlayerChests"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<PlayerChestDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator SetPlayerChests(List<PlayerChestDTO> req,
        UnityEvent<TDResponse<List<PlayerChestDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest<List<PlayerChestDTO>>()
        {
            Data = req,
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/SetPlayerChests"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<PlayerChestDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetPlayerVariable(
       UnityEvent<TDResponse<PlayerVariableDTO>> responseEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetPlayerVariable"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<PlayerVariableDTO>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator SetPlayerVariable(PlayerVariableDTO req,
        UnityEvent<TDResponse<PlayerVariableDTO>> responseEvent)
    {
        var reqWithInfo = new BaseRequest<PlayerVariableDTO>()
        {
            Data = req,
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/SetPlayerVariable"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<PlayerVariableDTO>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetPlayerItems(
       UnityEvent<TDResponse<List<PlayerItemDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetPlayerItems"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<PlayerItemDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetResearchNodes(
       UnityEvent<TDResponse<List<ResearchNodeDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetResearchNodes"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<ResearchNodeDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator GetPlayerResearchNodeLevels(
       UnityEvent<TDResponse<List<PlayerResearchNodeLevelDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/GetPlayerResearchNodeLevels"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<PlayerResearchNodeLevelDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator SetPlayerResearchNodeLevels(List<PlayerResearchNodeLevelDTO> req,
        UnityEvent<TDResponse<List<PlayerResearchNodeLevelDTO>>> responseEvent)
    {
        var reqWithInfo = new BaseRequest<List<PlayerResearchNodeLevelDTO>>()
        {
            Data = req,
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/SetPlayerResearchNodeLevels"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<List<PlayerResearchNodeLevelDTO>>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator SetPlayerItems(PlayerItemDTO req,
        UnityEvent<TDResponse<PlayerItemDTO>> responseEvent)
    {
        var reqWithInfo = new BaseRequest<PlayerItemDTO>()
        {
            Data = req,
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/SetPlayerItems"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse<PlayerItemDTO>>(www.downloadHandler.text);
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    private static IEnumerator AddProgressList(List<ProgressDTO> req, bool startSync,
        UnityEvent<TDResponse> responseEvent)
    {
        var reqWithInfo = new BaseRequest<List<ProgressDTO>>()
        {
            Data = req,
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/TD/AddProgressList"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(www.downloadHandler.text);
            var response =
                JsonConvert.DeserializeObject<TDResponse>(www.downloadHandler.text);
            response.GenericValue = startSync ? 1 : 0;
            responseEvent.Invoke(response);
            Debug.Log(response.Message);
        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    public static IEnumerator GetDialogScenes(UnityEvent<TDResponse<List<DialogSceneDTO>>> getDialogScenesEvent)
    {

        var reqWithInfo = new BaseRequest()
        {
            Info = GetInfo()
        };
        var serializedReq = JsonConvert.SerializeObject(reqWithInfo);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(serializedReq);
        UnityWebRequest www = new UnityWebRequest(new Uri(ApiUrl + "/api/Dialog/GetDialogScenes"));
        www.method = UnityWebRequest.kHttpVerbPOST;
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept", "*/*");
        www.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
        www.disposeUploadHandlerOnDispose = true;
        www.disposeDownloadHandlerOnDispose = true;
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {

            var response = JsonConvert.DeserializeObject<TDResponse<List<DialogSceneDTO>>>(www.downloadHandler.text);


            getDialogScenesEvent.Invoke(response);

        }
        else
        {
            Debug.Log("Error While Sending: " + www.error);
        }

        www.Dispose();
    }

    #endregion

    private static InfoDTO GetInfo()
    {
        return new InfoDTO()
        {
            AppVersion = Application.version,
            DeviceId = SystemInfo.deviceUniqueIdentifier,
            DeviceModel = SystemInfo.deviceModel,
            DeviceType = SystemInfo.deviceName + "[" + SystemInfo.deviceType + "]",
            OsVersion = SystemInfo.operatingSystem
        };
    }
}
