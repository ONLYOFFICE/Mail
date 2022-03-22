/*
 *
 * (c) Copyright Ascensio System Limited 2010-2020
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/

using ASC.Common.Logging;
using ASC.Mail.Core.Dao.Expressions.Mailbox;
using ASC.Mail.Core.Engine;
using ASC.Mail.Models;

using System;

namespace ASC.Mail.Iterators
{
    public class MailboxIterator : IMailboxIterator
    {
        private readonly int _tenant;
        private readonly string _userId;
        private readonly bool? _isRemoved;
        private readonly ILog _log;

        private readonly int _minMailboxId;
        private readonly int _maxMailboxId;

        private readonly MailboxEngine _mailboxEngine;

        public MailboxIterator(MailboxEngine mailboxEngine, int tenant = -1, string userId = null, bool? isRemoved = false, ILog log = null)
        {
            if (!string.IsNullOrEmpty(userId) && tenant < 0)
                throw new ArgumentException("Tenant must be initialized if user not empty");

            _mailboxEngine = mailboxEngine;

            _tenant = tenant;
            _userId = userId;
            _isRemoved = isRemoved;

            _log = log ?? new NullLog();

            var result = _mailboxEngine.GetRangeMailboxes(GetMailboxExp(_tenant, _userId, _isRemoved));

            if (result == null)
                return;

            _minMailboxId = result.Item1;
            _maxMailboxId = result.Item2;

            Current = null;
        }

        // Gets first item
        public MailBoxData First()
        {
            if (_minMailboxId == 0 && _minMailboxId == _maxMailboxId)
            {
                return null;
            }

            var exp = GetMailboxExp(_minMailboxId, _tenant, _userId, _isRemoved);
            var mailbox = _mailboxEngine.GetMailboxData(exp);

            Current = mailbox == null && _minMailboxId < _maxMailboxId
                ? GetNextMailbox(_minMailboxId)
                : mailbox;

            return Current;
        }

        // Gets next item
        public MailBoxData Next()
        {
            if (IsDone)
                return null;

            Current = GetNextMailbox(Current.MailBoxId);

            return Current;
        }

        // Gets current iterator item
        public MailBoxData Current { get; private set; }

        // Gets whether iteration is complete
        public bool IsDone
        {
            get
            {
                return _minMailboxId == 0
                       || _minMailboxId > _maxMailboxId
                       || Current == null;
            }
        }

        private MailBoxData GetNextMailbox(int id)
        {
            do
            {
                if (id < _minMailboxId || id >= _maxMailboxId)
                    return null;

                MailBoxData mailbox;

                var exp = GetNextMailboxExp(id, _tenant, _userId, _isRemoved);

                int failedId;

                if (!_mailboxEngine.TryGetNextMailboxData(exp, out mailbox, out failedId))
                {
                    if (failedId > 0)
                    {
                        id = failedId;

                        _log.ErrorFormat("MailboxEngine.GetNextMailboxData(Mailbox id = {0}) failed. Skip it.", id);

                        id++;
                    }
                    else
                    {
                        _log.ErrorFormat("MailboxEngine.GetNextMailboxData(Mailbox id = {0}) failed. End seek next.", id);
                        return null;
                    }
                }
                else
                {
                    return mailbox;
                }

            } while (id <= _maxMailboxId);

            return null;
        }

        private static IMailboxExp GetMailboxExp(int tenant = -1, string user = null, bool? isRemoved = false)
        {
            IMailboxExp mailboxExp;

            if (!string.IsNullOrEmpty(user) && tenant > -1)
            {
                mailboxExp = new UserMailboxExp(tenant, user, isRemoved);
            }
            else if (tenant > -1)
            {
                mailboxExp = new TenantMailboxExp(tenant, isRemoved);
            }
            else if (!string.IsNullOrEmpty(user))
            {
                throw new ArgumentException("Tenant must be initialized if user not empty");
            }
            else
            {
                mailboxExp = new SimpleMailboxExp(isRemoved);
            }

            return mailboxExp;
        }

        private static IMailboxExp GetMailboxExp(int id, int tenant, string user = null, bool? isRemoved = false)
        {
            IMailboxExp mailboxExp;

            if (!string.IsNullOrEmpty(user) && tenant > -1)
            {
                mailboxExp = new СoncreteUserMailboxExp(id, tenant, user, isRemoved);
            }
            else if (tenant > -1)
            {
                mailboxExp = new ConcreteTenantMailboxExp(id, tenant, isRemoved);
            }
            else if (!string.IsNullOrEmpty(user))
            {
                throw new ArgumentException("Tenant must be initialized if user not empty");
            }
            else
            {
                mailboxExp = new ConcreteSimpleMailboxExp(id, isRemoved);
            }

            return mailboxExp;
        }

        private static IMailboxExp GetNextMailboxExp(int id, int tenant, string user = null, bool? isRemoved = false)
        {
            IMailboxExp mailboxExp;

            if (!string.IsNullOrEmpty(user) && tenant > -1)
            {
                mailboxExp = new СoncreteUserNextMailboxExp(id, tenant, user, isRemoved);
            }
            else if (tenant > -1)
            {
                mailboxExp = new ConcreteTenantNextMailboxExp(id, tenant, isRemoved);
            }
            else if (!string.IsNullOrEmpty(user))
            {
                throw new ArgumentException("Tenant must be initialized if user not empty");
            }
            else
            {
                mailboxExp = new ConcreteSimpleNextMailboxExp(id, isRemoved);
            }

            return mailboxExp;
        }
    }
}
