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
            await _js.InvokeVoidAsync("sessionStorage.setItem", key, JsonSerializer.Serialize(value));
            await _js.InvokeVoidAsync("localStorage.removeItem", key);
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var json = await _js.InvokeAsync<string>("sessionStorage.getItem", key);
            return json == null ? default : JsonSerializer.Deserialize<T>(json);
        }

        public async Task RemoveAsync(string key)
        {
            await _js.InvokeVoidAsync("sessionStorage.removeItem", key);
            await _js.InvokeVoidAsync("localStorage.removeItem", key);
        }

      
    }
}
