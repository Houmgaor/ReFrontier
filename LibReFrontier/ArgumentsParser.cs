using System;
using System.Collections.Generic;

namespace LibReFrontier
{
    /// <summary>
    /// Useful methods for Command Line Interface.
    /// </summary>
    public class ArgumentsParser
    {

        /// <summary>
        /// Simple arguments parser.
        /// </summary>
        /// <param name="args">Input arguments from the CLI</param>
        /// <returns>Dictionary of arguments. Arguments with no value have a null value assigned.</returns>
        public static Dictionary<string, string> ParseArguments(string[] args)
        {
            var arguments = new Dictionary<string, string>();
            foreach (var arg in args)
            {
                string[] parts = arg.Split('=');
                if (parts.Length == 2)
                {
                    arguments[parts[0]] = parts[1];
                }
                else
                {
                    arguments[arg] = null;
                }
            }
            return arguments;
        }


        /// <summary>
        /// Print to console with seperator
        /// </summary>
        /// <param name="input">Value to print</param>
        /// <param name="printBefore">Set to true to display input before the separator.</param>
        public static void Print(string input, bool printBefore)
        {
            if (printBefore)
            {
                Console.WriteLine("\n==============================");
                Console.WriteLine(input);
            }
            else
            {
                Console.WriteLine(input);
                Console.WriteLine("==============================");
            }
        }
    }
}
