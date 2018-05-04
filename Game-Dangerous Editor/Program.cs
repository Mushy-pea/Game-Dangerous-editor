// Game::Dangerous Editor code by Steven Tinsley.  You are free to use this software and view its source code.
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
        public List<char> content;
    };

    struct Binding
    {
        public string symbol;
        public int read_index;
        public int write_index;
        public int init_value;
    }

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
                len = next_block.content.Count;
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
            sub_block.content = content;
            sub_block.line = l;
            sub_block.column = c;
            return sub_block;
        }
    }

class Value_binder
    {
        public List<Binding> Bind_values(List<GPLC_source> sub_blocks, int offset)
        {
            int i = 0, j, k = 0;
            string error_detail;
            List<string> error_log = new List<string>();
            List<string> fragment = new List<string>();
            bool success = true;
            List<Binding> b = new List<Binding>();
            List<Binding> failure = new List<Binding>();
            Binding this_b = new Binding();
            for (j = 0; j < sub_blocks.Count; j = j + 2)
            {
                this_b.symbol = string.Join("", sub_blocks[j].content);
                this_b.read_index = i;
                this_b.write_index = offset + i;
                try
                {
                    this_b.init_value = Convert.ToInt32(string.Join("", sub_blocks[j + 1].content));
                }
                catch (FormatException)
                {
                    success = false;
                    fragment.Add(string.Join("", sub_blocks[j + 1].content));
                    error_detail = "\nError at line " + Convert.ToString(sub_blocks[j + 1].line) + " column " + Convert.ToString(sub_blocks[j + 1].column) + ".  The second term in a value initialisation must be an integer.  For details of how non - integer arguments are handled see the Op - code arguments section of the GPLC specification.";
                    error_log.Add(error_detail);
                }
                b.Add(this_b);
                i++;
            }
            if (success == false)
            {
                foreach (string error in error_log)
                {
                    Console.WriteLine("\nfragment: " + fragment[k]);
                    Console.WriteLine(error);
                    k++;
                }
                return failure;
            }
            else { return b; }
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
