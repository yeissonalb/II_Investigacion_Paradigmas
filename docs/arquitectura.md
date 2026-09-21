# Arquitectura del Sistema: Event Sourcing

> **Documento de Arquitectura y Contrato de Integración**  
> **Proyecto:** Investigación II — Patrones y Mecanismos en Arquitecturas Distribuidas  
> **Tema:** Event Sourcing con CQRS  
> **Integrantes:** Fatima Carrillo Garcia, Samir Campos Díaz, Maria del Mar Diaz Ruiz, Brayan José Pérez Balladares, Yeisson Alberto Villalobos Toruño

---

## 1. ¿Qué es Event Sourcing?

**Event Sourcing** es un patrón de diseño arquitectónico en el que los cambios en el estado de una aplicación se capturan y almacenan no como el valor actual de los registros (como se hace en un CRUD tradicional), sino como una **secuencia inmutable de eventos ordenados en el tiempo** que representan hechos que ya ocurrieron (*append-only log*).

En una base de datos convencional, cuando un usuario actualiza su saldo o realiza una acción, la base de datos ejecuta un `UPDATE`, destruyendo el estado anterior y dejando únicamente el último valor conocido. En **Event Sourcing**, el estado nunca se sobreescribe ni se elimina:
- Cada acción significativa es un evento de negocio en tiempo pasado (ejemplo: `AccountOpened`, `MoneyDeposited`, `MoneyWithdrawn` o `BattleStarted`, `AttackPerformed`).
- El estado actual de cualquier entidad no es una fila en una tabla, sino la **suma acumulada de todos sus eventos pasados**.
- Para conocer el estado actual, el sistema realiza un **replay** (reproducción) de dichos eventos desde el inicio de la línea temporal.

> *"La fuente de la verdad son los eventos ocurridos, nunca una foto estática del estado final."*

---

## 2. ¿Qué problema resuelve? (Situación Real)

### El problema en arquitecturas tradicionales (CRUD sobreescrito)
Imagine una aplicación bancaria tradicional con una tabla `Cuentas` que tiene las columnas `Id` y `Saldo`.
1. Si un cliente tiene \$100, deposita \$50 y luego retira \$30, la base de datos simplemente tiene un registro: `Saldo: 120`.
2. Si al final del mes el cliente reclama que le faltan \$20, el sistema no tiene forma de demostrar internamente qué pasó a partir de esa tabla; debe recurrir a tablas secundarias de logs o auditorías que muchas veces se desincronizan o fallan si no están en la misma transacción.
3. El estado perdió la **intención de negocio**: no se sabe *por qué* cambió el saldo, qué operación falló, ni el orden exacto en caso de condiciones de carrera distribuidas.

### Cómo lo resuelve Event Sourcing
Event Sourcing elimina la pérdida de información:
- **Trazabilidad y Auditoría Nativa:** El log de eventos *es* la base de datos principal. No existe forma de que una acción ocurra sin quedar registrada de por vida.
- **Viaje en el tiempo (Time-Travel Debugging):** Es posible reconstruir el estado exacto de una entidad en cualquier fecha o minuto del pasado (ej. "¿cuál era el estado exacto de la cuenta el 15 de marzo a las 14:02?").
- **Facilidad para crear nuevas proyecciones:** Si meses después de lanzar el sistema el equipo de analítica o marketing necesita una métrica nueva (ej. "promedio de depósitos por fin de semana"), no es necesario haber diseñado esa tabla desde el día 1; basta con leer los eventos desde el inicio de los tiempos y calcular la nueva vista.

---

## 3. ¿Cómo funciona? Flujo de Comunicación

El sistema implementa el desacoplamiento de responsabilidades mediante **CQRS (Command Query Responsibility Segregation)**:

```
                          [ Cliente HTTP / Frontend ]
                                  |           ^
                     Comandos     |           | Consultas
                     (POST)       v           | (GET)
                            +-----------+   +-----------+
                            | Service A |   | Service B |
                            | (Command) |   |  (Query)  |
                            | Port 3001 |   | Port 3002 |
                            +-----------+   +-----------+
                                  |               ^
                   Valida replay  |               | Suscripción en tiempo
                   y hace Append  v               | real a $all (Read Model)
                            +---------------------------+
                            |       EventStoreDB        |
                            |        (Puerto 2113)      |
                            |   Streams inmutables      |
                            +---------------------------+
```

