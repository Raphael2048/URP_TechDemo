using System;
using System.Collections.Generic;

namespace GPUBaking.Editor
{
    /// <summary>
    /// use binary tree to pack rectangles.
    /// input rectangles should be sorted by max side first.
    /// </summary>
    public class BinaryRectWrapper
    {
        /// <summary>
        /// basic unit
        /// </summary>
        public class Node
        {
            int m_offsetX;
            int m_offsetY;
            int m_maxLeftoverWidth;
            int m_maxLeftoverHeight;
            bool m_bLeaf;           
            List<Node> m_children = null;
            public int Width { get; set; }
            public int Height { get; set; }
            public Node Parent { get; set; }
            public Node() { }
            public Node(int w, int h, int x, int y, Node parent, bool bLeaf = true)
            {
                m_offsetX = x;
                m_offsetY = y;                
                m_children = new List<Node>();
                m_bLeaf = bLeaf;
                Width = w;
                Height = h;
                Parent = parent;

                // record the max leftover width in this node and children
                m_maxLeftoverWidth = w;
                // record the max leftover height in this node and children
                m_maxLeftoverHeight = h;
            }

            public void AddChild(Node child)
            {
                m_children.Add(child);
            }
            public bool AddRectangle(int requiredWidth, int requiredHeight, out int offsetX, out int offsetY)
            {
                offsetX = m_offsetX;
                offsetY = m_offsetY;
                // check the max leftover width and height and save time to travse all the children
                if (requiredWidth > m_maxLeftoverWidth || requiredHeight > m_maxLeftoverHeight)
                {
                    return false;
                }
                if (requiredWidth <= Width && requiredHeight <= Height)
                {

                    if (m_bLeaf)
                    {
                        // satisfy requirement
                        // calculate two children nodes
                        if (requiredWidth == Width && requiredHeight == Height)
                        {
                            // fill up the whole node
                            // we don't need to create child node
                            m_maxLeftoverWidth = m_maxLeftoverHeight = 0;
                        }
                        else if (requiredWidth == Width)
                        {
                            Node child = new Node(Width, Height - requiredHeight, m_offsetX, m_offsetY + requiredHeight, this);
                            m_children.Add(child);
                            m_maxLeftoverWidth = child.Width;
                            m_maxLeftoverHeight = child.Height;
                        }
                        else if (requiredHeight == Height)
                        {
                            Node child = new Node(Width - requiredWidth, Height, m_offsetX + requiredWidth, m_offsetY, this);
                            m_children.Add(child);
                            m_maxLeftoverWidth = child.Width;
                            m_maxLeftoverHeight = child.Height;
                        }
                        else
                        {
                            Node firstChild = new Node(Width - requiredWidth, requiredHeight, m_offsetX + requiredWidth, m_offsetY, this);
                            Node secondChild = new Node(Width, Height - requiredHeight, m_offsetX, m_offsetY + requiredHeight, this);
                            m_children.Add(firstChild);
                            m_children.Add(secondChild);
                            m_maxLeftoverWidth = Math.Max(firstChild.Width, secondChild.Width);
                            m_maxLeftoverHeight = Math.Max(firstChild.Height, secondChild.Height);
                        }
                        m_bLeaf = false;
                        return true;
                    }
                    else
                    {
                        // go deeper
                        if (m_children.Count < 1)
                        {
                            // the whole node has been filled up
                            return false;
                        }
                        foreach (Node child in m_children)
                        {
                            if (child.AddRectangle(requiredWidth, requiredHeight, out offsetX, out offsetY))
                            {
                                UpdateMaxLeftoverInfo();
                                return true;
                            }
                        }
                    }
                }
                return false;
            }

            /// <summary>
            /// update max leftover width and height
            /// </summary>
            void UpdateMaxLeftoverInfo()
            {
                int tmpMaxLeftoverWidth = 0;
                int tmpMaxLeftoverHeight = 0;
                foreach (Node child in m_children)
                {
                    if (tmpMaxLeftoverWidth < child.m_maxLeftoverWidth)
                    {
                        tmpMaxLeftoverWidth = child.m_maxLeftoverWidth;
                    }
                    if (tmpMaxLeftoverHeight < child.m_maxLeftoverHeight)
                    {
                        tmpMaxLeftoverHeight = child.m_maxLeftoverHeight;
                    }
                }
                this.m_maxLeftoverWidth = tmpMaxLeftoverWidth;
                this.m_maxLeftoverHeight = tmpMaxLeftoverHeight;
            }
        }

