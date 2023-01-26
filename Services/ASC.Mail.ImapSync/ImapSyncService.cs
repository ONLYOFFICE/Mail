/*
 *
 * (c) Copyright Ascensio System Limited 2010-2020
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

namespace ASC.Mail.ImapSync;

[Singletone]
public class ImapSyncService : IHostedService
{
    private readonly ILogger _log;
    private readonly ILoggerProvider _logProvider;

    private readonly CancellationTokenSource _cancelTokenSource;

    private readonly ConcurrentDictionary<string, MailImapClient> clients;

    private readonly MailSettings _mailSettings;
    private readonly RedisClient _redisClient;

    private readonly SocketServiceClient _signalrServiceClient;

    private readonly IServiceProvider _serviceProvider;

    public ImapSyncService(
        RedisClient redisClient,
        MailSettings mailSettings,
        IServiceProvider serviceProvider,
        IOptionsSnapshot<SocketServiceClient> optionsSnapshot,
        ILoggerProvider loggerProvider)
    {
        _redisClient = redisClient;
        _mailSettings = mailSettings;
        _serviceProvider = serviceProvider;
        _signalrServiceClient = optionsSnapshot.Get("mail");
        clients = new ConcurrentDictionary<string, MailImapClient>();

        _cancelTokenSource = new CancellationTokenSource();

        try
        {
            _log = loggerProvider.CreateLogger("ASC.Mail.ImapSyncService");
            _logProvider = loggerProvider;

            _log.InfoImapSyncService("created");
        }
        catch (Exception ex)
        {
            _log.CritImapSyncServiceConstruct(ex.ToString());

            throw;
        }
    }

    public Task RedisSubscribe(CancellationToken cancellationToken)
    {
        if (_redisClient == null)
        {
            _log.CritImapSyncServiceConstruct("Don't connect to Redis.");

            return StopAsync(cancellationToken);
        }
        try
        {
            var result = _redisClient.SubscribeQueueKey<Models.RedisCachePubSubItem<CachedTenantUserMailBox>>(CreateNewClient);

            _log.InfoImapSyncService("subscrube to Redis chanel");

            return result;
        }
        catch (Exception ex)
        {
            _log.CritImapSyncServiceConstruct($"Don't subscribe to Redis chanel.\n {ex.Message}");

            return StopAsync(cancellationToken);
        }
    }

    public async Task CreateNewClient(Models.RedisCachePubSubItem<CachedTenantUserMailBox> redisCachePubSubItem)
    {
        _log.DebugImapSyncServiceOnlineUsersCount(clients.Count);

        var cashedTenantUserMailBox = redisCachePubSubItem.Object;

        if (string.IsNullOrEmpty(cashedTenantUserMailBox?.UserName)) return;

        if (clients.ContainsKey(cashedTenantUserMailBox.UserName))
        {
            if (clients[cashedTenantUserMailBox.UserName] == null)
            {
                _log.DebugImapSyncServiceWaitForClient(cashedTenantUserMailBox.UserName, cashedTenantUserMailBox.Folder);
            }
            else
            {
                await clients[cashedTenantUserMailBox.UserName]?.CheckRedis();
            }
            return;
        }
        else
        {
            if (!clients.TryAdd(cashedTenantUserMailBox.UserName, null))
            {
                _log.InfoImapSyncService($"create new client for {cashedTenantUserMailBox.UserName}");

                return;
            }
            MailImapClient client;

            try
            {
                client = new MailImapClient(
                    cashedTenantUserMailBox.UserName,
                    cashedTenantUserMailBox.Tenant,
                    _mailSettings,
                    _serviceProvider,
                    _signalrServiceClient,
                    _cancelTokenSource.Token,
                    _logProvider,
                    _redisClient);

                if (client == null)
                {
                    clients.TryRemove(cashedTenantUserMailBox.UserName, out _);

                    await ClearUserRedis(cashedTenantUserMailBox.UserName);
                }
                else
                {
                    clients.TryUpdate(cashedTenantUserMailBox.UserName, client, null);

                    client.OnCriticalError += Client_DeleteClient;
                }
            }
            catch (Exception ex)
            {
                clients.TryRemove(cashedTenantUserMailBox.UserName, out _);

                await ClearUserRedis(cashedTenantUserMailBox.UserName);

                _log.ErrorImapSyncCreateClient($"user {cashedTenantUserMailBox.UserName}. {ex}");
            }
        }
    }

    private void Client_DeleteClient(object sender, EventArgs e)
    {
        if (sender is MailImapClient client)
        {
            var clientKey = client?.UserName;

            if (clients.TryRemove(clientKey, out MailImapClient trashValue))
            {
                trashValue.OnCriticalError -= Client_DeleteClient;
                trashValue?.Stop();

                _log.InfoImapSyncService($"delete client {clientKey}");
            }
            else
            {
                _log.InfoImapSyncService($"client {clientKey} died but don`t remove.");
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => RedisSubscribe(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _cancelTokenSource.Cancel();

            _log.InfoImapSyncService("stopping");

        }
        catch (Exception ex)
        {
            _log.ErrorImapSyncServiceStop(ex.ToString());
        }

        return Task.CompletedTask;
    }

    public async Task<int> ClearUserRedis(string UserName)
    {
        int result = 0;
        string RedisKey = "ASC.MailAction:" + UserName;

        var localRedisClient = _redisClient;

        if (localRedisClient == null) return 0;

        try
        {
            while (true)
            {
                var actionFromCache = await localRedisClient.PopFromQueue<CashedMailUserAction>(RedisKey);

                if (actionFromCache == null) break;

                result++;
            }

        }
        catch (Exception ex)
        {
            _log.ErrorImapSyncServiceStop(ex.ToString());
        }

        return result;
    }
}