### Paso a paso del flujo:
1. **Comando entrante:** El cliente envía una petición `POST` al **Servicio A (Puerto 3001)** para ejecutar una acción (ej. depositar dinero o realizar un ataque).
2. **Replay y Validación de Reglas de Negocio:** El Servicio A lee los eventos previos del stream correspondiente en EventStoreDB para reconstruir el estado actual en memoria y verificar si la operación es válida (ej. si hay fondos suficientes o si el objetivo sigue con vida).
3. **Persistencia del Evento (Append-Only):** Si la validación pasa, el Servicio A emite un evento inmutable y lo agrega al final del stream (`account-{id}` o `battle-{id}`).
4. **Notificación / Proyección asíncrona:** **EventStoreDB** almacena el evento y notifica a los suscriptores conectados.
5. **Consumo y Proyección en Servicio B:** El **Servicio B (Puerto 3002)** está suscrito a la secuencia de eventos. Al recibir el nuevo evento, actualiza su modelo de lectura (*Read Store* optimizado para consultas).
6. **Consulta de estado:** Cuando un usuario consulta `GET` en el Servicio B, la respuesta se entrega de forma inmediata desde la proyección en memoria, con alta velocidad y sin bloquear la base de datos transaccional.

---

## 4. Ventajas y Desventajas

### Principales Ventajas:
1. **Auditoría completa e inalterable:** Cada cambio queda registrado con su fecha, datos y contexto. Cumple con normativas financieras estrictas por diseño.
2. **Sin pérdida de datos por sobreescritura:** Nunca se pierden datos por un `UPDATE` destructivo o un `DELETE` accidental.
3. **Escalabilidad y Rendimiento Asimétrico:** La escritura es extremadamente rápida (operaciones continuas de *append* al final del disco) y la lectura se escala independientemente mediante modelos cacheados o bases de datos de solo lectura.
4. **Capacidad de Corrección y Reproducción:** Si se introduce un bug en el código que calcula una estadística, se corrige el bug y se vuelven a procesar los eventos pasados para regenerar los datos corregidos.

### Principales Limitaciones:
1. **Curva de aprendizaje elevada:** Pensar en eventos requiere un cambio de paradigma total frente al modelo relacional tradicional.
2. **Consistencia Eventual:** Entre el momento en que el Servicio A escribe el evento y el Servicio B lo proyecta existe una pequeña ventana de milisegundos donde la vista de lectura puede estar ligeramente rezagada.
3. **Evolución y versionado de esquemas:** Una vez que un evento se guarda en producción, no puede alterarse. Si cambian los campos requeridos en el futuro, se deben soportar esquemas versionados (ej. `v1`, `v2`, *upcasting*).
4. **Crecimiento del almacenamiento:** Los streams crecen continuamente, requiriendo estrategias de *snapshots* (capturas periódicas) para streams con miles de eventos.

### ¿Cuándo es recomendable utilizarlo?
- Sistemas financieros, bancarios y de pagos.
- Plataformas de logística, seguimiento de envíos e inventarios.
- Sistemas con requisitos regulatorios de auditoría estricta (médicos, jurídicos).
- Sistemas basados en microservicios que requieran reactividad y trazabilidad temporal.

### ¿Cuándo probablemente NO sería necesario?
- Aplicaciones CRUD sencillas (blogs, catálogos estáticos, formularios simples).
- Sistemas donde el estado histórico carece de valor y solo importa el valor actual.
- Proyectos con plazos de entrega extremadamente reducidos donde el equipo no domina arquitecturas guiadas por eventos.

---

## 5. Alternativas Libres y Comparativa Técnica

Para almacenar y procesar eventos existen diversas herramientas de código abierto o gratuitas para entornos locales:

| Criterio | EventStoreDB (Open Source / Local) | PostgreSQL (Append-Only / Marten) | Apache Kafka | RabbitMQ |
| :--- | :--- | :--- | :--- | :--- |
| **Diseño principal** | Base de datos especializada en Event Sourcing | Base de datos relacional multi-propósito | Plataforma distribuida de event streaming | Message Broker (Broker de mensajes) |
| **Soporte de Streams por ID** | Nativo (`account-123`, `battle-1`) | Requiere modelado manual o tablas particionadas | Difícil (los topics no están pensados para millones de streams individuales) | No (las colas son transitorias, los mensajes se eliminan al consumirse) |
| **Control de Concurrencia Optimista** | Nativo (por número de versión esperado del stream) | Se implementa mediante transacciones `SERIALIZABLE` o columnas de versión | Limitado a particiones y offsets | No aplica |
| **Replay histórico** | Nativo desde cualquier posición del stream | Nativo mediante `SELECT ... ORDER BY version` | Nativo rebobinando el offset | Inviable (los mensajes se purgan tras el ACK) |
| **Facilidad de ejecución local** | Alta (imagen Docker oficial lista para Compose) | Alta (imagen oficial de PostgreSQL) | Media-Baja (requiere ZooKeeper o Kraft, mayor consumo) | Alta (imagen Docker ligera) |

