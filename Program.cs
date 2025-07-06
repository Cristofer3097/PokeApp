using PokeApp.Services;
using Microsoft.Extensions.Configuration; // Necesario para IConfiguration

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<PokeApiService>(); // Registra el HttpClient para tu servicio
builder.Services.AddMemoryCache();

// Construye la configuración para poder acceder a appsettings.json
var configuration = builder.Configuration;
builder.Services.AddSingleton<IConfiguration>(configuration); // Añadir IConfiguration como un singleton

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Pokemon}/{action=Index}/{id?}");

app.Run();