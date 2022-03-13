include "std/utils.y";
include "std/convert.y";

let map: *char = null;
let snake: *i32 = null;
let snakelen: u64 = 0;
let sizex = 28;
let sizey = 22;
let dirx = 1;
let diry = 0;
let seed = 0;
let fruitx = 0;
let fruity = 0;

fn main(): void 
{
    asm 
    {
        sub rsp, 2048
        mov qword[map], rsp; ptr to map
        sub rsp, 2048
        mov qword[snake], rsp; ptr to snake
    }
    asm 
    {
        invoke GetTickCount
        mov dword[seed], eax
    }
    initstdout();
    initsnake();
    enableVT();
    placefruit();
    while true 
    {
        clear();
        input();
        movesnake();
        fillmap();
        drawmap();
        sleep(55);
    }
}
fn placefruit() : void
{
    let x = modulo(nextrnd(), sizex);
    let y = modulo(nextrnd(), sizey);
    if x <= 0 
        x = 1;
    else if x >= sizex - 1
        x = sizex - 2;
    if y <= 0
        y = 1;
    else if y >= sizey - 1
        y = sizey - 2;
    fruitx = x;
    fruity = y;
}
fn numtohex(num: u8): char 
{
    if num > 10 
        ret char(num + 0x41 - 10);
    else 
        ret char(num + 48);
}
fn movesnake() : void
{
    let i = snakelen - 1;
    
    let hx = snake[0] + dirx;
    let hy = snake[1] + diry;
    
    if hx == sizex - 1 hx = 1;
    if hx == 0 hx = sizex - 2;
    if hy == sizey- 1 hy = 1;
    if hy == 0 hy = sizey - 2;
    
    if hx == fruitx && hy == fruity
    {
        let ind = snakelen * 2;
        snake[ind] = snake[0];
        snake[ind + 1] = snake[1];
        snakelen = snakelen + 1;
        placefruit();
    }
    
    while i > 0
    {
        let index = i * 2;
        snake[index] = snake[index - 2];
        snake[index + 1] = snake[index - 1];
        i = i - 1;
    }

    i = 1;
    while i < snakelen 
    {
        let sx = snake[i * 2];
        let sy = snake[i * 2 + 1];
        if sx == hx && sy == hy
        {
            writestr("Your score is: ");
            writenum(i32(snakelen - 2));
            asm 
            {
                mov rsp, rbp
                pop rbp
                invoke ExitProcess, 0
            }
        }
        i = i + 1;
    }
    snake[0] = hx;
    snake[1] = hy;
}
fn nextrnd() : i32
{
    let rnd = 0;
    asm 
    {
        mov edx, 0
        mov ecx, 1103515245
        mov eax, dword[seed]
        mul ecx 
        add eax, 12345
        mov dword[seed], eax
        mov edx, 0
        mov ecx, 65536 
        div ecx
        mov edx, 0
        mov ecx, 32768
        div ecx
        mov dword[rbp - 8], edx
    }
    ret rnd;
}
fn nl(): void 
{
    writechar(0xa);
    writechar(0xd);
}
fn initsnake(): void 
{
    snakelen = 2;
    snake[0] = 2;
    snake[1] = 2;
    snake[2] = 2;
    snake[4] = 2;
}
fn input(): void 
{
    let left = 0x25;
    let right = 0x27;
    let up = 0x26;
    let down = 0x28;
    if get_key_state(left) != 0 && dirx != 1
    {
        dirx = -1;
        diry = 0;
    }
    else if get_key_state(right) != 0  && dirx != -1
    {
        dirx = 1;
        diry = 0;
    }
    else if get_key_state(up) != 0 && diry != 1 
    {
        dirx = 0;
        diry = -1;
    } 
    else if get_key_state(down) != 0 && diry != -1
    {
        dirx = 0;
        diry = 1;
    }

}
fn fillmap() : void
{
    let y = 0;
    while y < sizey 
    {
        let x = 0;
        while x < sizex 
        {
            
            if x == 0 || x == sizex - 1 || y == 0 || y == sizey - 1 
            {
                map[u64(y * (sizex + 2) + x)] = '#';
            }
            else 
            {
                map[u64(y * (sizex + 2) + x)] = '.';
            }

            x = x + 1;
        }
        map[u64(y * (sizex + 2) + sizex)] = 10;
        map[u64(y * (sizex + 2) + sizex + 1)] = 13;
        y = y + 1;
    }
    map[u64(snake[1] * (sizex + 2) + snake[0])] = '0';
    let i:u64 = 1;
    while i < snakelen {
        let sx = snake[i * 2];
        let sy = snake[i * 2 + 1];
        map[u64(sy * (sizex + 2) + sx)] = 'o';
        i = i + 1;
    }
    map[u64(fruity * (sizex + 2) + fruitx)] = 'A';

}
fn drawmap() : void 
{
    let len = (sizex + 2) * sizey;
    asm 
    {
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        invoke WriteConsoleA, rax, qword[map], dword[rbp - 24], 0 
    }
}
fn i32tou8(i: i32) : u8 
{
    let res: u8 = 0;
    asm {
        mov al, byte[rbp + 16]
        mov byte[rbp - 8], al;
    }
    ret res;
}
fn get_key_state(key: i32): i32 
{  
    let res = 0;
    asm 
    {
        invoke GetAsyncKeyState, dword[rbp + 16]
        mov dword[rbp - 8], eax
    }
    ret res;
}
