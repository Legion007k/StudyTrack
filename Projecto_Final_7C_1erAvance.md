# StudyTrack - REST API Contract (PRD)

> Contrato del microservicio REST de StudyTrack. Los recursos son `Actividades` (actividades de estudio organizadas en árbol: actividad raíz con subactividades anidadas) y `Usuarios` (estudiantes registrados con los que se pueden compartir actividades).

## Overview
- **Service**: StudyTrack Management REST API
- **Language/Framework**: C# .NET 10.0 with Minimal APIs
- **Base URL**: `http://localhost:8080` (configurable vía `Urls` / `ASPNETCORE_HTTP_PORTS` y `Properties/launchSettings.json`)
- **Content-Type**: `application/json` (las respuestas de error usan `application/problem+json`)
- **ID Format**: Regex `[A-Za-z0-9\-]+` (se valida la cadena completa)
- **Usuario solicitante**: toda operación recibe `userId`. En `GET` y `DELETE` viaja en la query string (`?userId=`); en `POST` y `PUT` viaja en el cuerpo JSON. Los headers solo llevan metadatos: la API **no** lee `X-User-Id` ni ningún otro header para identificar al usuario.
- **Fechas**: instantes ISO 8601 **con zona horaria** (`Z` u offset, por ejemplo `2026-12-15T23:59:00Z` o `2026-12-15T23:59:00-08:00`). Una fecha sin zona se rechaza con `400`. La API guarda y devuelve siempre UTC (sufijo `Z`).
- **Documentación interactiva**: Swagger UI en `/swagger` y especificación OpenAPI en `/swagger/v1/swagger.json`. `GET /` redirige a `/swagger`.

---

## Architecture

