using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using CCRM2.Shared.Models;
using Microsoft.AspNetCore.Authorization;
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
        private readonly string _azureSearchIndex;

        public LlmChatController(IConfiguration configuration)
        {
            _configuration = configuration;            
            _openAIClient = new OpenAIClient(new Uri(_configuration["AzureOpenAI:Endpoint"]), new AzureKeyCredential(_configuration["AzureOpenAI:Key"]));
            _azureSearchIndex = $"azuresql-index-tenant-{_configuration["Auth0:tenant_id"]}";
        }

        [HttpPost]
        public async Task<ActionResult<AiMessage>> PostAsync([FromBody] LlmRequest request)
        {
            try
            {
                ChatCompletionsOptions chatCompletionsOptions = CreateChatCompletionsOptions(request);

                Response<ChatCompletions> response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                
                ChatResponseMessage message = response.Value.Choices[0].Message;                

                List<Citation> citations = new List<Citation>();

                if (message.AzureExtensionsContext.Citations != null)
                {
                    foreach (var citation in message.AzureExtensionsContext.Citations)
                    {
                        citations.Add(new Citation()
                        {
                            Content = citation.Content,
                            Title = citation.Title,
                            Url = citation.Url,
                            Filepath = citation.Filepath,
                            ChunkId = citation.ChunkId,
                        });
                    }
                }

                AiMessage responseMessage = new AiMessage()
                {
                    Role = message.Role.ToString(),
                    Content = new Content()
                    {
                        Message = message.Content,
                        Citations = citations
                    },
                    Intent = message.AzureExtensionsContext.Intent
                };

                return StatusCode(200, responseMessage);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        private ChatCompletionsOptions CreateChatCompletionsOptions(LlmRequest llmRequest)
        {
            AzureSearchChatExtensionConfiguration azureSearchChatExtensionConfiguration = new AzureSearchChatExtensionConfiguration()
            {
                SearchEndpoint = new Uri(_configuration["AzureAISearch:Endpoint"]),
                Authentication = new OnYourDataApiKeyAuthenticationOptions(_configuration["AzureAISearch:Key"]),
                IndexName = _azureSearchIndex,
                RoleInformation = $"Du skal fungere som en data analytiker i et crm system.\n" +
                $"- Du svarer kun på Dansk.\n" +
                $"- Hvis der bliver spurgt om \"Min\",\"Mine\" eller \"Har jeg\", skal der søges efter data der referere til dette: \"{llmRequest.CurrentEmployee}\".\n" +
                $"- Det data der er i content er i JSON format.",
                Strictness = 4,
                ShouldRestrictResultScope = true,
                QueryType = AzureSearchQueryType.Simple
            };

            ChatCompletionsOptions chatCompletionsOptions = new ChatCompletionsOptions()
            {
                DeploymentName = _configuration["AzureOpenAI:Deployment"],
                MaxTokens = 500,
                Temperature = 0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f,
                NucleusSamplingFactor = 0.95f,
                AzureExtensionsOptions = new AzureChatExtensionsOptions()
                {
                    Extensions = { azureSearchChatExtensionConfiguration }
                }
            };

            chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(llmRequest.Message));

            if (llmRequest.AiMessages != null)
            {
                foreach (AiMessage aiMessage in llmRequest.AiMessages)
                {
                    if (aiMessage.Role.Equals(ChatRole.User.ToString()))
                    {
                        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(aiMessage.Content.Message));
                    }
                    if (aiMessage.Role.Equals(ChatRole.Assistant.ToString()))
                    {
                        chatCompletionsOptions.Messages.Add(new ChatRequestAssistantMessage($"{aiMessage.Content.Message}\nIntent:\n{aiMessage.Intent}"));
                    }
                }
            }

            return chatCompletionsOptions;
        }
    }
}
