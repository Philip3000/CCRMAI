using CCRM2.Shared.Models;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CCRM2.Client.Services.Toasts;
using Syncfusion.Blazor.Notifications;
using System.Threading.Tasks;

namespace CCRM2.Client.AIChat
{
    public partial class AIDataSelector
    {

        private List<DataTable> cards = new List<DataTable>();
        private readonly IConfiguration _configuration;
        SfToast toast { get; set; }
        private int selectedCardCount = 0;
        private bool loaded = false;

        protected override async void OnInitialized()
        {
            loaded = true;
            var response = await Http.GetFromJsonAsync<List<DataTable>>("api/datatables");
            if (response != null)
            {
                cards = response.ToList();
                selectedCardCount = cards.Count(e => e.Selected);
            }
            else
            {
                Refresh();
            }
            StateHasChanged();
        }

        private async Task ToggleCardSelection(int index)
        {
            index--;
            if (cards[index].Selected)
            {
                selectedCardCount--; 
                cards[index].Selected = !cards[index].Selected;
            }
            else
            {
                if (selectedCardCount >= 19)
                {
                    toast.Content = "Can only select 19!";
                    toast.CssClass = "e-toast-danger";
                    await toast.ShowAsync();
                }
                else
                {
                    cards[index].Selected = !cards[index].Selected;
                    selectedCardCount++;
                }
            }
            StateHasChanged();
        }

        private string GetCardClass(int index)
        {
            index--;
            if (cards[index].Selected)
            {
                return $"card-selected";
            }
            else
            {
                return "card";
            }
        }
        private async void Save()
        {
            await Http.PutAsJsonAsync("api/datatables/UpdateAll", cards);
            toast.Content = "Updated selected tables";
            toast.CssClass = "e-toast-success";
            await toast.ShowAsync();
        }
        private void Back()
        {
            navigationManager.NavigateTo("/setup");
        }
        private async void Refresh()
        {
            await Http.PostAsJsonAsync("api/datatables/refresh", cards);
            var response = await Http.GetFromJsonAsync<List<DataTable>>("api/datatables");
            if (response != null)
            {
                cards = response.ToList();
            }
            toast.Content = "Refreshed datatables";
            toast.CssClass = "e-toast-success";
            await toast.ShowAsync();
            StateHasChanged();
        }

    }
}


