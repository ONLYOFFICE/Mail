﻿using ASC.Core.Users;
using ASC.Mail.Core.Dao.Expressions.Mailbox;
using ASC.Mail.Core.Engine.Operations.Base;
using ASC.Mail.Enums;
using ASC.Mail.Models;
using ASC.Web.Studio.Core.Notify;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Linq;

namespace ASC.Mail.Controllers
{
    public partial class MailController : ControllerBase
    {
        /// <summary>
        ///    Returns ServerData for mail server associated with tenant
        /// </summary>
        /// <returns>ServerData for current tenant.</returns>
        /// <short>Get mail server</short> 
        /// <category>Servers</category>
        [HttpGet(@"server")]
        public ServerData GetMailServer()
        {
            return _serverEngine.GetMailServer();
        }

        /// <summary>
        ///    Returns ServerData for mail server associated with tenant
        /// </summary>
        /// <returns>ServerData for current tenant.</returns>
        /// <short>Get mail server</short> 
        /// <category>Servers</category>
        [HttpGet(@"serverinfo/get")]
        public ServerFullData GetMailServerFullInfo()
        {
            var fullServerInfo = _serverEngine.GetMailServerFullInfo();

            if (!_coreBaseSettings.Standalone)
                return fullServerInfo;

            var commonDomain = fullServerInfo.Domains.FirstOrDefault(d => d.IsSharedDomain);
            if (commonDomain == null)
                return fullServerInfo;

            //Skip common domain
            fullServerInfo.Domains = fullServerInfo.Domains.Where(d => !d.IsSharedDomain).ToList();
            fullServerInfo.Mailboxes =
                fullServerInfo.Mailboxes.Where(m => m.Address.DomainId != commonDomain.Id).ToList();

            return fullServerInfo;
        }

        /// <summary>
        ///    Get or generate free to any domain DNS records
        /// </summary>
        /// <returns>DNS records for current tenant and user.</returns>
        /// <short>Get free DNS records</short>
        /// <category>DnsRecords</category>
        [HttpGet(@"freedns/get")]
        public ServerDomainDnsData GetUnusedDnsRecords()
        {
            return _serverEngine.GetOrCreateUnusedDnsData();
        }

        /// <summary>
        ///    Returns list of the web domains associated with tenant
        /// </summary>
        /// <returns>List of WebDomainData for current tenant</returns>
        /// <short>Get tenant web domain list</short> 
        /// <category>Domains</category>
        [HttpGet(@"domains/get")]
        public List<ServerDomainData> GetDomains()
        {
            var listDomainData = _serverDomainEngine.GetDomains();

            if (_coreBaseSettings.Standalone)
            {
                //Skip common domain
                listDomainData = listDomainData.Where(d => !d.IsSharedDomain).ToList();
            }

            return listDomainData;
        }

        /// <summary>
        ///    Returns the common web domain
        /// </summary>
        /// <returns>WebDomainData for common web domain</returns>
        /// <short>Get common web domain</short> 
        /// <category>Domains</category>
        [HttpGet(@"domains/common")]
        public ServerDomainData GetCommonDomain()
        {
            var commonDomain = _serverDomainEngine.GetCommonDomain();
            return commonDomain;
        }

        /// <summary>
        ///    Associate a web domain with tenant
        /// </summary>
        /// <param name="name">web domain name</param>
        /// <param name="id_dns"></param>
        /// <returns>WebDomainData associated with tenant</returns>
        /// <short>Add domain to mail server</short> 
        /// <category>Domains</category>
        [HttpPost(@"domains/add")]
        public ServerDomainData AddDomain(string name, int id_dns)
        {
            var domain = _serverDomainEngine.AddDomain(name, id_dns);
            return domain;
        }

        /// <summary>
        ///    Deletes the selected web domain
        /// </summary>
        /// <param name="id">id of web domain</param>
        /// <returns>MailOperationResult object</returns>
        /// <short>Remove domain from mail server</short> 
        /// <category>Domains</category>
        [HttpDelete(@"domains/remove/{id}")]
        public MailOperationStatus RemoveDomain(int id)
        {
            var status = _serverDomainEngine.RemoveDomain(id);
            return status;
        }

        /// <summary>
        ///    Returns dns records associated with domain
        /// </summary>
        /// <param name="id">id of domain</param>
        /// <returns>Dns records associated with domain</returns>
        /// <short>Returns dns records</short>
        /// <category>DnsRecords</category>
        [HttpGet(@"domains/dns/get")]
        public ServerDomainDnsData GetDnsRecords(int id)
        {
            var dns = _serverDomainEngine.GetDnsData(id);
            return dns;
        }

