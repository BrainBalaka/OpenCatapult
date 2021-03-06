﻿// Copyright (c) Polyrific, Inc 2018. All rights reserved.

using Newtonsoft.Json;
using Polyrific.Catapult.Api.Core.Entities;
using Polyrific.Catapult.Api.Core.Exceptions;
using Polyrific.Catapult.Api.Core.Repositories;
using Polyrific.Catapult.Api.Core.Security;
using Polyrific.Catapult.Api.Core.Specifications;
using Polyrific.Catapult.Shared.Dto.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Polyrific.Catapult.Api.Core.Services
{
    public class JobDefinitionService : IJobDefinitionService
    {
        private readonly IJobDefinitionRepository _jobDefinitionRepository;
        private readonly IJobTaskDefinitionRepository _jobTaskDefinitionRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ITaskProviderRepository _providerRepository;
        private readonly IExternalServiceRepository _externalServiceRepository;
        private readonly ITaskProviderAdditionalConfigRepository _providerAdditionalConfigRepository;
        private readonly ISecretVault _secretVault;

        private readonly List<(string, string[])> _allowedTaskTypes = new List<(string, string[])>
        {
            ( TaskProviderType.GeneratorProvider, new string[] { JobTaskDefinitionType.Generate } ),
            ( TaskProviderType.RepositoryProvider, new string[] { JobTaskDefinitionType.Pull, JobTaskDefinitionType.Push, JobTaskDefinitionType.Merge, JobTaskDefinitionType.DeleteRepository } ),
            ( TaskProviderType.BuildProvider, new string[] { JobTaskDefinitionType.Build,  } ),
            ( TaskProviderType.StorageProvider, new string[] { JobTaskDefinitionType.PublishArtifact } ),
            ( TaskProviderType.HostingProvider, new string[] { JobTaskDefinitionType.Deploy, JobTaskDefinitionType.DeleteHosting } ),
            ( TaskProviderType.DatabaseProvider, new string[] { JobTaskDefinitionType.DeployDb } ),
            ( TaskProviderType.TestProvider, new string[] { JobTaskDefinitionType.Test } ),
            ( TaskProviderType.GenericTaskProvider, new string[] { JobTaskDefinitionType.CustomTask } )
        };

        private readonly List<string> _deleteTaskTypes = new List<string>
        {
            JobTaskDefinitionType.DeleteRepository,
            JobTaskDefinitionType.DeleteHosting
        };
        
        public JobDefinitionService(IJobDefinitionRepository dataModelRepository,
            IJobTaskDefinitionRepository jobTaskDefinitionRepository,
            IProjectRepository projectRepository,
            ITaskProviderRepository providerRepository,
            IExternalServiceRepository externalServiceRepository,
            ITaskProviderAdditionalConfigRepository providerAdditionalConfigRepository,
            ISecretVault secretVault)
        {
            _jobDefinitionRepository = dataModelRepository;
            _jobTaskDefinitionRepository = jobTaskDefinitionRepository;
            _projectRepository = projectRepository;
            _providerRepository = providerRepository;
            _externalServiceRepository = externalServiceRepository;
            _providerAdditionalConfigRepository = providerAdditionalConfigRepository;
            _secretVault = secretVault;
        }

        public async Task<int> AddJobDefinition(int projectId, string name, bool isDeletion, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var project = await _projectRepository.GetById(projectId, cancellationToken);
            if (project == null)
            {
                throw new ProjectNotFoundException(projectId);
            }

            var projectJobDefinitionPropertyByProjectSpec = new JobDefinitionFilterSpecification(name, projectId);
            if (await _jobDefinitionRepository.CountBySpec(projectJobDefinitionPropertyByProjectSpec, cancellationToken) > 0)
            {
                throw new DuplicateJobDefinitionException(name);
            }

            if (isDeletion)
            {
                var deletionJobDefinitionSpec = new JobDefinitionFilterSpecification(projectId, true);
                if (await _jobDefinitionRepository.CountBySpec(deletionJobDefinitionSpec, cancellationToken) > 0)
                {
                    throw new MultipleDeletionJobException();
                }
            }

            var newJobDefinition = new JobDefinition { ProjectId = projectId, Name = name, IsDeletion = isDeletion };
            return await _jobDefinitionRepository.Create(newJobDefinition, cancellationToken);
        }

        public async Task DeleteJobDefinition(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var taskByJobSpec = new JobTaskDefinitionFilterSpecification(id);
            var tasks = await _jobTaskDefinitionRepository.GetBySpec(taskByJobSpec, cancellationToken);
            foreach (var task in tasks.ToList())
            {
                await DeleteJobTaskDefinition(task.Id, cancellationToken);
            }

            await _jobDefinitionRepository.Delete(id, cancellationToken);
        }

        public async Task DeleteJobDefinitions(int projectId, int[] jobIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jobDefinitionSpec = new JobDefinitionFilterSpecification(projectId, jobIds);
            var jobDefinitions = (await _jobDefinitionRepository.GetBySpec(jobDefinitionSpec, cancellationToken)).ToList();

            foreach (var jobDefinition in jobDefinitions)
            {
                await DeleteJobDefinition(jobDefinition.Id, cancellationToken);
            }
        }

        public async Task<JobDefinition> GetJobDefinitionById(int modelId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await _jobDefinitionRepository.GetById(modelId, cancellationToken);
        }

        public async Task<JobDefinition> GetJobDefinitionByName(int projectId, string jobDefinitionName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jobDefinitionSpec = new JobDefinitionFilterSpecification(jobDefinitionName, projectId);
            jobDefinitionSpec.Includes.Add(j => j.Tasks);
            return await _jobDefinitionRepository.GetSingleBySpec(jobDefinitionSpec, cancellationToken);
        }

        public async Task<JobDefinition> GetDeletionJobDefinition(int projectId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jobDefinitionSpec = new JobDefinitionFilterSpecification(projectId, true);
            return await _jobDefinitionRepository.GetSingleBySpec(jobDefinitionSpec, cancellationToken);
        }

        public async Task<List<JobDefinition>> GetJobDefinitions(int projectId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jobDefinitionSpec = new JobDefinitionFilterSpecification(projectId);
            var projectMembers = await _jobDefinitionRepository.GetBySpec(jobDefinitionSpec, cancellationToken);

            return projectMembers.ToList();
        }

        public async Task RenameJobDefinition(int id, string newName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dataModel = await _jobDefinitionRepository.GetById(id, cancellationToken);

            if (dataModel != null)
            {
                var dataModelByNameSpec = new JobDefinitionFilterSpecification(newName, dataModel.ProjectId, id);
                if (await _jobDefinitionRepository.CountBySpec(dataModelByNameSpec, cancellationToken) > 0)
                {
                    throw new DuplicateJobDefinitionException(newName);
                }

                dataModel.Name = newName;
                await _jobDefinitionRepository.Update(dataModel, cancellationToken);
            }
        }

        public async Task<int> AddJobTaskDefinition(JobTaskDefinition jobTaskDefinition, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jobDefinition = await _jobDefinitionRepository.GetById(jobTaskDefinition.JobDefinitionId, cancellationToken);
            if (jobDefinition == null)
            {
                throw new JobDefinitionNotFoundException(jobTaskDefinition.JobDefinitionId);
            }

            if (jobTaskDefinition.Sequence == null)
                jobTaskDefinition.Sequence = _jobTaskDefinitionRepository.GetMaxTaskSequence(jobTaskDefinition.JobDefinitionId) + 1;

            await ValidateJobTaskDefinition(jobDefinition, jobTaskDefinition, cancellationToken);

            return await _jobTaskDefinitionRepository.Create(jobTaskDefinition, cancellationToken);
        }

        public async Task<List<int>> AddJobTaskDefinitions(int jobDefinitionId, List<JobTaskDefinition> jobTaskDefinitions, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jobDefinition = await _jobDefinitionRepository.GetById(jobDefinitionId, cancellationToken);
            if (jobDefinition == null)
            {
                throw new JobDefinitionNotFoundException(jobDefinitionId);
            }

            var duplicateTasks = jobTaskDefinitions.GroupBy(x => x.Name.ToLower())
              .Where(g => g.Count() > 1)
              .Select(y => y.Key)
              .ToList();
            if (duplicateTasks.Count > 0)
            {
                throw new DuplicateJobTaskDefinitionException(string.Join(DataDelimiter.Comma.ToString(), duplicateTasks));
            }

            int currentMax = _jobTaskDefinitionRepository.GetMaxTaskSequence(jobDefinitionId);
            int newMax = jobTaskDefinitions.Max(t => t.Sequence) ?? 0;
            int maxSequence = Math.Max(currentMax, newMax);

            foreach (var task in jobTaskDefinitions)
            {
                if (task.Sequence == null)
                    task.Sequence = ++maxSequence;

                await ValidateJobTaskDefinition(jobDefinition, task, cancellationToken);
            }                

            jobTaskDefinitions.ForEach(j => j.JobDefinitionId = jobDefinitionId);

            return await _jobTaskDefinitionRepository.CreateRange(jobTaskDefinitions, cancellationToken);
        }

        public async Task UpdateJobTaskDefinition(JobTaskDefinition editedJobTaskDefinition, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jobTaskDefinitionSpec = new JobTaskDefinitionFilterSpecification(editedJobTaskDefinition.JobDefinitionId, editedJobTaskDefinition.Id);
            jobTaskDefinitionSpec.Includes.Add(t => t.JobDefinition);
            var jobTaskDefinition = await _jobTaskDefinitionRepository.GetSingleBySpec(jobTaskDefinitionSpec, cancellationToken);

            if (jobTaskDefinition != null)
            {
                jobTaskDefinition.Name = editedJobTaskDefinition.Name;
                jobTaskDefinition.Type = editedJobTaskDefinition.Type;
                jobTaskDefinition.Provider = editedJobTaskDefinition.Provider;
                jobTaskDefinition.ConfigString = editedJobTaskDefinition.ConfigString;
                jobTaskDefinition.AdditionalConfigString = editedJobTaskDefinition.AdditionalConfigString;
                jobTaskDefinition.Sequence = editedJobTaskDefinition.Sequence;
                
                await ValidateJobTaskDefinition(jobTaskDefinition.JobDefinition, jobTaskDefinition, cancellationToken);

                await _jobTaskDefinitionRepository.Update(jobTaskDefinition, cancellationToken);
            }
        }

        public async Task UpdateJobTaskConfig(int taskDefinitionId, Dictionary<string, string> jobTaskConfig, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jobTaskDefinition = await _jobTaskDefinitionRepository.GetById(taskDefinitionId, cancellationToken);

            if (jobTaskDefinition != null)
            {
                jobTaskDefinition.ConfigString = JsonConvert.SerializeObject(jobTaskConfig);

                await ValidateJobTaskDefinition(null, jobTaskDefinition, cancellationToken);

                await _jobTaskDefinitionRepository.Update(jobTaskDefinition, cancellationToken);
            }
        }

        public async Task DeleteJobTaskDefinition(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _jobTaskDefinitionRepository.Delete(id, cancellationToken);
        }

        public async Task<List<JobTaskDefinition>> GetJobTaskDefinitions(int jobDefinitionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var taskByJobSpec = new JobTaskDefinitionFilterSpecification(jobDefinitionId);
            var tasks = (await _jobTaskDefinitionRepository.GetBySpec(taskByJobSpec, cancellationToken)).ToList();

            foreach (var task in tasks)
                await DecryptSecretAdditionalConfigs(task);

            return tasks;
        }

        public async Task<JobTaskDefinition> GetJobTaskDefinitionById(int jobTaskDefinitionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var taskDefinition = await _jobTaskDefinitionRepository.GetById(jobTaskDefinitionId, cancellationToken);
            await DecryptSecretAdditionalConfigs(taskDefinition);
            return taskDefinition;
        }

        public async Task<JobTaskDefinition> GetJobTaskDefinitionByName(int jobDefinitionId, string jobTaskDefinitionName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var taskDefinition = await _jobTaskDefinitionRepository.GetSingleBySpec(new JobTaskDefinitionFilterSpecification(jobDefinitionId, jobTaskDefinitionName, 0), cancellationToken);
            await DecryptSecretAdditionalConfigs(taskDefinition);
            return taskDefinition;
        }

        public async Task ValidateJobTaskDefinition(JobDefinition jobDefinition, JobTaskDefinition jobTaskDefinition, CancellationToken cancellationToken = default(CancellationToken))
        {
            // validate task type
            bool isTaskTypeNotFitIntoJob = jobDefinition != null && jobTaskDefinition.Type != JobTaskDefinitionType.CustomTask && 
                ((jobDefinition.IsDeletion && !_deleteTaskTypes.Contains(jobTaskDefinition.Type)) ||
                (!jobDefinition.IsDeletion && _deleteTaskTypes.Contains(jobTaskDefinition.Type)));

            if (isTaskTypeNotFitIntoJob)
            {
                throw new JobTaskDefinitionTypeException(jobDefinition.IsDeletion, jobTaskDefinition.Type);
            }

            // Validate task name
            var taskSpec = new JobTaskDefinitionFilterSpecification(jobTaskDefinition.JobDefinitionId, jobTaskDefinition.Name, jobTaskDefinition.Id);
            if (await _jobTaskDefinitionRepository.CountBySpec(taskSpec, cancellationToken) > 0)
                throw new DuplicateJobTaskDefinitionException(jobTaskDefinition.Name);

            // validate task provider
            var providerSpec = new TaskProviderFilterSpecification(jobTaskDefinition.Provider, null);
            var provider = await _providerRepository.GetSingleBySpec(providerSpec, cancellationToken);

            if (provider == null)
            {
                throw new TaskProviderNotInstalledException(jobTaskDefinition.Provider);
            }

            var allowedTaskType = _allowedTaskTypes.FirstOrDefault(t => t.Item1.ToLower() == provider.Type.ToLower());
            if (allowedTaskType.Equals(default((string, string[]))))
            {
                throw new InvalidTaskProviderTypeException(provider.Type, jobTaskDefinition.Provider);
            }
            else if (!allowedTaskType.Item2.Any(taskType => jobTaskDefinition.Type.ToLower() == taskType.ToLower()))
            {
                throw new InvalidTaskProviderTypeException(provider.Type, jobTaskDefinition.Provider, allowedTaskType.Item2);
            }

            // validate external service
            if (!string.IsNullOrEmpty(provider.RequiredServicesString))
            {
                var requiredServices = provider.RequiredServicesString.Split(DataDelimiter.Comma);
                if (string.IsNullOrEmpty(jobTaskDefinition.ConfigString))
                {
                    throw new ExternalServiceRequiredException(requiredServices[0], provider.Name);
                }

                var config = JsonConvert.DeserializeObject<Dictionary<string, string>>(jobTaskDefinition.ConfigString);
                foreach (var requiredService in requiredServices)
                {
                    config.TryGetValue(GetServiceTaskConfigKey(requiredService), out var serviceName);

                    if (string.IsNullOrEmpty(serviceName))
                    {
                        throw new ExternalServiceRequiredException(requiredService, provider.Name);
                    }

                    var serviceSpec = new ExternalServiceFilterSpecification(0, serviceName);
                    serviceSpec.Includes.Add(x => x.ExternalServiceType);
                    var service = await _externalServiceRepository.GetSingleBySpec(serviceSpec, cancellationToken);

                    if (service == null)
                    {
                        throw new ExternalServiceNotFoundException(serviceName);
                    }

                    if (service.ExternalServiceType.Name != requiredService && service.ExternalServiceType.Name.ToLower() != ExternalServiceTypeName.Generic.ToLower())
                    {
                        throw new IncorrectExternalServiceTypeException(serviceName, requiredService);
                    }
                }
            }

            // Validate task config
            if (provider.Type == TaskProviderType.RepositoryProvider)
            {
                var taskConfig = !string.IsNullOrEmpty(jobTaskDefinition.ConfigString) ?
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(jobTaskDefinition.ConfigString) : null;

                if (taskConfig == null || !taskConfig.ContainsKey("Repository") || string.IsNullOrEmpty(taskConfig["Repository"]))
                {
                    throw new TaskConfigRequiredException(jobTaskDefinition.Type, "Repository");
                }
            }

            // validate additional config
            var additionalConfigsDefinitionSpec = new TaskProviderAdditionalConfigFilterSpecification(provider.Id);
            var additionalConfigsDefinition = await _providerAdditionalConfigRepository.GetBySpec(additionalConfigsDefinitionSpec, cancellationToken);
            var requiredConfigs = additionalConfigsDefinition.Where(c => c.IsRequired).Select(c => c.Name).ToList();
            var taskAdditionalConfigs = !string.IsNullOrEmpty(jobTaskDefinition.AdditionalConfigString) ?
                JsonConvert.DeserializeObject<Dictionary<string, string>>(jobTaskDefinition.AdditionalConfigString) : null;
            if (requiredConfigs.Count > 0)
            {
                if (taskAdditionalConfigs == null)
                {
                    throw new TaskProviderAdditionalConfigRequiredException(requiredConfigs[0], provider.Name);
                }

                foreach (var requiredConfig in requiredConfigs)
                {
                    taskAdditionalConfigs.TryGetValue(requiredConfig, out var conf);
                    if (conf == null)
                    {
                        throw new TaskProviderAdditionalConfigRequiredException(requiredConfig, provider.Name);
                    }
                }
            }

            var secretConfigs = additionalConfigsDefinition.Where(c => c.IsSecret).Select(c => c.Name).ToList();
            if (secretConfigs.Count > 0 && taskAdditionalConfigs != null)
            {
                foreach (var secretConfig in secretConfigs)
                {
                    if (taskAdditionalConfigs.TryGetValue(secretConfig, out var secretConfigValue))
                    {
                        var encryptedValue = await _secretVault.Encrypt(secretConfigValue);
                        taskAdditionalConfigs[secretConfig] = encryptedValue;
                    }
                }

                jobTaskDefinition.AdditionalConfigString = JsonConvert.SerializeObject(taskAdditionalConfigs);
            }
        }

        public static string GetServiceTaskConfigKey(string serviceTypeName)
        {
            return $"{serviceTypeName}ExternalService";
        }

        public async Task DecryptSecretAdditionalConfigs(JobTaskDefinition jobTaskDefinition, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(jobTaskDefinition?.Provider))
                return;

            var providerSpec = new TaskProviderFilterSpecification(jobTaskDefinition.Provider, null);
            var provider = await _providerRepository.GetSingleBySpec(providerSpec, cancellationToken);

            if (provider == null)
                return;

            var additionalConfigsDefinitionSpec = new TaskProviderAdditionalConfigFilterSpecification(provider.Id);
            var additionalConfigsDefinition = await _providerAdditionalConfigRepository.GetBySpec(additionalConfigsDefinitionSpec, cancellationToken);
            var secretConfigs = additionalConfigsDefinition.Where(c => c.IsSecret).Select(c => c.Name).ToList();
            var taskAdditionalConfigs = !string.IsNullOrEmpty(jobTaskDefinition.AdditionalConfigString) ?
                JsonConvert.DeserializeObject<Dictionary<string, string>>(jobTaskDefinition.AdditionalConfigString) : null;
            if (secretConfigs.Count > 0 && taskAdditionalConfigs != null)
            {
                foreach (var secretConfig in secretConfigs)
                {
                    if (taskAdditionalConfigs.TryGetValue(secretConfig, out var encryptedValue))
                    {
                        var decryptedValue = await _secretVault.Decrypt(encryptedValue);
                        taskAdditionalConfigs[secretConfig] = decryptedValue;
                    }
                }

                jobTaskDefinition.AdditionalConfigString = JsonConvert.SerializeObject(taskAdditionalConfigs);
            }
        }
    }
}
