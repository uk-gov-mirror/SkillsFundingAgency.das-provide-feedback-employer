﻿using System.Linq;
using System.Threading.Tasks;
using ESFA.DAS.ProvideFeedback.Data;
using ESFA.DAS.ProvideFeedback.Domain.Entities.Messages;
using Microsoft.Extensions.Logging;

namespace ESFA.DAS.ProvideFeedback.Employer.Application
{
    public class UserRefreshService
    {
        private ILogger<UserRefreshService> _logger;
        private IStoreEmployerEmailDetails _dbRepository;

        public UserRefreshService(
            ILogger<UserRefreshService> logger,
            IStoreEmployerEmailDetails dbRepository)
        {
            _logger = logger;
            _dbRepository = dbRepository;
        }

        public async Task UpdateAccountUsers(GroupedFeedbackRefreshMessage message)
        {
            _logger.LogInformation("Starting upserting user");

            var user = message.RefreshMessages.First().User;
            await _dbRepository.UpsertIntoUsers(user);
            _logger.LogInformation("Done upserting user");
        }
    }
}
