/* Game::Dangerous Editor code by Steven Tinsley.You are free to use this software and view its source code. */
/* If you wish to redistribute it or use it as part of your own work, this is permitted as long as you acknowledge the work is by the abovementioned author. */

/*This is the C# implementation of the GPLC bytecode generator, designed to be equivalent to Game-Dangerous/assm_gplc.hs but with source code error reporting. */

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

    class GPLC_parser
    {
        public List<GPLC_source> Parser(string source_in, int limit)
        {
            int i = 0, l = 0, c = 0, len;
            GPLC_source next_block;
            List<GPLC_source> source_out = new List<GPLC_source>();
            for ( ; ; )
            {
                next_block = build_sub_block(source_in, i, l, c);
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
                if (i > limit) {break;}
            }
            return source_out;
        }

        private GPLC_source build_sub_block(string source_in, int i, int l, int c)
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

    class Program
    {
        static void Main(string[] args)
        {
            string contents;
            int limit;
            GPLC_parser t = new GPLC_parser();
            List<GPLC_source> result;
            contents = System.IO.File.ReadAllText(args[0]);
            limit = contents.Length;
            result = t.Parser(contents, limit);
            Console.WriteLine("\nParser output: \n\n");
            foreach (GPLC_source sub_block in result) {
                Console.Write("\n\nline: " + sub_block.line);
                Console.Write("\ncolumn: " + sub_block.column);
                Console.Write("\ncontent: " + sub_block.content);
            };
            Console.ReadLine();
        }
    }
}
