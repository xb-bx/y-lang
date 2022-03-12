fn main(): void 
{
    let map: *u8 = null;
    let snake: *i32 = null;
    let snakelen: i32 = 0;
    let sizex = 28;
    let sizey = 22;
    let dirx = 1;
    let diry = 0;
    let seed = 0;
    let fruitx = 0;
    let fruity = 0;
    let len = sizex * sizey;
    asm 
    {
        sub rsp, 2048
        mov qword[rbp - 8], rsp; ptr to map
        sub rsp, 2048
        mov qword[rbp - 16], rsp; ptr to snake
    }
    asm 
    {
        invoke GetTickCount
        mov dword[rbp - 80], eax
    }
    initsnake(snake, &snakelen);
    enableVT();
    placefruit(&fruitx, &fruity, &seed, sizex, sizey);
    while true 
    {
        clear();
        input(&dirx, &diry);
        movesnake(snake, &snakelen, dirx, diry, sizex, sizey, &seed, &fruitx, &fruity);
        fillmap(map, sizex, sizey, snake, snakelen, fruitx, fruity);
        drawmap(map, sizex, sizey);
        writechar(numtohex(i32tou8(fruitx)));
        writechar(numtohex(i32tou8(fruity)));
        sleep(55);
    }
}
fn placefruit(fruitx: *i32, fruity: *i32, seed: *i32, sizex: i32, sizey: i32) : void
{
    let x = modulo(nextrnd(seed), sizex);
    let y = modulo(nextrnd(seed), sizey);
    if x <= 0 
        x = 1;
    else if x >= sizex - 1
        x = sizex - 2;
    if y <= 0
        y = 1;
    else if y >= sizey - 1
        y = sizey - 2;
    *fruitx = x;
    *fruity = y;
}
fn numtohex(num: u8): u8 
{
    let res = 0;
    if num > 10 
        ret num + 0x41 - 10;
    else 
        ret num + 48;
}
fn movesnake(snake: *i32, snakelen: *i32, dirx: i32, diry:i32, sizex: i32, sizey: i32, seed: *i32, fruitx: *i32, fruity: *i32) : void
{
    let i = *snakelen - 1;
    
    let hx = *snake + dirx;
    let hy = *(snake + 1) + diry;
    
    if hx == sizex - 1 hx = 1;
    if hx == 0 hx = sizex - 2;
    if hy == sizey- 1 hy = 1;
    if hy == 0 hy = sizey - 2;
    
    if hx == *fruitx && hy == *fruity
    {
        let ind = *snakelen * 2;
        *(snake + ind) = *snake;
        *(snake + (ind + 1)) = *(snake + 1);
        *snakelen = *snakelen + 1;
        placefruit(fruitx, fruity, seed, sizex, sizey);
    }
    
    while i > 0
    {
        let index = i * 2;
        *(snake + index) = *(snake + (index - 2));
        *(snake + (index + 1)) = *(snake + (index - 1));
        i = i - 1;
    }

    i = 1;
    while i < *snakelen 
    {
        let sx = *(snake + i * 2);
        let sy = *(snake + i * 2 + 1);
        if sx == hx && sy == hy
        {
            asm 
            {
                mov rsp, rbp
                pop rbp
                invoke ExitProcess, 0
            }
        }
        i = i + 1;
    }
    *snake = hx;
    *(snake + 1) = hy;
}
fn nextrnd(seed: *i32) : i32
{
    let rnd = 0;
    asm 
    {
        mov edx, 0
        mov ecx, 1103515245
        mov eax, [rbp + 16]
        mov eax, [eax]
        mul ecx 
        add eax, 12345
        mov rdx, [rbp + 16]
        mov dword[rdx], eax
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
    let b: u8 = 0xa;
    writechar(b);
    b = 0xd;
    writechar(b);
}
fn initsnake(snake: *i32, snakelen: *i32): void 
{
    *snakelen = 2;
    *snake = 5;
    *(snake + 1) = 1;
    *(snake + 2) = 5;
    *(snake + 3) = 1;
}
fn input(dirx: *i32, diry: *i32): void 
{
    let left = 0x25;
    let right = 0x27;
    let up = 0x26;
    let down = 0x28;
    if get_key_state(left) != 0 && *dirx != 1
    {
        *dirx = -1;
        *diry = 0;
    }
    else if get_key_state(right) != 0  && *dirx != -1
    {
        *dirx = 1;
        *diry = 0;
    }
    else if get_key_state(up) != 0 && *diry != 1 
    {
        *dirx = 0;
        *diry = -1;
    }
    else if get_key_state(down) != 0 && *diry != -1
    {
        *dirx = 0;
        *diry = 1;
    }

}
fn fillmap(map: *u8, sizex: i32, sizey: i32, snake: *i32, snakelen: i32, fruitx: i32, fruity: i32) : void
{
    let y = 0;
    while y < sizey 
    {
        let x = 0;
        while x < sizex 
        {
            
            if x == 0 || x == sizex - 1 || y == 0 || y == sizey - 1 
            {
                *(map + ((y * (sizex + 2)) + x)) = 35;
            }
            else 
            {
                *(map + ((y * (sizex + 2)) + x)) = 46;
            }

            x = x + 1;
        }
        *(map + ((y * (sizex + 2)) + (sizex))) = 10;
        *(map + ((y * (sizex + 2)) + (sizex + 1))) = 13;
        y = y + 1;
    }
    *(map + (((*(snake + 1)) * (sizex + 2)) + (*snake))) = 48;
    let i = 1;
    while i < snakelen {
        let sx = *(snake + i * 2);
        let sy = *(snake + i * 2 + 1);
        *(map + ((sy * (sizex + 2)) + sx)) = 111;
        i = i + 1;
    }
    *(map + (fruity * (sizex + 2)) + fruitx) = 0x41;

}
fn drawmap(map: *u8, sizex: i32, sizey: i32) : void 
{
    let len = (sizex + 2) * sizey;
    asm 
    {
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        invoke WriteConsoleA, rax, qword[rbp + 16], dword[rbp - 24], 0 
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
fn sleep(ms: i32) : void 
{
    asm 
    {
        invoke Sleep, dword[rbp + 16]
    }
}
fn modulo(x: i32, y:i32) : i32 
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
fn writeascii(x:i32, count: i32) : void 
{
    asm 
    {
        sub rsp, 8
        mov rax, qword[rbp + 16]
        mov qword[rsp], rax
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        mov r14, rax
        mov r15, rsp
        mov r13, qword[rbp + 24]
        invoke WriteConsoleA, r14, r15, r13d, 0
        add rsp, 8
    }
}
fn writechar(x: u8) : void 
{
    asm 
    {
        sub rsp, 1
        mov al, byte[rbp + 16]
        mov byte[rsp], al
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        mov r14, rax
        mov r15, rsp
        mov r13, 1
        invoke WriteConsoleA, r14, r15, r13d, 0
        add rsp, 1
    }
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
