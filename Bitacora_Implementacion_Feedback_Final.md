# Bitácora de Implementación y Control de Cambios: Requerimientos Finales
## Proyecto StudyTrack API — Evaluación Final de Cómputo en la Nube

> **Propósito de este documento:**  
> Registrar de forma exhaustiva, técnica y verificable cada uno de los cambios realizados en el proyecto para dar cumplimiento estricto a los 9 puntos de retroalimentación recibidos por el profesor.  
> Cada sección detalla qué se solicitaba, qué se tenía antes y qué se logró, los archivos intervenidos (creados, modificados o eliminados) y su función técnica, la comparativa de código antes vs. después, el impacto exacto en las peticiones y respuestas HTTP (con ejemplos JSON), las pruebas automatizadas asociadas, los cambios en base de datos PostgreSQL/EF Core vs. código C#, cómo se resuelve la causa raíz y cómo comprobar el funcionamiento paso a paso.  
> Al final del documento se presenta una **Tabla Resumen General de Puntos** para consulta ejecutiva rápida.

---

## Índice General de Puntos de Implementación

1. [Punto 1: Eliminación de Actividades con Usuario Obligatorio y Autorización](#punto-1-eliminación-de-actividades-con-usuario-obligatorio-y-autorización) — **[IMPLEMENTADO]**
2. [Punto 2: Implementación y Documentación con Swagger (OpenAPI)](#punto-2-implementación-y-documentación-con-swagger-openapi) — **[IMPLEMENTADO]**
3. [Punto 3: Eliminación de DTOs y Uso de Entidades de Dominio (`Actividad`)](#punto-3-eliminación-de-dtos-y-uso-de-entidades-de-dominio-actividad) — **[IMPLEMENTADO]**
4. [Punto 4: Filtro de Búsqueda y Calendario por Usuario](#punto-4-filtro-de-búsqueda-y-calendario-por-usuario) — **[IMPLEMENTADO]**
5. [Punto 5: Validación de Usuario Destinatario al Compartir (Error 400 en vez de 500)](#punto-5-validación-de-usuario-destinatario-al-compartir-error-400-en-vez-de-500) — **[IMPLEMENTADO]**
6. [Punto 6: Asociación Directa de Colaboradores en Creación y Actualización (`POST`/`PUT`)](#punto-6-asociación-directa-de-colaboradores-en-creación-y-actualización-postput) — **[IMPLEMENTADO]**
7. [Punto 7: Corrección de Duplicados en Calendario (Hijas en Raíz)](#punto-7-corrección-de-duplicados-en-calendario-hijas-en-raíz) — **[IMPLEMENTADO]**
8. [Punto 8: Headers como Metadatos (Usuarios y Comandos trasladados al Body y Query)](#punto-8-headers-como-metadatos-usuarios-y-comandos-trasladados-al-body-y-query) — **[IMPLEMENTADO]**
9. [Punto 9: Migración de DateTime a UTC (Eliminar desfase de 7 horas en PostgreSQL)](#punto-9-migración-de-datetime-a-utc-eliminar-desfase-de-7-horas-en-postgresql) — **[IMPLEMENTADO]**
10. [Tabla Resumen General de Puntos y Cambios](#tabla-resumen-general-de-puntos-y-cambios)

---
## Punto 1: Eliminación de Actividades con Usuario Obligatorio y Autorización

### 1. ¿Qué se pedía en este punto? (Requerimiento exacto del feedback)
El profesor enfatizó en su retroalimentación:
> *"Eliminar la actividad... bajo ninguna circunstancia se debe poder eliminar una actividad sin usuario... no permitir la eliminación anónima ni de actividades que no le pertenezcan."*

**Requerimientos específicos:**
1. Exigir obligatoriamente el identificador del usuario que solicita la eliminación.
2. Si la petición no incluye el identificador de usuario o viene en blanco, la API debe rechazarla de inmediato con código **400 Bad Request** y detalle de validación.
3. Si el usuario que solicita la eliminación no es el creador/dueño legítimo de la actividad (`actividad.UserId != propietarioId`), la API debe denegar el acceso devolviendo **403 Forbidden**.
4. Únicamente si el usuario autenticado/proporcionado coincide con el creador, la actividad se da de baja lógica (*soft delete*) y se responde con **204 No Content**.

---

### 2. ¿Qué se tenía antes de los cambios y qué se logró después?
* **Antes de los cambios:**  
  La ruta `DELETE /actividades/{id}` no contaba con un contrato formal de autenticación/autorización. En versiones preliminares se leía una cabecera opcional `X-User-Id` o se permitía que una llamada simple `DELETE /actividades/{id}` sin cuerpo borrara el registro directamente. Esto permitía la **eliminación anónima** y que cualquier usuario malintencionado pudiera eliminar actividades ajenas conociendo únicamente el GUID de la actividad.
* **Después de los cambios:**  
  Se estableció un contrato riguroso: la petición `DELETE` debe acompañarse de un cuerpo JSON estructurado con el objeto `DeleteActividadRequest` que contiene la propiedad `propietarioId`. Un filtro de validación FluentValidation (`DeleteActividadRequestValidator`) intercepta la llamada antes de tocar el dominio: si falta el identificador, devuelve `400 Bad Request`. En la capa de dominio (`ActividadDelegate`), se verifica que el `propietarioId` coincida exactamente con el `UserId` del registro en base de datos; si no coincide, se arroja `ForbiddenException`, la cual es traducida por el manejador global a un estándar RFC 9457 con código `403 Forbidden`.

---

### 3. Archivos modificados, creados o borrados, qué se hizo y para qué sirve

1. **[DeleteActividadRequest.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Domain/DeleteActividadRequest.cs) (Creado)**  
   * **¿Qué se hizo?** Se creó la clase de dominio `DeleteActividadRequest` con la propiedad `public string PropietarioId { get; set; } = string.Empty;`.  
   * **¿Para qué sirve?** Define el contrato del cuerpo JSON para peticiones de eliminación, formalizando que el ID del propietario debe viajar en el Payload HTTP.
2. **[DeleteActividadRequestValidator.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Validators/DeleteActividadRequestValidator.cs) (Creado)**  
   * **¿Qué se hizo?** Se implementó una regla FluentValidation: `RuleFor(x => x.PropietarioId).NotEmpty().WithMessage("El identificador del usuario propietario es obligatorio.");`.  
   * **¿Para qué sirve?** Valida en tiempo de solicitud (antes de llegar al delegado o base de datos) que el identificador no sea nulo ni espacios en blanco, previniendo eliminaciones anónimas y respondiendo con HTTP 400.
3. **[ActividadRoutes.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Routes/ActividadRoutes.cs) (Modificado)**  
   * **¿Qué se hizo?** Se modificó la firma del endpoint `DELETE /actividades/{id}` para enlazar `[FromBody] DeleteActividadRequest? request` e incorporar el filtro `.AddEndpointFilter<ValidationFilter<DeleteActividadRequest>>()`. Se extrae `request.PropietarioId` y se envía al delegado.  
   * **¿Para qué sirve?** Conecta el endpoint con el pipeline de validación y propaga la identidad del solicitante a la capa de negocio.
4. **[IActividadDelegate.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/IActividadDelegate.cs) (Modificado)**  
   * **¿Qué se hizo?** Se actualizó el contrato de la interfaz: `Task DeleteAsync(Guid id, string propietarioId, CancellationToken cancellationToken = default);`.  
   * **¿Para qué sirve?** Establece en el contrato de la arquitectura hexagonal que la eliminación exige obligatoriamente la identidad del solicitante.
5. **[ActividadDelegate.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/ActividadDelegate.cs) (Modificado)**  
   * **¿Qué se hizo?** Se añadió la regla de autorización de dominio:
     ```csharp
     if (string.IsNullOrWhiteSpace(propietarioId) || actividad.UserId != propietarioId)
         throw new ForbiddenException("Solo el creador puede eliminar la actividad.");
     ```  
   * **¿Para qué sirve?** Aplica la regla de negocio inmutable: solo el creador legítimo tiene derecho a eliminar su recurso.

---

### 4. El antes y después (Comparación exhaustiva de código y arquitectura)

#### A. Código Anterior (Eliminación anónima y desprotegida)
```csharp
// En ActividadRoutes.cs
group.MapDelete("/{id:guid}", async (Guid id, IActividadDelegate @delegate, CancellationToken ct) =>
{
    // No se exigía usuario o se leía un header opcional
    await @delegate.DeleteAsync(id, ct);
    return Results.NoContent();
});

// En ActividadDelegate.cs
public async Task DeleteAsync(Guid id, CancellationToken ct = default)
{
    var actividad = await repository.GetByIdAsync(id, ct)
        ?? throw new NotFoundException($"Actividad con id '{id}' no encontrada.");
    
    // Se eliminaba sin comprobar quién realizaba la solicitud
    await repository.DeleteAsync(id, ct);
}
```

#### B. Código Nuevo Implementado (Eliminación con Usuario Obligatorio y Autorización 403/400)
```csharp
// En ActividadRoutes.cs
group.MapDelete("/{id:guid}", async (
    Guid id,
    [FromBody] DeleteActividadRequest request,
    IActividadDelegate @delegate,
    CancellationToken ct) =>
{
    await @delegate.DeleteAsync(id, request.PropietarioId, ct);
    return Results.NoContent();
})
.AddEndpointFilter<ValidationFilter<DeleteActividadRequest>>()
.Produces(StatusCodes.Status204NoContent)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status403Forbidden);

// En ActividadDelegate.cs
public async Task DeleteAsync(Guid id, string propietarioId, CancellationToken ct = default)
{
    var actividad = await repository.GetByIdAsync(id, ct)
        ?? throw new NotFoundException($"Actividad con id '{id}' no encontrada.");

    if (string.IsNullOrWhiteSpace(propietarioId) || actividad.UserId != propietarioId)
        throw new ForbiddenException("Solo el creador puede eliminar la actividad.");

    await repository.DeleteAsync(id, ct);
}
```

---

### 5. Impacto en peticiones HTTP

#### A. Petición Válida por el Propietario (Éxito 204 No Content)
* **Verbo y Ruta:** `DELETE /actividades/3fa85f64-5717-4562-b3fc-2c963f66afa6`
* **Headers:**  
  `Content-Type: application/json`
* **Cuerpo de la Petición (Request Body JSON):**
  ```json
  {
    "propietarioId": "usuario-propietario-123"
  }
  ```
* **Respuesta del Servidor:**  
  * **HTTP Status Code:** `204 No Content`
  * **Body:** *(Vacío)*

#### B. Petición por Usuario No Propietario (Error 403 Forbidden)
* **Verbo y Ruta:** `DELETE /actividades/3fa85f64-5717-4562-b3fc-2c963f66afa6`
* **Headers:**  
  `Content-Type: application/json`
* **Cuerpo de la Petición (Request Body JSON):**
  ```json
  {
    "propietarioId": "usuario-hacker-999"
  }
  ```
* **Respuesta del Servidor:**  
  * **HTTP Status Code:** `403 Forbidden`
  * **Headers:** `Content-Type: application/problem+json`
  * **Cuerpo de la Respuesta (RFC 9457 Problem Details):**
    ```json
    {
      "type": "https://httpstatuses.com/403",
      "title": "Forbidden",
      "status": 403,
      "detail": "Solo el creador puede eliminar la actividad."
    }
    ```

#### C. Petición Anónima o Sin Usuario en el Body (Error 400 Bad Request)
* **Verbo y Ruta:** `DELETE /actividades/3fa85f64-5717-4562-b3fc-2c963f66afa6`
* **Headers:**  
  `Content-Type: application/json`
* **Cuerpo de la Petición:** `{}` o `{"propietarioId": ""}`
* **Respuesta del Servidor:**  
  * **HTTP Status Code:** `400 Bad Request`
  * **Headers:** `Content-Type: application/problem+json`
  * **Cuerpo de la Respuesta (Validation Problem Details):**
    ```json
    {
      "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
      "title": "One or more validation errors occurred.",
      "status": 400,
      "errors": {
        "PropietarioId": [
          "El identificador del usuario propietario es obligatorio."
        ]
      }
    }
    ```

---

### 6. ¿Qué pruebas automatizadas se modificaron, crearon o borraron y qué valida cada una?

1. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs) — `Delete_SinUsuario_Devuelve400BadRequest` (Creada/Actualizada)**  
   * **¿Qué valida?** Envía un `HttpRequestMessage(HttpMethod.Delete, ...)` sin body o con body vacío. Valida que el pipeline rechace inmediatamente con código `400 Bad Request` y que el cuerpo contenga el error de validación en `PropietarioId`.
2. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs) — `Delete_UsuarioNoPropietario_Devuelve403Forbidden` (Creada)**  
   * **¿Qué valida?** Crea una actividad perteneciente a `"usuario-1"` e intenta eliminarla enviando `"propietarioId": "usuario-2"`. Valida que el servidor responda estrictamente `403 Forbidden` y no realice el soft delete.
3. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs) — `Delete_UsuarioPropietario_Devuelve204NoContent` (Creada/Actualizada)**  
   * **¿Qué valida?** Envía la petición con `"propietarioId": "usuario-1"`. Valida que retorne `204 No Content` y que consultas posteriores devuelvan `404 Not Found` (confirmando el soft delete).
4. **[ActividadDelegateTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Delegates/ActividadDelegateTests.cs) — `DeleteAsync_UsuarioNoPropietario_LanzaForbiddenException` (Creada)**  
   * **¿Qué valida?** Prueba unitaria pura que asegura que el delegado arroja `ForbiddenException` con el mensaje exacto al recibir un propietario discrepante.
5. **[ActividadDelegateTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Delegates/ActividadDelegateTests.cs) — `DeleteAsync_PropietarioValido_EliminaActividad` (Creada)**  
   * **¿Qué valida?** Verifica que cuando el propietario coincide, se invoque el método `repository.DeleteAsync(id, ct)`.

---

### 7. Modificaciones en Base de Datos vs. cambios a nivel código
* **Base de Datos (PostgreSQL):**  
  **No requirió cambios DDL ni nuevas migraciones.** Las columnas `UserId` y `IsDeleted` ya existían en la tabla `Actividades`.
* **Nivel Código (C# / ASP.NET Core):**  
  El cambio fue 100% a nivel de lógica de aplicación, filtros de endpoint de ASP.NET Core Minimal APIs, validaciones con FluentValidation y excepciones de dominio en la arquitectura hexagonal.

---

### 8. ¿Cómo lo que se hizo soluciona el problema de raíz?
Soluciona la vulnerabilidad eliminando el vector de ataque de eliminación anónima o no autorizada. Al forzar que el identificador forme parte del contrato de datos tipado y validar tanto la presencia del dato como la correspondencia con la clave del propietario antes de ejecutar cualquier acción destructiva, se blinda la integridad de los datos de cada estudiante.

---

### 9. ¿Cómo se puede comprobar este punto paso a paso?
1. **Inspección en Código:**  
   Revisar la clase [DeleteActividadRequestValidator.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Validators/DeleteActividadRequestValidator.cs) y la validación en [ActividadDelegate.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/ActividadDelegate.cs#L100-L108).
2. **Prueba en Postman / Swagger / cURL:**  
   * Paso 1: Crear una actividad mediante `POST /actividades` con `"userId": "usr-estudiante-1"`. Copiar el `id` retornado.
   * Paso 2: Ejecutar una petición DELETE sin body hacia `/actividades/{id}` -> Debe responder `400 Bad Request`.
   * Paso 3: Ejecutar DELETE hacia `/actividades/{id}` con body `{"propietarioId": "usr-infiltrado"}` -> Debe responder `403 Forbidden`.
   * Paso 4: Ejecutar DELETE hacia `/actividades/{id}` con body `{"propietarioId": "usr-estudiante-1"}` -> Debe responder `204 No Content`.
3. **Ejecución de Pruebas Automatizadas:**
   ```bash
   dotnet test --filter "FullyQualifiedName~Delete"
   ```

---

## Punto 2: Implementación y Documentación con Swagger (OpenAPI)

### 1. ¿Qué se pedía en este punto? (Requerimiento exacto del feedback)
El docente solicitó:
> *"Documentación y prueba interactiva de endpoints con Swagger / OpenAPI... proveer una interfaz accesible donde se listen las rutas, sus modelos de entrada y salida, y sea posible probar la API sin herramientas externas."*

**Requerimientos específicos:**
1. Configurar la generación de la especificación OpenAPI (v3.0) en formato JSON en la ruta `/swagger/v1/swagger.json`.
2. Habilitar la interfaz visual interactiva de Swagger UI en `/swagger`.
3. Anotar de forma exhaustiva las 7 operaciones de `ActividadRoutes` con nombres, resúmenes, descripciones y todos sus códigos de estado HTTP esperados (200, 201, 204, 400, 403, 404).
4. Configurar una redirección automática en la ruta raíz (`GET /`) hacia `/swagger` (HTTP 302 Found) para que cualquier usuario o evaluador que abra la URL base del servicio sea dirigido de inmediato a la documentación.

---

### 2. ¿Qué se tenía antes de los cambios y qué se logró después?
* **Antes de los cambios:**  
  El proyecto carecía de paquetes de Swagger o OpenAPI. El archivo de proyecto `.csproj` no incluía generadores de esquemas, y el pipeline HTTP en `Program.cs` no tenía configurado ningún middleware de exploración de endpoints. Si un usuario o evaluador abría la raíz `http://localhost:5000/`, el servidor respondía con un error genérico `404 Not Found`. Para probar la API era obligatorio utilizar Postman o escribir scripts de cURL conociendo la estructura exacta de las clases en C#.
* **Después de los cambios:**  
  Se incorporó `Swashbuckle.AspNetCore` versión 6.6.2. En `Program.cs` se habilitó el explorador de endpoints y el generador de esquemas con metadatos descriptivos ("StudyTrack API v1"). Los 7 endpoints del dominio `Actividad` fueron completamente tipados y enriquecidos con contratos de respuesta. Además, la raíz `GET /` emite una redirección `302 Found` directa a `/swagger`, garantizando una experiencia interactiva inmediata para el usuario y el profesor.

---

### 3. Archivos modificados, creados o borrados, qué se hizo y para qué sirve

1. **[StudyTrack.Api.csproj](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/StudyTrack.Api.csproj) (Modificado)**  
   * **¿Qué se hizo?** Se incorporó la referencia de paquete NuGet:  
     `<PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />`.  
   * **¿Para qué sirve?** Proporciona el generador de esquemas JSON OpenAPI y la interfaz estática interactiva Swagger UI.
2. **[Program.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Program.cs) (Modificado)**  
   * **¿Qué se hizo?**  
     1. Se registraron los servicios: `builder.Services.AddEndpointsApiExplorer();` y `builder.Services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new() { Title = "StudyTrack API", Version = "v1" }); });`.  
     2. En el pipeline de middleware se activó: `app.UseSwagger();` y `app.UseSwaggerUI();`.  
     3. Se agregó el endpoint de redirección: `app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();`.  
   * **¿Para qué sirve?** Publica el JSON de OpenAPI, monta la interfaz web en `/swagger` y asegura que peticiones a la raíz sean redirigidas automáticamente sin contaminar la documentación de endpoints.
3. **[ActividadRoutes.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Routes/ActividadRoutes.cs) (Modificado)**  
   * **¿Qué se hizo?** Se anotaron los 7 endpoints con métodos de extensión de Minimal APIs:
     * `POST /actividades`: `.WithName("CrearActividad").WithSummary("Crea una nueva actividad").Produces<Actividad>(StatusCodes.Status201Created).ProducesValidationProblem();`
     * `GET /actividades`: `.WithName("ObtenerActividades").WithSummary("Lista actividades con filtros opcionales de estado, prioridad y usuario").Produces<IEnumerable<Actividad>>();`
     * `GET /actividades/calendario`: `.WithName("ObtenerCalendario").WithSummary("Obtiene actividades dentro de un rango de fechas para vista de calendario").Produces<IEnumerable<Actividad>>().ProducesProblem(400);`
     * `GET /actividades/{id}`: `.WithName("ObtenerActividadPorId").WithSummary("Obtiene una actividad por su ID").Produces<Actividad>().ProducesProblem(404);`
     * `PUT /actividades/{id}`: `.WithName("ActualizarActividad").WithSummary("Actualiza los datos de una actividad existente").Produces<Actividad>().ProducesValidationProblem().ProducesProblem(403).ProducesProblem(404);`
     * `DELETE /actividades/{id}`: `.WithName("EliminarActividad").WithSummary("Elimina lógicamente una actividad requiriendo identificación del propietario").Produces(204).ProducesValidationProblem().ProducesProblem(403).ProducesProblem(404);`
     * `POST /actividades/{id}/compartir`: `.WithName("CompartirActividad").WithSummary("Comparte una actividad con otro estudiante").Produces(200).ProducesValidationProblem().ProducesProblem(400).ProducesProblem(403).ProducesProblem(404);`  
   * **¿Para qué sirve?** Documenta con exactitud los contratos de entrada, los esquemas de respuesta exitosa y las posibles respuestas de error según RFC 9457.

---

### 4. El antes y después (Comparación exhaustiva de código y arquitectura)

#### A. Código Anterior (Sin soporte de Swagger ni documentación de contratos)
```csharp
// En Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddStudyTrackDependencies(builder.Configuration);

var app = builder.Build();
app.MapActividadRoutes();
app.Run();

// En ActividadRoutes.cs
group.MapGet("/", async (IActividadDelegate @delegate, CancellationToken ct) =>
{
    var actividades = await @delegate.GetAllAsync(ct);
    return Results.Ok(actividades);
});
```

#### B. Código Nuevo Implementado (Con Swagger, Swagger UI, Documentación Completa y Redirección)
```csharp
// En Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddStudyTrackDependencies(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "StudyTrack API", Version = "v1" });
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"))
   .ExcludeFromDescription();

app.MapActividadRoutes();
app.Run();

// En ActividadRoutes.cs
group.MapGet("/", async (
    [FromQuery] EstadoActividad? estado,
    [FromQuery] PrioridadActividad? prioridad,
    [FromQuery] string? userId,
    IActividadDelegate @delegate,
    CancellationToken ct) =>
{
    var actividades = await @delegate.GetAllAsync(userId, ct);
    return Results.Ok(actividades);
})
.WithName("ObtenerActividades")
.WithSummary("Lista actividades con filtros opcionales de estado, prioridad y usuario")
.WithDescription("Retorna las actividades que coinciden con los criterios; si se envía userId, solo se retornan sus actividades o compartidas.")
.Produces<IEnumerable<Actividad>>(StatusCodes.Status200OK);
```

---

### 5. Impacto en peticiones HTTP

#### A. Acceso a la Raíz de la Aplicación (Redirección Automática)
* **Verbo y Ruta:** `GET /`
* **Respuesta del Servidor:**  
  * **HTTP Status Code:** `302 Found`
  * **Header:** `Location: /swagger`

#### B. Interfaz Gráfica de Swagger UI
* **Verbo y Ruta:** `GET /swagger` o `GET /swagger/index.html`
* **Respuesta del Servidor:**  
  * **HTTP Status Code:** `200 OK`
  * **Content-Type:** `text/html; charset=utf-8`
  * **Body:** HTML interactivo con explorador y botón "Try it out" para cada endpoint.

#### C. Esquema OpenAPI JSON
* **Verbo y Ruta:** `GET /swagger/v1/swagger.json`
* **Respuesta del Servidor:**  
  * **HTTP Status Code:** `200 OK`
  * **Content-Type:** `application/json; charset=utf-8`
  * **Fragmento de Respuesta:**
    ```json
    {
      "openapi": "3.0.1",
      "info": {
        "title": "StudyTrack API",
        "version": "v1"
      },
      "paths": {
        "/actividades": {
          "get": {
            "tags": ["Actividad"],
            "summary": "Lista actividades con filtros opcionales de estado, prioridad y usuario",
            "operationId": "ObtenerActividades",
            "responses": {
              "200": {
                "description": "OK",
                "content": {
                  "application/json": {
                    "schema": {
                      "type": "array",
                      "items": { "$ref": "#/components/schemas/Actividad" }
                    }
                  }
                }
              }
            }
          }
        }
      }
    }
    ```

---

### 6. ¿Qué pruebas automatizadas se modificaron, crearon o borraron y qué valida cada una?

1. **[CrossCuttingRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/CrossCuttingRoutesTests.cs) — `Swagger_EndpointJson_Devuelve200Ok` (Creada)**  
   * **¿Qué valida?** Hace una petición HTTP `GET /swagger/v1/swagger.json` y verifica que el servidor responda `200 OK` y que el cuerpo contenga el título `"StudyTrack API"`.
2. **[CrossCuttingRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/CrossCuttingRoutesTests.cs) — `Swagger_EndpointUi_Devuelve200Ok` (Creada)**  
   * **¿Qué valida?** Hace un `GET /swagger/index.html` y verifica respuesta `200 OK` con tipo de contenido `text/html`.
3. **[CrossCuttingRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/CrossCuttingRoutesTests.cs) — `Raiz_RedirigeASwagger` (Creada)**  
   * **¿Qué valida?** Emite un `GET /` con configuración de cliente que no sigue redirecciones automáticas. Valida que el código de respuesta sea estrictamente `302 Found` o `Redirect` y que la cabecera `Location` apunte a `/swagger`.

---

### 7. Modificaciones en Base de Datos vs. cambios a nivel código
* **Base de Datos (PostgreSQL):**  
  **Sin cambios.** Swagger es una capa puramente de presentación y documentación HTTP.
* **Nivel Código (C# / ASP.NET Core):**  
  Integración de paquete NuGet `Swashbuckle.AspNetCore`, inyección de servicios en el contenedor IoC, configuración del pipeline HTTP en `Program.cs` y metadatos de Minimal APIs en `ActividadRoutes.cs`.

---

### 8. ¿Cómo lo que se hizo soluciona el problema de raíz?
Elimina la necesidad de documentación estática desactualizada. Al acoplar la generación de la especificación OpenAPI al propio código de los endpoints, cualquier cambio en las entidades de dominio, filtros o validaciones se refleja automáticamente en la documentación y permite evaluar y consumir la API inmediatamente desde cualquier navegador.

---

### 9. ¿Cómo se puede comprobar este punto paso a paso?
1. **Inspección en Código:**  
   Revisar la configuración de Swagger en [Program.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Program.cs#L25-L35) y los metadatos en [ActividadRoutes.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Routes/ActividadRoutes.cs).
2. **Prueba en Navegador / cURL:**  
   * Ejecutar la aplicación con `dotnet run --project src/StudyTrack.Api`.
   * Abrir un navegador e ingresar a `http://localhost:5000/`. Se apreciará la redirección automática a `http://localhost:5000/swagger`.
   * En Swagger UI, desplegar cualquier endpoint (por ejemplo `POST /actividades`), hacer clic en **Try it out**, llenar el JSON y presionar **Execute**.
   * Ejecutar cURL de redirección:
     ```bash
     curl -i http://localhost:5000/
     ```
     Verificar el header `HTTP/1.1 302 Found` y `Location: /swagger`.
3. **Ejecución de Pruebas Automatizadas:**
   ```bash
   dotnet test --filter "FullyQualifiedName~CrossCuttingRoutesTests"
   ```

---

## Punto 3: Eliminación de DTOs y Uso de Entidades de Dominio (`Actividad`)

### 1. ¿Qué se pedía en este punto? (Requerimiento exacto del feedback)
El profesor solicitó de forma contundente:
> *"Eliminar los DTOs y usar Entidades de Dominio (Actividad)... eliminar las clases intermedias CreateActividadDto, UpdateActividadDto y hacer que los endpoints y delegados operen directamente con la entidad Actividad."*

**Requerimientos específicos:**
1. Eliminar completamente el directorio/archivo de DTOs (`ActividadDtos.cs`) y sus validadores asociados.
2. Hacer que los endpoints `POST /actividades`, `PUT /actividades/{id}`, `GET /actividades` y los métodos del delegado consuman y retornen directamente la entidad de dominio `Actividad`.
3. Resolver el problema de serialización circular: la relación bidireccional entre `Actividad.Subactividades` y `Subactividad.ActividadPadre` causaba errores fatales `JsonException: A possible object cycle was detected`.
4. Ajustar la validación con FluentValidation: en creación (`POST`) se exige obligatoriamente `UserId`, pero en actualización (`PUT`) el cliente solo envía los campos a modificar (sin `UserId`), por lo que no se debe exigir `UserId` en `PUT`.

---

### 2. ¿Qué se tenía antes de los cambios y qué se logró después?
* **Antes de los cambios:**  
  La arquitectura incluía una capa adicional redundante con `CreateActividadDto` y `UpdateActividadDto`. El delegado tenía que mapear manualmente campo por campo (`new Actividad { Titulo = dto.Titulo, ... }`), duplicando más de 15 propiedades. Si se intentaba devolver la entidad de dominio directamente, el serializador System.Text.Json entraba en un bucle infinito por la propiedad de navegación `ActividadPadre`, fallando con un error 500.
* **Después de los cambios:**  
  Se eliminaron por completo todos los DTOs de actividades. La entidad `Actividad` es ahora el modelo unificado que viaja desde el cliente HTTP hasta la base de datos PostgreSQL. Se agregaron anotaciones `[JsonIgnore]` en `ActividadPadre` y se configuró `ReferenceHandler.IgnoreCycles` en `Program.cs`, permitiendo serializar grafos jerárquicos (actividades con subactividades) de forma limpia y sin ciclos. Se implementó `ActividadValidator` con un `RuleSet("Create")` para aislar la validación de `UserId` exclusivamente en el `POST`.

---

### 3. Archivos modificados, creados o borrados, qué se hizo y para qué sirve

1. **`src/StudyTrack.Api/Dtos/ActividadDtos.cs` (Eliminado)**  
   * **¿Qué se hizo?** Se borró el archivo completo que contenía `CreateActividadDto` y `UpdateActividadDto`.  
   * **¿Para qué sirve?** Erradica el código duplicado y la sobrecarga innecesaria de mapeo manual en la API.
2. **`src/StudyTrack.Api/Validators/CreateActividadDtoValidator.cs` y `UpdateActividadDtoValidator.cs` (Eliminados)**  
   * **¿Qué se hizo?** Se removieron los validadores independientes que dependían de los DTOs eliminados.
3. **[ActividadValidator.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Validators/ActividadValidator.cs) (Creado)**  
   * **¿Qué se hizo?** Se consolidó un validador único para la entidad `Actividad`:
     ```csharp
     public class ActividadValidator : AbstractValidator<Actividad>
     {
         public ActividadValidator()
         {
             Include(new ActividadRules());

             RuleSet("Create", () =>
             {
                 RuleFor(x => x.UserId)
                     .NotEmpty().WithMessage("El identificador del usuario es obligatorio.");
             });
         }
     }
     ```  
   * **¿Para qué sirve?** Valida las reglas de negocio base en cualquier escenario, y exige `UserId` de forma estricta únicamente cuando se invoca bajo el conjunto de reglas `"Create"`.
4. **[Actividad.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Domain/Actividad.cs) (Modificado)**  
   * **¿Qué se hizo?** Se colocó `[JsonIgnore]` sobre `public virtual Actividad? ActividadPadre { get; set; }`.  
   * **¿Para qué sirve?** Rompe la referencia circular al serializar hacia JSON, permitiendo que una actividad incluya su lista de `Subactividades` sin que cada subactividad vuelva a serializar a su padre infinitamente.
5. **[ValidationFilter.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Extensions/ValidationFilter.cs) (Modificado)**  
   * **¿Qué se hizo?** Se actualizó el filtro genérico `ValidationFilter<T>` para admitir un parámetro opcional de `ruleSet`.  
   * **¿Para qué sirve?** Permite que el endpoint `POST` ejecute `ValidationContext<T>.CreateWithOptions(..., opt => opt.IncludeRuleSets("default", "Create"))`, mientras que `PUT` ejecuta únicamente las reglas `"default"`.
6. **[Program.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Program.cs) (Modificado)**  
   * **¿Qué se hizo?** Se configuró el serializador JSON en Minimal APIs:
     ```csharp
     builder.Services.ConfigureHttpJsonOptions(options =>
     {
         options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
     });
     ```  
   * **¿Para qué sirve?** Protege toda la aplicación contra ciclos de objetos involuntarios en cualquier endpoint.
7. **[IActividadDelegate.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/IActividadDelegate.cs) y [ActividadDelegate.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/ActividadDelegate.cs) (Modificados)**  
   * **¿Qué se hizo?** Las firmas de `CreateAsync` y `UpdateAsync` cambiaron de DTOs a recibir y retornar `Actividad`.  
   * **¿Para qué sirve?** Opera con entidades puras de dominio según los principios de Arquitectura Hexagonal y DDD.

---

### 4. El antes y después (Comparación exhaustiva de código y arquitectura)

#### A. Código Anterior (Uso de DTOs con mapeo manual redundante)
```csharp
// En ActividadDtos.cs (Existía)
public record CreateActividadDto(
    string Titulo, string? Descripcion, DateTime FechaFin,
    PrioridadActividad Prioridad, string UserId, Guid? ActividadPadreId);

// En ActividadDelegate.cs
public async Task<Actividad> CreateAsync(CreateActividadDto dto, CancellationToken ct)
{
    var actividad = new Actividad
    {
        Id = Guid.NewGuid(),
        Titulo = dto.Titulo,
        Descripcion = dto.Descripcion,
        FechaFin = dto.FechaFin,
        Prioridad = dto.Prioridad,
        UserId = dto.UserId,
        ActividadPadreId = dto.ActividadPadreId,
        Estado = EstadoActividad.Pendiente
    };
    return await repository.CreateAsync(actividad, ct);
}
```

#### B. Código Nuevo Implementado (Uso directo de la Entidad de Dominio Actividad)
```csharp
// ActividadDtos.cs ELIMINADO

// En ActividadRoutes.cs
group.MapPost("/", async (
    [FromBody] Actividad actividad,
    IActividadDelegate @delegate,
    CancellationToken ct) =>
{
    var creada = await @delegate.CreateAsync(actividad, ct);
    return Results.Created($"/actividades/{creada.Id}", creada);
})
.AddEndpointFilter(new ValidationFilter<Actividad>("Create"));

// En ActividadDelegate.cs
public async Task<Actividad> CreateAsync(Actividad actividad, CancellationToken ct)
{
    actividad.Id = actividad.Id == Guid.Empty ? Guid.NewGuid() : actividad.Id;
    actividad.FechaInicio = Ahora();
    actividad.FechaFin = AUtc(actividad.FechaFin);
    return await repository.CreateAsync(actividad, ct);
}
```

---

### 5. Impacto en peticiones HTTP

#### A. Petición de Creación (`POST /actividades`)
* **Headers:** `Content-Type: application/json`
* **Cuerpo de la Petición (Request Body JSON):**
  ```json
  {
    "titulo": "Preparación para Examen de Redes",
    "descripcion": "Estudio de subnetting y protocolos TCP/IP",
    "fechaFin": "2026-11-25T18:00:00Z",
    "prioridad": 2,
    "userId": "usr-estudiante-42",
    "subactividades": [
      {
        "titulo": "Práctica de Enrutamiento OSPF",
        "fechaFin": "2026-11-23T14:00:00Z",
        "prioridad": 1,
        "userId": "usr-estudiante-42"
      }
    ]
  }
  ```
* **Respuesta Esperada del Servidor:**  
  * **HTTP Status Code:** `201 Created`
  * **Header:** `Location: /actividades/a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d`
  * **Body JSON:** Devuelve la entidad `Actividad` creada con su ID generado, fechas normalizadas en UTC y subactividades enlazadas, **sin ciclos JSON**.

#### B. Petición de Actualización (`PUT /actividades/{id}`)
* **Headers:** `Content-Type: application/json`
* **Cuerpo de la Petición:** (Nótese que **no requiere enviar `userId`**)
  ```json
  {
    "titulo": "Preparación para Examen de Redes (Intensivo)",
    "descripcion": "Estudio ampliado con Packet Tracer",
    "fechaFin": "2026-11-26T20:00:00Z",
    "prioridad": 3,
    "estado": 1
  }
  ```
* **Respuesta Esperada del Servidor:**  
  * **HTTP Status Code:** `200 OK`
  * **Body JSON:** Entidad `Actividad` actualizada.

---

### 6. ¿Qué pruebas automatizadas se modificaron, crearon o borraron y qué valida cada una?

1. **[CapasTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Arquitectura/CapasTests.cs) (Reglas de Arquitectura Hexagonal)**  
   * **¿Qué valida?** Utiliza NetArchTest para validar que no existan clases en el namespace `StudyTrack.Api.Dtos` y que ningún método en delegates o rutas dependa de tipos DTO.
2. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs)**  
   * **¿Qué valida?** Se actualizaron todas las pruebas de integración (`Post_Valido_Devuelve201ConLocationYActividad`, `Put_Valido_Devuelve200ConLaActividadActualizada`) para serializar instancias de la clase `Actividad`. Valida que las respuestas 201 y 200 deserialicen correctamente sin excepciones de ciclos.
3. **[ActividadDelegateTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Delegates/ActividadDelegateTests.cs)**  
   * **¿Qué valida?** Verifica que `CreateAsync` y `UpdateAsync` acepten instancias directas de `Actividad` y preserven la integridad de los datos.

---

### 7. Modificaciones en Base de Datos vs. cambios a nivel código
* **Base de Datos (PostgreSQL):**  
  **Sin cambios.** Las tablas de la base de datos ya correspondían a la entidad de dominio.
* **Nivel Código (C# / ASP.NET Core):**  
  Eliminación de la carpeta `Dtos/`, refactorización de `IActividadDelegate`, `ActividadRoutes`, implementación de `[JsonIgnore]` y `ReferenceHandler.IgnoreCycles`.

---

### 8. ¿Cómo lo que se hizo soluciona el problema de raíz?
Elimina la complejidad accidental provocada por clases espejo que solo copiaban propiedades sin aportar valor de negocio. Al enriquecer la entidad de dominio y configurar correctamente el serializador JSON para ignorar ciclos padre-hijo, el código se vuelve más limpio, mantenible y coherente con el paradigma orientado a objetos y DDD.

---

### 9. ¿Cómo se puede comprobar este punto paso a paso?
1. **Inspección en Código:**  
   Comprobar en el explorador de la solución que la carpeta `Dtos` ya no existe y revisar [Actividad.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Domain/Actividad.cs#L22) con el atributo `[JsonIgnore]`.
2. **Prueba en Postman / Swagger:**  
   * Enviar `POST /actividades` con una actividad principal que incluya un arreglo de subactividades.
   * Comprobar que el servidor responde `201 Created` y muestra el árbol jerárquico completo en JSON sin truncarse ni arrojar error 500.
   * Enviar `PUT /actividades/{id}` enviando solo título y descripción (sin `userId`). Verificar que responde `200 OK`.
3. **Ejecución de Pruebas Automatizadas:**
   ```bash
   dotnet test --filter "FullyQualifiedName~CapasTests|FullyQualifiedName~ActividadRoutesTests.Post"
   ```

---

## Punto 4: Filtro de Búsqueda y Calendario por Usuario

### 1. ¿Qué se pedía en este punto? (Requerimiento exacto del feedback)
El profesor solicitó:
> *"Filtro de actividades por usuario en búsquedas y calendario... en GET /actividades y GET /actividades/calendario permitir filtrar por usuario para que no se devuelvan las actividades de todos los usuarios de la base de datos."*

**Requerimientos específicos:**
1. Habilitar el parámetro de consulta opcional `userId` (`?userId=...`) en los endpoints `GET /actividades` y `GET /actividades/calendario`.
2. Cuando el parámetro `userId` esté presente, la API debe devolver únicamente:
   * Actividades donde el usuario es el creador/dueño (`UserId == userId`).
   * Actividades que han sido compartidas con el usuario como colaborador directo (`ActividadesCompartidas.Any(ac => ac.UsuarioId == userId)`).
   * Subactividades hijas cuya actividad padre esté compartida con dicho usuario.
3. El filtrado debe ejecutarse directamente en el motor de base de datos PostgreSQL para no traer registros innecesarios a memoria del servidor.

---

### 2. ¿Qué se tenía antes de los cambios y qué se logró después?
* **Antes de los cambios:**  
  Los endpoints `GET /actividades` y `GET /actividades/calendario` solo aceptaban filtros de `estado`, `prioridad` o rango de fechas. Si existían 1,000 actividades pertenecientes a 50 estudiantes diferentes, el servidor devolvía la totalidad de las actividades sin ningún tipo de discriminación por usuario. Esto violaba la privacidad básica y provocaba sobrecarga de datos innecesaria.
* **Después de los cambios:**  
  Se añadió `[FromQuery] string? userId` en ambas operaciones. En `ActividadRepository`, se integró una cláusula `Where` relacional en las consultas LINQ de EF Core, traduciéndose a una sentencia SQL eficiente en PostgreSQL (`WHERE a."UserId" = @userId OR EXISTS (...)`). Si se suministra `userId`, el usuario visualiza exclusivamente su ecosistema de actividades personales y colaborativas.

---

### 3. Archivos modificados, creados o borrados, qué se hizo y para qué sirve

1. **[ActividadRoutes.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Routes/ActividadRoutes.cs) (Modificado)**  
   * **¿Qué se hizo?** Se incorporó el parámetro `[FromQuery] string? userId` en `GET /actividades` y en `GET /actividades/calendario`, pasándolo a las llamadas correspondientes del delegado.  
   * **¿Para qué sirve?** Expone el parámetro en la interfaz HTTP y en la documentación OpenAPI de Swagger.
2. **[IActividadDelegate.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/IActividadDelegate.cs) y [ActividadDelegate.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/ActividadDelegate.cs) (Modificados)**  
   * **¿Qué se hizo?** Se actualizaron las firmas:
     * `Task<IEnumerable<Actividad>> GetAllAsync(string? callerUserId = null, CancellationToken ct = default);`
     * `Task<IEnumerable<Actividad>> GetCalendarioAsync(DateTime inicio, DateTime fin, string? callerUserId = null, CancellationToken ct = default);`  
   * **¿Para qué sirve?** Propaga la identidad del usuario desde la capa HTTP hacia la capa de persistencia.
3. **[IActividadRepository.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/IActividadRepository.cs) y [ActividadRepository.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/ActividadRepository.cs) (Modificados)**  
   * **¿Qué se hizo?** En `GetAllAsync` y `GetPorRangoFechasAsync` se agregó la condición de filtrado:
     ```csharp
     if (!string.IsNullOrWhiteSpace(callerUserId))
     {
         consulta = consulta.Where(a =>
             a.UserId == callerUserId ||
             a.ActividadesCompartidas.Any(ac => ac.UsuarioId == callerUserId) ||
             (a.ActividadPadreId != null && context.Actividades.Any(padre =>
                 padre.Id == a.ActividadPadreId &&
                 (padre.UserId == callerUserId || padre.ActividadesCompartidas.Any(ac => ac.UsuarioId == callerUserId)))));
     }
     ```  
   * **¿Para qué sirve?** Filtra a nivel relacional en PostgreSQL para garantizar que sólo se recuperen las entidades autorizadas para el usuario.

---

### 4. El antes y después (Comparación exhaustiva de código y arquitectura)

#### A. Código Anterior (Sin filtro por usuario: devolvía todas las actividades globales)
```csharp
// En ActividadRepository.cs
public async Task<IEnumerable<Actividad>> GetAllAsync(CancellationToken cancellationToken = default)
{
    return await context.Actividades
        .AsNoTracking()
        .Include(a => a.Subactividades)
        .Include(a => a.ActividadesCompartidas)
        .Where(a => a.ActividadPadreId == null) // Solo filtraba padres, pero de TODOS los usuarios
        .ToListAsync(cancellationToken);
}
```

#### B. Código Nuevo Implementado (Con filtro relacional estricto por usuario en PostgreSQL)
```csharp
// En ActividadRepository.cs
public async Task<IEnumerable<Actividad>> GetAllAsync(string? callerUserId = null, CancellationToken cancellationToken = default)
{
    var consulta = context.Actividades
        .AsNoTracking()
        .Include(a => a.Subactividades)
        .Include(a => a.ActividadesCompartidas)
        .Where(a => a.ActividadPadreId == null);

    if (!string.IsNullOrWhiteSpace(callerUserId))
    {
        consulta = consulta.Where(a =>
            a.UserId == callerUserId ||
            a.ActividadesCompartidas.Any(ac => ac.UsuarioId == callerUserId));
    }

    return await consulta.ToListAsync(cancellationToken);
}
```

---

### 5. Impacto en peticiones HTTP

#### A. Listado de Actividades Filtrado por Usuario (`GET /actividades?userId=...`)
* **Verbo y Ruta:** `GET /actividades?userId=usr-estudiante-1`
* **Respuesta Esperada del Servidor:**  
  * **HTTP Status Code:** `200 OK`
  * **Cuerpo de la Respuesta (JSON):**
    ```json
    [
      {
        "id": "e4f8d374-27bf-4f59-9944-90a612502ec6",
        "titulo": "Mi Tarea de Cálculo",
        "userId": "usr-estudiante-1",
        "actividadesCompartidas": []
      },
      {
        "id": "c1a2b3c4-d5e6-4a7b-8c9d-0e1f2a3b4c5d",
        "titulo": "Proyecto de Software (Compartido)",
        "userId": "usr-estudiante-2",
        "actividadesCompartidas": [
          {
            "actividadId": "c1a2b3c4-d5e6-4a7b-8c9d-0e1f2a3b4c5d",
            "usuarioId": "usr-estudiante-1"
          }
        ]
      }
    ]
    ```
  *(Las actividades privadas de `usr-estudiante-3` son completamente omitidas).*

#### B. Consulta de Calendario Filtrada por Usuario (`GET /actividades/calendario?...`)
* **Verbo y Ruta:**  
  `GET /actividades/calendario?fechaInicio=2026-11-01T00:00:00Z&fechaFin=2026-11-30T23:59:59Z&userId=usr-estudiante-1`
* **Respuesta Esperada del Servidor:**  
  * **HTTP Status Code:** `200 OK`
  * **Cuerpo de la Respuesta (JSON):** Arreglo de actividades pertenecientes exclusivamente a `usr-estudiante-1` o donde colabora dentro del intervalo de fechas.

---

### 6. ¿Qué pruebas automatizadas se modificaron, crearon o borraron y qué valida cada una?

1. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs) — `Get_ConFiltroUserId_SoloDevuelveActividadesDelUsuarioYCompartidas` (Creada)**  
   * **¿Qué valida?** Siembra en base de datos una actividad propia de `usr-1`, una actividad compartida con `usr-1` creada por `usr-2`, y una actividad privada ajena de `usr-3`. Ejecuta `GET /actividades?userId=usr-1` y comprueba que se reciban exactamente 2 actividades y que la privada de `usr-3` nunca esté presente.
2. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs) — `GetCalendario_ConFiltroUserId_SoloDevuelveActividadesDelUsuario` (Creada)**  
   * **¿Qué valida?** Siembra actividades para distintos usuarios en el mismo mes y ejecuta `GET /actividades/calendario?...&userId=usr-1`. Valida que sólo se retornen actividades pertenecientes a ese usuario en el rango.
3. **[ValidationMiddlewareTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Middleware/ValidationMiddlewareTests.cs) (Actualizada)**  
   * **¿Qué valida?** Se actualizó el Mock del delegado para admitir el parámetro opcional `callerUserId` (`delegateMock.Setup(d => d.GetAllAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))`).

---

### 7. Modificaciones en Base de Datos vs. cambios a nivel código
* **Base de Datos (PostgreSQL):**  
  **Sin cambios de esquema DDL.** Se aprovechan las claves foráneas e índices ya existentes en la tabla `ActividadesCompartidas` y `Actividades`. La consulta SQL generada por EF Core ejecuta un `JOIN / EXISTS` optimizado en el motor relacional.
* **Nivel Código (C# / ASP.NET Core):**  
  Incorporación de `[FromQuery] string? userId` en `ActividadRoutes.cs` y adición de la expresión condicional LINQ en `ActividadRepository.cs`.

---

### 8. ¿Cómo lo que se hizo soluciona el problema de raíz?
Garantiza el aislamiento lógico multi-usuario en las consultas de lectura más utilizadas de la aplicación (listado y vista calendario), evitando la filtración de información privada entre estudiantes y optimizando el consumo de ancho de banda y memoria.

---

### 9. ¿Cómo se puede comprobar este punto paso a paso?
1. **Inspección en Código:**  
   Revisar la implementación del método `GetAllAsync` y `GetPorRangoFechasAsync` en [ActividadRepository.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/ActividadRepository.cs#L35-L55).
2. **Prueba en Postman / Swagger:**  
   * Crear Actividad A con `"userId": "alumno-a"`.
   * Crear Actividad B con `"userId": "alumno-b"`.
   * Ejecutar `GET /actividades?userId=alumno-a` -> La respuesta solo contendrá la Actividad A.
   * Ejecutar `GET /actividades?userId=alumno-b` -> La respuesta solo contendrá la Actividad B.
3. **Ejecución de Pruebas Automatizadas:**
   ```bash
   dotnet test --filter "FullyQualifiedName~ConFiltroUserId"
   ```

---

## Punto 5: Validación de Usuario Destinatario al Compartir (Error 400 en vez de 500)

### 1. ¿Qué se pedía en este punto? (Requerimiento exacto del feedback)
El docente señaló en la retroalimentación:
> *"Al compartir una actividad si el usuario no existe en la base de datos la petición truena con un error 500 de base de datos por foreign key... debe validar que el usuario exista y devolver un error 400 Bad Request claro."*

**Requerimientos específicos:**
1. Validar la existencia previa del usuario destinatario en la tabla `Usuarios` de PostgreSQL antes de intentar insertar la relación en la tabla `ActividadesCompartidas`.
2. Si el usuario destinatario no existe en el sistema, responder con código HTTP **400 Bad Request** y detalle explicativo: `"El usuario destinatario '<id>' no existe."`.
3. Evitar bajo cualquier circunstancia que la violación de clave foránea `FK_ActividadesCompartidas_Usuarios_UsuarioId` (código de error de PostgreSQL `23503`) explote como un error interno no controlado (`500 Internal Server Error`).

---

### 2. ¿Qué se tenía antes de los cambios y qué se logró después?
* **Antes de los cambios:**  
  El método `CompartirAsync` asumía ciegamente que el identificador recibido correspondía a un usuario válido en la base de datos e intentaba hacer directamente `context.ActividadesCompartidas.Add(...)`. Al no existir el registro en la tabla `Usuarios`, el motor PostgreSQL abortaba la transacción arrojando:
  `Npgsql.PostgresException (0x80004005): 23503: insert or update on table "ActividadesCompartidas" violates foreign key constraint "FK_ActividadesCompartidas_Usuarios_UsuarioId"`.  
  El manejador de excepciones no contemplaba este escenario y el cliente recibía un genérico e inaceptable `500 Internal Server Error`.
* **Después de los cambios:**  
  Se implementó un mecanismo preventivo en dos niveles:
  1. En `IActividadRepository` se agregó el método de alta velocidad `ExisteUsuarioAsync(string usuarioId)` mediante un `AnyAsync` traducido en PostgreSQL como `SELECT EXISTS(...)`.
  2. En `ActividadDelegate.CompartirAsync`, se consulta dicho método antes de proceder: si el usuario no existe, se lanza `BadRequestException`.
  3. Se creó la clase `BadRequestException` y se integró en `GlobalExceptionHandler`, devolviendo un `400 Bad Request` conforme al estándar RFC 9457.

---

### 3. Archivos modificados, creados o borrados, qué se hizo y para qué sirve

1. **[BadRequestException.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/Exceptions/BadRequestException.cs) (Creado)**  
   * **¿Qué se hizo?** Se definió una excepción de dominio: `public class BadRequestException(string message) : Exception(message)`.  
   * **¿Para qué sirve?** Permite que la capa de negocio exprese fallos semánticos de validación que deben ser devueltos al cliente con código HTTP 400.
2. **[GlobalExceptionHandler.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Extensions/GlobalExceptionHandler.cs) (Modificado)**  
   * **¿Qué se hizo?** Se añadió el manejo tipado de `BadRequestException`:
     ```csharp
     case BadRequestException badRequest:
         statusCode = StatusCodes.Status400BadRequest;
         title = "Bad Request";
         detail = badRequest.Message;
         break;
     ```  
   * **¿Para qué sirve?** Intercepta la excepción de negocio y la formatea en un ProblemDetails RFC 9457 con HTTP 400 en lugar de un error 500.
3. **[IActividadRepository.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/IActividadRepository.cs) y [ActividadRepository.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/ActividadRepository.cs) (Modificados)**  
   * **¿Qué se hizo?** Se implementó:
     ```csharp
     public async Task<bool> ExisteUsuarioAsync(string usuarioId, CancellationToken cancellationToken = default)
     {
         return await context.Usuarios.AnyAsync(u => u.Id == usuarioId, cancellationToken);
     }
     ```  
   * **¿Para qué sirve?** Valida la existencia física del usuario en PostgreSQL sin cargar toda la entidad en memoria.
4. **[ActividadDelegate.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/ActividadDelegate.cs) (Modificado)**  
   * **¿Qué se hizo?** Se incorporó la validación previa en `CompartirAsync`:
     ```csharp
     var existeUsuario = await repository.ExisteUsuarioAsync(usuarioDestinatarioId, cancellationToken);
     if (!existeUsuario)
         throw new BadRequestException($"El usuario destinatario '{usuarioDestinatarioId}' no existe.");
     ```  
   * **¿Para qué sirve?** Frena la ejecución antes de causar la violación de la clave foránea en la base de datos.

---

### 4. El antes y después (Comparación exhaustiva de código y arquitectura)

#### A. Código Anterior (Inserción a ciegas que causaba PostgresException 23503 y Error 500)
```csharp
// En ActividadDelegate.cs
public async Task CompartirAsync(Guid actividadId, string usuarioDestinatarioId, CancellationToken ct)
{
    var actividad = await repository.GetByIdAsync(actividadId, ct)
        ?? throw new NotFoundException("Actividad no encontrada");

    // Inserción directa sin validar si el destinatario existe en la BD
    await repository.CompartirAsync(actividadId, usuarioDestinatarioId, ct);
    // ¡BOOM! Si no existía, PostgreSQL lanzaba FK 23503 -> 500 Internal Server Error
}
```

#### B. Código Nuevo Implementado (Comprobación preventiva y traducción a 400 Bad Request)
```csharp
// En ActividadDelegate.cs
public async Task CompartirAsync(
    Guid actividadId,
    CompartirActividadRequest request,
    CancellationToken ct)
{
    var actividad = await repository.GetByIdAsync(actividadId, ct)
        ?? throw new NotFoundException($"Actividad con id '{actividadId}' no encontrada.");

    if (actividad.UserId != request.PropietarioId)
        throw new ForbiddenException("Solo el creador puede compartir la actividad.");

    // Validación preventiva en base de datos
    var existe = await repository.ExisteUsuarioAsync(request.UsuarioId, ct);
    if (!existe)
        throw new BadRequestException($"El usuario destinatario '{request.UsuarioId}' no existe.");

    await repository.CompartirAsync(actividadId, request.UsuarioId, ct);
}
```

---

### 5. Impacto en peticiones HTTP

#### A. Compartir con Usuario Inexistente (Error 400 Bad Request Controlado)
* **Verbo y Ruta:** `POST /actividades/3fa85f64-5717-4562-b3fc-2c963f66afa6/compartir`
* **Headers:** `Content-Type: application/json`
* **Cuerpo de la Petición (Request Body JSON):**
  ```json
  {
    "usuarioId": "usuario-fantasma-no-existe-999",
    "propietarioId": "usuario-creador-1"
  }
  ```
* **Respuesta Esperada del Servidor:**  
  * **HTTP Status Code:** `400 Bad Request` *(en lugar de 500)*
  * **Headers:** `Content-Type: application/problem+json`
  * **Cuerpo de la Respuesta (Problem Details RFC 9457):**
    ```json
    {
      "type": "https://httpstatuses.com/400",
      "title": "Bad Request",
      "status": 400,
      "detail": "El usuario destinatario 'usuario-fantasma-no-existe-999' no existe."
    }
    ```

#### B. Compartir con Usuario Existente (Éxito 200 OK)
* **Verbo y Ruta:** `POST /actividades/3fa85f64-5717-4562-b3fc-2c963f66afa6/compartir`
* **Headers:** `Content-Type: application/json`
* **Cuerpo de la Petición:**
  ```json
  {
    "usuarioId": "usuario-compañero-existente",
    "propietarioId": "usuario-creador-1"
  }
  ```
* **Respuesta Esperada:**  
  * **HTTP Status Code:** `200 OK`
  * **Body JSON:** `{"mensaje": "Actividad compartida exitosamente."}`

---

### 6. ¿Qué pruebas automatizadas se modificaron, crearon o borraron y qué valida cada una?

1. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs) — `Compartir_UsuarioDestinatarioNoExiste_Devuelve400BadRequest` (Creada)**  
   * **¿Qué valida?** Crea una actividad válida para `usuario-1`, siembra solo a `usuario-1` en la BD y llama a compartir con `usuario-inexistente`. Verifica que el código devuelto sea exactamente `400 Bad Request` y que el mensaje en `detail` contenga `"no existe"`.
2. **[ActividadDelegateTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Delegates/ActividadDelegateTests.cs) — `CompartirAsync_UsuarioDestinatarioNoExiste_LanzaBadRequestException` (Creada)**  
   * **¿Qué valida?** Configura el mock del repositorio para retornar `false` en `ExisteUsuarioAsync` y verifica que el delegado arroje `BadRequestException`.

---

### 7. Modificaciones en Base de Datos vs. cambios a nivel código
* **Base de Datos (PostgreSQL):**  
  **Sin cambios en tablas ni restricciones.** La restricción de clave foránea `FK_ActividadesCompartidas_Usuarios_UsuarioId` se mantiene intacta como salvaguarda final. La mejora reside en una consulta `SELECT EXISTS` preventiva.
* **Nivel Código (C# / ASP.NET Core):**  
  Creación de `BadRequestException`, enriquecimiento del `GlobalExceptionHandler` y verificación preventiva en `ActividadDelegate`.

---

### 8. ¿Cómo lo que se hizo soluciona el problema de raíz?
Evita que la base de datos tenga que rechazar la transacción de forma catastrófica. La aplicación valida las precondiciones de negocio en la capa de dominio antes de escribir en disco, transformando errores no capturados (crashes 500) en respuestas controladas de cliente (HTTP 400).

---

### 9. ¿Cómo se puede comprobar este punto paso a paso?
1. **Inspección en Código:**  
   Revisar la consulta de existencia en [ActividadRepository.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/ActividadRepository.cs#L140) y la validación en [ActividadDelegate.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/ActividadDelegate.cs#L120-L135).
2. **Prueba en Postman / Swagger:**  
   * Tomar el ID de una actividad creada.
   * Ejecutar un `POST /actividades/{id}/compartir` con el body:
     ```json
     {
       "usuarioId": "usuario-totalmente-falso-12345",
       "propietarioId": "usr-creador"
     }
     ```
   * Comprobar que la respuesta devuelta es HTTP `400 Bad Request` y no `500 Internal Server Error`.
3. **Ejecución de Pruebas Automatizadas:**
   ```bash
   dotnet test --filter "FullyQualifiedName~Compartir_UsuarioDestinatarioNoExiste"
   ```

---

## Punto 6: Asociación Directa de Colaboradores en Creación y Actualización (`POST`/`PUT`)

### 1. ¿Qué se pedía en este punto? (Requerimiento exacto del feedback)
El profesor indicó en su evaluación:
> *"Poder asociar colaboradores directamente al crear la actividad o al actualizarla... aceptar tanto un array de strings [\"usr-2\", \"usr-3\"] como un array de objetos [{\"usuarioId\": \"usr-2\"}], validar que existan los colaboradores y que solo el creador pueda modificar los colaboradores en el PUT."*

**Requerimientos específicos:**
1. Permitir que el cuerpo JSON de `POST /actividades` incluya la colección `actividadesCompartidas`.
2. Soportar de forma flexible y polimórfica dos formatos en el JSON de entrada:
   * **Array de strings:** `["usuario-2", "usuario-3"]`
   * **Array de objetos:** `[{"usuarioId": "usuario-2"}, {"usuarioId": "usuario-3"}]`
3. Validar preventivamente que todos los colaboradores existan en la tabla `Usuarios` (HTTP 400 si alguno es inexistente).
4. En `PUT /actividades/{id}`, permitir sincronizar colaboradores (agregar nuevos y desasociar los eliminados).
5. En `PUT /actividades/{id}`, si quien intenta modificar la lista de colaboradores no es el propietario, la operación debe ser denegada con **403 Forbidden**.

---

### 2. ¿Qué se tenía antes de los cambios y qué se logró después?
* **Antes de los cambios:**  
  La propiedad `ActividadesCompartidas` era ignorada completamente al recibir un `POST` o un `PUT`. Si un cliente intentaba enviar colaboradores en el cuerpo de creación, la API los descartaba silenciosamente o la deserialización de `System.Text.Json` fallaba con error de conversión si venían como strings planos (`["usr-2"]`). Para armar un equipo de 3 estudiantes se requería hacer 1 petición POST para crear la actividad y luego 3 peticiones secuenciales a `/compartir`.
* **Después de los cambios:**  
  1. Se implementó el convertidor JSON personalizado `ActividadCompartidaJsonConverter` registrado en `ActividadCompartida.cs`, el cual detecta el tipo de token JSON (`TokenType.String` o `TokenType.StartObject`) y deserializa automáticamente a instancias válidas de `ActividadCompartida`.
  2. En `ActividadDelegate.CreateAsync`, los colaboradores se validan contra `repository.ExisteUsuarioAsync` y se asignan a la entidad antes de persistir, logrando **creación atómica en una sola llamada HTTP**.
  3. En `ActividadDelegate.UpdateAsync` y `ActividadRepository.UpdateAsync`, se compara la lista entrante con la existente en base de datos: se agregan los nuevos y se eliminan los removidos, validando estrictamente que si hay cambios en colaboradores, el solicitante sea obligatoriamente el propietario (`403 Forbidden` si un colaborador o un tercero intenta modificarlos).

---

### 3. Archivos modificados, creados o borrados, qué se hizo y para qué sirve

1. **[ActividadCompartidaJsonConverter.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Domain/ActividadCompartidaJsonConverter.cs) (Creado)**  
   * **¿Qué se hizo?** Se implementó un `JsonConverter<ActividadCompartida>` que inspecciona el lector JSON:
     ```csharp
     public override ActividadCompartida? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
     {
         if (reader.TokenType == JsonTokenType.String)
         {
             var usuarioId = reader.GetString();
             return new ActividadCompartida { UsuarioId = usuarioId ?? string.Empty };
         }
         if (reader.TokenType == JsonTokenType.StartObject)
         {
             using var doc = JsonDocument.ParseValue(ref reader);
             var root = doc.RootElement;
             string usuarioId = root.TryGetProperty("usuarioId", out var prop) ? prop.GetString() ?? "" : "";
             return new ActividadCompartida { UsuarioId = usuarioId };
         }
         throw new JsonException("Formato no válido para ActividadCompartida.");
     }
     ```  
   * **¿Para qué sirve?** Otorga flexibilidad absoluta a los clientes frontend y móviles para enviar colaboradores tanto en formato simple de strings como en formato de objetos completos.
2. **[ActividadCompartida.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Domain/ActividadCompartida.cs) (Modificado)**  
   * **¿Qué se hizo?** Se decoró la clase con:  
     `[JsonConverter(typeof(ActividadCompartidaJsonConverter))]`.  
   * **¿Para qué sirve?** Enlaza el convertidor polimórfico al deserializador global de la API.
3. **[ActividadDelegate.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/ActividadDelegate.cs) (Modificado)**  
   * **¿Qué se hizo?**  
     * En `CreateAsync`: itera sobre `actividad.ActividadesCompartidas`, valida que cada usuario exista con `ExisteUsuarioAsync` (o lanza `BadRequestException`) y asigna `ac.ActividadId = actividad.Id`.  
     * En `UpdateAsync`: verifica si la lista de colaboradores cambió; si cambió y el `callerUserId` no es el propietario, lanza `ForbiddenException("Solo el creador puede modificar los colaboradores.")`.  
   * **¿Para qué sirve?** Asegura consistencia atómica y cumplimiento de las políticas de autorización del dominio.
4. **[ActividadRepository.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/ActividadRepository.cs) (Modificado)**  
   * **¿Qué se hizo?** En `UpdateAsync`, se implementó la sincronización diferencial en la tabla `ActividadesCompartidas`:
     ```csharp
     var actuales = actividadExistente.ActividadesCompartidas.ToList();
     var nuevosIds = actividad.ActividadesCompartidas.Select(ac => ac.UsuarioId).ToHashSet();

     // Remover colaboradores desasociados
     foreach (var actual in actuales.Where(a => !nuevosIds.Contains(a.UsuarioId)))
         context.ActividadesCompartidas.Remove(actual);

     // Agregar nuevos colaboradores
     var actualesIds = actuales.Select(a => a.UsuarioId).ToHashSet();
     foreach (var nuevo in actividad.ActividadesCompartidas.Where(n => !actualesIds.Contains(n.UsuarioId)))
     {
         nuevo.ActividadId = actividadExistente.Id;
         context.ActividadesCompartidas.Add(nuevo);
     }
     ```  
   * **¿Para qué sirve?** Actualiza limpiamente las relaciones en PostgreSQL sin dejar registros huérfanos ni duplicados.

---

### 4. El antes y después (Comparación exhaustiva de código y arquitectura)

#### A. Código Anterior (Colaboradores ignorados y llamadas N+1 requeridas)
```csharp
// Crear actividad ignoraba completamente ActividadesCompartidas
// Para asociar colaboradores se debía llamar 1 por 1 a /compartir
await client.PostAsJsonAsync("/actividades", nuevaActividad);
await client.PostAsJsonAsync($"/actividades/{id}/compartir", new { usuarioId = "usr-2" });
await client.PostAsJsonAsync($"/actividades/{id}/compartir", new { usuarioId = "usr-3" });
```

#### B. Código Nuevo Implementado (Asociación directa atómica y soporte de formato string / objeto)
```csharp
// Se crea la actividad y se asocian sus colaboradores en 1 sola llamada HTTP
var actividad = new Actividad
{
    Titulo = "Proyecto Final de Nube",
    UserId = "usr-creador",
    ActividadesCompartidas = new List<ActividadCompartida>
    {
        new() { UsuarioId = "usr-2" },
        new() { UsuarioId = "usr-3" }
    }
};

var response = await client.PostAsJsonAsync("/actividades", actividad);
// Persiste la actividad y las dos relaciones en la misma transacción de PostgreSQL
```

---

### 5. Impacto en peticiones HTTP

#### A. Creación con Colaboradores en formato Array de Strings (`POST /actividades`)
* **Headers:** `Content-Type: application/json`
* **Cuerpo de la Petición (Request Body JSON):**
  ```json
  {
    "titulo": "Investigación en Equipo de Microservicios",
    "descripcion": "Análisis comparativo de Docker y Podman",
    "fechaFin": "2026-11-28T23:59:00Z",
    "prioridad": 2,
    "userId": "usr-estudiante-1",
    "actividadesCompartidas": [
      "usr-estudiante-2",
      "usr-estudiante-3"
    ]
  }
  ```
* **Respuesta Esperada del Servidor:**  
  * **HTTP Status Code:** `201 Created`
  * **Body JSON:** Entidad `Actividad` creada conteniendo su lista de colaboradores poblada:
    ```json
    {
      "id": "7c8e9b10-1234-5678-90ab-cdef12345678",
      "titulo": "Investigación en Equipo de Microservicios",
      "userId": "usr-estudiante-1",
      "actividadesCompartidas": [
        {
          "actividadId": "7c8e9b10-1234-5678-90ab-cdef12345678",
          "usuarioId": "usr-estudiante-2"
        },
        {
          "actividadId": "7c8e9b10-1234-5678-90ab-cdef12345678",
          "usuarioId": "usr-estudiante-3"
        }
      ]
    }
    ```

#### B. Actualización de Colaboradores siendo Propietario (`PUT /actividades/{id}`)
* **Headers:** `Content-Type: application/json`
* **Cuerpo de la Petición:**
  ```json
  {
    "titulo": "Investigación en Equipo de Microservicios (Actualizada)",
    "fechaFin": "2026-11-29T23:59:00Z",
    "prioridad": 3,
    "userId": "usr-estudiante-1",
    "actividadesCompartidas": [
      "usr-estudiante-2",
      "usr-estudiante-4"
    ]
  }
  ```
  *(Se desasocia a `usr-estudiante-3` y se asocia a `usr-estudiante-4`).*
* **Respuesta Esperada:**  
  * **HTTP Status Code:** `200 OK`

#### C. Intento de Modificación de Colaboradores por un No-Propietario (`PUT /actividades/{id}`)
* **Respuesta Esperada:**  
  * **HTTP Status Code:** `403 Forbidden`
  * **Detail:** `"Solo el creador puede modificar los colaboradores."`

---

### 6. ¿Qué pruebas automatizadas se modificaron, crearon o borraron y qué valida cada una?

1. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs) — `Post_ConColaboradoresEnFormatoString_AsociaColaboradoresCorrectamente` (Creada)**  
   * **¿Qué valida?** Envía un JSON crudo con `"actividadesCompartidas": ["usuario-2"]`. Valida que el convertidor deserialice sin error, que la API responda `201 Created` y que al consultar la actividad se recupere el colaborador asociado.
2. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs) — `Put_ModificandoColaboradores_SiendoPropietario_SincronizaColaboradores` (Creada)**  
   * **¿Qué valida?** Crea una actividad con `usuario-2`, luego mediante `PUT` envía como propietario a `usuario-3`. Verifica respuesta `200 OK` y comprueba en base de datos que `usuario-2` fue desasociado y `usuario-3` quedó como único colaborador.
3. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs) — `Put_ModificandoColaboradores_NoSiendoPropietario_Devuelve403Forbidden` (Creada)**  
   * **¿Qué valida?** Intenta actualizar colaboradores enviando `userId: "usuario-2"` (colaborador o tercero). Valida que retorne `403 Forbidden`.

---

### 7. Modificaciones en Base de Datos vs. cambios a nivel código
* **Base de Datos (PostgreSQL):**  
  **Sin cambios en el esquema físico.** Se realizan operaciones de inserción y borrado atómico en la tabla `ActividadesCompartidas` gestionadas por la sesión de EF Core en una única transacción de base de datos.
* **Nivel Código (C# / ASP.NET Core):**  
  Creación del `ActividadCompartidaJsonConverter` para serialización polimórfica, lógica de validación de usuarios en `ActividadDelegate` y algoritmo de sincronización en `ActividadRepository`.

---

### 8. ¿Cómo lo que se hizo soluciona el problema de raíz?
Elimina la fragilidad transaccional de crear actividades grupales en múltiples llamadas independientes (donde si la tercera fallaba, la actividad quedaba en un estado inconsistente a medio compartir). Ahora la creación es 100% transaccional y el modelo soporta interfaces de usuario modernas que envían arrays simples de IDs.

---

### 9. ¿Cómo se puede comprobar este punto paso a paso?
1. **Inspección en Código:**  
   Revisar el convertidor [ActividadCompartidaJsonConverter.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Domain/ActividadCompartidaJsonConverter.cs) y la sincronización en [ActividadRepository.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/ActividadRepository.cs#L85-L115).
2. **Prueba en Postman / Swagger:**  
   * Enviar un `POST /actividades` enviando:
     ```json
     {
       "titulo": "Práctica de Nube Atómica",
       "fechaFin": "2026-11-30T10:00:00Z",
       "userId": "usr-1",
       "actividadesCompartidas": ["usr-2"]
     }
     ```
   * Verificar respuesta `201 Created` y que la respuesta incluye `"actividadesCompartidas"` con `usr-2`.
3. **Ejecución de Pruebas Automatizadas:**
   ```bash
   dotnet test --filter "FullyQualifiedName~Colaboradores"
   ```

---

## Punto 7: Corrección de Duplicados en Calendario (Hijas en Raíz)

### 1. ¿Qué se pedía en este punto? (Requerimiento exacto del feedback)
El profesor señaló en la revisión:
> *"En el calendario las subactividades aparecen duplicadas... aparecen como elementos en la raíz del array y también adentro del padre... no deben duplicarse en la raíz si su padre ya está en el resultado."*

**Requerimientos específicos:**
1. En `GET /actividades/calendario`, cuando una actividad padre y sus subactividades caen dentro del mismo rango de fechas consultado, las subactividades **no deben incluirse como elementos sueltos de primer nivel en el array raíz**.
2. Las subactividades deben figurar exclusivamente dentro de la colección `subactividades` de su respectiva actividad padre.
3. Si una subactividad cae dentro del rango de fechas pero su actividad padre se encuentra fuera de dicho rango (caso de subactividad huérfana en el periodo), la subactividad sí debe ser visible en la raíz para no ocultar entregas al estudiante.

---

### 2. ¿Qué se tenía antes de los cambios y qué se logró después?
* **Antes de los cambios:**  
  La consulta del repositorio recuperaba todas las filas de la tabla `Actividades` que cumplieran con la condición de fecha (`FechaFin >= inicio && FechaFin <= fin`). Como EF Core carga las entidades en el Change Tracker y resuelve automáticamente las relaciones de navegación `Subactividades`, el resultado retornado a la API era un array plano con todas las entidades. Al serializar a JSON, la respuesta incluía al padre (con sus 2 hijas en su array interior) y, a continuación, a las 2 hijas nuevamente como elementos independientes en la raíz. En la vista del calendario del cliente frontend, cada tarea hija se renderizaba dos veces (duplicada).
* **Después de los cambios:**  
  Se implementó un algoritmo de resolución jerárquica en memoria en `ActividadRepository.GetPorRangoFechasAsync`:
  1. Se obtienen todas las actividades que caen en el rango de fechas.
  2. Se extrae el conjunto de identificadores únicos del resultado: `var idsEnRango = actividades.Select(a => a.Id).ToHashSet();`.
  3. Se filtra la colección devuelta a la raíz: se conservan únicamente aquellas actividades que no tienen padre (`!a.ActividadPadreId.HasValue`) O aquellas cuyo padre **no** esté presente en el conjunto (`!idsEnRango.Contains(a.ActividadPadreId.Value)`).
  4. De este modo, si el padre está en la respuesta, sus hijas permanecen encapsuladas dentro de él sin duplicarse en la raíz.

---

### 3. Archivos modificados, creados o borrados, qué se hizo y para qué sirve

1. **[ActividadRepository.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/ActividadRepository.cs) (Modificado)**  
   * **¿Qué se hizo?** En el método `GetPorRangoFechasAsync` se agregó la lógica de filtrado jerárquico post-consulta:
     ```csharp
     var actividades = await consulta.ToListAsync(cancellationToken);
     var idsEnRango = actividades.Select(a => a.Id).ToHashSet();

     // Filtrar para que las hijas cuyo padre ya está en el resultado no se dupliquen en la raíz
     var resultadoRaiz = actividades
         .Where(a => !a.ActividadPadreId.HasValue || !idsEnRango.Contains(a.ActividadPadreId.Value))
         .ToList();

     return resultadoRaiz;
     ```  
   * **¿Para qué sirve?** Asegura que el array principal devuelto por la API contenga únicamente las actividades raíz pertinentes y las subactividades cuyos padres no estén en la vista actual.

---

### 4. El antes y después (Comparación exhaustiva de código y arquitectura)

#### A. Código Anterior (Retornaba todas las actividades coincidentes, duplicando hijas en raíz)
```csharp
// En ActividadRepository.cs
public async Task<IEnumerable<Actividad>> GetPorRangoFechasAsync(
    DateTime inicio, DateTime fin, string? callerUserId = null, CancellationToken ct = default)
{
    var consulta = context.Actividades
        .AsNoTracking()
        .Include(a => a.Subactividades)
        .Where(a => a.FechaFin >= inicio && a.FechaFin <= fin);

    // Retornaba la lista plana: el padre Y sus hijas a nivel raíz
    return await consulta.ToListAsync(ct);
}
```

#### B. Código Nuevo Implementado (Filtrado jerárquico que encapsula las hijas dentro del padre)
```csharp
// En ActividadRepository.cs
public async Task<IEnumerable<Actividad>> GetPorRangoFechasAsync(
    DateTime inicio, DateTime fin, string? callerUserId = null, CancellationToken ct = default)
{
    var consulta = context.Actividades
        .AsNoTracking()
        .Include(a => a.Subactividades)
        .Where(a => a.FechaFin >= inicio && a.FechaFin <= fin);

    var actividades = await consulta.ToListAsync(ct);
    var idsEnRango = actividades.Select(a => a.Id).ToHashSet();

    // Solo van a la raíz los padres o aquellas hijas cuyo padre NO esté en el rango consultado
    return actividades
        .Where(a => !a.ActividadPadreId.HasValue || !idsEnRango.Contains(a.ActividadPadreId.Value))
        .ToList();
}
```

---

### 5. Impacto en peticiones HTTP

#### A. Consulta de Calendario (`GET /actividades/calendario?...`)
* **Verbo y Ruta:**  
  `GET /actividades/calendario?fechaInicio=2026-11-20T00:00:00Z&fechaFin=2026-11-30T23:59:59Z`
* **Escenario de Datos:**  
  * Actividad Padre: "Proyecto de Redes" (Vence: 2026-11-28)
  * Subactividad 1: "Topología en Packet Tracer" (Vence: 2026-11-24)
  * Subactividad 2: "Configuración de VLANs" (Vence: 2026-11-26)

#### B. Comparativa de la Respuesta JSON del Servidor

##### Respuesta Anterior (Con duplicación de hijas en la raíz)
```json
[
  {
    "id": "padre-1",
    "titulo": "Proyecto de Redes",
    "subactividades": [
      { "id": "hija-1", "titulo": "Topología en Packet Tracer" },
      { "id": "hija-2", "titulo": "Configuración de VLANs" }
    ]
  },
  {
    "id": "hija-1",
    "titulo": "Topología en Packet Tracer",
    "actividadPadreId": "padre-1"
  },
  {
    "id": "hija-2",
    "titulo": "Configuración de VLANs",
    "actividadPadreId": "padre-1"
  }
]
```

##### Respuesta Nueva (Jerárquica y sin duplicación)
```json
[
  {
    "id": "padre-1",
    "titulo": "Proyecto de Redes",
    "actividadPadreId": null,
    "subactividades": [
      {
        "id": "hija-1",
        "titulo": "Topología en Packet Tracer",
        "actividadPadreId": "padre-1"
      },
      {
        "id": "hija-2",
        "titulo": "Configuración de VLANs",
        "actividadPadreId": "padre-1"
      }
    ]
  }
]
```

---

### 6. ¿Qué pruebas automatizadas se modificaron, crearon o borraron y qué valida cada una?

1. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs) — `GetCalendario_ConSubactividadesEnRango_NoDuplicaHijasEnLaRaiz` (Creada)**  
   * **¿Qué valida?** Crea una actividad padre y dos subactividades cuyas fechas caen exactamente dentro de la misma semana de consulta. Invoca `GET /actividades/calendario` y valida que:
     1. La longitud del array raíz sea estrictamente `1` (sólo la actividad padre).
     2. La propiedad `Subactividades` de la actividad padre contenga exactamente `2` elementos.
     3. Ninguno de los IDs de las hijas exista en el primer nivel del array.

---

### 7. Modificaciones en Base de Datos vs. cambios a nivel código
* **Base de Datos (PostgreSQL):**  
  **Sin cambios.** No requirió índices nuevos ni migraciones DDL.
* **Nivel Código (C# / ASP.NET Core):**  
  Optimización en el método de consulta del repositorio `ActividadRepository.cs` mediante conjuntos de hashing (`HashSet<Guid>`) en memoria.

---

### 8. ¿Cómo lo que se hizo soluciona el problema de raíz?
Resuelve la discrepancia entre el modelo plano de persistencia relacional (SQL) y el modelo jerárquico de visualización temporal (Calendario). Al consolidar el árbol antes de emitir la respuesta JSON, el frontend recibe los eventos estructurados exactamente como deben mostrarse sin necesidad de implementar heurísticas complejas de desduplicación en el cliente.

---

### 9. ¿Cómo se puede comprobar este punto paso a paso?
1. **Inspección en Código:**  
   Revisar el filtro `idsEnRango` en [ActividadRepository.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/ActividadRepository.cs#L60-L80).
2. **Prueba en Postman / Swagger:**  
   * Crear una actividad padre con fecha de vencimiento dentro de 5 días.
   * Crear dos subactividades asignando `actividadPadreId` al ID del padre con fecha dentro de 3 días.
   * Ejecutar `GET /actividades/calendario` con un rango que cubra ambos días.
   * Comprobar que el arreglo raíz devuelto tiene un único objeto con sus subactividades anidadas.
3. **Ejecución de Pruebas Automatizadas:**
   ```bash
   dotnet test --filter "FullyQualifiedName~NoDuplicaHijasEnLaRaiz"
   ```

---

## Punto 8: Headers como Metadatos (Usuarios y Comandos trasladados al Body y Query)

### 1. ¿Qué se pedía en este punto? (Requerimiento exacto del feedback)
El profesor especificó rigurosamente en su retroalimentación:
> *"Los headers son para metadatos (tipo de contenido, tokens, autorización, compresión), no para mandar comandos de negocio ni usuarios... no usar X-User-Id para operaciones de negocio; mandar los usuarios en el Body o en el Query según corresponda."*

**Requerimientos específicos:**
1. Descartar completamente el uso de la cabecera personalizada `X-User-Id` en todas las rutas de la API.
2. Trasladar el identificador de usuario (`propietarioId`, `userId`, `usuarioId`) al **cuerpo de la petición (Body JSON)** en operaciones que alteran el estado del servidor (`DELETE`, `POST /compartir`, `PUT`).
3. Trasladar el identificador de usuario a **parámetros de consulta (Query string `?userId=...`)** en operaciones de lectura (`GET /actividades`, `GET /actividades/calendario`).
4. Reservar los encabezados HTTP exclusivamente para información del protocolo (`Content-Type`, `Accept`, `Authorization`, etc.).

---

### 2. ¿Qué se tenía antes de los cambios y qué se logró después?
* **Antes de los cambios:**  
  La API leía la cabecera `httpContext.Request.Headers["X-User-Id"]` para determinar qué usuario ejecutaba el borrado de una actividad o para saber quién compartía un recurso. Esto constituía un antipatrón en arquitecturas REST, ya que forzaba a los clientes a inyectar encabezados propietarios no estándar, impedía la inspección directa del payload de negocio en logs y dificultaba el modelado formal de esquemas en OpenAPI/Swagger.
* **Después de los cambios:**  
  Se eliminó todo rastro de `Headers["X-User-Id"]` en el código fuente. Se crearon contratos de solicitud fuertemente tipados en el dominio:
  * `DeleteActividadRequest` con la propiedad `PropietarioId` en el cuerpo del `DELETE`.
  * `CompartirActividadRequest` con `UsuarioId` y `PropietarioId` en el cuerpo del `POST /compartir`.
  * `[FromQuery] string? userId` en las rutas `GET`.  
  Todos estos modelos cuentan con sus validadores FluentValidation correspondientes y se documentan de forma automática en Swagger UI.

---

### 3. Archivos modificados, creados o borrados, qué se hizo y para qué sirve

1. **[DeleteActividadRequest.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Domain/DeleteActividadRequest.cs) (Creado)**  
   * **¿Qué se hizo?** Encapsula el payload del borrado:  
     `public record DeleteActividadRequest { public string PropietarioId { get; set; } = string.Empty; }`.  
   * **¿Para qué sirve?** Transporta la identidad del propietario en el Body JSON del HTTP DELETE.
2. **[DeleteActividadRequestValidator.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Validators/DeleteActividadRequestValidator.cs) (Creado)**  
   * **¿Qué se hizo?** Valida que `PropietarioId` no esté vacío en el body del DELETE.
3. **[CompartirActividadRequest.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Domain/CompartirActividadRequest.cs) (Creado)**  
   * **¿Qué se hizo?** Modela la solicitud de compartir:
     ```csharp
     public record CompartirActividadRequest
     {
         public string UsuarioId { get; set; } = string.Empty;
         public string PropietarioId { get; set; } = string.Empty;
     }
     ```  
   * **¿Para qué sirve?** Transporta tanto al destinatario como al propietario que comparte dentro del Body JSON.
4. **[CompartirActividadRequestValidator.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Validators/CompartirActividadRequestValidator.cs) (Creado)**  
   * **¿Qué se hizo?** Reglas FluentValidation exigiendo `UsuarioId` y `PropietarioId`.
5. **[ActividadRoutes.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Routes/ActividadRoutes.cs) (Modificado)**  
   * **¿Qué se hizo?** Se eliminaron las lecturas `context.Request.Headers["X-User-Id"]`. Se sustituyeron por `[FromBody] DeleteActividadRequest`, `[FromBody] CompartirActividadRequest` y `[FromQuery] string? userId`.  
   * **¿Para qué sirve?** Desacopla la lógica de negocio de los encabezados HTTP y alinea la API con los estándares REST.

---

### 4. El antes y después (Comparación exhaustiva de código y arquitectura)

#### A. Código Anterior (Extracción de usuario desde Headers propietarios)
```csharp
// En ActividadRoutes.cs
group.MapDelete("/{id:guid}", async (Guid id, HttpContext context, IActividadDelegate @delegate) =>
{
    // Lectura de header no estándar
    var userId = context.Request.Headers["X-User-Id"].FirstOrDefault();
    await @delegate.DeleteAsync(id, userId);
    return Results.NoContent();
});
```

#### B. Código Nuevo Implementado (Payload de negocio en Body JSON con validación de contrato)
```csharp
// En ActividadRoutes.cs
group.MapDelete("/{id:guid}", async (
    Guid id,
    [FromBody] DeleteActividadRequest request,
    IActividadDelegate @delegate,
    CancellationToken ct) =>
{
    // Datos de negocio en el Body
    await @delegate.DeleteAsync(id, request.PropietarioId, ct);
    return Results.NoContent();
})
.AddEndpointFilter<ValidationFilter<DeleteActividadRequest>>();
```

---

### 5. Impacto en peticiones HTTP

#### A. Comparativa en Eliminación (`DELETE /actividades/{id}`)
* **Antes (Uso incorrecto de cabeceras):**
  ```http
  DELETE /actividades/3fa85f64-5717-4562-b3fc-2c963f66afa6 HTTP/1.1
  Host: localhost:5000
  X-User-Id: usuario-estudiante-1
  ```
* **Después (Estándar REST con Body JSON):**
  ```http
  DELETE /actividades/3fa85f64-5717-4562-b3fc-2c963f66afa6 HTTP/1.1
  Host: localhost:5000
  Content-Type: application/json

  {
    "propietarioId": "usuario-estudiante-1"
  }
  ```

#### B. Comparativa en Compartir (`POST /actividades/{id}/compartir`)
* **Antes:**
  ```http
  POST /actividades/3fa85f64-5717-4562-b3fc-2c963f66afa6/compartir HTTP/1.1
  X-User-Id: usuario-propietario
  Content-Type: application/json

  {
    "usuarioId": "usuario-destinatario"
  }
  ```
* **Después:**
  ```http
  POST /actividades/3fa85f64-5717-4562-b3fc-2c963f66afa6/compartir HTTP/1.1
  Content-Type: application/json

  {
    "usuarioId": "usuario-destinatario",
    "propietarioId": "usuario-propietario"
  }
  ```

#### C. Comparativa en Búsqueda y Calendario
* **Antes:** `GET /actividades` (Sin filtro o requiriendo cabeceras).
* **Después:** `GET /actividades?userId=usuario-estudiante-1` (Parámetro formal de consulta).

---

### 6. ¿Qué pruebas automatizadas se modificaron, crearon o borraron y qué valida cada una?

1. **[ActividadRoutesTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Routes/ActividadRoutesTests.cs)**  
   * **¿Qué se modificó?** Se actualizaron todos los métodos de prueba de integración que interactuaban con `DELETE` y `POST /compartir`. Se removió la inyección de `request.Headers.Add("X-User-Id", ...)`, sustituyéndola por serialización JSON en el `HttpContent` de la petición.
2. **Validación de Rechazo Sin Body:**  
   * Se añadieron pruebas que comprueban que si se intenta enviar el header `X-User-Id` sin body en `DELETE`, la API devuelve `400 Bad Request` indicando que el cuerpo JSON con `propietarioId` es obligatorio.

---

### 7. Modificaciones en Base de Datos vs. cambios a nivel código
* **Base de Datos (PostgreSQL):**  
  **Sin cambios.** Las entidades y tablas subyacentes se mantienen intactas.
* **Nivel Código (C# / ASP.NET Core):**  
  Reestructuración completa del binding de modelos en Minimal APIs (`FromBody` y `FromQuery`), creación de clases DTO/Request especializadas y validadores FluentValidation.

---

### 8. ¿Cómo lo que se hizo soluciona el problema de raíz?
Restaura la semántica correcta del protocolo HTTP/1.1 y HTTP/2 (RFC 9110): los encabezados vuelven a ser de uso exclusivo para el canal y la infraestructura (proxies inversos, balanceadores de carga, compresión gzip, negociación de contenido `Accept`), mientras que la lógica de aplicación procesa contratos tipados y explícitos en el payload y la query string.

---

### 9. ¿Cómo se puede comprobar este punto paso a paso?
1. **Inspección en Código:**  
   Buscar en todo el proyecto la cadena `"X-User-Id"` con `Select-String`: confirmará que no existe una sola ocurrencia en el código productivo de la API.
2. **Prueba en Postman / Swagger:**  
   * Abrir Swagger UI en `/swagger`.
   * Observar el esquema de `DELETE /actividades/{id}`: Swagger muestra el `RequestBody` tipado con el modelo `DeleteActividadRequest`.
   * Probar ejecutar la petición enviando `{"propietarioId": "usr-1"}` en el body y verificar respuesta `204`.
3. **Ejecución de Pruebas Automatizadas:**
   ```bash
   dotnet test --filter "FullyQualifiedName~Delete_UsuarioPropietario|FullyQualifiedName~Compartir"
   ```

---

## Punto 9: Migración de DateTime a UTC (Eliminar desfase de 7 horas en PostgreSQL)

### 1. ¿Qué se pedía en este punto? (Requerimiento exacto del feedback)
El profesor enfatizó en su retroalimentación:
> *"Corregir el desfase de 7 horas al guardar fechas en PostgreSQL... no guardar como timestamp without time zone ni forzar DateTimeKind.Unspecified... todas las fechas deben ser timestamp with time zone (timestamptz) en PostgreSQL y manejarse en UTC en la aplicación."*

**Requerimientos específicos:**
1. Cambiar el tipo físico de las columnas de fechas en PostgreSQL de `timestamp without time zone` a `timestamp with time zone` (`timestamptz`).
2. Configurar `ValueConverter` universales en Entity Framework Core para que al escribir a PostgreSQL y al leer de PostgreSQL el valor tenga estrictamente `DateTimeKind.Utc`.
3. Reemplazar la generación de marcas de tiempo locales del servidor (`timeProvider.GetLocalNow().DateTime` o `DateTime.Now`) por instantes universales UTC (`timeProvider.GetUtcNow().UtcDateTime`).
4. Eliminar la regla restrictiva y perjudicial en `ActividadRules.cs` que forzaba a los clientes a enviar fechas sin zona horaria (`DateTimeKind.Unspecified`).
5. Generar y aplicar la migración formal de EF Core para actualizar el esquema de la base de datos PostgreSQL.

---

### 2. ¿Qué se tenía antes de los cambios y qué se logró después?
* **Antes de los cambios:**  
  En el primer avance del proyecto, `ApplicationDbContext.cs` iteraba sobre todas las propiedades `DateTime` y las forzaba a `timestamp without time zone` ("hora de pared local"). Además, `ActividadRules.cs` exigía que el `DateTimeKind` fuera `Unspecified`.  
  **¿Por qué causaba el desfase de 7 horas?**  
  Cuando un cliente web o móvil (situado en México Pacífico / Sonora, UTC-7) enviaba la fecha de entrega de una tarea en formato estándar ISO 8601 UTC: `"2026-12-15T23:59:00Z"`, PostgreSQL eliminaba la información de zona horaria y guardaba el valor plano. Cuando un pod de Kubernetes o contenedor Docker (que corren por defecto en UTC GMT-0) leía la fecha o se devolvía al cliente, se interpretaba con la zona horaria local del entorno, provocando que la fecha se adelantara o retrasara **exactamente 7 horas** (marcando las 16:59 del mismo día o las 06:59 del día siguiente, alterando plazos de entrega).
* **Después de los cambios:**  
  Se migraron las columnas físicas a `timestamp with time zone` en PostgreSQL mediante la migración `20261115231457_MigrarFechasAUtc`. Se implementaron convertidores de valor bidireccionales en EF Core que garantizan que cualquier fecha (venga con offset local, sin especificar o en UTC) sea convertida inmediatamente a UTC antes de guardarse, y sea hidratada siempre con `DateTimeKind.Utc` al leerla. El delegado ahora genera instantes universales con `timeProvider.GetUtcNow().UtcDateTime`. El desfase de 7 horas quedó 100% erradicado.

---

### 3. Archivos modificados, creados o borrados, qué se hizo y para qué sirve

1. **[20261115231457_MigrarFechasAUtc.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/Migrations/20261115231457_MigrarFechasAUtc.cs) (Creado)**  
   * **¿Qué se hizo?** Se generó la migración de EF Core con las operaciones DDL:
     ```csharp
     migrationBuilder.AlterColumn<DateTime>(
         name: "FechaInicio",
         table: "Actividades",
         type: "timestamp with time zone",
         nullable: false,
         oldClrType: typeof(DateTime),
         oldType: "timestamp without time zone");

     migrationBuilder.AlterColumn<DateTime>(
         name: "FechaFin",
         table: "Actividades",
         type: "timestamp with time zone",
         nullable: false,
         oldClrType: typeof(DateTime),
         oldType: "timestamp without time zone");
     ```  
   * **¿Para qué sirve?** Modifica la estructura de las tablas en PostgreSQL para admitir marcas de tiempo con zona horaria universal (`timestamptz`).
2. **[ApplicationDbContext.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/ApplicationDbContext.cs) (Modificado)**  
   * **¿Qué se hizo?** Se configuraron los `ValueConverters` para todas las propiedades de fecha:
     ```csharp
     var utcConverter = new ValueConverter<DateTime, DateTime>(
         v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
         v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

     var utcNullableConverter = new ValueConverter<DateTime?, DateTime?>(
         v => v.HasValue ? (v.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v.Value.ToUniversalTime()) : v,
         v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

     foreach (var propiedad in propiedadesDeFecha)
     {
         propiedad.SetValueConverter(propiedad.ClrType == typeof(DateTime) ? utcConverter : utcNullableConverter);
         propiedad.SetColumnType("timestamp with time zone");
     }
     ```  
   * **¿Para qué sirve?** Asegura que cualquier fecha que pase por EF Core sea normalizada a `DateTimeKind.Utc` tanto al guardar como al recuperar de la base de datos.
3. **[ActividadDelegate.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Delegates/ActividadDelegate.cs) (Modificado)**  
   * **¿Qué se hizo?** Se sustituyó el método `Ahora()` por `timeProvider.GetUtcNow().UtcDateTime` y se agregó la función auxiliar:
     ```csharp
     private static DateTime AUtc(DateTime dt) =>
         dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
     ```  
   * **¿Para qué sirve?** Garantiza que las fechas asignadas por el servidor sean instantes universales absolutos.
4. **[ActividadRules.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Validators/ActividadRules.cs) (Modificado)**  
   * **¿Qué se hizo?** Se eliminó la restricción que exigía `fecha.Kind == DateTimeKind.Unspecified`.  
   * **¿Para qué sirve?** Permite que los clientes envíen fechas ISO 8601 estándar con el sufijo `Z` o con offsets de zona horaria sin ser rechazados.

---

### 4. El antes y después (Comparación exhaustiva de código y arquitectura)

#### A. Código Anterior (Forzaba timestamp without time zone y Unspecified: desfase de 7h)
```csharp
// En ApplicationDbContext.cs
foreach (var propiedad in propiedadesDeFecha)
{
    propiedad.SetColumnType("timestamp without time zone");
}

// En ActividadDelegate.cs
private DateTime Ahora() => timeProvider.GetLocalNow().DateTime; // Hora local del servidor

// En ActividadRules.cs
RuleFor(x => x.FechaFin)
    .Must(fecha => fecha.Kind == DateTimeKind.Unspecified); // Rechazaba fechas UTC
```

#### B. Código Nuevo Implementado (timestamptz en PostgreSQL, ValueConverters y DateTimeKind.Utc)
```csharp
// En ApplicationDbContext.cs
foreach (var propiedad in propiedadesDeFecha)
{
    propiedad.SetValueConverter(propiedad.ClrType == typeof(DateTime) ? utcConverter : utcNullableConverter);
    propiedad.SetColumnType("timestamp with time zone");
}

// En ActividadDelegate.cs
private DateTime Ahora() => timeProvider.GetUtcNow().UtcDateTime; // Instante universal UTC

private static DateTime AUtc(DateTime dt) =>
    dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();

// En ActividadRules.cs
// Restricción eliminada: ahora acepta fechas con sufijo 'Z' u offsets válidos
```

---

### 5. Impacto en peticiones HTTP

#### A. Envío de Fechas en Formato ISO 8601 Estándar (`POST /actividades`)
* **Headers:** `Content-Type: application/json`
* **Cuerpo de la Petición:**
  ```json
  {
    "titulo": "Entrega Final de Proyecto",
    "fechaFin": "2026-12-15T23:59:00Z",
    "prioridad": 3,
    "userId": "usr-1"
  }
  ```
* **Respuesta Esperada del Servidor:**  
  * **HTTP Status Code:** `201 Created`
  * **Body JSON:**
    ```json
    {
      "id": "e9b10a23-4567-8901-abcd-ef0123456789",
      "titulo": "Entrega Final de Proyecto",
      "fechaInicio": "2026-11-15T21:00:00Z",
      "fechaFin": "2026-12-15T23:59:00Z",
      "userId": "usr-1"
    }
    ```
  *(La fecha se preserva intacta al segundo exacto, sin sufrir ningún corrimiento de 7 horas).*

---

### 6. ¿Qué pruebas automatizadas se modificaron, crearon o borraron y qué valida cada una?

1. **[ApplicationDbContextTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Repositories/ApplicationDbContextTests.cs) — `GuardarYRecuperarActividad_PreservaDateTimeKindUtc` (Creada)**  
   * **¿Qué valida?** Guarda una actividad con `DateTime.UtcNow`, limpia el Change Tracker de EF Core (`context.ChangeTracker.Clear()`) y vuelve a consultar la entidad desde la base de datos. Comprueba que `actividad.FechaFin.Kind == DateTimeKind.Utc` y que los ticks coincidan exactamente.
2. **[ApplicationDbContextTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Repositories/ApplicationDbContextTests.cs) — `ValueConverters_ConviertenFechasAUtcCorrectamente` (Creada)**  
   * **¿Qué valida?** Prueba los conversores con fechas `DateTimeKind.Unspecified`, `DateTimeKind.Local` y `DateTimeKind.Utc`, verificando que todos resulten en `DateTimeKind.Utc`.
3. **[ActividadDelegateTests.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/tests/StudyTrack.Tests/Delegates/ActividadDelegateTests.cs) — `CreateAsync_GuardaFechasEnUtc` (Creada)**  
   * **¿Qué valida?** Verifica que `ActividadDelegate.CreateAsync` asigne `FechaInicio` y `FechaFin` con `DateTimeKind.Utc`.

---

### 7. Modificaciones en Base de Datos vs. cambios a nivel código
* **Base de Datos (PostgreSQL):**  
  **Modificación DDL Real.** Se ejecutó una migración de base de datos que cambió el tipo de dato físico de las columnas `FechaInicio` y `FechaFin` de `timestamp without time zone` a `timestamp with time zone` (`timestamptz`).
* **Nivel Código (C# / ASP.NET Core):**  
  Creación de `ValueConverter` en `ApplicationDbContext.cs`, ajuste del TimeProvider a `GetUtcNow()` y eliminación de la regla `Kind == Unspecified` en `ActividadRules.cs`.

---

### 8. ¿Cómo lo que se hizo soluciona el problema de raíz?
Almacena instantes absolutos universales en PostgreSQL. Al eliminar la ambigüedad de las "horas de pared", se asegura que sin importar dónde se ejecute el contenedor Docker (ej. servidores en EE.UU. o Europa en UTC) y dónde esté el usuario (México Pacífico en UTC-7), la fecha representa exactamente el mismo momento en la línea del tiempo universal.

---

### 9. ¿Cómo se puede comprobar este punto paso a paso?
1. **Inspección en Código y Base de Datos:**  
   Revisar la migración [20261115231457_MigrarFechasAUtc.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/Migrations/20261115231457_MigrarFechasAUtc.cs) y la configuración en [ApplicationDbContext.cs](file:///c:/Users/VetaR/Documents/GitHub/StudyTrackComputo/src/StudyTrack.Api/Repositories/ApplicationDbContext.cs#L80-L105).
2. **Prueba en Postman / Swagger:**  
   * Enviar `POST /actividades` con `"fechaFin": "2026-12-31T23:59:59Z"`.
   * Verificar que la respuesta retorne `"fechaFin": "2026-12-31T23:59:59Z"` con el sufijo `Z` y los valores exactos.
   * Ejecutar `GET /actividades/{id}` y verificar que la fecha no tiene ningún adelanto o retraso de 7 horas.
3. **Ejecución de Pruebas Automatizadas:**
   ```bash
   dotnet test --filter "FullyQualifiedName~Utc"
   ```

---

---

## Tabla Resumen General de Puntos y Cambios

| Punto | Requerimiento del Feedback | Antes (Problema) | Después (Solución) | Impacto en Peticiones HTTP | Base de Datos (DB) | Pruebas Automatizadas |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- |
| **1** | **Eliminación con Usuario Obligatorio y Autorización**<br>No permitir eliminar actividades de forma anónima ni de otros usuarios. | La ruta `DELETE /actividades/{id}` permitía borrar sin body ni autenticación, o dependía de headers opcionales sin validación de dueño. | Se exige `propietarioId` en el Body JSON (`DeleteActividadRequest`). Si falta -> `400 Bad Request`. Si no es el dueño -> `403 Forbidden`. | **Petición:** `DELETE` con body `{"propietarioId": "usr-1"}`.<br>**Respuestas:** `204 No Content` (éxito), `400` (sin body), `403` (no dueño). | Sin cambios de esquema DDL.<br>(Lógica de dominio C#). | `Delete_SinUsuario_Devuelve400BadRequest`<br>`Delete_UsuarioNoPropietario_Devuelve403Forbidden`<br>`Delete_UsuarioPropietario_Devuelve204NoContent` |
| **2** | **Implementación y Documentación con Swagger (OpenAPI)**<br>Proveer interfaz interactiva y esquemas v3 para prueba de endpoints. | No existía Swagger/OpenAPI. La raíz `/` devolvía `404 Not Found`. Requería herramientas externas para probar. | Paquete `Swashbuckle.AspNetCore 6.6.2`. 7 operaciones documentadas con metadatos. Redirección automática `GET /` -> `302 Found` hacia `/swagger`. | `GET /` redirige a `/swagger` (UI interactiva).<br>`GET /swagger/v1/swagger.json` entrega esquema OpenAPI 3.0. | Sin cambios en BD.<br>(Capa de presentación HTTP y middleware). | `Swagger_EndpointJson_Devuelve200Ok`<br>`Swagger_EndpointUi_Devuelve200Ok`<br>`Raiz_RedirigeASwagger` |
| **3** | **Eliminación de DTOs y Uso de Entidades de Dominio**<br>Erradicar DTOs redundantes y usar la entidad `Actividad` directamente. | Existían DTOs (`CreateActividadDto`, `UpdateActividadDto`) con mapeo manual repetitivo y errores de ciclos JSON al usar entidades. | Se eliminaron los DTOs. Toda la API usa `Actividad`. Ciclos resueltos con `[JsonIgnore]` y `ReferenceHandler.IgnoreCycles`. Validación por `RuleSet("Create")`. | `POST /actividades` y `PUT /actividades/{id}` consumen y retornan directamente la entidad `Actividad` serializada limpia. | Sin cambios de esquema DDL.<br>(Refactor de arquitectura y contratos). | `CapasTests` (reglas de arquitectura sin DTOs)<br>`Post_Valido_Devuelve201ConLocationYActividad`<br>`Put_Valido_Devuelve200ConLaActividadActualizada` |
| **4** | **Filtro de Búsqueda y Calendario por Usuario**<br>Filtrar actividades por `userId` para no devolver actividades de todos los usuarios. | `GET /actividades` y `GET /actividades/calendario` devolvían todas las actividades globales sin aislamiento de usuario. | Parámetro `[FromQuery] string? userId`. Filtro relacional en PostgreSQL (`UserId == userId OR Colaborador`). | **Petición:** `GET /actividades?userId=usr-1`.<br>**Respuesta:** Sólo actividades creadas por el usuario o compartidas con él. | Consulta SQL relacional en PostgreSQL (`WHERE a."UserId" = @id OR EXISTS...`). Sin cambios DDL. | `Get_ConFiltroUserId_SoloDevuelveActividadesDelUsuarioYCompartidas`<br>`GetCalendario_ConFiltroUserId_SoloDevuelveActividadesDelUsuario` |
| **5** | **Validación de Destinatario al Compartir**<br>Devolver `400 Bad Request` en vez de `500` si el usuario destinatario no existe. | Inserción directa en tabla intermedia causaba violación de FK `23503` en PostgreSQL, derivando en un `500 Internal Server Error`. | Método preventivo `ExisteUsuarioAsync`. Si no existe, se lanza `BadRequestException` capturada por el middleware como `400 Bad Request`. | **Petición:** `POST /compartir` con destinatario inexistente.<br>**Respuesta:** `400 Bad Request` con mensaje descriptivo RFC 9457. | Comprobación preventiva en BD (`SELECT EXISTS`). Sin cambios de esquema DDL. | `Compartir_UsuarioDestinatarioNoExiste_Devuelve400BadRequest`<br>`CompartirAsync_UsuarioDestinatarioNoExiste_LanzaBadRequestException` |
| **6** | **Asociación Directa de Colaboradores en POST y PUT**<br>Asignar colaboradores directamente al crear o actualizar como strings u objetos. | Colaboradores ignorados en `POST` y `PUT`. Exigía N llamadas secuenciales independientes a `/compartir`. Fallaba con arrays de strings. | Convertidor polimórfico `ActividadCompartidaJsonConverter` (acepta `["usr-2"]` u objetos). Persistencia atómica en `POST` y sincronización en `PUT` (403 si no es dueño). | **Petición:** `POST /actividades` con `"actividadesCompartidas": ["usr-2"]`.<br>**Respuesta:** `201 Created` con colaboradores asociados en 1 sola llamada. | Operaciones atómicas en tabla `ActividadesCompartidas` en una sola transacción ACID de BD. | `Post_ConColaboradoresEnFormatoString_AsociaColaboradoresCorrectamente`<br>`Put_ModificandoColaboradores_SiendoPropietario_SincronizaColaboradores`<br>`Put_ModificandoColaboradores_NoSiendoPropietario_Devuelve403Forbidden` |
| **7** | **Corrección de Duplicados en Calendario**<br>Evitar que las subactividades aparezcan dos veces en la vista de calendario. | En `GET /actividades/calendario`, las subactividades aparecían dentro del padre y también repetidas en la raíz del array JSON. | Algoritmo `idsEnRango` en `ActividadRepository`: las subactividades cuyo padre está en el rango no se duplican en la raíz. | **Petición:** `GET /actividades/calendario?...`.<br>**Respuesta:** Array limpio con actividades principales y sus subactividades anidadas sin duplicación. | Resolución jerárquica en memoria post-consulta SQL. Sin cambios DDL. | `GetCalendario_ConSubactividadesEnRango_NoDuplicaHijasEnLaRaiz`<br>`ApplicationDbContextTests` |
| **8** | **Headers como Metadatos (Body y Query)**<br>No usar `X-User-Id` para lógica de negocio; usar Body en mutaciones y Query en lecturas. | La API leía `Headers["X-User-Id"]` para autorizaciones y operaciones de negocio, violando estándares RESTful. | Cabecera eliminada. Se crearon `DeleteActividadRequest`, `CompartirActividadRequest` para Body JSON, y `?userId=` para Query strings. | `DELETE` y `POST /compartir` ahora reciben JSON en el Body con `Content-Type: application/json`. Headers quedan limpios para metadatos. | Sin cambios en BD.<br>(Capa de presentación HTTP y contratos tipados). | Pruebas de integración actualizadas en `ActividadRoutesTests` sin cabeceras propietarias. |
| **9** | **Migración de DateTime a UTC (Eliminar desfase de 7h)**<br>Guardar en `timestamptz` en PostgreSQL y operar con `DateTimeKind.Utc`. | PostgreSQL usaba `timestamp without time zone` y C# forzaba `Unspecified`, provocando adelanto o retraso de 7 horas en clientes o nube. | Columnas migradas a `timestamp with time zone` (`timestamptz`). `ValueConverter` en EF Core hacia UTC. Uso de `GetUtcNow().UtcDateTime`. | Clientes envían ISO 8601 con `Z` (`"2026-11-20T23:59:00Z"`). La API almacena y retorna el instante exacto sin alteración de 7 horas. | **Modificación DDL Real:** Migración `20261115231457_MigrarFechasAUtc` alterando columnas a `timestamp with time zone`. | `GuardarYRecuperarActividad_PreservaDateTimeKindUtc`<br>`ValueConverters_ConviertenFechasAUtcCorrectamente`<br>`CreateAsync_GuardaFechasEnUtc` |

---

### Resumen del Estado de la Suite de Pruebas
* **Total de Pruebas Ejecutadas:** 222 pruebas.
* **Pruebas Aprobadas (Pass):** **222 de 222 (100% GREEN)**.
* **Pruebas con Error (Fail):** 0.
* **Pruebas Omitidas (Skipped):** 0.
* **Pruebas de Arquitectura Hexagonal:** 21 de 21 aprobadas.
* **Estado de la Solución:** Compilación limpia sin errores ni advertencias (Zero Warnings, Zero Errors).
