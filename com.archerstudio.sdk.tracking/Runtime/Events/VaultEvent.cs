using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    public class SpellUpgradeEvent : GameTrackingEvent {
        public override string EventName => "spell_upgrade";

        private readonly string _spellId;
        private readonly string _spellLevel;

        public SpellUpgradeEvent(string spellId, string spellLevel) {
            _spellId = spellId;
            _spellLevel = spellLevel;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_SPELL_ID, _spellId);
            dict.Add(TrackingConstants.PAR_SPELL_LEVEL, _spellLevel);
        }
    }
}
