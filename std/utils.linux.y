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
fn alloc(size: u64): *u8 
{ 
    asm 
    {
        mov eax, 9
        xor edi, edi
        mov rsi, qword[rbp + 16]
        mov edx, 3
        mov r10, 0x22
        mov r8, -1
        mov r9, 0
        syscall
        leave 
        ret
    }
    ret null;
}
fn free(ptr: *u8, size: u64) 
{
    asm 
    {
        mov eax, 11
        mov rdi, qword[rbp + 16]
        mov rsi, qword[rbp + 24]
        syscall
        leave
        ret
    }
}
fn exit()
{
    asm 
    {
        mov eax, 60
        xor rdi, rdi
        syscall
    }
}
