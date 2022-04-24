fn writechar(x: char)  
{
    asm 
    {
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        lea rbx, [rbp + 16]
        invoke WriteConsoleA, rax, rbx, 1, 0
    }
}
fn writestr(str: *char, len: u64)
{
    asm 
    {
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        mov rbx, [rbp + 16]
        mov r10d, dword[rbp + 24]
        invoke WriteConsoleA, rax, rbx, r10d, 0
    }
}
fn sleep(ms: i32)  
{
    asm 
    {
        invoke Sleep, dword[rbp + 16]
    }
}
fn exit() 
{
    asm { invoke ExitProcess, 0 } 
}
fn clear()  
{
    enableVT();
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
        inc rax
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        mov r14, rax
        mov r15, rsp
        mov r13, 10
        invoke WriteConsoleA, r14, r15, r13d, 0, 0
        add rsp, 10
    }
}
fn enableVT()  
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
fn commandline(): *char 
{
    asm 
    {
        invoke GetCommandLineA
        mov rsp, rbp
        pop rbp
        ret
    }
    ret null;
}
fn alloc(size: u64): *u8
{
    asm 
    {
        invoke GetProcessHeap
        invoke HeapAlloc, rax, 8, qword[rbp + 16]
        mov rsp, rbp
        pop rbp
        ret
    }
    ret null;
}
fn free(handle: *u8)
{
    asm 
    {
        invoke GetProcessHeap
        invoke HeapFree, rax, 0, qword[rbp + 16]
    }
    ret;
}
fn cmdargs(argc: *i32): **char 
{
    
    ret null;
}
