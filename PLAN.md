# Windows Tray Speech-to-Text Injector — Development Plan

## 1. Objectives & Success Criteria
- Provide a lightweight Windows tray app that converts live microphone input to text and injects it into the currently focused field while a user-held hotkey is pressed.
- Support both local (Whisper via `whisper.cpp`) and cloud (Azure Speech) STT engines with pluggable architecture.
- Deliver a responsive UX: start transcription within <300 ms of hotkey press, latency <1 s for streamed text.
- Ensure secure handling of configuration and secrets using JSON storage plus DPAPI for sensitive values.
- Produce actionable logs (rolling files) and graceful error handling to aid troubleshooting.

## 2. Stack & Architecture Overview
### 2.1 Platform & Key Versions (verified Dec 2025)
- **Runtime/UI:** .NET 10.0 + WPF desktop (@Windows) — current stable download per Microsoft.
- **Audio Capture:** NAudio **2.2.1** (latest NuGet release).
- **Cloud STT:** Microsoft.CognitiveServices.Speech **1.47.0** (latest NuGet release).
- **Logging:** Serilog **4.3.0** (latest NuGet release).
- **Hosting/DI:** Microsoft.Extensions.Hosting **10.0.1** (ships with .NET 10 SDK).

### 2.2 High-Level Architecture
1. **Tray Host (WPF shell + NotifyIcon):**
   - Runs minimized, exposes context menu (Start/Stop, Settings, Logs, Exit).
   - Hosts lightweight status window for debugging (optional overlay).
2. **Hotkey Listener Service:**
   - Uses Win32 RegisterHotKey for global hotkey + low-level hook to capture press/hold state.
   - Emits start/stop listening events.
3. **Audio Capture Pipeline:**
   - NAudio captures mono PCM at 16 kHz (configurable) with push buffer.
   - Streams samples to active STT engine.
4. **STT Engine Abstraction:**
   - Interface `ISpeechTranscriber` encapsulates start/stop + streaming callbacks.
   - Implementations:
     - **WhisperLocalTranscriber:** wraps `whisper.cpp` via native interop or subprocess server.
     - **AzureSpeechTranscriber:** uses Azure Cognitive Services SDK, handles auth tokens.
5. **Text Injection Service:**
   - Converts interim/final transcripts to `SendInput` keystroke sequences.
   - Handles whitespace/punctuation normalization and optional auto-capitalization.
6. **Configuration & Secrets:**
   - JSON file (app data folder) for general settings; secrets encrypted via DPAPI (ProtectedData APIs).
   - Settings UI dialog for hotkey selection, engine choice, Azure credentials, audio prefs.
7. **Logging & Diagnostics:**
   - Serilog (rolling file sink) or slim custom logger.
   - Log key lifecycle events, errors, performance metrics.
8. **Background Services Coordination:**
   - Use .NET Generic Host inside WPF app for DI, config, hosted services.

## 3. Project Structure & Asset Locations
- `src/VoxThisWay.App` — WPF entry point (UI, tray host, settings dialogs).
- `src/VoxThisWay.Core` — shared models, interfaces (`ISpeechTranscriber`, config entities).
- `src/VoxThisWay.Services` — audio capture, transcribers, hotkey, text injection, logging.
- `src/VoxThisWay.Interop` — P/Invoke wrappers for Win32 hotkeys, SendInput, DPAPI helpers.
- `tests/` — unit/integration test projects mirroring the service/core layers.
- `tools/whisper` — `whisper.cpp` binaries/scripts (CPU + CUDA builds) tracked via Git LFS or release artifacts.
- **Runtime Whisper assets:** install/download models under `%LOCALAPPDATA%/VoxThisWay/Models/Whisper` to avoid elevating permissions; ship bootstrapper that checks this cache and downloads the tiny model if missing.
- **Shared app data:** general settings JSON + DPAPI-protected secrets stored under `%APPDATA%/VoxThisWay/Settings`.

## 3. Detailed Work Breakdown
### Phase 0 – Repo & Tooling Setup
- Initialize .NET 8 WPF project structure.
- Add solution folders: `App`, `Services`, `Infrastructure`, `UI`, `Interop`.
- Configure Serilog, dependency injection, basic app settings file.

### Phase 1 – Tray Shell & Lifecycle
- Implement `App.xaml` startup that hides main window, registers `NotifyIcon`, builds context menu.
- Wire menu actions (Start/Stop listening, Settings, View Logs, Exit).
- Add status notifications (balloon/tooltips) for engine state.

### Phase 2 – Global Hotkey & Input State
- Create `HotkeyService` using Win32 P/Invoke for RegisterHotKey.
- Default combo: **Ctrl + Space** (push-to-talk). Provide settings UI to change the hotkey.
- First release focuses on push-to-talk (press-and-hold). Toggle mode deferred.
- Publish events to start/stop the transcription pipeline.
- Play an audible cue (short WAV/notification sound) when hotkey press is detected to confirm dictation is armed.
- Provide fallback when hotkey registration fails (conflict detection, UI feedback).

### Phase 3 – Audio Capture Pipeline
- Integrate NAudio for WASAPI loopback vs microphone capture (initial focus on default mic).
- Build `AudioCaptureService` with start/stop, buffering, and error recovery.
- Provide pluggable sample format conversions expected by STT implementations.

### Phase 4 – STT Integrations
1. **Abstraction Layer:**
   - Define `TranscriptionConfig`, `TranscriptSegment` models.
   - Event-driven streaming interface for partial/final results.
2. **Whisper Local:**
   - Decide on integration style (embedded `whisper.cpp` via DLL import vs background server process).
   - Manage model files (download path, selection UI, health check) with **tiny** model bundled/downloader by default.
   - Plan for NVIDIA GPU acceleration (CUDA build of `whisper.cpp`) while keeping CPU fallback.
