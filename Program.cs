using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace FileEncrypter
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new Form1();
            processArgs(args, form);
            Application.Run(form);
        }

        static void processArgs(string[] args, Form1 form)
        {
            if (args.Length < 2)
            {
                if (args.Length > 0)
                    Console.Error.WriteLine("Error: The minimum number of command line arguments is 2");
                return;
            }
            switch (args[0])
            {
                case "enc":
                    form.OperationIsEncryption = true;
                    break;
                case "dec":
                    form.OperationIsEncryption = false;
                    break;
                default:
                    Console.Error.WriteLine("Error: Invalid command line argument: '{0}'", args[0]);
                    return;
            }
            for (int i = 1; i < args.Length; ++i) form.AddFileToList(args[i]);
        }
    }
}
