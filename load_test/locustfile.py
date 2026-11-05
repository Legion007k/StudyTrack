"""Prueba de carga de StudyTrack para observar el escalado del HPA."""
import random
from datetime import datetime, timedelta
from uuid import uuid4

from locust import HttpUser, between, task

FORMATO_FECHA = "%Y-%m-%dT%H:%M:%S"  # sin zona horaria, como exige el contrato


class Estudiante(HttpUser):
    wait_time = between(0.5, 2)

    def on_start(self):
        self.user_id = f"carga-{uuid4()}"
        self.ids = []

    @property
    def cabeceras(self):
        return {"X-User-Id": self.user_id}

    @task(1)
    def crear_actividad(self):
        cuerpo = {
            "userId": self.user_id,
            "titulo": f"Tarea {uuid4()}",
            "fechaFin": (datetime.now() + timedelta(days=30)).strftime(FORMATO_FECHA),
            "tipo": "Entrega",
        }
        with self.client.post("/actividades", json=cuerpo, name="POST /actividades",
                              catch_response=True) as respuesta:
            if respuesta.status_code == 201:
                self.ids.append(respuesta.json()["id"])
                respuesta.success()
            else:
                respuesta.failure(f"{respuesta.status_code}: {respuesta.text[:200]}")

    @task(3)
    def leer_actividad(self):
        if self.ids:
            self.client.get(f"/actividades/{random.choice(self.ids)}",
                            headers=self.cabeceras, name="GET /actividades/[id]")

    @task(2)
    def ver_calendario(self):
        desde = datetime.now()
        hasta = desde + timedelta(days=60)
        self.client.get("/actividades/calendario",
                        params={"desde": desde.strftime(FORMATO_FECHA), "hasta": hasta.strftime(FORMATO_FECHA)},
                        name="GET /actividades/calendario")

    @task(1)
    def listar_todo(self):
        self.client.get("/actividades", name="GET /actividades")
