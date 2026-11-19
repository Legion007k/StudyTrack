using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using StudyTrack.Api.Delegates;
using StudyTrack.Api.Extensions;
using StudyTrack.Api.Routes;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddApi();
builder.Services.AddDelegates(connectionString);

var app = builder.Build();

// Las migraciones se aplican con "dotnet ef database update", no al arrancar: aplicarlas aqui
// obligaria a los tests con WebApplicationFactory a tener un PostgreSQL disponible.
app.UseApiErrorResponses();

// Swagger siempre esta activo en Development. En otros entornos (el cluster corre como Production)
// solo se publica si se configura Swagger:Habilitado=true (variable de entorno Swagger__Habilitado).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Habilitado"))
{
    app.UseApiSwagger();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapActividadRoutes();
app.MapUsuarioRoutes();

app.Run();
