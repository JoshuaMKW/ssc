using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace arookas
{
    class CodeGraph
    {
        public List<CodeVertex> Graph;

        public CodeGraph()
        {
            Graph = new List<CodeVertex>();
        }

        public void AddVertex(CodeVertex NewVertex)//Adds a vertex
        {
            Graph.Add(NewVertex);
        }


        private int IndexFromAddr(long Address)
        {
            for (int i = 0; i < Graph.Count; i++)
            {
                if (Graph[i].Addr < Address)
                {
                    continue;
                }
                return i;
            }
            return -1;//-1 indicates end of the code
        }

        public string OutputCode(int IndentL)
        {
            for (int i = 0; i < Graph.Count; i++)
            {
                if(Graph[i].Type == VertexType.Branch)
                {
                    int BranchIndex = IndexFromAddr(Graph[i].BranchTo);
                    if(BranchIndex == -1)
                    {
                        Graph[i].Code = "break " + Graph[i].BranchTo.ToString() + ";";//Branch to label
                        long EndAddr = Graph[Graph.Count - 1].Addr;
                        CodeVertex Label = new CodeVertex(VertexType.Label, -1, Graph[i].BranchTo.ToString() + ":", EndAddr+1);
                        Graph.Add(Label);
                    }
                    else
                    {
                        VertexType DestType = Graph[BranchIndex].Type;

                        if (DestType == VertexType.CodeBlock || DestType == VertexType.Branch)
                        {
                            Graph[i].Code = "break " + Graph[i].BranchTo.ToString() + ";";//Branch to label
                            CodeVertex NewLine = new CodeVertex(VertexType.Label, -1, Graph[i].BranchTo.ToString() + ":",Graph[i].BranchTo);
                            Graph.Insert(BranchIndex, NewLine);
                            if(BranchIndex <= i)
                            {
                                i++;
                            }
                        }
                        else if(DestType == VertexType.Label)
                        {
                            string LabelName = Graph[BranchIndex].Code.Replace(':',' ');
                            Graph[i].Code = "break " + LabelName + ";";//Branch to label
                        }
                        else if(DestType == VertexType.ConditionalBranch)
                        {
                            //While Loop Detected
                            Graph[BranchIndex].Code = "while(" + Graph[BranchIndex].Code + ")" + Environment.NewLine + "{";//Put while loop
                            int ClosingIndex = IndexFromAddr(Graph[BranchIndex].BranchTo);
                            if (ClosingIndex == -1)
                            {
                                CodeVertex NewLine = new CodeVertex(VertexType.CodeBlock, -1, "}", Graph[BranchIndex].BranchTo);
                                Graph.Add(NewLine);
                            }
                            else
                            {
                                CodeVertex NewLine = new CodeVertex(VertexType.CodeBlock, -1, "}", Graph[BranchIndex].BranchTo);
                                Graph.Insert(ClosingIndex, NewLine);
                                if (ClosingIndex <= i)
                                {
                                    i++;
                                }
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < Graph.Count; i++)
            {
                if (Graph[i].Type == VertexType.ConditionalBranch && !Graph[i].Code.StartsWith("while"))
                {
                    if (Graph[i].Addr < Graph[i].BranchTo)
                    {
                        //If Statement Detected
                        Graph[i].Code = "if(" + Graph[i].Code + ")" + Environment.NewLine + "{";//Put if
                        int ClosingIndex = IndexFromAddr(Graph[i].BranchTo);
                        if (ClosingIndex == -1)
                        {
                            CodeVertex NewLine = new CodeVertex(VertexType.CodeBlock, -1, "}", Graph[i].BranchTo);
                            Graph.Add(NewLine);
                        }
                        else
                        {
                            CodeVertex NewLine = new CodeVertex(VertexType.CodeBlock, -1, "}", Graph[i].BranchTo);
                            Graph.Insert(ClosingIndex, NewLine);
                            if (ClosingIndex <= i)
                            {
                                i++;
                            }
                        }
                    }
                    else
                    {
                        //Do loop detected
                        Graph[i].Code = "} while(" + Graph[i].Code + ")";//Put do loop
                        int ClosingIndex = IndexFromAddr(Graph[i].BranchTo);
                        CodeVertex NewLine = new CodeVertex(VertexType.CodeBlock, -1, "do" + Environment.NewLine + "{", Graph[i].BranchTo);
                        Graph.Insert(ClosingIndex, NewLine);
                        if (ClosingIndex <= i)
                        {
                            i++;
                        }
                    }
                }
            }

            string DecompiledCode = "";
            for (int i = 0; i < Graph.Count; i++)
            {
                DecompiledCode = DecompiledCode + Environment.NewLine + Graph[i].Code;
            }

            string[] lines = DecompiledCode.Split(new[] { Environment.NewLine },StringSplitOptions.None);
            DecompiledCode = "";

            int IndentLevel = IndentL;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == "")
                    continue;
                string IndentedString = new String(' ', IndentLevel * 4) + lines[i];
                if (lines[i].Contains("{"))
                {
                    IndentLevel++;
                }
                else if (lines[i].Contains("}"))
                {
                    IndentLevel = System.Math.Max(0, IndentLevel - 1);
                    IndentedString = new String(' ', IndentLevel * 4) + lines[i];
                }
                if(DecompiledCode == "")
                {
                    DecompiledCode = IndentedString;
                }
                else
                {
                    DecompiledCode = DecompiledCode + Environment.NewLine + IndentedString;
                }
                
            }

            return DecompiledCode;
        }

    }
}
