# Investigación II — Patrones en Arquitecturas Distribuidas
## Tema: Event Sourcing (Combate por Turnos)

Proyecto universitario enfocado en la implementación y coordinación de microservicios usando **Event Sourcing** con **EventStoreDB**, **.NET** y **Docker Compose**.

### Estructura general esperada:
- `/service-a`: Servicio de comandos (Persona 2).
- `/service-b`: Servicio de consulta y proyección (Persona 3). Expone `GET /battles`, `GET /battles/{id}`, `GET /battles/{id}/stats` y `GET /battles/{id}/history`.
- `docker-compose.yml`: Orquestación de infraestructura y servicios (Persona 4 y 5).
- `.env`: Puertos (`SERVICE_A_PORT`, `SERVICE_B_PORT`, `EVENTSTORE_PORT`) y `EVENTSTORE_URL`.
