﻿using System.Threading.Tasks;
using ESFA.DAS.ProvideFeedback.Data;
using ESFA.DAS.ProvideFeedback.Domain.Entities.Messages;
using Microsoft.Extensions.Logging;

namespace ESFA.DAS.Feedback.Employer.Emailer
{
    public class DataRefreshHelper
    {
        private ILogger<DataRefreshHelper> _logger;
        private IStoreEmployerEmailDetails _dbRepository;

        public DataRefreshHelper(
            ILogger<DataRefreshHelper> logger,
            IStoreEmployerEmailDetails dbRepository)
        {
            _logger = logger;
            _dbRepository = dbRepository;
        }

        public async Task RefreshFeedbackData(EmployerFeedbackRefreshMessage message)
        {
            _logger.LogInformation("Starting upserting users");
            await _dbRepository.UpsertIntoUsers(message.User);
            _logger.LogInformation("Done upserting users\nStarting upserting providers");
            await _dbRepository.UpsertIntoProviders(message.Provider);
            _logger.LogInformation("Done upserting providers");
        }
    }
}