        /// <summary>
        ///    Check web domain name existance
        /// </summary>
        /// <param name="name">web domain name</param>
        /// <returns>True if domain name already exists.</returns>
        /// <short>Is domain name exists.</short> 
        /// <category>Domains</category>
        [HttpGet(@"domains/exists")]
        public bool IsDomainExists(string name)
        {
            var isExists = _serverDomainEngine.IsDomainExists(name);
            return isExists;
        }

        /// <summary>
        ///    Check web domain name ownership over txt record in dns
        /// </summary>
        /// <param name="name">web domain name</param>
        /// <returns>True if user is owner of this domain.</returns>
        /// <short>Check domain ownership.</short> 
        /// <category>Domains</category>
        [HttpGet(@"domains/ownership/check")]
        public bool CheckDomainOwnership(string name)
        {
            var isOwnershipProven = _serverEngine.CheckDomainOwnership(name);
            return isOwnershipProven;
        }

        /// <summary>
        ///    Create mailbox
        /// </summary>
        /// <param name="name"></param>
        /// <param name="local_part"></param>
        /// <param name="domain_id"></param>
        /// <param name="user_id"></param>
        /// <param name="notifyCurrent">Send message to creating mailbox's address</param>
        /// <param name="notifyProfile">Send message to email from user profile</param>
        /// <returns>MailboxData associated with tenant</returns>
        /// <short>Create mailbox</short> 
        /// <category>Mailboxes</category>
        [HttpPost(@"mailboxes/add")]
        public ServerMailboxData CreateMailbox(string name, string local_part, int domain_id, string user_id,
            bool notifyCurrent = false, bool notifyProfile = false)
        {
            var serverMailbox = _serverMailboxEngine.CreateMailbox(name, local_part, domain_id, user_id);

            SendMailboxCreated(serverMailbox, notifyCurrent, notifyProfile);

            return serverMailbox;
        }

        /// <summary>
        ///    Create my mailbox
        /// </summary>
        /// <param name="name"></param>
        /// <returns>MailboxData associated with tenant</returns>
        /// <short>Create mailbox</short> 
        /// <category>Mailboxes</category>
        [HttpPost(@"mailboxes/addmy")]
        public ServerMailboxData CreateMyMailbox(string name)
        {
            var serverMailbox = _serverMailboxEngine.CreateMyCommonDomainMailbox(name);
            return serverMailbox;
        }

        /// <summary>
        ///    Returns list of the mailboxes associated with tenant
        /// </summary>
        /// <returns>List of MailboxData for current tenant</returns>
        /// <short>Get mailboxes list</short> 
        /// <category>Mailboxes</category>
        [HttpGet(@"mailboxes/get")]
        public List<ServerMailboxData> GetMailboxes()
        {
            var mailboxes = _serverMailboxEngine.GetMailboxes();
            return mailboxes;
        }

        /// <summary>
        ///    Deletes the selected mailbox
        /// </summary>
        /// <param name="id">id of mailbox</param>
        /// <returns>MailOperationResult object</returns>
        /// <exception cref="ArgumentException">Exception happens when some parameters are invalid. Text description contains parameter name and text description.</exception>
        /// <exception cref="ItemNotFoundException">Exception happens when mailbox wasn't found.</exception>
        /// <short>Remove mailbox from mail server</short> 
        /// <category>Mailboxes</category>
        [HttpDelete(@"mailboxes/remove/{id}")]
        public MailOperationStatus RemoveMailbox(int id)
        {
            var status = _serverMailboxEngine.RemoveMailbox(id);
            return status;
        }

        /// <summary>
        ///    Update mailbox
        /// </summary>
        /// <param name="mailbox_id">id of mailbox</param>
        /// <param name="name">sender name</param>
        /// <returns>Updated MailboxData</returns>
        /// <short>Update mailbox</short>
        /// <category>Mailboxes</category>
        [HttpPut(@"mailboxes/update")]
        public ServerMailboxData UpdateMailbox(int mailbox_id, string name)
        {
            var mailbox = _serverMailboxEngine.UpdateMailboxDisplayName(mailbox_id, name);
            return mailbox;
        }

        /// <summary>
        ///    Add alias to mailbox
        /// </summary>
        /// <param name="mailbox_id">id of mailbox</param>
        /// <param name="alias_name">name of alias</param>
        /// <returns>MailboxData associated with tenant</returns>
        /// <short>Add mailbox's aliases</short>
        /// <category>AddressData</category>
        [HttpPut(@"mailboxes/alias/add")]
        public ServerDomainAddressData AddMailboxAlias(int mailbox_id, string alias_name)
        {
            var serverAlias = _serverMailboxEngine.AddAlias(mailbox_id, alias_name);
            return serverAlias;
        }

