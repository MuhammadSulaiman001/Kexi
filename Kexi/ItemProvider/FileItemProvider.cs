﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Kexi.Common;
using Kexi.Interfaces;
using Kexi.ViewModel;
using Kexi.ViewModel.Item;
using Microsoft.WindowsAPICodePack.Shell;
using Shell32;

namespace Kexi.ItemProvider
{
    [Export(typeof(FileItemProvider))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class FileItemProvider : IDisposable
    {
        [ImportingConstructor]
        public FileItemProvider(Workspace workspace)
        {
            CancellationTokenSource = new CancellationTokenSource();
            Workspace               = workspace;
        }

        public Workspace Workspace { get; }

        public CancellationTokenSource CancellationTokenSource { get; private set; }

        public void Dispose()
        {
            CancellationTokenSource?.Dispose();
        }

        public void CancelCurrentTasks()
        {
            CancellationTokenSource.Cancel();
            CancellationTokenSource = new CancellationTokenSource();
        }

        public async Task<IEnumerable<FileItem>> GetItems(string path)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(path))
                    return GetRootItems().Items;

                IEnumerable<FileItem> items;
                try
                {
                    items = GetItemsInternal(path);
                }
                catch (UnauthorizedAccessException)
                {
                    if (path.StartsWith(@"\\"))
                    {
                        PinvokeWindowsNetworking.ConnectToRemote(path, "", "", true);
                        items = GetItemsInternal(path);
                    }
                    else
                    {
                        throw;
                    }
                }

                if (items.Any())
                    return Workspace.Options.ShowHiddenItems ? items : items.Where(i => !i.IsSystemOrHidden);

                return GetEmptyDirectoryItems(path);
            });
        }

        private static IEnumerable<FileItem> GetEmptyDirectoryItems(string path)
        {
            var parent = Directory.GetParent(path);
            var item   = new FileItem(parent?.FullName, ItemType.Container, "..");
            var items  = new[] {item};
            return items;
        }

        private IEnumerable<FileItem> GetItemsInternal(string path, bool showHidden = true)
        {
            if (path.StartsWith(@"\\") && !Regex.IsMatch(path, @"^\\\\.+\\.+$")) return GetShares(path);

            return Directory.EnumerateDirectories(path).Select(p => new FileItem(p, ItemType.Container, itemProvider: this))
                .Concat(
                    Directory.EnumerateFiles(path).Select(p => new FileItem(p, ItemType.Item, itemProvider: this))
                );
        }

        private IEnumerable<FileItem> GetShares(string path)
        {
            var folder = ShellObject.FromParsingName(path) as ShellFolder;
            if (folder == null)
                yield break;

            var thumb = Utils.GetImageFromRessource("share.png");

            ImmutableArray<FileItem> items;
            try
            {
                items = folder.Select(i => new FileItem(i.Properties.System.ParsingPath.Value, ItemType.Container, itemProvider: this)
                {
                    Thumbnail   = thumb,
                    IsFileShare = true
                }).ToImmutableArray();
            }
            catch (UnauthorizedAccessException)
            {
                PinvokeWindowsNetworking.ConnectToRemote(path, "", "", true);
                items = folder.Select(i => new FileItem(i.Properties.System.ParsingPath.Value, ItemType.Container, itemProvider: this)
                {
                    IsFileShare = true,
                    Thumbnail   = thumb
                }).ToImmutableArray();
            }

            foreach (var fi in items)
            {
                fi.Details = new FileDetailItem(fi, CancellationToken.None)
                {
                    Type = "Share"
                };
                yield return fi;
            }
        }

        public static string GetParentContainer(string path)
        {
            if (path == null)
                return null;
            if (path.StartsWith("::{"))
            {
                var index = path.LastIndexOf(@"\", StringComparison.Ordinal);
                return index < 0 ? null : path.Substring(0, index);
            }

            var parent = Directory.GetParent(path);
            return parent?.FullName;
        }

        private static ItemProviderResult<FileItem> GetRootItems()
        {
            IEnumerable<FileItem> rootItems = null;
            string                path      = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var compi = new Shell32.Shell().NameSpace(ShellSpecialFolderConstants.ssfDRIVES);
                rootItems = compi.Items().Cast<FolderItem2>()
                    .Where(f => Directory.Exists(f.Path))
                    .Select(i => new FileItem(i.Path, i.IsFolder ? ItemType.Container : ItemType.Item, i.Name));
                path = compi.Title;

                return new ItemProviderResult<FileItem>
                {
                    Items    = rootItems,
                    PathName = path
                };
            });

            return new ItemProviderResult<FileItem>
            {
                Items    = rootItems,
                PathName = path
            };
        }
    }
}