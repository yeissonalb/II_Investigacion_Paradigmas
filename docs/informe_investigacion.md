# Universidad Nacional de Costa Rica
**Sede:** Regional Chorotega  
**Campus:** Nicoya  
**Laboratorio #2: Event Sourcing en Arquitecturas Distribuidas**  

**Estudiantes:**  
- Fatima Carrillo Garcia  
- Samir Campos Díaz  
- Maria del Mar Diaz Ruiz  
- Brayan José Pérez Balladares  
- Yeisson Alberto Villalobos Toruño  

**Curso:**  
Paradigmas de programación  

**Profesor:**  
Msc. Alex Villegas Carranza  

**Fecha de entrega:**  
23/09/2026

---

## RESUMEN EJECUTIVO

En el desarrollo tradicional de software empresarial predomina el paradigma CRUD (*Create, Read, Update, Delete*), donde las bases de datos relacionales almacenan únicamente el estado actual de las entidades, sobreescribiendo valores mediante instrucciones `UPDATE` destructivas. Si bien este enfoque es intuitivo y suficiente para aplicaciones básicas, introduce serias limitaciones en entornos distribuidos, financieros o de misión crítica donde la auditoría, la trazabilidad temporal y la intención del negocio son indispensables.

El presente trabajo investiga el patrón arquitectónico **Event Sourcing** (combinado con **CQRS - Command Query Responsibility Segregation**), en el cual el estado de una entidad se modela como una secuencia ordenada e inmutable de eventos de dominio (*append-only log*). Para evidenciar su funcionamiento, se diseñó e implementó un sistema distribuido compuesto por dos microservicios autónomos en **.NET 9** coordinados a través de **EventStoreDB** y orquestados mediante **Docker Compose**.

---

## 1. ¿QUÉ ES EVENT SOURCING?

**Event Sourcing** es un patrón arquitectónico en el que todos los cambios realizados en el estado de una aplicación se registran como una serie continua de **eventos inmutables**.

### Principios Fundamentales:
1. **Inmutabilidad Absoluta:** Un evento representa un hecho consumado en el pasado (por ejemplo, `AccountOpened`, `MoneyDeposited`, `BattleStarted`, `AttackPerformed`). Los hechos ocurridos en el pasado no pueden ser modificados ni eliminados.
2. **Almacenamiento Append-Only:** La base de datos de eventos (llamada *Event Store*) únicamente permite operaciones de adición al final de una secuencia ordenada (*stream*). No existen operaciones de `UPDATE` ni `DELETE`.
3. **Reconstrucción del Estado mediante Replay:** El estado actual de cualquier entidad de negocio no se encuentra precalculado en una tabla fija; en su lugar, se calcula reproduciendo (*replay*) todos los eventos acumulados desde su creación.
4. **Desacoplamiento entre Escritura y Lectura:** Al combinarse con **CQRS**, el servicio de comandos se enfoca exclusivamente en recibir intenciones, validar reglas de negocio contra el historial y persistir nuevos eventos; mientras que los servicios de lectura se suscriben a los streams para generar vistas materializadas optimizadas para consultas inmediatas.

> En resumen: En un sistema tradicional la base de datos almacena el **resultado** de lo que pasó; en Event Sourcing la base de datos almacena la **historia completa** de lo que pasó.

---

## 2. ¿QUÉ PROBLEMA RESUELVE? (SITUACIÓN REAL)

### 2.1. El problema de la pérdida de información en bases de datos tradicionales
Considérese un sistema bancario o de billetera digital con una tabla relacional `Cuentas` con los campos `Id`, `NumeroCuenta` y `Saldo`.
- Si un cliente posee un saldo de \$100.000, luego se le acreditan \$50.000 y minutos después se debitan \$30.000, la base de datos relacional únicamente conserva: `Saldo: 120000`.
- Si al cierre del mes existe una discrepancia contable o el cliente desconoce una transacción, la tabla de saldos es incapaz de responder:
  - ¿Quién realizó cada operación?
  - ¿A qué hora exacta ocurrió cada transacción?
  - ¿En qué estado se encontraba la cuenta entre la primera y la segunda transacción?
- La solución improvisada tradicional es crear tablas auxiliares de "logs" o "auditoría". Sin embargo, en arquitecturas distribuidas, estos logs secundarios frecuentemente quedan desincronizados, se corrompen ante fallos de red o carecen de consistencia transaccional con la tabla principal.

