#include <iostream>

#include <stdio.h>
#include <inttypes.h>
// see https://cs.lmu.edu/~ray/notes/nasmtutorial/

extern "C"
{
    int64_t maxofthree(int64_t, int64_t, int64_t);
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