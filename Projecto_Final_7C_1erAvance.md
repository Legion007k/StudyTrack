# StudyTrack - REST API Contract (PRD)

> Contrato del microservicio REST de StudyTrack. El único recurso es `Actividades`: actividades de estudio organizadas en árbol (actividad raíz con subactividades anidadas).

## Overview
- **Service**: StudyTrack Management REST API
- **Language/Framework**: C# .NET 10.0 with Minimal APIs
- **Base URL**: `http://localhost:8080` (configurable via `Urls` en `appsettings.json` y `Properties/launchSettings.json`)
- **Content-Type**: `application/json` (las respuestas de error usan `application/problem+json`)
- **ID Format**: Regex `[A-Za-z0-9\-]+` (se valida la cadena completa)

---

## Architecture

### Layered Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                        API Layer (Minimal APIs)             │
│  • Endpoint definitions (MapGroup, MapGet, MapPost, etc.)   │
│  • Request/Response mapping (DTOs)                          │
│  • Validation (FluentValidation + endpoint filters)         │
│  • Error handling (GlobalExceptionHandler, ProblemDetails)  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Delegate Layer                         │
│  • IActividadDelegate (one per domain)                      │
│  • Business logic orchestration                             │
│  • Business rules (duplicados, fechas, jerarquía)           │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Repository Layer                         │
│  • IActividadRepository (one per aggregate root)            │
│  • Data access (EF Core + PostgreSQL / Npgsql)              │
│  • Query building (carga del subárbol con Include)          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Domain Models                          │
│  • Entities: Actividad                                      │
│  • Reglas de dominio: profundidad máxima, completar/reabrir │
│  • Enums: TipoActividad (General, Examen, Entrega,          │
│    Presentacion)                                            │
└─────────────────────────────────────────────────────────────┘
```

Todas las capas viven en un solo proyecto (`StudyTrack.Api`), separadas por carpetas y espacios de nombres. Las dependencias entre capas se verifican con pruebas de arquitectura (NetArchTest).

### Key Patterns
| Pattern | Implementation |
|---------|----------------|
| **Minimal APIs** | `MapGroup("/actividades")` con `MapGet()`, `MapPost()`, etc. en `Routes/ActividadRoutes.cs`; `Program.cs` llama a `app.MapActividadRoutes()` |
| **Delegate** | One per domain (`IActividadDelegate`) - orchestrates repositories, contains business rules |
| **Repository** | One per aggregate root (`IActividadRepository`) - data access abstraction, testable via EF Core InMemory |
| **Dependency Injection** | Built-in DI container, scoped per request; registro con `AddApi()`, `AddDelegates()` y `AddRepositories()`. La hora actual se inyecta con `TimeProvider` |
| **Validation** | FluentValidation for request DTOs, ejecutada por `ValidationFilter<T>`; el ID de ruta se valida con `IdFormatFilter` |
| **Serialization** | System.Text.Json with camelCase, enum as string (no se aceptan enums numéricos) |
| **Database** | EF Core 10 + PostgreSQL (Npgsql) (`ApplicationDbContext`); fechas guardadas como `timestamp without time zone` |
| **Identity** | Sin ASP.NET Core Identity ni tabla de usuarios: `userId` es un identificador opaco enviado por el cliente |


---

## Endpoints

Recurso principal: `Actividades`.

| Method  | Path | Request Body | Response | Description |
|---------|------|--------------|----------|-------------|
| `GET`   | `/actividades` | - | `200` `ActividadDto[]` |  Lista las actividades raíz con sus subactividades anidadas (árbol), ordenadas por `fechaInicio` |
| `GET`   | `/actividades/{actividadId}` | - | `200` `ActividadDto` \| `400` (invalid ID) \| `404` | Obtiene una actividad con sus subactividades |
| `POST`  | `/actividades` | `CreateActividadDto` | `201` `ActividadDto` + `Location` \| `400` \| `415` \| `422` | Crea una actividad raíz o subactividad. `fechaInicio` la fija el servidor con la hora de creación |
| `PUT`   | `/actividades/{actividadId}` | `UpdateActividadDto` | `200` `ActividadDto` \| `400` \| `404` \| `415` \| `422` | Reemplaza los campos editables de una actividad. Con `completada: true` se fija `fechaCompletada` |
| `DELETE`| `/actividades/{actividadId}` | - | `204` \| `400` (invalid ID) \| `404` | Elimina actividad y todas sus subactividades |

**ActividadDto**
```json
{
  "id": "string",
  "userId": "string",
  "titulo": "string",
  "descripcion": "string | null",
  "fechaInicio": "2026-12-15T23:59:00",
  "fechaFin": "2026-12-15T23:59:00",
  "tipo": "General | Examen | Entrega | Presentacion",
  "completada": false,
  "fechaCompletada": "2026-12-15T23:59:00 | null",
  "generadaAutomaticamente": false,
  "actividadPadreId": "string | null",
  "actividadesHijas": "ActividadDto[]"
}
```

**CreateActividadDto**
```json
{
  "userId": "string",
  "titulo": "string",
  "descripcion": "string (opcional)",
  "fechaFin": "2026-12-15T23:59:00",
  "tipo": "General | Examen | Entrega | Presentacion (opcional, por defecto General)",
  "actividadPadreId": "string | null"
}
```

**UpdateActividadDto**
```json
{
  "titulo": "string",
  "descripcion": "string | null",
  "fechaFin": "2026-12-15T23:59:00",
  "tipo": "General | Examen | Entrega | Presentacion (opcional, por defecto General)",
  "completada": false,
  "actividadPadreId": "string | null"
}
```

---

## Error Responses

| Code | Scenario |
|------|----------|
| `400` | Formato de ID inválido en la ruta (ej: `id_invalido`, `id.con.puntos`), error en `actividadId`. JSON malformado o con tipos incorrectos (fecha inválida, `tipo` desconocido o numérico), error en `body`. Fallo de validación: `userId`, `titulo` o `fechaFin` vacíos; `userId` o `actividadPadreId` con caracteres no permitidos (ej: `usuario_1`, `padre#1`); `userId` > 64, `titulo` > 200 o `descripcion` > 2000 caracteres; `fechaFin` con zona horaria (`Z` u offset) |
| `404` | Actividad no encontrada por ID (GET, PUT, DELETE) |
| `415` | Content-Type del cuerpo no es `application/json` (POST, PUT) |
| `422` | Violación de regla de negocio: `fechaFin` anterior a `fechaInicio` (al crear equivale a una fecha en el pasado), actividad duplicada (mismo `userId` + `titulo` + `fechaFin`), `actividadPadreId` inexistente, `actividadPadreId` pertenece a otro `userId`, `actividadPadreId` es la propia actividad o una de sus subactividades, se supera la profundidad máxima del tipo de la raíz (General 1 nivel, Examen 2, Presentacion 2, Entrega 3), cambiar el tipo de una raíz deja subactividades fuera del límite |
| `500` | Excepción no controlada (se registra en el log y no se exponen detalles internos) |

