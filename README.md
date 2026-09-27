# Verbal

Verbal is a Windows desktop app that transcribes your speech locally and can
type the transcript into a text field automatically or allow you to copy the transcribed text.
**Whisper model files are not included.**

## Using Verbal

### Requirements

- Windows 10 or later
- A microphone
- At least one compatible Whisper `ggml-*.bin` model in
  `whisper_cpp\models`

The GitHub release is self-contained and includes the app runtime and CPU
Whisper CLI. You do not need to install .NET to use that release.

### Install a Whisper model

Download a compatible Whisper model in `ggml-*.bin` format and place it in the
`whisper_cpp\models` folder next to `Verbal.exe`. Model weights can be quite large and
are intentionally not included with Verbal. The app looks for models when it
starts, so restart it after adding or removing model files.

For example, `ggml-base.en.bin` is a suitable English model. Other compatible
models can be obtained from the
[whisper.cpp model collection](https://huggingface.co/ggerganov/whisper.cpp).

I recommend starting with the `ggml-base.en.bin` model. It is a good balance
between size and accuracy. If you find you need more accuracy, try a larger one.

### Record and insert a transcript

1. Open the destination app and select the text field where you want the
   transcript to go.
2. Start recording in Verbal:
   - Hold **F8** or **Hold to talk** to record while pressed. Release to stop
     and transcribe.
   - Press **F9** or **Start recording** to toggle recording on and off.
3. The transcript appears in Verbal's editable text area. Automatic typing is
   enabled by default. Turn it off if you want to review or edit the transcript
   before inserting it.
4. Use **Insert into target** to type the transcript into the selected field,
   or **Copy** to put it on the clipboard.

The settings menu will allow you to select the model and your microphone.

Verbal remembers the last active window outside itself and restores it before
typing. Windows may prevent foreground activation in some situations. If that
happens, the transcript remains in Verbal so you can select the destination
field and insert it again.

Audio is recorded as temporary 16 kHz mono PCM WAV files and deleted after
transcription. Recording and transcription run **locally on your computer.**

## Download and use a GitHub release

1. Open [Verbal Releases](https://github.com/Aftonone/verbal/releases).
2. Download the appropriate release archive for Windows and extract it.
3. Keep the extracted folder together. It contains `Verbal.exe` and the
   `whisper_cpp` folder with the Whisper CLI and its required DLLs.
4. Add at least one compatible model file to
   `whisper_cpp\models` (see [Install a Whisper model](#install-a-whisper-model)).
5. Run `Verbal.exe`.

Do not move `Verbal.exe` out of the extracted folder; the app needs the adjacent
Whisper files. The release does not contain model weights, so add a model
before recording.

## Clone the repository and build from source

### Requirements

- Windows 10 or later
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A microphone
- At least one compatible Whisper `ggml-*.bin` model in
  `whisper_cpp\models` to transcribe speech

The repository includes the CPU `whisper-cli.exe` and runtime DLLs. Model
weights are excluded from the repository and must be added separately.

Clone the repository:

```powershell
git clone https://github.com/Aftonone/verbal.git
cd verbal
```

Add a compatible model to `whisper_cpp\models` if you want to transcribe
recordings, then run the app from source:

```powershell
dotnet run
```

### Build a self-contained release

Publish a Windows x64 release with the .NET runtime included:

```powershell
dotnet publish .\Verbal.csproj -c Release -r win-x64 --self-contained true -o .\publish
```

Run `.\publish\Verbal.exe` and keep the entire `publish` folder together. The
publish includes the CPU Whisper CLI and its DLLs directly in
`publish\whisper_cpp`. If `whisper_cpp\cuda\whisper-cli.exe` and its DLLs are
present, those files are also included under `publish\whisper_cpp\cuda`. Model
weights are not included; add models to `publish\whisper_cpp\models`. To
prepare a clean release, publish to an empty output folder or remove an older
output folder first so stale files are not left behind.

## Models and inference

Verbal uses CPU inference by default for compatibility across Windows systems.
When `whisper_cpp\cuda\whisper-cli.exe` is present, an inference selector
appears in Settings so users can choose CPU or CUDA. CUDA inference requires
compatible NVIDIA hardware and drivers. Model files are user-managed: add or
remove compatible `ggml-*.bin` files in `whisper_cpp\models` and restart the
app to refresh the model selector. Preferences for the inference backend,
microphone, model, and appearance are stored locally for the current Windows
user.
