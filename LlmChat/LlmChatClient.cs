using CCRM2.Shared.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System;

namespace CCRM2.Client.Services
{
    public class LlmChatClient
    {
        private readonly HttpClient _httpClient;
        public ObservableCollection<AiMessage> Messages { get; set; }

        public LlmChatClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            Messages = new ObservableCollection<AiMessage>();
        }

        public async Task<AiMessage> SendMessageAsync(string userInput)
        {
            if (string.IsNullOrEmpty(userInput))
            {
                throw new ArgumentException("User input cannot be null or empty", nameof(userInput));
            }

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("api/llmchat2", userInput);
            response.EnsureSuccessStatusCode(); // Throws if the response status is not successful

            AiMessage message = await response.Content.ReadFromJsonAsync<AiMessage>();
            return message;
        }
        public void ClearChat()
        {
            Messages.Clear();
        }
    }
}