**Error Response Format**

Todas las respuestas de error se envían con `Content-Type: application/problem+json`.

**400 - Validation Failed**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Failed",
  "status": 400,
  "errors": {
    "titulo": ["El titulo es obligatorio."],
    "fechaFin": ["La fecha de fin debe enviarse sin zona horaria, por ejemplo 2026-09-20T10:00:00."]
  }
}
```

**404 - Not Found**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "No existe una actividad con id 'no-existe-123'."
}
```

**415 - Unsupported Media Type**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.13",
  "title": "Unsupported Media Type",
  "status": 415,
  "detail": "El cuerpo de la solicitud debe enviarse como application/json."
}
```

**422 - Business Rule Violation**
```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Business Rule Violation",
  "status": 422,
  "errors": {
    "titulo": ["El usuario ya tiene una actividad con el mismo titulo y fecha de fin."]
  }
}
```

**500 - Internal Server Error**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "Ocurrio un error inesperado al procesar la solicitud."
}
```

---

## Configuration (`appsettings.json`)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=studytrack_db;Username=postgres;Password="
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Urls": "http://localhost:8080"
}
```

---

## Testing Requirements

### Test Strategy Overview
| Layer | Test Type | Framework | Coverage Target |
|-------|-----------|-----------|-----------------|
| API Routes | Integration | xUnit + WebApplicationFactory + EF Core InMemory + FakeTimeProvider | 100% endpoint coverage |
| Delegates | Unit | xUnit + Moq + FakeTimeProvider | 90%+ branch coverage |
| Repositories | Unit + Integration | xUnit + EF Core InMemory | 80%+ |
| Domain | Unit | xUnit | 100% business logic |
| Arquitectura | Unit | xUnit + NetArchTest.Rules | Dependencias entre capas |

---

#### Required Test Cases per Endpoint

**Actividades Routes** (`/actividades`)
| Test Case | Description |
|-----------|-------------|
| `GET /actividades` returns 200 with empty array | No hay actividades en DB |
| `GET /actividades` returns 200 with items | Existen varias actividades; las raíces vienen ordenadas y con sus hijas anidadas |
| `GET /actividades/{id}` returns 200 with item | ID válido existe |
| `GET /actividades/{id}` returns 404 | ID válido pero no existe |
| `GET /actividades/{id}` returns 400 | ID con formato inválido (ej: `id_con_guion_bajo`, `id!`, espacios, `ñ`) |
| `POST /actividades` returns 201 + Location | CreateActividadDto válido; devuelve la actividad creada con ID GUID y `fechaInicio` = hora de creación |
| `POST /actividades` ignora `fechaInicio` del cliente | Se usa siempre la hora de creación |
| `POST /actividades` returns 201 (subactividad) | CreateActividadDto válido con `actividadPadreId`; queda anidada en el padre |
| `POST /actividades` returns 400 | Falta `userId`, `titulo` o `fechaFin`; `titulo` en blanco; `userId` = `usuario_1`; `actividadPadreId` = `padre#1`; `fechaFin` con zona horaria `Z`; `titulo` > 200 caracteres |
| `POST /actividades` returns 415 | Content-Type no es `application/json` |
| `POST /actividades` returns 422 | `fechaFin` anterior a `fechaInicio` |
| `POST /actividades` returns 422 | Actividad duplicada (mismo `userId` + `titulo` + `fechaFin`) |
| `POST /actividades` returns 422 | `actividadPadreId` no existe |
| `POST /actividades` returns 422 | Se supera la profundidad máxima del tipo de la raíz |
| `PUT /actividades/{id}` returns 200 | UpdateActividadDto válido, marcar `completada: true` (fija `fechaCompletada`) |
| `PUT /actividades/{id}` returns 404 | ID válido pero no existe |
| `PUT /actividades/{id}` returns 400 | `titulo` vacío |
| `PUT /actividades/{id}` returns 415 | Content-Type no es `application/json` |
| `PUT /actividades/{id}` returns 422 | `fechaFin` anterior a `fechaInicio` |
| `PUT /actividades/{id}` returns 422 | `actividadPadreId` es una de sus propias subactividades |
| `DELETE /actividades/{id}` returns 204 | ID válido existe, responde sin cuerpo y elimina también subactividades |
| `DELETE /actividades/{id}` returns 404 | ID válido pero no existe |