### Layered Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                        API Layer (Minimal APIs)             │
│  • Endpoint definitions (MapGroup, MapGet, MapPost, etc.)   │
│  • Request/Response: entidades de dominio (sin DTOs)        │
│  • Validation (FluentValidation + endpoint filters)         │
│  • Error handling (GlobalExceptionHandler, ProblemDetails)  │
│  • Swagger / OpenAPI                                        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Delegate Layer                         │
│  • IActividadDelegate, IUsuarioDelegate                     │
│  • Business logic orchestration                             │
│  • Business rules (duplicados, fechas, jerarquía,           │
│    autorización por dueño / colaborador)                    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Repository Layer                         │
│  • IActividadRepository, IUsuarioRepository                 │
│  • Data access (EF Core + PostgreSQL / Npgsql)              │
│  • Query building (visibilidad por usuario, subárbol)       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Domain Models                          │
│  • Entities: Actividad, ActividadCompartida, Usuario        │
│  • Requests: CompartirActividadRequest                      │
│  • Reglas de dominio: profundidad máxima, completar/reabrir,│
│    eliminar lógicamente, compartir                          │
│  • Enums: TipoActividad (General, Examen, Entrega,          │
│    Presentacion)                                            │
└─────────────────────────────────────────────────────────────┘
```

Todas las capas viven en un solo proyecto (`StudyTrack.Api`), separadas por carpetas y espacios de nombres. Las dependencias entre capas se verifican con pruebas de arquitectura (NetArchTest).

### Key Patterns
| Pattern | Implementation |
|---------|----------------|
| **Minimal APIs** | `MapGroup("/actividades")` en `Routes/ActividadRoutes.cs` y `MapGroup("/usuarios")` en `Routes/UsuarioRoutes.cs`; `Program.cs` llama a `MapActividadRoutes()` y `MapUsuarioRoutes()` |
| **Delegate** | One per domain (`IActividadDelegate`, `IUsuarioDelegate`) - orchestrates repositories, contains business rules |
| **Repository** | One per aggregate root (`IActividadRepository`, `IUsuarioRepository`) - data access abstraction, testable via EF Core InMemory |
| **Dependency Injection** | Built-in DI container, scoped per request; registro con `AddApi()`, `AddDelegates()` y `AddRepositories()`. La hora actual se inyecta con `TimeProvider` (también para la fecha de eliminación) |
| **Validation** | FluentValidation sobre las entidades (`ActividadValidator`, `UsuarioValidator`, `CompartirActividadRequestValidator`), ejecutada por `ValidationFilter<T>`; el ID de ruta se valida con `IdFormatFilter` y el `userId` de la query con `UserIdQueryFilter` |
| **Serialization** | System.Text.Json with camelCase, enum as string (no se aceptan enums numéricos), `ReferenceHandler.IgnoreCycles`. Los colaboradores se aceptan como `"usuario-2"` o `{ "usuarioId": "usuario-2" }` (`ActividadCompartidaJsonConverter`) |
| **Database** | EF Core 10 + PostgreSQL (Npgsql) (`ApplicationDbContext`); fechas guardadas como `timestamp with time zone` (UTC) |
| **Identity** | Sin autenticación: `userId` es el identificador que envía el cliente. La tabla `Usuarios` registra a los estudiantes que pueden recibir actividades compartidas |
| **Soft delete** | `DELETE` marca `eliminada = true` en la actividad y su subárbol; un filtro global oculta las eliminadas |
| **Swagger** | Activo en `Development`; en otros entornos solo si `Swagger:Habilitado = true` (en el clúster: variable `Swagger__Habilitado=true`) |

---

## Endpoints

### Actividades

| Method  | Path | Usuario | Request Body | Response | Description |
|---------|------|---------|--------------|----------|-------------|
| `GET`   | `/actividades` | `?userId=` | - | `200` `Actividad[]` \| `400` | Actividades visibles para el usuario como árbol: propias, compartidas con él y las que cuelgan de una compartida. Una subactividad compartida cuyo padre no es visible aparece como raíz. Ordenadas por `fechaInicio` |
| `GET`   | `/actividades/calendario?desde=&hasta=` | `?userId=` | - | `200` `Actividad[]` \| `400` \| `422` | Actividades visibles cuyo periodo `[fechaInicio, fechaFin]` se traslapa con `[desde, hasta]`. `desde` y `hasta` deben traer zona horaria. Una hija cuyo padre también está en el rango aparece solo anidada |
| `GET`   | `/actividades/{actividadId}` | `?userId=` | - | `200` `Actividad` \| `400` \| `403` \| `404` | Actividad con sus subactividades. `403` si el usuario no es dueño ni colaborador (de ella o de un ancestro) |
| `POST`  | `/actividades` | `userId` en el body (dueño) | `Actividad` (crear) | `201` `Actividad` + `Location` \| `400` \| `415` \| `422` | Crea una actividad raíz o subactividad. El servidor fija `id`, `fechaInicio` y los demás campos del servidor |
| `PUT`   | `/actividades/{actividadId}` | `userId` en el body (solicitante) | `Actividad` (actualizar) | `200` `Actividad` \| `400` \| `403` \| `404` \| `415` \| `422` | Reemplaza los campos editables. Dueño y colaboradores pueden editar; solo el dueño puede enviar `colaboradores` |
| `DELETE`| `/actividades/{actividadId}` | `?userId=` (dueño) | - | `204` \| `400` \| `403` \| `404` | Elimina lógicamente la actividad y todas sus subactividades. Solo el dueño |
| `POST`  | `/actividades/{actividadId}/compartir` | `userId` en el body (dueño) | `CompartirActividadRequest` | `200` \| `400` \| `403` \| `404` \| `415` \| `422` | Comparte la actividad con un usuario registrado |

Orden de validación: `400` (formato del ID, `userId` faltante o inválido, cuerpo inválido) → `404` (no existe) → `403` (no autorizado) → `422` (regla de negocio).

### Usuarios

| Method  | Path | Request Body | Response | Description |
|---------|------|--------------|----------|-------------|
| `POST`  | `/usuarios` | `Usuario` | `201` `Usuario` + `Location` \| `400` \| `409` \| `415` | Registra un estudiante. `409` si el `id` ya existe |
| `GET`   | `/usuarios/{usuarioId}` | - | `200` `Usuario` \| `400` \| `404` | Obtiene un usuario registrado |

**Actividad (respuesta)**
```json
{
  "id": "string (GUID generado por el servidor)",
  "userId": "string (dueño)",
  "titulo": "string",
  "descripcion": "string | null",
  "fechaInicio": "2026-12-15T23:59:00Z",
  "fechaFin": "2026-12-15T23:59:00Z",
  "tipo": "General | Examen | Entrega | Presentacion",
  "completada": false,
  "fechaCompletada": "2026-12-15T23:59:00Z | null",
  "generadaAutomaticamente": false,
  "eliminada": false,
  "fechaEliminacion": "null",
  "actividadPadreId": "string | null",
  "actividadesHijas": "Actividad[]",
  "colaboradores": [ { "actividadId": "string", "usuarioId": "string", "fechaCompartida": "2026-12-15T23:59:00Z" } ]
}
```

**Actividad (crear, `POST /actividades`)**
```json
{
  "userId": "string (dueño, obligatorio)",
  "titulo": "string",
  "descripcion": "string (opcional)",
  "fechaFin": "2026-12-15T23:59:00Z (con zona horaria)",
  "tipo": "General | Examen | Entrega | Presentacion (opcional, por defecto General)",
  "actividadPadreId": "string | null",
  "colaboradores": ["usuario-2", { "usuarioId": "usuario-3" }]
}
```
El servidor ignora `id`, `fechaInicio`, `completada`, `fechaCompletada`, `generadaAutomaticamente`, `eliminada` y `fechaEliminacion` si el cliente los envía. `actividadesHijas` debe ir vacío: cada subactividad se crea con su propio `POST` indicando `actividadPadreId`.

**Actividad (actualizar, `PUT /actividades/{id}`)**
```json
{
  "userId": "string (solicitante, obligatorio)",
  "titulo": "string",
  "descripcion": "string | null",
  "fechaFin": "2026-12-15T23:59:00Z (con zona horaria)",
  "tipo": "General | Examen | Entrega | Presentacion (opcional, por defecto General)",
  "completada": false,
  "actividadPadreId": "string | null",
  "colaboradores": "(opcional) lista completa de colaboradores; si se omite no cambia. Solo el dueño"
}
```

**CompartirActividadRequest**
```json
{
  "userId": "string (dueño que comparte)",
  "usuarioId": "string (destinatario registrado)"
}
```

**Usuario**
```json
{
  "id": "string",
  "nombre": "string",
  "email": "string"
}
```

---

## Error Responses

| Code | Scenario |
|------|----------|
| `400` | Formato de ID inválido en la ruta (ej: `id_invalido`, `id.con.puntos`), error en `actividadId`. `userId` faltante o con formato inválido (query o body). JSON malformado o con tipos incorrectos (fecha inválida, `tipo` desconocido o numérico, colaborador que no es texto ni objeto), error en `body`. Fallo de validación: `userId`, `titulo` o `fechaFin` vacíos; `userId` o `actividadPadreId` con caracteres no permitidos (ej: `usuario_1`, `padre#1`); `userId` > 64, `titulo` > 200 o `descripcion` > 2000 caracteres; **`fechaFin`, `desde` o `hasta` sin zona horaria**; `actividadesHijas` no vacío al crear. Destinatario o colaborador no registrado en `Usuarios` |
| `403` | El usuario no es dueño ni colaborador (GET por id, PUT); un colaborador intenta cambiar `colaboradores`; alguien distinto del dueño intenta eliminar o compartir |
| `404` | Actividad o usuario no encontrado por ID |
| `409` | `POST /usuarios` con un `id` ya registrado |
| `415` | Content-Type del cuerpo no es `application/json` (POST, PUT) |
| `422` | Violación de regla de negocio: `fechaFin` anterior a `fechaInicio` (al crear equivale a una fecha en el pasado), actividad duplicada (mismo `userId` + `titulo` + `fechaFin`), `actividadPadreId` inexistente, `actividadPadreId` pertenece a otro `userId`, `actividadPadreId` es la propia actividad o una de sus subactividades, se supera la profundidad máxima del tipo de la raíz (General 1 nivel, Examen 2, Presentacion 2, Entrega 3), cambiar el tipo de una raíz deja subactividades fuera del límite, compartir con el propio dueño, rango de calendario invertido |
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
    "fechaFin": ["La fecha debe incluir zona horaria (Z u offset), por ejemplo 2026-09-20T10:00:00Z o 2026-09-20T10:00:00-07:00."]
  }
}
```

**403 - Forbidden**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403,
  "detail": "Solo el propietario puede eliminar la actividad."
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

**409 - Conflict**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "Ya existe un usuario con id 'estudiante-1'."
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
    "DefaultConnection": "Host=localhost;Port=5432;Database=studytrack;Username=postgres;Password="
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Swagger": {
    "Habilitado": false
  }
}
```
`Swagger:Habilitado` es opcional: en `Development` Swagger siempre está activo. En el clúster (Production) se activa con la variable de entorno `Swagger__Habilitado=true` (`kubernetes/api.yml`).

