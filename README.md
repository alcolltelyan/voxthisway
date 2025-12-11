# VoxThisWay – Windows Tray Speech‑to‑Text Injector

VoxThisWay is a lightweight Windows tray app that listens to your microphone while a push‑to‑talk hotkey is held, transcribes speech using either a local Whisper engine or Azure Speech, and injects the resulting text into the currently focused field using synthetic keystrokes.

This document explains how to:

- Install prerequisites
- Obtain and arrange Whisper local assets
- Build, run, and test the app from source
- Build a portable ZIP for distribution (Strategy A)

---

## 1. Prerequisites

### 1.1 Platform

- **OS:** Windows 10 or 11 (x64)
- **.NET SDK:** .NET 10 SDK (or the version configured in the solution)
- **Build tools:**
  - Visual Studio 2022 or later **or**
  - `dotnet` CLI with MSBuild

### 1.2 Whisper local assets

The app can use a local Whisper engine via a `whisper_cli.exe`‑style binary plus a Whisper model file.

**Expected layout (relative to `VoxThisWay.App.exe`):**

```text
VoxThisWay/                  # app root (any folder name is fine)
  VoxThisWay.App.exe        # WPF tray app
  (other .dll / config files)
  Speech/
    whisper_cli.exe         # Whisper CLI executable
    Models/
      ggml-tiny.en.bin      # default Whisper model file
```

At runtime, the app resolves paths like:

- Executable: `Speech/whisper_cli.exe`
- Model: `Speech/Models/ggml-tiny.en.bin`

> **Note:** The exact Whisper binary and model are not included in this repository. You must obtain them yourself from a trusted source (e.g. official `whisper.cpp` releases) and place them in the `Speech/` folder as shown above.

### 1.3 Azure Speech (optional)

If you want to use Azure Speech instead of Whisper local:

- Create an Azure Cognitive Services Speech resource.
- Obtain an **API key** and **region**.
- You will enter these in the app’s **Settings → Speech engine → Azure Speech** section at runtime.

The key is stored securely using DPAPI via the `AzureSpeechCredentialStore` implementation.

---

## 2. Building the application from source

### 2.1 Clone the repository

```powershell
cd d:\Projects
git clone <your-repo-url> voxthisway
cd .\voxthisway
```

(Adjust paths as appropriate; the examples below assume `d:\Projects\voxthisway`.)

### 2.2 Restore and build

Use the `dotnet` CLI from the repo root:

```powershell
# Restore all projects
dotnet restore

# Build in Debug
dotnet build -c Debug

# Build in Release
dotnet build -c Release
```

You can also open the solution in Visual Studio and build using the IDE.

---

## 3. Running the app (Debug)

From the repo root:

```powershell
# Run the WPF tray app in Debug configuration
dotnet run -c Debug --project src\VoxThisWay.App\VoxThisWay.App.csproj
```

Behavior:

- The main window hides; a tray icon appears in the system tray.
- By default, the hotkey is **Ctrl+Space** (you can change it in Settings).
- Hold the hotkey to start dictation; release to stop.

