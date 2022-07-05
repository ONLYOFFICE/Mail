using ASC.Mail.Core.Engine.Operations.Base;
using ASC.Mail.Core.Resources;
using ASC.Mail.Enums;
using ASC.Mail.Exceptions;
using ASC.Mail.Extensions;
using ASC.Mail.Models;
using ASC.Web.Mail.Resources;

using Microsoft.AspNetCore.Mvc;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ASC.Mail.Controllers
{
    public partial class MailController : ControllerBase
    {
        /// <summary>
        ///    Returns the list of default folders
        /// </summary>
        /// <returns>Folders list</returns>
        /// <short>Get folders</short> 
        /// <category>Folders</category>
        [HttpGet(@"folders")]
        public IEnumerable<MailFolderData> GetFolders()
        {
            if (!_mailSettings.Aggregator.EnableSignalr)
                _accountEngine.SetAccountsActivity();

            return _folderEngine.GetFolders()
                                 .Where(f => f.id != FolderType.Sending)
                                 .ToList()
                                 .ToFolderData();
        }

        /// <summary>
        ///    Removes all the messages from the folder. Trash or Spam.
        /// </summary>
        /// <param name="folderid">Selected folder id. Trash - 4, Spam 5.</param>
        /// <short>Remove all messages from folder</short> 
        /// <category>Folders</category>
        [HttpDelete(@"folders/{folderid}/messages")]
        public int RemoveFolderMessages(int folderid)
        {
            var folderType = (FolderType)folderid;

            if (folderType == FolderType.Trash || folderType == FolderType.Spam)
            {
                _messageEngine.SetRemoved(folderType);
            }

            return folderid;
        }


        /// <summary>
        ///    Recalculate folders counters
        /// </summary>
        /// <returns>MailOperationResult object</returns>
        /// <short>Get folders</short> 
        /// <category>Folders</category>
        /// <visible>false</visible>
        [HttpGet(@"folders/recalculate")]
        public MailOperationStatus RecalculateFolders()
        {
            _operationEngine.RecalculateFolders(TranslateMailOperationStatus);
            throw new NotImplementedException();
        }

        /// <summary>
        ///    Returns the list of user folders
        /// </summary>
        /// <param name="ids" optional="true">List of folder's id</param>
        /// <param name="parentId" optional="true">Selected parent folder id (root level equals 0)</param>
        /// <returns>Folders list</returns>
        /// <short>Get folders</short> 
        /// <category>Folders</category>
        [HttpGet(@"userfolders")]
        public IEnumerable<MailUserFolderData> GetUserFolders(List<int> ids, int? parentId)
        {
            var list = _userFolderEngine.GetList(ids, parentId);
            return list;
        }

        /// <summary>
        ///    Create user folder
        /// </summary>
        /// <param name="name">Folder name</param>
        /// <param name="parentId">Parent folder id (default = 0)</param>
        /// <returns>Folders list</returns>
        /// <short>Create folder</short> 
        /// <category>Folders</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpPost(@"userfolders")]
        public MailUserFolderData CreateUserFolder(string name, int parentId = 0)
        {
            //Thread.CurrentThread.CurrentCulture = CurrentCulture;
            //Thread.CurrentThread.CurrentUICulture = CurrentCulture;

            try
            {
                var userFolder = _userFolderEngine.Create(name, parentId);
                return userFolder;
            }
            catch (AlreadyExistsFolderException)
            {
                throw new ArgumentException(MailApiResource.ErrorUserFolderNameAlreadyExists
                    .Replace("%1", "\"" + name + "\""));
            }
            catch (EmptyFolderException)
            {
                throw new ArgumentException(MailApiResource.ErrorUserFoldeNameCantBeEmpty);
            }
            catch (Exception)
            {
                throw new Exception(MailApiErrorsResource.ErrorInternalServer);
            }
        }

        /// <summary>
        ///    Update user folder
        /// </summary>
        /// <param name="id">Folder id</param>
        /// <param name="name">new Folder name</param>
        /// <param name="parentId">new Parent folder id (default = 0)</param>
        /// <returns>Folders list</returns>
        /// <short>Update folder</short> 
        /// <category>Folders</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpPut(@"userfolders/{id}")]
        public MailUserFolderData UpdateUserFolder(int id, string name, int? parentId = null)
        {
            //Thread.CurrentThread.CurrentCulture = CurrentCulture;
            //Thread.CurrentThread.CurrentUICulture = CurrentCulture;

            try
            {
                var userFolder = _userFolderEngine.Update(id, name, parentId);
                return userFolder;
            }
            catch (AlreadyExistsFolderException)
            {
                throw new ArgumentException(MailApiResource.ErrorUserFolderNameAlreadyExists
                    .Replace("%1", "\"" + name + "\""));
            }
            catch (EmptyFolderException)
            {
                throw new ArgumentException(MailApiResource.ErrorUserFoldeNameCantBeEmpty);
            }
            catch (Exception)
            {
                throw new Exception(MailApiErrorsResource.ErrorInternalServer);
            }
        }

        /// <summary>
        ///    Delete user folder
        /// </summary>
        /// <param name="id">Folder id</param>
        /// <short>Delete folder</short> 
        /// <category>Folders</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        /// <returns>MailOperationResult object</returns>
        [HttpDelete(@"userfolders/{id}")]
        public MailOperationStatus DeleteUserFolder(int id)
        {
            Thread.CurrentThread.CurrentCulture = CurrentCulture;
            Thread.CurrentThread.CurrentUICulture = CurrentCulture;

            try
            {
                return _operationEngine.RemoveUserFolder(id, TranslateMailOperationStatus);
            }
            catch (Exception)
            {
                throw new Exception(MailApiErrorsResource.ErrorInternalServer);
            }
        }

        /// <summary>
        ///    Returns the user folders by mail id
        /// </summary>
        /// <param name="mailId">List of folder's id</param>
        /// <returns>User Folder</returns>
        /// <short>Get folder by mail id</short> 
        /// <category>Folders</category>
        [HttpGet(@"userfolders/bymail")]
        public MailUserFolderData GetUserFolderByMailId(uint mailId)
        {
            var folder = _userFolderEngine.GetByMail(mailId);
            return folder;
        }
    }
}
