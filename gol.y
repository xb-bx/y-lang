include "std/gc.y";
include "std/utils.y";
include "std/StringBuilder.y";
struct Size 
{
    x: u64;
    y: u64;
}
struct Game 
{
    current: *u8;
    next: *u8;
    size: Size;
    constructor(size: Size)
    {
        this.size = size;
        this.current = new_array(size.x * size.y, typeof(u8)).data;
        this.next = new_array(size.x * size.y, typeof(u8)).data;
    }
    fn fill()
    {
        let y:u64 = 0;
        while y < this.size.y 
        {
            let x:u64 = 0;
            while x < this.size.x 
            {
                if nextrnd() % 2 == 1 
                {
                    this.current[y * this.size.x + x] = 1;
                }
                else 
                {
                    this.current[y * this.size.x + x] = 0;
                }
                x = x + 1;
            }
            y = y + 1;
        }
    }
    fn draw()
    {
        let builder = new StringBuilder();
        let y: u64 = 0;
        while y < this.size.y 
        {
            let x: u64 = 0;
            while x < this.size.x
            {
                if this.get(x, y) == 1 builder.append('#');
                else builder.append('.');
                x = x + 1;
            }
            builder.endl();
            y = y + 1;
        }
        builder.write();
    }
    fn step()
    {
        let y: u64 = 0;
        while y < this.size.y 
        {
            let x: u64 = 0;
            while x < this.size.x
            {
                let ngs = this.count_neighbours(x, y);
                if this.get(x, y) == 0 
                {
                    if ngs == 3 this.next[y * this.size.x + x] = 1;
                    else this.next[y * this.size.x + x] = 0;
                }
                else 
                {
                    if ngs == 2 || ngs == 3 this.next[y * this.size.x + x] = 1;
                    else this.next[y * this.size.x + x] = 0;
                }
                x = x + 1;
            }
            y = y + 1;
        }
        let temp = this.current;
        this.current = this.next;
        this.next = temp;
    }
    fn count_neighbours(x: u64, y: u64): u64 
    {
        let res: u64 = 0;
        if this.get(x+1, y) == 1 res = res + 1;
        if this.get(x-1, y) == 1 res = res + 1;
        if this.get(x+1, y+1) == 1 res = res + 1;
        if this.get(x-1, y-1) == 1 res = res + 1;
        if this.get(x, y+1) == 1 res = res + 1;
        if this.get(x, y-1) == 1 res = res + 1;
        if this.get(x+1, y-1) == 1 res = res + 1;
        if this.get(x-1, y+1) == 1 res = res + 1;
        ret res;
    }
    fn get(x: u64, y: u64): u8 
    {
        if x < 0 x = this.size.x - x;
        else if x >= this.size.x x = x - this.size.x;
        if y < 0 y = this.size.y - y;
        else if y >= this.size.y y = y - this.size.y;
        
        ret this.current[y * this.size.x + x];
    }
}
let seed = 0;
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
fn main()
{
    set_start();
    seed = GetTickCount();
    let size = new Size();
    size.x = 30;
    size.y = 20;
    let game = new Game(size);
    game.fill();
    game.draw();
    while true 
    {
        read();
        game.step();
        clear();
        game.draw();
    }
}
fn read(): char 
{
    
    let buff: *char = null;
    asm 
    {
        sub rsp, 16
        mov [rbp - 8], rsp
    }
    let readed: u32 = 0;
    ReadConsoleA(GetStdHandle(-10), buff, 1, &a, null);
    ret buff[0];
}
