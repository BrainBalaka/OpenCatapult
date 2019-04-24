﻿// Copyright (c) Polyrific, Inc 2018. All rights reserved.

using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Polyrific.Catapult.Cli.Extensions;
using Polyrific.Catapult.Shared.Dto.Constants;
using Polyrific.Catapult.Shared.Dto.User;
using Polyrific.Catapult.Shared.Service;

namespace Polyrific.Catapult.Cli.Commands.Account
{
    [Command("update", Description = "Update user profile")]
    public class UpdateCommand : BaseCommand
    {
        private readonly IAccountService _accountService;

        public UpdateCommand(IConsole console, ILogger<UpdateCommand> logger, IAccountService accountService) : base(console, logger)
        {
            _accountService = accountService;
        }

        [Required]
        [Option("-u|--user <USER>", "Username (email) of the user", CommandOptionType.SingleValue)]
        public string User { get; set; }

        [Option("-fn|--firstname <FIRSTNAME>", "First name  of the user", CommandOptionType.SingleValue)]
        public string FirstName { get; set; }

        [Option("-ln|--lastname <LASTNAME>", "Last name of the user", CommandOptionType.SingleValue)]
        public string LastName { get; set; }

        public override string Execute()
        {
            Console.WriteLine($"Trying to update user {User}...");

            string message;

            var user = _accountService.GetUserByEmail(User).Result;
            if (user != null)
            {
                var userId = int.Parse(user.Id);

                var updatedUser = new UpdateUserDto
                {
                    Id = userId,
                    FirstName = FirstName ?? user.FirstName,
                    LastName = LastName ?? user.LastName,
                    ExternalAccountIds = user.ExternalAccountIds
                };

                Console.WriteLine("Please enter the following additional user info if it is available");
                var githubId = Console.GetString("GitHub Id (Leave blank to use previous value):");
                if (!string.IsNullOrEmpty(githubId))
                {
                    if (updatedUser.ExternalAccountIds != null)
                    {
                        updatedUser.ExternalAccountIds[ExternalAccountType.GitHub] = githubId;
                    }
                    else
                    {
                        updatedUser.ExternalAccountIds = new System.Collections.Generic.Dictionary<string, string>
                        {
                            {ExternalAccountType.GitHub, githubId}
                        };
                    }
                }

                _accountService.UpdateUser(userId, updatedUser).Wait();

                message = $"User {User} has been updated";
                Logger.LogInformation(message);
            }
            else
            {
                message = $"User {User} was not found";
            }

            return message;
        }
    }
}
