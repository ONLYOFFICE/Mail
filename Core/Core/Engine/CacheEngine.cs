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

namespace ASC.Mail.Core.Engine;

[Singletone]
public class CacheEngine
{
    private readonly ICache _cache;
    private readonly ICacheNotify<AccountCacheItem> _cacheNotifyItem;
    private readonly TimeSpan _cacheExpiration;
    private static readonly Regex AllReg = new Regex(".*", RegexOptions.Compiled);

    public CacheEngine(ICacheNotify<AccountCacheItem> notify, ICache cache)
    {
        _cache = cache;

        _cacheNotifyItem = notify;

        _cacheExpiration = TimeSpan.FromMinutes(20);

        _cacheNotifyItem.Subscribe((i) =>
        {
            if (string.IsNullOrEmpty(i.Key))
            {
                _cache.Remove(AllReg);
            }
            else
            {
                _cache.Remove(i.Key);
            }
        }, CacheNotifyAction.Remove);
    }

    public List<AccountInfo> Get(string username)
    {
        return _cache.Get<List<AccountInfo>>(username);
    }

    public void Set(string username, List<AccountInfo> accounts)
    {
        _cache.Insert(username, accounts, _cacheExpiration);
    }

    public void Clear(string username)
    {
        _cacheNotifyItem.Publish(new AccountCacheItem { Key = username }, CacheNotifyAction.Remove);
    }

    public void ClearAll()
    {
        _cacheNotifyItem.Publish(new AccountCacheItem(), CacheNotifyAction.Remove);
    }
}