### 2.2. Solución aportada por Event Sourcing
Con Event Sourcing, el saldo jamás se almacena como una verdad estática modificable:
1. **Auditoría Nativa al 100%:** El registro de eventos *es* la base de datos principal. No existe ninguna acción que cambie el negocio sin generar un evento firmado con timestamp.
2. **Consultas Temporales (Time-Travel):** Permite consultar el estado de cualquier cuenta en un punto exacto de la historia: *"¿Cuál era el estado de la cuenta el 10 de marzo a las 15:30?"* Basta con reproducir los eventos ocurridos antes de ese instante.
3. **Flexibilidad Evolutiva para Nuevas Proyecciones:** Si meses después de entrar a producción el departamento financiero solicita una métrica nunca antes imaginada (por ejemplo, *promedio de transacciones rechazadas en fines de semana*), no se pierde información histórica: se crea una nueva proyección que procesa todos los eventos pasados desde el día cero.

---

## 3. ¿CÓMO FUNCIONA? FLUJO DE COMUNICACIÓN

El sistema opera bajo el principio de separación de responsabilidades:

```
                            [ Cliente / Usuario ]
                                |           ^
                   1. Comandos  |           | 5. Consultas
                      (POST)    v           |    (GET)
                         +-----------+   +-----------+
                         | Service A |   | Service B |
                         | (Command) |   |  (Query)  |
                         | Port 3001 |   | Port 3002 |
                         +-----------+   +-----------+
                               |               ^
                 2. Replay     |               | 4. Suscripción en
                 3. Append     v               |    tiempo real ($all)
                         +---------------------------+
                         |       EventStoreDB        |
                         |       (Puerto 2113)       |
                         |    Streams Inmutables     |
                         +---------------------------+
```

### Paso a Paso del Flujo:
1. **Emisión del Comando:** El cliente envía una petición `POST` al **Servicio A (Puerto 3001)** indicando la acción que desea ejecutar.
2. **Reconstrucción del Estado (Replay):** El Servicio A consulta a **EventStoreDB** los eventos previos del stream correspondiente (`account-{id}` o `battle-{id}`). Reconstituye en memoria la entidad y valida si el comando cumple con las reglas de negocio (por ejemplo, verificar si el saldo es suficiente o si el participante no ha sido derrotado).
3. **Persistencia Inmutable (Append-Only):** Si la regla se cumple, el Servicio A crea el evento correspondiente en tiempo pasado y lo agrega al final del stream en EventStoreDB.
4. **Propagación Reactiva:** EventStoreDB asigna un número de versión secuencial al evento y lo emite inmediatamente a los suscriptores conectados mediante gRPC / TCP.
5. **Proyección en el Servicio B (Read Model):** El **Servicio B (Puerto 3002)**, que corre un servicio en segundo plano suscrito a `$all`, recibe el evento y actualiza su modelo de lectura en memoria.
6. **Consulta Eficiente (Query):** Los clientes que consultan `GET` en el Servicio B reciben lecturas precalculadas ultra rápidas, sin consultar ni sobrecargar el almacén de eventos.

---

## 4. VENTAJAS Y DESVENTAJAS

### 4.1. Principales Ventajas
- **Trazabilidad total e inalterabilidad:** Los eventos son históricos y no se pueden alterar, garantizando cumplimiento legal y normativo (auditoría financiera, médica o logística).
- **Cero pérdida de contexto:** Se preserva la intención del usuario y no solo el estado final.
- **Modelos de lectura infinitos y desacoplados:** Se pueden crear múltiples modelos de lectura optimizados para diferentes consumidores (dashboards analíticos, APIs móviles, reportes gerenciales) consumiendo el mismo flujo de eventos.
- **Resiliencia y recuperación ante desastres:** Si una proyección o base de datos de lectura se corrompe, se borra y se regenera leyendo los eventos desde el origen.

### 4.2. Principales Limitaciones
- **Consistencia Eventual:** Existe una latencia mínima (usualmente de milisegundos) entre que el Servicio A persiste el evento y el Servicio B actualiza su modelo de lectura.
- **Complejidad y Curva de Aprendizaje:** Requiere pensar en flujos asíncronos y modelado guiado por el dominio (DDD), alejándose del patrón relacional intuitivo.
- **Evolución y Versionado de Esquemas:** Los eventos guardados en producción son permanentes. Modificar un contrato requiere técnicas avanzadas como *Upcasting*, adaptadores o esquemas multi-versión.
- **Tamaño y Rendimiento del Replay:** Entidades con miles de eventos tardan más en reconstruirse, lo que obliga a implementar la técnica de *Snapshots* (guardar fotos periódicas del estado cada $N$ eventos).