#### Cross-Cutting Route Tests
| Test Case | Description |
|-----------|-------------|
| All endpoints return 401/403 | When auth middleware enabled (future; no hay autenticación ni pruebas por ahora) |
| Request validation | FluentValidation errors return 400 with ProblemDetails (`type`, `title`, `status`, `errors`) y no llegan al delegate |
| Malformed body | JSON malformado, fecha inválida o `tipo` desconocido/numérico return 400 con error en `body` |
| Content-Type | Respuestas exitosas son `application/json`; errores son `application/problem+json`; POST/PUT con otro Content-Type return 415 |
| ID format validation | GET, PUT y DELETE rechazan con 400 los `{id}` que no cumplen la regex (incluido `%0A`); un ID válido inexistente da 404 |
| Error handling | 404, 422 y 500 con formato ProblemDetails; el 500 no expone detalles internos |
| Concurrency | 25 POST en paralelo crean todas las actividades; 10 PUT en paralelo dejan un estado completo de una de las solicitudes |

---

### Delegate Tests (Unit Tests)

#### Setup Pattern
```csharp
public class ActividadDelegateTests
{
    private static readonly DateTime Ahora = ActividadBuilder.FechaBase;

    private readonly Mock<IActividadRepository> _repoMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly IActividadDelegate _delegate;

    public ActividadDelegateTests()
    {
        _repoMock = new Mock<IActividadRepository>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(Ahora, TimeSpan.Zero));
        _delegate = new ActividadDelegate(_repoMock.Object, _timeProvider);
    }
}
```

