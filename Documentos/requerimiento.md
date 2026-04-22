# Pitch – Descarga Masiva de Soportes PDF

> Refinamiento aplicando Shape Up (Basecamp). Formato: Pitch listo para betting table.

---

## 1. Problema

El equipo de operaciones descarga manualmente soportes PDF desde la intranet de Helpharma, uno por uno, buscando cada documento por su código (ej. `FCA70905`). Cuando el volumen supera los 200–300 documentos por ciclo, el proceso toma horas, es propenso a omisiones y no deja trazabilidad de qué se descargó y qué falló.

**Situación actual (baseline):**
- El usuario accede manualmente a `intranet.helpharma.com`.
- Busca cada documento, abre el PDF, lo guarda con nombre manual.
- No existe registro de fallidos ni de duplicados ya descargados.

**Por qué importa ahora:** los soportes son requeridos para auditorías de dispensación. Un faltante detectado tarde genera reprocesos costosos.

---

## 2. Apetito

**Small batch — 2 semanas de un desarrollador.**

El problema está acotado: no se construye un portal, no se integra con sistemas externos nuevos, no se diseña UI. Si en 2 semanas no se puede entregar funcionando, se reduce el alcance (ver No-Gos), no se extiende el tiempo.

---

## 3. Solución

### Flujo principal (breadboard)

```
[entrada.txt]
     │
     ▼
[Leer códigos]──────────────────────────────────────┐
     │                                              │
     ▼                                              │
[GET /api/v1/consultasoporte/{codigo}]              │
     │                                              │
  success?                                          │
  ┌──┴──┐                                           │
 NO    SÍ                                           │
  │     │                                           │
  ▼     ▼                                           │
[fallidos.txt]  [Construir URL ver-pdf/...]          │
                │                                   │
                ▼                                   │
        [¿Sesión activa?]                           │
         ┌─────┴──────┐                             │
        SÍ           NO                             │
         │            │                             │
         │        [Login Playwright]                │
         │            │                             │
         └────────────┘                             │
                │                                   │
                ▼                                   │
        [Descargar PDF con Playwright]              │
                │                                   │
          ¿Exitoso?                                 │
         ┌──────┴──────┐                            │
        SÍ            NO                            │
         │             │                            │
[descargados/]   [fallidos.txt]                     │
[descargados.txt]                                   │
         │                                          │
         └──────────────────── siguiente ───────────┘
```

### Elementos clave de la solución

**Entrada:** archivo `input/entrada.txt`, un código por línea.

**Consulta API:** `GET /api/v1/consultasoporte/{codigo}` con Bearer token. Devuelve `storage_path` o `success: false`.

**URL de descarga:** se construye como:
```
https://intranet.helpharma.com/ver-pdf/{storage_path}
```

**Sesión:** Playwright mantiene una sesión de browser con login en `intranet.helpharma.com/login`. Si detecta redirección al login, reautentica automáticamente y **retoma el proceso desde el documento donde se interrumpió** — sin volver a descargar los ya exitosos.

**Salida:**
```
wwwroot/
├── descargados/       ← PDFs descargados
├── descargados.txt    ← códigos exitosos
└── fallidos.txt       ← códigos fallidos (con motivo)
```

**Consola:**
```
[1/250] FCA70905 → Descargado
[2/250] FCA70906 → No encontrado en API
[3/250] FCA70907 → Sesión vencida, reintentando login...
[3/250] FCA70907 → Descargado
```

### Scopes de construcción

| Scope | Qué incluye |
|---|---|
| **Lector de entrada** | Leer `entrada.txt`, deduplicar, ignorar líneas vacías |
| **Consultor API** | `HttpClient` con Bearer token, deserializar respuesta, manejar `success: false` |
| **Gestor de sesión** | Playwright: login inicial, detección de redirección, re-login automático |
| **Descargador PDF** | Navegación Playwright a URL `ver-pdf/`, captura del archivo, guardado en disco |
| **Registro de resultados** | Escritura de `descargados.txt` y `fallidos.txt` con motivo de fallo |

---

## 4. Riesgos y Huecos (Rabbit Holes)

