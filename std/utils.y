include "std/convert.y";
let stdout = null;

fn writechar(x: char) : void 
{
    asm 
    {
        lea rbx, [rbp + 16]
        invoke WriteConsoleA, [stdout], rbx, 1, 0
    }
}
fn strlen(str: *char): u32 
{
    let i: u32 = 0;
    while *(str + i) != 0
        i = i + 1;
    ret i;
}
fn writestr(str: *char) : void 
{
    let len = strlen(str);
    asm 
    {
        mov rbx, [rbp + 16]
        mov r10d, dword[rbp - 8]
        invoke WriteConsoleA, [stdout], rbx, r10d, 0
    }
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
fn writenum(x: i32): void
{
    let buff: *char = null;
    asm 
    {
        sub rsp, 32
        mov [rbp - 8], rsp
    }
    let index: u64 = 0;
    while x > 0 
    {
        let rem = modulo(x, 10);
        buff[index] = char(rem + 48);
        index = index + 1;
        x = x / 10;
    }
    while index > 0
    {
        index = index - 1;
        writechar(buff[index]);
    }
}
fn modulo(x:u64, y:u64): u64 
{
    ret u64(modulo(i32(x), i32(y)));
} 
fn modulo(x: i32, y: i32) : i32 
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
fn pow(x: i32, pow:i32): i32
{
    if pow == 0 
        ret 1;
    else if pow == 1
        ret x;
    let res = x;
    while pow > 1 
    {
        res = res * x;
        pow = pow - 1;
    }
    ret res;
}
fn parsei32(str: *char): i32
{
    let len = (strlen(str));
    let i = len - 1;
    let x = 0;
    let res = 0;
    while i >= 0
    {
        let n = i32(u8(str[i] - 48));
        res = res + n * pow(10, x);
        i = i - 1;
        x = x + 1;
    }
    ret res;
}
