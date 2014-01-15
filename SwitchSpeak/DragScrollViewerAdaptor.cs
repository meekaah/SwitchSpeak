using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SwitchSpeak
{
    public class DragScrollViewerAdaptor
    {
        // Fields
        private Point _currentPoint;
        private DispatcherTimer _dragScrollTimer = null;
        private double _friction = 0.2;
        private bool _isDragging = false;
        private bool _mouseDown = false;
        private Point _previousPoint;
        private Point _previousPreviousPoint;
        private ScrollViewer _wrappedScrollViewer;
        private const double DEFAULT_FRICTION = 0.2;
        private const double DRAG_POLLING_INTERVAL = 10.0;
        private const double MAXIMUM_FRICTION = 1.0;
        private const double MINIMUM_FRICTION = 0.0;

        // Methods
        public DragScrollViewerAdaptor(ScrollViewer scrollViewerToWrap)
        {
            this._wrappedScrollViewer = scrollViewerToWrap;
        }

        private void BeginDrag()
        {
            this._mouseDown = true;
            this._wrappedScrollViewer.Cursor = Cursors.SizeNS;
        }

        private void CancelDrag()
        {
            this._isDragging = false;
            this.Momentum = this.Velocity;
        }

        private void CancelDrag(Vector velocityToUse)
        {
            if (this._isDragging)
            {
                this.Momentum = velocityToUse;
            }
            this._isDragging = false;
            this._mouseDown = false;
            this._wrappedScrollViewer.Cursor = Cursors.Arrow;
        }

        protected void DragScroll()
        {
            if (this._dragScrollTimer == null)
            {
                this._dragScrollTimer = new DispatcherTimer();
                this._dragScrollTimer.Tick += this.TickDragScroll;
                this._dragScrollTimer.Interval = new TimeSpan(0, 0, 0, 0, 10);
                this._dragScrollTimer.Start();
            }
        }

        private void PerformScroll(Vector displacement)
        {
            double num = Math.Max((double)0.0, (double)(this._wrappedScrollViewer.VerticalOffset - displacement.Y));
            this._wrappedScrollViewer.ScrollToVerticalOffset(num);
            double num2 = Math.Max((double)0.0, (double)(this._wrappedScrollViewer.HorizontalOffset - displacement.X));
            this._wrappedScrollViewer.ScrollToHorizontalOffset(num2);
        }

        private void TickDragScroll(object sender, EventArgs e)
        {
            if (this._isDragging)
            {
                Point point = this._wrappedScrollViewer.TransformToVisual(this._wrappedScrollViewer).Transform(new Point(0.0, 0.0));
                Rect rect = new Rect(point, this._wrappedScrollViewer.RenderSize);
                if (rect.Contains(this._currentPoint))
                {
                    this.PerformScroll(this.PreviousVelocity);
                }
                if (!this._mouseDown)
                {
                    this.CancelDrag(this.Velocity);
                }
                this._previousPreviousPoint = this._previousPoint;
                this._previousPoint = this._currentPoint;
            }
            else if (this.Momentum.Length > 0.0)
            {
                this.Momentum = (Vector)(this.Momentum * (1.0 - (this._friction / 4.0)));
                this.PerformScroll(this.Momentum);
            }
            else if (this._dragScrollTimer != null)
            {
                this._dragScrollTimer.Tick -= this.TickDragScroll;
                this._dragScrollTimer.Stop();
                this._dragScrollTimer = null;
            }
        }

        private void WrappedScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            this.CancelDrag(this.PreviousVelocity);
        }

        public void MouseLeftButtonDown(Point point)
        {
            this._currentPoint = this._previousPoint = this._previousPreviousPoint = point;
            this.Momentum = new Vector(0.0, 0.0);
            this.BeginDrag();
        }

        public void MouseLeftButtonUp()
        {
            this.CancelDrag(this.PreviousVelocity);
        }

        public void MouseMove(Point point)
        {
            this._currentPoint = point;
            if (!(!this._mouseDown || this._isDragging))
            {
                this._isDragging = true;
                this.DragScroll();
            }
        }

        // Properties
        public double Friction
        {
            get
            {
                return this._friction;
            }
            set
            {
                this._friction = Math.Min(Math.Max(value, 0.0), 1.0);
            }
        }

        private Vector Momentum { get; set; }

        private Vector PreviousVelocity
        {
            get
            {
                return new Vector(this._previousPoint.X - this._previousPreviousPoint.X, this._previousPoint.Y - this._previousPreviousPoint.Y);
            }
        }

        private Vector Velocity
        {
            get
            {
                return new Vector(this._currentPoint.X - this._previousPoint.X, this._currentPoint.Y - this._previousPoint.Y);
            }
        }

        // Nested Types
        private class Vector
        {
            // Methods
            public Vector(double x, double y)
            {
                this.X = x;
                this.Y = y;
            }

            public static DragScrollViewerAdaptor.Vector operator *(DragScrollViewerAdaptor.Vector vector, double scalar)
            {
                return new DragScrollViewerAdaptor.Vector(vector.X * scalar, vector.Y * scalar);
            }

            // Properties
            public double Length
            {
                get
                {
                    return Math.Sqrt((this.X * this.X) + (this.Y * this.Y));
                }
            }

            public double X { get; set; }

            public double Y { get; set; }
        }
    }


}