### Migraciones
Las migraciones se aplican con `dotnet ef database update`. La migración `20261115231457_MigrarFechasAUtc` convierte las columnas de fecha a `timestamp with time zone` interpretando los valores existentes como hora de `America/Tijuana` (`USING ... AT TIME ZONE 'America/Tijuana'`), que es como los guardaba la versión anterior.

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
| Carga | Load | Locust (`load_test/locustfile.py`) | Un bot por estudiante que usa todas las funciones |

---

#### Required Test Cases per Endpoint

**Actividades Routes** (`/actividades`)
| Test Case | Description |
|-----------|-------------|
| `GET /actividades` returns 200 with empty array | No hay actividades visibles para el usuario |
| `GET /actividades` returns 200 with items | Las raíces vienen ordenadas y con sus hijas anidadas |
| `GET /actividades` returns 400 | Falta `userId`, viene vacío o con formato inválido; el header `X-User-Id` no lo sustituye |
| `GET /actividades` filtra por usuario | Devuelve propias y compartidas, no las de otros usuarios; incluye el subárbol de una raíz compartida; una subactividad compartida sin su padre aparece como raíz |
| `GET /actividades/calendario` returns 400 | Falta `desde`, `hasta` o `userId`; fecha sin zona horaria o inválida |
| `GET /actividades/calendario` returns 422 | `desde` posterior a `hasta` |
| `GET /actividades/calendario` returns 200 | Solo actividades visibles en el rango; hija con padre en rango solo anidada; offsets interpretados como instantes UTC |
| `GET /actividades/{id}` returns 200 with item | ID válido existe y el usuario es dueño o colaborador |
| `GET /actividades/{id}` returns 403 | Usuario sin acceso |
| `GET /actividades/{id}` returns 404 | ID válido pero no existe |
| `GET /actividades/{id}` returns 400 | ID con formato inválido (ej: `id_con_guion_bajo`, `id!`, espacios, `ñ`) o sin `userId` |
| `POST /actividades` returns 201 + Location | Devuelve la actividad creada con ID GUID y `fechaInicio` = hora de creación |
| `POST /actividades` ignora los campos del servidor | `id`, `fechaInicio`, `completada`, `eliminada`, `generadaAutomaticamente`, etc. enviados por el cliente no se guardan |
| `POST /actividades` returns 201 (subactividad) | Con `actividadPadreId`; queda anidada en el padre |
| `POST /actividades` returns 201 (colaboradores) | Colaboradores como texto u objeto; `actividadId` y `fechaCompartida` los fija el servidor |
| `POST /actividades` returns 400 | Falta `userId`, `titulo` o `fechaFin`; `titulo` en blanco; `userId` = `usuario_1`; `actividadPadreId` = `padre#1`; `fechaFin` sin zona horaria; `titulo` > 200 caracteres; `actividadesHijas` no vacío; colaborador vacío, numérico o no registrado |
| `POST /actividades` returns 415 | Content-Type no es `application/json` |
| `POST /actividades` returns 422 | `fechaFin` anterior a `fechaInicio`; duplicado; `actividadPadreId` no existe; se supera la profundidad máxima |
| `PUT /actividades/{id}` returns 200 | Marcar `completada: true` (fija `fechaCompletada`); colaborador edita sin enviar `colaboradores`; dueño reemplaza `colaboradores` |
| `PUT /actividades/{id}` returns 400 | `titulo` vacío; sin `userId` (no modifica nada) |
| `PUT /actividades/{id}` returns 403 | Usuario sin acceso; colaborador que envía `colaboradores` |
| `PUT /actividades/{id}` returns 404 | ID válido pero no existe |
| `PUT /actividades/{id}` returns 415 | Content-Type no es `application/json` |
| `PUT /actividades/{id}` returns 422 | `fechaFin` anterior a `fechaInicio`; `actividadPadreId` es una de sus propias subactividades |
| `DELETE /actividades/{id}` returns 204 | El dueño elimina; responde sin cuerpo y elimina también subactividades |
| `DELETE /actividades/{id}` returns 400 | Sin `?userId=` (el usuario en header o body no cuenta), incluso si el ID no existe |
| `DELETE /actividades/{id}` returns 403 | Usuario ajeno o colaborador |
| `DELETE /actividades/{id}` returns 404 | ID válido pero no existe |
| `POST /actividades/{id}/compartir` returns 200 | El dueño comparte con un usuario registrado |
| `POST /actividades/{id}/compartir` returns 400 | Sin `userId` en el body; destinatario no registrado |
| `POST /actividades/{id}/compartir` returns 403 | Quien comparte no es el dueño |

