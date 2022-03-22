using ASC.Common;
using ASC.Core.Common.Settings;
using ASC.Mail.Models;

namespace ASC.Mail.Core.Engine
{
    [Scope]
    public class SettingEngine
    {
        private readonly SettingsManager _settingsManager;

        public SettingEngine(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }

        public MailCommonSettings GetCommonSettings()
        {
            var commonSettings = _settingsManager.LoadForCurrentUser<MailCommonSettings>();

            return commonSettings;
        }

        public bool GetEnableConversationFlag()
        {
            var settings = GetCommonSettings();

            var value = settings.EnableConversationsSetting;

            return value;
        }

        public void SetEnableConversationFlag(bool enabled)
        {
            var settings = GetCommonSettings();

            settings.EnableConversationsSetting = enabled;

            _settingsManager.SaveForCurrentUser(settings);
        }

        public bool GetAlwaysDisplayImagesFlag()
        {
            var settings = GetCommonSettings();

            var value = settings.AlwaysDisplayImagesSetting;

            return value;
        }

        public void SetAlwaysDisplayImagesFlag(bool enabled)
        {
            var settings = GetCommonSettings();

            settings.AlwaysDisplayImagesSetting = enabled;

            _settingsManager.SaveForCurrentUser(settings);
        }

        public bool GetCacheUnreadMessagesFlag()
        {
            //TODO: Change cache algoritnm and restore it back
            /*var settings = GetCommonSettings();

            var value = settings.CacheUnreadMessagesSetting;

            return value;*/

            return false;
        }

        public void SetCacheUnreadMessagesFlag(bool enabled)
        {
            var settings = GetCommonSettings();

            settings.CacheUnreadMessagesSetting = enabled;

            _settingsManager.SaveForCurrentUser(settings);
        }

        public bool GetEnableGoNextAfterMoveFlag()
        {
            var settings = GetCommonSettings();

            var value = settings.EnableGoNextAfterMoveSetting;

            return value;
        }

        public void SetEnableGoNextAfterMoveFlag(bool enabled)
        {
            var settings = GetCommonSettings();

            settings.EnableGoNextAfterMoveSetting = enabled;

            _settingsManager.SaveForCurrentUser(settings);
        }

        public bool GetEnableReplaceMessageBodyFlag()
        {
            var settings = GetCommonSettings();

            var value = settings.ReplaceMessageBodySetting;

            return value;
        }

        public void SetEnableReplaceMessageBodyFlag(bool enabled)
        {
            var settings = GetCommonSettings();

            settings.ReplaceMessageBodySetting = enabled;

            _settingsManager.SaveForCurrentUser(settings);
        }
    }
}
