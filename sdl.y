include "std/utils.y";
let SDL_INIT_VIDEO: u32 = 0x00000020;
struct Window {}
struct Renderer {}
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

dllimport "SDL2.dll" 
{
    extern fn SDL_Init(flags: u32): i32;
    extern fn SDL_CreateWindow(title: *char, x: i32, y: i32, w: i32, h: i32, flags: u32): *Window;
    extern fn SDL_CreateRenderer(window: *Window, id: i32, flags: u32): *Renderer;
    extern fn SDL_GetError(): *char;
    extern fn SDL_WaitEvent(event: *Event): i32;
    extern fn SDL_Quit();
}
fn ptr_as_num(ptr: *void): u64
{
    ret cast(ptr, u64);
}
fn main()
{
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
    while true 
    {
        SDL_WaitEvent(&event);
    }
    SDL_Quit();
}
