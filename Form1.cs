using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Threading;
using ZetaLongPaths;

namespace VDU
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            timer1.Start();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string dir = textBox1.Text;
            try
            {
                ShutdownThread();
                m_threadAlive = true;
                m_rootUpdated = false;
                m_thread = new Thread(delegate() { CreateNodeTree(dir, null); lock (UpdatedLock) { m_currentDir = "[Done]"; m_rootUpdated = true; } });
                m_thread.Start();
            }
            catch (Exception exn)
            {
                MessageBox.Show(exn.ToString());
            }
        }

        private void ShutdownThread()
        {
            if (m_thread != null)
            {
                m_threadAlive = false;
                if (!m_thread.Join(1000))
                    m_thread.Abort();
                m_thread = null;
            }
        }

        Dictionary<string, List<ListViewItem>> d = new Dictionary<string, List<ListViewItem>>();
        Thread m_thread = null;
        ConcentricRingTree.Node rootNode;
        object UpdatedLock = new object();
        string m_currentDir = "[Done]";
        bool m_rootUpdated = false;
        bool m_threadAlive = true;

        Dictionary<string, Tuple<DateTime, List<ListViewItem>, long>> _cache = new Dictionary<string, Tuple<DateTime, List<ListViewItem>, long>>();

        private ConcentricRingTree.Node CreateNodeTree(string dir, ConcentricRingTree.Node parent)
        {
            lock (UpdatedLock)
            {
                m_currentDir = dir;
                m_rootUpdated = true;
            }
            var node = new ConcentricRingTree.Node();
            node.Label = dir;
            node.Value = 0;
            node.Children = new List<ConcentricRingTree.Node>();
            if (!m_threadAlive)
                return node;
            List<ListViewItem> llvi = new List<ListViewItem>();
            var zdir = new ZlpDirectoryInfo(dir);
            Tuple<DateTime, List<ListViewItem>, long> cachedInfo;
            var items = new List<ListViewItem>();
            long totalLen = 0;
            if (_cache.TryGetValue(dir, out cachedInfo) && zdir.LastWriteTime == cachedInfo.Item1)
            {
                foreach (var lvi in cachedInfo.Item2)
                    items.Add(lvi);
                totalLen = cachedInfo.Item3;
            }
            else
            {
                foreach (var zfile in zdir.GetFiles())
                {
                    var lvi = new ListViewItem(new string[] {
                    zfile.Name,
                    zfile.LastWriteTime.ToShortDateString() + " " + zfile.LastWriteTime.ToShortTimeString(),
                    zfile.Extension,
                    SizeString(zfile.Length) });
                    lvi.Tag = zfile.Length;
                    llvi.Add(lvi);
                    totalLen += zfile.Length;
                    items.Add(lvi);
                    if (!m_threadAlive)
                        return node;
                }
                items.Sort((a, b) => (Math.Sign(((long)b.Tag) - (long)a.Tag)));
                _cache[dir] = Tuple.Create(zdir.LastWriteTime, llvi, totalLen);
            }
            node.Value += totalLen;
            lock (UpdatedLock)
                if (parent != null)
                    parent.AddChild(node);
                else
                    rootNode = node;
            lock (UpdatedLock)
                m_rootUpdated = true;
            d[dir.ToLower()] = items;
            foreach (var zsubdir in zdir.GetDirectories())
            {
                try
                {
                    CreateNodeTree(zsubdir.FullName, node);
                }
                catch (UnauthorizedAccessException)
                { }
            }
            lock (UpdatedLock)
                m_rootUpdated = true;
            return node;
        }

        ConcentricRingTree.Node lastNode = null;

        private void concentricRingTree1_SelectedNodeChanged()
        {
            if (concentricRingTree1.SelectedNode == null)
            {
                textBox2.Text = "";
                return;
            }
            if (lastNode == concentricRingTree1.SelectedNode)
            {
                return;
            }
            lastNode = concentricRingTree1.SelectedNode;

            List<ListViewItem> lfi;
            if (d.TryGetValue(concentricRingTree1.SelectedNode.Label.ToLower(), out lfi))
            {
                listView1.Items.Clear();
                listView1.Items.AddRange(lfi.ToArray());
            }

            textBox2.Text = string.Format("{0,10} {1,10} {2}",
                SizeString(concentricRingTree1.SelectedNode.Value),
                SizeString(concentricRingTree1.SelectedNode.TotalValue),
                concentricRingTree1.SelectedNode.Label);
        }

        private string SizeString(double p)
        {
            if (p < 1024) return string.Format("{0} bytes", p);
            if (p < 10240) return string.Format("{0:0.00} KB", p / 1024.0);
            if (p < 102400) return string.Format("{0:0.0} KB", p / 1024.0);
            if (p < 1048576) return string.Format("{0:0} KB", p / 1024.0);
            if (p < 10485760) return string.Format("{0:0.00} MB", p / 1048576.0);
            if (p < 104857600) return string.Format("{0:0.0} MB", p / 1048576.0);
            if (p < 1024.0 * 1048576) return string.Format("{0:0} MB", p / 1048576.0);
            if (p < 10240.0 * 1048576) return string.Format("{0:0.00} GB", p / (1024 * 1048576.0));
            if (p < 102400.0 * 1048576) return string.Format("{0:0.0} GB", p / (1024 * 1048576.0));
            return string.Format("{0:0} GB", p / (1024 * 1048576.0));
        }

        private void concentricRingTree1_DoubleClick(object sender, EventArgs e)
        {
        }

        private void concentricRingTree1_Click(object sender, EventArgs e)
        {
        }

        private void concentricRingTree1_MouseClick(object sender, MouseEventArgs e)
        {
            if (concentricRingTree1.SelectedNode == null)
                return;

            if (e.Button == MouseButtons.Right)
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.WorkingDirectory = concentricRingTree1.SelectedNode.Label;
                Process.Start(psi);
                return;
            }

            if (concentricRingTree1.SelectedNode == concentricRingTree1.Root)
            {
                textBox1.Text = Path.GetDirectoryName(textBox1.Text);
            }
            else
            {
                textBox1.Text = concentricRingTree1.SelectedNode.Label;
            }
            button1_Click(null, null);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            lock (UpdatedLock)
            {
                if (m_rootUpdated)
                {
                    labelCurrentAction.Text = m_currentDir;
                    concentricRingTree1.Root = rootNode;
                    concentricRingTree1.UpdateStructure();
                    m_rootUpdated = false;
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            ShutdownThread();
        }
    }
}
