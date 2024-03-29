﻿using ASC.Mail.Models;
using ASC.Web.Api.Routing;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace ASC.Mail.Controllers
{
    public partial class MailController : ControllerBase
    {
        /// <summary>
        ///    Returns Common Settings
        /// </summary>
        /// <returns>MailCommonSettings object</returns>
        /// <short>Get common settings</short> 
        /// <category>Settings</category>
        [HttpGet(@"settings")]
        public MailCommonSettings GetCommonSettings()
        {
            return _settingEngine.GetCommonSettings();
        }

        /// <summary>
        ///    Returns EnableConversations flag
        /// </summary>
        /// <returns>boolean</returns>
        /// <short>Get EnableConversations flag</short> 
        /// <category>Settings</category>
        [HttpGet(@"settings/conversationsEnabled")]
        public bool GetEnableConversationFlag()
        {
            return _settingEngine.GetEnableConversationFlag();
        }

        /// <summary>
        ///    Set EnableConversations flag
        /// </summary>
        /// <param name="enabled">True or False value</param>
        /// <short>Set EnableConversations flag</short> 
        /// <category>Settings</category>
        [HttpPut(@"settings/conversationsEnabled")]
        public void SetEnableConversationFlag(bool enabled)
        {
            _settingEngine.SetEnableConversationFlag(enabled);
        }

        /// <summary>
        ///    Returns AlwaysDisplayImages flag
        /// </summary>
        /// <returns>boolean</returns>
        /// <short>Get AlwaysDisplayImages flag</short> 
        /// <category>Settings</category>
        [HttpGet(@"settings/alwaysDisplayImages")]
        public bool GetAlwaysDisplayImagesFlag()
        {
            return _settingEngine.GetAlwaysDisplayImagesFlag();
        }

        /// <summary>
        ///    Set AlwaysDisplayImages flag
        /// </summary>
        /// <param name="enabled">True or False value</param>
        /// <short>Set AlwaysDisplayImages flag</short> 
        /// <category>Settings</category>
        [HttpPut(@"settings/alwaysDisplayImages")]
        public void SetAlwaysDisplayImagesFlag(bool enabled)
        {
            _settingEngine.SetAlwaysDisplayImagesFlag(enabled);
        }

        /// <summary>
        ///    Returns CacheUnreadMessages flag
        /// </summary>
        /// <returns>boolean</returns>
        /// <short>Get CacheUnreadMessages flag</short> 
        /// <category>Settings</category>
        [HttpGet(@"settings/cacheMessagesEnabled")]
        public bool GetCacheUnreadMessagesFlag()
        {
            //TODO: Change cache algoritnm and restore it back
            // return SettingEngine.GetCacheUnreadMessagesFlag()

            return false;
        }

        /// <summary>
        ///    Set CacheUnreadMessages flag
        /// </summary>
        /// <param name="enabled">True or False value</param>
        /// <short>Set CacheUnreadMessages flag</short> 
        /// <category>Settings</category>
        [HttpPut(@"settings/cacheMessagesEnabled")]
        public void SetCacheUnreadMessagesFlag(bool enabled)
        {
            _settingEngine.SetCacheUnreadMessagesFlag(enabled);
        }

        /// <summary>
        ///    Returns GoNextAfterMove flag
        /// </summary>
        /// <returns>boolean</returns>
        /// <short>Get GoNextAfterMove flag</short> 
        /// <category>Settings</category>
        [HttpGet(@"settings/goNextAfterMoveEnabled")]
        public bool GetEnableGoNextAfterMoveFlag()
        {
            return _settingEngine.GetEnableGoNextAfterMoveFlag();
        }

        /// <summary>
        ///    Set GoNextAfterMove flag
        /// </summary>
        /// <param name="enabled">True or False value</param>
        /// <short>Set GoNextAfterMove flag</short> 
        /// <category>Settings</category>
        [HttpPut(@"settings/goNextAfterMoveEnabled")]
        public void SetEnableGoNextAfterMoveFlag(bool enabled)
        {
            _settingEngine.SetEnableGoNextAfterMoveFlag(enabled);
        }

        /// <summary>
        ///    Returns ReplaceMessageBody flag
        /// </summary>
        /// <returns>boolean</returns>
        /// <short>Get ReplaceMessageBody flag</short> 
        /// <category>Settings</category>
        [HttpGet(@"settings/replaceMessageBody")]
        public bool GetEnableReplaceMessageBodyFlag()
        {
            return _settingEngine.GetEnableReplaceMessageBodyFlag();
        }

        /// <summary>
        ///    Set ReplaceMessageBody flag
        /// </summary>
        /// <param name="enabled">True or False value</param>
        /// <short>Set ReplaceMessageBody flag</short> 
        /// <category>Settings</category>
        [HttpPut(@"settings/replaceMessageBody")]
        public void SetEnableReplaceMessageBodyFlag(bool enabled)
        {
            _settingEngine.SetEnableReplaceMessageBodyFlag(enabled);
        }
    }
}
