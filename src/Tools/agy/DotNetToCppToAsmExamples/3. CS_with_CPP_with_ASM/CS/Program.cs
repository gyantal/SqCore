
using System;
using System.Runtime.InteropServices;


namespace MyApp // Note: actual namespace depends on the project name.
{
    internal class Program
    {
		[DllImport("CppLib.dll", EntryPoint = "GetAnswerOfLife")]
        public static extern int GetAnswerOfLife();
		
		[DllImport("CppLib.dll", EntryPoint = "CallAsmMaxOfThree")]
        public static extern int CallAsmMaxOfThree([In] int a, [In] int b, [In] int c);
		
        static void Main(string[] args)
        {
            // See https://aka.ms/new-console-template for more information
			Console.WriteLine("C# code BEGIN. It will call C++ code and ASM code (via C++) and print results.\n");
			int cppResult = GetAnswerOfLife();
			Console.WriteLine($"C++ result: {cppResult}\n");
			
			int cppResultMax = CallAsmMaxOfThree(15, 150, 77);
			Console.WriteLine($"ASM result of calling CallAsmMaxOfThree(15, 150, 77) via C++: {cppResultMax}\n");

			Console.WriteLine("C# code END.\n");
        }
    }
}




