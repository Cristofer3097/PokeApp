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

        public async Task<PokemonListResponse?> GetPokemons(int limit, int offset)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}pokemon?limit={limit}&offset={offset}");
                response.EnsureSuccessStatusCode(); // Lanza HttpRequestException para códigos de error HTTP
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<PokemonListResponse>(content);
            }
            catch (HttpRequestException)
            {
                // Puedes loggear el error aquí si lo deseas
                return null; // Devuelve null si falla la llamada HTTP
            }
            catch (JsonException)
            {
                // Puedes loggear el error de deserialización
                return null; // Devuelve null si falla la deserialización
            }
        }

        public async Task<Pokemon?> GetPokemonDetails(string nameOrId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}pokemon/{nameOrId}/");
                response.EnsureSuccessStatusCode(); // Lanza HttpRequestException para códigos de error HTTP
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<Pokemon>(content);
            }
            catch (HttpRequestException)
            {
                return null; // Devuelve null si falla la llamada HTTP (ej. 404)
            }
            catch (JsonException)
            {
                return null; // Devuelve null si falla la deserialización
            }
        }

        public async Task<PokemonSpecies?> GetPokemonSpecies(string nameOrId)
        {
            string cacheKey = $"PokemonSpecies_{nameOrId}";
            if (_cache.TryGetValue(cacheKey, out PokemonSpecies? species))
            {
                return species;
            }

            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}pokemon-species/{nameOrId}/");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                species = JsonConvert.DeserializeObject<PokemonSpecies>(content);

                if (species != null)
                {
                    _cache.Set(cacheKey, species, TimeSpan.FromMinutes(10));
                }
                return species;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public async Task<List<TypeInfo>> GetPokemonTypes()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}type/");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<dynamic>(content);
                List<TypeInfo> types = new List<TypeInfo>();

                if (result != null && result.results != null)
                {
                    foreach (var item in result.results)
                    {
                        if (item != null && item.name != null && item.url != null)
                        {
                            types.Add(new TypeInfo { Name = item.name, Url = item.url });
                        }
                    }
                }
                return types;
            }
            catch (HttpRequestException)
            {
                return new List<TypeInfo>(); // Devuelve una lista vacía en caso de error HTTP
            }
            catch (JsonException)
            {
                return new List<TypeInfo>(); // Devuelve una lista vacía en caso de error de deserialización
            }
        }
    }
}