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
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CCRM2.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class LLmChat2Controller : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly OpenAIClient _openAIClient;
        private readonly ChatCompletionsOptions _chatCompletionsOptions;
        private readonly AzureSearchChatExtensionConfiguration _azureSearchChatExtension;

        public LLmChat2Controller(IConfiguration configuration)
        {
            _configuration = configuration;
            _openAIClient = new OpenAIClient(new Uri(_configuration["AzureOpenAI:Endpoint"]), new AzureKeyCredential(_configuration["AzureOpenAI:Key"]));
            _azureSearchChatExtension = new AzureSearchChatExtensionConfiguration()
            {
                SearchEndpoint = new Uri(_configuration["AzureAISearch:Endpoint"]),
                Authentication = new OnYourDataApiKeyAuthenticationOptions(_configuration["AzureAISearch:Key"]),
                IndexName = "sql-test-philip",
                RoleInformation =
                "Du er en data analytiker der assistere brugere af et CRM-system. Du vil først få en prompt. Baseret på din viden om databasen og dens struktur, svar da kun med et SQL script der kan hente de relevante informationer. " +
                "Herefter vil du modtage de relevante informationer og du vil så svare på den første prompt du modtog med svaret. Databasen du vil interagere med er en MSQL relational database, hvor skemaet hvor tabellerne er i hedder [crm] husk dette i dine sql script. Tabelnavnene er dem du har adgang til med din AI search funktionalitet" +
                "Du må kun lave sql scripts du mener er sikkerhedsmæssigt relevant" +
                "Et eksempel på et spørgsmål/svar kunne være at du modtager et spørgsmål: Hvilke kunder har jeg i Roskilde?. Du svarer kun med et sql script uden citationstegn eller andet som f.eks: Select name from [dbo].[Company] where KAM = 'bruger id du har modtaget med prompt'. Dernæst får du resultatet fra databasen" +
                "som en ny prompt, hvor du så vil inkorpere disse data (eller mangel på data) ind i dit svar",
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
                
                // Execute AI chat completion
                Response<ChatCompletions> response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                ChatResponseMessage message = response.Value.Choices[0].Message;
                string query = message.Content;
                query = query.Trim();
                query = query.Replace("﻿```", "");
                query = query.Replace("sql", "");
                Console.WriteLine(query);
                // Execute SQL query asynchronously
                string replyFromDb = await ExecuteSqlQueryAsync(query);

                // Pass the database response to AI model for further processing
                chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(replyFromDb));
                Response<ChatCompletions> response2 = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                ChatResponseMessage message2 = response2.Value.Choices[0].Message;

                // Construct and return the final response
                return StatusCode(200, new AiMessage()
                {
                    Role = ChatRole.Assistant.ToString(),
                    Content = new Content()
                    {
                        Message = message2.Content,
                    }
                });
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        private async Task<string> ExecuteSqlQueryAsync(string query)
        {
            using (SqlConnection connection = new SqlConnection(_configuration["ConnectionStrings:DefaultConnection"]))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    // Execute the query and retrieve data
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Assuming the first column contains the response
                            return reader.GetString(0);
                        }
                    }
                }
            }
            // Return null if no data is retrieved
            return null;
        }
        
    }
}