#### Required Test Cases per Delegate

**ActividadDelegate**
| Method | Test Cases |
|--------|------------|
| `GetByIdAsync` | Returns DTO when found, null when not found, maps subactividades anidadas en orden |
| `GetAllAsync` | Returns empty list, returns raíces con sus hijas anidadas |
| `CreateAsync` | Generates ID, saves via repo, returns ID, `fechaInicio` = momento de creación, validates unique constraint, rechaza `fechaFin` anterior a la creación, enlaza padre válido, padre inexistente, padre de otro usuario, profundidad máxima por tipo (Entrega admite 3 niveles) |
| `UpdateAsync` | Updates all fields and returns updated DTO, throws if not found, al completar fija `fechaCompletada`, si ya estaba completada conserva la fecha original, al reabrir limpia `fechaCompletada`, `fechaFin` anterior a `fechaInicio`, duplicado excluyéndose a sí misma, padre = misma actividad, padre = descendiente, mover subárbol fuera del límite, cambiar tipo de raíz fuera del límite, quitar el padre la convierte en raíz |
| `DeleteAsync` | Deletes via repo entregando el subárbol completo, throws if not found |

---

### Test Data Builders
```csharp
// Fluent builder con valores fijos por defecto para pruebas deterministas
public class ActividadBuilder
{
    public static readonly DateTime FechaBase = new(2026, 9, 1, 8, 0, 0);

    private readonly Actividad _actividad = new()
    {
        Id = "actividad-1",
        UserId = "usuario-1",
        Titulo = "Examen de Calculo",
        Descripcion = "Derivadas e integrales",
        FechaInicio = FechaBase,
        FechaFin = FechaBase.AddDays(7),
        Tipo = TipoActividad.General
    };

    public ActividadBuilder WithId(string id) { _actividad.Id = id; return this; }
    public ActividadBuilder WithUserId(string userId) { _actividad.UserId = userId; return this; }
    public ActividadBuilder WithTitulo(string titulo) { _actividad.Titulo = titulo; return this; }
    public ActividadBuilder WithDescripcion(string? descripcion) { _actividad.Descripcion = descripcion; return this; }
    public ActividadBuilder WithFechaInicio(DateTime fechaInicio) { _actividad.FechaInicio = fechaInicio; return this; }
    public ActividadBuilder WithFechaFin(DateTime fechaFin) { _actividad.FechaFin = fechaFin; return this; }
    public ActividadBuilder WithTipo(TipoActividad tipo) { _actividad.Tipo = tipo; return this; }
    public ActividadBuilder WithGeneradaAutomaticamente(bool generada) { _actividad.GeneradaAutomaticamente = generada; return this; }

    public ActividadBuilder WithCompletada(DateTime fechaCompletada)
    {
        _actividad.Completada = true;
        _actividad.FechaCompletada = fechaCompletada;
        return this;
    }

    // Enlaza ambos lados de la relación: la hija apunta al padre y el padre la incluye
    public ActividadBuilder WithPadre(Actividad padre)
    {
        _actividad.ActividadPadreId = padre.Id;
        _actividad.ActividadPadre = padre;
        padre.ActividadesHijas.Add(_actividad);
        return this;
    }

    public Actividad Build() => _actividad;
}
```

---

