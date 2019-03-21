﻿using ScriptImporter.Models;
using ScriptImporter.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ScriptImporter.DataLogic
{
    public class CodeMerger
    {
        // It will print something like this:
        // 
        // // --------------------------------------------
        // // File: E:\Github\SFDScript\BotExtended\Bot.cs
        // // --------------------------------------------
        private string GetFileHeader(string filePath)
        {
            var relativePath = filePath.Substring(VSProject.ProjectDirectory.Length + 1);
            var filePathLength = relativePath.Length;
            var bar = new string('-', filePathLength + 6);
            return $@"// {bar}
// File: {relativePath}
// {bar}{Environment.NewLine}";
        }

        private string GetHeader(ScriptInfo info)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// This file is auto generated. Do not modify");
            sb.AppendLine();
            sb.AppendLine("/*");
            sb.AppendLine($"* author: {info.Author}");
            sb.AppendLine($"* description: {info.Description}");
            sb.AppendLine($"* mapmodes: {info.MapModes}");
            sb.AppendLine("*/");

            return sb.ToString();
        }

        public string Merge(string projectDir, string scriptPath, string rootNS, ScriptInfo info)
        {
            var mergedFiles = new HashSet<string>();
            var usingNamespaces = new HashSet<string>();
            var newNamespaces = new List<string>();
            var sb = new StringBuilder();

            sb.Append(GetHeader(info));

            var gameSriptInfo = ReadGameScriptFile(scriptPath);

            sb.AppendLine(gameSriptInfo.Content);
            mergedFiles.Add(scriptPath);
            newNamespaces = gameSriptInfo.UsingNamespaces
                .Where(ns => ns.StartsWith(rootNS))
                .Select(ns => ns.Trim()).ToList();

            while (newNamespaces.Any())
            {
                var addedNamespaces = new List<string>();
                foreach (var file in FileUtil.GetScriptFiles(projectDir))
                {
                    if (mergedFiles.Contains(file)) continue;

                    var ns = FileUtil.GetNamespace(file);
                    if (newNamespaces.Contains(ns))
                    {
                        var fileInfo = ReadCSharpFile(file, gameSriptInfo.Namespace);

                        sb.Append(fileInfo.Content);
                        addedNamespaces.AddRange(fileInfo.UsingNamespaces);
                        mergedFiles.Add(file);
                    }
                }

                newNamespaces = addedNamespaces;

                foreach (var newNS in newNamespaces.ToList())
                {
                    if (!usingNamespaces.Contains(newNS) && newNS.StartsWith(rootNS))
                        usingNamespaces.Add(newNS.Trim());
                    else
                        newNamespaces.Remove(newNS);
                }
            }

            sb.AppendLine();
            return sb.ToString();
        }

        public CSharpFileInfo ReadGameScriptFile(string filePath)
        {
            var sourceCode = new StringBuilder();
            var usingNamespaces = new List<string>();
            var fileNamespace = "";

            sourceCode.AppendLine(GetFileHeader(filePath));

            using (var file = File.OpenText(filePath))
            {
                var line = "";
                var codeBlock = 0;

                while ((line = file.ReadLine()) != null)
                {
                    if (codeBlock <= 1)
                    {
                        var match = RegexPattern.UsingStatement.Match(line);
                        if (match.Success)
                            usingNamespaces.Add(match.Groups[1].Value);

                        match = RegexPattern.Namespace.Match(line);
                        if (match.Success)
                        {
                            fileNamespace = match.Groups[1].Value;
                            usingNamespaces.Add(fileNamespace);
                        }
                    }

                    var commentIndex = line.IndexOf("//"); // -1 if not found

                    if (commentIndex == -1)
                        commentIndex = line.Length;

                    var startBlock = false; // if current line has character '{' (not in comment)
                    var endBlock = false; // if current line has character '}' (not in comment)

                    for (var i = 0; i < line.Length; i++)
                    {
                        if (i > commentIndex)
                            continue;

                        if (line[i] == '{') startBlock = true;
                        if (line[i] == '}') endBlock = true;
                    }

                    if (startBlock)
                        codeBlock++;

                    if (RegexPattern.GameScriptCtor.Match(line).Success)
                    {
                        goto Finish;
                    }

                    if (codeBlock == 2 && !startBlock && !endBlock)
                    {
                        sourceCode.AppendLine(line);
                    }
                    if (codeBlock > 2)
                    {
                        sourceCode.AppendLine(line);
                    }

                    Finish:
                    if (endBlock)
                        codeBlock--;
                }
            }

            return new CSharpFileInfo()
            {
                Content = sourceCode.ToString(),
                UsingNamespaces = usingNamespaces,
                Namespace = fileNamespace,
            };
        }

        public CSharpFileInfo ReadCSharpFile(string filePath, string gameScriptNamespace)
        {
            var sourceCode = new StringBuilder();
            var usingNamespaces = new List<string>();
            var fileNamespace = "";

            sourceCode.AppendLine(GetFileHeader(filePath));

            using (var file = File.OpenText(filePath))
            {
                var line = "";
                var codeBlock = 0;

                while ((line = file.ReadLine()) != null)
                {
                    if (codeBlock <= 1)
                    {
                        var match = RegexPattern.UsingStatement.Match(line);
                        if (match.Success)
                            usingNamespaces.Add(match.Groups[1].Value);

                        match = RegexPattern.Namespace.Match(line);
                        if (match.Success)
                        {
                            fileNamespace = match.Groups[1].Value;
                            usingNamespaces.Add(fileNamespace);
                        }
                    }

                    if (RegexPattern.GameScriptClass.Match(line).Success)
                    {
                        if (fileNamespace == gameScriptNamespace) // partial GameScript class
                        {
                            return ReadGameScriptFile(filePath);
                        }
                        else
                            return new CSharpFileInfo(); // Only import normal c# file. Ignore another game script unless it's a partial class
                    }

                    if (string.IsNullOrEmpty(fileNamespace))
                    {
                        var match = RegexPattern.Namespace.Match(line);
                        if (match.Success)
                            fileNamespace = match.Groups[1].Value;
                    }

                    var commentIndex = line.IndexOf("//"); // -1 if not found

                    if (commentIndex == -1)
                        commentIndex = line.Length;

                    var startBlock = false; // if current line has character '{' (not in comment)
                    var endBlock = false; // if current line has character '}' (not in comment)

                    for (var i = 0; i < line.Length; i++)
                    {
                        if (i > commentIndex)
                            continue;

                        if (line[i] == '{') startBlock = true;
                        if (line[i] == '}') endBlock = true;
                    }

                    if (startBlock)
                        codeBlock++;

                    if (codeBlock == 1 && !startBlock && !endBlock)
                    {
                        sourceCode.AppendLine(line);
                    }
                    if (codeBlock > 1)
                    {
                        sourceCode.AppendLine(line);
                    }

                    if (endBlock)
                        codeBlock--;
                }
            }

            return new CSharpFileInfo()
            {
                Content = sourceCode.ToString(),
                UsingNamespaces = usingNamespaces,
                Namespace = fileNamespace,
            };
        }
    }
}