### 4.3. ¿Cuándo es recomendable utilizarlo?
- Aplicaciones financieras, contables, bancarias y pasarelas de pago.
- Sistemas de subastas, inventarios y seguimiento de paquetes en tiempo real.
- Sistemas distribuidos que requieren auditoría estricta por entes reguladores.
- Sistemas de colaboración concurrente (ej. Figma, Google Docs, herramientas de edición).

### 4.4. ¿Cuándo probablemente NO sería necesario?
- Aplicaciones CRUD sencillas (blogs, sitios web corporativos, catálogos estáticos).
- Sistemas donde el historial no tiene ningún valor de negocio y solo importa el dato presente.
- Proyectos con equipos sin experiencia previa en arquitecturas reactivas o con presupuestos y tiempos muy ajustados.

---

## 5. ALTERNATIVAS LIBRES Y COMPARATIVA TÉCNICA

Para implementar Event Sourcing se evaluaron distintas alternativas gratuitas y Open Source que pueden ejecutarse localmente:

| Herramienta | Tipo de Herramienta | Soporte Nativo de Streams | Control de Concurrencia | Replay Histórico | Veredicto para Event Sourcing |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **EventStoreDB** | Base de datos especializada | Nativo (`stream-{id}`) | Optimista por versión esperada | Nativo y eficiente por stream | **Ideal:** Creada exclusivamente para este propósito. |
| **PostgreSQL** | Base de datos relacional Open Source | Mediante tablas / particionado | Mediante transacciones ACID o librerías (*Marten*) | Nativo (`SELECT WHERE stream_id ORDER BY version`) | **Excelente alternativa:** Permite implementar el patrón usando tablas relacionales con JSONB sin agregar software extra. |
| **Apache Kafka** | Plataforma de Event Streaming | Limitado (no escalable a millones de particiones) | Limitado a nivel de partición / offset | Rebobinando offset de consumidores | **No recomendada como Event Store puro:** Excelente como bus de integración entre microservicios, pero inadecuado para streams individuales de entidad. |
| **RabbitMQ** | Message Broker | Nulo | No aplica | Inviable (los mensajes se purgan tras el ACK) | **Inadecuada como Event Store:** Es un broker de colas transitorias, no un almacén persistente e inmutable de eventos. |

### Justificación de Selección:
Se seleccionó **EventStoreDB** debido a su naturaleza especializada: expone streams individuales por entidad (`battle-{id}` o `account-{id}`), soporta suscripciones reactivas en tiempo real mediante gRPC y cuenta con una interfaz web integrada en el puerto 2113 para auditar e inspeccionar los eventos de forma gráfica.

Como contraparte libre de propósito general, **PostgreSQL** representa la alternativa más sólida en la industria actual mediante bibliotecas como *Marten* en el ecosistema .NET, permitiendo almacenar el payload en formato `jsonb` con garantías ACID.

---

## 6. CONTRATO DE INTEGRACIÓN DEL EQUIPO

Para evitar conflictos de integración entre el **Servicio A** y el **Servicio B**, se estandarizó el siguiente contrato técnico:

### 6.1. Puertos y Direccionamiento
- **Servicio A (Command):** `http://localhost:3001`
- **Servicio B (Query):** `http://localhost:3002`
- **EventStoreDB (UI / gRPC):** `http://localhost:2113`
- **Conexión interna Docker:** `esdb://eventstore:2113?tls=false`
- **Conexión local Host:** `esdb://localhost:2113?tls=false`

### 6.2. Convención de Streams
- Patrón de nomenclatura: `account-{id}` / `battle-{id}`.

### 6.3. Eventos Formalizados
- **Creación de Entidad:** `AccountOpened` / `BattleStarted`
- **Modificación con Regla de Negocio:** `MoneyDeposited` / `MoneyWithdrawn` / `AttackPerformed` / `HealUsed`

---

## 7. CONCLUSIONES Y LECCIONES APRENDIDAS

1. **La verdad reside en los eventos:** En Event Sourcing, los estados finales (como el saldo de una cuenta o los puntos de vida de un combate) son simplemente proyecciones calculadas; la única fuente de verdad inmutable son los eventos que ocurrieron a lo largo del tiempo.
2. **Desacoplamiento arquitectónico efectivo:** El servicio de comandos no tiene dependencia directa del servicio de consultas, lo que permite escalar ambos de forma asimétrica y desplegarlos de manera independiente.
3. **Reproducibilidad y Observabilidad:** Gracias a Docker Compose y a la UI de EventStoreDB, cualquier desarrollador o auditor puede clonar el repositorio, ejecutar la solución y observar con exactitud matemática cómo y cuándo interactúan los servicios distribuidos.
