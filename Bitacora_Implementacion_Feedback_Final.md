# Bitácora de Implementación y Control de Cambios: Requerimientos Finales
## Proyecto StudyTrack API — Evaluación Final de Cómputo en la Nube

> **Propósito de este documento:**
> Registrar cada uno de los cambios realizados para atender los 9 puntos de retroalimentación del profesor.
> Para cada punto se detalla qué se pedía, qué había antes y qué hay ahora, los archivos intervenidos, el impacto en las peticiones HTTP (con ejemplos reales), las pruebas automatizadas, los cambios en base de datos y cómo comprobarlo paso a paso.
> Todo lo descrito corresponde al código actual del repositorio: los nombres de archivos, campos y pruebas son los reales y los ejemplos se ejecutaron contra la API.
> Al final hay una **Tabla Resumen General de Puntos** y el estado de la suite de pruebas.

---

## Contrato común a todos los puntos

Estas reglas se aplican en todos los endpoints y se repiten en varios puntos:

| Regla | Detalle |
|---|---|
| Usuario solicitante | Siempre se llama **`userId`**. En `GET` y `DELETE` viaja en la query string (`?userId=`); en `POST` y `PUT` viaja en el cuerpo JSON. Es obligatorio: si falta o tiene formato inválido la API responde `400`. |
| Headers | Solo metadatos (`Content-Type`, `Accept`, ...). La API no lee `X-User-Id` ni ningún otro header para identificar al usuario. |
| Fechas | ISO 8601 **con zona horaria** (`Z` u offset). Una fecha sin zona responde `400`. La API guarda y devuelve UTC. |
| Orden de errores | `400` (formato del ID, `userId`, cuerpo) → `404` (no existe) → `403` (no autorizado) → `422` (regla de negocio). |
| Errores | `application/problem+json` con `type`, `title`, `status` y `errors` (validación) o `detail`. |

---

## Índice General de Puntos de Implementación

