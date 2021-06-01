using System.Linq;

namespace LiveSplit.TheWildAtHeart {
    public partial class TheWildAtHeartComponent {

        private readonly RemainingDictionary remainingSplits;

        public override bool Update() {
            return memory.Update();
        }

        public override bool Start() {
            return memory.HasStartedNewSave();
        }

        public override void OnStart() {
            remainingSplits.Setup(settings.Splits);
            memory.OnStart();
        }

        public override bool Split() {
            return remainingSplits.Count() != 0
                && (SplitSpriteling() || SplitInventory() || SplitSaveData());

            bool SplitSpriteling() {
                if(!remainingSplits.ContainsKey("Spriteling")) {
                    return false;
                }
                foreach(Spriteling spriteling in memory.SpritelingSequence()) {
                    if(remainingSplits.Split("Spriteling", spriteling.ToString())) {
                        return true;
                    }
                }
                return false;
            }

            bool SplitInventory() {
                if(!remainingSplits.ContainsKey("Inventory")) {
                    return false;
                }
                foreach(string item in memory.InventorySequence()) {
                    if(remainingSplits.Split("Inventory", item)) {
                        return true;
                    }
                }
                return false;
            }

            bool SplitSaveData() {
                if(!remainingSplits.ContainsKey("SaveData") && !remainingSplits.ContainsKey("End")) {
                    return false;
                }
                bool needSplit = false;
                foreach(string name in memory.SaveDataSequence()) {
                    if(remainingSplits.Split("SaveData", name)) {
                        needSplit = true;
                        break;
                    }
                }
                needSplit |= remainingSplits.ContainsKey("End") && memory.IsGameFinished() && remainingSplits.Split("End");
                return needSplit;
            }
        }

        public override bool Reset() {
            return memory.HasDeletedSave();
        }

        public override bool Loading() {
            return memory.IsLoading();
        }
    }
}