include "std/gc.y";
include "std/utils.y";
include "std/StringBuilder.y";
struct Game 
{
    current: *u8;
    next: *u8;
    constructor()
    {
        this.current = new_array(100);
        this.next = new_array(100);
        let i: u64 = 0;
        while i < 100 
        {
            this.current[i] = 0;
            this.next[i] = 0;
            i = i + 1;
        }
    }
    fn fill()
    {
        let y:u64 = 0;
        while y < 10 
        {
            let x:u64 = 0;
            while x < 10 
            {
                if nextrnd() % 2 == 1 
                {
                    this.current[y * 10 + x] = 1;
                }
                else 
                {
                    this.current[y * 10 + x] = 0;
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
        while y < 10 
        {
            let x: u64 = 0;
            while x < 10 
            {
                if this.get(x, y) == 1 builder.append('#');
                else builder.append(' ');
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
        while y < 10 
        {
            let x: u64 = 0;
            while x < 10 
            {
                let ngs = this.count_neighbours(x, y);
                if this.get(x, y) == 0 
                {
                    if ngs == 3 this.next[y * 10 + x] = 1;
                    else this.next[y * 10 + x] = 0;
                }
                else 
                {
                    if ngs == 2 || ngs == 3 this.next[y * 10 + x] = 1;
                    else this.next[y * 10 + x] = 0;
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
        if x >= 0 && x < 10 && y >= 0 && y < 10
            ret this.current[y * 10 + x];
        ret 0;
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
fn create_game(): *Game 
{
    let game = cast(new_obj(32), *Game);
    game.current = new_array(100);
    game.next = new_array(100);
    ret game;
}
fn main()
{
    set_start();
    asm 
    {
        invoke GetTickCount
        mov qword[seed], rax
    }
    let game = create_game();
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
fn u64(n: i32): u64 
{
    ret cast(n, u64);
}
fn read(): char 
{
    asm 
    {
        invoke GetStdHandle, STD_INPUT_HANDLE
        mov r13, rax
        sub rsp, 16
        mov r10, rsp
        mov r11, 1
        sub rsp, 16
        mov r12, rsp
        invoke ReadConsoleA, rax, r10, r11, r12, 0
        mov al, byte[rsp + 16]
        leave
        ret
    }
    ret 0;
}