        /// <summary>
        ///    Remove alias from mailbox
        /// </summary>
        /// <param name="mailbox_id">id of mailbox</param>
        /// <param name="address_id"></param>
        /// <returns>id of mailbox</returns>
        /// <short>Remove mailbox's aliases</short>
        /// <category>Mailboxes</category>
        [HttpPut(@"mailboxes/alias/remove")]
        public int RemoveMailboxAlias(int mailbox_id, int address_id)
        {
            _serverMailboxEngine.RemoveAlias(mailbox_id, address_id);

            return mailbox_id;
        }

        /// <summary>
        ///    Change mailbox password
        /// </summary>
        /// <param name="mailbox_id"></param>
        /// <param name="password"></param>
        /// <short>Change mailbox password</short> 
        /// <category>Mailboxes</category>
        [HttpPut(@"mailboxes/changepwd")]
        public void ChangeMailboxPassword(int mailbox_id, string password)
        {
            _serverMailboxEngine.ChangePassword(mailbox_id, password);

            SendMailboxPasswordChanged(mailbox_id);
        }

        /// <summary>
        ///    Check existence of mailbox address
        /// </summary>
        /// <param name="local_part"></param>
        /// <param name="domain_id"></param>
        /// <short>Is server mailbox address exists</short>
        /// <returns>True - address exists, False - not exists</returns>
        /// <category>Mailboxes</category>
        [HttpGet(@"mailboxes/alias/exists")]
        public bool IsAddressAlreadyRegistered(string local_part, int domain_id)
        {
            return _serverMailboxEngine.IsAddressAlreadyRegistered(local_part, domain_id);
        }

        /// <summary>
        ///    Validate mailbox address
        /// </summary>
        /// <param name="local_part"></param>
        /// <param name="domain_id"></param>
        /// <short>Is server mailbox address valid</short>
        /// <returns>True - address valid, False - not valid</returns>
        /// <category>Mailboxes</category>
        [HttpGet(@"mailboxes/alias/valid")]
        public bool IsAddressValid(string local_part, int domain_id)
        {
            return _serverMailboxEngine.IsAddressValid(local_part, domain_id);
        }

        /// <summary>
        ///    Create group address
        /// </summary>
        /// <param name="name"></param>
        /// <param name="domain_id"></param>
        /// <param name="address_ids"></param>
        /// <returns>MailGroupData associated with tenant</returns>
        /// <short>Create mail group address</short>
        /// <category>MailGroup</category>
        [HttpPost(@"groupaddress/add")]
        public ServerDomainGroupData CreateMailGroup(string name, int domain_id, List<int> address_ids)
        {
            var group = _serverMailgroupEngine.CreateMailGroup(name, domain_id, address_ids);

            return group;
        }

        /// <summary>
        ///    Add addresses to group
        /// </summary>
        /// <param name="mailgroup_id">id of group address</param>
        /// <param name="address_id"></param>
        /// <returns>MailGroupData associated with tenant</returns>
        /// <short>Add group's addresses</short> 
        /// <category>MailGroup</category>
        [HttpPut(@"groupaddress/address/add")]
        public ServerDomainGroupData AddMailGroupAddress(int mailgroup_id, int address_id)
        {
            var group = _serverMailgroupEngine.AddMailGroupMember(mailgroup_id, address_id);

            return group;
        }

        /// <summary>
        ///    Remove address from group
        /// </summary>
        /// <param name="mailgroup_id">id of group address</param>
        /// <param name="address_id"></param>
        /// <returns>id of group address</returns>
        /// <short>Remove group's address</short>
        /// <category>MailGroup</category>
        [HttpDelete(@"groupaddress/addresses/remove")]
        public int RemoveMailGroupAddress(int mailgroup_id, int address_id)
        {
            _serverMailgroupEngine.RemoveMailGroupMember(mailgroup_id, address_id);

            return address_id;
        }

        /// <summary>
        ///    Returns list of group addresses associated with tenant
        /// </summary>
        /// <returns>List of MailGroupData for current tenant</returns>
        /// <short>Get mail group list</short>
        /// <category>MailGroup</category>
        [HttpGet(@"groupaddress/get")]
        public List<ServerDomainGroupData> GetMailGroups()
        {
            var groups = _serverMailgroupEngine.GetMailGroups();

            return groups;
        }

        /// <summary>
        ///    Deletes the selected group address
        /// </summary>
        /// <param name="id">id of group address</param>
        /// <returns>id of group address</returns>
        /// <short>Remove group address from mail server</short> 
        /// <category>MailGroup</category>
        [HttpDelete(@"groupaddress/remove/{id}")]
        public int RemoveMailGroup(int id)
        {
            _serverMailgroupEngine.RemoveMailGroup(id);

            return id;
        }

