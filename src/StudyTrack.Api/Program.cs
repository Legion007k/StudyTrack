using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
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

app.MapActividadRoutes();

app.Run();
