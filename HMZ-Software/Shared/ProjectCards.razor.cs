﻿using HMZSoftwareBlazorWebAssembly.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using HMZSoftwareBlazorWebAssembly.Helpers;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace HMZSoftwareBlazorWebAssembly.Shared
{
    public partial class ProjectCards : ComponentBase, IDisposable
    {
        [Inject] private HttpClient HttpClient { get; set; }
        [Inject] private IJSRuntime JSRuntime { get; set; }

        private List<GitRepo> GitHubProjects { get; set; } = new List<GitRepo>();
        private bool DataLoaded { get; set; } = false;
        private bool DataLoading { get; set; } = false;
        private bool HasMoreData { get; set; } = false;

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                if (!await JSRuntime.InvokeAsync<bool>("BlazorHelpers.ObserveElement", DotNetObjectReference.Create(this), null, "0px", 0.5, "#Work", "OnScrollIndex"))
                {
                    throw new Exception("Failed to invoke the Intersection Observer for the ProjectCards component.");
                };
            }
        }

        [JSInvokable]
        public async Task OnScrollIndex(IntersectionObserverEventArgs intersectionObserverEventArgs)
        {
            if (intersectionObserverEventArgs.isIntersecting && !DataLoaded)
            {
                if (DataLoading)
                    return;

                DataLoading = true;
                //IO is intersecting...
                await FetchGitData();
                StateHasChanged();
            }
        }

        private async Task FetchGitData()
        {
            if (GlobalVariables.GitHubData == null)
            {
                HttpRequestMessage GitData = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/users/hmz777/repos");
                GitData.Headers.Accept.Clear();
                GitData.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

                var data = await HttpClient.SendAsync(GitData, HttpCompletionOption.ResponseContentRead);

                if (!data.IsSuccessStatusCode)
                    throw new Exception("GitHub data fetch failed!");

                var json = await data.Content.ReadAsStringAsync();
                var projects = JsonSerializer.Deserialize<JsonElement>(json);

                foreach (var project in projects.EnumerateArray())
                {
                    if (!project.GetProperty("fork").GetBoolean())
                    {
                        GitHubProjects.Add(new GitRepo()
                        {
                            Name = project.GetProperty("name").GetString(),
                            Description = project.GetProperty("description").GetString(),
                            Url = project.GetProperty("html_url").GetString(),
                            Forks = project.GetProperty("forks_count").GetInt32(),
                            Stars = project.GetProperty("stargazers_count").GetInt32(),
                            Watchers = project.GetProperty("watchers_count").GetInt32(),
                            Topics = await GetProjectTopics(project.GetProperty("name").GetString())
                        });
                    }
                }

                if (GitHubProjects.Count > 9)
                    HasMoreData = true;

                //Cache the response
                GlobalVariables.GitHubData = GitHubProjects;
                GitHubProjects = GitHubProjects.Take(9).OrderBy(o => o.Name).ToList();
            }
            else
            {
                if (GlobalVariables.GitHubData.Count > 9)
                    HasMoreData = true;

                //Get from cache
                GitHubProjects = GlobalVariables.GitHubData.Take(9).OrderBy(o => o.Name).ToList(); ;
            }

            DataLoaded = true;
        }

        private async Task<string[]> GetProjectTopics(string ProjectName)
        {
            HttpRequestMessage TopicsData = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/hmz777/{ProjectName}/topics");
            TopicsData.Headers.Accept.Clear();
            TopicsData.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.mercy-preview+json"));

            var topics = await HttpClient.SendAsync(TopicsData, HttpCompletionOption.ResponseContentRead);

            if (!topics.IsSuccessStatusCode)
                throw new Exception("Topics fetch failed!");

            var json = await topics.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<JsonElement>(json).GetProperty("names").EnumerateArray().Select(item => item.ToString()).ToArray();
        }

        public void Dispose()
        {
            JSRuntime.InvokeVoidAsync("BlazorHelpers.DisposeOfObservableElement", "#Work");
        }
    }
}
