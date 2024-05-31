using CCRM2.Shared.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Azure.AI.OpenAI;
using System.Collections.Generic;
using System.Linq;

namespace CCRM2.Client.Services
{
    public class LlmChatClient
    {
        private readonly AppData _appData;
        private readonly HttpClient _httpClient;
        private ObservableCollection<AiMessage> Messages { get; set; }

        public LlmChatClient(AppData appData, HttpClient httpClient)
        {

            _appData = appData;
            _httpClient = httpClient;
            Messages = new ObservableCollection<AiMessage>();
        }

        public async Task<AiMessage> SendMessageAsync(string userInput)
        {
            Messages.Add(new AiMessage()
            {
                Role = ChatRole.User.ToString(),
                Content = new Content()
                {
                    Message = userInput
                }
            });

            LlmRequest request = new LlmRequest()
            {
                CurrentEmployee = _appData.CurrentEmployee.ID,
                Message = userInput,
                AiMessages = Messages.ToList(),
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("api/llmchat", request);

            AiMessage message = await response.Content.ReadFromJsonAsync<AiMessage>();
            
            Messages.Add(message);

            return message;
        }

        public ObservableCollection<AiMessage> GetMessages()
        {
            return new ObservableCollection<AiMessage>(Messages);
        }

        public void ClearChat()
        {
            Messages.Clear();
        }
    }
}
