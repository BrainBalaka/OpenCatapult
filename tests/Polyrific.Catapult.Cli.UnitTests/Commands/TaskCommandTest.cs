﻿// Copyright (c) Polyrific, Inc 2018. All rights reserved.

using McMaster.Extensions.CommandLineUtils;
using Moq;
using Polyrific.Catapult.Cli.Commands;
using Polyrific.Catapult.Cli.Commands.Task;
using Polyrific.Catapult.Cli.UnitTests.Commands.Utilities;
using Polyrific.Catapult.Shared.Dto.Constants;
using Polyrific.Catapult.Shared.Dto.ExternalService;
using Polyrific.Catapult.Shared.Dto.ExternalServiceType;
using Polyrific.Catapult.Shared.Dto.JobDefinition;
using Polyrific.Catapult.Shared.Dto.Plugin;
using Polyrific.Catapult.Shared.Dto.Project;
using Polyrific.Catapult.Shared.Service;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Polyrific.Catapult.Cli.UnitTests.Commands
{
    public class TaskCommandTests
    {
        private readonly Mock<IConsole> _console;
        private readonly Mock<IConsoleReader> _consoleReader;
        private readonly ITestOutputHelper _output;
        private readonly Mock<IProjectService> _projectService;
        private readonly Mock<IJobDefinitionService> _jobDefinitionService;
        private readonly Mock<IPluginService> _pluginService;
        private readonly Mock<IExternalServiceService> _externalServiceService;
        private readonly Mock<IExternalServiceTypeService> _externalServiceTypeService;

        public TaskCommandTests(ITestOutputHelper output)
        {
            _output = output;
            _consoleReader = new Mock<IConsoleReader>();

            var projects = new List<ProjectDto>
            {
                new ProjectDto
                {
                    Id = 1,
                    Name = "Project 1"
                }
            };

            var jobs = new List<JobDefinitionDto>
            {
                new JobDefinitionDto
                {
                    Id = 1,
                    ProjectId = 1,
                    Name = "Default"
                }
            };

            var tasks = new List<JobTaskDefinitionDto>
            {
                new JobTaskDefinitionDto
                {
                    Id = 1,
                    JobDefinitionId = 1,
                    Name = "Generate",
                    Type = JobTaskDefinitionType.Generate
                }
            };

            var plugins = new List<PluginDto>
            {
                new PluginDto
                {
                    Id = 1,
                    Name = "GithubPushProvider",
                    RequiredServices = new string[] { "GitHub" }
                },
                new PluginDto
                {
                    Id = 2,
                    Name = "AzureAppService",
                    RequiredServices = new string[] { "AzureAppService" },
                    AdditionalConfigs = new PluginAdditionalConfigDto[]
                    {
                        new PluginAdditionalConfigDto
                        {
                            Name = "SubscriptionId",
                            Label = "Subscription Id",
                            Type = "string",
                            IsRequired = true,
                            IsSecret = false
                        },
                        new PluginAdditionalConfigDto
                        {
                            Name = "AppKey",
                            Label = "AppKey Id",
                            Type = "string",
                            IsRequired = true,
                            IsSecret = true
                        }
                    }
                }
            };

            var services = new List<ExternalServiceDto>
            {
                new ExternalServiceDto
                {
                    Id = 1,
                    ExternalServiceTypeId = 1,
                    ExternalServiceTypeName = "GitHub",
                    Name = "Default-Github",
                    Description = "Default github service",
                    Config = new Dictionary<string, string> { { "user", "test" } }
                },
                new ExternalServiceDto
                {
                    Id = 2,
                    ExternalServiceTypeId = 2,
                    ExternalServiceTypeName = "AzureAppService",
                    Name = "azure-default",
                    Description = "Default azure service",
                    Config = new Dictionary<string, string> { { "user", "test" } }
                }
            };

            var serviceTypes = new List<ExternalServiceTypeDto>
            {
                new ExternalServiceTypeDto
                {
                    Id = 1,
                    Name = "GitHub",
                    ExternalServiceProperties = new List<ExternalServicePropertyDto>
                    {
                        new ExternalServicePropertyDto
                        {
                            Name = "RemoteUrl",
                            Description = "Remote Url",
                            IsRequired = true
                        },
                        new ExternalServicePropertyDto
                        {
                            Name = "RemoteAuthType",
                            Description = "Remote auth type",
                            IsRequired = true,
                            AllowedValues = new string[] { "userPassword", "authToken" }
                        },
                        new ExternalServicePropertyDto
                        {
                            Name = "AuthToken",
                            Description = "Auth token",
                            IsSecret = true
                        }
                    }
                }
            };

            _console = new Mock<IConsole>();

            _jobDefinitionService = new Mock<IJobDefinitionService>();
            _jobDefinitionService.Setup(s => s.GetJobDefinitionByName(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync((int projectId, string name) => jobs.FirstOrDefault(u => u.ProjectId == projectId && u.Name == name));
            _jobDefinitionService.Setup(s => s.CreateJobTaskDefinition(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CreateJobTaskDefinitionDto>())).ReturnsAsync((int projectId, int jobDefinitionId, CreateJobTaskDefinitionDto dto) => new JobTaskDefinitionDto
            {
                Id = 2,
                Name = dto.Name,
                JobDefinitionId = jobDefinitionId
            });
            _jobDefinitionService.Setup(s => s.GetJobTaskDefinitions(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync((int projectId, int jobDefinitionId) => tasks);
            _jobDefinitionService.Setup(s => s.GetJobTaskDefinitionByName(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync((int projectId, int jobDefinitionId, string name) => tasks.FirstOrDefault(t => t.JobDefinitionId == jobDefinitionId && t.Name == name));

            _projectService = new Mock<IProjectService>();
            _projectService.Setup(p => p.GetProjectByName(It.IsAny<string>())).ReturnsAsync((string name) => projects.FirstOrDefault(p => p.Name == name));
            

            _pluginService = new Mock<IPluginService>();
            _pluginService.Setup(s => s.GetPluginByName(It.IsAny<string>()))
                .ReturnsAsync((string pluginName) => plugins.FirstOrDefault(x => x.Name == pluginName));

            _externalServiceService = new Mock<IExternalServiceService>();
            _externalServiceService.Setup(s => s.GetExternalServiceByName(It.IsAny<string>())).ReturnsAsync((string name) => services.FirstOrDefault(u => u.Name == name));

            _externalServiceTypeService = new Mock<IExternalServiceTypeService>();
            _externalServiceTypeService.Setup(s => s.GetExternalServiceTypeByName(It.IsAny<string>())).ReturnsAsync((string name) => serviceTypes.FirstOrDefault(u => u.Name == name));
        }

        [Fact]
        public void Task_Execute_ReturnsEmpty()
        {
            var command = new TaskCommand(_console.Object, LoggerMock.GetLogger<TaskCommand>().Object);
            var resultMessage = command.Execute();

            Assert.Equal("", resultMessage);
        }

        [Fact]
        public void TaskAdd_Execute_ReturnsSuccessMessage()
        {
            _consoleReader.Setup(x => x.GetPassword(It.IsAny<string>(), null, null)).Returns("testPassword");

            var console = new TestConsole(_output, "azure-default");
            var command = new AddCommand(console, LoggerMock.GetLogger<AddCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Deploy",
                Type = JobTaskDefinitionType.Deploy,
                Provider = "AzureAppService",
            };

            var resultMessage = command.Execute();

            Assert.StartsWith("Task Deploy added to job Default:", resultMessage);
        }

        [Fact]
        public void TaskAdd_Execute_ReturnsNotFoundMessage()
        {
            var command = new AddCommand(_console.Object, LoggerMock.GetLogger<AddCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default 2",
                Name = "Push",
                Type = JobTaskDefinitionType.Push
            };

            var resultMessage = command.Execute();

            Assert.Equal("Failed adding task Push. Make sure the project and job definition names are correct.", resultMessage);
        }

        [Fact]
        public void TaskAdd_Execute_ReturnsProviderNotInstalledMessage()
        {
            var console = new TestConsole(_output);
            var command = new AddCommand(console, LoggerMock.GetLogger<AddCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Push",
                Type = JobTaskDefinitionType.Push,
                Provider = "test"
            };

            var resultMessage = command.Execute();

            Assert.Equal("The provider \"test\" is not installed", resultMessage);
        }

        [Fact]
        public void TaskAdd_Execute_ReturnsServiceRequiredMessage()
        {
            var console = new TestConsole(_output);
            var command = new AddCommand(console, LoggerMock.GetLogger<AddCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Push",
                Type = JobTaskDefinitionType.Push,
                Provider = "GithubPushProvider"
            };

            var resultMessage = command.Execute();

            Assert.Equal("The GitHub external service is required for the provider GithubPushProvider. If you do not have it in the system, please add them using \"service add\" command", resultMessage);
        }

        [Fact]
        public void TaskAdd_Execute_ReturnsServiceNotFoundMessage()
        {
            var console = new TestConsole(_output, "test");
            var command = new AddCommand(console, LoggerMock.GetLogger<AddCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Push",
                Type = JobTaskDefinitionType.Push,
                Provider = "GithubPushProvider"
            };

            var resultMessage = command.Execute();

            Assert.Equal("The external service test is not found.", resultMessage);
        }

        [Fact]
        public void TaskAdd_Execute_ReturnsServiceTypeIncorrectMessage()
        {
            var console = new TestConsole(_output, "azure-default");
            var command = new AddCommand(console, LoggerMock.GetLogger<AddCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Push",
                Type = JobTaskDefinitionType.Push,
                Provider = "GithubPushProvider"
            };

            var resultMessage = command.Execute();

            Assert.Equal("The entered external service is not a GitHub service", resultMessage);
        }

        [Fact]
        public void TaskGet_Execute_ReturnsSuccessMessage()
        {
            _pluginService.Setup(x => x.GetPluginAdditionalConfigByPluginName(It.IsAny<string>())).ReturnsAsync(new List<PluginAdditionalConfigDto>());
            var command = new GetCommand(_console.Object, LoggerMock.GetLogger<GetCommand>().Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Generate"
            };

            var resultMessage = command.Execute();

            Assert.StartsWith("Task Generate in job Default:", resultMessage);
        }

        [Fact]
        public void TaskGet_Execute_ReturnsNotFoundMessage()
        {
            var command = new GetCommand(_console.Object, LoggerMock.GetLogger<GetCommand>().Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object)
            {
                Project = "Project 1",
                Job = "Default 2",
                Name = "Push"
            };

            var resultMessage = command.Execute();

            Assert.Equal("Failed fetching task Push. Make sure the project, job definition, and task names are correct.", resultMessage);
        }

        [Fact]
        public void TaskList_Execute_ReturnsSuccessMessage()
        {
            _pluginService.Setup(x => x.GetPluginAdditionalConfigByPluginName(It.IsAny<string>())).ReturnsAsync(new List<PluginAdditionalConfigDto>());
            var command = new ListCommand(_console.Object, LoggerMock.GetLogger<ListCommand>().Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object)
            {
                Project = "Project 1",
                Job = "Default"
            };

            var resultMessage = command.Execute();

            Assert.StartsWith("Job task definitions in job Default:", resultMessage);
        }

        [Fact]
        public void TaskList_Execute_ReturnsNotFoundMessage()
        {
            var command = new ListCommand(_console.Object, LoggerMock.GetLogger<ListCommand>().Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object)
            {
                Project = "Project 1",
                Job = "Default 2"
            };

            var resultMessage = command.Execute();

            Assert.Equal("Failed fetching tasks. Make sure the project and job names are correct.", resultMessage);
        }

        [Fact]
        public void TaskUpdate_Execute_ReturnsSuccessMessage()
        {
            _jobDefinitionService.Setup(x => x.GetJobTaskDefinitionByName(1, 1, "Deploy")).ReturnsAsync((int projectId, int jobId, string taskName) => new JobTaskDefinitionDto
            {
                Id = 1,
                Type = "Deploy",
                JobDefinitionId = jobId,
                Name = taskName,
                Provider = "AzureAppService",
                Configs = new Dictionary<string, string>
                {
                    {"AzureAppServiceExternalService", "azure-default" }
                },
                AdditionalConfigs = new Dictionary<string, string>
                {
                    {"SubscriptionId", "test" },
                    {"AppKey", "test" }
                }
            });
            _consoleReader.Setup(x => x.GetPassword(It.IsAny<string>(), null, null)).Returns("testPassword");

            var console = new TestConsole(_output, "azure-default");
            var command = new UpdateCommand(console, LoggerMock.GetLogger<UpdateCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Deploy"
            };

            var resultMessage = command.Execute();

            Assert.Equal("Task Deploy was updated", resultMessage);
            _jobDefinitionService.Verify(x => x.UpdateJobTaskDefinition(1, 1, 1, It.Is<UpdateJobTaskDefinitionDto>(t => t.AdditionalConfigs.Count == 2)), Times.Once);
        }

        [Fact]
        public void TaskUpdate_Execute_UpdateProviderReturnsSuccessMessage()
        {
            _consoleReader.Setup(x => x.GetPassword(It.IsAny<string>(), null, null)).Returns("testPassword");

            var console = new TestConsole(_output, "azure-default");
            var command = new UpdateCommand(console, LoggerMock.GetLogger<UpdateCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Generate",
                Rename = "Deploy",
                Type = "Deploy",
                Provider = "AzureAppService"
            };

            var resultMessage = command.Execute();

            Assert.Equal("Task Generate was updated", resultMessage);
            _jobDefinitionService.Verify(x => x.UpdateJobTaskDefinition(1, 1, 1, It.Is<UpdateJobTaskDefinitionDto>(t => t.AdditionalConfigs.Count == 2)), Times.Once);
        }

        [Fact]
        public void TaskUpdate_Execute_ReturnsNotFoundMessage()
        {
            var command = new UpdateCommand(_console.Object, LoggerMock.GetLogger<UpdateCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Push"
            };

            var resultMessage = command.Execute();

            Assert.Equal("Failed updating task Push. Make sure the project and job definition names are correct.", resultMessage);
        }

        [Fact]
        public void TaskUpdate_Execute_ReturnsProviderNotInstalledMessage()
        {
            var console = new TestConsole(_output);
            var command = new UpdateCommand(console, LoggerMock.GetLogger<UpdateCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Generate",
                Type = JobTaskDefinitionType.Generate,
                Provider = "test"
            };

            var resultMessage = command.Execute();

            Assert.Equal("The provider \"test\" is not installed", resultMessage);
        }

        [Fact]
        public void TaskUpdate_Execute_ReturnsServiceRequiredMessage()
        {
            _jobDefinitionService.Setup(s => s.GetJobTaskDefinitionByName(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync((int projectId, int jobDefinitionId, string name) => 
            new JobTaskDefinitionDto
            {
                JobDefinitionId = 1,
                Name = "Push",
                Type = JobTaskDefinitionType.Push,
                Provider = "GithubPushProvider",
            });

            var console = new TestConsole(_output);
            var command = new UpdateCommand(console, LoggerMock.GetLogger<UpdateCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Push",
                Type = JobTaskDefinitionType.Push,
                Provider = "AzureAppService"
            };

            var resultMessage = command.Execute();

            Assert.Equal("The AzureAppService external service is required for the provider AzureAppService. If you do not have it in the system, please add them using \"service add\" command", resultMessage);
        }

        [Fact]
        public void TaskUpdate_Execute_ReturnsServiceNotFoundMessage()
        {
            _jobDefinitionService.Setup(s => s.GetJobTaskDefinitionByName(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync((int projectId, int jobDefinitionId, string name) =>
            new JobTaskDefinitionDto
            {
                JobDefinitionId = 1,
                Name = "Push",
                Type = JobTaskDefinitionType.Push,
                Provider = "GithubPushProvider",
            });

            var console = new TestConsole(_output, "test");
            var command = new UpdateCommand(console, LoggerMock.GetLogger<UpdateCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Push",
                Type = JobTaskDefinitionType.Push,
                Provider = "GithubPushProvider"
            };

            var resultMessage = command.Execute();

            Assert.Equal("The external service test is not found.", resultMessage);
        }

        [Fact]
        public void TaskUpdate_Execute_ReturnsServiceTypeIncorrectMessage()
        {
            _jobDefinitionService.Setup(s => s.GetJobTaskDefinitionByName(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync((int projectId, int jobDefinitionId, string name) =>
            new JobTaskDefinitionDto
            {
                JobDefinitionId = 1,
                Name = "Push",
                Type = JobTaskDefinitionType.Push,
                Provider = "GithubPushProvider",
            });

            var console = new TestConsole(_output, "azure-default");
            var command = new UpdateCommand(console, LoggerMock.GetLogger<UpdateCommand>().Object, _consoleReader.Object, _projectService.Object, _jobDefinitionService.Object, _pluginService.Object, _externalServiceService.Object, _externalServiceTypeService.Object)
            {
                Project = "Project 1",
                Job = "Default",
                Name = "Push",
                Type = JobTaskDefinitionType.Push,
                Provider = "GithubPushProvider"
            };

            var resultMessage = command.Execute();

            Assert.Equal("The entered external service is not a GitHub service", resultMessage);
        }
    }
}
