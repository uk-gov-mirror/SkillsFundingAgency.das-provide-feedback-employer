﻿using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http.Headers;
using ESFA.DAS.Feedback.Employer.Emailer;
using ESFA.DAS.Feedback.Employer.Emailer.Configuration;
using ESFA.DAS.ProvideFeedback.Data;
using ESFA.DAS.ProvideFeedback.Employer.Application;
using ESFA.DAS.ProvideFeedback.Employer.Application.Configuration;
using ESFA.DAS.ProvideFeedback.Employer.Functions.Emailer;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using SFA.DAS.Commitments.Api.Client;
using SFA.DAS.Commitments.Api.Client.Interfaces;
using SFA.DAS.EAS.Account.Api.Client;
using SFA.DAS.Http;
using SFA.DAS.Http.TokenGenerators;
using SFA.DAS.NLog.Targets.Redis.DotNetCore;
using SFA.DAS.Notifications.Api.Client;
using SFA.DAS.Notifications.Api.Client.Configuration;
using SFA.DAS.Providers.Api.Client;
using LogLevel = NLog.LogLevel;

[assembly: FunctionsStartup(typeof(Startup))]
namespace ESFA.DAS.ProvideFeedback.Employer.Functions.Emailer
{
    internal class Startup : FunctionsStartup
    {
        private IConfigurationRoot _configuration;
        public override void Configure(IFunctionsHostBuilder builder)
        {
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            builder.Services.AddTransient<IDbConnection>(c => new SqlConnection(_configuration.GetConnectionStringOrSetting("EmployerEmailStoreConnection")));

            builder.Services.AddLogging((options) =>
            {

                options.AddConfiguration(_configuration.GetSection("Logging"));
                options.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                options.AddNLog(new NLogProviderOptions
                {
                    CaptureMessageTemplates = true,
                    CaptureMessageProperties = true
                });

                options.AddConsole();

                ConfigureNLog();
            });

            builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            builder.Services.Configure<EmailSettings>(_configuration.GetSection("EmailSettings"));

            var notificationApiConfig = _configuration.GetSection("NotificationApi").Get<NotificationApiConfig>();

            builder.Services.AddHttpClient<INotificationsApi, NotificationsApi>(c =>
            {
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", notificationApiConfig.ClientToken);
            });

            builder.Services.AddSingleton<INotificationsApiClientConfiguration, NotificationsApiClientConfiguration>(a =>
                new NotificationsApiClientConfiguration
                {
                    ApiBaseUrl = notificationApiConfig.BaseUrl,
                    ClientToken = notificationApiConfig.ClientToken
                }
            );

            builder.Services.AddSingleton<EmployerSurveyInviteEmailer>();
            builder.Services.AddSingleton<EmployerSurveyReminderEmailer>();
            builder.Services.AddTransient<IStoreEmployerEmailDetails, EmployerFeedbackRepository>();
            builder.Services.AddTransient<EmployerFeedbackDataRetrievalService>();
            builder.Services.AddTransient<DataRefreshHelper>();
            builder.Services.AddTransient<SurveyInviteGenerator>();
            builder.Services.AddTransient<ProviderRefreshService>();

            var providerApiConfig = _configuration.GetSection("ProviderApi").Get<ProviderApiConfig>();
            builder.Services.AddSingleton<IProviderApiClient, ProviderApiClient>(a =>
                 new ProviderApiClient(providerApiConfig.BaseUrl)
            );

            var commitmentApiConfig = _configuration.GetSection("CommitmentApi").Get<CommitmentsApiClientConfig>();
            var bearerToken = (IGenerateBearerToken)new JwtBearerTokenGenerator(commitmentApiConfig);
            var httpClient = new HttpClientBuilder().WithDefaultHeaders().WithBearerAuthorisationHeader(bearerToken).Build();
            builder.Services.AddSingleton<IEmployerCommitmentApi, EmployerCommitmentApi>(a =>
                 new EmployerCommitmentApi(httpClient, commitmentApiConfig)
            );

            var accApiConfig = _configuration.GetSection("AccountApi").Get<AccountApiConfiguration>();
            builder.Services.AddSingleton<IAccountApiClient, AccountApiClient>(a =>
                 new AccountApiClient(accApiConfig)
            );
        }

        private void ConfigureNLog()
        {
            var appName = _configuration.GetConnectionStringOrSetting("AppName");
            var localLogPath = _configuration.GetConnectionStringOrSetting("LogDir");
            var env = _configuration.GetConnectionStringOrSetting("ASPNETCORE_ENVIRONMENT");
            var config = new LoggingConfiguration();

            if (string.IsNullOrEmpty(env))
            {
                AddLocalTarget(config, localLogPath, appName);
            }
            else
            {
                AddRedisTarget(config, appName, env);
            }

            LogManager.Configuration = config;
        }

        private static void AddLocalTarget(LoggingConfiguration config, string localLogPath, string appName)
        {
            InternalLogger.LogFile = Path.Combine(localLogPath, $"{appName}\\nlog-internal.{appName}.log");
            var fileTarget = new FileTarget("Disk")
            {
                FileName = Path.Combine(localLogPath, $"{appName}\\{appName}.${{shortdate}}.log"),
                Layout = "${longdate} [${uppercase:${level}}] [${logger}] - ${message} ${onexception:${exception:format=tostring}}"
            };
            config.AddTarget(fileTarget);

            config.AddRule(GetMinLogLevel(), LogLevel.Fatal, "Disk");
        }

        private static void AddRedisTarget(LoggingConfiguration config, string appName, string environment)
        {
            var target = new RedisTarget
            {
                Name = "RedisLog",
                AppName = appName,
                EnvironmentKeyName = "ASPNETCORE_ENVIRONMENT",
                ConnectionStringName = "Redis",
                IncludeAllProperties = true,
                Layout = "${message}"
            };

            config.AddTarget(target);
            config.AddRule(GetMinLogLevel(), LogLevel.Fatal, "RedisLog");
        }

        private static LogLevel GetMinLogLevel() => LogLevel.FromString("Info");
    }
}
