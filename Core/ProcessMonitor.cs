using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace FamilyTree.Core
{
    /// <summary>
    /// One node in the process family tree.
    /// We keep a flat dictionary keyed by PID and walk up via ParentPid,
    /// because PID reuse means a tree built once can go stale — we re-walk on demand.
    /// </summary>
    public class ProcessNode
    {
        public int Pid;
        public int ParentPid;
        public string Name;
        public string CmdLine;
        public DateTime StartTime;
        public List<ProcessNode> Children = new List<ProcessNode>();
    }

    /// <summary>
    /// Watches process creation/exit via WMI.
    /// WMI is used instead of polling because polling every second misses
    /// short-lived processes (installers, updaters) that live less than a second.
    /// </summary>
    public class ProcessMonitor
    {
        private Dictionary<int, ProcessNode> _nodes = new Dictionary<int, ProcessNode>();
        private ManagementEventWatcher _startWatcher;
        private ManagementEventWatcher _stopWatcher;

        public event Action<ProcessNode> OnProcessStarted;
        public event Action<int> OnProcessExited;

        public void Start()
        {
            SeedExistingProcesses();
            StartWmiWatchers();
        }

        // On startup, we snapshot everything already running so the tree is full,
        // not just processes that started after we launched.
        private void SeedExistingProcesses()
        {
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    var node = BuildNode(p);
                    if (node != null) _nodes[node.Pid] = node;
                }
                catch { /* process exited between GetProcesses and access */ }
            }
        }

        private void StartWmiWatchers()
        {
            try
            {
                _startWatcher = new ManagementEventWatcher(
                    "SELECT * FROM Win32_ProcessStartTrace");
                _startWatcher.EventArrived += OnStartEvent;
                _startWatcher.Start();

                _stopWatcher = new ManagementEventWatcher(
                    "SELECT * FROM Win32_ProcessStopTrace");
                _stopWatcher.EventArrived += OnStopEvent;
                _stopWatcher.Start();
            }
            catch
            {
                // WMI requires admin; if we can't start, we degrade to "no live updates"
                // and the tree still shows the snapshot from SeedExistingProcesses.
                throw new InvalidOperationException("WMI watch failed — run as administrator for live tracking.");
            }
        }

        private void OnStartEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                int pid = Convert.ToInt32(e.NewEvent["ProcessID"]);
                int parentPid = Convert.ToInt32(e.NewEvent["ParentProcessID"]);
                string name = (string)e.NewEvent["ProcessName"];

                var node = new ProcessNode
                {
                    Pid = pid,
                    ParentPid = parentPid,
                    Name = name,
                    StartTime = DateTime.Now
                };
                _nodes[pid] = node;
                if (OnProcessStarted != null) OnProcessStarted(node);
            }
            catch { /* malformed event, skip */ }
        }

        private void OnStopEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                int pid = Convert.ToInt32(e.NewEvent["ProcessID"]);
                _nodes.Remove(pid);
                if (OnProcessExited != null) OnProcessExited(pid);
            }
            catch { }
        }

        private ProcessNode BuildNode(Process p)
        {
            var node = new ProcessNode
            {
                Pid = p.Id,
                Name = p.ProcessName,
                StartTime = SafeStartTime(p)
            };
            try
            {
                using (var query = new ManagementObject(
                    string.Format("Win32_Process.Handle='{0}'", p.Id)))
                {
                    node.ParentPid = Convert.ToInt32(query["ParentProcessId"]);
                    node.CmdLine = (string)query["CommandLine"] ?? "";
                }
            }
            catch { }
            return node;
        }

        // Some system processes (System Idle Process) don't expose StartTime.
        private DateTime SafeStartTime(Process p)
        {
            try { return p.StartTime; }
            catch { return DateTime.MinValue; }
        }

        /// <summary>
        /// Returns the tree rooted at PID 0 (System Idle Process).
        /// We rebuild children on each call because PID reuse can orphan nodes.
        /// </summary>
        public List<ProcessNode> GetTree()
        {
            foreach (var n in _nodes.Values) n.Children.Clear();
            var roots = new List<ProcessNode>();
            foreach (var n in _nodes.Values)
            {
                if (_nodes.ContainsKey(n.ParentPid))
                    _nodes[n.ParentPid].Children.Add(n);
                else
                    roots.Add(n); // orphaned (parent exited) -> root
            }
            return roots;
        }

        public ProcessNode FindNode(int pid)
        {
            ProcessNode n;
            return _nodes.TryGetValue(pid, out n) ? n : null;
        }

        public void Stop()
        {
            try { if (_startWatcher != null) _startWatcher.Stop(); } catch { }
            try { if (_stopWatcher != null) _stopWatcher.Stop(); } catch { }
        }
    }
}
