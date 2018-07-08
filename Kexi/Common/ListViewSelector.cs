﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kexi.Common
{
    public sealed class ListViewSelector
    {
        // This stores the ListViewSelector for each ListBox so we can unregister it.
        /// <summary>Identifies the IsEnabled attached property.</summary>
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(ListViewSelector), new UIPropertyMetadata(false, IsEnabledChangedCallback));

        private static readonly Dictionary<ListView, ListViewSelector> AttachedControls = new Dictionary<ListView, ListViewSelector>();


        private static void IsEnabledChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var listView = d as ListView;
            if (listView != null)
            {
                if ((bool) e.NewValue)
                {
                    // If we're enabling selection by a rectangle we can assume
                    // this means we want to be able to select more than one item.
                    if (listView.SelectionMode == SelectionMode.Single) listView.SelectionMode = SelectionMode.Extended;

                    AttachedControls.Add(listView, new ListViewSelector(listView));
                }
                else // Unregister the selector
                {
                    ListViewSelector selector;
                    if (AttachedControls.TryGetValue(listView, out selector))
                    {
                        AttachedControls.Remove(listView);
                        selector.UnRegister();
                    }
                }
            }
        }

        /// <summary>
        ///     Gets the value of the IsEnabled attached property that indicates
        ///     whether a selection rectangle can be used to select items or not.
        /// </summary>
        /// <param name="obj">Object on which to get the property.</param>
        /// <returns>
        ///     true if items can be selected by a selection rectangle; otherwise, false.
        /// </returns>
        public static bool GetEnabled(DependencyObject obj)
        {
            return (bool) obj.GetValue(EnabledProperty);
        }

        /// <summary>
        ///     Sets the value of the IsEnabled attached property that indicates
        ///     whether a selection rectangle can be used to select items or not.
        /// </summary>
        /// <param name="obj">Object on which to set the property.</param>
        /// <param name="value">Value to set.</param>
        public static void SetEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(EnabledProperty, value);
        }

        // Finds the nearest child of the specified type, or null if one wasn't found.
        private static T FindChild<T>(DependencyObject reference) where T : class
        {
            // Do a breadth first search.
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(reference);
            while (queue.Count > 0)
            {
                var child = queue.Dequeue();
                var obj   = child as T;
                if (obj != null) return obj;

                // Add the children to the queue to search through later.
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(child); i++) queue.Enqueue(VisualTreeHelper.GetChild(child, i));
            }

            return null; // Not found.
        }

        public ListViewSelector(ListView listView)
        {
            _listView = listView;
            if (_listView.IsLoaded)
                Register();
            else
                _listView.Loaded += OnListViewLoaded;
        }

        private readonly ListView     _listView;
        private          AutoScroller _autoScroller;
        private          Point        _end;

        private bool                   _mouseCaptured;
        private ScrollContentPresenter _scrollContent;

        private SelectionAdorner     _selectionRect;
        private ItemsControlSelector _selector;
        private Point                _start;

        public bool Register()
        {
            _scrollContent = FindChild<ScrollContentPresenter>(_listView);
            if (_scrollContent != null)
            {
                _autoScroller               =  new AutoScroller(_listView);
                _autoScroller.OffsetChanged += OnOffsetChanged;

                _selectionRect = new SelectionAdorner(_scrollContent);
                _scrollContent.AdornerLayer.Add(_selectionRect);

                _selector = new ItemsControlSelector(_listView);

                // The ListBox intercepts the regular MouseLeftButtonDown event
                // to do its selection processing, so we need to handle the
                // PreviewMouseLeftButtonDown. The scroll content won't receive
                // the message if we click on a blank area so use the ListBox.
                _listView.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                _listView.MouseLeftButtonUp          += OnMouseLeftButtonUp;
                _listView.MouseMove                  += OnMouseMove;
            }

            // Return success if we found the ScrollContentPresenter
            return _scrollContent != null;
        }

        public void UnRegister()
        {
            StopSelection();

            // Remove all the event handlers so this instance can be reclaimed by the GC.
            _listView.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            _listView.MouseLeftButtonUp          -= OnMouseLeftButtonUp;
            _listView.MouseMove                  -= OnMouseMove;

            _autoScroller.UnRegister();
        }

        private void OnListViewLoaded(object sender, EventArgs e)
        {
            if (Register()) _listView.Loaded -= OnListViewLoaded;
        }

        private void OnOffsetChanged(object sender, OffsetChangedEventArgs e)
        {
            _selector.Scroll(e.HorizontalChange, e.VerticalChange);
            UpdateSelection();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_mouseCaptured)
            {
                _mouseCaptured = false;
                _scrollContent.ReleaseMouseCapture();
                StopSelection();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_mouseCaptured)
            {
                // Get the position relative to the content of the ScrollViewer.
                _end = e.GetPosition(_scrollContent);
                _autoScroller.Update(_end);
                UpdateSelection();
            }
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check that the mouse is inside the scroll content (could be on the
            // scroll bars for example).
            var mouse = e.GetPosition(_scrollContent);
            if (mouse.X >= 0 && mouse.X < _scrollContent.ActualWidth &&
                mouse.Y >= 0 && mouse.Y < _scrollContent.ActualHeight)
            {
                _mouseCaptured = TryCaptureMouse(e);
                if (_mouseCaptured) StartSelection(mouse);
            }
        }

        private bool TryCaptureMouse(MouseButtonEventArgs e)
        {
            var position = e.GetPosition(_scrollContent);

            // Check if there is anything under the mouse.
            var element = _scrollContent.InputHitTest(position) as UIElement;
            if (element != null)
            {
                // Simulate a mouse click by sending it the MouseButtonDown
                // event based on the data we received.
                var args = new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left, e.StylusDevice);
                args.RoutedEvent = Mouse.MouseDownEvent;
                args.Source      = e.Source;
                element.RaiseEvent(args);

                // The ListBox will try to capture the mouse unless something
                // else captures it.
                if (!Equals(Mouse.Captured, _listView)) return false; // Something else wanted the mouse, let it keep it.
            }

            // Either there's nothing under the mouse or the element doesn't want the mouse.
            return _scrollContent.CaptureMouse();
        }

        private void StopSelection()
        {
            // Hide the selection rectangle and stop the auto scrolling.
            _selectionRect.IsEnabled = false;
            _autoScroller.IsEnabled  = false;
        }

        private void StartSelection(Point location)
        {
            // We've stolen the MouseLeftButtonDown event from the ListBox
            // so we need to manually give it focus.
            _listView.Focus();

            _start = location;
            _end   = location;

            // Do we need to start a new selection?
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 &&
                (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                _listView.SelectedItems.Clear();

            _selector.Reset();
            UpdateSelection();

            _selectionRect.IsEnabled = true;
            _autoScroller.IsEnabled  = true;
        }

        private void UpdateSelection()
        {
            // Offset the start point based on the scroll offset.
            var start = _autoScroller.TranslatePoint(_start);

            // Draw the selecion rectangle.
            // Rect can't have a negative width/height...
            var x      = Math.Min(start.X, _end.X);
            var y      = Math.Min(start.Y, _end.Y);
            var width  = Math.Abs(_end.X - start.X);
            var height = Math.Abs(_end.Y - start.Y);
            var area   = new Rect(x, y, width, height);
            _selectionRect.SelectionArea = area;

            // Select the items.
            // Transform the points to be relative to the ListBox.
            var topLeft     = _scrollContent.TranslatePoint(area.TopLeft, _listView);
            var bottomRight = _scrollContent.TranslatePoint(area.BottomRight, _listView);

            // And select the items.
            _selector.UpdateSelection(new Rect(topLeft, bottomRight));
        }

        /// <summary>
        ///     Automatically scrolls an ItemsControl when the mouse is dragged outside
        ///     of the control.
        /// </summary>
        private sealed class AutoScroller
        {
            /// <summary>
            ///     Initializes a new instance of the AutoScroller class.
            /// </summary>
            /// <param name="itemsControl">The ItemsControl that is scrolled.</param>
            /// <exception cref="ArgumentNullException">itemsControl is null.</exception>
            public AutoScroller(ItemsControl itemsControl)
            {
                _itemsControl          =  itemsControl ?? throw new ArgumentNullException(nameof(itemsControl));
                _scrollViewer               =  FindChild<ScrollViewer>(itemsControl);
                _scrollViewer.ScrollChanged += OnScrollChanged;
                _scrollContent              =  FindChild<ScrollContentPresenter>(_scrollViewer);

                _autoScroll.Tick     += delegate { PreformScroll(); };
                _autoScroll.Interval =  TimeSpan.FromMilliseconds(GetRepeatRate());
            }

            /// <summary>
            ///     Gets or sets a value indicating whether the auto-scroller is enabled
            ///     or not.
            /// </summary>
            public bool IsEnabled
            {
                get => _isEnabled;
                set
                {
                    if (_isEnabled != value)
                    {
                        _isEnabled = value;

                        // Reset the auto-scroller and offset.
                        _autoScroll.IsEnabled = false;
                        _offset               = new Point();
                    }
                }
            }

            private readonly DispatcherTimer        _autoScroll = new DispatcherTimer();
            private readonly ItemsControl           _itemsControl;
            private readonly ScrollContentPresenter _scrollContent;
            private readonly ScrollViewer           _scrollViewer;
            private          bool                   _isEnabled;
            private          Point                  _mouse;
            private          Point                  _offset;

            /// <summary>Occurs when the scroll offset has changed.</summary>
            public event EventHandler<OffsetChangedEventArgs> OffsetChanged;

            /// <summary>
            ///     Translates the specified point by the current scroll offset.
            /// </summary>
            /// <param name="point">The point to translate.</param>
            /// <returns>A new point offset by the current scroll amount.</returns>
            public Point TranslatePoint(Point point)
            {
                return new Point(point.X - _offset.X, point.Y - _offset.Y);
            }

            /// <summary>
            ///     Removes all the event handlers registered on the control.
            /// </summary>
            public void UnRegister()
            {
                _scrollViewer.ScrollChanged -= OnScrollChanged;
            }

            /// <summary>
            ///     Updates the location of the mouse and automatically scrolls if required.
            /// </summary>
            /// <param name="mouse">
            ///     The location of the mouse, relative to the ScrollViewer's content.
            /// </param>
            public void Update(Point mouse)
            {
                _mouse = mouse;

                // If scrolling isn't enabled then see if it needs to be.
                if (!_autoScroll.IsEnabled) PreformScroll();
            }

            // Returns the default repeat rate in milliseconds.
            private static int GetRepeatRate()
            {
                // The RepeatButton uses the SystemParameters.KeyboardSpeed as the
                // default value for the Interval property. KeyboardSpeed returns
                // a value between 0 (400ms) and 31 (33ms).
                const double ratio = (400.0 - 33.0) / 31.0;
                return 400 - (int) (SystemParameters.KeyboardSpeed * ratio);
            }

            private double CalculateOffset(int startIndex, int endIndex)
            {
                double sum = 0;
                for (var i = startIndex; i != endIndex; i++)
                {
                    var container = _itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (container != null)
                    {
                        // Height = Actual height + margin
                        sum += container.ActualHeight;
                        sum += container.Margin.Top + container.Margin.Bottom;
                    }
                }

                return sum;
            }

            private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
            {
                // Do we need to update the offset?
                if (IsEnabled)
                {
                    var horizontal = e.HorizontalChange;
                    var vertical   = e.VerticalChange;

                    // VerticalOffset means two seperate things based on the CanContentScroll
                    // property. If this property is true then the offset is the number of
                    // items to scroll; false then it's in Device Independant Pixels (DIPs).
                    if (_scrollViewer.CanContentScroll)
                    {
                        // We need to either increase the offset or decrease it.
                        if (e.VerticalChange < 0)
                        {
                            var start = (int) e.VerticalOffset;
                            var end   = (int) (e.VerticalOffset - e.VerticalChange);
                            vertical = -CalculateOffset(start, end);
                        }
                        else
                        {
                            var start = (int) (e.VerticalOffset - e.VerticalChange);
                            var end   = (int) e.VerticalOffset;
                            vertical = CalculateOffset(start, end);
                        }
                    }

                    _offset.X += horizontal;
                    _offset.Y += vertical;

                    var callback = OffsetChanged;
                    if (callback != null) callback(this, new OffsetChangedEventArgs(horizontal, vertical));
                }
            }

            private void PreformScroll()
            {
                var scrolled = false;

                if (_mouse.X > _scrollContent.ActualWidth)
                {
                    _scrollViewer.LineRight();
                    scrolled = true;
                }
                else if (_mouse.X < 0)
                {
                    _scrollViewer.LineLeft();
                    scrolled = true;
                }

                if (_mouse.Y > _scrollContent.ActualHeight)
                {
                    _scrollViewer.LineDown();
                    scrolled = true;
                }
                else if (_mouse.Y < 0)
                {
                    _scrollViewer.LineUp();
                    scrolled = true;
                }

                // It's important to disable scrolling if we're inside the bounds of
                // the control so that when the user does leave the bounds we can
                // re-enable scrolling and it will have the correct initial delay.
                _autoScroll.IsEnabled = scrolled;
            }
        }

        /// <summary>Enables the selection of items by a specified rectangle.</summary>
        private sealed class ItemsControlSelector
        {
            /// <summary>
            ///     Initializes a new instance of the ItemsControlSelector class.
            /// </summary>
            /// <param name="itemsControl">
            ///     The control that contains the items to select.
            /// </param>
            /// <exception cref="ArgumentNullException">itemsControl is null.</exception>
            public ItemsControlSelector(ItemsControl itemsControl)
            {
                _itemsControl = itemsControl ?? throw new ArgumentNullException(nameof(itemsControl));
            }

            private readonly ItemsControl _itemsControl;
            private          Rect         _previousArea;

            /// <summary>
            ///     Resets the cached information, allowing a new selection to begin.
            /// </summary>
            public void Reset()
            {
                _previousArea = new Rect();
            }

            /// <summary>
            ///     Scrolls the selection area by the specified amount.
            /// </summary>
            /// <param name="x">The horizontal scroll amount.</param>
            /// <param name="y">The vertical scroll amount.</param>
            public void Scroll(double x, double y)
            {
                _previousArea.Offset(-x, -y);
            }

            /// <summary>
            ///     Updates the controls selection based on the specified area.
            /// </summary>
            /// <param name="area">
            ///     The selection area, relative to the control passed in the contructor.
            /// </param>
            public void UpdateSelection(Rect area)
            {
                // Check eack item to see if it intersects with the area.
                for (var i = 0; i < _itemsControl.Items.Count; i++)
                {
                    var item = _itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (item != null)
                    {
                        // Get the bounds in the parent's co-ordinates.
                        var topLeft    = item.TranslatePoint(new Point(0, 0), _itemsControl);
                        var itemBounds = new Rect(topLeft.X, topLeft.Y, item.ActualWidth, item.ActualHeight);

                        // Only change the selection if it intersects with the area
                        // (or intersected i.e. we changed the value last time).
                        if (itemBounds.IntersectsWith(area))
                            Selector.SetIsSelected(item, true);
                        else if (itemBounds.IntersectsWith(_previousArea)) Selector.SetIsSelected(item, false);
                    }
                }

                _previousArea = area;
            }
        }

        /// <summary>The event data for the AutoScroller.OffsetChanged event.</summary>
        private sealed class OffsetChangedEventArgs : EventArgs
        {
            /// <summary>
            ///     Initializes a new instance of the OffsetChangedEventArgs class.
            /// </summary>
            /// <param name="horizontal">The change in horizontal scroll.</param>
            /// <param name="vertical">The change in vertical scroll.</param>
            internal OffsetChangedEventArgs(double horizontal, double vertical)
            {
                HorizontalChange = horizontal;
                VerticalChange   = vertical;
            }

            /// <summary>Gets the change in horizontal scroll position.</summary>
            public double HorizontalChange { get; }

            /// <summary>Gets the change in vertical scroll position.</summary>
            public double VerticalChange { get; }
        }

        /// <summary>Draws a selection rectangle on an AdornerLayer.</summary>
        private sealed class SelectionAdorner : Adorner
        {
            /// <summary>
            ///     Initializes a new instance of the SelectionAdorner class.
            /// </summary>
            /// <param name="parent">
            ///     The UIElement which this instance will overlay.
            /// </param>
            /// <exception cref="ArgumentNullException">parent is null.</exception>
            public SelectionAdorner(UIElement parent)
                : base(parent)
            {
                // Make sure the mouse doesn't see us.
                IsHitTestVisible = false;

                // We only draw a rectangle when we're enabled.
                IsEnabledChanged += delegate { InvalidateVisual(); };
            }

            /// <summary>Gets or sets the area of the selection rectangle.</summary>
            public Rect SelectionArea
            {
                get => _selectionRect;
                set
                {
                    _selectionRect = value;
                    InvalidateVisual();
                }
            }

            private Rect _selectionRect;

            /// <summary>
            ///     Participates in rendering operations that are directed by the layout system.
            /// </summary>
            /// <param name="drawingContext">The drawing instructions.</param>
            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);

                if (IsEnabled)
                {
                    // Make the lines snap to pixels (add half the pen width [0.5])
                    double[] x = {SelectionArea.Left + 0.5, SelectionArea.Right + 0.5};
                    double[] y = {SelectionArea.Top + 0.5, SelectionArea.Bottom + 0.5};
                    drawingContext.PushGuidelineSet(new GuidelineSet(x, y));

                    Brush fill = SystemColors.HighlightBrush.Clone();
                    fill.Opacity = 0.4;
                    drawingContext.DrawRectangle(fill, new Pen(SystemColors.HighlightBrush, 1.0), SelectionArea);
                }
            }
        }
    }
}