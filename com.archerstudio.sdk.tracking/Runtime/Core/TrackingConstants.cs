namespace ArcherStudio.SDK.Tracking {

    /// <summary>
    /// Central constants for event names, parameter keys, and user properties.
    /// Migrated from ArcherStudio.Game.Tracking.TrackingConstants — exact same values.
    /// </summary>
    public static class TrackingConstants {

        // ─── Event Names (v2) ───
        public const string EVT_AD_IMPRESSION = "ad_impression";
        public const string EVT_AD_REVENUE = "ad_revenue";
        public const string EVT_TUTORIAL = "tutorial";
        public const string EVT_STAGE_START = "stage_start";
        public const string EVT_STAGE_END = "stage_end";
        public const string EVT_FEATURE_UNLOCK = "feature_unlock";
        public const string EVT_FEATURE_OPEN = "feature_open";
        public const string EVT_FEATURE_CLOSE = "feature_close";
        public const string EVT_BUTTON_CLICK = "button_click";
        public const string EVT_EARN_RESOURCE = "earn_resource";
        public const string EVT_BUY_RESOURCE = "buy_resource";
        public const string EVT_SPEND_RESOURCE = "spend_resource";
        public const string EVT_EXPLORATION_START = "exploration_start";
        public const string EVT_EXPLORATION_END = "exploration_end";
        public const string EVT_EXPLORATION_RANK_UP = "exploration_rank_up";
        public const string EVT_LOADING_START = "loading_start";
        public const string EVT_LOADING_RESULT = "loading_result";
        public const string EVT_PURCHASE_SHOW = "purchase_show";
        public const string EVT_PURCHASE_RESULT = "purchase_result";

        // ─── Parameter Keys ───
        public const string PAR_AD_MEDIATION = "ad_mediation";
        public const string PAR_AD_SOURCE = "ad_source";
        public const string PAR_AD_UNIT_ID = "ad_unit_id";
        public const string PAR_PLACEMENT = "placement";
        public const string PAR_IAA_REVENUE_MICRO = "iaa_revenue_micro";
        public const string PAR_AD_PLATFORM = "ad_platform";
        public const string PAR_AD_FORMAT = "ad_format";
        public const string PAR_AD_UNIT_NAME = "ad_unit_name";
        public const string PAR_CURRENCY = "currency";
        public const string PAR_VALUE = "value";

        public const string PAR_TUT_CATEGORY = "tut_category";
        public const string PAR_TUT_NAME = "tut_name";
        public const string PAR_TUT_INDEX = "tut_index";

        public const string PAR_CATEGORY = "category";
        public const string PAR_STAGE_ID = "stage_id";
        public const string PAR_DURATION = "duration";

        public const string PAR_FEATURE_ID = "feature_id";
        public const string PAR_DURATION_FEATURE = "duration_feature";

        public const string PAR_NAME = "name";
        public const string PAR_DESC = "desc";

        public const string PAR_ITEM_CATEGORY = "item_category";
        public const string PAR_ITEM_ID = "item_id";
        public const string PAR_SOURCE = "source";
        public const string PAR_SOURCE_ID = "source_id";
        public const string PAR_REMAINING_VALUE = "remaining_value";
        public const string PAR_TOTAL_EARN_VALUE = "total_earn_value";
        public const string PAR_TOTAL_BOUGHT_VALUE = "total_bought_value";
        public const string PAR_TOTAL_SPENT_VALUE = "total_spent_value";

        public const string PAR_SPELL_ID = "spell_id";
        public const string PAR_SPELL_LEVEL = "spell_level";
        public const string PAR_DAY_SINCE_UNLOCK = "day_since_unlock";

        public const string PAR_FORGE_LEVEL = "forge_level";
        public const string PAR_LEVEL_ID = "level_id";

        public const string PAR_BOSS_LEVEL = "boss_level";
        public const string PAR_PLAY_INDEX = "play_index";
        public const string PAR_LOSE_INDEX = "lose_index";
        public const string PAR_RESULT = "result";
        public const string PAR_LOSE_BY = "lose_by";
        public const string PAR_RANK_LEVEL = "rank_level";

        public const string PAR_IS_USER_ACTION = "is_user_action";
        public const string PAR_TIMEOUT_MSEC = "timeout_msec";
        public const string PAR_FPS = "fps";

        public const string PAR_PRODUCT_ID = "product_id";
        public const string PAR_REASON = "reason";
        public const string PAR_STATUS = "status";

        // ─── Resource Sources ───
        public const string SOURCE_MAIN = "main";
        public const string SOURCE_GACHA = "gacha";
        public const string SOURCE_SHOP = "shop";
        public const string SOURCE_EXPLORATION = "exploration";
        public const string SOURCE_EXPLORE_CHEST = "explore_chest";
        public const string SOURCE_FORGE = "forge";
        public const string SOURCE_IDLE_REWARD = "idle";
        public const string SOURCE_DAILY_TASK = "daily_task_achivement";
        public const string SOURCE_WEEKLY_TASK = "weekly_task_achivement";
        public const string SOURCE_SLOT_MACHINE = "slot_machine";
        public const string SOURCE_ADS_GOLD_OFFER = "ads_gold_offer";

        // ─── Resource Source IDs ───
        public const string SOURCE_ID_GACHA_ADS = "gacha_ads";
        public const string SOURCE_ID_GACHA_NORMAL = "gacha_normal";
        public const string SOURCE_ID_GACHA_PREMIUM = "gacha_premium";
        public const string SOURCE_ID_CHEST_ADS = "chest_ads";
        public const string SOURCE_ID_CHEST = "chest";
        public const string SOURCE_ID_X2PROFIT = "x2_income";
        public const string SOURCE_ID_IDLE = "idle";
        public const string SOURCE_ID_IDLE_ADS = "idle_ads";
        public const string SOURCE_ID_STAGE_QUEST = "stage_quest";
        public const string SOURCE_ID_STATION = "station";


        // ─── User Properties (v2) ───
        public const string UP_ADJUST_ID = "adjust_id";
        public const string UP_DEVICE_ID = "device_id";
        public const string UP_FIREBASE_STORAGE_ID = "firebase_storage_id";
        public const string UP_CURRENT_TASK = "current_task";
        public const string UP_CURRENT_STAGE = "current_stage";
        public const string UP_PROGRESS_STAGE = "progress_stage";
        public const string UP_EXPLORE_STAGE = "explore_stage";
        public const string UP_CURRENT_EXPLORE_TICKET = "current_explore_ticket";
        public const string UP_CURRENT_FORGE_SHOP_LEVEL = "current_forgeshoplevel";
        public const string UP_CURRENT_EXPLORE_BOSS_LEVEL = "current_explorebosslevel";
        public const string UP_CURRENT_GEM = "current_gem";
        public const string UP_LEVEL = "level";
        public const string UP_DAY_SINCE_INSTALL = "day_since_install";
        public const string UP_IAP_COUNT = "iap_count";
        public const string UP_IAA_COUNT = "iaa_count";
        public const string UP_STORE_NAME = "store_name";
        public const string UP_STORE_APP_ID = "store_app_id";
    }
}
