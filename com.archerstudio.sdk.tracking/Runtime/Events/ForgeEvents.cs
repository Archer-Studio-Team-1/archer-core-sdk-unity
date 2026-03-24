using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    public class ForgeUpgradeEvent : GameTrackingEvent {
        public override string EventName => "forge_upgrade";

        private readonly string _forgeLevel;

        public ForgeUpgradeEvent(string forgeLevel) { _forgeLevel = forgeLevel; }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_FORGE_LEVEL, _forgeLevel);
        }
    }

    public class CharacterLevelUpEvent : GameTrackingEvent {
        public override string EventName => "character_levelup";

        private readonly string _levelId;

        public CharacterLevelUpEvent(string levelId) { _levelId = levelId; }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_LEVEL_ID, _levelId);
        }
    }
}