1. [Punto 1: Eliminación de Actividades con Usuario Obligatorio y Autorización](#punto-1-eliminación-de-actividades-con-usuario-obligatorio-y-autorización) — **[IMPLEMENTADO]**
2. [Punto 2: Implementación y Documentación con Swagger (OpenAPI)](#punto-2-implementación-y-documentación-con-swagger-openapi) — **[IMPLEMENTADO]**
3. [Punto 3: Eliminación de DTOs y Uso de Entidades de Dominio (`Actividad`)](#punto-3-eliminación-de-dtos-y-uso-de-entidades-de-dominio-actividad) — **[IMPLEMENTADO]**
4. [Punto 4: Filtro de Búsqueda y Calendario por Usuario](#punto-4-filtro-de-búsqueda-y-calendario-por-usuario) — **[IMPLEMENTADO]**
5. [Punto 5: Validación de Usuario Destinatario al Compartir (Error 400 en vez de 500)](#punto-5-validación-de-usuario-destinatario-al-compartir-error-400-en-vez-de-500) — **[IMPLEMENTADO]**
6. [Punto 6: Asociación Directa de Colaboradores en Creación y Actualización (`POST`/`PUT`)](#punto-6-asociación-directa-de-colaboradores-en-creación-y-actualización-postput) — **[IMPLEMENTADO]**
7. [Punto 7: Corrección de Duplicados en Calendario (Hijas en Raíz)](#punto-7-corrección-de-duplicados-en-calendario-hijas-en-raíz) — **[IMPLEMENTADO]**
8. [Punto 8: Headers como Metadatos (Usuarios trasladados al Body y Query)](#punto-8-headers-como-metadatos-usuarios-trasladados-al-body-y-query) — **[IMPLEMENTADO]**
9. [Punto 9: Migración de DateTime a UTC (Eliminar desfase de 7 horas en PostgreSQL)](#punto-9-migración-de-datetime-a-utc-eliminar-desfase-de-7-horas-en-postgresql) — **[IMPLEMENTADO]**
10. [Tabla Resumen General de Puntos y Cambios](#tabla-resumen-general-de-puntos-y-cambios)

---

## Punto 1: Eliminación de Actividades con Usuario Obligatorio y Autorización

### 1. ¿Qué se pedía en este punto?
> *"Eliminar la actividad... bajo ninguna circunstancia se debe poder eliminar una actividad sin usuario... no permitir la eliminación anónima ni de actividades que no le pertenezcan."*

1. Exigir el identificador del usuario que solicita la eliminación.
2. Si no viene o viene en blanco, rechazar con **400 Bad Request**.
3. Si el usuario no es el dueño de la actividad, responder **403 Forbidden**.
4. Solo el dueño puede eliminar: baja lógica (*soft delete*) y **204 No Content**.

### 2. ¿Qué se tenía antes y qué se logró después?
* **Antes:** `DELETE /actividades/{id}` tomaba el usuario de un header opcional `X-User-Id`. Sin header, la actividad se eliminaba igual: cualquiera que conociera el ID podía borrarla.
* **Después:** el usuario viaja en la query string, `DELETE /actividades/{id}?userId=...`, y es obligatorio. `UserIdQueryFilter` rechaza la petición con `400` antes de llegar al delegate si falta o tiene formato inválido. `ActividadDelegate.DeleteAsync` comprueba que el solicitante sea el dueño (`403` si no lo es) y marca como eliminadas la actividad y todo su subárbol, con la hora del `TimeProvider`.
* **¿Por qué en la query y no en el cuerpo?** RFC 9110 no define semántica para un cuerpo en `DELETE`: Swagger UI no lo envía y algunos proxies o clientes HTTP lo descartan. La query string llega siempre y queda documentada en Swagger como parámetro obligatorio.

### 3. Archivos modificados, creados o borrados
| Archivo | Cambio | Para qué sirve |
|---|---|---|
| `src/StudyTrack.Api/Extensions/UserIdQueryFilter.cs` | Creado | Filtro de endpoint que exige `?userId=` (obligatorio, ≤ 64 caracteres, formato `[A-Za-z0-9-]+`) y responde `400` con `errors.userId`. |
| `src/StudyTrack.Api/Routes/ActividadRoutes.cs` | Modificado | `DELETE` recibe `[FromQuery, Required] string? userId` y aplica `IdFormatFilter` y `UserIdQueryFilter`. |
| `src/StudyTrack.Api/Delegates/IActividadDelegate.cs` / `ActividadDelegate.cs` | Modificado | `DeleteAsync(string id, string callerUserId)`: el usuario es obligatorio y solo el dueño puede eliminar. |
| `src/StudyTrack.Api/Repositories/IActividadRepository.cs` / `ActividadRepository.cs` | Modificado | `DeleteAsync(actividad, ahora)`: la fecha de eliminación la decide el delegate con `TimeProvider` (antes era `DateTime.Now`). |

### 4. El antes y después
```csharp
// Antes (ActividadRoutes.cs): el header era opcional y sin el se eliminaba igual
private static async Task<IResult> DeleteAsync(string actividadId,
    [FromHeader(Name = "X-User-Id")] string? callerUserId, IActividadDelegate actividadDelegate, ...)

// Ahora (ActividadRoutes.cs)
actividades.MapDelete($"/{{{IdParametro}}}", DeleteAsync)
    .AddEndpointFilter(new IdFormatFilter(IdParametro))
    .AddEndpointFilter<UserIdQueryFilter>();

private static async Task<IResult> DeleteAsync(string actividadId,
    [FromQuery, Required] string? userId, IActividadDelegate actividadDelegate, CancellationToken ct)
{
    await actividadDelegate.DeleteAsync(actividadId, userId!, ct);
    return Results.NoContent();
}

// Ahora (ActividadDelegate.cs)
if (!actividad.EsPropietario(callerUserId))
    throw new ForbiddenException("Solo el propietario puede eliminar la actividad.");
await repository.DeleteAsync(actividad, Ahora(), cancellationToken);
```

### 5. Impacto en peticiones HTTP
| Petición | Respuesta |
|---|---|
| `DELETE /actividades/{id}?userId=usuario-1` (dueño) | `204 No Content`, sin cuerpo |
| `DELETE /actividades/{id}?userId=usuario-ajeno` | `403` — `"detail": "Solo el propietario puede eliminar la actividad."` |
| `DELETE /actividades/{id}` (sin usuario, aunque se mande `X-User-Id` o un cuerpo) | `400` — `"errors": { "userId": ["El parametro de consulta 'userId' es obligatorio."] }` |
| `DELETE /actividades/no-existe?userId=usuario-1` | `404` |
| `DELETE /actividades/id_invalido?userId=usuario-1` | `400` — `errors.actividadId` |

### 6. Pruebas automatizadas
| Prueba | Valida |
|---|---|
| `ActividadRoutesTests.Delete_Existente_Devuelve204SinCuerpoYEliminaSusDescendientes` | El dueño elimina; la actividad y su hija responden `404` después. |
| `ActividadRoutesTests.Delete_SinUsuario_Devuelve400YNoElimina` | Sin `userId` → `400` y la actividad sigue existiendo. |
| `ActividadRoutesTests.Delete_SinUsuarioSobreUnIdInexistente_Devuelve400AntesQue404` | El `400` se evalúa antes que el `404`. |
| `ActividadRoutesTests.Delete_UsuarioSoloEnHeaderOBody_Devuelve400` | Ni `X-User-Id` ni un cuerpo sustituyen a `?userId=`. |
| `ActividadRoutesTests.Delete_UsuarioNoDuenio_Devuelve403Forbidden` / `Delete_ColaboradorNoDuenio_Devuelve403Forbidden` | Un tercero o un colaborador reciben `403`. |
| `ActividadDelegateTests.DeleteAsync_SinUsuario_LanzaBadRequestException` | El delegate también rechaza un usuario vacío. |
| `ActividadDelegateTests.DeleteAsync_UsaLaHoraDelTimeProvider` | La fecha de eliminación sale del `TimeProvider`. |

### 7. Base de datos vs. código
Sin cambios de esquema: las columnas `Eliminada` y `FechaEliminacion` ya existían (migración `AgregarSoftDeleteActividad`). El cambio es de contrato HTTP y de reglas en el delegate.

### 8. ¿Cómo soluciona el problema de raíz?
La eliminación ya no puede ocurrir sin identificar al solicitante: el filtro la detiene antes de tocar datos, y el delegate solo permite que la ejecute el dueño.

### 9. ¿Cómo comprobarlo paso a paso?
```bash
curl -X POST http://localhost:8080/actividades -H "Content-Type: application/json" \
  -d '{"userId":"usr-estudiante-1","titulo":"Para borrar","fechaFin":"2026-12-20T10:00:00Z"}'   # copiar el id
curl -i -X DELETE "http://localhost:8080/actividades/{id}"                                      # 400
curl -i -X DELETE "http://localhost:8080/actividades/{id}?userId=usr-infiltrado"                # 403
curl -i -X DELETE "http://localhost:8080/actividades/{id}?userId=usr-estudiante-1"              # 204
dotnet test --filter "FullyQualifiedName~Delete"
```

---

## Punto 2: Implementación y Documentación con Swagger (OpenAPI)

### 1. ¿Qué se pedía en este punto?
> *"Documentación y prueba interactiva de endpoints con Swagger / OpenAPI... proveer una interfaz accesible donde se listen las rutas, sus modelos de entrada y salida, y sea posible probar la API sin herramientas externas."*

### 2. ¿Qué se tenía antes y qué se logró después?
* **Antes:** no había Swagger; `GET /` respondía `404`.
* **Después:** `Swashbuckle.AspNetCore 6.6.2` publica la especificación OpenAPI 3.0 en `/swagger/v1/swagger.json` y Swagger UI en `/swagger`, y `GET /` redirige (`302`) a `/swagger`. Los 9 endpoints (7 de actividades y 2 de usuarios) tienen nombre, resumen, descripción, cuerpo y códigos de respuesta. Los parámetros `userId` aparecen como obligatorios y ya no se documenta ningún header `X-User-Id`.
* **Entornos:** Swagger se publica siempre en `Development`. En cualquier otro entorno (el clúster corre como `Production`) solo se publica si la configuración `Swagger:Habilitado` es `true`. En `kubernetes/api.yml` se agregó la variable `Swagger__Habilitado=true` para que esté disponible en el clúster de forma explícita.

### 3. Archivos
| Archivo | Cambio |
|---|---|
| `src/StudyTrack.Api/StudyTrack.Api.csproj` | `Swashbuckle.AspNetCore` 6.6.2. |
| `src/StudyTrack.Api/Extensions/ApiServiceCollectionExtensions.cs` | `AddEndpointsApiExplorer()` y `AddSwaggerGen(...)` con título "StudyTrack API". |
| `src/StudyTrack.Api/Extensions/ApiApplicationBuilderExtensions.cs` | `UseApiSwagger()`: `UseSwagger()` + `UseSwaggerUI()` en `/swagger`. |
| `src/StudyTrack.Api/Program.cs` | Publica Swagger y la redirección de `/` solo en `Development` o con `Swagger:Habilitado`. |
| `src/StudyTrack.Api/appsettings.json` | `"Swagger": { "Habilitado": false }`. |
| `kubernetes/api.yml` | Variable `Swagger__Habilitado=true` en el Deployment. |
| `src/StudyTrack.Api/Routes/ActividadRoutes.cs` / `UsuarioRoutes.cs` | `WithName`, `WithSummary`, `WithDescription`, `Accepts`, `Produces` y `ProducesProblem` por endpoint. |

### 4. Código
```csharp
// Program.cs
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Habilitado"))
{
    app.UseApiSwagger();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}
```

### 5. Impacto en peticiones HTTP
| Petición | Development, o Production con `Swagger:Habilitado=true` | Production sin la opción |
|---|---|---|
| `GET /` | `302` → `/swagger` | `404` |
| `GET /swagger` | `301` → `/swagger/index.html` → `200 text/html` | `404` |
| `GET /swagger/v1/swagger.json` | `200 application/json`, `"openapi": "3.0.1"` | `404` |

### 6. Pruebas automatizadas
`CrossCuttingRoutesTests`: `Swagger_JsonSpec_Devuelve200OkConEsquemaYEndpoints`, `Swagger_Ui_Devuelve200OkConPaginaHtml`, `Swagger_Raiz_RedirigeASwagger`, `Swagger_JsonSpec_NoDocumentaElHeaderXUserIdEIncluyeUsuarios`, `Swagger_EnProductionSinConfiguracion_NoSePublica` y `Swagger_EnProductionConSwaggerHabilitado_SePublica`.

### 7. Base de datos vs. código
Sin cambios en la base de datos.

### 8. ¿Cómo soluciona el problema de raíz?
La documentación se genera del propio código de los endpoints, así que no se desactualiza, y la API se puede probar desde el navegador. Que sea configurable evita exponer la documentación en un entorno donde no se desea.

### 9. ¿Cómo comprobarlo paso a paso?
```bash
dotnet run --project src/StudyTrack.Api      # http://localhost:8080
curl -i http://localhost:8080/               # 302 Location: /swagger
# Abrir http://localhost:8080/swagger y usar "Try it out" en cualquier endpoint
dotnet test --filter "FullyQualifiedName~Swagger"
```

---

## Punto 3: Eliminación de DTOs y Uso de Entidades de Dominio (`Actividad`)

### 1. ¿Qué se pedía en este punto?
> *"Eliminar los DTOs y usar Entidades de Dominio (Actividad)... eliminar las clases intermedias CreateActividadDto, UpdateActividadDto y hacer que los endpoints y delegados operen directamente con la entidad Actividad."*

### 2. ¿Qué se tenía antes y qué se logró después?
* **Antes:** `Dtos/ActividadDtos.cs` contenía `CreateActividadDto`, `UpdateActividadDto` y `ActividadDto`, con mapeo manual en el delegate y validadores propios.
* **Después:** los DTOs se eliminaron. Los endpoints reciben y devuelven `Actividad`. Como la entidad trae campos que solo debe decidir el servidor, **el delegate los fija al crear y descarta lo que haya enviado el cliente**: `id` (GUID nuevo), `fechaInicio` (hora de creación), `completada = false`, `fechaCompletada = null`, `generadaAutomaticamente = false`, `eliminada = false` y `fechaEliminacion = null`. En `colaboradores`, `actividadId` y `fechaCompartida` también los fija el servidor. `actividadesHijas` debe ir vacío al crear (`400` si no): cada subactividad se crea con su propio `POST` indicando `actividadPadreId`, para que pase por las reglas de dueño, fechas, duplicados y profundidad. En `PUT` solo se copian los campos editables (título, descripción, fecha de fin, tipo, completada, padre y colaboradores).
* La relación padre–hija no forma ciclos al serializar: `[JsonIgnore]` en `Actividad.ActividadPadre` y `ReferenceHandler.IgnoreCycles`.

### 3. Archivos
| Archivo | Cambio |
|---|---|
| `src/StudyTrack.Api/Dtos/ActividadDtos.cs` | Eliminado. |
| `Validators/CreateActividadDtoValidator.cs`, `UpdateActividadDtoValidator.cs`, `CompartirActividadDtoValidator.cs` | Eliminados. |
| `src/StudyTrack.Api/Validators/ActividadValidator.cs` | Creado: un validador para `Actividad`. `userId`, título, descripción, fecha con zona, tipo, padre y colaboradores en cualquier operación; el RuleSet `Create` exige además `actividadesHijas` vacío. |
| `src/StudyTrack.Api/Extensions/ValidationFilter.cs` | Acepta un RuleSet (`Create` o `Update`). |
| `src/StudyTrack.Api/Delegates/ActividadDelegate.cs` | `CreateAsync(Actividad)` fija los campos del servidor; `UpdateAsync` copia solo los editables. |
| `src/StudyTrack.Api/Domain/Actividad.cs` | `[JsonIgnore]` en `ActividadPadre`; `Colaboradores` como colección. |
| `src/StudyTrack.Api/Extensions/ActividadCompartidaJsonConverter.cs` | Convertidor de colaboradores (ver punto 6). Vive en la capa HTTP para que el dominio no dependa del formato JSON. |
| `tests/StudyTrack.Tests/Arquitectura/CapasTests.cs` | Se quitaron las reglas del namespace `Dtos`; las demás reglas siguen igual y la de Routes ahora también prohíbe usar `UsuarioDelegate` directamente. |

### 4. Código
```csharp
// ActividadDelegate.CreateAsync: lo que controla el servidor se fija aqui
actividad.Id = Guid.NewGuid().ToString();
actividad.FechaInicio = Ahora();
actividad.FechaFin = AUtc(actividad.FechaFin);
actividad.Completada = false;
actividad.FechaCompletada = null;
actividad.GeneradaAutomaticamente = false;
actividad.Eliminada = false;
actividad.FechaEliminacion = null;
actividad.ActividadesHijas = new List<Actividad>();
```

### 5. Impacto en peticiones HTTP
```http
POST /actividades
Content-Type: application/json

{ "id": "id-del-cliente", "userId": "usuario-1", "titulo": "Intento", "fechaFin": "2026-12-20T10:00:00Z",
  "fechaInicio": "2020-01-01T00:00:00Z", "eliminada": true, "completada": true, "generadaAutomaticamente": true }
```
Respuesta `201` con `id` = GUID nuevo, `fechaInicio` = hora del servidor, `eliminada: false`, `completada: false` y `generadaAutomaticamente: false`. La actividad es visible en los `GET`.

`POST` con `"actividadesHijas": [ ... ]` → `400` con `errors.actividadesHijas`, y no se crea nada.

### 6. Pruebas automatizadas
`ActividadRoutesTests.Post_Valido_Devuelve201ConLocation`, `Post_FechaInicioEnviadaPorElCliente_SeIgnoraYSeUsaLaHoraDeCreacion`, `Post_CamposDelServidorEnviadosPorElCliente_SeIgnoran`, `Post_ConActividadesHijasAnidadas_Devuelve400YNoCreaNada`, `Put_Valido_Devuelve200ConLaActividadActualizada`; `ActividadDelegateTests.CreateAsync_IgnoraLosCamposQueControlaElServidor`; `CapasTests` (21 pruebas de arquitectura).

### 7. Base de datos vs. código
Sin cambios de esquema: la tabla ya correspondía a la entidad.

### 8. ¿Cómo soluciona el problema de raíz?
Se elimina el código duplicado de los DTOs sin abrir la puerta a que el cliente escriba campos que no le corresponden: el servidor sigue siendo la única fuente de `id`, fechas de sistema y estados internos.

### 9. ¿Cómo comprobarlo paso a paso?
Enviar el `POST` del ejemplo anterior y verificar en la respuesta y en la base que no se guardó nada de lo que controla el servidor. `dotnet test --filter "FullyQualifiedName~CapasTests|FullyQualifiedName~ActividadRoutesTests.Post"`.

---

## Punto 4: Filtro de Búsqueda y Calendario por Usuario

### 1. ¿Qué se pedía en este punto?
> *"Filtro de actividades por usuario en búsquedas y calendario... permitir filtrar por usuario para que no se devuelvan las actividades de todos los usuarios de la base de datos."*

### 2. ¿Qué se tenía antes y qué se logró después?
* **Antes:** `GET /actividades` y `GET /actividades/calendario` devolvían las actividades de todos los usuarios.
* **Después:** `userId` es **obligatorio** (`?userId=`) en `GET /actividades`, `GET /actividades/calendario` y `GET /actividades/{id}`. Sin él la API responde `400`: no existe una consulta que devuelva datos de todos. Cada usuario ve:
  * sus actividades;
  * las que le compartieron;
  * todas las subactividades (a cualquier nivel) de una actividad compartida con él.

  El filtro se ejecuta en PostgreSQL (`ActividadRepository.AccesiblesPor`). El delegate arma el árbol desde la lista plana: es raíz toda actividad cuyo padre no está en el resultado, así que una subactividad compartida sin su padre aparece como raíz en vez de desaparecer. `GET /actividades/{id}` aplica la misma regla: el colaborador de un ancestro también puede ver la actividad (`403` para los demás).

### 3. Archivos
| Archivo | Cambio |
|---|---|
| `Routes/ActividadRoutes.cs` | `[FromQuery, Required] string? userId` + `UserIdQueryFilter` en los tres `GET`. |
| `Delegates/ActividadDelegate.cs` | `GetAllAsync`, `GetCalendarioAsync` y `GetByIdAsync` exigen usuario; `ArmarArbol` y `PuedeAccederAsync` (acceso heredado de ancestros). |
| `Repositories/ActividadRepository.cs` | `AccesiblesPor(query, userId)`: dueño, colaborador o descendiente de una compartida (hasta el bisabuelo, por la profundidad máxima de 3). |

### 4. Código
```csharp
// ActividadRepository.cs (fragmento)
query.Where(a =>
    a.UserId == userId
    || a.Colaboradores!.Any(c => c.UsuarioId == userId)
    || (a.ActividadPadre != null && (a.ActividadPadre.UserId == userId
        || a.ActividadPadre.Colaboradores!.Any(c => c.UsuarioId == userId)
        || /* abuelo y bisabuelo */ ...)));
```

### 5. Impacto en peticiones HTTP
| Petición | Respuesta |
|---|---|
| `GET /actividades?userId=alumno-a` | Solo actividades de `alumno-a`, las compartidas con él y sus subárboles. |
| `GET /actividades` | `400` — `errors.userId` |
| `GET /actividades/calendario?desde=2026-11-01T00:00:00Z&hasta=2026-11-30T23:59:59Z&userId=alumno-a` | Actividades visibles para `alumno-a` cuyo periodo se traslapa con el rango. |
| `GET /actividades/{id}?userId=otro` | `403` si `otro` no es dueño ni colaborador de la actividad o de un ancestro. |

### 6. Pruebas automatizadas
`ActividadRoutesTests.GetAll_ConFiltroUsuario_DevuelveSoloActividadesPropiasYCompartidas`, `GetAll_SinUsuarioOConFormatoInvalido_Devuelve400`, `GetAll_SubactividadCompartidaSinSuPadre_ApareceComoRaizParaElColaborador`, `GetAll_RaizCompartida_IncluyeSusSubactividadesAnidadas`, `GetCalendario_ConFiltroUsuario_DevuelveSoloActividadesEnRangoDelUsuario`, `GetCalendario_SinUsuario_Devuelve400`, `GetById_SinUsuario_Devuelve400`, `GetById_UsuarioAjeno_Devuelve403Forbidden`; `ActividadRepositoryTests.GetAllAsync_SoloDevuelveLasActividadesVisiblesParaElUsuario`, `GetAllAsync_IncluyeTodoElSubarbolDeUnaRaizCompartida`, `GetPorRangoFechasAsync_ExcluyeActividadesDeOtrosUsuarios`; `ActividadDelegateTests.GetByIdAsync_SubactividadDeUnaRaizCompartida_EsAccesibleParaElColaborador`, `GetAllAsync_SubactividadCompartidaSinSuPadre_ApareceComoRaiz`.

### 7. Base de datos vs. código
Sin cambios de esquema. El filtro se traduce a SQL (`WHERE ... OR EXISTS (...)` con `LEFT JOIN` a los ancestros).

### 8. ¿Cómo soluciona el problema de raíz?
Ninguna consulta devuelve datos de otros usuarios: el usuario es obligatorio y el filtro lo aplica la base de datos.

### 9. ¿Cómo comprobarlo paso a paso?
Crear una actividad con `"userId":"alumno-a"` y otra con `"userId":"alumno-b"`; `GET /actividades?userId=alumno-a` solo muestra la primera; `GET /actividades` responde `400`. `dotnet test --filter "FullyQualifiedName~GetAll|FullyQualifiedName~GetCalendario"`.

---

## Punto 5: Validación de Usuario Destinatario al Compartir (Error 400 en vez de 500)

### 1. ¿Qué se pedía en este punto?
> *"Al compartir una actividad si el usuario no existe en la base de datos la petición truena con un error 500 de base de datos por foreign key... debe validar que el usuario exista y devolver un error 400 Bad Request claro."*

### 2. ¿Qué se tenía antes y qué se logró después?
* **Antes:** se insertaba la relación sin validar y la violación de la llave foránea (`23503`) terminaba en `500`.
* **Después:** `ActividadDelegate.CompartirAsync` valida, en este orden, el dueño (`403`), que no se comparta con el propio dueño (`422`) y que el destinatario exista en `Usuarios` con `ExisteUsuarioAsync` (`400` con `errors.usuarioId`). La llave foránea queda solo como respaldo final.
* **Registro de usuarios:** para que haya destinatarios válidos se agregó `POST /usuarios` (`{ id, nombre, email }`, `201`, o `409` si el id ya existe) y `GET /usuarios/{id}`. Antes la tabla `Usuarios` solo podía llenarse a mano en la base de datos.

### 3. Archivos
| Archivo | Cambio |
|---|---|
| `Delegates/Exceptions/BadRequestException.cs` | Excepción de negocio que se traduce a `400` con `errors` por campo. |
| `Extensions/GlobalExceptionHandler.cs` | Traduce `BadRequestException` (`400`) y `ConflictException` (`409`). |
| `Repositories/ActividadRepository.cs` | `ExisteUsuarioAsync` (`SELECT EXISTS`). |
| `Delegates/ActividadDelegate.cs` | Validaciones de `CompartirAsync`. |
| `Domain/CompartirActividadRequest.cs` + `Validators/CompartirActividadRequestValidator.cs` | Cuerpo `{ userId, usuarioId }`, ambos obligatorios. |
| `Routes/UsuarioRoutes.cs`, `Delegates/IUsuarioDelegate.cs`, `Delegates/UsuarioDelegate.cs`, `Repositories/IUsuarioRepository.cs`, `Repositories/UsuarioRepository.cs`, `Validators/UsuarioValidator.cs`, `Delegates/Exceptions/ConflictException.cs` | Creados: registro y consulta de usuarios. |

### 4. Código
```csharp
// ActividadDelegate.CompartirAsync
if (!actividad.EsPropietario(callerUserId))
    throw new ForbiddenException("Solo el propietario puede compartir la actividad con otros usuarios.");
if (usuarioIdDestino == actividad.UserId)
    throw new BusinessRuleException("usuarioId", "No se puede compartir la actividad con su propio creador.");
if (!await repository.ExisteUsuarioAsync(usuarioIdDestino, cancellationToken))
    throw new BadRequestException("usuarioId", $"El usuario destinatario '{usuarioIdDestino}' no existe.");
```

### 5. Impacto en peticiones HTTP
```http
POST /actividades/{id}/compartir
Content-Type: application/json

{ "userId": "usuario-creador-1", "usuarioId": "usuario-fantasma-999" }
```
→ `400` `{ "title": "Validation Failed", "status": 400, "errors": { "usuarioId": ["El usuario destinatario 'usuario-fantasma-999' no existe."] } }`

Con un destinatario registrado → `200` `{ "mensaje": "Actividad compartida exitosamente con 'usuario-companero'." }`

### 6. Pruebas automatizadas
`ActividadRoutesTests.Compartir_UsuarioDestinoNoExiste_Devuelve400BadRequest`, `Compartir_DuenioComparte_Devuelve200Ok`, `Compartir_UsuarioNoDuenio_Devuelve403Forbidden`, `Compartir_SinUsuario_Devuelve400`; `ActividadDelegateTests.CompartirAsync_UsuarioDestinoNoExiste_LanzaBadRequestException` y demás `CompartirAsync_*`; `UsuarioRoutesTests` (registro, 409, validaciones, 404 y compartir con un usuario registrado).

### 7. Base de datos vs. código
Sin cambios de esquema: la tabla `Usuarios` y la llave foránea ya existían (migración `AgregarUsuariosYActividadesCompartidas`).

### 8. ¿Cómo soluciona el problema de raíz?
La precondición se valida antes de escribir, así que el error de la base de datos ya no llega al cliente como `500`.

### 9. ¿Cómo comprobarlo paso a paso?
```bash
curl -X POST http://localhost:8080/usuarios -H "Content-Type: application/json" -d '{"id":"usr-2","nombre":"Beto","email":"beto@test.com"}'
curl -X POST http://localhost:8080/actividades/{id}/compartir -H "Content-Type: application/json" -d '{"userId":"usr-creador","usuarioId":"usuario-falso"}'  # 400
curl -X POST http://localhost:8080/actividades/{id}/compartir -H "Content-Type: application/json" -d '{"userId":"usr-creador","usuarioId":"usr-2"}'          # 200
```

---

## Punto 6: Asociación Directa de Colaboradores en Creación y Actualización (`POST`/`PUT`)

### 1. ¿Qué se pedía en este punto?
> *"Poder asociar colaboradores directamente al crear la actividad o al actualizarla... aceptar tanto un array de strings [\"usr-2\", \"usr-3\"] como un array de objetos [{\"usuarioId\": \"usr-2\"}], validar que existan los colaboradores y que solo el creador pueda modificar los colaboradores en el PUT."*

### 2. ¿Qué se tenía antes y qué se logró después?
* **Antes:** para armar un equipo había que crear la actividad y luego llamar a `/compartir` una vez por colaborador.
* **Después:** `POST /actividades` y `PUT /actividades/{id}` aceptan el campo **`colaboradores`**, como arreglo de textos, de objetos o mezclado.
  * Cada colaborador debe estar registrado en `Usuarios` (`400` si no) y no puede ser el creador (`422`). Los repetidos se guardan una sola vez.
  * Un elemento que no sea texto ni objeto (número, booleano, arreglo) responde `400`, no `500`.
  * En `PUT`, si se envía `colaboradores`, la lista se **reemplaza** (se agregan los nuevos y se quitan los que ya no vienen). Solo el dueño puede hacerlo: un colaborador o un tercero reciben `403`. Si se omite el campo, los colaboradores no cambian; así un colaborador puede editar los datos de la actividad.
  * `PUT` sin `userId` responde `400` y no modifica nada.

### 3. Archivos
| Archivo | Cambio |
|---|---|
| `Extensions/ActividadCompartidaJsonConverter.cs` | Convierte `"usr-2"` o `{ "usuarioId": "usr-2" }` en `ActividadCompartida`. Rechaza otros tokens con `JsonException` (→ `400`). Registrado una sola vez en `ApiServiceCollectionExtensions`. |
| `Validators/ActividadValidator.cs` | `RuleForEach(Colaboradores)`: no nulo, no vacío, formato de ID. |
| `Delegates/ActividadDelegate.cs` | `ValidarColaboradoresAsync` (existencia, creador, repetidos); `ProcesarColaboradoresCreacionAsync` / `ProcesarColaboradoresActualizacionAsync`; la verificación de dueño ocurre antes de modificar cualquier campo. |

### 4. Código
```csharp
// ActividadDelegate.UpdateAsync
if (!await PuedeAccederAsync(actividad, callerUserId, cancellationToken))
    throw new ForbiddenException("No tiene autorizacion para modificar esta actividad.");
if (actividadActualizada.Colaboradores is not null && !actividad.EsPropietario(callerUserId))
    throw new ForbiddenException("Solo el propietario puede modificar los colaboradores de la actividad.");
```

### 5. Impacto en peticiones HTTP
```http
POST /actividades
Content-Type: application/json

{ "userId": "usr-estudiante-1", "titulo": "Investigación en equipo", "fechaFin": "2026-11-28T23:59:00Z",
  "tipo": "Entrega", "colaboradores": ["usr-estudiante-2", { "usuarioId": "usr-estudiante-3" }] }
```
→ `201` con `"colaboradores": [ { "actividadId": "<id>", "usuarioId": "usr-estudiante-2", "fechaCompartida": "..." }, { ... "usr-estudiante-3" ... } ]`

| Petición `PUT /actividades/{id}` | Respuesta |
|---|---|
| Dueño con `"colaboradores": ["usr-estudiante-3"]` | `200`; queda solo `usr-estudiante-3` |
| Colaborador con `"colaboradores": [...]` | `403` |
| Colaborador sin `colaboradores` | `200` (puede editar los datos) |
| Sin `userId` | `400` |

### 6. Pruebas automatizadas
`ActividadRoutesTests.Post_ConColaboradoresEnFormatoString_CreaYDevuelveColaboradores`, `Post_ConColaboradoresEnFormatoObjeto_IgnoraActividadIdYFechaDelCliente`, `Post_ConColaboradorInexistente_Devuelve400BadRequest`, `Post_ColaboradorConFormatoInvalido_Devuelve400NoUn500`, `Put_DuenioActualizaColaboradores_Devuelve200Ok`, `Put_ColaboradorIntentaModificarColaboradores_Devuelve403Forbidden`, `Put_ColaboradorEditaSinTocarColaboradores_Devuelve200`, `Put_SinUsuario_Devuelve400YNoModifica`; `ActividadDelegateTests.CreateAsync_ConColaboradores*`, `CreateAsync_ColaboradoresRepetidos_SeGuardanUnaVez`, `UpdateAsync_*Colaboradores*`.

### 7. Base de datos vs. código
Sin cambios de esquema: se insertan y borran filas de `ActividadesCompartidas` en el mismo `SaveChanges` que la actividad (una transacción).

### 8. ¿Cómo soluciona el problema de raíz?
Una actividad grupal se crea en una sola llamada atómica, y la lista de colaboradores solo la controla el dueño.

### 9. ¿Cómo comprobarlo paso a paso?
Registrar `usr-2` con `POST /usuarios`, enviar el `POST` del ejemplo y verificar `colaboradores` en la respuesta. `dotnet test --filter "FullyQualifiedName~Colaborador"`.

---

## Punto 7: Corrección de Duplicados en Calendario (Hijas en Raíz)

### 1. ¿Qué se pedía en este punto?
> *"En el calendario las subactividades aparecen duplicadas... aparecen como elementos en la raíz del array y también adentro del padre... no deben duplicarse en la raíz si su padre ya está en el resultado."*

### 2. ¿Qué se tenía antes y qué se logró después?
* **Antes:** el calendario devolvía la lista plana, así que una hija salía en la raíz y además dentro de su padre.
* **Después:** el delegate arma el árbol con `ArmarArbol` (la misma función que usa `GET /actividades`). Una hija cuyo padre está en el resultado solo aparece anidada. Si el padre no está (cae fuera del rango), la hija aparece en la raíz para no ocultarle la entrega al estudiante. El filtro del calendario es por **traslape**: aparece toda actividad cuyo periodo `[fechaInicio, fechaFin]` toca el rango `[desde, hasta]`.

### 3. Archivos
`Delegates/ActividadDelegate.cs` (`GetCalendarioAsync` + `ArmarArbol`).

### 4. Código
```csharp
var ids = actividades.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
var raices = OrdenadasParaMostrar(
    actividades.Where(a => a.ActividadPadreId is null || !ids.Contains(a.ActividadPadreId))).ToList();
```

### 5. Impacto en peticiones HTTP
`GET /actividades/calendario?desde=2026-11-20T00:00:00Z&hasta=2026-11-30T23:59:59Z&userId=usr-1`, con un padre y dos hijas en el rango → un solo elemento en la raíz con sus dos hijas dentro de `actividadesHijas`.

### 6. Pruebas automatizadas
`ActividadRoutesTests.GetCalendario_ConJerarquiaPadreEHija_NoDuplicaHijaEnNivelSuperior`; `ActividadDelegateTests.GetCalendarioAsync_ConActividadPadreEHija_NoDuplicaHijaEnNivelSuperior` y `GetCalendarioAsync_HijaEnRangoPeroPadreFueraDeRango_DevuelveHijaEnNivelSuperior`.

### 7. Base de datos vs. código
Sin cambios en la base de datos.

### 8. ¿Cómo soluciona el problema de raíz?
El cliente recibe el árbol tal como debe mostrarse, sin tener que eliminar duplicados por su cuenta.

### 9. ¿Cómo comprobarlo paso a paso?
Crear un padre (`tipo: "Examen"`) y dos hijas con `actividadPadreId`; consultar el calendario con un rango que las cubra y verificar que la raíz tiene un solo elemento. `dotnet test --filter "FullyQualifiedName~NoDuplica"`.

---

## Punto 8: Headers como Metadatos (Usuarios trasladados al Body y Query)

### 1. ¿Qué se pedía en este punto?
> *"Los headers son para metadatos (tipo de contenido, tokens, autorización, compresión), no para mandar comandos de negocio ni usuarios... no usar X-User-Id para operaciones de negocio; mandar los usuarios en el Body o en el Query según corresponda."*

### 2. ¿Qué se tenía antes y qué se logró después?
* **Antes:** todas las rutas leían `X-User-Id` con `[FromHeader]`.
* **Después:** no queda ninguna lectura de `X-User-Id` en el código ni en la especificación de Swagger. El usuario se llama `userId` en todos los endpoints:

| Endpoint | Dónde viaja el usuario |
|---|---|
| `GET /actividades`, `GET /actividades/calendario`, `GET /actividades/{id}` | `?userId=` |
| `DELETE /actividades/{id}` | `?userId=` |
| `POST /actividades` | `userId` en el body (dueño) |
| `PUT /actividades/{id}` | `userId` en el body (solicitante) |
| `POST /actividades/{id}/compartir` | `userId` en el body (dueño) + `usuarioId` (destinatario) |

Una petición que solo trae `X-User-Id` se trata como si no trajera usuario: `400`.

### 3. Archivos
`Routes/ActividadRoutes.cs` (se quitaron todos los `[FromHeader]`), `Extensions/UserIdQueryFilter.cs`, `Domain/CompartirActividadRequest.cs` (`userId` + `usuarioId`, sin alias). Se eliminaron `Domain/DeleteActividadRequest.cs` y `Validators/DeleteActividadRequestValidator.cs`, que no se usaban.

### 4. Código
```csharp
// Antes
[FromHeader(Name = "X-User-Id")] string? headerUserId
// Ahora
[FromQuery, Required] string? userId        // GET y DELETE
actividad.UserId / request.UserId           // POST y PUT (cuerpo)
```

### 5. Impacto en peticiones HTTP
```http
# Antes
DELETE /actividades/3fa85f64 HTTP/1.1
X-User-Id: usuario-estudiante-1

# Ahora
DELETE /actividades/3fa85f64?userId=usuario-estudiante-1 HTTP/1.1
```
```http
# Antes
POST /actividades/3fa85f64/compartir
X-User-Id: usuario-propietario
{ "usuarioId": "usuario-destinatario" }

# Ahora
POST /actividades/3fa85f64/compartir
Content-Type: application/json
{ "userId": "usuario-propietario", "usuarioId": "usuario-destinatario" }
```

### 6. Pruebas automatizadas
`ActividadRoutesTests.GetAll_HeaderXUserId_SeIgnora`, `Delete_UsuarioSoloEnHeaderOBody_Devuelve400`, `Compartir_SinUsuario_Devuelve400` (envía el header y aun así responde `400`); `CrossCuttingRoutesTests.Swagger_JsonSpec_NoDocumentaElHeaderXUserIdEIncluyeUsuarios`.

### 7. Base de datos vs. código
Sin cambios en la base de datos.

### 8. ¿Cómo soluciona el problema de raíz?
Los headers vuelven a ser solo del protocolo, y el dato de negocio queda en un contrato explícito, validado y documentado.

### 9. ¿Cómo comprobarlo paso a paso?
`grep -rn "X-User-Id" src` no encuentra ninguna ocurrencia. `curl -i http://localhost:8080/actividades -H "X-User-Id: usr-1"` responde `400`. `dotnet test --filter "FullyQualifiedName~Header|FullyQualifiedName~Compartir"`.

---

## Punto 9: Migración de DateTime a UTC (Eliminar desfase de 7 horas en PostgreSQL)

### 1. ¿Qué se pedía en este punto?
> *"Corregir el desfase de 7 horas al guardar fechas en PostgreSQL... no guardar como timestamp without time zone ni forzar DateTimeKind.Unspecified... todas las fechas deben ser timestamp with time zone (timestamptz) en PostgreSQL y manejarse en UTC en la aplicación."*

### 2. ¿Qué se tenía antes y qué se logró después?
* **Antes:** las columnas eran `timestamp without time zone`, la API exigía fechas sin zona (horas de pared) y el servidor usaba `GetLocalNow()`. La misma fecha significaba horas distintas según la zona del servidor (contenedor en UTC) y la del estudiante (Tijuana, UTC-8/UTC-7).
* **Después:**
  * Columnas `timestamp with time zone`; EF Core lee y escribe con `DateTimeKind.Utc` (`ValueConverter` en `ApplicationDbContext`).
  * El servidor usa `timeProvider.GetUtcNow()` para `fechaInicio`, `fechaCompletada`, `fechaCompartida` y `fechaEliminacion` (esta última antes usaba `DateTime.Now`).
  * **Las fechas deben traer zona horaria** (`Z` u offset), tanto en el cuerpo (`fechaFin`) como en la query del calendario (`desde`, `hasta`). Una fecha sin zona es ambigua y se rechaza con `400` en lugar de adivinar su zona. Una fecha con offset se convierte al instante UTC equivalente.
  * **Datos existentes:** la migración convierte cada columna con `USING "Columna" AT TIME ZONE 'America/Tijuana'`, porque los valores guardados eran horas de pared de Tijuana. Sin esa cláusula, PostgreSQL usaría la zona de la sesión (UTC en el contenedor) y desplazaría los datos 7 u 8 horas. El `Down` aplica la conversión inversa.

### 3. Archivos
| Archivo | Cambio |
|---|---|
| `Repositories/Migrations/20261115231457_MigrarFechasAUtc.cs` | `ALTER COLUMN ... TYPE timestamp with time zone USING ... AT TIME ZONE 'America/Tijuana'` en `FechaInicio`, `FechaFin`, `FechaCompletada`, `FechaEliminacion` y `FechaCompartida`. |
| `Repositories/ApplicationDbContext.cs` | Tipo de columna `timestamp with time zone` y convertidores a UTC. |
| `Validators/ActividadRules.cs` | `FechaFinValida`: obligatoria y con zona horaria. |
| `Extensions/FechaConsulta.cs` | Lee `desde` y `hasta` del calendario con la misma regla. |
| `Delegates/ActividadDelegate.cs` | `Ahora() => timeProvider.GetUtcNow().UtcDateTime`; conversión a UTC de las fechas recibidas. |
| `Repositories/ActividadRepository.cs` | `DeleteAsync` recibe la hora del delegate (`TimeProvider`). |
| `tests/StudyTrack.Tests/Builders/ActividadBuilder.cs` | `FechaBase` con `DateTimeKind.Utc`. |

### 4. Código
```csharp
// Migracion
migrationBuilder.Sql(
    "ALTER TABLE \"Actividades\" ALTER COLUMN \"FechaFin\" TYPE timestamp with time zone " +
    "USING \"FechaFin\" AT TIME ZONE 'America/Tijuana';");

// ActividadRules.cs
regla.NotEmpty().WithMessage("La fecha de fin es obligatoria.")
    .Must(fecha => fecha == default || TieneZonaHoraria(fecha)).WithMessage(MensajeFechaSinZona);
```

### 5. Impacto en peticiones HTTP
| `fechaFin` enviada | Respuesta |
|---|---|
| `"2026-12-31T23:59:59Z"` | `201`, `"fechaFin": "2026-12-31T23:59:59Z"` |
| `"2026-12-15T23:59:00-08:00"` | `201`, `"fechaFin": "2026-12-16T07:59:00Z"` (el mismo instante) |
| `"2026-12-15T23:59:00"` | `400`, `"errors": { "fechaFin": ["La fecha debe incluir zona horaria (Z u offset), ..."] }` |

**Bordes del día en el calendario:** una tarea que vence el 15/12 a las 23:30 en Tijuana (`-08:00`) es el instante `2026-12-16T07:30Z`. Si el cliente pide el día con su offset (`desde=2026-12-15T00:00:00-08:00&hasta=2026-12-15T23:59:59-08:00`), la tarea aparece el 15 y no el 16.

**Datos existentes (comprobado en la base local):** `2026-09-13 17:16:48` (septiembre, horario de verano, `-07:00`) → `2026-09-14 00:16:48+00`; `2026-12-15 23:59:00` (diciembre, `-08:00`) → `2026-12-16 07:59:00+00`.

### 6. Pruebas automatizadas
`ApplicationDbContextTests.PropiedadesDateTime_SeMapeanATimestampConZonaHorariaUtc`; `ActividadRoutesTests.Post_FechaFinConOffset_SeGuardaComoInstanteUtc`, `Post_CamposFaltantesOInvalidos_Devuelve400` (caso "Sin zona"), `GetCalendario_FechaSinZonaOInvalida_Devuelve400`, `GetCalendario_ConOffset_SeInterpretaComoInstanteUtc`; `ActividadDelegateTests.CreateAsync_FechaInicioEsElMomentoDeCreacion` (`Kind == Utc`), `DeleteAsync_UsaLaHoraDelTimeProvider`.

### 7. Base de datos vs. código
**Sí hay cambio de esquema:** la migración `20261115231457_MigrarFechasAUtc` cambia el tipo de 5 columnas y convierte los datos existentes. Se aplica con `dotnet ef database update`.

> Si una base ya aplicó la versión anterior de esta migración (la que no tenía `USING`), hay que revertirla con el archivo anterior (`dotnet ef database update AgregarUsuariosYActividadesCompartidas`), actualizar el código y volver a aplicarla. La base local se corrigió de esta forma.

### 8. ¿Cómo soluciona el problema de raíz?
Se guardan instantes absolutos, y la API ya no acepta fechas cuya zona tendría que adivinar. La hora se interpreta igual sin importar dónde corran el contenedor o el estudiante.

### 9. ¿Cómo comprobarlo paso a paso?
Enviar `POST /actividades` con `"fechaFin": "2026-12-15T23:59:00-08:00"` y verificar `"2026-12-16T07:59:00Z"`. Repetir sin zona y verificar el `400`. En PostgreSQL: `\d "Actividades"` muestra `timestamp with time zone`. `dotnet test --filter "FullyQualifiedName~Fecha|FullyQualifiedName~Utc|FullyQualifiedName~Offset"`.

---

## Prueba de carga (`load_test/locustfile.py`)

El bot de Locust se reescribió para que actúe como un estudiante real con el mismo contrato que la API:
- se registra con `POST /usuarios` y consulta su perfil;
- crea actividades (algunas con colaboradores, como texto u objeto) y subactividades;
- consulta el listado, el detalle y el calendario con `?userId=` y fechas en UTC con `Z`;
- actualiza y completa sus actividades, reemplaza colaboradores y comparte con otros bots;
- edita como colaborador las actividades que le compartieron;
- elimina sus actividades con `DELETE ?userId=`.

Ninguna petición usa el header `X-User-Id`. Una corrida local de 60 s con 20 bots ejecutó las 12 operaciones (880 peticiones) con 0 fallas y sin errores en el log de la API.

---

## Tabla Resumen General de Puntos y Cambios

| Punto | Requerimiento | Antes | Después | Petición HTTP | Base de datos | Pruebas principales |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- |
| **1** | Eliminar solo con usuario y solo el dueño | Header opcional; sin él se eliminaba igual | `?userId=` obligatorio (`400`), solo el dueño (`403`), soft delete del subárbol (`204`) | `DELETE /actividades/{id}?userId=usr-1` | Sin cambios | `Delete_SinUsuario_Devuelve400YNoElimina`, `Delete_UsuarioNoDuenio_Devuelve403Forbidden`, `Delete_Existente_...` |
| **2** | Swagger / OpenAPI | No existía | Swashbuckle 6.6.2, UI y JSON, `/` → `/swagger`; configurable por entorno, activo en el clúster | `GET /swagger`, `GET /swagger/v1/swagger.json` | Sin cambios | `Swagger_*` (6 pruebas) |
| **3** | Sin DTOs, entidad `Actividad` | DTOs con mapeo manual | Entidad directa; el servidor fija id, fechas y estados; subactividades una por una | `POST`/`PUT /actividades` con `Actividad` | Sin cambios | `Post_CamposDelServidorEnviadosPorElCliente_SeIgnoran`, `CapasTests` |
| **4** | Filtrar por usuario | Se devolvían las de todos | `?userId=` obligatorio; propias, compartidas y subárbol de compartidas; filtro en SQL | `GET /actividades?userId=usr-1` | Sin cambios | `GetAll_ConFiltroUsuario_...`, `GetAll_SubactividadCompartidaSinSuPadre_...` |
| **5** | Destinatario inexistente → 400 | FK 23503 → `500` | `ExisteUsuarioAsync` → `400`; registro con `POST /usuarios` | `POST /actividades/{id}/compartir` | Sin cambios | `Compartir_UsuarioDestinoNoExiste_Devuelve400BadRequest`, `UsuarioRoutesTests` |
| **6** | Colaboradores en POST/PUT | Una llamada a `/compartir` por colaborador | `colaboradores` como texto u objeto; validados; solo el dueño los cambia | `POST /actividades` con `"colaboradores": ["usr-2"]` | Inserción/borrado en `ActividadesCompartidas` en la misma transacción | `Post_ConColaboradores*`, `Put_*Colaboradores*`, `Post_ColaboradorConFormatoInvalido_Devuelve400NoUn500` |
| **7** | Sin duplicados en el calendario | Hijas en la raíz y dentro del padre | Árbol armado en el delegate; hija con padre fuera de rango en la raíz | `GET /actividades/calendario?...` | Sin cambios | `GetCalendario_ConJerarquiaPadreEHija_NoDuplicaHijaEnNivelSuperior` |
| **8** | Headers solo metadatos | `X-User-Id` en todas las rutas | `userId` en query (GET/DELETE) o body (POST/PUT); sin headers | Ver tabla del punto 8 | Sin cambios | `GetAll_HeaderXUserId_SeIgnora`, `Delete_UsuarioSoloEnHeaderOBody_Devuelve400` |
| **9** | UTC y timestamptz | Horas de pared sin zona; desfase de 7 h | `timestamptz`, UTC en la app, fechas con zona obligatorias, migración con `AT TIME ZONE 'America/Tijuana'` | `fechaFin` con `Z` u offset | **Migración** `20261115231457_MigrarFechasAUtc` | `Post_FechaFinConOffset_...`, `GetCalendario_ConOffset_...`, `PropiedadesDateTime_...Utc` |

---

### Resumen del Estado de la Suite de Pruebas
* **Total de pruebas:** 268.
* **Aprobadas:** 268 de 268.
* **Fallidas:** 0. **Omitidas:** 0.
* **Pruebas de arquitectura (`CapasTests`):** 21 de 21.
* **Compilación:** 0 errores, 0 advertencias.
* **Modelo vs. migraciones:** `dotnet ef migrations has-pending-model-changes` → sin cambios pendientes.
