﻿using System;
using System.Linq;
using System.Threading.Tasks;

using Discord.WebSocket;

using Microsoft.AspNetCore.Mvc;

using Modix.Data.Models.Tags;
using Modix.Services.Core;
using Modix.Services.Tags;

namespace Modix.Controllers
{
    [Route("~/api/tags")]
    public class TagController : ModixController
    {
        private ITagService TagService { get; }

        public TagController(DiscordSocketClient client, IAuthorizationService modixAuth, ITagService tagService) : base(client, modixAuth)
        {
            TagService = tagService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTagsAsync()
        {
            var summaries = await TagService.GetSummariesAsync(new TagSearchCriteria
            {
                GuildId = UserGuild.Id,
            });

            var data = summaries.Select(x => new Models.Tags.TagData()
            {
                Content = x.Content,
                Created = x.CreateAction.Created,
                IsOwnedByRole = !(x.OwnerRole is null),
                Name = x.Name,
                OwnerName = x.OwnerUser?.DisplayName ?? x.OwnerRole?.Name,
                OwnerColor = "#000000",
                Uses = x.Uses,
                TagSummary = x,
            })
            .ToArray();

            foreach (var tag in data)
            {
                if (tag.IsOwnedByRole)
                {
                    var role = UserGuild.GetRole(tag.TagSummary.OwnerRole.Id);
                    tag.OwnerColor = role.Color.ToString();
                }

                tag.CanMaintain = await TagService.CanUserMaintainTagAsync(tag.TagSummary, SocketUser.Id);
            }

            return Ok(data);
        }

        [HttpPut("{name}")]
        public async Task<IActionResult> CreateTagAsync([FromRoute] string name, [FromBody] Models.Tags.TagCreationData data)
        {
            try
            {
                await TagService.CreateTagAsync(ModixUser.SelectedGuild, ModixUser.UserId, name, data.Content);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }

            return Ok();
        }

        [HttpPatch("{name}")]
        public async Task<IActionResult> UpdateTagAsync([FromRoute] string name, [FromBody] Models.Tags.TagMutationData data)
        {
            try
            {
                await TagService.ModifyTagAsync(ModixUser.SelectedGuild, ModixUser.UserId, name, data.Content);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }

            return Ok();
        }

        [HttpDelete("{name}")]
        public async Task<IActionResult> DeleteTagAsync([FromRoute] string name)
        {
            try
            {
                await TagService.DeleteTagAsync(ModixUser.SelectedGuild, ModixUser.UserId, name);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }

            return Ok();
        }
    }
}
