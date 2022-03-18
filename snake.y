include "std/utils.y";

struct Point 
{
    x: i32;
    y: i32;
}
let map: *char = null;
let snake: *Point = null;
let snakelen: u64 = 0;
let sizex = 28;
let sizey = 22;
let dir = new Point();
let seed = 0;
let fruit = new Point();
let sleeptime = 40;


fn main() 
{
    dir.x = 1;
    dir.y = 0;
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
    initsnake();
    placefruit();
    while true 
    {
        clear();
        input();
        movesnake();
        fillmap();
        drawmap();
        sleep(sleeptime);
    }
}
fn placefruit() 
{
    let x = nextrnd() % sizex;
    let y = nextrnd() % sizey;
    if x <= 0 
        x = 1;
    else if x >= (sizex - 1)
        x = sizex - 2;
    if y <= 0
        y = 1;
    else if y >= sizey - 1
        y = sizey - 2;
    fruit.x = x;
    fruit.y = y;
}
fn numtohex(num: u8): char 
{
    if num > 10 
        ret cast(num + 0x41 - 10, char);
    else 
        ret cast(num + 48, char);
}
fn movesnake() 
{
    let i = snakelen - 1;
    let head = new Point();
    head.x = snake[0].x + dir.x;
    head.y = snake[0].y + dir.y;
    
    if head.x == sizex - 1 head.x = 1;
    if head.x == 0 head.x = sizex - 2;
    if head.y == sizey- 1 head.y = 1;
    if head.y == 0 head.y = sizey - 2;
    
    if head.x == fruit.x && head.y == fruit.y
    {
        let ind = snakelen;
        snake[ind] = snake[0];
        snakelen = snakelen + 1;
        placefruit();
    }
    
    while i > 0
    {
        snake[i] = snake[i - 1];
        i = i - 1;
    }

    i = 1;
    while i < snakelen 
    {
        let tail = snake[i];
        if tail.x == head.x && tail.y == head.y
        {
            writestr("Your score is: ");
            writenum(cast(snakelen - 2, i32));
            asm 
            {
                mov rsp, rbp
                pop rbp
                invoke ExitProcess, 0
            }
        }
        i = i + 1;
    }
    snake[0] = head;
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
fn nl() 
{
    writestr("\r\n");
}
fn initsnake() 
{
    snakelen = 2;
    snake[0].x = 2;
    snake[0].y = 2;
    snake[1].x = 2;
    snake[1].y = 2;
}
fn input() 
{
    let left = 0x25;
    let right = 0x27;
    let up = 0x26;
    let down = 0x28;
    if get_key_state(left) != 0 && dir.x != 1
    {
        dir.x = -1;
        dir.y = 0;
    }
    else if get_key_state(right) != 0  && dir.x != -1
    {
        dir.x = 1;
        dir.y = 0;
    }
    else if get_key_state(up) != 0 && dir.y != 1 
    {
        dir.x = 0;
        dir.y = -1;
    } 
    else if get_key_state(down) != 0 && dir.y != -1
    {
        dir.x = 0;
        dir.y = 1;
    }

}
fn fillmap() 
{
    let y = 0;
    while y < sizey 
    {
        let x = 0;
        while x < sizex 
        {
            
            if x == 0 || x == sizex - 1 || y == 0 || y == sizey - 1 
            {
                map[cast(y * (sizex + 2) + x, u64)] = '#';
            }
            else 
            {
                map[cast(y * (sizex + 2) + x, u64)] = '.';
            }

            x = x + 1;
        }
        map[cast(y * (sizex + 2) + sizex, u64)] = 10;
        map[cast(y * (sizex + 2) + sizex + 1, u64)] = 13;
        y = y + 1;
    }
    map[cast(snake[0].y * (sizex + 2) + snake[0].x, u64)] = '0';
    let i:u64 = 1;
    while i < snakelen {
        let s = snake[i];
        map[cast(s.y * (sizex + 2) + s.x, u64)] = 'o';
        i = i + 1;
    }
    map[cast(fruit.y * (sizex + 2) + fruit.x, u64)] = 'A';

}
fn drawmap()  
{
    let len = (sizex + 2) * sizey;
    asm 
    {
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        invoke WriteConsoleA, rax, qword[map], dword[rbp - 24], 0 
    }
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
