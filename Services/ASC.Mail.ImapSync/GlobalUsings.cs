global using ASC.Api.Core;
global using ASC.Common;
global using ASC.Common.Caching;
global using ASC.Common.DependencyInjection;
global using ASC.Common.Mapping;
global using ASC.Common.Utils;
global using ASC.Core;
global using ASC.Core.Notify.Signalr;
global using ASC.Core.Users;
global using ASC.Data.Storage;
global using ASC.Mail.Configuration;
global using ASC.Mail.Core;
global using ASC.Mail.Core.Dao.Expressions.Mailbox;
global using ASC.Mail.Core.Dao.Expressions.Message;
global using ASC.Mail.Core.Dao.Interfaces;
global using ASC.Mail.Core.Engine;
global using ASC.Mail.Core.Entities;
global using ASC.Mail.Core.Search;
global using ASC.Mail.Enums;
global using ASC.Mail.Extensions;
global using ASC.Mail.ImapSync;
global using ASC.Mail.ImapSync.Loggers;
global using ASC.Mail.Models;
global using ASC.Mail.Storage;
global using ASC.Mail.Utils;

global using Autofac;
global using Autofac.Extensions.DependencyInjection;

global using MailKit;
global using MailKit.Net.Imap;
global using MailKit.Security;

global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;

global using MimeKit;

global using StackExchange.Redis;
global using StackExchange.Redis.Extensions.Core.Abstractions;
global using StackExchange.Redis.Extensions.Core.Configuration;
global using StackExchange.Redis.Extensions.Core.Implementations;
global using StackExchange.Redis.Extensions.Newtonsoft;

global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.IO;
global using System.Linq;
global using System.Net;
global using System.Reflection;
global using System.Runtime.InteropServices;
global using System.Security.Authentication;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;

global using ILogger = Microsoft.Extensions.Logging.ILogger;

public delegate MimeMessage ConvertMessageToMimeMessage(int messageId, SimpleImapClient simpleImapClient);
