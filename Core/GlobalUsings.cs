﻿global using ASC.Api.Core;
global using ASC.Common;
global using ASC.Common.Caching;
global using ASC.Common.Security.Authentication;
global using ASC.Common.Security.Authorizing;
global using ASC.Common.Threading;
global using ASC.Common.Utils;
global using ASC.Common.Web;
global using ASC.Core;
global using ASC.Core.Billing;
global using ASC.Core.Common;
global using ASC.Core.Common.Configuration;
global using ASC.Core.Common.EF;
global using ASC.Core.Common.EF.Model;
global using ASC.Core.Common.Settings;
global using ASC.Core.Notify.Socket;
global using ASC.Core.Tenants;
global using ASC.Core.Users;
global using ASC.Data.Storage;
global using ASC.ElasticSearch;
global using ASC.ElasticSearch.Core;
global using ASC.FederatedLogin;
global using ASC.FederatedLogin.Helpers;
global using ASC.FederatedLogin.LoginProviders;
global using ASC.Files.Core;
global using ASC.Mail.Authorization;
global using ASC.Mail.Clients;
global using ASC.Mail.Clients.Imap;
global using ASC.Mail.Configuration;
global using ASC.Mail.Core.Dao;
global using ASC.Mail.Core.Dao.Context;
global using ASC.Mail.Core.Dao.Entities;
global using ASC.Mail.Core.Dao.Expressions;
global using ASC.Mail.Core.Dao.Expressions.Attachment;
global using ASC.Mail.Core.Dao.Expressions.Contact;
global using ASC.Mail.Core.Dao.Expressions.Conversation;
global using ASC.Mail.Core.Dao.Expressions.Mailbox;
global using ASC.Mail.Core.Dao.Expressions.Message;
global using ASC.Mail.Core.Dao.Expressions.UserFolder;
global using ASC.Mail.Core.Dao.Interfaces;
global using ASC.Mail.Core.Engine;
global using ASC.Mail.Core.Engine.Operations;
global using ASC.Mail.Core.Engine.Operations.Base;
global using ASC.Mail.Core.Entities;
global using ASC.Mail.Core.Exceptions;
global using ASC.Mail.Core.Loggers;
global using ASC.Mail.Core.MailServer.Core.Dao;
global using ASC.Mail.Core.Resources;
global using ASC.Mail.Core.Search;
global using ASC.Mail.Enums;
global using ASC.Mail.Enums.Filter;
global using ASC.Mail.Exceptions;
global using ASC.Mail.Extensions;
global using ASC.Mail.Iterators;
global using ASC.Mail.Models;
global using ASC.Mail.Models.Base;
global using ASC.Mail.Server.Core.Dao;
global using ASC.Mail.Server.Core.Dao.Interfaces;
global using ASC.Mail.Server.Core.Entities;
global using ASC.Mail.Server.Exceptions;
global using ASC.Mail.Server.Utils;
global using ASC.Mail.Storage;
global using ASC.Mail.Utils;
global using ASC.Security.Cryptography;
global using ASC.Web.Core;
global using ASC.Web.Core.Files;
global using ASC.Web.Core.PublicResources;
global using ASC.Web.Core.Users;
global using ASC.Web.Core.Utility;
global using ASC.Web.Core.Utility.Skins;
global using ASC.Web.Files.Classes;
global using ASC.Web.Files.Services.WCFService;
global using ASC.Web.Studio.Core;
global using ASC.Web.Studio.Utility;

global using DotNetOpenAuth.Messaging;
global using DotNetOpenAuth.OAuth2;

global using HtmlAgilityPack;

global using ICSharpCode.SharpZipLib.Zip;

global using MailKit;
global using MailKit.Net.Imap;
global using MailKit.Net.Pop3;
global using MailKit.Net.Smtp;
global using MailKit.Search;
global using MailKit.Security;

global using Microsoft.AspNetCore.Http;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Storage;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;

global using MimeKit;
global using MimeKit.Text;
global using MimeKit.Tnef;

global using Nest;

global using Newtonsoft.Json;
global using Newtonsoft.Json.Converters;
global using Newtonsoft.Json.Linq;
global using Newtonsoft.Json.Serialization;

global using RestSharp;

global using System;
global using System.CodeDom.Compiler;
global using System.Collections;
global using System.Collections.Generic;
global using System.Collections.Specialized;
global using System.ComponentModel;
global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
global using System.Configuration;
global using System.Data;
global using System.Diagnostics;
global using System.Drawing;
global using System.Globalization;
global using System.IO;
global using System.Linq;
global using System.Linq.Expressions;
global using System.Net;
global using System.Net.Mail;
global using System.Net.Security;
global using System.Reflection;
global using System.Runtime.Caching;
global using System.Runtime.Serialization;
global using System.Runtime.Serialization.Json;
//global using System.Security;
global using System.Security.Authentication;
global using System.Security.Cryptography;
global using System.Security.Cryptography.X509Certificates;
global using System.Text;
global using System.Text.RegularExpressions;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Web;
global using System.Xml;
global using System.Xml.Serialization;

global using Ude;

global using ILogger = Microsoft.Extensions.Logging.ILogger;
global using LogLevel = Microsoft.Extensions.Logging.LogLevel;
