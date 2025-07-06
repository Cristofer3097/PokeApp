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
using System.IO;
using Microsoft.Extensions.Configuration; // Necesario para IConfiguration

namespace PokeApp.Controllers
{
    public class PokemonController : Controller
    {
        private readonly PokeApiService _pokeApiService;
        private readonly IConfiguration _configuration; // Inyectar IConfiguration

        public PokemonController(PokeApiService pokeApiService, IConfiguration configuration)
        {
            _pokeApiService = pokeApiService;
            _configuration = configuration; // Asignar IConfiguration
        }

        public async Task<IActionResult> Index(string? nameFilter, string? speciesFilter, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                var pokemonsResponse = await _pokeApiService.GetPokemons(pageSize, (pageNumber - 1) * pageSize);
                var pokemons = new List<Pokemon>();

                if (pokemonsResponse?.Results != null)
                {
                    foreach (var item in pokemonsResponse.Results)
                    {
                        // Solo intentar obtener detalles si el nombre del Pokémon en la lista no es nulo o vacío
                        if (!string.IsNullOrEmpty(item?.Name))
                        {
                            var pokemonDetails = await _pokeApiService.GetPokemonDetails(item.Name);
                            // Solo añadir a la lista si se obtuvieron los detalles correctamente
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
                    pokemons = pokemons.Where(p => p.Types?.Any(t => t.Type?.Name != null && t.Type.Name.Equals(speciesFilter, StringComparison.OrdinalIgnoreCase)) == true).ToList();
                }

                // Paginación manual
                var totalPokemons = pokemonsResponse?.Count ?? 0;
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
            var pokemonsResponse = await _pokeApiService.GetPokemons(100000, 0); // Obtener todos los Pokémon (o un número muy grande)
            var pokemonsToExport = new List<Pokemon>();

            if (pokemonsResponse?.Results != null)
            {
                foreach (var item in pokemonsResponse.Results)
                {
                    if (!string.IsNullOrEmpty(item?.Name))
                    {
                        var pokemonDetails = await _pokeApiService.GetPokemonDetails(item.Name);
                        if (pokemonDetails != null)
                        {
                            pokemonsToExport.Add(pokemonDetails);
                        }
                    }
                }
            }

            // Aplicar filtros a la lista completa antes de exportar
            if (!string.IsNullOrEmpty(nameFilter))
            {
                pokemonsToExport = pokemonsToExport.Where(p => p.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(speciesFilter) && speciesFilter != "all")
            {
                pokemonsToExport = pokemonsToExport.Where(p => p.Types?.Any(t => t.Type?.Name != null && t.Type.Name.Equals(speciesFilter, StringComparison.OrdinalIgnoreCase)) == true).ToList();
            }

            // Crear el archivo Excel
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Pokémon");
                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "Nombre";
                worksheet.Cell(1, 3).Value = "Especie";

                int row = 2;
                foreach (var pokemon in pokemonsToExport)
                {
                    worksheet.Cell(row, 1).Value = pokemon.Id;
                    worksheet.Cell(row, 2).Value = pokemon.Name;
                    worksheet.Cell(row, 3).Value = string.Join(", ", pokemon.Types?.Select(t => t.Type?.Name ?? string.Empty) ?? Enumerable.Empty<string>());
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
                var senderEmail = _configuration["SmtpSettings:SenderEmail"];
                var senderPassword = _configuration["SmtpSettings:SenderPassword"];
                var smtpHost = _configuration["SmtpSettings:SmtpHost"];
                var smtpPort = int.Parse(_configuration["SmtpSettings:SmtpPort"]);

                if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword) || string.IsNullOrEmpty(smtpHost) || smtpPort == 0)
                {
                    TempData["ErrorMessage"] = "Error de configuración SMTP. Por favor, verifica appsettings.json.";
                    return RedirectToAction("Index", new { nameFilter, speciesFilter });
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Pokemon App", senderEmail));
                message.To.Add(new MailboxAddress("", emailAddress));
                message.Subject = subject;

                var builder = new BodyBuilder();

                if (sendIndividual)
                {
                    // Lógica para enviar detalles del Pokémon seleccionado
                    // Esto requeriría saber qué Pokémon está actualmente en el modal de detalles
                    // y pasarlo al controlador, lo cual no está implementado en la vista actual.
                    // Para una implementación individual, necesitarías modificar el JS
                    // que abre el modal de detalles para capturar el nombre del Pokémon y enviarlo aquí.
                    // Por ahora, solo se enviará el cuerpo del correo general.
                    builder.HtmlBody = body;
                }
                else
                {
                    // Lógica para adjuntar el Excel (similar a ExportToExcel)
                    var pokemonsResponse = await _pokeApiService.GetPokemons(100000, 0);
                    var pokemonsToExport = new List<Pokemon>();

                    if (pokemonsResponse?.Results != null)
                    {
                        foreach (var item in pokemonsResponse.Results)
                        {
                            if (!string.IsNullOrEmpty(item?.Name))
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
                        worksheet.Cell(1, 1).Value = "ID";
                        worksheet.Cell(1, 2).Value = "Nombre";
                        worksheet.Cell(1, 3).Value = "Especie";

                        int row = 2;
                        foreach (var pokemon in pokemonsToExport)
                        {
                            worksheet.Cell(row, 1).Value = pokemon.Id;
                            worksheet.Cell(row, 2).Value = pokemon.Name;
                            worksheet.Cell(row, 3).Value = string.Join(", ", pokemon.Types?.Select(t => t.Type?.Name ?? string.Empty) ?? Enumerable.Empty<string>());
                            row++;
                        }

                        using (var stream = new MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            stream.Position = 0; // Reiniciar la posición del stream
                            builder.Attachments.Add("Pokemons.xlsx", stream);
                        }
                    }
                    builder.HtmlBody = body; // Añadir también el cuerpo del correo
                }

                message.Body = builder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true; // Solo para desarrollo, no usar en producción
                    await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(senderEmail, senderPassword);
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
                if (pokemon == null)
                {
                    return NotFound($"No se encontró el Pokémon con nombre/ID: '{name}'. La API no devolvió datos para este Pokémon.");
                }

                var species = await _pokeApiService.GetPokemonSpecies(name);
                ViewBag.PokemonSpecies = species;

                return PartialView("_PokemonDetailsPartial", pokemon);
            }
            catch (HttpRequestException ex)
            {
                var statusCode = ex.StatusCode.HasValue ? $"Código de estado: {(int)ex.StatusCode.Value}. " : "";
                return StatusCode((int)(ex.StatusCode ?? System.Net.HttpStatusCode.InternalServerError),
                                  $"Error de la API al obtener detalles de '{name}': {statusCode}{ex.Message}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Ocurrió un error al obtener detalles del Pokémon '{name}': {ex.Message}");
            }
        }
    }
}