        /// <summary>
        ///    Create address for tenant notifications
        /// </summary>
        /// <param name="name"></param>
        /// <param name="password"></param>
        /// <param name="domain_id"></param>
        /// <returns>NotificationAddressData associated with tenant</returns>
        /// <short>Create notification address</short> 
        /// <category>Notifications</category>
        [HttpPost(@"notification/address/add")]
        public ServerNotificationAddressData CreateNotificationAddress(string name, string password, int domain_id)
        {
            //need refactoring
            var notifyAddress = new ServerNotificationAddressData();

            //_serverEngine.CreateNotificationAddress(name, password, domain_id);
            return notifyAddress;
        }

        /// <summary>
        ///    Deletes address for notification 
        /// </summary>
        /// <short>Remove mailbox from mail server</short> 
        /// <category>Notifications</category>
        [HttpDelete(@"notification/address/remove")]
        public void RemoveNotificationAddress(string address)
        {
            _serverEngine.RemoveNotificationAddress(address);
        }

        private void SendMailboxCreated(ServerMailboxData serverMailbox, bool toMailboxUser, bool toUserProfile)
        {
            try
            {
                if (serverMailbox == null)
                    throw new ArgumentNullException("serverMailbox");

                if ((!toMailboxUser && !toUserProfile))
                    return;

                var emails = new List<string>();

                if (toMailboxUser)
                {
                    emails.Add(serverMailbox.Address.Email);
                }

                var userInfo = _userManager.GetUsers(new Guid(serverMailbox.UserId));

                if (userInfo == null || userInfo.Equals(Constants.LostUser))
                    throw new Exception(string.Format("SendMailboxCreated(mailboxId={0}): user not found",
                        serverMailbox.Id));

                if (toUserProfile)
                {
                    if (userInfo != null && !userInfo.Equals(Constants.LostUser))
                    {
                        if (!emails.Contains(userInfo.Email) &&
                            userInfo.ActivationStatus == EmployeeActivationStatus.Activated)
                        {
                            emails.Add(userInfo.Email);
                        }
                    }
                }

                var mailbox =
                    _mailboxEngine.GetMailboxData(
                        new ConcreteUserServerMailboxExp(serverMailbox.Id, TenantId, serverMailbox.UserId));

                if (mailbox == null)
                    throw new Exception(string.Format("SendMailboxCreated(mailboxId={0}): mailbox not found",
                        serverMailbox.Id));

                using var scope = _serviceProvider.CreateScope();
                var studioNotifyService = scope.ServiceProvider.GetService<StudioNotifyService>();

                if (_coreBaseSettings.Standalone)
                {
                    var encType = Enum.GetName(typeof(EncryptionType), mailbox.Encryption) ?? DefineConstants.START_TLS;

                    string mxHost = null;

                    try
                    {
                        mxHost = _serverEngine.GetMailServerMxDomain();
                    }
                    catch (Exception ex)
                    {
                        //_log.ErrorFormat("GetMailServerMxDomain() failed. Exception: {0}", ex.ToString());
                    }

                    studioNotifyService.SendMailboxCreated(emails, userInfo.DisplayUserName(_displayUserSettingsHelper),
                        mailbox.EMail.Address,
                        string.IsNullOrEmpty(mxHost) ? mailbox.Server : mxHost, encType.ToUpper(), mailbox.Port,
                        mailbox.SmtpPort, mailbox.Account);
                }
                else
                {
                    studioNotifyService.SendMailboxCreated(emails, userInfo.DisplayUserName(_displayUserSettingsHelper),
                        mailbox.EMail.Address);
                }

            }
            catch (Exception ex)
            {
                //_log.Error(ex.ToString());
            }
        }

        private void SendMailboxPasswordChanged(int mailboxId)
        {
            try
            {
                if (!_coreBaseSettings.Standalone)
                    return;

                if (mailboxId < 0)
                    throw new ArgumentNullException("mailboxId");

                var mailbox =
                    _mailboxEngine.GetMailboxData(
                        new ConcreteTenantServerMailboxExp(mailboxId, TenantId, false));

                if (mailbox == null)
                    throw new Exception(string.Format("SendMailboxPasswordChanged(mailboxId={0}): mailbox not found",
                        mailboxId));

                var userInfo = _userManager.GetUsers(new Guid(mailbox.UserId));

                if (userInfo == null || userInfo.Equals(Constants.LostUser))
                    throw new Exception(string.Format("SendMailboxPasswordChanged(mailboxId={0}): user not found",
                        mailboxId));

                var toEmails = new List<string>
                {
                    userInfo.ActivationStatus == EmployeeActivationStatus.Activated
                        ? userInfo.Email
                        : mailbox.EMail.Address
                };

                using var scope = _serviceProvider.CreateScope();
                var studioNotifyService = scope.ServiceProvider.GetService<StudioNotifyService>();

                studioNotifyService.SendMailboxPasswordChanged(toEmails,
                    userInfo.DisplayUserName(_displayUserSettingsHelper), mailbox.EMail.Address);
            }
            catch (Exception ex)
            {
                //_log.Error(ex.ToString());
            }
        }
    }
}
