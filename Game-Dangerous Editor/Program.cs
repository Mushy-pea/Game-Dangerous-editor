﻿// Game :: Dangerous Editor code by Steven Tinsley.  You are free to use this software and view its source code.
// If you wish to redistribute it or use it as part of your own work, this is permitted as long as you acknowledge the work is by the abovementioned author.

//This is the C# implementation of the GPLC compiler, designed to be equivalent to Game-Dangerous/assm_gplc.hs but with source code error reporting.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Game_Dangerous_Editor
{

    struct GPLCSource
    {
        public int line;
        public int column;
        public string content;
    }

    struct Binding
    {
        public string symbol;
        public int readIndex;
        public int writeIndex;
        public int initValue;
    }

    struct GPLCProgramIn
    {
        public string progName;
        public List<GPLCSource> subBlocks;
        public List<Binding> bs;
    }

    struct GPLCProgramOut
    {

        public GPLCProgramOut(string progNameIn, List<int> bytecodeIn, List<Binding> bsIn)
        {
            progName = progNameIn;
            bytecode = bytecodeIn;
            bs = bsIn;
        }

        private string progName;
        private List<int> bytecode;
        private List<Binding> bs;
    }

    // The methods of this class are used to transform GPLC opcode arguments to their bytecode form in an error safe way, such that source code errors are handled and user feedback added to the error log.
    class SafeArgumentHandler
    {
        public static int RefToOffset(List<Binding> b, GPLCSource subBlock, int mode, List<string> errorLog)
        {
            int n;
            for (n = 0; n <= b.Count; n++)
            {
                if (n == b.Count) { errorLog.Add("\n\nError at line " + Convert.ToString(subBlock.line) + " column " + Convert.ToString(subBlock.column) + ".  " + subBlock.content + " is an undeclared reference argument."); }
                else if (b[n].symbol == subBlock.content)
                {
                    if (mode == 0) { return b[n].readIndex; }
                    else { return b[n].writeIndex; }
                }
                else { }
            }
            return 0;
        }

        public static int ReadLiteral(GPLCSource subBlock, List<string> errorLog, int mode)
        {
            string errorDetail;
            try
            {
                return (Convert.ToInt32(subBlock.content));
            }
            catch (FormatException)
            {
                errorDetail = "\n\nError at line " + Convert.ToString(subBlock.line) + " column " + Convert.ToString(subBlock.column) + ".  ";
                if (mode == 0) { errorDetail = errorDetail + "The second term in a value initialisation must be an integer.  For details of how non - integer arguments are handled see the Op - code arguments section of the GPLC specification."; }
                else { errorDetail = errorDetail + "This opcode argument must be a literal integer."; }
                errorLog.Add(errorDetail);
            }
            return 0;
        }
    }

    // The methods of this class parse the GPLC source code into a list of structs, each of which holds the parsed block of code and its line and column location in the input text file.
    // This is so it is simple to report the locations of source code errors to the user when they are detected further along the transformation pipeline.
    class GPLCParser
    {
        public static List<GPLCSource> Parser(string sourceIn)
        {
            int i = 0, l = 0, c = 0, len;
            GPLCSource nextBlock;
            List<GPLCSource> sourceOut = new List<GPLCSource>();
            for (; ; )
            {
                if (i == sourceIn.Length) { break; }
                nextBlock = BuildSubBlock(sourceIn, i, l, c);
                sourceOut.Add(nextBlock);
                len = nextBlock.content.Length;
                i = i + len;
                if (sourceIn[i] == ' ') { c = c + len + 1; i++; }
                else
                {
                    c = 0;
                    l = l + 1;
                    i = i + 2;
                }
            }
            return sourceOut;
        }

        private static GPLCSource BuildSubBlock(string sourceIn, int i, int l, int c)
        {
            List<char> content = new List<char>();
            GPLCSource subBlock = new GPLCSource();
            for ( ; i < sourceIn.Length; i++)
            {
                if (sourceIn[i] == ' ' || (sourceIn[i] == '\r' && sourceIn[i + 1] == '\n')) { break; }
                else { content.Add(sourceIn[i]); }
            }
            subBlock.content = string.Join("", content);
            subBlock.line = l + 1;
            subBlock.column = c + 1;
            return subBlock;
        }
    }

    // The BindValues member takes as input a list of GPLCSource structs arising from the parsing of a GPLC program's value block.  Its output is a list of Binding structs
    // that each encode the relationship between a GPLC symbolic reference argument and the corresponding data block offset used at bytecode level.
    class ValueBinder
    {
        public List<Binding> BindValues(List<GPLCSource> subBlocks, int offset, List<string> errorLog)
        {
            int i = 0, j;
            List<Binding> b = new List<Binding>();
            Binding thisB = new Binding();
            for (j = 0; j < subBlocks.Count; j = j + 2)
            {
                thisB.symbol = subBlocks[j].content;
                thisB.readIndex = i;
                thisB.writeIndex = offset + i;
                thisB.initValue = SafeArgumentHandler.ReadLiteral(subBlocks[j + 1], errorLog, 0);
                i++;
            }
            return b;
        }
    }

    // The members of this class handle the transformation of GPLC keywords to op - codes and of their reference arguments to the data block offsets used at bytecode level.
    class GenerateBytecode
    {
        private List<GPLCProgramOut> progGroup;

        public GenerateBytecode()
        {
            progGroup = new List<GPLCProgramOut>();
        }

        public GPLCProgramOut GetProgramOut(int n)
        {
            return progGroup[n];
        }

        public void CheckLength()
        {
            Console.WriteLine("prog_group length: " + Convert.ToString(progGroup.Count));
        }

        private List<int> TransformArguments(List<GPLCSource> source, List<Binding> bs, List<int> mode, List<string> errorLog, int i)
        {
            int n;
            List<int> transformedArguments = new List<int>();
            i++;
            for (n = 0; n < mode.Count; n++)
            {
                if (mode[n] == 0) { transformedArguments.Add(SafeArgumentHandler.RefToOffset(bs, source[i], 0, errorLog)); }
                else if (mode[n] == 1) { transformedArguments.Add(SafeArgumentHandler.RefToOffset(bs, source[i], 1, errorLog)); }
                else { transformedArguments.Add(SafeArgumentHandler.ReadLiteral(source[i], errorLog, 1)); }
                i++;
            }
            return transformedArguments;
        }

        public void TransformProgram(GPLCProgramIn progIn, List<string> errorLog)
        {
            int offset = 0, blockSize = 0, i, n;
            List<int> sigBlock = new List<int>();
            List<int> codeBlock = new List<int>();
            List<int> result = new List<int> { 0, 0, 0 };
            string errorDetail;
            bool blockStart = true;
            for (i = 0; i < progIn.subBlocks.Count;)
            {
                if (blockStart == true && progIn.subBlocks[i].content != "--signal")
                {
                    errorDetail = "\n\nError at line " + Convert.ToString(progIn.subBlocks[i].line) + " column " + Convert.ToString(progIn.subBlocks[i].column) + ".  Every block of code must begin with --signal x, where x is the integer signal to be handled by the block.";
                    errorLog.Add(errorDetail);
                    break;
                }
                else if (blockStart == true)
                {
                    try
                    {
                        sigBlock.Add(Convert.ToInt32(progIn.subBlocks[i + 1].content));
                        sigBlock.Add(offset);
                        blockStart = false;
                        i = i + 2;
                    }
                    catch (FormatException)
                    {
                        errorDetail = "\n\nError at line " + Convert.ToString(progIn.subBlocks[i + 1].line) + " column " + Convert.ToString(progIn.subBlocks[i + 1].column) + ".  The second term in a signal handler statement must be an integer.";
                        errorLog.Add(errorDetail);
                        break;
                    }
                }
                else
                {
                    if (progIn.subBlocks[i].content == "if")
                    {
                        List<int> mode = new List<int> { 2, 0, 0, 2, 2 };
                        List<int> thisLine = new List<int> { 1 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 6;
                        offset = offset + 6;
                        blockSize = blockSize + 6;
                    }
                    else if (progIn.subBlocks[i].content == "chg_state")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 2 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 10;
                        offset = offset + 10;
                        blockSize = blockSize + 10;
                    }
                    else if (progIn.subBlocks[i].content == "chg_grid")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 3 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 8;
                        offset = offset + 8;
                        blockSize = blockSize + 8;
                    }
                    else if (progIn.subBlocks[i].content == "send_signal")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 4 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 5;
                        offset = offset + 5;
                        blockSize = blockSize + 5;
                    }
                    else if (progIn.subBlocks[i].content == "chg_value")
                    {
                        List<int> mode = new List<int> { 1, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 5 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 7;
                        offset = offset + 7;
                        blockSize = blockSize + 7;
                    }
                    else if (progIn.subBlocks[i].content == "chg_floor")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 6 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 7;
                        offset = offset + 7;
                        blockSize = blockSize + 7;
                    }
                    else if (progIn.subBlocks[i].content == "chg_ps1")
                    {
                        List<int> mode = new List<int> { 0, 0, 0 };
                        List<int> thisLine = new List<int> { 7 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 4;
                        offset = offset + 4;
                        blockSize = blockSize + 4;
                    }
                    else if (progIn.subBlocks[i].content == "chg_obj_type")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 8 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 5;
                        offset = offset + 5;
                        blockSize = blockSize + 5;
                    }
                    else if (progIn.subBlocks[i].content == "place_hold")
                    {
                        List<int> mode = new List<int> { 0 };
                        List<int> thisLine = new List<int> { 9 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 2;
                        offset = offset + 2;
                        blockSize = blockSize + 2;
                    }
                    else if (progIn.subBlocks[i].content == "chg_grid_")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 10 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 8;
                        offset = offset + 8;
                        blockSize = blockSize + 8;
                    }
                    else if (progIn.subBlocks[i].content == "copy_ps1")
                    {
                        List<int> mode = new List<int> { 2, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 11 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 5;
                        offset = offset + 5;
                        blockSize = blockSize + 5;
                    }
                    else if (progIn.subBlocks[i].content == "copy_lstate")
                    {
                        List<int> mode = new List<int> { 2, 0, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 12 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 8;
                        offset = offset + 8;
                        blockSize = blockSize + 8;
                    }
                    else if (progIn.subBlocks[i].content == "pass_msg")
                    {
                        List<int> mode = new List<int> { 0 };
                        List<int> thisLine = new List<int> { 13 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i + 1));
                        for (n = 3; n <= SafeArgumentHandler.ReadLiteral(progIn.subBlocks[i + 1], errorLog, 1); n++)
                        {
                            thisLine.Add(SafeArgumentHandler.ReadLiteral(progIn.subBlocks[i + n], errorLog, 1));
                        }
                        codeBlock.AddRange(thisLine);
                        i = i + n + 1;
                        offset = offset + n;
                        blockSize = blockSize + n;
                    }
                    else if (progIn.subBlocks[i].content == "chg_ps0")
                    {
                        List<int> mode = new List<int> { 0, 0, 0 };
                        List<int> thisLine = new List<int> { 14 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 4;
                        offset = offset + 4;
                        blockSize = blockSize + 4;
                    }
                    else if (progIn.subBlocks[i].content == "copy_ps0")
                    {
                        List<int> mode = new List<int> { 2, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 15 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 5;
                        offset = offset + 5;
                        blockSize = blockSize + 5;
                    }
                    else if (progIn.subBlocks[i].content == "binary_dice")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 2 };
                        List<int> thisLine = new List<int> { 16 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 7;
                        offset = offset + 7;
                        blockSize = blockSize + 7;
                    }
                    else if (progIn.subBlocks[i].content == "project_init")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0 };
                        List<int> thisLine = new List<int> { 17 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 14;
                        offset = offset + 14;
                        blockSize = blockSize + 14;
                    }
                    else if (progIn.subBlocks[i].content == "project_update")
                    {
                        List<int> mode = new List<int> { 0, 1, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 18 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 6;
                        offset = offset + 6;
                        blockSize = blockSize + 6;
                    }
                    else if (progIn.subBlocks[i].content == "init_npc")
                    {
                        List<int> mode = new List<int> { 0, 0 };
                        List<int> thisLine = new List<int> { 19 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 3;
                        offset = offset + 3;
                        blockSize = blockSize + 3;
                    }
                    else if (progIn.subBlocks[i].content == "npc_decision")
                    {
                        List<int> mode = new List<int> { 1 };
                        List<int> thisLine = new List<int> { 20 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 2;
                        offset = offset + 2;
                        blockSize = blockSize + 2;
                    }
                    else if (progIn.subBlocks[i].content == "npc_move")
                    {
                        List<int> mode = new List<int> { 1 };
                        List<int> thisLine = new List<int> { 21 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 2;
                        offset = offset + 2;
                        blockSize = blockSize + 2;
                    }
                    else if (progIn.subBlocks[i].content == "npc_damage")
                    {
                        List<int> mode = new List<int> { 2 };
                        List<int> thisLine = new List<int> { 22 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 2;
                        offset = offset + 2;
                        blockSize = blockSize + 2;
                    }
                    else if (progIn.subBlocks[i].content == "block")
                    {
                        List<int> mode = new List<int> { 2, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 5, 536870910, 0 };
                        thisLine.AddRange(TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i));
                        codeBlock.AddRange(thisLine);
                        i = i + 5;
                        offset = offset + 7;
                        blockSize = blockSize + 7;
                    }
                    else if (progIn.subBlocks[i].content == "--signal")
                    {
                        sigBlock.Add(blockSize);
                        blockSize = 0;
                        blockStart = true;
                    }
                    else
                    {
                        errorDetail = "\n\nError at line " + Convert.ToString(progIn.subBlocks[i].line) + " column " + Convert.ToString(progIn.subBlocks[i].column) + ".  " + progIn.subBlocks[i].content + ".  Invalid GPLC op - code.";
                        errorLog.Add(errorDetail);
                        break;
                    }
                }
            }
            List<int> dataBlock = new List<int>(AddDataBlock(progIn.bs));
            result.AddRange(sigBlock); result.Add(536870911); result.AddRange(codeBlock); result.Add(536870911); result.AddRange(dataBlock);
            result[0] = result.Count - 1;
            GPLCProgramOut thisProg = new GPLCProgramOut(progIn.progName, result, progIn.bs);
            progGroup.Add(thisProg);
        }

        private List<int> AddDataBlock(List<Binding> bs)
        {
            List<int> dataBlock = new List<int>();
            foreach (Binding b in bs) { dataBlock.Add(b.initValue); }
            return dataBlock;
        }
    }



    class Program
    {
        static void Main(string[] args)
        {
            int m, n = 0, i;
            List<GPLCSource> progName = new List<GPLCSource>();
            List<GPLCSource> valueBlock = new List<GPLCSource>();
            List<GPLCSource> codeBlock = new List<GPLCSource>();
            string source = File.ReadAllText(args[0]) + " ";
            List<GPLCSource> blocks = GPLCParser.Parser(source);
//            for (m = 0; m < blocks.Count; m++)
//            {
//                Console.Write("\ncontent: " + blocks[m].content);
//                Console.Write("\nlength: " + blocks[m].content.Length);
//            }
            for (i = 0; i < blocks.Count; i++)
            {
                if (n == 3)
                {
                    Console.Write("\n\nprogName: ");
                    showBlocks(progName);
                    Console.Write("\n\nvalueBlock: ");
                    showBlocks(valueBlock);
                    Console.Write("\n\ncodeBlock: ");
                    showBlocks(codeBlock);
                    progName = new List<GPLCSource>();
                    valueBlock = new List<GPLCSource>();
                    codeBlock = new List<GPLCSource>();
                    n = 0;
                    i--;
               }
                else if (blocks[i].content == "~") { n++; }
                else if (n == 0) { progName.Add(blocks[i]); }
                else if (n == 1) { valueBlock.Add(blocks[i]); }
                else { codeBlock.Add(blocks[i]); }
            }
            Console.ReadLine();
        }

        static void showBlocks(List<GPLCSource> blocks)
        {
            int i;
            for (i = 0; i < blocks.Count; i++)
            {
                Console.Write(blocks[i].content + " ");
            }
        }
    }
}

