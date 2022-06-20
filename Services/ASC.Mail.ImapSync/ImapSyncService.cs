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
    private readonly ILog _log;
    private readonly IOptionsMonitor<ILog> _options;

    private readonly CancellationTokenSource _cancelTokenSource;

    private readonly ConcurrentDictionary<string, MailImapClient> clients;

    private readonly MailSettings _mailSettings;
    private readonly RedisClient _redisClient;
    private readonly RedisFactory _redisFactory;

    private readonly SignalrServiceClient _signalrServiceClient;

    private readonly IServiceProvider _serviceProvider;

    public ImapSyncService(IOptionsMonitor<ILog> options,
        RedisFactory redisFactory,
        MailSettings mailSettings,
        IServiceProvider serviceProvider,
        IOptionsSnapshot<SignalrServiceClient> optionsSnapshot,
        NlogCongigure mailLogCongigure)
    {
        _options = options;
        _redisFactory = redisFactory;
        _redisClient = redisFactory.GetRedisClient();
        _mailSettings = mailSettings;
        _serviceProvider = serviceProvider;
        _signalrServiceClient = optionsSnapshot.Get("mail");
        _signalrServiceClient.EnableSignalr = true;
        clients = new ConcurrentDictionary<string, MailImapClient>();

        _cancelTokenSource = new CancellationTokenSource();

        try
        {
            mailLogCongigure.Configure();

            _log = _options.Get("ASC.Mail.ImapSyncService");

            _log.Info("Service is ready.");
        }
        catch (Exception ex)
        {
            _log.FatalFormat("ImapSyncService error under construct: {0}", ex.ToString());

            throw;
        }
    }

    public Task RedisSubscribe(CancellationToken cancellationToken)
    {
        if (_redisClient == null) return StopAsync(cancellationToken);

        try
        {
            return _redisClient.SubscribeQueueKey<ASC.Mail.ImapSync.Models.RedisCachePubSubItem<CachedTenantUserMailBox>>(CreateNewClient);
        }
        catch (Exception ex)
        {
            _log.Error($"Didn`t subscribe to redis. Message: {ex.Message}");

            return StopAsync(cancellationToken);
        }
        finally
        {
            _log.Info("Try to subscribe redis...");
        }
    }

    public async Task CreateNewClient(ASC.Mail.ImapSync.Models.RedisCachePubSubItem<CachedTenantUserMailBox> redisCachePubSubItem)
    {
        _log.Debug($"Online Users count: {clients.Count}");

        var cashedTenantUserMailBox = redisCachePubSubItem.Object;

        if (cashedTenantUserMailBox == null) return;

        if (string.IsNullOrEmpty(cashedTenantUserMailBox.UserName)) return;

        if (clients.ContainsKey(cashedTenantUserMailBox.UserName))
        {
            if (clients[cashedTenantUserMailBox.UserName] == null)
            {
                _log.Debug($"User Activity -> {cashedTenantUserMailBox.UserName}, folder={cashedTenantUserMailBox.Folder}. Wait for client start...");
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
                _log.Debug($"User Activity -> {cashedTenantUserMailBox.UserName}, folder={cashedTenantUserMailBox.Folder}. Try to create client ...");

                return;
            }

            MailImapClient client;

            try
            {
                client = new MailImapClient(cashedTenantUserMailBox.UserName, cashedTenantUserMailBox.Tenant, _mailSettings, _serviceProvider, _signalrServiceClient, _cancelTokenSource.Token);

                if (client == null)
                {
                    clients.TryRemove(cashedTenantUserMailBox.UserName, out _);

                    await ClearUserRedis(cashedTenantUserMailBox.UserName);

                    _log.Info($"Can`t create Mail client for user {cashedTenantUserMailBox.UserName}.");
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

                _log.Error($"Create mail client for user {cashedTenantUserMailBox.UserName}. {ex}");
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

                _log.Info($"ImapSyncService. MailImapClient {clientKey} died and was remove.");
            }
            else
            {
                _log.Info($"ImapSyncService. MailImapClient {clientKey} died, bud wasn`t remove.");
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _log.Info("Start service\r\n");

            return RedisSubscribe(cancellationToken);
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);

            return StopAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _cancelTokenSource.Cancel();

            _log.Info("Stoping service\r\n");

        }
        catch (Exception ex)
        {
            _log.ErrorFormat("Stop service Error: {0}\r\n", ex.ToString());
        }
        finally
        {
            _log.Info("Stop service\r\n");
        }

        return Task.CompletedTask;
    }

    public async Task<int> ClearUserRedis(string UserName)
    {
        int result = 0;

        string RedisKey = "ASC.MailAction:" + UserName;

        var localRedisClient= _redisFactory.GetRedisClient();

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
            _log.Error($"ClearUserRedis exception: {ex}");
        }

        _log.Info($"Clear Redis: User={UserName} Count={result}");

        return result;
    }
}