**Usuarios Routes** (`/usuarios`)
| Test Case | Description |
|-----------|-------------|
| `POST /usuarios` returns 201 + Location | Registra y se puede consultar con `GET /usuarios/{id}` |
| `POST /usuarios` returns 409 | `id` repetido |
| `POST /usuarios` returns 400 | Falta `id` o `nombre`, `id` con formato inválido, `email` inválido |
| `GET /usuarios/{id}` returns 404 | No existe |

#### Cross-Cutting Route Tests
| Test Case | Description |
|-----------|-------------|
| Request validation | FluentValidation errors return 400 with ProblemDetails (`type`, `title`, `status`, `errors`) y no llegan al delegate |
| Malformed body | JSON malformado, fecha inválida o `tipo` desconocido/numérico return 400 con error en `body` |
| Content-Type | Respuestas exitosas son `application/json`; errores son `application/problem+json`; POST/PUT con otro Content-Type return 415 |
| ID format validation | GET, PUT y DELETE rechazan con 400 los `{id}` que no cumplen la regex (incluido `%0A`); un ID válido inexistente da 404 |
| Error handling | 404, 422 y 500 con formato ProblemDetails; el 500 no expone detalles internos |
| Concurrency | 25 POST en paralelo crean todas las actividades; 10 PUT en paralelo dejan un estado completo de una de las solicitudes |
| Swagger | `/swagger/v1/swagger.json` y `/swagger/index.html` responden 200 en Development; `GET /` redirige; en Production solo con `Swagger:Habilitado`; la especificación no documenta `X-User-Id` |

