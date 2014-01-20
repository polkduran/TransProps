using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TransProp.Core;

namespace TransProp.TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            PropsDocument document = new PropsDocument(true);
            document.Load(@"P:\Perso\i3-label_fr - Copy.properties");

            
            foreach (PropsElement element in document.Elements)
            {
                Console.WriteLine(element);
            }

            Console.ReadLine();
        }
    }
}
