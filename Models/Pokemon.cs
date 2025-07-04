using Newtonsoft.Json;

namespace PokeApp.Models
{
    public class Pokemon
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // Inicializa con string.Empty
        public Sprite? Sprites { get; set; } // Puede ser nulo
        public List<PokemonType> Types { get; set; } = new List<PokemonType>(); // Inicializa con lista vacía
    }

    public class Sprite
    {
        [JsonProperty("front_default")]
        public string? FrontDefault { get; set; } // Puede ser nulo
    }

    public class PokemonType
    {
        public TypeInfo? Type { get; set; } // Puede ser nulo
    }

    public class TypeInfo
    {
        public string Name { get; set; } = string.Empty; // Inicializa
        public string Url { get; set; } = string.Empty; // Inicializa
    }

    public class PokemonListResponse
    {
        public int Count { get; set; }
        public string? Next { get; set; } // Puede ser nulo
        public string? Previous { get; set; } // Puede ser nulo
        public List<PokemonListItem> Results { get; set; } = new List<PokemonListItem>(); // Inicializa
    }

    public class PokemonListItem
    {
        public string Name { get; set; } = string.Empty; // Inicializa
        public string Url { get; set; } = string.Empty; // Inicializa
    }

    public class PokemonSpecies
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // Inicializa
        public List<FlavorTextEntry> FlavorTextEntries { get; set; } = new List<FlavorTextEntry>(); // Inicializa
    }

    public class FlavorTextEntry
    {
        [JsonProperty("flavor_text")]
        public string FlavorText { get; set; } = string.Empty; // Inicializa
        public Language? Language { get; set; } // Puede ser nulo
    }

    public class Language
    {
        public string Name { get; set; } = string.Empty; // Inicializa
    }
}