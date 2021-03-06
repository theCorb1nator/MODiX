﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Rest;
using MediatR;

using Modix.Data.Models.Moderation;
using Modix.Services.Messages.Discord;
using Modix.Services.Utilities;

namespace Modix.Services.Moderation
{
    /// <summary>
    /// Implements a handler that synchronizes infractions when applied manually through the Discord UI instead of through MODiX.
    /// </summary>
    public class InfractionSyncingHandler :
        INotificationHandler<UserBanned>
    {
        private readonly IModerationService _moderationService;
        private readonly DiscordRestClient _restClient;

        /// <summary>
        /// Constructs a new <see cref="InfractionSyncingHandler"/> object with the given injected dependencies.
        /// </summary>
        /// <param name="moderationService">A moderation service to interact with the infractions system.</param>
        /// <param name="restClient">A REST client to interact with the Discord API.</param>
        public InfractionSyncingHandler(
            IModerationService moderationService,
            DiscordRestClient restClient)
        {
            _moderationService = moderationService;
            _restClient = restClient;
        }

        public Task Handle(UserBanned notification, CancellationToken cancellationToken)
            => TryCreateBanInfractionAsync(notification.Guild, notification.BannedUser);

        /// <summary>
        /// Creates a ban infraction for the user if they do not already have one.
        /// </summary>
        /// <param name="guild">The guild that the user was banned from.</param>
        /// <param name="user">The user who was banned.</param>
        /// <returns>
        /// A <see cref="Task"/> that will complete when the operation completes.
        /// </returns>
        private async Task TryCreateBanInfractionAsync(IGuild guild, IUser user)
        {
            if (await _moderationService.AnyInfractionsAsync(GetBanSearchCriteria(guild, user)))
            {
                return;
            }

            var restGuild = await _restClient.GetGuildAsync(guild.Id);
            var auditLogs = (await restGuild.GetAuditLogsAsync(10)
                .FlattenAsync())
                .Where(x => x.Action == ActionType.Ban)
                .Select(x => (Entry: x, Data: (BanAuditLogData)x.Data));

            var banLog = auditLogs.FirstOrDefault(x => x.Data.Target.Id == user.Id);

            var reason = string.IsNullOrWhiteSpace(banLog.Entry.Reason)
                ? $"Banned by {banLog.Entry.User.GetDisplayNameWithDiscriminator()}."
                : banLog.Entry.Reason;

            await _moderationService.CreateInfractionAsync(guild.Id, banLog.Entry.User.Id, InfractionType.Ban, user.Id, reason, null);
        }

        private InfractionSearchCriteria GetBanSearchCriteria(IGuild guild, IUser user)
            => new InfractionSearchCriteria()
            {
                GuildId = guild.Id,
                SubjectId = user.Id,
                Types = new[] { InfractionType.Ban },
                IsDeleted = false,
                IsRescinded = false,
            };
    }
}
