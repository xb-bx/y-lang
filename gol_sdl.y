include "std/utils.y";
struct Size 
{
    x: u64;
    y: u64;
}
let CELL_SIZE: i32 = 10;
struct Game 
{
    current: *u8;
    next: *u8;
    size: Size;
    renderer: *Renderer;
    constructor(size: Size, renderer: *Renderer)
    {
        this.size = size;
        this.current = alloc(size.x * size.y);
        this.next = alloc(size.x * size.y);
        this.renderer = renderer;
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
        this.renderer.set_color(white);
        let y: i32 = 0;
        while y < cast(this.size.y, i32) 
        {
            let x: i32 = 0;
            while x < cast(this.size.x, i32)
            {
                if this.get(cast(x, u64), cast(y, u64)) == 1
                    this.renderer.fill_rect(new Rect(x * CELL_SIZE, y * CELL_SIZE, CELL_SIZE, CELL_SIZE));
                x = x + 1;
            }
            y = y + 1;
        }
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
let SDL_INIT_VIDEO: u32 = 0x00000020;
let black = new RGBA(0,0,0,255);
let white = new RGBA(255, 255, 255, 255);
struct Window {}
struct Renderer 
{
    fn set_color(color: RGBA)
    {
        SDL_SetRenderDrawColor(this, color.r, color.g, color.b, color.a);
    }
    fn clear() 
    {
        SDL_RenderClear(this);
    }
    fn render_present()
    {
        SDL_RenderPresent(this);
    }
    fn fill_rect(rect: Rect)
    {
        SDL_RenderFillRect(this, &rect); 
    }
}
struct Event 
{
    type: i32;
    empty_shit: i64;
    empty_shit1: i64;
    empty_shit2: i64;
    empty_shit3: i64;
    empty_shit4: i64;
    empty_shit5: i64;
    empty_shit6: i64;
}
struct RGBA 
{
    r: u8;
    g: u8;
    b: u8;
    a: u8;
    constructor(r: u8, g: u8, b: u8, a: u8)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

}
struct Rect
{
    x: i32;
    y: i32;
    w: i32;
    h: i32;
    constructor(x: i32, y: i32, w: i32, h: i32)
    {
        this.x = x;
        this.y = y;
        this.w = w;
        this.h = h;
    }

}
dllimport "SDL2.dll" 
{
    extern fn SDL_Init(flags: u32): i32;
    extern fn SDL_CreateWindow(title: *char, x: i32, y: i32, w: i32, h: i32, flags: u32): *Window;
    extern fn SDL_CreateRenderer(window: *Window, id: i32, flags: u32): *Renderer;
    extern fn SDL_GetError(): *char;
    extern fn SDL_WaitEvent(event: *Event): i32;
    extern fn SDL_PollEvent(event: *Event): i32;
    extern fn SDL_SetRenderDrawColor(renderer: *Renderer, r: u8, g: u8, b: u8, a: u8): i32;
    extern fn SDL_RenderClear(renderer: *Renderer): i32;
    extern fn SDL_RenderPresent(renderer: *Renderer);
    extern fn SDL_RenderFillRect(renderer: *Renderer, rect: *Rect);
    extern fn SDL_Delay(ms: i32); 
    extern fn SDL_Quit();
}
fn main()
{
    seed = 10;
    let event = new Event();
    if SDL_Init(SDL_INIT_VIDEO) < 0 
    {
        writestr(SDL_GetError());
        ret;
    }
    let window = SDL_CreateWindow("w", 100, 100, 640, 480, 0);
    if window == null
    {
        writestr(SDL_GetError());
        ret;
    }
    let renderer = SDL_CreateRenderer(window, -1, 0);
    let size = new Size();
    size.x = 64;
    size.y = 48;
    let game = new Game(size, renderer);
    game.fill();
    while true 
    {
        SDL_PollEvent(&event);
        game.step();
        renderer.set_color(black);
        renderer.clear();
        game.draw();
        renderer.render_present();
        SDL_Delay(33);
    }
    SDL_Quit();
}