3. **Azure Speech:**
   - Hook Azure Speech SDK (NuGet) with region + key stored via DPAPI.
   - Implement streaming recognition with partial results.
   - Reconnect/backoff logic for transient network errors.
   - Settings UI must allow users to enter/update their own Azure Speech key + region (no bundled credentials); validate and encrypt with DPAPI.

### Phase 5 – Text Injection & Focus Safety
- Implement `TextInjectionService` using `SendInput` to emit keystrokes to the foreground window.
- Buffer interim transcripts; inject incremental text while respecting caret position.
- On hotkey release, simply stop injecting text—no newline or extra terminator.
- Add heuristics to avoid duplicating already injected text (diffing segments).
- Do **not** persist transcripts; discard once injected.

### Phase 6 – Settings UI & Persistence
- Build WPF dialogs for:
  - General settings (autostart, notifications, logging level).
  - Hotkey selection (capture keyboard input, validate conflicts).
  - Engine selection + per-engine configuration (including Azure key/region entry fields with validation + DPAPI encryption).
- Implement settings repository with JSON serialization and DPAPI for secure fields.
- Add import/export of settings (excluding secrets) for backup.

### Phase 7 – Diagnostics, Telemetry & UX Polish
- Add in-app log viewer or button to open log directory.
- Provide microphone level meter + connection status indicator in tray tooltip.
- Optional onboarding wizard to download Whisper models or collect Azure creds.
- Add error dialogs routed through dispatcher to avoid WPF threading issues.

### Phase 8 – Testing & Packaging
- Unit tests for services (hotkey, settings, DPAPI encryption, text diffing).
- Integration smoke tests using dependency injection and mock STT engines.
- Manual test matrix: Windows 10/11, different locales, UAC levels.
- Package via MSIX or self-contained installer (e.g., WiX Toolset / Squirrel) with prerequisites (VC++ runtime, Whisper binaries).

## 4. Key Deliverables
- `voxthisway.sln` with WPF project + supporting libraries.
- Configurable tray application with working hotkey-triggered transcription.
- Dual STT backend support with runtime switch.
- Secure settings storage and diagnostics/logging suite.
- Deployment artifacts + documentation (README, setup guide).

## 5. Risks & Mitigations
| Risk | Impact | Mitigation |
| --- | --- | --- |
| Whisper performance on CPU-only systems | Latency too high | Allow model size selection, support GPU builds, document requirements |
| Hotkey conflicts with other apps | Hotkey unusable | Provide UI validation, fallback to alternate combos, display error notification |
| Text injection blocked by elevated apps/UAC | No output | Detect privilege mismatch, prompt user to run elevated when needed |
| Azure quota or connectivity issues | STT unavailable | Implement retry/backoff, show status, allow offline Whisper fallback |
| DPAPI scope issues (machine vs user) | Settings unreadable after reinstall | Decide scope early (likely CurrentUser), document limitations |
| Microphone access permissions | Capture failure | Detect on startup, surface actionable error, link to Windows privacy settings |

## 6. Testing Strategy
- **Unit Tests:** Mocked services for hotkey handling, configuration, DPAPI wrapper, text diffing/injection logic (without actual SendInput).
- **Integration Tests:** Loopback tests that feed audio samples into STT abstraction and verify transcripts (using short fixtures).
- **Manual QA:** Scripted scenarios (hotkey hold, rapid toggling, switching engines mid-session, network loss, whisper model missing).
- **Performance Monitoring:** Log capture start latency and transcription throughput for regression detection.

## 7. Deployment & Distribution
- Build Release configuration targeting x64 .NET 8.
- Bundle `whisper.cpp` binaries + default model downloader.
- Provide MSIX (preferred) plus ZIP portable build (confirmed acceptable for initial release).
- Document prerequisites (VC++ runtime, microphone permission, Azure key setup).
- Note: installers currently unsigned; add follow-up task to obtain code signing cert before public release.

## 8. User Decisions / Assumptions
1. Default hotkey is **Ctrl + Space**, user-configurable via settings.
2. Launch version supports **push-to-talk only** (hold to dictate); toggle mode deferred.
3. Ship with Whisper **tiny** model and plan for **NVIDIA GPU** acceleration; allow CPU fallback.
4. No additional Azure Speech regional/subscription constraints beyond standard setup.
5. Transcripts **not stored** after injection (no history).
6. No enterprise auto-start or centralized policy requirements.
7. Initial release targets **English-only** recognition; no multi-language auto-detection yet.
8. No external API/WebSocket exposure needed at this time.

## 9. Milestone Task Checklist
- [x] **Phase 0 – Foundation & Tooling:** Initialize repo, .NET 8 WPF solution, DI/logging scaffolding, and configuration storage primitives.
- [x] **Phase 1 – Tray Shell & Hotkey MVP:** Implement NotifyIcon host, global Ctrl+Space push-to-talk hotkey with audible cue, and lifecycle wiring.
- [x] **Phase 2 – Audio + STT Abstraction Layer:** Build NAudio capture pipeline and `ISpeechTranscriber` interface with mock/test harness.
- [x] **Phase 3 – Whisper Local Integration:** Embed `whisper.cpp` tiny model workflow with GPU-aware configuration plus model management UI.
- [x] **Phase 4 – Azure Speech Integration:** Add cloud transcriber implementation with DPAPI-protected credentials and resilience logic.
- [x] **Phase 5 – Text Injection & UX Polish:** Complete SendInput service, caret-safe diffing, status notifications, and settings dialogs.
- [ ] **Phase 6 – Diagnostics & Packaging:** Finalize logging, health checks, installers (MSIX + portable ZIP), and documentation/testing sign-off.

