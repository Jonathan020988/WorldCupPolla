using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using System.Text.Json;

namespace WorldCup.App.Shared.Services
{
    public class LocalStorageService
    {
        private readonly IJSRuntime _js;

        public LocalStorageService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task SetAsync<T>(string key, T value)
        {
            await _js.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(value));
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var json = await _js.InvokeAsync<string>("localStorage.getItem", key);
            return json == null ? default : JsonSerializer.Deserialize<T>(json);
        }

        public async Task RemoveAsync(string key)
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", key);
        }

        //public async Task SetAsync<T>(string key, T value)
        //{
        //    var json = JsonSerializer.Serialize(value);
        //    await _js.InvokeVoidAsync("localStorage.setItem", key, json);
        //}

        //public async Task<T?> GetAsync<T>(string key)
        //{
        //    var json = await _js.InvokeAsync<string>("localStorage.getItem", key);
        //    if (string.IsNullOrEmpty(json)) return default;
        //    return JsonSerializer.Deserialize<T>(json);
        //}

        //public async Task RemoveAsync(string key)
        //{
        //    await _js.InvokeVoidAsync("localStorage.removeItem", key);
        //}
    }
}
