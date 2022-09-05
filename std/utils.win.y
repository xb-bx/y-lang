dllimport "kernel32.dll" 
{
    extern fn WriteConsoleA(handle: *void, str: *char, len: u32, written: *u32);
    extern fn GetStdHandle(num: i32): *void;
    extern fn Sleep(ms: u32);
    extern fn exit(code: i32) from "ExitProcess";
    extern fn GetConsoleMode(handle: *void, mode: *u32);
    extern fn SetConsoleMode(handle: *void, mode: u32);
    extern fn GetProcessHeap(): *void;
    extern fn HeapAlloc(heap: *void, flags: u32, size: u64): *u8;
    extern fn HeapFree(heap: *void, flags: u32, ptr: *u8);
    extern fn GetTickCount(): i32;
    extern fn ReadConsoleA(handle: *void, buffer: *u8, count: u32, read: *u32, inputControl: *void);
}
fn exit() { exit(0); }
fn print(x: char)
{
    let buff: *char = null;
    let b = &buff;
    asm 
    {
        sub rsp, 8
        mov [rbp - 8], rsp
    }
    buff[0] = x;
    WriteConsoleA(GetStdHandle(-11), buff, 1, null);
}
fn print(str: *char, len: u64)
{
    WriteConsoleA(GetStdHandle(-11), str, len, null);
}
fn clear()  
{
    let buff: *char = null;
    let b = &buff;
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
        mov [rbp - 8], rsp
    }
    print(buff, 10);
}
fn enableVT()  
{
    let hOut = 0;
    let mode: u32 = 0;
    let handle = GetStdHandle(-11);
    GetConsoleMode(handle, &mode);
    mode = mode | 4;
    SetConsoleMode(handle, mode);
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
    ret HeapAlloc(GetProcessHeap(), 8, size);
}
fn free(handle: *u8)
{
    HeapFree(GetProcessHeap(), 0, handle);
}
fn cmdargs(argc: *i32): **char 
{
    
    ret null;
}