---

### Delegate Tests (Unit Tests)

#### Setup Pattern
```csharp
public class ActividadDelegateTests
{
    private static readonly DateTime Ahora = ActividadBuilder.FechaBase; // Kind = Utc

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
| `GetByIdAsync` | Returns the entity when found, null when not found, subactividades anidadas en orden, 403 a un usuario ajeno, acceso del colaborador de la actividad o de un ancestro |
| `GetAllAsync` | Returns empty list, raíces con sus hijas anidadas, propaga el usuario al repositorio, subactividad compartida sin su padre como raíz |
| `GetCalendarioAsync` | Rango invertido, rango válido ordenado, propaga el usuario, no duplica hijas, hija con padre fuera de rango como raíz |
| `CreateAsync` | Generates ID, saves via repo, `fechaInicio` = momento de creación (UTC), ignora los campos del servidor, validates unique constraint, rechaza `fechaFin` anterior a la creación, padre válido / inexistente / de otro usuario, profundidad máxima por tipo, colaboradores válidos / inexistentes / repetidos / el propio creador |
| `UpdateAsync` | Updates all fields, throws if not found, 403 a un usuario ajeno sin guardar, colaborador edita sin tocar colaboradores, colaborador no cambia colaboradores, dueño reemplaza colaboradores, completar / reabrir, `fechaFin` anterior a `fechaInicio`, duplicado excluyéndose a sí misma, padre = misma actividad / descendiente, mover subárbol o cambiar tipo fuera del límite, quitar el padre |
| `CompartirAsync` | Sin usuario (400), no dueño (403), consigo mismo (422), destinatario inexistente (400), dueño comparte |
| `DeleteAsync` | Deletes via repo entregando el subárbol completo con la hora del `TimeProvider`, throws if not found, sin usuario (400), colaborador (403) |
| Todos | `userId` vacío o en blanco lanza `BadRequestException` |

---

### Test Data Builders
```csharp
// Fluent builder con valores fijos por defecto para pruebas deterministas
public class ActividadBuilder
{
    public static readonly DateTime FechaBase = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

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

