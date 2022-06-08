#include <iostream>

#include <stdio.h>
#include <inttypes.h>

extern "C"
{
    int64_t maxofthree(int64_t, int64_t, int64_t);
	
	__declspec (dllexport) const int GetAnswerOfLife();
	__declspec (dllexport) const int CallAsmMaxOfThree(int a, int b, int c);
}

const int GetAnswerOfLife()
{
	// it seems, windows System calls are not allowed.
	// calling  printf(), C# code will do exception: System.AccessViolationException: Attempted to read or write protected memory. This is often an indication that other memory is corrupt.
	// https://stackoverflow.com/questions/58991825/accessviolationexception-while-trying-to-p-invoke-a-simple-c-function
	// "If I change the function to just simply return an integer then it seems to work, and the C# program receives the int value (which shows that the exported name isn't mangled). But if I put basically any other code in there (including a simple printf) then I get the exception."
	// printf("C++ code BEGIN. It will call ASM code and print results. ASM code BEGIN\n");
	
	// std::cout << "Hello World" << std::endl; // this crashes AccessViolationException too
	return 42;
}

const int CallAsmMaxOfThree(int a, int b, int c)
{
	// printf("C++ code BEGIN. It will call ASM code and print results. ASM code BEGIN\n");
	// int64_t k = maxofthree(a, b, c);
	// int64_t k_64 = a;
    // printf("ASM results, maxOfThree: %ld\n", k_64);
	// printf("C++ code END.\n");
	// int k_int32 = (int)k_64;
	// return k_int32;
	int64_t a64 = (int64_t)a;
	int64_t b64 = (int64_t)b;
	int64_t c64 = (int64_t)c;
	int64_t result64 = maxofthree(a64, b64, c64);
	int result = (int)result64;
	return result;
}

int main()
{
    int i = 5;
    i++;
    int j = 6;
    j = j + i;

    printf("C++ code BEGIN. It will call ASM code and print results. ASM code BEGIN\n");

    int64_t k = maxofthree(1, 2, 3);
    printf("ASM results, maxOf(1, 2, 3): %ld\n", k);
    int64_t k2 = maxofthree(150, 12, 11);
    printf("ASM results, maxOf(150, 12, 11): %ld\n", k2);

    // https://www.codeproject.com/Articles/15971/Using-Inline-Assembly-in-C-C
    /* Add 10 and 20 and store result into register %eax */
    __asm__ ( "movl $10, %eax;"
                "movl $20, %ebx;"
                "addl %ebx, %eax;"
    );
    printf("C++ code END.\n");

    // std::cout << "Hello World" << std::endl;
}