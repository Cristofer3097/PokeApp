using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using ClosedXML.Excel;
using MimeKit;
using MailKit.Net.Smtp;
using PokeApp.Services;
using PokeApp.Models;
using System.IO; // Para MemoryStream

namespace PokeApp.Controllers
{
    public class PokemonController : Controller
    {
        private readonly PokeApiService _pokeApiService;

        public PokemonController(PokeApiService pokeApiService)
        {
            _pokeApiService = pokeApiService;
        }

        public async Task<IActionResult> Index(string? nameFilter, string? speciesFilter, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                var pokemonsResponse = await _pokeApiService.GetPokemons(pageSize, (pageNumber - 1) * pageSize);
                var pokemons = new List<Pokemon>();

                if (pokemonsResponse?.Results != null) // Comprobar si Results no es nulo
                {
                    foreach (var item in pokemonsResponse.Results)
                    {
                        if (item?.Name != null) // Comprobar si Name no es nulo
                        {
                            var pokemonDetails = await _pokeApiService.GetPokemonDetails(item.Name);
                            if (pokemonDetails != null)
                            {
                                pokemons.Add(pokemonDetails);
                            }
                        }
                    }
                }

                // Filtrado
                if (!string.IsNullOrEmpty(nameFilter))
                {
                    pokemons = pokemons.Where(p => p.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (!string.IsNullOrEmpty(speciesFilter) && speciesFilter != "all")
                {
                    // Asegurarse de que Types no sea nulo antes de Any
                    pokemons = pokemons.Where(p => p.Types?.Any(t => t.Type?.Name != null && t.Type.Name.Equals(speciesFilter, StringComparison.OrdinalIgnoreCase)) == true).ToList();
                }

                // Paginación manual
                var totalPokemons = pokemonsResponse?.Count ?? 0; // Usar el operador de fusión de nulos
                var totalPages = (int)Math.Ceiling((double)totalPokemons / pageSize);

                ViewBag.CurrentPage = pageNumber;
                ViewBag.TotalPages = totalPages;
                ViewBag.NameFilter = nameFilter;
                ViewBag.SpeciesFilter = speciesFilter;

                var pokemonTypes = await _pokeApiService.GetPokemonTypes();
                ViewBag.PokemonTypes = new SelectList(pokemonTypes, "Name", "Name", speciesFilter);

                return View(pokemons);
            }
            catch (HttpRequestException ex)
            {
                ViewBag.ErrorMessage = $"Error al conectar con la API de Pokémon: {ex.Message}";
                return View("Error");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Ocurrió un error: {ex.Message}";
                return View("Error");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportToExcel(string? nameFilter, string? speciesFilter)
        {
            var pokemonsResponse = await _pokeApiService.GetPokemons(100000, 0);
            var pokemonsToExport = new List<Pokemon>();

            if (pokemonsResponse?.Results != null)
            {
                foreach (var item in pokemonsResponse.Results)
                {
                    if (item?.Name != null)
                    {
                        var pokemonDetails = await _pokeApiService.GetPokemonDetails(item.Name);
                        if (pokemonDetails != null)
                        {
                            pokemonsToExport.Add(pokemonDetails);
                        }
                    }
                }
            }

            // Aplicar filtros para la exportación
            if (!string.IsNullOrEmpty(nameFilter))
            {
                pokemonsToExport = pokemonsToExport.Where(p => p.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(speciesFilter) && speciesFilter != "all")
            {
                pokemonsToExport = pokemonsToExport.Where(p => p.Types?.Any(t => t.Type?.Name != null && t.Type.Name.Equals(speciesFilter, StringComparison.OrdinalIgnoreCase)) == true).ToList();
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Pokémon");
                worksheet.Cell(1, 1).Value = "Nombre";
                worksheet.Cell(1, 2).Value = "Especie";

                int row = 2;
                foreach (var pokemon in pokemonsToExport)
                {
                    worksheet.Cell(row, 1).Value = pokemon.Name;
                    // Usar el operador de navegación segura y fusión de nulos
                    worksheet.Cell(row, 2).Value = string.Join(", ", pokemon.Types?.Select(t => t.Type?.Name ?? string.Empty) ?? Enumerable.Empty<string>());
                    row++;
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Pokemons.xlsx");
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendEmail(string emailAddress, string subject, string body, string? nameFilter, string? speciesFilter, bool sendIndividual)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Pokemon App", "tu_correo@example.com")); // Reemplaza con tu correo
                message.To.Add(new MailboxAddress("", emailAddress));
                message.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = body };

                if (!sendIndividual)
                {
                    // Lógica para adjuntar el Excel (similar a ExportToExcel)
                    // Considera llamar a ExportToExcel aquí y adjuntar el resultado.
                    // var pokemonsToExport = new List<Pokemon>();
                    // ... (obtener datos filtrados) ...
                    // builder.Attachments.Add("Pokemons.xlsx", excelStream);
                }

                message.Body = builder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync("smtp.example.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync("tu_correo@example.com", "tu_contraseña");
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                TempData["SuccessMessage"] = "Correo enviado exitosamente!";
                return RedirectToAction("Index", new { nameFilter, speciesFilter });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al enviar el correo: {ex.Message}";
                return RedirectToAction("Index", new { nameFilter, speciesFilter });
            }
        }

        public async Task<IActionResult> Details(string name)
        {
            try
            {
                var pokemon = await _pokeApiService.GetPokemonDetails(name);
                var species = await _pokeApiService.GetPokemonSpecies(name);

                // Comprobar si pokemon y species no son nulos antes de pasarlos a la vista
                if (pokemon == null)
                {
                    return NotFound($"No se encontró el Pokémon con nombre: {name}");
                }

                ViewBag.PokemonSpecies = species; // species puede ser nulo, la vista ya lo maneja con '?'

                return PartialView("_PokemonDetailsPartial", pokemon);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener detalles del Pokémon: {ex.Message}");
            }
        }
    }
}