# Plan de Trabajo – Descarga Masiva de Soportes PDF

> Apetito: **2 semanas / 1 desarrollador** | Inicio: 22 abr 2026 — Entrega: 6 may 2026

---

## Tareas

Las tareas están ordenadas por dependencia. Completar de arriba hacia abajo.

---

### Scaffolding (S1)

- [ ] Crear solución: `dotnet new console -n SoportesDescarga`
- [ ] Crear estructura de carpetas: `Domain/`, `Application/Interfaces/`, `Infrastructure/`, `Shared/`, `input/`, `wwwroot/descargados/`
- [ ] Agregar `appsettings.json` con configuración base (ver pitch sección 6)
- [ ] Instalar paquetes: `Microsoft.Extensions.Configuration.Json`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Playwright`
- [ ] Configurar `Program.cs` con DI y lectura de `appsettings.json`
- [ ] Verificar que `dotnet build` pasa sin errores

---

### Dominio y lectura de entrada (S2)

- [ ] Crear `Domain/ApiResponse.cs` con propiedades: `Success`, `Data`, `Message`
- [ ] Crear `Domain/SoporteItem.cs` con propiedades: `FechaRegistro`, `StorageDisk`, `StoragePath`
- [ ] Crear interfaz `Application/Interfaces/IArchivoService.cs`
- [ ] Implementar `Infrastructure/ArchivoService.cs`:
  - Leer `entrada.txt` línea a línea
  - Eliminar duplicados e ignorar líneas vacías
  - Retornar `IEnumerable<string>`
- [ ] Crear `input/entrada.txt` con 5 códigos de prueba y verificar la lectura

---

### Consulta API (S3)

- [ ] Crear interfaz `Application/Interfaces/IConsultaSoporteService.cs`
- [ ] Implementar `Infrastructure/ConsultaSoporteService.cs`:
  - `HttpClient` con Bearer token en header `Authorization`
  - `GET /api/v1/consultasoporte/{codigo}`
  - Deserializar respuesta con `System.Text.Json`
  - Si `success: false` → retornar null con motivo `"No encontrado"`
  - Si HTTP != 200 → retornar null con motivo `"Error API {statusCode}"` y continuar
- [ ] Crear `Shared/ConfigHelper.cs` para centralizar acceso a configuración
- [ ] Probar con código real `FCA70905` e imprimir `storage_path` en consola

> ⚠️ **Punto de decisión (R1):** antes de continuar, abrir en browser autenticado la URL `https://intranet.helpharma.com/ver-pdf/{storage_path}` e inspeccionar la pestaña Network para determinar si el PDF se sirve directamente o redirige a una URL de S3. La respuesta define cómo implementar S5.

---

### Gestión de sesión con Playwright (S4)

- [ ] Ejecutar `playwright install chromium`
- [ ] Crear interfaz `Application/Interfaces/ILoginService.cs`
- [ ] Implementar `Infrastructure/LoginService.cs`:
  - `LoginAsync()`: navegar a `/login`, completar usuario y clave, submit, esperar redirección exitosa
  - `IsSessionActiveAsync()`: navegar a URL protegida y verificar que no redirige a `/login`
  - `EnsureSessionAsync()`: si sesión inactiva → llamar `LoginAsync()`
  - Timeout máximo de 15 segundos por operación
- [ ] Verificar que las cookies persisten en la instancia de `IBrowserContext` entre llamadas

---

### Descarga de PDF (S5)

Implementar según lo definido en el punto de decisión R1:

**Si `ver-pdf/` sirve el PDF directamente:**
- [ ] Navegar con Playwright a la URL y capturar el response con `page.RouteAsync`
- [ ] Leer bytes y guardar en `wwwroot/descargados/{codigo}.pdf`

**Si `ver-pdf/` redirige a S3:**
- [ ] Interceptar la URL firmada de S3 con Playwright
- [ ] Descargar el archivo con `HttpClient` usando esa URL temporal
- [ ] Guardar en `wwwroot/descargados/{codigo}.pdf`

Común a ambas opciones:
- [ ] Crear interfaz `Application/Interfaces/IDescargaPdfService.cs`
- [ ] Implementar `Infrastructure/DescargaPdfService.cs`
- [ ] Llamar `EnsureSessionAsync()` antes de cada descarga
- [ ] Si detecta redirección al login durante la descarga → re-login + 1 reintento automático
- [ ] Si el reintento falla → retornar fallo con motivo `"Error descarga tras re-login"`

---

### Registro de resultados y resume (S6)

- [ ] Extender `IArchivoService` y `ArchivoService` con:
  - `AppendDescargado(string codigo)` — append en `descargados.txt`
  - `AppendFallido(string codigo, string motivo)` — append `codigo | motivo` en `fallidos.txt`
  - `GetDescargadosExistentes()` — lee `descargados.txt` al inicio para saber qué ya fue procesado
- [ ] Al iniciar el proceso: cruzar lista de entrada con `descargados.txt` y saltar los ya procesados
- [ ] Si el PDF ya existe físicamente en `wwwroot/descargados/`: saltar sin re-descargar

---

### Orquestador, consola y concurrencia (S7)

- [ ] Implementar `AppRunner.cs`:
  - Cargar entrada y aplicar lógica de resume (S6)
  - Bucle principal con `SemaphoreSlim` para máximo 3 descargas simultáneas
  - Por cada código: consultar API → si falla → `fallidos.txt`; si ok → descargar → registrar resultado
  - Output en consola: `[{i}/{total}] {codigo} → {resultado}`
- [ ] Implementar `Shared/RetryHelper.cs`: 1 reintento para errores transitorios de red
- [ ] Conectar `Program.cs` → `AppRunner.RunAsync()`
- [ ] Manejar `CancellationToken` para que Ctrl+C escriba el estado antes de salir
- [ ] Verificar flujo completo con los 5 códigos de prueba del `entrada.txt` inicial

---

### Prueba con volumen real — lote pequeño (S8a)

- [ ] Preparar `entrada.txt` con 50 códigos reales
- [ ] Ejecutar y verificar:
  - ¿La API responde consistentemente?
  - ¿Los PDFs guardados se pueden abrir (no están en 0 bytes)?
  - ¿El output en consola es claro y legible?
  - ¿Playwright no se cuelga en ningún código?
- [ ] Ajustar timeouts o comportamiento según lo observado

---

### Prueba con volumen completo (S8b)

- [ ] Preparar `entrada.txt` con 250 códigos reales
- [ ] Ejecutar proceso completo sin intervención manual hasta el final
- [ ] Verificar que `descargados.txt` + `fallidos.txt` cubren el 100% de los códigos de entrada
- [ ] Verificar que al detener y relanzar el proceso retoma sin re-descargar los ya guardados

---

### Entrega

- [ ] Crear `README.md` con: prerrequisitos, cómo preparar `entrada.txt`, cómo ejecutar, dónde quedan los archivos, qué hacer si el proceso se detiene
- [ ] Confirmar que credenciales y token están solo en `appsettings.json`, no en código
- [ ] Compilar en modo Release: `dotnet publish -c Release -o ./release`
- [ ] Entregar carpeta `release/` con `input/entrada.txt` vacío y `wwwroot/descargados/` creado

---

## Qué no se puede cortar

S2 (lectura) → S3 (API) → S4 (sesión) → S5 (descarga) son el núcleo. Sin ellos no hay producto.

Si el tiempo se ajusta, se elimina en este orden: concurrencia (S7) → resume automático (S6).