For Whisper local to work, ensure the `Speech/` folder with `whisper_cli.exe` and the model is present **next to** the built `VoxThisWay.App.exe` for the configuration you are running (Debug/Release). In Debug runs via `dotnet run`, the base directory will be the build output folder for that configuration (e.g. `bin\Debug\netX\`).

If Whisper assets are missing and `WhisperLocal` is the active engine, the app will:

- Log a warning indicating the missing executable/model path.
- Show a tray balloon: **"Whisper local not ready"** with instructions to ensure the `Speech` folder is present.

---

## 4. Using the application

### 4.1 Tray menu

Right‑click the tray icon to access:

- **Start Listening** / **Stop Listening**
- **Settings…**
- **View Logs**
- **Exit**

### 4.2 Settings window

Settings are stored in a JSON file under `%APPDATA%\VoxThisWay\Settings` and include:

- **Input device**
  - Dropdown listing available audio input devices.
  - **Test** button to run a short microphone test.

- **Speech engine**
  - `Whisper (local)` – uses the local Whisper executable/model.
  - `Azure Speech` – uses Azure Cognitive Services.
  - When `Azure Speech` is selected:
    - Shows **Azure Speech API key** panel.
    - Displays a summary such as `An API key is configured (…1234).`
    - You can enter a new key; leaving the field blank keeps the existing key.

- **Push‑to‑talk hotkey**
  - Shows the current hotkey (e.g. `Ctrl+Space` or `F9`).
  - **Change** button to capture a new combination. Plain keys (e.g. `F9`) and combinations with modifiers are supported.

When you click **Save**, the app:

- Writes updated settings to the JSON store.
- Persists any new Azure key via the secure credential store.
- Restarts the hotkey service so the new hotkey takes effect immediately.

### 4.3 Microphone test

In **Settings → Input device**:

1. Select the desired input device.
2. Click **Test**.
3. The app briefly starts capture (~2 seconds) and reports:
   - **Success**: number of audio buffers received.
   - **Warning**: no audio buffers received (check mic, device, or OS audio settings).
   - **Error**: if capture fails to start.

This test does not start transcription; it only verifies audio capture from the selected device.

---

## 5. Logs and diagnostics

Logs are written to:

- `%LOCALAPPDATA%\VoxThisWay\Logs\voxthisway.log`

Logging configuration:

- Minimum level: **Information**
- Microsoft framework logs: **Warning** and above
- High‑volume, per‑chunk diagnostics (Whisper internals, text injection details) are logged at **Debug** level and are generally not present in normal runs.

Use **View Logs** from the tray menu to open the logs directory in Explorer.

---

## 6. Building a portable ZIP

To produce a portable ZIP suitable for end‑users (Strategy A):

1. **Publish the app in Release**

   From the repo root:

   ```powershell
   dotnet publish src\VoxThisWay.App\VoxThisWay.App.csproj -c Release -r win-x64 --self-contained false -o .\publish\VoxThisWay
   ```

   This creates a folder like `publish\VoxThisWay\` containing `VoxThisWay.App.exe` and its dependencies.

2. **Add Whisper local assets**

   Inside `publish\VoxThisWay\` create:

   ```text
   Speech\
     whisper_cli.exe
     Models\
       ggml-tiny.en.bin
   ```

   Copy your Whisper executable and model into those paths.

3. **Create the ZIP**

   ```powershell
   cd .\publish
   Compress-Archive -Path .\VoxThisWay\* -DestinationPath .\VoxThisWay.zip
   ```

   Distribute `VoxThisWay.zip` to users.

4. **User installation**

   - User downloads `VoxThisWay.zip`.
   - User extracts it to any folder (e.g. `C:\Tools\VoxThisWay`).
   - The extracted folder should contain:

     ```text
     VoxThisWay\
       VoxThisWay.App.exe
       (other .dll/config files)
       Speech\
         whisper_cli.exe
         Models\
           ggml-tiny.en.bin
     ```

   - User runs `VoxThisWay.App.exe`; the tray icon appears.

---

## 7. Testing checklist

When validating a build (Debug or ZIP Release), run through:

1. **Hotkey & dictation**
   - Verify the default hotkey works (or a custom one you configure in Settings).
   - Dictate into a text editor (Notepad) and ensure text appears and spaces between segments are correct.

2. **Whisper local**
   - With `Whisper (local)` selected in Settings and `Speech/` present:
     - Confirm no "Whisper local not ready" warning appears.
     - Dictate and verify local transcription works.

3. **Azure Speech** (if configured)
   - Switch engine to `Azure Speech`.
   - Enter valid Azure key and region.
   - Dictate and confirm cloud transcription works.

4. **Device switching**
   - Change the input device in Settings.
   - Use **Test** to verify the new device captures audio.
   - Dictate again and verify transcription.

5. **Logging**
   - Trigger a few sessions.
   - Open logs via the tray menu and confirm entries are being written and no unexpected errors appear.

---

## 8. Troubleshooting notes

- **Whisper local not ready**
  - Verify that `Speech/whisper_cli.exe` and `Speech/Models/ggml-tiny.en.bin` exist next to `VoxThisWay.App.exe`.
  - Confirm file names match exactly.

- **No text appears in target app**
  - Ensure the target window has focus and accepts text input.
  - Check that the hotkey is not conflicting with other software.
  - Review logs in `%LOCALAPPDATA%\VoxThisWay\Logs` for `SendInput` errors.

- **Microphone test reports no buffers**
  - Confirm the correct input device is selected.
  - Check Windows sound settings and privacy permissions for microphone access.

- **Azure Speech fails**
  - Verify your key and region in Settings.
  - Confirm the resource is active in Azure and that you have not exceeded quota.

This README is meant as the canonical guide for building, running, and distributing VoxThisWay via a portable ZIP. Keep it in sync with any future changes to paths, engines, or packaging strategy.
