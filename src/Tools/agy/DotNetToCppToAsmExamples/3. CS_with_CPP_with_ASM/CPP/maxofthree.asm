; -----------------------------------------------------------------------------
; A 64-bit function that returns the maximum value of its three 64-bit integer
; arguments.  The function has signature:
;
;   int64_t maxofthree(int64_t x, int64_t y, int64_t z)
;
; Note that the parameters have already been passed in rdi, rsi, and rdx.  We
; just have to return the value in rax.
; -----------------------------------------------------------------------------

        global  maxofthree
        section .text
maxofthree:
        ; mov     rax, rdi                ; result (rax) initially holds x
        ; cmp     rax, rsi                ; is x less than y?
        ; cmovl   rax, rsi                ; if so, set result to y
        ; cmp     rax, rdx                ; is max(x,y) less than z?
        ; cmovl   rax, rdx                ; if so, set result to z
        ; ret                             ; the max will be in rax

; Agy: looking at Disassembly, nasm -fwin64 maxofthree.asm did differently. It places the parameters into ecx, edx, r8d
; but those are 32 bit registers. I had to find the 64bit equivalents.
        mov     rax, rcx                ; result (rax) initially holds x
        cmp     rax, rdx                ; is x less than y?
        cmovl   rax, rdx                ; if so, set result to y
        cmp     rax, r8                ; is max(x,y) less than z?
        cmovl   rax, r8                ; if so, set result to z
        ret                             ; the max will be in rax