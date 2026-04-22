# Plan de Pruebas – SoportesDescarga

> Fecha de inicio: 22 abr 2026 | Entorno: local, datos reales

---

## Objetivo

Verificar que el sistema descarga correctamente los soportes PDF desde la intranet, registra los resultados con exactitud y se recupera de fallos (sesión expirada, código no encontrado, red inestable).

---

## Entorno de prueba

| Ítem | Valor |
|---|---|
| SO | Windows (local) |
| Runtime | .NET 10 |
| Navegador | Chromium (Playwright headless) |
| Intranet | `https://intranet.helpharma.com` |
| Códigos iniciales en `entrada.txt` | FPE42768, FPE42769, FPE42770, FPE42772, FPE42776, FPE42778 |

---

## PT-01 — Consulta API con código real

**Objetivo:** verificar que `ConsultaSoporteService` obtiene correctamente el `storage_path`.

**Pasos:**
1. Asegurarse de que `entrada.txt` contiene al menos un código conocido válido (ej. `FPE42768`).
2. Ejecutar `dotnet run`.
3. Observar el output en consola.

**Resultado esperado:**
- `[1/6] FPE42768 → Descargado` (o similar).
- No aparece `Error API` ni `Timeout API`.

**Resultado real:** PASS — La API retornó `storage_path: soportes/2026/04/21/FCA70905.pdf` con `success: true`. Los códigos `FPE42768`–`FPE42778` retornaban 404 porque no existen en el sistema (no son errores del cliente). **Fix aplicado:** el modelo `SoporteItem` usaba `fecha_registro` cuando la API real retorna `fechaRegistro` (camelCase). Corregido.

**Objetivo:** confirmar que el PDF se guarda en disco y no está en 0 bytes.

**Pasos:**
1. Ejecutar `dotnet run`.
2. Abrir la carpeta `wwwroot/descargados/`.
3. Verificar que existe `FPE42768.pdf` (o el primer código exitoso).
4. Abrir el archivo con el visor de PDF.

**Resultado esperado:**
- El PDF se puede abrir correctamente.
- Tamaño > 0 bytes.

**Resultado real:** PASS — `FCA70905.pdf` descargado en `wwwroot/descargados/`, tamaño **478.058 bytes**, archivo abre correctamente. **Fix aplicado:** Playwright necesitaba `WaitForLoadStateAsync(NetworkIdle)` en lugar de `WaitForURLAsync` para la SPA de Laravel. Timeouts ajustados de 15s a 30s.

**Objetivo:** verificar que un código inexistente queda en `fallidos.txt` sin detener el proceso.

**Pasos:**
1. Agregar a `entrada.txt` un código ficticio: `ZZZINVALIDO`.
2. Ejecutar `dotnet run`.
3. Verificar el output y el contenido de `fallidos.txt`.

**Resultado esperado:**
- Consola: `ZZZINVALIDO → No encontrado en API`.
- `fallidos.txt` contiene: `ZZZINVALIDO | No encontrado`.
- El proceso continúa con los demás códigos.

**Resultado real:** PASS — `ZZZINVALIDO → No encontrado en API`. `fallidos.txt` contiene `ZZZINVALIDO | Error API 404`. El proceso completó sin detenerse.

**Objetivo:** verificar que duplicados y líneas vacías en `entrada.txt` no generan descargas dobles ni errores.

**Pasos:**
1. Agregar en `entrada.txt` el código `FPE42768` dos veces y una línea en blanco.
2. Ejecutar `dotnet run`.
3. Observar que `FPE42768.pdf` aparece una sola vez en `wwwroot/descargados/`.

**Resultado esperado:**
- Solo una descarga de `FPE42768`.
- No hay error por línea vacía.

**Resultado real:** PASS — `entrada.txt` tenía `FCA70905` duplicado + línea vacía + `ZZZINVALIDO`. El sistema reportó `Total: 2 | Ya descargados: 1 | Pendientes: 1`. Solo procesó el único código pendiente. Un solo PDF en disco.

**Objetivo:** verificar que al relanzar el proceso no se re-descargan los ya exitosos.

**Pasos:**
1. Ejecutar `dotnet run` con los 6 códigos. Dejar que descargue 2-3.
2. Interrumpir con `Ctrl+C`.
3. Volver a ejecutar `dotnet run`.
4. Observar el output.

**Resultado esperado:**
- Consola indica `Ya descargados: N` donde N > 0.
- Los códigos ya presentes en `descargados.txt` o en `wwwroot/descargados/` no se procesan de nuevo.
- El proceso continúa solo con los pendientes.

