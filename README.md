# Verbal

A native Windows desktop utility for transcribing speech into the active app.
Audio recording and transcription are handled locally using the bundled
`whisper-cli.exe`. Whisper model files are user-provided and are not bundled.

## What you need

- Windows 10 or later
- .NET 8 desktop runtime (or publish as a self-contained app)
- The bundled `whisper_cpp` CLI files
- At least one compatible Whisper `ggml-*.bin` model in `whisper_cpp\models`
- A microphone

## Build and Run Source Code

From the source code directory:

```powershell
dotnet run
```

Compile a self-contained Windows x64 publish:

```powershell
dotnet publish .\Verbal.csproj -c Release -r win-x64 --self-contained true -o .\publish
```

Then start `.\publish\Verbal.exe`. The publish step places the CPU CLI and its
DLLs in `publish\whisper_cpp\cpu`, alongside the self-contained .NET runtime.
If a CUDA build is available under `whisper_cpp\cuda`, its files are published
to `publish\whisper_cpp\cuda` as well. Whisper model files are deliberately not
bundled. Add and remove compatible `ggml-*.bin` model files in
`publish\whisper_cpp\models`; restart the app after changing the folder so the
model selector refreshes.

Distribute and keep the whole `publish` folder together; the executable alone
is not the complete release. The model directory includes a small README with
these instructions. The current model-free publish is about 196 MiB; add-on
models do not increase the app release size.

## Use

1. Select the destination text field in another application.
2. Open **Settings** (gear button) to choose the default or a specific
   microphone and a Whisper model. Model files are discovered from
   `whisper_cpp\models`; `ggml-base.en.bin` is the default when available.
   Restart the app after adding or removing model files so the list refreshes.
   Choose CPU or CUDA inference when both CLI variants are installed.
   The radial input indicator around the status dot shows the live level while
   recording.
   Use **Dark mode** in Settings to switch the app appearance; the choice is
   saved for the next launch in your local app settings.
3. Hold **F8** (or the on-screen **Hold to talk** button) to record; release to
   transcribe.
4. Or press **F9** (or **Start recording**) to toggle recording.
5. The transcript appears in the editable text area. Automatic typing is on by
   default; turn it off to review text before inserting it.
6. Use **Insert into target** or **Copy** as needed.

The app remembers the last active window outside itself and restores it before
typing. Windows can block foreground activation in some circumstances; if that
happens, the transcript remains in the app so you can select the destination
and insert it again.

Recording uses temporary 16 kHz mono PCM WAV files. Audio is deleted after
transcription. Verbal defaults to CPU inference for broad hardware
compatibility. To offer a CUDA option, place its executable and required DLLs
in `whisper_cpp\cuda`; keep a separately organized CPU build in
`whisper_cpp\cpu` with its executable named `whisper-cli.exe`.
If the root `whisper_cpp` folder contains `whisper-cli.exe`, publishing places
that executable and its DLLs in the CPU folder automatically. When both
backends are installed, CPU is selected by default; users can choose CUDA in
Settings. CUDA inference requires a CUDA-enabled build and a compatible NVIDIA
GPU/driver. The selected backend, microphone, and model preferences are saved
alongside the appearance setting.
