using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class RepositoryTreeNodeToggleButton : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                DataContext is ViewModels.RepositoryNode { IsRepository: false } node)
            {
                ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);
            }

            e.Handled = true;
        }
    }

    public class RepositoryListBox : ListBox
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (SelectedItem is ViewModels.RepositoryNode node && e.KeyModifiers == KeyModifiers.None)
            {
                if (e.Key is Key.Delete or Key.Back)
                {
                    node.Delete();
                    e.Handled = true;
                }
                else if (node.IsRepository)
                {
                    if (e.Key == Key.Enter)
                    {
                        var parent = this.FindAncestorOfType<Launcher>();
                        if (parent is { DataContext: ViewModels.Launcher launcher })
                            launcher.OpenRepositoryInTab(node, null);

                        e.Handled = true;
                    }
                }
                else if ((node.IsExpanded && e.Key == Key.Left) || (!node.IsExpanded && e.Key == Key.Right) || e.Key == Key.Enter)
                {
                    ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);
                    e.Handled = true;
                }
            }

            if (!e.Handled)
                base.OnKeyDown(e);
        }
    }

    public partial class Welcome : UserControl
    {
        public Welcome()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!e.Handled)
            {
                if (e.Key == Key.Down && ViewModels.Welcome.Instance.Rows.Count > 0)
                {
                    TreeContainer.SelectedIndex = 0;
                    TreeContainer.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    ViewModels.Welcome.Instance.ClearSearchFilter();
                    e.Handled = true;
                }
            }
        }

        private void SetupTreeViewDragAndDrop(object sender, RoutedEventArgs _)
        {
            if (sender is ListBox view)
            {
                DragDrop.SetAllowDrop(view, true);
                view.AddHandler(DragDrop.DragOverEvent, DragOverTreeView);
                view.AddHandler(DragDrop.DropEvent, DropOnTreeView);
            }
        }

        private void SetupTreeNodeDragAndDrop(object sender, RoutedEventArgs _)
        {
            if (sender is Grid grid)
            {
                DragDrop.SetAllowDrop(grid, true);
                grid.AddHandler(DragDrop.DragOverEvent, DragOverTreeNode);
                grid.AddHandler(DragDrop.DropEvent, DropOnTreeNode);
            }
        }

        private void OnTreeNodeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is Grid { DataContext: ViewModels.RepositoryNode node } grid)
            {
                var menu = new ContextMenu();

                if (!node.IsRepository && node.SubNodes.Count > 0)
                {
                    var openAll = new MenuItem();
                    openAll.Header = App.Text("Welcome.OpenAllInNode");
                    openAll.Icon = App.CreateMenuIcon("Icons.Folder.Open");
                    openAll.Click += (_, e) =>
                    {
                        node.Open();
                        e.Handled = true;
                    };

                    menu.Items.Add(openAll);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                if (node.IsRepository)
                {
                    var open = new MenuItem();
                    open.Header = App.Text("Welcome.OpenOrInit");
                    open.Icon = App.CreateMenuIcon("Icons.Folder.Open");
                    open.Click += (_, e) =>
                    {
                        node.Open();
                        e.Handled = true;
                    };

                    var explore = new MenuItem();
                    explore.Header = App.Text("Repository.Explore");
                    explore.Icon = App.CreateMenuIcon("Icons.Explore");
                    explore.Click += (_, e) =>
                    {
                        node.OpenInFileManager();
                        e.Handled = true;
                    };

                    var terminal = new MenuItem();
                    terminal.Header = App.Text("Repository.Terminal");
                    terminal.Icon = App.CreateMenuIcon("Icons.Terminal");
                    terminal.Click += (_, e) =>
                    {
                        node.OpenTerminal();
                        e.Handled = true;
                    };

                    menu.Items.Add(open);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(explore);
                    menu.Items.Add(terminal);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
                else
                {
                    var addSubFolder = new MenuItem();
                    addSubFolder.Header = App.Text("Welcome.AddSubFolder");
                    addSubFolder.Icon = App.CreateMenuIcon("Icons.Folder.Add");
                    addSubFolder.Click += (_, e) =>
                    {
                        node.AddSubFolder();
                        e.Handled = true;
                    };
                    menu.Items.Add(addSubFolder);
                }

                var edit = new MenuItem();
                edit.Header = App.Text("Welcome.Edit");
                edit.Icon = App.CreateMenuIcon("Icons.Edit");
                edit.Click += (_, e) =>
                {
                    node.Edit();
                    e.Handled = true;
                };

                var move = new MenuItem();
                move.Header = App.Text("Welcome.Move");
                move.Icon = App.CreateMenuIcon("Icons.MoveTo");
                move.Click += (_, e) =>
                {
                    node.Move();
                    e.Handled = true;
                };

                var delete = new MenuItem();
                delete.Header = App.Text("Welcome.Delete");
                delete.Icon = App.CreateMenuIcon("Icons.Clear");
                delete.Click += (_, e) =>
                {
                    node.Delete();
                    e.Handled = true;
                };

                menu.Items.Add(edit);
                menu.Items.Add(move);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(delete);
                menu.Open(grid);
            }

            e.Handled = true;
        }

        private void OnPointerPressedTreeNode(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
            {
                _pressedTreeNode = true;
                _startDragTreeNode = false;
                _pressedTreeNodePosition = e.GetPosition(sender as Grid);
            }
            else
            {
                _pressedTreeNode = false;
                _startDragTreeNode = false;
            }
        }

        private void OnPointerReleasedOnTreeNode(object _1, PointerReleasedEventArgs _2)
        {
            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void OnPointerMovedOverTreeNode(object sender, PointerEventArgs e)
        {
            if (_pressedTreeNode && !_startDragTreeNode &&
                sender is Grid { DataContext: ViewModels.RepositoryNode node } grid)
            {
                var delta = e.GetPosition(grid) - _pressedTreeNodePosition;
                var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
                if (sizeSquired < 64)
                    return;

                _startDragTreeNode = true;

                var data = new DataObject();
                data.Set("MovedRepositoryTreeNode", node);
                DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            }
        }

        private void OnTreeViewLostFocus(object _1, RoutedEventArgs _2)
        {
            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void DragOverTreeView(object sender, DragEventArgs e)
        {
            if (e.Data.Contains("MovedRepositoryTreeNode") || e.Data.Contains(DataFormats.Files))
            {
                e.DragEffects = DragDropEffects.Move;
                e.Handled = true;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private async void DropOnTreeView(object sender, DragEventArgs e)
        {
            if (e.Data.Contains("MovedRepositoryTreeNode") && e.Data.Get("MovedRepositoryTreeNode") is ViewModels.RepositoryNode moved)
            {
                e.Handled = true;
                ViewModels.Welcome.Instance.MoveNode(moved, null);
            }
            else if (e.Data.Contains(DataFormats.Files))
            {
                e.Handled = true;

                var items = e.Data.GetFiles();
                if (items != null)
                {
                    var refresh = false;

                    foreach (var item in items)
                    {
                        var path = await ViewModels.Welcome.Instance.GetRepositoryRootAsync(item.Path.LocalPath);
                        if (!string.IsNullOrEmpty(path))
                        {
                            ViewModels.Welcome.Instance.AddRepository(path, null, true, false);
                            refresh = true;
                        }
                    }

                    if (refresh)
                        ViewModels.Welcome.Instance.Refresh();
                }
            }

            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void DragOverTreeNode(object sender, DragEventArgs e)
        {
            if (e.Data.Contains("MovedRepositoryTreeNode") || e.Data.Contains(DataFormats.Files))
            {
                var grid = sender as Grid;

                if (grid?.DataContext is not ViewModels.RepositoryNode)
                    return;

                e.DragEffects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private async void DropOnTreeNode(object sender, DragEventArgs e)
        {
            if (sender is not Grid grid)
                return;

            if (grid.DataContext is not ViewModels.RepositoryNode to)
            {
                e.Handled = true;
                return;
            }

            if (to.IsRepository)
                to = ViewModels.Welcome.Instance.FindParentGroup(to);

            if (e.Data.Contains("MovedRepositoryTreeNode") &&
                e.Data.Get("MovedRepositoryTreeNode") is ViewModels.RepositoryNode moved)
            {
                e.Handled = true;

                if (to != moved)
                    ViewModels.Welcome.Instance.MoveNode(moved, to);
            }
            else if (e.Data.Contains(DataFormats.Files))
            {
                e.Handled = true;

                var items = e.Data.GetFiles();
                if (items != null)
                {
                    var refresh = false;

                    foreach (var item in items)
                    {
                        var path = await ViewModels.Welcome.Instance.GetRepositoryRootAsync(item.Path.LocalPath);
                        if (!string.IsNullOrEmpty(path))
                        {
                            ViewModels.Welcome.Instance.AddRepository(path, to, true, false);
                            refresh = true;
                        }
                    }

                    if (refresh)
                        ViewModels.Welcome.Instance.Refresh();
                }
            }

            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void OnDoubleTappedTreeNode(object sender, TappedEventArgs e)
        {
            if (sender is Grid { DataContext: ViewModels.RepositoryNode node })
            {
                if (node.IsRepository)
                {
                    var parent = this.FindAncestorOfType<Launcher>();
                    if (parent is { DataContext: ViewModels.Launcher launcher })
                        launcher.OpenRepositoryInTab(node, null);
                }
                else
                {
                    ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);
                }

                e.Handled = true;
            }
        }

        private bool _pressedTreeNode = false;
        private Point _pressedTreeNodePosition = new Point();
        private bool _startDragTreeNode = false;
    }
}
