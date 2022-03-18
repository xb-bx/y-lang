#if WINDOWS include "std/utils.win.y"; #endif
#if LINUX include "std/utils.linux.y"; #endif
fn writenum(x: i32)
{
    if x == 0
    {
        writechar('0');
        ret;
    }
    let buff: *char = null;
    asm 
    {
        sub rsp, 64
        mov [rbp - 16], rsp
    }
    if x < 0
    {
        writechar('-');
        x = -x;
    }
    let index: u64 = 0;
    while x > 0 
    {
        let rem = modulo(x, 10);
        buff[index] = cast(rem + 48, char);
        index = index + 1;
        x = x / 10;
    }
    while index > 0
    {
        index = index - 1;
        writechar(buff[index]);
    }
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
        let n = cast((str[i] - 48), i32);
        res = res + n * pow(10, x);
        i = i - 1;
        x = x + 1;
    }
    ret res;
}
fn strlen(str: *char): u32 
{
    let i: u64 = 0;
    while str[i] != 0
        i = i + 1;
    ret cast(i, u32);
}
