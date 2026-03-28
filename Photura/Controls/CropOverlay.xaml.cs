using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace Photura.Controls
{
    public partial class CropOverlay : UserControl
    {
        private enum Handle
        {
            None, Move,
            TL, TC, TR,
            ML, MR,
            BL, BC, BR
        }

        public event Action<Rect>? CropChanged;

        private Rect _cropRect;
        private double _zoom = 1.0;
        private double _imageW, _imageH;
        private double _aspectRatio = 0;
        private double _offsetX = 0;
        private double _offsetY = 0;

        private Handle _dragHandle = Handle.None;
        private Point _dragStartMouse;
        private Rect _dragStartRect;

        private readonly Rectangle _handleMove;

        public CropOverlay()
        {
            InitializeComponent();
            _handleMove = new Rectangle
            {
                Fill = System.Windows.Media.Brushes.Transparent,
                Cursor = Cursors.SizeAll
            };
            RootCanvas.Children.Add(_handleMove);
            SizeChanged += (s, e) => UpdateOverlay();
        }

        // ── Public API ───────────────────────────────────────────
        public void Initialize(double imageW, double imageH, double zoom,
                               double offsetX = 0, double offsetY = 0)
        {
            _imageW = imageW;
            _imageH = imageH;
            _zoom = zoom;
            _offsetX = offsetX;
            _offsetY = offsetY;
            _cropRect = new Rect(0, 0, imageW, imageH);
            UpdateOverlay();
        }

        public void SetZoom(double zoom, double offsetX, double offsetY)
        {
            _zoom = zoom;
            _offsetX = offsetX;
            _offsetY = offsetY;
            UpdateOverlay();
        }

        public void SetAspectRatio(double ratio)
        {
            _aspectRatio = ratio;
            if (ratio > 0)
                _cropRect = EnforceAR(_cropRect, ratio, _imageW, _imageH);
            UpdateOverlay();
            CropChanged?.Invoke(_cropRect);
        }

        public void SetOriginalAspectRatio()
        {
            if (_imageH > 0)
                SetAspectRatio(_imageW / _imageH);
        }

        public Rect GetCropRect() => _cropRect;
        public void SetCropRect(Rect rect) { _cropRect = rect; UpdateOverlay(); }

        // ── Mouse ────────────────────────────────────────────────
        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(RootCanvas);
            var hit = HitTest(pos);
            if (hit == Handle.None) return;

            _dragHandle = hit;
            _dragStartMouse = pos;
            _dragStartRect = _cropRect;
            RootCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragHandle != Handle.None)
            {
                _dragHandle = Handle.None;
                RootCanvas.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(RootCanvas);

            if (e.LeftButton != MouseButtonState.Pressed || _dragHandle == Handle.None)
            {
                UpdateCursor(pos);
                return;
            }

            double dx = (pos.X - _dragStartMouse.X) / _zoom;
            double dy = (pos.Y - _dragStartMouse.Y) / _zoom;

            Rect newRect = _dragHandle == Handle.Move
                ? DoMove(dx, dy)
                : DoResize(_dragHandle, dx, dy);

            _cropRect = newRect;
            UpdateOverlay();
            CropChanged?.Invoke(_cropRect);
            e.Handled = true;
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false; // Let right-click pass through for panning
        }

        // ── Move ─────────────────────────────────────────────────
        private Rect DoMove(double dx, double dy)
        {
            double x = Math.Clamp(_dragStartRect.X + dx, 0, _imageW - _dragStartRect.Width);
            double y = Math.Clamp(_dragStartRect.Y + dy, 0, _imageH - _dragStartRect.Height);
            return new Rect(x, y, _dragStartRect.Width, _dragStartRect.Height);
        }

        // ── Resize — opposite corner anchored, AR always enforced ─
        private Rect DoResize(Handle handle, double dx, double dy)
        {
            const double MinSize = 10;

            double x = _dragStartRect.X;
            double y = _dragStartRect.Y;
            double w = _dragStartRect.Width;
            double h = _dragStartRect.Height;

            // Apply delta to the moving edge
            switch (handle)
            {
                case Handle.TL: x += dx; y += dy; w -= dx; h -= dy; break;
                case Handle.TC: y += dy; h -= dy; break;
                case Handle.TR: y += dy; w += dx; h -= dy; break;
                case Handle.ML: x += dx; w -= dx; break;
                case Handle.MR: w += dx; break;
                case Handle.BL: x += dx; w -= dx; h += dy; break;
                case Handle.BC: h += dy; break;
                case Handle.BR: w += dx; h += dy; break;
            }

            // Clamp the MOVING edge to image bounds, anchor edge stays fixed
            switch (handle)
            {
                case Handle.TL:
                    x = Math.Max(0, x); w = _dragStartRect.Right - x;
                    y = Math.Max(0, y); h = _dragStartRect.Bottom - y;
                    break;
                case Handle.TC:
                    y = Math.Max(0, y); h = _dragStartRect.Bottom - y;
                    break;
                case Handle.TR:
                    w = Math.Min(w, _imageW - _dragStartRect.Left);
                    y = Math.Max(0, y); h = _dragStartRect.Bottom - y;
                    break;
                case Handle.ML:
                    x = Math.Max(0, x); w = _dragStartRect.Right - x;
                    break;
                case Handle.MR:
                    w = Math.Min(w, _imageW - _dragStartRect.Left);
                    break;
                case Handle.BL:
                    x = Math.Max(0, x); w = _dragStartRect.Right - x;
                    h = Math.Min(h, _imageH - _dragStartRect.Top);
                    break;
                case Handle.BC:
                    h = Math.Min(h, _imageH - _dragStartRect.Top);
                    break;
                case Handle.BR:
                    w = Math.Min(w, _imageW - _dragStartRect.Left);
                    h = Math.Min(h, _imageH - _dragStartRect.Top);
                    break;
            }

            w = Math.Max(MinSize, w);
            h = Math.Max(MinSize, h);

            // Enforce aspect ratio
            if (_aspectRatio > 0)
            {
                // For each handle, determine which dimension drives and re-anchor
                switch (handle)
                {
                    case Handle.ML:
                    case Handle.MR:
                        h = w / _aspectRatio;
                        // Keep vertical center at drag-start vertical center
                        double vcenter = _dragStartRect.Y + _dragStartRect.Height / 2;
                        y = vcenter - h / 2;
                        break;

                    case Handle.TC:
                        w = h * _aspectRatio;
                        // Keep horizontal center at current center (not drag-start)
                        double hcenterTC = _dragStartRect.X + _dragStartRect.Width / 2;
                        x = hcenterTC - w / 2;
                        break;

                    case Handle.BC:
                        w = h * _aspectRatio;
                        double hcenterBC = _dragStartRect.X + _dragStartRect.Width / 2;
                        x = hcenterBC - w / 2;
                        break;

                    case Handle.TL:
                        if (w / _aspectRatio <= h) h = w / _aspectRatio;
                        else w = h * _aspectRatio;
                        x = _dragStartRect.Right - w;
                        y = _dragStartRect.Bottom - h;
                        break;

                    case Handle.TR:
                        if (w / _aspectRatio <= h) h = w / _aspectRatio;
                        else w = h * _aspectRatio;
                        x = _dragStartRect.Left;
                        y = _dragStartRect.Bottom - h;
                        break;

                    case Handle.BL:
                        if (w / _aspectRatio <= h) h = w / _aspectRatio;
                        else w = h * _aspectRatio;
                        x = _dragStartRect.Right - w;
                        y = _dragStartRect.Top;
                        break;

                    case Handle.BR:
                        if (w / _aspectRatio <= h) h = w / _aspectRatio;
                        else w = h * _aspectRatio;
                        x = _dragStartRect.Left;
                        y = _dragStartRect.Top;
                        break;
                }

                // If AR result overflows image bounds, scale down uniformly
                // preserving AR, then re-anchor cleanly
                double r = x + w;
                double b = y + h;

                double scale = 1.0;
                if (x < 0) scale = Math.Min(scale, w / (w - x));
                if (y < 0) scale = Math.Min(scale, h / (h - y));
                if (r > _imageW) scale = Math.Min(scale, (_imageW - Math.Max(0, x)) / w);
                if (b > _imageH) scale = Math.Min(scale, (_imageH - Math.Max(0, y)) / h);

                if (scale < 1.0 && scale > 0)
                {
                    w *= scale;
                    h *= scale;

                    // Re-anchor after scale using the same anchor logic
                    switch (handle)
                    {
                        case Handle.TL:
                            x = _dragStartRect.Right - w;
                            y = _dragStartRect.Bottom - h;
                            break;
                        case Handle.TR:
                            x = _dragStartRect.Left;
                            y = _dragStartRect.Bottom - h;
                            break;
                        case Handle.BL:
                            x = _dragStartRect.Right - w;
                            y = _dragStartRect.Top;
                            break;
                        case Handle.BR:
                            x = _dragStartRect.Left;
                            y = _dragStartRect.Top;
                            break;
                        case Handle.TC:
                            // Anchor = bottom, center horizontally
                            y = _dragStartRect.Bottom - h;
                            x = _dragStartRect.X + (_dragStartRect.Width - w) / 2;
                            break;
                        case Handle.BC:
                            // Anchor = top, center horizontally
                            y = _dragStartRect.Top;
                            x = _dragStartRect.X + (_dragStartRect.Width - w) / 2;
                            break;
                        case Handle.ML:
                            // Anchor = right, center vertically
                            x = _dragStartRect.Right - w;
                            y = _dragStartRect.Y + (_dragStartRect.Height - h) / 2;
                            break;
                        case Handle.MR:
                            // Anchor = left, center vertically
                            x = _dragStartRect.Left;
                            y = _dragStartRect.Y + (_dragStartRect.Height - h) / 2;
                            break;
                    }
                }
            }

            // Final hard clamp
            x = Math.Clamp(x, 0, _imageW - MinSize);
            y = Math.Clamp(y, 0, _imageH - MinSize);
            w = Math.Clamp(w, MinSize, _imageW - x);
            h = Math.Clamp(h, MinSize, _imageH - y);

            return new Rect(x, y, w, h);
        }
        private static Rect EnforceAR(Rect r, double ar, double imageW, double imageH)
        {
            double maxW = imageW;
            double maxH = imageH;
            double w, h;

            if (maxW / ar <= maxH) { w = maxW; h = w / ar; }
            else { h = maxH; w = h * ar; }

            double cx = r.X + r.Width / 2;
            double cy = r.Y + r.Height / 2;
            double x = Math.Clamp(cx - w / 2, 0, imageW - w);
            double y = Math.Clamp(cy - h / 2, 0, imageH - h);

            return new Rect(x, y, w, h);
        }

        // ── Hit test ─────────────────────────────────────────────
        private Handle HitTest(Point p)
        {
            Rect d = ToDisplay(_cropRect);

            // Use tighter tolerance for handles (8px) vs move interior
            // This prevents corner handles from accidentally triggering Move
            const double HandleTol = 10;

            bool NearH(double a) => Math.Abs(p.X - a) < HandleTol;
            bool NearV(double a) => Math.Abs(p.Y - a) < HandleTol;

            bool onL = NearH(d.Left);
            bool onR = NearH(d.Right);
            bool onT = NearV(d.Top);
            bool onB = NearV(d.Bottom);
            bool onMX = NearH(d.Left + d.Width / 2);
            bool onMY = NearV(d.Top + d.Height / 2);

            // Check corners first — they take priority over edges
            if (onT && onL) return Handle.TL;
            if (onT && onR) return Handle.TR;
            if (onB && onL) return Handle.BL;
            if (onB && onR) return Handle.BR;

            // Then mid-edge handles
            if (onT && onMX) return Handle.TC;
            if (onB && onMX) return Handle.BC;
            if (onL && onMY) return Handle.ML;
            if (onR && onMY) return Handle.MR;

            // Then edges themselves (anywhere along the edge)
            if (onT && d.Left <= p.X && p.X <= d.Right) return Handle.TC;
            if (onB && d.Left <= p.X && p.X <= d.Right) return Handle.BC;
            if (onL && d.Top <= p.Y && p.Y <= d.Bottom) return Handle.ML;
            if (onR && d.Top <= p.Y && p.Y <= d.Bottom) return Handle.MR;

            // Interior = move, but only if strictly inside (not near any edge)
            bool strictlyInside =
                p.X > d.Left + HandleTol &&
                p.X < d.Right - HandleTol &&
                p.Y > d.Top + HandleTol &&
                p.Y < d.Bottom - HandleTol;

            if (strictlyInside) return Handle.Move;

            return Handle.None;
        }

        private void UpdateCursor(Point p)
        {
            RootCanvas.Cursor = HitTest(p) switch
            {
                Handle.TL or Handle.BR => Cursors.SizeNWSE,
                Handle.TR or Handle.BL => Cursors.SizeNESW,
                Handle.TC or Handle.BC => Cursors.SizeNS,
                Handle.ML or Handle.MR => Cursors.SizeWE,
                Handle.Move => Cursors.SizeAll,
                _ => Cursors.Arrow
            };
        }

        // ── Overlay drawing ──────────────────────────────────────
        private void UpdateOverlay()
        {
            double cw = RootCanvas.ActualWidth;
            double ch = RootCanvas.ActualHeight;
            if (cw <= 0 || ch <= 0) return;

            Rect d = ToDisplay(_cropRect);

            SetRect(OverlayTop, 0, 0, cw, d.Top);
            SetRect(OverlayBottom, 0, d.Bottom, cw, ch - d.Bottom);
            SetRect(OverlayLeft, 0, d.Top, d.Left, d.Height);
            SetRect(OverlayRight, d.Right, d.Top, cw - d.Right, d.Height);

            SetRect(CropBorder, d.Left, d.Top, d.Width, d.Height);

            double t1x = d.Left + d.Width / 3;
            double t2x = d.Left + d.Width * 2 / 3;
            double t1y = d.Top + d.Height / 3;
            double t2y = d.Top + d.Height * 2 / 3;
            SetLine(GridV1, t1x, d.Top, t1x, d.Bottom);
            SetLine(GridV2, t2x, d.Top, t2x, d.Bottom);
            SetLine(GridH1, d.Left, t1y, d.Right, t1y);
            SetLine(GridH2, d.Left, t2y, d.Right, t2y);

            const double hs = 5;
            PlaceHandle(HandleTL, d.Left - hs, d.Top - hs);
            PlaceHandle(HandleTC, d.Left + d.Width / 2 - hs, d.Top - hs);
            PlaceHandle(HandleTR, d.Right - hs, d.Top - hs);
            PlaceHandle(HandleML, d.Left - hs, d.Top + d.Height / 2 - hs);
            PlaceHandle(HandleMR, d.Right - hs, d.Top + d.Height / 2 - hs);
            PlaceHandle(HandleBL, d.Left - hs, d.Bottom - hs);
            PlaceHandle(HandleBC, d.Left + d.Width / 2 - hs, d.Bottom - hs);
            PlaceHandle(HandleBR, d.Right - hs, d.Bottom - hs);

            Canvas.SetLeft(_handleMove, d.Left + 10);
            Canvas.SetTop(_handleMove, d.Top + 10);
            _handleMove.Width = Math.Max(0, d.Width - 20);
            _handleMove.Height = Math.Max(0, d.Height - 20);
        }

        // ── Coordinate helpers ───────────────────────────────────
        private Rect ToDisplay(Rect r) => new(
            _offsetX + r.X * _zoom,
            _offsetY + r.Y * _zoom,
            r.Width * _zoom,
            r.Height * _zoom);

        private static void SetRect(Rectangle r, double x, double y, double w, double h)
        {
            Canvas.SetLeft(r, x); Canvas.SetTop(r, y);
            r.Width = Math.Max(0, w);
            r.Height = Math.Max(0, h);
        }

        private static void SetLine(Line l, double x1, double y1, double x2, double y2)
        { l.X1 = x1; l.Y1 = y1; l.X2 = x2; l.Y2 = y2; }

        private static void PlaceHandle(Rectangle h, double x, double y)
        { Canvas.SetLeft(h, x); Canvas.SetTop(h, y); }
    }
}