        int m_maxValidWidth;
        int m_maxValidHeight;
        Node m_root;
        public int Width { get; set; }
        public int Height { get; set; }
        public BinaryRectWrapper() { }

        public BinaryRectWrapper(int w, int h)
        {
            m_maxValidWidth = 0;
            m_maxValidHeight = 0;
            Width = w;
            Height = h;

            m_root = null;
        }

        public bool AddRectangle(int requiredWidth, int requiredHeight, out int offsetX, out int offsetY)
        {
            offsetX = 0;
            offsetY = 0;
            if (requiredWidth <= Width && requiredHeight <= Height)
            {
                // init m_root
                if (m_root == null)
                {
                    int maxSide = Math.Max(requiredWidth, requiredHeight);
                    // init the square node according to the max side of the first rectangle
                    m_root = new Node(requiredWidth, requiredHeight, offsetX, offsetY, null);
                }
                if (m_root.AddRectangle(requiredWidth, requiredHeight, out offsetX, out offsetY))
                {
                    // free space enough
                    UpdateMaxValidRect(requiredWidth, requiredHeight, offsetX, offsetY);
                    return true;
                }
                else
                {
                    // free space not enough
                    // need to grow the size of the node
                    if (GrowNode(requiredWidth, requiredHeight))
                    {
                        if (m_root.AddRectangle(requiredWidth, requiredHeight, out offsetX, out offsetY))
                        {
                            // free space enough
                            UpdateMaxValidRect(requiredWidth, requiredHeight, offsetX, offsetY);
                            return true;
                        }
                    }

                    return false;
                }

            }

            return false;
        }

        bool GrowNode(int width, int height)
        {
            bool bCanGrowUp = width <= m_root.Width;
            bool bCanGrowRight = height <= m_root.Height;

            bool bShouldGrowRight = bCanGrowRight && (m_root.Height >= (m_root.Width + width));
            bool bShouldGrowUp = bCanGrowUp && (m_root.Width >= (m_root.Height + height));

            if (bShouldGrowRight)
            {
                return GrowNodeRight(width, height);
            }
            else if (bShouldGrowUp)
            {
                return GrowNodeUp(width, height);
            }
            else if (bCanGrowRight)
            {
                return GrowNodeRight(width, height);
            }
            else if (bCanGrowUp)
            {
                return GrowNodeUp(width, height);
            }
            else
            {
                // error
                DawnDebug.LogError("Grow Wrapper Node Error!!!");
            }

            return false;
        }

        bool GrowNodeUp(int width, int height)
        {
            if (m_root.Height + height > this.Height)
            {
                // exceed the wrapper's size
                return false;
            }
            Node newRoot = new Node(m_root.Width, m_root.Height + height, 0, 0, null, false);
            Node growNode = new Node(m_root.Width, height, 0, m_root.Height, newRoot);
            m_root.Parent = newRoot;
            newRoot.AddChild(growNode);
            newRoot.AddChild(m_root);
            m_root = newRoot;
            return true;
        }

        bool GrowNodeRight(int width, int height)
        {
            if (m_root.Width + width > this.Width)
            {
                // exceed the wrapper's size
                return false;
            }
            Node newRoot = new Node(m_root.Width + width, m_root.Height, 0, 0, null, false);
            Node growNode = new Node(width, m_root.Height, m_root.Width, 0, newRoot);
            m_root.Parent = newRoot;
            newRoot.AddChild(growNode);
            newRoot.AddChild(m_root);
            m_root = newRoot;
            return true;
        }
        void UpdateMaxValidRect(int requiredWidth, int requiredHeight, int offsetX, int offsetY)
        {
            if (offsetX + requiredWidth > m_maxValidWidth)
            {
                m_maxValidWidth = offsetX + requiredWidth;
            }
            if (offsetY + requiredHeight > m_maxValidHeight)
            {
                m_maxValidHeight = offsetY + requiredHeight;
            }
        }
        /// <summary>
        /// Get the min valid rectangle size
        /// </summary>
        /// <returns>lenght 2 int array, index 0 is width, index 1 is height</returns>
        public void GetMinValidRect(out int width, out int height)
        {
            width = m_maxValidWidth;
            height = m_maxValidHeight;
        }
    }
}