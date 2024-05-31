using CCRM2.Shared.Models;
using Microsoft.AspNetCore.Components.Web;
using Syncfusion.Blazor.Inputs;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Data;
using Microsoft.AspNetCore.Components;

namespace CCRM2.Client.AIChat
{
    public partial class AIChatComponent
    {
        public bool DialogVisible { get; set; } = false;
        string input = "";
        SfTextBox SfTextBox;
        bool noConnect = true;
        public ObservableCollection<AiMessage> DataSource = new ObservableCollection<AiMessage>();
        List<CustomPrompts> Prompts = new List<CustomPrompts>();
        CustomPrompts newPrompt = new CustomPrompts();

        protected override async Task OnInitializedAsync()
        {
            DialogVisible = true;
            if (ChatClient.Messages != null)
            {
                DataSource = ChatClient.Messages;
            }
            var response = await Http.GetFromJsonAsync<List<CustomPrompts>>("api/customprompts");

            if (response != null)
            {
                Prompts = response.Where(p => p.EmployeeID == appData.CurrentEmployee.ID || p.EmployeeID == null).ToList();
            }

            StateHasChanged();
        }

        public async Task SendCustomPrompt(string prompt)
        {
            input = prompt;
            await OnSend();
        }

        async Task OnSend()
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                AiMessage LoadMessage = new AiMessage
                {
                    Role = "Ai",
                    Content = new Content()
                    {
                        Message = "..."
                    }
                };
                AiMessage sentMessage = new AiMessage
                {
                    Role = "user",
                    Content = new Content()
                    {
                        Message = input
                    }
                };
                DataSource.Add(sentMessage);
                DataSource.Add(LoadMessage);
                var mes = input;
                input = "";
                await SendMessageAsync(mes);
                DataSource.Remove(LoadMessage);
                ChatClient.Messages = DataSource;
                StateHasChanged();
            }
        }

        async Task SendMessageAsync(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                message = "Jeg har employeeID: " + $"{ appData.CurrentEmployee.ID}. Min besked til dig er: " + message;
                var response = await ChatClient.SendMessageAsync(message);
                noConnect = false;
                DataSource.Add(response);
                StateHasChanged();
            }
        }

        async Task HandleKeyUp(KeyboardEventArgs e)
        {
            if (e.Code == "Enter")
            {
                if (input != "")
                {
                    await OnSend();
                    input = "";
                    StateHasChanged();
                }
            }
        }

        async void AddRequest()
        {
            if (input != "")
            {
                newPrompt.Prompt = input;
                newPrompt.EmployeeID = appData.CurrentEmployee.ID;
                int ownedPrompts = Prompts.Count(p => p.EmployeeID == newPrompt.EmployeeID);
                newPrompt.PromptName = $"Custom_request no. {ownedPrompts + 1}";
                await Http.PostAsJsonAsync("api/customprompts", newPrompt);
                Prompts = await Http.GetFromJsonAsync<List<CustomPrompts>>("api/customprompts");
                input = "";
                StateHasChanged();
            }
        }

        void OnClear()
        {
            DataSource = new ObservableCollection<AiMessage>();
            ChatClient.ClearChat();
            StateHasChanged();
        }

        async Task OnSelectedRequest(string request)
        {
            input = request;
            await OnSend();
            input = "";
            StateHasChanged();
        }

        async Task DeleteRequest(CustomPrompts prompt)
        {
            await Http.DeleteAsync($"api/customprompts/{prompt.PromptId}");
            StateHasChanged();
        }
    }
}