using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace ClangOnlyChangedLines
{
    class Program
    {
        static string getGitDiffOutput(string pathToFiles)
        {
            Process gitProcess = new Process();
            gitProcess.StartInfo.WorkingDirectory = pathToFiles;
            gitProcess.StartInfo.FileName = "git";
            gitProcess.StartInfo.Arguments = "diff -U0";
            gitProcess.StartInfo.UseShellExecute = false;
            gitProcess.StartInfo.RedirectStandardOutput = true;
            gitProcess.StartInfo.RedirectStandardError = true;
            gitProcess.Start();

            string output = (gitProcess.StandardOutput.ReadToEnd());
            string error_output = (gitProcess.StandardError.ReadToEnd());
            gitProcess.WaitForExit();
            return output;
        }
        static void Main(string[] args)
        {
            if(args.Length < 2)
            {
                Console.Write(@"Program usage: arg0 - path to the directory where you changes belong and must be formated, arg1 - path to clang executable
                            !!!!Warning!!!!
                            Run this program BEFORE you made git commit!
                            Otherwise it will not work!
                        ");
                return;
            }

            string pathToProject = args[0];
            string pathToClang = args[1];
            //git diff -U0
            string fileChangePattern = @"@@[\s]+(-|\+)([0-9]*)[\s]*(,([0-9]+))?[\s]+(-|\+)([0-9]*)[\s]*(,([0-9]+))?";
            string fileRegexPattern = @"diff --git a(\/[A-z0-9_\/\-.]+)";
            Regex changeRegex = new Regex(fileChangePattern, RegexOptions.IgnoreCase);
            Regex fileRegex = new Regex(fileRegexPattern, RegexOptions.IgnoreCase);

            string git_output = getGitDiffOutput(pathToProject);
            MatchCollection fileMatches = fileRegex.Matches(git_output);
            Dictionary<string, List<Tuple<int, int>>> changesInFiles = new Dictionary<string, List<Tuple<int, int>>>();
            foreach (Match fileMatch in fileMatches)
            {
                string fileName = fileMatch.Groups[1].Value;
                changesInFiles.Add(fileName, new List<Tuple<int, int>>());
                Func<int> getSubstrLength = () =>
                {
                    if (fileMatch.NextMatch() != null && fileMatch.NextMatch().Index!=0)
                    {
                        return fileMatch.NextMatch().Index;
                    }
                    else
                    {
                        return Math.Abs((git_output.Length - 1) - fileMatch.Index);
                    }
                };
                string file_diff_data = git_output.Substring(fileMatch.Index, getSubstrLength());
                MatchCollection changesMatches = changeRegex.Matches(file_diff_data);

                foreach (Match match in changesMatches)
                {
                    var groups = match.Groups;
                    const int requiredNumberOfParams = 9;
                    if (groups.Count != requiredNumberOfParams)
                    {
                        throw new Exception("Number of params in group mismatch");
                    }
                    try
                    {
                        int startLine = int.Parse(groups[2].Value);
                        int endLine = int.Parse(groups[6].Value);

                        Func<int, int> getValue = (int index) => {
                            if ((groups[index].Value != string.Empty))
                            {
                                return int.Parse(groups[index].Value);
                            }
                            return -1;
                        };

                        int countOfLinesChanged = Math.Max(Math.Max(getValue(4), getValue(8)), Math.Abs(endLine - startLine));

                        changesInFiles[fileName].Add(new Tuple<int, int>(startLine, countOfLinesChanged));
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e.Message + e.StackTrace);
                    }
                }
            } //end of foreach matchfiles

            foreach(var changeInFile in changesInFiles)
            {
                string fileName = changeInFile.Key;
                var changesList = changeInFile.Value;
                string fullPathToFile = pathToProject + fileName.Replace(@"/", @"\");
                clangFormatFile(pathToClang, createClangArgs(changesList, fullPathToFile), fullPathToFile, Encoding.ASCII);
            }

        }

        static string createClangArgs(List<Tuple<int, int>> changesList, string fullPathToFile)
        {
            //       -lines =< string > - < start line >:< end line > -format a range of
            //                         lines(both 1 - based).
            //                         Multiple ranges can be formatted by specifying
            //                         several - lines arguments.

            //- style = file(, clang - format) file
            StringBuilder clangArgs = new StringBuilder();
            foreach(var change in changesList)
            {
                //String s = String.Format("The current price is {0} per ounce.",
                //pricePerOunce);
                clangArgs.Append(string.Format(@" -lines {0}:{1} ", change.Item1.ToString(), (change.Item1+change.Item2).ToString()));

            }
            clangArgs.Append(" -style=file ");
            clangArgs.Append(" -i " + fullPathToFile);
            return clangArgs.ToString();
        }
        static void clangFormatFile(string clangPath, string clangArgs, string fullPathToFile, Encoding encoding)
        {
            var clangProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = clangPath,
                    Arguments = clangArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
           // then start the process and read from it:

            clangProcess.Start();
            string output = (clangProcess.StandardOutput.ReadToEnd());
            string error_output = (clangProcess.StandardError.ReadToEnd());
            clangProcess.WaitForExit();
            //write clang output back to file

            //using (System.IO.StreamWriter file =
            //                        new System.IO.StreamWriter(fullPathToFile, false, encoding))
            //{
            //    file.Write(output);
            //}
        }
    }
}
