# Investigación II — Patrones en Arquitecturas Distribuidas
## Tema: Event Sourcing (Combate por Turnos)

Proyecto universitario enfocado en la implementación y coordinación de microservicios usando **Event Sourcing** con **EventStoreDB**, **.NET** y **Docker Compose**.

### Estructura general esperada:
- `/service-a`: Servicio de comandos (Persona 2).
- `/service-b`: Servicio de consulta y proyección (Persona 3). Expone `GET /battles`, `GET /battles/{id}`, `GET /battles/{id}/stats` y `GET /battles/{id}/history`.
- `docker-compose.yml`: Orquestación de infraestructura y servicios (Persona 4 y 5).
- `.env`: Puertos (`SERVICE_A_PORT`, `SERVICE_B_PORT`, `EVENTSTORE_PORT`) y `EVENTSTORE_URL`.

## Infraestructura EventStoreDB (Persona 4)

`docker-compose.yml` define el nodo único de **EventStoreDB** usado por ambos servicios. Se ejecuta en modo inseguro **solo para desarrollo local**, con el puerto `2113` publicado y volúmenes nombrados para conservar datos y logs entre reinicios.

1. Iniciá la infraestructura con `docker compose up -d eventstore`.
2. Abrí la UI administrativa en [http://localhost:2113](http://localhost:2113).
3. En el navegador de streams buscá `battle-{id}`; por ejemplo, `battle-demo-1`.

El flujo es: **Service A** recibe comandos y persiste `BattleStarted`, `AttackPerformed` y `HealUsed` en ese stream; **Service B** se suscribe a `$all`, filtra esos streams y materializa la vista de lectura. Por eso, la fuente de verdad es la secuencia de eventos, no el HP mostrado por Service B.

### Contrato de conexión

- Dentro de Docker, ambos servicios deben recibir `EVENTSTORE_URL=esdb://eventstore:2113?tls=false` desde `.env`: `eventstore` es el nombre del servicio de Compose, no `localhost`.
- Desde la máquina host, un cliente local usaría `esdb://localhost:2113?tls=false`.
- La UI y el cliente gRPC comparten el puerto `2113` en esta configuración.

### Handoff para Persona 5

Agregá los bloques `service-a` y `service-b` al mismo `docker-compose.yml`, reutilizando `.env` y la red por defecto de Compose. Cada servicio debe definir `EVENTSTORE_URL: ${EVENTSTORE_URL}` y depender de `eventstore`; no hay que cambiar la lógica de comandos o consultas. El bloque de `eventstore` y sus volúmenes son responsabilidad de Persona 4.

> Límite intencional: `EVENTSTORE_INSECURE=true` deshabilita autenticación y TLS. Es adecuado para la demo local, nunca para producción.
