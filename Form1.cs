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
                m_thread = new Thread(delegate() { CreateNodeTree(dir, null); });
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
        bool m_rootUpdated = false;
        bool m_threadAlive = true;

        private ConcentricRingTree.Node CreateNodeTree(string dir, ConcentricRingTree.Node parent)
        {
            var node = new ConcentricRingTree.Node();
            node.Label = dir;
            node.Value = 0;
            node.Children = new List<ConcentricRingTree.Node>();
            if (!m_threadAlive)
                return node;
            List<ListViewItem> lfi = new List<ListViewItem>();
            foreach (string file in Directory.GetFiles(dir))
            {
                var fi = new FileInfo(file);
                var lvi = new ListViewItem(new string[] {
                    fi.Name,
                    fi.LastWriteTime.ToShortDateString() + " " + fi.LastWriteTime.ToShortTimeString(),
                    fi.Extension,
                    SizeString(fi.Length) });
                lvi.Tag = fi.Length;
                lfi.Add(lvi);
                node.Value += fi.Length;
                if (!m_threadAlive)
                    return node;
            }
            lock (UpdatedLock)
                if (parent != null)
                    parent.AddChild(node);
                else
                    rootNode = node;
            lock (UpdatedLock)
                m_rootUpdated = true;
            lfi.Sort((a, b) => (Math.Sign(((long)b.Tag) - (long)a.Tag)));
            d[dir.ToLower()] = lfi;
            foreach (string subdir in Directory.GetDirectories(dir))
            {
                try
                {
                    CreateNodeTree(subdir, node);
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