### Test Execution Requirements
| Requirement | Detail |
|-------------|--------|
| **CI Pipeline** | No hay pipeline de CI configurado en el repositorio; las pruebas se ejecutan con `dotnet test` |
| **Coverage** | `dotnet test --collect:"XPlat Code Coverage"` (coverlet.collector); no hay umbrales configurados |
| **Parallelization** | Tests must be parallelizable (no shared state): cada prueba de rutas levanta su propio host con una base InMemory de nombre único |
| **Determinism** | No flaky tests; fechas fijas con `FakeTimeProvider` y `ActividadBuilder.FechaBase`, IDs fijos |
| **Isolation** | Each test creates own data; no test ordering dependency |

---

### Test Project Structure
```
tests/
└── StudyTrack.Tests/
    ├── Arquitectura/
    │   └── CapasTests.cs
    ├── Builders/
    │   └── ActividadBuilder.cs
    ├── Delegates/
    │   └── ActividadDelegateTests.cs
    ├── Domain/
    │   └── ActividadTests.cs
    ├── Middleware/
    │   └── ValidationMiddlewareTests.cs
    ├── Repositories/
    │   ├── ActividadRepositoryTests.cs
    │   └── ApplicationDbContextTests.cs
    ├── Routes/
    │   ├── ActividadRoutesTests.cs
    │   ├── CrossCuttingRoutesTests.cs
    │   └── StudyTrackApiTests.cs (base class)
    └── StudyTrack.Tests.csproj
```

---

## Project Structure
**Estructura objetivo**
```
StudyTrack/ 
├── src/ 
│ ├── StudyTrack.Api/ 
│ │ ├── Program.cs # Minimal API setup, DI registration 
│ │ ├── Routes/ 
│ │ │ └── ActividadRoutes.cs 
│ │ ├── Dtos/ 
│ │ │ └── ActividadDtos.cs 
│ │ ├── Validators/ 
│ │ └── Extensions/ 
│ ├── StudyTrack.Delegates/ 
│ │ ├── IActividadDelegate.cs 
│ │ └── ActividadDelegate.cs 
│ ├── StudyTrack.Repositories/ 
│ │ ├── IActividadRepository.cs 
│ │ └── ActividadRepository.cs 
│ └── StudyTrack.Domain/ 
│ └── Actividad.cs 
├── tests/ 
│ ├── StudyTrack.Api.Tests/ 
│ ├── StudyTrack.Delegates.Tests/ 
│ └── StudyTrack.Repositories.Tests/ 
└── StudyTrack.slnx
```
**Estructura actual**
```
StudyTrack/ 
├── src/ 
│ └── StudyTrack.Api/ 
│   ├── Program.cs # Minimal API setup (AddApi, AddDelegates, MapActividadRoutes) 
│   ├── appsettings.json 
│   ├── appsettings.Development.json 
│   ├── Properties/ 
│   │ └── launchSettings.json 
│   ├── Routes/ 
│   │ └── ActividadRoutes.cs 
│   ├── Dtos/ 
│   │ └── ActividadDtos.cs 
│   ├── Validators/ 
│   │ ├── ActividadRules.cs 
│   │ ├── CreateActividadDtoValidator.cs 
│   │ ├── UpdateActividadDtoValidator.cs 
│   │ └── IdFormat.cs 
│   ├── Extensions/ 
│   │ ├── ApiApplicationBuilderExtensions.cs 
│   │ ├── ApiServiceCollectionExtensions.cs 
│   │ ├── GlobalExceptionHandler.cs 
│   │ ├── IdFormatFilter.cs 
│   │ ├── ProblemDetailsResponses.cs 
│   │ └── ValidationFilter.cs 
│   ├── Delegates/ 
│   │ ├── IActividadDelegate.cs 
│   │ ├── ActividadDelegate.cs 
│   │ ├── DelegateServiceCollectionExtensions.cs 
│   │ └── Exceptions/ 
│   │   ├── BusinessRuleException.cs 
│   │   └── NotFoundException.cs 
│   ├── Repositories/ 
│   │ ├── IActividadRepository.cs 
│   │ ├── ActividadRepository.cs 
│   │ ├── ApplicationDbContext.cs 
│   │ ├── RepositoryServiceCollectionExtensions.cs 
│   │ └── Migrations/ 
│   └── Domain/ 
│     └── Actividad.cs 
├── tests/ 
│ └── StudyTrack.Tests/ 
└── StudyTrack.slnx
```