Estos son los puntos que podrían convertirse en pozos sin fondo si no se delimitan antes de empezar:

**R1 — Playwright y descarga de archivos protegidos**
La URL `ver-pdf/` puede servir el PDF directamente como response del navegador o puede redirigir a S3 con URL firmada. La estrategia de captura con Playwright difiere en cada caso. Validar el comportamiento real antes de implementar `DescargaPdfService`. Si la URL redirige a S3 con token temporal, se puede usar `HttpClient` directamente con esa URL en lugar de Playwright.

**R2 — Bearer token estático (no expira)**
El token `4050281|BTH7oV8sR3n5pc4Ko8LHxpnhbWiJKga8p6M3IAjw` es de larga duración y no expira. No es necesario manejo de renovación. Si la API retorna 401 inesperadamente, registrar el código en `fallidos.txt` con motivo `"Error API 401"` y continuar con el siguiente — no detener el proceso completo.

**R3 — Volumen y throttling**
No se conoce si la API tiene rate limiting. Usar concurrencia máxima de 3 requests simultáneos como límite seguro por defecto. No implementar un sistema de throttling dinámico — eso excede el apetito.

**R4 — Estructura del `storage_path`**
El campo `data[0].storage_path` asume siempre el primer elemento del array. Si un documento tiene múltiples soportes (array con más de un item), la solución actual solo descarga el primero. Esto es aceptable para este ciclo.

---

## 5. No-Gos (fuera de alcance)

Estas cosas están explícitamente **excluidas** de este ciclo:

- ❌ Interfaz gráfica (GUI, web, dashboard).
- ❌ Almacenamiento en base de datos de resultados.
- ❌ Envío de notificaciones por correo o mensajería.
- ❌ Manejo de múltiples soportes por documento (solo se descarga `data[0]`).
- ❌ Barra de progreso visual animada — solo output de texto en consola.
- ❌ Despliegue en servidor — se ejecuta localmente por el operador.

---

## 6. Configuración (appsettings.json)

```json
{
  "Paths": {
    "Input": "input/entrada.txt",
    "Output": "wwwroot/descargados"
  },
  "Login": {
    "Url": "https://intranet.helpharma.com/login",
    "User": "alejandro.ortiz@zentria.com.co",
    "Password": "H3lph4rm4."
  },
  "Api": {
    "BaseUrl": "https://intranet.helpharma.com/api/v1/consultasoporte/",
    "BearerToken": "4050281|BTH7oV8sR3n5pc4Ko8LHxpnhbWiJKga8p6M3IAjw"
  },
  "Concurrency": {
    "MaxParallelDownloads": 3
  }
}
```

---

## 7. Estructura del proyecto (.NET 10 — Consola)

```text
SoportesDescarga/
├── Program.cs
├── AppRunner.cs
├── appsettings.json
├── input/
│   └── entrada.txt
├── wwwroot/
│   └── descargados/
├── Domain/
│   ├── ApiResponse.cs
│   └── SoporteItem.cs
├── Application/
│   └── Interfaces/
│       ├── IConsultaSoporteService.cs
│       ├── IDescargaPdfService.cs
│       └── IArchivoService.cs
├── Infrastructure/
│   ├── ConsultaSoporteService.cs   ← HttpClient + Bearer token
│   ├── DescargaPdfService.cs       ← Playwright, descarga, guardado
│   ├── LoginService.cs             ← Playwright, login, detección sesión
│   └── ArchivoService.cs           ← Leer entrada.txt, escribir resultados
└── Shared/
    ├── ConfigHelper.cs
    └── RetryHelper.cs
```

---

## Criterio de éxito del ciclo

La solución se considera **terminada** cuando:

1. Dado un `entrada.txt` con 250 códigos reales, el proceso corre sin intervención manual hasta el final.
2. Cada PDF encontrado queda en `wwwroot/descargados/` con el nombre del código.
3. `descargados.txt` y `fallidos.txt` reflejan con exactitud lo que ocurrió.
4. Si la sesión expira a mitad del proceso, el sistema reautentica y continúa desde el documento donde se interrumpió, sin re-descargar los ya guardados.
