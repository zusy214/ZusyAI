# Deployment and packaging

## Windows desktop installer

From `frontend`, build the Windows x64 installer:

```powershell
npm run desktop:build
```

This runs the Vite production build and then `electron-builder --win`. The current Electron Builder configuration produces an NSIS per-user installer at:

```text
frontend/release/ZusyAI Setup 0.0.0.exe
```

The supplied green layered mark is used for the in-app logo, browser favicon, Electron window, and Windows installer icon.

The unpacked desktop app is placed in `frontend/release/win-unpacked/`. The package metadata currently sets version `0.0.0`; update `frontend/package.json` before making a versioned release.

The installer is not code-signed because no signing certificate is configured. Windows may show a SmartScreen warning. The default Electron application icon is also used.

## Runtime requirements after installation

The installer contains the Vue build and Electron shell only. It does **not** bundle or start the .NET API, Ollama, or the model. On the machine running the installed app:

1. Install Ollama and pull the configured text model. For image questions, also install Ollama 0.12.7+ and pull `qwen3-vl:2b`.
2. Run the .NET API so it listens at `127.0.0.1:5030`.
3. Launch ZusyAI.

The Electron main process forwards chat requests to that fixed loopback API address. A machine without the API running will show a connection error. The API's default model and Ollama URL are configured in `backend/ZusyAI.Api/appsettings.json`.

## Publishing the API

The API is not currently included in the desktop installer. To publish it separately from the repository root:

```powershell
dotnet publish .\backend\ZusyAI.Api\ZusyAI.Api.csproj -c Release -o .\publish\api
```

The default publish is framework-dependent. Install the ASP.NET Core 9 runtime on the target machine, then run the API bound to loopback:

```powershell
$env:ASPNETCORE_URLS = "http://127.0.0.1:5030"
dotnet .\publish\api\ZusyAI.Api.dll
```

For Local mode, the target machine also needs Ollama and the configured local model; image questions additionally need the configured vision model. For Cloud mode, configure `OLLAMA_API_KEY` in the protected environment of the API process; an installed desktop client does not need to hold the key. Cloud prompts, images, and extracted document text leave the PC and usage is billed by Ollama. Keep the API bound to loopback: it has no authentication and is intended for a local desktop client, not public hosting.

## Release checklist

- Set a real app version in `frontend/package.json`.
- Build and test the installer on a clean Windows machine.
- Confirm the .NET 9 runtime is installed on the target machine.
- For Local mode, confirm Ollama and the configured local model are installed.
- For Cloud mode, configure `OLLAMA_API_KEY` in the API process environment and confirm the account can use the configured cloud model.
- Decide how the API will be launched and kept running; the current installer does not manage it.
- Configure a Windows code-signing certificate before distributing broadly.
