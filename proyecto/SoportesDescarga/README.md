# SoportesDescarga — Descarga Masiva de Soportes PDF

Herramienta de consola .NET 10 para descargar masivamente soportes PDF desde la intranet de Helpharma.

---

## Prerrequisitos

- .NET 10 SDK instalado
- Acceso a la red interna de Helpharma (`intranet.helpharma.com`)
- Chromium ya instalado (incluido en el setup del proyecto)

---

## Cómo preparar `entrada.txt`

Edita el archivo `input/entrada.txt` con un código por línea:

```
FCA70905
FCA70906
FCA70907
```

- Las líneas vacías se ignoran automáticamente.
- Los duplicados se eliminan automáticamente.

---

## Cómo ejecutar

Desde la carpeta del proyecto:

```bash
dotnet run
```

O compilado en Release:

```bash
dotnet publish -c Release -o ./release
cd release
dotnet SoportesDescarga.dll
```

---

## Dónde quedan los archivos

```
wwwroot/
├── descargados/       ← PDFs descargados (nombre: {codigo}.pdf)
├── descargados.txt    ← Códigos descargados exitosamente
└── fallidos.txt       ← Códigos fallidos con motivo (ej: FCA70905 | No encontrado)
```

---

## Output en consola

```
Total: 250 | Ya descargados: 0 | Pendientes: 250
------------------------------------------------------------
[1/250] FCA70905 → Descargado
[2/250] FCA70906 → No encontrado en API
[3/250] FCA70907 → FALLIDO: Timeout esperando descarga del PDF
------------------------------------------------------------
Proceso finalizado.
```

---

## Si el proceso se detiene a mitad

Al volver a ejecutar, el sistema:
1. Lee `descargados.txt` para saber qué ya fue procesado.
2. Verifica si el PDF ya existe físicamente en `wwwroot/descargados/`.
3. **Omite los ya descargados** y continúa solo con los pendientes.

No es necesario limpiar ningún archivo. Simplemente vuelve a ejecutar `dotnet run`.

---

## Para interrumpir manualmente

Presiona `Ctrl+C`. El proceso cancelará el ciclo actual y terminará limpiamente.

---

## Configuración (`appsettings.json`)

Las credenciales y el token están únicamente en `appsettings.json`. No modificar el código fuente para cambiar configuración.

| Clave | Descripción |
|---|---|
| `Login:User` / `Login:Password` | Credenciales de intranet |
| `Api:BearerToken` | Token Bearer para la API |
| `Concurrency:MaxParallelDownloads` | Descargas simultáneas (defecto: 3) |
