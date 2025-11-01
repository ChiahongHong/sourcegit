﻿using System;
using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    public class BranchCompareCommandPalette : ICommandPalette
    {
        public List<Models.Branch> Branches
        {
            get => _branches;
            private set => SetProperty(ref _branches, value);
        }

        public Models.Branch SelectedBranch
        {
            get => _selectedBranch;
            set => SetProperty(ref _selectedBranch, value);
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    UpdateBranches();
            }
        }

        public BranchCompareCommandPalette(Launcher launcher, Repository repo)
        {
            _launcher = launcher;
            _repo = repo;
            UpdateBranches();
        }

        public override void Cleanup()
        {
            _launcher = null;
            _repo = null;
            _branches.Clear();
            _selectedBranch = null;
            _filter = null;
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public void Launch()
        {
            if (_selectedBranch != null)
                App.ShowWindow(new BranchCompare(_repo.FullPath, _selectedBranch, _repo.CurrentBranch));
            _launcher?.CancelCommandPalette();
        }

        private void UpdateBranches()
        {
            var current = _repo.CurrentBranch;
            if (current == null)
                return;

            var branches = new List<Models.Branch>();
            foreach (var b in _repo.Branches)
            {
                if (b == current)
                    continue;

                if (string.IsNullOrEmpty(_filter) || b.FriendlyName.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    branches.Add(b);
            }

            branches.Sort((l, r) =>
            {
                if (l.IsLocal == r.IsLocal)
                    return l.Name.CompareTo(r.Name);

                return l.IsLocal ? -1 : 1;
            });

            Branches = branches;
        }

        private Launcher _launcher;
        private Repository _repo;
        private List<Models.Branch> _branches = [];
        private Models.Branch _selectedBranch = null;
        private string _filter;
    }
}
