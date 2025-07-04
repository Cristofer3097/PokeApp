using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.Extensions.Caching.Memory;
using System;
using PokeApp.Models;

namespace PokeApp.Services
{
    public class PokeApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;

        public PokeApiService(HttpClient httpClient, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
        }

        private const string BaseUrl = "https://pokeapi.co/api/v2/";

        // Marcar como anulable el retorno, ya que DeserializeObject podría devolver null
        public async Task<PokemonListResponse?> GetPokemons(int limit, int offset)
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}pokemon?limit={limit}&offset={offset}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<PokemonListResponse>(content);
        }

        // Marcar como anulable el retorno
        public async Task<Pokemon?> GetPokemonDetails(string nameOrId)
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}pokemon/{nameOrId}/");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Pokemon>(content);
        }

        // Marcar como anulable el parámetro 'species' en 'out'
        public async Task<PokemonSpecies?> GetPokemonSpecies(string nameOrId)
        {
            string cacheKey = $"PokemonSpecies_{nameOrId}";
            if (_cache.TryGetValue(cacheKey, out PokemonSpecies? species)) // Usar '?' para el parámetro out
            {
                return species;
            }

            var response = await _httpClient.GetAsync($"{BaseUrl}pokemon-species/{nameOrId}/");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // Si estás seguro de que la deserialización no será nula, puedes usar '!'
            // Si podría ser nula, maneja el caso de null.
            species = JsonConvert.DeserializeObject<PokemonSpecies>(content);

            if (species != null) // Comprobar si species no es null antes de cachear
            {
                _cache.Set(cacheKey, species, TimeSpan.FromMinutes(10));
            }
            return species;
        }

        public async Task<List<TypeInfo>> GetPokemonTypes()
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}type/");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // Usar '?' para el resultado de DeserializeObject y comprobar antes de acceder a 'results'
            var result = JsonConvert.DeserializeObject<dynamic>(content);
            List<TypeInfo> types = new List<TypeInfo>();

            // Comprobación para evitar CS8602 y CS8600
            if (result != null && result.results != null)
            {
                foreach (var item in result.results)
                {
                    // Añadir comprobaciones individuales para item.name y item.url
                    if (item != null && item.name != null && item.url != null)
                    {
                        types.Add(new TypeInfo { Name = item.name, Url = item.url });
                    }
                }
            }
            return types;
        }
    }
}