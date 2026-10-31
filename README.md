# Family Tree

**Stop asking "what spawned this" and "who popped up this ad". Watch the process family tree live.**

Most tools tell you what is running. Family Tree tells you *who spawned it* and *which process popped up that dialog*. It draws the parent-child process tree in real time, and highlights the exact process responsible when a new popup appears.

No installer, no cloud. One WinForms exe, .NET Framework 4.8.

---

## Why

- An ad window appears. You close it. 10 minutes later it's back. Who keeps launching it?
- You uninstalled software, but a background process still respawns. What is its parent?
- Explorer spawned a mysterious process at boot. Where did it come from?

Task Manager shows the flat list. Family Tree shows the family tree — and when a popup appears, it jumps straight to the parent process.

## Features

| | |
|---|---|
| **Live process tree** | Watches process start/exit via WMI and draws the full parent-child tree. |
| **Popup tracking** | Hooks Win32 window events; when a new top-level window appears, it logs which process owns it and highlights it in the tree. |
| **Command line inspection** | Select a node to see its full command line in the title bar. |
| **No polling** | WMI event subscriptions catch processes that live less than a second (installers, updaters). |

## Quick start

```bash
# build (uses the in-box C# compiler, needs admin for WMI live tracking)
build.cmd

# run as administrator (WMI process events require elevation)
bin\FamilyTree.exe
```

Left panel: process family tree. Right panel: detected popups. When a popup appears, the owning process is auto-selected in the tree.

## Positioning

Family Tree is a **diagnostic tool** for your own machine. It shows lineage; it does not kill, block, or modify anything.

Not for: tampering with managed devices at work, or hiding processes from other users.

## License

[MIT](LICENSE).

---

If this helps you catch the process that keeps popping up ads, a ⭐ is appreciated.
