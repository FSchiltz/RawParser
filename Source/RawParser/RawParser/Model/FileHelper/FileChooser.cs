﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RawParser.Model.FileHelper
{
    class FileChooser
    {
        public static void DisplayDrive(ComboBox combo)
        {
            var drivesList = System.IO.DriveInfo.GetDrives();
            foreach(DriveInfo drive in drivesList)
            {
                combo.Items.Add(drive.Name);
            }
        }

        public static void addRootPath(TreeView treeView, string path)
        {
            treeView.Nodes.Clear();
            var rootDirectory = new DirectoryInfo(path);
            var node = new TreeNode(rootDirectory.Name) { Tag = rootDirectory };
            
            var directoryInfo = (DirectoryInfo)node.Tag;
            foreach (var directory in directoryInfo.GetDirectories())
            {

                var childDirectoryNode = new TreeNode(directory.Name) { Tag = directory };
                node.Nodes.Add(childDirectoryNode);

            }

            string fileTypeRegex = "*.nef";
            foreach (var file in directoryInfo.GetFiles(fileTypeRegex))
            {
                node.Nodes.Add(new TreeNode(file.Name));

            }
            treeView.Nodes.Add(node);
        }

        public static void addSubfolder(TreeNode treeNode)
        {
            foreach (TreeNode node in treeNode.Nodes)
            {
                try
                {
                    node.Nodes.Clear();
                    var directoryInfo = (DirectoryInfo)node.Tag;
                   
                    foreach (var directory in directoryInfo.GetDirectories())
                    {
                        var childDirectoryNode = new TreeNode(directory.Name) { Tag = directory };
                        node.Nodes.Add(childDirectoryNode);
                    }

                    string fileTypeRegex = "*.nef";
                    foreach (var file in directoryInfo.GetFiles(fileTypeRegex))
                    {
                        //genreate exception, To fix
                        node.Nodes.Add(new TreeNode(file.Name));
                    }
                }
                catch (UnauthorizedAccessException ex)
                {

                }
             }
       }

        public static void ListDirectory(TreeView treeView, string path)
        {
            treeView.Nodes.Clear();

            var stack = new Stack<TreeNode>();
            var rootDirectory = new DirectoryInfo(path);
            var node = new TreeNode(rootDirectory.Name) { Tag = rootDirectory };
            stack.Push(node);
            while (stack.Count > 0)
            {
                try
                {
                    var currentNode = stack.Pop();
                    var directoryInfo = (DirectoryInfo)currentNode.Tag;
                    foreach (var directory in directoryInfo.GetDirectories())
                    {
                       
                            var childDirectoryNode = new TreeNode(directory.Name) { Tag = directory };
                            currentNode.Nodes.Add(childDirectoryNode);
                            stack.Push(childDirectoryNode);
                       
                    }
                    foreach (var file in directoryInfo.GetFiles())
                    {
                        
                          currentNode.Nodes.Add(new TreeNode(file.Name));
                        
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
         
                }
                
            }

            treeView.Nodes.Add(node);
        }
    }
}
