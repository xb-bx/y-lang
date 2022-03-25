fn writestr(str: *char, len: u64)
{
    asm 
    {
        mov eax, 1
        mov edi, 1
        mov rsi, [rbp + 16]
        mov rdx, [rbp + 24]
        syscall
    }
}
fn writechar(c: char) 
{
    asm 
    {
        mov eax, 1
        mov edi, 1
        lea rsi, [rbp + 16]
        mov edx, 1
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
