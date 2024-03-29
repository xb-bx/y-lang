dllimport "user32.dll" 
{
    extern fn get_key_state(key: i32): i32 from "GetAsyncKeyState";
}
struct Point 
{
    x: i32;
    y: i32;
    constructor(x: i32, y: i32) 
    {
        this.x = x;
        this.y = y;
    }
    fn equals(other: Point): bool 
    {
        ret this.x == other.x && this.y == other.y;
    }
}
let map: *char = null;
let snake: *Point = null;
let snakelen: u64 = 0;
let sizex = 50;
let sizey = 25;
let dir = new Point(0, 0);
let seed = 0;
let fruit = new Point(0, 0);
let sleeptime = 35;

fn setcursor()
{
    let buff: *char = null;
    asm 
    {
        sub rsp, 8
        mov byte[rsp], 27
        mov byte[rsp + 1], '['
        mov byte[rsp + 2], '1'
        mov byte[rsp + 3], ';'
        mov byte[rsp + 4], '1'
        mov byte[rsp + 5], 'H'
        mov [rbp - 8], rsp
    }
    print(buff, 6);
}

fn main() 
{
    dir.x = 1;
    dir.y = 0;
    map = cast(alloc(2048), *char);
    snake = cast(alloc(2048), *Point);
    seed = GetTickCount();
    let len = sizey * (sizex + 2); 
    initsnake();
    placefruit();
    clear();
    while true 
    {
        setcursor();
        input();
        movesnake();
        fillmap();
        print(map, len);
        Sleep(sleeptime);
    }
}
fn placefruit() 
{
    let x = nextrnd() % sizex;
    let y = nextrnd() % sizey;
    if x <= 0 
        x = 1;
    else if x >= sizex - 1
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
    let head = new Point(snake[0].x + dir.x, snake[0].y + dir.y);
    
    if head.x == sizex - 1 head.x = 1;
    if head.x == 0 head.x = sizex - 2;
    if head.y == sizey- 1 head.y = 1;
    if head.y == 0 head.y = sizey - 2;
    
    if head.equals(fruit)
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
        if snake[i].equals(head)
        {
            clear();
            print("Your score is: ", 15);
            print(cast(snakelen - 2, i32));
            asm 
            {
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
    print("\r\n", 2);
}
fn initsnake() 
{
    snakelen = 2;
    snake[0] = new Point(2, 2);
    snake[1] = new Point(2, 2);
}
enum Key 
{
    Left = 0x25,
    Up,
    Right,
    Down
}
fn input() 
{
    if get_key_state(Key.Left) != 0 && dir.x != 1
    {
        dir.x = -1;
        dir.y = 0;
    }
    else if get_key_state(Key.Right) != 0  && dir.x != -1
    {
        dir.x = 1;
        dir.y = 0;
    }
    else if get_key_state(Key.Up) != 0 && dir.y != 1 
    {
        dir.x = 0;
        dir.y = -1;
    } 
    else if get_key_state(Key.Down) != 0 && dir.y != -1
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
