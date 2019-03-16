// Game :: Dangerous Editor code by Steven Tinsley.  You are free to use this software and view its source code.
// If you wish to redistribute it or use it as part of your own work, this is permitted as long as you acknowledge the work is by the abovementioned author.

//This is the C# implementation of the GPLC bytecode generator, designed to be equivalent to Game-Dangerous/assm_gplc.hs but with source code error reporting.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameDangerousEditor
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
                error_log.Add(error_detail);
            }
            return 0;
        }
    }

    // The methods of this class parse the GPLC source code into a list of structs, each of which holds the parsed block of code and its line and column location in the input text file.
    // This is so it is simple to report the locations of source code errors to the user when they are detected further along the transformation pipeline.
    class GPLCParser
    {
        public List<GPLCSource> Parser(string sourceIn)
        {
            int i = 0, l = 0, c = 0, len;
            GPLCSource nextBlock;
            List<GPLCSource> sourceOut = new List<GPLCSource>();
            for ( ; ; )
            {
                if (i == sourceIn.Length) {break;}
                nextBlock = BuildSubBlock(sourceIn, i, l, c);
                sourceOut.Add(nextBlock);
                len = nextBlock.content.Length;
                i = i + len;
                if (sourceIn[i] == ' ') {c = c + len + 1;}
                else
                {
                    c = 0;
                    l = l + 1;
                }
                i++;
            }
            return sourceOut;
        }

        private GPLCSource BuildSubBlock(string sourceIn, int i, int l, int c)
        {
            List<char> content = new List<char>();
            GPLCSource subBlock = new GPLCSource();
            for ( ; ; )
            {
                if (sourceIn[i] == ' ' || sourceIn[i] == '\n') {break;}
                else {content.Add(sourceIn[i]);}
                i++;
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
        public List<Binding> BindValues(List<GPLC_source> subBlocks, int offset, List<string> errorLog)
        {
            int i = 0, j;
            List<Binding> b = new List<Binding>();
            Binding thisB = new Binding();
            for (j = 0; j < subBlocks.Count; j = j + 2)
            {
                thisB.symbol = subBlocks[j].content;
                thisB.readIndex = i;
                thisB.writeIndex = offset + i;
                thisB.initValue = SafeArgHdlr.ReadLiteral(subBlocks[j + 1], errorLog, 0);
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

        private List<int> TransformArguments (List<GPLCSource> source, List<Binding> bs, List<int> mode, List<string> errorLog, int i)
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
            for (i = 0; i < progIn.subBlocks.Count; )
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
                        List<int> thisLine = new List<int> { 1, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 6;
                        offset = offset + 6;
                        blockSize = blockSize + 6;
                    }
                    else if (progIn.subBlocks[i].content == "chg_state")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 2, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 10;
                        offset = offset + 10;
                        blockSize = blockSize + 10;
                    }
                    else if (progIn.subBlocks[i].content == "chg_grid")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 3, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 8;
                        offset = offset + 8;
                        blockSize = blockSize + 8;
                    }
                    else if (progIn.subBlocks[i].content == "send_signal")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 4, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 5;
                        offset = offset + 5;
                        blockSize = blockSize + 5;
                    }
                    else if (progIn.subBlocks[i].content == "chg_value")
                    {
                        List<int> mode = new List<int> { 1, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 5, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 7;
                        offset = offset + 7;
                        blockSize = blockSize + 7;
                    }
                    else if (progIn.subBlocks[i].content == "chg_floor")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 6, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 7;
                        offset = offset + 7;
                        blockSize = blockSize + 7;
                    }
                    else if (progIn.subBlocks[i].content == "chg_ps1")
                    {
                        List<int> mode = new List<int> { 0, 0, 0 };
                        List<int> thisLine = new List<int> { 7, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 4;
                        offset = offset + 4;
                        blockSize = blockSize + 4;
                    }
                    else if (progIn.subBlocks[i].content == "chg_obj_type")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 8, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 5;
                        offset = offset + 5;
                        blockSize = blockSize + 5;
                    }
                    else if (progIn.subBlocks[i].content == "place_hold")
                    {
                        List<int> mode = new List<int> { 0 };
                        List<int> thisLine = new List<int> { 9, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 2;
                        offset = offset + 2;
                        blockSize = blockSize + 2;
                    }
                    else if (progIn.subBlocks[i].content == "chg_grid_")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 10, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 8;
                        offset = offset + 8;
                        blockSize = blockSize + 8;
                    }
                    else if (progIn.subBlocks[i].content == "copy_ps1")
                    {
                        List<int> mode = new List<int> { 2, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 11, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 5;
                        offset = offset + 5;
                        blockSize = blockSize + 5;
                    }
                    else if (progIn.subBlocks[i].content == "copy_lstate")
                    {
                        List<int> mode = new List<int> { 2, 0, 0, 0, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 12, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 8;
                        offset = offset + 8;
                        blockSize = blockSize + 8;
                    }
                    else if (progIn.subBlocks[i].content == "pass_msg")
                    {
                        List<int> mode = new List<int> { 0 };
                        List<int> thisLine = new List<int> { 13, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i + 1) };
                        for (n = 3; n <= SafeArgumentHandler.ReadLiteral(progIn.subBlocks[i + 1], error_log, 1); n++)
                        {
                            thisLine.Add(SafeArgumentHandler.ReadLiteral(progIn.subBlocks[i + n], error_log, 1));
                        }
                        codeBlock.AddRange(thisLine);
                        i = i + n + 1;
                        offset = offset + n;
                        blockSize = blockSize + n;
                    }
                    else if (progIn.subBlocks[i].content == "chg_ps0")
                    {
                        List<int> mode = new List<int> { 0, 0, 0 };
                        List<int> thisLine = new List<int> { 14, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 4;
                        offset = offset + 4;
                        blockSize = blockSize + 4;
                    }
                    else if (progIn.subBlocks[i].content == "copy_ps0")
                    {
                        List<int> mode = new List<int> { 2, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 15, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 5;
                        offset = offset + 5;
                        blockSize = blockSize + 5;
                    }
                    else if (progIn.subBlocks[i].content == "binary_dice")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 2 };
                        List<int> thisLine = new List<int> { 16, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 7;
                        offset = offset + 7;
                        blockSize = blockSize + 7;
                    }
                    else if (progIn.subBlocks[i].content == "project_init")
                    {
                        List<int> mode = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0 };
                        List<int> thisLine = new List<int> { 17, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 14;
                        offset = offset + 14;
                        blockSize = blockSize + 14;
                    }
                    else if (progIn.subBlocks[i].content == "project_update")
                    {
                        List<int> mode = new List<int> { 0, 1, 0, 0, 0 };
                        List<int> thisLine = new List<int> { 18, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 6;
                        offset = offset + 6;
                        blockSize = blockSize + 6;
                    }
                    else if (progIn.subBlocks[i].content == "init_npc")
                    {
                        List<int> mode = new List<int> { 0, 0 };
                        List<int> thisLine = new List<int> { 19, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 3;
                        offset = offset + 3;
                        blockSize = blockSize + 3;
                    }
                    else if (progIn.subBlocks[i].content == "npc_decision")
                    {
                        List<int> mode = new List<int> { 1 };
                        List<int> thisLine = new List<int> { 20, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 2;
                        offset = offset + 2;
                        blockSize = blockSize + 2;
                    }
                    else if (progIn.subBlocks[i].content == "npc_move")
                    {
                        List<int> mode = new List<int> { 1 };
                        List<int> thisLine = new List<int> { 21, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 2;
                        offset = offset + 2;
                        blockSize = blockSize + 2;
                    }
                    else if (progIn.subBlocks[i].content == "npc_damage")
                    {
                        List<int> mode = new List<int> { 2 };
                        List<int> thisLine = new List<int> { 22, TransformArguments(progIn.subBlocks, progIn.bs, mode, errorLog, i) };
                        codeBlock.AddRange(thisLine);
                        i = i + 2;
                        offset = offset + 2;
                        blockSize = blockSize + 2;
                    }
                    else if (progIn.subBlocks[i].content == "block")
                    {

                        List<int> this_line = new List<int> { 5, 0, 0, Safe_arg_hdlr.Read_literal(source[m + 1], error_log, 1), Safe_arg_hdlr.Ref_to_offset(prog_in.bs, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(prog_in.bs, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(prog_in.bs, source[m + 4], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 5;
                        offset = offset + 7;
                        block_size = block_size + 7;
                    }
                    else if (source[m].content == "--signal")
                    {
                        sig_block.Add(block_size);
                        block_size = 0;
                        block_start = true;
                    }
                    else
                    {
                        error_detail = "\n\nError at line " + Convert.ToString(source[m].line) + " column " + Convert.ToString(source[m].column) + ".  " + source[m].content + " is not a valis GPLC op - code.  Compilation aborted.";
                        error_log.Add(error_detail);
                        break;
                    }
                }
            }
            List<int> data_block = new List<int>(Add_data_block(prog_in.bs));
            result.AddRange(sig_block); result.Add(536870911); result.AddRange(code_block); result.Add(536870911); result.AddRange(data_block);
            result[0] = result.Count - 1;
            result.Add(result.Count - 1 - data_block.Count);
            GPLC_program_out this_prog = new GPLC_program_out(prog_in.prog_name, result, prog_in.bs);
            prog_group.Add(this_prog);
            Console.WriteLine("Transform_program added to prog_group.  Length: " + Convert.ToString(prog_group.Count));
        }

        public List<int> Add_data_block(List<Binding> bs)
        {
            List<int> data_block = new List<int>();
            foreach (Binding b in bs) { data_block.Add(b.init_value); }
            return data_block;
        }
    }

    

    class Program
    {
        static void Main(string[] args)
        {
            int m, n = 0, offset;
            string source, structure;
            string[] src_blocks, struct_blocks;
            List<string> error_log = new List<string>();
            GPLC_parser s = new GPLC_parser();
            Value_binder t = new Value_binder();
            GPLC_program_in this_prog = new GPLC_program_in();
            List<GPLC_source> temp = new List<GPLC_source>();
            Gen_bytecode fst_pass = new Gen_bytecode(); Gen_bytecode snd_pass = new Gen_bytecode();

            source = File.ReadAllText(args[0]) + " ";
            structure = File.ReadAllText(args[1]);
            src_blocks = Regex.Split(source, "~");
            struct_blocks = Regex.Split(structure, ", ");
            foreach (string block in src_blocks)
            {
                Console.Write("\n\n" + Convert.ToString(n) + ": " + block);
                n++;
            }
            for (m = 0; m < 2; m++) {
                n = 0;
                foreach (string block in src_blocks)
                {
                    if (n % 3 == 0) { this_prog.prog_name = block; }
                    else if (n % 3 == 1)
                    {
                        temp = s.Parser(block);
                        if (m == 0)
                        {
                            this_prog.bs = t.Bind_values(temp, 0, error_log);
                            if (error_log.Count > 0)
                            {
                                report_error(error_log);
                                Console.Write("\n\nCompilation failed at value binding stage.");
                                Environment.Exit(0);
                            }
                        }
                        else
                        {
                            offset = fst_pass.Get_program_out(n / 3).bytecode.Last();
                            this_prog.bs = t.Bind_values(temp, offset, error_log);
                        }
                    }
                    else
                    {
                        this_prog.sub_blocks = s.Parser(block);
                        if (m == 0)
                        {
                            fst_pass.Transform_program(this_prog, error_log);
                            Console.WriteLine("fst_pass.Transform_program called.");
                            if (error_log.Count > 0)
                            {
                                report_error(error_log);
                                Console.Write("\n\nCompilation failed at code block transformation stage.");
                                Environment.Exit(0);
                            }
                        }
                        else
                        {
                            snd_pass.Transform_program(this_prog, error_log);
                        }
                    }
                    n++;
                }
            }
            fst_pass.Check_length();
            snd_pass.Check_length();
            Console.Write("\nEnter the number of the program you wish to view: ");
            string choice = Console.ReadLine();
            GPLC_program_out prog_out = snd_pass.Get_program_out(Convert.ToInt32(choice));
            Console.Write("\nOutput: " + show_ints(prog_out.bytecode));
            

        }

        static void report_error(List<string> error_log)
        {
            foreach (string error in error_log) { Console.Write(error); }
        }

        static string show_ints(List<int> xs)
        {
            string result = "";
            foreach (int x in xs) { result = result + Convert.ToString(x) + ", "; }
            return result;
        }

    }
    
}