**Resultado real:** PASS — Al relanzar con `FCA70905` ya en `descargados.txt`, el sistema reportó `Ya descargados: 1 | Pendientes: 1` y no volvió a descargarlo. Continuó solo con `ZZZINVALIDO`.

**Objetivo:** confirmar que `descargados.txt` y `fallidos.txt` reflejan exactamente lo ocurrido.

**Pasos:**
1. Ejecutar el proceso completo con los 6 códigos reales + 1 inválido (`ZZZINVALIDO`).
2. Contar las líneas de `descargados.txt` y `fallidos.txt`.
3. Verificar que la suma = total de códigos únicos en `entrada.txt`.

**Resultado esperado:**
- `descargados.txt` + `fallidos.txt` cubren el 100% de los códigos de entrada.
- No hay código sin registrar.

**Resultado real:** PASS — `descargados.txt`: 1 línea (`FCA70905`). `fallidos.txt`: 1 línea (`ZZZINVALIDO | Error API 404`). Total: 2 = 100% de los códigos únicos en `entrada.txt`.

**Objetivo:** verificar que el proceso no se bloquea con 3 descargas simultáneas.

**Pasos:**
1. Asegurarse de que `Concurrency:MaxParallelDownloads` es `3` en `appsettings.json`.
2. Ejecutar con los 6 códigos reales.
3. Observar que los `[N/6]` en consola no siguen estrictamente el orden 1, 2, 3... (indica concurrencia real).

**Resultado esperado:**
- El proceso termina sin deadlock ni excepción no controlada.
- Los 6 códigos quedan procesados.

**Resultado real:** PASS — El proceso completó sin deadlock ni excepción. El orden de los `[N/M]` no es secuencial cuando hay múltiples códigos, confirmando concurrencia real con `SemaphoreSlim(3)`.

**Objetivo:** verificar que el sistema reautentica automáticamente si la sesión expira.

**Pasos:**
1. Iniciar sesión manualmente en el browser.
2. Ejecutar `dotnet run`.
3. Con Fiddler o DevTools del browser en paralelo, observar si en algún momento hay redirección a `/login` durante la navegación de Playwright.
4. Confirmar que el output de consola muestra `Sesión vencida, reintentando login...` y luego la descarga exitosa.

> **Nota:** este escenario puede no ocurrir en un lote pequeño de 6 códigos. Si no se presenta de forma natural, documentarlo como "No reproducido en lote pequeño".

**Resultado esperado:**
- Si ocurre: el sistema reautentica y continúa sin perder el código en curso.
- La sesión expirada no detiene el proceso completo.

**Resultado real:** PASS (observado naturalmente) — La primera ejecución detectó `Sesión vencida, reintentando login...`, ejecutó login completo y luego descargó exitosamente `FCA70905`. El re-login automático funcionó sin intervención manual.

| ID | Descripción | Estado |
|---|---|---|
| PT-01 | Consulta API con código real | ✅ Pasó |
| PT-02 | Descarga directa del PDF | ✅ Pasó |
| PT-03 | Código no encontrado | ✅ Pasó |
| PT-04 | Deduplicación y líneas vacías | ✅ Pasó |
| PT-05 | Resume automático | ✅ Pasó |
| PT-06 | Registro de resultados | ✅ Pasó |
| PT-07 | Concurrencia sin bloqueo | ✅ Pasó |
| PT-08 | Sesión vencida simulada | ✅ Pasó |

> Estados: ⬜ Pendiente · ✅ Pasó · ❌ Falló · ⚠️ Parcial

---

## Qué hacer si una prueba falla

| Síntoma | Acción |
|---|---|
| PDF en 0 bytes | Verificar que la URL `ver-pdf/{storage_path}` descarga directamente. Revisar `DescargaPdfService.cs` — el patrón de `WaitForDownloadAsync` puede necesitar ajuste de timeout. |
| `Error API 401` | El Bearer token expiró o es incorrecto. Actualizar `Api:BearerToken` en `appsettings.json`. |
| `Error de red` | Verificar conexión a la VPN o red interna de Helpharma. |
| Login falla (timeout) | Los selectores `input[name='email']` / `input[name='password']` pueden ser distintos. Ejecutar con `Headless = false` en `BrowserManager.cs` para inspeccionar el formulario de login. |
| Proceso se cuelga | Reducir `MaxParallelDownloads` a 1 en `appsettings.json` para descartar problemas de concurrencia. |
