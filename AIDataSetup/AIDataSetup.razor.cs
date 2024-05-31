using CCRM2.Client.Pages.Merge;
using CCRM2.Shared.Models;
using Microsoft.AspNetCore.Components;
using Syncfusion.Blazor.Buttons;
using Syncfusion.Blazor.Navigations;
using Syncfusion.Blazor.Spinner;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CCRM2.Client.Pages.Setup.AIDataSetup
{
    public partial class AIDataSetup
    {
        List<DataTable> dataTables = new List<DataTable>();
        SfSpinner spinner { get; set; }
        protected override async Task OnInitializedAsync()
        {
            var response = await Http.GetFromJsonAsync<List<DataTable>>("api/datatables");
            if (response != null)
            {
                dataTables = response.Where(e => e.Selected == true).ToList();
            }
        }
        private void Back()
        {
            navigationManager.NavigateTo("setup");
        }
        private async void Export()
        {
            spinner.Visible = true;
            await Http.PostAsJsonAsync("api/datatables/ExportData", dataTables);
            spinner.Visible = false;
        }
    }
}