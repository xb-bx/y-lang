let stdout = null;

fn writechar(x: char) : void 
{
    asm 
    {
        sub rsp, 1
        mov al, byte[rbp + 16]
        mov byte[rsp], al
        mov r15, rsp
        mov r13, 1
        invoke WriteConsoleA, [stdout], r15, r13d, 0
        add rsp, 1
    }
}
fn u8tochar(x: u8): char 
{   
    let res = ' ';
    asm 
    {
        mov al, byte[rbp + 16]
        mov byte[rbp - 8], al
    }
    ret res;
}
fn initstdout(): void 
{
    asm 
    {
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        mov [stdout], rax
    }
    ret;
}
fn sleep(ms: i32) : void 
{
    asm 
    {
        invoke Sleep, dword[rbp + 16]
    }
}
fn modulo(x: i32, y:i32) : i32 
{
    let res = 0;
    asm 
    {
        mov eax, dword[rbp + 16]
        mov edx, 0
        idiv dword[rbp + 24]
        mov dword[rbp - 8], edx
    }
    ret res;
}
fn clear() : void 
{
    asm 
    {
        sub rsp, 10
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
        inc rax
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        mov r14, rax
        mov r15, rsp
        mov r13, 10
        invoke WriteConsoleA, r14, r15, r13d, 0, 0
        add rsp, 10
    }
}
fn enableVT() : void 
{
    let hOut = 0;
    let mode = 0;
    asm 
    {
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        mov qword[rbp - 8], rax
        lea r14, [rbp - 16]
        invoke GetConsoleMode, qword[rbp - 8], r14
        or qword[rbp - 16], 4
        mov r14, [rbp - 16]
        mov r15, [rbp - 8]
        invoke SetConsoleMode, r15, r14 
    }
}
