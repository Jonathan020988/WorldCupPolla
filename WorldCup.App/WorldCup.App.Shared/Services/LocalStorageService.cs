using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;
using System.Text.Json;

namespace WorldCup.App.Shared.Services
{
    public class LocalStorageService
    {
        private readonly IJSRuntime _js;
        private readonly IDataProtector _protector;
        private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(18);

        public LocalStorageService(
            IJSRuntime js,
            IDataProtectionProvider dataProtectionProvider)
        {
            _js = js;
            _protector = dataProtectionProvider.CreateProtector("WorldCup.App.Session.v2");
        }

        public async Task SetAsync<T>(string key, T value)
        {
            var envelope = new ProtectedStorageEnvelope<T>
            {
                Value = value,
                ExpiresAt = DateTimeOffset.UtcNow.Add(SessionDuration)
            };
            var payload = JsonSerializer.Serialize(envelope);
            var protectedPayload = _protector.Protect(payload);

            await _js.InvokeVoidAsync("sessionStorage.setItem", key, protectedPayload);
            await _js.InvokeVoidAsync("localStorage.removeItem", key);
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var protectedPayload = await _js.InvokeAsync<string>("sessionStorage.getItem", key);
            if (string.IsNullOrWhiteSpace(protectedPayload))
            {
                return default;
            }

            try
            {
                var json = _protector.Unprotect(protectedPayload);
                var envelope = JsonSerializer.Deserialize<ProtectedStorageEnvelope<T>>(json);

                if (envelope == null || envelope.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    await RemoveAsync(key);
                    return default;
                }

                return envelope.Value;
            }
            catch
            {
                await RemoveAsync(key);
                return default;
            }
        }

        public async Task RemoveAsync(string key)
        {
            await _js.InvokeVoidAsync("sessionStorage.removeItem", key);
            await _js.InvokeVoidAsync("localStorage.removeItem", key);
        }

      
        private class ProtectedStorageEnvelope<T>
        {
            public T? Value { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
        }
    }
}
