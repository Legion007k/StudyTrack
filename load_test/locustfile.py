"""Prueba de carga de StudyTrack.

Cada bot es un estudiante que usa todas las funciones que ofrece la API:
se registra, crea actividades y subactividades, las consulta (listado, detalle y calendario),
las actualiza y completa, comparte actividades con otros estudiantes, edita como colaborador
lo que le compartieron y elimina sus propias actividades.

Contrato que respeta (igual que la API):
- El usuario que hace la solicitud siempre es "userId": en la query string para GET y DELETE,
  en el cuerpo JSON para POST y PUT. No se usan headers para identificar al usuario.
- Todas las fechas viajan con zona horaria (UTC con "Z").
"""
import random
from datetime import datetime, timedelta, timezone
from uuid import uuid4

from locust import HttpUser, between, task

TIPOS = ["General", "Examen", "Entrega", "Presentacion"]
MAX_REGISTRADOS = 500  # tope de la lista compartida de estudiantes conocidos


def iso_utc(fecha: datetime) -> str:
    """Fecha en ISO 8601 con zona horaria, como exige la API."""
    return fecha.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def ahora() -> datetime:
    return datetime.now(timezone.utc)


class Estudiante(HttpUser):
    wait_time = between(0.5, 2)

    # Estudiantes registrados por los bots de este proceso: con ellos se comparte y se colabora.
    registrados: list[str] = []

    def on_start(self):
        self.user_id = f"carga-{uuid4()}"
        self.propias: dict[str, dict] = {}       # id -> cuerpo con el que se creo (para el PUT)
        self.compartidas: set[str] = set()       # ids de actividades que otros compartieron conmigo
        self.registrado = False
        self.registrarse()

    # ---------- utilidades ----------

    def otros_estudiantes(self, cantidad: int) -> list[str]:
        candidatos = [u for u in Estudiante.registrados if u != self.user_id]
        return random.sample(candidatos, min(cantidad, len(candidatos)))

    def raices_propias(self) -> list[str]:
        return [i for i, a in self.propias.items() if a["actividadPadreId"] is None]

    def olvidar(self, actividad_id: str):
        """Quita la actividad y sus descendientes conocidos (un DELETE elimina todo el subarbol)."""
        pendientes = [actividad_id]
        while pendientes:
            actual = pendientes.pop()
            self.propias.pop(actual, None)
            pendientes += [i for i, a in self.propias.items() if a["actividadPadreId"] == actual]

    def crear(self, cuerpo: dict, nombre: str):
        with self.client.post("/actividades", json=cuerpo, name=nombre, catch_response=True) as r:
            if r.status_code == 201:
                creada = r.json()
                self.propias[creada["id"]] = {
                    "titulo": creada["titulo"],
                    "descripcion": creada.get("descripcion"),
                    "fechaFin": cuerpo["fechaFin"],
                    "tipo": creada["tipo"],
                    "actividadPadreId": creada.get("actividadPadreId"),
                    "completada": False,
                }
                r.success()
            elif r.status_code == 422 and cuerpo.get("actividadPadreId"):
                # El padre pudo eliminarse mientras tanto: es un resultado valido de negocio.
                self.olvidar(cuerpo["actividadPadreId"])
                r.success()
            else:
                r.failure(f"{r.status_code}: {r.text[:200]}")

    # ---------- registro ----------

    def registrarse(self):
        cuerpo = {"id": self.user_id, "nombre": f"Estudiante {self.user_id[-6:]}", "email": f"{self.user_id}@carga.test"}
        with self.client.post("/usuarios", json=cuerpo, name="POST /usuarios", catch_response=True) as r:
            if r.status_code in (201, 409):
                self.registrado = True
                Estudiante.registrados.append(self.user_id)
                del Estudiante.registrados[:-MAX_REGISTRADOS]
                r.success()
            else:
                r.failure(f"{r.status_code}: {r.text[:200]}")

    @task(1)
    def ver_mi_perfil(self):
        if not self.registrado:
            self.registrarse()
            return
        self.client.get(f"/usuarios/{self.user_id}", name="GET /usuarios/[id]")

    # ---------- crear ----------

    @task(3)
    def crear_actividad(self):
        cuerpo = {
            "userId": self.user_id,
            "titulo": f"Tarea {uuid4()}",
            "descripcion": "Generada por la prueba de carga",
            "fechaFin": iso_utc(ahora() + timedelta(days=random.randint(1, 45), hours=random.randint(0, 23))),
            "tipo": random.choice(TIPOS),
        }
        # A veces se crea en equipo: colaboradores en la misma solicitud, como texto o como objeto.
        otros = self.otros_estudiantes(random.randint(1, 2)) if random.random() < 0.3 else []
        if otros:
            cuerpo["colaboradores"] = [u if random.random() < 0.5 else {"usuarioId": u} for u in otros]
        self.crear(cuerpo, "POST /actividades")

    @task(2)
    def crear_subactividad(self):
        raices = self.raices_propias()
        if not raices:
            return
        padre_id = random.choice(raices)
        cuerpo = {
            "userId": self.user_id,
            "titulo": f"Subtarea {uuid4()}",
            "fechaFin": iso_utc(ahora() + timedelta(days=random.randint(1, 30))),
            "tipo": "General",
            "actividadPadreId": padre_id,
        }
        self.crear(cuerpo, "POST /actividades (subactividad)")

    # ---------- consultar ----------

    @task(4)
    def listar(self):
        with self.client.get("/actividades", params={"userId": self.user_id},
                             name="GET /actividades", catch_response=True) as r:
            if r.status_code != 200:
                r.failure(f"{r.status_code}: {r.text[:200]}")
                return
            # Las actividades de otros dueños que aparecen son las que me compartieron.
            self.compartidas = {a["id"] for a in r.json() if a["userId"] != self.user_id}
            r.success()

    @task(4)
    def ver_detalle(self):
        candidatos = list(self.propias) + list(self.compartidas)
        if not candidatos:
            return
        actividad_id = random.choice(candidatos)
        with self.client.get(f"/actividades/{actividad_id}", params={"userId": self.user_id},
                             name="GET /actividades/[id]", catch_response=True) as r:
            if r.status_code == 200:
                r.success()
            elif r.status_code in (403, 404) and actividad_id in self.compartidas:
                # El dueño la elimino o me quito como colaborador.
                self.compartidas.discard(actividad_id)
                r.success()
            elif r.status_code == 404:
                self.olvidar(actividad_id)  # su padre se elimino en cascada
                r.success()
            else:
                r.failure(f"{r.status_code}: {r.text[:200]}")

    @task(3)
    def ver_calendario(self):
        desde = ahora()
        params = {"desde": iso_utc(desde), "hasta": iso_utc(desde + timedelta(days=30)), "userId": self.user_id}
        self.client.get("/actividades/calendario", params=params, name="GET /actividades/calendario")

    # ---------- actualizar ----------

    @task(2)
    def actualizar_propia(self):
        if not self.propias:
            return
        actividad_id = random.choice(list(self.propias))
        datos = self.propias[actividad_id]
        datos["completada"] = not datos["completada"]
        cuerpo = {"userId": self.user_id, **datos, "titulo": f"{datos['titulo'].split(' (')[0]} (editada)"}
        with self.client.put(f"/actividades/{actividad_id}", json=cuerpo,
                             name="PUT /actividades/[id]", catch_response=True) as r:
            if r.status_code == 200:
                datos["titulo"] = cuerpo["titulo"]
                r.success()
            elif r.status_code == 404:
                self.olvidar(actividad_id)
                r.success()
            else:
                r.failure(f"{r.status_code}: {r.text[:200]}")

    @task(1)
    def editar_como_colaborador(self):
        if not self.compartidas:
            return
        actividad_id = random.choice(list(self.compartidas))
        with self.client.get(f"/actividades/{actividad_id}", params={"userId": self.user_id},
                             name="GET /actividades/[id]", catch_response=True) as r:
            if r.status_code != 200:
                self.compartidas.discard(actividad_id)
                r.success()
                return
            actual = r.json()
            r.success()
        # Un colaborador puede editar los datos, pero no la lista de colaboradores (no se envia).
        cuerpo = {
            "userId": self.user_id,
            "titulo": actual["titulo"],
            "descripcion": f"Revisada por {self.user_id[-6:]}",
            "fechaFin": actual["fechaFin"],
            "tipo": actual["tipo"],
            "completada": actual["completada"],
            "actividadPadreId": actual["actividadPadreId"],
        }
        with self.client.put(f"/actividades/{actividad_id}", json=cuerpo,
                             name="PUT /actividades/[id] (colaborador)", catch_response=True) as r:
            if r.status_code in (200, 403, 404):
                r.success()
            else:
                r.failure(f"{r.status_code}: {r.text[:200]}")

    @task(1)
    def cambiar_colaboradores(self):
        raices = self.raices_propias()
        if not raices:
            return
        actividad_id = random.choice(raices)
        cuerpo = {"userId": self.user_id, **self.propias[actividad_id],
                  "colaboradores": self.otros_estudiantes(random.randint(0, 2))}
        with self.client.put(f"/actividades/{actividad_id}", json=cuerpo,
                             name="PUT /actividades/[id] (colaboradores)", catch_response=True) as r:
            if r.status_code == 200:
                r.success()
            elif r.status_code == 404:
                self.olvidar(actividad_id)
                r.success()
            else:
                r.failure(f"{r.status_code}: {r.text[:200]}")

    # ---------- compartir ----------

    @task(2)
    def compartir(self):
        raices = self.raices_propias()
        otros = self.otros_estudiantes(1)
        if not raices or not otros:
            return
        actividad_id = random.choice(raices)
        cuerpo = {"userId": self.user_id, "usuarioId": otros[0]}
        with self.client.post(f"/actividades/{actividad_id}/compartir", json=cuerpo,
                              name="POST /actividades/[id]/compartir", catch_response=True) as r:
            if r.status_code == 200:
                r.success()
            elif r.status_code == 404:
                self.olvidar(actividad_id)
                r.success()
            else:
                r.failure(f"{r.status_code}: {r.text[:200]}")

    # ---------- eliminar ----------

    @task(1)
    def eliminar(self):
        # Se conserva una base de actividades para que el resto de tareas tenga con que trabajar.
        if len(self.propias) < 6:
            return
        actividad_id = random.choice(list(self.propias))
        with self.client.delete(f"/actividades/{actividad_id}", params={"userId": self.user_id},
                                name="DELETE /actividades/[id]", catch_response=True) as r:
            if r.status_code in (204, 404):
                self.olvidar(actividad_id)
                r.success()
            else:
                r.failure(f"{r.status_code}: {r.text[:200]}")