    // WithId, WithUserId, WithTitulo, WithDescripcion, WithFechaInicio, WithFechaFin,
    // WithTipo, WithGeneradaAutomaticamente, WithCompletada, WithPadre ...
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
| **Carga** | `locust -f load_test/locustfile.py --host http://<api>`; cada bot se registra en `/usuarios` y usa todos los endpoints con el mismo contrato que la API |

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
    │   ├── UsuarioRoutesTests.cs
    │   └── StudyTrackApiTests.cs (base class)
    └── StudyTrack.Tests.csproj
```

---

## Project Structure
**Estructura actual**
```
StudyTrack/
├── src/
│ └── StudyTrack.Api/
│   ├── Program.cs # AddApi, AddDelegates, Swagger, MapActividadRoutes, MapUsuarioRoutes
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Properties/
│   │ └── launchSettings.json
│   ├── Routes/
│   │ ├── ActividadRoutes.cs
│   │ └── UsuarioRoutes.cs
│   ├── Validators/
│   │ ├── ActividadRules.cs
│   │ ├── ActividadValidator.cs
│   │ ├── CompartirActividadRequestValidator.cs
│   │ ├── UsuarioValidator.cs
│   │ └── IdFormat.cs
│   ├── Extensions/
│   │ ├── ActividadCompartidaJsonConverter.cs
│   │ ├── ApiApplicationBuilderExtensions.cs
│   │ ├── ApiServiceCollectionExtensions.cs
│   │ ├── FechaConsulta.cs
│   │ ├── GlobalExceptionHandler.cs
│   │ ├── IdFormatFilter.cs
│   │ ├── ProblemDetailsResponses.cs
│   │ ├── UserIdQueryFilter.cs
│   │ └── ValidationFilter.cs
│   ├── Delegates/
│   │ ├── IActividadDelegate.cs
│   │ ├── ActividadDelegate.cs
│   │ ├── IUsuarioDelegate.cs
│   │ ├── UsuarioDelegate.cs
│   │ ├── DelegateServiceCollectionExtensions.cs
│   │ └── Exceptions/
│   │   ├── BadRequestException.cs
│   │   ├── BusinessRuleException.cs
│   │   ├── ConflictException.cs
│   │   ├── ForbiddenException.cs
│   │   └── NotFoundException.cs
│   ├── Repositories/
│   │ ├── IActividadRepository.cs
│   │ ├── ActividadRepository.cs
│   │ ├── IUsuarioRepository.cs
│   │ ├── UsuarioRepository.cs
│   │ ├── ApplicationDbContext.cs
│   │ ├── RepositoryServiceCollectionExtensions.cs
│   │ └── Migrations/
│   └── Domain/
│     ├── Actividad.cs
│     ├── ActividadCompartida.cs
│     ├── CompartirActividadRequest.cs
│     └── Usuario.cs
├── tests/
│ └── StudyTrack.Tests/
├── load_test/
│ ├── locustfile.py
│ └── requirements.txt
├── kubernetes/
└── StudyTrack.slnx
```
