﻿using System;
using System.Collections.Generic;
using System.IO;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Welcome : ObservableObject
    {
        public static Welcome Instance { get; } = new();

        public AvaloniaList<RepositoryNode> Rows
        {
            get;
            private set;
        } = [];

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    Refresh();
            }
        }

        public Welcome()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (string.IsNullOrWhiteSpace(_searchFilter))
            {
                foreach (var node in Preferences.Instance.RepositoryNodes)
                    ResetVisibility(node);
            }
            else
            {
                foreach (var node in Preferences.Instance.RepositoryNodes)
                    SetVisibilityBySearch(node);
            }

            var rows = new List<RepositoryNode>();
            MakeTreeRows(rows, Preferences.Instance.RepositoryNodes);
            Rows.Clear();
            Rows.AddRange(rows);
        }

        public void ToggleNodeIsExpanded(RepositoryNode node)
        {
            node.IsExpanded = !node.IsExpanded;

            var depth = node.Depth;
            var idx = Rows.IndexOf(node);
            if (idx == -1)
                return;

            if (node.IsExpanded)
            {
                var subrows = new List<RepositoryNode>();
                MakeTreeRows(subrows, node.SubNodes, depth + 1);
                Rows.InsertRange(idx + 1, subrows);
            }
            else
            {
                var removeCount = 0;
                for (int i = idx + 1; i < Rows.Count; i++)
                {
                    var row = Rows[i];
                    if (row.Depth <= depth)
                        break;

                    removeCount++;
                }
                Rows.RemoveRange(idx + 1, removeCount);
            }
        }

        public void OpenOrInitRepository(string path, RepositoryNode parent, bool bMoveExistedNode)
        {
            if (!Directory.Exists(path))
            {
                if (File.Exists(path))
                    path = Path.GetDirectoryName(path);
                else
                    return;
            }

            var isBare = new Commands.IsBareRepository(path).GetResultAsync().Result;
            var repoRoot = path;
            if (!isBare)
            {
                var test = new Commands.QueryRepositoryRootPath(path).GetResultAsync().Result;
                if (!test.IsSuccess || string.IsNullOrEmpty(test.StdOut))
                {
                    InitRepository(path, parent, test.StdErr);
                    return;
                }

                repoRoot = test.StdOut.Trim();
            }

            var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(repoRoot, parent, bMoveExistedNode);
            Refresh();

            var launcher = App.GetLauncher();
            launcher?.OpenRepositoryInTab(node, launcher.ActivePage);
        }

        public void InitRepository(string path, RepositoryNode parent, string reason)
        {
            if (!Preferences.Instance.IsGitConfigured())
            {
                App.RaiseException(string.Empty, App.Text("NotConfigured"));
                return;
            }

            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new Init(activePage.Node.Id, path, parent, reason);
        }

        public void Clone()
        {
            if (!Preferences.Instance.IsGitConfigured())
            {
                App.RaiseException(string.Empty, App.Text("NotConfigured"));
                return;
            }

            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new Clone(activePage.Node.Id);
        }

        public void OpenTerminal()
        {
            if (!Preferences.Instance.IsGitConfigured())
                App.RaiseException(string.Empty, App.Text("NotConfigured"));
            else
                Native.OS.OpenTerminal(null);
        }

        public void ScanDefaultCloneDir()
        {
            if (!Preferences.Instance.IsGitConfigured())
            {
                App.RaiseException(string.Empty, App.Text("NotConfigured"));
                return;
            }

            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new ScanRepositories();
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        public void AddRootNode()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new CreateGroup(null);
        }

        public RepositoryNode FindParentGroup(RepositoryNode node, RepositoryNode group = null)
        {
            var collection = (group == null) ? Preferences.Instance.RepositoryNodes : group.SubNodes;
            if (collection.Contains(node))
                return group;

            foreach (var item in collection)
            {
                if (!item.IsRepository)
                {
                    var parent = FindParentGroup(node, item);
                    if (parent != null)
                        return parent;
                }
            }

            return null;
        }

        public void MoveNode(RepositoryNode from, RepositoryNode to)
        {
            Preferences.Instance.MoveNode(from, to, true);
            Refresh();
        }

        private void ResetVisibility(RepositoryNode node)
        {
            node.IsVisible = true;
            foreach (var subNode in node.SubNodes)
                ResetVisibility(subNode);
        }

        private void SetVisibilityBySearch(RepositoryNode node)
        {
            if (!node.IsRepository)
            {
                if (node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                {
                    node.IsVisible = true;
                    foreach (var subNode in node.SubNodes)
                        ResetVisibility(subNode);
                }
                else
                {
                    bool hasVisibleSubNode = false;
                    foreach (var subNode in node.SubNodes)
                    {
                        SetVisibilityBySearch(subNode);
                        hasVisibleSubNode |= subNode.IsVisible;
                    }
                    node.IsVisible = hasVisibleSubNode;
                }
            }
            else
            {
                node.IsVisible = node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    node.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void MakeTreeRows(List<RepositoryNode> rows, List<RepositoryNode> nodes, int depth = 0)
        {
            foreach (var node in nodes)
            {
                if (!node.IsVisible)
                    continue;

                node.Depth = depth;
                rows.Add(node);

                if (node.IsRepository || !node.IsExpanded)
                    continue;

                MakeTreeRows(rows, node.SubNodes, depth + 1);
            }
        }

        private string _searchFilter = string.Empty;
    }
}
