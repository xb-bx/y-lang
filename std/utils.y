#if WINDOWS include "std/utils.win.y"; #endif
#if LINUX include "std/utils.linux.y"; #endif
fn streq(str1: *char, str2: *char): bool 
{
    let len1 = cast(strlen(str1), u64);
    let len2 = cast(strlen(str2), u64);
    if len1 != len2 ret false;
    let i: u64 = 0;
    while i < len1 
    {
        if str1[i] != str2[i] ret false;
        i = i + 1;
    }
    ret true;
}
fn print(str: *char)  
{
    let len = strlen(str);
    print(str, len);
}
fn print(x: u64)
{
    if x == 0
    {
        print("0", 1);
        ret;
    }
    let buff: *char = stackalloc char[64];
    let secondbuff: *char = stackalloc char[64];
    let i: u64 = 0;
    let index: u64 = 0;
    while x > 0 
    {
        let rem = x % 10;
        buff[index] = cast(rem + 48, char);
        index = index + 1;
        x = x / 10;
    }
    let len = index;
    index = index - 1;
    while index >= 0
    {
        secondbuff[i] = buff[index];
        index = index - 1;
        i = i + 1;
    }
    print(secondbuff, len);
}
fn print(x: i64)
{
    if x == 0
    {
        print("0", 1);
        ret;
    }
    let buff: *char = stackalloc char[64];
    let secondbuff: *char = stackalloc char[64];
    let i: u64 = 0;
    let index: u64 = 0;
    let wasneg = false;
    if x < 0
    {
        x = -x;
        wasneg = true;
    }
    while x > 0 
    {
        let rem = x % 10;
        buff[index] = cast(rem + 48, char);
        index = index + 1;
        x = x / 10;
    }
    if(wasneg)
    {
        buff[index] = '-';
        index = index + 1;
    }
    let len = index;
    index = index - 1;
    while index >= 0
    {
        secondbuff[i] = buff[index];
        index = index - 1;
        i = i + 1;
    }
    print(secondbuff, len);
}
fn print(x: i32)
{
    if x == 0
    {
        print("0", 1);
        ret;
    }
    asm {;START}
    let buff: *char = stackalloc char[32];
    let secondbuff: *char = stackalloc char[32];
    let i: u64 = 0;
    let index: u64 = 0;
    let wasneg = false;
    if x < 0
    {
        x = -x;
        wasneg = true;
    }
    while x > 0 
    {
        let rem = x % 10;
        buff[index] = cast(rem + 48, char);
        index = index + 1;
        x = x / 10;
    }
    if(wasneg)
    {
        buff[index] = '-';
        index = index + 1;
    }
    let len = index;
    index = index - 1;
    while index >= 0
    {
        secondbuff[i] = buff[index];
        index = index - 1;
        i = i + 1;
    }
    print(secondbuff, len);
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
fn println(str: *char) 
{
    print(str);
    print("\r\n", 2);
}
fn println(val: i32) 
{
    print(val);
    print("\r\n", 2);
}
fn println(val: i64) 
{
    print(val);
    print("\r\n", 2);
}
fn println(val: u64) 
{
    print(val);
    print("\r\n", 2);
}
fn println(val: u32) 
{
    print(val);
    print("\r\n", 2);
}
fn strlen(str: *char): u32 
{
    let i: u64 = 0;
    while str[i] != 0
        i = i + 1;
    ret cast(i, u32);
}
