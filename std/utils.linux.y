fn writestr(str: *char)
{
    let len = strlen(str);
    asm 
    {
        mov rax, 1
        mov rdi, 1
        mov rsi, [rbp + 16]
        mov rdx, [rbp - 16]
        syscall
    }
}
fn writechar(c: char) 
{
    asm 
    {
        mov rax, 1
        mov rdi, 1
        lea rsi, [rbp + 16]
        mov rdx, 1
        syscall
    }
}
fn clear() 
{
    let str: *char = null;
    asm 
    {
        sub rsp, 16
        mov rax, rsp
        mov byte[rax], 0x1b
        inc rax
        mov byte[rax], 0x5b
        inc rax
        mov byte[rax], 0x31
        inc rax
        mov byte[rax], 0x3b
        inc rax
        mov byte[rax], 0x31
        inc rax
        mov byte[rax], 0x48
        inc rax
        mov byte[rax], 0x1b
        inc rax
        mov byte[rax], 0x5b
        inc rax
        mov byte[rax], 0x32
        inc rax
        mov byte[rax], 0x4a
        mov [rbp - 8], rsp 
    }
    writestr(str);
}
