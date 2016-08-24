using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace VDU
{
    public partial class ConcentricRingTree : UserControl
    {
        public ConcentricRingTree()
        {
            InitializeComponent();
            this.SetStyle(
              ControlStyles.AllPaintingInWmPaint |
              ControlStyles.UserPaint |
              ControlStyles.DoubleBuffer, true);
        }

        public class Node
        {
            public string Label { get; set; }
            public double Value { get; set; }
            public List<Node> Children { get; set; }

            public void AddChild(Node n)
            {
                lock (LockObj)
                    Children.Add(n);
            }

            public object LockObj = new object();

            public double TotalValue { get { return Sum; } }

            internal double Sum;
            internal int Level;
            internal double StartDegrees;
            internal double DegreeCoverage;
            internal Node Parent = null;
        }

        public Node Root;

        protected int MaxLevel = 0;
        public void UpdateStructure()
        {
            if (Root == null)
                return;

            Root.Parent = null;
            Root.Sum = 0;
            SumTree(Root);
            Root.Level = 0;
            Root.StartDegrees = 0;
            Root.DegreeCoverage = 360;

            MaxLevel = 0;
            Root.Level = 0;
            AssignLevelAndDegrees(Root);

            this.Invalidate();
        }

        private void AssignLevelAndDegrees(Node n)
        {
            double start = n.StartDegrees;
            if (n.Level > MaxLevel)
                MaxLevel = n.Level;
            lock (n.LockObj)
            foreach (var child in n.Children)
            {
                child.Level = n.Level + 1;
                child.StartDegrees = start;
                child.DegreeCoverage = n.DegreeCoverage * child.Sum / n.Sum;
                start += child.DegreeCoverage;
                AssignLevelAndDegrees(child);
            }
        }

        private double SumTree(Node n)
        {
            double total = n.Value;
            if (total < 0)
                throw new Exception("Value should be non-negative");
            lock (n.LockObj)
            foreach (var child in n.Children)
            {
                child.Parent = n;
                total += SumTree(child);
            }
            n.Sum = total;
            return total;
        }

        List<KeyValuePair<Region, Node>> m_regions = new List<KeyValuePair<Region, Node>>();

        private void ConcentricRingTree_Paint(object sender, PaintEventArgs e)
        {
            m_regions.Clear();
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (Root == null)
                return;

            int bound = Math.Min(this.Width, this.Height);
            int xoffset = (this.Width - bound) / 2;
            int yoffset = (this.Height - bound) / 2;
            int pixelsPerLevel = bound / (2 * (MaxLevel + 1));
            var rectanges = new List<Rectangle>();
            for (int i = 0; i <= MaxLevel; ++i)
            {
                rectanges.Add(new Rectangle(
                    xoffset + pixelsPerLevel * (MaxLevel - i),
                    yoffset + pixelsPerLevel * (MaxLevel - i),
                    pixelsPerLevel * 2 * (i + 1),
                    pixelsPerLevel * 2 * (i + 1)));
            }
            for (int i = MaxLevel; i >= 0; --i)
            {
                PaintTree(Root, g, rectanges, i);
            }
            m_regions.Reverse();
            ConcentricRingTree_MouseMove(null, null);
        }

        private void PaintTree(Node n, Graphics g, List<Rectangle> lr, int level)
        {
            lock (n.LockObj)
            foreach (var child in n.Children)
            {
                PaintTree(child, g, lr, level);
            }
            if (level == n.Level && n.DegreeCoverage > 0.1)
            {
                Random r = new Random(n.Label.GetHashCode());
                GraphicsPath p = new GraphicsPath();
                p.AddPie(lr[n.Level], (float)n.StartDegrees, (float)n.DegreeCoverage);
                m_regions.Add(new KeyValuePair<Region, Node>(new Region(p), n));
                using (var brush = new SolidBrush(Color.FromArgb(r.Next(0, 256), r.Next(0, 256), r.Next(0, 256))))
                    g.FillPie(brush, lr[n.Level], (float)n.StartDegrees, (float)n.DegreeCoverage);
                g.DrawArc(Pens.Black, lr[n.Level], (float)n.StartDegrees, (float)n.DegreeCoverage);
            }
        }

        private void ConcentricRingTree_Resize(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        public Node SelectedNode = null;
        public delegate void NodeChangeEvent();
        public event NodeChangeEvent SelectedNodeChanged;

        private void ConcentricRingTree_MouseMove(object sender, MouseEventArgs e)
        {
            var p = this.PointToClient(MousePosition);
            foreach (var r in m_regions)
            {
                if (r.Key.IsVisible(p))
                {
                    SelectedNode = r.Value;
                    if (SelectedNodeChanged != null)
                    {
                        SelectedNodeChanged();
                    }
                    break;
                }
            }
        }
    }
}
