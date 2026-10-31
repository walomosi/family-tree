using System;
using System.Drawing;
using System.Windows.Forms;
using FamilyTree.Core;

namespace FamilyTree
{
    public class MainForm : Form
    {
        private TreeView _tree;
        private ListView _popupList;
        private ProcessMonitor _monitor;
        private PopupTracker _tracker;

        public MainForm()
        {
            Text = "Family Tree - who spawned what, and who pops up";
            Width = 1000; Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();
            StartMonitoring();
        }

        // Split into left tree / right popups, with a refresh button on top.
        private void BuildLayout()
        {
            var lbl = new Label();
            lbl.Text = "Process family tree (PID -> parent -> children):";
            lbl.Top = 10; lbl.Left = 12; lbl.Width = 500;
            Controls.Add(lbl);

            _tree = new TreeView();
            _tree.Top = 30; _tree.Left = 12;
            _tree.Width = 600; _tree.Height = 620;
            _tree.AfterSelect += OnTreeSelected;
            Controls.Add(_tree);

            var lblPop = new Label();
            lblPop.Text = "Detected popups (who popped up what):";
            lblPop.Top = 10; lbl.Left = 630; lblPop.Width = 350;
            Controls.Add(lblPop);

            _popupList = new ListView();
            _popupList.Top = 30; _popupList.Left = 630;
            _popupList.Width = 350; _popupList.Height = 620;
            _popupList.View = View.Details;
            _popupList.FullRowSelect = true;
            _popupList.Columns.Add("Time", 70);
            _popupList.Columns.Add("Process", 100);
            _popupList.Columns.Add("PID", 50);
            _popupList.Columns.Add("Window title", 130);
            Controls.Add(_popupList);

            var btnRefresh = new Button();
            btnRefresh.Text = "Refresh tree";
            btnRefresh.Top = 10; btnRefresh.Left = 500;
            btnRefresh.Width = 90; btnRefresh.Height = 24;
            btnRefresh.Click += (s, e) => RebuildTree();
            Controls.Add(btnRefresh);
        }

        private void StartMonitoring()
        {
            _monitor = new ProcessMonitor();
            _monitor.OnProcessStarted += node =>
            {
                try { _tree.BeginInvoke(new Action(() => AddNodeToTree(node))); } catch { }
            };
            _monitor.OnProcessExited += pid =>
            {
                try { _tree.BeginInvoke(new Action(() => RemoveNodeFromTree(pid))); } catch { }
            };
            try { _monitor.Start(); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            _tracker = new PopupTracker();
            _tracker.OnPopup += delegate(PopupEvent popup)
            {
                try
                {
                    _popupList.BeginInvoke(new Action(() => AddPopup(popup)));
                }
                catch { }
            };
            _tracker.Start();

            RebuildTree();
        }

        // Walk the flat node dict and attach TreeNodes with the "name (pid)" label.
        private void RebuildTree()
        {
            _tree.Nodes.Clear();
            var roots = _monitor.GetTree();
            foreach (var root in roots)
            {
                var tn = BuildTreeNode(root);
                _tree.Nodes.Add(tn);
            }
        }

        private TreeNode BuildTreeNode(ProcessNode node)
        {
            var tn = new TreeNode(string.Format("{0} ({1})", node.Name, node.Pid));
            tn.Tag = node.Pid;
            foreach (var child in node.Children)
                tn.Nodes.Add(BuildTreeNode(child));
            return tn;
        }

        private void AddNodeToTree(ProcessNode node)
        {
            // Try to attach under parent if we know it, else add to root.
            var parentNode = _monitor.FindNode(node.ParentPid);
            var newTn = new TreeNode(string.Format("{0} ({1})", node.Name, node.Pid)) { Tag = node.Pid };
            if (parentNode != null)
            {
                var parentTn = FindTreeNodeByTag(_tree.Nodes, node.ParentPid);
                if (parentTn != null) parentTn.Nodes.Add(newTn);
                else _tree.Nodes.Add(newTn);
            }
            else
            {
                _tree.Nodes.Add(newTn);
            }
        }

        private void RemoveNodeFromTree(int pid)
        {
            var tn = FindTreeNodeByTag(_tree.Nodes, pid);
            if (tn != null) tn.Remove();
        }

        private TreeNode FindTreeNodeByTag(TreeNodeCollection coll, int pid)
        {
            foreach (TreeNode tn in coll)
            {
                if ((int)tn.Tag == pid) return tn;
                var found = FindTreeNodeByTag(tn.Nodes, pid);
                if (found != null) return found;
            }
            return null;
        }

        private void AddPopup(PopupEvent popup)
        {
            // When a popup appears, also try to highlight the owning process in the tree.
            var item = new ListViewItem(popup.Timestamp.ToString("HH:mm:ss"));
            item.SubItems.Add(popup.OwningProcessName);
            item.SubItems.Add(popup.OwningPid.ToString());
            item.SubItems.Add(popup.WindowTitle);
            _popupList.Items.Insert(0, item);

            var tn = FindTreeNodeByTag(_tree.Nodes, popup.OwningPid);
            if (tn != null)
            {
                _tree.SelectedNode = tn;
                tn.EnsureVisible();
            }
        }

        private void OnTreeSelected(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag == null) return;
            int pid = (int)e.Node.Tag;
            var node = _monitor.FindNode(pid);
            if (node != null && !string.IsNullOrEmpty(node.CmdLine))
                Text = string.Format("Family Tree - {0}: {1}", node.Name, node.CmdLine);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_monitor != null) _monitor.Stop();
            if (_tracker != null) _tracker.Stop();
            base.OnFormClosing(e);
        }
    }
}
