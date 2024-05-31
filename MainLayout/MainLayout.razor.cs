using Microsoft.AspNetCore.Components.Web;
using Syncfusion.Blazor.Navigations;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Net.Http.Json;
using CCRM2.Shared.Models;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Routing;
using Syncfusion.Blazor.Buttons;
using Syncfusion.Blazor.Popups;
using static System.Net.WebRequestMethods;
using Syncfusion.Blazor.Inputs;
using Syncfusion.Blazor.HeatMap.Internal;
using CCRM2.Client.AIChat;
using Azure.Core;

namespace CCRM2.Client.Shared
{
    public partial class MainLayout
    {
        SfDialog LlmChatDialog { get; set; }
        AIChatComponent AIChatComponent { get; set; }
        List<MenuItemModel> MenuItemModels { get; set; }
        SfSpeedDial SpeedDial { get; set; }
        string SearchValue { get; set; }
        bool SpinnerVisible { get; set; } = false;
        public bool SidebarToggle { get; set; } = true;
        public ExpandAction Expand { get; set; } = ExpandAction.Click;
        public IJSObjectReference JSObject { get; set; } = null!;
        bool isMenuOpen { get; set; } = true;
        bool AIChatVisible { get; set; } = false;
        List<CustomPrompts> _customPrompts = new List<CustomPrompts>();

        protected void OnInputEvent(Microsoft.AspNetCore.Components.ChangeEventArgs args)
        {
            SearchValue = (string)args.Value;
        }

        protected void KeyPressed(KeyboardEventArgs args)
        {
            if (args.Key.Equals("Enter"))
            {
                if (SearchValue != null && SearchValue != "")
                {
                    appData.AddPageToHistory(NavigationManager.Uri);
                    NavigationManager.NavigateTo($"/globalsearch/{SearchValue}");
                }
            }
        }

        protected override async Task OnInitializedAsync()
        {
            SpinnerVisible = true;
            while (appData.CurrentEmployee == null)
            {
                Console.WriteLine("we wait in the mainmenu - oninitializedAsync.");
                await Task.Delay(1000);
            }
            Console.WriteLine("Finally - the wait is over..........CurrentEmployee");

            while (appData.CurrentTenantId == null)
            {
                Console.WriteLine("we wait for the licenseinfo - oninitializedAsync.");
                await Task.Delay(1000);
            }
            Console.WriteLine("Finally - the wait is over..........LicenseInfo");

            appData.Licenseinfo = await http.GetFromJsonAsync<Licenseinfo>($"api/initialize/licenseinfo/{appData.CurrentTenantId}");
            if (appData.Licenseinfo.LicenseNumber == 0)
            {
                NavigationManager.NavigateTo($"/invalidlicense");
            }
            CustomPrompts customPrompt = new CustomPrompts { Prompt = "Hej"};
            _customPrompts = await http.GetFromJsonAsync<List<CustomPrompts>>("api/customprompts");
            _customPrompts.Add(customPrompt);
            

            MenuItemModels = await http.GetFromJsonAsync<List<MenuItemModel>>("api/Sidebar/{" + appData.CurrentEmployee.ID + "}");
            SpinnerVisible = false;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JsRuntime.InvokeVoidAsync("setThemeColor", _config["Styles:PrimaryColor"], _config["Styles:HoverColor"], _config["Styles:GradientColor"]);
            }
        }

        bool IsActive(string href, NavLinkMatch navLinkMatch = NavLinkMatch.Prefix)
        {
            if (string.IsNullOrEmpty(href))
            {
                return false;
            }
            var relativePath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri).ToLower();
            //return navLinkMatch == NavLinkMatch.All ? relativePath == href.ToLower() : relativePath.StartsWith(href.ToLower());
            return relativePath.Contains(href.ToLower());
        }

        string GetActive(string href, NavLinkMatch navLinkMatch = NavLinkMatch.Prefix) => IsActive(href, navLinkMatch) ? "active" : "";

        public async Task MenuClicked(string id)
        {
            await JsRuntime.InvokeVoidAsync("OpenMenu", id);
        }
        public async Task ExpandMenu()
        {
            await JsRuntime.InvokeVoidAsync("ExpandMenu");
        }

        public async Task ToggleSideBar()
        {
            await JsRuntime.InvokeVoidAsync("toggleSideBar");
        }

        public async Task OpenChatDialog()
        {
            AIChatVisible = !AIChatVisible;
            await LlmChatDialog.ShowDialog();
        }

        public async Task OnSelectedRequest(string request)
        {
            await OpenChatDialog();
            await AIChatComponent.SendCustomPrompt(request);
        }
    }
}
