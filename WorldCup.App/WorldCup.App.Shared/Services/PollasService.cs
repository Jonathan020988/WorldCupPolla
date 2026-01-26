using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Http.Json;
using WorldCup.App.Shared.DTOs;

namespace WorldCup.App.Shared.Services
{
    public class PollasService
    {
        private readonly HttpClient _http;

        public PollasService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<PollaDto>> GetMisPollasAsync()
        {
            return await _http.GetFromJsonAsync<List<PollaDto>>(
                "api/Polla"
            ) ?? new();
        }

        public async Task<List<PollaDto>> GetPollasPorUsuarioAsync(int usuarioId)
        {
            return await _http.GetFromJsonAsync<List<PollaDto>>(
                $"api/Polla/usuario/{usuarioId}"
            ) ?? new();
        }

        public async Task<PollaDto?> GetPollaAsync(int pollaId)
        {
            return await _http.GetFromJsonAsync<PollaDto>(
                $"api/Polla/{pollaId}"
            );
        }

        public async Task<List<RankingPollaDto>> GetRankingAsync(int pollaId)
        {
            return await _http.GetFromJsonAsync<List<RankingPollaDto>>(
                $"api/Polla/{pollaId}/ranking"
            ) ?? new();
        }

        //public async Task<List<ParticipanteDto>> GetParticipantesAsync(int pollaId)
        //{
        //    return await _http.GetFromJsonAsync<List<ParticipanteDto>>(
        //        $"api/Polla/{pollaId}/participantes"
        //    ) ?? new();
        //}

        public async Task<List<string>> GetParticipantesAsync(int pollaId)
        {
            return await _http.GetFromJsonAsync<List<string>>(
                $"api/Polla/{pollaId}/participantes"
            ) ?? new();
        }

        public async Task InvitarUsuarioAsync(int pollaId, int usuarioId)
        {
            await _http.PostAsync(
                $"api/Polla/{pollaId}/invitar/{usuarioId}",
                null
            );
        }



    }
}
