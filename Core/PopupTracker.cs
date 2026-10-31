using System;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace FamilyTree.Core
{
    public class PopupEvent
    {
        public IntPtr Hwnd;
        public string WindowTitle;
        public int OwningPid;
        public string OwningProcessName;
        public DateTime Timestamp;
    }

    /// <summary>
    /// Hooks the Win32 event system to watch for new top-level windows.
    /// We filter to "visible, top-level, not owned by us" windows so we catch
    /// real popups/dialogs, not child controls or our own form.
    /// </summary>
    public class PopupTracker
    {
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

        private const uint EVENT_OBJECT_CREATE = 0x8000;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint GA_ROOT = 2;

        private IntPtr _hook;
        private WinEventDelegate _del; // keep a reference so GC doesn't collect it
        public event Action<PopupEvent> OnPopup;

        public void Start()
        {
            _del = new WinEventDelegate(OnWinEvent);
            _hook = SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_CREATE,
                IntPtr.Zero, _del, 0, 0, WINEVENT_OUTOFCONTEXT);
            if (_hook == IntPtr.Zero)
                throw new InvalidOperationException("Failed to hook Win32 window events.");
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // We only care about window objects (idObject=OBJID_WINDOW), not child items.
            if (idObject != 0) return;
            if (!IsWindowVisible(hwnd)) return;

            // Only top-level windows; GetAncestor GA_ROOT gives the real top window.
            IntPtr root = GetAncestor(hwnd, GA_ROOT);
            if (root != hwnd) return;

            uint pid;
            GetWindowThreadProcessId(root, out pid);
            if (pid == 0) return;

            var sb = new StringBuilder(256);
            GetWindowText(root, sb, sb.Capacity);
            string title = sb.ToString();
            if (string.IsNullOrEmpty(title)) return; // skip windows without titles

            try
            {
                var p = System.Diagnostics.Process.GetProcessById((int)pid);
                if (OnPopup != null)
                    OnPopup(new PopupEvent
                    {
                        Hwnd = root,
                    WindowTitle = title,
                    OwningPid = (int)pid,
                    OwningProcessName = p.ProcessName,
                    Timestamp = DateTime.Now
                });
            }
            catch { /* process exited */ }
        }

        public void Stop()
        {
            if (_hook != IntPtr.Zero) UnhookWinEvent(_hook);
        }
    }
}
