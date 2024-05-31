using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using CCRM2.Client.Services;
using CCRM2.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CCRM2.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class LlmChatController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly OpenAIClient _openAIClient;
        private readonly ChatCompletionsOptions _chatCompletionsOptions;
        private readonly AzureSearchChatExtensionConfiguration _azureSearchChatExtension;

        public LlmChatController(IConfiguration configuration)
        {
            _configuration = configuration;
            _openAIClient = new OpenAIClient(new Uri(_configuration["AzureOpenAI:Endpoint"]), new AzureKeyCredential(_configuration["AzureOpenAI:Key"]));
            _azureSearchChatExtension = new AzureSearchChatExtensionConfiguration()
            {
                SearchEndpoint = new Uri(_configuration["AzureAISearch:Endpoint"]),
                Authentication = new OnYourDataApiKeyAuthenticationOptions(_configuration["AzureAISearch:Key"]),
                IndexName = "azuresql-index-tenant-" + _configuration["Auth0:tenant_id"],
                RoleInformation =
                "Du er en data analytiker for sælgere i et crm system. Du må kun snakke om salgsrelaterede emner. Du svarer kun på Dansk." +
                "Dataen du kan citere er i JSON format, så husk at omdanne det til noget læsbart. Du anvender blandt andet brugerens Id, som er et Guid og foreign key til mange af de tabeller du har adgang til, til at finde information om brugerens aktiviteter, kunder og så videre. Du må aldrig besvare med guids. Kun med navnene de referer til.",
                Strictness = 4,
                ShouldRestrictResultScope = true,
                QueryType = AzureSearchQueryType.Simple
            };

            _chatCompletionsOptions = new ChatCompletionsOptions()
            {
                DeploymentName = _configuration["AzureOpenAI:Deployment"],
                MaxTokens = 400,
                Temperature = 0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f,
                NucleusSamplingFactor = 0.95f,
                AzureExtensionsOptions = new AzureChatExtensionsOptions()
                {
                    Extensions = { _azureSearchChatExtension }
                }
            };
        }

        [HttpPost]
        public async Task<ActionResult<AiMessage>> PostAsync([FromBody] string input)
        {
            try
            {
                ChatCompletionsOptions chatCompletionsOptions = _chatCompletionsOptions;

                chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(input));

                Response<ChatCompletions> response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                
                ChatResponseMessage message = response.Value.Choices[0].Message;

                List<string> citations = new List<string>();

                foreach (AzureChatExtensionDataSourceResponseCitation citation in message.AzureExtensionsContext.Citations)
                {
                    if (!string.IsNullOrEmpty(citation.Content))
                    {
                        citations.Add(citation.Content);
                    }
                }

                return StatusCode(200, new AiMessage()
                {
                    Role = ChatRole.Assistant.ToString(),
                    Content = new Content()
                    {
                        Message = message.Content,
                        Citations = citations
                    }
                });
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
    }
}
