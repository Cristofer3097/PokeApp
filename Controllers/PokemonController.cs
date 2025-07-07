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
            try
            {
                // 1. Obtener la lista de todos los nombres de Pokémon (esto es rápido, solo una llamada a la API)
                var pokemonsResponse = await _pokeApiService.GetPokemons(2000, 0); // Un límite alto para traer a todos
                var pokemonList = pokemonsResponse?.Results ?? new List<PokemonListItem>();

                // 2. Aplicar el filtro por nombre ANTES de buscar los detalles
                if (!string.IsNullOrEmpty(nameFilter))
                {
                    pokemonList = pokemonList.Where(p => p.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var pokemonsToExport = new List<Pokemon>();
                // 3. Obtener los detalles solo para la lista ya pre-filtrada
                foreach (var item in pokemonList)
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

                // 4. Aplicar el filtro por especie (tipo) sobre la lista detallada
                if (!string.IsNullOrEmpty(speciesFilter) && speciesFilter != "all")
                {
                    pokemonsToExport = pokemonsToExport.Where(p => p.Types?.Any(t => t.Type?.Name.Equals(speciesFilter, StringComparison.OrdinalIgnoreCase) == true) == true).ToList();
                }

                // 5. Crear el archivo Excel (esta parte es igual)
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
                        // Esta línea le dice al navegador que descargue el archivo
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Pokemons.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al exportar a Excel: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return BadRequest("El nombre del Pokémon es requerido.");
            }

            try
            {
                // 1. Obtener los detalles principales del Pokémon
                var pokemonDetails = await _pokeApiService.GetPokemonDetails(name);
                if (pokemonDetails == null)
                {
                    return NotFound($"No se encontraron detalles para el Pokémon: {name}");
                }

                // 2. Obtener los detalles de la especie para la descripción
                var pokemonSpecies = await _pokeApiService.GetPokemonSpecies(name);
                ViewBag.PokemonSpecies = pokemonSpecies;

                // 3. Devolver la vista parcial con los datos para mostrar en el modal
                return PartialView("_PokemonDetailsPartial", pokemonDetails);
            }
            catch (Exception ex)
            {
                // En caso de un error inesperado, devolver una respuesta de error al AJAX.
                return StatusCode(500, $"Error interno al procesar la solicitud: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendEmail(
    string emailAddress, string subject, string body,
    string? nameFilter, string? speciesFilter,
    string? pokemonName, int pokemonId, string? pokemonTypes, string? pokemonImage)
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

                // --- LÓGICA ACTUALIZADA ---
                // Si se proporciona un pokemonName, es un correo individual con sus detalles.
                if (!string.IsNullOrEmpty(pokemonName))
                {
                    // Construir un cuerpo de correo en HTML con los detalles del Pokémon.
                    builder.HtmlBody = $@"
                <h1>Detalles de {pokemonName}</h1>
                <img src='{pokemonImage}' alt='Imagen de {pokemonName}' width='150' />
                <p><strong>ID:</strong> {pokemonId}</p>
                <p><strong>Especie(s):</strong> {pokemonTypes}</p>
                <hr>
                <p>{body}</p>"; // Añade el mensaje personalizado del usuario.
                }
                else // Si no, es un correo con la lista completa (comportamiento anterior).
                {
                    builder.HtmlBody = body; // Cuerpo del correo general.

                    // Lógica para adjuntar el Excel (similar a ExportToExcel)
                    var pokemonsResponse = await _pokeApiService.GetPokemons(100000, 0); // O un límite razonable
                    var pokemonsToExport = new List<Pokemon>();

                    if (pokemonsResponse?.Results != null)
                    {
                        // Este bucle puede ser lento. Para producción, considera una estrategia de carga en segundo plano.
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
                            stream.Position = 0;
                            builder.Attachments.Add("Pokemons.xlsx", stream.ToArray(), ContentType.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
                        }
                    }
                }

                message.Body = builder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true; // Solo para desarrollo.
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
    }
}