### Justificación de EventStoreDB vs PostgreSQL:
- **EventStoreDB:** Fue creada exclusivamente para este propósito. Soporta streams individuales ligeros de forma nativa, suscripciones reactivas gRPC y control de concurrencia optimista sin configuración extra.
- **PostgreSQL (Alternativa recomendada):** Es una alternativa libre excelente. Con extensiones como *Marten* (en .NET) o una simple tabla `events (stream_id, version, event_type, payload JSONB)`, permite construir Event Sourcing sin añadir un motor de base de datos extra, aunque requiere gestionar manualmente las proyecciones y el bloqueo de concurrencia.

---

## 6. Contrato de Integración del Equipo (Persona 1 a Personas 2 y 3)

Para asegurar que **Persona 2 (Servicio A - Comandos)** y **Persona 3 (Servicio B - Consultas)** trabajen coordinados sin conflictos de integración, se establecen las siguientes definiciones formales de contrato:

### 6.1. Configuración de Red y Puertos
- **Servicio A (Command):** Puerto `3001` (HTTP REST).
- **Servicio B (Query):** Puerto `3002` (HTTP REST).
- **EventStoreDB:** Puerto `2113` (HTTP Web UI y gRPC endpoint).
- **URL EventStoreDB interna en Docker:** `esdb://eventstore:2113?tls=false`.
- **URL EventStoreDB desde el host:** `esdb://localhost:2113?tls=false`.

---

### 6.2. Contrato de Eventos y Streams

#### Identificador de Stream:
El identificador de stream sigue la convención:
`account-{id}` (para el dominio bancario) / `battle-{id}` (para el dominio de combate por turnos).

---

#### Especificación Formal: Dominio Bancario (Contrato Base de Cuentas)

1. **`AccountOpened`**
   - Disparado cuando una cuenta es creada.
   - Payload JSON:
     ```json
     {
       "accountId": "acc-101",
       "owner": "Yeisson",
       "initialBalance": 1000.0,
       "openedAt": "2026-09-20T22:00:00Z"
     }
     ```

2. **`MoneyDeposited`**
   - Disparado cuando se ingresa dinero.
   - Payload JSON:
     ```json
     {
       "accountId": "acc-101",
       "amount": 250.0,
       "resultingBalance": 1250.0,
       "depositedAt": "2026-09-20T22:05:00Z"
     }
     ```

3. **`MoneyWithdrawn`**
   - Disparado cuando se retira dinero tras validar fondos suficientes.
   - Payload JSON:
     ```json
     {
       "accountId": "acc-101",
       "amount": 100.0,
       "resultingBalance": 1150.0,
       "withdrawnAt": "2026-09-20T22:10:00Z"
     }
     ```

---

#### Especificación Implementada en el Repositorio: Combate por Turnos

1. **`BattleStarted`**
   - Stream: `battle-{id}`
   - Payload:
     ```json
     {
       "battleId": "battle-demo-1",
       "heroName": "Guerrero",
       "heroHp": 100,
       "enemyName": "Dragón",
       "enemyHp": 100,
       "startedAt": "2026-09-20T22:00:00Z"
     }
     ```

2. **`AttackPerformed`**
   - Stream: `battle-{id}`
   - Payload:
     ```json
     {
       "battleId": "battle-demo-1",
       "attacker": "Hero",
       "target": "Dragón",
       "damage": 25,
       "targetRemainingHp": 75,
       "isCritical": false,
       "occurredAt": "2026-09-20T22:01:00Z"
     }
     ```

3. **`HealUsed`**
   - Stream: `battle-{id}`
   - Payload:
     ```json
     {
       "battleId": "battle-demo-1",
       "target": "Guerrero",
       "amount": 15,
       "targetRemainingHp": 90,
       "occurredAt": "2026-09-20T22:02:00Z"
     }
     ```

---

### 6.3. Reglas de Convivencia para Persona 2 y Persona 3:
1. **Persona 2 (Servicio A):**
   - Solo escribe eventos (`AppendToStreamAsync`).
   - Reconstruye el estado haciendo replay del stream antes de aceptar un nuevo comando.
   - Si una regla falla (ej. saldo insuficiente o personaje muerto), retorna HTTP `400 Bad Request` y **NO escribe ningún evento**.
2. **Persona 3 (Servicio B):**
   - Solo lee eventos suscribiéndose a `$all` o a los streams específicos.
   - Mantiene una vista en memoria (*Read Model*) optimizada para servir las respuestas a los clientes.
   - No modifica eventos ni realiza llamadas de escritura a EventStoreDB.
