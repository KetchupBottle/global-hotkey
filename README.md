# GlobalHotKey

A small Windows tray app that turns key combinations into actions. Press `Ctrl+Alt+X` anywhere,
in any program, and it opens what you told it to open: a program with arguments, a folder, or a URL.

Windows .NET 10, WinForms. Ships as one portable `.exe` with no runtime to install.

## Using it

Add a hotkey, click the shortcut box, press the combination you want, then pick what it opens.
Closing the window hides it; the app keeps running in the notification area and the shortcuts stay
live. The tray menu has **Open**, **Start with Windows** and **Exit**.

If nothing appears when you press a combination, another program already owns it. Windows tells the
app when a registration is refused, and the list shows those rows as `Not registered`.

Only one instance runs at a time, since two would fight over the same registrations.

## Configuration

`%AppData%\GlobalHotKey\hotkeys.json`, written atomically so a crash mid-save cannot truncate it.

```json
[
  {
    "modifiers": "Alt, Control",
    "key": "X",
    "action": "Program",
    "target": "C:\\Windows\\notepad.exe",
    "arguments": "C:\\notes.txt"
  },
  { "modifiers": "Alt, Control", "key": "D", "action": "Folder", "target": "C:\\Users" },
  { "modifiers": "Control, Shift", "key": "G", "action": "Url", "target": "https://github.com" }
]
```

`modifiers` is a comma-separated list of `Alt`, `Control`, `Shift`, `Win`, serialized in that
numeric order regardless of how the UI displays it. `action` is `Program`, `Folder` or `Url`;
`arguments` applies to `Program` only. If the file is not valid JSON the app copies it to
`hotkeys.json.corrupt`, starts empty and says so in a tray balloon.

## Building

```
dotnet build
dotnet test                                  # 46 tests
dotnet test --filter "Category!=Integration" # the 28 that touch nothing outside the process
```

Integration tests use the real Win32 hotkey API, the filesystem and the registry. They register
`Ctrl+Alt+Shift+F22/F23`, which nothing else claims, and always unregister afterwards. Registry
tests write to a throwaway key under `HKCU\Software\GlobalHotKey.Tests` and delete it; the real
`Run` key is never touched. One test synthesizes real key presses, so it needs an interactive
desktop session and fails if an elevated window has focus (UIPI blocks synthetic input).

## Publishing the portable exe

```
dotnet publish src/GlobalHotKey/GlobalHotKey.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

Self-contained, so it runs on a Windows machine with no .NET installed. Not trimmed: the SDK blocks
trimming for WinForms, which relies on COM marshalling the trimmer cannot analyze.

## The icon

`src/GlobalHotKey/app.ico` is generated, not hand-drawn. `tools/MakeIcon.cs` is a one-off .NET 10
file-based app that draws the keycap with `System.Drawing` and writes the 7-frame `.ico` directly:

```
dotnet run --file tools/MakeIcon.cs -- src/GlobalHotKey/app.ico <preview-dir>
```

It also writes PNG previews and a contact sheet so the small sizes can be checked before committing.

## Known limitation

"Start with Windows" writes to `HKCU\...\CurrentVersion\Run`. If you later disable the app from the
Startup tab in Task Manager, Windows records that separately in `StartupApproved` and the tray menu
will still show the option as checked. Toggle it from the tray menu to keep the two in sync.
