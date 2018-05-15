// Game :: Dangerous Editor code by Steven Tinsley.  You are free to use this software and view its source code.
// If you wish to redistribute it or use it as part of your own work, this is permitted as long as you acknowledge the work is by the abovementioned author.

//This is the C# implementation of the GPLC bytecode generator, designed to be equivalent to Game-Dangerous/assm_gplc.hs but with source code error reporting.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game_Dangerous_Editor
{

    struct GPLC_source
    {
        public int line;
        public int column;
        public string content;
    };

    struct Binding
    {
        public string symbol;
        public int read_index;
        public int write_index;
        public int init_value;
    }

    // The methods of this class are used to transform GPLC opcode arguments to their bytecode form in an error safe way, such that source code errors are handled and user feedback added to the error log.
    class Safe_arg_hdlr
    {
        public static int Ref_to_offset(List<Binding> b, GPLC_source sub_block, int mode, List<string> error_log)
        {
            int n;
            for (n = 0; n <= b.Count; n++)
            {
                if (n == b.Count) { error_log.Add("\n\nError at line " + Convert.ToString(sub_block.line) + " column " + Convert.ToString(sub_block.column) + ".  " + sub_block.content + " is an undeclared reference argument."); }
                else if (b[n].symbol == sub_block.content)
                {
                    if (mode == 0) { return b[n].read_index; }
                    else { return b[n].write_index; }
                }
                else { }
            }
            return 0;
        }

        public static int Read_literal(GPLC_source sub_block, List<string> error_log, int mode)
        {
            string error_detail;
            try
            {
                return (Convert.ToInt32(sub_block.content));
            }
            catch (FormatException)
            {
                error_detail = "\n\nError at line " + Convert.ToString(sub_block.line) + " column " + Convert.ToString(sub_block.column) + ".  ";
                if (mode == 0) {error_detail = error_detail + "The second term in a value initialisation must be an integer.  For details of how non - integer arguments are handled see the Op - code arguments section of the GPLC specification."};
                else { error_detail = error_detail + "This opcode argument must be a literal integer."; }
                error_log.Add(error_detail);
            }
            return 0;
        }
    }

    // The methods of this class parse the GPLC source code into a list of structs, each of which holds the parsed block of code and its line and column location in the input text file.
    // This is so it is simple to report the locations of source code errors to the user when they are detected further along the transformation pipeline.
    class GPLC_parser
    {
        public List<GPLC_source> Parser(string source_in, int limit)
        {
            int i = 0, l = 0, c = 0, len;
            GPLC_source next_block;
            List<GPLC_source> source_out = new List<GPLC_source>();
            for ( ; ; )
            {
                if (i > limit) {break;}
                next_block = Build_sub_block(source_in, i, l, c);
                source_out.Add(next_block);
                len = next_block.content.Length;
                i = i + len;
                if (source_in[i] == ' ') {c = c + len + 1;}
                else
                {
                    c = 0;
                    l = l + 1;
                }
                i++;
            }
            return source_out;
        }

        private GPLC_source Build_sub_block(string source_in, int i, int l, int c)
        {
            List<char> content = new List<char>();
            GPLC_source sub_block = new GPLC_source();
            for ( ; ; )
            {
                if (source_in[i] == ' ' || source_in[i] == '\n') {break;}
                else {content.Add(source_in[i]);}
                i++;
            }
            sub_block.content = string.Join("", content);
            sub_block.line = l;
            sub_block.column = c;
            return sub_block;
        }
    }

    // The Bind_values member takes as input a list of GPLC_source structs arising from the parsing of a GPLC program's value block.  Its output is a list of Binding structs
    // that each encode the relationship between a GPLC symbolic reference argument and the corresponding data block offset used at bytecode level.
    class Value_binder
    {
        public List<Binding> Bind_values(List<GPLC_source> sub_blocks, int offset, List<string> error_log)
        {
            int i = 0, j;
            List<Binding> b = new List<Binding>();
            Binding this_b = new Binding();
            for (j = 0; j < sub_blocks.Count; j = j + 2)
            {
                this_b.symbol = sub_blocks[j].content;
                this_b.read_index = i;
                this_b.write_index = offset + i;
                this_b.init_value = Safe_arg_hdlr.Read_literal(sub_blocks[j + 1], error_log, 0);
                i++;
            }
            return b;
        }
    }

    // The members of this class handle the transformation of GPLC keywords to op - codes and of their reference arguments to the data block offsets used at bytecode level.
    class Process_opcodes
    {
        public List<int> Transform_opcode(List<GPLC_source> source, List<Binding> b, List<string> error_log)
        {
            int offset = 0, block_size = 0, m, n;
            List<int> sig_block = new List<int>();
            List<int> code_block = new List<int>();
            List<int> result = new List<int> { 0, 0 };
            string error_detail;
            bool block_start = true;
            for (m = 0; m < source.Count; )
            {
                if (block_start == true && source[m].content != "--signal")
                {
                    error_detail = "\n\nError at line " + Convert.ToString(source[m].line) + " column " + Convert.ToString(source[m].column) + ".  Every block of code must begin with --signal x, where x is the integer signal to be handled by the block.";
                    error_log.Add(error_detail);
                    break;
                }
                else if (block_start == true)
                {
                    try
                    {
                        sig_block.Add(Convert.ToInt32(source[m + 1].content));
                        sig_block.Add(offset);
                        block_start = false;
                        m = m + 2;
                    }
                    catch (FormatException)
                    {
                        error_detail = "\n\nError at line " + Convert.ToString(source[m + 1].line) + " column " + Convert.ToString(source[m + 1].column) + ".  The second term in a signal handler statement must be an integer.";
                        error_log.Add(error_detail);
                        break;
                    }
                }
                else
                {
                    if (source[m].content == "if")
                    {
                        List<int> this_line = new List<int> { 1, Safe_arg_hdlr.Read_literal(source[m + 1], error_log, 1), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Read_literal(source[m + 4], error_log, 1), Safe_arg_hdlr.Read_literal(source[m + 5], error_log, 1) };
                        code_block.AddRange(this_line);
                        m = m + 6;
                        offset = offset + 6;
                        block_size = block_size + 6;
                    }
                    else if (source[m].content == "chg_state")
                    {
                        List<int> this_line = new List<int> { 2, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 5], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 6], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 7;
                        offset = offset + 7;
                        block_size = block_size + 7;
                    }
                    else if (source[m].content == "chg_grid")
                    {
                        List<int> this_line = new List<int> { 3, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 5], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 6], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 7], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 8;
                        offset = offset + 8;
                        block_size = block_size + 8;
                    }
                    else if (source[m].content == "send_signal")
                    {
                        List<int> this_line = new List<int> { 4, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 5;
                        offset = offset + 5;
                        block_size = block_size + 5;
                    }
                    else if (source[m].content == "chg_value")
                    {
                        List<int> this_line = new List<int> { 5, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 1, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 5], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 6], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 7;
                        offset = offset + 7;
                        block_size = block_size + 7;
                    }
                    else if (source[m].content == "chg_floor")
                    {
                        List<int> this_line = new List<int> { 6, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 5], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 6], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 7;
                        offset = offset + 7;
                        block_size = block_size + 7;
                    }
                    else if (source[m].content == "chg_ps1")
                    {
                        List<int> this_line = new List<int> { 7, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 4;
                        offset = offset + 4;
                        block_size = block_size + 4;
                    }
                    else if (source[m].content == "chg_obj_type")
                    {
                        List<int> this_line = new List<int> { 8, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 5;
                        offset = offset + 5;
                        block_size = block_size + 5;
                    }
                    else if (source[m].content == "place_hold")
                    {
                        List<int> this_line = new List<int> { 9, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 2;
                        offset = offset + 2;
                        block_size = block_size + 2;
                    }
                    else if (source[m].content == "chg_grid_")
                    {
                        List<int> this_line = new List<int> { 10, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 5], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 6], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 7], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 8;
                        offset = offset + 8;
                        block_size = block_size + 8;
                    }
                    else if (source[m].content == "copy_ps1")
                    {
                        List<int> this_line = new List<int> { 11, Safe_arg_hdlr.Read_literal(source[m + 1], error_log, 1), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 5;
                        offset = offset + 5;
                        block_size = block_size + 5;
                    }
                    else if (source[m].content == "copy_lstate")
                    {
                        List<int> this_line = new List<int> { 12, Safe_arg_hdlr.Read_literal(source[m + 1], error_log, 1), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 5], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 6], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 7], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 8;
                        offset = offset + 8;
                        block_size = block_size + 8;
                    }
                    else if (source[m].content == "pass_msg")
                    {
                        List<int> this_line = new List<int> { 13, Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log)};
                        for (n = 3; n <= Safe_arg_hdlr.Read_literal(source[m + 1], error_log, 1); n++)
                        {
                            this_line.Add(Safe_arg_hdlr.Read_literal(source[m + n], error_log, 1));
                        }
                        code_block.AddRange(this_line);
                        m = m + n + 1;
                        offset = offset + n;
                        block_size = block_size + n;
                    }
                    else if (source[m].content == "chg_ps0")
                    {
                        List<int> this_line = new List<int> { 14, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 4;
                        offset = offset + 4;
                        block_size = block_size + 4;
                    }
                    else if (source[m].content == "copy_ps0")
                    {
                        List<int> this_line = new List<int> { 15, Safe_arg_hdlr.Read_literal(source[m + 1], error_log, 1), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 5;
                        offset = offset + 5;
                        block_size = block_size + 5;
                    }
                    else if (source[m].content == "binary_dice")
                    {
                        List<int> this_line = new List<int> { 16, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 5], 0, error_log), Safe_arg_hdlr.Read_literal(source[m + 6], error_log, 1) };
                        code_block.AddRange(this_line);
                        m = m + 7;
                        offset = offset + 7;
                        block_size = block_size + 7;
                    }
                    else if (source[m].content == "project_init")
                    {
                        List<int> this_line = new List<int> { 17, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 5], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 6], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 7], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 8], 0, error_log), Safe_arg_hdlr.Read_literal(source[m + 9], error_log, 1), Safe_arg_hdlr.Ref_to_offset(b, source[m + 10], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 11;
                        offset = offset + 11;
                        block_size = block_size + 11;
                    }
                    else if (source[m].content == "project_update")
                    {
                        List<int> this_line = new List<int> { 18, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 1, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 5], 0, error_log) };
                        code_block.AddRange(this_line);
                        m = m + 6;
                        offset = offset + 6;
                        block_size = block_size + 6;
                    }
                    else if (source[m].content == "init_npc")
                    {
                        List<int> this_line = new List<int> { 19, Safe_arg_hdlr.Ref_to_offset(b, source[m + 1], 0, error_log), Safe_arg_hdlr.Read_literal(source[m + 2], error_log, 1) };
                        code_block.AddRange(this_line);
                        m = m + 3;
                        offset = offset + 3;
                        block_size = block_size + 3;
                    }
                    else if (source[m].content == "npc_decision")
                    {
                        List<int> this_line = new List<int> { 20, Safe_arg_hdlr.Read_literal(source[m + 1], error_log, 1) };
                        code_block.AddRange(this_line);
                        m = m + 2;
                        offset = offset + 2;
                        block_size = block_size + 2;
                    }
                    else if (source[m].content == "npc_move")
                    {
                        List<int> this_line = new List<int> { 21, Safe_arg_hdlr.Read_literal(source[m + 1], error_log, 1) };
                        code_block.AddRange(this_line);
                        m = m + 2;
                        offset = offset + 2;
                        block_size = block_size + 2;
                    }
                    else if (source[m].content == "npc_damage")
                    {
                        List<int> this_line = new List<int> { 22 };
                        code_block.AddRange(this_line);
                        m = m + 1;
                        offset = offset + 1;
                        block_size = block_size + 1;
                    }
                    else if (source[m].content == "block")
                    {
                        List<int> this_line = new List<int> { 5, 0, 0, Safe_arg_hdlr.Read_literal(source[m + 1], error_log, 1), Safe_arg_hdlr.Ref_to_offset(b, source[m + 2], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 3], 0, error_log), Safe_arg_hdlr.Ref_to_offset(b, source[m + 4], 0, error_log) };
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
            result.AddRange(sig_block); result.Add(536870911); result.AddRange(code_block); result.Add(536870911);
            return (result);
        }

        private List<int> Add_value_block(List<Binding> b)
        {

        }

    }

    class Program
    {
        static void Main(string[] args)
        {
            string contents;
            GPLC_parser s = new GPLC_parser();
            Value_binder t = new Value_binder();
            List<GPLC_source> source;
            List<Binding> result;
            contents = System.IO.File.ReadAllText(args[0]);
            source = s.Parser(contents, contents.Length - 1);
            result = t.Bind_values(source, Convert.ToInt32(args[1]));
            foreach (GPLC_source src in source)
            {
                Console.WriteLine("\nline: " + src.line);
                Console.WriteLine("column: " + src.column);
                Console.WriteLine("content: " + string.Join("", src.content));
            }
            if (result.Count == 0) { Console.WriteLine("\nCompilation failed at value binding stage."); }
            else
            {
                foreach (Binding b in result)
                {
                    Console.WriteLine("\nSymbol: " + b.symbol);
                    Console.WriteLine("Read index: " + b.read_index);
                    Console.WriteLine("Write index: " + b.write_index);
                    Console.WriteLine("Initial value: " + b.init_value);
                }
            }
            Console.ReadLine();
        }
    }
    
}
