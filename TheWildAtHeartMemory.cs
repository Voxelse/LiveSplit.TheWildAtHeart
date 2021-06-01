using System;
using System.Collections.Generic;
using Voxif.AutoSplitter;
using Voxif.Helpers.Unity;
using Voxif.IO;
using Voxif.Memory;

namespace LiveSplit.TheWildAtHeart {
    public class TheWildAtHeartMemory : Memory {

        protected override string[] ProcessNames => new string[] { "The Wild At Heart" };

        private Pointer<bool> Loaded { get; set; }

        private Pointer<IntPtr> Spriteling { get; set; }
        private readonly HashSet<Spriteling> spritelings = new HashSet<Spriteling>();

        private Pointer<IntPtr> MasterInventory { get; set; }
        private readonly HashSet<string> inventory = new HashSet<string>();

        private Pointer<IntPtr> SettingsData { get; set; }
        private const int SaveCount = 3;
        private readonly long[] fileSaveTimes = new long[SaveCount];

        private Pointer<IntPtr> SaveData { get; set; }
        private int saveDataVersion = 0;
        private readonly Dictionary<string, string> saveDataDict = new Dictionary<string, string>();

        private Pointer<float> ScreenFadeAlpha { get; set; }

        private UnityHelperTask unityTask;

        public TheWildAtHeartMemory(Logger logger) : base(logger) {
            OnHook += () => {
                unityTask = new UnityHelperTask(game, logger);
                unityTask.Run(InitPointers);
            };

            OnExit += () => {
                if(unityTask != null) {
                    unityTask.Dispose();
                    unityTask = null;
                }
            };
        }

        private void InitPointers(IMonoHelper unity) {
            MonoNestedPointerFactory ptrFactory = new MonoNestedPointerFactory(game, unity);

            Loaded = ptrFactory.Make<bool>("BSceneManager", "<SceneLoadingFinished>k__BackingField");

            SettingsData = ptrFactory.Make<IntPtr>("SettingsData", "Instance");

            ScreenFadeAlpha = ptrFactory.Make<float>("BScreenFade", "<Instance>k__BackingField", "backing", 0x74);

            var playerData = ptrFactory.Make<IntPtr>("PlayerData", "Instance", out IntPtr pData);
            SaveData = ptrFactory.Make<IntPtr>(playerData, unity.GetFieldOffset(pData, "VariableSaveData"), 0x10, 0x10);
            Spriteling = ptrFactory.Make<IntPtr>(playerData, unity.GetFieldOffset(pData, "AcquiredSpritelingTypes"));
            MasterInventory = ptrFactory.Make<IntPtr>(playerData, unity.GetFieldOffset(pData, "MasterInventory"), 0x10);
            
            logger.Log(ptrFactory.ToString());

            unityTask = null;
        }

        public override bool Update() => base.Update() && unityTask == null;

        public void OnStart() {
            saveDataDict.Clear();
            inventory.Clear();
        }

        private long FileSaveTime(int index) {
            return game.Read<long>(game.Read(SettingsData.New, 0x10, 0x10, 0x20 + 0x8 * index, 0x28));
        }

        public bool HasStartedNewSave() {
            for(int i = 0; i < SaveCount; i++) {
                long fileSaveTime = FileSaveTime(i);
                if(fileSaveTimes[i] != fileSaveTime) {
                    long oldSaveTime = fileSaveTimes[i];
                    fileSaveTimes[i] = fileSaveTime;
                    if(oldSaveTime == 0 && fileSaveTime != 0 
                        && TimeSpan.FromTicks(DateTime.Now.Ticks - fileSaveTime).TotalSeconds < 1
                        && game.Read<float>(game.Read(SettingsData.New, 0x10, 0x10, 0x20 + 0x8 * i, 0x20)) < 1) {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool HasDeletedSave() {
            for(int i = 0; i < SaveCount; i++) {
                long fileSaveTime = FileSaveTime(i);
                if(fileSaveTimes[i] != fileSaveTime) {
                    fileSaveTimes[i] = fileSaveTime;
                    if(fileSaveTime == 0) {
                        return true;
                    }
                }
            }
            return false;
        }

        public IEnumerable<Spriteling> SpritelingSequence() {
            int spritelingCount = game.Read<int>(Spriteling.New + 0x18);
            if(spritelingCount == spritelings.Count) {
                yield break;
            }
            for(int i = 0; i < spritelingCount; i++) {
                Spriteling spriteling = game.Read<Spriteling>(game.Read<IntPtr>(Spriteling.New + 0x10) + 0x20 + 0x4 * i);
                if(spritelings.Add(spriteling)) {
                    yield return spriteling;
                }
            }
        }


        public IEnumerable<string> InventorySequence() {
            int inventoryCount = game.Read<int>(MasterInventory.New + 0x18);
            if(inventoryCount == inventory.Count) {
                yield break;
            }
            for(int i = 0; i < inventoryCount; i++) {
                string item = game.ReadString(game.Read(MasterInventory.New, 0x10, 0x20 + 0x8 * i, 0x10, 0x14), EStringType.AutoSized);
                if(inventory.Add(item)) {
                    yield return item;
                }
            }
        }
        public IEnumerable<string> SaveDataSequence() {
            int dataVersion = game.Read<int>(SaveData.New + 0x44);
            if(dataVersion == saveDataVersion) {
                yield break;
            }
            saveDataVersion = dataVersion;

            IntPtr dataEntries = game.Read<IntPtr>(SaveData.New + 0x18);
            int dataCount = game.Read<int>(SaveData.New + 0x40);

            for(int dataId = 0; dataId < dataCount; dataId++) {
                IntPtr dataOffset = dataEntries + 0x20 + dataId * 0x18;

                IntPtr keyPtr = game.Read<IntPtr>(dataOffset + 0x8);
                string key = game.ReadString(keyPtr + 0x14, EStringType.AutoSized);

                IntPtr valuePtr = game.Read<IntPtr>(dataOffset + 0x10);
                string value = game.ReadString(valuePtr + 0x14, EStringType.AutoSized);

                if(!saveDataDict.ContainsKey(key)) {
                    saveDataDict.Add(key, value);
                    yield return ParseData(key, value);
                } else if(saveDataDict[key] != value) {
                    saveDataDict[key] = value;
                    yield return ParseData(key, value);
                }
            }

            string ParseData(string key, string value) {
                if(Single.TryParse(value, out float result)) {
                    return key + "_" + result;
                } else {
                    return key;
                }
            }
        }

        public bool IsGameFinished() {
            return saveDataDict.ContainsKey("hasSeenNWW - Wake Goes Home (NC.DialogueTrees.DialogueTree)0SS")
                && ScreenFadeAlpha.New == 1 && ScreenFadeAlpha.Old != 1;
        }

        public bool IsLoading() {
            return !Loaded.New;
        }
    }

    public enum Spriteling {
        Twigling = 1,
        Barbling,
        Emberling,
        Shiverling,
        Lunaling,
        ShiverlingClone = 100
    }
}